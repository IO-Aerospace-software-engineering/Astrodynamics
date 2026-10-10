// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Prince-Dormand 7(8) Runge-Kutta integrator with adaptive or fixed step-size control.
/// 13-stage, 8th-order accurate with embedded 7th-order error estimate.
/// Implements the PI step-size controller from Hairer, Norsett, Wanner §II.4
/// and a mixed absolute/relative error norm per component (Montenbruck &amp; Gill §4.4).
/// Produces a PropagationSegment with AcceptedSteps for dense output interpolation.
/// </summary>
public sealed class RK78Integrator : Integrator
{
    private const int MaxRejections = 100;
    private const double LandingTolerance = 1e-12;

    /// <summary>Absolute error tolerance (meters for position, m/s for velocity).</summary>
    public double AbsoluteTolerance { get; }

    /// <summary>Relative error tolerance (dimensionless).</summary>
    public double RelativeTolerance { get; }

    /// <summary>Whether the integrator uses adaptive step-size control.</summary>
    public bool AdaptiveMode { get; }

    // Step controller (null in fixed mode)
    private readonly RK78StepController _controller;

    // Initial step size for reset at segment boundaries
    private readonly double _initialH;

    // Current internal step size (seconds, always positive)
    private double _currentH;

    // The 13 stages of a step, with their reusable stage states (avoids per-step allocations)
    private readonly RK78Stepper _stepper;

    // Variational equations, null when off; Y and Q relative to the segment start, at the start and end of a step
    private VariationalEquations _variational;
    private bool _variationalInStepControl;
    private double[] _variationalY0;
    private double[] _variationalY1;
    private double[] _variationalQ0;
    private double[] _variationalQ1;
    private double[] _variationalScales;

    // The next segment, set by BeginVariationalSegment
    private bool _variationalSegmentPending;
    private ForceEvaluationContext _variationalContext;
    private double[] _variationalEntryY;
    private double[] _variationalEntryQ;
    private ManeuverRecord? _variationalEntryManeuver;

    /// <summary>
    /// Create an adaptive RK7(8) integrator.
    /// Forces are added externally via <see cref="Integrator.AddForce"/>;
    /// call <see cref="Initialize"/> with the initial state before propagation.
    /// </summary>
    public RK78Integrator(
        double absoluteTolerance = 1e-9,
        double relativeTolerance = 1e-9,
        double initialStepSize = 60.0,
        double minStepSize = 1e-6,
        double maxStepSize = 86400.0,
        double safetyFactor = 0.9,
        double minFactor = 0.2,
        double maxFactor = 5.0)
    {
        if (absoluteTolerance <= 0) throw new ArgumentOutOfRangeException(nameof(absoluteTolerance), "Must be positive.");
        if (relativeTolerance <= 0) throw new ArgumentOutOfRangeException(nameof(relativeTolerance), "Must be positive.");
        if (initialStepSize <= 0) throw new ArgumentOutOfRangeException(nameof(initialStepSize), "Must be positive.");

        AbsoluteTolerance = absoluteTolerance;
        RelativeTolerance = relativeTolerance;
        AdaptiveMode = true;

        _controller = new RK78StepController(safetyFactor, minFactor, maxFactor, minStepSize, maxStepSize);
        _initialH = initialStepSize;
        _currentH = initialStepSize;

        _stepper = new RK78Stepper(ForceList);
    }

    /// <summary>
    /// Create a fixed-step RK7(8) integrator (no error control).
    /// Uses the 8th-order solution unconditionally.
    /// </summary>
    public RK78Integrator(double fixedStepSize)
    {
        if (fixedStepSize <= 0) throw new ArgumentOutOfRangeException(nameof(fixedStepSize), "Must be positive.");

        AbsoluteTolerance = double.MaxValue;
        RelativeTolerance = double.MaxValue;
        AdaptiveMode = false;

        _controller = null;
        _initialH = fixedStepSize;
        _currentH = fixedStepSize;

        _stepper = new RK78Stepper(ForceList);
    }

