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
        Assert.Equal($"0 TAI{Environment.NewLine}", Run("2000-01-01T12:00:00 TAI", to: "tai", format: "seconds"));
        Assert.Equal($"2451545 JD GPS{Environment.NewLine}", Run("2000-01-01T12:00:00 GPS", to: "gps", format: "JD"));
        // The aliases give the same outputs
        Assert.Equal(Run("0.0 TDB", to: "utc", format: "seconds"), Run("0.0 TDB", toUtc: true, toSeconds: true));
    }

    /// <summary>
    /// Every output names its scale (D3.4 of #668), so the command reads it back as the same instant: an untagged Julian
    /// date or seconds of UTC, TAI or GPS were read as TDB, 69.18 s early for UTC in 2024. 2024-01-01T00:00:00Z is JD
    /// 2460310.5 UTC and 757339200 s of UTC from J2000 (8765.5 days), the definitions of the forms.
    /// </summary>
    [Theory]
    [InlineData("utc", "jd", "2460310.5 JD UTC")]
    [InlineData("utc", "seconds", "757339200 UTC")]
    [InlineData("tai", "jd", null)]
    [InlineData("tai", "seconds", "757339237 TAI")]
    [InlineData("gps", "seconds", "757339218 GPS")]
    [InlineData("tt", "seconds", "757339269.184 TT")]
    [InlineData("tdb", "jd", null)]
    [InlineData("tdb", "seconds", null)]
    public void JulianDatesAndSeconds_NameTheirScale_AndReadBackAsTheSameInstant(string to, string format, string? expected)
    {
        const string epoch = "2024-01-01T00:00:00Z";
        string written = Run(epoch, to: to, format: format).TrimEnd();
        if (expected != null)
        {
            Assert.Equal(expected, written);
        }

        Assert.EndsWith(" " + to.ToUpperInvariant(), written);
        // Read back and written in ISO UTC: the instant given, to the tick a Julian date's double holds (about 40 µs at
        // JD 2.46e6)
        var back = DateTime.Parse(Run(written, to: "utc").TrimEnd().TrimEnd('Z'), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(System.Math.Abs((back - new DateTime(2024, 1, 1)).TotalSeconds) < 1e-4, $"{written} read back as {back:O}");
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
    public void AnUnknownScaleOrFormat_OrLocalOutsideIso_IsAnInputError(string? to, string? format)
    {
        using (Configuration.Lock())
        {
            Assert.StartsWith("Error: ", CliRun.Fails(2, Args("2017-01-01T00:00:00Z", to, format, false, false, false)));
        }
    }

    [Fact]
    public void AnOptionAndAnAliasThatDisagree_AreAnInputError()
    {
        using (Configuration.Lock())
        {
            Assert.Equal($"Error: Two scales are asked for: utc and tdb. Give one.{Environment.NewLine}",
                CliRun.Fails(2, Args("2017-01-01T00:00:00Z", "utc", null, true, false, false)));
            Assert.Equal($"Error: Two formats are asked for: jd and seconds. Give one.{Environment.NewLine}",
                CliRun.Fails(2, Args("2017-01-01T00:00:00Z", null, "jd", false, false, true)));
        }

        // An alias that agrees with its option is accepted
        Assert.Equal(Run("2017-01-01T00:00:00Z", to: "utc"), Run("2017-01-01T00:00:00Z", to: "utc", toUtc: true));
    }

    [Fact]
    public void AnArgumentAfterTheEpoch_IsAnInputError()
    {
        // An unquoted tag (time-converter 2020-01-01T12:00:00 TAI) reached the command as a second argument, which was
        // dropped, and the epoch read untagged, as TDB (D3.7 of #668)
        using (Configuration.Lock())
        {
            var error = CliRun.Fails(2, "time-converter", "2020-01-01T12:00:00", "TAI");
            Assert.Contains("\"2020-01-01T12:00:00 TAI\"", error);
        }
    }

    /// <summary>
    /// A negative number of seconds, bare (R29 of #668: Cocona read "-1000" as the options -1, -0, ...), and an epoch the
    /// grammar refuses, an input error.
    /// </summary>
    [Fact]
    public void NegativeSeconds_AndAnEpochOfNoForm()
    {
        using (Configuration.Lock())
        {
            Assert.Equal($"2000-01-01T11:43:20.0000000 TDB{Environment.NewLine}", CliRun.Ok("time-converter", "-1000"));
            Assert.Equal($"-1000 UTC{Environment.NewLine}", CliRun.Ok("time-converter", "-1000 UTC", "--to", "utc", "--format", "seconds"));
            Assert.StartsWith("Error: epoch: 'tomorrow' is not an epoch", CliRun.Fails(2, "time-converter", "tomorrow"));
        }
    }

    private static string[] Args(string epoch, string? to, string? format, bool toTdb, bool toUtc, bool toSeconds)
    {
        var args = new List<string> { "time-converter", epoch };
        if (to != null) args.AddRange(["--to", to]);
        if (format != null) args.AddRange(["--format", format]);
        if (toTdb) args.Add("-t");
        if (toUtc) args.Add("-u");
        if (toSeconds) args.Add("-e");
        return args.ToArray();
    }

    private static string Run(string epoch, string? to = null, string? format = null, bool toTdb = false, bool toUtc = false,
        bool toSeconds = false)
    {
        using (Configuration.Lock())
        {
            return CliRun.Ok(Args(epoch, to, format, toTdb, toUtc, toSeconds));
        }
    }
}
