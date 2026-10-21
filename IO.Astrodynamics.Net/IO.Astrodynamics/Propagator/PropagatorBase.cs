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

namespace IO.Astrodynamics.Propagator;

public abstract class PropagatorBase : IPropagator
{
    protected readonly CelestialItem OriginalObserver;
    public Window Window { get; }
    public Spacecraft Spacecraft { get; }
    public IIntegrator Integrator { get; }
    public TimeSpan DeltaT { get; }

    protected StateVector InitialState { get; set; }

    // Standalone attitude maneuver to apply at each output epoch (null if none)
    private Attitude _continuousAttitude;

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

    protected PropagatorBase(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
    {
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        Integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
        ValidateInertialFrame(spacecraft);

        OriginalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        DeltaT = deltaT;
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

        if ((initial.Epoch - start) != TimeSpan.Zero)
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
    /// (<see cref="CovarianceChecks.Validate"/>); null when it is accepted, with its symmetrized values.
    /// </summary>
    internal static string InitialCovarianceRejection(Math.Matrix p0, out double[] symmetrized)
    {
        symmetrized = null;
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

        return CovarianceChecks.Validate(values, 6, "The initial covariance", out symmetrized);
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
        // 2. Build event detectors from maneuver chain (also discovers standalone attitude maneuvers)
        _continuousAttitude = null;
        var eventDetectors = BuildEventDetectors();

        // 1. Add initial orientation (skip zero-quaternion sentinel when continuous attitude provides the real one)
        if (_continuousAttitude == null)
        {
            Spacecraft.Frame.AddStateOrientationToICRF(
                new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.StartDate,
                    Spacecraft.InitialOrbitalParameters.Frame));
        }

        // 3. Create solution
        var solution = new PropagationSolution();

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
                _covarianceModel, DynamicsSyncRoot ?? new object()) { Integrator = rk78 };
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

        // Compute required propagation duration (may exceed window for non-fitting step sizes)
        uint outputCount = (uint)System.Math.Round(Window.Length.TotalSeconds / DeltaT.TotalSeconds,
            MidpointRounding.AwayFromZero) + 1;
        double outputDuration = (outputCount - 1) * DeltaT.TotalSeconds;
        double propagationDuration = System.Math.Max(Window.Length.TotalSeconds, outputDuration);
        var propagationEnd = Window.StartDate.AddSeconds(propagationDuration);

        // 4. Segment loop
        while (true)
        {
            double remaining = (propagationEnd - currentEpoch).TotalSeconds;
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
                var eventEpoch = currentEpoch.AddSeconds(evt.EventTime);

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

                // Delegate to the event detector
                var detector = eventDetectors[evt.DetectorIndex];
                var eventResult = detector.HandleEvent(eventState, Spacecraft);

                if (eventResult.Orientation != null)
                {
                    Spacecraft.Frame.AddStateOrientationToICRF(eventResult.Orientation);
                }

                // The next segment starts at the event: from the state after the maneuver on StopAndRestart, from the
                // state at the event, unchanged, on Continue (#355: the same segment used to be integrated again, without
                // end)
                bool restart = eventResult.Action == EventAction.StopAndRestart;
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

                // Update event detectors for next segment
                eventDetectors = UpdateEventDetectors(eventResult.NextDetector);

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
        solution.Dynamics = dynamics;
        solution.ForceModel = PropagationForceModel.TryCreate(Integrator, solution, InitialState, cache,
            dynamics?.SyncRoot ?? DynamicsSyncRoot);
        solution.Observer = InitialState.Observer;
        solution.Frame = InitialState.Frame;
        solution.CovariancePropagation = _covariancePropagation;
        var outputStates = GenerateOutput(solution, outputCount);

        // 6. Add final orientation
        var latestOrientation = Spacecraft.Frame.GetLatestStateOrientationToICRF();
        Spacecraft.Frame.AddStateOrientationToICRF(
            new StateOrientation(latestOrientation.Rotation, latestOrientation.AngularVelocity,
                Window.EndDate, latestOrientation.ReferenceFrame));

        // 7. Store propagated states
        StorePropagatedStates(outputStates);

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
    /// Build event detectors from the spacecraft's standby maneuver chain.
    /// Processes leading attitude maneuvers immediately (they don't need event detection).
    /// </summary>
    private List<IEventDetector> BuildEventDetectors()
    {
        var detectors = new List<IEventDetector>();

        // Walk the maneuver chain to find the first impulse maneuver
        var maneuver = Spacecraft.StandbyManeuver;
        while (maneuver != null)
        {
            if (maneuver is ImpulseManeuver impulse)
            {
                detectors.Add(new ManeuverEventDetector(impulse));
                break;
            }

            if (maneuver is Attitude attitude)
            {
                // Process initial attitude maneuvers immediately at the initial state
                var (_, so) = attitude.TryExecute(InitialState);
                if (so.Rotation != Quaternion.Zero)
                {
                    Spacecraft.Frame.AddStateOrientationToICRF(so);
                }

                // Remember for continuous orientation computation at each output epoch
                _continuousAttitude = attitude;

                maneuver = attitude.NextManeuver;
            }
            else
            {
                break;
            }
        }

        return detectors;
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
    /// Also computes continuous attitude orientations at each output epoch if a standalone attitude maneuver exists.
    /// </summary>
    private StateVector[] GenerateOutput(PropagationSolution solution, uint count)
    {
        var output = new StateVector[count];

        for (int i = 0; i < count; i++)
        {
            var epoch = Window.StartDate + (i * DeltaT);
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

            // Apply continuous attitude maneuver at each output epoch
            if (_continuousAttitude != null)
            {
                var localSv = output[i].RelativeTo(_continuousAttitude.ManeuverCenter, Aberration.None).ToStateVector();
                var orientation = _continuousAttitude.ComputeOrientation(localSv);
                if (orientation != Quaternion.Zero)
                {
                    Spacecraft.Frame.AddStateOrientationToICRF(
                        new StateOrientation(orientation, Vector3.Zero, epoch,
                            Spacecraft.InitialOrbitalParameters.Frame));
                }
            }
        }

        return output;
    }

    protected abstract void StorePropagatedStates(StateVector[] outputStates);

    private static void ValidateInertialFrame(Spacecraft spacecraft)
    {
        if (spacecraft.InitialOrbitalParameters.Frame != Frame.ICRF
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
