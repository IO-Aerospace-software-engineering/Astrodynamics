using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude target that computes direction from the spacecraft's orbital state.
/// </summary>
public class OrbitalDirectionTarget : IAttitudeTarget
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
            OrbitalDirection.Normal => observerState.Position.Cross(observerState.Velocity).Normalize(),
            OrbitalDirection.AntiNormal => observerState.Position.Cross(observerState.Velocity).Inverse().Normalize(),
            _ => throw new ArgumentOutOfRangeException(nameof(Direction), Direction, "Unknown orbital direction.")
        };
    }
}
