// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Geocentric Celestial Reference Frame (GCRF): the axes of the International Celestial Reference
/// System (ICRS), centred on the Earth.
/// The library pivot frame <see cref="Frame.ICRF"/> (SPICE <c>J2000</c>) is treated as aligned with
/// the ICRS, like the DE planetary ephemerides it carries, so the rotation <c>GCRF → ICRF</c> is the
/// identity and the angular velocity is zero.
/// The IAU 2006 frame bias (~23 mas) relates the ICRS to the mean equator and equinox of J2000
/// (EME2000). It belongs to the precession-nutation matrix used by <see cref="CirsFrame"/>, and is
/// not applied between GCRF and ICRF.
/// </summary>
public sealed class GcrfFrame : Frame
{
    public GcrfFrame() : base("GCRF")
    {
    }

    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        if (OrientationCache != null)
            return OrientationCache.GetOrientation(date);

        return _stateOrientationsToICRF.GetOrAdd(date, _ =>
            new StateOrientation(Quaternion.Zero, Vector3.Zero, date, this));
    }
}
