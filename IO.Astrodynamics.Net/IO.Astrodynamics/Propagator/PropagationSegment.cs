// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// One continuous propagation arc (no discontinuities).
/// Stores accepted integration steps and provides cubic Hermite interpolation.
/// </summary>
public sealed class PropagationSegment
{
    private readonly List<AcceptedStep> _steps;

    /// <summary>
    /// Absolute start time of this segment.
    /// </summary>
    public Time BaseEpoch { get; }

    /// <summary>
    /// Ordered accepted steps in this segment.
    /// </summary>
    public IReadOnlyList<AcceptedStep> Steps => _steps;

    /// <summary>
    /// Total elapsed time in seconds from BaseEpoch to the end of the last step.
    /// </summary>
    public double Duration => _steps.Count > 0
        ? _steps[^1].CumulativeTime + _steps[^1].StepSize
        : 0.0;

    public PropagationSegment(Time baseEpoch)
    {
        BaseEpoch = baseEpoch;
        _steps = new List<AcceptedStep>();
    }

    public PropagationSegment(Time baseEpoch, int estimatedStepCount)
    {
        BaseEpoch = baseEpoch;
        _steps = new List<AcceptedStep>(estimatedStepCount);
    }

    /// <summary>
    /// Add an accepted step to this segment.
    /// </summary>
    public void AddStep(in AcceptedStep step)
    {
        _steps.Add(step);
    }

    /// <summary>
    /// Interpolate position and velocity at time t (seconds from BaseEpoch)
    /// using cubic Hermite interpolation within the appropriate step.
    /// </summary>
    public (Vector3 position, Vector3 velocity) InterpolateAt(double t)
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
    /// Find the index of the step containing time t using binary search.
    /// </summary>
    private int FindStepIndex(double t)
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
    /// Cubic Hermite interpolation within an accepted step.
    /// </summary>
    public static (Vector3 position, Vector3 velocity) HermiteInterpolate(in AcceptedStep step, double t)
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
