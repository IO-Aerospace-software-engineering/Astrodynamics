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
/// <remarks>
/// TIRS is the last frame of the IAU chain: polar motion, which leads to the ITRS, is not applied.
/// Only UT1 - UTC is taken from the Earth orientation parameters.
/// </remarks>
public sealed class TirsFrame : Frame
{
    /// <summary>
    /// Earth rotation rate, the rate of the Earth rotation angle (rad/s): 2π × 1.00273781191135448 / 86400.
    /// </summary>
    /// <remarks>
    /// IAU SOFA, <c>iauPvtob</c> (release 2023-10-11), constant <c>OM</c>, which gives the velocity of an Earth-fixed
    /// point in the CIRS as Ω ẑ × r. As there, the rate is per UT1 second, and the angular velocity of the CIP
    /// itself (precession-nutation, a few 1e-12 rad/s) is not added.
    /// </remarks>
    internal const double EarthRotationRate = 2.0 * System.Math.PI * 1.00273781191135448 / 86400.0;

    private readonly IEarthOrientationParameters _eop;

    /// <summary>
    /// Builds a TIRS frame without Earth orientation parameters (<see cref="NullEop"/>, UT1 = UTC).
    /// The frame is then off by the actual UT1 - UTC, within 0.9 s: up to about 13.5 arcseconds of Earth
    /// rotation angle, about 420 m for a point fixed on the equator.
    /// </summary>
    public TirsFrame() : this(NullEop.Instance)
    {
    }

    /// <summary>
    /// Builds a TIRS frame that takes UT1 - UTC from the given provider.
    /// </summary>
    /// <param name="eop">Earth orientation parameters; <c>null</c> falls back to <see cref="NullEop"/>,
    /// with the accuracy described on <see cref="TirsFrame()"/>.</param>
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
        // Two-part Julian date (J2000 + days elapsed), so that the ERA keeps the 0.1 µs resolution of the epoch.
        double jdUt1High = TimeSystem.Time.JULIAN_J2000;
        double jdUt1Low = utc.DaysFromJ2000() + deltaUT1 / 86400.0;
        double era = Iau2006Model.EarthRotationAngle(jdUt1High, jdUt1Low);

        // Compose the inverse of the standard ICRF -> TIRS chain to obtain TIRS -> ICRF.
        var eraMatrix = Matrix.CreateRotationMatrixZ(era);
        var tirs2icrf = qt.Transpose().Multiply(eraMatrix);
        var rotation = tirs2icrf.ToQuaternion();

        // Angular velocity of ICRF relative to TIRS, in TIRS axes: the convention of the SPICE frames, see
        // GetStateOrientationToICRF. TIRS turns at the ERA rate about the CIP, its z-axis.
        var angularVelocity = new Vector3(0.0, 0.0, -EarthRotationRate);

        return new StateOrientation(rotation, angularVelocity, date, this);
    }
}
