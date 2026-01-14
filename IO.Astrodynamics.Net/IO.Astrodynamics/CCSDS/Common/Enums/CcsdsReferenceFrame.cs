// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// CCSDS reference frames supported by the IO.Astrodynamics framework.
/// </summary>
/// <remarks>
/// The CCSDS OMM schema allows any string for REF_FRAME, but this enum
/// is restricted to frames that can be processed by the framework.
/// Maps to IO.Astrodynamics.Frames.Frame instances.
/// </remarks>
public enum CcsdsReferenceFrame
{
    /// <summary>
    /// International Celestial Reference Frame (IERS standard).
    /// Maps to Frame.ICRF (J2000).
    /// </summary>
    [Description("ICRF")]
    ICRF,

    /// <summary>
    /// Earth Mean Equator and Equinox of J2000.
    /// Equivalent to ICRF for most purposes. Maps to Frame.ICRF.
    /// </summary>
    [Description("EME2000")]
    EME2000,

    /// <summary>
    /// Geocentric Celestial Reference Frame (IAU standard).
    /// Equivalent to ICRF. Maps to Frame.ICRF.
    /// </summary>
    [Description("GCRF")]
    GCRF,

    /// <summary>
    /// True Equator Mean Equinox frame (used by SGP4/SDP4).
    /// Maps to Frame.TEME.
    /// </summary>
    [Description("TEME")]
    TEME,

    /// <summary>
    /// International Terrestrial Reference Frame 1993 (Earth body-fixed).
    /// </summary>
    [Description("ITRF93")]
    ITRF93,

    /// <summary>
    /// Ecliptic coordinate system at epoch J2000.
    /// Maps to Frame.ECLIPTIC_J2000.
    /// </summary>
    [Description("ECLIPJ2000")]
    ECLIPJ2000,

    /// <summary>
    /// Ecliptic coordinate system at epoch B1950.
    /// Maps to Frame.ECLIPTIC_B1950.
    /// </summary>
    [Description("ECLIPB1950")]
    ECLIPB1950,

    /// <summary>
    /// Equatorial coordinate system at epoch B1950.
    /// Maps to Frame.B1950.
    /// </summary>
    [Description("B1950")]
    B1950,

    /// <summary>
    /// Fourth Fundamental Catalog (FK4) coordinate system.
    /// Maps to Frame.FK4.
    /// </summary>
    [Description("FK4")]
    FK4
}
