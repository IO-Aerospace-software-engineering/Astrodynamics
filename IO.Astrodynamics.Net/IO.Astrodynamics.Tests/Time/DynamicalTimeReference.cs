// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The seconds of TT and of TDB between two tagged epochs, independent of the framework's conversions: the leap seconds
/// of the IERS (TAI − UTC, 35 s from 2012-07-01, 36 s from 2015-07-01, 37 s from 2017-01-01, as in
/// <c>latest_leapseconds.tls</c>), TT = TAI + 32.184 s, GPS = TAI − 19 s, and TDB − TT by the formula of the SPICE leap
/// seconds kernel, K sin(E), E = M + EB sin M, M = M0 + M1·t (<c>DELTET/K</c>, <c>EB</c>, <c>M</c>), not by the
/// framework's series. The difference of the two instants is a difference of ticks held as a long plus a correction in
/// double, so it is not quantized by an absolute epoch in double (1.2e-7 s near 2025).
/// </summary>
/// <remarks>
/// The framework's TDB − TT series is within 1.2423 µs of the leap seconds kernel's formula from 1972 to 2100 (measured
/// on 935 041 instants, design of #501, §9), so a difference of two instants through a TDB tag can differ from this
/// reference by 2.49 µs at most; a difference between UTC, TAI, TT and GPS tags is exact in both.
/// </remarks>
internal static class DynamicalTimeReference
{
    private const double K = 1.657e-3;
    private const double Eb = 1.671e-2;
    private const double M0 = 6.239996;
    private const double M1 = 1.99096871e-7;
    private static readonly long J2000Ticks = TimeSystem.Time.J2000.Ticks;

    /// <summary>The bound of a difference through a TDB tag: twice the largest departure of the two series.</summary>
    internal const double SeriesBound = 2.49e-6;

    /// <summary>
    /// The bound of a difference between two TDB tags <paramref name="seconds"/> apart, which carries the change of the
    /// departure of the two series over the interval only, not the departure itself.
    /// </summary>
    /// <remarks>
    /// Both series are A sin(M + e sin M) of the mean anomaly of the Earth, with A, e and the rate of M a few 1e-4
    /// apart, so their departure is, to 1e-10 s, a trigonometric polynomial of the annual and semi-annual frequencies.
    /// By Bernstein's inequality its rate is at most the semi-annual angular frequency (2 M1) times its largest value
    /// (1.2423 µs, half of <see cref="SeriesBound"/>): 4.95e-13 s per second, 1.78e-9 s over an hour. The largest rate
    /// sampled from 1972 to 2100 is 2.0e-13 s per second (computed, 2026-11-01).
    /// </remarks>
    internal static double SeriesChangeBound(double seconds) =>
        2.0 * M1 * (SeriesBound / 2.0) * System.Math.Abs(seconds);

    /// <summary>The TT seconds from <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal static double TtSecondsBetween(TimeSystem.Time from, TimeSystem.Time to)
    {
        var (fromTicks, fromCorrection) = TtParts(from);
        var (toTicks, toCorrection) = TtParts(to);
        return (toTicks - fromTicks) / (double)TimeSpan.TicksPerSecond + (toCorrection - fromCorrection);
    }

    /// <summary>The TDB seconds from <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal static double TdbSecondsBetween(TimeSystem.Time from, TimeSystem.Time to)
    {
        var (fromTicks, fromCorrection) = TdbParts(from);
        var (toTicks, toCorrection) = TdbParts(to);
        return (toTicks - fromTicks) / (double)TimeSpan.TicksPerSecond + (toCorrection - fromCorrection);
    }

    /// <summary>TAI − UTC at a UTC calendar instant, from 2012-07-01.</summary>
    internal static int TaiMinusUtc(DateTime utc)
    {
        if (utc < new DateTime(2012, 7, 1))
            throw new ArgumentOutOfRangeException(nameof(utc), utc, "The reference holds the leap seconds from 2012-07-01.");

        if (utc < new DateTime(2015, 7, 1))
            return 35;

        return utc < new DateTime(2017, 1, 1) ? 36 : 37;
    }

    // TT as ticks of a TT calendar plus a correction in seconds
    private static (long Ticks, double Correction) TtParts(TimeSystem.Time time)
    {
        long ticks = time.DateTime.Ticks;
        switch (time.Frame)
        {
            case TDTTimeFrame:
                return (ticks, 0.0);
            case TAITimeFrame:
                return (ticks + Seconds(32.184), 0.0);
            case GPSTimeFrame:
                return (ticks + Seconds(19.0 + 32.184), 0.0);
            case UTCTimeFrame:
                return (ticks + Seconds(TaiMinusUtc(time.DateTime) + 32.184), 0.0);
            case LocalTimeFrame:
                // The UTC of the local time, by the time zone of the system
                var utc = DateTime.SpecifyKind(time.DateTime, DateTimeKind.Local).ToUniversalTime();
                return (utc.Ticks + Seconds(TaiMinusUtc(utc) + 32.184), 0.0);
            case TDBTimeFrame:
                return (ticks, -TdbMinusTtAtTdb((ticks - J2000Ticks) / (double)TimeSpan.TicksPerSecond));
            default:
                throw new ArgumentException($"No reference for the frame {time.Frame}.", nameof(time));
        }
    }

    // TDB as ticks of a TDB calendar plus a correction in seconds
    private static (long Ticks, double Correction) TdbParts(TimeSystem.Time time)
    {
        if (time.Frame is TDBTimeFrame)
            return (time.DateTime.Ticks, 0.0);

        var (ticks, correction) = TtParts(time);
        double tt = (ticks - J2000Ticks) / (double)TimeSpan.TicksPerSecond + correction;
        return (ticks, correction + TdbMinusTtAtTt(tt));
    }

    private static long Seconds(double seconds) => (long)System.Math.Round(seconds * TimeSpan.TicksPerSecond);

    // TDB − TT at a TDB of t seconds past J2000: the formula of the kernel, whose argument is TDB
    private static double TdbMinusTtAtTdb(double t)
    {
        double m = M0 + M1 * t;
        return K * System.Math.Sin(m + Eb * System.Math.Sin(m));
    }

    // TDB − TT at a TT of t seconds past J2000: the fixed point of the same formula, which converges by the rate of
    // TDB − TT (3.4e-10) at each iteration
    private static double TdbMinusTtAtTt(double t)
    {
        double offset = 0.0;
        for (int i = 0; i < 3; i++)
            offset = TdbMinusTtAtTdb(t + offset);

        return offset;
    }
}
