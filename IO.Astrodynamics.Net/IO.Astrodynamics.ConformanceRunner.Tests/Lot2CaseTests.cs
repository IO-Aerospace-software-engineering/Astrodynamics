// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The propagator cases of lot 2 (design of #500): drag with NRLMSISE-00, SRP with the shadows of the Earth and the
/// Moon, high eccentric orbits, and the Moon, Mars and the Sun as central bodies, with GMAT R2026a goldens, run by the
/// whole runner on the fixture <c>Fixture/Lot2</c> synced from the suite (<see cref="SuiteFixture.Lot2"/>).
/// </summary>
/// <remarks>
/// Each test writes its own suite from the fixture, deleted at the end. Only <see cref="EveryCaseOfTheFixture_Passes"/>
/// prints <c>MEASURE</c> lines, one per compared output, which the CI logs of the three platforms carry: the
/// framework's margin to the tolerance of each case, which the suite sets from the window's length (decision 23 of
/// #457) without any framework's deltas.
/// </remarks>
public sealed class Lot2CaseTests : IDisposable
{
    private const string KernelsPath = SuiteFixture.KernelsPath;
    private const string DragCase = "cases/propagator/006";
    private const string SixHourCase = "cases/propagator/007";
    private const string GeoSrpCase = "cases/propagator/010";
    private const string MoonCase = "cases/propagator/016";
    private const string MarsCase = "cases/propagator/017";

    private readonly string _directory;
    private readonly ITestOutputHelper _output;

