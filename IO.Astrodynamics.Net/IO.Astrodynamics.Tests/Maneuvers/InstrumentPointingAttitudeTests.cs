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
        var spc = TestHelpers.Spacecraft;
        spc.AddCircularInstrument("CAM602", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0), naifId: -172600, model: "mod1");
        InstrumentPointingToAttitude attitude =
            new InstrumentPointingToAttitude(spc.Instruments.First(), new CelestialAttitudeTarget(TestHelpers.EarthAtJ2000, Aberration.LT), new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromHours(1.0));
        LabelAssert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), attitude.MinEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.HoldDuration);
        Assert.Equal(spc.Instruments.First(), attitude.Instrument);
        Assert.Equal(TestHelpers.EarthAtJ2000, ((CelestialAttitudeTarget)attitude.Target).Target);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(-System.Math.PI / 2.0, 0.0, 0.0)]
    [InlineData(0.0, System.Math.PI / 2.0, 0.0)]
    [InlineData(0.3, 0.5, 0.2)]
    [InlineData(-0.7, 1.1, 2.3)]
    public void Execute(double x, double y, double z)
    {
        var epoch = new TimeSystem.Time(2021, 01, 01, 13, 0, 0, TimeScale.TDB);
        var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, epoch,
            Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, orbitalParams, 3000.0, new Clock("GenericClk", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));

        spc.AddCircularInstrument("inst", 1.57, Vector3.VectorZ, Vector3.VectorY, new Vector3(x, y, z), naifId: -666600, model: "instmodel");
        InstrumentPointingToAttitude maneuver =
            new InstrumentPointingToAttitude(spc.Instruments.First(), new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.LT), new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromSeconds(10.0),
                TestHelpers.MoonAtJ2000);
        Assert.Equal(Aberration.LT, ((CelestialAttitudeTarget)maneuver.Target).Aberration);
        var (_, maneuverOrientation) = maneuver.TryExecute(orbitalParams.ToStateVector());

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
        double angle = Body.AttitudeRecordTests.RotationAngle(Body.AttitudeRecordTests.MinimalRotation(boresight, toMoon), v => v.Rotate(maneuverOrientation.Rotation));
        double pointing = Body.AttitudeRecordTests.Angle(boresight.Rotate(maneuverOrientation.Rotation), toMoon);
        _output.WriteLine($"MEASURE InstrumentPointing ({x}, {y}, {z}): attitude - closed form {angle:E3} rad, boresight from the Moon {pointing:E3} rad");
        Assert.True(angle <= InstrumentPointingTolerance, $"attitude {angle:E3} rad from the closed form");
        Assert.True(pointing <= BoresightTolerance, $"boresight {pointing:E3} rad from the Moon");
        Assert.Equal(Frames.Frame.ICRF, maneuverOrientation.ReferenceFrame);
        Assert.Equal(0.0, maneuver.FuelBurned);
        LabelAssert.Equal(new Window(epoch, TimeSpan.FromSeconds(10.0)), maneuver.ManeuverWindow);
        LabelAssert.Equal(new Window(epoch, TimeSpan.Zero), maneuver.ThrustWindow);
    }

    // Twice the worst over the five orientations on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669), rounded up
    // at two digits: the attitude against the closed form on spkcvo_c's direction, 5.210e-14 rad at (-pi/2, 0, 0) on Linux
    // x64 and macOS arm64, 5.239e-14 rad on Windows x64, and the boresight from it, 3.885e-14 rad on Linux x64 and macOS
    // arm64, 3.897e-14 rad on Windows x64; the light time of the library against
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
        var spc = new Spacecraft(id, $"PT{-id}", 1000.0, state, 3000.0, new Clock($"PT{-id}_CLK", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));
        spc.AddCircularInstrument("inst", 0.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.3, 0.5, 0.2), naifId: id * 1000 - 1, model: "instmodel");
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
                var attitude = new InstrumentPointingToAttitude(instrument, new CelestialAttitudeTarget(moon, flag), new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero);
                var orientation = attitude.ComputeStateOrientation(state);
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
                var attitude = new InstrumentPointingToAttitude(instrument, new CelestialAttitudeTarget(site, flag), new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero);
                var orientation = attitude.ComputeStateOrientation(state);
                var boresight = instrument.GetBoresightInSpacecraftFrame().Rotate(orientation.Rotation);
                worst = System.Math.Max(worst, Body.AttitudeRecordTests.Angle(boresight, expected));
            }

            _output.WriteLine($"MEASURE #549 the site from S {flag.GetDescription(),-6}: worst boresight from spkcpt {worst:E3} rad");
            Assert.True(worst <= SitePointingTolerance, $"{flag}: {worst:E3} rad");
        }
    }

    /// <summary>
    /// Review of #549: a <see cref="Star"/> target takes no light time, its "+S" corrections the stellar aberration alone,
    /// as <see cref="Star.GetEphemeris"/> reads it (its catalog position is the direction of the light at the barycenter,
    /// confirmed by S. Guillet, 2026-11-11; that reading is checked against SOFA's <c>iauStarpm</c> and CSPICE in
    /// <see cref="Occultation.StarOccultationTests"/>): the closed form, the star at its catalog position moved by its
    /// proper motion to the epoch (<see cref="Star.GetEquatorialCoordinates"/>) minus the spacecraft from the barycenter at
    /// the epoch, and for "+S" that direction rotated by <c>stelab</c> (<see cref="Star.GetEphemeris"/>). At 20 pc and
    /// 0.1″/yr the light time moved the boresight by 4.47e-5 rad (9.22″, LT) before this fix, and at 2000 pc its epoch
    /// was beyond the range of a <see cref="TimeSystem.Time"/>, which threw.
    /// </summary>
    [Theory]
    [InlineData(20.0)]
    [InlineData(2000.0)]
    public void PointsAtAStarWithoutLightTime(double distancePc)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var (epoch, s, _) = Pointing()[3];
        var state = new StateVector(new Vector3(s[0], s[1], s[2]), new Vector3(s[3], s[4], s[5]), earth, epoch, Frames.Frame.ICRF);
        const double arcsec = System.Math.PI / 180.0 / 3600.0;
        var star = new Star(9549, "S9549", 1.989e30, "G2V", 5.0, 1.0 / distancePc, Equatorial.FromRightAscensionDeclination(0.3, 1.2, 1.0, epoch.AddDays(-36525.0)),
            0.1 * arcsec, 0.1 * arcsec, 0.0, 0.0, 0.0, 0.0, epoch.AddDays(-36525.0));
        foreach (var flag in Flags)
        {
            var (spc, instrument) = PointingSpacecraft(-5493, state);
            var attitude = new InstrumentPointingToAttitude(instrument, new CelestialAttitudeTarget(star, flag), new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero);
            var orientation = attitude.ComputeStateOrientation(state);
            var boresight = instrument.GetBoresightInSpacecraftFrame().Rotate(orientation.Rotation);
            var geometric = star.GetEquatorialCoordinates(epoch).ToCartesian() - (state.Position + earth.GetGeometricStateFromICRF(epoch).ToStateVector().Position);
            var expected = flag is Aberration.LTS or Aberration.CNS or Aberration.XLTS or Aberration.XCNS
                ? star.GetEphemeris(epoch, spc, Frames.Frame.ICRF, flag).Position
                : geometric;
            double angle = Body.AttitudeRecordTests.Angle(boresight, expected);
            _output.WriteLine($"MEASURE #549 review: a star at {distancePc} pc {flag.GetDescription(),-6}: boresight from the closed form {angle:E3} rad");
            Assert.True(angle <= StarPointingTolerance, $"{distancePc} pc {flag}: {angle:E3} rad");
        }
    }

    // Twice the worst over the corrections on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669), rounded up at two
    // digits: the Moon 1.012e-13 rad (CN), the site 1.140e-12 rad (XCN) on the three, the light time of the library against
    // CSPICE's at a range of a few thousand km; geometric 2.3e-16 rad for the Moon, 2.9e-16 rad for the site on Linux x64
    // and 5.4e-16 rad on Windows x64 and macOS arm64. Since lot L5b the preview takes the state about its own observer, the
    // Earth, where the centre was the Moon (decision 62 of #668: the centre the run infers, rounding only) and the
    // direction is CelestialAttitudeTarget's: re-measured the Moon 1.122e-13 rad (CN+S; 1.120e-13 CN), the site 1.140e-12 rad
    // (XCN, its centre the Earth before and after), geometric 1.99e-16 rad for the Moon, the
    // same on Linux x64, Windows x64 and macOS arm64, and 2.24e-16 rad for the site on Linux x64, 5.06e-16 rad on Windows x64
    // and macOS arm64 (CI of #820 at 5d97c750), within the tolerances
    private const double MoonPointingTolerance = 2.1e-13;
    private const double SitePointingTolerance = 2.3e-12;

    // Twice the worst over the corrections and the two distances on Linux x64, Windows x64 and macOS arm64 (the MEASURE
    // lines of PointsAtAStarWithoutLightTime, CI run 44225490669), rounded up at two digits: 1.573e-16 rad on the three
    // (XLT+S and XCN+S at 20 pc), where the light time moved the boresight by 4.5e-5 rad (9.2″) at 20 pc. Since lot L5b
    // (decision 62 of #668, the preview about the Earth, the state's observer, where the centre was the star): re-measured
    // 1.138e-16 rad (the geometric corrections at 20 pc), the same on Linux x64, Windows x64 and macOS arm64 (CI of #820 at
    // 5d97c750), within the tolerance
    private const double StarPointingTolerance = 3.2e-16;
}