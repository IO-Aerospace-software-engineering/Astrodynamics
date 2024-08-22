// using System;
// using System.Linq;
//
// namespace IO.Astrodynamics.TimeSystem;
//
// public static class Time
// {
//     const double TDT_TAI_DELTA = 32.184;
//     const double PREVIOUS_OFFSET = 9.0; //before 1972;
//     const double OFFSET = TDT_TAI_DELTA + PREVIOUS_OFFSET;
//
//
//     private static Time[] LEAP_SECONDS =
//     {
//         new Time(1972, 1, 1),
//         new Time(1972, 7, 1),
//         new Time(1973, 1, 1),
//         new Time(1974, 1, 1),
//         new Time(1975, 1, 1),
//         new Time(1976, 1, 1),
//         new Time(1977, 1, 1),
//         new Time(1978, 1, 1),
//         new Time(1979, 1, 1),
//         new Time(1980, 1, 1),
//         new Time(1981, 7, 1),
//         new Time(1982, 7, 1),
//         new Time(1983, 7, 1),
//         new Time(1985, 7, 1),
//         new Time(1988, 1, 1),
//         new Time(1990, 1, 1),
//         new Time(1991, 1, 1),
//         new Time(1992, 7, 1),
//         new Time(1993, 7, 1),
//         new Time(1994, 7, 1),
//         new Time(1996, 1, 1),
//         new Time(1997, 7, 1),
//         new Time(1999, 1, 1),
//         new Time(2006, 1, 1),
//         new Time(2009, 1, 1),
//         new Time(2012, 7, 1),
//         new Time(2015, 7, 1),
//         new Time(2017, 1, 1),
//     };
//
//     private static IOrderedEnumerable<Time> LeapSeconds = LEAP_SECONDS.OrderBy(x => x);
//
//     // public static readonly Time J2000 = new Time(2000, 01, 01, 12, 0, 0, DateTimeKind.Unspecified);
//
//     
//
//     /// <summary>
//     /// Convert datetime to TDB
//     /// </summary>
//     /// <param name="date"></param>
//     /// <returns></returns>
//     public static Time ToTDB(this Time date)
//     {
//         if (date.Kind == DateTimeKind.Unspecified)
//         {
//             return date;
//         }
//
//         if (date.Kind == DateTimeKind.Local)
//         {
//             date = date.ToUniversalTime();
//         }
//
//         return Time.SpecifyKind(date.AddSeconds(OFFSET + LeapSeconds.Count(x => x < date)),
//             DateTimeKind.Unspecified);
//     }
//
//     /// <summary>
//     /// Convert datetime to UTC
//     /// </summary>
//     /// <param name="date"></param>
//     /// <returns></returns>
//     public static Time ToUTC(this Time date)
//     {
//         if (date.Kind == DateTimeKind.Utc)
//         {
//             return date;
//         }
//
//         var utc = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : date.AddSeconds(-(OFFSET + LeapSeconds.Count(x => x < date)));
//         return Time.SpecifyKind(utc, DateTimeKind.Utc);
//     }
//
//     /// <summary>
//     /// Convert to julian date
//     /// </summary>
//     /// <param name="date"></param>
//     /// <returns></returns>
//     public static double ToJulianDate(this Time date)
//     {
//         return date.ToOADate() + 2415018.5; //julian date at 1899-12-30 00:00:00
//     }
//
//     
//
//     /// <summary>
//     /// Seconds from J2000 relative to TDB
//     /// </summary>
//     /// <param name="date"></param>
//     /// <returns></returns>
//     public static double SecondsFromJ2000TDB(this Time date)
//     {
//         if (date.Kind != DateTimeKind.Unspecified)
//         {
//             date = date.ToTDB();
//         }
//
//         return (date - J2000).TotalSeconds;
//     }
//
//     /// <summary>
//     /// Seconds from J2000 relative to UTC
//     /// </summary>
//     /// <param name="date"></param>
//     /// <returns></returns>
//     public static double SecondsFromJ2000UTC(this Time date)
//     {
//         if (date.Kind != DateTimeKind.Utc)
//         {
//             date = date.ToUTC();
//         }
//
//         return (date - J2000).TotalSeconds;
//     }
//
//     /// <summary>
//     /// Seconds from J2000 relative to Local
//     /// </summary>
//     /// <param name="date"></param>
//     /// <returns></returns>
//     public static double SecondsFromJ2000Local(this Time date)
//     {
//         if (date.Kind != DateTimeKind.Local)
//         {
//             date = date.ToUTC().ToLocalTime();
//         }
//
//         return (date - J2000).TotalSeconds;
//     }
//
//     /// <summary>
//     /// Create TDB from seconds elapsed from J2000
//     /// </summary>
//     /// <param name="secondsFromJ2000"></param>
//     /// <returns></returns>
//     public static Time CreateTDB(double secondsFromJ2000)
//     {
//         return J2000.AddSeconds(secondsFromJ2000).ToTDB();
//     }
//
//     /// <summary>
//     /// Create UTC from seconds elapsed from J2000
//     /// </summary>
//     /// <param name="secondsFromJ2000"></param>
//     /// <returns></returns>
//     public static Time CreateUTC(double secondsFromJ2000)
//     {
//         var date = J2000.AddSeconds(secondsFromJ2000);
//         Time.SpecifyKind(date, DateTimeKind.Utc);
//         return Time.SpecifyKind(date, DateTimeKind.Utc).ToUTC();
//     }
//
//     /// <summary>
//     /// Create TDB from Julian date
//     /// </summary>
//     /// <param name="julianDate"></param>
//     /// <returns></returns>
//     public static Time CreateTDBFromJD(double julianDate)
//     {
//         var sinceEpoch = julianDate - JULIAN_J2000;
//         return new Time(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified).AddDays(sinceEpoch);
//     }
//
//     /// <summary>
//     /// Create UTC from Julian date
//     /// </summary>
//     /// <param name="julianDate"></param>
//     /// <returns></returns>
//     public static Time CreateUTCFromJD(double julianDate)
//     {
//         var sinceEpoch = julianDate - JULIAN_J2000;
//         return new Time(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(sinceEpoch);
//     }
//
//     public static string ToFormattedString(this Time date)
//     {
//         return date.ToString("O") + (date.Kind == DateTimeKind.Unspecified ? " (TDB)" : string.Empty);
//     }
// }