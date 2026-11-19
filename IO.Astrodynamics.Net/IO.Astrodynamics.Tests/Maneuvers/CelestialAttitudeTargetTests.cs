using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class CelestialAttitudeTargetTests
{
    private readonly ITestOutputHelper _output;

    public CelestialAttitudeTargetTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Aberration[] Flags =
    [
        Aberration.None, Aberration.LT, Aberration.LTS, Aberration.CN, Aberration.CNS, Aberration.XLT, Aberration.XLTS,
        Aberration.XCN, Aberration.XCNS
    ];

    // #740: twice the worst angle from spkcvo_c over the eight states of the "pointing" rows and the nine corrections
    // over Linux x64, Windows x64 and macOS arm64, the same on the three (the MEASURE lines, CI run 44395076184,
    // 2026-11-18), rounded up at two digits. The Moon 1.120e-13 rad (CN+S), the Sun 1.629e-16 rad (CN), the Earth,
    // the centre of the state at 6778 km, 6.235e-12 rad (LT): 4.2e-5 m across the line of sight, below the 3.6 mm that the
    // Earth's 30 km/s cover in the 1.2e-7 s of a double 2024 epoch; the TRIAD primary 5.834e-14 rad and the plane of its
    // secondary 7.418e-15 rad
    private const double MoonTolerance = 2.3e-13;
    private const double SunTolerance = 3.3e-16;
    private const double EarthTolerance = 1.3e-11;
    private const double TriadPrimaryTolerance = 1.2e-13;
    private const double TriadPlaneTolerance = 1.5e-14;

    /// <summary>The states of the "pointing" rows of <c>tools/light_time_reference</c> (a LEO about the Earth, J2000) and
    /// the position of <paramref name="target"/> that <c>spkcvo_c</c> gives from each, per correction.</summary>
    private static (TimeSystem.Time Epoch, StateVector State, System.Collections.Generic.Dictionary<Aberration, Vector3> Expected)[] Pointing(
        string target, CelestialBody earth)
    {
        var reference = JsonDocument.Parse(File.ReadAllText("Data/LightTimeReference/light_time_reference.json")).RootElement;
        return reference.GetProperty("pointing").EnumerateArray().Select(p =>
        {
            var epoch = TimeSystem.Time.CreateTDB(p.GetProperty("epoch_tdb").GetDouble());
            var s = p.GetProperty("state").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var state = new StateVector(new Vector3(s[0], s[1], s[2]), new Vector3(s[3], s[4], s[5]), earth, epoch, Frames.Frame.ICRF);
            var expected = Flags.ToDictionary(f => f, f =>
            {
                var v = p.GetProperty(target).GetProperty(f.GetDescription()).EnumerateArray().Select(x => x.GetDouble()).ToArray();
                return new Vector3(v[0], v[1], v[2]);
            });
            return (epoch, state, expected);
        }).ToArray();
    }

    private static double Angle(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    /// <summary>
    /// #740: the direction of a <see cref="CelestialAttitudeTarget"/> is the target as the spacecraft sees it, as
    /// <c>spkcvo_c</c> corrects it from the spacecraft's state (light time of <c>spkltc</c> from its position, stellar
    /// aberration of <c>stelab</c> from its SSB velocity), for the Moon, the Sun and the Earth, the centre of the state,
    /// and the nine corrections. Before the fix the correction was the central body's (measured on f70ee32: the Moon with
    /// LT 6.9e-7 rad off, with LT+S 2.5e-5 rad; the Earth, uncorrected, 4.2e-5 rad with LT).
    /// </summary>
    [Theory]
    [InlineData("moon", 301, MoonTolerance)]
    [InlineData("sun", 10, SunTolerance)]
    [InlineData("earth", 399, EarthTolerance)]
    public void GetDirection_IsTheTargetAsSpkcvoSeesItFromTheSpacecraft(string label, int targetId, double tolerance)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var target = new CelestialBody(targetId);
        var worst = Flags.ToDictionary(f => f, _ => 0.0);
        foreach (var flag in Flags)
        {
            foreach (var (_, state, expected) in Pointing(label, earth))
            {
                var direction = new CelestialAttitudeTarget(target, flag).GetDirection(state);
                worst[flag] = System.Math.Max(worst[flag], Angle(direction, expected[flag]));
                Assert.Equal(1.0, direction.Magnitude(), 15);
            }

            _output.WriteLine($"MEASURE #740 {label} {flag.GetDescription(),-6}: worst direction from spkcvo {worst[flag]:E3} rad");
        }

        foreach (var flag in Flags)
        {
            Assert.True(worst[flag] <= tolerance, $"{label} {flag}: {worst[flag]:E3} rad");
        }
    }

    /// <summary>
    /// #740: a <see cref="TriadAttitude"/> on two celestial targets with light time takes their directions from the
    /// spacecraft: its primary body vector on the Moon as <c>spkcvo_c</c> gives it with LT, its secondary in the plane of
    /// the Moon and the Sun with LT+S, on the Sun's side. Before the fix the primary was 6.9e-7 rad off and the plane
    /// turned by the Sun's 7.8e-6 rad.
    /// </summary>
    [Fact]
    public void TriadAttitude_OnCelestialTargets_TakesTheirDirectionsFromTheSpacecraft()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);
        var suns = Pointing("sun", earth);
        double worstPrimary = 0.0, worstPlane = 0.0;
        foreach (var ((epoch, state, moonExpected), k) in Pointing("moon", earth).Select((p, k) => (p, k)))
        {
            var spc = new Spacecraft(-7401, "S740", 1000.0, 3000.0, new Clock("S740_CLK", 65536), state);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            var attitude = new TriadAttitude(earth, epoch, TimeSpan.Zero, Spacecraft.Front, new CelestialAttitudeTarget(moon, Aberration.LT),
                Spacecraft.Up, new CelestialAttitudeTarget(sun, Aberration.LTS), spc.Engines.First());
            var q = attitude.ComputeOrientation(state);
            var m = moonExpected[Aberration.LT].Normalize();
            var s = suns[k].Expected[Aberration.LTS].Normalize();
            worstPrimary = System.Math.Max(worstPrimary, Angle(Spacecraft.Front.Rotate(q), m));
            // The secondary body vector in the half-plane of the Moon and the Sun: the normals of the two planes agree
            worstPlane = System.Math.Max(worstPlane, Angle(m.Cross(Spacecraft.Up.Rotate(q)), m.Cross(s)));
        }

        _output.WriteLine($"MEASURE #740 TRIAD primary on the Moon (LT) {worstPrimary:E3} rad, plane of the Sun (LT+S) {worstPlane:E3} rad");
        Assert.True(worstPrimary <= TriadPrimaryTolerance, $"primary {worstPrimary:E3} rad");
        Assert.True(worstPlane <= TriadPlaneTolerance, $"plane {worstPlane:E3} rad");
    }

    [Fact]
    public void Constructor_NullTarget_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CelestialAttitudeTarget(null));
    }

    [Fact]
    public void GetDirection_MatchesManualEphemerisComputation()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);
        var direction = target.GetDirection(sv);

        // Manually compute the same direction (default is now Aberration.None)
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(sv.Epoch, sv.Observer, sv.Frame, Aberration.None);
        var expectedDirection = (moonEphemeris.ToStateVector().Position - sv.Position).Normalize();

        Assert.True(System.Math.Abs(direction.X - expectedDirection.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y - expectedDirection.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z - expectedDirection.Z) < 1e-10);
    }

    [Fact]
    public void GetDirection_ReturnsUnitVector()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var target = new CelestialAttitudeTarget(TestHelpers.Sun);
        var direction = target.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.Magnitude() - 1.0) < 1e-10);
    }

    [Fact]
    public void Name_ReturnsTargetName()
    {
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);
        Assert.Equal("MOON", target.Name);
    }

    [Fact]
    public void GetDirection_NullObserverState_ThrowsArgumentNullException()
    {
        // Arrange
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => target.GetDirection(null));
    }

    [Fact]
    public void DefaultAberration_IsNone()
    {
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);
        Assert.Equal(Aberration.None, target.Aberration);
    }

    [Fact]
    public void ExplicitAberration_LT_IsStored()
    {
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.LT);
        Assert.Equal(Aberration.LT, target.Aberration);
    }

    [Fact]
    public void GetDirection_WithNone_MatchesGeometricEphemeris()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.None);
        var direction = target.GetDirection(sv);

        // Manually compute with Aberration.None
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(sv.Epoch, sv.Observer, sv.Frame, Aberration.None);
        var expectedDirection = (moonEphemeris.ToStateVector().Position - sv.Position).Normalize();

        Assert.True(System.Math.Abs(direction.X - expectedDirection.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y - expectedDirection.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z - expectedDirection.Z) < 1e-10);
    }

    [Fact]
    public void GetDirection_LT_DiffersFromNone()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var targetLT = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.LT);
        var targetNone = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.None);

        var directionLT = targetLT.GetDirection(sv);
        var directionNone = targetNone.GetDirection(sv);

        // LT and None should produce different directions (Moon light-time is ~1.28s)
        var diff = new Vector3(
            directionLT.X - directionNone.X,
            directionLT.Y - directionNone.Y,
            directionLT.Z - directionNone.Z);

        Assert.True(diff.Magnitude() > 1e-12, "LT and None should produce measurably different directions");
    }
}
