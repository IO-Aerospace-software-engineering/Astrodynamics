// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using Xunit;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// <see cref="TimeConversionSolver"/> on an epoch of each scale, against the definitions of spec.md section 3: TT = TAI
/// + 32.184 s, GPS = TAI − 19 s, UTC = TAI − ΔAT (37 s from 2017-01-01, 32 s in 2000), and TDB − TT within the spread
/// of the standard series.
/// </summary>
public class TimeConversionSolverTests
{
    private static readonly long J2000 = new DateTime(2000, 1, 1, 12, 0, 0).Ticks;

    private readonly TimeConversionSolver _solver = new();

    public TimeConversionSolverTests()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(Lot1Fixture.KernelsPath));
    }

    private Dictionary<string, object> Solve(string epoch)
    {
        return _solver.Solve(new CaseInput
        {
            Id = "time_conversion_test",
            Category = "time_conversion",
            Inputs = new Dictionary<string, object> { ["epoch"] = epoch }
        });
    }

    // Seconds from J2000 of a calendar date and time, plus an offset
    private static double Seconds(DateTime dateTime, double offsetSeconds = 0.0)
    {
        return (dateTime.Ticks + TimeSpan.FromSeconds(offsetSeconds).Ticks - J2000) / (double)TimeSpan.TicksPerSecond;
    }

    [Fact]
    public void Category_IsTimeConversion()
    {
        Assert.Equal("time_conversion", _solver.Category);
    }

    [Fact]
    public void AUtcEpoch_AfterTheLeapSecondOf2016()
    {
        var utc = new DateTime(2017, 1, 1, 0, 0, 0);

        var outputs = Solve("2017-01-01T00:00:00.000 UTC");

        Assert.Equal(Seconds(utc), outputs["utc_s"]);
        Assert.Equal(Seconds(utc, 37.0), outputs["tai_s"]);
        Assert.Equal(Seconds(utc, 37.0 + 32.184), outputs["tt_s"]);
        Assert.Equal(Seconds(utc, 37.0 - 19.0), outputs["gps_s"]);
    }

    [Fact]
    public void ATaiEpochInsideTheLeapSecond()
    {
        // 2016-12-31T23:59:60.5 UTC, which a UTC calendar of 86400 s days cannot write
        var tai = new DateTime(2017, 1, 1, 0, 0, 36, 500);

        var outputs = Solve("2017-01-01T00:00:36.500 TAI");

        Assert.Equal(Seconds(tai), outputs["tai_s"]);
        Assert.Equal(Seconds(tai, 32.184), outputs["tt_s"]);
        Assert.Equal(Seconds(tai, -19.0), outputs["gps_s"]);
    }

    [Fact]
    public void J2000InTt()
    {
        var outputs = Solve("2000-01-01T12:00:00.000 TT");

        Assert.Equal(0.0, outputs["tt_s"]);
        Assert.Equal(-32.184, (double)outputs["tai_s"], 1e-9);
        Assert.Equal(-32.184 - 32.0, (double)outputs["utc_s"], 1e-9);
    }

    [Fact]
    public void ATdbEpoch_IsExactInTdb_AndWithinTheSpreadOfTheSeriesInTt()
    {
        // Arrange: TDB - TT = 0.001657 sin g + 0.000014 sin 2g, g = 357.53 deg + 0.98560028 deg/day; the standard series
        // differ by up to 37.6 us (design of #458, section 1)
        var tdb = new DateTime(2021, 3, 20, 12, 0, 0);
        double days = (tdb.Ticks - J2000) / (double)TimeSpan.TicksPerDay;
        double g = (357.53 + 0.98560028 * days) * System.Math.PI / 180.0;
        double tdbMinusTt = 0.001657 * System.Math.Sin(g) + 0.000014 * System.Math.Sin(2.0 * g);

        // Act
        var outputs = Solve("2021-03-20T12:00:00.000 TDB");

        // Assert
        Assert.Equal(Seconds(tdb), outputs["tdb_s"]);
        Assert.Equal(tdbMinusTt, Seconds(tdb) - (double)outputs["tt_s"], 4e-5);
        Assert.Equal(32.184, (double)outputs["tt_s"] - (double)outputs["tai_s"], 1e-6);
    }

    [Fact]
    public void AScaleWhoseDateTheCalendarCannotHold_IsAnErrorOnlyWhenTheGoldenHoldsIt()
    {
        // Arrange: an epoch the schema accepts, whose TT and TDB dates fall after 9999-12-31
        var tai = new DateTime(9999, 12, 31, 23, 59, 50);
        var caseInput = new CaseInput
        {
            Id = "time_conversion_test",
            Category = "time_conversion",
            Inputs = new Dictionary<string, object> { ["epoch"] = "9999-12-31T23:59:50.000 TAI" }
        };
        var defaults = new Dictionary<string, TolerancePair>
        {
            [MetricTable.TimeKey] = new() { AbsTol = 9.6e-7, RelTol = 0.0 },
            [MetricTable.TdbTimeKey] = new() { AbsTol = 7.6e-5, RelTol = 0.0 }
        };

        // Act
        var outputs = _solver.Solve(caseInput);

        // Assert: the other scales are computed, and a golden without TT passes
        Assert.Equal(Seconds(tai), outputs["tai_s"]);
        Assert.Equal(Seconds(tai, -19.0), outputs["gps_s"]);
        Assert.IsType<ArgumentOutOfRangeException>(outputs["tt_s"]);
        Assert.IsType<ArgumentOutOfRangeException>(outputs["tdb_s"]);
        string taiSeconds = Seconds(tai).ToString("R", CultureInfo.InvariantCulture);
        Assert.True(OutputComparer.Compare(caseInput, outputs, JsonDocument.Parse($"{{\"tai_s\": {taiSeconds}}}").RootElement,
            defaults).Single().Passed);

        // A golden holding TT is an ERROR naming it
        var exception = Assert.Throws<InvalidOperationException>(() => OutputComparer.Compare(caseInput, outputs,
            JsonDocument.Parse($"{{\"tai_s\": {taiSeconds}, \"tt_s\": 0.0}}").RootElement, defaults));
        Assert.Contains("tt_s", exception.Message);
        Assert.Contains("cannot compute", exception.Message);
    }
}
