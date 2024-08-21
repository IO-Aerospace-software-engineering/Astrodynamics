using System;
using System.Linq;

namespace IO.Astrodynamics.Time;

public abstract class DateTimeBase
{
    private static DateTime[] LEAP_SECONDS =
    {
        new DateTime(1972, 1, 1),
        new DateTime(1972, 7, 1),
        new DateTime(1973, 1, 1),
        new DateTime(1974, 1, 1),
        new DateTime(1975, 1, 1),
        new DateTime(1976, 1, 1),
        new DateTime(1977, 1, 1),
        new DateTime(1978, 1, 1),
        new DateTime(1979, 1, 1),
        new DateTime(1980, 1, 1),
        new DateTime(1981, 7, 1),
        new DateTime(1982, 7, 1),
        new DateTime(1983, 7, 1),
        new DateTime(1985, 7, 1),
        new DateTime(1988, 1, 1),
        new DateTime(1990, 1, 1),
        new DateTime(1991, 1, 1),
        new DateTime(1992, 7, 1),
        new DateTime(1993, 7, 1),
        new DateTime(1994, 7, 1),
        new DateTime(1996, 1, 1),
        new DateTime(1997, 7, 1),
        new DateTime(1999, 1, 1),
        new DateTime(2006, 1, 1),
        new DateTime(2009, 1, 1),
        new DateTime(2012, 7, 1),
        new DateTime(2015, 7, 1),
        new DateTime(2017, 1, 1),
    };

    public DateTime DateTime { get; }
    public DateTimeKind Kind { get; }


    public DateTimeBase(DateTime dateTime, DateTimeKind kind)
    {
        DateTime = dateTime;
        Kind = kind;
    }

    public TimeSpan LeapSecondsFrom(DateTime dateTime)
    {
        return TimeSpan.FromSeconds(LEAP_SECONDS.Count(x => x < dateTime));
    }
    
    public abstract TAIDateTime ToTAI();

    public GPSDateTime ToGPS()
    {
        this.ToTAI().
    }

    public DateTimeBase Add(in TimeSpan timeSpan)
    {
        return new DateTimeBase(DateTime.Add(timeSpan), Kind);
    }
}