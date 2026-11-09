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
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The <c>stm</c> category of the conformance runner (F4 of phase 2, feature 1; design of #408), on the case
/// <c>stm_24h_leo_twobody_001</c> of the suite (spec.md section 16.4; conformance-tests#10, absorbed by lot 6 of #457),
/// synced in <c>Fixture/Lot6</c> (<see cref="SuiteFixture.Lot6"/>): its golden is GMAT R2026a's Φ of the reference case
/// R1 on the suite's Earth turned to ICRF, an independent value. The final state takes the keys of the propagator's
/// (spec.md section 12.1). The three cases of the category are run by <see cref="Lot6CaseTests"/>.
/// </summary>
/// <remarks>
/// Each test runs the runner on its own suite written from the fixture, so that the variants (a perturbed, a missing or
/// a malformed golden) do not touch the others; the suites are deleted at the end.
/// </remarks>
public sealed class StmCaseTests : IDisposable
{
    private const string StmCase = "stm_24h_leo_twobody_001";
    private const string StmCaseDirectory = "cases/stm/001";
    private const string KernelsPath = SuiteFixture.KernelsPath;

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public StmCaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("stm-fixture-").FullName;
        SuiteFixture.Lot6.WriteSuite(_directory, StmCaseDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void StmCase_AgainstTheGmatGolden_Passes()
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
        // The golden of GMAT against the library: 7.6e-12 at most over the day (GmatReferenceTests), 8.0e-12 on the
        // golden regenerated on the suite's Earth (Linux x64, 2026-11-09)
        Assert.All(StmComparer.BlockNames, block => Assert.True(stm.Deltas[block].MaxRelDelta < 1e-10, block));
        Assert.All(new[] { "final_x_km", "final_vx_km_s" }, metric => Assert.True(stm.Deltas[metric].Passed, metric));
        Assert.Equal(1, report.Summary.Passed);
        Assert.Contains(report.Summary.AccuracyByMetric, m => m.Metric == "stm_rv" && m.Unit == "-");
    }

    [Fact]
    public void StmCase_WithOneElementOfTheGoldenPerturbed_FailsOnItsBlockOnly()
    {
        // Arrange: Φ_rv[0][0] (row 0, column 3) of the golden moved by 1e-6 of the norm of its block, ten times stm_block
        EditGolden(outputs =>
        {
            var rv = Block(outputs, 0, 3);
            double norm = System.Math.Sqrt(rv.Sum(x => x * x));
            var row = outputs["final_stm"]![0]!.AsArray();
            row[3] = row[3]!.GetValue<double>() + 1e-6 * norm;
        });

        // Act
        var stm = new Runner(_directory, KernelsPath).Run().Results.Single(r => r.CaseId == StmCase);

        // Assert
        Assert.Equal("FAIL", stm.Status);
        Assert.False(stm.Deltas["stm_rv"].Passed);
        Assert.InRange(stm.Deltas["stm_rv"].MaxRelDelta, 0.9e-6, 1.1e-6);
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
    public void StmSolver_GivesTheStmAndTheStateOfAPropagationWithOutputsAtTheStepOfTheCase()
    {
        // Arrange: the stm case of the fixture, and the same propagation with an output at every step of the case, as
        // the solver did before #426
        CaseInput Case() => new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build()
            .Deserialize<CaseInput>(File.ReadAllText(Path.Combine(_directory, StmCaseDirectory, "inputs.yaml")));
        Dictionary<string, object> computed;
        using (new CaseKernels(_directory, InputParser.ParseKernels(Case().Inputs)))
        {
            computed = new StmSolver(_directory).Solve(Case());
        }

        using var kernels = new CaseKernels(_directory, InputParser.ParseKernels(Case().Inputs));
        var reference = PropagationCase.Build(Case(), Path.Combine(_directory, "data"));
        var solution = new CentralBodyPropagator(reference.Window, reference.Spacecraft,
            PropagatorSolver.CreateIntegrator(), reference.Bodies, reference.Drag,
            reference.SolarRadiationPressure, reference.StepSize,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly }).Propagate();
        var end = reference.Window.EndDate;
        var phi = solution.GetStateTransitionMatrix(end);
        var (position, velocity) = solution.InterpolateAt(end);

        // Assert: the same, bit for bit, since the accepted steps do not depend on the outputs
        Assert.True(reference.StepSize < reference.Window.Length);
        var stm = (double[])computed["final_stm"];
        Assert.All(Enumerable.Range(0, 36), k => Assert.Equal(phi.Get(k / 6, k % 6), stm[k]));
        Assert.Equal(UnitConversion.MToKm(position.X), computed["final_x_km"]);
        Assert.Equal(UnitConversion.MToKm(position.Y), computed["final_y_km"]);
        Assert.Equal(UnitConversion.MToKm(position.Z), computed["final_z_km"]);
        Assert.Equal(UnitConversion.MSToKmS(velocity.X), computed["final_vx_km_s"]);
        Assert.Equal(UnitConversion.MSToKmS(velocity.Y), computed["final_vy_km_s"]);
        Assert.Equal(UnitConversion.MSToKmS(velocity.Z), computed["final_vz_km_s"]);
    }

    [Fact]
    public void StmSolver_HandlesTheStmCategory()
    {
        Assert.Equal("stm", new StmSolver(_directory).Category);
    }

    private void EditGolden(Action<JsonObject> edit)
    {
        var path = Path.Combine(_directory, StmCaseDirectory, "expected-result.json");
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
}
