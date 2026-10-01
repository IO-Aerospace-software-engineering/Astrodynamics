// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Stores relative position and velocity at an encounter epoch in both inertial and RTN representations.
/// </summary>
public sealed class RelativeState
{
    /// <summary>
    /// Initializes a relative-state snapshot.
    /// </summary>
    public RelativeState(
        Time epoch,
        Vector3 relativePositionInertial,
        Vector3 relativeVelocityInertial,
        Vector3 relativePositionRtn,
        Vector3 relativeVelocityRtn)
    {
        Epoch = epoch;
        RelativePositionInertial = relativePositionInertial;
        RelativeVelocityInertial = relativeVelocityInertial;
        RelativePositionRtn = relativePositionRtn;
        RelativeVelocityRtn = relativeVelocityRtn;
    }

    /// <summary>
    /// Gets the epoch at which the relative state was evaluated.
    /// </summary>
    public Time Epoch { get; }

    /// <summary>
    /// Gets the inertial relative position vector from protected object to secondary object, in meters.
    /// </summary>
    public Vector3 RelativePositionInertial { get; }

    /// <summary>
    /// Gets the inertial relative velocity vector from protected object to secondary object, in meters per second.
    /// </summary>
    public Vector3 RelativeVelocityInertial { get; }

    /// <summary>
    /// Gets the relative position expressed in the protected object's RTN frame, in meters.
    /// </summary>
    public Vector3 RelativePositionRtn { get; }

    /// <summary>
    /// Gets the relative velocity expressed in the protected object's RTN frame, in meters per second.
    /// </summary>
    public Vector3 RelativeVelocityRtn { get; }
}
