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
using IO.Astrodynamics.ConformanceRunner.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The TLE cases of lot 5 (design of lot 5 of #457): SGP4 states in TEME and ICRF against Vallado's code and GMAT
/// R2026a's TEME axes, the TLE text read and written against the exact decimal evaluation of the format, and TEME ↔ ICRF
/// and TEME → ITRF93 against GMAT, run by the whole runner on the fixture <c>Fixture/Lot5</c> synced from the suite
/// (<see cref="SuiteFixture.Lot5"/>); and the parts of the solvers and of the comparer a case of the suite does not reach:
/// a model or mode the framework cannot run, lines that are not a TLE, the column table.
/// </summary>
/// <remarks>
/// Each test that runs the runner writes its own suite from the fixture, deleted at the end. Only
/// <see cref="EveryCaseOfTheFixture_Passes"/> prints <c>MEASURE</c> lines, one per compared output, which the CI logs of
/// the three platforms carry: the framework's margin to the tolerance of each output.
/// </remarks>
public sealed class Lot5CaseTests : IDisposable
{
    private const string KernelsPath = SuiteFixture.KernelsPath;
    private const string TemeCase = "cases/sgp4/001";
    private const string ReadCase = "cases/tle_conversion/003";
    private const string WriteCase = "cases/tle_conversion/004";

    // The ISS of CelesTrak's GP data of 2026-11-07 (sgp4 010)
    private const string IssLine1 = "1 25544U 98067A   26311.20783432  .00014294  00000+0  26479-3 0  9992";
    private const string IssLine2 = "2 25544  51.6321 312.3516 0007247 357.8301   2.2655 15.49235271593925";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public Lot5CaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("lot5-fixture-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryCaseOfTheFixture_Passes()
    {
        // Arrange
        SuiteFixture.Lot5.WriteSuite(_directory);

        // Act
        var report = new Runner(_directory, KernelsPath).Run();

        // Assert: the measures first, so that a failing case still prints them
        foreach (var result in report.Results)
        {
            foreach (var measure in result.Measures)
            {
                _output.WriteLine(measure.ToMeasureLine(result.CaseId));
            }
        }

        Assert.All(report.Results, r => Assert.True(r.Status == "PASS", $"{r.CaseId}: {r.Status} {r.Message}"));
        Assert.Equal(25, report.Summary.Passed);
        Assert.Equal(SuiteFixture.Lot5.CaseDirectories().Count, report.Summary.Total);

        // Every output is compared against the override of its key when the case has one (the ICRF outputs and the TEME
        // frames, GMAT's kinematic terms), the default of its key otherwise, an exact output against nothing; the lines of
        // a TLE give their exact part and their ten fields
        var defaults = SuiteFixture.ReadTolerances(_directory).Defaults;
        foreach (var caseDirectory in SuiteFixture.Lot5.CaseDirectories())
        {
            var caseInput = SuiteFixture.ReadCase(_directory, caseDirectory);
            var result = report.Results.Single(r => r.CaseId == caseInput.Id);
            foreach (var measure in result.Measures)
            {
                Assert.True(MetricTable.TryGet(caseInput.Category, measure.Output, out var metric), measure.Output);
                if (metric.Kind is MetricKind.Exact or MetricKind.TleLine)
                {
                    Assert.Equal("exact", measure.ToleranceName);
                    continue;
                }

                // The TEME frame cases override their outputs (position_km), the sgp4 cases the ICRF keys
                var overrides = caseInput.TolerancesOverride ?? new Dictionary<string, TolerancePair>();
                string name = overrides.ContainsKey(measure.Output) ? measure.Output : metric.Key;
                Assert.Equal(name, measure.ToleranceName);
                var tolerance = overrides.TryGetValue(name, out var o) ? o : defaults[name];
                Assert.Equal(tolerance.RelTol == 0.0 ? tolerance.AbsTol : tolerance.RelTol, measure.Threshold);
            }

            if (caseInput.Category == "tle_conversion" && caseInput.Inputs["operation"].ToString() == TleConversionInputs.OmmToTle)
            {
                int items = ((List<object>)caseInput.Inputs["items"]).Count;
                Assert.Equal(1 + items * (1 + MetricTable.TleFields.Count), result.Measures.Count);
            }
        }
    }

    [Theory]
    [InlineData("wgs72", "afspc", "cannot be run")]
    [InlineData("wgs84", "afspc", "cannot be run")]
    [InlineData("wgs72old", "improved", "cannot be run")]
    [InlineData("egm96", "afspc", "is not a gravity model of SGP4")]
    [InlineData("wgs72old", "fast", "is not a mode of SGP4")]
    public void AModelOrModeTheFrameworkCannotRun_IsRefusedWithItsReason(string gravityModel, string opsmode, string message)
    {
        // Arrange
        SuiteFixture.Lot5.WriteSuite(_directory, TemeCase);
        var caseInput = SuiteFixture.ReadCase(_directory, TemeCase);
        caseInput.Inputs["gravity_model"] = gravityModel;
        caseInput.Inputs["opsmode"] = opsmode;

        // Act
        var error = Assert.Throws<ArgumentException>(() => new Sgp4Solver().Solve(caseInput));

        // Assert
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void AModelTheSchemaDoesNotHold_IsAnError()
    {
        // Arrange
        SuiteFixture.Lot5.WriteSuite(_directory, TemeCase);
        var path = Path.Combine(_directory, TemeCase, "inputs.yaml");
        File.WriteAllText(path, File.ReadAllText(path).Replace("gravity_model: \"wgs72old\"", "gravity_model: \"wgs84\""));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
    }

    [Fact]
    public void IcrfWithTsince_IsRefusedByTheParser()
    {
        var inputs = SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot5, TemeCase).Inputs;
        inputs["output_frames"] = new List<object> { "TEME", "ICRF" };

        var error = Assert.Throws<ArgumentException>(() => InputParser.ParseSgp4Inputs(inputs));
        Assert.Contains("utc_epochs only", error.Message);
    }

    [Fact]
    public void TheColumnTable_ReadsTheGoldenOfTheAlpha5AndPivotCase()
    {
        // The golden of tle_conversion 003 is the exact decimal evaluation of the format by the suite's generator
        var caseInput = SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot5, ReadCase);
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(SuiteFixture.Lot5.Root, ReadCase, "expected-result.json")));
        var goldens = expected.RootElement.GetProperty("outputs").GetProperty("items").EnumerateArray().ToList();
        var items = (List<object>)caseInput.Inputs["items"];
        Assert.Equal(goldens.Count, items.Count);
        for (int k = 0; k < items.Count; k++)
        {
            var tle = (Dictionary<object, object>)((Dictionary<object, object>)items[k])["tle"];
            var fields = TleColumns.Decode(tle["line1"].ToString(), tle["line2"].ToString());
            var golden = goldens[k];

            Assert.Equal(golden.GetProperty("NORAD_CAT_ID").GetInt32(), fields.CatalogNumber);
            Assert.Equal(golden.GetProperty("OBJECT_ID").GetString(), fields.ObjectId);
            Assert.Equal(golden.GetProperty("ELEMENT_SET_NO").GetInt32(), fields.ElementSetNumber);
            Assert.Equal(golden.GetProperty("REV_AT_EPOCH").GetInt32(), fields.RevAtEpoch);
            foreach (var field in new[] { "BSTAR", "MEAN_MOTION_DOT", "ECCENTRICITY", "MEAN_MOTION", "MEAN_ANOMALY" })
            {
                Assert.Equal(golden.GetProperty(field).GetDouble(), (double)fields.Value(field));
            }

            var epoch = OutputComparer.ParseCalendarInstant(golden.GetProperty("EPOCH").GetString());
            Assert.Equal((epoch - new DateTime(1950, 1, 1)).Ticks, (long)(fields.EpochDays * TimeSpan.TicksPerDay));
        }
    }

    [Fact]
    public void LinesWithABadChecksum_Fail()
    {
        // Act: the last digit of line 2 changed
        var measures = Compare(IssLine1, IssLine2[..^1] + "6");

        // Assert: a FAIL of the lines and of every field, not an ERROR
        Assert.Equal(1 + MetricTable.TleFields.Count, measures.Count);
        Assert.All(measures, m => Assert.False(m.Passed));
        Assert.Contains("checksum of line 2", string.Join(";", TleColumns.Problems(IssLine1, IssLine2[..^1] + "6")));
    }

    [Fact]
    public void AnEccentricityOneDigitOff_Passes_AndAnEpochOneDayEarly_Fails()
    {
        // e .0007248 for .0007247: one unit of the last digit, its key's tolerance (the rounding convention is free)
        var eccentricity = Compare(IssLine1, WithChecksum(IssLine2[..26] + "0007248" + IssLine2[33..68]));
        Assert.All(eccentricity, m => Assert.True(m.Passed, m.Output));
        Assert.Equal(1e-7, eccentricity.Single(m => m.Output.EndsWith(".ECCENTRICITY")).AbsoluteDelta, 1e-20);

        // The epoch of #600 before its fix: the day of the carry dropped
        var epoch = Compare(WithChecksum(IssLine1[..18] + "26310.20783432" + IssLine1[32..68]), IssLine2);
        Assert.False(epoch.Single(m => m.Output.EndsWith(".EPOCH")).Passed);
        Assert.Equal(86400.0, epoch.Single(m => m.Output.EndsWith(".EPOCH")).AbsoluteDelta);
        Assert.True(epoch[0].Passed);
    }

    [Fact]
    public void ATtOmm_IsWrittenAtItsUtcEpoch()
    {
        // Arrange: the TT item of tle_conversion 004
        var inputs = InputParser.ParseTleConversionInputs(SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot5, WriteCase).Inputs);
        var item = inputs.Omms.Single(o => o.TimeSystem == "TT");
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));

        // Act
        var tle = TleConversionSolver.ToOmm(item).ToTle();

        // Assert: TT − UTC = 32.184 s + 37 s (no leap second since 2017-01-01), to the nearest 1e-8 day
        var utc = item.Epoch.AddSeconds(-69.184);
        decimal expected = (utc - new DateTime(1950, 1, 1)).Ticks / (decimal)TimeSpan.TicksPerDay;
        var fields = TleColumns.Decode(tle.Line1, tle.Line2);
        Assert.InRange(System.Math.Abs(fields.EpochDays - expected), 0m, 0.5e-8m);
    }

    [Fact]
    public void TheOutputsOfAnSgp4AndATleGolden_AreTheLeavesOfEachElement_ButTheLines()
    {
        using var golden = JsonDocument.Parse("""
                                              {"epoch": "2026-11-07T04:59:16.885248 UTC",
                                               "states": [{"teme": {"position_km": [1.0, 2.0, 3.0], "velocity_km_s": [0.0, 1.0, 0.0]}}],
                                               "items": [{"tle": {"line1": "1", "line2": "2"}}]}
                                              """);

        Assert.Equal(new[] { "epoch", "states", "states[0].teme.position_km", "states[0].teme.velocity_km_s" },
            OutputComparer.Outputs(golden.RootElement, "sgp4").Select(o => o.Output).Take(4));
        Assert.Equal(new[] { "items", "items[0].tle" }, OutputComparer.Outputs(golden.RootElement, "tle_conversion").Select(o => o.Output).Skip(2));
        Assert.Equal("states[].teme.position_km", MetricTable.Generic("states[12].teme.position_km"));
        Assert.Equal("states", MetricTable.IndexedArrayOf("sgp4", "states[12].icrf.velocity_km_s"));
    }

    [Fact]
    public void TheComponentAndRelativeKinds_FollowSpecSection61()
    {
        using var golden = JsonDocument.Parse("""{"v": [1.0, 2.0, 3.0], "zero": 0.0, "x": 2.0}""");
        var (component, _) = OutputComparer.Deltas(MetricKind.ScalarPerComponent, new[] { 1.5, 2.0, 2.0 }, golden.RootElement.GetProperty("v"), "v");
        Assert.Equal(1.0, component);
        Assert.Equal(0.25, OutputComparer.Deltas(MetricKind.Relative, 2.5, golden.RootElement.GetProperty("x"), "x").Relative);
        Assert.Equal(0.0, OutputComparer.Deltas(MetricKind.Relative, 0.0, golden.RootElement.GetProperty("zero"), "zero").Relative);
        Assert.Equal(double.PositiveInfinity, OutputComparer.Deltas(MetricKind.Relative, 1e-300, golden.RootElement.GetProperty("zero"), "zero").Relative);
        Assert.Throws<FormatException>(() => OutputComparer.ParseCalendarInstant("2026-11-07T04:59:16.885 UTC"));
    }

    // The measures of computed lines against the ISS
    private static IReadOnlyList<OutputMeasure> Compare(string line1, string line2)
    {
        using var golden = JsonDocument.Parse($$"""{"line1": "{{IssLine1}}", "line2": "{{IssLine2}}"}""");
        var defaults = new Dictionary<string, TolerancePair>
        {
            ["tle_field_epoch_s"] = new() { AbsTol = 4.4e-4 },
            ["tle_field_mean_motion_rev_day"] = new() { AbsTol = 1e-8 },
            ["tle_field_eccentricity"] = new() { AbsTol = 1e-7 },
            ["tle_field_angle_deg"] = new() { AbsTol = 1e-4 },
            ["tle_field_mean_motion_dot"] = new() { AbsTol = 1e-8 },
            ["tle_field_exponent_rel"] = new() { RelTol = 1e-4 }
        };
        return TleLineComparer.Compare("tle_conversion", "items[0].tle", new TleText(line1, line2), golden.RootElement,
            (field, key) => (defaults[key], key));
    }

    private static string WithChecksum(string columns1To68)
    {
        return columns1To68 + TleColumns.Checksum(columns1To68).ToString(CultureInfo.InvariantCulture);
    }
}