    /// <summary>
    /// Initialize the integrator with the initial state.
    /// Sets the observer and frame, allocates the stage pool, and resets the PI controller.
    /// Called by the propagator after forces have been added and at segment boundaries.
    /// </summary>
    public override void Initialize(StateVector initialState)
    {
        if (initialState == null) throw new ArgumentNullException(nameof(initialState));
        base.Initialize(initialState);

        _stepper.Reset(Observer, ReferenceFrame, initialState.Epoch);

        // Reset step size and PI controller at segment boundaries.
        // After a maneuver the velocity is discontinuous, so the previous segment's
        // step size is inappropriate — start conservatively to avoid stepping over
        // nearby events (e.g., a periapsis crossing immediately after a burn).
        _currentH = _initialH;
        _controller?.Reset();
    }

    /// <summary>
    /// The variational equations integrated with the state, null when they are off.
    /// </summary>
    internal VariationalEquations VariationalEquations => _variational;

    /// <summary>
    /// Integrate the variational equations with the state from now on.
    /// </summary>
    /// <param name="options">What to integrate.</param>
    internal void EnableVariationalEquations(VariationalOptions options)
    {
        _variational = new VariationalEquations(options);
        _variationalInStepControl = options.IncludeInStepControl && AdaptiveMode;
        int covarianceLength = _variational.HasProcessNoise ? VariationalEquations.CovarianceLength : 0;
        _variationalY0 = new double[_variational.YLength];
        _variationalY1 = new double[_variational.YLength];
        _variationalQ0 = new double[covarianceLength];
        _variationalQ1 = new double[covarianceLength];
        _variationalScales = new double[_variational.ColumnCount];
    }

    /// <summary>
    /// Prepare the variational data of the next segment: <see cref="IntegrateSegment"/> creates it, attaches it to the
    /// segment and fills it with Y and Q at the end of each accepted step.
    /// </summary>
    /// <param name="context">Mass and coefficients of the segment.</param>
    /// <param name="entryY">Cumulative Y at the segment start.</param>
    /// <param name="entryQ">Cumulative Q at the segment start; empty without process noise.</param>
    /// <param name="entryManeuver">The maneuver that starts the segment, if any.</param>
    /// <exception cref="InvalidOperationException">The variational equations are off.</exception>
    internal void BeginVariationalSegment(in ForceEvaluationContext context, double[] entryY, double[] entryQ,
        ManeuverRecord? entryManeuver)
    {
        if (_variational == null)
            throw new InvalidOperationException("The variational equations are not enabled on this integrator.");

        _variationalSegmentPending = true;
        _variationalContext = context;
        _variationalEntryY = entryY;
        _variationalEntryQ = entryQ;
        _variationalEntryManeuver = entryManeuver;
    }

