// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// CCSDS reference frames used in Navigation Data Messages.
/// </summary>
/// <remarks>
/// As defined in CCSDS 500.0-G-4 and related standards.
/// Note: CCSDS allows any string for REF_FRAME, but these are the standard values.
/// </remarks>
public enum CcsdsReferenceFrame
{
    /// <summary>
    /// Earth Mean Equator and Equinox of J2000 (equivalent to ICRF for most purposes)
    /// </summary>
    [Description("EME2000")]
    EME2000,

    /// <summary>
    /// Geocentric Celestial Reference Frame (IAU standard, equivalent to ICRF)
    /// </summary>
    [Description("GCRF")]
    GCRF,

    /// <summary>
    /// Greenwich Rotating Coordinates (Earth body-fixed, rotating)
    /// </summary>
    [Description("GRC")]
    GRC,

    /// <summary>
    /// International Celestial Reference Frame (IERS standard)
    /// </summary>
    [Description("ICRF")]
    ICRF,

    /// <summary>
    /// International Terrestrial Reference Frame (Earth body-fixed)
    /// </summary>
    [Description("ITRF")]
    ITRF,

    /// <summary>
    /// International Terrestrial Reference Frame 1993 (specific realization)
    /// </summary>
    [Description("ITRF93")]
    ITRF93,

    /// <summary>
    /// International Terrestrial Reference Frame 1997 (specific realization)
    /// </summary>
    [Description("ITRF97")]
    ITRF97,

    /// <summary>
    /// International Terrestrial Reference Frame 2000 (specific realization)
    /// </summary>
    [Description("ITRF2000")]
    ITRF2000,

    /// <summary>
    /// Mean of Date frame (precessing equator and equinox)
    /// </summary>
    [Description("MCI")]
    MCI,

    /// <summary>
    /// True Equator Mean Equinox (used by SGP4/SDP4)
    /// </summary>
    [Description("TEME")]
    TEME,

    /// <summary>
    /// True of Date frame (true equator and equinox)
    /// </summary>
    [Description("TOD")]
    TOD,

    /// <summary>
    /// Radial-Transverse-Normal (satellite local orbital frame)
    /// </summary>
    [Description("RTN")]
    RTN,

    /// <summary>
    /// Tangent-Normal-Cross (TNW frame)
    /// </summary>
    [Description("TNW")]
    TNW,

    /// <summary>
    /// User-defined reference frame
    /// </summary>
    [Description("CUSTOM")]
    Custom
}
