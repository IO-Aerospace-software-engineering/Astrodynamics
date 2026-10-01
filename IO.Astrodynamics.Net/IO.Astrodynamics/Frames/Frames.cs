// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
namespace IO.Astrodynamics.Frames;

/// <summary>
/// Unified frame access: every reference frame in one place.
/// Provides a consistent experience for all reference frames.
/// Each frame follows the same transform convention:
/// <c>frame.GetStateOrientationToICRF(epoch)</c> returns a rotation <c>frame → ICRF</c>.
/// </summary>
public static class Frames
{
    // ---- SPICE-backed frames (re-exported for unified access) ----

    /// <summary>International Celestial Reference Frame (ICRF) at epoch J2000.</summary>
    public static readonly Frame ICRF = Frame.ICRF;

    /// <summary>True Equator Mean Equinox (TEME) — used by SGP4/TLE.</summary>
    public static readonly Frame TEME = Frame.TEME;

    /// <summary>Ecliptic coordinate system at epoch J2000.</summary>
    public static readonly Frame ECLIPTIC_J2000 = Frame.ECLIPTIC_J2000;

    /// <summary>Ecliptic coordinate system at epoch B1950.</summary>
    public static readonly Frame ECLIPTIC_B1950 = Frame.ECLIPTIC_B1950;

    /// <summary>Galactic coordinate system.</summary>
    public static readonly Frame GALACTIC_SYSTEM2 = Frame.GALACTIC_SYSTEM2;

    /// <summary>Equatorial coordinate system at epoch B1950.</summary>
    public static readonly Frame B1950 = Frame.B1950;

    /// <summary>Fourth Fundamental Catalog (FK4) coordinate system.</summary>
    public static readonly Frame FK4 = Frame.FK4;

    // ---- IAU 2006/2000A CIO-based chain ----

    /// <summary>GCRF — Geocentric Celestial Reference Frame. <c>GetStateOrientationToICRF</c> returns <c>GCRF → ICRF</c>.</summary>
    public static readonly Frame GCRF = new GcrfFrame();

    /// <summary>CIRS — Celestial Intermediate Reference System. <c>GetStateOrientationToICRF</c> returns <c>CIRS → ICRF</c>.</summary>
    public static readonly Frame CIRS = new CirsFrame();

    /// <summary>TIRS — Terrestrial Intermediate Reference System. <c>GetStateOrientationToICRF</c> returns <c>TIRS → ICRF</c>.</summary>
    public static readonly Frame TIRS = new TirsFrame();
}
