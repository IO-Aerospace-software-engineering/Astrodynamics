using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace IO.Astrodynamics.TimeSystem;

/// <summary>
/// An instant: a calendar (<see cref="DateTime"/>, to the tick of 100 ns) in a time scale (<see cref="Scale"/>). The scale
/// is a required argument of every constructor and factory (D3 of #668, approved by S. Guillet, 2026-11-13, amended 2026-11-19): nothing reads
/// a calendar as TDB by default any more.
/// </summary>
/// <remarks>
/// <para>
/// Two times are equal when they are the same instant, whatever their scales (#512): <see cref="Equals(Time)"/>,
/// <c>==</c>, <see cref="GetHashCode"/> and <see cref="CompareTo(Time)"/> follow one key, the tick of the instant in TAI,
/// so two consecutive TDB ticks of one TAI tick are equal. <see cref="EqualsExact"/> and <see cref="ExactComparer"/>
/// compare the labels: the calendar and the scale.
/// </para>
/// <para>
/// The default time is 0001-01-01T00:00:00 TDB (<see cref="TimeScale.TDB"/> is 0).
/// </para>
/// </remarks>
public readonly record struct Time : IComparable<Time>, IComparable, ISpanParsable<Time>, ISpanFormattable
{
    /// <summary>
    /// The calendar of J2000, 2000-01-01T12:00:00, of kind <see cref="DateTimeKind.Unspecified"/>: the origin of the
    /// seconds of <see cref="Create(double, TimeScale)"/> and <see cref="TimeSpanFromJ2000"/> in every scale.
    /// </summary>
    internal static readonly DateTime J2000Calendar = new DateTime(2000, 01, 01, 12, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// J2000, 2000-01-01T12:00:00 TDB.
    /// </summary>
    public static readonly Time J2000TDB = new Time(J2000Calendar, TimeScale.TDB);

    /// <summary>
    /// 2000-01-01T12:00:00 UTC.
    /// </summary>
    public static readonly Time J2000UTC = new Time(J2000Calendar, TimeScale.UTC);

    /// <summary>
    /// The Julian date of B1950's day, 1950-01-01T00:00:00.
    /// </summary>
    public const double JULIAN_J1950 = 2433282.5;

    /// <summary>
    /// The Julian date of J2000, 2000-01-01T12:00:00.
    /// </summary>
    public const double JULIAN_J2000 = 2451545.0;

    /// <summary>
    /// The Julian year, in days.
    /// </summary>
    public const double JULIAN_YEAR = 365.25;

    /// <summary>
    /// The seconds of a day without leap second.
    /// </summary>
    public const double SECONDS_PER_DAY = 86400.0;

    /// <summary>
    /// The calendar of this time in its <see cref="Scale"/>: of kind <see cref="DateTimeKind.Utc"/> in UTC,
    /// <see cref="DateTimeKind.Unspecified"/> otherwise.
    /// </summary>
    public DateTime DateTime { get; }

    /// <summary>
    /// The time scale of the calendar (replaces <c>Frame</c>, an <c>ITimeFrame</c>, of 10.x, D3 of #668).
    /// </summary>
    public TimeScale Scale { get; }

    /// <summary>
    /// The time of the calendar <paramref name="dateTime"/> in <paramref name="scale"/>. The calendar is stored with the
    /// kind of its scale: <see cref="DateTimeKind.Utc"/> for UTC, <see cref="DateTimeKind.Unspecified"/> otherwise.
    /// </summary>
    /// <param name="dateTime">The calendar, of kind <see cref="DateTimeKind.Unspecified"/>, or
    /// <see cref="DateTimeKind.Utc"/> with <see cref="TimeScale.UTC"/>.</param>
    /// <param name="scale">The time scale of the calendar.</param>
    /// <exception cref="ArgumentException"><paramref name="dateTime"/> is of kind <see cref="DateTimeKind.Local"/> (a
    /// local time is not a scale: give its <see cref="System.DateTime.ToUniversalTime"/> with <see cref="TimeScale.UTC"/>),
    /// or of kind <see cref="DateTimeKind.Utc"/> with another scale than UTC (D3 of #668: before 11.0.0 the kind was
    /// replaced by the frame's, silently).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public Time(in DateTime dateTime, TimeScale scale)
    {
        RequireScale(scale, nameof(scale));
        if (dateTime.Kind == DateTimeKind.Local)
        {
            throw new ArgumentException(
                "A local DateTime has no time scale: give its ToUniversalTime() with TimeScale.UTC.", nameof(dateTime));
        }

        if (dateTime.Kind == DateTimeKind.Utc && scale != TimeScale.UTC)
        {
            throw new ArgumentException(
                $"A DateTime of kind Utc is a UTC calendar: give TimeScale.UTC, or a calendar of kind Unspecified for {scale}.",
                nameof(dateTime));
        }

        Scale = scale;
        DateTime = System.DateTime.SpecifyKind(dateTime,
            scale == TimeScale.UTC ? DateTimeKind.Utc : DateTimeKind.Unspecified);
    }

    /// <summary>
    /// The time of midnight of a day in <paramref name="scale"/>.
    /// </summary>
    /// <param name="year">The year, from 1 to 9999.</param>
    /// <param name="month">The month, from 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="scale">The time scale of the calendar.</param>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its range, or the scale is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public Time(int year, int month, int day, TimeScale scale)
        : this(new DateTime(year, month, day), scale)
    {
    }

    /// <summary>
    /// The time of a calendar to the second in <paramref name="scale"/>.
    /// </summary>
    /// <param name="year">The year, from 1 to 9999.</param>
    /// <param name="month">The month, from 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="hour">The hour, from 0 to 23.</param>
    /// <param name="minute">The minute, from 0 to 59.</param>
    /// <param name="second">The second, from 0 to 59 (a time holds no leap second).</param>
    /// <param name="scale">The time scale of the calendar.</param>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its range, or the scale is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public Time(int year, int month, int day, int hour, int minute, int second, TimeScale scale)
        : this(new DateTime(year, month, day, hour, minute, second), scale)
    {
    }

    /// <summary>
    /// The time of a calendar to the millisecond in <paramref name="scale"/>.
    /// </summary>
    /// <param name="year">The year, from 1 to 9999.</param>
    /// <param name="month">The month, from 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="hour">The hour, from 0 to 23.</param>
    /// <param name="minute">The minute, from 0 to 59.</param>
    /// <param name="second">The second, from 0 to 59 (a time holds no leap second).</param>
    /// <param name="millisecond">The millisecond, from 0 to 999.</param>
    /// <param name="scale">The time scale of the calendar.</param>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its range, or the scale is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public Time(int year, int month, int day, int hour, int minute, int second, int millisecond, TimeScale scale)
        : this(new DateTime(year, month, day, hour, minute, second, millisecond), scale)
    {
    }

    /// <summary>
    /// The time of a calendar to the microsecond in <paramref name="scale"/>.
    /// </summary>
    /// <param name="year">The year, from 1 to 9999.</param>
    /// <param name="month">The month, from 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="hour">The hour, from 0 to 23.</param>
    /// <param name="minute">The minute, from 0 to 59.</param>
    /// <param name="second">The second, from 0 to 59 (a time holds no leap second).</param>
    /// <param name="millisecond">The millisecond, from 0 to 999.</param>
    /// <param name="microsecond">The microsecond, from 0 to 999.</param>
    /// <param name="scale">The time scale of the calendar.</param>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its range, or the scale is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public Time(int year, int month, int day, int hour, int minute, int second, int millisecond, int microsecond,
        TimeScale scale)
        : this(new DateTime(year, month, day, hour, minute, second, millisecond, microsecond), scale)
    {
    }

    /// <summary>
    /// The instant of the clock of the machine, in UTC (<see cref="System.DateTime.UtcNow"/>).
    /// </summary>
    public static Time UtcNow => new Time(System.DateTime.UtcNow, TimeScale.UTC);

    private static void RequireScale(TimeScale scale, string paramName)
    {
        if (scale is < TimeScale.TDB or > TimeScale.GPS)
        {
            throw new ArgumentOutOfRangeException(paramName, scale, "Unknown time scale.");
        }
    }

    #region Parse and format

    /// <summary>
    /// The time a tagged string writes (D3 of #668): an ISO 8601 date (<c>yyyy-MM-dd</c>) or a CCSDS day of year
    /// (<c>yyyy-DDD</c>), then optionally <c>T</c> (or a space) and <c>hh:mm</c>, <c>hh:mm:ss</c> or
    /// <c>hh:mm:ss.f…</c>, then <c>Z</c> or <c>±hh:mm</c> (the UTC instant) or the tag of its scale (<c>UTC</c>,
    /// <c>TT</c> or <c>TDT</c>, <c>TDB</c>, <c>TAI</c>, <c>GPS</c>, in any case, the space before it optional); or a Julian
    /// date <c>JD n</c> or <c>n JD</c> followed by its tag, its digits read exactly to the nearest tick. <see cref="ToString()"/>
    /// writes a string it reads back exactly (<see cref="EqualsExact"/>).
    /// </summary>
    /// <remarks>
    /// A fraction of up to 7 digits is exact; a longer one is rounded to the tick as <see cref="System.DateTime"/>
    /// parsing rounds it. A second 60 is refused (a time holds no leap second; give a UTC leap second in TAI), and so are a
    /// tag after <c>Z</c> or an offset, a string without a tag (<see cref="Parse(string, TimeScale)"/> gives one its
    /// scale), and the other forms <c>Time(string)</c> read through <see cref="System.DateTime.TryParse(string, out DateTime)"/>
    /// before 11.0.0 (<c>06/21/2024 12:00</c>, RFC 1123). An offset is the UTC instant it writes, never the machine's
    /// local time.
    /// </remarks>
    /// <param name="s">The string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The string is not of this grammar, or names no scale.</exception>
    public static Time Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), null);
    }

    /// <summary>
    /// The time a string writes, of the grammar of <see cref="Parse(string)"/>, <paramref name="scale"/> the scale of a
    /// string without a tag, <c>Z</c> or offset (D3.4 of #668); a tag of the same scale is accepted (<c>TDT</c> for
    /// <see cref="TimeScale.TT"/>, <c>Z</c> or an offset for <see cref="TimeScale.UTC"/>), and one of another scale
    /// contradicts it and throws.
    /// </summary>
    /// <param name="s">The string.</param>
    /// <param name="scale">The scale of an untagged string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The string is not of this grammar, or names another scale than
    /// <paramref name="scale"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public static Time Parse(string s, TimeScale scale)
    {
        ArgumentNullException.ThrowIfNull(s);
        RequireScale(scale, nameof(scale));
        if (!TimeGrammar.TryParse(s.AsSpan(), scale, out var result, out var error))
        {
            throw new FormatException(error);
        }

        return result;
    }

    /// <summary>
    /// <see cref="Parse(string)"/>, <paramref name="provider"/> unused (<see cref="IParsable{TSelf}"/>): the grammar is
    /// culture invariant.
    /// </summary>
    /// <param name="s">The string.</param>
    /// <param name="provider">Unused.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The string is not of the grammar, or names no scale.</exception>
    public static Time Parse(string s, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>
    /// <see cref="Parse(string)"/> of a span, <paramref name="provider"/> unused (<see cref="ISpanParsable{TSelf}"/>).
    /// </summary>
    /// <param name="s">The characters.</param>
    /// <param name="provider">Unused.</param>
    /// <exception cref="FormatException">The string is not of the grammar, or names no scale.</exception>
    public static Time Parse(ReadOnlySpan<char> s, IFormatProvider provider)
    {
        if (!TimeGrammar.TryParse(s, null, out var result, out var error))
        {
            throw new FormatException(error);
        }

        return result;
    }

    /// <summary>
    /// <see cref="Parse(string)"/> without exception: false for a null string or one the grammar refuses.
    /// </summary>
    /// <param name="s">The string.</param>
    /// <param name="result">The time, the default time when false.</param>
    public static bool TryParse([NotNullWhen(true)] string s, out Time result)
    {
        result = default;
        return s != null && TimeGrammar.TryParse(s.AsSpan(), null, out result, out _);
    }

    /// <summary>
    /// <see cref="Parse(string, TimeScale)"/> without exception: false for a null string, one the grammar refuses, one that
    /// names another scale than <paramref name="scale"/>, or a scale that is not a member of <see cref="TimeScale"/>.
    /// </summary>
    /// <param name="s">The string.</param>
    /// <param name="scale">The scale of an untagged string.</param>
    /// <param name="result">The time, the default time when false.</param>
    public static bool TryParse([NotNullWhen(true)] string s, TimeScale scale, out Time result)
    {
        result = default;
        return s != null && scale is >= TimeScale.TDB and <= TimeScale.GPS &&
               TimeGrammar.TryParse(s.AsSpan(), scale, out result, out _);
    }

    /// <summary>
    /// <see cref="TryParse(string, out Time)"/>, <paramref name="provider"/> unused (<see cref="IParsable{TSelf}"/>).
    /// </summary>
    /// <param name="s">The string.</param>
    /// <param name="provider">Unused.</param>
    /// <param name="result">The time, the default time when false.</param>
    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, out Time result)
    {
        return TryParse(s, out result);
    }

    /// <summary>
    /// <see cref="TryParse(string, out Time)"/> of a span, <paramref name="provider"/> unused
    /// (<see cref="ISpanParsable{TSelf}"/>).
    /// </summary>
    /// <param name="s">The characters.</param>
    /// <param name="provider">Unused.</param>
    /// <param name="result">The time, the default time when false.</param>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out Time result)
    {
        return TimeGrammar.TryParse(s, null, out result, out _);
    }

    /// <summary>
    /// The calendar in the round-trip form of <see cref="System.DateTime"/> (<c>O</c>), then the tag of the scale:
    /// <c>Z</c> for UTC, <c> TDB</c>, <c> TT</c>, <c> TAI</c> or <c> GPS</c> otherwise (TT was written <c>TDT</c> before
    /// 11.0.0). <see cref="Parse(string)"/> reads it back exactly.
    /// </summary>
    public override string ToString()
    {
        return DateTime.ToString("O", CultureInfo.InvariantCulture) + TimeScaleConversions.Suffix(Scale);
    }

    /// <summary>
    /// The time in <paramref name="format"/> (<see cref="IFormattable"/>, D3.5 of #668): null, empty, <c>G</c>, <c>O</c>
    /// or <c>o</c> give <see cref="ToString()"/>; any other format is a custom format of <see cref="System.DateTime"/>
    /// applied to the calendar, followed by the tag of the scale (<c>Z</c> for UTC, <c> TDB</c>, <c> TT</c>, <c> TAI</c>
    /// or <c> GPS</c>).
    /// </summary>
    /// <remarks>
    /// A standard format of <see cref="System.DateTime"/> (one letter, <c>u</c> or <c>R</c> among them, which write their
    /// own UTC designator) and a zone specifier (<c>z</c>, <c>zz</c>, <c>zzz</c>, <c>K</c>, which write the machine's
    /// offset for a calendar of another scale than UTC and a second designator after a UTC one) are refused: the scale is
    /// the tag. A letter in quotes or after a backslash is a literal; <c>%</c> before a letter makes it a custom
    /// specifier (<c>%d</c>).
    /// </remarks>
    /// <param name="format">The format.</param>
    /// <param name="formatProvider">The culture of a format of <see cref="System.DateTime"/>; the invariant culture when
    /// null.</param>
    /// <exception cref="FormatException">The format is not a custom format of <see cref="System.DateTime"/>, is a standard
    /// format, or holds a zone specifier.</exception>
    public string ToString(string format, IFormatProvider formatProvider)
    {
        if (IsRoundTripFormat(format))
        {
            return ToString();
        }

        RequireCustomFormatWithoutZone(format);
        return DateTime.ToString(format, formatProvider ?? CultureInfo.InvariantCulture) + Tag(Scale);
    }

    /// <summary>
    /// <see cref="ToString(string, IFormatProvider)"/> written into <paramref name="destination"/>
    /// (<see cref="ISpanFormattable"/>).
    /// </summary>
    /// <param name="destination">The span written.</param>
    /// <param name="charsWritten">The characters written, 0 when false.</param>
    /// <param name="format">The format.</param>
    /// <param name="provider">The culture of a format of <see cref="System.DateTime"/>; the invariant culture when null.</param>
    /// <returns>False when <paramref name="destination"/> is too short.</returns>
    /// <exception cref="FormatException">The format is refused by <see cref="ToString(string, IFormatProvider)"/>.</exception>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider provider)
    {
        string text = ToString(format.IsEmpty ? null : format.ToString(), provider);
        if (text.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        text.AsSpan().CopyTo(destination);
        charsWritten = text.Length;
        return true;
    }

    private static bool IsRoundTripFormat(string format) => string.IsNullOrEmpty(format) || format is "G" or "O" or "o";

    // A custom format of DateTime without a zone specifier (D3.5 of #668): one character is a standard format, and z or K
    // outside quotes and escapes writes a zone the tag already gives (the machine's offset for a calendar of kind
    // Unspecified)
    private static void RequireCustomFormatWithoutZone(string format)
    {
        if (format.Length == 1)
        {
            throw new FormatException(
                $"'{format}' is a standard format of DateTime: a Time takes null, G, O or a custom format (yyyy-MM-ddTHH:mm:ss), followed by the tag of its scale.");
        }

        char quote = '\0';
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else if (c == '\\')
                {
                    i++;
                }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '\\')
            {
                i++;
            }
            else if (c is 'z' or 'K')
            {
                throw new FormatException(
                    $"'{format}' holds the zone specifier '{c}': the scale of a Time is the tag written after the format, never an offset.");
            }
        }
    }

    // The tag after a calendar written with a custom format: Z right after a UTC calendar, the scale after a space otherwise
    private static string Tag(TimeScale scale) =>
        scale == TimeScale.UTC ? "Z" : TimeScaleConversions.Suffix(scale);

    #endregion

    #region Arithmetic

    /// <summary>
    /// This time plus <paramref name="timeSpan"/>, in its scale.
    /// </summary>
    public Time Add(in TimeSpan timeSpan)
    {
        return new Time(DateTime.Add(timeSpan), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="years"/> years of its calendar.
    /// </summary>
    public Time AddYears(int years)
    {
        return new Time(DateTime.AddYears(years), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="months"/> months of its calendar.
    /// </summary>
    public Time AddMonths(int months)
    {
        return new Time(DateTime.AddMonths(months), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="days"/>, to the nearest tick (100 ns), ties to even (#637, decided by S. Guillet,
    /// 2026-11-17; before 11.0.0, <see cref="System.DateTime.AddDays"/>, which truncates toward zero, one tick early for
    /// about half of the spans).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is not finite (#680: NaN returned this time
    /// before 11.0.0), or the result is outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddDays(double days)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(days, TimeSpan.TicksPerDay, nameof(days))), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="hours"/>, to the nearest tick (100 ns), ties to even (#637, decided by S. Guillet,
    /// 2026-11-17; before 11.0.0, <see cref="System.DateTime.AddHours"/>, which truncates toward zero).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hours"/> is not finite (#680: NaN returned this time
    /// before 11.0.0), or the result is outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddHours(double hours)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(hours, TimeSpan.TicksPerHour, nameof(hours))), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="minutes"/>, to the nearest tick (100 ns), ties to even (#637, decided by S. Guillet,
    /// 2026-11-17; before 11.0.0, <see cref="System.DateTime.AddMinutes"/>, which truncates toward zero).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minutes"/> is not finite (#680: NaN returned this time
    /// before 11.0.0), or the result is outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddMinutes(double minutes)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(minutes, TimeSpan.TicksPerMinute, nameof(minutes))), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="seconds"/>, to the nearest tick (100 ns), ties to even (#625).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is not finite, or the result is outside
    /// the range of <see cref="System.DateTime"/>.</exception>
    public Time AddSeconds(double seconds)
    {
        return new Time(DateTime.AddTicks(SecondsToTicks(seconds)), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="milliseconds"/>, to the nearest tick (100 ns), ties to even (#625).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="milliseconds"/> is not finite, or the result is
    /// outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddMilliseconds(double milliseconds)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(milliseconds, TimeSpan.TicksPerMillisecond, nameof(milliseconds))), Scale);
    }

    /// <summary>
    /// This time plus <paramref name="microseconds"/>, to the nearest tick (100 ns), ties to even (#625).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="microseconds"/> is not finite, or the result is
    /// outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddMicroseconds(double microseconds)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(microseconds, TimeSpan.TicksPerMicrosecond, nameof(microseconds))), Scale);
    }

    /// <summary>
    /// The tick count (100 ns) nearest <paramref name="seconds"/>, ties to even, symmetric in sign: the conversion of the
    /// seconds of <c>Create</c>, <see cref="AddSeconds"/> (with the ticks per millisecond and microsecond, of
    /// <see cref="AddMilliseconds"/> and <see cref="AddMicroseconds"/>) and of the TDB − TT offset to ticks (#625).
    /// <see cref="System.DateTime.AddSeconds"/> and <see cref="TimeSpan.FromSeconds(double)"/> truncate toward zero
    /// instead, up to one tick early after J2000 and late before it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is not finite or its ticks do not fit in
    /// a <see cref="long"/>.</exception>
    internal static long SecondsToTicks(double seconds) => ToNearestTicks(seconds, TimeSpan.TicksPerSecond, nameof(seconds));

    /// <summary>
    /// The <see cref="TimeSpan"/> of the tick count nearest <paramref name="seconds"/>, ties to even
    /// (<see cref="SecondsToTicks"/>): the spans the library computes outside <see cref="Time"/> (the period of an orbit,
    /// the sidereal period and true solar day of a body, the thrust window of an impulse, the hold of a phasing maneuver,
    /// the offsets of the launch windows, the steps and epochs of the conjunction search; #637, decided by S. Guillet,
    /// 2026-11-17). Before 11.0.0 they went through <see cref="TimeSpan.FromSeconds(double)"/>, which truncates toward
    /// zero; its exception types are kept, the bound of the overflow that of <see cref="SecondsToTicks"/>, 0.48 s below the
    /// 922 337 203 685.477 s <see cref="TimeSpan.FromSeconds(double)"/> accepts.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="seconds"/> is NaN.</exception>
    /// <exception cref="OverflowException"><paramref name="seconds"/> is infinite, or its ticks do not fit in a
    /// <see cref="TimeSpan"/> (|seconds| of 922 337 203 685 or more).</exception>
    internal static TimeSpan SecondsToSpan(double seconds)
    {
        if (double.IsNaN(seconds))
        {
            throw new ArgumentException("A span cannot be NaN seconds.", nameof(seconds));
        }

        if (!(System.Math.Abs(seconds) < MaximumSpanSeconds))
        {
            throw new OverflowException($"A span of {seconds:R} s is outside the range of a TimeSpan.");
        }

        return TimeSpan.FromTicks(SecondsToTicks(seconds));
    }

    /// <summary>
    /// The seconds below which a span has its ticks in a <see cref="long"/>, the bound <see cref="SecondsToTicks"/> checks.
    /// </summary>
    internal const double MaximumSpanSeconds = long.MaxValue / TimeSpan.TicksPerSecond;

    // The magnitude of the value, its whole units and its fraction apart, so that the whole part and the fraction are
    // exact (a fraction 1 - |value| of a negative value above -1 would round to the ulp of [0.5, 1)); the fraction times
    // the ticks per unit rounded once, its residual (exact by the fused multiply-add) deciding the side of a half tick,
    // so that the result is the nearest tick of the exact value of the double; ties to even, which is symmetric, so the
    // sign is put back last
    private static long ToNearestTicks(double value, long ticksPerUnit, string paramName)
    {
        if (!double.IsFinite(value) || System.Math.Abs(value) >= long.MaxValue / ticksPerUnit)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The span does not fit in the ticks of a Time.");
        }

        double magnitude = System.Math.Abs(value);
        double whole = System.Math.Floor(magnitude);
        double fraction = magnitude - whole;
        double scaled = fraction * ticksPerUnit;
        double residual = System.Math.FusedMultiplyAdd(fraction, ticksPerUnit, -scaled);
        double floor = System.Math.Floor(scaled);
        double rest = scaled - floor;
        long ticks = (long)whole * ticksPerUnit + (long)floor;
        if (rest > 0.5 || (rest == 0.5 && (residual > 0.0 || (residual == 0.0 && (ticks & 1L) == 1L))))
        {
            ticks++;
        }

        return value < 0.0 ? -ticks : ticks;
    }

    /// <summary>
    /// This time plus <paramref name="ticks"/> ticks (100 ns) of its calendar.
    /// </summary>
    public Time AddTicks(long ticks)
    {
        return new Time(DateTime.AddTicks(ticks), Scale);
    }

    /// <summary>
    /// <paramref name="left"/> plus <paramref name="right"/>, in its scale.
    /// </summary>
    public static Time operator +(Time left, TimeSpan right)
    {
        return left.Add(right);
    }

    /// <summary>
    /// <paramref name="left"/> less <paramref name="right"/>, in its scale.
    /// </summary>
    public static Time operator -(Time left, TimeSpan right)
    {
        return left.Add(right.Negate());
    }

    /// <summary>
    /// The time elapsed from <paramref name="right"/> to <paramref name="left"/>: the difference of their calendars in a
    /// uniform scale (TDB, TT, TAI, GPS), otherwise the difference of their TAI, so that the leap seconds between two UTC
    /// times count (#508).
    /// </summary>
    /// <remarks>
    /// A UTC calendar skips the leap seconds: from 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z, 121 s elapse, the
    /// leap second 2016-12-31T23:59:60 included, while the calendars differ by 120 s. Between two scales, the difference
    /// is that of TAI, so TT seconds. Two equal times (<see cref="Equals(Time)"/>) of two consecutive TDB ticks differ by
    /// one tick.
    /// </remarks>
    public static TimeSpan operator -(Time left, Time right)
    {
        if (left.Scale == right.Scale && left.Scale != TimeScale.UTC)
        {
            return left.DateTime.Subtract(right.DateTime);
        }

        return new TimeSpan(TimeScaleConversions.TaiTicks(left) - TimeScaleConversions.TaiTicks(right));
    }

    #endregion

    #region Conversions

    /// <summary>
    /// This time in <paramref name="scale"/>, through TAI; this time itself when it is already in that scale.
    /// </summary>
    /// <remarks>
    /// The round trip through TAI of a TDB time rounds TDB − TT to the nearest 100 ns tick twice, at two arguments of the
    /// series, so it can move the time by a tick (1 of the 935 041 TT instants every 0.05 day from 1972 to 2100, #625;
    /// 2 when the conversions truncated, #510); a conversion to the scale of the time is the identity.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public Time ConvertTo(TimeScale scale)
    {
        RequireScale(scale, nameof(scale));
        if (Scale == scale)
        {
            return this;
        }

        var tai = new DateTime(TimeScaleConversions.TaiTicks(this));
        return new Time(TimeScaleConversions.FromTai(tai, scale), scale);
    }

    /// <summary>
    /// This time in GPS time.
    /// </summary>
    public Time ToGPS()
    {
        return ConvertTo(TimeScale.GPS);
    }

    /// <summary>
    /// This time in TAI.
    /// </summary>
    public Time ToTAI()
    {
        return ConvertTo(TimeScale.TAI);
    }

    /// <summary>
    /// This time in TT (<c>ToTDT</c> before 11.0.0).
    /// </summary>
    public Time ToTT()
    {
        return ConvertTo(TimeScale.TT);
    }

    /// <summary>
    /// This time in UTC.
    /// </summary>
    public Time ToUTC()
    {
        return ConvertTo(TimeScale.UTC);
    }

    /// <summary>
    /// This time in TDB.
    /// </summary>
    public Time ToTDB()
    {
        return ConvertTo(TimeScale.TDB);
    }

    /// <summary>
    /// The span of the calendar from J2000 (2000-01-01T12:00:00 of the same scale), the seconds of
    /// <see cref="Create(double, TimeScale)"/>.
    /// </summary>
    public TimeSpan TimeSpanFromJ2000()
    {
        return DateTime - J2000Calendar;
    }

    /// <summary>
    /// The time of <paramref name="secondsFromJ2000"/> seconds of the calendar of <paramref name="scale"/> from
    /// 2000-01-01T12:00:00 of that scale, to the nearest tick (100 ns), ties to even (#625): an ET of SPICE from 2017 to
    /// 2028 comes back from <see cref="TimeSpanFromJ2000"/> exactly.
    /// </summary>
    /// <param name="secondsFromJ2000">The seconds from J2000.</param>
    /// <param name="scale">The time scale.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="secondsFromJ2000"/> is not finite or the result is
    /// outside the range of <see cref="System.DateTime"/>, or <paramref name="scale"/> is not a member of
    /// <see cref="TimeScale"/>.</exception>
    public static Time Create(double secondsFromJ2000, TimeScale scale)
    {
        return new Time(J2000Calendar.AddTicks(SecondsToTicks(secondsFromJ2000)), scale);
    }

    /// <summary>
    /// The time of a day of year and the seconds of that day in <paramref name="scale"/>, to the nearest tick (100 ns),
    /// ties to even (#625); <c>Time.Create(year, doy, sec)</c> before 11.0.0, which was UTC.
    /// </summary>
    /// <remarks>
    /// The seconds are those of a day without leap second; a value below 86400 whose nearest tick is 86400 s is the next
    /// day's midnight, the nearest instant, except on 9999-12-31, whose next midnight no <see cref="System.DateTime"/>
    /// holds: there seconds above 86399.99999995, whose nearest tick is 86400 s, throw naming <paramref name="seconds"/>.
    /// Before 11.0.0 nothing was checked (#680): year 0 was read as 2000, and a day or seconds outside their range rolled
    /// into the neighbouring day or year.
    /// </remarks>
    /// <param name="year">The year, from 1 to 9999.</param>
    /// <param name="dayOfYear">The day of the year, from 1 to 365, or 366 in a leap year of the Gregorian calendar.</param>
    /// <param name="seconds">The seconds of the day, finite, in [0, 86400).</param>
    /// <param name="scale">The time scale.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside its range; the exception names it.</exception>
    public static Time FromDayOfYear(int year, int dayOfYear, double seconds, TimeScale scale)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(dayOfYear, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dayOfYear, System.DateTime.IsLeapYear(year) ? 366 : 365);
        if (!(seconds >= 0.0 && seconds < 86400.0))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "The seconds of a day are finite, in [0, 86400).");
        }

        RequireScale(scale, nameof(scale));
        var day = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
        long ticks = SecondsToTicks(seconds);
        if (ticks > System.DateTime.MaxValue.Ticks - day.Ticks)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "The nearest tick of the seconds is past 9999-12-31T23:59:59.9999999.");
        }

        return new Time(day.AddTicks(ticks), scale);
    }

    /// <summary>
    /// The UTC time of <paramref name="secondsFromJ2000"/> seconds of the UTC calendar from 2000-01-01T12:00:00Z
    /// (<see cref="Create(double, TimeScale)"/>).
    /// </summary>
    /// <param name="secondsFromJ2000">The seconds from J2000.</param>
    public static Time CreateUTC(double secondsFromJ2000)
    {
        return Create(secondsFromJ2000, TimeScale.UTC);
    }

    /// <summary>
    /// The TDB time of <paramref name="secondsFromJ2000"/> seconds from J2000 TDB, an ET of SPICE
    /// (<see cref="Create(double, TimeScale)"/>).
    /// </summary>
    /// <param name="secondsFromJ2000">The seconds from J2000.</param>
    public static Time CreateTDB(double secondsFromJ2000)
    {
        return Create(secondsFromJ2000, TimeScale.TDB);
    }

    /// <summary>
    /// The Julian date of this time, in its scale.
    /// </summary>
    /// <remarks>
    /// Computed from the <see cref="System.DateTime"/> ticks (0.1 µs). A Julian date held in a single double
    /// resolves about 40 µs; <see cref="Centuries"/> and the ticks keep the full resolution.
    /// </remarks>
    public double ToJulianDate()
    {
        return JULIAN_J2000 + DaysFromJ2000();
    }

    /// <summary>
    /// Days elapsed since J2000 (2000-01-01 12:00:00) in this time scale, from the <see cref="System.DateTime"/> ticks.
    /// </summary>
    internal double DaysFromJ2000()
    {
        return (DateTime.Ticks - J2000Calendar.Ticks) / (double)TimeSpan.TicksPerDay;
    }

    /// <summary>
    /// The Julian centuries of this time's calendar from J2000, in its scale.
    /// </summary>
    public double Centuries()
    {
        return DaysFromJ2000() / 36525.0;
    }

    /// <summary>
    /// The time of <paramref name="julianDate"/> in <paramref name="scale"/>: J2000 plus the nearest tick of the days
    /// <paramref name="julianDate"/> − 2451545.0, ties to even (#637, decided by S. Guillet, 2026-11-17; before 11.0.0,
    /// <see cref="System.DateTime.AddDays"/>, which truncates toward zero). The difference is exact for a Julian date from
    /// 1225772.5 to 4903090 (Sterbenz's lemma), about the years −1356 to 8711.
    /// </summary>
    /// <param name="julianDate">The Julian date.</param>
    /// <param name="scale">The time scale.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="julianDate"/> is not finite (#680: NaN gave J2000
    /// before 11.0.0), or the result is outside the range of <see cref="System.DateTime"/>, or <paramref name="scale"/> is
    /// not a member of <see cref="TimeScale"/>.</exception>
    public static Time CreateFromJD(double julianDate, TimeScale scale)
    {
        var date = J2000Calendar.AddTicks(ToNearestTicks(julianDate - JULIAN_J2000, TimeSpan.TicksPerDay, nameof(julianDate)));
        return new Time(date, scale);
    }

    #endregion

    #region COMPARATOR

    /// <summary>
    /// The ticks of this time converted to TAI, the key of the order, the equality and the hash code of times (#512).
    /// </summary>
    internal long TaiTicks()
    {
        return TimeScaleConversions.TaiTicks(this);
    }

    /// <summary>
    /// Compares the instants of two times, within a scale and across scales alike, by their ticks converted to TAI: one
    /// total order, so that two times compare to 0 exactly when they are equal (<see cref="Equals(Time)"/>, #512).
    /// </summary>
    /// <remarks>
    /// Within UTC, TAI, TT or GPS the conversion to TAI keeps the order of the calendars, which are compared directly.
    /// Within TDB it does too for times two ticks or more apart; two consecutive TDB ticks can convert to one TAI tick
    /// (TDB − TT, rounded to the tick, gains one there), and then compare to 0. Within TDB, <c>-</c> still counts the ticks
    /// of the calendars: two such equal ticks differ by one tick. The default time is 0001-01-01T00:00:00 TDB.
    /// </remarks>
    public int CompareTo(Time other)
    {
        if (Scale == other.Scale)
        {
            if (Scale != TimeScale.TDB)
            {
                return DateTime.CompareTo(other.DateTime);
            }

            long difference = DateTime.Ticks - other.DateTime.Ticks;
            if (difference is not (1 or -1))
            {
                return difference.CompareTo(0L);
            }
        }

        return TaiTicks().CompareTo(other.TaiTicks());
    }

    /// <summary>
    /// Whether two times are the same instant, whatever their scales: <see cref="CompareTo(Time)"/> returns 0 (#512,
    /// decided by S. Guillet, 2026-11-17; the TAI tick confirmed as the key by S. Guillet, 2026-11-19). <c>==</c>,
    /// <c>!=</c>, <see cref="object.Equals(object)"/> and the hash collections follow it, and <see cref="GetHashCode"/>
    /// hashes the TAI ticks.
    /// </summary>
    /// <remarks>
    /// To tell the labels apart (the same calendar in the same scale), use <see cref="EqualsExact"/> or
    /// <see cref="ExactComparer"/>.
    /// </remarks>
    public bool Equals(Time other)
    {
        return CompareTo(other) == 0;
    }

    /// <summary>
    /// Whether two times carry the same label: the same calendar, to the tick, in the same scale. Equal times
    /// (<see cref="Equals(Time)"/>) can carry different labels, the same instant in two scales, or two consecutive TDB
    /// ticks of one TAI tick (#512).
    /// </summary>
    /// <param name="other">The other time.</param>
    public bool EqualsExact(Time other)
    {
        return DateTime.Ticks == other.DateTime.Ticks && Scale == other.Scale;
    }

    /// <summary>
    /// The equality of labels (<see cref="EqualsExact"/>), for a hash collection that keeps the labels apart. The
    /// library's caches whose values carry their key as a label use it too (the range rates of the conjunction search): a
    /// key in another scale, or a TDB tick next to one of the same TAI tick, is
    /// read again rather than given the value of another label, bit for bit (#512).
    /// </summary>
    public static IEqualityComparer<Time> ExactComparer { get; } = new TimeExactComparer();

    private sealed class TimeExactComparer : IEqualityComparer<Time>
    {
        public bool Equals(Time x, Time y) => x.EqualsExact(y);

        public int GetHashCode(Time time) => HashCode.Combine(time.DateTime.Ticks, time.Scale);
    }

    /// <summary>
    /// The hash code of the instant: that of its TAI ticks, so that equal times, in any scales, share it (#512).
    /// </summary>
    public override int GetHashCode()
    {
        return TaiTicks().GetHashCode();
    }

    /// <summary>
    /// <see cref="CompareTo(Time)"/> of a boxed time; a null object is before every time.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="obj"/> is not a <see cref="Time"/>.</exception>
    public int CompareTo(object obj)
    {
        if (obj is null) return 1;
        return obj is Time other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Time)}");
    }

    /// <summary>
    /// Whether <paramref name="left"/> is before <paramref name="right"/> (<see cref="CompareTo(Time)"/>).
    /// </summary>
    public static bool operator <(Time left, Time right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Whether <paramref name="left"/> is after <paramref name="right"/> (<see cref="CompareTo(Time)"/>).
    /// </summary>
    public static bool operator >(Time left, Time right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Whether <paramref name="left"/> is before or equal to <paramref name="right"/> (<see cref="CompareTo(Time)"/>).
    /// </summary>
    public static bool operator <=(Time left, Time right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Whether <paramref name="left"/> is after or equal to <paramref name="right"/> (<see cref="CompareTo(Time)"/>).
    /// </summary>
    public static bool operator >=(Time left, Time right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
