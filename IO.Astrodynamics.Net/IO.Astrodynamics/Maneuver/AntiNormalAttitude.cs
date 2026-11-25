// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// Attitude maneuver that orients the spacecraft body front axis along the anti-normal direction (-h = -(r x v)).
/// </summary>
public class AntiNormalAttitude : Attitude
{
    /// <summary>
    /// The attitude that points <paramref name="bodyAxis"/> of the spacecraft along the anti-normal (−h = −(r × v)).
    /// </summary>
    /// <param name="minEpoch">The earliest epoch of its activation; default(Time) for none.</param>
    /// <param name="holdDuration">Its hold, zero or positive; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers, the observer of the initial
    /// orbit (required in SSB mode, decisions 61, 64 and 99 of #668).</param>
    /// <param name="bodyAxis">The axis of the spacecraft it points, <see cref="BodyAxis.Front"/> by default, resolved on
    /// the spacecraft of the plan (<see cref="Maneuver.GetBodyAxis"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative, or
    /// <paramref name="bodyAxis"/> is not a value of <see cref="BodyAxis"/>.</exception>
    /// <remarks>
    /// D1 rule 8 and decision 69 of #668 (lot L5b, decided by S. Guillet, 2026-11-23): every parameter optional; before
    /// 11.0.0 <c>(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)</c>, each required, the front axis only.
    /// </remarks>
    public AntiNormalAttitude(Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null,
        BodyAxis bodyAxis = BodyAxis.Front) : base(minEpoch, holdDuration, maneuverCenter)
    {
        BodyAxis = CheckBodyAxis(bodyAxis, nameof(bodyAxis));
    }

    /// <summary>
    /// The axis of the spacecraft the attitude points (decision 69 of #668).
    /// </summary>
    public BodyAxis BodyAxis { get; }

    public override Quaternion ComputeOrientation(StateVector stateVector)
    {
        var normal = stateVector.Position.Cross(stateVector.Velocity);
        if (normal.MagnitudeSquared() < double.Epsilon)
        {
            throw new InvalidOperationException(
                $"Cannot compute orbital normal: position and velocity are collinear at epoch {stateVector.Epoch}. " +
                "The orbit is degenerate (rectilinear trajectory).");
        }

        return GetBodyAxis(BodyAxis).ToOrIdentity(normal.Inverse());
    }
}
