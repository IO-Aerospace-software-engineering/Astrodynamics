// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Terrestrial Intermediate Reference System (TIRS).
/// Combines CIRS with Earth rotation angle (ERA).
/// The CIO-based model first forms <c>ICRF → TIRS</c>; this class returns the inverse,
/// <c>TIRS → ICRF</c>, from <see cref="GetStateOrientationToICRF(Time)"/>.
/// </summary>
public sealed class TirsFrame : Frame
{
    private readonly IEarthOrientationParameters _eop;

    public TirsFrame() : this(NullEop.Instance)
    {
    }

    public TirsFrame(IEarthOrientationParameters eop) : base("TIRS")
    {
        _eop = eop ?? NullEop.Instance;
    }

    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        if (OrientationCache != null)
            return OrientationCache.GetOrientation(date);

        return _stateOrientationsToICRF.GetOrAdd(date, ComputeOrientation);
    }

    private StateOrientation ComputeOrientation(Time date)
    {
        var tdb = date.ConvertTo(TimeFrame.TDBFrame);
        double t = tdb.Centuries();

        // Build Q(t), the CIO-based precession-nutation matrix (ICRF -> CIRS).
        var (x, y) = Iau2006Model.CipXY(t);
        double s = Iau2006Model.CioLocator(t, x, y);
        var qt = Iau2006Model.PrecessionNutationMatrix(x, y, s);

        // Earth Rotation Angle
        var utc = date.ConvertTo(TimeFrame.UTCFrame);
        double deltaUT1 = _eop.GetDeltaUT1(utc);
        double jdUtc = utc.ToJulianDate();
        double jdUt1High = jdUtc;
        double jdUt1Low = deltaUT1 / 86400.0;
        double era = Iau2006Model.EarthRotationAngle(jdUt1High, jdUt1Low);

        // Compose the inverse of the standard ICRF -> TIRS chain to obtain TIRS -> ICRF.
        var eraMatrix = Matrix.CreateRotationMatrixZ(era);
        var tirs2icrf = qt.Transpose().Multiply(eraMatrix);
        var rotation = tirs2icrf.ToQuaternion();

        // Angular velocity matches the returned TIRS -> ICRF rotation convention.
        var angularVelocity = ComputeAngularVelocity(t);

        return new StateOrientation(rotation, angularVelocity, date, this);
    }

    private static Vector3 ComputeAngularVelocity(double t)
    {
        // Expressed in ICRF, Earth's instantaneous spin is along the CIP direction.
        // The precession-nutation angular velocity (~1e-11 rad/s) is negligible here.
        // ERA rate: dERA/dt = 2π * 1.00273781191135448 / 86400 rad/s
        const double OmegaEarth = 2.0 * System.Math.PI * 1.00273781191135448 / 86400.0;

        var (x, y) = Iau2006Model.CipXY(t);
        double s = Iau2006Model.CioLocator(t, x, y);
        var qt = Iau2006Model.PrecessionNutationMatrix(x, y, s);

        return new Vector3(qt.Get(2, 0) * OmegaEarth,
                           qt.Get(2, 1) * OmegaEarth,
                           qt.Get(2, 2) * OmegaEarth);
    }
}
