// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// The parser of a CCSDS date, the date branch of the NDM/XML <c>epochType</c>
/// (<c>ndmxml-4.0.0-common-4.0.xsd</c>), shared by the CDM, OPM and OMM readers (#490).
/// </summary>
/// <remarks>
/// <para>
/// A year, then <c>-MM-DD</c> (calendar) or <c>-DDD</c> (day of year, 1 to 365, 366 in a leap year), then
/// <c>Thh:mm:ss</c>, an optional fraction of any length and an optional <c>Z</c> or <c>±hh:mm</c>. The result is UTC
/// (<see cref="DateTimeKind.Utc"/>), the offset applied. Up to 7 fraction digits are exact, in 100 ns ticks; a longer
/// fraction is rounded to the tick as <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/>
/// rounds it, so a calendar date gives the <see cref="DateTime"/> the readers gave before.
/// </para>
/// <para>
/// A text outside this form goes to <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/>,
/// the readers' former fallback, unchanged (its leniency is #619). A day of year out of range is rejected, as CSPICE's
/// <c>str2et</c> rejects it.
/// </para>
/// </remarks>
internal static class CcsdsEpochParser
{
    private const DateTimeStyles Styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private static readonly Regex EpochPattern = new(
        @"^(?<year>\d{4})-(?:(?<month>\d{2})-(?<day>\d{2})|(?<doy>\d{3}))T(?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2})(?:\.(?<fraction>\d+))?(?<zone>Z|[+-]\d{2}:\d{2})?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // The base of the rounding of a fraction longer than 7 digits
    private static readonly DateTime FractionBase = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Parses a CCSDS date.
    /// </summary>
    /// <param name="value">The text of the date.</param>
    /// <param name="utc">The UTC instant, when the text is a date.</param>
    /// <returns>True when the text is a date.</returns>
    internal static bool TryParse(string value, out DateTime utc)
    {
        utc = default;
        if (value == null)
        {
            return false;
        }

        var match = EpochPattern.Match(value);
        if (!match.Success)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, Styles, out utc);
        }

        int year = Int(match, "year");
        if (match.Groups["doy"].Success)
        {
            int dayOfYear = Int(match, "doy");
            if (dayOfYear < 1 || dayOfYear > (DateTime.IsLeapYear(year) ? 366 : 365))
            {
                return false;
            }

            var date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOfYear - 1);
            return TryCompose(match, date.Month, date.Day, out utc);
        }

        // A calendar text the composition rejects (a day or an hour out of range) keeps the former fallback
        return TryCompose(match, Int(match, "month"), Int(match, "day"), out utc) ||
               DateTime.TryParse(value, CultureInfo.InvariantCulture, Styles, out utc);
    }

    private static bool TryCompose(Match match, int month, int day, out DateTime utc)
    {
        utc = default;
        int year = Int(match, "year");
        int hour = Int(match, "hour");
        int minute = Int(match, "minute");
        int second = Int(match, "second");
        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 ||
            second > 59)
        {
            return false;
        }

        long ticks = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).Ticks;
        if (match.Groups["fraction"].Success)
        {
            ticks += FractionTicks(match.Groups["fraction"].Value);
        }

        var zone = match.Groups["zone"];
        if (zone.Success && zone.Value != "Z")
        {
            int offsetHours = int.Parse(zone.Value.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            int offsetMinutes = int.Parse(zone.Value.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            long offset = new TimeSpan(offsetHours, offsetMinutes, 0).Ticks;
            if (offsetMinutes > 59 || offset > TimeSpan.FromHours(14).Ticks)
            {
                return false;
            }

            // Local time minus its offset from UTC
            ticks -= zone.Value[0] == '+' ? offset : -offset;
        }

        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        utc = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    // Up to 7 digits, the exact ticks; more, the ticks DateTime.TryParse rounds them to (the former fallback), a carry into
    // the next second included
    private static long FractionTicks(string digits)
    {
        if (digits.Length <= 7)
        {
            return long.Parse(digits.PadRight(7, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        return DateTime.Parse("2000-01-01T00:00:00." + digits, CultureInfo.InvariantCulture, Styles).Ticks - FractionBase.Ticks;
    }

    private static int Int(Match match, string group)
    {
        return int.Parse(match.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
