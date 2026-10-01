// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Geocentric Celestial Reference Frame (GCRF).
/// The GCRF is the realization of the International Celestial Reference System (ICRS).
/// It differs from SPICE's J2000 frame by the IAU 2006 frame bias (~23 mas rotation).
/// The rotation is constant (no time dependence) and the angular velocity is zero.
/// <see cref="GetStateOrientationToICRF(Time)"/> returns the rotation <c>GCRF → ICRF</c>.
/// </summary>
public sealed class GcrfFrame : Frame
{
    private static readonly Quaternion _biasQuaternion = Iau2006Model.FrameBias().ToQuaternion();

    public GcrfFrame() : base("GCRF")
    {
    }

    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        if (OrientationCache != null)
            return OrientationCache.GetOrientation(date);

        // The stored quaternion rotates vectors from GCRF into ICRF.
        return _stateOrientationsToICRF.GetOrAdd(date, _ =>
            new StateOrientation(_biasQuaternion, Vector3.Zero, date, this));
    }
}
