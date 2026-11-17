using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class InstrumentPointingAttitudeTests
{
    private readonly ITestOutputHelper _output;

    public InstrumentPointingAttitudeTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);
        var spc = TestHelpers.Spacecraft;
        spc.AddCircularInstrument(-172600, "CAM602", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
        InstrumentPointingToAttitude attitude =
            new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromHours(1.0), spc.Instruments.First(), TestHelpers.EarthAtJ2000, eng);
        Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
        Assert.Equal(spc.Instruments.First(), attitude.Instrument);
        Assert.Equal(TestHelpers.EarthAtJ2000, attitude.Target);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(-System.Math.PI / 2.0, 0.0, 0.0)]
    [InlineData(0.0, System.Math.PI / 2.0, 0.0)]
    [InlineData(0.3, 0.5, 0.2)]
    [InlineData(-0.7, 1.1, 2.3)]
    public void Execute(double x, double y, double z)
    {
        var epoch = new TimeSystem.Time(2021, 01, 01, 13, 0, 0);
        var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, epoch,
            Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

        spc.AddCircularInstrument(-666600, "inst", "instmodel", 1.57, Vector3.VectorZ, Vector3.VectorY, new Vector3(x, y, z));
        InstrumentPointingToAttitude maneuver =
            new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromSeconds(10.0), spc.Instruments.First(), TestHelpers.MoonAtJ2000, spc.Engines.First());
        Assert.Equal(Aberration.LT, maneuver.Aberration);
        maneuver.TryExecute(orbitalParams.ToStateVector());

        // The closed form: the minimal rotation taking the boresight, in the spacecraft frame by the Euler angles of the frame
        // kernel (Rx(-x) Ry(-y) Rz(-z), Frames Required Reading, written in the test, #524), to the direction of the Moon
        // as the spacecraft sees it with LT, the default (#549): spkcvo_c from this state (tools/light_time_reference, the
        // last state of "pointing"), compared as a rotation, the roll included. Before 11.0.0 the direction was the geometric
        // one, 4.4e-5 rad away, the light time the maneuver asked for not applied to the Moon, its own centre (#549); before
        // #537 the attitude was the inverse rotation (reader 4 of the design of #537)
        var row = Pointing().Single(p => System.Math.Abs((p.Epoch - epoch).TotalSeconds) < 1e-6);
        Assert.Equal(orbitalParams.Position, new Vector3(row.State[0], row.State[1], row.State[2]));
        var toMoon = row.Moon[Aberration.LT];
        var boresight = Body.AttitudeRecordTests.EulerToSpacecraft(new Vector3(x, y, z), Vector3.VectorZ);
        double angle = Body.AttitudeRecordTests.RotationAngle(Body.AttitudeRecordTests.MinimalRotation(boresight, toMoon), v => v.Rotate(maneuver.StateOrientation.Rotation));
        double pointing = Body.AttitudeRecordTests.Angle(boresight.Rotate(maneuver.StateOrientation.Rotation), toMoon);
        _output.WriteLine($"MEASURE InstrumentPointing ({x}, {y}, {z}): attitude - closed form {angle:E3} rad, boresight from the Moon {pointing:E3} rad");
        Assert.True(angle <= InstrumentPointingTolerance, $"attitude {angle:E3} rad from the closed form");
        Assert.True(pointing <= BoresightTolerance, $"boresight {pointing:E3} rad from the Moon");
        Assert.Equal(Frames.Frame.ICRF, maneuver.StateOrientation.ReferenceFrame);
        Assert.Equal(0.0, maneuver.FuelBurned);
        Assert.Equal(new Window(epoch, TimeSpan.FromSeconds(10.0)), maneuver.ManeuverWindow);
        Assert.Equal(new Window(epoch, TimeSpan.Zero), maneuver.ThrustWindow);
    }

    // Provisional, twice the worst over the five orientations on Linux x64 (2026-11-17), rounded up at two digits, to be set
    // again from the Windows x64 and macOS arm64 CI logs: the attitude against the closed form on spkcvo_c's direction,
    // 5.21e-14 rad at (-pi/2, 0, 0), and the boresight from it, 3.89e-14 rad; the light time of the library against
    // CSPICE's (the Moon's position 1.5e-5 m apart at 384000 km), where the geometric direction held 4.4e-16 rad
    private const double InstrumentPointingTolerance = 1.1e-13;
    private const double BoresightTolerance = 7.8e-14;

    private static readonly Aberration[] Flags =
    [
        Aberration.None, Aberration.LT, Aberration.LTS, Aberration.CN, Aberration.CNS, Aberration.XLT, Aberration.XLTS,
        Aberration.XCN, Aberration.XCNS
    ];

    private static JsonElement Reference() =>
        JsonDocument.Parse(File.ReadAllText("Data/LightTimeReference/light_time_reference.json")).RootElement;

    private static Vector3 ToVector(JsonElement e)
    {
        var v = e.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        return new Vector3(v[0], v[1], v[2]);
    }

    /// <summary>The states of the "pointing" rows of <c>tools/light_time_reference</c> and the Moon that <c>spkcvo_c</c>
    /// gives from each, per correction.</summary>
    private static (TimeSystem.Time Epoch, double[] State, System.Collections.Generic.Dictionary<Aberration, Vector3> Moon)[] Pointing() =>
        Reference().GetProperty("pointing").EnumerateArray().Select(p => (TimeSystem.Time.CreateTDB(p.GetProperty("epoch_tdb").GetDouble()),
            p.GetProperty("state").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
            Flags.ToDictionary(f => f, f => ToVector(p.GetProperty("moon").GetProperty(f.GetDescription()))))).ToArray();

    private static (Spacecraft Spacecraft, Instrument Instrument) PointingSpacecraft(int id, StateVector state)
    {
        var spc = new Spacecraft(id, $"PT{-id}", 1000.0, 3000.0, new Clock($"PT{-id}_CLK", 65536), state);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        spc.AddCircularInstrument(id * 1000 - 1, "inst", "instmodel", 0.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.3, 0.5, 0.2));
        return (spc, spc.Instruments.First());
    }

    /// <summary>
    /// #549: the boresight on the Moon as <c>spkcvo_c</c> sees it from each state of the LEO of the issue (a = 6778.137 km,
    /// e = 0, i = 51.6°, every 600 s over 1 h, and the state of <see cref="Execute"/>), for the nine corrections. Before
    /// 11.0.0 every correction gave the geometric direction, 4.2e-5 to 4.5e-5 rad from LT's.
    /// </summary>
    [Fact]
    public void PointsAtTheMoonAsSpkcvo()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        foreach (var flag in Flags)
        {
            double worst = 0.0;
            foreach (var (epoch, s, expected) in Pointing())
            {
                var state = new StateVector(new Vector3(s[0], s[1], s[2]), new Vector3(s[3], s[4], s[5]), earth, epoch, Frames.Frame.ICRF);
                var (spc, instrument) = PointingSpacecraft(-5491, state);
                var attitude = new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, instrument, moon,
                    spc.Engines.First(), flag);
                var (_, orientation) = attitude.TryExecute(state);
                var boresight = instrument.GetBoresightInSpacecraftFrame().Rotate(orientation.Rotation);
                worst = System.Math.Max(worst, Body.AttitudeRecordTests.Angle(boresight, expected[flag]));
            }

            _output.WriteLine($"MEASURE #549 the Moon from the LEO {flag.GetDescription(),-6}: worst boresight from spkcvo {worst:E3} rad");
            Assert.True(worst <= MoonPointingTolerance, $"{flag}: {worst:E3} rad");
        }
    }

    /// <summary>
    /// #549: the boresight on a site given by its coordinates (longitude −117°, latitude 34°, altitude 1000 m) as
    /// <c>spkcpt_c</c> sees it from the spacecraft S of <c>tools/light_time_reference</c>, at its three epochs, for the nine
    /// corrections: the state of S its geometric state from the Earth (the row of <c>spkezr_c</c> without correction).
    /// Before 11.0.0 the light time was that of the site from the Earth's centre, and none for a site given by its
    /// coordinates (#489).
    /// </summary>
    [Fact]
    public void PointsAtACoordinateSiteAsSpkcpt()
    {
        var reference = Reference();
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var siteCoordinates = reference.GetProperty("site");
        var site = new Site(549, "G549", earth, new Planetodetic(siteCoordinates.GetProperty("longitude_deg").GetDouble() * IO.Astrodynamics.Constants.Deg2Rad,
            siteCoordinates.GetProperty("latitude_deg").GetDouble() * IO.Astrodynamics.Constants.Deg2Rad, siteCoordinates.GetProperty("altitude_m").GetDouble()));
        var epochs = reference.GetProperty("epochs_tdb").EnumerateArray().Select(e => TimeSystem.Time.CreateTDB(e.GetDouble())).ToArray();
        var states = reference.GetProperty("states");
        foreach (var flag in Flags)
        {
            double worst = 0.0;
            for (int e = 0; e < epochs.Length; e++)
            {
                var s = states.GetProperty("spacecraft_from_earth").GetProperty("NONE").EnumerateArray().ElementAt(e).EnumerateArray().Select(x => x.GetDouble()).ToArray();
                var state = new StateVector(new Vector3(s[0], s[1], s[2]), new Vector3(s[3], s[4], s[5]), earth, epochs[e], Frames.Frame.ICRF);
                var expected = ToVector(states.GetProperty("site_from_spacecraft").GetProperty(flag.GetDescription()).EnumerateArray().ElementAt(e));
                var (spc, instrument) = PointingSpacecraft(-5492, state);
                var attitude = new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, instrument, site,
                    spc.Engines.First(), flag);
                var (_, orientation) = attitude.TryExecute(state);
                var boresight = instrument.GetBoresightInSpacecraftFrame().Rotate(orientation.Rotation);
                worst = System.Math.Max(worst, Body.AttitudeRecordTests.Angle(boresight, expected));
            }

            _output.WriteLine($"MEASURE #549 the site from S {flag.GetDescription(),-6}: worst boresight from spkcpt {worst:E3} rad");
            Assert.True(worst <= SitePointingTolerance, $"{flag}: {worst:E3} rad");
        }
    }

    [Fact]
    public void RejectsAnUnknownAberration()
    {
        var spc = TestHelpers.Spacecraft;
        spc.AddCircularInstrument(-172601, "CAM603", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        var tank = new FuelTank("ft", "ftA", "123456", 1000.0, 900.0);
        var engine = new Engine("eng", "engmk1", "12345", 450, 50, tank);
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.Zero, spc.Instruments.First(), TestHelpers.MoonAtJ2000, engine, (Aberration)99));
        Assert.Equal("aberration", exception.ParamName);
    }

    // Provisional, twice the worst over the corrections on Linux x64 (2026-11-17), rounded up at two digits, to be set again
    // from the Windows x64 and macOS arm64 CI logs: the Moon 1.01e-13 rad (CN), the site 1.14e-12 rad (XCN), the light time
    // of the library against CSPICE's at a range of a few thousand km; geometric 2.3e-16 and 2.9e-16 rad
    private const double MoonPointingTolerance = 2.1e-13;
    private const double SitePointingTolerance = 2.3e-12;
}