    /// <summary>
    /// Integrate a full segment from the given start state.
    /// The integrator runs free with adaptive (or fixed) step control.
    /// Each accepted step is stored as an AcceptedStep for dense output.
    /// Event detectors are checked after each accepted step.
    /// </summary>
    public override IntegrationResult IntegrateSegment(
        Vector3 startPosition, Vector3 startVelocity,
        Time baseEpoch, double duration,
        IReadOnlyList<IEventDetector> eventDetectors = null)
    {
        int estimatedSteps = AdaptiveMode
            ? (int)(duration / _currentH) + 16
            : (int)(duration / _currentH) + 2;
        var segment = new PropagationSegment(baseEpoch, estimatedSteps);
        var variational = StartVariationalSegment(segment, estimatedSteps, startPosition, startVelocity);

        var pos = startPosition;
        var vel = startVelocity;
        double t = 0.0;
        double h = System.Math.Min(_currentH, duration);
        int rejections = 0;

        // Initialize event detector g-values
        double[] prevG = null;
        double[] currG = null;
        if (eventDetectors != null && eventDetectors.Count > 0)
        {
            prevG = new double[eventDetectors.Count];
            currG = new double[eventDetectors.Count];
            UpdateEvalState(pos, vel, baseEpoch);
            for (int i = 0; i < eventDetectors.Count; i++)
            {
                prevG[i] = eventDetectors[i].IsActive ? eventDetectors[i].Evaluate(EvalState) : 0.0;
            }
        }

        while (true)
        {
            double remaining = duration - t;

            if (remaining <= LandingTolerance * System.Math.Max(1.0, duration))
                break;

            if (h > remaining)
                h = remaining;

            // Compute one RK78 step
            var (posNew, velNew, err) = ComputeRK78Step(pos, vel, baseEpoch, t, h);

            if (variational != null && _variationalInStepControl)
            {
                AdvanceVariational(h);
                err = System.Math.Max(err, _variational.ScaledError(_variationalY0, _variationalY1, _variationalScales,
                    AbsoluteTolerance, RelativeTolerance));
            }

            if (AdaptiveMode)
            {
                var (accepted, hNew) = _controller.Evaluate(h, err);

                if (!accepted)
                {
                    rejections++;
                    if (rejections > MaxRejections)
                        throw new InvalidOperationException(
                            $"RK78 adaptive step failed: {MaxRejections} consecutive rejections at t={t:F6}s. " +
                            $"Error={err:E3}, h={h:E6}s. Consider relaxing tolerances or checking force model.");

                    h = hNew;
                    continue;
                }

                rejections = 0;

                // Store accepted step
                segment.AddStep(new AcceptedStep(t, h, pos, vel, posNew, velNew, _stepper.StartAcceleration,
                    _stepper.EndAcceleration));
                if (variational != null)
                {
                    if (!_variationalInStepControl)
                    {
                        AdvanceVariational(h);
                    }

                    CommitVariational(variational);
                }

                t += h;
                pos = posNew;
                vel = velNew;

                // Check event detectors
                if (eventDetectors != null && eventDetectors.Count > 0)
                {
                    UpdateEvalState(pos, vel, baseEpoch.AddSeconds(t));
                    int detIdx = CheckEventDetectors(eventDetectors, EvalState, prevG, currG);

                    if (detIdx >= 0)
                    {
                        _currentH = hNew;
                        return new IntegrationResult(segment,
                            LocateEvent(segment, eventDetectors[detIdx], detIdx, prevG[detIdx], currG[detIdx]));
                    }

                    (prevG, currG) = (currG, prevG);
                }

                // Continue with new step size
                double newRemaining = duration - t;
                h = System.Math.Min(hNew, newRemaining);
                _currentH = hNew;
            }
            else
            {
                // Fixed step — always accepted
                segment.AddStep(new AcceptedStep(t, h, pos, vel, posNew, velNew, _stepper.StartAcceleration,
                    _stepper.EndAcceleration));
                if (variational != null)
                {
                    AdvanceVariational(h);
                    CommitVariational(variational);
                }

                t += h;
                pos = posNew;
                vel = velNew;

                // Check event detectors
                if (eventDetectors != null && eventDetectors.Count > 0)
                {
                    UpdateEvalState(pos, vel, baseEpoch.AddSeconds(t));
                    int detIdx = CheckEventDetectors(eventDetectors, EvalState, prevG, currG);

                    if (detIdx >= 0)
                    {
                        return new IntegrationResult(segment,
                            LocateEvent(segment, eventDetectors[detIdx], detIdx, prevG[detIdx], currG[detIdx]));
                    }

                    (prevG, currG) = (currG, prevG);
                }
            }
        }

        return new IntegrationResult(segment, null);
    }

