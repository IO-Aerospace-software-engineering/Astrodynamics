// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Provides the high-level SSA workflow for screening, conjunction analysis, and simple avoidance trade studies.
/// </summary>
/// <remarks>
/// <para>
/// For most end-user scenarios, use <see cref="Analyze(ProtectedSpacecraftProfile, ILocalizable, Window, ConjunctionAnalysisOptions)"/>
/// to assess one protected-vs-secondary pair and use <see cref="Screen"/> to rank many candidates.
/// </para>
/// <para>
/// Use the <c>PropagationSolution</c> overloads when both trajectories have already been propagated and you want SSA to
/// reuse that dense trajectory information instead of sampling the participants directly.
/// </para>
/// </remarks>
public static class ConjunctionAssessment
{
    // The NAIF ids of the trial spacecraft of the avoidance options, cycling over [-2 099 999, -2 000 000] (#676): inside
    // the range whose frame id NaifId × 1000 is an int, where -9 000 001 and below wrapped around before. Nothing keys on
    // them: a trial reads no kernel, is listed by its observer by instance, and passes through no NAIF-id filter
    private const int TrialIdFirst = -2_000_000;
    private const int TrialIdCount = 100_000;
    private static int _trialIdCounter = -1;

    private sealed class StateSamplingSource
    {
        public StateSamplingSource(
            Func<Time, StateVector> getState,
            IReadOnlyList<Time> searchKnots,
            DenseTrajectorySource denseTrajectory = null,
            PropagationSolution solution = null)
        {
            GetState = getState;
            SearchKnots = searchKnots;
            DenseTrajectory = denseTrajectory;
            Solution = solution;
        }

        public Func<Time, StateVector> GetState { get; }

        // The propagation of the participant, from which its covariance is propagated to the encounter (E2); null when
        // the library does not propagate it
        public PropagationSolution Solution { get; }

        public IReadOnlyList<Time> SearchKnots { get; }

        public DenseTrajectorySource DenseTrajectory { get; }
    }

    private sealed class DenseTrajectorySource
    {
        public DenseTrajectorySource(ILocalizable observer, Frame frame, IReadOnlyList<DenseTrajectoryStep> steps)
        {
            Observer = observer;
            Frame = frame;
            Steps = steps;
        }

        public ILocalizable Observer { get; }

        public Frame Frame { get; }

        public IReadOnlyList<DenseTrajectoryStep> Steps { get; }
    }

    private sealed class DenseTrajectoryStep
    {
        public DenseTrajectoryStep(Time segmentBaseEpoch, AcceptedStep step, Time startEpoch, Time endEpoch,
            PropagationTimeArgument timeArgument)
        {
            TimeArgument = timeArgument;
            SegmentBaseEpoch = segmentBaseEpoch;
            Step = step;
            StartEpoch = startEpoch;
            EndEpoch = endEpoch;
        }

        // The time argument of the trajectory, which maps an epoch to the seconds of the step (#501)
        public PropagationTimeArgument TimeArgument { get; }

        public Time SegmentBaseEpoch { get; }

        public AcceptedStep Step { get; }

        public Time StartEpoch { get; }

        public Time EndEpoch { get; }
    }

    private sealed class VectorPolynomial
    {
        public VectorPolynomial(double[] x, double[] y, double[] z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double[] X { get; }

        public double[] Y { get; }

        public double[] Z { get; }
    }

    /// <summary>
    /// Screens a set of candidate objects against one protected spacecraft and returns the highest-priority encounters.
    /// </summary>
    /// <param name="protectedAsset">Protected spacecraft profile, including maneuver constraints.</param>
    /// <param name="candidates">Candidate secondary objects to analyze.</param>
    /// <param name="screeningWindow">Time window over which conjunctions are searched.</param>
    /// <param name="options">Screening configuration. Defaults are used when <see langword="null"/>.</param>
    /// <returns>
    /// Ranked encounter cases ordered by decreasing collision probability and then by miss distance.
    /// </returns>
    public static IReadOnlyList<EncounterCase> Screen(
        ProtectedSpacecraftProfile protectedAsset,
        IEnumerable<ILocalizable> candidates,
        Window screeningWindow,
        ScreeningOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(protectedAsset);
        ArgumentNullException.ThrowIfNull(candidates);

        options ??= new ScreeningOptions();

        var rankedCases = new List<EncounterCase>();
        foreach (var candidate in candidates.Where(candidate => candidate != null && candidate.NaifId != protectedAsset.Spacecraft.NaifId))
        {
            var encounter = Analyze(
                protectedAsset,
                candidate,
                screeningWindow,
                new ConjunctionAnalysisOptions
                {
                    SampleStep = options.SampleStep,
                    MaximumEventSearchStep = options.MaximumEventSearchStep,
                    SecondaryHardBodyRadiusMeters = options.DefaultSecondaryHardBodyRadiusMeters,
                    LowRelativeSpeedThresholdMetersPerSecond = options.LowRelativeSpeedThresholdMetersPerSecond,

                    // The screening ranks many candidates and never propagates the covariance (E2)
                    CovariancePropagation = null
                });

            if (encounter.EncounterState.MissDistanceMeters <= options.MaxMissDistanceMeters)
            {
                rankedCases.Add(encounter);
            }
        }

        return rankedCases
            .OrderByDescending(CaseProbabilitySortKey)
            .ThenBy(c => c.EncounterState.MissDistanceMeters)
            .Take(options.MaxResults)
            .ToArray();
    }

    /// <summary>
    /// Analyzes one protected-vs-secondary pair and returns the single best conjunction found in the window.
    /// </summary>
    /// <param name="protectedAsset">Protected spacecraft profile, including maneuver constraints.</param>
    /// <param name="secondaryObject">Secondary object to analyze against the protected asset.</param>
    /// <param name="screeningWindow">Time window over which close approaches are searched.</param>
    /// <param name="options">Analysis configuration. Defaults are used when <see langword="null"/>.</param>
    /// <returns>
    /// The highest-priority encounter within the window. If no range-rate zero crossing is found, a midpoint fallback is used.
    /// </returns>
    /// <exception cref="ArgumentException">The window leaves the propagated states of the protected spacecraft or of a
    /// propagated secondary spacecraft, which would be extrapolated (#527).</exception>
    public static EncounterCase Analyze(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        ConjunctionAnalysisOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(protectedAsset);
        ArgumentNullException.ThrowIfNull(secondaryObject);

        options ??= new ConjunctionAnalysisOptions();
        ValidateAnalysisOptions(options);

        CheckStatesCover(protectedAsset.Spacecraft, secondaryObject, screeningWindow);
        var observer = protectedAsset.Spacecraft.InitialOrbitalParameters.Observer;
        var protectedSource = CreateLocalizableStateSource(protectedAsset.Spacecraft, observer);
        var secondarySource = CreateLocalizableStateSource(secondaryObject, observer);
        return AnalyzeInternal(protectedAsset, secondaryObject, screeningWindow, options, protectedSource, secondarySource);
    }

    /// <summary>
    /// Analyzes one protected-vs-secondary pair using precomputed propagated trajectories for both participants.
    /// </summary>
    /// <param name="protectedAsset">Protected spacecraft profile, including maneuver constraints.</param>
    /// <param name="protectedTrajectory">Propagated trajectory of the protected spacecraft.</param>
    /// <param name="secondaryObject">Secondary object represented by <paramref name="secondaryTrajectory"/>.</param>
    /// <param name="secondaryTrajectory">Propagated trajectory of the secondary object.</param>
    /// <param name="screeningWindow">Time window over which close approaches are searched.</param>
    /// <param name="options">Analysis configuration. Defaults are used when <see langword="null"/>.</param>
    /// <returns>
    /// The highest-priority encounter within the window: the closest interior local minimum of the distance when the window
    /// holds one, a window end never preferred to it (#621); without an interior minimum, the end where the distance grows
    /// into the window when the trajectories share their observer and frame (an end where the range rate is exactly zero
    /// included), the midpoint otherwise. On that path a minimum lies inside the window when the range rate of the states
    /// says so, the cubic Hermite of the stored steps only seeding the search (#363): a minimum whose range-rate zero lies
    /// outside the window is never returned, however near an end, and a range rate negative at the start and positive at
    /// the end returns the minimum between them, however flat the distance over the window; a minimum of the Hermite that
    /// the range rate does not bracket, a maximum of the distance on a slow pass, is never returned in place of the end; the
    /// midpoint is never returned on that path.
    /// </returns>
    public static EncounterCase Analyze(
        ProtectedSpacecraftProfile protectedAsset,
        PropagationSolution protectedTrajectory,
        ILocalizable secondaryObject,
        PropagationSolution secondaryTrajectory,
        Window screeningWindow,
        ConjunctionAnalysisOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(protectedAsset);
        ArgumentNullException.ThrowIfNull(protectedTrajectory);
        ArgumentNullException.ThrowIfNull(secondaryObject);
        ArgumentNullException.ThrowIfNull(secondaryTrajectory);

        options ??= new ConjunctionAnalysisOptions();
        ValidateAnalysisOptions(options);

        var observer = protectedAsset.Spacecraft.InitialOrbitalParameters.Observer;
        ValidateTrajectoryCoverage(protectedTrajectory, screeningWindow, nameof(protectedTrajectory));
        ValidateTrajectoryCoverage(secondaryTrajectory, screeningWindow, nameof(secondaryTrajectory));

        var protectedSource = CreateTrajectoryStateSource(protectedAsset.Spacecraft, protectedTrajectory, observer, screeningWindow);
        var secondarySource = CreateTrajectoryStateSource(secondaryObject, secondaryTrajectory, observer, screeningWindow);
        return AnalyzeInternal(protectedAsset, secondaryObject, screeningWindow, options, protectedSource, secondarySource);
    }

    /// <summary>
    /// Analyzes one protected-vs-secondary pair and returns every close approach found in the window.
    /// </summary>
    /// <param name="protectedAsset">Protected spacecraft profile, including maneuver constraints.</param>
    /// <param name="secondaryObject">Secondary object to analyze against the protected asset.</param>
    /// <param name="screeningWindow">Time window over which close approaches are searched.</param>
    /// <param name="options">Analysis configuration. Defaults are used when <see langword="null"/>.</param>
    /// <returns>
    /// All detected encounters in the window. If no TCA is detected, a single midpoint fallback encounter is returned.
    /// </returns>
    /// <exception cref="ArgumentException">The window leaves the propagated states of the protected spacecraft or of a
    /// propagated secondary spacecraft, which would be extrapolated (#527).</exception>
    public static IReadOnlyList<EncounterCase> AnalyzeAll(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        ConjunctionAnalysisOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(protectedAsset);
        ArgumentNullException.ThrowIfNull(secondaryObject);

        options ??= new ConjunctionAnalysisOptions();
        ValidateAnalysisOptions(options);

        CheckStatesCover(protectedAsset.Spacecraft, secondaryObject, screeningWindow);
        var observer = protectedAsset.Spacecraft.InitialOrbitalParameters.Observer;
        var protectedSource = CreateLocalizableStateSource(protectedAsset.Spacecraft, observer);
        var secondarySource = CreateLocalizableStateSource(secondaryObject, observer);
        return AnalyzeAllInternal(protectedAsset, secondaryObject, screeningWindow, options, protectedSource, secondarySource);
    }

    /// <summary>
    /// Analyzes one protected-vs-secondary pair using precomputed propagated trajectories and returns every close approach found.
    /// </summary>
    /// <param name="protectedAsset">Protected spacecraft profile, including maneuver constraints.</param>
    /// <param name="protectedTrajectory">Propagated trajectory of the protected spacecraft.</param>
    /// <param name="secondaryObject">Secondary object represented by <paramref name="secondaryTrajectory"/>.</param>
    /// <param name="secondaryTrajectory">Propagated trajectory of the secondary object.</param>
    /// <param name="screeningWindow">Time window over which close approaches are searched.</param>
    /// <param name="options">Analysis configuration. Defaults are used when <see langword="null"/>.</param>
    /// <returns>
    /// All detected encounters in the window. When the trajectories share their observer and frame, the interior local
    /// minima of the distance only, an empty list when the window holds none (#621: a window end was listed before 11.0.0);
    /// otherwise, without a TCA, a single midpoint fallback encounter. On the first path a minimum lies inside the window
    /// when the range rate of the states says so, the cubic Hermite of the stored steps only seeding the search (#363): a
    /// minimum whose range-rate zero lies just inside a window end is listed, one whose zero lies just outside is not, nor
    /// one whose zero is exactly a window end (a window end is never listed), and a range rate negative at the start and
    /// positive at the end lists the minimum between them, however flat the distance over the window. Every encounter listed
    /// on that path is a zero of the range rate going from negative to positive, each minimum listed once: a minimum of the
    /// Hermite that the range rate does not bracket, a maximum of the distance on a slow pass, is not listed.
    /// </returns>
    public static IReadOnlyList<EncounterCase> AnalyzeAll(
        ProtectedSpacecraftProfile protectedAsset,
        PropagationSolution protectedTrajectory,
        ILocalizable secondaryObject,
        PropagationSolution secondaryTrajectory,
        Window screeningWindow,
        ConjunctionAnalysisOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(protectedAsset);
        ArgumentNullException.ThrowIfNull(protectedTrajectory);
        ArgumentNullException.ThrowIfNull(secondaryObject);
        ArgumentNullException.ThrowIfNull(secondaryTrajectory);

        options ??= new ConjunctionAnalysisOptions();
        ValidateAnalysisOptions(options);

        var observer = protectedAsset.Spacecraft.InitialOrbitalParameters.Observer;
        ValidateTrajectoryCoverage(protectedTrajectory, screeningWindow, nameof(protectedTrajectory));
        ValidateTrajectoryCoverage(secondaryTrajectory, screeningWindow, nameof(secondaryTrajectory));

        var protectedSource = CreateTrajectoryStateSource(protectedAsset.Spacecraft, protectedTrajectory, observer, screeningWindow);
        var secondarySource = CreateTrajectoryStateSource(secondaryObject, secondaryTrajectory, observer, screeningWindow);
        return AnalyzeAllInternal(protectedAsset, secondaryObject, screeningWindow, options, protectedSource, secondarySource);
    }

