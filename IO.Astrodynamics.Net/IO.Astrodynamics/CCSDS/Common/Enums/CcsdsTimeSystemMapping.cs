// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// The time frame of a CCSDS <c>TIME_SYSTEM</c> (#485): the frame in which the epochs of an OPM or an OMM are read.
/// </summary>
internal static class CcsdsTimeSystemMapping
{
    /// <summary>
    /// The time frame of <paramref name="timeSystem"/>: UTC, TAI, TDB, TT (<see cref="TimeFrame.TDTFrame"/>) and GPS.
    /// </summary>
    /// <param name="timeSystem">The parsed time system, null when the declared value is none of the five.</param>
    /// <param name="declared">The value of <c>TIME_SYSTEM</c> as the message gives it, for the message of the exception.</param>
    /// <exception cref="InvalidOperationException">The time system is none of the five the framework converts.</exception>
    internal static ITimeFrame ToTimeFrame(CcsdsTimeSystem? timeSystem, string declared) => timeSystem switch
    {
        CcsdsTimeSystem.UTC => TimeFrame.UTCFrame,
        CcsdsTimeSystem.TAI => TimeFrame.TAIFrame,
        CcsdsTimeSystem.TDB => TimeFrame.TDBFrame,
        CcsdsTimeSystem.TT => TimeFrame.TDTFrame,
        CcsdsTimeSystem.GPS => TimeFrame.GPSFrame,
        _ => throw new InvalidOperationException(
            $"TIME_SYSTEM '{declared}' cannot be converted to an epoch: the supported time systems are UTC, TAI, TDB, TT and GPS.")
    };
}
