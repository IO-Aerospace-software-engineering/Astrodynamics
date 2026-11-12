using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using FuelTank = IO.Astrodynamics.Body.Spacecraft.FuelTank;
using KeplerianElements = IO.Astrodynamics.OrbitalParameters.KeplerianElements;
using NadirAttitude = IO.Astrodynamics.Maneuver.NadirAttitude;
using Payload = IO.Astrodynamics.Body.Spacecraft.Payload;
using Planetodetic = IO.Astrodynamics.Coordinates.Planetodetic;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Scenario = IO.Astrodynamics.Mission.Scenario;
using Site = IO.Astrodynamics.Surface.Site;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests;

/// <remarks>
/// The epochs of the windows that CSPICE searches (distance, occultation, coordinate, illumination) are those of the same
/// searches in spiceypy, the window ends and the results each the nearest tick of its ET (#625):
/// <c>tools/seconds_to_ticks/spice_windows_reference.py</c>, whose truncated mode gives the strings held before #625.
/// </remarks>
public class SpiceApiTest
{
    private readonly ITestOutputHelper _output;

    public SpiceApiTest(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void CheckVersion()
    {
        Assert.Equal("CSPICE_N0067", SpiceAPI.Instance.GetSpiceVersion());
    }

    [Fact]
    public void FindWindowsOnDistanceConstraintProxy()
    {
        //Find time windows when the moon will be 400000 km away from the Earth
        var res = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(
            new Window(TimeSystem.Time.CreateTDB(220881665.18391809),
                TimeSystem.Time.CreateTDB(228657665.18565452)),
            TestHelpers.EarthAtJ2000.NaifId, TestHelpers.MoonAtJ2000.NaifId, RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0));
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(4, windows.Count());
        Assert.Equal("2007-01-08T00:11:07.6285911 TDB", windows.ElementAt(0).StartDate.ToString());
        Assert.Equal("2007-01-13T06:37:47.9481441 TDB", windows.ElementAt(0).EndDate.ToString());
        Assert.Equal("2007-03-29T22:53:58.1518964 TDB", windows.ElementAt(3).StartDate.ToString());
        Assert.Equal("2007-04-01T00:01:05.1856545 TDB", windows.ElementAt(3).EndDate.ToString());
    }

