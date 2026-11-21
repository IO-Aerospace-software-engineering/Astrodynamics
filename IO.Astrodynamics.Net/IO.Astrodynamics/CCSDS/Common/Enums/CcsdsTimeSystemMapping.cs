// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// The time scale of a CCSDS <c>TIME_SYSTEM</c> (#485): the scale in which the epochs of an OPM or an OMM are read.
/// </summary>
internal static class CcsdsTimeSystemMapping
{
    /// <summary>
    /// The time scale of <paramref name="timeSystem"/>: UTC, TAI, TDB, TT and GPS.
    /// </summary>
    /// <param name="timeSystem">The parsed time system, null when the declared value is none of the five.</param>
    /// <param name="declared">The value of <c>TIME_SYSTEM</c> as the message gives it, for the message of the exception.</param>
    /// <exception cref="InvalidOperationException">The time system is none of the five the framework converts.</exception>
    internal static TimeScale ToTimeScale(CcsdsTimeSystem? timeSystem, string declared) => timeSystem switch
    {
        CcsdsTimeSystem.UTC => TimeScale.UTC,
        CcsdsTimeSystem.TAI => TimeScale.TAI,
        CcsdsTimeSystem.TDB => TimeScale.TDB,
        CcsdsTimeSystem.TT => TimeScale.TT,
        CcsdsTimeSystem.GPS => TimeScale.GPS,
        _ => throw new InvalidOperationException(
            $"TIME_SYSTEM '{declared}' cannot be converted to an epoch: the supported time systems are UTC, TAI, TDB, TT and GPS.")
    };

    /// <summary>
    /// The time of the clock reading <paramref name="epoch"/> of a message in <paramref name="scale"/>: the calendar of the
    /// reader (of kind <see cref="DateTimeKind.Utc"/>) or of the caller, whatever its kind, read in the scale, as before
    /// 11.0.0, where the frame replaced the kind (D3 of #668: a <see cref="Time"/> refuses a calendar of kind
    /// <see cref="DateTimeKind.Utc"/> in another scale than UTC).
    /// </summary>
    internal static Time ToTime(DateTime epoch, TimeScale scale) =>
        new(DateTime.SpecifyKind(epoch, DateTimeKind.Unspecified), scale);
}