    public Lot2CaseTests(ITestOutputHelper output)
    {
        _output = output;
        _directory = Directory.CreateTempSubdirectory("lot2-fixture-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryCaseOfTheFixture_Passes()
    {
        // Arrange
        SuiteFixture.Lot2.WriteSuite(_directory);

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
        Assert.Equal(13, report.Summary.Passed);
        Assert.Equal(SuiteFixture.Lot2.CaseDirectories().Count, report.Summary.Total);

        // Every output of every golden is compared, in its order, against the tolerance of its key: the case's override
        // where the window is not one day long (spec.md section 6.4), the default otherwise
        var defaults = SuiteFixture.ReadTolerances(_directory).Defaults;
        foreach (var caseDirectory in SuiteFixture.Lot2.CaseDirectories())
        {
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, caseDirectory, "expected-result.json")));
            var caseInput = SuiteFixture.ReadCase(_directory, caseDirectory);
            var result = report.Results.Single(r => r.CaseId == caseInput.Id);
            Assert.Equal(expected.RootElement.GetProperty("outputs").EnumerateObject().Select(p => p.Name),
                result.Measures.Select(m => m.Output));
            foreach (var measure in result.Measures)
            {
                var tolerance = caseInput.TolerancesOverride?.GetValueOrDefault(measure.ToleranceName) ?? defaults[measure.ToleranceName];
                Assert.Equal(measure.Output.StartsWith("final_v") ? "propagator_velocity_km_s" : "propagator_position_km",
                    measure.ToleranceName);
                Assert.Equal(tolerance.AbsTol, measure.Threshold);
            }
        }
    }

    [Fact]
    public void AGoldenMovedByTwiceTheToleranceOfItsCase_Fails_ThoughWithinTheDefault()
    {
        // Arrange: 007 lasts 6 h, so its tolerance is a quarter of the one-day default (spec.md section 6.4); its vy
        // moved by twice it stays within the default
        SuiteFixture.Lot2.WriteSuite(_directory, SixHourCase);
        var caseInput = SuiteFixture.ReadCase(_directory, SixHourCase);
        double tolerance = caseInput.TolerancesOverride["propagator_velocity_km_s"].AbsTol;
        double fallback = SuiteFixture.ReadTolerances(_directory).Defaults["propagator_velocity_km_s"].AbsTol;
        Assert.Equal(fallback / 4.0, tolerance);
        SuiteFixture.EditGolden(_directory, SixHourCase,
            outputs => outputs["final_vy_km_s"] = outputs["final_vy_km_s"]!.GetValue<double>() + 2.0 * tolerance);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("FAIL", result.Status);
        Assert.Equal(new[] { "final_vy_km_s" }, result.Measures.Where(m => !m.Passed).Select(m => m.Output));
        Assert.InRange(result.Measures.Single(m => m.Output == "final_vy_km_s").Ratio, 1.0, 3.0);
    }

    [Fact]
    public void DragWithoutTheSpacecraft_FailsTheSchema()
    {
        // Arrange: the spacecraft block of 006 removed; the runner refuses it as well (PropagationCaseTests)
        SuiteFixture.Lot2.WriteSuite(_directory, DragCase);
        EditInputs(DragCase, text => text[..text.IndexOf("  spacecraft:", StringComparison.Ordinal)] +
                                     text[text.IndexOf("  step_size_s:", StringComparison.Ordinal)..]);

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert
        Assert.Equal("ERROR", result.Status);
        Assert.StartsWith("Schema validation failed for case input", result.Message);
    }

    [Fact]
    public void TheMarsCase_WithoutTheMarsCut_DoesNotPass()
    {
        // Arrange: de440s holds the Mars barycentre, not the centre of Mars (499), which the suite's cut gives
        SuiteFixture.Lot2.WriteSuite(_directory, MarsCase);
        EditInputs(MarsCase, text => text.Replace("\"mar099s_20260119_20260123.bsp\", ", ""));

        // Act
        var result = new Runner(_directory, KernelsPath).Run().Results.Single();

        // Assert: the case reads the cut. Not ERROR alone: the library reports no ephemeris read outside the coverage of
        // the kernels (ReadEphemerisAtGivenEpochProxy drops the SPICE error and returns its uninitialized state), so the
        // propagation starts from whatever that state holds: on Linux, the integrator stops on a NaN error (ERROR)
        Assert.Contains(result.Status, new[] { "ERROR", "FAIL" });
    }

    [Fact]
    public void TheMoonCase_IsComputedInMoonPa_WhileItsKernelsAreLoaded()
    {
        // Arrange: the runner's kernels associate the Moon with MOON_ME; the case's moon_assoc_pa.tf, loaded after them,
        // with MOON_PA, the frame of LP165P (spec.md section 12.1)
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));
        SuiteFixture.Lot2.WriteSuite(_directory, MoonCase);
        var caseInput = SuiteFixture.ReadCase(_directory, MoonCase);

        // Act
        string inside;
        using (new CaseKernels(_directory, InputParser.ParseKernels(caseInput.Inputs)))
        {
            inside = ((CelestialBody)PropagationCase.Build(caseInput, Path.Combine(_directory, "data")).Bodies[0]).Frame.Name;
        }

        string after = new CelestialBody(PlanetsAndMoons.MOON).Frame.Name;

        // Assert
        Assert.Equal("MOON_PA", inside);
        Assert.Equal("MOON_ME", after);
    }

    [Fact]
    public void TheShadowBodiesOfACase_AreTheOccultersOfTheFrameworksSrp()
    {
        // Arrange: the check of the runner (PropagationCase.Occulters) against the bodies the framework's SRP takes
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));
        SuiteFixture.Lot2.WriteSuite(_directory, GeoSrpCase);
        var caseInput = SuiteFixture.ReadCase(_directory, GeoSrpCase);
        PropagationCase propagationCase;
        using (new CaseKernels(_directory, InputParser.ParseKernels(caseInput.Inputs)))
        {
            propagationCase = PropagationCase.Build(caseInput, Path.Combine(_directory, "data"));
        }

        // Act
        var integrator = PropagatorSolver.CreateIntegrator();
        _ = new CentralBodyPropagator(propagationCase.Window, propagationCase.Spacecraft, integrator, propagationCase.Bodies,
            propagationCase.Drag, propagationCase.SolarRadiationPressure, propagationCase.OutputStep);
        var srp = integrator.Forces.OfType<SolarRadiationPressure>().Single();
        var occulters = (CelestialBody[])typeof(SolarRadiationPressure)
            .GetField("_occultingBodies", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(srp);

        // Assert
        Assert.Equal(new[] { 301, 399 }, occulters!.Select(b => b.NaifId).Order());
        Assert.Equal(occulters.Select(b => b.NaifId).Order(), PropagationCase.Occulters(propagationCase.Bodies).Order());
        Assert.False(propagationCase.Drag);
        Assert.True(propagationCase.SolarRadiationPressure);
    }

    private void EditInputs(string caseDirectory, Func<string, string> edit)
    {
        var path = Path.Combine(_directory, caseDirectory, "inputs.yaml");
        File.WriteAllText(path, edit(File.ReadAllText(path)));
    }
}
