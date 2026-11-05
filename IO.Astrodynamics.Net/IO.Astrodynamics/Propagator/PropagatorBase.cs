// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Propagator;

public abstract class PropagatorBase : IPropagator
{
    protected readonly CelestialItem OriginalObserver;
    public Window Window { get; }
    public Spacecraft Spacecraft { get; }
    public IIntegrator Integrator { get; }
    public TimeSpan DeltaT { get; }

    protected StateVector InitialState { get; set; }

    // The attitude timeline of the propagation (#539): the activations of its maneuver chain, sampled on the solution once
    // the integration is done
    private AttitudeTimeline _timeline;

    // The length of the propagation, in seconds of the time argument from the start of the window: an activation after it
    // is not executed (#539)
    private double _propagationDuration;

    // The impulses of the last propagation, in epoch order, their states on either side for the arcs of the stored states
    private readonly List<ImpulseBoundary> _impulses = new();

    /// <summary>
    /// The impulses of the propagation, in epoch order, each with the state of the integrator at its event before the
    /// maneuver executes and the state the maneuver gives, in the frame of the propagation, with their covariance in
    /// covariance mode (#540). For <see cref="StorePropagatedStates"/>.
    /// </summary>
    private protected IReadOnlyList<ImpulseBoundary> Impulses => _impulses;

    /// <summary>The solution of the propagation, during <see cref="StorePropagatedStates"/> only; null otherwise.</summary>
    private protected PropagationSolution CurrentSolution { get; private set; }

    // Options of the variational equations, null when they are off (internal until the public API of step 8)
    private VariationalOptions _variationalOptions;

    // The propagated covariance, null when it is off
    private CovarianceModel _covarianceModel;

    // The public options of the variational equations, null when they are off or enabled through the internal options
    private CovariancePropagationOptions _covariancePropagation;

    /// <summary>
    /// In RK7(8), the lock of the dynamics of the solution: null for a lock of its own; the lock of another propagation
    /// whose forces this one shares (an avoidance option, a perturbed propagation of the non-linearity index).
    /// </summary>
    private protected object DynamicsSyncRoot { get; set; }

    /// <summary>
    /// The time argument of the propagation (#501): TT about the Earth and TDB about any other central body for a
    /// <see cref="CentralBodyPropagator"/>, TDB for a propagator derived from this class by a user. Every epoch it
    /// produces is a TDB label.
    /// </summary>
    internal PropagationTimeArgument TimeArgument { get; }

    // The length of the window in seconds of the time argument, between its ends as the caller tagged them
    private readonly double _windowLength;

    protected PropagatorBase(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT, PropagationTimeArgument.Tdb)
    {
    }

    /// <summary>
    /// A propagator whose integration variable counts the seconds of <paramref name="timeArgument"/> (#501). The window
    /// keeps the TDB labels of its ends (<see cref="Window"/>, the start of the propagation and the caches); its length is
    /// that of the time argument between the ends as the caller tagged them.
    /// </summary>
    private protected PropagatorBase(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT,
        PropagationTimeArgument timeArgument)
    {
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        Integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
        ValidateInertialFrame(spacecraft);

        OriginalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        DeltaT = deltaT;
        TimeArgument = timeArgument ?? throw new ArgumentNullException(nameof(timeArgument));
        _windowLength = TimeArgument.Length(window, Window);
        if (integrator is Integrator integratorWithForces)
        {
            integratorWithForces.TimeArgument = TimeArgument;
        }
    }

