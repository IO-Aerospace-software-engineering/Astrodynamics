// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude that points the boresight of an instrument at a target: the minimal rotation that takes the boresight, in
/// the spacecraft frame (<see cref="Instrument.GetBoresightInSpacecraftFrame"/>, the Euler angles of the frame kernel,
/// #524), to the direction of the target. The roll about the boresight is not constrained.
/// </summary>
/// <remarks>
/// The direction is that of the target as the spacecraft sees it, corrected by <see cref="Aberration"/>, <see cref="Aberration.LT"/>
/// by default: the target at t − lt from the spacecraft's position at t, lt the light time of CSPICE's <c>spkltc</c>, and
/// for "+S" the stellar aberration of <c>stelab</c> from the spacecraft's velocity, as <c>spkcvo_c</c> corrects it
/// (#549, decided by S. Guillet, 2026-11-17). Before 11.0.0 the law asked for LT, but a celestial target, the centre of the
/// maneuver, was read relative to itself, so its direction was geometric (the Moon 4.2e-5 to 4.5e-5 rad, about 40 km,
/// from its LT direction from a LEO), and a <see cref="Site"/> target took the light time from its body's centre.
/// </remarks>
public class InstrumentPointingToAttitude : Attitude
{
    public Instrument Instrument { get; }
    public ILocalizable Target { get; }

    /// <summary>
    /// The correction of the target's direction, <see cref="Aberration.LT"/> by default; <see cref="Aberration.None"/>
    /// points at its geometric direction.
    /// </summary>
    public Aberration Aberration { get; }

    /// <summary>
    /// An attitude that points the boresight of <paramref name="instrument"/> at <paramref name="target"/>.
    /// </summary>
    /// <param name="minimumEpoch">The earliest epoch of the attitude.</param>
    /// <param name="maneuverHoldDuration">The hold of the attitude.</param>
    /// <param name="instrument">The instrument whose boresight points at the target.</param>
    /// <param name="target">The target: a celestial item, a site or a spacecraft.</param>
    /// <param name="engine">The engine of the maneuver chain.</param>
    /// <param name="aberration">The correction of the target's direction, light time (<see cref="Aberration.LT"/>) by
    /// default (#549).</param>
    public InstrumentPointingToAttitude(Time minimumEpoch, TimeSpan maneuverHoldDuration, Instrument instrument, ILocalizable target, Engine engine,
        Aberration aberration = Aberration.LT) : base(
        target is CelestialItem t ? t : (target as Site)?.CelestialBody, minimumEpoch, maneuverHoldDuration, engine)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (!Enum.IsDefined(aberration))
        {
            throw new ArgumentOutOfRangeException(nameof(aberration), aberration, "Unknown aberration correction.");
        }

        Aberration = aberration;
    }

    /// <summary>
    /// The rotation from the spacecraft frame to the frame of <paramref name="stateVector"/> that takes the boresight to
    /// the target, <c>direction = boresight.Rotate(q)</c>, as the other attitudes take their body vector to theirs. The
    /// direction is the target's as the point of <paramref name="stateVector"/> sees it, corrected by
    /// <see cref="Aberration"/> (#549).
    /// </summary>
    public override Quaternion ComputeOrientation(StateVector stateVector)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        var targetVector = CelestialItem.ApparentPositionFromState(Target, stateVector, Aberration);
        return Instrument.GetBoresightInSpacecraftFrame().ToOrIdentity(targetVector);
    }
}