    [Fact]
    public void FindWindowsOnDistanceConstraintFromIdsProxy()
    {
        //Find time windows when the moon will be 400000 km away from the Earth
        var res = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(
            new Window(TimeSystem.Time.CreateTDB(220881665.18391809),
                TimeSystem.Time.CreateTDB(228657665.18565452)),
            TestHelpers.EarthAtJ2000.NaifId, TestHelpers.MoonAtJ2000.NaifId, RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0));
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(4, windows.Count());
        Assert.Equal("2007-01-08T00:11:07.6285911 TDB", windows.ElementAt(0).StartDate.ToString());
        Assert.Equal("2007-01-13T06:37:47.9481441 TDB", windows.ElementAt(0).EndDate.ToString());
        Assert.Equal("2007-03-29T22:53:58.1518964 TDB", windows.ElementAt(3).StartDate.ToString());
        Assert.Equal("2007-04-01T00:01:05.1856545 TDB", windows.ElementAt(3).EndDate.ToString());
    }

    [Fact]
    public void FindWindowsOnOccultationConstraint()
    {
        //Find time windows when the Sun will be occulted by the moon
        var res = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(
            new Window(TimeSystem.Time.CreateTDB(61473664.183390938),
                TimeSystem.Time.CreateTDB(61646464.183445148)), TestHelpers.EarthAtJ2000.NaifId, TestHelpers.Sun.NaifId,
            ShapeType.Ellipsoid, TestHelpers.MoonAtJ2000.NaifId, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0));
        var windows = res as Window[] ?? res.ToArray();
        Assert.Single(windows);
        Assert.Equal("2001-12-14T20:10:15.4105882 TDB", windows[0].StartDate.ToString());
        Assert.Equal("2001-12-14T21:35:49.1005209 TDB", windows[0].EndDate.ToString());
    }

    [Fact]
    public void FindWindowsOnIlluminationConstraint()
    {
        //Find time windows when the planetodetic point is illuminated by the sun (Official twilight 0.8° bellow horizon)
        var res = SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(
            new Window(TimeSystem.Time.CreateTDB(674524800), TimeSystem.Time.CreateTDB(674611200)),
            TestHelpers.Sun.NaifId, TestHelpers.EarthAtJ2000.NaifId, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), TestHelpers.Sun.NaifId);
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(2, windows.Count());
        Assert.Equal("2021-05-17T12:00:00.0000000 TDB", windows[0].StartDate.ToString());
        Assert.Equal("2021-05-17T19:35:24.9088323 TDB", windows[0].EndDate.ToString());
        Assert.Equal("2021-05-18T04:18:32.4437503 TDB", windows[1].StartDate.ToString());
        Assert.Equal("2021-05-18T12:00:00.0000000 TDB", windows[1].EndDate.ToString());
    }

    [Fact]
    public void FindWindowsOnCoordinateConstraint()
    {
        Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad,
                35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad, 0.107));
        //Find time windows when the moon will be above the horizon relative to Deep Space Station 13
        var res = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(
            new Window(TimeSystem.Time.CreateTDB(730036800.0), TimeSystem.Time.CreateTDB(730123200)), site.NaifId,
            TestHelpers.MoonAtJ2000.NaifId, site.Frame, CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater,
            0.0, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0));

        var windows = res as Window[] ?? res.ToArray();
        Assert.Single(windows);
        Assert.Equal("2023-02-19T14:33:08.9179869 TDB", windows[0].StartDate.ToString());
        Assert.Equal("2023-02-20T00:00:00.0000000 TDB", windows[0].EndDate.ToString());
    }

    [Fact]
    public void FromIDs()
    {
        //Find time windows when the planetodetic point is illuminated by the sun (Official twilight 0.8° bellow horizon)
        var res = SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(
            new Window(TimeSystem.Time.CreateTDB(674524800), TimeSystem.Time.CreateTDB(674611200)),
            TestHelpers.Sun.NaifId, TestHelpers.EarthAtJ2000.NaifId, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), TestHelpers.Sun.NaifId);
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(2, windows.Count());
        Assert.Equal("2021-05-17T12:00:00.0000000 TDB", windows[0].StartDate.ToString());
        Assert.Equal("2021-05-17T19:35:24.9088323 TDB", windows[0].EndDate.ToString());
        Assert.Equal("2021-05-18T04:18:32.4437503 TDB", windows[1].StartDate.ToString());
        Assert.Equal("2021-05-18T12:00:00.0000000 TDB", windows[1].EndDate.ToString());
    }

    [Fact]
    public void ReadEphemeris()
    {
        var searchWindow = new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(100.0));
        var res = SpiceAPI.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000.NaifId, TestHelpers.MoonAtJ2000.NaifId,
            Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0)).Select(x => x.ToStateVector());

        var stateVectors = res as StateVector[] ?? res.ToArray();
        Assert.Equal(-291569264.48965073, stateVectors[0].Position.X);
        Assert.Equal(-266709187.1624887, stateVectors[0].Position.Y);
        Assert.Equal(-76099155.244104564, stateVectors[0].Position.Z);
        Assert.Equal(643.53061483971885, stateVectors[0].Velocity.X);
        Assert.Equal(-666.08181440799092, stateVectors[0].Velocity.Y);
        Assert.Equal(-301.32283209101018, stateVectors[0].Velocity.Z);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, stateVectors[0].Observer.NaifId);
        Assert.Equal(Frames.Frame.ICRF, stateVectors[0].Frame);
        Assert.Equal(0.0, stateVectors[0].Epoch.TimeSpanFromJ2000().TotalSeconds);
    }

    [Fact]
    public void ReadEphemerisUTC()
    {
        var searchWindow = new Window(TimeSystem.Time.CreateUTC(0.0), TimeSystem.Time.CreateUTC(100.0));
        var res = SpiceAPI.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000.NaifId, TestHelpers.MoonAtJ2000.NaifId,
            Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0)).Select(x => x.ToStateVector());

        var stateVectors = res as StateVector[] ?? res.ToArray();
        Assert.Equal(new Vector3(-291527956.4143643, -266751935.53610146, -76118494.37190592), stateVectors[0].Position, TestHelpers.VectorComparer);
        Assert.Equal(new Vector3(643.6475664431179, -665.9766990302671, -301.2930723673155), stateVectors[0].Velocity, TestHelpers.VectorComparer);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, stateVectors[0].Observer.NaifId);
        Assert.Equal(Frames.Frame.ICRF, stateVectors[0].Frame);
        Assert.Equal(0, stateVectors[0].Epoch.TimeSpanFromJ2000().TotalSeconds, 6);
    }

    [Fact]
    public void ReadLongEphemeris()
    {
        var searchWindow = new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(15000.0));
        var res = SpiceAPI.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000.NaifId, TestHelpers.MoonAtJ2000.NaifId,
            Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(1.0)).Select(x => x.ToStateVector());

        Assert.Equal(15001, res.Count());

        var stateVectors = res as StateVector[] ?? res.ToArray();
        Assert.Equal(-291569264.48965073, stateVectors[0].Position.X);
        Assert.Equal(-266709187.1624887, stateVectors[0].Position.Y);
        Assert.Equal(-76099155.244104564, stateVectors[0].Position.Z);
        Assert.Equal(643.53061483971885, stateVectors[0].Velocity.X);
        Assert.Equal(-666.08181440799092, stateVectors[0].Velocity.Y);
        Assert.Equal(-301.32283209101018, stateVectors[0].Velocity.Z);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, stateVectors[0].Observer.NaifId);
        Assert.Equal(Frames.Frame.ICRF, stateVectors[0].Frame);
        Assert.Equal(0.0, stateVectors[0].Epoch.TimeSpanFromJ2000().TotalSeconds);
    }

    [Fact]
    public async Task ReadOrientation()
    {
        TimeSystem.Time start = TimeSystem.Time.CreateTDB(662778000.0);
        TimeSystem.Time end = start.AddSeconds(60.0);
        Window window = new Window(start, end);

        //Configure scenario
        Scenario scenario = new Scenario("Scenario_B", new Astrodynamics.Mission.Mission("mission10"), window);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);

        //Define parking orbit

        StateVector parkingOrbit = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
            start, Frames.Frame.ICRF);

        //Configure spacecraft
        Clock clock = new Clock("clk1", 65536);
        Spacecraft spacecraft =
            new Spacecraft(-1794, "DRAGONFLY4", 1000.0, 10000.0, clock, parkingOrbit);

        FuelTank fuelTank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
        Engine engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank);
        spacecraft.AddFuelTank(fuelTank);
        spacecraft.AddEngine(engine);
        spacecraft.AddPayload(new Payload("payload1", 50.0, "pay01"));
        spacecraft.AddCircularInstrument(-1794600, "CAM600", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);

        spacecraft.SetStandbyManeuver(new NadirAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
            spacecraft.Engines.First()));

        scenario.AddSpacecraft(spacecraft);

        //Execute scenario
        var root = Constants.OutputPath.CreateSubdirectory(scenario.Mission.Name).CreateSubdirectory(scenario.Name).CreateSubdirectory("Spacecrafts");
        await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));
        var ckFile = new FileInfo(root + "/OrientationTestFile.ck");
        var clckFile = new FileInfo(root + "/ClckTestFile.tsc");
        await spacecraft.Clock.WriteAsync(clckFile);
        SpiceAPI.Instance.LoadKernels(clckFile);

        spacecraft.WriteOrientation(ckFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        // The SPK of the same propagation: the reference of the attitude is the nadir of its positions (spkezr)
        var spkFile = new FileInfo(root + "/EphemerisTestFile.bsp");
        spacecraft.WriteEphemeris(spkFile);
        SpiceAPI.Instance.LoadKernels(spkFile);
        //Read spacecraft orientation

        var res = SpiceAPI.Instance.ReadOrientation(window, spacecraft.NaifId, TimeSpan.FromSeconds(10.0), Frames.Frame.ICRF,
            TimeSpan.FromSeconds(10.0)).ToArray();

        // The CK holds the C-matrix, J2000 to the spacecraft frame (#529): the inverse of the nadir attitude, the minimal
        // rotation taking the front (+Y) to -r, r the position of spkezr on the SPK of the same propagation (choice 14 of
        // the design of #537), compared as a rotation angle. ckgpav's angular velocity is that of the spacecraft relative to
        // J2000 in J2000: the rotation of the positions, from spkezr 1 s before and after (the output step), about their
        // normal. Not the closed form pi/2 + n t of the circular orbit: Velocity-Verlet turns its positions at
        // n (1 + (n h)^2 / 6), 2.4e-10 rad/s faster than v / r (the review of the design)
        Vector3 Position(TimeSystem.Time t) => SpiceAPI.Instance.ReadEphemeris(t, 399, spacecraft.NaifId, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        double worstRotation = 0.0, worstRate = 0.0;
        for (int i = 1; i < res.Length - 1; i++)
        {
            var t = res[i].Epoch;
            var nadir = Body.AttitudeRecordTests.MinimalRotation(Spacecraft.Front, Position(t).Inverse());
            worstRotation = System.Math.Max(worstRotation, Body.AttitudeRecordTests.RotationAngle(nadir, v => v.Rotate(res[i].Rotation.Conjugate())));
            var before = Position(t.AddSeconds(-1.0));
            var after = Position(t.AddSeconds(1.0));
            var normal = before.Cross(after);
            var rate = normal.Normalize() * (System.Math.Atan2(normal.Magnitude(), before * after) / 2.0);
            worstRate = System.Math.Max(worstRate, (res[i].AngularVelocity - rate).Magnitude());
        }

        SpiceAPI.Instance.UnloadKernels(spkFile);
        SpiceAPI.Instance.UnloadKernels(ckFile);
        SpiceAPI.Instance.UnloadKernels(clckFile);

        _output?.WriteLine($"MEASURE #529 ReadOrientation: the C-matrix read - the inverse of the nadir of spkezr {worstRotation:E2} rad, the rate - the rotation of spkezr's positions {worstRate:E2} rad/s");
        Assert.True(worstRotation <= ReadOrientationRotation, $"{worstRotation:E3} rad");
        Assert.True(worstRate <= ReadOrientationRate, $"{worstRate:E3} rad/s");
        Assert.Equal(window.StartDate, res.ElementAt(0).Epoch, TestHelpers.TimeComparer);
        Assert.Equal(Frames.Frame.ICRF, res.ElementAt(0).ReferenceFrame);

        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.ReadOrientation(window, spacecraft.NaifId,
            TimeSpan.FromSeconds(10.0), null,
            TimeSpan.FromSeconds(10.0)).ToArray());
    }

    // The limits of ReadOrientation, twice the worst over its five records over Linux, Windows and macOS arm64 (CI run
    // 41522747911, 2026-11-02): 2.57e-16 / 2.57e-16 / 2.57e-16 rad, 3.08e-12 / 3.08e-12 / 3.08e-12 rad/s
    private const double ReadOrientationRotation = 5.2e-16;
    private const double ReadOrientationRate = 6.2e-12;

    [Fact]
    public void ReadOrientationFromExternalCKKernel()
    {
        // PHSRM (Phobos Soil Return Mission) — spacecraft NAIF ID -555 (NAIF convention: negative)
        // SCLK epoch: tick=0 → TDB ≈ 373,960,866 s from J2000 (2011-11-07)
        // CK coverage: 2011-11-08 to 2013-02-14; CK object ID = -555000
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName,
            "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName,
            "phsrm_sc_test2_111108_130214_v00.bc"));

        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            // Window safely within the CK coverage (~1.6 days past SCLK epoch)
            var start = TimeSystem.Time.CreateTDB(374100000.0);
            var window = new Window(start, start.AddSeconds(300.0));

            var results = SpiceAPI.Instance.ReadOrientation(
                window, -555, TimeSpan.FromSeconds(60.0),
                Frames.Frame.ICRF, TimeSpan.FromSeconds(60.0)).ToArray();

            Assert.NotEmpty(results);
            Assert.All(results, r =>
            {
                Assert.Equal(Frames.Frame.ICRF, r.ReferenceFrame);
                // Quaternion must be unit length
                var q = r.Rotation;
                var norm = System.Math.Sqrt(q.W * q.W
                    + q.VectorPart.X * q.VectorPart.X
                    + q.VectorPart.Y * q.VectorPart.Y
                    + q.VectorPart.Z * q.VectorPart.Z);
                Assert.Equal(1.0, norm, 6);
            });
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    // The frames named GCRF a caller can give SPICE (#543): the instance of the library, a frame built from the name (the
    // CLI), and the name in lower case, as SPICE compares frame names ignoring case
    private static Frames.Frame[] GcrfFrames() => [Frames.Frame.GCRF, new Frames.Frame("GCRF"), new Frames.Frame("gcrf")];

    [Theory]
    [InlineData(Aberration.None)]
    [InlineData(Aberration.LT)]
    [InlineData(Aberration.CNS)]
    public void ReadEphemeris_InGcrf_IsTheJ2000Read(Aberration aberration)
    {
        // GCRF has the axes of J2000 (GcrfFrame): the reference is SPICE's read of the Moon from the Earth in J2000, at an
        // epoch and over a window; a read in GCRF is the same numbers, labelled with the frame the caller gave
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var window = new Window(epoch, epoch.AddSeconds(120.0));
        var j2000 = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, aberration).ToStateVector();
        var j2000Window = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.ICRF, aberration, TimeSpan.FromSeconds(60.0))
            .Select(x => x.ToStateVector()).ToArray();
        // The reference is a state, not the zeros of a read SPICE refused
        Assert.True(j2000.Position.Magnitude() > 3.0e8);
        Assert.Equal(3, j2000Window.Length);
        Assert.All(j2000Window, x => Assert.True(x.Position.Magnitude() > 3.0e8));

        foreach (var gcrf in GcrfFrames())
        {
            var read = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, gcrf, aberration).ToStateVector();
            Assert.Equal(gcrf.Name, read.Frame.Name);
            Assert.Equal(j2000.Epoch, read.Epoch);
            Assert.Equal(j2000.Position, read.Position);
            Assert.Equal(j2000.Velocity, read.Velocity);

            var readWindow = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, gcrf, aberration, TimeSpan.FromSeconds(60.0))
                .Select(x => x.ToStateVector()).ToArray();
            Assert.Equal(j2000Window.Length, readWindow.Length);
            for (int i = 0; i < j2000Window.Length; i++)
            {
                Assert.Equal(gcrf.Name, readWindow[i].Frame.Name);
                Assert.Equal(j2000Window[i].Epoch, readWindow[i].Epoch);
                Assert.Equal(j2000Window[i].Position, readWindow[i].Position);
                Assert.Equal(j2000Window[i].Velocity, readWindow[i].Velocity);
            }
        }
    }

    [Fact]
    public void TransformFrame_FromOrToGcrf_IsTheJ2000Transform()
    {
        // The reference is SPICE's transformation from and to J2000 (sxform); from or to GCRF it is the same, through
        // SpiceAPI and through Frame.ToFrame of a frame built from the name, which the data provider hands to SPICE
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        foreach (var other in new[] { Frames.Frame.ECLIPTIC_J2000, new Frames.Frame("IAU_EARTH"), Frames.Frame.ICRF })
        {
            var fromJ2000 = SpiceAPI.Instance.TransformFrame(epoch, Frames.Frame.ICRF, other);
            var toJ2000 = SpiceAPI.Instance.TransformFrame(epoch, other, Frames.Frame.ICRF);
            // The reference is a rotation, not the zero quaternion of a transformation SPICE refused
            Assert.Equal(1.0, fromJ2000.Rotation.Magnitude(), 12);
            Assert.Equal(1.0, toJ2000.Rotation.Magnitude(), 12);

            foreach (var gcrf in GcrfFrames())
            {
                var from = SpiceAPI.Instance.TransformFrame(epoch, gcrf, other);
                var to = SpiceAPI.Instance.TransformFrame(epoch, other, gcrf);
                Assert.Equal(fromJ2000.Rotation, from.Rotation);
                Assert.Equal(fromJ2000.AngularVelocity, from.AngularVelocity);
                Assert.Equal(gcrf.Name, from.ReferenceFrame.Name);
                Assert.Equal(toJ2000.Rotation, to.Rotation);
                Assert.Equal(toJ2000.AngularVelocity, to.AngularVelocity);
            }

            var byName = new Frames.Frame("GCRF").ToFrame(other, epoch);
            var icrf = new Frames.Frame("J2000").ToFrame(other, epoch);
            Assert.Equal(icrf.Rotation, byName.Rotation);
            Assert.Equal(icrf.AngularVelocity, byName.AngularVelocity);
        }
    }

    [Fact]
    public void FindWindowsOnCoordinateConstraint_InGcrf_IsTheJ2000Search()
    {
        // The windows where the Moon is north of the equator of J2000 seen from the Earth, over 40 days (gfposc): in GCRF the
        // same windows
        var searchWindow = new Window(TimeSystem.Time.CreateTDB(730036800.0), TimeSystem.Time.CreateTDB(730036800.0 + 40.0 * 86400.0));
        Window[] Search(Frames.Frame frame) => SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(searchWindow, 399, 301, frame,
            CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None, TimeSpan.FromHours(1.0)).ToArray();

        var j2000 = Search(Frames.Frame.ICRF);
        // The Moon crosses the equator about every 13.7 days: the reference holds windows, not the empty result of a
        // search SPICE refused
        Assert.True(j2000.Length >= 2, string.Join("; ", j2000.Select(x => $"{x.StartDate} {x.EndDate}")));
        foreach (var gcrf in GcrfFrames())
        {
            Assert.Equal(j2000, Search(gcrf));
        }
    }

    [Fact]
    public void ReadOrientation_OutsideTheCk_Throws()
    {
        // The proxy finds no orientation and returns false (Proxy.cpp, "No orientation found"). Its C++ bool is one byte:
        // read as a four-byte BOOL, the false came back true on Windows x64 and the read returned an empty sequence (#564)
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_sc_test2_111108_130214_v00.bc"));

        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            // A day before the CK starts (2011-11-08), within the clock (from 2011-11-07)
            var start = TimeSystem.Time.CreateTDB(373970000.0);
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.ReadOrientation(
                new Window(start, start), -555, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0)).ToArray());
            Assert.Contains("No orientation found", exception.Message);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Fact]
    public void ReadOrientationToleranceHonored()
    {
        // Verify that tolerance is correctly interpreted as time duration (not raw ticks).
        // Before the bug fix, tolerance seconds were passed directly to ckgpav_c as ticks,
        // giving a near-zero search window → "No orientation found" exception.
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName,
            "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName,
            "phsrm_sc_test2_111108_130214_v00.bc"));

        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            var start = TimeSystem.Time.CreateTDB(374100000.0);
            // Use a step size (7.5 s) smaller than the CK sample interval so query epochs
            // fall between CK data points — tolerance must bridge the gap.
            var window = new Window(start, start.AddSeconds(120.0));

            // Must NOT throw: tolerance of 30 s must successfully find the nearest sample
            var results = SpiceAPI.Instance.ReadOrientation(
                window, -555, TimeSpan.FromSeconds(30.0),
                Frames.Frame.ICRF, TimeSpan.FromSeconds(7.5)).ToArray();

            Assert.NotEmpty(results);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Fact]
    public void ReadOrientationFromExternalCKKernel_InGcrf_IsTheJ2000Read()
    {
        // The records of the PHSRM CK (ckgpav) read in J2000 are the reference; read in GCRF, the same records, labelled
        // with the frame the caller gave (#543)
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_sc_test2_111108_130214_v00.bc"));
        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            var start = TimeSystem.Time.CreateTDB(374100000.0);
            var window = new Window(start, start.AddSeconds(300.0));
            var j2000 = SpiceAPI.Instance.ReadOrientation(window, -555, TimeSpan.FromSeconds(60.0), Frames.Frame.ICRF, TimeSpan.FromSeconds(60.0)).ToArray();
            Assert.NotEmpty(j2000);
            Assert.All(j2000, x => Assert.Equal(1.0, x.Rotation.Magnitude(), 12));
            foreach (var gcrf in GcrfFrames())
            {
                var read = SpiceAPI.Instance.ReadOrientation(window, -555, TimeSpan.FromSeconds(60.0), gcrf, TimeSpan.FromSeconds(60.0)).ToArray();
                Assert.Equal(j2000.Length, read.Length);
                for (int i = 0; i < j2000.Length; i++)
                {
                    Assert.Equal(gcrf.Name, read[i].ReferenceFrame.Name);
                    Assert.Equal(j2000[i].Epoch, read[i].Epoch);
                    Assert.Equal(j2000[i].Rotation, read[i].Rotation);
                    Assert.Equal(j2000[i].AngularVelocity, read[i].AngularVelocity);
                }
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Fact]
    void WriteEphemeris()
    {
        //Load solar system kernels
        const int size = 10;
        Clock clock = new Clock("clk1", 65536);
        var spacecraft = new Spacecraft(-135, "Spc1", 3000.0, 10000.0, clock, new StateVector(new Vector3(6800, 0, 0),
            new Vector3(0, 8.0, 0),
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.CreateTDB(0.0), Frames.Frame.ICRF));

        var sv = new StateVector[size];
        for (int i = 0; i < size; ++i)
        {
            sv[i] = new StateVector(new Vector3(6800 + i, i, i), new Vector3(i, 8.0 + i * 0.001, i),
                TestHelpers.EarthAtJ2000,
                TimeSystem.Time.CreateTDB(i), Frames.Frame.ICRF);
        }

        //Write ephemeris file
        FileInfo file = new FileInfo("EphemerisTestFile.spk");

        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.WriteEphemeris(null, spacecraft.NaifId, sv));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.WriteEphemeris(file, spacecraft.NaifId, null));
        SpiceAPI.Instance.WriteEphemeris(file, spacecraft.NaifId, sv);

        //Load ephemeris file
        SpiceAPI.Instance.LoadKernels(file);
        try
        {
            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(9.0));
            var stateVectors = SpiceAPI.Instance.ReadEphemeris(window, TestHelpers.EarthAtJ2000.NaifId, spacecraft.NaifId, Frames.Frame.ICRF,
                    Aberration.None, TimeSpan.FromSeconds(1.0))
                .Select(x => x.ToStateVector()).ToArray();
            for (int i = 0; i < size; ++i)
            {
                Assert.Equal(6800.0 + i, stateVectors[i].Position.X, 9);
                Assert.Equal(i, stateVectors[i].Position.Y, 12);
                Assert.Equal(i, stateVectors[i].Position.Z, 12);
                Assert.Equal(i, stateVectors[i].Velocity.X, 12);
                Assert.Equal(8 + i * 0.001, stateVectors[i].Velocity.Y, 12);
                Assert.Equal(i, stateVectors[i].Velocity.Z, 12);
                Assert.Equal(i, stateVectors[i].Epoch.TimeSpanFromJ2000().TotalSeconds);
                Assert.Equal(PlanetsAndMoons.EARTH.NaifId, stateVectors[i].Observer.NaifId);
                Assert.Equal(Frames.Frame.ICRF, stateVectors[i].Frame);
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
        }
    }

    [Fact]
    public async Task WriteOrientation()
    {
        //Load solar system kernels
        const int size = 10;
        Clock clock = new Clock("clk1", 65536);
        var spacecraft = new Spacecraft(-175, "Spc1", 3000.0, 10000.0, clock, new StateVector(new Vector3(6800, 0, 0),
            new Vector3(0, 8.0, 0),
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.CreateTDB(0.0), Frames.Frame.ICRF));

        var so = new StateOrientation[size];
        for (int i = 0; i < size; ++i)
        {
            so[i] = new StateOrientation(new Quaternion(i, 1 + i * 0.1, 1 + i * 0.2, 1 + i * 0.3), Vector3.Zero, TimeSystem.Time.CreateTDB(i), Frames.Frame.ICRF);
        }

        var clockFile = new FileInfo("OrientationClockTest.tsc");
        await clock.WriteAsync(clockFile);
        //Write ephemeris file
        FileInfo file = new FileInfo("OrientationTestFile.ck");
        var frameFile = new FileInfo("OrientationTestFrame.tf");
        double worst = 0.0;
        SpiceAPI.Instance.LoadKernels(clockFile);
        try
        {
            Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.WriteOrientation(null, spacecraft.NaifId, so));
            Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.WriteOrientation(file, spacecraft.NaifId, null));
            SpiceAPI.Instance.WriteOrientation(file, spacecraft.NaifId, so);

            //Load ephemeris file
            SpiceAPI.Instance.LoadKernels(file);

            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(9.0));
            var stateOrientation = SpiceAPI.Instance.ReadOrientation(window, spacecraft.NaifId, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0))
                .Select(x => x).ToArray();

            Assert.Equal(0.0, stateOrientation[0].Rotation.W, 9);
            Assert.Equal(new Vector3(-0.57735026918962573, -0.57735026918962573, -0.57735026918962573), stateOrientation[0].Rotation.VectorPart);
            Assert.Equal(Vector3.Zero, stateOrientation[0].AngularVelocity);
            Assert.Equal(0.0, stateOrientation[0].Epoch.TimeSpanFromJ2000().TotalSeconds);
            Assert.Equal(Frames.Frame.ICRF, stateOrientation[0].ReferenceFrame);

            Assert.Equal(0.78386180166962049, stateOrientation[4].Rotation.W, 9);
            Assert.Equal(new Vector3(0.27435163058436718, 0.35273781075132921, 0.43112399091829129), stateOrientation[4].Rotation.VectorPart);
            Assert.Equal(Vector3.Zero, stateOrientation[4].AngularVelocity);
            Assert.Equal(4.0, stateOrientation[4].Epoch.TimeSpanFromJ2000().TotalSeconds);
            Assert.Equal(Frames.Frame.ICRF, stateOrientation[4].ReferenceFrame);

            Assert.Equal(0.87358057364767872, stateOrientation[9].Rotation.W, 9);
            Assert.Equal(new Vector3(0.18442256554784328, 0.27178062291261118, 0.359138680277379), stateOrientation[9].Rotation.VectorPart);
            Assert.Equal(Vector3.Zero, stateOrientation[9].AngularVelocity);
            Assert.Equal(9.0, stateOrientation[9].Epoch.TimeSpanFromJ2000().TotalSeconds);
            Assert.Equal(Frames.Frame.ICRF, stateOrientation[9].ReferenceFrame);

            // pxform(J2000, SC) on the written CK, with the frame kernel of the spacecraft: the C-matrix written, J2000 to the
            // spacecraft frame (choice 7 of the design of #537), at its records
            await ((SpacecraftFrame)spacecraft.Frame).WriteAsync(frameFile);
            SpiceAPI.Instance.LoadKernels(frameFile);
            foreach (var written in so)
            {
                var pxform = SpiceAPI.Instance.TransformFrame(written.Epoch, Frames.Frame.ICRF, spacecraft.Frame).Rotation;
                worst = System.Math.Max(worst, Body.AttitudeRecordTests.RotationAngle(written.Rotation.Normalize(), pxform));
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(frameFile);
            SpiceAPI.Instance.UnloadKernels(file);
            SpiceAPI.Instance.UnloadKernels(clockFile);
        }

        _output?.WriteLine($"MEASURE #537 WriteOrientation: pxform(J2000, SC) - the C-matrix written {worst:E2} rad");
        // Twice the worst over its ten records over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02):
        // 3.31e-16 / 3.31e-16 / 3.31e-16 rad
        Assert.True(worst <= WriteOrientationPxform, $"{worst:E3} rad");
    }

    private const double WriteOrientationPxform = 6.7e-16;

    [Fact]
    void GetCelestialBodyInformation()
    {
        //Read celestial celestialItem information from spice kernels
        var res = SpiceAPI.Instance.GetCelestialBodyInfo(TestHelpers.EarthAtJ2000.NaifId);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, res.Id);
        Assert.Equal(Stars.Sun.NaifId, res.CenterOfMotionId);
        Assert.Equal(Barycenters.EARTH_BARYCENTER.NaifId, res.BarycenterOfMotionId);
        Assert.Equal(PlanetsAndMoons.EARTH.Name, res.Name);
        Assert.Equal(13000, res.FrameId);
        Assert.Equal("ITRF93", res.FrameName);
        Assert.Equal(398600435507022.62, res.GM);
        Assert.Equal(6378136.5999999998, res.Radii.X);
        Assert.Equal(6378136.5999999998, res.Radii.Y);
        Assert.Equal(6356751.9000000002, res.Radii.Z);
        Assert.Equal(0.001082616, res.J2);
        Assert.Equal(-2.5388099999999996E-06, res.J3);
        Assert.Equal(-1.6559699999999999E-06, res.J4);
    }

    [Fact]
    void GetCelestialBodyInformationWithoutJ()
    {
        //Read celestial celestialItem information from spice kernels
        var res = SpiceAPI.Instance.GetCelestialBodyInfo(TestHelpers.MoonAtJ2000.NaifId);
        Assert.Equal(PlanetsAndMoons.MOON.NaifId, res.Id);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, res.CenterOfMotionId);
        Assert.Equal(Barycenters.EARTH_BARYCENTER.NaifId, res.BarycenterOfMotionId);
        Assert.Equal(PlanetsAndMoons.MOON.Name, res.Name);
        Assert.Equal(31001, res.FrameId);
        Assert.Equal("MOON_ME", res.FrameName);
        Assert.Equal(4902800118457.5488, res.GM);
        Assert.Equal(1737400.0, res.Radii.X);
        Assert.Equal(1737400.0, res.Radii.Y);
        Assert.Equal(1737400.0, res.Radii.Z);
        Assert.Equal(double.NaN, res.J2);
        Assert.Equal(double.NaN, res.J3);
        Assert.Equal(double.NaN, res.J4);
    }

    [Fact]
    void TransformFrame()
    {
        //Get the quaternion to transform
        var res = SpiceAPI.Instance.TransformFrame(TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, new Frames.Frame(PlanetsAndMoons.EARTH.Frame));
        Assert.Equal(0.76713121207787449, res.Rotation.W, 6);
        Assert.Equal(-1.8618836714990174E-05, res.Rotation.VectorPart.X, 6);
        Assert.Equal(8.4688405480964646E-07, res.Rotation.VectorPart.Y, 6);
        Assert.Equal(0.64149022058684046, res.Rotation.VectorPart.Z, 6);
        Assert.Equal(-1.9637713280171745E-09, res.AngularVelocity.X, 6);
        Assert.Equal(-2.0389347198634933E-09, res.AngularVelocity.Y, 6);
        Assert.Equal(7.2921150643333896E-05, res.AngularVelocity.Z, 6);
    }

    [Fact]
    void TransformFrameWindows()
    {
        //Get the quaternion to transform
        var res = SpiceAPI.Instance.TransformFrame(new TimeSystem.Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1.0)), Frames.Frame.ICRF,
            new Frames.Frame(PlanetsAndMoons.EARTH.Frame), TimeSpan.FromDays(1.0));
        Assert.Equal(0.76713121207787449, res.First().Rotation.W, 6);
        Assert.Equal(-1.8618836714990174E-05, res.First().Rotation.VectorPart.X, 6);
        Assert.Equal(8.4688405480964646E-07, res.First().Rotation.VectorPart.Y, 6);
        Assert.Equal(0.64149022058684046, res.First().Rotation.VectorPart.Z, 6);
        Assert.Equal(-1.9637713280171745E-09, res.First().AngularVelocity.X, 6);
        Assert.Equal(-2.0389347198634933E-09, res.First().AngularVelocity.Y, 6);
        Assert.Equal(7.2921150643333896E-05, res.First().AngularVelocity.Z, 6);
    }

    [Fact]
    void TransformFrameExceptions()
    {
        //Get the quaternion to transform
        Assert.Throws<ArgumentNullException>(() =>
            SpiceAPI.Instance.TransformFrame(TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, null));
        Assert.Throws<ArgumentNullException>(() =>
            SpiceAPI.Instance.TransformFrame(TimeSystem.Time.J2000TDB, null, new Frames.Frame(PlanetsAndMoons.EARTH.Frame)));
    }

    [Fact]
    void Quaternion()
    {
        DTO.Quaternion q = new DTO.Quaternion(1.0, 2.0, 3.0, 4.0);
        Assert.Equal(1.0, q.W);
        Assert.Equal(2.0, q.X);
        Assert.Equal(3.0, q.Y);
        Assert.Equal(4.0, q.Z);
    }

    [Fact]
    void StateOrientation()
    {
        DTO.StateOrientation so = new DTO.StateOrientation(new DTO.Quaternion(1.0, 2.0, 3.0, 4.0),
            new Vector3D(1.0, 2.0, 3.0), 10.0, "J2000");
        Assert.Equal(new DTO.Quaternion(1.0, 2.0, 3.0, 4.0), so.Rotation);
        Assert.Equal(new Vector3D(1.0, 2.0, 3.0), so.AngularVelocity);
        Assert.Equal(10.0, so.Epoch);
        Assert.Equal("J2000", so.Frame);
    }

    [Fact]
    void AzimuthRange()
    {
        DTO.AzimuthRange so = new DTO.AzimuthRange(10.0, 20.0);
        Assert.Equal(10.0, so.Start);
        Assert.Equal(20.0, so.End);
    }

    [Fact]
    void LoadKernelException()
    {
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.LoadKernels(null));
    }

    private static object lockobj = new Object();

    [Fact]
    void UnloadKernels()
    {
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));
        SpiceAPI.Instance.UnloadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
        var kernels = SpiceAPI.Instance.GetLoadedKernels().ToArray();
        Assert.Equal(1, @kernels.Count(x => x.FullName.Contains("scn100")));
    }

    [Fact]
    void UnloadKernels2()
    {
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Clocks/DRAGONFLY32.tsc"));

        SpiceAPI.Instance.UnloadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Clocks/DRAGONFLY32.tsc"));
        var kernels = SpiceAPI.Instance.GetLoadedKernels().ToArray();
        Assert.Equal(0, kernels.Count(x => x.FullName.Contains("DRAGONFLY32.tsc")));
    }

    [Fact]
    void LoadKernels()
    {
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));

        var kernels = SpiceAPI.Instance.GetLoadedKernels().ToArray();
        Assert.Equal(1, @kernels.Count(x => x.FullName.Contains("scn100")));
    }

    [Fact]
    void LoadKernels2()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));

        var kernels = SpiceAPI.Instance.GetLoadedKernels().ToArray();
        Assert.Equal(1, @kernels.Count(x => x.FullName.Contains("scn100")));
    }

    [Fact]
    void CelestialBody()
    {
        DTO.CelestialBody celestialBody = new CelestialBody(1, 2, 3, "celestialItem", new Vector3D(1.0, 2.0, 3.0), 123, "frame", 147, 1.0, 2.0, 3.0);
        Assert.Equal(1, celestialBody.Id);
        Assert.Equal(2, celestialBody.CenterOfMotionId);
        Assert.Equal(3, celestialBody.BarycenterOfMotionId);
        Assert.Equal("celestialItem", celestialBody.Name);
        Assert.Equal(new Vector3D(1.0, 2.0, 3.0), celestialBody.Radii);
        Assert.Equal(147, celestialBody.FrameId);
        Assert.Equal("frame", celestialBody.FrameName);
        Assert.Equal(123, celestialBody.GM);
        Assert.Equal(1.0, celestialBody.J2);
        Assert.Equal(2.0, celestialBody.J3);
        Assert.Equal(3.0, celestialBody.J4);
    }

    [Fact]
    void TLEElements()
    {
        DTO.TLEElements tle = new TLEElements(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        Assert.Equal(1, tle.BalisticCoefficient);
        Assert.Equal(2, tle.SecondDerivativeOfMeanMotion);
        Assert.Equal(3, tle.DragTerm);
        Assert.Equal(4, tle.Epoch);
        Assert.Equal(5, tle.A);
        Assert.Equal(6, tle.E);
        Assert.Equal(7, tle.I);
        Assert.Equal(8, tle.W);
        Assert.Equal(9, tle.O);
        Assert.Equal(10, tle.M);
    }

    [Fact]
    void ConvertTLE()
    {
        var res = SpiceAPI.Instance.ConvertTleToStateVector("ISS",
            "1 25544U 98067A   24001.00000000  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703", new TimeSystem.Time(2024, 1, 1, frame: TimeFrame.UTCFrame));
        
    }

    [Fact]
    void ConvertConicToState()
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB);
        var ke2 = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        Assert.Equal(ke.A, ke2.A, 6);
        Assert.Equal(ke.E, ke2.E, 6);
        Assert.Equal(ke.I, ke2.I, 6);
        Assert.Equal(ke.RAAN, ke2.RAAN, 6);
        Assert.Equal(ke.AOP, ke2.AOP, 6);
        Assert.Equal(ke.M, ke2.M, 6);
        Assert.Equal(ke.Epoch, ke2.Epoch);
        Assert.Equal(ke.Frame, ke2.Frame);
        Assert.Equal(ke.Observer, ke2.Observer);
    }

    [Fact]
    void ConvertConicHyperbolicToState()
    {
        var ke = new KeplerianElements(-6800000.0, 1.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB);
        var ke2 = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        Assert.Equal(ke.A, ke2.A, 6);
        Assert.Equal(ke.E, ke2.E, 6);
        Assert.Equal(ke.I, ke2.I, 6);
        Assert.Equal(ke.RAAN, ke2.RAAN, 6);
        Assert.Equal(ke.AOP, ke2.AOP, 6);
        Assert.Equal(ke.M, ke2.M, 6);
        Assert.Equal(ke.Epoch, ke2.Epoch);
        Assert.Equal(ke.Frame, ke2.Frame);
        Assert.Equal(ke.Observer, ke2.Observer);
    }

    [Fact]
    public void ReadEphemerisFromIds()
    {
        var searchWindow = new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(100.0));
        var res = SpiceAPI.Instance.ReadEphemeris(searchWindow, PlanetsAndMoons.EARTH.NaifId, PlanetsAndMoons.MOON.NaifId,
            Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0)).Select(x => x.ToStateVector());

        var stateVectors = res as StateVector[] ?? res.ToArray();
        Assert.Equal(-291569264.48965073, stateVectors[0].Position.X);
        Assert.Equal(-266709187.1624887, stateVectors[0].Position.Y);
        Assert.Equal(-76099155.244104564, stateVectors[0].Position.Z);
        Assert.Equal(643.53061483971885, stateVectors[0].Velocity.X);
        Assert.Equal(-666.08181440799092, stateVectors[0].Velocity.Y);
        Assert.Equal(-301.32283209101018, stateVectors[0].Velocity.Z);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, stateVectors[0].Observer.NaifId);
        Assert.Equal(Frames.Frame.ICRF, stateVectors[0].Frame);
        Assert.Equal(0.0, stateVectors[0].Epoch.TimeSpanFromJ2000().TotalSeconds);
    }

    [Fact]
    public void ReadEphemerisAtEpochFromIds()
    {
        var epoch = TimeSystem.Time.CreateTDB(0.0);
        var res = SpiceAPI.Instance.ReadEphemeris(epoch, PlanetsAndMoons.EARTH.NaifId, PlanetsAndMoons.MOON.NaifId,
            Frames.Frame.ICRF, Aberration.LT);

        var sv = res.ToStateVector();
        Assert.Equal(-291569264.48965073, sv.Position.X);
        Assert.Equal(-266709187.1624887, sv.Position.Y);
        Assert.Equal(-76099155.244104564, sv.Position.Z);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, sv.Observer.NaifId);
    }

    [Fact]
    public void ReadEphemerisFromSsbObserver()
    {
        // Test with SSB observer (naifId = 0) — CelestialItem.Create(0) returns a Barycenter
        var epoch = TimeSystem.Time.CreateTDB(0.0);
        var res = SpiceAPI.Instance.ReadEphemeris(epoch, 0, PlanetsAndMoons.EARTH.NaifId,
            Frames.Frame.ICRF, Aberration.None);

        var sv = res.ToStateVector();
        Assert.Equal(0, sv.Observer.NaifId);
        Assert.NotEqual(0.0, sv.Position.Magnitude());
    }

    [Fact]
    void WriteEphemerisFromId()
    {
        const int size = 10;
        var sv = new StateVector[size];
        for (int i = 0; i < size; ++i)
        {
            sv[i] = new StateVector(new Vector3(6800 + i, i, i), new Vector3(i, 8.0 + i * 0.001, i),
                TestHelpers.EarthAtJ2000,
                TimeSystem.Time.CreateTDB(i), Frames.Frame.ICRF);
        }

        FileInfo file = new FileInfo("EphemerisTestFileFromId.spk");
        SpiceAPI.Instance.WriteEphemeris(file, -136, sv);
        SpiceAPI.Instance.LoadKernels(file);
        try
        {
            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(9.0));
            var stateVectors = SpiceAPI.Instance.ReadEphemeris(window, PlanetsAndMoons.EARTH.NaifId, -136,
                    Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(1.0))
                .Select(x => x.ToStateVector()).ToArray();
            for (int i = 0; i < size; ++i)
            {
                Assert.Equal(6800.0 + i, stateVectors[i].Position.X, 9);
                Assert.Equal(i, stateVectors[i].Position.Y, 12);
                Assert.Equal(i, stateVectors[i].Position.Z, 12);
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
        }
    }

    [Fact]
    async Task WriteOrientationFromId()
    {
        const int size = 10;
        Clock clock = new Clock("clk1", 65536);
        var spacecraft = new Spacecraft(-176, "Spc2", 3000.0, 10000.0, clock, new StateVector(new Vector3(6800, 0, 0),
            new Vector3(0, 8.0, 0),
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.CreateTDB(0.0), Frames.Frame.ICRF));

        var so = new StateOrientation[size];
        for (int i = 0; i < size; ++i)
        {
            so[i] = new StateOrientation(new Quaternion(i, 1 + i * 0.1, 1 + i * 0.2, 1 + i * 0.3), Vector3.Zero, TimeSystem.Time.CreateTDB(i), Frames.Frame.ICRF);
        }

        var clockFile = new FileInfo("OrientationClockTestFromId.tsc");
        await clock.WriteAsync(clockFile);
        SpiceAPI.Instance.LoadKernels(clockFile);
        FileInfo file = new FileInfo("OrientationTestFileFromId.ck");
        try
        {
            SpiceAPI.Instance.WriteOrientation(file, spacecraft.NaifId, so);
            SpiceAPI.Instance.LoadKernels(file);

            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(9.0));
            var stateOrientation = SpiceAPI.Instance.ReadOrientation(window, spacecraft.NaifId, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0))
                .ToArray();

            Assert.Equal(0.0, stateOrientation[0].Rotation.W, 9);
            Assert.Equal(Frames.Frame.ICRF, stateOrientation[0].ReferenceFrame);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
            SpiceAPI.Instance.UnloadKernels(clockFile);
        }
    }

    [Fact]
    void ConvertConicToStateWithExplicitGm()
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var gm = TestHelpers.EarthAtJ2000.GM;
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB, gm);
        var ke2 = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state, gm);
        Assert.Equal(ke.A, ke2.A, 6);
        Assert.Equal(ke.E, ke2.E, 6);
        Assert.Equal(ke.I, ke2.I, 6);
        Assert.Equal(ke.RAAN, ke2.RAAN, 6);
        Assert.Equal(ke.AOP, ke2.AOP, 6);
        Assert.Equal(ke.M, ke2.M, 6);
    }

    [Fact]
    void Propagate2BodiesWithExplicitGm()
    {
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0),
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var gm = TestHelpers.EarthAtJ2000.GM;
        var result = SpiceAPI.Instance.Propagate2Bodies(sv, gm, TimeSpan.FromSeconds(60.0));
        Assert.NotEqual(0.0, result.Position.Magnitude());
        Assert.NotEqual(sv.Position.X, result.Position.X);
    }

    [Fact]
    public void ReadOrientationFromCLPS_APM1_CKKernel_UTCWindow()
    {
        // Reproduce customer scenario: CLPS APM1 lander (NAIF ID -244)
        // CK object ID: -244000 (derived as naifId * 1000)
        // CK coverage: 2024-JAN-08 08:17:27.955 ET to 2024-JAN-18 13:29:39.231 ET
        //
        // Customer-specified start: 2024-01-08T08:16:18.771 (UTC)
        // UTC 08:16:18.771 → TDB ≈ 08:17:27.955 (exactly at CK coverage start)
        // Without ToTDB() fix, SPICE receives TDB 08:16:18.771 → before CK start → crash
        var sclkFile = new FileInfo(Path.Combine(Constants.ClpsDataPath.FullName,
            "clps_to2ab_apm1_v02.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.ClpsDataPath.FullName,
            "clps_to2ab_apm1_cru_rec_240108_240118_v02.bc"));

        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            // Create the window in UTC — the customer's natural time frame
            var start = new TimeSystem.Time(2024, 1, 8, 8, 16, 19, frame: TimeSystem.TimeFrame.UTCFrame);
            var window = new Window(start, start.AddSeconds(60.0));

            var results = SpiceAPI.Instance.ReadOrientation(
                window, -244, TimeSpan.FromSeconds(10.0),
                Frames.Frame.ICRF, TimeSpan.FromSeconds(10.0)).ToArray();

            Assert.NotEmpty(results);

            // Verify returned epochs are near 2024 (far from J2000)
            var windowStartTdbSeconds = window.StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds;
            foreach (var orientation in results)
            {
                var epochSeconds = orientation.Epoch.TimeSpanFromJ2000().TotalSeconds;
                // Epoch must be far from J2000 (>24 years away)
                Assert.True(System.Math.Abs(epochSeconds) > 1e6,
                    $"Epoch {epochSeconds} s from J2000 is suspiciously close to J2000 — window conversion bug?");
                // Epoch must be reasonably close to the requested window start
                Assert.True(System.Math.Abs(epochSeconds - windowStartTdbSeconds) < 120.0,
                    $"Epoch {epochSeconds} is too far from window start {windowStartTdbSeconds}");
            }

            // Quaternions must be unit length
            Assert.All(results, r =>
            {
                var q = r.Rotation;
                var norm = System.Math.Sqrt(q.W * q.W
                    + q.VectorPart.X * q.VectorPart.X
                    + q.VectorPart.Y * q.VectorPart.Y
                    + q.VectorPart.Z * q.VectorPart.Z);
                Assert.Equal(1.0, norm, 6);
            });
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Fact]
    public void ReadOrientationFromCLPS_APM1_CKKernel_TDBWindow()
    {
        // Same scenario as above but with a TDB window safely within CK coverage.
        // CK coverage: 2024-JAN-08 08:17:27.955 ET to 2024-JAN-18 13:29:39.231 ET
        var sclkFile = new FileInfo(Path.Combine(Constants.ClpsDataPath.FullName,
            "clps_to2ab_apm1_v02.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.ClpsDataPath.FullName,
            "clps_to2ab_apm1_cru_rec_240108_240118_v02.bc"));

        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            // Use a TDB time safely within CK coverage (a few minutes after start)
            var start = new TimeSystem.Time(2024, 1, 8, 8, 20, 0);
            var window = new Window(start, start.AddSeconds(60.0));

            var results = SpiceAPI.Instance.ReadOrientation(
                window, -244, TimeSpan.FromSeconds(10.0),
                Frames.Frame.ICRF, TimeSpan.FromSeconds(10.0)).ToArray();

            Assert.NotEmpty(results);

            // Quaternions must be unit length
            Assert.All(results, r =>
            {
                var q = r.Rotation;
                var norm = System.Math.Sqrt(q.W * q.W
                    + q.VectorPart.X * q.VectorPart.X
                    + q.VectorPart.Y * q.VectorPart.Y
                    + q.VectorPart.Z * q.VectorPart.Z);
                Assert.Equal(1.0, norm, 6);
            });
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Fact]
    void WriteOrientationAndEphemeris_OfNoRecord_AreRejected()
    {
        var file = new FileInfo(Path.Combine(Constants.OutputPath.FullName, "empty_records.bsp"));
        Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteOrientation(file, -1106, Array.Empty<StateOrientation>()));
        Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteEphemeris(file, -1106, Array.Empty<StateVector>()));
    }

    /// <summary>
    /// The overloads of the two-body propagation and of the conic conversion: the duration, the target epoch and the
    /// observer's GM give the call with the explicit GM bit for bit; the elliptic conversion without an epoch and the
    /// equinoctial conversion, on a circular equatorial orbit at the periapsis, its closed form r = (a, 0, 0),
    /// v = (0, √(GM / a), 0).
    /// </summary>
    [Fact]
    void Propagate2BodiesAndConicOverloads()
    {
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0),
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var gm = TestHelpers.EarthAtJ2000.GM;
        var explicitGm = SpiceAPI.Instance.Propagate2Bodies(sv, gm, TimeSpan.FromSeconds(60.0));
        var byDuration = SpiceAPI.Instance.Propagate2Bodies(sv, TimeSpan.FromSeconds(60.0));
        var byEpoch = SpiceAPI.Instance.Propagate2Bodies(sv, TimeSystem.Time.J2000TDB.AddSeconds(60.0));
        Assert.Equal(explicitGm.Position, byDuration.Position);
        Assert.Equal(explicitGm.Velocity, byDuration.Velocity);
        Assert.Equal(explicitGm.Position, byEpoch.Position);
        Assert.Equal(explicitGm.Velocity, byEpoch.Velocity);

        const double a = 6800000.0;
        var ke = new KeplerianElements(a, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke);
        double positionError = (state.Position - new Vector3(a, 0.0, 0.0)).Magnitude();
        double velocityError = (state.Velocity - new Vector3(0.0, System.Math.Sqrt(gm / a), 0.0)).Magnitude();
        _output?.WriteLine($"MEASURE conic e = 0 against its closed form: {positionError:E3} m, {velocityError:E3} m/s");
        // 0 m and 0 m/s on Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02): the closed form to the bit
        Assert.Equal(0.0, positionError);
        Assert.Equal(0.0, velocityError);

        // The same circular equatorial orbit as equinoctial elements (p = a, f = g = h = k = 0, the longitude 0), through eqncpv
        var equinoctial = new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(a, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var fromEquinoctial = SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(equinoctial);
        double equinoctialPositionError = (fromEquinoctial.Position - new Vector3(a, 0.0, 0.0)).Magnitude();
        double equinoctialVelocityError = (fromEquinoctial.Velocity - new Vector3(0.0, System.Math.Sqrt(gm / a), 0.0)).Magnitude();
        _output?.WriteLine($"MEASURE equinoctial e = 0 against its closed form: {equinoctialPositionError:E3} m, {equinoctialVelocityError:E3} m/s");
        Assert.True(equinoctialPositionError <= EquinoctialPositionLimit, $"{equinoctialPositionError:E3} m");
        Assert.True(equinoctialVelocityError <= EquinoctialVelocityLimit, $"{equinoctialVelocityError:E3} m/s");
    }

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02): 4.164e-10 / 4.164e-10 /
    // 4.164e-10 m and 1.125e-12 / 1.125e-12 / 1.125e-12 m/s
    private const double EquinoctialPositionLimit = 8.4e-10;
    private const double EquinoctialVelocityLimit = 2.3e-12;
}
