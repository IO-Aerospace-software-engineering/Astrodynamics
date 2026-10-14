// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// What an RK7(8) propagation needs to evaluate its variational equations again after the fact: its forces, the
/// observer and frame of the integrated state, and the orientation cache it used.
/// </summary>
/// <remarks>
/// <para>
/// Kept by the <see cref="PropagationSolution"/> of every RK7(8) propagation, with or without the variational equations,
/// for the evaluations at any epoch (A3) and for the replay of the conjunction analysis (step 10). It keeps the forces,
/// and through them the ephemeris cache, alive as long as the solution.
/// </para>
/// <para>
/// An evaluation computes a shortened RK7(8) step with <see cref="RK78Stepper"/>, the same code as the integrator, in
/// the context of the segment (its mass and coefficients), so a step of the full size reproduces the stored values bit
/// for bit. The orientation cache of the propagation is made active for the central body frame on the evaluating thread
/// only, for the duration of the evaluation (<see cref="Frame.UseOrientationCache"/>): the shared frame is never
/// changed, so evaluations of solutions sharing a central body may run concurrently. Evaluations of one solution are
/// serialized by its lock: the stepper and the force partials reuse buffers.
/// </para>
/// </remarks>
internal sealed class PropagationDynamics
{
    private readonly object _lock = new();
    private readonly ForceBase[] _forces;
    private readonly Frame _orientationCacheFrame;
    private readonly PropagationFrameOrientationCache _orientationCache;
    private readonly RK78Stepper _stepper;
    private readonly VariationalEquations _propagationEquations;
    private readonly VariationalEquations _evaluationEquations;
    private readonly double[] _identity;
    private readonly double[] _zeroCovariance;
    private readonly double[] _segmentY;
    private readonly double[] _segmentQ;
    private readonly double[] _cumulativeY;
    private readonly double[] _cumulativeQ;

    /// <summary>
    /// Record the dynamics of a propagation.
    /// </summary>
    /// <param name="forces">The forces of the integrator, in their order.</param>
    /// <param name="observer">Observer of the integrated state.</param>
    /// <param name="frame">Frame of the integrated state.</param>
    /// <param name="epoch">Initial epoch of the propagation.</param>
    /// <param name="orientationCacheFrame">The frame whose orientation cache the propagation used, or null.</param>
    /// <param name="orientationCache">That cache, or null.</param>
    /// <param name="options">The options of the variational equations, or null when they are off.</param>
    /// <param name="propagationEquations">The equations of the integrator, or null when they are off.</param>
    /// <param name="covariance">The propagated covariance, or null when it is off.</param>
    internal PropagationDynamics(IReadOnlyList<ForceBase> forces, ILocalizable observer, Frame frame, in Time epoch,
        Frame orientationCacheFrame, PropagationFrameOrientationCache orientationCache, VariationalOptions options,
        VariationalEquations propagationEquations, CovarianceModel covariance = null)
    {
        ArgumentNullException.ThrowIfNull(forces);
        _forces = forces.ToArray();
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        ArgumentNullException.ThrowIfNull(frame);
        _orientationCacheFrame = orientationCacheFrame;
        _orientationCache = orientationCache;
        Options = options;
        _propagationEquations = propagationEquations;
        Covariance = covariance;

        _stepper = new RK78Stepper(_forces);
        _stepper.Reset(observer, frame, epoch);
        if (options != null)
        {
            _evaluationEquations = new VariationalEquations(options);
            _identity = new double[_evaluationEquations.YLength];
            _evaluationEquations.SetIdentity(_identity);
            int covarianceLength = _evaluationEquations.HasProcessNoise ? VariationalEquations.CovarianceLength : 0;
            _zeroCovariance = new double[covarianceLength];
            _segmentY = new double[_evaluationEquations.YLength];
            _segmentQ = new double[covarianceLength];
            _cumulativeY = new double[_evaluationEquations.YLength];
            _cumulativeQ = new double[_evaluationEquations.CarriedCovarianceLength];
        }
    }

    /// <summary>Observer of the integrated state.</summary>
    internal ILocalizable Observer { get; }

    /// <summary>The options of the variational equations, or null when they were off.</summary>
    internal VariationalOptions Options { get; }

    /// <summary>The propagated covariance, or null when it was off.</summary>
    internal CovarianceModel Covariance { get; }

