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
/// <para>
/// The direction is that of the target, an <see cref="IAttitudeTarget"/> (<see cref="IAttitudeTarget.GetDirection"/>):
/// for a <see cref="CelestialAttitudeTarget"/>, the target as the spacecraft sees it, corrected by the aberration of the
/// target, <see cref="Aberration.None"/> by default, <see cref="Aberration.LT"/> the target at t − lt from the
/// spacecraft's position at t, lt the light time of CSPICE's <c>spkltc</c>, and for "+S" the stellar aberration of
/// <c>stelab</c> from the spacecraft's velocity, as <c>spkcvo_c</c> corrects it (#549, decided by S. Guillet,
/// 2026-11-17); a <see cref="Star"/> takes no light time.
/// </para>
/// <para>
/// Since lot L5b (decision 67 of #668, decided by S. Guillet, 2026-11-23) the target is an <see cref="IAttitudeTarget"/>,
/// which carries its correction: the attitude's own <c>Aberration</c>, <see cref="Aberration.LT"/> by default, is
/// removed, so a target given as <c>new CelestialAttitudeTarget(moon)</c> is geometric where the former default corrected
/// the light time (9.165e-5 rad for the Moon from a LEO in the gap analysis of L5); pass
/// <c>new CelestialAttitudeTarget(moon, Aberration.LT)</c> for the former direction. The centre follows the rule of every
/// maneuver (decision 62 of #668): the one given, else the one the run infers, where the target, or a site's body, was
/// the centre before; the direction depends on the centre through rounding only.
/// </para>
/// </remarks>
public class InstrumentPointingToAttitude : Attitude
{
    /// <summary>
    /// The instrument whose boresight points at the target, an instrument of the spacecraft propagated (#797).
    /// </summary>
    public Instrument Instrument { get; }

    /// <summary>
    /// The target of the boresight, with its correction (decision 67 of #668: an <c>ILocalizable</c> before lot L5b).
    /// </summary>
    public IAttitudeTarget Target { get; }

    /// <summary>
    /// An attitude that points the boresight of <paramref name="instrument"/> at <paramref name="target"/>.
    /// </summary>
    /// <param name="instrument">The instrument whose boresight points at the target.</param>
    /// <param name="target">The target, with its correction: a <see cref="CelestialAttitudeTarget"/> of a celestial item,
    /// a site or a spacecraft, or an orbital direction (<see cref="OrbitalDirectionTarget"/>).</param>
    /// <param name="minEpoch">The earliest epoch of the attitude; default(Time) for none.</param>
    /// <param name="holdDuration">The hold of the attitude, zero or positive; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (decisions 61 and 62 of #668);
    /// in SSB mode, where it cannot be inferred, a library <see cref="CelestialAttitudeTarget"/> takes the central body of
    /// the run, and any other target requires it (decision 99 of #668, decided by S. Guillet, 2026-11-24).</param>
    /// <exception cref="ArgumentNullException"><paramref name="instrument"/> or <paramref name="target"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
    /// <remarks>
    /// D1 rule 8 of #668 (lot L5b, decided by S. Guillet, 2026-11-23); before 11.0.0 <c>(minimumEpoch,
    /// maneuverHoldDuration, instrument, target, engine, aberration = Aberration.LT)</c>.
    /// </remarks>
    public InstrumentPointingToAttitude(Instrument instrument, IAttitudeTarget target, Time minEpoch = default, TimeSpan holdDuration = default,
        CelestialItem maneuverCenter = null) : base(minEpoch, holdDuration, maneuverCenter)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    internal override System.Collections.Generic.IEnumerable<Instrument> PointedInstruments => [Instrument];

    // Whether the law reads its centre (decision 99 of #668, decided by S. Guillet, 2026-11-24): not for the library's own
    // law pointing at a library CelestialAttitudeTarget, which takes the central body of the run when the centre cannot be
    // inferred; a user subclass, which may replace the law, reads it (as a user Attitude subclass)
    internal override bool ReadsCenter =>
        GetType() != typeof(InstrumentPointingToAttitude) || Target.GetType() != typeof(CelestialAttitudeTarget);

    /// <summary>
    /// The rotation from the spacecraft frame to the frame of <paramref name="stateVector"/> that takes the boresight to
    /// the direction of the target, <c>direction = boresight.Rotate(q)</c>, as the other attitudes take their body vector
    /// to theirs (<see cref="IAttitudeTarget.GetDirection"/>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    public override Quaternion ComputeOrientation(StateVector stateVector)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        return Instrument.GetBoresightInSpacecraftFrame().ToOrIdentity(Target.GetDirection(stateVector));
    }
}
