// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The conversion categories of lot 1 (design of #458): orbital_elements, time_conversion and frame_transformation,
/// with GMAT R2026a goldens, run by the whole runner on the fixture <c>Fixture/Lot1</c> synced from the suite.
/// </summary>
/// <remarks>
/// Each test writes its own suite from the fixture (<see cref="Lot1Fixture"/>), so that its edits touch no other; the
/// suite is deleted at the end. Only <see cref="EveryCaseOfTheFixture_Passes"/> prints <c>MEASURE</c> lines, one per
/// compared output, which the CI logs of the three platforms carry and from which the tolerances are set (design of
/// #458, sections 2 and 10); the negative tests, whose goldens are edited, print none.
/// </remarks>
public sealed class Lot1CaseTests : IDisposable
{
    private const string KernelsPath = Lot1Fixture.KernelsPath;
    private const string Kernel = "earth_latest_high_prec.bpc";
    private const string Pointer = SuiteDataFile.LfsPointerHeader + "v1\noid sha256:3f6ce47a0a\nsize 4939776\n";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public Lot1CaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("lot1-fixture-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryCaseOfTheFixture_Passes()
    {
        // Arrange
        Lot1Fixture.WriteSuite(_directory);

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
        Assert.Equal(Lot1Fixture.CaseDirectories().Count, report.Summary.Passed);
        Assert.All(Lot1Fixture.Categories, category => Assert.Contains(report.Results, r => r.Category == category));

        // Every output of every golden is compared, in the order of the golden
        foreach (var caseDirectory in Lot1Fixture.CaseDirectories())
        {
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, caseDirectory, "expected-result.json")));
            var result = report.Results.Single(r => r.CaseId == expected.RootElement.GetProperty("id").GetString());
            Assert.Equal(expected.RootElement.GetProperty("outputs").EnumerateObject().Select(p => p.Name),
                result.Measures.Select(m => m.Output));
        }

        // The rows of accuracy_by_metric by category, in the units of spec.md section 11
        Assert.Contains(report.Summary.AccuracyByMetric, m => m.Metric == "orbital_elements/semi_major_axis_km" && m.Unit == "km");
        Assert.Contains(report.Summary.AccuracyByMetric, m => m.Metric == "time_conversion/tdb_s" && m.Unit == "s");
        Assert.Contains(report.Summary.AccuracyByMetric, m => m.Metric == "frame_transformation/velocity_km_s" && m.Unit == "km/s");
        Assert.DoesNotContain(report.Summary.AccuracyByMetric, m => m.Metric == "semi_major_axis_km");
    }

    [Theory]
    [InlineData(Lot1Fixture.ElementsCase, "semi_major_axis_km", -1, MetricKind.Scalar, "oe_length_km")] // relative key
    [InlineData(Lot1Fixture.ElementsCase, "eccentricity", -1, MetricKind.Scalar, "oe_eccentricity")] // absolute key
    [InlineData(Lot1Fixture.ElementsCase, "raan_deg", -1, MetricKind.WrappedAngle, "oe_angle_deg")]
    [InlineData(Lot1Fixture.ElementsCase, "declination_deg", -1, MetricKind.Angle, "oe_angle_deg")]
    [InlineData(Lot1Fixture.ElementsCase, "angular_momentum_km2_s", 2, MetricKind.Vector3, "oe_angular_momentum_km2_s")] // relative
    [InlineData(Lot1Fixture.FrameCase, "velocity_km_s", 0, MetricKind.Vector3, "frame_velocity_km_s")] // absolute
    [InlineData(Lot1Fixture.TimeCase, "tdb_s", -1, MetricKind.Seconds, MetricTable.TimeKey)]
    [InlineData(Lot1Fixture.TimeCase, "tai_s", -1, MetricKind.Seconds, MetricTable.TdbTimeKey)] // crossing TDB-TT
    public void AGoldenMovedByTwiceItsTolerance_FailsThatOutputOnly(string caseDirectory, string output, int component, MetricKind kind,
        string key)
    {
        // Arrange: the golden moved by 2 T, T the active threshold of the output (relative: 2 T times the golden)
        Lot1Fixture.WriteSuite(_directory, caseDirectory);
        var (tolerance, _) = OutputComparer.ResolveTolerance(output, key,
            Lot1Fixture.ReadCase(_directory, caseDirectory).TolerancesOverride, Lot1Fixture.ReadTolerances(_directory).Defaults);
        bool absolute = tolerance.RelTol == 0.0;
        Lot1Fixture.EditGolden(_directory, caseDirectory, outputs =>
        {
            if (component < 0)
            {
                double golden = outputs[output]!.GetValue<double>();
                outputs[output] = golden + 2.0 * (absolute ? tolerance.AbsTol : tolerance.RelTol * System.Math.Abs(golden));
            }
            else
            {
                var vector = outputs[output]!.AsArray();
                double norm = System.Math.Sqrt(vector.Sum(x => x!.GetValue<double>() * x.GetValue<double>()));
                vector[component] = vector[component]!.GetValue<double>() +
                                    2.0 * (absolute ? tolerance.AbsTol : tolerance.RelTol * norm);
            }
        });

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert: not a MEASURE line, which would stand beside the genuine one of the same output
        var measure = result.Measures.Single(m => m.Output == output);
        _output.WriteLine($"moved golden {result.CaseId} {output}: ratio={measure.Ratio.ToString("R", CultureInfo.InvariantCulture)} key={measure.ToleranceName}");
        Assert.Equal("FAIL", result.Status);
        Assert.False(result.Deltas[output].Passed);
        Assert.All(result.Deltas.Where(d => d.Key != output), d => Assert.True(d.Value.Passed, d.Key));
        Assert.Equal(kind, measure.Kind);
        Assert.Equal(key, measure.ToleranceName);
        Assert.InRange(measure.Ratio, 1.0 + 1e-6, 3.0);
    }

    [Fact]
    public void ANullGolden_IsSkipped()
    {
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.ElementsCase);
        Lot1Fixture.EditGolden(_directory, Lot1Fixture.ElementsCase, outputs => outputs["raan_deg"] = null);

        Assert.Equal("SKIP", new Runner(_directory, KernelsPath).Run().Results.Single().Status);
    }

    [Fact]
    public void ANullVectorComponentInTheGolden_IsSkipped()
    {
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.FrameCase);
        Lot1Fixture.EditGolden(_directory, Lot1Fixture.FrameCase, outputs => outputs["position_km"]!.AsArray()[1] = null);

        Assert.Equal("SKIP", new Runner(_directory, KernelsPath).Run().Results.Single().Status);
    }

    [Fact]
    public void AGoldenOutputUnknownToTheSchema_IsASchemaError()
    {
        // Arrange
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.ElementsCase);
        Lot1Fixture.EditGolden(_directory, Lot1Fixture.ElementsCase, outputs => outputs["c3_energy_km2_s2"] = 120.0);

        // Act: a case that fails the validation is named by its directory
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("expected result", result.Message);
    }

    [Fact]
    public void AToleranceKeyMissingFromTolerancesYaml_IsAnError_WithoutFallback()
    {
        // Arrange: oe_angle_deg removed from the defaults
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.ElementsCase);
        var tolerances = Lot1Fixture.ReadTolerances(_directory);
        Assert.True(tolerances.Defaults.Remove("oe_angle_deg"));
        File.WriteAllText(Path.Combine(_directory, "tolerances.yaml"), new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build().Serialize(tolerances));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("oe_angle_deg", result.Message);
        Assert.Contains("tolerances.yaml", result.Message);
    }

    [Fact]
    public void AGoldenOutputTheSolverCannotProduce_IsAnError()
    {
        // Arrange: a hyperbolic anomaly in the golden of an ellipse, which the schema accepts
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.ElementsCase);
        Lot1Fixture.EditGolden(_directory, Lot1Fixture.ElementsCase, outputs => outputs["hyperbolic_anomaly_deg"] = 10.0);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("hyperbolic_anomaly_deg", result.Message);
        Assert.Contains("does not produce", result.Message);
    }

    [Fact]
    public void AGeodeticGoldenOutputWithoutEllipsoid_IsAnError()
    {
        // Arrange
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.FrameCase);
        Lot1Fixture.EditGolden(_directory, Lot1Fixture.FrameCase, outputs => outputs["latitude_deg"] = 10.0);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("latitude_deg", result.Message);
        Assert.Contains("ellipsoid", result.Message);
    }

    [Fact]
    public void AGravitationalParameterMovedByOneUlp_IsAnError()
    {
        // Arrange: 398600.43550702266, the decimal of the kernel, is 1 ulp above the double CSPICE reads
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.ElementsCase);
        var path = Path.Combine(_directory, Lot1Fixture.ElementsCase, "inputs.yaml");
        var yaml = File.ReadAllText(path);
        var gm = Regex.Match(yaml, @"gm_km3_s2: ([0-9.eE+-]+)");
        double moved = System.Math.BitIncrement(double.Parse(gm.Groups[1].Value, CultureInfo.InvariantCulture));
        File.WriteAllText(path, yaml.Replace(gm.Value, "gm_km3_s2: " + moved.ToString("R", CultureInfo.InvariantCulture)));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("gravitational parameter", result.Message);
    }

    [Fact]
    public void AMissingKernel_IsAnError()
    {
        // Arrange
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.FrameCase);
        File.Delete(Path.Combine(_directory, "data", Kernel));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains(Kernel, result.Message);
        Assert.Contains("does not exist", result.Message);
    }

    [Fact]
    public void AKernelLeftAsAGitLfsPointer_IsAnError_AndIsNotLoaded()
    {
        // Arrange
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.FrameCase);
        var kernel = Path.Combine(_directory, "data", Kernel);
        File.WriteAllText(kernel, Pointer);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("git lfs pull", result.Message);
        Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(), k => k.FullName == new FileInfo(kernel).FullName);
    }

    [Fact]
    public void AnEpochAtSecond60_IsAnError()
    {
        // Arrange: the instant of a leap second is given in TAI (spec.md section 3)
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.TimeCase);
        var path = Path.Combine(_directory, Lot1Fixture.TimeCase, "inputs.yaml");
        var yaml = File.ReadAllText(path);
        var epoch = Regex.Match(yaml, "epoch: \"[^\"]+\"");
        File.WriteAllText(path, yaml.Replace(epoch.Value, "epoch: \"2016-12-31T23:59:60.000 UTC\""));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert: the schema rejects it before the parser, which rejects it too (EpochParserTests)
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("case input", result.Message);
    }

    [Fact]
    public void AnOverrideNamingTheOutput_PrecedesAnOverrideNamingItsKey_WhichPrecedesTheDefault()
    {
        // Arrange
        Lot1Fixture.WriteSuite(_directory, Lot1Fixture.ElementsCase);
        var path = Path.Combine(_directory, Lot1Fixture.ElementsCase, "inputs.yaml");
        File.AppendAllText(path, """

                                 tolerances_override:
                                   raan_deg:
                                     abs_tol: 1.0e-9
                                     rel_tol: 0.0
                                     justification: "An override naming the output, written by the test."
                                   oe_angle_deg:
                                     abs_tol: 1.0e-8
                                     rel_tol: 0.0
                                     justification: "An override naming the key, written by the test."

                                 """);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("PASS", result.Status);
        var raan = result.Measures.Single(m => m.Output == "raan_deg");
        Assert.Equal(("raan_deg", 1e-9), (raan.ToleranceName, raan.Threshold));
        var argp = result.Measures.Single(m => m.Output == "argp_deg");
        Assert.Equal(("oe_angle_deg", 1e-8), (argp.ToleranceName, argp.Threshold));
        var sma = result.Measures.Single(m => m.Output == "semi_major_axis_km");
        Assert.Equal(("oe_length_km", Lot1Fixture.ReadTolerances(_directory).Defaults["oe_length_km"].RelTol),
            (sma.ToleranceName, sma.Threshold));
    }
}
