// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClaudeMdSamples;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Documentation;

/// <summary>
/// The C# examples of the two CLAUDE.md files (#668, third round): each block equals its compiled region of
/// <see cref="ClaudeMdExamples"/> (or <see cref="ClaudeMdTestProcessExamples"/> for the blocks about the test process), so
/// the files cannot drift from code the compiler checks, and the blocks that need only Data/SolarSystem run. Before the
/// third round of #668, 11 of the 27 blocks of IO.Astrodynamics.Net/CLAUDE.md did not compile against the library
/// (alternatives declaring the same locals, <c>Omm.SaveToFile</c> with the parameters of <c>Opm.SaveToFile</c>, an
/// <c>OpmData</c> built from a <c>StateVector</c>, <c>Frames.Frame</c> outside the test namespaces), and two threw: the
/// Earth observation TRIAD of opposite body vectors and the geopotential example with an Earth of other models listed.
/// </summary>
public class ClaudeMdExamplesTests
{
    private static readonly string Docs = Path.Combine(AppContext.BaseDirectory, "Docs");
    private static readonly string ExamplesPath = Path.Combine(Docs, "ClaudeMdExamples.cs");

    private readonly ITestOutputHelper _output;

    public ClaudeMdExamplesTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    #region The files against the compiled regions

    /// <summary>
    /// Every csharp block of each CLAUDE.md file equals its region of <see cref="ClaudeMdExamples"/>, its using directives
    /// aside, and there are as many regions as blocks: red on 66bc7bc, whose 11 blocks that do not compile and two that
    /// throw cannot equal compiled, corrected regions.
    /// </summary>
    [Theory]
    [InlineData("CLAUDE.net.md", "net")]
    [InlineData("CLAUDE.root.md", "root")]
    public void ClaudeMdBlocks_EqualTheirCompiledRegions(string file, string prefix)
    {
        var blocks = DocumentationCodeBlocks.Blocks(Path.Combine(Docs, file));
        Assert.NotEmpty(blocks);
        DocumentationCodeBlocks.AssertBlocksEqualRegions(file, blocks, 0, DocumentationCodeBlocks.Regions(ExamplesPath, prefix));
    }

    // A parameter or a return type, as the declaration names it
    private static string Signature(MethodInfo method) =>
        $"{method.ReturnType.Name} {method.Name}(" + string.Join(", ", method.GetParameters().Select(p =>
            $"{p.ParameterType.Name} {p.Name}{(p.IsOptional ? " = " + (p.DefaultValue ?? "null") : "")}")) + ")";

