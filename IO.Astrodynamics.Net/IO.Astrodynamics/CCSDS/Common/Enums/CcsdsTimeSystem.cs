// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// CCSDS time systems used in Navigation Data Messages.
/// </summary>
/// <remarks>
/// As defined in CCSDS 505.0-B-3 (NDM/XML) and related standards.
/// </remarks>
public enum CcsdsTimeSystem
{
    /// <summary>
    /// Greenwich Mean Sidereal Time
    /// </summary>
    [Description("GMST")]
    GMST,

    /// <summary>
    /// Global Positioning System time
    /// </summary>
    [Description("GPS")]
    GPS,

    /// <summary>
    /// Mission Elapsed Time
    /// </summary>
    [Description("MET")]
    MET,

    /// <summary>
    /// Mission Relative Time
    /// </summary>
    [Description("MRT")]
    MRT,

    /// <summary>
    /// Spacecraft Clock (SCLK requires clock ID)
    /// </summary>
    [Description("SCLK")]
    SCLK,

    /// <summary>
    /// International Atomic Time
    /// </summary>
    [Description("TAI")]
    TAI,

    /// <summary>
    /// Barycentric Coordinate Time
    /// </summary>
    [Description("TCB")]
    TCB,

    /// <summary>
    /// Geocentric Coordinate Time
    /// </summary>
    [Description("TCG")]
    TCG,

    /// <summary>
    /// Barycentric Dynamical Time
    /// </summary>
    [Description("TDB")]
    TDB,

    /// <summary>
    /// Terrestrial Time (formerly TDT)
    /// </summary>
    [Description("TT")]
    TT,

    /// <summary>
    /// Universal Time 1
    /// </summary>
    [Description("UT1")]
    UT1,

    /// <summary>
    /// Coordinated Universal Time
    /// </summary>
    [Description("UTC")]
    UTC
}
