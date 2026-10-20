// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The <c>stm</c> category of the conformance runner (F4 of phase 2, feature 1; design of #408), run on the fixture
/// written by <c>tools/gmat_reference/make_stm_case.py</c>: the schemas of the conformance-tests suite extended with the
/// category, and the case <c>stm_24h_leo_twobody_001</c>, whose golden is GMAT R2026a's Φ of the reference case R1
/// turned to ICRF, an independent value; and the two-body propagator case 004 of the suite, unchanged.
/// </summary>
/// <remarks>
/// Each test runs the runner on its own copy of the fixture, so that the variants (a perturbed, a missing or a
/// malformed golden) do not touch the others; the copies are deleted at the end.
/// </remarks>
public sealed class StmCaseTests : IDisposable
{
    private const string StmCase = "stm_24h_leo_twobody_001";
    private const string KernelsPath = "Data/SolarSystem";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public StmCaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Path.Combine(Path.GetTempPath(), "stm-fixture-" + Guid.NewGuid().ToString("N"));
        Copy("Fixture", _directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void StmCase_AgainstTheGmatGolden_Passes_AndThePropagatorCaseIsUnchanged()
    {
        // Act
        var report = new Runner(_directory, KernelsPath).Run();

        // Assert
        var stm = report.Results.Single(r => r.CaseId == StmCase);
        foreach (var block in StmComparer.BlockNames)
        {
            _output.WriteLine($"{block}: relative {stm.Deltas[block].MaxRelDelta:E2}, absolute {stm.Deltas[block].MaxAbsDelta:E2}");
        }

        Assert.Equal("PASS", stm.Status);
        // The golden of GMAT against the library: 7.6e-12 at most over the day (GmatReferenceTests)
        Assert.All(StmComparer.BlockNames, block => Assert.True(stm.Deltas[block].MaxRelDelta < 1e-10, block));
        Assert.All(new[] { "final_x_km", "final_vx_km_s" }, metric => Assert.True(stm.Deltas[metric].Passed, metric));
        Assert.Equal("PASS", report.Results.Single(r => r.CaseId == "propagator_1sd_geo_twobody_004").Status);
        Assert.Equal(2, report.Summary.Passed);
        Assert.Contains(report.Summary.AccuracyByMetric, m => m.Metric == "stm_rv" && m.Unit == "-");
    }

    [Fact]
    public void StmCase_WithOneElementOfTheGoldenPerturbed_FailsOnItsBlockOnly()
    {
        // Arrange: Φ_rv[0][0] (row 0, column 3) of the golden moved by 1e-8 of the norm of its block
        EditGolden(outputs =>
        {
            var rv = Block(outputs, 0, 3);
            double norm = System.Math.Sqrt(rv.Sum(x => x * x));
            var row = outputs["final_stm"]![0]!.AsArray();
            row[3] = row[3]!.GetValue<double>() + 1e-8 * norm;
        });

        // Act
        var stm = new Runner(_directory, KernelsPath).Run().Results.Single(r => r.CaseId == StmCase);

        // Assert
        Assert.Equal("FAIL", stm.Status);
        Assert.False(stm.Deltas["stm_rv"].Passed);
        Assert.InRange(stm.Deltas["stm_rv"].MaxRelDelta, 0.9e-8, 1.1e-8);
        Assert.All(new[] { "stm_rr", "stm_vr", "stm_vv" }, block => Assert.True(stm.Deltas[block].Passed, block));
    }

    [Fact]
    public void StmCase_WithANullElementInTheGolden_IsSkipped()
    {
        // Arrange
        EditGolden(outputs => outputs["final_stm"]![2]!.AsArray()[2] = null);

        // Act
        var stm = new Runner(_directory, KernelsPath).Run().Results.Single(r => r.CaseId == StmCase);

        // Assert
        Assert.Equal("SKIP", stm.Status);
    }

    [Fact]
    public void StmCase_WithAGoldenOfFiveRows_FailsTheSchema()
    {
        // Arrange
        EditGolden(outputs => outputs["final_stm"]!.AsArray().RemoveAt(5));

        // Act: a case that fails the validation is named by its directory, its inputs not read
        var stm = new Runner(_directory, KernelsPath).Run().Results.Single(r => r.CaseId == "001");

        // Assert
        Assert.Equal("ERROR", stm.Status);
        Assert.Contains("expected result", stm.Message);
    }

    [Fact]
    public void StmComparer_ZeroBlocks_PassAndAMalformedGoldenThrows()
    {
        // Arrange
        var identity = Enumerable.Range(0, 36).Select(k => k / 6 == k % 6 ? 1.0 : 0.0).ToArray();
        var golden = JsonDocument.Parse(JsonSerializer.Serialize(
            Enumerable.Range(0, 6).Select(i => identity.Skip(6 * i).Take(6).ToArray()).ToArray())).RootElement;
        var tolerance = new TolerancePair { AbsTol = 0.0, RelTol = 1e-12 };

        // Act
        var deltas = StmComparer.Compare(identity, golden, tolerance);
        var shifted = StmComparer.Compare(identity.Select((x, k) => k == 3 ? 1e-30 : x).ToArray(), golden, tolerance);

        // Assert: zero blocks against zero blocks pass (relative 0); against a zero golden block, any departure has an
        // infinite relative delta and passes only by abs_tol
        Assert.All(deltas.Values, d => Assert.True(d.Passed));
        Assert.Equal(0.0, deltas["stm_rv"].MaxRelDelta);
        Assert.True(double.IsPositiveInfinity(shifted["stm_rv"].MaxRelDelta));
        Assert.False(shifted["stm_rv"].Passed);
        var fiveRows = JsonDocument.Parse("[[1,0,0,0,0,0],[0,1,0,0,0,0],[0,0,1,0,0,0],[0,0,0,1,0,0],[0,0,0,0,1,0]]")
            .RootElement;
        Assert.Throws<ArgumentException>(() => StmComparer.Compare(identity, fiveRows, tolerance));
        var shortRow = JsonDocument.Parse("[[1,0,0,0,0,0],[0,1,0,0,0,0],[0,0,1,0,0,0],[0,0,0,1,0,0],[0,0,0,0,1,0],[0]]")
            .RootElement;
        Assert.Throws<ArgumentException>(() => StmComparer.Compare(identity, shortRow, tolerance));
    }

    [Fact]
    public void StmSolver_HandlesTheStmCategory()
    {
        Assert.Equal("stm", new IO.Astrodynamics.ConformanceRunner.Solvers.StmSolver(KernelsPath).Category);
    }

    private void EditGolden(Action<JsonObject> edit)
    {
        var path = Path.Combine(_directory, "cases", "stm", "001", "expected-result.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(document["outputs"]!.AsObject());
        File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static double[] Block(JsonObject outputs, int rowOffset, int columnOffset)
    {
        var rows = outputs["final_stm"]!.AsArray();
        return Enumerable.Range(0, 9)
            .Select(k => rows[rowOffset + k / 3]![columnOffset + k % 3]!.GetValue<double>()).ToArray();
    }

    private static void Copy(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }
}
