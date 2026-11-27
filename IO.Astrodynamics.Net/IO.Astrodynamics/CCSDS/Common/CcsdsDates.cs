// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// The date a CCSDS writer writes for a <see cref="DateTime"/>, by its <see cref="DateTime.Kind"/> (#617, decided by
/// S. Guillet, 2026-11-17).
/// </summary>
/// <remarks>
/// <para>
/// A date the standard states in UTC (every date of a CDM, CCSDS 508.0-B-1 Cor. 2, 6.3.2.6; the <c>CREATION_DATE</c> of an
/// OPM or OMM, CCSDS 502.0-B-3, Table 3-1): <see cref="DateTimeKind.Utc"/> as given, <see cref="DateTimeKind.Local"/>
/// converted to UTC, <see cref="DateTimeKind.Unspecified"/> taken as UTC, the clock reading given.
/// </para>
/// <para>
/// A date in <c>TIME_SYSTEM</c> (the <c>EPOCH</c>, <c>REF_FRAME_EPOCH</c> and <c>MAN_EPOCH_IGNITION</c> of an OPM or OMM):
/// <see cref="DateTimeKind.Utc"/> and <see cref="DateTimeKind.Unspecified"/> the clock reading in <c>TIME_SYSTEM</c> as
/// given, <see cref="DateTimeKind.Local"/> converted to UTC under <c>TIME_SYSTEM = UTC</c> and refused under any other
/// time system, in which a local time has no reading the writer could give without a conversion of time scales.
/// </para>
/// <para>
/// Before #617 the CDM writer read an <see cref="DateTimeKind.Unspecified"/> date as local time (a TCA built with
/// <c>new DateTime(...)</c> was written shifted by the offset of the machine's zone), and the OPM and OMM writers wrote a
/// local time as its clock reading whatever <c>TIME_SYSTEM</c>.
/// </para>
/// </remarks>
internal static class CcsdsDates
{
    /// <summary>
    /// The UTC date of <paramref name="value"/>, a date the standard states in UTC.
    /// </summary>
    internal static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// The UTC <see cref="Time"/> of <paramref name="value"/>, a date the standard states in UTC (<see cref="ToUtc"/>): the
    /// <see cref="Time"/> accessors of the CDM records (R19 of #668).
    /// </summary>
    internal static Time ToUtcTime(DateTime value) => new(ToUtc(value), TimeScale.UTC);

    /// <summary>The UTC <see cref="Time"/> of <paramref name="value"/>, null for none.</summary>
    internal static Time? ToUtcTime(DateTime? value) => value is { } date ? ToUtcTime(date) : null;

    /// <summary>
    /// The clock reading in <c>TIME_SYSTEM</c> of <paramref name="value"/>, the date of <paramref name="keyword"/>.
    /// </summary>
    /// <param name="value">The date.</param>
    /// <param name="timeSystem">The parsed <c>TIME_SYSTEM</c>, null for one the library does not know.</param>
    /// <param name="declaredTimeSystem">The <c>TIME_SYSTEM</c> as declared, for the message.</param>
    /// <param name="keyword">The keyword of the date, for the message.</param>
    /// <param name="paramName">The parameter of the writer that holds the message.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is of kind <see cref="DateTimeKind.Local"/> and <c>TIME_SYSTEM</c> is not UTC.
    /// </exception>
    internal static DateTime InTimeSystem(DateTime value, CcsdsTimeSystem? timeSystem, string declaredTimeSystem, string keyword,
        string paramName)
    {
        if (value.Kind != DateTimeKind.Local)
        {
            return value;
        }

        if (timeSystem == CcsdsTimeSystem.UTC)
        {
            return value.ToUniversalTime();
        }

        throw new ArgumentException(
            $"{keyword} is a DateTime of kind Local, a local time, which TIME_SYSTEM = {declaredTimeSystem} cannot state: give " +
            $"the clock reading in {declaredTimeSystem} with DateTimeKind.Utc or Unspecified, or write the message under " +
            "TIME_SYSTEM = UTC, where a local time is converted.", paramName);
    }
}