    /// <summary>
    /// Stage Jacobian evaluations of the propagation and of the evaluations after it (13 per step).
    /// </summary>
    internal long StageJacobianEvaluations =>
        (_propagationEquations?.StageJacobianEvaluations ?? 0) + (_evaluationEquations?.StageJacobianEvaluations ?? 0);

    /// <summary>
    /// The cumulative Y and Q, from the start of the propagation, at <paramref name="t"/> seconds from the base epoch of
    /// <paramref name="segment"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The segment holds no variational data.</exception>
    /// <exception cref="ArgumentException"><paramref name="y"/> does not hold 6n values, or <paramref name="q"/> does
    /// not hold 21 values when Q is carried (process noise or covariance) and none otherwise.</exception>
    internal void Evaluate(PropagationSegment segment, double t, Span<double> y, Span<double> q)
    {
        var data = VariationalData(segment);
        if (y.Length != _evaluationEquations.YLength)
            throw new ArgumentException(
                $"Y holds {_evaluationEquations.YLength} values (6 × {_evaluationEquations.ColumnCount}); got {y.Length}.",
                nameof(y));
        if (q.Length != _cumulativeQ.Length)
            throw new ArgumentException(
                _cumulativeQ.Length == 0
                    ? $"Q must be empty without process noise or covariance; got {q.Length} values."
                    : $"Q holds {_cumulativeQ.Length} values with process noise or covariance; got {q.Length}.",
                nameof(q));

        lock (_lock)
        {
            EvaluateCore(segment, data, t, y, q);
        }
    }

    /// <summary>
    /// The propagated covariance P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q at <paramref name="t"/> seconds from the base epoch of
    /// <paramref name="segment"/> (<see cref="CovarianceModel.Covariance"/>), or the extended covariance
    /// (<see cref="CovarianceModel.ExtendedCovariance"/>).
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <param name="t">Seconds from its base epoch.</param>
    /// <param name="covariance">P, 36 values, or the extended covariance, m² values.</param>
    /// <param name="extended">Whether the extended covariance is wanted.</param>
    /// <exception cref="InvalidOperationException">The propagation did not propagate the covariance.</exception>
    /// <exception cref="ArgumentException"><paramref name="covariance"/> does not hold 36 values, or m² values for the
    /// extended covariance; checked before the evaluation.</exception>
    internal void EvaluateCovariance(PropagationSegment segment, double t, Span<double> covariance, bool extended)
    {
        if (Covariance == null)
            throw new InvalidOperationException(
                "The solution holds no covariance: the propagation did not propagate the covariance.");

        int dimension = extended ? Covariance.ExtendedDimension : VariationalEquations.StateDimension;
        if (covariance.Length != dimension * dimension)
            throw new ArgumentException(
                $"The {(extended ? "extended covariance" : "covariance")} holds {dimension * dimension} values " +
                $"({dimension}×{dimension}); got {covariance.Length}.", nameof(covariance));

        var data = VariationalData(segment);
        lock (_lock)
        {
            EvaluateCore(segment, data, t, _cumulativeY, _cumulativeQ);
            if (extended)
                Covariance.ExtendedCovariance(_cumulativeY, _cumulativeQ, covariance);
            else
                Covariance.Covariance(_cumulativeY, _cumulativeQ, covariance);
        }
    }

    private void EvaluateCore(PropagationSegment segment, VariationalSegmentData data, double t, Span<double> y,
        Span<double> q)
    {
        SegmentValues(segment, data, t, _segmentY, _segmentQ);
        VariationalEquations.Compose(_evaluationEquations.ColumnCount, _segmentY, _segmentQ, data.EntryY, data.EntryQ,
            y, q);
    }

    /// <summary>
    /// A shortened step of <paramref name="tau"/> seconds from the start of accepted step <paramref name="step"/> of
    /// <paramref name="segment"/>: Y and Q relative to the segment start, and the state, at the end of that step.
    /// </summary>
    /// <exception cref="InvalidOperationException">The segment holds no variational data.</exception>
    internal (Vector3 Position, Vector3 Velocity) ShortenedStep(PropagationSegment segment, int step, double tau,
        Span<double> ySegment, Span<double> qSegment)
    {
        var data = VariationalData(segment);
        lock (_lock)
        {
            return ShortenedStepCore(segment, data, step, tau, ySegment, qSegment);
        }
    }

