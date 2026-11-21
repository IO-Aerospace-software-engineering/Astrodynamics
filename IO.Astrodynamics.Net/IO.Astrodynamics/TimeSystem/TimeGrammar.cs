using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.TimeSystem;

/// <summary>
/// The one grammar of the time strings of the library (D3.4 of #668): <see cref="Time.Parse(string)"/>,
/// <see cref="Time.TryParse(string, out Time)"/> and their overloads, the CLI's epochs, and, through its strict branch
/// <see cref="TryParseCcsds"/>, the CCSDS dates of the CDM, OPM and OMM readers (the core of <c>CcsdsEpochParser</c>,
/// moved here, bit for bit).
/// </summary>
/// <remarks>
/// The digits are ASCII: .NET's \d matches every Unicode decimal digit, which int.Parse does not read. \z, not $, which
/// also matches before a final line feed (#619).
/// </remarks>
internal static class TimeGrammar
{
    private const string Tags = "UTC|TT|TDT|TDB|TAI|GPS";

    // The CCSDS epochType date (CCSDS 508.0-B-1 §6.3.2.6, 502.0-B-3): yyyy-mm-ddThh:mm:ss[.d→d][Z|±hh:mm] or
    // yyyy-dddThh:mm:ss[.d→d][Z|±hh:mm], all fields with their leading zeros
    private static readonly Regex CcsdsPattern = new(
        @"^(?<year>[0-9]{4})-(?:(?<month>[0-9]{2})-(?<day>[0-9]{2})|(?<doy>[0-9]{3}))T(?<hour>[0-9]{2}):(?<minute>[0-9]{2}):(?<second>[0-9]{2})(?:\.(?<fraction>[0-9]+))?(?<zone>Z|[+-][0-9]{2}:[0-9]{2})?\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // The calendar of Time.Parse: the CCSDS date with a T or a space, hh:mm or hh:mm:ss[.f…] optional, then a Z, an offset
    // or a tag, in any case, the space before the tag optional; blanks around the whole
    private static readonly Regex CalendarPattern = new(
        $@"^\s*(?<year>[0-9]{{4}})-(?:(?<month>[0-9]{{2}})-(?<day>[0-9]{{2}})|(?<doy>[0-9]{{3}}))(?:[Tt ](?<hour>[0-9]{{2}}):(?<minute>[0-9]{{2}})(?::(?<second>[0-9]{{2}})(?:\.(?<fraction>[0-9]+))?)?)?(?:(?<zone>[Zz]|[+-][0-9]{{2}}:[0-9]{{2}})|\s*(?<tag>{Tags}))?\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A Julian date: JD n or n JD, then a tag, in any case, blanks optional between them
    private static readonly Regex JulianDatePattern = new(
        $@"^\s*(?:JD\s*(?<jd>[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))|(?<jd>[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))\s*JD)(?:\s*(?<tag>{Tags}))?\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // The base of the rounding of a fraction longer than 7 digits
    private static readonly DateTime FractionBase = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const DateTimeStyles FractionStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private const string Grammar =
        "a time is yyyy-MM-dd or yyyy-DDD, optionally followed by T (or a space) and hh:mm[:ss[.f]], then Z, ±hh:mm or a scale tag (UTC, TT, TDT, TDB, TAI, GPS), or a Julian date 'JD n TAG' or 'n JD TAG'";

    /// <summary>
    /// Parses a CCSDS date, the strict branch of the grammar: the UTC instant (<see cref="DateTimeKind.Utc"/>), the offset
    /// applied; false for a text outside the two CCSDS forms or a field out of range (#619), a year 0000 or a day of year
    /// outside the year included.
    /// </summary>
    internal static bool TryParseCcsds(string value, out DateTime utc)
    {
        utc = default;
        if (value == null)
        {
            return false;
        }

        var match = CcsdsPattern.Match(value);
        if (!match.Success)
        {
            return false;
        }

        if (!TryCalendarTicks(match, out long ticks, out _))
        {
            return false;
        }

        utc = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    /// <summary>
    /// Parses a time string of the grammar of <see cref="Time.Parse(string)"/>; <paramref name="untaggedScale"/> the scale
    /// of a string without a tag, Z or offset, null to refuse it.
    /// </summary>
    internal static bool TryParse(ReadOnlySpan<char> text, TimeScale? untaggedScale, out Time result, out string error)
    {
        result = default;
        string value = text.ToString();
        var match = CalendarPattern.Match(value);
        if (match.Success)
        {
            return TryCalendar(match, untaggedScale, out result, out error);
        }

        match = JulianDatePattern.Match(value);
        if (match.Success)
        {
            return TryJulianDate(match, untaggedScale, out result, out error);
        }

        error = $"'{value}' is not a time: {Grammar}.";
        return false;
    }

    private static bool TryCalendar(Match match, TimeScale? untaggedScale, out Time result, out string error)
    {
        result = default;
        string value = match.Value;
        if (!TryCalendarTicks(match, out long ticks, out bool leapSecond))
        {
            error = leapSecond
                ? $"'{value}' holds a second 60: a Time holds no leap second (a UTC leap second can be given in TAI)."
                : $"'{value}' holds a field out of its range.";
            return false;
        }

        TimeScale scale;
        if (match.Groups["zone"].Success)
        {
            // Z or an offset: the UTC instant, never the machine's local time
            scale = TimeScale.UTC;
        }
        else if (match.Groups["tag"].Success)
        {
            scale = ScaleOfTag(match.Groups["tag"].Value);
        }
        else if (untaggedScale.HasValue)
        {
            scale = untaggedScale.Value;
        }
        else
        {
            error = $"'{value}' names no time scale: end it with Z, an offset or a tag (UTC, TT, TDB, TAI, GPS), or give the scale to Parse(s, scale).";
            return false;
        }

        result = new Time(new DateTime(ticks), scale);
        error = null;
        return true;
    }

    private static bool TryJulianDate(Match match, TimeScale? untaggedScale, out Time result, out string error)
    {
        result = default;
        string value = match.Value;
        TimeScale scale;
        if (match.Groups["tag"].Success)
        {
            scale = ScaleOfTag(match.Groups["tag"].Value);
        }
        else if (untaggedScale.HasValue)
        {
            scale = untaggedScale.Value;
        }
        else
        {
            error = $"'{value}' names no time scale: follow the Julian date with a tag (UTC, TT, TDB, TAI, GPS), or give the scale to Parse(s, scale).";
            return false;
        }

        // The digits read exactly (decimal holds 28 significant digits), the days from J2000 to the nearest tick, ties to
        // even
        if (!decimal.TryParse(match.Groups["jd"].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal julianDate))
        {
            error = $"'{value}' holds a Julian date out of range.";
            return false;
        }

        decimal ticks = decimal.Round((julianDate - (decimal)Time.JULIAN_J2000) * TimeSpan.TicksPerDay,
            MidpointRounding.ToEven);
        decimal j2000 = Time.J2000Calendar.Ticks;
        if (ticks < -j2000 || ticks > DateTime.MaxValue.Ticks - j2000)
        {
            error = $"'{value}' is outside the range of a Time.";
            return false;
        }

        result = new Time(new DateTime((long)(j2000 + ticks)), scale);
        error = null;
        return true;
    }

    private static TimeScale ScaleOfTag(string tag) => tag.ToUpperInvariant() switch
    {
        "TDB" => TimeScale.TDB,
        "TT" or "TDT" => TimeScale.TT,
        "TAI" => TimeScale.TAI,
        "GPS" => TimeScale.GPS,
        _ => TimeScale.UTC
    };

    // The ticks of the calendar of a match, the offset of its zone applied; false for a field out of range
    private static bool TryCalendarTicks(Match match, out long ticks, out bool leapSecond)
    {
        ticks = 0;
        leapSecond = false;
        int year = Int(match, "year");
        if (year < 1)
        {
            // Year 0000 matches the pattern but no DateTime holds it
            return false;
        }

        int month, day;
        if (match.Groups["doy"].Success)
        {
            int dayOfYear = Int(match, "doy");
            if (dayOfYear < 1 || dayOfYear > (DateTime.IsLeapYear(year) ? 366 : 365))
            {
                return false;
            }

            var date = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
            month = date.Month;
            day = date.Day;
        }
        else
        {
            month = Int(match, "month");
            day = Int(match, "day");
        }

        int hour = match.Groups["hour"].Success ? Int(match, "hour") : 0;
        int minute = match.Groups["minute"].Success ? Int(match, "minute") : 0;
        int second = match.Groups["second"].Success ? Int(match, "second") : 0;
        if (second == 60 && hour <= 23 && minute <= 59)
        {
            leapSecond = true;
            return false;
        }

        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 ||
            second > 59)
        {
            return false;
        }

        ticks = new DateTime(year, month, day, hour, minute, second).Ticks;
        if (match.Groups["fraction"].Success)
        {
            ticks += FractionTicks(match.Groups["fraction"].Value);
        }

        var zone = match.Groups["zone"];
        if (zone.Success && zone.Length > 1)
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

        return ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks;
    }

    // Up to 7 digits, the exact ticks; more, the ticks DateTime parsing rounds them to (CCSDS's former fallback and
    // Time(string)'s), a carry into the next second included
    private static long FractionTicks(string digits)
    {
        if (digits.Length <= 7)
        {
            return long.Parse(digits.PadRight(7, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        return DateTime.Parse("2000-01-01T00:00:00." + digits, CultureInfo.InvariantCulture, FractionStyles).Ticks -
               FractionBase.Ticks;
    }

    private static int Int(Match match, string group)
    {
        return int.Parse(match.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
