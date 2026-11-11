using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.TimeSystem;

public readonly record struct Time : IComparable<Time>, IComparable
{
    public static readonly DateTime J2000 = new DateTime(2000, 01, 01, 12, 0, 0, DateTimeKind.Unspecified);
    public static readonly Time J2000TDB = new TimeSystem.Time(J2000, TimeFrame.TDBFrame);
    public static readonly Time J2000UTC = new TimeSystem.Time(J2000, TimeFrame.UTCFrame);
    public const double JULIAN_J1950 = 2433282.5;
    public const double JULIAN_J2000 = 2451545.0;
    public const double JULIAN_YEAR = 365.25;
    public const double SECONDS_PER_DAY = 86400.0;
    public DateTime DateTime { get; }
    public ITimeFrame Frame { get; }

    public Time(in DateTime dateTime, ITimeFrame frame)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        DateTime = dateTime;
        if (frame is UTCTimeFrame)
        {
            DateTime = System.DateTime.SpecifyKind(DateTime, DateTimeKind.Utc);
        }
        else if (frame is LocalTimeFrame)
        {
            DateTime = System.DateTime.SpecifyKind(DateTime, DateTimeKind.Local);
        }
        else
        {
            DateTime = System.DateTime.SpecifyKind(DateTime, DateTimeKind.Unspecified);
        }
    }

    public Time(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int millisecond = 0, int microseconds = 0, ITimeFrame frame = null) : this(
        new DateTime(year, month, day, hour, minute, second, millisecond, microseconds), frame ?? TimeFrame.TDBFrame)
    {
    }

    /// <summary>
    /// The time a string writes: an ISO 8601 calendar ending in Z (UTC), with an offset (local) or with neither (TDB), or a
    /// calendar followed by the name of its frame, with or without a space, in any case: UTC, TT or TDT, TDB, TAI, GPS (#455).
    /// </summary>
    /// <exception cref="ArgumentException">The string is empty, names another frame, holds a second 60, or gives a Z or an
    /// offset before a frame name.</exception>
    /// <exception cref="FormatException">The calendar before the frame name cannot be read.</exception>
    public Time(string timeString)
    {
        if (string.IsNullOrEmpty(timeString)) throw new ArgumentException("Value cannot be null or empty.", nameof(timeString));

        if (DateTime.TryParse(timeString, CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind, out DateTime datetime))
        {
            DateTime = datetime;
            Frame = datetime.Kind == DateTimeKind.Utc ? TimeFrame.UTCFrame : datetime.Kind == DateTimeKind.Local ? TimeFrame.LocalFrame : TimeFrame.TDBFrame;
        }
        else
        {
            if (LeapSecond.IsMatch(timeString))
                throw new ArgumentException("Second 60 cannot be represented: a Time holds no leap second (a UTC leap second can be given in TAI).", nameof(timeString));

            // The calendar, then the name of its frame, with or without a space (#455)
            var match = FrameName.Match(timeString);
            if (!match.Success) throw new ArgumentException("Value is not a valid time frame.", nameof(timeString));
            var frame = FrameFromName(match.Groups["name"].Value);

            // A Z or an offset gives the zone of the calendar: DateTime would convert it to the machine's local time
            var dateTime = DateTime.Parse(match.Groups["calendar"].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (dateTime.Kind != DateTimeKind.Unspecified)
                throw new ArgumentException("A calendar with a Z or an offset takes no frame name.", nameof(timeString));
            DateTime = frame is UTCTimeFrame ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) : dateTime;
            Frame = frame;
        }
    }

    /// <summary>
    /// A second 60, in the hh:mm:ss of a calendar.
    /// </summary>
    private static readonly Regex LeapSecond = new(@"\d:\d{2}:60(?!\d)", RegexOptions.CultureInvariant);

    /// <summary>
    /// A calendar, then the name of a frame after a space or right after the calendar's last digit or Z, in any case.
    /// </summary>
    private static readonly Regex FrameName = new(@"^\s*(?<calendar>\S.*?)\s*(?<=[\dZz\s])(?<name>UTC|TT|TDT|TDB|TAI|GPS)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// The frame a time string names after its calendar, in any case: TDB, TT or TDT, TAI, GPS, UTC; null for another name.
    /// </summary>
    private static ITimeFrame FrameFromName(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "TDB" => TimeFrame.TDBFrame,
            "TT" or "TDT" => TimeFrame.TDTFrame,
            "TAI" => TimeFrame.TAIFrame,
            "GPS" => TimeFrame.GPSFrame,
            "UTC" => TimeFrame.UTCFrame,
            _ => null
        };
    }

    public Time Add(in TimeSpan timeSpan)
    {
        return new TimeSystem.Time(DateTime.Add(timeSpan), Frame);
    }

    public Time AddYears(int years)
    {
        return new Time(DateTime.AddYears(years), Frame);
    }

    public Time AddMonths(int months)
    {
        return new Time(DateTime.AddMonths(months), Frame);
    }

    public Time AddDays(double days)
    {
        return new Time(DateTime.AddDays(days), Frame);
    }

    public Time AddHours(double hours)
    {
        return new Time(DateTime.AddHours(hours), Frame);
    }

    public Time AddMinutes(double minutes)
    {
        return new Time(DateTime.AddMinutes(minutes), Frame);
    }

    /// <summary>
    /// This time plus <paramref name="seconds"/>, to the nearest tick (100 ns), ties to even (#625).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is not finite, or the result is outside
    /// the range of <see cref="System.DateTime"/>.</exception>
    public Time AddSeconds(double seconds)
    {
        return new Time(DateTime.AddTicks(SecondsToTicks(seconds)), Frame);
    }

    /// <summary>
    /// This time plus <paramref name="milliseconds"/>, to the nearest tick (100 ns), ties to even (#625).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="milliseconds"/> is not finite, or the result is
    /// outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddMilliseconds(double milliseconds)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(milliseconds, TimeSpan.TicksPerMillisecond, nameof(milliseconds))), Frame);
    }

    /// <summary>
    /// This time plus <paramref name="microseconds"/>, to the nearest tick (100 ns), ties to even (#625).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="microseconds"/> is not finite, or the result is
    /// outside the range of <see cref="System.DateTime"/>.</exception>
    public Time AddMicroseconds(double microseconds)
    {
        return new Time(DateTime.AddTicks(ToNearestTicks(microseconds, TimeSpan.TicksPerMicrosecond, nameof(microseconds))), Frame);
    }

    /// <summary>
    /// The tick count (100 ns) nearest <paramref name="seconds"/>, ties to even, symmetric in sign: the conversion of the
    /// seconds of <c>Create</c>, <see cref="AddSeconds"/> (with the ticks per millisecond and microsecond, of
    /// <see cref="AddMilliseconds"/> and <see cref="AddMicroseconds"/>) and of the TDB − TT offset to ticks (#625). <see cref="System.DateTime.AddSeconds"/> and <see cref="TimeSpan.FromSeconds(double)"/>
    /// truncate toward zero instead, up to one tick early after J2000 and late before it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is not finite or its ticks do not fit in
    /// a <see cref="long"/>.</exception>
    internal static long SecondsToTicks(double seconds) => ToNearestTicks(seconds, TimeSpan.TicksPerSecond, nameof(seconds));

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

    public Time AddTicks(long ticks)
    {
        return new Time(DateTime.AddTicks(ticks), Frame);
    }

    public static Time operator +(Time left, TimeSpan right)
    {
        return left.Add(right);
    }

    public static Time operator -(Time left, TimeSpan right)
    {
        return left.Add(right.Negate());
    }

    /// <summary>
    /// The time elapsed from <paramref name="right"/> to <paramref name="left"/>: the difference of their calendars in a
    /// uniform frame (TDB, TT, TAI, GPS), otherwise the difference of their TAI, so that the leap seconds between two UTC
    /// (or local) times count (#508).
    /// </summary>
    /// <remarks>
    /// A UTC calendar skips the leap seconds: from 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z, 121 s elapse, the
    /// leap second 2016-12-31T23:59:60 included, while the calendars differ by 120 s. Between two frames, the difference
    /// is that of TAI, so TT seconds, as before.
    /// </remarks>
    public static TimeSpan operator -(Time left, Time right)
    {
        if (left.Frame.Equals(right.Frame) && left.Frame is not (UTCTimeFrame or LocalTimeFrame))
        {
            return left.DateTime.Subtract(right.DateTime);
        }

        // Convert both times to TAI to ensure correct physical time difference
        // when the times are in different frames (e.g., UTC vs TDB) or in a frame with leap seconds
        var leftTai = left.ToTAI();
        var rightTai = right.ToTAI();
        return leftTai.DateTime.Subtract(rightTai.DateTime);
    }

    /// <summary>
    /// This time in <paramref name="targetReference"/>, through TAI; this time itself when it is already in that frame.
    /// </summary>
    /// <remarks>
    /// The round trip through TAI of a TDB time rounds TDB − TT to the nearest 100 ns tick twice, at two arguments of the
    /// series, so it can move the time by a tick (1 of the 935 041 TT instants every 0.05 day from 1972 to 2100, #625;
    /// 2 when the conversions truncated, #510); a conversion to the frame of the time is the identity.
    /// </remarks>
    public Time ConvertTo(ITimeFrame targetReference)
    {
        if (Frame.Equals(targetReference))
        {
            return this;
        }

        var taiTimeWithRef = Frame.ConvertToTAI(this);
        return targetReference.ConvertFromTAI(taiTimeWithRef);
    }

    public Time ToGPS()
    {
        return ConvertTo(TimeFrame.GPSFrame);
    }

    public Time ToTAI()
    {
        return ConvertTo(TimeFrame.TAIFrame);
    }

    public Time ToTDT()
    {
        return ConvertTo(TimeFrame.TDTFrame);
    }

    public Time ToUTC()
    {
        return ConvertTo(TimeFrame.UTCFrame);
    }

    public Time ToTDB()
    {
        return ConvertTo(TimeFrame.TDBFrame);
    }

    public Time ToLocal()
    {
        return ConvertTo(TimeFrame.LocalFrame);
    }

    public override string ToString()
    {
        string suffix = string.Empty;
        if (!string.IsNullOrEmpty(this.Frame.ToString()))
        {
            suffix = " " + this.Frame.ToString();
        }

        return DateTime.ToString("O") + suffix;
    }

    public TimeSpan TimeSpanFromJ2000()
    {
        return DateTime - J2000;
    }

    /// <summary>
    /// Create a time from seconds elapsed from J2000 in <paramref name="frame"/>, to the nearest tick (100 ns), ties to
    /// even (#625): an ET of SPICE from 2017 to 2028 comes back from <see cref="TimeSpanFromJ2000"/> exactly.
    /// </summary>
    /// <param name="secondsFromJ2000"></param>
    /// <param name="frame"></param>
    /// <returns></returns>
    public static Time Create(double secondsFromJ2000, ITimeFrame frame)
    {
        var date = J2000.AddTicks(SecondsToTicks(secondsFromJ2000));
        if (frame is UTCTimeFrame utcFrame)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return new TimeSystem.Time(date, TimeFrame.UTCFrame);
        }

        return new TimeSystem.Time(date, frame);
    }
    
    /// <summary>
    /// Create a UTC time from year, day of year, and seconds of day, to the nearest tick (100 ns), ties to even (#625)
    /// </summary>
    /// <param name="year"></param>
    /// <param name="doy"></param>
    /// <param name="sec"></param>
    /// <returns></returns>
    public static Time Create(int year, int doy, double sec)
    {
        var baseDate = new DateTime(year == 0 ? 2000 : year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var date = baseDate.AddDays(doy - 1).AddTicks(SecondsToTicks(sec));
        return new Time(date, TimeFrame.UTCFrame);
    }

    /// <summary>
    /// Create UTC from elapsed seconds from J2000
    /// </summary>
    /// <param name="secondsFromJ2000"></param>
    /// <returns></returns>
    public static Time CreateUTC(double secondsFromJ2000)
    {
        return Create(secondsFromJ2000, TimeFrame.UTCFrame);
    }

    /// <summary>
    /// Create TDB from elapsed seconds from J2000
    /// </summary>
    /// <param name="secondsFromJ2000"></param>
    /// <returns></returns>
    public static Time CreateTDB(double secondsFromJ2000)
    {
        return Create(secondsFromJ2000, TimeFrame.TDBFrame);
    }

    /// <summary>
    /// Convert to julian date, in this time frame.
    /// </summary>
    /// <remarks>
    /// Computed from the <see cref="System.DateTime"/> ticks (0.1 µs). A Julian date held in a single double
    /// resolves about 40 µs; <see cref="DaysFromJ2000"/> keeps the full resolution.
    /// </remarks>
    public double ToJulianDate()
    {
        return JULIAN_J2000 + DaysFromJ2000();
    }

    /// <summary>
    /// Days elapsed since J2000 (2000-01-01 12:00:00) in this time frame, from the <see cref="System.DateTime"/> ticks.
    /// </summary>
    internal double DaysFromJ2000()
    {
        return (DateTime.Ticks - J2000.Ticks) / (double)TimeSpan.TicksPerDay;
    }

    /// <summary>
    /// Get number of centuries
    /// </summary>
    /// <returns></returns>
    public double Centuries()
    {
        return DaysFromJ2000() / 36525.0;
    }

    public static Time CreateFromJD(double julianDate, ITimeFrame frame)
    {
        var sinceEpoch = julianDate - JULIAN_J2000;
        var date = J2000.AddDays(sinceEpoch);
        if (frame is UTCTimeFrame)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return new TimeSystem.Time(date, frame);
    }

    #region COMPARATOR

    public int CompareTo(Time other)
    {
        // Convert both times to TAI to ensure correct physical time comparison
        // when the times are in different frames (e.g., UTC vs TDB)
        if (this.Frame.Equals(other.Frame))
        {
            return this.DateTime.CompareTo(other.DateTime);
        }
        var thisTai = this.ToTAI();
        var otherTai = other.ToTAI();
        return thisTai.DateTime.CompareTo(otherTai.DateTime);
    }

    public int CompareTo(object obj)
    {
        if (obj is null) return 1;
        return obj is Time other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Time)}");
    }

    public static bool operator <(Time left, Time right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Time left, Time right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Time left, Time right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Time left, Time right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}