    /// <summary>
    /// The state at <paramref name="t"/> seconds from the base epoch of <paramref name="segment"/>: the stored state at a
    /// step boundary, a shortened step from the start of the accepted step that contains <paramref name="t"/> otherwise
    /// (#363). Before the segment, its first state; after it, its last state.
    /// </summary>
    /// <remarks>
    /// The state has the accuracy of the integrator, unlike the cubic Hermite interpolation of
    /// <see cref="PropagationSegment.InterpolateAt"/>. Inside a step it costs one RK7(8) step, 13 evaluations of the
    /// forces; at a step boundary, none.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The segment holds no steps or no context.</exception>
    internal (Vector3 Position, Vector3 Velocity) StateAt(PropagationSegment segment, double t)
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (segment.Steps.Count == 0)
            throw new InvalidOperationException("Segment contains no steps.");

        if (t <= 0.0)
            return (segment.Steps[0].StartPosition, segment.Steps[0].StartVelocity);

        // The end of a step as the integrator computes it (t_k + h_k), so that its exact boundary reads the stored state
        int k = segment.FindStepIndex(System.Math.Min(t, segment.Duration));
        var step = segment.Steps[k];
        if (t >= step.CumulativeTime + step.StepSize)
            return (step.EndPosition, step.EndVelocity);

        lock (_lock)
        {
            return StateInsideStep(segment, k, t - step.CumulativeTime);
        }
    }

    private VariationalSegmentData VariationalData(PropagationSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (_evaluationEquations == null || segment.Variational == null)
        {
            throw new InvalidOperationException(
                "The segment holds no variational data: the propagation did not integrate the variational equations.");
        }

        return segment.Variational;
    }

    // Y and Q relative to the segment start at t: the stored values at a step boundary, a shortened step inside a step
    private void SegmentValues(PropagationSegment segment, VariationalSegmentData data, double t, Span<double> y,
        Span<double> q)
    {
        if (segment.Steps.Count == 0 || t <= 0.0)
        {
            _identity.AsSpan().CopyTo(y);
            _zeroCovariance.AsSpan().CopyTo(q);
            return;
        }

        // The end of a step as the integrator computes it (t_k + h_k), so that its exact boundary reads the stored values
        int k = segment.FindStepIndex(System.Math.Min(t, segment.Duration));
        var step = segment.Steps[k];
        if (t >= step.CumulativeTime + step.StepSize)
        {
            data.StepEndY(k).CopyTo(y);
            data.StepEndQ(k).CopyTo(q);
        }
        else
        {
            ShortenedStepCore(segment, data, k, t - step.CumulativeTime, y, q);
        }
    }

    private (Vector3 Position, Vector3 Velocity) ShortenedStepCore(PropagationSegment segment,
        VariationalSegmentData data, int k, double tau, Span<double> y, Span<double> q)
    {
        var context = ContextOf(segment);
        ReadOnlySpan<double> yStart = k == 0 ? _identity : data.StepEndY(k - 1);
        ReadOnlySpan<double> qStart = k == 0 ? _zeroCovariance : data.StepEndQ(k - 1);

        using (Frame.UseOrientationCache(_orientationCacheFrame, _orientationCache))
        {
            var state = Step(segment, context, k, tau);
            _evaluationEquations.Step(_stepper.StageStates, _forces, context, tau, yStart, qStart, y, q);
            return state;
        }
    }

    private (Vector3 Position, Vector3 Velocity) StateInsideStep(PropagationSegment segment, int k, double tau)
    {
        var context = ContextOf(segment);
        using (Frame.UseOrientationCache(_orientationCacheFrame, _orientationCache))
        {
            return Step(segment, context, k, tau);
        }
    }

    private static ForceEvaluationContext ContextOf(PropagationSegment segment) =>
        segment.Context ?? throw new InvalidOperationException("The segment holds no context.");

    // A step of tau seconds from the start of accepted step k, in the context of the segment; its stage states stay in
    // the stepper for the variational step
    private (Vector3 Position, Vector3 Velocity) Step(PropagationSegment segment, in ForceEvaluationContext context,
        int k, double tau)
    {
        var step = segment.Steps[k];
        _stepper.Context = context;
        _stepper.Step(step.StartPosition, step.StartVelocity, segment.BaseEpoch, step.CumulativeTime, tau,
            out var position, out var velocity, out _, out _);
        return (position, velocity);
    }
}
