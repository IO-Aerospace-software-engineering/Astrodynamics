using System;
using System.Collections.Generic;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.TimeSystem;

public readonly record struct Time : IComparable<Time>, IComparable
{
   

    public static readonly DateTime J2000 = new DateTime(2000, 01, 01, 12, 0, 0, DateTimeKind.Unspecified);
    public static readonly Time J2000TDB = new Time(J2000, TimeFrame.TDBFrame);
    public static readonly Time J2000UTC = new Time(J2000, TimeFrame.UTCFrame);
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
    }

    public Time Add(in TimeSpan timeSpan)
    {
        return new Time(DateTime.Add(timeSpan), Frame);
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

    public override string ToString()
    {
        return DateTime.ToString("O") + " " + this.Frame.ToString();
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
            return new Time(date, TimeFrame.UTCFrame);
        }

        return new Time(date, frame);
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

        return new Time(date, frame);
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