    // The variational data of the segment, when BeginVariationalSegment prepared it; Y and Q restart at [I | 0] and 0
    private VariationalSegmentData StartVariationalSegment(PropagationSegment segment, int estimatedSteps,
        in Vector3 startPosition, in Vector3 startVelocity)
    {
        if (!_variationalSegmentPending)
            return null;

        _variationalSegmentPending = false;
        var data = new VariationalSegmentData(_variational.YLength, _variational.HasProcessNoise, estimatedSteps,
            _variationalEntryY, _variationalEntryQ, _variationalEntryManeuver);
        segment.Variational = data;
        _variational.SetIdentity(_variationalY0);
        Array.Clear(_variationalQ0);
        if (_variationalInStepControl)
        {
            _variational.ColumnScales(startPosition, startVelocity, _variationalContext, _variationalScales);
        }

        return data;
    }

    // Y and Q at the end of the step just computed, from the stage states of that step
    private void AdvanceVariational(double h)
    {
        _variational.Step(_stepper.StageStates, ForceList, _variationalContext, h, _variationalY0, _variationalQ0,
            _variationalY1, _variationalQ1);
    }

    // The step is accepted: store its end values, which become the start values of the next step
    private void CommitVariational(VariationalSegmentData data)
    {
        data.Append(_variationalY1, _variationalQ1);
        (_variationalY0, _variationalY1) = (_variationalY1, _variationalY0);
        (_variationalQ0, _variationalQ1) = (_variationalQ1, _variationalQ0);
    }

    /// <summary>
    /// Locate the event detected in the last accepted step of <paramref name="segment"/>, and the state at the event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every state comes from a shortened RK7(8) step from the start of the last accepted step, so the event is located
    /// on the integrated trajectory, and the propagation restarts from a state at the accuracy of the integrator (#363).
    /// The step is the one <see cref="PropagationDynamics"/> computes for the variational values at the event, so both
    /// are the same doubles.
    /// </para>
    /// <para>
    /// The event time is then moved to the epoch the propagator restarts from, which <see cref="Time"/> holds to
    /// 100 ns, so that the state, the epoch of the next segment and the variational values refer to the same instant.
    /// </para>
    /// </remarks>
    private EventInfo LocateEvent(PropagationSegment segment, IEventDetector detector, int detectorIndex, double gStart,
        double gEnd)
    {
        var step = segment.Steps[^1];
        double stepEnd = step.CumulativeTime + step.StepSize;
        var baseEpoch = segment.BaseEpoch;

        double eventT = BisectionEventFinder.FindRoot(step.CumulativeTime, stepEnd, t =>
        {
            var (p, v) = StateInsideStep(step, baseEpoch, t);
            return detector.Evaluate(CreateTempState(p, v, baseEpoch.AddSeconds(t)));
        }, gStart, gEnd);

        eventT = OnEpochGrid(baseEpoch, baseEpoch.AddSeconds(eventT), step.CumulativeTime, stepEnd);
        var (eventPos, eventVel) = StateInsideStep(step, baseEpoch, eventT);
        return new EventInfo(detectorIndex, eventT, eventPos, eventVel);
    }

    // The state at t, in seconds from the segment base epoch, by a shortened step from the start of the accepted step
    private (Vector3 Position, Vector3 Velocity) StateInsideStep(in AcceptedStep step, in Time baseEpoch, double t)
    {
        _stepper.Step(step.StartPosition, step.StartVelocity, baseEpoch, step.CumulativeTime, t - step.CumulativeTime,
            out var position, out var velocity, out _, out _);
        return (position, velocity);
    }

    /// <summary>
    /// A time t, in seconds from <paramref name="baseEpoch"/> and within [<paramref name="tStart"/>,
    /// <paramref name="tEnd"/>], that <see cref="Time.AddSeconds"/> carries to <paramref name="epoch"/> from
    /// <paramref name="baseEpoch"/>, as the propagator does with the event time; the time of
    /// <paramref name="epoch"/> clamped to the interval when there is none.
    /// </summary>
    internal static double OnEpochGrid(in Time baseEpoch, in Time epoch, double tStart, double tEnd)
    {
        const int maxAdjustments = 64;
        double t = System.Math.Clamp((epoch - baseEpoch).TotalSeconds, tStart, tEnd);
        double onGrid = t;
        for (int i = 0; i < maxAdjustments && onGrid >= tStart && onGrid <= tEnd; i++)
        {
            var found = baseEpoch.AddSeconds(onGrid);
            if (found == epoch)
                return onGrid;

            onGrid = found < epoch ? System.Math.BitIncrement(onGrid) : System.Math.BitDecrement(onGrid);
        }

        return t;
    }

