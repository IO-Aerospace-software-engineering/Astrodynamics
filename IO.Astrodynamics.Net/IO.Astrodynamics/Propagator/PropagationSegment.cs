// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// One continuous propagation arc (no discontinuities): the accepted integration steps from its base epoch.
/// </summary>
/// <remarks>
/// Immutable from outside the library (decision 55 of #668, decided by S. Guillet, 2026-11-23, #796): an
/// <see cref="Integrators.IIntegrator"/> implemented outside it collects its <see cref="AcceptedStep"/> values and builds
/// its segment once, with <see cref="PropagationSegment(Time, IEnumerable{AcceptedStep})"/>; <see cref="Steps"/> is a
/// read-only view. Before 11.0.0 <c>AddStep</c> was public, so the steps of a published solution could be extended.
/// </remarks>
public sealed class PropagationSegment
{
    private readonly List<AcceptedStep> _steps;
    private readonly ReadOnlyCollection<AcceptedStep> _stepsView;

    /// <summary>
    /// Absolute start time of this segment, a TDB label.
    /// </summary>
    public Time BaseEpoch { get; }

    /// <summary>
    /// Ordered accepted steps in this segment, a read-only view (a cast cannot write it).
    /// </summary>
    public IReadOnlyList<AcceptedStep> Steps => _stepsView;

    /// <summary>
    /// Total elapsed time from BaseEpoch to the end of the last step, in seconds of the time argument of the propagation:
    /// TT seconds about the Earth, TDB seconds about any other central body (#501).
    /// </summary>
    public double Duration => _steps.Count > 0
        ? _steps[^1].CumulativeTime + _steps[^1].StepSize
        : 0.0;

    /// <summary>
    /// The mass and coefficients of the spacecraft during the segment, set by the propagator in RK7(8); null otherwise.
    /// The mass is constant within a segment: fuel burns only at the impulsive maneuvers between segments.
    /// </summary>
    internal ForceEvaluationContext? Context { get; set; }

    /// <summary>
    /// The variational data of the segment, when the propagation integrates the variational equations; null otherwise.
    /// </summary>
    internal VariationalSegmentData Variational { get; set; }

    /// <summary>
    /// The impulsive maneuver that starts the segment, set by the propagator in RK7(8) with or without the variational
    /// equations; null for the first segment and after an event without a maneuver. The replay of the variational
    /// equations by the conjunction analysis adds its execution error there (C5).
    /// </summary>
    internal ManeuverRecord? EntryManeuver { get; set; }

    /// <summary>
    /// The time of the event that ends the segment, in seconds of the time argument from <see cref="BaseEpoch"/>, as the
    /// propagator handled it, set in RK7(8); null when no event ends it. The replay evaluates the variational values
    /// there, as the propagation did, so that the next segment starts from the same doubles.
    /// </summary>
    internal double? EventTime { get; set; }

    /// <summary>
    /// A segment of no step from <paramref name="baseEpoch"/>: what an integrator returns for a duration of zero.
    /// </summary>
    /// <param name="baseEpoch">The start of the segment, a TDB label.</param>
    public PropagationSegment(Time baseEpoch)
    {
        BaseEpoch = baseEpoch;
        _steps = new List<AcceptedStep>();
        _stepsView = _steps.AsReadOnly();
    }

    /// <summary>
    /// A segment of the given steps from <paramref name="baseEpoch"/>, copied: a later change of
    /// <paramref name="steps"/> does not reach it (decision 55 of #668).
    /// </summary>
    /// <param name="baseEpoch">The start of the segment, a TDB label.</param>
    /// <param name="steps">The accepted steps, in order, their times counted from <paramref name="baseEpoch"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="steps"/> is null.</exception>
    public PropagationSegment(Time baseEpoch, IEnumerable<AcceptedStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        BaseEpoch = baseEpoch;
        _steps = new List<AcceptedStep>(steps);
        _stepsView = _steps.AsReadOnly();
    }

    /// <summary>
    /// An empty segment with room for <paramref name="estimatedStepCount"/> steps, filled by the library's integrators
    /// through <see cref="AddStep"/>; internal since 11.0.0 (decision 55 of #668).
    /// </summary>
    internal PropagationSegment(Time baseEpoch, int estimatedStepCount)
    {
        BaseEpoch = baseEpoch;
        _steps = new List<AcceptedStep>(estimatedStepCount);
        _stepsView = _steps.AsReadOnly();
    }

    /// <summary>
    /// Add an accepted step to this segment; internal since 11.0.0 (decision 55 of #668).
    /// </summary>
    internal void AddStep(in AcceptedStep step)
    {
        _steps.Add(step);
    }

    /// <summary>
    /// Replace the last accepted step: the step of an event that Velocity-Verlet integrates again to the label of the
    /// event (#572).
    /// </summary>
    internal void ReplaceLastStep(in AcceptedStep step)
    {
        _steps[^1] = step;
    }

    /// <summary>
    /// Interpolate position and velocity at time t (seconds from BaseEpoch)
    /// using cubic Hermite interpolation within the appropriate step.
    /// </summary>
    /// <remarks>
    /// The interpolation is exact at the step boundaries only. Between them its error grows as the fourth power of the
    /// step size: about 100 m in LEO for the steps of an RK7(8) integration at its default tolerance. For an RK7(8)
    /// propagation, <see cref="PropagationSolution.InterpolateAt"/> computes the state at the accuracy of the
    /// integrator instead. Internal since 11.0.0 (decision 55 of #668): the reader of a solution is
    /// <see cref="PropagationSolution.GetStateVector"/>.
    /// </remarks>
    internal (Vector3 position, Vector3 velocity) InterpolateAt(double t)
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Segment contains no steps.");

        if (t <= 0.0)
            return (_steps[0].StartPosition, _steps[0].StartVelocity);

        double duration = Duration;
        if (t >= duration)
            return (_steps[^1].EndPosition, _steps[^1].EndVelocity);

        // Binary search for the step containing time t
        int idx = FindStepIndex(t);
        var step = _steps[idx];

        return HermiteInterpolate(in step, t);
    }

    /// <summary>
    /// Find the index of the step containing time t using binary search. At the boundary between two steps, the
    /// earlier one.
    /// </summary>
    internal int FindStepIndex(double t)
    {
        int lo = 0;
        int hi = _steps.Count - 1;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            double stepEnd = _steps[mid].CumulativeTime + _steps[mid].StepSize;

            if (t > stepEnd)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    /// <summary>
    /// Cubic Hermite interpolation within an accepted step; internal since 11.0.0 (decision 55 of #668).
    /// </summary>
    internal static (Vector3 position, Vector3 velocity) HermiteInterpolate(in AcceptedStep step, double t)
    {
        double localT = t - step.CumulativeTime;
        double theta = localT / step.StepSize;
        double h = step.StepSize;

        double t2 = theta * theta;
        double t3 = t2 * theta;

        // Hermite basis functions
        double h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
        double h10 = t3 - 2.0 * t2 + theta;
        double h01 = -2.0 * t3 + 3.0 * t2;
        double h11 = t3 - t2;

        // Position: H00*r0 + H10*h*v0 + H01*r1 + H11*h*v1
        var pos = step.StartPosition * h00 + step.StartVelocity * (h10 * h)
                  + step.EndPosition * h01 + step.EndVelocity * (h11 * h);

        // Velocity: H00*v0 + H10*h*a0 + H01*v1 + H11*h*a1
        var vel = step.StartVelocity * h00 + step.StartAcceleration * (h10 * h)
                  + step.EndVelocity * h01 + step.EndAcceleration * (h11 * h);

        return (pos, vel);
    }
}