    /// <summary>
    /// The interface the block "Integrator Interface" declares has the members of the library's
    /// <see cref="IO.Astrodynamics.Propagator.Integrators.IIntegrator"/>: names, return types, parameter types and names,
    /// optional flags and defaults, read by reflection from both declarations.
    /// </summary>
    [Fact]
    public void IIntegratorBlock_DeclaresTheLibraryInterface()
    {
        var library = typeof(IO.Astrodynamics.Propagator.Integrators.IIntegrator);
        var block = typeof(ClaudeMdSamples.Declarations.IIntegrator);
        Assert.True(block.IsInterface);
        var expected = library.GetMethods().OrderBy(m => m.Name).ToList();
        var actual = block.GetMethods().OrderBy(m => m.Name).ToList();
        Assert.Equal(expected.Count, actual.Count);
        Assert.Empty(library.GetProperties());
        Assert.Empty(block.GetProperties());
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(Signature(expected[i]), Signature(actual[i]));
            Assert.Equal(expected[i].ReturnType, actual[i].ReturnType);
            Assert.Equal(expected[i].GetParameters().Select(p => p.ParameterType), actual[i].GetParameters().Select(p => p.ParameterType));
        }
    }

    #endregion

    #region The inputs of the blocks

    private static readonly TimeSystem.Time Epoch = new(2024, 1, 1, 12, 0, 0, TimeScale.TDB);

    private static CelestialBody Earth => PlanetsAndMoons.EARTH_BODY;

    private static CelestialBody Sun => Stars.SUN_BODY;

    // A LEO about the Earth, 6800 km in radius, inclined 51.6 degrees
    private static KeplerianElements Orbit() =>
        new(6800000.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, Earth, Epoch, Frames.Frame.ICRF);

    // A spacecraft of the given id with a tank, an engine on it and a camera (boresight +Z, reference vector +Y)
    private static Spacecraft SpacecraftWithEngine(int naifId, out Engine engine, string cosparId = null)
    {
        var spacecraft = new Spacecraft(naifId, $"CLAUDEMD{-naifId}", 500.0, Orbit(), 1000.0, new Clock($"CLAUDEMD{-naifId}_CLK", 256),
            cosparId: cosparId);
        var tank = new FuelTank("Tank", 100.0, 100.0, "Model", "SN1");
        spacecraft.AddFuelTank(tank);
        engine = new Engine("Engine", 300.0, 0.05, tank, "Model", "SN1");
        spacecraft.AddEngine(engine);
        spacecraft.AddCircularInstrument("Camera", 0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: naifId * 1000 - 1, model: "Imager");
        return spacecraft;
    }

    private static Clock NewClock(string name) => new(name, 256);

    #endregion

    #region The blocks run

    // The blocks that run on Data/SolarSystem alone, with no file written or read and no global state changed. Compiled
    // only: net 3 to 7 (CCSDS files read or written), 15 (a declaration, compared above), 18 and 23 (stars of the
    // caller's) and root 1 (the global data provider, which no test of this process may set)
    public static IEnumerable<object[]> RunnableBlocks() =>
        new[] { "net:1", "net:2", "net:8", "net:9", "net:10", "net:11", "net:12", "net:13", "net:14", "net:16", "net:17", "net:19", "net:20",
                "net:21", "net:22", "net:24", "net:25", "net:26", "net:27", "root:2" }
            .Select(n => new object[] { n });

    /// <summary>A block of the CLAUDE.md files runs without an exception, on the inputs its opening comment names.</summary>
    [Theory]
    [MemberData(nameof(RunnableBlocks))]
    public void Block_Runs(string block)
    {
        Engine engine;
        switch (block)
        {
            case "net:1": Assert.NotNull(ClaudeMdExamples.Net01(Earth, Epoch)); break;
            case "net:2": Assert.NotNull(ClaudeMdExamples.Net02(new TimeSystem.Time(2021, 1, 21, TimeScale.TDB))); break;
            case "net:8": Assert.Single(ClaudeMdExamples.Net08(PropagatedWithImpulse(-6701, out _), Epoch.AddHours(2.0)).Data.Maneuvers); break;
            case "net:9": Assert.NotNull(ClaudeMdExamples.Net09(Orbit().ToStateVector())); break;
            case "net:10": Assert.NotNull(ClaudeMdExamples.Net10(Orbit().ToStateVector())); break;
            case "net:11": ClaudeMdExamples.Net11(); break;
            case "net:12": Assert.NotNull(ClaudeMdExamples.Net12(Epoch, new Window(Epoch, Epoch.AddHours(1)), NewClock("CLAUDEMD12_CLK"))); break;
            case "net:13":
                Assert.NotNull(ClaudeMdExamples.Net13(Epoch, new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
                    SpacecraftWithEngine(-6713, out _)));
                break;
            case "net:14": Assert.NotNull(ClaudeMdExamples.Net14(NewClock("CLAUDEMD14_CLK"), Orbit(), Earth)); break;
            case "net:16":
                SpacecraftWithEngine(-6716, out engine);
                var spacecraft = ClaudeMdExamples.Net16(NewClock("CLAUDEMD16_CLK"), Orbit(), Earth, Epoch, TimeSpan.FromMinutes(30), engine);
                Assert.Equal(Vector3.VectorX, spacecraft.BodyFront);
                break;
            case "net:17":
                var camera = SpacecraftWithEngine(-6717, out engine).Instruments.First();
                Assert.NotNull(ClaudeMdExamples.Net17(Epoch, TimeSpan.FromMinutes(30), camera, Earth, Sun, engine));
                break;
            case "net:19":
                SpacecraftWithEngine(-6719, out engine);
                Assert.NotNull(ClaudeMdExamples.Net19(Epoch, TimeSpan.FromMinutes(30), PlanetsAndMoons.MOON_BODY, Sun, engine));
                break;
            case "net:20":
                SpacecraftWithEngine(-6720, out engine);
                ClaudeMdExamples.Net20(Earth, Epoch, TimeSpan.FromMinutes(30), Sun, engine);
                break;
            case "net:21":
                SpacecraftWithEngine(-6721, out engine);
                ClaudeMdExamples.Net21(Earth, Epoch, TimeSpan.FromMinutes(30), Sun, engine);
                break;
            case "net:22":
                SpacecraftWithEngine(-6722, out engine);
                Assert.NotNull(ClaudeMdExamples.Net22(Epoch, Earth, Sun, engine));
                break;
            case "net:24":
                SpacecraftWithEngine(-6724, out engine);
                Assert.NotNull(ClaudeMdExamples.Net24(Earth, Epoch, TimeSpan.FromMinutes(30), engine));
                break;
            case "net:25": Assert.NotNull(ClaudeMdExamples.Net25(Vector3.VectorX, Vector3.VectorY, Vector3.VectorZ)); break;
            case "net:26": ClaudeMdTestProcessExamples.Net26(); break;
            case "net:27":
                var (frame, epoch, deg2Rad) = ClaudeMdTestProcessExamples.Net27();
                Assert.Same(IO.Astrodynamics.Frames.Frame.ICRF, frame);
                LabelAssert.Equal(TimeSystem.Time.J2000TDB, epoch);
                Assert.Equal(System.Math.PI / 180.0, deg2Rad);
                break;
            case "root:2": ClaudeMdTestProcessExamples.Root02(); break;
            default: throw new ArgumentOutOfRangeException(nameof(block), block);
        }
    }

    #endregion

    #region What the blocks state

    /// <summary>
    /// The TLE block's catalog number is the one its text holds: columns 3 to 7 of line 1 (the TLE format), 25544. On
    /// 66bc7bc the block read it from <c>newTle</c>, which it never declared.
    /// </summary>
    [Fact]
    public void TleBlock_GivesTheCatalogNumberOfTheText()
    {
        var tle = ClaudeMdExamples.Net02(new TimeSystem.Time(2021, 1, 21, TimeScale.TDB));
        Assert.Equal(int.Parse(tle.Line1.Substring(2, 5), System.Globalization.CultureInfo.InvariantCulture), tle.NoradCatalogId);
        Assert.Equal(25544, tle.NoradCatalogId);
    }

    /// <summary>
    /// The elements of the OMM block are mean, with the mean motion of the OMM in rad/s: 15.49309423 rev/day × 2π / 86400 s
    /// (CCSDS 502.0-B-3, MEAN_MOTION in rev/day), within the rounding of that product.
    /// </summary>
    [Fact]
    public void MeanElementsBlock_IsMean()
    {
        var elements = ClaudeMdExamples.Net01(Earth, Epoch);
        Assert.Equal(OrbitalElementsType.Mean, elements.ElementsType);
        double expected = 15.49309423 * 2.0 * System.Math.PI / 86400.0;
        Assert.True(System.Math.Abs(elements.MeanMotion() - expected) <= System.Math.Abs(expected) * 2.3e-16,
            $"{elements.MeanMotion():R} against {expected:R}");
    }

    /// <summary>
    /// The OPM blocks hold the state in the units of CCSDS 502.0-B-3, km and km/s: the state's m and m/s divided by 1000.
    /// </summary>
    [Fact]
    public void OpmBlocks_ConvertToKilometres()
    {
        var state = Orbit().ToStateVector();
        foreach (var data in new[] { ClaudeMdExamples.Net09(state), ClaudeMdExamples.Net10(state) })
        {
            Assert.Equal(state.Position.X / 1000.0, data.StateVector.X);
            Assert.Equal(state.Position.Y / 1000.0, data.StateVector.Y);
            Assert.Equal(state.Position.Z / 1000.0, data.StateVector.Z);
            Assert.Equal(state.Velocity.X / 1000.0, data.StateVector.XDot);
            Assert.Equal(state.Velocity.Y / 1000.0, data.StateVector.YDot);
            Assert.Equal(state.Velocity.Z / 1000.0, data.StateVector.ZDot);
        }

        Assert.Equal("STS-001", ClaudeMdExamples.Net09(state).UserDefinedParameters.Parameters["MISSION_ID"]);
        Assert.Equal(1.0e-6, ClaudeMdExamples.Net10(state).Covariance.CxX);
    }

    /// <summary>
    /// The maneuver export block runs on what its opening comment names, a propagated spacecraft with a COSPAR id, here one
    /// whose apogee raise (to 7000 km from the Earth's centre, at the first perigee of a two-hour window) executed: the
    /// loop of the block then exports it, and the OPM of <c>ToOpm(includeManeuvers: true)</c> carries it in the units of
    /// CCSDS 502.0-B-3, Δv in km/s (the Δv in m/s divided by 1000), the expelled mass negative, the duration in seconds and
    /// the ignition epoch in UTC. On 608bcf6 <c>Block_Runs</c> passed an unpropagated spacecraft, so the loop ran zero
    /// times.
    /// </summary>
    [Fact]
    public void ManeuverExportBlock_ExportsTheExecutedImpulse()
    {
        var spacecraft = PropagatedWithImpulse(-6733, out var impulse);
        Assert.Contains(spacecraft.ExecutedManeuvers, m => m is ImpulseManeuver i && i.ThrustWindow.HasValue);
        Assert.Same(impulse, Assert.Single(spacecraft.ExecutedManeuvers.OfType<ImpulseManeuver>()));

        var opm = ClaudeMdExamples.Net08(spacecraft, Epoch.AddHours(2.0));
        var exported = Assert.Single(opm.Data.Maneuvers);
        Assert.Equal(impulse.DeltaV.X / 1000.0, exported.ManDv1);
        Assert.Equal(impulse.DeltaV.Y / 1000.0, exported.ManDv2);
        Assert.Equal(impulse.DeltaV.Z / 1000.0, exported.ManDv3);
        Assert.True(impulse.FuelBurned > 0.0);
        Assert.Equal(-impulse.FuelBurned, exported.ManDeltaMass);
        Assert.Equal(impulse.ThrustWindow!.Value.Length.TotalSeconds, exported.ManDuration);
        Assert.Equal(impulse.ThrustWindow.Value.StartDate.ToUTC().DateTime, exported.ManEpochIgnition);

        // The input of 608bcf6: unpropagated, no executed maneuver, nothing exported
        var unpropagated = SpacecraftWithEngine(-6734, out _, "2024-001A");
        Assert.Empty(unpropagated.ExecutedManeuvers);
        Assert.Empty(ClaudeMdExamples.Net08(unpropagated, Epoch.AddHours(2.0)).Data.Maneuvers ?? []);
    }

    // A spacecraft of the given id with a COSPAR id, propagated over two hours about a point-mass Earth with an apogee raise
    // to 7000 km from the Earth's centre, which fires at the first perigee
    private static Spacecraft PropagatedWithImpulse(int naifId, out ImpulseManeuver impulse)
    {
        var spacecraft = SpacecraftWithEngine(naifId, out var engine, "2024-001A");
        impulse = new ApogeeHeightManeuver(Earth, Epoch, TimeSpan.Zero, 7000000.0, engine);
        spacecraft.SetManeuvers(impulse);
        spacecraft.Propagate(new Window(Epoch, Epoch.AddHours(2.0)), new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60.0) }, OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = [Earth] });
        return spacecraft;
    }

    // U.S. Standard Atmosphere 1976 (NASA-TM-X-74335), table I at 10 km of geometric altitude: 4.1351e-1 kg/m³; the
    // library's documented agreement with its table I, 4.2e-4 relative
    private const double StandardDensityAt10Km = 4.1351e-1;
    private const double StandardTableAgreement = 4.2e-4;

    /// <summary>
    /// The atmosphere block: the altitude-only standard atmosphere at 10 km (geometric, as the library reads it) is the 1976
    /// standard's table value within its documented agreement, a model-less Earth gives the same with an altitude only and
    /// the NRLMSISE-00 of nominal space weather with the full context (#674, decided by S. Guillet, 2026-11-14).
    /// </summary>
    [Fact]
    public void AtmosphereBlock()
    {
        var (standard, thermosphere, earthFull, earthLow, fullContext) = ClaudeMdExamples.Net11();
        double relative = System.Math.Abs(standard.Density - StandardDensityAt10Km) / StandardDensityAt10Km;
        _output.WriteLine($"MEASURE #668 CLAUDE.md atmosphere block: standard density at 10 km {standard.Density:E6} kg/m3, {relative:E3} from table I");
        Assert.True(relative <= StandardTableAgreement, $"{relative:E3}");
        Assert.Null(standard.Details);
        Assert.Equal(standard.Density, earthLow.Density);
        Assert.Equal(standard.Temperature, earthLow.Temperature);
        Assert.Equal(standard.Pressure, earthLow.Pressure);

        Assert.IsType<NrlmsiseDetails>(thermosphere.Details);
        var nominal = new Nrlmsise00Model(SpaceWeather.Nominal).GetAtmosphere(fullContext);
        Assert.Equal(nominal.Density, thermosphere.Density);
        Assert.Equal(nominal.Density, earthFull.Density);
        Assert.Equal(nominal.Temperature, earthFull.Temperature);
    }

    /// <summary>
    /// The geopotential block of 66bc7bc, read naturally with an orbit about <c>EARTH_BODY</c>, throws: a listed body of the
    /// observer's identifier with another gravitational field is rejected (#674). The corrected block builds its orbit on
    /// the degree-10 Earth, and its propagator builds.
    /// </summary>
    [Fact]
    public void GeopotentialBlock_ListedEarthOfOtherModels_Throws()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var spacecraft = new Spacecraft(-6730, "CLAUDEMD6730", 100.0, Orbit(), 10000.0, NewClock("CLAUDEMD6730_CLK"));
        var exception = Assert.Throws<ArgumentException>(() => new CentralBodyPropagator(new Window(Epoch, Epoch.AddHours(1)), spacecraft, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0), ThirdBodies = [earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY] }));
        Assert.Equal("celestialBodies", exception.ParamName);

        Assert.NotNull(ClaudeMdExamples.Net12(Epoch, new Window(Epoch, Epoch.AddHours(1)), NewClock("CLAUDEMD12B_CLK")));
    }

    // The angle between two vectors, by atan2(|a × b|, a · b), which resolves angles near 0 to the rounding of the
    // components (Vector3.Angle's acos resolves only about 1.5e-8 rad there)
    private static double AngleBetween(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a.Dot(b));

    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (the same on the three, CI run 43745883709, 2026-11-15;
    // MEASURE line below), rounded up at the second significant digit: the angles of the Earth observation TRIAD's Down to
    // the closed-form nadir and of its Right to the component of the SPICE geometric Sun direction perpendicular to nadir,
    // over 24 epochs 4 min apart: 2.483e-16 and 6.489e-16 rad, the rounding of the TRIAD rotation (Vector3.Angle's acos
    // gave 1.490e-8 and 2.107e-8 rad, its own resolution)
    private const double EarthObservationDownLimit = 5.0e-16;
    private const double EarthObservationRightLimit = 1.3e-15;

    /// <summary>
    /// The corrected Earth observation block points <c>Spacecraft.Down</c> at nadir, −r/|r| (the closed form), and
    /// <c>Spacecraft.Right</c> (+X) at the component of the Sun direction perpendicular to nadir (TRIAD's
    /// secondary), the Sun's geometric state from the Earth read from SPICE (<c>SpiceAPI.ReadEphemeris</c>), over 24 epochs
    /// 4 min apart of a LEO.
    /// </summary>
    [Fact]
    public void EarthObservationBlock_PointsDownAndRight()
    {
        SpacecraftWithEngine(-6731, out var engine);
        var attitude = ClaudeMdExamples.Net22(Epoch, Earth, Sun, engine);
        var orbit = Orbit();
        double worstDown = 0.0, worstRight = 0.0;
        for (int k = 0; k < 24; k++)
        {
            var state = orbit.AtEpoch(Epoch.AddSeconds(240.0 * k)).ToStateVector();
            var q = attitude.ComputeOrientation(state);
            var nadir = state.Position.Inverse() / state.Position.Magnitude();
            var sunFromEarth = SpiceAPI.Instance.ReadEphemeris(state.Epoch, PlanetsAndMoons.EARTH.NaifId, Stars.Sun.NaifId, Frames.Frame.ICRF,
                Aberration.None).Position;
            var sunDirection = sunFromEarth - state.Position;
            var perpendicular = sunDirection - nadir * sunDirection.Dot(nadir);
            worstDown = System.Math.Max(worstDown, AngleBetween(Spacecraft.Down.Rotate(q), nadir));
            worstRight = System.Math.Max(worstRight, AngleBetween(Spacecraft.Right.Rotate(q), perpendicular));
        }

        _output.WriteLine($"MEASURE #668 CLAUDE.md Earth observation block: Down to nadir {worstDown:E3} rad, Right to the Sun's perpendicular {worstRight:E3} rad");
        Assert.True(worstDown <= EarthObservationDownLimit, $"{worstDown:E3} rad");
        Assert.True(worstRight <= EarthObservationRightLimit, $"{worstRight:E3} rad");
    }

    /// <summary>
    /// The body vectors of the block before the third round of #668, <c>Spacecraft.Down</c> and <c>Spacecraft.Up</c>, are
    /// 180 degrees apart: <c>TriadAttitude</c> rejects them, its body vectors at least 5 degrees from collinear.
    /// </summary>
    [Fact]
    public void EarthObservationBlock_OldBodyVectors_Throw()
    {
        SpacecraftWithEngine(-6732, out var engine);
        var exception = Assert.Throws<ArgumentException>(() => new TriadAttitude(Epoch, TimeSpan.FromMinutes(30),
            Spacecraft.Down, Earth, Spacecraft.Up, Sun));
        Assert.Contains("collinear", exception.Message);
    }

    #endregion
}
