// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude target that computes direction from the spacecraft's orbital state.
/// </summary>
public sealed class OrbitalDirectionTarget : IAttitudeTarget
{
    public static readonly OrbitalDirectionTarget Prograde = new(OrbitalDirection.Prograde);
    public static readonly OrbitalDirectionTarget Retrograde = new(OrbitalDirection.Retrograde);
    public static readonly OrbitalDirectionTarget Nadir = new(OrbitalDirection.Nadir);
    public static readonly OrbitalDirectionTarget Zenith = new(OrbitalDirection.Zenith);
    public static readonly OrbitalDirectionTarget Normal = new(OrbitalDirection.Normal);
    public static readonly OrbitalDirectionTarget AntiNormal = new(OrbitalDirection.AntiNormal);

    public OrbitalDirection Direction { get; }

    public string Name => Direction.ToString();

    public OrbitalDirectionTarget(OrbitalDirection direction)
    {
        Direction = direction;
    }

    public Vector3 GetDirection(StateVector observerState)
    {
        if (observerState == null) throw new ArgumentNullException(nameof(observerState));

        return Direction switch
        {
            OrbitalDirection.Prograde => observerState.Velocity.Normalize(),
            OrbitalDirection.Retrograde => observerState.Velocity.Inverse().Normalize(),
            OrbitalDirection.Nadir => observerState.Position.Inverse().Normalize(),
            OrbitalDirection.Zenith => observerState.Position.Normalize(),
            OrbitalDirection.Normal => NormalDirection(observerState),
            OrbitalDirection.AntiNormal => NormalDirection(observerState).Inverse(),
            _ => throw new ArgumentOutOfRangeException(nameof(Direction), Direction, "Unknown orbital direction.")
        };
    }

    private static Vector3 NormalDirection(StateVector observerState)
    {
        var normal = observerState.Position.Cross(observerState.Velocity);
        if (normal.MagnitudeSquared() < double.Epsilon)
        {
            throw new InvalidOperationException(
                "Cannot compute orbital normal: position and velocity are collinear. " +
                "The orbit is degenerate (rectilinear trajectory).");
        }

        return normal.Normalize();
    }
}
