// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
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
    /// Interpolate position and velocity at the given epoch.
    /// Finds the correct segment and delegates to its Hermite interpolation.
    /// </summary>
    public (Vector3 position, Vector3 velocity) InterpolateAt(Time epoch)
    {
        if (_segments.Count == 0)
            throw new InvalidOperationException("Solution contains no segments.");

        // Find the segment containing the given epoch
        // At boundaries, prefer the later segment (post-maneuver state)
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var segment = _segments[i];
            double t = (epoch - segment.BaseEpoch).TotalSeconds;

            if (t >= 0.0)
            {
                return segment.InterpolateAt(t);
            }
        }

        // Before the first segment: return the start of the first segment
        return _segments[0].InterpolateAt(0.0);
    }
}
