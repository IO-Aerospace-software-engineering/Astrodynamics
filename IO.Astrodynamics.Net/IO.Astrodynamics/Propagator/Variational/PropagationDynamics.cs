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
/// for bit. The orientation cache of the central body frame, cleared at the end of the propagation, is put back for the
/// duration of the evaluation. Evaluations are serialized by a lock: the stepper and the force partials reuse buffers.
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
    internal PropagationDynamics(IReadOnlyList<ForceBase> forces, ILocalizable observer, Frame frame, in Time epoch,
        Frame orientationCacheFrame, PropagationFrameOrientationCache orientationCache, VariationalOptions options,
        VariationalEquations propagationEquations)
    {
        ArgumentNullException.ThrowIfNull(forces);
        _forces = forces.ToArray();
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        ArgumentNullException.ThrowIfNull(frame);
        _orientationCacheFrame = orientationCacheFrame;
        _orientationCache = orientationCache;
        Options = options;
        _propagationEquations = propagationEquations;

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
        }
    }

    /// <summary>Observer of the integrated state.</summary>
    internal ILocalizable Observer { get; }

    /// <summary>The options of the variational equations, or null when they were off.</summary>
    internal VariationalOptions Options { get; }

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
    internal void Evaluate(PropagationSegment segment, double t, Span<double> y, Span<double> q)
    {
        var data = VariationalData(segment);
        lock (_lock)
        {
            SegmentValues(segment, data, t, _segmentY, _segmentQ);
            VariationalEquations.Compose(_evaluationEquations.ColumnCount, _segmentY, _segmentQ, data.EntryY,
                data.EntryQ, y, q);
        }
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

        var previousCache = PutOrientationCacheBack();
        try
        {
            var state = Step(segment, context, k, tau);
            _evaluationEquations.Step(_stepper.StageStates, _forces, context, tau, yStart, qStart, y, q);
            return state;
        }
        finally
        {
            RestoreOrientationCache(previousCache);
        }
    }

    private (Vector3 Position, Vector3 Velocity) StateInsideStep(PropagationSegment segment, int k, double tau)
    {
        var context = ContextOf(segment);
        var previousCache = PutOrientationCacheBack();
        try
        {
            return Step(segment, context, k, tau);
        }
        finally
        {
            RestoreOrientationCache(previousCache);
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

    // The orientation cache of the central body frame, cleared at the end of the propagation, put back for an evaluation;
    // returns the cache it replaces
    private PropagationFrameOrientationCache PutOrientationCacheBack()
    {
        if (_orientationCacheFrame == null || _orientationCache == null)
            return null;

        var previousCache = _orientationCacheFrame.OrientationCache;
        _orientationCacheFrame.OrientationCache = _orientationCache;
        return previousCache;
    }

    private void RestoreOrientationCache(PropagationFrameOrientationCache previousCache)
    {
        if (_orientationCacheFrame != null && _orientationCache != null)
        {
            _orientationCacheFrame.OrientationCache = previousCache;
        }
    }
}