    /// <summary>
    /// Integrate the variational equations with the state: the state transition matrix, the sensitivity to the force
    /// parameters and the process-noise covariance of <paramref name="options"/>, read afterwards with
    /// <see cref="PropagationSolution.EvaluateVariational"/>.
    /// </summary>
    /// <param name="options">What to integrate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <remarks>
    /// With <see cref="VariationalOptions.PropagateCovariance"/>, P0 is the covariance of
    /// <see cref="Spacecraft.InitialOrbitalParameters"/>, as a state vector, which must be given at the start of the
    /// window: the propagator does not carry a covariance from another epoch. It is in the frame of the propagation,
    /// which is that of the initial orbital parameters, and relative to their observer; moving the origin to the central
    /// body is a translation by a known ephemeris, of Jacobian I, which leaves P0 unchanged.
    /// </remarks>
    /// <exception cref="NotSupportedException">The integrator is not an <see cref="RK78Integrator"/>.</exception>
    /// <exception cref="ArgumentException">The options name consider parameters without the covariance, or a consider
    /// parameter without its column of Ψ.</exception>
    /// <exception cref="InvalidOperationException">The covariance is asked for, and the initial orbital parameters carry
    /// none, or carry it at another epoch than the start of the window, or carry one that is not symmetric and positive
    /// semi-definite.</exception>
    internal void EnableVariationalEquations(VariationalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rk78 = RequireRK78();

        if (options.GeopotentialPartialsDegree < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options),
                "The degree of the geopotential partials must be at least 2, not " +
                $"{options.GeopotentialPartialsDegree}.");
        }

        var covarianceModel = CreateCovarianceModel(options);

        var gravities = rk78.ForceList.OfType<GravitationalAcceleration>().ToArray();
        foreach (var gravity in gravities)
        {
            gravity.CelestialItem.ValidateGravitationalPartialsDegree(options.GeopotentialPartialsDegree);
        }

        foreach (var gravity in gravities)
        {
            gravity.PartialsDegree = options.GeopotentialPartialsDegree;
        }

        rk78.EnableVariationalEquations(options);
        _variationalOptions = options;
        _covarianceModel = covarianceModel;
        _covariancePropagation = null;
    }

    /// <summary>
    /// Propagate the state transition matrix, and the covariance in
    /// <see cref="CovariancePropagationMode.Covariance"/>, with the state: the public activation of the variational
    /// equations (D2). Null leaves them off. Called once the forces of the integrator are final, since the columns of Ψ
    /// follow them.
    /// </summary>
    /// <param name="options">The options, or null for none.</param>
    /// <exception cref="NotSupportedException">The integrator is not an <see cref="RK78Integrator"/>.</exception>
    /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a consider
    /// parameter without its force.</exception>
    /// <exception cref="InvalidOperationException">The covariance is asked for, and the initial orbital parameters carry
    /// none, carry it at another epoch than the start of the window, or carry one that is not symmetric and positive
    /// semi-definite.</exception>
    internal void EnableCovariancePropagation(CovariancePropagationOptions options)
    {
        if (options == null)
        {
            return;
        }

        var rk78 = RequireRK78();
        EnableVariationalEquations(options.ToVariationalOptions(rk78.ForceList));
        _covariancePropagation = options;
    }

    /// <summary>
    /// The initial covariance of the state and the consider parameters, C0 = cov(δx0, δp), 6 × the number of consider
    /// parameters, row-major (<see cref="CovarianceModel.SetInitialCrossCovariance"/>): for a propagation from a state
    /// already propagated with the same consider parameters (step 10, E4). Call it after the activation of the
    /// covariance, before <see cref="Propagate"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The covariance is not propagated.</exception>
    internal void SetInitialCrossCovariance(ReadOnlySpan<double> cross)
    {
        if (_covarianceModel == null)
            throw new InvalidOperationException("The covariance is not propagated: there is no consider covariance to set.");

        _covarianceModel.SetInitialCrossCovariance(cross);
    }

    private RK78Integrator RequireRK78()
    {
        if (Integrator is not RK78Integrator rk78)
        {
            throw new NotSupportedException(
                "The state transition matrix is integrated by the RK7(8) integrator (RK78Integrator) only; this " +
                $"propagator uses {Integrator.GetType().Name}.");
        }

        return rk78;
    }

    // The covariance model of the options, after the checks of P0; null without the covariance
    private CovarianceModel CreateCovarianceModel(VariationalOptions options) =>
        CreateCovarianceModel(Spacecraft, Window.StartDate, options);

    /// <summary>
    /// The covariance model of <paramref name="options"/> for a propagation of <paramref name="participant"/> from
    /// <paramref name="start"/>, after the checks of P0, the covariance of its initial orbital parameters; null without
    /// the covariance. Shared with the replay of the variational equations by the conjunction analysis, which gives
    /// <paramref name="initialCovariance"/> when it remediated P0.
    /// </summary>
    /// <param name="participant">The participant.</param>
    /// <param name="start">The start of the propagation.</param>
    /// <param name="options">The options of the variational equations.</param>
    /// <param name="initialCovariance">P0 in place of the covariance of the initial orbital parameters, at their epoch;
    /// null for that covariance.</param>
    /// <exception cref="ArgumentException">Consider parameters without the covariance.</exception>
    /// <exception cref="InvalidOperationException">The initial orbital parameters carry no covariance, carry it at another
    /// epoch than <paramref name="start"/>, or carry one that is not 6×6, symmetric and positive semi-definite.</exception>
    internal static CovarianceModel CreateCovarianceModel(ILocalizable participant, in Time start,
        VariationalOptions options, Math.Matrix? initialCovariance = null)
    {
        if (!options.PropagateCovariance)
        {
            if (options.ConsiderParameters != null)
            {
                throw new ArgumentException(
                    "Consider parameters enter the propagated covariance only: set PropagateCovariance.",
                    nameof(options));
            }

            return null;
        }

        var initial = participant.InitialOrbitalParameters;
        var covariance = initial.ToStateVector().Covariance;
        if (!covariance.HasValue)
        {
            throw InitialCovarianceError(participant,
                "The covariance is propagated from that of the initial orbital parameters of the spacecraft, which " +
                "carry none: give the initial state vector a covariance, or propagate the state transition matrix " +
                "only.");
        }

        // Compared as TDB labels, as the message prints them: a UTC epoch and its TDB label differ through TAI by a tick
        // at some instants (#510)
        if (initial.Epoch.ToTDB() != start.ToTDB())
        {
            throw InitialCovarianceError(participant,
                $"The initial covariance is given at {initial.Epoch.ToTDB()}, and the propagation starts at " +
                $"{start}: the propagator does not carry a covariance from another epoch. Start the window " +
                "at the epoch of the initial orbital parameters.");
        }

        var reason = InitialCovarianceRejection(initialCovariance ?? covariance.Value, out var symmetrized);
        if (reason != null)
        {
            throw InitialCovarianceError(participant, reason);
        }

        return new CovarianceModel(symmetrized, options);
    }

    /// <summary>
    /// Why the initial covariance <paramref name="p0"/> is rejected: not 6×6, symmetric and positive semi-definite
    /// (<see cref="CovarianceChecks.Validate(ReadOnlySpan{double}, int, string, out double[])"/>); null when it is
    /// accepted, with its symmetrized values.
    /// </summary>
    internal static string InitialCovarianceRejection(Math.Matrix p0, out double[] symmetrized) =>
        InitialCovarianceRejection(p0, out symmetrized, out _);

    // The same, with the smallest eigenvalue of the correlation matrix (NaN when it is not computed)
    private static string InitialCovarianceRejection(Math.Matrix p0, out double[] symmetrized,
        out double smallestEigenvalue)
    {
        symmetrized = null;
        smallestEigenvalue = double.NaN;
        if (p0.Rows != VariationalEquations.StateDimension || p0.Columns != VariationalEquations.StateDimension)
        {
            return $"The initial covariance must be 6×6; it is {p0.Rows}×{p0.Columns}.";
        }

        var values = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                values[6 * i + j] = p0.Get(i, j);
            }
        }

        return CovarianceChecks.Validate(values, 6, "The initial covariance", out symmetrized, out smallestEigenvalue);
    }

    /// <summary>
    /// The initial covariance <paramref name="p0"/> as the propagation takes it, or, when rounding made it indefinite
    /// (the propagation would reject it), remediated by eigenvalue clipping
    /// (<see cref="CovarianceChecks.RemediateByEigenvalueClipping(ReadOnlySpan{double}, int)"/>). The one implementation
    /// of the conjunction analysis and of <see cref="StateVector.WithRemediatedCovariance"/> (#405); the propagation
    /// itself never calls it.
    /// </summary>
    /// <param name="p0">The initial covariance.</param>
    /// <param name="remediated">The remediated covariance; null when <paramref name="p0"/> is accepted as it is, or
    /// refused.</param>
    /// <param name="smallestEigenvalue">The smallest eigenvalue of the correlation matrix of <paramref name="p0"/>, its
    /// positive variances (zero when none is); NaN when it is refused.</param>
    /// <returns>Null when <paramref name="p0"/> is accepted or remediated; otherwise why it is not one that rounding
    /// explains: not 6×6, not finite, a negative variance, an asymmetry beyond the tolerance.</returns>
    internal static string RemediateInitialCovariance(Math.Matrix p0, out Math.Matrix? remediated,
        out double smallestEigenvalue)
    {
        remediated = null;
        var rejection = InitialCovarianceRejection(p0, out _, out smallestEigenvalue);
        if (rejection == null)
            return null;

        if (p0.Rows != VariationalEquations.StateDimension || p0.Columns != VariationalEquations.StateDimension)
            return rejection;

        var values = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                values[6 * i + j] = p0.Get(i, j);
            }
        }

        var clipped = CovarianceChecks.RemediateByEigenvalueClipping(values, 6, out var refusal, out smallestEigenvalue);
        if (clipped == null)
            return refusal;

        var matrix = new Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                matrix.Set(i, j, clipped[6 * i + j]);
            }
        }

        remediated = matrix;
        return null;
    }

    // A rejected initial covariance, naming the spacecraft, so that a scenario of several spacecraft says which one
    private static InvalidOperationException InitialCovarianceError(ILocalizable participant, string reason) =>
        new($"The covariance of {(participant is Spacecraft ? "spacecraft" : "object")} '{participant.Name}' cannot be " +
            $"propagated: {reason}");

    /// <summary>
    /// The frame whose orientation cache the propagation uses, and that cache, active on the propagating thread during
    /// the propagation and during the evaluations of its solution after it. None by default.
    /// </summary>
    private protected virtual (Frame Frame, PropagationFrameOrientationCache Cache) OrientationCache => (null, null);

    public PropagationSolution Propagate()
    {
        // The orientation cache is active on this thread only, so concurrent propagations sharing a central body do not
        // see each other's cache, and none is left on the shared frame
        var (cacheFrame, cache) = OrientationCache;
        using (Frame.UseOrientationCache(cacheFrame, cache))
        {
            return PropagateWithOrientationCache(cacheFrame, cache);
        }
    }

    private PropagationSolution PropagateWithOrientationCache(Frame cacheFrame, PropagationFrameOrientationCache cache)
    {
        // 1. The length of the propagation (may exceed the window for non-fitting step sizes), in seconds of the time
        // argument. In TT, a grid that misses the end of the window by the drift between TT and the labels ends at the
        // window, its last output labelled with the end of the window as the caller gave it
        uint outputCount = (uint)System.Math.Round(_windowLength / DeltaT.TotalSeconds,
            MidpointRounding.AwayFromZero) + 1;
        double outputDuration = (outputCount - 1) * DeltaT.TotalSeconds;
        bool endsAtWindowEnd = TimeArgument.EndsAtWindowEnd(_windowLength, outputDuration);
        double propagationDuration = endsAtWindowEnd ? _windowLength : System.Math.Max(_windowLength, outputDuration);
        var propagationEnd = endsAtWindowEnd ? Window.EndDate : TimeArgument.EpochAt(Window.StartDate, propagationDuration);
        _propagationDuration = propagationDuration;

        // 2. The leading attitudes of the chain on the attitude timeline, and the detector of its first impulse (#539)
        _timeline = new AttitudeTimeline();
        _impulses.Clear();
        var eventDetectors = BuildEventDetectors();

        // 3. Create solution
        var solution = new PropagationSolution { TimeArgument = TimeArgument };

        var currentPos = InitialState.Position;
        var currentVel = InitialState.Velocity;
        var currentEpoch = Window.StartDate;

        // RK7(8): the dynamics, kept by the solution, and the variational equations when they are on
        var rk78 = Integrator as RK78Integrator;
        PropagationDynamics dynamics = null;
        if (rk78 != null)
        {
            dynamics = new PropagationDynamics(rk78.ForceList, InitialState.Observer, InitialState.Frame,
                InitialState.Epoch, cacheFrame, cache, _variationalOptions, rk78.VariationalEquations,
                _covarianceModel, DynamicsSyncRoot ?? new object()) { Integrator = rk78, TimeArgument = TimeArgument };
        }

        var variational = _variationalOptions != null ? rk78.VariationalEquations : null;
        double[] entryY = null;
        double[] entryQ = null;
        ManeuverRecord? entryManeuver = null;

        // In RK7(8), the maneuver that starts each segment, recorded with or without the variational equations for their
        // replay by the conjunction analysis
        ManeuverRecord? segmentManeuver = null;
        if (variational != null)
        {
            entryY = new double[variational.YLength];
            variational.SetIdentity(entryY);
            entryQ = new double[variational.CarriedCovarianceLength];
        }

        // The seconds of the time argument from the start of the window to the base of the current segment, the sum of the
        // event times: the time argument of each event, from which the windows of the chain count their seconds (#547)
        double segmentBaseTau = 0.0;

        // 4. Segment loop
        while (true)
        {
            double remaining = TimeArgument.Remaining(Window.StartDate, propagationDuration, propagationEnd, currentEpoch);
            if (remaining <= 0.0) break;

            // a. Integrate segment, with the mass and coefficients of the segment, recorded for the force model of the
            // propagation (#402) and, in RK7(8), for the evaluations of its solution
            var context = ForceEvaluationContext.FromSpacecraft(Spacecraft);
            if (variational != null)
            {
                rk78.BeginVariationalSegment(context, entryY, entryQ, entryManeuver);
            }

            var result = Integrator.IntegrateSegment(
                currentPos, currentVel, currentEpoch, remaining, eventDetectors);
            result.Segment.Context = context;
            if (rk78 != null)
            {
                result.Segment.EntryManeuver = segmentManeuver;
            }

            // b. Add segment to solution
            solution.AddSegment(result.Segment);

            // c. Handle event
            if (result.Event.HasValue)
            {
                var evt = result.Event.Value;
                var eventEpoch = TimeArgument.EpochAt(currentEpoch, evt.EventTime);
                double eventTau = segmentBaseTau + evt.EventTime;

                // Create StateVector at event epoch
                var eventState = new StateVector(evt.EventPosition, evt.EventVelocity,
                    InitialState.Observer, eventEpoch, InitialState.Frame);

                // Variational values at the event, before the maneuver changes the spacecraft: by a shortened step
                // from the start of the last accepted step
                double[] eventY = null;
                double[] eventQ = null;
                if (variational != null)
                {
                    eventY = new double[variational.YLength];
                    eventQ = new double[entryQ.Length];
                    dynamics.Evaluate(result.Segment, evt.EventTime, eventY, eventQ);
                }

                // The state before the maneuver, kept apart for the arcs of the stored states (#540): the maneuver changes the
                // velocity of the state it executes on. In covariance mode, with the covariance at the event
                var stateBefore = new StateVector(evt.EventPosition, evt.EventVelocity, InitialState.Observer, eventEpoch,
                    InitialState.Frame, _covarianceModel != null ? CovarianceOf(eventY, eventQ) : null);

                // Delegate to the event detector
                var detector = eventDetectors[evt.DetectorIndex];
                var eventResult = detector.HandleEvent(eventState, Spacecraft);

                // The next segment starts at the event: from the state after the maneuver on StopAndRestart, from the
                // state at the event, unchanged, on Continue (#355: the same segment used to be integrated again, without
                // end)
                bool restart = eventResult.Action == EventAction.StopAndRestart;

                // An executed impulse: its windows in seconds of the time argument, its attitude and the rest of the chain
                // on the timeline, and the detector of the next impulse (#539, #547); the detector executes the impulse
                // only
                if (restart && detector is ManeuverEventDetector executed)
                {
                    eventResult = eventResult with
                    {
                        NextDetector = OnImpulse(executed.Maneuver, eventTau, eventResult.Orientation)
                    };
                }

                var nextState = restart ? eventResult.ModifiedState : eventState;
                currentPos = nextState.Position;
                currentVel = nextState.Velocity;
                currentEpoch = nextState.Epoch;

                // Open loop: the maneuver is fixed in epoch and ΔV, so the variational values are continuous at the
                // event and become the entry values of the next segment; the execution error of the burn, if any, adds
                // to the velocity block of the carried Q (C5)
                if (variational != null)
                {
                    entryY = eventY;
                    entryQ = eventQ;
                    entryManeuver = restart && detector is ManeuverEventDetector maneuverDetector
                        ? ManeuverRecordOf(eventEpoch, maneuverDetector.Maneuver, entryQ)
                        : null;
                    segmentManeuver = entryManeuver;
                }
                else if (rk78 != null)
                {
                    segmentManeuver = restart && detector is ManeuverEventDetector maneuverDetector
                        ? ManeuverRecordOf(eventEpoch, maneuverDetector.Maneuver, Array.Empty<double>())
                        : null;
                }

                if (rk78 != null)
                {
                    result.Segment.EventTime = evt.EventTime;
                }

                // An executed impulse: its states on either side, the state after with the covariance the next segment starts
                // from, the execution error of the burn included (#540)
                if (restart && detector is ManeuverEventDetector)
                {
                    var after = eventResult.ModifiedState;
                    _impulses.Add(new ImpulseBoundary(eventEpoch, stateBefore,
                        new StateVector(after.Position, after.Velocity, after.Observer, after.Epoch, after.Frame,
                            _covarianceModel != null ? CovarianceOf(eventY, eventQ) : null)));
                }

                // Update event detectors for next segment
                eventDetectors = UpdateEventDetectors(eventResult.NextDetector);
                segmentBaseTau = eventTau;

                // Re-initialize integrator for new dynamics
                Integrator.Initialize(new StateVector(currentPos, currentVel, InitialState.Observer, currentEpoch,
                    InitialState.Frame));

                // On Continue, the segment starts at the root just handled, which the detector must not find again;
                // Velocity-Verlet locates the event at the end of a step, already past the root
                if (!restart)
                {
                    rk78?.SkipRootOfLastEvent();
                }
            }
            else
            {
                // No event — propagation complete
                break;
            }
        }

        // 5. Generate output StateVectors at DeltaT epochs; in RK7(8), from the dynamics of the propagation
        if (endsAtWindowEnd)
        {
            solution.WindowEnd = Window.EndDate;
        }

        solution.Dynamics = dynamics;
        solution.ForceModel = PropagationForceModel.TryCreate(Integrator, solution, InitialState, cache,
            dynamics?.SyncRoot ?? DynamicsSyncRoot);
        solution.Observer = InitialState.Observer;
        solution.Frame = InitialState.Frame;
        solution.CovariancePropagation = _covariancePropagation;
        var outputStates = GenerateOutput(solution, outputCount, endsAtWindowEnd);

        // 5b. The attitude record, from the timeline, on the solution (#539)
        RecordAttitudeTimeline(solution, outputStates);

        // 6. Add final orientation
        Spacecraft.Frame.RecordEnd(Window.EndDate);

        // 7. Store propagated states, split at the impulses, the short arcs filled from the solution (#540)
        CurrentSolution = solution;
        try
        {
            StorePropagatedStates(outputStates);
        }
        finally
        {
            CurrentSolution = null;
        }

        // 8. Attach sampled output to solution
        solution.SetOutputStates(outputStates);

        // 9. In RK7(8), the spacecraft keeps it, for the conjunction analysis that reads its propagated states (step 10,
        // E2); another integrator leaves nothing to replay, so its solution is not kept (H3 review of step 10, B2)
        Spacecraft.LastPropagation = solution.Dynamics != null ? solution : null;

        // With any integrator whose settings can be copied, the spacecraft keeps the force model of the propagation, without
        // its steps, for the avoidance options of the conjunction assessment (#402)
        Spacecraft.LastForceModel = solution.ForceModel;

        return solution;
    }

    // The covariance of the state from the cumulative Y and the carried Q, as the outputs of a propagation of the covariance
    // take it (PropagationDynamics.StateAndCovarianceAt)
    private Matrix CovarianceOf(double[] y, double[] q)
    {
        var covariance = new double[VariationalEquations.StateDimension * VariationalEquations.StateDimension];
        _covarianceModel.Covariance(y, q, covariance);
        return new Matrix(VariationalEquations.StateDimension, VariationalEquations.StateDimension, covariance);
    }

    /// <summary>
    /// The record of an executed maneuver; with an execution error and a carried Q, its covariance L_e is added to the
    /// velocity block of <paramref name="q"/>: Q⁺ = Q⁻ + B L_e Bᵀ, B = [0 ; I], the error being independent of the
    /// state before the burn.
    /// </summary>
    private static ManeuverRecord ManeuverRecordOf(in Time epoch, ImpulseManeuver maneuver, double[] q)
    {
        if (maneuver.ExecutionError == null)
        {
            return new ManeuverRecord(epoch, maneuver.DeltaV);
        }

        var covariance = new double[9];
        maneuver.ExecutionError.Covariance(maneuver.DeltaV, covariance);
        if (q.Length > 0)
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = i; j < 3; j++)
                {
                    q[VariationalEquations.CovarianceIndex(i + 3, j + 3)] += covariance[3 * i + j];
                }
            }
        }

        return new ManeuverRecord(epoch, maneuver.DeltaV, covariance);
    }

    /// <summary>
    /// The leading attitudes of the spacecraft's maneuver chain on the attitude timeline (#539), each at its minimum epoch
    /// or at the end of the previous one's hold, whichever is later, and the detector of the first impulse, whose gate the
    /// last hold raises.
    /// </summary>
    private List<IEventDetector> BuildEventDetectors()
    {
        var detectors = new List<IEventDetector>();
        var impulse = ScheduleAttitudes(Spacecraft.StandbyManeuver, Window.StartDate, 0.0);
        if (impulse != null)
        {
            detectors.Add(new ManeuverEventDetector(impulse));
        }

        return detectors;
    }

    /// <summary>
    /// Activates the attitudes of the chain from <paramref name="maneuver"/> on, on the attitude timeline: each at its
    /// <see cref="Maneuver.Maneuver.MinimumEpoch"/> or at the end of the previous maneuver's window
    /// (<paramref name="previousEnd"/>, at <paramref name="previousEndTau"/> seconds of the time argument from the start of
    /// the window), whichever is later, its window its hold counted in seconds of the time argument (#547). Each executed
    /// attitude raises the gate of the next maneuver to the end of its hold, for this propagation only
    /// (<see cref="Spacecraft.ChainStandbyManeuver"/>, #573). An attitude that would activate after the end of the
    /// propagation is not executed, and stops the chain.
    /// </summary>
    /// <returns>The first impulse maneuver after these attitudes, null if the chain ends or stops before one.</returns>
    private ImpulseManeuver ScheduleAttitudes(Maneuver.Maneuver maneuver, Time previousEnd, double previousEndTau)
    {
        while (maneuver is Attitude attitude)
        {
            var start = previousEnd;
            double tau = previousEndTau;
            var minimumEpoch = attitude.MinimumEpoch;
            double minimumTau = TimeArgument.SecondsBetween(Window.StartDate, minimumEpoch);
            if (minimumTau > tau)
            {
                start = minimumEpoch;
                tau = minimumTau;
            }

            if (tau > _propagationDuration + PropagationSolution.EpochTick)
            {
                return null;
            }

            double endTau = tau + attitude.ManeuverHoldDuration.TotalSeconds;
            var end = After(start, endTau, attitude.ManeuverHoldDuration);
            attitude.ThrustWindow = new Window(start, TimeSpan.Zero);
            attitude.ManeuverWindow = new Window(start, end);
            _timeline.ActivateLaw(start, attitude);

            // The attitude is executed: the next maneuver of the chain may start at the end of its hold, in this
            // propagation only (#573)
            Spacecraft.ChainStandbyManeuver(attitude.NextManeuver, end);
            previousEnd = end;
            previousEndTau = endTau;
            maneuver = attitude.NextManeuver;
        }

        return maneuver as ImpulseManeuver;
    }

    // The epoch at tau seconds of the time argument from the start of the window, span after from: about the Earth the TT
    // seconds of the argument (#547); otherwise from + span, the TDB arithmetic of the maneuvers, bit for bit
    private Time After(in Time from, double tau, TimeSpan span) =>
        TimeArgument == PropagationTimeArgument.Tdb ? from + span : TimeArgument.EpochAt(Window.StartDate, tau);

    /// <summary>
    /// An executed impulse on the timeline: about the Earth, its thrust and maneuver windows in TT seconds from the time
    /// argument of its event, and the gate of the next maneuver set to the end of its maneuver window (#547); its attitude
    /// from the opening of its thrust window (#539); after its maneuver window, the law in force before it, unless an
    /// attitude chained after it activates there; then the attitudes chained after it.
    /// </summary>
    /// <param name="impulse">The impulse its detector executed.</param>
    /// <param name="eventTau">The seconds of the time argument from the start of the window to its event.</param>
    /// <param name="orientation">Its attitude, the front along ΔV; null for none.</param>
    /// <returns>The detector of the next impulse, null if none.</returns>
    private IEventDetector OnImpulse(ImpulseManeuver impulse, double eventTau, StateOrientation orientation)
    {
        // The windows ImpulseManeuver.TryExecute set from the event, in TDB seconds; about the Earth, counted in TT seconds
        // from the event's time argument (#547)
        var thrust = impulse.ThrustWindow!.Value;
        double maneuverEndTau;
        if (TimeArgument == PropagationTimeArgument.Tdb)
        {
            maneuverEndTau = TimeArgument.SecondsBetween(Window.StartDate, impulse.ManeuverWindow!.Value.EndDate);
        }
        else
        {
            double dt = thrust.Length.TotalSeconds;
            double thrustStartTau = eventTau - dt * 0.666;
            var thrustStart = TimeArgument.EpochAt(Window.StartDate, thrustStartTau);
            thrust = new Window(thrustStart, TimeArgument.EpochAt(Window.StartDate, thrustStartTau + dt));
            maneuverEndTau = thrustStartTau + System.Math.Max(impulse.ManeuverHoldDuration.TotalSeconds, dt);
            impulse.ThrustWindow = thrust;
            impulse.ManeuverWindow = new Window(thrustStart, TimeArgument.EpochAt(Window.StartDate, maneuverEndTau));

            // TryExecute raised the gate of the next maneuver to the TDB end of the window: the TT end replaces it, for
            // this propagation only (#573)
            impulse.NextManeuver?.SetPropagationGate(impulse.ManeuverWindow.Value.EndDate);
        }

        var maneuverEnd = impulse.ManeuverWindow!.Value.EndDate;

        // The impulse's attitude from the opening of its thrust window, no earlier than the window or the last activation
        var start = thrust.StartDate;
        if (start < Window.StartDate) start = Window.StartDate;
        if (_timeline.LastEpoch is { } last && start < last) start = last;
        if (orientation != null)
        {
            _timeline.ActivateDiscrete(start, orientation);
        }

        // After its maneuver window, the law in force before it
        if (_timeline.CurrentLaw != null && maneuverEndTau <= _propagationDuration + PropagationSolution.EpochTick)
        {
            _timeline.ActivateLaw(maneuverEnd, _timeline.CurrentLaw, true);
        }

        var nextImpulse = ScheduleAttitudes(impulse.NextManeuver, maneuverEnd, maneuverEndTau);
        return nextImpulse != null ? new ManeuverEventDetector(nextImpulse) : null;
    }

    /// <summary>
    /// Update event detectors after an event (next detector from event result).
    /// </summary>
    private static List<IEventDetector> UpdateEventDetectors(IEventDetector nextDetector)
    {
        var detectors = new List<IEventDetector>();
        if (nextDetector != null)
        {
            detectors.Add(nextDetector);
        }

        return detectors;
    }

    /// <summary>
    /// Generate output StateVectors at DeltaT intervals with <see cref="PropagationSolution.InterpolateAt"/>.
    /// Output may extend slightly past the window end when the step size doesn't evenly divide the window.
    /// </summary>
    /// <remarks>
    /// The outputs are uniform in the time argument and labelled TDB (<see cref="PropagationTimeArgument.OutputEpoch"/>);
    /// each state is the solution at its label, which in TT holds i·ΔT to the tick only. With
    /// <paramref name="lastAtWindowEnd"/>, the last output is labelled with the end of the window.
    /// </remarks>
    private StateVector[] GenerateOutput(PropagationSolution solution, uint count, bool lastAtWindowEnd)
    {
        var output = new StateVector[count];

        for (int i = 0; i < count; i++)
        {
            var epoch = lastAtWindowEnd && i == count - 1
                ? Window.EndDate
                : TimeArgument.OutputEpoch(Window.StartDate, i, DeltaT);
            if (_covarianceModel != null)
            {
                // The state and its covariance from one shortened step: the state is that of InterpolateAt
                output[i] = solution.StateAndCovarianceAt(epoch);
            }
            else
            {
                var (pos, vel) = solution.InterpolateAt(epoch);
                output[i] = new StateVector(pos, vel, InitialState.Observer, epoch, InitialState.Frame);
            }
        }

        return output;
    }

    // The rotation from the spacecraft frame to ICRF of an attitude law, computed on the state in ICRF
    private Quaternion Sample(Attitude law, StateVector state)
    {
        var localSv = ToIcrf(state).RelativeTo(law.ManeuverCenter, Aberration.None).ToStateVector();
        return law.ComputeOrientation(localSv);
    }

    /// <summary>
    /// The attitude record from the timeline (#539), on the solution: the identity of ICRF at the start, which an attitude
    /// activated there replaces; each activation at its epoch, a law executed on the state of the solution there
    /// (<see cref="Attitude.TryExecute"/>), a resumed law sampled there; each law sampled at every output epoch of its
    /// interval, and once more <see cref="SpacecraftFrame.DiscreteChangeLead"/> before each discrete change that follows
    /// it (#529 part 3, #534). An identity is recorded like any rotation: no sentinel.
    /// </summary>
    private void RecordAttitudeTimeline(PropagationSolution solution, StateVector[] outputs)
    {
        var frame = Spacecraft.Frame;
        var activations = _timeline.Activations;

        // The attitude without an attitude maneuver: the identity of ICRF (#529, #535), as the frame of a spacecraft not
        // propagated
        frame.Record(new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.StartDate, Frame.ICRF), null, false);

        int output = 0;
        var laws = new List<Attitude>();
        for (int i = 0; i < activations.Count; i++)
        {
            var activation = activations[i];
            if (activation.Law == null)
            {
                frame.Record(activation.Discrete, null, true);
                continue;
            }

            var law = activation.Law;
            if (!laws.Contains(law)) laws.Add(law);
            var (position, velocity) = solution.InterpolateAt(activation.Epoch);
            var state = new StateVector(position, velocity, InitialState.Observer, activation.Epoch, InitialState.Frame);
            if (activation.Resumed)
            {
                frame.Record(new StateOrientation(Sample(law, state), Vector3.Zero, activation.Epoch, Frame.ICRF), law, true);
            }
            else
            {
                // The attitude's own execution, on the state at its activation: its StateOrientation, in ICRF
                var (_, so) = law.TryExecute(state);
                frame.Record(so, law, true);
            }

            // Its samples at the outputs of its interval, up to the next activation
            Time? next = i + 1 < activations.Count ? activations[i + 1].Epoch : null;
            while (output < outputs.Length && outputs[output].Epoch <= activation.Epoch) output++;
            for (int o = output; o < outputs.Length && (next == null || outputs[o].Epoch < next.Value); o++)
            {
                frame.Record(new StateOrientation(Sample(law, outputs[o]), Vector3.Zero, outputs[o].Epoch, Frame.ICRF), law, false);
            }
        }

        foreach (var law in laws)
        {
            RecordLeads(solution, law);
        }
    }

    /// <summary>
    /// Samples <paramref name="law"/> once more before each discrete change that follows it (an impulse, another attitude),
    /// on the state of the solution <see cref="SpacecraftFrame.DiscreteChangeLead"/> before the change: the attitude between
    /// the last sample and the change is then the interpolation of the law, and the CK repeats that sample before the
    /// change (#529 part 3, #534).
    /// </summary>
    private void RecordLeads(PropagationSolution solution, Attitude law)
    {
        foreach (var change in Spacecraft.Frame.ChangesAfterSamplesOf(law))
        {
            // A change after the propagation has no state before it to sample
            if (change > solution.End)
            {
                continue;
            }

            var epoch = change.AddSeconds(-SpacecraftFrame.DiscreteChangeLead);
            var (position, velocity) = solution.InterpolateAt(epoch);
            var orientation = Sample(law, new StateVector(position, velocity, InitialState.Observer, epoch, InitialState.Frame));
            Spacecraft.Frame.RecordLead(new StateOrientation(orientation, Vector3.Zero, epoch, Frame.ICRF), law, change);
        }
    }

    /// <summary>
    /// Stores the output states of the propagation in its spacecraft. The impulses of the propagation, with the states on
    /// either side of each (<see cref="Impulses"/>), and its solution (<see cref="CurrentSolution"/>) are available during
    /// the call (#540).
    /// </summary>
    /// <param name="outputStates">The output states, in the frame of the propagation, in epoch order.</param>
    protected abstract void StorePropagatedStates(StateVector[] outputStates);

    private Quaternion? _toIcrf;

    /// <summary>
    /// A state of the propagation in ICRF: the frame of the propagation is inertial (<see cref="ValidateInertialFrame"/>),
    /// its rotation to ICRF constant, read once, so that no static frame keeps an orientation per output epoch.
    /// </summary>
    private protected StateVector ToIcrf(StateVector state)
    {
        if (state.Frame == Frame.ICRF)
        {
            return state;
        }

        var q = _toIcrf ??= state.Frame.ToFrame(Frame.ICRF, Window.StartDate).Rotation;
        Matrix? covariance = null;
        if (state.Covariance.HasValue)
        {
            covariance = Matrix.TransformCovarianceWithJacobian(state.Covariance.Value, Matrix.CreateStateTransformationJacobian(q, Vector3.Zero));
        }

        return new StateVector(state.Position.Rotate(q), state.Velocity.Rotate(q), state.Observer, state.Epoch, Frame.ICRF, covariance);
    }

    private static void ValidateInertialFrame(Spacecraft spacecraft)
    {
        if (spacecraft.InitialOrbitalParameters.Frame != Frame.ICRF
            && spacecraft.InitialOrbitalParameters.Frame != Frame.GCRF
            && spacecraft.InitialOrbitalParameters.Frame != Frame.B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frame.FK4
            && spacecraft.InitialOrbitalParameters.Frame != Frame.ECLIPTIC_J2000
            && spacecraft.InitialOrbitalParameters.Frame != Frame.ECLIPTIC_B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frame.GALACTIC_SYSTEM2)
        {
            throw new ArgumentException("Spacecraft initial orbital parameters must be defined in inertial frame", nameof(spacecraft));
        }
    }

    public void Dispose()
    {
    }
}
