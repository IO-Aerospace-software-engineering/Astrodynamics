using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// <c>time-converter &lt;epoch&gt; --to tdb|tt|tai|utc|gps|local --format iso|jd|seconds</c> (D3.7 of #668, decided by
/// S. Guillet, 2026-11-19), TDB and ISO by default, <c>-t -u -l -j -e -d</c> their aliases. The expected values are
/// the definitions of the scales: 2017-01-01T00:00:00Z is TAI 00:00:37 (TAI − UTC = 37 s), TT = TAI + 32.184 s,
/// GPS = TAI − 19 s; J2000 is 2000-01-01T12:00:00 and JD 2451545.0 of each scale.
/// </summary>
public class TimeConverterOptionsTests
{
    [Theory]
    [InlineData("tai", "2017-01-01T00:00:37.0000000 TAI")]
    [InlineData("TT", "2017-01-01T00:01:09.1840000 TT")]
    [InlineData("gps", "2017-01-01T00:00:18.0000000 GPS")]
    [InlineData("utc", "2017-01-01T00:00:00.0000000Z")]
    public void To_ConvertsToEachScale(string to, string expected)
    {
        Assert.Equal($"{expected}{Environment.NewLine}", Run("2017-01-01T00:00:00Z", to: to));
    }

    [Fact]
    public void WithoutAnOption_TheOutputIsTdbInIso()
    {
        // Before 11.0.0 a target scale and a format were required; TDB and ISO are the defaults since D3.7
        Assert.Equal($"2000-01-01T12:00:00.0000000 TDB{Environment.NewLine}", Run("0.0 TDB"));
    }

    [Fact]
    public void Format_WritesSecondsAndJulianDatesOfTheTargetScale()
    {
        Assert.Equal($"0{Environment.NewLine}", Run("2000-01-01T12:00:00 TAI", to: "tai", format: "seconds"));
        Assert.Equal($"2451545 JD{Environment.NewLine}", Run("2000-01-01T12:00:00 GPS", to: "gps", format: "JD"));
        // The aliases give the same outputs
        Assert.Equal(Run("0.0 TDB", to: "utc", format: "seconds"), Run("0.0 TDB", toUtc: true, toSeconds: true));
    }

    [Fact]
    public void Local_IsTheCalendarOfTheZoneOfTheMachine()
    {
        // A local time is not a scale (D3.1): the UTC instant in the machine's zone, by .NET's rule for that zone
        var expected = new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"{expected}{Environment.NewLine}", Run("2017-01-01T00:00:00Z", to: "local"));
        Assert.Equal(Run("2017-01-01T00:00:00Z", to: "local"), Run("2017-01-01T00:00:37 TAI", to: "local"));
    }

    [Theory]
    [InlineData("tcg", null)]
    [InlineData(null, "csv")]
    [InlineData("local", "jd")]
    [InlineData("local", "seconds")]
    public void AnUnknownScaleOrFormat_OrLocalOutsideIso_Throws(string? to, string? format)
    {
        Assert.Throws<ArgumentException>(() => Run("2017-01-01T00:00:00Z", to: to, format: format));
    }

    [Fact]
    public void AnOptionAndAnAliasThatDisagree_Throw()
    {
        Assert.Throws<ArgumentException>(() => Run("2017-01-01T00:00:00Z", to: "utc", toTdb: true));
        Assert.Throws<ArgumentException>(() => Run("2017-01-01T00:00:00Z", format: "jd", toSeconds: true));
        // An alias that agrees with its option is accepted
        Assert.Equal(Run("2017-01-01T00:00:00Z", to: "utc"), Run("2017-01-01T00:00:00Z", to: "utc", toUtc: true));
    }

    [Fact]
    public async Task AnArgumentAfterTheEpoch_Throws()
    {
        // An unquoted tag (time-converter 2020-01-01T12:00:00 TAI) reached the command as a second argument, which was
        // dropped, and the epoch read untagged, as TDB (D3.7 of #668)
        using (Configuration.Lock())
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => new TimeConverterCommand().TimeConverter(
                new EpochParameters { Epoch = "2020-01-01T12:00:00" }, unexpected: ["TAI"]));
            Assert.Contains("\"2020-01-01T12:00:00 TAI\"", exception.Message);
        }
    }

    private static string Run(string epoch, string? to = null, string? format = null, bool toTdb = false, bool toUtc = false,
        bool toSeconds = false)
    {
        using (Configuration.Lock())
        {
            var sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            new TimeConverterCommand().TimeConverter(new EpochParameters { Epoch = epoch }, toTdb, toUtc, false, false, toSeconds,
                false, to, format);
            return sb.ToString();
        }
    }
}
