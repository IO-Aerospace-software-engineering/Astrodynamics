// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The maneuver and lambert cases of lot 3 (design of #587): chains of impulsive maneuvers with the mass they consume,
/// and Lambert's problem, with GMAT R2026a goldens, run by the whole runner on the fixture <c>Fixture/Lot3</c> synced
/// from the suite (<see cref="SuiteFixture.Lot3"/>); and the parts of the two solvers a case of the suite does not
/// reach: the maneuver of each step type, the Lambert branch by the semi-major axis, the steps the runner refuses.
/// </summary>
/// <remarks>
/// Each test that runs the runner writes its own suite from the fixture, deleted at the end. Only
/// <see cref="EveryCaseOfTheFixture_Passes"/> prints <c>MEASURE</c> lines, one per compared output, which the CI logs of
/// the three platforms carry: the framework's margin to the tolerance of each output.
/// </remarks>
public sealed class Lot3CaseTests : IDisposable
{
    private const string KernelsPath = SuiteFixture.KernelsPath;
    private const string ApoapsisCase = "cases/maneuver/001";
    private const string HohmannCase = "cases/maneuver/003";
    private const string GtoCase = "cases/maneuver/012";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public Lot3CaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("lot3-fixture-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryCaseOfTheFixture_Passes()
    {
        // Arrange
        SuiteFixture.Lot3.WriteSuite(_directory);

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
        Assert.Equal(19, report.Summary.Passed);
        Assert.Equal(SuiteFixture.Lot3.CaseDirectories().Count, report.Summary.Total);

        // Every output of every golden is compared, in its order (each field of each burn, named by its label), against
        // the default of its key: no case of the lot has an override (spec.md section 6.4)
        var defaults = SuiteFixture.ReadTolerances(_directory).Defaults;
        foreach (var caseDirectory in SuiteFixture.Lot3.CaseDirectories())
        {
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, caseDirectory, "expected-result.json")));
            var caseInput = SuiteFixture.ReadCase(_directory, caseDirectory);
            var result = report.Results.Single(r => r.CaseId == caseInput.Id);
            Assert.Null(caseInput.TolerancesOverride);
            Assert.Equal(ExpectedOutputs(expected.RootElement.GetProperty("outputs")), result.Measures.Select(m => m.Output));
            foreach (var measure in result.Measures)
            {
                Assert.Equal(ExpectedKey(measure.Output), measure.ToleranceName);
                Assert.Equal(defaults[measure.ToleranceName].AbsTol, measure.Threshold);
            }
        }
    }

    [Fact]
    public void ABurnEpochMovedByTwiceItsTolerance_Fails()
    {
        // Arrange: the second burn of the Hohmann transfer
        SuiteFixture.Lot3.WriteSuite(_directory, HohmannCase);
        double tolerance = SuiteFixture.ReadTolerances(_directory).Defaults["maneuver_epoch_s"].AbsTol;
        SuiteFixture.EditGolden(_directory, HohmannCase, outputs =>
        {
            var burn = outputs["burns"]!.AsArray()[1]!.AsObject();
            burn["elapsed_s"] = burn["elapsed_s"]!.GetValue<double>() + 2.0 * tolerance;
        });

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("FAIL", result.Status);
        Assert.Equal(new[] { "burns[raise_periapsis].elapsed_s" }, result.Measures.Where(m => !m.Passed).Select(m => m.Output));
        Assert.InRange(result.Measures.Single(m => m.Output == "burns[raise_periapsis].elapsed_s").Ratio, 1.0, 3.0);
    }

    [Fact]
    public void AnExhaustedTank_IsAnError()
    {
        // Arrange: 100 kg of fuel for the 1562 kg of the transfer burn
        SuiteFixture.Lot3.WriteSuite(_directory, GtoCase);
        EditInputs(GtoCase, text => text.Replace("fuel_mass_kg: 1800.0", "fuel_mass_kg: 100.0"));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.StartsWith(nameof(InsufficientFuelException), result.Message);
    }

    [Fact]
    public void AStepWhoseEventDoesNotComeWithinTheWindow_IsAnError_AndSoIsTheStepAfterIt()
    {
        // Arrange: the first burn of the Hohmann transfer no earlier than 25 h after the start of its 24 h window
        SuiteFixture.Lot3.WriteSuite(_directory, HohmannCase);
        EditInputs(HohmannCase, text => ReplaceFirst(text, "earliest_after_previous_s: 0.0", "earliest_after_previous_s: 90000.0"));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert: the first output of the golden, the epoch of the first burn, names the step
        Assert.Equal("ERROR", result.Status);
        Assert.Contains("burns[raise_apoapsis].elapsed_s", result.Message);
        Assert.Contains("the step 'raise_apoapsis' (apoapsis_radius) was not executed", result.Message);
    }

    [Fact]
    public void ASecondStepThatIsNotExecuted_GivesItsOutputsAsTheReason_TheFirstBurnAndTheFinalStateComputed()
    {
        // Arrange
        var solver = new ManeuverSolver(_directory);
        SuiteFixture.Lot3.WriteSuite(_directory, HohmannCase);
        EditInputs(HohmannCase, text => text.Replace("earliest_after_previous_s: 0.0\n      target_periapsis_radius_km",
            "earliest_after_previous_s: 90000.0\n      target_periapsis_radius_km"));
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));

        // Act
        var outputs = solver.Solve(SuiteFixture.ReadCase(_directory, HohmannCase));

        // Assert
        Assert.IsType<double>(outputs["burns[raise_apoapsis].elapsed_s"]);
        Assert.IsType<double>(outputs["final_x_km"]);
        foreach (var field in ManeuverSolver.BurnFields)
        {
            var failure = Assert.IsType<InvalidOperationException>(outputs[$"burns[raise_periapsis].{field}"]);
            Assert.Contains("'raise_periapsis' (periapsis_radius) was not executed", failure.Message);
        }
    }

    [Fact]
    public void EachStepType_IsTheFrameworksManeuver_WithTheHoldOfTheNextStep()
    {
        // Arrange
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));
        var epoch = new Time(2026, 1, 20, 6, 0, 0, frame: TimeFrame.UTCFrame);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch);
        var state = new StateVector(new Vector3(6428778.39, 2100929.85, -1661580.75), new Vector3(-1460.44, 6743.21, 3149.61), earth, epoch,
            Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-998, "Lot3Steps", 1000.0, 10000.0, new Clock("Lot3Clk", 256), state);
        var tank = new FuelTank("t", "m", "s", 500.0, 500.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("e", "m", "s", 300.0, ManeuverSolver.FuelFlow, tank);
        spacecraft.AddEngine(engine);
        var target = new StateVectorOrbit { Type = "state_vector", PositionKm = new[] { 6073.7, 3205.9, -1101.0 }, VelocityKmS = new[] { -2.95, 6.18, 3.51 } };
        var steps = new (ManeuverStep Step, Type Maneuver)[]
        {
            (new ManeuverStep { Label = "a", Type = ManeuverStepType.ApoapsisRadius, TargetApoapsisRadiusKm = 8000.0 }, typeof(ApogeeHeightManeuver)),
            (new ManeuverStep { Label = "b", Type = ManeuverStepType.PeriapsisRadius, TargetPeriapsisRadiusKm = 7800.0 }, typeof(PerigeeHeightManeuver)),
            (new ManeuverStep { Label = "c", Type = ManeuverStepType.PlaneChange, TargetPlane = new TargetPlane { InclinationDeg = 32.0, RaanDeg = 50.0 } },
                typeof(PlaneAlignmentManeuver)),
            (new ManeuverStep { Label = "d", Type = ManeuverStepType.ApsidalAlignment, TargetOrbit = new TargetOrbitInPlane { AKm = 8000.0, E = 0.1, ArgpDeg = 40.0 } },
                typeof(ApsidalAlignmentManeuver)),
            (new ManeuverStep { Label = "e", Type = ManeuverStepType.Phasing, Revolutions = 3, Target = target }, typeof(PhasingManeuver)),
            (new ManeuverStep { Label = "f", Type = ManeuverStepType.Combined, TargetPeriapsisRadiusKm = 7600.0, TargetInclinationDeg = 25.0 },
                typeof(CombinedManeuver))
        };

        foreach (var (step, type) in steps)
        {
            // Act
            var maneuver = ManeuverSolver.CreateManeuver(step, earth, state, epoch, TimeSpan.FromSeconds(2000.0), engine);

            // Assert
            Assert.IsType(type, maneuver);
            Assert.Equal(TimeSpan.FromSeconds(2000.0), maneuver.ManeuverHoldDuration);
            Assert.Equal(epoch, maneuver.MinimumEpoch);
            Assert.Same(earth, maneuver.ManeuverCenter);
        }

        // The targets of the plane change and of the apsidal alignment: the plane of the case, the current plane
        var plane = (PlaneAlignmentManeuver)ManeuverSolver.CreateManeuver(steps[2].Step, earth, state, epoch, TimeSpan.Zero, engine);
        Assert.Equal(32.0, plane.TargetOrbit.Inclination() * IO.Astrodynamics.Constants.Rad2Deg, 12);
        Assert.Equal(50.0, plane.TargetOrbit.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg, 12);
        var apsidal = (ApsidalAlignmentManeuver)ManeuverSolver.CreateManeuver(steps[3].Step, earth, state, epoch, TimeSpan.Zero, engine);
        Assert.Equal(state.Inclination(), apsidal.TargetOrbit.Inclination(), 12);
        Assert.Equal(state.AscendingNode(), apsidal.TargetOrbit.AscendingNode(), 12);
        Assert.Equal(40.0, apsidal.TargetOrbit.ArgumentOfPeriapsis() * IO.Astrodynamics.Constants.Rad2Deg, 9);
        Assert.Equal(0.1, apsidal.TargetOrbit.Eccentricity(), 12);

        var unknown = new ManeuverStep { Label = "g", Type = "hohmann" };
        Assert.Throws<ArgumentException>(() => ManeuverSolver.CreateManeuver(unknown, earth, state, epoch, TimeSpan.Zero, engine));
    }

    [Fact]
    public void AnApsidalAlignmentAfterAPlaneChange_IsRefused()
    {
        var steps = new List<ManeuverStep>
        {
            new() { Label = "align_first", Type = ManeuverStepType.ApsidalAlignment },
            new() { Label = "combined", Type = ManeuverStepType.Combined },
            new() { Label = "align", Type = ManeuverStepType.ApsidalAlignment }
        };

        ManeuverSolver.CheckPlanes(steps.Take(1).ToList());
        var error = Assert.Throws<InvalidOperationException>(() => ManeuverSolver.CheckPlanes(steps));
        Assert.Contains("'align' follows the step 'combined'", error.Message);
    }

    [Theory]
    [InlineData("y", "hohmann", "target_apoapsis_radius_km: 8000.0", "is not a maneuver type")]
    [InlineData("x", "apoapsis_radius", "target_apoapsis_radius_km: 8000.0", "have the label 'x'")]
    [InlineData("y", "plane_change", "target_apoapsis_radius_km: 8000.0", "has no target_plane")]
    public void AStepTheSpecDoesNotDefine_IsRefusedByTheParser(string label, string type, string field, string message)
    {
        // Arrange: a first step x, then the step of the case
        var inputs = Inputs($"""
                             propulsion: {"{"}fuel_mass_kg: 500.0, isp_s: 300.0{"}"}
                             maneuvers:
                               - label: "x"
                                 type: "apoapsis_radius"
                                 earliest_after_previous_s: 0.0
                                 target_apoapsis_radius_km: 8000.0
                               - label: "{label}"
                                 type: "{type}"
                                 earliest_after_previous_s: 0.0
                                 {field}
                             """);

        // Act, Assert
        var error = Assert.Throws<ArgumentException>(() => InputParser.ParseManeuverInputs(inputs));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void ThePhasingStep_ReadsItsRevolutionsAndTarget()
    {
        var inputs = Inputs("""
                            propulsion: {fuel_mass_kg: 500.0, isp_s: 300.0}
                            maneuvers:
                              - label: "phase"
                                type: "phasing"
                                earliest_after_previous_s: 0.0
                                revolutions: 3
                                target: {type: "state_vector", position_km: [1.0, 2.0, 3.0], velocity_km_s: [4.0, 5.0, 6.0]}
                            """);

        var step = InputParser.ParseManeuverInputs(inputs).Steps.Single();

        Assert.Equal(3, step.Revolutions);
        var target = Assert.IsType<StateVectorOrbit>(step.Target);
        Assert.Equal(new[] { 4.0, 5.0, 6.0 }, target.VelocityKmS);
    }

    [Theory]
    [InlineData(LambertCaseSolver.LowerA, 0)]
    [InlineData(LambertCaseSolver.HigherA, 1)]
    public void TheLambertBranch_IsChosenBySemiMajorAxis_WhateverTheSolverCallsIt(string branch, int expected)
    {
        // Arrange: the Left solution has the larger semi-major axis, and the solver lists it first
        const double gm = 3.986004355070226e14;
        var r1 = new Vector3(7000e3, 0.0, 0.0);
        var faster = new Vector3(0.0, 8000.0, 0.0);
        var slower = new Vector3(0.0, 7600.0, 0.0);
        var result = new LambertResult(1);
        result.AddSolution(new LambertSolution(Vector3.Zero, Vector3.Zero, 0.0, 0, Vector3.Zero, Vector3.Zero));
        result.AddSolution(new LambertSolution(1, faster, Vector3.Zero, 0.0, 0, Vector3.Zero, Vector3.Zero, LambertBranch.Left));
        result.AddSolution(new LambertSolution(1, slower, Vector3.Zero, 0.0, 0, Vector3.Zero, Vector3.Zero, LambertBranch.Right));
        Assert.True(LambertCaseSolver.SemiMajorAxis(r1, faster, gm) > LambertCaseSolver.SemiMajorAxis(r1, slower, gm));

        // Act
        var solution = LambertCaseSolver.Select(result, new LambertInputs { Revolutions = 1, Branch = branch }, r1, gm);

        // Assert
        Assert.Equal(new[] { slower, faster }[expected], solution.V1);
    }

    [Fact]
    public void TheLambertBranch_IsRequiredWithRevolutions_AndRefusedWithout()
    {
        var r1 = new Vector3(7000e3, 0.0, 0.0);
        var result = new LambertResult(1);
        result.AddSolution(new LambertSolution(Vector3.Zero, Vector3.Zero, 0.0, 0, Vector3.Zero, Vector3.Zero));

        Assert.Throws<ArgumentException>(() => LambertCaseSolver.Select(result, new LambertInputs { Revolutions = 0, Branch = "lower_a" }, r1, 1.0));
        Assert.Throws<InvalidOperationException>(() => LambertCaseSolver.Select(result, new LambertInputs { Revolutions = 1, Branch = "lower_a" }, r1, 1.0));
        result.AddSolution(new LambertSolution(1, Vector3.VectorY, Vector3.Zero, 0.0, 0, Vector3.Zero, Vector3.Zero, LambertBranch.Left));
        result.AddSolution(new LambertSolution(1, Vector3.VectorY, Vector3.Zero, 0.0, 0, Vector3.Zero, Vector3.Zero, LambertBranch.Right));
        Assert.Throws<ArgumentException>(() => LambertCaseSolver.Select(result, new LambertInputs { Revolutions = 1, Branch = null }, r1, 1.0));
        Assert.Same(result.GetZeroRevolutionSolution(), LambertCaseSolver.Select(result, new LambertInputs { Revolutions = 0 }, r1, 1.0));
    }

    [Fact]
    public void TheOutputsOfALabelledArray_AreNamedByTheirLabel_AndTwoEqualLabelsAreRefused()
    {
        using var golden = JsonDocument.Parse("""{"burns": [{"label": "a", "elapsed_s": 1.0, "position_km": [1, 2, 3]}, {"label": "b", "elapsed_s": 2.0}], "final_x_km": 4.0}""");
        using var twice = JsonDocument.Parse("""{"burns": [{"label": "a", "elapsed_s": 1.0}, {"label": "a", "elapsed_s": 2.0}]}""");
        using var withNull = JsonDocument.Parse("""{"burns": [{"label": "a", "elapsed_s": null}]}""");

        Assert.Equal(new[] { "burns[a].elapsed_s", "burns[a].position_km", "burns[b].elapsed_s", "final_x_km" },
            OutputComparer.Outputs(golden.RootElement).Select(o => o.Output));
        Assert.Throws<InvalidOperationException>(() => OutputComparer.Outputs(twice.RootElement).ToList());
        Assert.True(SkipDetector.ShouldSkip(withNull.RootElement));
        Assert.False(SkipDetector.ShouldSkip(golden.RootElement));
        Assert.Equal("maneuver/burns[].elapsed_s", MetricTable.SummaryKey("maneuver", "burns[a].elapsed_s"));
        Assert.True(MetricTable.TryGetSummaryUnit("maneuver/burns[].mass_after_kg", out var unit));
        Assert.Equal("kg", unit);
        Assert.False(MetricTable.TryGet("maneuver", "burns[a].thrust_s", out _));
    }

    // The outputs of a golden in its order, each field of a burn named burns[<label>].<field>
    private static IEnumerable<string> ExpectedOutputs(JsonElement outputs)
    {
        foreach (var property in outputs.EnumerateObject())
        {
            if (property.Name != "burns")
            {
                yield return property.Name;
                continue;
            }

            foreach (var burn in property.Value.EnumerateArray())
            {
                string label = burn.GetProperty("label").GetString();
                foreach (var field in burn.EnumerateObject().Where(f => f.Name != "label"))
                {
                    yield return $"burns[{label}].{field.Name}";
                }
            }
        }
    }

    // The key of an output (spec.md section 13)
    private static string ExpectedKey(string output)
    {
        string field = output.StartsWith("burns[", StringComparison.Ordinal) ? output[(output.IndexOf("].", StringComparison.Ordinal) + 2)..] : output;
        return field switch
        {
            "elapsed_s" => "maneuver_epoch_s",
            "position_km" => "maneuver_position_km",
            "delta_v_km_s" => "maneuver_delta_v_km_s",
            "mass_after_kg" => "maneuver_mass_kg",
            "v1_km_s" or "v2_km_s" => "lambert_velocity_km_s",
            _ when field.StartsWith("final_v", StringComparison.Ordinal) => "propagator_velocity_km_s",
            _ => "propagator_position_km"
        };
    }

    private static Dictionary<string, object> Inputs(string yaml)
    {
        return new YamlDotNet.Serialization.Deserializer().Deserialize<Dictionary<string, object>>(yaml);
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        int index = text.IndexOf(oldValue, StringComparison.Ordinal);
        return text[..index] + newValue + text[(index + oldValue.Length)..];
    }

    private void EditInputs(string caseDirectory, Func<string, string> edit)
    {
        var path = Path.Combine(_directory, caseDirectory, "inputs.yaml");
        File.WriteAllText(path, edit(File.ReadAllText(path).Replace("\r\n", "\n")));
    }
}
