using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common;

/// <summary>
/// The CCSDS date parser of the CDM, OPM and OMM readers (#490). The reference,
/// <c>Data/CcsdsEpochReference/ccsds_epoch_reference.json</c>, comes from <c>tools/ccsds_epoch_reference</c>: the ticks of
/// each day-of-year date by the proleptic Gregorian ordinal, each checked against CSPICE's <c>str2et</c> (the day-of-year
/// and calendar texts give the same ET, <c>et2utc</c> the same instant), and the days <c>str2et</c> rejects.
/// </summary>
public class CcsdsEpochParserTests
{
    private const string ReferenceFile = "Data/CcsdsEpochReference/ccsds_epoch_reference.json";

    private const DateTimeStyles FallbackStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private static readonly Lazy<JsonDocument> Reference = new(() => JsonDocument.Parse(File.ReadAllText(ReferenceFile)));

    public static IEnumerable<object[]> DayOfYearRows() => Rows("day_of_year");

    public static IEnumerable<object[]> CalendarRows() => Rows("calendar");

    private static IEnumerable<object[]> Rows(string section)
    {
        return Reference.Value.RootElement.GetProperty(section).EnumerateArray()
            .Select(row => new object[] { row.GetProperty("text").GetString() });
    }

    private static JsonElement Row(string section, string text)
    {
        return Reference.Value.RootElement.GetProperty(section).EnumerateArray()
            .Single(row => row.GetProperty("text").GetString() == text);
    }

    [Theory]
    [MemberData(nameof(DayOfYearRows))]
    public void DayOfYear_IsTheInstantOfStr2et(string text)
    {
        var row = Row("day_of_year", text);
        bool parsed = CcsdsEpochParser.TryParse(text, out var utc);
        if (!row.GetProperty("valid").GetBoolean())
        {
            // A day out of range (SPICE(BADTIMESTRING)), or the literal DDD the readers loaded as 1 January before
            Assert.False(parsed, text);
            return;
        }

        Assert.True(parsed, text);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(row.GetProperty("ticks").GetInt64(), utc.Ticks);

        // The same instant as its calendar text, bit for bit
        Assert.True(CcsdsEpochParser.TryParse(row.GetProperty("calendar").GetString(), out var calendar));
        Assert.Equal(calendar, utc);
    }

    [Theory]
    [MemberData(nameof(CalendarRows))]
    public void Calendar_IsUnchanged(string text)
    {
        Assert.True(CcsdsEpochParser.TryParse(text, out var utc));
        Assert.Equal(Row("calendar", text).GetProperty("ticks").GetInt64(), utc.Ticks);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);

        // The readers' former fallback gives the same DateTime
        Assert.True(DateTime.TryParse(text, CultureInfo.InvariantCulture, FallbackStyles, out var former));
        Assert.Equal(former, utc);
        Assert.Equal(former.Kind, utc.Kind);
    }

    [Theory]
    [InlineData("2021-03-20T12:00:00.12345674")]
    [InlineData("2021-03-20T12:00:00.12345675")]
    [InlineData("2021-03-20T12:00:00.12345665Z")]
    [InlineData("2026-01-13T05:00:55.752768123")]
    [InlineData("2021-03-20T23:59:59.99999996")]
    [InlineData("2021-03-20T12:00:00.123456789+05:30")]
    public void LongFraction_RoundsAsBefore(string calendar)
    {
        // Calendar: the DateTime of the former fallback, bit for bit
        Assert.True(CcsdsEpochParser.TryParse(calendar, out var utc));
        Assert.True(DateTime.TryParse(calendar, CultureInfo.InvariantCulture, FallbackStyles, out var former));
        Assert.Equal(former, utc);

        // Day of year: the same rounding
        var date = DateTime.ParseExact(calendar[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dayOfYear = $"{date.Year:D4}-{date.DayOfYear:D3}{calendar[10..]}";
        Assert.True(CcsdsEpochParser.TryParse(dayOfYear, out var fromDayOfYear));
        Assert.Equal(utc, fromDayOfYear);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2021-DDDT12:00:00Z")]
    [InlineData("2016-12-31T23:59:60")]
    [InlineData("2016-366T23:59:60")]
    [InlineData("2021-079T24:00:00")]
    [InlineData("2021-079T12:60:00")]
    [InlineData("2021-079T12:00:00+15:00")]
    [InlineData("2021-079T12:00:00+05:60")]
    [InlineData("2026-02-30T05:00:55")]
    [InlineData("2021-079T12:00:00.")]
    [InlineData("0001-001T00:00:00+01:00")]
    [InlineData("9999-365T23:59:59.9999999-01:00")]
    public void NotADate_IsRejected(string text)
    {
        Assert.False(CcsdsEpochParser.TryParse(text, out _));
    }

    [Fact]
    public void OtherText_KeepsTheFormerFallback()
    {
        // Outside the epochType form, DateTime.TryParse as before (#619 keeps its leniency)
        const string text = "2021-03-20 12:00:00";
        Assert.True(CcsdsEpochParser.TryParse(text, out var utc));
        Assert.Equal(new DateTime(2021, 3, 20, 12, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Readers_ReadDayOfYearDates()
    {
        var expected = new DateTime(Row("day_of_year", "2026-013T05:00:55.752768").GetProperty("ticks").GetInt64(), DateTimeKind.Utc);
        var creation = new DateTime(Row("day_of_year", "2014-021T17:42:47.000").GetProperty("ticks").GetInt64(), DateTimeKind.Utc);

        // OMM: EPOCH and CREATION_DATE
        var ommXml = File.ReadAllText(Path.Combine("CCSDS", "iss.omm"))
            .Replace("<CREATION_DATE>2026-01-13T10:18:41</CREATION_DATE>", "<CREATION_DATE>2014-021T17:42:47.000</CREATION_DATE>")
            .Replace("<EPOCH>2026-01-13T05:00:55.752768</EPOCH>", "<EPOCH>2026-013T05:00:55.752768</EPOCH>");
        var omm = new OmmReader().ReadFromString(ommXml);
        Assert.Equal(expected, omm.Data.MeanElements.Epoch);
        Assert.Equal(creation, omm.Header.CreationDate);

        // OPM: EPOCH and CREATION_DATE
        var opm = new OpmReader().ReadFromString(OpmXml("2014-021T17:42:47.000", "2026-013T05:00:55.752768"));
        Assert.Equal(expected, opm.Data.StateVector.Epoch);
        Assert.Equal(creation, opm.Header.CreationDate);

        // CDM: CdmTests.ReadFromString_DayOfYearDates
    }

    [Fact]
    public void Readers_RejectTheLiteralDdd()
    {
        Assert.Throws<OpmParseException>(() => new OpmReader().ReadFromString(OpmXml("2021-DDDT12:00:00", "2026-013T05:00:55")));
        var ommXml = File.ReadAllText(Path.Combine("CCSDS", "iss.omm"))
            .Replace("<EPOCH>2026-01-13T05:00:55.752768</EPOCH>", "<EPOCH>2026-DDDT05:00:55.752768</EPOCH>");
        Assert.Throws<OmmParseException>(() => new OmmReader().ReadFromString(ommXml));
    }

    private static string OpmXml(string creationDate, string epoch)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>{creationDate}</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>{epoch}</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";
    }
}
