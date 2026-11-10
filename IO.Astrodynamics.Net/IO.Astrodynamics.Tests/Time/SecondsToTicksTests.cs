using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The conversions of seconds to ticks of <see cref="TimeSystem.Time"/> give the nearest tick, ties to even (#625):
/// <see cref="TimeSystem.Time.Create(double, ITimeFrame)"/>, <see cref="TimeSystem.Time.Create(int, int, double)"/>,
/// <see cref="TimeSystem.Time.AddSeconds"/>, <see cref="TimeSystem.Time.AddMilliseconds"/>,
/// <see cref="TimeSystem.Time.AddMicroseconds"/> and the TDB − TT offset of <see cref="TDBTimeFrame"/> (not
/// <c>AddDays</c>, <c>AddHours</c>, <c>AddMinutes</c>, <c>CreateFromJD</c> nor the <c>TimeSpan.FromSeconds</c> of the
/// library outside <see cref="TimeSystem.Time"/>, which still truncate); a tick made seconds and back, which is not
/// always that tick from 2017 to 2028 (and 1971 to 1983); and the labels and window ends of the CSPICE reads, which keep the ticks asked.
/// The references
/// are exact: Python's Fraction (<c>tools/seconds_to_ticks/seconds_to_ticks_reference.py</c>,
/// <c>Data/SecondsToTicksReference/seconds_to_ticks_reference.json</c>), integer arithmetic (<see cref="ExactTicks"/>),
/// and spiceypy 8.3.0 (CSPICE N0067) on the LRO kernel of <c>Data/LRO</c>.
/// </summary>
public class SecondsToTicksTests
{
    private const string ReferencePath = "Data/SecondsToTicksReference/seconds_to_ticks_reference.json";
    private const string LroKernel = "Data/LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp";
    private readonly ITestOutputHelper _output;

    public SecondsToTicksTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        SpiceAPI.Instance.LoadKernels(new FileInfo(LroKernel));
    }

    private static JsonElement Reference() => JsonDocument.Parse(File.ReadAllText(ReferencePath)).RootElement;

    private static double Parse(JsonElement element) => double.Parse(element.GetString(), CultureInfo.InvariantCulture);

    private static long FromJ2000(TimeSystem.Time time) => time.DateTime.Ticks - TimeSystem.Time.J2000.Ticks;

    [Fact]
    public void EveryConversion_IsTheExactNearestTick()
    {
        int count = 0, moved = 0;
        foreach (var row in Reference().GetProperty("values").EnumerateArray())
        {
            double seconds = Parse(row.GetProperty("s"));
            long ticks = row.GetProperty("ticks").GetInt64();
            string label = $"{row.GetProperty("kind").GetString()} {seconds:R} s";
            Assert.True(ticks == TimeSystem.Time.SecondsToTicks(seconds), label);
            Assert.True(ticks == ExactTicks.Nearest(seconds), label);
            Assert.True(ticks == FromJ2000(TimeSystem.Time.CreateTDB(seconds)), label);
            Assert.True(ticks == FromJ2000(TimeSystem.Time.CreateUTC(seconds)), label);
            Assert.True(ticks == FromJ2000(TimeSystem.Time.Create(seconds, TimeFrame.TAIFrame)), label);
            Assert.True(ticks == FromJ2000(TimeSystem.Time.J2000TDB.AddSeconds(seconds)), label);
            Assert.True(row.GetProperty("ms_ticks").GetInt64() == FromJ2000(TimeSystem.Time.J2000TDB.AddMilliseconds(seconds)), label);
            Assert.True(row.GetProperty("us_ticks").GetInt64() == FromJ2000(TimeSystem.Time.J2000TDB.AddMicroseconds(seconds)), label);
            if (ticks != row.GetProperty("truncated").GetInt64())
                moved++;
            count++;
        }

        // 968 of the 1978 values are not their truncation, the former result
        _output.WriteLine($"{count} values, {moved} whose nearest tick is not their truncation");
        Assert.Equal(1978, count);
        Assert.Equal(968, moved);
    }

    [Fact]
    public void ANegativeValueAboveMinusOneUnit_IsTheMirrorOfItsMagnitude()
    {
        // Above -1 unit the fraction 1 - |value| of a floor is not exact in double, so a near tie can fall on the wrong
        // side (the review of #625): -2.5e-7 s is -2.49999999999999995 ticks, -0.15 us -1.4999999999999999 ticks,
        // -0.00025 ms -2.5000000000000001 ticks (Fraction)
        Assert.Equal(-2L, TimeSystem.Time.SecondsToTicks(-2.5e-7));
        Assert.Equal(2L, TimeSystem.Time.SecondsToTicks(2.5e-7));
        Assert.Equal(0L, TimeSystem.Time.SecondsToTicks(-5e-8));
        Assert.Equal(-1L, FromJ2000(TimeSystem.Time.J2000TDB.AddMicroseconds(-0.15)));
        Assert.Equal(-3L, FromJ2000(TimeSystem.Time.J2000TDB.AddMilliseconds(-0.00025)));

        // So adding a span and its opposite gives the time back: -(k + 1/2) 1e-7 s, k < 5000, missed it for 2499 of them before
        var random = new Random(6251);
        var start = TimeSystem.Time.CreateTDB(7.6e8 + 0.1234567);
        for (int k = 0; k < 5000; k++)
        {
            double seconds = double.Parse($"{k}.5e-7", CultureInfo.InvariantCulture);
            Assert.Equal(-TimeSystem.Time.SecondsToTicks(seconds), TimeSystem.Time.SecondsToTicks(-seconds));
            Assert.Equal(start, start.AddSeconds(seconds).AddSeconds(-seconds));
            Assert.Equal(start, start.AddSeconds(-seconds).AddSeconds(seconds));
            double any = (random.NextDouble() - 0.5) * System.Math.Pow(10.0, random.Next(-9, 6));
            Assert.Equal(-TimeSystem.Time.SecondsToTicks(any), TimeSystem.Time.SecondsToTicks(-any));
            Assert.Equal(ExactTicks.Nearest(any), TimeSystem.Time.SecondsToTicks(any));
        }
    }

    [Fact]
    public void TheValuesOfTheIssue()
    {
        Assert.Equal(1234568L, TimeSystem.Time.SecondsToTicks(0.12345678999));
        Assert.Equal(-1234568L, TimeSystem.Time.SecondsToTicks(-0.12345678999));
        Assert.Equal(7558704691834258L, TimeSystem.Time.SecondsToTicks(755870469.1834258));

        // An exact tie, 39062.5 ticks, to the even tick; 3 x 2^-8 s is 117187.5 ticks
        Assert.Equal(39062L, TimeSystem.Time.SecondsToTicks(0.00390625));
        Assert.Equal(117188L, TimeSystem.Time.SecondsToTicks(0.01171875));
        Assert.Equal(-117188L, TimeSystem.Time.SecondsToTicks(-0.01171875));

        // 2023 day 349 at 69.1834258 s: 691834257.99999995 ticks into the day (Fraction)
        var time = TimeSystem.Time.Create(2023, 349, 69.1834258);
        Assert.Equal(TimeFrame.UTCFrame, time.Frame);
        Assert.Equal(new DateTime(2023, 12, 15).Ticks + 691834258L, time.DateTime.Ticks);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e12)]
    [InlineData(-1e300)]
    public void ASpanTicksCannotHold_Throws(double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.SecondsToTicks(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.CreateTDB(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.J2000TDB.AddSeconds(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.J2000TDB.AddMilliseconds(seconds * 1e4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.J2000TDB.AddMicroseconds(seconds * 1e7));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.Create(2000, 1, seconds));
    }

    [Fact]
    public void AnEpochPastTheRangeOfDateTime_Throws()
    {
        // 3e11 s from J2000 is past 9999-12-31: DateTime refuses it, as AddSeconds did
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.CreateTDB(3e11));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.CreateTDB(-1e11));
    }

    [Fact]
    public void AnEtFrom2017To2028_ComesBackExactly()
    {
        // From 2^29 s (2017-01-05) to 2^53 ticks (2028-07-17) an ulp of the ET is larger than a tick, so the nearest tick
        // gives back the same double; the truncation gave back an earlier one for 40.6 % of the ET (#625)
        const double from = 536870912.0;
        const double to = 900719925.4740992;
        var random = new Random(625);
        for (int i = 0; i < 200_000; i++)
        {
            double et = from + random.NextDouble() * (to - from);
            double back = TimeSystem.Time.CreateTDB(et).TimeSpanFromJ2000().TotalSeconds;
            if (back != et)
                Assert.Fail($"{et:R} s came back as {back:R} s");
        }
    }

    [Fact]
    public void ATickMadeSeconds_ComesBackExactlyWithin2Pow29SecondsOfJ2000_AndWithinOneTickBeyond()
    {
        // The reverse of the round trip above is not the identity where an ulp of the seconds is coarser than a tick,
        // |ET| from 2^29 s to 2^53 ticks (1971-06-17 to 1982-12-27 and 2017-01-05 to 2028-07-17): the double nearest a
        // tick can be up to 0.596 tick from it, and its nearest tick is then the neighbour (the review of #625). Fraction:
        // 5652918976297924 ticks -> 565291897.6297925 s -> ...925
        Assert.Equal(565291897.6297925, TimeSpan.FromTicks(5652918976297924).TotalSeconds);
        Assert.Equal(5652918976297925L, FromJ2000(TimeSystem.Time.CreateTDB(565291897.6297925)));

        // Every tick comes back within one tick of itself, through the double nearest it (ExactTicks, exact); within
        // 2^29 s of J2000 (1982-12-27 to 2017-01-05) an ulp is finer than a tick, so the double is within half a tick and
        // the tick comes back exactly
        const long limit = 9_007_199_254_740_992L;
        const long inner = 5_368_709_120_000_000L;
        var random = new Random(6252);
        var moved = new int[3];
        var ranges = new[] { (-limit, -inner, false), (-inner, inner, true), (inner, limit, false) };
        for (int r = 0; r < ranges.Length; r++)
        {
            var (from, to, exact) = ranges[r];
            for (int i = 0; i < 100_000; i++)
            {
                long ticks = from + (long)(random.NextDouble() * (to - from));
                double seconds = TimeSpan.FromTicks(ticks).TotalSeconds;
                long back = FromJ2000(TimeSystem.Time.CreateTDB(seconds));
                Assert.Equal(ExactTicks.Nearest(seconds), back);
                Assert.InRange(back - ticks, exact ? 0L : -1L, exact ? 0L : 1L);
                if (back != ticks)
                    moved[r]++;
            }
        }

        // About 16 % of the ticks beyond 2^29 s (Fraction, 100 000 uniform ticks from 2017 to 2028 in the review: 16 072),
        // none within
        _output.WriteLine($"Ticks of 100 000 that come back as a neighbour: 1971-1983 {moved[0]}, 1983-2017 {moved[1]}, 2017-2028 {moved[2]}");
        Assert.Equal(0, moved[1]);
    }

    [Fact]
    public void AStateReadAtATick_IsLabelledWithThatTick()
    {
        // SpiceAPI.ReadEphemeris(Time ...) labelled its state with the epoch CSPICE returned, made a Time again: the
        // neighbouring tick for about 16 % of the ticks of 2024 (the review of #625; Fraction: 2024-12-18T09:36:47.8294540
        // came back .8294539). It is labelled with the epoch asked, the instant SPICE read at, as the data provider does
        var epochs = new System.Collections.Generic.List<TimeSystem.Time>
            { new(new DateTime(2024, 12, 18, 9, 36, 47).AddTicks(8294540), TimeFrame.TDBFrame) };
        var random = new Random(6253);
        long start = new DateTime(2024, 1, 1).Ticks;
        long span = new DateTime(2025, 1, 1).Ticks - start;
        for (int i = 0; i < 2000; i++)
            epochs.Add(new TimeSystem.Time(new DateTime(start + (long)(random.NextDouble() * span)), TimeFrame.TDBFrame));

        foreach (var epoch in epochs)
        {
            var state = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.None);
            Assert.Equal(epoch.DateTime.Ticks, state.Epoch.DateTime.Ticks);
            Assert.Equal(TimeFrame.TDBFrame, state.Epoch.Frame);
        }

        // An epoch tagged UTC is labelled with its TDB instant, as before
        var utc = new TimeSystem.Time(new DateTime(2024, 6, 1, 12, 0, 0), TimeFrame.UTCFrame);
        Assert.Equal(utc.ToTDB().DateTime.Ticks,
            SpiceAPI.Instance.ReadEphemeris(utc, 399, 301, Frames.Frame.ICRF, Aberration.None).Epoch.DateTime.Ticks);
    }

    [Fact]
    public void AKernelBackedSpacecraft_IsAtTheEpochAsked()
    {
        // Its initial state was labelled with the epoch CSPICE returned (Fraction: 2024-01-11T00:35:02.8486908 came back
        // .8486907, the effect the issue names): it is the epoch asked, as SpiceDataProvider.GetEphemeris labels it
        var epochs = new System.Collections.Generic.List<TimeSystem.Time>
            { new(new DateTime(2024, 1, 11, 0, 35, 2).AddTicks(8486908), TimeFrame.TDBFrame) };
        var random = new Random(6254);
        long start = new DateTime(2023, 12, 16).Ticks;
        long span = new DateTime(2024, 3, 14).Ticks - start;
        for (int i = 0; i < 300; i++)
            epochs.Add(new TimeSystem.Time(new DateTime(start + (long)(random.NextDouble() * span)), TimeFrame.TDBFrame));

        foreach (var epoch in epochs)
        {
            var lro = new Spacecraft(-85, "LRO", epoch);
            Assert.Equal(epoch.DateTime.Ticks, lro.InitialOrbitalParameters.Epoch.DateTime.Ticks);
            Assert.Equal(TimeFrame.TDBFrame, lro.InitialOrbitalParameters.Epoch.Frame);
        }
    }

    [Fact]
    public void ACspiceWindow_StaysInsideItsSearchWindow()
    {
        // The Moon is always farther than 1 km from the Earth, so gfdist gives back the search window, its ends as the
        // doubles of their ticks: made a Time again, an end was the neighbouring tick for about 16 % of the ticks of 2024,
        // one tick outside the search window (the review of #625; Fraction: the end 2024-02-29T03:34:37.2052700 came back
        // .2052701). The ends are clamped to the search window: the window is the search window, tick for tick
        var searches = new System.Collections.Generic.List<Window>
        {
            new(new TimeSystem.Time(new DateTime(2024, 2, 28, 3, 34, 37).AddTicks(2052700), TimeFrame.TDBFrame),
                new TimeSystem.Time(new DateTime(2024, 2, 29, 3, 34, 37).AddTicks(2052700), TimeFrame.TDBFrame))
        };
        var random = new Random(6255);
        long start = new DateTime(2024, 1, 1).Ticks;
        long span = new DateTime(2024, 12, 1).Ticks - start;
        for (int i = 0; i < 200; i++)
        {
            long from = start + (long)(random.NextDouble() * span);
            long length = TimeSpan.TicksPerHour + (long)(random.NextDouble() * 10 * TimeSpan.TicksPerDay);
            searches.Add(new Window(new TimeSystem.Time(new DateTime(from), TimeFrame.TDBFrame),
                new TimeSystem.Time(new DateTime(from + length), TimeFrame.TDBFrame)));
        }

        foreach (var search in searches)
        {
            var windows = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(search, 399, 301, RelationnalOperator.Greater, 1000.0,
                Aberration.None, TimeSpan.FromHours(6.0)).ToArray();
            Assert.Single(windows);
            Assert.Equal(search.StartDate.DateTime.Ticks, windows[0].StartDate.DateTime.Ticks);
            Assert.Equal(search.EndDate.DateTime.Ticks, windows[0].EndDate.DateTime.Ticks);
        }

        // A search window tagged UTC: the window is its TDB instants
        var utc = new Window(new TimeSystem.Time(new DateTime(2024, 6, 1), TimeFrame.UTCFrame),
            new TimeSystem.Time(new DateTime(2024, 6, 2), TimeFrame.UTCFrame));
        var inTdb = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(utc, 399, 301, RelationnalOperator.Greater, 1000.0,
            Aberration.None, TimeSpan.FromHours(6.0)).Single();
        Assert.Equal(utc.StartDate.ToTDB().DateTime.Ticks, inTdb.StartDate.DateTime.Ticks);
        Assert.Equal(utc.EndDate.ToTDB().DateTime.Ticks, inTdb.EndDate.DateTime.Ticks);
    }

    [Fact]
    public void TheTdbOffset_IsTheNearestTickOfTheSeries()
    {
        // The offsets 32.184 s and 19 s are whole ticks; TDB − TT is not, and both conversions take its nearest tick
        Assert.Equal(321840000L, TimeSpan.FromSeconds(32.184).Ticks);
        Assert.Equal(190000000L, TimeSpan.FromSeconds(19.0).Ticks);
        var random = new Random(6250);
        long start = new DateTime(1972, 1, 1).Ticks;
        long span = new DateTime(2100, 1, 1).Ticks - start;
        for (int i = 0; i < 100_000; i++)
        {
            long ticks = start + (long)(random.NextDouble() * span);
            var tdb = new TimeSystem.Time(new DateTime(ticks), TimeFrame.TDBFrame);
            var tai = new TimeSystem.Time(new DateTime(ticks), TimeFrame.TAIFrame);
            long toTai = ticks - 321840000L - ExactTicks.Nearest(TDBTimeFrame.TdbMinusTt(tdb.Centuries()));
            var tt = tai.ToTDT();
            long fromTai = tt.DateTime.Ticks + ExactTicks.Nearest(TDBTimeFrame.TdbMinusTt(tt.Centuries()));
            Assert.Equal(toTai, tdb.ToTAI().DateTime.Ticks);
            Assert.Equal(fromTai, tai.ToTDB().DateTime.Ticks);
        }
    }

    [Fact]
    public void ALroBuiltAtTheStartOfItsSpk_IsSpiceState()
    {
        // The start of the SPK by spkcov: a = 755870469.18342578 s exactly, 7558704691834257.84 ticks. CreateTDB(a)
        // truncated it to ...257, 84 ns before the coverage, and the read threw SPKINSUFFDATA (#625)
        var reference = Reference();
        foreach (var end in new[] { "start", "end" })
        {
            var expected = reference.GetProperty("lro_states").GetProperty(end);
            double et = Parse(expected.GetProperty("et"));
            var epoch = TimeSystem.Time.CreateTDB(et);
            Assert.Equal(et, epoch.TimeSpanFromJ2000().TotalSeconds);

            var lro = new Spacecraft(-85, "LRO", epoch);
            var state = lro.InitialOrbitalParameters.ToStateVector();
            Assert.Equal(et, state.Epoch.TimeSpanFromJ2000().TotalSeconds);
            Assert.Equal(399, state.Observer.NaifId);
            Assert.Equal(Frames.Frame.ICRF, state.Frame);
            var values = expected.GetProperty("state_m");
            var p = new[] { state.Position.X, state.Position.Y, state.Position.Z };
            var v = new[] { state.Velocity.X, state.Velocity.Y, state.Velocity.Z };
            double position = 0.0, velocity = 0.0;
            for (int i = 0; i < 3; i++)
            {
                position = System.Math.Max(position, System.Math.Abs(p[i] - Parse(values[i])));
                velocity = System.Math.Max(velocity, System.Math.Abs(v[i] - Parse(values[i + 3])));
            }

            // A tick off, the state would move by |v| x 1e-7 s, 2e-4 m
            _output.WriteLine($"{end}: position {position:E3} m, velocity {velocity:E3} m/s from spkezr");
            Assert.InRange(position, 0.0, 1e-6);
            Assert.InRange(velocity, 0.0, 1e-9);
        }
    }
}
