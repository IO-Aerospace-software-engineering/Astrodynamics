// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The TDB − TT series of <see cref="TDBTimeFrame"/>, shared by its conversions and the time argument of the
/// propagation (#501): <see cref="TDBTimeFrame.ConvertToTAI"/> and <see cref="TDBTimeFrame.ConvertFromTAI"/> give, tick
/// for tick, what the expressions they held before gave, on the TT instants every 0.05 day from 1972 to 2100 (935 041)
/// and on 2 000 000 random ticks of the same span. The reference is the former code of the two conversions, copied
/// below: it guards that the conversions are unchanged, not that they are correct.
/// </summary>
public class TdbSeriesTests
{
    private static readonly long Start = new DateTime(1972, 1, 1).Ticks;
    private static readonly long End = new DateTime(2100, 1, 1).Ticks;

    [Fact]
    public void Grid()
    {
        // Arrange: the grid of the TT instants every 0.05 day
        long step = 4320L * TimeSpan.TicksPerSecond;
        int count = 0;

        // Act and assert: each tick count read as TDB and as TAI
        for (long ticks = Start; ticks <= End; ticks += step)
        {
            AssertUnchanged(ticks);
            count++;
        }

        Assert.Equal(935041, count);
    }

    [Fact]
    public void RandomTicks()
    {
        // Arrange: the random ticks of the verification of #510
        var random = new Random(501);

        // Act and assert
        for (int i = 0; i < 2_000_000; i++)
        {
            AssertUnchanged(Start + (long)(random.NextDouble() * (End - Start)));
        }
    }

    [Fact]
    public void TdbMinusTt()
    {
        // The series at J2000 and at a quarter of the anomalistic year, against its closed form
        double g = IO.Astrodynamics.Constants._2PI * 357.528 / 360.0;
        Assert.Equal(0.001658 * System.Math.Sin(g + 0.0167 * System.Math.Sin(g)), TDBTimeFrame.TdbMinusTt(0.0));
        Assert.Equal(0.0, TDBTimeFrame.TdbMinusTt((360.0 - 357.528) / 35999.05), 1e-15);
    }

    private static void AssertUnchanged(long ticks)
    {
        var tdb = new TimeSystem.Time(new DateTime(ticks), TimeFrame.TDBFrame);
        var tai = new TimeSystem.Time(new DateTime(ticks), TimeFrame.TAIFrame);
        long toTai = TimeFrame.TDBFrame.ConvertToTAI(tdb).DateTime.Ticks;
        long fromTai = TimeFrame.TDBFrame.ConvertFromTAI(tai).DateTime.Ticks;
        if (toTai != FormerConvertToTai(tdb) || fromTai != FormerConvertFromTai(tai))
        {
            Assert.Fail($"{ticks}: to TAI {toTai} against {FormerConvertToTai(tdb)}, from TAI {fromTai} against " +
                        $"{FormerConvertFromTai(tai)}");
        }
    }

    // TDBTimeFrame.ConvertToTAI before #501: g at the TDB centuries
    private static long FormerConvertToTai(TimeSystem.Time time)
    {
        var tempTAI = time.Add(TimeSpan.FromSeconds(-32.184));
        var g = G(time.Centuries());
        return tempTAI.DateTime.Add(TimeSpan.FromSeconds(0.001658 * System.Math.Sin(g + 0.0167 * System.Math.Sin(g))).Negate())
            .Ticks;
    }

    // TDBTimeFrame.ConvertFromTAI before #501: g at the TT centuries
    private static long FormerConvertFromTai(TimeSystem.Time time)
    {
        var tdt = time.ToTDT();
        var g = G(tdt.Centuries());
        return tdt.DateTime.Add(TimeSpan.FromSeconds(0.001658 * System.Math.Sin(g + 0.0167 * System.Math.Sin(g)))).Ticks;
    }

    private static double G(double t) => IO.Astrodynamics.Constants._2PI * (357.528 + 35999.05 * t) / 360.0;
}