    /// <summary>
    /// Explores simple impulsive avoidance options for an existing encounter and returns the best-ranked candidates.
    /// </summary>
    /// <param name="encounterCase">Encounter to mitigate.</param>
    /// <param name="options">Avoidance search configuration. Defaults are used when <see langword="null"/>.</param>
    /// <returns>
    /// Candidate avoidance maneuvers ordered by internal ranking score, where lower values are better.
    /// </returns>
    public static IReadOnlyList<AvoidanceOption> EvaluateAvoidance(
        EncounterCase encounterCase,
        AvoidanceSearchOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(encounterCase);

        options ??= new AvoidanceSearchOptions();
        var constraints = encounterCase.ProtectedAsset.ManeuverConstraints;
        var observer = encounterCase.ProtectedAsset.Spacecraft.InitialOrbitalParameters.Observer;
        var burnDirections = BuildBurnDirections().ToArray();
        var avoidanceOptions = new List<AvoidanceOption>();
        var tca = encounterCase.EncounterState.Epoch;

        // With the covariance of the protected participant propagated, each option is propagated numerically from the
        // state and the covariance at its burn (E4); otherwise with the force model of the participant's propagation when
        // it has one, or on the two-body orbit of the state after the burn, the covariance held in RTN (#402)
        var propagated = PropagatedAvoidance.TryCreate(encounterCase, options, observer);
        var fallback = propagated == null ? new FallbackAvoidance(encounterCase, options, observer) : null;
        var trajectory = propagated != null || fallback.Propagated ? AvoidanceTrajectory.Propagated : AvoidanceTrajectory.TwoBody;

        foreach (var leadTime in options.LeadTimes.Where(lead => lead >= constraints.MinimumLeadTime && lead <= constraints.MaximumLeadTime))
        {
            var burnEpoch = tca - leadTime;
            if (!encounterCase.ScreeningWindow.Intersects(burnEpoch))
            {
                continue;
            }

            if (propagated != null ? !propagated.Covers(burnEpoch) : !fallback.Covers(burnEpoch))
            {
                continue;
            }

            var burnState = propagated?.BurnState(burnEpoch) ?? fallback.BurnState(burnEpoch);

            // The fuel from the mass at the burn, not the mass of the spacecraft after its whole propagation (#415)
            var massAtBurn = propagated?.Context(burnEpoch).TotalMass ?? fallback.MassAt(burnEpoch);

            foreach (var magnitude in options.DeltaVMagnitudesMetersPerSecond.Where(mag => mag > 0.0 && mag <= constraints.MaxDeltaVMetersPerSecond))
            {
                foreach (var direction in burnDirections)
                {
                    var deltaV = burnState.FromRtn(direction * magnitude);
                    var fuelCost = EstimateFuelCost(encounterCase.ProtectedAsset.Spacecraft, massAtBurn, magnitude);
                    var postEncounter = propagated != null
                        ? propagated.Analyze(burnState, deltaV, fuelCost)
                        : fallback.Analyze(burnState, deltaV, fuelCost);

                    var probability = postEncounter.CollisionRisk?.ProbabilityOfCollision ?? 1.0;
                    var rankingScore = (probability <= options.PcThreshold ? 0.0 : 1e9) + magnitude;

                    avoidanceOptions.Add(new AvoidanceOption(
                        burnEpoch,
                        deltaV,
                        fuelCost,
                        postEncounter,
                        rankingScore) { Trajectory = trajectory });
                }
            }
        }

        return avoidanceOptions
            .OrderBy(option => option.RankingScore)
            .Take(options.MaxReturnedOptions)
            .ToArray();
    }

    /// <summary>
    /// The avoidance options of an encounter whose protected covariance is not propagated (#402). Each option is
    /// propagated from the state after its burn with the force model of the propagation of the protected participant the
    /// analysis sampled (<see cref="EncounterCase.ProtectedTrajectory"/>, or the force model the spacecraft keeps from
    /// its last propagation, <see cref="Spacecraft.LastForceModel"/>): its forces, its integrator and step settings
    /// (RK7(8) or Velocity-Verlet), its orientation cache, under its lock, with the mass of the trajectory at the burn
    /// less the fuel of the option. Without such a propagation (not propagated, TLE, SPICE kernel), the option is the
    /// two-body orbit of the state after the burn, as before (<see cref="AvoidanceTrajectory.TwoBody"/>).
    /// </summary>
    /// <remarks>
    /// The covariance of the protected participant is the one the analysis resolves for it at the burn (given there by
    /// its ephemeris, or its initial covariance held in RTN), held in RTN from the burn to the TCA of the option: rotated
    /// into RTN with the state before the burn, back with the state of the option at its TCA. With the initial
    /// covariance, that is the initial covariance held in RTN up to the TCA of the option, the same rotation at the burn
    /// cancelling, and its age is counted from the initial state. The execution error of the burn is not added: a
    /// velocity covariance at the burn held in RTN never reaches the position covariance at the TCA. The secondary
    /// participant keeps the resolution of its covariance of the encounter, propagated with the same options when it
    /// was, for all the options.
    /// </remarks>
    private sealed class FallbackAvoidance
    {
        private readonly EncounterCase _encounterCase;
        private readonly AvoidanceSearchOptions _options;
        private readonly ILocalizable _observer;
        private readonly Spacecraft _spacecraft;
        private readonly PropagationSolution _trajectory;
        private readonly PropagationForceModel _forceModel;
        private readonly Time _end;
        private readonly StateSamplingSource _secondarySource;
        private readonly EncounterCovarianceResolver _secondaryResolver;

        internal FallbackAvoidance(EncounterCase encounterCase, AvoidanceSearchOptions options, ILocalizable observer)
        {
            _encounterCase = encounterCase;
            _options = options;
            _observer = observer;
            _spacecraft = encounterCase.ProtectedAsset.Spacecraft;
            _trajectory = encounterCase.ProtectedTrajectory;
            _forceModel = _trajectory != null ? _trajectory.ForceModel : _spacecraft.LastForceModel;

            // Within the window and the force model of the propagation, whose ephemeris and orientation caches cover it
            _end = encounterCase.ScreeningWindow.EndDate;
            if (_forceModel != null && _forceModel.End < _end)
            {
                _end = _forceModel.End;
            }

            _secondarySource = CreateLocalizableStateSource(encounterCase.SecondaryObject, observer);
            var secondary = encounterCase.SecondaryCovariance;
            var secondaryPropagation = secondary?.Source == CovarianceSource.Propagated ? secondary.Solution.CovariancePropagation : null;
            _secondaryResolver = new EncounterCovarianceResolver(encounterCase.SecondaryObject, _secondarySource.Solution,
                secondaryPropagation, _end);
        }

        /// <summary>Whether the options are propagated with a force model, rather than the two-body orbit.</summary>
        internal bool Propagated => _forceModel != null;

        // A burn the propagation covers, before the end of the options
        internal bool Covers(in Time burnEpoch) => _forceModel == null || (_forceModel.Covers(burnEpoch) && burnEpoch < _end);

        // The state at the burn: on the trajectory the analysis sampled, in the frame of its propagation and relative to its
        // central body; on the ephemeris of the spacecraft without one
        internal StateVector BurnState(in Time burnEpoch)
        {
            if (_forceModel == null)
                return GetState(_spacecraft, burnEpoch, _observer);

            if (_trajectory != null)
            {
                var (position, velocity) = _trajectory.InterpolateAt(burnEpoch);
                return new StateVector(position, velocity, _forceModel.Observer, burnEpoch, _forceModel.Frame);
            }

            return _spacecraft.GetEphemeris(burnEpoch, _forceModel.Observer, _forceModel.Frame, Aberration.None).ToStateVector();
        }

        // The mass of the trajectory at the burn, that of the spacecraft without a propagation
        internal double MassAt(in Time burnEpoch) => _forceModel?.ContextAt(burnEpoch).TotalMass ?? _spacecraft.GetTotalMass();

        internal EncounterCase Analyze(StateVector burnState, Vector3 deltaV, double fuelCost)
        {
            var burnEpoch = burnState.Epoch;
            var held = EncounterCovariance.Resolve(_spacecraft, burnState);
            Matrix? covarianceRtn = held.CovarianceInertial.HasValue ? burnState.RotateCovarianceToRtn(held.CovarianceInertial.Value) : null;
            Time? heldEpoch = held.Source switch
            {
                CovarianceSource.InitialStateHeldInRtn => _spacecraft.InitialOrbitalParameters.Epoch,
                CovarianceSource.ProvidedAtEncounter => burnEpoch,
                _ => null
            };

            var afterBurn = new StateVector(burnState.Position, burnState.Velocity + deltaV, burnState.Observer, burnEpoch,
                burnState.Frame);
            var postBurn = covarianceRtn.HasValue
                ? new StateVector(afterBurn.Position, afterBurn.Velocity, afterBurn.Observer, burnEpoch, afterBurn.Frame,
                    afterBurn.RotateCovarianceFromRtn(covarianceRtn.Value))
                : afterBurn;

            var window = new Window(burnEpoch, _end);
            Spacecraft trial;
            StateSamplingSource protectedSource;
            if (_forceModel == null)
            {
                // Not propagated: its ephemeris is the two-body orbit of the state after the burn
                trial = CloneSpacecraft(_spacecraft, postBurn);
                protectedSource = CreateLocalizableStateSource(trial, _observer);
            }
            else
            {
                var context = _forceModel.ContextAt(burnEpoch);
                trial = CloneSpacecraft(_spacecraft, postBurn, System.Math.Max(context.TotalMass - fuelCost, 0.0),
                    context.DragCoefficient, context.ReflectivityCoefficient);
                var trajectory = PropagateOption(trial, _forceModel.NewIntegrator(ForceEvaluationContext.FromSpacecraft(trial)),
                    window, null, _forceModel.OrientationCache, _forceModel.SyncRoot);
                protectedSource = CreateTrajectoryStateSource(trial, trajectory, _observer, window);
            }

            var analysisOptions = new ConjunctionAnalysisOptions
            {
                SampleStep = _options.AnalysisSampleStep,
                MaximumEventSearchStep = _options.MaximumEventSearchStep,
                SecondaryHardBodyRadiusMeters = _options.SecondaryHardBodyRadiusMeters,
                LowRelativeSpeedThresholdMetersPerSecond = _options.LowRelativeSpeedThresholdMetersPerSecond,
                CovariancePropagation = null
            };
            var profile = new ProtectedSpacecraftProfile(trial, _encounterCase.ProtectedAsset.ManeuverConstraints);
            var protectedResolver = new EncounterCovarianceResolver(trial, protectedSource.Solution, null, window.EndDate,
                heldCovarianceEpoch: heldEpoch);
            return AnalyzeInternal(profile, _encounterCase.SecondaryObject, window, analysisOptions, protectedSource,
                _secondarySource, protectedResolver, _secondaryResolver);
        }
    }

    // The propagation of an avoidance option with the forces of the trajectory it leaves, under the lock of those forces;
    // the solution of the option evaluates them under that lock afterwards. Its analysis takes no lock inside it: the lock of
    // the secondary participant is never taken under that of the protected one. Two outputs, at the ends: the analysis
    // reads the steps.
    private static PropagationSolution PropagateOption(Spacecraft trial, Integrator integrator, in Window window,
        CovariancePropagationOptions covariancePropagation, PropagationFrameOrientationCache orientationCache,
        object syncRoot, Func<double[]> crossCovariance = null)
    {
        lock (syncRoot)
        {
            using var propagator = new CentralBodyPropagator(window, trial, integrator, window.Length,
                covariancePropagation, orientationCache, syncRoot);
            var cross = crossCovariance?.Invoke();
            if (cross != null)
            {
                propagator.SetInitialCrossCovariance(cross);
            }

            return propagator.Propagate();
        }
    }

    /// <summary>
    /// The avoidance options of an encounter whose protected participant has a propagated covariance (E4): each option is
    /// propagated in RK7(8) from the state after its burn, with the forces and the integrator settings of the trajectory
    /// it leaves, and the covariance at the burn, P(t_b) + B L_e Bᵀ (C5, model of Gates; B = [0 ; I]); with consider
    /// parameters, from the extended covariance at the burn, so that the correlation of the state and the parameters
    /// carries over (<see cref="CovarianceModel.SetInitialCrossCovariance"/>). Each option is a fixed ΔV at a fixed epoch,
    /// the open-loop model of the propagated covariance (A5). The secondary participant keeps one resolution of its
    /// covariance for all the options, up to the end of the window.
    /// </summary>
    private sealed class PropagatedAvoidance
    {
        private readonly EncounterCase _encounterCase;
        private readonly AvoidanceSearchOptions _options;
        private readonly ILocalizable _observer;
        private readonly PropagationSolution _solution;
        private readonly PropagationDynamics _dynamics;
        private readonly CovariancePropagationOptions _covariancePropagation;

        // The options of the replays, the secondary participant's and those of the analysis of each option: controlled on
        // the state only, the steps being those of the propagation (#424)
        private readonly CovariancePropagationOptions _replayPropagation;
        private readonly Time _end;
        private readonly StateSamplingSource _secondarySource;
        private readonly EncounterCovarianceResolver _secondaryResolver;

        private PropagatedAvoidance(EncounterCase encounterCase, AvoidanceSearchOptions options, ILocalizable observer,
            EncounterCovariance covariance)
        {
            _encounterCase = encounterCase;
            _options = options;
            _observer = observer;
            _solution = covariance.Solution;
            _dynamics = _solution.Dynamics;
            _covariancePropagation = _solution.CovariancePropagation;
            _replayPropagation = _covariancePropagation with { IncludeInStepControl = false };

            // The options are propagated within the window and within the force model of the trajectory (its ephemeris
            // and orientation caches): up to the end of the propagation the covariance comes from, past the last
            // encounter where its replay may end
            var source = covariance.Propagation ?? _solution;
            var sourceEnd = source.End;
            _end = encounterCase.ScreeningWindow.EndDate < sourceEnd ? encounterCase.ScreeningWindow.EndDate : sourceEnd;
            _secondarySource = CreateLocalizableStateSource(encounterCase.SecondaryObject, observer);
            _secondaryResolver = new EncounterCovarianceResolver(encounterCase.SecondaryObject, _secondarySource.Solution,
                _replayPropagation, _end);
        }

        internal static PropagatedAvoidance TryCreate(EncounterCase encounterCase, AvoidanceSearchOptions options,
            ILocalizable observer)
        {
            var covariance = encounterCase.ProtectedCovariance;
            if (covariance?.Source != CovarianceSource.Propagated || covariance.Solution?.Dynamics?.Integrator == null ||
                covariance.Solution.CovariancePropagation == null)
            {
                return null;
            }

            return new PropagatedAvoidance(encounterCase, options, observer, covariance);
        }

        // A burn the solution covers, before the end of the options
        internal bool Covers(in Time burnEpoch)
        {
            return burnEpoch < _end && _solution.Covers(burnEpoch, 0.0);
        }

        // The state at the burn, with its covariance, in the frame of the propagation and relative to its central body
        internal StateVector BurnState(in Time burnEpoch) => _solution.GetStateVector(burnEpoch);

        // The mass and coefficients of the trajectory at the burn: those of its segment there
        internal ForceEvaluationContext Context(in Time burnEpoch) =>
            _solution.ContextAt(burnEpoch) ?? ForceEvaluationContext.FromSpacecraft(_encounterCase.ProtectedAsset.Spacecraft);

        internal EncounterCase Analyze(StateVector burnState, Vector3 deltaV, double fuelCost)
        {
            var burnEpoch = burnState.Epoch;
            var covariance = Copy(burnState.Covariance!.Value);
            if (_options.ExecutionError != null)
            {
                var execution = new double[9];
                _options.ExecutionError.Covariance(deltaV, execution);
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        covariance.Set(i + 3, j + 3, covariance.Get(i + 3, j + 3) + execution[3 * i + j]);
                    }
                }
            }

            var postBurn = new StateVector(burnState.Position, burnState.Velocity + deltaV, burnState.Observer, burnEpoch,
                burnState.Frame, covariance);
            var context = Context(burnEpoch);
            var trial = CloneSpacecraft(_encounterCase.ProtectedAsset.Spacecraft, postBurn,
                System.Math.Max(context.TotalMass - fuelCost, 0.0), context.DragCoefficient, context.ReflectivityCoefficient);

            // The state integrated with the mass and coefficients of the option, as its variational equations (#415)
            var integrator = _dynamics.Integrator.WithSameSettings();
            integrator.EvaluateForcesWith(ForceEvaluationContext.FromSpacecraft(trial));
            foreach (var force in _dynamics.Forces)
            {
                integrator.AddForce(force);
            }

            // The forces are those of the trajectory, under the lock of its dynamics
            var window = new Window(burnEpoch, _end);
            var trajectory = PropagateOption(trial, integrator, window, _covariancePropagation, _dynamics.OrientationCache,
                _dynamics.SyncRoot,
                _solution.ExtendedCovarianceParameters.Count > 0
                    ? () => CrossCovariance(_solution.GetExtendedCovariance(burnEpoch))
                    : null);

            return AnalyzeOption(trial, trajectory, window);
        }

