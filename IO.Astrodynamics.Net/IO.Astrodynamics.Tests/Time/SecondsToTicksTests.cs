using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// Every span of seconds becomes the nearest tick of a <see cref="TimeSystem.Time"/>, ties to even (#625):
/// <see cref="TimeSystem.Time.Create(double, ITimeFrame)"/>, <see cref="TimeSystem.Time.Create(int, int, double)"/>,
/// <see cref="TimeSystem.Time.AddSeconds"/>, <see cref="TimeSystem.Time.AddMilliseconds"/>,
/// <see cref="TimeSystem.Time.AddMicroseconds"/> and the TDB − TT offset of <see cref="TDBTimeFrame"/>. The references
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

        // 857 of the 1744 values are not their truncation, the former result
        _output.WriteLine($"{count} values, {moved} whose nearest tick is not their truncation");
        Assert.Equal(1744, count);
        Assert.Equal(857, moved);
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
