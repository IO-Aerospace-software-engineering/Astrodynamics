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
    private static int _trialIdCounter = -9_000_000;

    private sealed class StateSamplingSource
    {
        public StateSamplingSource(
            Func<Time, StateVector> getState,
            IReadOnlyList<Time> searchKnots,
            DenseTrajectorySource denseTrajectory = null)
        {
            GetState = getState;
            SearchKnots = searchKnots;
            DenseTrajectory = denseTrajectory;
        }

        public Func<Time, StateVector> GetState { get; }

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
        public DenseTrajectoryStep(Time segmentBaseEpoch, AcceptedStep step, Time startEpoch, Time endEpoch)
        {
            SegmentBaseEpoch = segmentBaseEpoch;
            Step = step;
            StartEpoch = startEpoch;
            EndEpoch = endEpoch;
        }

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
                    LowRelativeSpeedThresholdMetersPerSecond = options.LowRelativeSpeedThresholdMetersPerSecond
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
    /// <returns>The highest-priority encounter within the window.</returns>
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
    /// <returns>All detected encounters in the window.</returns>
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

        foreach (var leadTime in options.LeadTimes.Where(lead => lead >= constraints.MinimumLeadTime && lead <= constraints.MaximumLeadTime))
        {
            var burnEpoch = tca - leadTime;
            if (!encounterCase.ScreeningWindow.Intersects(burnEpoch))
            {
                continue;
            }

            var burnState = GetState(encounterCase.ProtectedAsset.Spacecraft, burnEpoch, observer);

            foreach (var magnitude in options.DeltaVMagnitudesMetersPerSecond.Where(mag => mag > 0.0 && mag <= constraints.MaxDeltaVMetersPerSecond))
            {
                foreach (var direction in burnDirections)
                {
                    var deltaV = burnState.FromRtn(direction * magnitude);
                    var postBurnState = new StateVector(
                        burnState.Position,
                        burnState.Velocity + deltaV,
                        burnState.Observer,
                        burnEpoch,
                        burnState.Frame,
                        burnState.Covariance);

                    var trialSpacecraft = CloneSpacecraft(encounterCase.ProtectedAsset.Spacecraft, postBurnState);
                    var trialProfile = new ProtectedSpacecraftProfile(
                        trialSpacecraft,
                        constraints);

                    var postEncounter = Analyze(
                        trialProfile,
                        encounterCase.SecondaryObject,
                        new Window(burnEpoch, encounterCase.ScreeningWindow.EndDate),
                        new ConjunctionAnalysisOptions
                        {
                            SampleStep = options.AnalysisSampleStep,
                            MaximumEventSearchStep = options.MaximumEventSearchStep,
                            SecondaryHardBodyRadiusMeters = options.SecondaryHardBodyRadiusMeters,
                            LowRelativeSpeedThresholdMetersPerSecond = options.LowRelativeSpeedThresholdMetersPerSecond
                        });

                    var fuelCost = EstimateFuelCost(encounterCase.ProtectedAsset.Spacecraft, magnitude);
                    var probability = postEncounter.CollisionRisk?.ProbabilityOfCollision ?? 1.0;
                    var rankingScore = (probability <= options.PcThreshold ? 0.0 : 1e9) + magnitude;

                    avoidanceOptions.Add(new AvoidanceOption(
                        burnEpoch,
                        deltaV,
                        fuelCost,
                        postEncounter,
                        rankingScore));
                }
            }
        }

        return avoidanceOptions
            .OrderBy(option => option.RankingScore)
            .Take(options.MaxReturnedOptions)
            .ToArray();
    }

    private static EncounterCase AnalyzeInternal(
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
            options.BisectionToleranceSeconds);

        Time tca = SelectBestTcaEpoch(tcaEpochs, screeningWindow, protectedSource, secondarySource);
        return BuildEncounterCase(protectedAsset, secondaryObject, screeningWindow, tca, options, protectedSource, secondarySource);
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
            options.BisectionToleranceSeconds);

        if (tcaEpochs.Count == 0)
        {
            return new[]
            {
                AnalyzeInternal(protectedAsset, secondaryObject, screeningWindow, options, protectedSource, secondarySource)
            };
        }

        return tcaEpochs
            .Select(tca => BuildEncounterCase(protectedAsset, secondaryObject, screeningWindow, tca, options, protectedSource, secondarySource))
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
        double bisectionToleranceSeconds)
    {
        if (CanUseDensePolynomialSearch(protectedSource, secondarySource))
        {
            return FindDenseCloseApproaches(
                protectedSource,
                secondarySource,
                window,
                bisectionToleranceSeconds);
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
                var tca = BisectForZero(protectedSource, secondarySource, previousEpoch, currentEpoch, bisectionToleranceSeconds);
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

    private static List<Time> FindDenseCloseApproaches(
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        Window window,
        double toleranceSeconds)
    {
        var protectedDense = protectedSource.DenseTrajectory;
        var secondaryDense = secondarySource.DenseTrajectory;
        var boundaries = BuildDenseIntervalBoundaries(protectedDense, secondaryDense, window, toleranceSeconds);
        if (boundaries.Count < 2)
        {
            return new List<Time>();
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
        return SelectLocalMinimumEpochs(distinctCandidates, protectedSource, secondarySource, toleranceSeconds);
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
        Time tHigh,
        double toleranceSeconds)
    {
        const int maxIterations = 80;
        for (int i = 0; i < maxIterations; i++)
        {
            double interval = (tHigh - tLow).TotalSeconds;
            if (interval < toleranceSeconds)
            {
                break;
            }

            var tMid = tLow + TimeSpan.FromSeconds(interval * 0.5);
            double gMid = EvaluateRangeRate(protectedSource, secondarySource, tMid);

            if (gMid < 0.0)
            {
                tLow = tMid;
            }
            else
            {
                tHigh = tMid;
            }
        }

        return tLow + TimeSpan.FromSeconds((tHigh - tLow).TotalSeconds * 0.5);
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
        double intervalOffsetSeconds = (intervalStart - denseStep.StartEpoch).TotalSeconds;
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
        StateSamplingSource protectedSource,
        StateSamplingSource secondarySource,
        double toleranceSeconds)
    {
        if (candidateEpochs.Count < 2)
        {
            return new List<Time>();
        }

        var missDistanceSquared = candidateEpochs
            .Select(epoch =>
            {
                var protectedState = protectedSource.GetState(epoch);
                var secondaryState = secondarySource.GetState(epoch);
                return (secondaryState.Position - protectedState.Position).MagnitudeSquared();
            })
            .ToArray();
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
        StateSamplingSource secondarySource)
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

        var combinedCovarianceIcrf = BuildCombinedCovarianceIcrf(
            protectedAsset.Spacecraft, protectedState,
            secondaryObject, secondaryState,
            ref qualityFlags);

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
            qualityFlags);

        return new EncounterCase(
            protectedAsset,
            secondaryObject,
            screeningWindow,
            encounterState,
            collisionRisk,
            protectedState,
            secondaryState);
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
        ILocalizable protectedObject,
        StateVector protectedState,
        ILocalizable secondaryObject,
        StateVector secondaryState,
        ref EncounterQualityFlags qualityFlags)
    {
        var combined = new Matrix(6, 6);
        bool hasCovariance = false;

        var protectedCovariance = ResolveCovariance(protectedObject, protectedState, ref qualityFlags);
        var secondaryCovariance = ResolveCovariance(secondaryObject, secondaryState, ref qualityFlags);

        if (protectedCovariance.HasValue)
        {
            combined = protectedCovariance.Value;
            hasCovariance = true;
        }
        else
        {
            qualityFlags |= EncounterQualityFlags.MissingProtectedCovariance;
        }

        if (secondaryCovariance.HasValue)
        {
            combined = combined + secondaryCovariance.Value;
            hasCovariance = true;
        }
        else
        {
            qualityFlags |= EncounterQualityFlags.MissingSecondaryCovariance;
        }

        return hasCovariance ? combined : new Matrix(6, 6);
    }

    private static Matrix RotateCovarianceToRtn(Matrix covarianceIcrf, StateVector referenceState)
    {
        return referenceState.RotateCovarianceToRtn(covarianceIcrf);
    }

    private static Matrix? ResolveCovariance(ILocalizable source, StateVector state, ref EncounterQualityFlags qualityFlags)
    {
        if (state.Covariance.HasValue)
        {
            return state.Covariance.Value;
        }

        if (source.InitialOrbitalParameters is StateVector initialState && initialState.Covariance.HasValue)
        {
            qualityFlags |= EncounterQualityFlags.StaleCovarianceUsed;
            return initialState.Covariance.Value;
        }

        return null;
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
                    projectedCovariance = maximumCovariance;
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

                    projectedCovariance = prepared;
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

    // --- Foster/Chan collision probability ---

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

    private static Matrix? PrepareMaximumProbabilityEncounterPlaneCovariance(
        double[] miss2D,
        Matrix knownCov2D,
        double combinedRadius,
        out bool remediatedKnownCovariance,
        out bool remediatedMaximumCovariance)
    {
        remediatedMaximumCovariance = false;
        var preparedKnownCovariance = PrepareEncounterPlaneCovariance(knownCov2D, combinedRadius, out remediatedKnownCovariance);
        if (preparedKnownCovariance is null)
        {
            return null;
        }

        var criticalCovariance = BuildMaximumProbabilityEncounterPlaneCovariance(miss2D, preparedKnownCovariance.Value);
        if (criticalCovariance is null)
        {
            return null;
        }

        return PrepareEncounterPlaneCovariance(criticalCovariance.Value, combinedRadius, out remediatedMaximumCovariance);
    }

    internal static Matrix? BuildMaximumProbabilityEncounterPlaneCovariance(
        double[] miss2D,
        Matrix knownCov2D)
    {
        if (miss2D.Length != 2 || knownCov2D.Rows != 2 || knownCov2D.Columns != 2)
        {
            return null;
        }

        double x = miss2D[0];
        double y = miss2D[1];
        double missSquared = x * x + y * y;
        if (!double.IsFinite(missSquared))
        {
            return null;
        }

        double a = knownCov2D.Get(0, 0);
        double b = 0.5 * (knownCov2D.Get(0, 1) + knownCov2D.Get(1, 0));
        double c = knownCov2D.Get(1, 1);
        double determinant = a * c - b * b;
        if (!double.IsFinite(determinant) || determinant <= 0.0)
        {
            return null;
        }

        if (missSquared <= 0.0)
        {
            return knownCov2D;
        }

        double kSquared = (c * x * x - 2.0 * b * x * y + a * y * y) / determinant;
        if (!double.IsFinite(kSquared) || kSquared <= 1.0)
        {
            return knownCov2D;
        }

        double criticalVariance = missSquared * (kSquared - 1.0) / kSquared;
        if (!double.IsFinite(criticalVariance) || criticalVariance <= 0.0)
        {
            return knownCov2D;
        }

        double ux = x / System.Math.Sqrt(missSquared);
        double uy = y / System.Math.Sqrt(missSquared);
        var unknownCovariance = new Matrix(2, 2);
        unknownCovariance.Set(0, 0, criticalVariance * ux * ux);
        unknownCovariance.Set(0, 1, criticalVariance * ux * uy);
        unknownCovariance.Set(1, 0, criticalVariance * ux * uy);
        unknownCovariance.Set(1, 1, criticalVariance * uy * uy);

        return knownCov2D + unknownCovariance;
    }

    private static Matrix? PrepareEncounterPlaneCovariance(
        Matrix cov2D,
        double combinedRadius,
        out bool remediated)
    {
        remediated = false;

        if (cov2D.Rows != 2 || cov2D.Columns != 2 || !double.IsFinite(combinedRadius))
        {
            return null;
        }

        double a = cov2D.Get(0, 0);
        double b = 0.5 * (cov2D.Get(0, 1) + cov2D.Get(1, 0));
        double c = cov2D.Get(1, 1);
        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c))
        {
            return null;
        }

        double trace = a + c;
        double discriminant = (a - c) * (a - c) + 4.0 * b * b;
        double sqrtDisc = System.Math.Sqrt(System.Math.Max(discriminant, 0.0));
        double lambda1 = 0.5 * (trace + sqrtDisc);
        double lambda2 = 0.5 * (trace - sqrtDisc);
        if (!double.IsFinite(lambda1) || !double.IsFinite(lambda2))
        {
            return null;
        }

        if (combinedRadius <= 0.0)
        {
            return BuildSymmetric2X2(a, b, c);
        }

        double eigenvalueClip = System.Math.Pow(1.0e-4 * combinedRadius, 2);
        double clippedLambda1 = System.Math.Max(lambda1, eigenvalueClip);
        double clippedLambda2 = System.Math.Max(lambda2, eigenvalueClip);
        remediated = clippedLambda1 != lambda1 || clippedLambda2 != lambda2;

        double theta = 0.5 * System.Math.Atan2(2.0 * b, a - c);
        double cosTheta = System.Math.Cos(theta);
        double sinTheta = System.Math.Sin(theta);
        var rotation = new Matrix(2, 2);
        rotation.Set(0, 0, cosTheta);
        rotation.Set(0, 1, -sinTheta);
        rotation.Set(1, 0, sinTheta);
        rotation.Set(1, 1, cosTheta);

        var diagonal = new Matrix(2, 2);
        diagonal.Set(0, 0, clippedLambda1);
        diagonal.Set(1, 1, clippedLambda2);

        return rotation * diagonal * rotation.Transpose();
    }

    private static double? ComputeFosterCollisionProbabilityCore(
        double[] miss2D,
        Matrix cov2D,
        double combinedRadius)
    {
        if (combinedRadius <= 0.0)
        {
            return 0.0;
        }

        double a = cov2D.Get(0, 0);
        double b = 0.5 * (cov2D.Get(0, 1) + cov2D.Get(1, 0));
        double c = cov2D.Get(1, 1);

        double trace = a + c;
        double discriminant = (a - c) * (a - c) + 4.0 * b * b;
        double sqrtDisc = System.Math.Sqrt(System.Math.Max(discriminant, 0.0));
        double lambda1 = 0.5 * (trace + sqrtDisc);
        double lambda2 = 0.5 * (trace - sqrtDisc);
        if (lambda1 <= 0.0 || lambda2 <= 0.0)
        {
            return null;
        }

        double theta = 0.5 * System.Math.Atan2(2.0 * b, a - c);
        double cosTheta = System.Math.Cos(theta);
        double sinTheta = System.Math.Sin(theta);
        double xPrincipal = miss2D[0] * cosTheta + miss2D[1] * sinTheta;
        double yPrincipal = -miss2D[0] * sinTheta + miss2D[1] * cosTheta;

        return IntegrateGaussianOverCircularHardBodyRegion(
            xPrincipal,
            yPrincipal,
            lambda1,
            lambda2,
            combinedRadius);
    }

    private static double IntegrateGaussianOverCircularHardBodyRegion(
        double xCenter,
        double yCenter,
        double varianceX,
        double varianceY,
        double radius)
    {
        double determinant = varianceX * varianceY;
        if (determinant <= 0.0 || !double.IsFinite(determinant))
        {
            return 0.0;
        }

        double radiusSquared = radius * radius;
        double normalizer = 1.0 / (2.0 * System.Math.PI * System.Math.Sqrt(determinant));
        double yScale = System.Math.Sqrt(System.Math.PI * varianceY / 2.0);
        double erfScale = 1.0 / System.Math.Sqrt(2.0 * varianceY);

        double Integrand(double x)
        {
            double dx = x - xCenter;
            if (System.Math.Abs(dx) > radius)
            {
                return 0.0;
            }

            double halfChord = System.Math.Sqrt(System.Math.Max(radiusSquared - dx * dx, 0.0));
            double yLow = yCenter - halfChord;
            double yHigh = yCenter + halfChord;
            double xWeight = System.Math.Exp(-(x * x) / (2.0 * varianceX));
            double yIntegral = yScale * (SpecialFunctions.ErrorFunction(yHigh * erfScale) - SpecialFunctions.ErrorFunction(yLow * erfScale));
            return normalizer * xWeight * yIntegral;
        }

        double lower = xCenter - radius;
        double upper = xCenter + radius;
        double integral = AdaptiveSimpson(Integrand, lower, upper, 1.0e-12, 20);
        return System.Math.Clamp(integral, 0.0, 1.0);
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

    private static Matrix BuildSymmetric2X2(double a, double b, double c)
    {
        var matrix = new Matrix(2, 2);
        matrix.Set(0, 0, a);
        matrix.Set(0, 1, b);
        matrix.Set(1, 0, b);
        matrix.Set(1, 1, c);
        return matrix;
    }

    private static double AdaptiveSimpson(Func<double, double> integrand, double lower, double upper, double tolerance, int maxDepth)
    {
        double midpoint = (lower + upper) * 0.5;
        double fLower = integrand(lower);
        double fMid = integrand(midpoint);
        double fUpper = integrand(upper);
        double whole = SimpsonEstimate(lower, upper, fLower, fMid, fUpper);
        return AdaptiveSimpsonRecursive(integrand, lower, upper, tolerance, whole, fLower, fMid, fUpper, maxDepth);
    }

    private static double AdaptiveSimpsonRecursive(
        Func<double, double> integrand,
        double lower,
        double upper,
        double tolerance,
        double whole,
        double fLower,
        double fMid,
        double fUpper,
        int depth)
    {
        double midpoint = (lower + upper) * 0.5;
        double leftMid = (lower + midpoint) * 0.5;
        double rightMid = (midpoint + upper) * 0.5;
        double fLeftMid = integrand(leftMid);
        double fRightMid = integrand(rightMid);
        double left = SimpsonEstimate(lower, midpoint, fLower, fLeftMid, fMid);
        double right = SimpsonEstimate(midpoint, upper, fMid, fRightMid, fUpper);
        double delta = left + right - whole;

        if (depth <= 0 || System.Math.Abs(delta) <= 15.0 * tolerance)
        {
            return left + right + delta / 15.0;
        }

        return AdaptiveSimpsonRecursive(integrand, lower, midpoint, tolerance * 0.5, left, fLower, fLeftMid, fMid, depth - 1)
             + AdaptiveSimpsonRecursive(integrand, midpoint, upper, tolerance * 0.5, right, fMid, fRightMid, fUpper, depth - 1);
    }

    private static double SimpsonEstimate(double lower, double upper, double fLower, double fMid, double fUpper)
    {
        return (upper - lower) * (fLower + 4.0 * fMid + fUpper) / 6.0;
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
    }

    private static void ValidateTrajectoryCoverage(PropagationSolution trajectory, Window window, string paramName)
    {
        ArgumentNullException.ThrowIfNull(trajectory);
        if (trajectory.Segments.Count == 0)
        {
            throw new ArgumentException("Trajectory must contain at least one propagation segment.", paramName);
        }

        var firstSegment = trajectory.Segments[0];
        var lastSegment = trajectory.Segments[^1];
        var trajectoryStart = firstSegment.BaseEpoch;
        var trajectoryEnd = lastSegment.BaseEpoch + TimeSpan.FromSeconds(lastSegment.Duration);
        if (trajectoryStart > window.StartDate || trajectoryEnd < window.EndDate)
        {
            throw new ArgumentException("Trajectory must cover the entire screening window.", paramName);
        }
    }

    private static StateSamplingSource CreateLocalizableStateSource(ILocalizable localizable, ILocalizable observer)
    {
        return new StateSamplingSource(
            epoch => GetState(localizable, epoch, observer),
            Array.Empty<Time>());
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
            denseTrajectory);
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
                var stepStart = segment.BaseEpoch + TimeSpan.FromSeconds(step.CumulativeTime);
                var stepEnd = stepStart + TimeSpan.FromSeconds(step.StepSize);
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
                var stepStart = segment.BaseEpoch + TimeSpan.FromSeconds(step.CumulativeTime);
                var stepEnd = stepStart + TimeSpan.FromSeconds(step.StepSize);
                if (stepEnd < screeningWindow.StartDate || stepStart > screeningWindow.EndDate)
                {
                    continue;
                }

                steps.Add(new DenseTrajectoryStep(segment.BaseEpoch, step, stepStart, stepEnd));
            }
        }

        return steps
            .OrderBy(step => step.StartEpoch)
            .ToArray();
    }

    private static Spacecraft CloneSpacecraft(Spacecraft template, StateVector initialState)
    {
        int trialId = Interlocked.Decrement(ref _trialIdCounter);
        return new Spacecraft(
            trialId,
            $"{template.Name}_SSA_TRIAL",
            template.DryOperatingMass,
            template.MaximumOperatingMass,
            new Clock($"{template.Clock.Name}_SSA", template.Clock.Resolution),
            initialState,
            template.SectionalArea,
            template.DragCoefficient,
            template.CosparId,
            template.SolarRadiationCoeff,
            template.BodyFront,
            template.BodyRight,
            template.BodyUp,
            template.HardBodyRadius);
    }

    private static double EstimateFuelCost(Spacecraft spacecraft, double deltaVMetersPerSecond)
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

        return Tsiolkovski.DeltaM(isp, spacecraft.GetTotalMass(), deltaVMetersPerSecond);
    }

    private static double SafeSqrt(double value)
    {
        return value > 0.0 ? System.Math.Sqrt(value) : 0.0;
    }
}
