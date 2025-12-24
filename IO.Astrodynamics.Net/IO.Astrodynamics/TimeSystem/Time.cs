using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            var kind = string.Concat(timeString.TakeLast(3));
            ITimeFrame frame = null;
            switch (kind)
            {
                case "TDB":
                    frame = TimeFrame.TDBFrame;
                    break;
                case "TDT":
                    frame = TimeFrame.TDTFrame;
                    break;
                case "GPS":
                    frame = TimeFrame.GPSFrame;
                    break;
                case "TAI":
                    frame = TimeFrame.TAIFrame;
                    break;
                default:
                    throw new ArgumentException("Value is not a valid time frame.", nameof(timeString));
            }

            DateTime = DateTime.Parse(string.Concat(timeString.SkipLast(4) ?? throw new InvalidOperationException("invalid time string")),
                CultureInfo.InvariantCulture);
            Frame = frame;
        }
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

    public Time AddSeconds(double seconds)
    {
        return new Time(DateTime.AddSeconds(seconds), Frame);
    }

    public Time AddMilliseconds(double milliseconds)
    {
        return new Time(DateTime.AddMilliseconds(milliseconds), Frame);
    }

    public Time AddMicroseconds(double microseconds)
    {
        return new Time(DateTime.AddMicroseconds(microseconds), Frame);
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

    public static TimeSpan operator -(Time left, Time right)
    {
        return left.DateTime.Subtract(right.DateTime);
    }

    public Time ConvertTo(ITimeFrame targetReference)
    {
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
    /// Create TDB from seconds elapsed from J2000
    /// </summary>
    /// <param name="secondsFromJ2000"></param>
    /// <param name="frame"></param>
    /// <returns></returns>
    public static Time Create(double secondsFromJ2000, ITimeFrame frame)
    {
        var date = J2000.AddSeconds(secondsFromJ2000);
        if (frame is UTCTimeFrame utcFrame)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return new TimeSystem.Time(date, TimeFrame.UTCFrame);
        }

        return new TimeSystem.Time(date, frame);
    }
    
    /// <summary>
    /// Create time from year, day of year, and seconds of day
    /// </summary>
    /// <param name="year">Calendar year (0 defaults to 2000)</param>
    /// <param name="doy">Day of year (1-366)</param>
    /// <param name="sec">Seconds within the day (0-86399)</param>
    /// <returns></returns>
    public static Time Create(int year, int doy, double sec)
    {
        var baseDate = new DateTime(year == 0 ? 2000 : year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var date = baseDate.AddDays(doy - 1).AddSeconds(sec);
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
    /// Convert to julian date
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public double ToJulianDate()
    {
        return DateTime.ToOADate() + 2415018.5; //julian date at 1899-12-30 00:00:00
    }

    /// <summary>
    /// Get number of centuries
    /// </summary>
    /// <returns></returns>
    public double Centuries()
    {
        return (ToJulianDate() - JULIAN_J2000) / 36525.0;
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
        return DateTime.CompareTo(other.DateTime);
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