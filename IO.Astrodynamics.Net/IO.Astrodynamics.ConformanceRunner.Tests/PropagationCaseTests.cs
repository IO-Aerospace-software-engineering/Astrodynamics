// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// <see cref="PropagationCase.Build"/>, shared by the <c>propagator</c> and <c>stm</c> categories, on the kinds of
/// inputs the fixture cases do not hold (H3 review of #408, B1): Keplerian elements with a geopotential, perturbing
/// bodies, drag and SRP; a TLE; and an orbit type that a propagation does not take; and the model of spec.md section
/// 12.1 (design of #500, section 6.4): NRLMSISE-00, the spacecraft and the shadow bodies required, the field table, the
/// tagged epochs, the integrator, with the cases the runner refuses.
/// </summary>
public class PropagationCaseTests
{
    private const string KernelsPath = "Data/SolarSystem";

    public PropagationCaseTests()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));
    }

    // A case as the runner reads it from its inputs.yaml
    private static CaseInput Parse(string orbit, string geopotential, string bodies, string forces, string spacecraft = "",
        string centralBody = "Earth", string start = "2021-03-20T12:00:00.000 UTC", string end = "2021-03-20T18:00:00.000 UTC")
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
                       epoch: "{{start}}"
                       orbit:
                     {{orbit}}
                       propagation_window:
                         start: "{{start}}"
                         end: "{{end}}"
                       central_body: "{{centralBody}}"
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
            "[\"Moon\", \"Sun\"]", DragAndSrp, Spacecraft);

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

        // NRLMSISE-00 at the space weather of the case, every switch on (switch 9 = 1: the daily Ap alone), bit for bit;
        // F10.7 and F10.7A swapped give another density
        var context = AtmosphericContext.FromPlanetodetic(400e3, 0.5, 1.0, state.Epoch);
        var standard = NrlmsiseFlags.CreateStandard();
        Assert.Equal(1, standard.Switches[9]);
        Assert.Equal(new Nrlmsise00Model(standard, new SpaceWeather { F107 = 160.0, F107A = 145.0, Ap = 15.0 }).GetDensity(context),
            earth.GetAirDensity(context));
        Assert.NotEqual(new Nrlmsise00Model(standard, new SpaceWeather { F107 = 145.0, F107A = 160.0, Ap = 15.0 }).GetDensity(context),
            earth.GetAirDensity(context));
        var acceleration = earth.EvaluateGravitationalAcceleration(state);
        var pointMass = state.Position * (-earth.GM / System.Math.Pow(state.Position.Magnitude(), 3.0));
        double departure = (acceleration - pointMass).Magnitude() / pointMass.Magnitude();
        Assert.InRange(departure, 1e-4, 1e-2);

        // The field of spec.md section 12.1: EGM2008's GM and radius for the whole field, central term included, bit
        // for bit; the GM of the kernels in the central term (the library's default) is 1.5e-8 of it away
        var model = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, state.Epoch,
            new GeopotentialModelParameters(Path.Combine(KernelsPath, "EGM2008_to70_TideFree"), 10, 3.986004415e14, 6378136.3)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });
        var kernelCentralTerm = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, state.Epoch,
            new GeopotentialModelParameters(Path.Combine(KernelsPath, "EGM2008_to70_TideFree"), 10));
        Assert.Equal(model.EvaluateGravitationalAcceleration(state), acceleration);
        Assert.InRange((kernelCentralTerm.EvaluateGravitationalAcceleration(state) - acceleration).Magnitude() / acceleration.Magnitude(),
            1.4e-8, 1.6e-8);

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
        var computed = new PropagatorSolver(null, KernelsPath).Solve(Case());

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

    // The drag and the SRP of spec.md section 12.1: NRLMSISE-00 at constant space weather, the shadows of the Earth and
    // of the Moon
    private const string DragAndSrp = """
                                          drag:
                                            model: "NRLMSISE-00"
                                            f107_sfu: 160.0
                                            f107a_sfu: 145.0
                                            ap: 15.0
                                          srp:
                                            shadow_bodies: ["Earth", "Moon"]
                                      """;

    private const string Spacecraft = """
                                        spacecraft:
                                          mass_kg: 500.0
                                          max_mass_kg: 800.0
                                          sectional_area_m2: 4.0
                                          drag_coefficient: 2.1
                                          radiation_pressure_coefficient: 1.3
                                      """;

    private const string DragOnly = """
                                        drag:
                                          model: "NRLMSISE-00"
                                          f107_sfu: 160.0
                                          f107a_sfu: 145.0
                                          ap: 15.0
                                        srp: false
                                    """;

    private const string StateVectorOrbit = """
                                                type: "state_vector"
                                                position_km: [1838.0, 0.0, 0.0]
                                                velocity_km_s: [0.0, 0.0, 1.633]
                                            """;

    private const string NoField = """
                                     geopotential_degree: 0
                                     geopotential_model: "none"
                                   """;

    [Theory]
    [InlineData(DragOnly)] // drag
    [InlineData("""
                    drag: false
                    srp:
                      shadow_bodies: ["Earth", "Moon"]
                """)] // SRP
    public void DragOrSrp_WithoutTheSpacecraft_IsAnError(string forces)
    {
        // Arrange: the schema requires the block; the runner refuses to default it
        var caseInput = Parse(KeplerianOrbit, Geopotential10, "[\"Moon\", \"Sun\"]", forces);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Contains("no spacecraft", exception.Message);
    }

    [Theory]
    [InlineData("[\"Moon\", \"Sun\"]", "[\"Earth\"]")] // the Moon occults in the framework, not in the case
    [InlineData("[\"Sun\"]", "[\"Earth\", \"Moon\"]")] // the Moon is no body of the propagation
    [InlineData("[\"Moon\", \"Sun\"]", "[\"Earth\", \"Sun\"]")] // the Sun does not occult itself
    public void ShadowBodies_OtherThanTheOccultersOfTheFramework_AreAnError(string bodies, string shadowBodies)
    {
        // Arrange
        var caseInput = Parse(KeplerianOrbit, Geopotential10, bodies, $"""
                                                                            drag: false
                                                                            srp:
                                                                              shadow_bodies: {shadowBodies}
                                                                        """, Spacecraft);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Contains("are not the bodies that occult the Sun in the framework", exception.Message);
    }

    [Fact]
    public void Occulters_AreTheCelestialBodiesButTheSun()
    {
        // Arrange: barycentres are no celestial bodies
        var bodies = new CelestialItem[]
        {
            new CelestialBody(PlanetsAndMoons.EARTH), PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY, Barycenters.JUPITER_BARYCENTER
        };

        // Act and assert
        Assert.Equal(new[] { 301, 399 }, PropagationCase.Occulters(bodies).Order());
    }

    [Fact]
    public void Srp_AboutTheMoon_IsAnError()
    {
        // Arrange: the shadow bodies are the framework's occulters, but spec.md section 12.1 models SRP about the Earth
        // only
        var caseInput = Parse(StateVectorOrbit, NoField, "[\"Earth\", \"Sun\"]", """
                                                                             drag: false
                                                                             srp:
                                                                               shadow_bodies: ["Moon", "Earth"]
                                                                         """, Spacecraft, centralBody: "Moon");

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Contains("SRP about the Earth only", exception.Message);
    }

    [Fact]
    public void Drag_AboutTheMoon_IsAnError()
    {
        // Arrange
        var caseInput = Parse(StateVectorOrbit, NoField, "[\"Earth\"]", DragOnly, Spacecraft, centralBody: "Moon");

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Contains("drag about the Earth only", exception.Message);
    }

    [Fact]
    public void AnotherAtmosphereModel_IsAnError()
    {
        // Arrange
        var caseInput = Parse(KeplerianOrbit, Geopotential10, "[]", DragOnly.Replace("NRLMSISE-00", "JB2008"), Spacecraft);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Contains("'JB2008' is not the suite's", exception.Message);
    }

    [Theory]
    [InlineData("""
                    drag: true
                    srp: false
                """, "force_model.drag")]
    [InlineData("""
                    drag: false
                    srp: true
                """, "force_model.srp")]
    [InlineData("""
                    drag: "yes please"
                    srp: false
                """, "force_model.drag")]
    [InlineData("""
                    drag: false
                    srp: null
                """, "force_model.srp")]
    public void AForceTurnedOnWithoutItsDefinition_IsAnError(string forces, string force)
    {
        // Arrange: true gives neither the space weather nor the shadow bodies, and null turns no force off
        var caseInput = Parse(KeplerianOrbit, Geopotential10, "[]", forces, Spacecraft);

        // Act and assert
        var exception = Assert.Throws<ArgumentException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.StartsWith(force, exception.Message);
    }

    [Theory]
    [InlineData("Earth", "JGM3", "is not a field of spec.md section 12.1")] // unknown
    [InlineData("Earth", "LP165P_to20", "is the field of the body 301, not of the central body 399")]
    [InlineData("Moon", "EGM2008_to70_TideFree", "is the field of the body 399, not of the central body 301")]
    public void AFieldTheTableDoesNotGiveForTheCentralBody_IsAnError(string centralBody, string model, string message)
    {
        // Arrange
        var caseInput = Parse(centralBody == "Earth" ? KeplerianOrbit : StateVectorOrbit, $"""
                                                                                              geopotential_degree: 10
                                                                                              geopotential_model: "{model}"
                                                                                            """, "[]", NoForces,
            centralBody: centralBody);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, Lot2Data));
        Assert.Contains(message, exception.Message);
    }

    [Fact]
    public void NoField_WithADegree_IsAnError()
    {
        // Arrange: a degree without its file would be dropped
        var caseInput = Parse(KeplerianOrbit, """
                                                geopotential_degree: 10
                                                geopotential_model: "none"
                                              """, "[]", NoForces);

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, KernelsPath));
        Assert.Contains("takes \"none\" with degree 0 only", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AFieldToDegree0Or1_IsThePointMassOfTheModelsGm(int degree)
    {
        // Arrange: EGM2008 without its harmonics (spec.md section 12.1: the model's GM on the whole field)
        var caseInput = Parse(KeplerianOrbit, $"""
                                                 geopotential_degree: {degree}
                                                 geopotential_model: "EGM2008_to70_TideFree"
                                               """, "[]", NoForces);

        // Act
        var propagationCase = PropagationCase.Build(caseInput, KernelsPath);

        // Assert: the point mass of EGM2008's GM to the rounding, 1.5e-8 from that of the kernel GM
        var earth = (CelestialBody)propagationCase.Bodies.Single();
        var state = propagationCase.Spacecraft.InitialOrbitalParameters.ToStateVector();
        var acceleration = earth.EvaluateGravitationalAcceleration(state);
        double r3 = System.Math.Pow(state.Position.Magnitude(), 3.0);
        var model = state.Position * (-3.986004415e14 / r3);
        var kernel = state.Position * (-earth.GM / r3);
        Assert.InRange((acceleration - model).Magnitude() / model.Magnitude(), 0.0, 1e-15);
        Assert.InRange((acceleration - kernel).Magnitude() / kernel.Magnitude(), 1.4e-8, 1.6e-8);
    }

    [Fact]
    public void ADegreeBeyondTheFile_IsAnError()
    {
        // Arrange
        var caseInput = Parse(StateVectorOrbit, """
                                                  geopotential_degree: 21
                                                  geopotential_model: "LP165P_to20"
                                                """, "[]", NoForces, centralBody: "Moon");

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => PropagationCase.Build(caseInput, Lot2Data));
        Assert.Contains("goes to degree 20: degree 21", exception.Message);
    }

    // The field files of lot 2, which only the lot-2 fixture ships
    private const string Lot2Data = "Fixture/Lot2/data";

    [Theory]
    [InlineData("Earth", "EGM2008_to70_TideFree", KernelsPath, 3.986004415e14, 6378136.3, 7000.0)]
    [InlineData("Moon", "LP165P_to20", Lot2Data, 4.902801056e12, 1738000.0, 1838.0)]
    [InlineData("Mars", "GMM2B_to20", Lot2Data, 4.28283719012840e13, 3397000.0, 3797.0)]
    public void EachField_TakesTheConstantsOfItsModel_OnTheWholeField(string centralBody, string model, string data, double gm,
        double radius, double distanceKm)
    {
        // Arrange
        var caseInput = Parse($"""
                                   type: "state_vector"
                                   position_km: [{distanceKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 100.0, 300.0]
                                   velocity_km_s: [0.0, 0.5, 1.5]
                               """, $"""
                                       geopotential_degree: 20
                                       geopotential_model: "{model}"
                                     """, "[]", NoForces, centralBody: centralBody);

        // Act
        var propagationCase = PropagationCase.Build(caseInput, data);

        // Assert: the acceleration of the field of spec.md section 12.1, bit for bit, the model's GM in the central term
        var body = (CelestialBody)propagationCase.Bodies.Single();
        var state = propagationCase.Spacecraft.InitialOrbitalParameters.ToStateVector();
        var expected = new CelestialBody(body.NaifId, Frames.Frame.ICRF, state.Epoch,
            new GeopotentialModelParameters(Path.Combine(data, model), 20, gm, radius)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });
        Assert.Equal(expected.EvaluateGravitationalAcceleration(state), body.EvaluateGravitationalAcceleration(state));
        Assert.Equal(new[] { model }, GeopotentialFieldCatalog.Fields.Where(f => f.BodyNaifId == body.NaifId).Select(f => f.Model));
    }

    [Fact]
    public void ATdbWindow_AndAWindowEndWithSevenDecimals_AreReadExactly()
    {
        // Arrange: a window tagged TDB (the Moon, Mars and the Sun) and a UTC end with seven decimals (004, 005)
        var tdb = Parse(StateVectorOrbit, NoField, "[]", NoForces, centralBody: "Moon",
            start: "2026-01-20T06:00:00.000 TDB", end: "2026-01-21T06:00:00.000 TDB");
        var utc = Parse(KeplerianOrbit, NoField, "[]", NoForces, end: "2021-03-21T11:56:04.0998101 UTC");

        // Act
        var tdbCase = PropagationCase.Build(tdb, KernelsPath);
        var utcCase = PropagationCase.Build(utc, KernelsPath);

        // Assert
        Assert.Equal(TimeSystem.TimeFrame.TDBFrame, tdbCase.Window.StartDate.Frame);
        Assert.Equal(86400.0, tdbCase.Window.Length.TotalSeconds);
        Assert.Equal(TimeSystem.TimeFrame.TDBFrame, tdbCase.Spacecraft.InitialOrbitalParameters.Epoch.Frame);
        Assert.Equal(TimeSpan.FromTicks(861_640_998_101L), utcCase.Window.Length);
    }

    [Fact]
    public void TheIntegratorOfTheCategory_IsRk78At1em13()
    {
        // Act
        var integrator = PropagatorSolver.CreateIntegrator();

        // Assert
        Assert.Equal(1e-13, integrator.AbsoluteTolerance);
        Assert.Equal(1e-13, integrator.RelativeTolerance);
        Assert.True(integrator.AdaptiveMode);
    }

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
