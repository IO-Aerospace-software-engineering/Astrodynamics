// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// CCSDS time systems supported by the IO.Astrodynamics framework.
/// </summary>
/// <remarks>
/// The CCSDS OMM schema allows any string for TIME_SYSTEM, but this enum
/// is restricted to time systems that can be processed by the framework.
/// Maps to the members of IO.Astrodynamics.TimeSystem.TimeScale.
/// </remarks>
public enum CcsdsTimeSystem
{
    /// <summary>
    /// Coordinated Universal Time.
    /// Maps to TimeScale.UTC. Standard for TLE/SGP4 data.
    /// </summary>
    [Description("UTC")]
    UTC,

    /// <summary>
    /// International Atomic Time.
    /// Maps to TimeScale.TAI.
    /// </summary>
    [Description("TAI")]
    TAI,

    /// <summary>
    /// Barycentric Dynamical Time.
    /// Maps to TimeScale.TDB.
    /// </summary>
    [Description("TDB")]
    TDB,

    /// <summary>
    /// Terrestrial Time (formerly TDT - Terrestrial Dynamical Time).
    /// Maps to TimeScale.TT.
    /// </summary>
    [Description("TT")]
    TT,

    /// <summary>
    /// Global Positioning System time.
    /// Maps to TimeScale.GPS.
    /// </summary>
    [Description("GPS")]
    GPS
}
