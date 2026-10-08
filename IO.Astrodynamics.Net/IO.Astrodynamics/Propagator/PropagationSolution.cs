// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Complete propagation trajectory spanning multiple segments.
/// At maneuver boundaries, returns the post-maneuver state for epochs at or after the event.
/// </summary>
public sealed class PropagationSolution
{
    private readonly List<PropagationSegment> _segments = new();
    private StateVector[] _stateVectors = Array.Empty<StateVector>();

    /// <summary>
    /// Ordered propagation segments.
    /// </summary>
    public IReadOnlyList<PropagationSegment> Segments => _segments;

    /// <summary>
    /// Pre-computed state vectors sampled at DeltaT intervals.
    /// </summary>
    public IReadOnlyList<StateVector> StateVectors => _stateVectors;

    /// <summary>
    /// The dynamics of the propagation, kept in RK7(8) for the evaluations of the variational equations after it; null
    /// for another integrator.
    /// </summary>
    internal PropagationDynamics Dynamics { get; set; }

    /// <summary>
    /// Set the sampled output state vectors.
    /// </summary>
    public void SetOutputStates(StateVector[] states)
    {
        _stateVectors = states ?? throw new ArgumentNullException(nameof(states));
    }

    /// <summary>
    /// Add a completed segment to the solution.
    /// </summary>
    public void AddSegment(PropagationSegment segment)
    {
        if (segment == null) throw new ArgumentNullException(nameof(segment));
        _segments.Add(segment);
    }

    /// <summary>
    /// Position and velocity at the given epoch, in the frame of the propagation and relative to its observer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For an RK7(8) propagation, the state is computed by a shortened RK7(8) step from the start of the accepted step
    /// that contains the epoch, so it has the accuracy of the integrator; at a step boundary, the stored state is
    /// returned. Inside a step this costs 13 evaluations of the forces, which run under a lock of the solution: like the
    /// propagation itself, it must not run concurrently with another propagation or solution that shares a
    /// <see cref="IO.Astrodynamics.Body.CelestialBody"/> with a geopotential.
    /// </para>
    /// <para>
    /// For another integrator, the state is the cubic Hermite interpolation of the accepted step
    /// (<see cref="PropagationSegment.InterpolateAt"/>).
    /// </para>
    /// <para>
    /// At a maneuver, the state after the maneuver is returned for epochs at or after it. Before the solution, its
    /// first state; after it, its last state.
    /// </para>
    /// </remarks>
    public (Vector3 position, Vector3 velocity) InterpolateAt(Time epoch)
    {
        var segment = SegmentAt(epoch, out double t);
        return Dynamics != null ? Dynamics.StateAt(segment, t) : segment.InterpolateAt(t);
    }

    /// <summary>
    /// The cumulative variational values at <paramref name="epoch"/>, from the start of the propagation: Y = [Φ | Ψ] and
    /// Q (<see cref="VariationalEquations"/>). Picks the segment as <see cref="InterpolateAt"/> does, the later one at a
    /// maneuver.
    /// </summary>
    /// <param name="epoch">The epoch, within the solution.</param>
    /// <param name="y">Y, 6n values.</param>
    /// <param name="q">Q, 21 values, or empty without process noise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the solution.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not integrate the variational equations.</exception>
    internal void EvaluateVariational(Time epoch, Span<double> y, Span<double> q)
    {
        if (_segments.Count == 0)
            throw new InvalidOperationException("Solution contains no segments.");

        var last = _segments[^1];
        if (epoch < _segments[0].BaseEpoch || (epoch - last.BaseEpoch).TotalSeconds > last.Duration)
            throw new ArgumentOutOfRangeException(nameof(epoch), epoch,
                "The epoch is outside the propagation.");

        var segment = SegmentAt(epoch, out double t);
        if (Dynamics == null)
            throw new InvalidOperationException(
                "The solution holds no variational data: the propagation did not integrate the variational equations.");

        Dynamics.Evaluate(segment, t, y, q);
    }

    // The segment containing the epoch, the later one at boundaries (post-maneuver state), and the time from its start
    private PropagationSegment SegmentAt(in Time epoch, out double t)
    {
        if (_segments.Count == 0)
            throw new InvalidOperationException("Solution contains no segments.");

        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var segment = _segments[i];
            t = (epoch - segment.BaseEpoch).TotalSeconds;

            if (t >= 0.0)
            {
                return segment;
            }
        }

        // Before the first segment: the start of the first segment
        t = 0.0;
        return _segments[0];
    }
}
