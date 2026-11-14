using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// The calendar epochs of the command line (#509): one tagged UTC, in any case and with or without a space, is a UTC
/// calendar, as one ending in Z. It used to be read as TDB, the frame of an untagged epoch, 69.18 s early in 2020.
/// </summary>
public class EpochInputTests
{
    // 2020-01-01T12:00:00 UTC in TDB seconds from J2000, the value of TimeTests.DateToSecondsFromJ2000 for the Z form:
    // 7305 days of calendar, plus TAI − UTC (37 s), TT − TAI (32.184 s) and TDB − TT, its nearest tick since #625
    // (tools/seconds_to_ticks/time_conversion_reference.py; .1839218 when it was truncated)
    private const string UtcNoonInTdbSeconds = "631152069.1839217";

    [Theory]
    [InlineData("2020-01-01T12:00:00Z")]
    [InlineData("2020-01-01T12:00:00 UTC")]
    [InlineData("2020-01-01T12:00:00UTC")]
    [InlineData("2020-01-01T12:00:00 utc")]
    [InlineData("2020-01-01T12:00:00 Utc")]
    [InlineData("  2020-01-01T12:00:00   UTC  ")]
    [InlineData("2020-01-01 12:00:00 UTC")]
    public void ACalendarEpochTaggedUtc_IsUtc(string epoch)
    {
        Assert.Equal($"{UtcNoonInTdbSeconds}{Environment.NewLine}", SecondsFromJ2000InTdb(epoch));
    }

    [Theory]
    [InlineData("2020-01-01T12:00:00 UTC", "2020-01-01T12:00:00.0000000Z")]
    [InlineData("2020-01-01T12:00:00utc", "2020-01-01T12:00:00.0000000Z")]
    public void ACalendarEpochTaggedUtc_ConvertedToUtc_IsItself(string epoch, string expected)
    {
        using (Configuration.Lock())
        {
            var sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            new TimeConverterCommand().TimeConverter(new EpochParameters { Epoch = epoch }, false, true, false, false,
                false, true);
            Assert.Equal($"{expected}{Environment.NewLine}", sb.ToString());
        }
    }

    [Theory]
    [InlineData("2020-01-01T12:00:00 TDB")]
    [InlineData("2020-01-01T12:00:00tdb")]
    [InlineData("2020-01-01T12:00:00")]
    public void ACalendarEpochTaggedTdbOrUntagged_IsTdb(string epoch)
    {
        // The calendar difference of 2020-01-01T12:00:00 and 2000-01-01T12:00:00, 7305 days
        Assert.Equal($"{7305 * 86400.0}{Environment.NewLine}", SecondsFromJ2000InTdb(epoch));
    }

    [Fact]
    public void SecondsTaggedUtc_AreUtc()
    {
        // Unchanged by #509: the numeric form already read its tag, in any case
        Assert.Equal(SecondsFromJ2000InTdb("631152000.0 UTC"), SecondsFromJ2000InTdb("631152000.0 utc"));
        Assert.NotEqual(SecondsFromJ2000InTdb("631152000.0 UTC"), SecondsFromJ2000InTdb("631152000.0"));
    }

    [Theory]
    [InlineData("2458850.0 JD UTC")]
    [InlineData("2458850.0 jd utc")]
    public void AJulianDateTaggedUtc_IsUtc(string epoch)
    {
        // Unchanged by #509: JD 2458850.0 is 2020-01-01T12:00:00, 7305 days after JD 2451545.0
        Assert.Equal($"{UtcNoonInTdbSeconds}{Environment.NewLine}", SecondsFromJ2000InTdb(epoch));
    }

    [Fact]
    public void AnUntaggedJulianDate_IsTdb()
    {
        Assert.Equal($"{7305 * 86400.0}{Environment.NewLine}", SecondsFromJ2000InTdb("2458850.0 JD"));
    }

    // #680: NaN is not a Julian date; CreateFromJD read it as 0 days from J2000, and the command ran at J2000
    [Theory]
    [InlineData("NaN JD")]
    [InlineData("NaN JD UTC")]
    [InlineData("nan jd tdb")]
    public void NaNJulianDate_IsRejected(string epoch)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => SecondsFromJ2000InTdb(epoch));
        Assert.Equal("julianDate", exception.ParamName);
    }

    private static string SecondsFromJ2000InTdb(string epoch)
    {
        using (Configuration.Lock())
        {
            var sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            new TimeConverterCommand().TimeConverter(new EpochParameters { Epoch = epoch }, true, false, false, false,
                true, false);
            return sb.ToString();
        }
    }
}