        private EncounterCase AnalyzeOption(Spacecraft trial, PropagationSolution trajectory, in Window window)
        {
            var analysisOptions = new ConjunctionAnalysisOptions
            {
                SampleStep = _options.AnalysisSampleStep,
                MaximumEventSearchStep = _options.MaximumEventSearchStep,
                SecondaryHardBodyRadiusMeters = _options.SecondaryHardBodyRadiusMeters,
                LowRelativeSpeedThresholdMetersPerSecond = _options.LowRelativeSpeedThresholdMetersPerSecond,
                CovariancePropagation = _replayPropagation,

                // Twelve propagations per option: the index is that of the encounter, not of each option
                ComputeNonlinearityIndex = false
            };
            var protectedSource = CreateTrajectoryStateSource(trial, trajectory, _observer, window);
            var profile = new ProtectedSpacecraftProfile(trial, _encounterCase.ProtectedAsset.ManeuverConstraints);
            return AnalyzeInternal(profile, _encounterCase.SecondaryObject, window, analysisOptions, protectedSource,
                _secondarySource, secondaryResolver: _secondaryResolver);
        }

        private static Matrix Copy(Matrix matrix)
        {
            var copy = new Matrix(matrix.Rows, matrix.Columns);
            for (int i = 0; i < matrix.Rows; i++)
            {
                for (int j = 0; j < matrix.Columns; j++)
                {
                    copy.Set(i, j, matrix.Get(i, j));
                }
            }

            return copy;
        }

