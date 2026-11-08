// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// True Equator, Mean Equinox (TEME), the frame of the SGP4/SDP4 states of a two-line element set.
/// </summary>
/// <remarks>
/// <para>
/// The definition is that of Vallado, Crawford, Hujsak and Kelso (2006), "Revisiting Spacetrack Report #3",
/// AIAA 2006-6753 Rev2, appendix C (to be verified by S. Guillet): the pseudo Earth-fixed frame of a TEME state is
/// r_PEF = R3(GMST82(UT1)) r_TEME, the PEF taken as the TIRS. So <c>TEME → ICRF = Q(t)ᵀ · Rz(ERA − GMST82)</c>, with
/// <c>Q(t)</c> the CIO-based precession-nutation matrix of <see cref="CirsFrame"/> (IAU 2006 precession, IAU 2000B
/// nutation, <see cref="Iau2006Model"/>), the Earth rotation angle of <see cref="TirsFrame"/>, GMST82 the IAU 1982
/// Greenwich mean sidereal time (<see cref="Gmst82"/>) and <c>Rz</c> the active rotation about z
/// (<see cref="Matrix.CreateRotationMatrixZ"/>).
/// </para>
/// <para>
/// No file is read: UT1 enters GMST82 and the ERA alike, so UT1 − UTC cancels between them to 0.03 mm in LEO and
/// 0.2 mm at the geostationary radius; UT1 = UTC (<see cref="NullEop"/>) unless another provider is given. TEME ↔ ICRF
/// works at any epoch, before the Earth's binary PCK and after it.
/// </para>
/// <para>
/// TEME is quasi-inertial: the angular velocity is zero, so a state transforms as r' = R r and v' = R v, as Vallado's
/// <c>teme2eci</c> and GMAT. The rotation rate of the frame, the precession of the mean equinox (about 7e-12 rad/s), would
/// add at most 0.08 mm/s to a LEO velocity and 0.39 mm/s at the geostationary radius. Toward a rotating frame (ITRF93,
/// TIRS) the angular velocity of that frame applies.
/// </para>
/// <para>
/// Measured on 2026-11-08 (<c>tools/teme_reference</c>, 1980 to 2026): within 1 mm (LEO) and 19 mm (GEO) of ERFA's CIP
/// route with IAU 2006/2000B, 0.55 mas of it with IAU 2006/2000A; GMAT R2026a's TEME → EarthICRF is up to 9 cm (LEO) and
/// 56 cm (GEO) from both, the kinematic terms of the 1994 equation of the equinoxes it applies from 1997-02-27.
/// </para>
/// </remarks>
public sealed class TemeFrame : Frame
{
    private readonly IEarthOrientationParameters _eop;

    /// <summary>
    /// Builds the TEME frame with UT1 = UTC (<see cref="NullEop"/>), which cancels to 0.2 mm at the geostationary radius.
    /// </summary>
    public TemeFrame() : this(NullEop.Instance)
    {
    }

    /// <summary>
    /// Builds the TEME frame that takes UT1 − UTC from the given provider.
    /// </summary>
    /// <param name="eop">Earth orientation parameters; <c>null</c> falls back to <see cref="NullEop"/>.</param>
    public TemeFrame(IEarthOrientationParameters eop) : base("TEME")
    {
        _eop = eop ?? NullEop.Instance;
    }

    /// <remarks>
    /// Computed once per epoch and kept; TEME is never the body-fixed frame of a propagation, so it reads no orientation
    /// cache of a propagation (<see cref="Frame.UseOrientationCache"/>).
    /// </remarks>
    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        return _stateOrientationsToICRF.GetOrAdd(date, ComputeOrientation);
    }

    private StateOrientation ComputeOrientation(Time date)
    {
        // Q(t), ICRF -> CIRS, at TDB centuries, as CirsFrame and TirsFrame
        double t = date.ConvertTo(TimeFrame.TDBFrame).Centuries();
        var (x, y) = Iau2006Model.CipXY(t);
        double s = Iau2006Model.CioLocator(t, x, y);
        var qt = Iau2006Model.PrecessionNutationMatrix(x, y, s);

        // UT1 as a two-part Julian date (J2000 + days), so that both angles keep the 0.1 µs resolution of the epoch
        var utc = date.ConvertTo(TimeFrame.UTCFrame);
        double jdUt1Low = utc.DaysFromJ2000() + _eop.GetDeltaUT1(utc) / 86400.0;
        double era = Iau2006Model.EarthRotationAngle(Time.JULIAN_J2000, jdUt1Low);
        double gmst = Gmst82(Time.JULIAN_J2000, jdUt1Low);

        // TEME -> TIRS = Rz(-GMST82) (r_PEF = R3(GMST82) r_TEME), TIRS -> ICRF = Q^T Rz(ERA)
        var teme2icrf = qt.Transpose().Multiply(Matrix.CreateRotationMatrixZ(era - gmst));
        return new StateOrientation(teme2icrf.ToQuaternion(), Vector3.Zero, date, this);
    }

    /// <summary>
    /// Greenwich mean sidereal time, IAU 1982 model (radians, in [0, 2π)), from UT1 as a two-part Julian date: the
    /// expression of IAU SOFA <c>iauGmst82</c> (release 2023-10-11), Aoki et al. (1982) with the IAU 1982 coefficients.
    /// </summary>
    /// <param name="jdUt1High">First part of the UT1 Julian date.</param>
    /// <param name="jdUt1Low">Second part of the UT1 Julian date.</param>
    internal static double Gmst82(double jdUt1High, double jdUt1Low)
    {
        // Coefficients of the IAU 1982 GMST-UT1 model, A adjusted by 12 h for the noon origin of the Julian date
        const double a = 24110.54841 - 86400.0 / 2.0;
        const double b = 8640184.812866;
        const double c = 0.093104;
        const double d = -6.2e-6;

        // The smaller part first, as SOFA, for the precision of the fraction of the day
        double d1 = jdUt1High < jdUt1Low ? jdUt1High : jdUt1Low;
        double d2 = jdUt1High < jdUt1Low ? jdUt1Low : jdUt1High;
        double t = (d1 + (d2 - Time.JULIAN_J2000)) / 36525.0;
        double f = 86400.0 * (d1 % 1.0 + d2 % 1.0);

        double gmst = Constants._2PI / 86400.0 * (a + (b + (c + d * t) * t) * t + f);
        gmst %= Constants._2PI;
        if (gmst < 0.0) gmst += Constants._2PI;
        return gmst;
    }
}
