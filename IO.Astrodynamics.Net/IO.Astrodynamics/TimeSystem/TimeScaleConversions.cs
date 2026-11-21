using System;
using System.Linq;

namespace IO.Astrodynamics.TimeSystem;

/// <summary>
/// The conversions between the <see cref="TimeScale"/>s, through TAI, tick for tick those of the <c>TimeFrame</c> classes
/// of 10.x they replace (D3 of #668): TAI − UTC from the leap second table, TT = TAI + 32.184 s, GPS = TAI − 19 s and
/// TDB − TT by the one-term series of <see cref="TdbMinusTt"/>, rounded to the nearest tick (#625).
/// </summary>
internal static class TimeScaleConversions
{
    /// <summary>
    /// TAI − UTC before 1972, 9 s.
    /// </summary>
    private const double PreviousOffset = 9.0;

    /// <summary>
    /// The first UTC instant of each offset TAI − UTC from 1972, 10 s on 1972-01-01 to 37 s from 2017-01-01.
    /// </summary>
    internal static readonly DateTime[] LeapSeconds =
    [
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
        new DateTime(2017, 1, 1)
    ];

    /// <summary>
    /// TT − TAI, 32.184 s, in ticks.
    /// </summary>
    internal static readonly long TtMinusTaiTicks = TimeSpan.FromSeconds(32.184).Ticks;

    /// <summary>
    /// TAI − GPS, 19 s, in ticks.
    /// </summary>
    internal static readonly long TaiMinusGpsTicks = TimeSpan.FromSeconds(19.0).Ticks;

    // The terms of the series of TDB − TT
    private const double T1 = 357.528;
    private const double T2 = 35999.05;
    private const double T3 = 0.0167;

    /// <summary>
    /// TAI − UTC at the given UTC instant.
    /// </summary>
    /// <remarks>
    /// Each entry of the leap second table is the first UTC instant of the new offset (00:00:00 UTC), so it counts from
    /// that instant onward: 2017-01-01T00:00:00 UTC already has TAI − UTC = 37 s.
    /// </remarks>
    internal static TimeSpan LeapSecondsAtUtc(DateTime utc)
    {
        return TimeSpan.FromSeconds(PreviousOffset + LeapSeconds.Count(x => x <= utc));
    }

    /// <summary>
    /// TAI − UTC at the given TAI instant.
    /// </summary>
    /// <remarks>
    /// The leap second inserted before a table entry D starts at the TAI instant D + (previous offset): from there on the
    /// new offset applies. During the inserted second, UTC 23:59:60, which <see cref="DateTime"/> cannot represent, reads
    /// as 23:59:59 a second time; D 00:00:00 UTC is reached one second later, as it should be.
    /// </remarks>
    internal static TimeSpan LeapSecondsAtTai(DateTime tai)
    {
        int count = 0;
        for (int i = 0; i < LeapSeconds.Length; i++)
        {
            if (tai >= LeapSeconds[i].AddSeconds(PreviousOffset + i))
            {
                count++;
            }
        }

        return TimeSpan.FromSeconds(PreviousOffset + count);
    }

    /// <summary>
    /// TDB − TT in seconds by the series 0.001658 sin(g + 0.0167 sin g), in double: the conversions of <see cref="Time"/>
    /// round it to the nearest 100 ns tick (<see cref="Time.SecondsToTicks"/>, #625; they truncated it toward zero
    /// before), the time argument of the propagation (#501) does not.
    /// </summary>
    /// <param name="centuries">Julian centuries from J2000 at which g is evaluated: those of the TDB time converted to TAI,
    /// those of the TT time converted to TDB.</param>
    internal static double TdbMinusTt(double centuries)
    {
        var g = Constants.TwoPi * (T1 + T2 * centuries) / 360.0;
        return 0.001658 * System.Math.Sin(g + T3 * System.Math.Sin(g));
    }

    /// <summary>
    /// The ticks of <paramref name="time"/> converted to TAI, in integer arithmetic: the key of the order, the equality and
    /// the hash code of times (#512).
    /// </summary>
    /// <remarks>
    /// The TAI ticks of a TDB time are its ticks less TT − TAI and less TDB − TT at its own centuries, rounded to the tick.
    /// Not one to one: where the rounded TDB − TT gains a tick, two consecutive TDB ticks give one TAI tick (about every
    /// 300 s near its steepest), and where it loses one a TAI tick has no TDB tick; since TDB − TT changes by less than
    /// 3.4e-10 s per second, two TDB ticks two or more apart never give one TAI tick nor change their order (#512).
    /// </remarks>
    internal static long TaiTicks(in Time time)
    {
        long ticks = time.DateTime.Ticks;
        return time.Scale switch
        {
            TimeScale.TAI => ticks,
            TimeScale.UTC => ticks + LeapSecondsAtUtc(time.DateTime).Ticks,
            TimeScale.TT => ticks - TtMinusTaiTicks,
            TimeScale.GPS => ticks + TaiMinusGpsTicks,
            TimeScale.TDB => ticks - TtMinusTaiTicks - Time.SecondsToTicks(TdbMinusTt(time.Centuries())),
            _ => throw new ArgumentOutOfRangeException(nameof(time), time.Scale, "Unknown time scale.")
        };
    }

    /// <summary>
    /// The calendar, in <paramref name="scale"/>, of the TAI calendar <paramref name="tai"/>.
    /// </summary>
    internal static DateTime FromTai(DateTime tai, TimeScale scale)
    {
        switch (scale)
        {
            case TimeScale.TAI:
                return tai;
            case TimeScale.UTC:
                return DateTime.SpecifyKind(tai - LeapSecondsAtTai(tai), DateTimeKind.Utc);
            case TimeScale.TT:
                return tai.AddTicks(TtMinusTaiTicks);
            case TimeScale.GPS:
                return tai.AddTicks(-TaiMinusGpsTicks);
            case TimeScale.TDB:
                var tt = new Time(tai.AddTicks(TtMinusTaiTicks), TimeScale.TT);
                return tt.DateTime.AddTicks(Time.SecondsToTicks(TdbMinusTt(tt.Centuries())));
            default:
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unknown time scale.");
        }
    }

    /// <summary>
    /// The tag a time string writes after the calendar of <paramref name="scale"/>: " TDB", " TT", " TAI", " GPS", or none
    /// for UTC, whose calendar ends in Z.
    /// </summary>
    internal static string Suffix(TimeScale scale) => scale switch
    {
        TimeScale.TDB => " TDB",
        TimeScale.TT => " TT",
        TimeScale.TAI => " TAI",
        TimeScale.GPS => " GPS",
        _ => string.Empty
    };
}
