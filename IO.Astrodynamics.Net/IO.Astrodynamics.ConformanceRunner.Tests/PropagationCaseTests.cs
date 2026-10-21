// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// <see cref="PropagationCase.Build"/>, shared by the <c>propagator</c> and <c>stm</c> categories, on the kinds of
/// inputs the fixture cases do not hold (H3 review of #408, B1): Keplerian elements with a geopotential, perturbing
/// bodies, drag and SRP; a TLE; and an orbit type that a propagation does not take.
/// </summary>
public class PropagationCaseTests
{
    private const string KernelsPath = "Data/SolarSystem";

    public PropagationCaseTests()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));
    }

    // A case as the runner reads it from its inputs.yaml
    private static CaseInput Parse(string orbit, string geopotential, string bodies, string forces, string spacecraft = "")
    {
        var yaml = $$"""
                     id: "propagation_case_test"
                     schema_version: "v1"
                     version: "0.1.0"
                     category: "propagator"
                     created: "2026-10-20"
                     description: "test"
                     metadata:
                       ephemeris_kernel: "de440s.bsp"
                       reference_frame: "EME2000"
                       time_scale: "UTC"
                     inputs:
                       epoch: "2021-03-20T12:00:00.000 UTC"
                       orbit:
                     {{orbit}}
                       propagation_window:
                         start: "2021-03-20T12:00:00.000 UTC"
                         end: "2021-03-20T18:00:00.000 UTC"
                       central_body: "Earth"
                     {{geopotential}}
                       perturbation_bodies: {{bodies}}
                       force_model:
                     {{forces}}
                       step_size_s: 30.0
                     {{spacecraft}}
                     """;
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build()
            .Deserialize<CaseInput>(yaml);
    }

    [Fact]
    public void KeplerianElements_WithGeopotentialBodiesDragAndSrp()
    {
        // Arrange
        var caseInput = Parse("""
                                  type: "keplerian"
                                  a_km: 6778.137
                                  e: 0.001
                                  i_deg: 51.6
                                  raan_deg: 30.0
                                  argp_deg: 40.0
                                  ma_deg: 50.0
                              """,
            """
              geopotential_degree: 10
              geopotential_model: "EGM2008_to70_TideFree"
            """,
            "[\"Moon\", \"Sun\"]",
            """
                drag: true
                srp: true
            """,
            """
              spacecraft:
                mass_kg: 500.0
                max_mass_kg: 800.0
                sectional_area_m2: 4.0
                drag_coefficient: 2.1
                radiation_pressure_coefficient: 1.3
            """);

        // Act
        var propagationCase = PropagationCase.Build(caseInput, KernelsPath);

        // Assert: the initial state holds the given elements
        var state = propagationCase.Spacecraft.InitialOrbitalParameters.ToStateVector();
        var elements = state.ToKeplerianElements();
        Assert.Equal(6778137.0, elements.A, 6);
        Assert.Equal(0.001, elements.E, 12);
        Assert.Equal(51.6 * IO.Astrodynamics.Constants.Deg2Rad, elements.I, 12);
        Assert.Equal(30.0 * IO.Astrodynamics.Constants.Deg2Rad, elements.RAAN, 12);
        Assert.Equal(40.0 * IO.Astrodynamics.Constants.Deg2Rad, elements.AOP, 10);
        Assert.Equal(50.0 * IO.Astrodynamics.Constants.Deg2Rad, elements.M, 10);
        Assert.Equal(Frames.Frame.ICRF, state.Frame);

        // The central body first, with the geopotential (J2 moves the acceleration by about 1e-3 from the point mass)
        // and an atmosphere, then the perturbing bodies
        Assert.Equal(new[] { 399, 301, 10 }, propagationCase.Bodies.Select(b => b.NaifId));
        var earth = (CelestialBody)propagationCase.Bodies[0];
        Assert.True(earth.HasAtmosphericModel);
        var acceleration = earth.EvaluateGravitationalAcceleration(state);
        var pointMass = state.Position * (-earth.GM / System.Math.Pow(state.Position.Magnitude(), 3.0));
        double departure = (acceleration - pointMass).Magnitude() / pointMass.Magnitude();
        Assert.InRange(departure, 1e-4, 1e-2);

        // The spacecraft and the forces
        Assert.Equal(500.0, propagationCase.Spacecraft.DryOperatingMass);
        Assert.Equal(800.0, propagationCase.Spacecraft.MaximumOperatingMass);
        Assert.Equal(4.0, propagationCase.Spacecraft.SectionalArea);
        Assert.Equal(2.1, propagationCase.Spacecraft.DragCoefficient);
        Assert.Equal(1.3, propagationCase.Spacecraft.SolarRadiationCoeff);
        Assert.True(propagationCase.Drag);
        Assert.True(propagationCase.SolarRadiationPressure);
        Assert.Equal(TimeSpan.FromSeconds(30.0), propagationCase.StepSize);
        Assert.Equal(TimeSpan.FromHours(6.0), propagationCase.Window.Length);
        Assert.Equal(propagationCase.Window.Length, propagationCase.OutputStep);
    }

    [Fact]
    public void PropagatorSolver_GivesTheFinalStateOfAPropagationWithOutputsAtTheStepOfTheCase()
    {
        // Arrange: a case with a geopotential and perturbing bodies, and the same propagation with an output at every
        // step of the case (30 s), as the solver did before #426
        CaseInput Case() => Parse(KeplerianOrbit, Geopotential10, "[\"Moon\", \"Sun\"]", NoForces);
        var reference = PropagationCase.Build(Case(), KernelsPath);
        var (position, velocity) = new CentralBodyPropagator(reference.Window, reference.Spacecraft,
                PropagatorSolver.CreateIntegrator(), reference.Bodies, reference.Drag, reference.SolarRadiationPressure,
                reference.StepSize)
            .Propagate().InterpolateAt(reference.Window.EndDate.ToTDB());

        // Act
        var computed = new PropagatorSolver(KernelsPath).Solve(Case());

        // Assert: the same, bit for bit, since the accepted steps do not depend on the outputs
        Assert.Equal(UnitConversion.MToKm(position.X), computed["final_x_km"]);
        Assert.Equal(UnitConversion.MToKm(position.Y), computed["final_y_km"]);
        Assert.Equal(UnitConversion.MToKm(position.Z), computed["final_z_km"]);
        Assert.Equal(UnitConversion.MSToKmS(velocity.X), computed["final_vx_km_s"]);
        Assert.Equal(UnitConversion.MSToKmS(velocity.Y), computed["final_vy_km_s"]);
        Assert.Equal(UnitConversion.MSToKmS(velocity.Z), computed["final_vz_km_s"]);
    }

    [Fact]
    public void GeopotentialModelLeftAsAGitLfsPointer_IsAnError()
    {
        // Arrange: a data directory whose geopotential model is a Git LFS pointer
        var directory = Directory.CreateTempSubdirectory("lfs-pointer-").FullName;
        var path = Path.Combine(directory, "EGM2008_to70_TideFree");
        File.WriteAllText(path, SuiteDataFile.LfsPointerHeader + "v1\noid sha256:0\nsize 257852\n");
        var caseInput = Parse(KeplerianOrbit, Geopotential10, "[]", NoForces);

        try
        {
            // Act and assert
            var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, directory));
            Assert.Contains(path, exception.Message);
            Assert.Contains("git lfs pull", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private const string KeplerianOrbit = """
                                              type: "keplerian"
                                              a_km: 6778.137
                                              e: 0.001
                                              i_deg: 51.6
                                              raan_deg: 30.0
                                              argp_deg: 40.0
                                              ma_deg: 50.0
                                          """;

    private const string Geopotential10 = """
                                            geopotential_degree: 10
                                            geopotential_model: "EGM2008_to70_TideFree"
                                          """;

    private const string NoForces = """
                                        drag: false
                                        srp: false
                                    """;

    [Fact]
    public void Tle_TheOsculatingStateOfSgp4_InTheFrameOfTheCase()
    {
        // Arrange
        const string line1 = "1 25544U 98067A   21079.50000000  .00001264  00000-0  31429-4 0  9990";
        const string line2 = "2 25544  51.6443 107.6529 0003131 140.3893 297.0447 15.48905238274800";
        var caseInput = Parse($"""
                                   type: "tle"
                                   name: "ISS"
                                   line1: "{line1}"
                                   line2: "{line2}"
                               """,
            """
              geopotential_degree: 0
              geopotential_model: "none"
            """,
            "[]",
            """
                drag: false
                srp: false
            """);

        // Act
        var propagationCase = PropagationCase.Build(caseInput, KernelsPath);

        // Assert: the SGP4/SDP4 state at the epoch of the TLE, turned to the frame of the case, relative to the central
        // body of the case, which has no geopotential and no atmosphere; the defaults of the spacecraft
        var expected = new TLE("ISS", line1, line2).ToStateVector().ToFrame(Frames.Frame.ICRF).ToStateVector();
        var state = propagationCase.Spacecraft.InitialOrbitalParameters.ToStateVector();
        Assert.Equal(expected.Epoch, state.Epoch);
        Assert.Equal(expected.Position, state.Position);
        Assert.Equal(expected.Velocity, state.Velocity);
        Assert.Equal(Frames.Frame.ICRF, state.Frame);
        var earth = (CelestialBody)propagationCase.Bodies.Single();
        Assert.Equal(399, earth.NaifId);
        Assert.False(earth.HasAtmosphericModel);
        Assert.Equal(state.Position * (-earth.GM / System.Math.Pow(state.Position.Magnitude(), 3.0)),
            earth.EvaluateGravitationalAcceleration(state));
        Assert.Equal(100.0, propagationCase.Spacecraft.DryOperatingMass);
        Assert.Equal(1000.0, propagationCase.Spacecraft.MaximumOperatingMass);
        Assert.Equal(1.0, propagationCase.Spacecraft.SectionalArea);
        Assert.Equal(2.2, propagationCase.Spacecraft.DragCoefficient);
        Assert.Equal(1.0, propagationCase.Spacecraft.SolarRadiationCoeff);
        Assert.False(propagationCase.Drag);
        Assert.False(propagationCase.SolarRadiationPressure);
    }

    [Fact]
    public void OrbitFromAnSpk_IsNotAPropagationInput()
    {
        // Arrange: a type the parser knows (the eclipse cases) but a propagation does not take
        var caseInput = Parse("""
                                  type: "bsp_source"
                                  naif_id: -1000
                                  filename: "orbit.bsp"
                              """,
            """
              geopotential_degree: 0
              geopotential_model: "none"
            """,
            "[]",
            """
                drag: false
                srp: false
            """);

        // Act and assert
        var exception = Assert.Throws<ArgumentException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Equal("Unknown orbit type", exception.Message);
    }
}
