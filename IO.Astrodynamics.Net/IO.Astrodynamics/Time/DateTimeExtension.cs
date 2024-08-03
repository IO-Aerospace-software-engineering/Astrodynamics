using System;
using System.Linq;

namespace IO.Astrodynamics.Time;

public static class DateTimeExtension
{
    const double TDT_TAI_DELTA = 32.184;
    const double PREVIOUS_OFFSET = 9.0; //before 1972;
    const double OFFSET = TDT_TAI_DELTA + PREVIOUS_OFFSET;


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

    private static IOrderedEnumerable<DateTime> LeapSeconds = LEAP_SECONDS.OrderBy(x => x);

    public static readonly DateTime J2000 = new DateTime(2000, 01, 01, 12, 0, 0, DateTimeKind.Unspecified);

    public const double JULIAN_J1950 = 2433282.5;
    public const double JULIAN_J2000 = 2451545.0;
    public const double JULIAN_YEAR = 365.25;
    public const double SECONDS_PER_DAY = 86400.0;

    /// <summary>
    /// Convert datetime to TDB
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DateTime ToTDB(this DateTime date)
    {
        if (date.Kind == DateTimeKind.Unspecified)
        {
            return date;
        }

        if (date.Kind == DateTimeKind.Local)
        {
            date = date.ToUniversalTime();
        }

        return DateTime.SpecifyKind(date.AddSeconds(OFFSET + LeapSeconds.Count(x => x < date)),
            DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Convert datetime to UTC
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DateTime ToUTC(this DateTime date)
    {
        if (date.Kind == DateTimeKind.Utc)
        {
            return date;
        }

        var utc= date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : date.AddSeconds(-(OFFSET + LeapSeconds.Count(x => x < date)));
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    /// <summary>
    /// Convert to julian date
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static double ToJulianDate(this DateTime date)
    {
        return date.ToOADate () + 2415018.5;//julian date at 1899-12-30 00:00:00
    }

    /// <summary>
    /// Seconds from J2000 relative to TDB
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static double SecondsFromJ2000TDB(this DateTime date)
    {
        if (date.Kind != DateTimeKind.Unspecified)
        {
            date = date.ToTDB();
        }

        return (date - J2000).TotalSeconds;
    }
    
    /// <summary>
    /// Seconds from J2000 relative to UTC
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static double SecondsFromJ2000UTC(this DateTime date)
    {
        if (date.Kind != DateTimeKind.Utc)
        {
            date = date.ToUTC();
        }

        return (date - J2000).TotalSeconds;
    }
    
    /// <summary>
    /// Seconds from J2000 relative to Local
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static double SecondsFromJ2000Local(this DateTime date)
    {
        if (date.Kind != DateTimeKind.Local)
        {
            date = date.ToUTC().ToLocalTime();
        }

        return (date - J2000).TotalSeconds;
    }

    /// <summary>
    /// Create TDB from seconds elapsed from J2000
    /// </summary>
    /// <param name="secondsFromJ2000"></param>
    /// <returns></returns>
    public static DateTime CreateTDB(double secondsFromJ2000)
    {
        return J2000.AddSeconds(secondsFromJ2000).ToTDB();
    }

    /// <summary>
    /// Create UTC from seconds elapsed from J2000
    /// </summary>
    /// <param name="secondsFromJ2000"></param>
    /// <returns></returns>
    public static DateTime CreateUTC(double secondsFromJ2000)
    {
        var date = J2000.AddSeconds(secondsFromJ2000);
        DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return DateTime.SpecifyKind(date, DateTimeKind.Utc).ToUTC();
    }

    /// <summary>
    /// Create TDB from Julian date
    /// </summary>
    /// <param name="julianDate"></param>
    /// <returns></returns>
    public static DateTime CreateTDBFromJD(double julianDate)
    {
        var sinceEpoch = julianDate - JULIAN_J2000;
        return new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified).AddDays(sinceEpoch);
    }

    /// <summary>
    /// Create UTC from Julian date
    /// </summary>
    /// <param name="julianDate"></param>
    /// <returns></returns>
    public static DateTime CreateUTCFromJD(double julianDate)
    {
        var sinceEpoch = julianDate - JULIAN_J2000;
        return new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(sinceEpoch);
    }

    public static string ToFormattedString(this DateTime date)
    {
        return date.ToString("O") + (date.Kind == DateTimeKind.Unspecified ? " (TDB)" : string.Empty);
    }
}