    /// <summary>
    /// Create a temporary StateVector for event detector evaluation.
    /// </summary>
    private StateVector CreateTempState(in Vector3 pos, in Vector3 vel, in Time epoch)
    {
        return new StateVector(pos, vel, Observer, epoch, ReferenceFrame);
    }

    /// <summary>
    /// Compute one RK7(8) step from state (pos0, vel0) at time baseEpoch + tOffset,
    /// advancing by step size h seconds.
    /// </summary>
    /// <returns>(posNew, velNew, normalizedError) where the step is accepted when error &lt;= 1.0.</returns>
    internal (Vector3 posNew, Vector3 velNew, double err) ComputeRK78Step(
        in Vector3 pos0, in Vector3 vel0, in Time baseEpoch, double tOffset, double h)
    {
        _stepper.Step(pos0, vel0, baseEpoch, tOffset, h, out var posNew, out var velNew, out var errPos,
            out var errVel);

        double err = ComputeErrorNorm(pos0, posNew, vel0, velNew, errPos, errVel);

        return (posNew, velNew, err);
    }

    /// <summary>
    /// Compute the normalized error using a mixed absolute/relative tolerance per component.
    /// Position and velocity components are scaled independently.
    /// Uses infinity norm (max over all 6 components) — step accepted when result &lt;= 1.0.
    /// Reference: Hairer, Norsett, Wanner §II.4: sc_i = Atol + max(|y0i|, |y1i|) Rtol is Eq. (4.10), and the maximum
    /// norm is the alternative given after the root-mean-square norm of Eq. (4.11); to be verified by S. Guillet.
    /// </summary>
    private double ComputeErrorNorm(
        in Vector3 pos0, in Vector3 posNew,
        in Vector3 vel0, in Vector3 velNew,
        in Vector3 errPos, in Vector3 errVel)
    {
        double maxErr = 0.0;

        // Position components
        maxErr = System.Math.Max(maxErr, ComponentError(pos0.X, posNew.X, errPos.X));
        maxErr = System.Math.Max(maxErr, ComponentError(pos0.Y, posNew.Y, errPos.Y));
        maxErr = System.Math.Max(maxErr, ComponentError(pos0.Z, posNew.Z, errPos.Z));

        // Velocity components
        maxErr = System.Math.Max(maxErr, ComponentError(vel0.X, velNew.X, errVel.X));
        maxErr = System.Math.Max(maxErr, ComponentError(vel0.Y, velNew.Y, errVel.Y));
        maxErr = System.Math.Max(maxErr, ComponentError(vel0.Z, velNew.Z, errVel.Z));

        return maxErr;
    }

    /// <summary>
    /// Compute the scaled error for a single component.
    /// sc = absTol + relTol * max(|y0|, |y1|)
    /// err = |errComponent| / sc
    /// </summary>
    private double ComponentError(double y0, double y1, double errComponent)
    {
        return ScaledComponentError(y0, y1, errComponent, AbsoluteTolerance, RelativeTolerance);
    }

    /// <summary>
    /// The scaled error of one component with given tolerances: |err| / (absTol + relTol * max(|y0|, |y1|)). Shared with
    /// the error of the variational equations, when it takes part in the step-size control.
    /// </summary>
    internal static double ScaledComponentError(double y0, double y1, double errComponent, double absoluteTolerance,
        double relativeTolerance)
    {
        double sc = absoluteTolerance + relativeTolerance * System.Math.Max(System.Math.Abs(y0), System.Math.Abs(y1));
        return System.Math.Abs(errComponent) / sc;
    }
}
