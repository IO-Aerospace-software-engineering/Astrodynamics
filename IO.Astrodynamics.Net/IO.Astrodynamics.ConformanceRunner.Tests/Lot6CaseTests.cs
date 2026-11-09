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
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The conjunction cases of lot 6 (design of lot 6 of #457): the TCA, the relative state and the covariance at TCA
/// against GMAT R2026a and CSPICE, the 2D Pc against CARA's definition at 50 digits, the CDM read and written against
/// its decimal text, and the state transition matrix against GMAT, run by the whole runner on the fixture
/// <c>Fixture/Lot6</c> synced from the suite (<see cref="SuiteFixture.Lot6"/>); and the parts of the solvers and of the
/// comparer a case of the suite does not reach: a count of minima that differs, an absent covariance, an eigenvalue the
/// framework clipped, the kinds of spec.md section 6.1 the lot adds.
/// </summary>
/// <remarks>
/// Each test that runs the runner writes its own suite from the fixture, deleted at the end. Only
/// <see cref="EveryCaseOfTheFixture_Passes"/> prints <c>MEASURE</c> lines, one per compared output, which the CI logs of
/// the three platforms carry: the framework's margin to the tolerance of each output.
/// </remarks>
public sealed class Lot6CaseTests : IDisposable
{
    private const string KernelsPath = SuiteFixture.KernelsPath;
    private const string AllMinimaCase = "cases/conjunction/008";
    private const string ClippedCase = "cases/collision_probability/006";
    private const string FrisbeeCase = "cases/collision_probability/007";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public Lot6CaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("lot6-fixture-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryCaseOfTheFixture_Passes()
    {
        // Arrange
        SuiteFixture.Lot6.WriteSuite(_directory);

        // Act
        var report = new Runner(_directory, KernelsPath).Run();

        // Assert: the measures first, so that a failing case still prints them, with the worst ratio of each case
        foreach (var result in report.Results)
        {
            foreach (var measure in result.Measures)
            {
                _output.WriteLine(measure.ToMeasureLine(result.CaseId));
            }

            var worst = result.Measures.Where(m => !double.IsNaN(m.Ratio)).OrderByDescending(m => m.Ratio).FirstOrDefault();
            _output.WriteLine($"WORST {result.CaseId} {worst?.Output} ratio={worst?.Ratio:R}");
            if (result.Category == "stm")
            {
                _output.WriteLine($"WORST {result.CaseId} final_stm block={StmComparer.BlockNames.Max(b => result.Deltas[b].MaxRelDelta):R}");
            }
        }

        Assert.All(report.Results, r => Assert.True(r.Status == "PASS", $"{r.CaseId}: {r.Status} {r.Message}"));
        Assert.Equal(25, report.Summary.Passed);
        Assert.Equal(SuiteFixture.Lot6.CaseDirectories().Count, report.Summary.Total);

        // Every output is compared against the override of its key when the case has one, the default of its key
        // otherwise, an exact output against nothing; a cdm export by the table of the fields it writes
        var defaults = SuiteFixture.ReadTolerances(_directory).Defaults;
        foreach (var caseDirectory in SuiteFixture.Lot6.CaseDirectories())
        {
            var caseInput = SuiteFixture.ReadCase(_directory, caseDirectory);
            var result = report.Results.Single(r => r.CaseId == caseInput.Id);
            string table = MetricTable.Table(caseInput.Category, caseInput.Inputs);
            foreach (var measure in result.Measures)
            {
                Assert.True(MetricTable.TryGet(table, measure.Output, out var metric), measure.Output);
                if (metric.Kind is MetricKind.Exact)
                {
                    Assert.Equal("exact", measure.ToleranceName);
                    continue;
                }

                var overrides = caseInput.TolerancesOverride ?? new Dictionary<string, TolerancePair>();
                Assert.Equal(metric.Key, measure.ToleranceName);
                var tolerance = overrides.TryGetValue(metric.Key, out var o) ? o : defaults[metric.Key];
                Assert.Equal(tolerance.RelTol == 0.0 ? tolerance.AbsTol : tolerance.RelTol, measure.Threshold);
            }
        }

        // The matrix of each stm case by its four blocks
        Assert.All(report.Results.Where(r => r.Category == "stm"),
            r => Assert.All(StmComparer.BlockNames, block => Assert.True(r.Deltas[block].Passed, $"{r.CaseId} {block}")));
    }

    [Fact]
    public void AMinimumMissingFromTheComputedList_FailsTheCount()
    {
        // Arrange: the golden of 008 with a sixteenth minimum, a copy of its last
        SuiteFixture.Lot6.WriteSuite(_directory, AllMinimaCase);
        SuiteFixture.EditGolden(_directory, AllMinimaCase, outputs =>
        {
            var minima = outputs["minima"]!.AsArray();
            minima.Add(minima[^1]!.DeepClone());
        });

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert: a FAIL of the count and of the element the solver does not give, every other minimum compared
        Assert.Equal("FAIL", result.Status);
        Assert.False(result.Measures.Single(m => m.Output == "minima").Passed);
        Assert.False(result.Measures.Single(m => m.Output == "minima[15].tca_s").Passed);
        Assert.All(result.Measures.Where(m => m.Output != "minima" && !m.Output.StartsWith("minima[15]")), m => Assert.True(m.Passed, m.Output));
    }

    [Fact]
    public void AnAbsentCovariance_GivesFrisbeesMaximum_AndTwoGiveThePc()
    {
        // Arrange: 007, object 2 without covariance; then with a zero covariance
        var caseInput = SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot6, FrisbeeCase);
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));
        var solver = new CollisionProbabilitySolver();

        // Act
        var maximum = solver.Solve(caseInput);
        var objects = (List<object>)caseInput.Inputs["objects"];
        ((Dictionary<object, object>)objects[1])["covariance_rtn"] = Enumerable.Repeat((object)"0.0", 6).ToList();
        var both = solver.Solve(caseInput);

        // Assert: the maximum Pc with one covariance, the Pc with two; the sigmas are those of the given covariance, without
        // Frisbee's addition, which raises the Pc
        Assert.True(maximum.ContainsKey("maximum_collision_probability"));
        Assert.False(maximum.ContainsKey("collision_probability"));
        Assert.True(both.ContainsKey("collision_probability"));
        Assert.False(both.ContainsKey("maximum_collision_probability"));
        Assert.True((double)maximum["maximum_collision_probability"] > (double)both["collision_probability"]);
        Assert.Equal(both["encounter_plane_sigma_major_km"], maximum["encounter_plane_sigma_major_km"]);
        Assert.Equal(both["encounter_plane_sigma_minor_km"], maximum["encounter_plane_sigma_minor_km"]);
    }

    [Fact]
    public void ASigmaTheFrameworkClipped_IsAnErrorThatSaysSo()
    {
        // Arrange: the golden of 006 with the minor sigma, whose eigenvalue (−1 m²) the framework raises to the clip
        SuiteFixture.Lot6.WriteSuite(_directory, ClippedCase);
        SuiteFixture.EditGolden(_directory, ClippedCase, outputs => outputs["encounter_plane_sigma_minor_km"] = 1e-9);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("to the clip", result.Message);
    }

    [Fact]
    public void AConjunctionThatPropagatesOneObjectAndReadsTheOther_IsRefusedByTheParser()
    {
        var inputs = SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot6, "cases/conjunction/002").Inputs;
        ((List<object>)inputs["objects"])[1] = new Dictionary<object, object> { ["name"] = "object2", ["naif_id"] = "-10002" };

        var error = Assert.Throws<ArgumentException>(() => InputParser.ParseConjunctionInputs(inputs));
        Assert.Contains("two of a kind", error.Message);

        inputs = SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot6, "cases/conjunction/002").Inputs;
        inputs["report"] = "farthest";
        Assert.Contains("closest", Assert.Throws<ArgumentException>(() => InputParser.ParseConjunctionInputs(inputs)).Message);
    }

    [Fact]
    public void ACdmOperationOutsideTheSpec_IsRefusedByTheParser()
    {
        var inputs = SuiteFixture.ReadCaseFromFixture(SuiteFixture.Lot6, "cases/cdm/001").Inputs;
        inputs["operation"] = "encode";
        Assert.Contains("is not decode, to_inertial or export", Assert.Throws<ArgumentException>(() => InputParser.ParseCdmInputs(inputs)).Message);

        inputs["operation"] = "decode";
        inputs["cdm_file"] = "../cdm.xml";
        Assert.Contains("not a file name", Assert.Throws<ArgumentException>(() => InputParser.ParseCdmInputs(inputs)).Message);
    }

    [Fact]
    public void TheArrayAndCovarianceKinds_FollowSpecSection61()
    {
        using var golden = JsonDocument.Parse("""{"s": [2.0, 4.0, 0.0], "c": [4.0, 1.0, 9.0], "r": [0.1, -0.2, 0.3, 0.4]}""");
        var root = golden.RootElement;

        // relative per component: the largest |d|/|g|, a zero golden only on an exact zero
        Assert.Equal(0.5, OutputComparer.Deltas(MetricKind.Relative, new[] { 3.0, 4.0, 0.0 }, root.GetProperty("s"), "s").Relative);
        Assert.Equal(double.PositiveInfinity, OutputComparer.Deltas(MetricKind.Relative, new[] { 2.0, 4.0, 1e-300 }, root.GetProperty("s"), "s").Relative);

        // scalar per component of any length
        Assert.Equal(0.5, OutputComparer.Deltas(MetricKind.ScalarPerComponent, new[] { 0.1, -0.2, 0.3, -0.1 }, root.GetProperty("r"), "r").Absolute, 1e-15);

        // normalized covariance: |ΔC_ij| / √(C_ii C_jj) of the lower triangle [C00, C10, C11]
        Assert.Equal(0.5, OutputComparer.Deltas(MetricKind.NormalizedCovariance, new[] { 4.0, 4.0, 9.0 }, root.GetProperty("c"), "c").Absolute, 1e-15);
        Assert.Equal(0.25, OutputComparer.Deltas(MetricKind.NormalizedCovariance, new[] { 5.0, 1.0, 9.0 }, root.GetProperty("c"), "c").Absolute, 1e-15);

        // a length that differs is no comparison
        Assert.Throws<InvalidOperationException>(() => OutputComparer.Deltas(MetricKind.Relative, new[] { 1.0 }, root.GetProperty("s"), "s"));
        Assert.Throws<InvalidOperationException>(() => OutputComparer.NormalizedCovarianceDelta(new[] { 1.0, 0.0, 1.0 }, new[] { 0.0, 0.0, 1.0 }));
    }

    [Fact]
    public void TheLowerTriangle_IsTheOrderOfCcsds508()
    {
        // CR_R, CT_R, CT_T: the symmetric 2x2 [[1, 2], [2, 9]]; sigmas 1, 3 and the correlation 2/3
        var matrix = LowerTriangle.ToMatrix(new[] { 1.0, 2.0, 9.0 });
        Assert.Equal(2.0, matrix.Get(0, 1));
        Assert.Equal(new[] { 1.0, 2.0, 9.0 }, LowerTriangle.Of(matrix));
        var (sigmas, correlations) = LowerTriangle.SigmasAndCorrelations(matrix, 1e-3);
        Assert.Equal(1e-3, sigmas[0], 1e-18);
        Assert.Equal(3e-3, sigmas[1], 1e-18);
        Assert.Equal(2.0 / 3.0, correlations.Single(), 1e-16);
        Assert.Throws<ArgumentException>(() => LowerTriangle.Order(4));
        Assert.Equal((4.0, 1.0), CollisionProbabilitySolver.Eigenvalues(LowerTriangle.ToMatrix(new[] { 4.0, 0.0, 1.0 })));
    }

    [Fact]
    public void TheTableOfACdmExport_IsItsOwn()
    {
        Assert.Equal(MetricTable.CdmExportTable, MetricTable.Table("cdm", new Dictionary<string, object> { ["operation"] = "export" }));
        Assert.Equal("cdm", MetricTable.Table("cdm", new Dictionary<string, object> { ["operation"] = "decode" }));
        Assert.True(MetricTable.TryGet(MetricTable.CdmExportTable, "objects[1].covariance_rtn", out var written));
        Assert.True(MetricTable.TryGet("cdm", "objects[1].covariance_rtn", out var decoded));
        Assert.Equal(MetricKind.NormalizedCovariance, written.Kind);
        Assert.Equal("cdm_value_rel", decoded.Key);
        Assert.True(MetricTable.IsIndexed("conjunction", "minima"));
    }
}