        // C0 = cov(δx(t_b), δp), the cross block of the extended covariance at the burn, unchanged by the execution error
        private static double[] CrossCovariance(Matrix extended)
        {
            int count = extended.Columns - 6;
            var cross = new double[6 * count];
            for (int i = 0; i < 6; i++)
            {
                for (int c = 0; c < count; c++)
                {
                    cross[i * count + c] = extended.Get(i, 6 + c);
                }
            }

            return cross;
        }
    }

    private static EncounterCase AnalyzeInternal(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        ConjunctionAnalysisOptions options,
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        EncounterCovarianceResolver protectedResolver = null,
        EncounterCovarianceResolver secondaryResolver = null)
    {
        var tcaEpochs = FindCloseApproaches(
            protectedSource,
            secondarySource,
            screeningWindow,
            options.SampleStep,
            options.MaximumEventSearchStep,
            options.BisectionToleranceSeconds,
            readRangeRateOverWindow: options.ReadRangeRateOverWindow);

        Time tca = SelectBestTcaEpoch(tcaEpochs, screeningWindow, protectedSource, secondarySource);
        return BuildEncounterCase(protectedAsset, secondaryObject, screeningWindow, tca, options, protectedSource, secondarySource,
            protectedResolver ?? CreateResolver(protectedAsset.Spacecraft, protectedSource, options, tca),
            secondaryResolver ?? CreateResolver(secondaryObject, secondarySource, options, tca));
    }

    // The covariance of a participant at the encounter epochs of the analysis, propagated up to the last one (E2)
    private static EncounterCovarianceResolver CreateResolver(ILocalizable participant, StateSamplingSource source,
        ConjunctionAnalysisOptions options, in Time lastEncounter)
    {
        return new EncounterCovarianceResolver(participant, source.Solution, options.CovariancePropagation, lastEncounter,
            options.ComputeNonlinearityIndex);
    }

    private static IReadOnlyList<EncounterCase> AnalyzeAllInternal(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        ConjunctionAnalysisOptions options,
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource)
    {
        var tcaEpochs = FindCloseApproaches(
            protectedSource,
            secondarySource,
            screeningWindow,
            options.SampleStep,
            options.MaximumEventSearchStep,
            options.BisectionToleranceSeconds,
            windowEndsWithoutInteriorMinimum: false,
            readRangeRateOverWindow: options.ReadRangeRateOverWindow);

        // On the dense path, the interior minima only, none when the window holds none (#621); the sampled path keeps its
        // midpoint fallback
        if (tcaEpochs.Count == 0 && CanUseDensePolynomialSearch(protectedSource, secondarySource))
        {
            return Array.Empty<EncounterCase>();
        }

        if (tcaEpochs.Count == 0)
        {
            return new[]
            {
                AnalyzeInternal(protectedAsset, secondaryObject, screeningWindow, options, protectedSource, secondarySource)
            };
        }

        var lastTca = tcaEpochs.Max();
        var protectedResolver = CreateResolver(protectedAsset.Spacecraft, protectedSource, options, lastTca);
        var secondaryResolver = CreateResolver(secondaryObject, secondarySource, options, lastTca);
        return tcaEpochs
            .Select(tca => BuildEncounterCase(protectedAsset, secondaryObject, screeningWindow, tca, options, protectedSource,
                secondarySource, protectedResolver, secondaryResolver))
            .ToArray();
    }

    private static Time SelectBestTcaEpoch(
        IReadOnlyList<Time> tcaEpochs,
        Window screeningWindow,
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource)
    {
        if (tcaEpochs.Count == 0)
        {
            return screeningWindow.StartDate + TimeSpan.FromSeconds(
                (screeningWindow.EndDate - screeningWindow.StartDate).TotalSeconds * 0.5);
        }

        var tca = tcaEpochs[0];
        double minDist = double.MaxValue;
        foreach (var epoch in tcaEpochs)
        {
            var pState = protectedSource.GetState(epoch);
            var sState = secondarySource.GetState(epoch);
            var dist = (sState.Position - pState.Position).MagnitudeSquared();
            if (dist < minDist)
            {
                minDist = dist;
                tca = epoch;
            }
        }

        return tca;
    }

    // --- Range-rate root-finding for TCA detection ---

    internal static List<Time> FindCloseApproaches(
        ILocalizable protectedObject,
        ILocalizable secondaryObject,
        Window window,
        TimeSpan sampleStep,
        double bisectionToleranceSeconds,
        ILocalizable observer)
    {
        return FindCloseApproaches(
            CreateLocalizableStateSource(protectedObject, observer),
            CreateLocalizableStateSource(secondaryObject, observer),
            window,
            sampleStep,
            TimeSpan.FromSeconds(60.0),
            bisectionToleranceSeconds);
    }

    internal static List<Time> FindCloseApproaches(
        ILocalizable protectedObject,
        ILocalizable secondaryObject,
        Window window,
        TimeSpan sampleStep,
        TimeSpan maximumEventSearchStep,
        double bisectionToleranceSeconds,
        ILocalizable observer)
    {
        return FindCloseApproaches(
            CreateLocalizableStateSource(protectedObject, observer),
            CreateLocalizableStateSource(secondaryObject, observer),
            window,
            sampleStep,
            maximumEventSearchStep,
            bisectionToleranceSeconds);
    }

    private static List<Time> FindCloseApproaches(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        Window window,
        TimeSpan sampleStep,
        TimeSpan maximumEventSearchStep,
        double bisectionToleranceSeconds,
        bool windowEndsWithoutInteriorMinimum = true,
        bool readRangeRateOverWindow = false)
    {
        if (CanUseDensePolynomialSearch(protectedSource, secondarySource))
        {
            // The reads of the range rate over the window are opt-in (decision 54 of #457): without them, the search of
            // main before #628, bit for bit
            return FindDenseCloseApproaches(
                protectedSource,
                secondarySource,
                window,
                readRangeRateOverWindow ? maximumEventSearchStep : null,
                bisectionToleranceSeconds,
                windowEndsWithoutInteriorMinimum);
        }

        var tcaEpochs = new List<Time>();
        var searchEpochs = BuildSearchEpochs(protectedSource, secondarySource, window, sampleStep, maximumEventSearchStep, bisectionToleranceSeconds);
        if (searchEpochs.Count == 0)
        {
            return tcaEpochs;
        }

        var previousEpoch = searchEpochs[0];
        double previousG = EvaluateRangeRate(protectedSource, secondarySource, previousEpoch);
        for (int index = 1; index < searchEpochs.Count; index++)
        {
            var currentEpoch = searchEpochs[index];
            double currentG = EvaluateRangeRate(protectedSource, secondarySource, currentEpoch);

            if (previousEpoch == window.StartDate && previousG == 0.0 && currentG > 0.0)
            {
                AddDistinctEpoch(tcaEpochs, previousEpoch, bisectionToleranceSeconds);
            }
            else if (previousG < 0.0 && currentG >= 0.0)
            {
                var tca = BisectForZero(protectedSource, secondarySource, previousEpoch, previousG, currentEpoch, currentG,
                    bisectionToleranceSeconds);
                AddDistinctEpoch(tcaEpochs, tca, bisectionToleranceSeconds);
            }

            previousG = currentG;
            previousEpoch = currentEpoch;
        }

        return tcaEpochs;
    }

    private static bool CanUseDensePolynomialSearch(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource)
    {
        return protectedSource.DenseTrajectory is not null
               && secondarySource.DenseTrajectory is not null
               && protectedSource.DenseTrajectory.Observer.NaifId == secondarySource.DenseTrajectory.Observer.NaifId
               && protectedSource.DenseTrajectory.Frame.Equals(secondarySource.DenseTrajectory.Frame);
    }

    /// <summary>
    /// The range rates of the states read by the dense search on this thread (each epoch once per search), counted for the
    /// tests of decision 54 of #457: without <see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/>, the public
    /// <c>Analyze</c> and <c>AnalyzeAll</c> read no more than the seeds need, whatever
    /// <see cref="ConjunctionAnalysisOptions.MaximumEventSearchStep"/>. One increment per read, no result depends on it.
    /// </summary>
    [ThreadStatic] internal static long DenseRangeRateReads;

    /// <summary>
    /// The close approaches of the polynomial search on the stored steps of two propagations: the interior local minima of
    /// the distance, refined on the range rate (#621: a window end, where the distance grows into the window, is no close
    /// approach; it was listed before 11.0.0). The minima of the cubic Hermite of the stored steps, the window ends
    /// included, only seed the search: whether a minimum lies inside the window is decided on the range rate of the states
    /// (<see cref="RefineOnRangeRate"/>), since the Hermite misses its zero by up to milliseconds (#363). With
    /// <see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/> (opt-in, decision 54 of #457), the range rate is
    /// also read at every candidate and at samples between them, so that no two reads are more than
    /// <paramref name="maximumReadSpacing"/> apart, and each change from negative to positive between two reads that holds
    /// no zero of the seeds is bisected (#628): a minimum of the states with no minimum of the Hermite near it; without
    /// it, such a minimum may be lost, every close approach listed still a minimum of the states. Without a zero inside, a
    /// range rate negative at the start and zero or positive at the end is bisected over the window (a slow pass whose
    /// candidates all lie within the flatness tolerance of the seeds, a window of a few ticks about a minimum; with the
    /// reads, the end where the range rate is exactly zero, the bisection over the window then possibly ending on a
    /// minimum inside the last pair of reads that its own bisection stepped over, or a zero in a window one tick wide,
    /// which no epoch inside resolves). A window end is never a close approach, a zero of the range rate exactly there
    /// included. Every close approach is a zero of the range rate from negative to positive: a seed that the refinement
    /// does not bracket on the range rate, a maximum of the distance seeded by the error of the Hermite on a slow pass, is
    /// none, and does not keep the dense <c>Analyze</c> from its window ends.
    /// </summary>
    /// <param name="maximumReadSpacing">The largest spacing of the reads of the range rate over the window, the
    /// <see cref="ConjunctionAnalysisOptions.MaximumEventSearchStep"/> of the sampled search (60 s by default), with
    /// <see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/>; null without it: the seeds alone.</param>
    /// <param name="windowEndsWithoutInteriorMinimum">When the window holds no interior minimum, return the ends where the
    /// distance grows into the window, on the range rate of the states (an end where it is exactly zero included), instead
    /// of nothing; within a window one tick wide whose range-rate zero no epoch inside resolves, the closer end. <see cref="Analyze(ProtectedSpacecraftProfile, PropagationSolution, ILocalizable, PropagationSolution, Window, ConjunctionAnalysisOptions)"/>
    /// keeps its former answer there, which #621's own change decides; <c>AnalyzeAll</c> lists the interior minima only.</param>
    private static List<Time> FindDenseCloseApproaches(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        Window window,
        TimeSpan? maximumReadSpacing,
        double toleranceSeconds,
        bool windowEndsWithoutInteriorMinimum)
    {
        var protectedDense = protectedSource.DenseTrajectory;
        var secondaryDense = secondarySource.DenseTrajectory;
        var boundaries = BuildDenseIntervalBoundaries(protectedDense, secondaryDense, window, toleranceSeconds);
        if (boundaries.Count < 2)
        {
            // A window of zero length: its one epoch for Analyze, nothing for AnalyzeAll
            return windowEndsWithoutInteriorMinimum ? new List<Time> { window.StartDate } : new List<Time>();
        }

        var candidateEpochs = new List<Time>(boundaries);
        int protectedStepIndex = 0;
        int secondaryStepIndex = 0;

        for (int intervalIndex = 1; intervalIndex < boundaries.Count; intervalIndex++)
        {
            var left = boundaries[intervalIndex - 1];
            var right = boundaries[intervalIndex];
            double durationSeconds = (right - left).TotalSeconds;
            if (durationSeconds <= System.Math.Max(toleranceSeconds, 1.0e-12))
            {
                continue;
            }

            while (protectedStepIndex < protectedDense.Steps.Count &&
                   protectedDense.Steps[protectedStepIndex].EndEpoch <= left)
            {
                protectedStepIndex++;
            }

            while (secondaryStepIndex < secondaryDense.Steps.Count &&
                   secondaryDense.Steps[secondaryStepIndex].EndEpoch <= left)
            {
                secondaryStepIndex++;
            }

            if (protectedStepIndex >= protectedDense.Steps.Count ||
                secondaryStepIndex >= secondaryDense.Steps.Count)
            {
                break;
            }

            var protectedStep = protectedDense.Steps[protectedStepIndex];
            var secondaryStep = secondaryDense.Steps[secondaryStepIndex];
            if (left < protectedStep.StartEpoch || right > protectedStep.EndEpoch ||
                left < secondaryStep.StartEpoch || right > secondaryStep.EndEpoch)
            {
                continue;
            }

            var relativePositionPolynomial = BuildRelativePositionPolynomial(
                protectedStep,
                secondaryStep,
                left,
                durationSeconds);
            var distanceSquaredPolynomial = BuildSquaredMagnitudePolynomial(relativePositionPolynomial);
            var stationaryPolynomial = Differentiate(distanceSquaredPolynomial);
            if (IsEffectivelyZeroPolynomial(stationaryPolynomial))
            {
                continue;
            }

            double rootTolerance = System.Math.Clamp(
                toleranceSeconds / durationSeconds,
                1.0e-12,
                1.0e-6);

            foreach (var root in FindPolynomialRootsInUnitInterval(stationaryPolynomial, rootTolerance))
            {
                if (root <= rootTolerance || root >= 1.0 - rootTolerance)
                {
                    continue;
                }

                candidateEpochs.Add(left + TimeSpan.FromSeconds(durationSeconds * root));
            }
        }

        var distinctCandidates = DistinctEpochs(candidateEpochs, toleranceSeconds);

        // The minima are selected on the polynomials of the stored steps, which costs no force evaluation; only their
        // refinement uses the states of the sources, which inside a step cost an RK7(8) step each (#363)
        var minima = SelectLocalMinimumEpochs(distinctCandidates,
            epoch => (DensePosition(secondaryDense, epoch) - DensePosition(protectedDense, epoch)).MagnitudeSquared(),
            toleranceSeconds);

        // The minima of the polynomials, the window ends included, are only the seeds of the search: whether a minimum
        // lies inside the window is decided on the range rate of the states, whose zero the Hermite misses by its error
        // (#363)
        return CloseApproachesOnRangeRate(minima, distinctCandidates,
            epoch =>
            {
                DenseRangeRateReads++;
                return EvaluateRangeRate(protectedSource, secondarySource, epoch);
            },
            epoch => DistanceSquared(protectedSource, secondarySource, epoch), toleranceSeconds,
            maximumReadSpacing?.TotalSeconds, windowEndsWithoutInteriorMinimum);
    }

    /// <summary>
    /// The close approaches of the dense search from its seeds and candidates, on the range rate of the states
    /// (<see cref="FindDenseCloseApproaches"/>): the zeros of <see cref="RefineOnRangeRate"/> inside the window, then,
    /// without one, the fallbacks over the window and, for <c>Analyze</c>, its ends.
    /// </summary>
    /// <param name="minima">The minima of the polynomial search, each one of <paramref name="candidates"/>.</param>
    /// <param name="candidates">The sorted candidate epochs of the polynomial search, the window ends first and last.</param>
    /// <param name="rangeRate">The range rate of the states, (r_s − r_p)·(v_s − v_p), at an epoch.</param>
    /// <param name="distanceSquared">The squared distance of the states at an epoch.</param>
    /// <param name="toleranceSeconds">Tolerance of the bisection, in s.</param>
    /// <param name="maximumReadSpacingSeconds">The largest spacing of the reads of the range rate over the window, in s;
    /// null: no read over the window, the seeds alone (the default of the public options).</param>
    /// <param name="windowEndsWithoutInteriorMinimum">As for <see cref="FindDenseCloseApproaches"/>.</param>
    /// <returns>The close approaches, in order.</returns>
    internal static List<Time> CloseApproachesOnRangeRate(
        IReadOnlyList<Time> minima,
        IReadOnlyList<Time> candidates,
        Func<Time, double> rangeRate,
        Func<Time, double> distanceSquared,
        double toleranceSeconds,
        double? maximumReadSpacingSeconds,
        bool windowEndsWithoutInteriorMinimum)
    {
        // A window end is never a close approach (#621), not even a zero of the range rate exactly there, which the dense
        // Analyze keeps by its end fallback below. Each epoch is read once, those of RefineOnRangeRate and of the
        // fallbacks below alike (round 1 of the review of #628: the window ends were read again after the refinement)
        rangeRate = ReadOnce(rangeRate);
        var start = candidates[0];
        var end = candidates[^1];
        double StartRate() => rangeRate(start);
        double EndRate() => rangeRate(end);

        // The bisection returns the tick nearer the zero (#624): a zero inside the first or last tick of the window may
        // round onto its end. It is the tick next to that end inside the window, when that tick lies inside: the tick
        // after the start (the range rate negative there), the tick before the end (positive there). A zero exactly at an
        // end (the range rate zero there) is that end, no zero inside
        Time? Inside(Time epoch)
        {
            if (epoch > start && epoch < end)
            {
                return epoch;
            }

            if (epoch == start)
            {
                var next = start.AddTicks(1);
                return next < end && StartRate() < 0.0 ? next : null;
            }

            var previous = end.AddTicks(-1);
            return epoch == end && previous > start && EndRate() > 0.0 ? previous : null;
        }

        var closeApproaches = new List<Time>();
        foreach (var refined in RefineOnRangeRate(minima, candidates, rangeRate, toleranceSeconds,
                     maximumReadSpacingSeconds))
        {
            if (Inside(refined) is { } epoch)
            {
                AddDistinctEpoch(closeApproaches, epoch, toleranceSeconds);
            }
        }

        if (closeApproaches.Count > 0)
        {
            return closeApproaches;
        }

        // No zero inside: a range rate negative at the start and zero or positive at the end holds a minimum inside, or a
        // zero exactly at the end, and is bisected over the window. Without the reads over the window, the distances of
        // the candidates may all lie within the flatness tolerance of SelectLocalMinimumEpochs (a slow pass, a window of a
        // few ticks about a minimum); a zero exactly at the end, which the bisection returns as the end, is that end, no
        // minimum inside (Inside). With the reads (#628), which always hold a change of sign there, no zero is kept inside
        // only when theirs is the end, the range rate exactly zero there: the bisection over the window, whose middles are
        // not those of the last pair of reads, may then end on a minimum inside that pair, narrower than the reads, which
        // that pair's bisection stepped over (the limit of the reads; review round 3 of #628), or on the end again; or the
        // window is one tick wide, for the end fallback below. With a tolerance coarser than a tick the bisection ends on
        // the middle of its last bracket, inside the window, which is then listed although the range rate is negative
        // there (as before #628; out of its scope)
        double startRate = StartRate();
        double endRate = EndRate();
        Time? zero = null;
        if (startRate < 0.0 && endRate >= 0.0)
        {
            zero = BisectForZero(rangeRate, start, startRate, end, endRate, toleranceSeconds);
            if (Inside(zero.Value) is { } epoch)
            {
                closeApproaches.Add(epoch);
                return closeApproaches;
            }
        }

        if (!windowEndsWithoutInteriorMinimum)
        {
            return closeApproaches;
        }

        // No minimum inside the window: the ends where the distance grows into it, on the states (the first and last
        // candidates are the window ends, BuildDenseIntervalBoundaries)
        if (startRate >= 0.0)
        {
            closeApproaches.Add(start);
        }

        if (endRate <= 0.0)
        {
            closeApproaches.Add(end);
        }

        // A zero inside a window one tick wide, which no epoch inside resolves: the closer end, never the midpoint
        if (closeApproaches.Count == 0 && zero.HasValue)
        {
            closeApproaches.Add(distanceSquared(start) <= distanceSquared(end) ? start : end);
        }

        return closeApproaches;
    }

    private static double DistanceSquared(StateSamplingSource protectedSource, StateSamplingSource secondarySource,
        Time epoch)
    {
        return (secondarySource.GetState(epoch).Position - protectedSource.GetState(epoch).Position).MagnitudeSquared();
    }

    // The cubic Hermite position of a dense trajectory, from the step that contains the epoch (the earlier one at a
    // boundary), as the polynomial search sees it
    private static Vector3 DensePosition(DenseTrajectorySource dense, Time epoch)
    {
        var steps = dense.Steps;
        int lo = 0;
        int hi = steps.Count - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (epoch > steps[mid].EndEpoch)
                lo = mid + 1;
            else
                hi = mid;
        }

        var step = steps[lo];
        return PropagationSegment.HermiteInterpolate(step.Step,
            step.TimeArgument.SecondsBetween(step.SegmentBaseEpoch, epoch)).position;
    }

    /// <summary>
    /// The number of times <see cref="RefineOnRangeRate"/> moves the bracket of a minimum to the next candidate before it
    /// follows the range rate over the candidates, and the reach of its secant estimates, in candidates past the neighbours
    /// of the minimum.
    /// </summary>
    internal const int MaximumBracketWidenings = 2;

    // Base of the secant of the range rate at a minimum of the polynomial search, in s
    private const double SecantBaseSeconds = 1.0e-3;

    // Half-width of the bracket checked around a secant estimate, in s: ten ticks
    private const double SecantBracketHalfWidthSeconds = 1.0e-6;

    /// <summary>
    /// The largest spacing, in s, of the reads of the range rate by which <see cref="RefineOnRangeRate"/> searches the zero
    /// of a minimum from the minimum itself: the distances from it double from 1 ms while their spacing stays below this
    /// one (up to 1.024 s), then grow by it. An interval of the sign the search looks for at least this wide holds a read,
    /// whatever its distance from the minimum within the reach of the search.
    /// </summary>
    internal const double SeedSearchMaximumSpacingSeconds = 1.0;

    /// <summary>
    /// Moves each minimum of the polynomial search to the zero of <paramref name="rangeRate"/>, from negative to
    /// positive, near it, and drops a minimum whose zero lies outside the candidates; then, given
    /// <paramref name="maximumReadSpacingSeconds"/>, adds the zeros that the reads of the range rate over the candidates
    /// show and no minimum reached (#628). Every epoch returned is the tick nearer such
    /// a zero (<see cref="BisectForZero(Func{Time, double}, Time, double, Time, double, double)"/>), a window end included
    /// when the zero lies within half a tick of it, or a window end where the range rate is exactly zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The polynomial search locates each minimum on the cubic Hermite interpolation of the positions, which is off by up
    /// to about 100 m between the steps of an RK7(8) propagation (#363): the TCA is then off by up to milliseconds. The
    /// range rate comes from the states of the sources, as in the sampled search, and it alone decides whether a minimum
    /// lies between the first and the last candidates, the window ends.
    /// </para>
    /// <para>
    /// A minimum at the first candidate is a minimum inside when the range rate is negative there (the distance still
    /// decreases into the window), and its zero is searched after it; zero there with a positive range rate at the next
    /// candidate, it is the minimum itself, as in the sampled search; positive, it is no minimum and is dropped. A minimum
    /// at the last candidate is a minimum inside when the range rate is zero or positive there, and its zero is searched
    /// before it; negative, it is dropped. A zero of the range rate exactly at a window end (the range rate negative at
    /// the candidate before the last one, or positive at the one after the first) is that end itself, whichever minimum
    /// it refines. The dense search drops it, a window end being no close approach, and takes a zero within the first or
    /// last tick that the bisection rounds onto a window end as the tick next to that end inside the window.
    /// </para>
    /// <para>
    /// The range rate is close to linear near its zero: its relative curvature is of the order of the orbital rate, so
    /// over a few milliseconds the zero of its secant is within a fraction of a tick (100 ns) of the zero. The secant
    /// through the minimum and an epoch 1 ms away (before it when the last candidate is nearer than that) gives a first
    /// estimate, a second secant within ten ticks of it a second one, and a bracket of one tick on each side of that is
    /// checked before the bisection. Each bracket is checked before it is used, and the bisection ends on the bracket one
    /// tick wide that contains the change of sign on the grid of the epochs, whichever checked bracket it starts from,
    /// and returns the tick of that bracket where the range rate is smaller in magnitude (#624): these estimates take the
    /// refinement from about 33 evaluations of the states of each source to about 7 without changing the refined TCA. The
    /// range rate is never evaluated outside the first and last candidates.
    /// </para>
    /// <para>
    /// When an estimate cannot be checked, the range rate at the minimum itself says on which side its zero lies: zero
    /// there, the minimum is the zero; positive, before it; negative, after it. It is read on that side 1 ms from the
    /// minimum, then at distances doubling up to 1.024 s and growing by <see cref="SeedSearchMaximumSpacingSeconds"/> (1 s)
    /// after that, up to the neighbouring candidate, until it changes sign, and the bracket of the last two reads is
    /// bisected. This finds the zero between a minimum and its neighbour when the Hermite has placed the minimum past the
    /// zero of the states and the range rate has the same sign at the candidates on both sides (a maximum of the distance
    /// lying between the zero and the candidate), and a zero whose range rate keeps the sign of the minimum's except over
    /// an interval at least 1 s wide (a shallow minimum next to a fold of the range rate): the resolution of this search
    /// is 1 s, an interval narrower than that between two reads may be stepped over. It reaches the neighbouring
    /// candidate whatever <paramref name="maximumReadSpacingSeconds"/>: the reads over the window, up to that spacing apart,
    /// would step over an interval narrower than it that these reads find (#628, round 1 of its review).
    /// </para>
    /// <para>
    /// When the range rate keeps its sign up to that candidate, the bisection starts from the candidates next to the
    /// minimum. When the minimum on the states lies past one of them, the range rate has the wrong sign there; the bracket
    /// then moves to the next candidate on that side, at most <see cref="MaximumBracketWidenings"/> times in all. A
    /// minimum whose bracket reaches the first candidate with a range rate zero or positive there, or the last one with a
    /// negative range rate, has its zero outside the candidates and is dropped.
    /// </para>
    /// <para>
    /// A minimum still not bracketed, the widenings spent between the first and last candidates, is followed over the
    /// candidates, on the side where the range rate says the minimum lies (before the minimum when it is zero or positive
    /// at the left end of the bracket, after it when it is negative at the right end, on both sides when both hold: a
    /// maximum on the states), to the nearest pair of consecutive candidates where it changes from negative to zero or
    /// positive, whose zero is bisected. A side that reaches a window end without the change holds no zero inside; a
    /// minimum with no such zero on either side is dropped: it is not a minimum on the range rate (a maximum of the
    /// distance on the states, seeded by the error of the Hermite on a slow pass). A minimum that is not one of the
    /// candidates is dropped: there is no bracket to refine it in.
    /// </para>
    /// <para>
    /// The zeros found from the seeds give one minimum per pair of consecutive candidates: a zero that lies in the pair of
    /// an earlier one (no candidate strictly between them) is the same minimum, reached from another seed, the range rate
    /// changing sign more than once there at the level of its noise. The zeros of the secants and of the brackets of the
    /// candidates come first, then those read from the minima on their own range rate, then those of the pairs reached by
    /// following it. The reads below, when asked for, add their zeros one per pair of consecutive reads, so a pair of
    /// candidates may then hold more than one minimum (two minima of one long step). Without them, a minimum that no seed
    /// reaches is not returned: a second minimum between two candidates, or one between two candidates where the range
    /// rate closes at one and recedes at the next with no seed near (#628).
    /// </para>
    /// <para>
    /// Last, given <paramref name="maximumReadSpacingSeconds"/> (the option
    /// <see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/>, decision 54 of #457), the range rate is read at
    /// every candidate and, between two candidates more than
    /// <paramref name="maximumReadSpacingSeconds"/> and one tick apart, at the fewest epochs evenly spaced in ticks that leave
    /// no gap wider by more than one tick (#628; a step of 60 TT seconds about the Earth may span 60 s and one tick),
    /// and each change from negative to zero or positive between two consecutive reads is bisected, unless a zero already
    /// lies between them: a minimum that no minimum of the polynomial search reached, the range rate closing at one
    /// candidate and receding at the next with no seed near (the Hermite of long or mixed steps), or a second minimum
    /// between two candidates, which the reads between them separate (one long step holding a minimum, a maximum and a
    /// minimum). A zero exactly at the window end, closing at the read before it, is the end itself when the bisection of
    /// that last pair reaches the end within its tolerance; a change of sign inside the pair is bisected as any other. A
    /// change of sign over an interval narrower than the spacing of the reads is not guaranteed to be found unless the reads
    /// 1 s apart from a minimum reach it (a minimum whose secants are not checked, between it and its neighbouring candidate
    /// on the side its range rate points to, up to the first change of sign; a minimum whose secants are checked reads
    /// nothing between itself and their zero, so an interval between them, however wide against 1 s, is read only by the
    /// reads over the window), whichever its sign (a narrow fold of the range rate, or the second minimum of a pair next to
    /// a positive interval narrower than the spacing, both reads around it closing; or a minimum after a negative interval
    /// narrower than the spacing that follows a zero found from a seed in the same pair of reads, which is not bisected
    /// again). Each epoch is read once. The zeros are returned in order.
    /// </para>
    /// </remarks>
    /// <param name="minima">The minima of the polynomial search, each one of <paramref name="candidates"/>.</param>
    /// <param name="candidates">The sorted candidate epochs of the polynomial search, the window ends first and last.</param>
    /// <param name="rangeRate">The range rate, (r_s − r_p)·(v_s − v_p), at an epoch.</param>
    /// <param name="toleranceSeconds">Tolerance of the bisection, in s.</param>
    /// <param name="maximumReadSpacingSeconds">The largest spacing of the reads of the range rate over the candidates, in
    /// s (+∞: the candidates alone are read); null by default: no read over the candidates, the seeds alone.</param>
    /// <returns>The refined minima inside the candidates, in order: those of the minima one per pair of consecutive
    /// candidates, those of the reads, if any, one per pair of consecutive reads; the ticks nearer the zeros of the range rate from
    /// negative to positive, or a window end where it is exactly zero.</returns>
    internal static List<Time> RefineOnRangeRate(
        IReadOnlyList<Time> minima,
        IReadOnlyList<Time> candidates,
        Func<Time, double> rangeRate,
        double toleranceSeconds,
        double? maximumReadSpacingSeconds = null)
    {
        // Each epoch is read once: the reads over the candidates come back to those the seeds have read, and the seeds
        // read some epochs more than once (the range rate of the states is deterministic, so no zero changes)
        rangeRate = ReadOnce(rangeRate);
        var refined = new List<Time>(minima.Count);
        int last = candidates.Count - 1;

        // The zeros found from the seeds on their own range rate
        var fromSeeds = new List<Time>();

        // The pairs of consecutive candidates, by the index of the first one, where the range rate followed from a minimum
        // that the widenings did not bracket changes from negative to zero or positive, with the range rates at both
        var followed = new SortedDictionary<int, (double Low, double High)>();
        foreach (var minimum in minima)
        {
            int index = IndexOf(candidates, minimum);
            if (index < 0 || last < 1)
            {
                // No candidates to bracket it: not refined, so not a minimum on the range rate
                continue;
            }

            int left = System.Math.Max(index - 1, 0);
            int right = System.Math.Min(index + 1, last);
            double leftRate = double.NaN;
            double rightRate = double.NaN;
            if (index == 0)
            {
                // The window start: a minimum inside when the distance still decreases into the window
                leftRate = rangeRate(minimum);
                if (leftRate >= 0.0)
                {
                    if (leftRate == 0.0 && rangeRate(candidates[1]) > 0.0)
                    {
                        AddDistinctEpoch(refined, minimum, toleranceSeconds);
                    }

                    continue;
                }
            }
            else if (index == last)
            {
                // The window end: a minimum inside when the distance no longer decreases there
                rightRate = rangeRate(minimum);
                if (rightRate < 0.0)
                {
                    continue;
                }
            }

            // The estimates may not go further than the widened bracket of the candidates
            var lowest = candidates[System.Math.Max(index - 1 - MaximumBracketWidenings, 0)];
            var highest = candidates[System.Math.Min(index + 1 + MaximumBracketWidenings, last)];
            if (TryZeroBySecants(rangeRate, minimum, lowest, highest, candidates[last], toleranceSeconds, out var zero))
            {
                AddDistinctEpoch(refined, zero, toleranceSeconds);
                continue;
            }

            // The range rate at the seed itself points to its zero: receding there, the zero lies before it; still
            // closing, after it; zero, it is the seed
            double seedRate = index == 0 ? leftRate : index == last ? rightRate : rangeRate(minimum);
            if (seedRate == 0.0)
            {
                AddDistinctEpoch(refined, minimum, toleranceSeconds);
                continue;
            }

            if (TryZeroFromTheSeed(rangeRate, candidates, index, seedRate, toleranceSeconds, out var seedZero))
            {
                fromSeeds.Add(seedZero);
                continue;
            }

            if (double.IsNaN(leftRate))
            {
                leftRate = rangeRate(candidates[left]);
            }

            if (double.IsNaN(rightRate))
            {
                rightRate = rangeRate(candidates[right]);
            }

            for (int widening = 0;
                 widening < MaximumBracketWidenings && (leftRate >= 0.0 || rightRate < 0.0);
                 widening++)
            {
                // Already receding at the left candidate: the minimum lies before it; still closing at the right
                // candidate: after it
                if (leftRate >= 0.0 && left > 0)
                {
                    leftRate = rangeRate(candidates[--left]);
                }
                else if (rightRate < 0.0 && right < last)
                {
                    rightRate = rangeRate(candidates[++right]);
                }
                else
                {
                    break;
                }
            }

            if (leftRate < 0.0 && rightRate >= 0.0)
            {
                // A zero exactly at the window end, closing before it, is the end itself, whatever change of sign the
                // bisection of a bracket wider than the last pair would reach first
                var tca = right == last && rightRate == 0.0
                                        && (left == last - 1 ? leftRate : rangeRate(candidates[last - 1])) < 0.0
                    ? candidates[last]
                    : BisectForZero(rangeRate, candidates[left], leftRate, candidates[right], rightRate,
                        toleranceSeconds);
                AddDistinctEpoch(refined, tca, toleranceSeconds);
                continue;
            }

            // A zero exactly at the window start, receding after it, is the start itself, as for a minimum there
            if (left == 0 && leftRate == 0.0 && rangeRate(candidates[1]) > 0.0)
            {
                AddDistinctEpoch(refined, candidates[0], toleranceSeconds);
                continue;
            }

            // Receding at the window start or still closing at the window end: the zero lies outside the window
            if ((left == 0 && leftRate >= 0.0) || (right == last && rightRate < 0.0))
            {
                continue;
            }

            // Still not bracketed, the widenings spent between the window ends: the range rate is followed over the
            // candidates, on each side where the states place the minimum, to the nearest change from negative to zero or
            // positive. Receding on both sides of the seed (a maximum of the distance on the states, the seed a minimum of
            // the Hermite's error), each side is followed; a side that reaches a window end without the change holds no
            // minimum inside
            if (leftRate >= 0.0)
            {
                double afterRate = leftRate;
                while (leftRate >= 0.0 && left > 0)
                {
                    afterRate = leftRate;
                    leftRate = rangeRate(candidates[--left]);
                }

                if (leftRate < 0.0)
                {
                    followed.TryAdd(left, (leftRate, afterRate));
                }
            }

            if (rightRate < 0.0)
            {
                double beforeRate = rightRate;
                while (rightRate < 0.0 && right < last)
                {
                    beforeRate = rightRate;
                    rightRate = rangeRate(candidates[++right]);
                }

                if (rightRate >= 0.0)
                {
                    followed.TryAdd(right - 1, (beforeRate, rightRate));
                }
            }
        }

        return MergeByPairs(refined, fromSeeds, followed, candidates,
            maximumReadSpacingSeconds is { } spacing ? WithRangeRateSamples(candidates, spacing) : null, rangeRate,
            toleranceSeconds);
    }

    // The range rate read once per epoch, each value kept for the later reads of the same epoch
    private static Func<Time, double> ReadOnce(Func<Time, double> rangeRate)
    {
        var values = new Dictionary<Time, double>();
        return epoch =>
        {
            if (!values.TryGetValue(epoch, out double value))
            {
                value = rangeRate(epoch);
                values.Add(epoch, value);
            }

            return value;
        };
    }

    // The epochs where RefineOnRangeRate reads the range rate over the whole window (#628): the candidates, and between
    // two of them more than maximumSpacingSeconds and one tick apart, the fewest epochs evenly spaced in ticks that leave
    // no gap wider than that spacing by more than one tick, as the sampled search subdivides the intervals of its knots
    // (BuildSearchEpochs) but for that tick. About the Earth the steps count TT seconds and their boundaries are TDB
    // labels cut to the tick (#501): when TDB - TT increases, a step of 60 TT seconds spans 60 s and one tick of the
    // labels, which a strict comparison split at its middle, one RK7(8) step more for a read 100 ns closer (review round 3
    // of #628: 32 reads in 3 h at 60 s steps from 2026-01-20)
    private static List<Time> WithRangeRateSamples(IReadOnlyList<Time> candidates, double maximumSpacingSeconds)
    {
        double maximumSpacingTicks =
            System.Math.Max(1.0, System.Math.Round(maximumSpacingSeconds * TimeSpan.TicksPerSecond));
        var reads = new List<Time>(candidates.Count);
        for (int index = 0; index < candidates.Count; index++)
        {
            if (index > 0)
            {
                var left = candidates[index - 1];
                long gapTicks = (candidates[index] - left).Ticks;
                if (gapTicks - 1 > maximumSpacingTicks)
                {
                    long subdivisions = (long)System.Math.Ceiling((gapTicks - 1) / maximumSpacingTicks);
                    for (long subdivision = 1; subdivision < subdivisions; subdivision++)
                    {
                        reads.Add(left.AddTicks(gapTicks / subdivisions * subdivision +
                                                gapTicks % subdivisions * subdivision / subdivisions));
                    }
                }
            }

            reads.Add(candidates[index]);
        }

        return reads;
    }

    // The zeros of RefineOnRangeRate in order, one per pair of consecutive candidates: those refined from the seeds by
    // the secants or the widened brackets, then those found from the seeds on their own range rate, then those of the
    // pairs reached by following the range rate, each dropped when a zero before it lies in its pair (no candidate
    // strictly between the two): the same minimum, reached from another seed, the range rate at the noise level changing
    // sign more than once there; then, when reads are given (opt-in, decision 54 of #457), those of the reads over the
    // window, one per pair of consecutive reads (#628): a pair of reads that holds a zero already is not bisected again, so
    // a second change of sign in it, after an interval of either sign narrower than the reads, is not found
    private static List<Time> MergeByPairs(List<Time> refined, List<Time> fromSeeds,
        SortedDictionary<int, (double Low, double High)> followed, IReadOnlyList<Time> candidates,
        IReadOnlyList<Time> reads, Func<Time, double> rangeRate, double toleranceSeconds)
    {
        var zeros = new List<Time>(refined.Count + fromSeeds.Count + followed.Count);
        var held = new HashSet<int>();
        int last = candidates.Count - 1;

        // The pairs a zero lies in: the one that holds it, and the one before when it is a candidate
        void Add(Time zero)
        {
            int pair = PairOf(candidates, zero);
            bool onCandidate = candidates[pair] == zero;
            if (held.Contains(pair) || (onCandidate && held.Contains(pair - 1)))
            {
                return;
            }

            held.Add(pair);
            if (onCandidate && pair > 0)
            {
                held.Add(pair - 1);
            }

            zeros.Add(zero);
        }

        foreach (var zero in refined)
        {
            Add(zero);
        }

        foreach (var zero in fromSeeds)
        {
            Add(zero);
        }

        foreach (var (pair, rates) in followed)
        {
            if (held.Contains(pair))
            {
                continue;
            }

            // A zero exactly at the window end, closing before it, is the end itself
            var right = candidates[pair + 1];
            Add(pair + 1 == last && rates.High == 0.0
                ? right
                : BisectForZero(rangeRate, candidates[pair], rates.Low, right, rates.High, toleranceSeconds));
        }

        if (reads is not null)
        {
            AddZerosOfTheReads(zeros, reads, rangeRate, toleranceSeconds);
        }

        zeros.Sort();
        var distinct = new List<Time>(zeros.Count);
        foreach (var zero in zeros)
        {
            AddDistinctEpoch(distinct, zero, toleranceSeconds);
        }

        return distinct;
    }

    // The zeros of the reads of the range rate over the window, added to those of the seeds (#628, opt-in by decision 54 of
    // #457): each change from negative to zero or positive between consecutive reads, in a pair of reads that holds no
    // zero yet: a minimum of the states that no seed reached, the range rate closing at one candidate and receding at the
    // next (form A), or two minima between two candidates, which the reads between them separate (form B). The last pair
    // of reads is bisected too: a zero exactly at the window end, closing before it, is the end itself when the bisection
    // reaches it within its tolerance (a bisection to a tick returns the end, a coarser one the middle of its last
    // bracket), and a change of sign inside the pair is bisected as any other (round 1 of the review of #628: the end was
    // returned before the pair was bisected)
    private static void AddZerosOfTheReads(List<Time> zeros, IReadOnlyList<Time> reads, Func<Time, double> rangeRate,
        double toleranceSeconds)
    {
        double previousRate = rangeRate(reads[0]);
        for (int index = 1; index < reads.Count; index++)
        {
            double rate = rangeRate(reads[index]);
            if (previousRate < 0.0 && rate >= 0.0 && !HoldsAZero(zeros, reads[index - 1], reads[index]))
            {
                var zero = BisectForZero(rangeRate, reads[index - 1], previousRate, reads[index], rate,
                    toleranceSeconds);
                if (index == reads.Count - 1 && rate == 0.0 && (reads[index] - zero).TotalSeconds < toleranceSeconds)
                {
                    zero = reads[index];
                }

                zeros.Add(zero);
            }

            previousRate = rate;
        }
    }

    // Whether one of the zeros lies in [left, right]
    private static bool HoldsAZero(List<Time> zeros, Time left, Time right)
    {
        foreach (var zero in zeros)
        {
            if (zero >= left && zero <= right)
            {
                return true;
            }
        }

        return false;
    }

    // The index of the pair of consecutive candidates that holds an epoch between the first and the last: the last
    // candidate at or before it, the pair before the last at the last candidate
    private static int PairOf(IReadOnlyList<Time> candidates, Time epoch)
    {
        int low = 0;
        int high = candidates.Count - 2;
        while (low < high)
        {
            int middle = low + (high - low + 1) / 2;
            if (candidates[middle] <= epoch)
                low = middle;
            else
                high = middle - 1;
        }

        return low;
    }

    // The zero of the range rate between a seed and its neighbouring candidate on the side its own range rate points to
    // (RefineOnRangeRate): before it when the range rate is positive there, after it when negative. The range rate is read
    // at 1 ms from the seed, then at distances doubling up to 1.024 s, then every SeedSearchMaximumSpacingSeconds (1 s) up
    // to that candidate, until it changes sign, and the bracket of the last two reads is bisected; a zero exactly at the
    // window end, closing before it, is the end itself. The spacing is capped: reads doubling all the way stride
    // over an interval of the other sign narrower than their spacing (a shallow minimum next to a fold of the range rate,
    // positive over 1.6 s between reads 4.096 s apart, lost in round 5 of the lot-6 review). It costs at most
    // 11 + ceil(D - 1.024) reads for a neighbouring candidate D seconds away, against 1 + ceil(log2(D / 1 ms)) before, and
    // the bisection of a bracket at most 1 s wide. Its reach is not capped by the spacing of the reads over the window
    // (#628, round 1 of its review): the reads, up to that spacing apart, step over an interval narrower than it that the
    // 1 s reads reach (a positive interval of 2.3 to 43 s, 73 to 87 s past the seed, at fixed 600 s steps). False when
    // the range rate keeps its sign up to the neighbouring candidate, or the seed has no neighbour on that side
    private static bool TryZeroFromTheSeed(Func<Time, double> rangeRate, IReadOnlyList<Time> candidates, int index,
        double seedRate, double toleranceSeconds, out Time zero)
    {
        zero = candidates[index];
        bool backward = seedRate > 0.0;
        int neighbour = backward ? index - 1 : index + 1;
        if (neighbour < 0 || neighbour >= candidates.Count)
        {
            return false;
        }

        var seed = candidates[index];
        var bound = candidates[neighbour];
        var previous = seed;
        double previousRate = seedRate;
        for (double distance = SecantBaseSeconds;
             ;
             distance += System.Math.Min(distance, SeedSearchMaximumSpacingSeconds))
        {
            var offset = TimeSpan.FromSeconds(distance);
            var read = backward
                ? (seed - bound > offset ? seed - offset : bound)
                : (bound - seed > offset ? seed + offset : bound);
            double rate = rangeRate(read);
            if (backward ? rate < 0.0 : rate >= 0.0)
            {
                zero = !backward && read == bound && neighbour == candidates.Count - 1 && rate == 0.0
                    ? bound
                    : backward
                        ? BisectForZero(rangeRate, read, rate, previous, previousRate, toleranceSeconds)
                        : BisectForZero(rangeRate, previous, previousRate, read, rate, toleranceSeconds);
                return true;
            }

            if (read == bound)
            {
                return false;
            }

            previous = read;
            previousRate = rate;
        }
    }

    // The zero of the range rate near a minimum, by two secants and a checked bracket (RefineOnRangeRate); false when a
    // secant is undefined or leaves [lowest, highest], or when the bracket around it does not hold the change of sign.
    // The first secant takes the epoch 1 ms after the minimum, or 1 ms before it when the window end is nearer: the range
    // rate is never evaluated past the window end, nor before lowest
    private static bool TryZeroBySecants(Func<Time, double> rangeRate, Time minimum, Time lowest, Time highest,
        Time windowEnd, double toleranceSeconds, out Time zero)
    {
        zero = minimum;
        var secantBase = TimeSpan.FromSeconds(SecantBaseSeconds);
        var second = minimum + secantBase;
        if (second > windowEnd)
        {
            second = minimum - secantBase;
            if (second < lowest)
            {
                return false;
            }
        }

        if (!TrySecantZero(rangeRate, minimum, second, lowest, highest, out var estimate))
        {
            return false;
        }

        var halfWidth = TimeSpan.FromSeconds(SecantBracketHalfWidthSeconds);
        var left = estimate - halfWidth;
        var right = estimate + halfWidth;
        if (left <= lowest || right >= highest)
        {
            return false;
        }

        double leftRate = rangeRate(left);
        double rightRate = rangeRate(right);
        if (leftRate >= 0.0 || rightRate < 0.0)
        {
            return false;
        }

        // Within the checked bracket, the second secant and the two ticks around it, checked in turn
        if (TrySecantZero(left, leftRate, right, rightRate, left, right, out var refinedEstimate))
        {
            var low = refinedEstimate.AddTicks(-1);
            var high = refinedEstimate.AddTicks(1);
            if (low > left && high < right)
            {
                double lowRate = rangeRate(low);
                if (lowRate < 0.0)
                {
                    double highRate = rangeRate(high);
                    if (highRate >= 0.0)
                    {
                        zero = BisectForZero(rangeRate, low, lowRate, high, highRate, toleranceSeconds);
                        return true;
                    }
                }
            }
        }

        zero = BisectForZero(rangeRate, left, leftRate, right, rightRate, toleranceSeconds);
        return true;
    }

    private static bool TrySecantZero(Func<Time, double> rangeRate, Time first, Time second, Time lowest,
        Time highest, out Time estimate)
    {
        return TrySecantZero(first, rangeRate(first), second, rangeRate(second), lowest, highest, out estimate);
    }

    private static bool TrySecantZero(Time first, double firstRate, Time second, double secondRate, Time lowest,
        Time highest, out Time estimate)
    {
        estimate = first;
        double slope = (secondRate - firstRate) / (second - first).TotalSeconds;
        if (!(slope > 0.0) || double.IsInfinity(slope))
        {
            return false;
        }

        double offset = -firstRate / slope;
        if (System.Math.Abs(offset) > (highest - lowest).TotalSeconds)
        {
            return false;
        }

        estimate = first + TimeSpan.FromSeconds(offset);
        return estimate > lowest && estimate < highest;
    }

    private static int IndexOf(IReadOnlyList<Time> epochs, Time epoch)
    {
        for (int i = 0; i < epochs.Count; i++)
        {
            if (epochs[i] == epoch)
                return i;
        }

        return -1;
    }

    private static double EvaluateRangeRate(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        Time epoch)
    {
        var pState = protectedSource.GetState(epoch);
        var sState = secondarySource.GetState(epoch);
        var relPos = sState.Position - pState.Position;
        var relVel = sState.Velocity - pState.Velocity;
        return relPos * relVel;
    }

    private static Time BisectForZero(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        Time tLow,
        double gLow,
        Time tHigh,
        double gHigh,
        double toleranceSeconds)
    {
        return BisectForZero(epoch => EvaluateRangeRate(protectedSource, secondarySource, epoch), tLow, gLow, tHigh,
            gHigh, toleranceSeconds);
    }

    /// <summary>
    /// The zero of the range rate in [<paramref name="tLow"/>, <paramref name="tHigh"/>], where it is negative at
    /// <paramref name="tLow"/> (<paramref name="gLow"/>) and zero or positive at <paramref name="tHigh"/>
    /// (<paramref name="gHigh"/>), the values the caller already holds: the bracket is bisected on the sign of the range
    /// rate, its middle in ticks, the values at its ends carried along, until it is narrower than
    /// <paramref name="toleranceSeconds"/> or one tick wide (100 ns, the resolution of the epochs).
    /// </summary>
    /// <returns>
    /// On a bracket one tick wide, the tick nearer the zero of the chord of the range rate: <paramref name="tHigh"/> when
    /// |gHigh| &lt; |gLow| (a zero exactly on that tick included), <paramref name="tLow"/> otherwise (a tie included).
    /// For a range rate linear over the tick, that is the nearest tick to its zero, half a tick from it at most (#624:
    /// before 11.0.0 the lower tick was always returned, up to a tick before the zero, a zero exactly on a tick one tick
    /// early). A bracket left wider than a tick by a tolerance of a tick or more gives its middle, in ticks.
    /// </returns>
    private static Time BisectForZero(Func<Time, double> rangeRate, Time tLow, double gLow, Time tHigh, double gHigh,
        double toleranceSeconds)
    {
        const int maxIterations = 80;
        for (int i = 0; i < maxIterations; i++)
        {
            long ticks = (tHigh - tLow).Ticks;
            if (ticks <= 1 || (tHigh - tLow).TotalSeconds < toleranceSeconds)
            {
                break;
            }

            var tMid = tLow.AddTicks(ticks / 2);
            double gMid = rangeRate(tMid);
            if (gMid < 0.0)
            {
                tLow = tMid;
                gLow = gMid;
            }
            else
            {
                tHigh = tMid;
                gHigh = gMid;
            }
        }

        long width = (tHigh - tLow).Ticks;
        if (width == 1)
        {
            return System.Math.Abs(gHigh) < System.Math.Abs(gLow) ? tHigh : tLow;
        }

        return tLow.AddTicks(width / 2);
    }

    private static List<Time> BuildDenseIntervalBoundaries(
        DenseTrajectorySource protectedDense,
        DenseTrajectorySource secondaryDense,
        Window window,
        double toleranceSeconds)
    {
        var boundaries = new List<Time> { window.StartDate, window.EndDate };
        AddDenseStepBoundaries(boundaries, protectedDense, window);
        AddDenseStepBoundaries(boundaries, secondaryDense, window);
        return DistinctEpochs(boundaries, toleranceSeconds);
    }

    private static void AddDenseStepBoundaries(
        List<Time> boundaries,
        DenseTrajectorySource denseTrajectory,
        Window window)
    {
        foreach (var step in denseTrajectory.Steps)
        {
            if (step.StartEpoch > window.StartDate && step.StartEpoch < window.EndDate)
            {
                boundaries.Add(step.StartEpoch);
            }

            if (step.EndEpoch > window.StartDate && step.EndEpoch < window.EndDate)
            {
                boundaries.Add(step.EndEpoch);
            }
        }
    }

    private static VectorPolynomial BuildRelativePositionPolynomial(
        DenseTrajectoryStep protectedStep,
        DenseTrajectoryStep secondaryStep,
        Time intervalStart,
        double intervalDurationSeconds)
    {
        var protectedPolynomial = BuildPositionPolynomial(protectedStep, intervalStart, intervalDurationSeconds);
        var secondaryPolynomial = BuildPositionPolynomial(secondaryStep, intervalStart, intervalDurationSeconds);
        return new VectorPolynomial(
            SubtractPolynomials(secondaryPolynomial.X, protectedPolynomial.X),
            SubtractPolynomials(secondaryPolynomial.Y, protectedPolynomial.Y),
            SubtractPolynomials(secondaryPolynomial.Z, protectedPolynomial.Z));
    }

    private static VectorPolynomial BuildPositionPolynomial(
        DenseTrajectoryStep denseStep,
        Time intervalStart,
        double intervalDurationSeconds)
    {
        double stepSize = denseStep.Step.StepSize;
        // In the seconds of the time argument from the start of the step (#501); the scale, intervalDurationSeconds, is
        // in seconds of the labels, which differ from TT seconds by 3.4e-10 at most
        double intervalOffsetSeconds = denseStep.TimeArgument.SecondsIntoStep(denseStep.SegmentBaseEpoch,
            denseStep.StartEpoch, denseStep.Step.CumulativeTime, intervalStart);
        double affineConstant = intervalOffsetSeconds / stepSize;
        double affineScale = intervalDurationSeconds / stepSize;

        var h00 = ComposeAffinePolynomial(new[] { 1.0, 0.0, -3.0, 2.0 }, affineConstant, affineScale);
        var h10 = ComposeAffinePolynomial(new[] { 0.0, stepSize, -2.0 * stepSize, stepSize }, affineConstant, affineScale);
        var h01 = ComposeAffinePolynomial(new[] { 0.0, 0.0, 3.0, -2.0 }, affineConstant, affineScale);
        var h11 = ComposeAffinePolynomial(new[] { 0.0, 0.0, -stepSize, stepSize }, affineConstant, affineScale);

        return new VectorPolynomial(
            BuildHermiteComponentPolynomial(
                denseStep.Step.StartPosition.X,
                denseStep.Step.StartVelocity.X,
                denseStep.Step.EndPosition.X,
                denseStep.Step.EndVelocity.X,
                h00,
                h10,
                h01,
                h11),
            BuildHermiteComponentPolynomial(
                denseStep.Step.StartPosition.Y,
                denseStep.Step.StartVelocity.Y,
                denseStep.Step.EndPosition.Y,
                denseStep.Step.EndVelocity.Y,
                h00,
                h10,
                h01,
                h11),
            BuildHermiteComponentPolynomial(
                denseStep.Step.StartPosition.Z,
                denseStep.Step.StartVelocity.Z,
                denseStep.Step.EndPosition.Z,
                denseStep.Step.EndVelocity.Z,
                h00,
                h10,
                h01,
                h11));
    }

    private static double[] BuildHermiteComponentPolynomial(
        double startPosition,
        double startVelocity,
        double endPosition,
        double endVelocity,
        double[] h00,
        double[] h10,
        double[] h01,
        double[] h11)
    {
        return AddPolynomials(
            AddPolynomials(ScalePolynomial(h00, startPosition), ScalePolynomial(h10, startVelocity)),
            AddPolynomials(ScalePolynomial(h01, endPosition), ScalePolynomial(h11, endVelocity)));
    }

    private static double[] BuildSquaredMagnitudePolynomial(VectorPolynomial vectorPolynomial)
    {
        return AddPolynomials(
            AddPolynomials(
                MultiplyPolynomials(vectorPolynomial.X, vectorPolynomial.X),
                MultiplyPolynomials(vectorPolynomial.Y, vectorPolynomial.Y)),
            MultiplyPolynomials(vectorPolynomial.Z, vectorPolynomial.Z));
    }

    private static List<double> FindPolynomialRootsInUnitInterval(double[] coefficients, double domainTolerance)
    {
        coefficients = TrimPolynomial(coefficients);
        if (coefficients.Length <= 1)
        {
            return new List<double>();
        }

        if (coefficients.Length == 2)
        {
            double slope = coefficients[1];
            if (System.Math.Abs(slope) <= ComputeCoefficientTolerance(coefficients))
            {
                return new List<double>();
            }

            double root = -coefficients[0] / slope;
            if (root >= -domainTolerance && root <= 1.0 + domainTolerance)
            {
                return new List<double> { System.Math.Clamp(root, 0.0, 1.0) };
            }

            return new List<double>();
        }

        double[] derivative = Differentiate(coefficients);
        var criticalPoints = FindPolynomialRootsInUnitInterval(derivative, domainTolerance);
        var partitions = new List<double> { 0.0 };
        foreach (var point in criticalPoints.Where(point => point > domainTolerance && point < 1.0 - domainTolerance))
        {
            AddDistinctScalar(partitions, point, domainTolerance);
        }

        partitions.Add(1.0);
        partitions.Sort();

        var roots = new List<double>();
        double valueTolerance = ComputePolynomialValueTolerance(coefficients);

        foreach (var boundary in partitions)
        {
            double value = EvaluatePolynomial(coefficients, boundary);
            if (System.Math.Abs(value) <= valueTolerance)
            {
                AddDistinctScalar(roots, boundary, domainTolerance);
            }
        }

        for (int i = 1; i < partitions.Count; i++)
        {
            double left = partitions[i - 1];
            double right = partitions[i];
            if (right - left <= domainTolerance)
            {
                continue;
            }

            double leftValue = EvaluatePolynomial(coefficients, left);
            double rightValue = EvaluatePolynomial(coefficients, right);
            if (System.Math.Abs(leftValue) <= valueTolerance || System.Math.Abs(rightValue) <= valueTolerance)
            {
                continue;
            }

            if (leftValue * rightValue < 0.0)
            {
                double root = BisectPolynomialRoot(coefficients, left, right, domainTolerance, valueTolerance);
                AddDistinctScalar(roots, root, domainTolerance);
            }
        }

        roots.Sort();
        return roots;
    }

    private static double BisectPolynomialRoot(
        double[] coefficients,
        double left,
        double right,
        double domainTolerance,
        double valueTolerance)
    {
        double leftValue = EvaluatePolynomial(coefficients, left);
        for (int iteration = 0; iteration < 80; iteration++)
        {
            double midpoint = 0.5 * (left + right);
            double midpointValue = EvaluatePolynomial(coefficients, midpoint);
            if (System.Math.Abs(midpointValue) <= valueTolerance || right - left <= domainTolerance)
            {
                return midpoint;
            }

            if (leftValue * midpointValue < 0.0)
            {
                right = midpoint;
            }
            else
            {
                left = midpoint;
                leftValue = midpointValue;
            }
        }

        return 0.5 * (left + right);
    }

    private static double EvaluatePolynomial(double[] coefficients, double value)
    {
        double result = 0.0;
        for (int i = coefficients.Length - 1; i >= 0; i--)
        {
            result = result * value + coefficients[i];
        }

        return result;
    }

    private static double[] Differentiate(double[] coefficients)
    {
        if (coefficients.Length <= 1)
        {
            return new[] { 0.0 };
        }

        var derivative = new double[coefficients.Length - 1];
        for (int i = 1; i < coefficients.Length; i++)
        {
            derivative[i - 1] = coefficients[i] * i;
        }

        return TrimPolynomial(derivative);
    }

    private static double[] ComposeAffinePolynomial(double[] coefficients, double constant, double scale)
    {
        var result = new[] { 0.0 };
        for (int degree = coefficients.Length - 1; degree >= 0; degree--)
        {
            result = MultiplyPolynomials(result, new[] { constant, scale });
            result[0] += coefficients[degree];
        }

        return TrimPolynomial(result);
    }

    private static double[] MultiplyPolynomials(double[] left, double[] right)
    {
        var result = new double[left.Length + right.Length - 1];
        for (int leftIndex = 0; leftIndex < left.Length; leftIndex++)
        {
            for (int rightIndex = 0; rightIndex < right.Length; rightIndex++)
            {
                result[leftIndex + rightIndex] += left[leftIndex] * right[rightIndex];
            }
        }

        return TrimPolynomial(result);
    }

    private static double[] AddPolynomials(double[] left, double[] right)
    {
        int degree = System.Math.Max(left.Length, right.Length);
        var result = new double[degree];
        for (int index = 0; index < degree; index++)
        {
            double leftValue = index < left.Length ? left[index] : 0.0;
            double rightValue = index < right.Length ? right[index] : 0.0;
            result[index] = leftValue + rightValue;
        }

        return TrimPolynomial(result);
    }

    private static double[] SubtractPolynomials(double[] left, double[] right)
    {
        int degree = System.Math.Max(left.Length, right.Length);
        var result = new double[degree];
        for (int index = 0; index < degree; index++)
        {
            double leftValue = index < left.Length ? left[index] : 0.0;
            double rightValue = index < right.Length ? right[index] : 0.0;
            result[index] = leftValue - rightValue;
        }

        return TrimPolynomial(result);
    }

    private static double[] ScalePolynomial(double[] coefficients, double scale)
    {
        var result = new double[coefficients.Length];
        for (int index = 0; index < coefficients.Length; index++)
        {
            result[index] = coefficients[index] * scale;
        }

        return TrimPolynomial(result);
    }

    private static double[] TrimPolynomial(double[] coefficients)
    {
        int degree = coefficients.Length - 1;
        double tolerance = ComputeCoefficientTolerance(coefficients);
        while (degree > 0 && System.Math.Abs(coefficients[degree]) <= tolerance)
        {
            degree--;
        }

        if (degree == coefficients.Length - 1)
        {
            return coefficients;
        }

        var trimmed = new double[degree + 1];
        Array.Copy(coefficients, trimmed, degree + 1);
        return trimmed;
    }

    private static bool IsEffectivelyZeroPolynomial(double[] coefficients)
    {
        double tolerance = ComputeCoefficientTolerance(coefficients);
        return coefficients.All(coefficient => System.Math.Abs(coefficient) <= tolerance);
    }

    private static double ComputeCoefficientTolerance(double[] coefficients)
    {
        double scale = coefficients.Length == 0 ? 1.0 : coefficients.Max(coefficient => System.Math.Abs(coefficient));
        return System.Math.Max(1.0, scale) * 1.0e-14;
    }

    private static double ComputePolynomialValueTolerance(double[] coefficients)
    {
        double scale = coefficients.Length == 0 ? 1.0 : coefficients.Max(coefficient => System.Math.Abs(coefficient));
        return System.Math.Max(1.0, scale) * 1.0e-12;
    }

    private static void AddDistinctScalar(List<double> values, double value, double tolerance)
    {
        if (values.Any(existing => System.Math.Abs(existing - value) <= tolerance))
        {
            return;
        }

        values.Add(value);
    }

    private static List<Time> DistinctEpochs(IEnumerable<Time> epochs, double toleranceSeconds)
    {
        var orderedEpochs = epochs.OrderBy(epoch => epoch).ToArray();
        var distinctEpochs = new List<Time>(orderedEpochs.Length);
        foreach (var epoch in orderedEpochs)
        {
            if (distinctEpochs.Count == 0 ||
                System.Math.Abs((epoch - distinctEpochs[^1]).TotalSeconds) > System.Math.Max(toleranceSeconds, 1.0e-9))
            {
                distinctEpochs.Add(epoch);
            }
        }

        return distinctEpochs;
    }

    private static List<Time> SelectLocalMinimumEpochs(
        IReadOnlyList<Time> candidateEpochs,
        Func<Time, double> distanceSquared,
        double toleranceSeconds)
    {
        if (candidateEpochs.Count < 2)
        {
            return new List<Time>();
        }

        var missDistanceSquared = candidateEpochs.Select(distanceSquared).ToArray();
        double comparisonTolerance = System.Math.Max(
            1.0e-6,
            missDistanceSquared.Max(distance => System.Math.Abs(distance)) * 1.0e-12);

        var minima = new List<Time>();
        for (int index = 0; index < candidateEpochs.Count; index++)
        {
            bool lowerThanPrevious = index == 0 || missDistanceSquared[index] <= missDistanceSquared[index - 1] + comparisonTolerance;
            bool lowerThanNext = index == candidateEpochs.Count - 1 || missDistanceSquared[index] <= missDistanceSquared[index + 1] + comparisonTolerance;
            bool strictlyLowerNeighbor =
                (index > 0 && missDistanceSquared[index] + comparisonTolerance < missDistanceSquared[index - 1]) ||
                (index < candidateEpochs.Count - 1 && missDistanceSquared[index] + comparisonTolerance < missDistanceSquared[index + 1]);

            if (lowerThanPrevious && lowerThanNext && strictlyLowerNeighbor)
            {
                AddDistinctEpoch(minima, candidateEpochs[index], toleranceSeconds);
            }
        }

        return minima;
    }

    // --- Observer-aware state query ---

    private static StateVector GetState(ILocalizable localizable, Time epoch, ILocalizable observer)
    {
        return localizable.GetEphemeris(epoch, observer, Frame.ICRF, Aberration.None).ToStateVector();
    }

    // --- Core encounter building ---

    private static EncounterCase BuildEncounterCase(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        Time tca,
        ConjunctionAnalysisOptions options,
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        EncounterCovarianceResolver protectedResolver,
        EncounterCovarianceResolver secondaryResolver)
    {
        var qualityFlags = EncounterQualityFlags.None;
        var protectedState = protectedSource.GetState(tca);
        var secondaryState = secondarySource.GetState(tca);
        var relativeState = BuildRelativeState(protectedState, secondaryState);
        if (options.LowRelativeSpeedThresholdMetersPerSecond > 0.0 &&
            relativeState.RelativeVelocityInertial.Magnitude() < options.LowRelativeSpeedThresholdMetersPerSecond)
        {
            qualityFlags |= EncounterQualityFlags.LowRelativeVelocityEncounter;
        }

        // The covariance at the final TCA only, propagated when the conditions of E2 hold (E1)
        var protectedCovariance = protectedResolver.Resolve(protectedState);
        var secondaryCovariance = secondaryResolver.Resolve(secondaryState);
        protectedState = WithCovarianceUsed(protectedState, protectedCovariance);
        secondaryState = WithCovarianceUsed(secondaryState, secondaryCovariance);
        if (protectedCovariance.IsStale(options.StaleCovarianceThreshold) ||
            secondaryCovariance.IsStale(options.StaleCovarianceThreshold))
        {
            qualityFlags |= EncounterQualityFlags.StaleCovarianceUsed;
        }

        // Beyond the domain of the linear propagation, measured against a Monte Carlo of the probability (the guard)
        if (protectedCovariance.NonlinearityIndex > options.NonlinearityIndexThreshold ||
            secondaryCovariance.NonlinearityIndex > options.NonlinearityIndexThreshold)
        {
            qualityFlags |= EncounterQualityFlags.NonlinearCovariancePropagation;
        }

        var combinedCovarianceIcrf = BuildCombinedCovarianceIcrf(protectedCovariance, secondaryCovariance, ref qualityFlags);

        var combinedCovarianceRtn = RotateCovarianceToRtn(combinedCovarianceIcrf, protectedState);

        if (qualityFlags.HasFlag(EncounterQualityFlags.MissingProtectedCovariance) &&
            qualityFlags.HasFlag(EncounterQualityFlags.MissingSecondaryCovariance))
        {
            qualityFlags |= EncounterQualityFlags.CovarianceUnavailable;
        }

        var collisionRisk = BuildCollisionRisk(
            relativeState,
            combinedCovarianceIcrf,
            combinedCovarianceRtn,
            protectedAsset.Spacecraft.HardBodyRadius + options.SecondaryHardBodyRadiusMeters,
            ref qualityFlags);

        var encounterState = new EncounterState(
            tca,
            relativeState,
            relativeState.RelativePositionInertial.Magnitude(),
            combinedCovarianceRtn,
            protectedCovariance.ToParticipantCovariance(),
            secondaryCovariance.ToParticipantCovariance(),
            qualityFlags);

        return new EncounterCase(
            protectedAsset,
            secondaryObject,
            screeningWindow,
            encounterState,
            collisionRisk,
            protectedState,
            secondaryState)
        {
            ProtectedCovariance = protectedCovariance,
            SecondaryCovariance = secondaryCovariance,
            ProtectedTrajectory = protectedSource.Solution
        };
    }

    // The state retained by the encounter case carries the covariance the analysis used when it was propagated, so that
    // the CDM exports what the probability used; the other states are left as the source gave them
    private static StateVector WithCovarianceUsed(StateVector state, EncounterCovariance covariance)
    {
        return covariance.Source == CovarianceSource.Propagated
            ? new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
                covariance.CovarianceInertial)
            : state;
    }

    private static double CaseProbabilitySortKey(EncounterCase encounterCase)
    {
        return encounterCase.CollisionRisk?.ProbabilityOfCollision ?? double.NegativeInfinity;
    }

    private static RelativeState BuildRelativeState(StateVector primaryState, StateVector secondaryState)
    {
        var deltaPosition = secondaryState.Position - primaryState.Position;
        var deltaVelocity = secondaryState.Velocity - primaryState.Velocity;

        return new RelativeState(
            primaryState.Epoch,
            deltaPosition,
            deltaVelocity,
            primaryState.ToRtn(deltaPosition),
            primaryState.ToRtn(deltaVelocity));
    }

    // --- Covariance handling ---

    private static Matrix BuildCombinedCovarianceIcrf(
        EncounterCovariance protectedCovariance,
        EncounterCovariance secondaryCovariance,
        ref EncounterQualityFlags qualityFlags)
    {
        var combined = new Matrix(6, 6);
        bool hasCovariance = false;

        if (protectedCovariance.CovarianceInertial.HasValue)
        {
            combined = protectedCovariance.CovarianceInertial.Value;
            hasCovariance = true;
        }
        else
        {
            qualityFlags |= EncounterQualityFlags.MissingProtectedCovariance;
        }

        if (secondaryCovariance.CovarianceInertial.HasValue)
        {
            combined = combined + secondaryCovariance.CovarianceInertial.Value;
            hasCovariance = true;
        }
        else
        {
            qualityFlags |= EncounterQualityFlags.MissingSecondaryCovariance;
        }

        // An initial covariance remediated before its propagation (H3 review of step 10, N2)
        if (protectedCovariance.InitialCovarianceRemediated || secondaryCovariance.InitialCovarianceRemediated)
        {
            qualityFlags |= EncounterQualityFlags.CovarianceRemediated;
        }

        return hasCovariance ? combined : new Matrix(6, 6);
    }

    private static Matrix RotateCovarianceToRtn(Matrix covarianceIcrf, StateVector referenceState)
    {
        return referenceState.RotateCovarianceToRtn(covarianceIcrf);
    }

    // --- Encounter-plane projection ---

    private static CollisionRisk BuildCollisionRisk(
        RelativeState relativeState,
        Matrix combinedCovarianceIcrf,
        Matrix combinedCovarianceRtn,
        double combinedHardBodyRadiusMeters,
        ref EncounterQualityFlags qualityFlags)
    {
        double sigmaR = combinedCovarianceRtn.Rows == 6 ? SafeSqrt(combinedCovarianceRtn.Get(0, 0)) : 0.0;
        double sigmaT = combinedCovarianceRtn.Rows == 6 ? SafeSqrt(combinedCovarianceRtn.Get(1, 1)) : 0.0;
        double sigmaN = combinedCovarianceRtn.Rows == 6 ? SafeSqrt(combinedCovarianceRtn.Get(2, 2)) : 0.0;
        bool hasCov = !qualityFlags.HasFlag(EncounterQualityFlags.CovarianceUnavailable) &&
                      combinedCovarianceIcrf.Rows == 6;
        bool singleCovarianceOnly =
            qualityFlags.HasFlag(EncounterQualityFlags.MissingProtectedCovariance) ^
            qualityFlags.HasFlag(EncounterQualityFlags.MissingSecondaryCovariance);

        Matrix projectedCovariance;
        double? probability = null;

        if (hasCov)
        {
            var posCov = ExtractPositionCovariance(combinedCovarianceIcrf);

            var (miss2D, cov2D) = ProjectOntoEncounterPlane(
                relativeState.RelativePositionInertial,
                relativeState.RelativeVelocityInertial,
                posCov);

            if (singleCovarianceOnly)
            {
                var maximumPcCovariance = PrepareMaximumProbabilityEncounterPlaneCovariance(
                    miss2D,
                    cov2D,
                    combinedHardBodyRadiusMeters,
                    out bool remediatedKnownCovariance,
                    out bool remediatedMaximumCovariance);
                if (maximumPcCovariance is { } maximumCovariance)
                {
                    if (remediatedKnownCovariance || remediatedMaximumCovariance)
                    {
                        qualityFlags |= EncounterQualityFlags.CovarianceRemediated;
                    }

                    qualityFlags |= EncounterQualityFlags.SingleCovarianceMaximumPcUsed;
                    projectedCovariance = maximumCovariance.ToMatrix();
                    probability = ComputeFosterCollisionProbabilityCore(miss2D, maximumCovariance, combinedHardBodyRadiusMeters);
                }
                else
                {
                    projectedCovariance = new Matrix(2, 2);
                }
            }
            else
            {
                var preparedCovariance = PrepareEncounterPlaneCovariance(cov2D, combinedHardBodyRadiusMeters, out bool remediated);
                if (preparedCovariance is { } prepared)
                {
                    if (remediated)
                    {
                        qualityFlags |= EncounterQualityFlags.CovarianceRemediated;
                    }

                    projectedCovariance = prepared.ToMatrix();
                    probability = ComputeFosterCollisionProbabilityCore(miss2D, prepared, combinedHardBodyRadiusMeters);
                }
                else
                {
                    projectedCovariance = new Matrix(2, 2);
                }
            }
        }
        else
        {
            projectedCovariance = new Matrix(2, 2);
        }

        return new CollisionRisk(
            combinedHardBodyRadiusMeters,
            probability,
            projectedCovariance,
            sigmaR,
            sigmaT,
            sigmaN);
    }

    private static Matrix ExtractPositionCovariance(Matrix covariance)
    {
        var pos = new Matrix(3, 3);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                pos.Set(i, j, covariance.Get(i, j));
            }
        }

        return pos;
    }

    internal static (double[] miss2D, Matrix cov2D) ProjectOntoEncounterPlane(
        Vector3 relPosInertial,
        Vector3 relVelInertial,
        Matrix posCov)
    {
        double relVelMag = relVelInertial.Magnitude();

        Vector3 e1, e2;
        if (relVelMag < 1e-10)
        {
            e1 = Vector3.VectorX;
            e2 = Vector3.VectorY;
        }
        else
        {
            var vHat = relVelInertial * (1.0 / relVelMag);
            double missAlongV = relPosInertial * vHat;
            var missInPlane = relPosInertial - vHat * missAlongV;
            double missInPlaneMag = missInPlane.Magnitude();

            if (missInPlaneMag < 1e-10)
            {
                var seed = System.Math.Abs(vHat.X) < 0.9 ? Vector3.VectorX : Vector3.VectorY;
                e1 = (seed - vHat * (seed * vHat)).Normalize();
                e2 = vHat.Cross(e1).Normalize();
            }
            else
            {
                e1 = missInPlane * (1.0 / missInPlaneMag);
                e2 = vHat.Cross(e1).Normalize();
            }
        }

        double m1 = relPosInertial * e1;
        double m2 = relPosInertial * e2;

        // C2D = P * C3x3 * P^T where P is 2x3 [e1; e2]
        var proj = new Matrix(2, 3);
        proj.Set(0, 0, e1.X); proj.Set(0, 1, e1.Y); proj.Set(0, 2, e1.Z);
        proj.Set(1, 0, e2.X); proj.Set(1, 1, e2.Y); proj.Set(1, 2, e2.Z);

        var cov2D = proj * posCov * proj.Transpose();
        return (new[] { m1, m2 }, cov2D);
    }

    // --- Foster collision probability (CARA's Pc2D) ---

    internal static double? ComputeFosterCollisionProbability(
        double[] miss2D,
        Matrix cov2D,
        double combinedRadius)
    {
        var preparedCovariance = PrepareEncounterPlaneCovariance(cov2D, combinedRadius, out _);
        if (preparedCovariance is null)
        {
            return null;
        }

        return ComputeFosterCollisionProbabilityCore(miss2D, preparedCovariance.Value, combinedRadius);
    }

    internal static double? ComputeSingleCovarianceMaximumCollisionProbability(
        double[] miss2D,
        Matrix knownCov2D,
        double combinedRadius)
    {
        var maximumPcCovariance = PrepareMaximumProbabilityEncounterPlaneCovariance(
            miss2D,
            knownCov2D,
            combinedRadius,
            out _,
            out _);
        if (maximumPcCovariance is null)
        {
            return null;
        }

        return ComputeFosterCollisionProbabilityCore(miss2D, maximumPcCovariance.Value, combinedRadius);
    }

    private static EncounterPlaneGaussian? PrepareMaximumProbabilityEncounterPlaneCovariance(
        double[] miss2D,
        Matrix knownCov2D,
        double combinedRadius,
        out bool remediatedKnownCovariance,
        out bool remediatedMaximumCovariance)
    {
        remediatedMaximumCovariance = false;
        var preparedKnownCovariance = PrepareEncounterPlaneCovariance(knownCov2D, combinedRadius, out remediatedKnownCovariance);
        if (preparedKnownCovariance is null || miss2D.Length != 2)
        {
            return null;
        }

        var criticalCovariance = preparedKnownCovariance.Value.WithFrisbeeCriticalCovariance(miss2D[0], miss2D[1]);
        if (criticalCovariance is null)
        {
            return null;
        }

        return ClipEncounterPlaneCovariance(criticalCovariance.Value, combinedRadius, out remediatedMaximumCovariance);
    }

    internal static Matrix? BuildMaximumProbabilityEncounterPlaneCovariance(
        double[] miss2D,
        Matrix knownCov2D)
    {
        if (miss2D.Length != 2)
        {
            return null;
        }

        var known = EncounterPlaneGaussian.FromCovariance(knownCov2D);
        if (known is null)
        {
            return null;
        }

        var critical = known.Value.WithFrisbeeCriticalCovariance(miss2D[0], miss2D[1]);
        if (critical is null)
        {
            return null;
        }

        return critical.Value.Equals(known.Value) ? knownCov2D : critical.Value.ToMatrix();
    }

    /// <summary>
    /// The principal axes of the encounter-plane covariance, each variance raised to at least (1e-4 R)², CARA's clip
    /// (<paramref name="remediated"/> when one is raised); not clipped when R ≤ 0; null when the covariance is not 2×2,
    /// R or an element is not finite.
    /// </summary>
    private static EncounterPlaneGaussian? PrepareEncounterPlaneCovariance(
        Matrix cov2D,
        double combinedRadius,
        out bool remediated)
    {
        remediated = false;
        if (!double.IsFinite(combinedRadius))
        {
            return null;
        }

        var gaussian = EncounterPlaneGaussian.FromCovariance(cov2D);
        if (gaussian is null)
        {
            return null;
        }

        return ClipEncounterPlaneCovariance(gaussian.Value, combinedRadius, out remediated);
    }

    private static EncounterPlaneGaussian ClipEncounterPlaneCovariance(EncounterPlaneGaussian gaussian, double combinedRadius, out bool remediated)
    {
        if (combinedRadius <= 0.0)
        {
            remediated = false;
            return gaussian;
        }

        return gaussian.Clip(System.Math.Pow(1.0e-4 * combinedRadius, 2), out remediated);
    }

    private static double? ComputeFosterCollisionProbabilityCore(
        double[] miss2D,
        EncounterPlaneGaussian covariance,
        double combinedRadius)
    {
        if (combinedRadius <= 0.0)
        {
            return 0.0;
        }

        if (!(covariance.MajorVariance > 0.0) || !(covariance.MinorVariance > 0.0))
        {
            return null;
        }

        var (xPrincipal, yPrincipal) = covariance.ToPrincipal(miss2D[0], miss2D[1]);
        return EncounterPlaneProbability.Integrate(
            xPrincipal,
            yPrincipal,
            covariance.MajorVariance,
            covariance.MinorVariance,
            combinedRadius);
    }

    private static IEnumerable<Vector3> BuildBurnDirections()
    {
        yield return Vector3.VectorX;
        yield return Vector3.VectorX.Inverse();
        yield return Vector3.VectorY;
        yield return Vector3.VectorY.Inverse();
        yield return Vector3.VectorZ;
        yield return Vector3.VectorZ.Inverse();
    }

    private static TimeSpan ComputeCloseApproachSearchStep(TimeSpan sampleStep, TimeSpan maximumEventSearchStep, double bisectionToleranceSeconds)
    {
        double searchStepSeconds = System.Math.Min(sampleStep.TotalSeconds / 16.0, maximumEventSearchStep.TotalSeconds);
        searchStepSeconds = System.Math.Max(searchStepSeconds, bisectionToleranceSeconds * 4.0);
        searchStepSeconds = System.Math.Min(searchStepSeconds, sampleStep.TotalSeconds);
        return TimeSpan.FromSeconds(searchStepSeconds);
    }

    private static List<Time> BuildSearchEpochs(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        Window window,
        TimeSpan sampleStep,
        TimeSpan maximumEventSearchStep,
        double bisectionToleranceSeconds)
    {
        var denseKnots = protectedSource.SearchKnots.Concat(secondarySource.SearchKnots)
            .Where(epoch => epoch > window.StartDate && epoch < window.EndDate)
            .OrderBy(epoch => epoch)
            .ToArray();

        if (denseKnots.Length == 0)
        {
            return BuildUniformSearchEpochs(window, ComputeCloseApproachSearchStep(sampleStep, maximumEventSearchStep, bisectionToleranceSeconds));
        }

        var epochs = new List<Time> { window.StartDate };
        var boundaries = new List<Time> { window.StartDate };
        boundaries.AddRange(denseKnots);
        boundaries.Add(window.EndDate);

        for (int i = 1; i < boundaries.Count; i++)
        {
            var left = boundaries[i - 1];
            var right = boundaries[i];
            double intervalSeconds = (right - left).TotalSeconds;
            if (intervalSeconds > maximumEventSearchStep.TotalSeconds)
            {
                int subdivisions = (int)System.Math.Ceiling(intervalSeconds / maximumEventSearchStep.TotalSeconds);
                for (int subdivision = 1; subdivision < subdivisions; subdivision++)
                {
                    epochs.Add(left + TimeSpan.FromSeconds(intervalSeconds * subdivision / subdivisions));
                }
            }

            if (right > epochs[^1])
            {
                epochs.Add(right);
            }
        }

        return epochs;
    }

    private static List<Time> BuildUniformSearchEpochs(Window window, TimeSpan searchStep)
    {
        var epochs = new List<Time>();
        var currentEpoch = window.StartDate;
        while (currentEpoch < window.EndDate)
        {
            epochs.Add(currentEpoch);
            currentEpoch += searchStep;
        }

        if (epochs.Count == 0 || epochs[^1] < window.EndDate)
        {
            epochs.Add(window.EndDate);
        }

        return epochs;
    }

    private static void AddDistinctEpoch(List<Time> epochs, Time epoch, double toleranceSeconds)
    {
        if (epochs.Count == 0)
        {
            epochs.Add(epoch);
            return;
        }

        double separationSeconds = System.Math.Abs((epoch - epochs[^1]).TotalSeconds);
        if (separationSeconds > System.Math.Max(toleranceSeconds, 1.0e-6))
        {
            epochs.Add(epoch);
        }
    }

    private static void ValidateAnalysisOptions(ConjunctionAnalysisOptions options)
    {
        if (options.SampleStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SampleStep), "Sample step must be positive.");
        }

        if (options.MaximumEventSearchStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumEventSearchStep), "Maximum event search step must be positive.");
        }

        if (options.CovariancePropagation is { } covariance)
        {
            if (covariance.Mode != CovariancePropagationMode.Covariance)
            {
                throw new ArgumentException(
                    "The conjunction analysis propagates the covariance: CovariancePropagation.Mode must be " +
                    $"{nameof(CovariancePropagationMode.Covariance)}, or the options null to hold the initial covariance " +
                    "in RTN.", nameof(options));
            }

            if (covariance.IncludeInStepControl)
            {
                throw new ArgumentException(
                    "The conjunction analysis replays the variational equations along the steps of the propagation of each " +
                    "participant, controlled on the state only: IncludeInStepControl cannot be set. Propagate the " +
                    "participant with the covariance and this option instead, and pass its solution.", nameof(options));
            }
        }

        if (!(options.NonlinearityIndexThreshold > 0.0) || double.IsInfinity(options.NonlinearityIndexThreshold))
        {
            throw new ArgumentOutOfRangeException(nameof(options.NonlinearityIndexThreshold),
                "The non-linearity index threshold must be positive and finite.");
        }
    }

    // The ILocalizable overloads read the stored states of a propagated spacecraft, which must cover the window (#527), as
    // a trajectory must (ValidateTrajectoryCoverage)
    private static void CheckStatesCover(Spacecraft protectedSpacecraft, ILocalizable secondaryObject, in Window window)
    {
        protectedSpacecraft.CheckInterpolatedStatesCover(window, "screening window", "screeningWindow");
        (secondaryObject as Spacecraft)?.CheckInterpolatedStatesCover(window, "screening window", "screeningWindow");
    }

    private static void ValidateTrajectoryCoverage(PropagationSolution trajectory, Window window, string paramName)
    {
        ArgumentNullException.ThrowIfNull(trajectory);
        if (trajectory.Segments.Count == 0)
        {
            throw new ArgumentException("Trajectory must contain at least one propagation segment.", paramName);
        }

        // In the seconds of the time argument of the trajectory (#507): a window in another frame than the TDB labels
        // of the trajectory compares with them through TAI, one tick off at some instants
        if (!trajectory.Covers(window.StartDate, PropagationSolution.EpochTick) ||
            !trajectory.Covers(window.EndDate, PropagationSolution.EpochTick))
        {
            throw new ArgumentException("Trajectory must cover the entire screening window.", paramName);
        }
    }

    private static StateSamplingSource CreateLocalizableStateSource(ILocalizable localizable, ILocalizable observer)
    {
        // A spacecraft keeps its last propagation, whose output states its ephemeris interpolates
        return new StateSamplingSource(
            epoch => GetState(localizable, epoch, observer),
            Array.Empty<Time>(),
            solution: (localizable as Spacecraft)?.LastPropagation);
    }

    private static StateSamplingSource CreateTrajectoryStateSource(
        ILocalizable localizable,
        PropagationSolution trajectory,
        ILocalizable observer,
        Window screeningWindow)
    {
        var trajectoryObserver = ResolveTrajectoryObserver(localizable, trajectory);
        var trajectoryFrame = ResolveTrajectoryFrame(localizable, trajectory);
        var searchKnots = CollectTrajectoryKnots(trajectory, screeningWindow);
        DenseTrajectorySource denseTrajectory = null;
        if (trajectoryObserver.NaifId == observer.NaifId)
        {
            denseTrajectory = new DenseTrajectorySource(
                trajectoryObserver,
                trajectoryFrame,
                CollectTrajectorySteps(trajectory, screeningWindow));
        }

        return new StateSamplingSource(
            epoch =>
            {
                var (position, velocity) = trajectory.InterpolateAt(epoch);
                var state = new StateVector(position, velocity, trajectoryObserver, epoch, trajectoryFrame);
                return trajectoryObserver.NaifId == observer.NaifId
                    ? state
                    : state.RelativeTo(observer, Aberration.None).ToStateVector();
            },
            searchKnots,
            denseTrajectory,
            trajectory);
    }

    private static ILocalizable ResolveTrajectoryObserver(ILocalizable localizable, PropagationSolution trajectory)
    {
        if (trajectory.StateVectors.Count > 0)
        {
            return trajectory.StateVectors[0].Observer;
        }

        return localizable.InitialOrbitalParameters.Observer;
    }

    private static Frame ResolveTrajectoryFrame(ILocalizable localizable, PropagationSolution trajectory)
    {
        if (trajectory.StateVectors.Count > 0)
        {
            return trajectory.StateVectors[0].Frame;
        }

        return localizable.InitialOrbitalParameters.Frame;
    }

    private static IReadOnlyList<Time> CollectTrajectoryKnots(PropagationSolution trajectory, Window screeningWindow)
    {
        var knots = new List<Time>();
        foreach (var segment in trajectory.Segments)
        {
            foreach (var step in segment.Steps)
            {
                var (stepStart, stepEnd) =
                    trajectory.TimeArgument.StepEpochs(segment.BaseEpoch, step.CumulativeTime, step.StepSize);
                if (stepStart >= screeningWindow.StartDate && stepStart <= screeningWindow.EndDate)
                {
                    knots.Add(stepStart);
                }

                if (stepEnd >= screeningWindow.StartDate && stepEnd <= screeningWindow.EndDate)
                {
                    knots.Add(stepEnd);
                }
            }
        }

        return knots
            .Distinct()
            .OrderBy(epoch => epoch)
            .ToArray();
    }

    private static IReadOnlyList<DenseTrajectoryStep> CollectTrajectorySteps(
        PropagationSolution trajectory,
        Window screeningWindow)
    {
        var steps = new List<DenseTrajectoryStep>();
        foreach (var segment in trajectory.Segments)
        {
            foreach (var step in segment.Steps)
            {
                var (stepStart, stepEnd) =
                    trajectory.TimeArgument.StepEpochs(segment.BaseEpoch, step.CumulativeTime, step.StepSize);
                if (stepEnd < screeningWindow.StartDate || stepStart > screeningWindow.EndDate)
                {
                    continue;
                }

                steps.Add(new DenseTrajectoryStep(segment.BaseEpoch, step, stepStart, stepEnd, trajectory.TimeArgument));
            }
        }

        return steps
            .OrderBy(step => step.StartEpoch)
            .ToArray();
    }

    private static Spacecraft CloneSpacecraft(Spacecraft template, StateVector initialState)
    {
        return CloneSpacecraft(template, initialState, template.DryOperatingMass, template.DragCoefficient,
            template.SolarRadiationCoeff);
    }

    // The trial spacecraft of an avoidance option: the template with the given mass, without tanks or engines, and the
    // given coefficients
    private static Spacecraft CloneSpacecraft(Spacecraft template, StateVector initialState, double mass,
        double dragCoefficient, double reflectivityCoefficient)
    {
        int trialId = TrialIdFirst - (int)((uint)Interlocked.Increment(ref _trialIdCounter) % TrialIdCount);
        return new Spacecraft(
            trialId,
            $"{template.Name}_SSA_TRIAL",
            mass,
            System.Math.Max(template.MaximumOperatingMass, mass),
            new Clock($"{template.Clock.Name}_SSA", template.Clock.Resolution),
            initialState,
            template.SectionalArea,
            dragCoefficient,
            template.CosparId,
            reflectivityCoefficient,
            template.BodyFront,
            template.BodyRight,
            template.BodyUp,
            template.HardBodyRadius);
    }

    private static double EstimateFuelCost(Spacecraft spacecraft, double massAtBurn, double deltaVMetersPerSecond)
    {
        if (spacecraft.Engines.Count == 0)
        {
            return 0.0;
        }

        double isp = spacecraft.GetTotalISP();
        if (isp <= 0.0)
        {
            return 0.0;
        }

        return Tsiolkovski.DeltaM(isp, massAtBurn, deltaVMetersPerSecond);
    }

    private static double SafeSqrt(double value)
    {
        return value > 0.0 ? System.Math.Sqrt(value) : 0.0;
    }
}
