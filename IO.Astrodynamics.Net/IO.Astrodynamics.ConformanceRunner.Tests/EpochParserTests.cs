// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The tagged epoch of spec.md section 3: what the case schema accepts, and nothing it rejects.
/// </summary>
public class EpochParserTests
{
    [Theory]
    [InlineData("2021-03-20T12:00:00.000 UTC", "UTC")]
    [InlineData("2021-03-20T12:00:00.000 TAI", "TAI")]
    [InlineData("2021-03-20T12:00:00.000 TT", "TT")]
    [InlineData("2021-03-20T12:00:00.000 TDB", "TDB")]
    public void EachTag_GivesItsTimeFrame(string epoch, string scale)
    {
        // Act
        var time = EpochParser.Parse(epoch);

        // Assert: TT is the framework's TDT
        ITimeFrame expected = scale switch
        {
            "UTC" => TimeFrame.UTCFrame,
            "TAI" => TimeFrame.TAIFrame,
            "TT" => TimeFrame.TDTFrame,
            _ => TimeFrame.TDBFrame
        };
        Assert.Equal(expected, time.Frame);
        Assert.Equal(new DateTime(2021, 3, 20, 12, 0, 0), time.DateTime);
        Assert.Equal(scale, EpochParser.Scale(epoch));
    }

    [Theory]
    [InlineData("2016-12-31T23:59:59 UTC", 0L)]
    [InlineData("2016-12-31T23:59:59.5 UTC", 5_000_000L)]
    [InlineData("2016-12-31T23:59:59.25 UTC", 2_500_000L)]
    [InlineData("2016-12-31T23:59:59.125 UTC", 1_250_000L)]
    public void ZeroToThreeDecimals_AreReadExactly(string epoch, long ticks)
    {
        var time = EpochParser.Parse(epoch);

        Assert.Equal(new DateTime(2016, 12, 31, 23, 59, 59).Ticks + ticks, time.DateTime.Ticks);
    }

    [Theory]
    [InlineData("2016-12-31T23:59:59.1234 UTC")] // four decimals
    [InlineData("2016-12-31T23:59:60.000 UTC")] // second 60: the instant is given in TAI
    [InlineData("2021-03-20T12:00:00.000 GPS")] // GPS is an output only
    [InlineData("2021-03-20T12:00:00.000 TDT")] // the framework's name of TT
    [InlineData("2021-03-20T12:00:00.000 tdb")]
    [InlineData("2021-03-20T12:00:00.000")] // no tag
    [InlineData("2021-03-20T12:00:00.000  TDB")] // two spaces
    [InlineData("2021-03-20 12:00:00.000 TDB")] // no T
    [InlineData("2021-03-20T12:00:00.000 TDB\n")] // a final newline, which $ of .NET would let through
    [InlineData("2021-03-20T24:00:00.000 TDB")]
    [InlineData("2021-3-20T12:00:00.000 TDB")]
    [InlineData("")]
    [InlineData(null)]
    public void WhatTheSchemaRejects_IsRejected(string epoch)
    {
        Assert.Throws<FormatException>(() => EpochParser.Parse(epoch));
        Assert.Throws<FormatException>(() => EpochParser.Scale(epoch));
    }

    [Fact]
    public void ThePattern_IsThatOfTheCaseSchemaOfTheFixture()
    {
        // A re-sync of the fixture that changes the schema's pattern fails here until the parser takes the new text
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(Lot1Fixture.Root, "schemas", "v1", "case.schema.json")));

        Assert.Equal(EpochParser.Pattern,
            schema.RootElement.GetProperty("$defs").GetProperty("tagged_epoch").GetProperty("pattern").GetString());
    }

    [Fact]
    public void ADateTheCalendarDoesNotHave_IsRejected()
    {
        // The pattern of the schema lets 30 February through; the calendar does not
        var exception = Assert.Throws<FormatException>(() => EpochParser.Parse("2021-02-30T12:00:00.000 TDB"));

        Assert.Contains("2021-02-30", exception.Message);
    }
}
