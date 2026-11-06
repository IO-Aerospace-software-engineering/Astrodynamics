// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The geometry and event cases of lot 4 (design of lot 4 of #457): ground-station passes and samples, every eclipse of
/// a window by the Earth and the Moon, apsides, nodes and altitude crossings, attitude laws, with GMAT R2026a and
/// CSPICE goldens, run by the whole runner on the fixture <c>Fixture/Lot4</c> synced from the suite
/// (<see cref="SuiteFixture.Lot4"/>); and the parts of the solvers and of the comparer a case of the suite does not
/// reach: the light-time flags, the inputs the runner refuses, a count that differs from the golden's.
/// </summary>
/// <remarks>
/// Each test that runs the runner writes its own suite from the fixture, deleted at the end. Only
/// <see cref="EveryCaseOfTheFixture_Passes"/> prints <c>MEASURE</c> lines, one per compared output, which the CI logs of
/// the three platforms carry: the framework's margin to the tolerance of each output.
/// </remarks>
public sealed class Lot4CaseTests : IDisposable
{
    private const string KernelsPath = SuiteFixture.KernelsPath;
    private const string StationCase = "cases/ground_station/001";
    private const string AnnularCase = "cases/eclipse/005";
    private const string EventsCase = "cases/orbital_events/001";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public Lot4CaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("lot4-fixture-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryCaseOfTheFixture_Passes()
    {
        // Arrange
        SuiteFixture.Lot4.WriteSuite(_directory);

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
        Assert.Equal(22, report.Summary.Passed);
        Assert.Equal(SuiteFixture.Lot4.CaseDirectories().Count, report.Summary.Total);

        // Every output of every golden is compared, in its order (each field of each element of an indexed array), against
        // the default of its key, an exact output against nothing: no case of the lot has an override (spec.md section 6.4)
        var defaults = SuiteFixture.ReadTolerances(_directory).Defaults;
        foreach (var caseDirectory in SuiteFixture.Lot4.CaseDirectories())
        {
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, caseDirectory, "expected-result.json")));
            var caseInput = SuiteFixture.ReadCase(_directory, caseDirectory);
            var result = report.Results.Single(r => r.CaseId == caseInput.Id);
            Assert.Null(caseInput.TolerancesOverride);
            Assert.Equal(ExpectedOutputs(caseInput.Category, expected.RootElement.GetProperty("outputs")), result.Measures.Select(m => m.Output));
            foreach (var measure in result.Measures)
            {
                Assert.True(MetricTable.TryGet(caseInput.Category, measure.Output, out var metric));
                if (metric.Kind == MetricKind.Exact)
                {
                    Assert.Equal("exact", measure.ToleranceName);
                    continue;
                }

                Assert.Equal(metric.Key, measure.ToleranceName);
                Assert.Equal(defaults[measure.ToleranceName].AbsTol, measure.Threshold);
            }
        }
    }

    [Fact]
    public void APassTheFrameworkDoesNotFind_Fails()
    {
        // Arrange: a seventh pass in the golden of the station, a copy of the sixth
        SuiteFixture.Lot4.WriteSuite(_directory, StationCase);
        SuiteFixture.EditGolden(_directory, StationCase, outputs =>
        {
            var passes = outputs["passes"]!.AsArray();
            passes.Add(JsonNode.Parse(passes[^1]!.ToJsonString()));
        });

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert: a FAIL, not an ERROR: the count and the fields of the missing pass
        Assert.Equal("FAIL", result.Status);
        Assert.Equal(new[] { "passes", "passes[6].rise", "passes[6].set", "passes[6].duration_s", "passes[6].culmination", "passes[6].max_elevation_deg" },
            result.Measures.Where(m => !m.Passed).Select(m => m.Output));
    }

    [Fact]
    public void AnEventTypeTheSpecDoesNotDefine_IsAnError()
    {
        // Arrange
        SuiteFixture.Lot4.WriteSuite(_directory, EventsCase);
        var path = Path.Combine(_directory, EventsCase, "inputs.yaml");
        File.WriteAllText(path, File.ReadAllText(path).Replace("type: \"apoapsis\"", "type: \"perigee\""));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
    }

    [Fact]
    public void AnAnnularEclipse_IsListedBetweenItsPartialPhases()
    {
        // Arrange: the annular eclipse of 2026-02-17 crossed from a LEO
        SuiteFixture.Lot4.WriteSuite(_directory, AnnularCase);
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));

        // Act
        var outputs = new EclipseSolver(_directory).Solve(SuiteFixture.ReadCase(_directory, AnnularCase));

        // Assert: partial, annular, partial, each of the Moon, one following the other
        Assert.Equal(3, outputs["events"]);
        Assert.Equal(new[] { "penumbra", "antumbra", "penumbra" }, Enumerable.Range(0, 3).Select(k => outputs[$"events[{k}].type"]));
        Assert.All(Enumerable.Range(0, 3), k => Assert.Equal("Moon", outputs[$"events[{k}].body"]));
        for (int k = 0; k < 2; k++)
        {
            Assert.InRange(System.Math.Abs(((Time)outputs[$"events[{k + 1}].start"] - (Time)outputs[$"events[{k}].stop"]).TotalSeconds), 0.0, 1e-6);
        }
    }

    [Theory]
    [InlineData("none", Aberration.None)]
    [InlineData("lt", Aberration.LT)]
    [InlineData("lt+s", Aberration.LTS)]
    [InlineData("xlt", Aberration.XLT)]
    public void TheStationFlags_MapToTheCorrectionsOfSpice(string flag, Aberration aberration)
    {
        Assert.Equal(aberration, LightTimeFlags.Map(flag, LightTimeFlags.Station, "a case"));
    }

    [Fact]
    public void TheEclipseFlags_MapToTheCorrectionsOfSpice_AndAFlagOfAnotherCategoryIsRefused()
    {
        Assert.Equal(Aberration.None, LightTimeFlags.Map("none", LightTimeFlags.Eclipse, "a case"));
        Assert.Equal(Aberration.CN, LightTimeFlags.Map("cn", LightTimeFlags.Eclipse, "a case"));
        Assert.Throws<ArgumentException>(() => LightTimeFlags.Map("lt", LightTimeFlags.Eclipse, "a case"));
        Assert.Throws<ArgumentException>(() => LightTimeFlags.Map("cn", LightTimeFlags.Station, "a case"));
        Assert.Throws<ArgumentException>(() => LightTimeFlags.Map("LT", LightTimeFlags.Station, "a case"));
        Assert.Throws<ArgumentException>(() => LightTimeFlags.Map(null, LightTimeFlags.Station, "a case"));
    }

    [Theory]
    [InlineData("- type: \"perigee\"", "is not an event")]
    [InlineData("- type: \"altitude\"\n  value_km: 620.0\n  direction: \"both\"", "is not up, down or any")]
    [InlineData("- type: \"altitude\"\n  direction: \"up\"", "has no value_km")]
    [InlineData("- type: \"periapsis\"\n- type: \"periapsis\"", "is listed twice")]
    public void AnEventTheSpecDoesNotDefine_IsRefusedByTheParser(string events, string message)
    {
        var inputs = Inputs($"events:\n{Indent(events)}");
        var error = Assert.Throws<ArgumentException>(() => InputParser.ParseOrbitalEvents(inputs));
        Assert.Contains(message, error.Message);
        Assert.Throws<ArgumentException>(() => InputParser.ParseOrbitalEvents(Inputs("events: []")));
    }

    [Fact]
    public void AnAttitudeTheSpecDoesNotDefine_IsRefusedByTheParser()
    {
        const string common = """
                              epoch: "2026-03-01T00:00:00.000 UTC"
                              orbit: {type: "state_vector", position_km: [7000.0, 0.0, 0.0], velocity_km_s: [0.0, 7.5, 0.0]}
                              gm_km3_s2: 398600.4415
                              propagation_window: {start: "2026-03-01T00:00:00.000 UTC", end: "2026-03-01T01:00:00.000 UTC"}
                              sample_step_s: 60.0
                              """;
        var direction = Assert.Throws<ArgumentException>(() => InputParser.ParseAttitudeInputs(Inputs(
            common + "\nattitude: {pointing: {body_vector: [0.0, 1.0, 0.0], direction: \"zenith\"}}")));
        Assert.Contains("is not nadir, velocity or orbit_normal", direction.Message);
        var law = Assert.Throws<ArgumentException>(() => InputParser.ParseAttitudeInputs(Inputs(
            common + "\nattitude: {primary: {body_vector: [0.0, 1.0, 0.0], direction: \"nadir\"}}")));
        Assert.Contains("{primary, secondary} or {pointing}", law.Message);
        Assert.Throws<ArgumentException>(() => AttitudeSolver.Target("zenith"));
    }

    [Fact]
    public void TheSamplesOfAnAttitude_EndAtTheEndOfTheWindow_WhoseLengthIsAWholeNumberOfSteps()
    {
        var start = new Time(2026, 3, 1, 0, 0, 0, frame: TimeFrame.UTCFrame);
        Assert.Equal(121, AttitudeSolver.SampleCount(new Window(start, start.AddSeconds(7200.0)), 60.0));
        Assert.Throws<ArgumentException>(() => AttitudeSolver.SampleCount(new Window(start, start.AddSeconds(7230.0)), 60.0));
        Assert.Throws<ArgumentException>(() => AttitudeSolver.SampleCount(new Window(start, start.AddSeconds(7200.0)), 0.0));
    }

    [Fact]
    public void TheCrossingsOfWindows_LeaveOutTheBoundsOfTheSearch()
    {
        var s = new Time(2026, 1, 20, 6, 0, 0, frame: TimeFrame.TDBFrame);
        var search = new Window(s, s.AddSeconds(1000.0));
        var windows = new[] { new Window(s, s.AddSeconds(100.0)), new Window(s.AddSeconds(300.0), s.AddSeconds(400.0)), new Window(s.AddSeconds(900.0), s.AddSeconds(1000.0)) };

        Assert.Equal(new[] { s.AddSeconds(300.0), s.AddSeconds(900.0) }, OrbitalEventsSolver.Crossings(windows, search, true));
        Assert.Equal(new[] { s.AddSeconds(100.0), s.AddSeconds(400.0) }, OrbitalEventsSolver.Crossings(windows, search, false));
    }

    [Fact]
    public void AnOccultation_CutsThePassesItOverlaps()
    {
        var s = new Time(2023, 12, 15, 0, 0, 0, frame: TimeFrame.TDBFrame);
        Window W(double a, double b) => new(s.AddSeconds(a), s.AddSeconds(b));
        var passes = new[] { W(0.0, 100.0), W(200.0, 300.0), W(400.0, 500.0) };
        var hidden = new[] { W(50.0, 60.0), W(250.0, 350.0), W(390.0, 510.0) };

        Assert.Equal(new[] { W(0.0, 50.0), W(60.0, 100.0), W(200.0, 250.0) }, GroundStationSolver.Subtract(passes, hidden));
    }

    [Fact]
    public void TheOutputsOfAnIndexedArray_AreItsCountThenEachFieldOfEachElement()
    {
        // Arrange
        using var golden = JsonDocument.Parse("""
                                              {"events": {"periapsis": [{"elapsed_s": 1.0, "radius_km": 7000.0}], "apoapsis": []},
                                               "attitude_quaternions": [[1.0, 0.0, 0.0, 0.0]]}
                                              """);

        // Act
        var events = OutputComparer.Outputs(golden.RootElement, "orbital_events").Select(o => o.Output).ToList();
        var attitudes = OutputComparer.Outputs(golden.RootElement, "attitude").Select(o => o.Output).ToList();

        // Assert
        Assert.Equal(new[] { "events.periapsis", "events.periapsis[0].elapsed_s", "events.periapsis[0].radius_km", "events.apoapsis",
            "attitude_quaternions" }, events);
        Assert.Equal(new[] { "periapsis", "apoapsis", "attitude_quaternions", "attitude_quaternions[0]" }, attitudes);
        Assert.Equal("events.periapsis[].radius_km", MetricTable.Generic("events.periapsis[3].radius_km"));
        Assert.Equal("attitude_quaternions[]", MetricTable.Generic("attitude_quaternions[12]"));
        Assert.Equal("passes", MetricTable.IndexedArrayOf("ground_station", "passes[2].rise"));
        Assert.Null(MetricTable.IndexedArrayOf("ground_station", "arcs[2].rise"));
        Assert.Null(MetricTable.IndexedArrayOf("ground_station", null));
    }

    [Fact]
    public void TheDirectionAngle_IsResolvedAtSmallAngles()
    {
        double angle = 1e-9;
        var golden = new[] { 1.0, 0.0, 0.0 };
        var computed = new[] { System.Math.Cos(angle), System.Math.Sin(angle), 0.0 };

        Assert.Equal(angle, OutputComparer.DirectionAngle(computed, golden), 1e-22);
        Assert.Equal(System.Math.PI, OutputComparer.DirectionAngle(new[] { -1.0, 0.0, 0.0 }, golden), 15);
        Assert.Equal(0.5 * System.Math.PI, OutputComparer.DirectionAngle(new[] { 0.0, 0.0, 1.0 }, golden), 15);
    }

    [Fact]
    public void ACountAndAStringOfTheGolden_AreComparedExactly()
    {
        using var golden = JsonDocument.Parse("""{"n": [1, 2], "s": "Moon"}""");
        Assert.True(OutputComparer.ExactMeasure("n", 2, golden.RootElement.GetProperty("n")).Passed);
        Assert.False(OutputComparer.ExactMeasure("n", 3, golden.RootElement.GetProperty("n")).Passed);
        Assert.True(OutputComparer.ExactMeasure("s", "Moon", golden.RootElement.GetProperty("s")).Passed);
        Assert.False(OutputComparer.ExactMeasure("s", "Earth", golden.RootElement.GetProperty("s")).Passed);
    }

    // The outputs of a golden, in its order: an indexed array, its count, then each field of each element
    private static IEnumerable<string> ExpectedOutputs(string category, JsonElement outputs)
    {
        foreach (var property in outputs.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var list in property.Value.EnumerateObject())
                {
                    foreach (var output in Elements($"{property.Name}.{list.Name}", list.Value))
                    {
                        yield return output;
                    }
                }

                continue;
            }

            foreach (var output in property.Value.ValueKind == JsonValueKind.Array ? Elements(property.Name, property.Value) : new[] { property.Name })
            {
                yield return output;
            }
        }
    }

    private static IEnumerable<string> Elements(string array, JsonElement elements)
    {
        yield return array;
        int k = 0;
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in element.EnumerateObject())
                {
                    yield return $"{array}[{k}].{field.Name}";
                }
            }
            else
            {
                yield return $"{array}[{k}]";
            }

            k++;
        }
    }

    private static Dictionary<string, object> Inputs(string yaml)
    {
        return new YamlDotNet.Serialization.Deserializer().Deserialize<Dictionary<string, object>>(yaml);
    }

    private static string Indent(string text) => string.Join("\n", text.Split('\n').Select(line => "  " + line));
}
