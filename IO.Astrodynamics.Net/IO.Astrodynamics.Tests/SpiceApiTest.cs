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
using IO.Astrodynamics.Exceptions;
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
public partial class SpiceApiTest
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

        spacecraft.SetStandbyManeuver(new NadirAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero,
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
            var rate = normal.Normalize() * (System.Math.Atan2(normal.Magnitude(), before.Dot(after)) / 2.0);
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

    // A state read from a site is the exact negation of the state read to it (#672: spkezr_c subtracts the same two states
    // from the solar system barycentre, and a rotation commutes with the negation), 0 measured in each frame on Linux,
    // Windows and macOS arm64; against the Site object's read, 2.98e-8 m on Linux, 2.98e-8 m on Windows and 2.98e-8 m on
    // macOS arm64, twice that worst (CI run 43600640037, 2026-11-14)
    private const double SiteObserverNegationTolerance = 0.0;
    private const double SiteObjectPositionTolerance = 6.0e-8;

    private static Frames.Frame SiteObserverFrame(string name) => name switch
    {
        "ICRF" => Frames.Frame.ICRF,
        "ECLIPJ2000" => Frames.Frame.ECLIPTIC_J2000,
        _ => Frames.Frame.TEME
    };

    private void AssertNegatedState(StateVector expectedReversed, StateVector actual, string label)
    {
        double dr = (actual.Position + expectedReversed.Position).Magnitude();
        double dv = (actual.Velocity + expectedReversed.Velocity).Magnitude();
        _output.WriteLine($"MEASURE site observer {label} |dr| m: {dr:E3}, |dv| m/s: {dv:E3}");
        Assert.True(dr <= SiteObserverNegationTolerance, $"{label}: |dr| {dr:E3} m");
        Assert.True(dv <= SiteObserverNegationTolerance, $"{label}: |dv| {dv:E3} m/s");
        LabelAssert.Equal(expectedReversed.Epoch, actual.Epoch);
    }

    private static void AssertSiteObserver(StateVector state, string frameName)
    {
        var site = Assert.IsType<Site>(state.Observer);
        Assert.Equal(399013, site.NaifId);
        Assert.Equal(13, site.Id);
        Assert.Equal("DSS-13", site.Name);
        Assert.Equal(399, site.CelestialBody.NaifId);
        Assert.Equal(frameName, state.Frame.Name);
    }

    /// <summary>
    /// A site identifier as the observer of the reads by identifiers (#672): the observer was built as a celestial body,
    /// whose record has no radii, so the read threw or labelled its state with a body built from stack bytes. The geometric
    /// state from the site to the Moon is the negation of the state from the Moon to the site, a read with a celestial
    /// observer this change does not touch; in a fixed SPICE frame and in the managed TEME (quasi-inertial, v' = R v).
    /// </summary>
    [Theory]
    [InlineData("ICRF")]
    [InlineData("ECLIPJ2000")]
    [InlineData("TEME")]
    public void ReadEphemeris_FromASite_IsTheNegatedReadToTheSite(string frameName)
    {
        var frame = SiteObserverFrame(frameName);
        var epoch = new TimeSystem.Time(2023, 1, 1, 1, 0, 0, TimeScale.TDB);
        var reversed = SpiceAPI.Instance.ReadEphemeris(epoch, 301, 399013, frame, Aberration.None).ToStateVector();
        Assert.True(reversed.Position.Magnitude() > 3.0e8);

        var read = SpiceAPI.Instance.ReadEphemeris(epoch, 399013, 301, frame, Aberration.None).ToStateVector();
        AssertSiteObserver(read, frame.Name);
        AssertNegatedState(reversed, read, $"{frameName} at an epoch");

        var window = new Window(epoch, epoch.AddSeconds(60.0));
        var reads = SpiceAPI.Instance.ReadEphemeris(window, 399013, 301, frame, Aberration.None, TimeSpan.FromSeconds(60.0))
            .Select(x => x.ToStateVector()).ToArray();
        Assert.Equal(2, reads.Length);
        for (int i = 0; i < reads.Length; i++)
        {
            AssertSiteObserver(reads[i], frame.Name);
            var reversedAt = SpiceAPI.Instance.ReadEphemeris(reads[i].Epoch, 301, 399013, frame, Aberration.None).ToStateVector();
            AssertNegatedState(reversedAt, reads[i], $"{frameName} over a window, state {i}");
        }
    }

    [Theory]
    [InlineData(399013, true)]
    [InlineData(301001, true)]
    [InlineData(101999, true)]
    [InlineData(999001, true)]
    [InlineData(399000, false)]
    [InlineData(391013, false)]
    [InlineData(395013, false)]
    [InlineData(100013, false)]
    [InlineData(1000013, false)]
    [InlineData(-399013, false)]
    [InlineData(399, false)]
    [InlineData(2000002, false)]
    public void IsSiteId_IsTheRuleOfSiteNaifId(int naifId, bool expected)
    {
        // Site.NaifId is its body's identifier times 1000 plus its own, the body a planet or a moon
        Assert.Equal(expected, SpiceAPI.IsSiteId(naifId));
    }

    [Fact]
    public void ReadEphemeris_FromAnUnnamedSite_Throws()
    {
        // A site identifier SPICE does not name has no site to build (#672)
        var epoch = new TimeSystem.Time(2023, 1, 1, 1, 0, 0, TimeScale.TDB);
        var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReadEphemeris(epoch, 399998, 301, Frames.Frame.ICRF, Aberration.None));
        Assert.Equal("naifId", e.ParamName);
        e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReadEphemeris(new Window(epoch, epoch.AddSeconds(60.0)), 399998, 301,
            Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(60.0)));
        Assert.Equal("naifId", e.ParamName);
    }

    /// <summary>
    /// A window read in a managed frame that runs past the coverage of de440s (which ends on 2150 JAN 22) throws SPICE's
    /// message, as the same read in J2000 does (#714, review of the branch). The epochs SPICE gives come first and are rotated
    /// into the frame of a site on Mars (IAU_MARS of the text PCK, no time limit), a rotation that calls
    /// <c>TransformFrameProxy</c>, which clears the last native error when it starts: the message of the failed epochs must
    /// be read before it. The reference is SPICE's message of the same read in J2000, where no rotation runs, which
    /// names the first failed epoch, 2150 JAN 22 12:00 TDB, where <c>SpiceAPI</c> stops (de440s covers the Sun up to
    /// 2150 JAN 22 00:00 TDB, <c>spkcov_c</c>; #714, decision 1 of #719, decided by S. Guillet, 2026-11-17: it named
    /// the last, 2150 JAN 25).
    /// </summary>
    [Fact]
    public void ReadEphemeris_OverAWindowPastTheCoverageInAManagedFrame_ThrowsSpicesMessage()
    {
        var site = new Site(714, "PROBE714", new IO.Astrodynamics.Body.CelestialBody(PlanetsAndMoons.MARS), new Planetodetic(0.1, 0.2, 0.0));
        var start = new TimeSystem.Time(2150, 1, 20, TimeScale.TDB);
        var window = new Window(start, start.AddDays(5.0));
        var step = TimeSpan.FromHours(12.0);

        var reference = Assert.Throws<SpiceException>(() =>
            SpiceAPI.Instance.ReadEphemeris(window, 399, 10, Frames.Frame.ICRF, Aberration.None, step));
        Assert.Contains("Insufficient ephemeris data", reference.Message);
        Assert.Contains("2150 JAN 22 12:00:00.000", reference.Message);
        Assert.DoesNotContain("2150 JAN 25", reference.Message);

        var e = Assert.Throws<SpiceException>(() =>
            SpiceAPI.Instance.ReadEphemeris(window, 399, 10, site.Frame, Aberration.None, step));
        Assert.Equal(reference.Message, e.Message);
    }

    /// <summary>
    /// The documented limitation of a site observer read by identifiers (#672, documented in the fourth round of #668): the
    /// observer is a <see cref="Site"/>, which needs a SPICE name and a state at J2000 TDB. A site from coordinates whose SPK
    /// the library wrote (no naming kernel loaded) throws <see cref="ArgumentException"/> naming <c>naifId</c> as observer,
    /// while the read with the site as target, <c>spkezr_c</c> by number, works: it is the site's own position from the
    /// Earth, the workaround the documentation gives. With the naming frame kernel loaded and an SPK that does not cover
    /// J2000 TDB, the site's constructor reads past it: SPICE's error at 2000 JAN 01 12:00:00.
    /// </summary>
    [Fact]
    public async Task ReadEphemeris_FromAnUnnamedWrittenSite_ThrowsAndTheTargetReadWorks()
    {
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
        var site = new Site(786, "RE786", PlanetsAndMoons.EARTH_BODY, new Planetodetic(0.5, 0.2, 1000.0));
        var spk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site786", ".bsp");
        var fk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site786_names");
        site.WriteEphemeris(new Window(epoch.AddDays(-1.0), epoch.AddDays(1.0)), spk);
        await site.WriteFrameAsync(fk);
        Assert.Empty(KernelShadowing.Find([spk, fk], KernelShadowing.SolarSystem));
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReadEphemeris(epoch, site.NaifId, 301, Frames.Frame.ICRF, Aberration.None));
            Assert.Equal("naifId", e.ParamName);
            Assert.Contains("has no name in SPICE", e.Message);
            e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReadEphemeris(new Window(epoch, epoch.AddHours(2.0)), site.NaifId, 301,
                Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1.0)));
            Assert.Equal("naifId", e.ParamName);

            var asTarget = SpiceAPI.Instance.ReadEphemeris(epoch, 399, site.NaifId, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var managed = site.GetEphemeris(epoch, PlanetsAndMoons.EARTH_BODY, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            Assert.Equal(managed.Position, asTarget.Position, TestHelpers.VectorComparer);

            SpiceAPI.Instance.LoadKernels(fk);
            try
            {
                var past = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.ReadEphemeris(epoch, site.NaifId, 301, Frames.Frame.ICRF, Aberration.None));
                Assert.Contains("2000 JAN 01 12:00:00", past.Message);
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(fk);
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
            IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(spk);
            IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(fk);
        }
    }

    [Fact]
    public void ReadEphemeris_FromASite_ObserverMatchesTheSiteObject()
    {
        // The library's own path for a site observer: the Moon read from a Site object, through the data provider
        var epoch = new TimeSystem.Time(2023, 1, 1, 1, 0, 0, TimeScale.TDB);
        var site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
        var expected = TestHelpers.MoonAtJ2000.GetEphemeris(epoch, site, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var read = SpiceAPI.Instance.ReadEphemeris(epoch, 399013, 301, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        AssertSiteObserver(read, Frames.Frame.ICRF.Name);
        double dr = (read.Position - expected.Position).Magnitude();
        _output.WriteLine($"MEASURE site observer against the Site object |dr| m: {dr:E3}");
        Assert.True(dr <= SiteObjectPositionTolerance, $"|dr| {dr:E3} m");
    }

    [Theory]
    [InlineData(Aberration.None)]
    [InlineData(Aberration.LT)]
    [InlineData(Aberration.CNS)]
    public void ReadEphemeris_InGcrf_IsTheJ2000Read(Aberration aberration)
    {
        // GCRF has the axes of J2000 (GcrfFrame): the reference is SPICE's read of the Moon from the Earth in J2000, at an
        // epoch and over a window; a read in GCRF is the same numbers, labelled with the frame the caller gave
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
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
            LabelAssert.Equal(j2000.Epoch, read.Epoch);
            Assert.Equal(j2000.Position, read.Position);
            Assert.Equal(j2000.Velocity, read.Velocity);

            var readWindow = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, gcrf, aberration, TimeSpan.FromSeconds(60.0))
                .Select(x => x.ToStateVector()).ToArray();
            Assert.Equal(j2000Window.Length, readWindow.Length);
            for (int i = 0; i < j2000Window.Length; i++)
            {
                Assert.Equal(gcrf.Name, readWindow[i].Frame.Name);
                LabelAssert.Equal(j2000Window[i].Epoch, readWindow[i].Epoch);
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
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
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
            LabelAssert.Equal(j2000, Search(gcrf));
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

    // A search SPICE cannot run throws SPICE's error (#668, fourth round): before, the five proxies of the window searches
    // printed it, dropped it and returned no window, which a caller could not tell from a condition that never holds.
    // The reference is SPICE's own error signal, read through the native library (getmsg_c), so these tests run on every
    // platform; the windows of the valid searches are the CSPICE goldens of the tests above, unchanged
    private static readonly Window ErrorSearchWindow = new(new TimeSystem.Time(2024, 3, 1, TimeScale.TDB),
        new TimeSystem.Time(2024, 3, 10, TimeScale.TDB));

    // Past the coverage of de440s.bsp (1849 to 2150) and of the Earth's high-precision PCK
    private static readonly Window PastTheEphemerisWindow = new(new TimeSystem.Time(2160, 3, 1, TimeScale.TDB),
        new TimeSystem.Time(2160, 3, 10, TimeScale.TDB));

    private static readonly Planetodetic ErrorSearchPoint = new(30.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, 1000.0);

    [Fact]
    public void FindWindowsOnCoordinateConstraint_UnknownFrame_ThrowsArgumentException_WithTheSpiceError()
    {
        // A frame name SPICE does not know, which the frames of the library (SpiceKnowsFrame) do not cover: a bad argument
        // since R25 of #668 (review of L1), gfposc_c's error its inner exception
        var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(ErrorSearchWindow, 399, 301,
            new Frames.Frame("NOT_A_FRAME_668"), CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 0.0, 0.0,
            Aberration.None, TimeSpan.FromHours(1.0)));
        Assert.Equal("frame", e.ParamName);
        var inner = Assert.IsType<SpiceException>(e.InnerException);
        Assert.Contains("NOT_A_FRAME_668", inner.Message);
        Assert.Contains("not recognized", inner.Message);
        // and NotEqual, formed from the search of "="
        e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(ErrorSearchWindow, 399, 301,
            new Frames.Frame("NOT_A_FRAME_668"), CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.NotEqual, 0.0, 0.0,
            Aberration.None, TimeSpan.FromHours(1.0)));
        Assert.Equal("frame", e.ParamName);
        Assert.Contains("NOT_A_FRAME_668", Assert.IsType<SpiceException>(e.InnerException).Message);
    }

    [Fact]
    public void FindWindowsOnCoordinateConstraint_WithoutData_ThrowsSpiceError()
    {
        var e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(ErrorSearchWindow, 399, 399999,
            Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
            TimeSpan.FromHours(1.0)));
        Assert.Contains("399999", e.Message);
        e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(PastTheEphemerisWindow, 399, 301,
            Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
            TimeSpan.FromHours(1.0)));
        Assert.Contains("Insufficient ephemeris data", e.Message);
        Assert.Contains("2160", e.Message);
        // A step SPICE rejects
        e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(ErrorSearchWindow, 399, 301,
            Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
            TimeSpan.Zero));
        Assert.Contains("step size must be positive", e.Message);
    }

    [Fact]
    public void FindWindowsOnDistanceConstraint_WithoutData_ThrowsSpiceError()
    {
        var e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnDistanceConstraint(ErrorSearchWindow, 399, 399999,
            RelationnalOperator.Greater, 400000000.0, Aberration.None, TimeSpan.FromHours(1.0)).ToArray());
        Assert.Contains("399999", e.Message);
        e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnDistanceConstraint(PastTheEphemerisWindow, 399, 301,
            RelationnalOperator.Greater, 400000000.0, Aberration.None, TimeSpan.FromHours(1.0)).ToArray());
        Assert.Contains("Insufficient ephemeris data", e.Message);
        Assert.Contains("2160", e.Message);
    }

    [Fact]
    public void FindWindowsOnOccultationConstraint_WithoutData_ThrowsSpiceError()
    {
        var e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnOccultationConstraint(PastTheEphemerisWindow, 399, 10,
            ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT, TimeSpan.FromMinutes(1.0)).ToArray());
        Assert.Contains("2160", e.Message);
        e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnOccultationConstraint(ErrorSearchWindow, 399, 399999,
            ShapeType.Point, 301, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT, TimeSpan.FromMinutes(1.0)).ToArray());
        Assert.Contains("399999", e.Message);
    }

    [Fact]
    public void FindWindowsOnIlluminationConstraint_UnknownFrameOrWithoutData_ThrowsArgumentExceptionOrSpiceError()
    {
        // An unknown fixed frame is a bad argument since R25 of #668 (review of L1), gfilum_c's error its inner exception
        var unknown = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(ErrorSearchWindow, 301, 399,
            new Frames.Frame("NOT_A_FRAME_668"), ErrorSearchPoint, IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI / 3.0, 0.0,
            Aberration.None, TimeSpan.FromHours(1.0), 10));
        Assert.Equal("fixedFrame", unknown.ParamName);
        Assert.Contains("NOT_A_FRAME_668", Assert.IsType<SpiceException>(unknown.InnerException).Message);
        var e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(PastTheEphemerisWindow, 301, 399,
            PlanetsAndMoons.EARTH_BODY.Frame, ErrorSearchPoint, IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI / 3.0, 0.0,
            Aberration.None, TimeSpan.FromHours(1.0), 10));
        Assert.Contains("2160", e.Message);
    }

    [Fact]
    public void FindWindowsInFieldOfViewConstraint_InstrumentWithoutKernel_ThrowsSpiceError()
    {
        // gftfov reads the instrument's field of view in the kernel pool: no instrument kernel defines -999123
        var e = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(ErrorSearchWindow, -999, -999123,
            301, PlanetsAndMoons.MOON_BODY.Frame, ShapeType.Ellipsoid, Aberration.None, TimeSpan.FromHours(1.0)).ToArray());
        Assert.Contains("INS-999123_FOV_FRAME", e.Message);
    }

    [Fact]
    public void FindWindows_ValidSearchesOnTheErrorWindow_StillGiveWindows()
    {
        // The controls of the tests above, on the same window: the Moon south of the equator of J2000 over the 9 days,
        // farther than 380000 km, and the incidence at the point below 60°
        var south = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(ErrorSearchWindow, 399, 301, Frames.Frame.ICRF, CoordinateSystem.Rectangular,
            Coordinate.Z, RelationnalOperator.Lower, 0.0, 0.0, Aberration.None, TimeSpan.FromHours(1.0)).ToArray();
        Assert.Single(south);
        Assert.NotEmpty(SpiceAPI.Instance.FindWindowsOnDistanceConstraint(ErrorSearchWindow, 399, 301, RelationnalOperator.Greater, 380000000.0,
            Aberration.None, TimeSpan.FromHours(1.0)));
        Assert.NotEmpty(SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(ErrorSearchWindow, 301, 399, PlanetsAndMoons.EARTH_BODY.Frame,
            ErrorSearchPoint, IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI / 3.0, 0.0, Aberration.None,
            TimeSpan.FromHours(1.0), 10));
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
                    LabelAssert.Equal(j2000[i].Epoch, read[i].Epoch);
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
        var res = SpiceAPI.Instance.TransformFrame(TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, new Frames.Frame("ITRF93"));
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
            new Frames.Frame("ITRF93"), TimeSpan.FromDays(1.0));
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
            SpiceAPI.Instance.TransformFrame(TimeSystem.Time.J2000TDB, null, new Frames.Frame("ITRF93")));
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
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.LoadKernels((FileSystemInfo)null));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.LoadKernels((string)null));
    }

    // The rotation angle of a frame from ICRF, in degrees, read by SPICE (pxform through TransformFrame; the sign of a
    // quaternion is free)
    private static double RotationAngleFromIcrfDeg(string frameName)
    {
        var q = SpiceAPI.Instance.TransformFrame(new TimeSystem.Time(2024, 1, 1, TimeScale.TDB), new Frames.Frame(frameName), Frames.Frame.ICRF).Rotation;
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(q.W))) * Astrodynamics.Constants.Rad2Deg;
    }

    // The KEEPER count of the files SPICE holds (CspiceDirect, Linux only), -1 elsewhere
    private static int KeeperCount() => OperatingSystem.IsLinux() ? Body.CspiceDirect.LoadedKernelFiles().Count : -1;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void LoadKernels_OfAMissingFile_ThrowsArgumentException_AndLoadsNothing(bool asDirectory)
    {
        // A path that does not exist on disk, under a fresh temporary folder: before, LoadKernels returned and loaded nothing,
        // and the first read that needed the kernel failed far from the cause
        var root = Directory.CreateTempSubdirectory("load-missing-");
        try
        {
            var missing = Path.Combine(root.FullName, asDirectory ? "Kernels" : "missing.bsp");
            FileSystemInfo path = asDirectory ? new DirectoryInfo(missing) : new FileInfo(missing);
            lock (SpiceAPI.SyncRoot)
            {
                var listed = SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName).ToArray();
                int keeper = KeeperCount();

                var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.LoadKernels(path));

                Assert.Equal("path", e.ParamName);
                Assert.Contains(missing, e.Message);
                Assert.Equal(listed, SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName));
                Assert.Equal(keeper, KeeperCount());
            }
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    void LoadKernels_OfAFileInfoNamingADirectory_LoadsTheDirectory()
    {
        // A TK frame of 90° about z from J2000 (TKFRAME_ANGLES (90, 0, 0) about the axes (3, 2, 1), NAIF, Frames Required
        // Reading): its rotation angle from ICRF is 90° in closed form. Before, a FileInfo naming a folder loaded nothing
        var root = Directory.CreateTempSubdirectory("load-fileinfo-folder-");
        var folder = new DirectoryInfo(Path.Combine(root.FullName, "Frames"));
        TestKernelWriter.WriteFrameKernel(folder, "a.tf", 1999970, "IOA_G4_FI_DIR", 90.0);
        var path = new FileInfo(folder.FullName);
        try
        {
            SpiceAPI.Instance.LoadKernels(path);

            Assert.Equal(90.0, RotationAngleFromIcrfDeg("IOA_G4_FI_DIR"), 1e-12);
            Assert.Contains(SpiceAPI.Instance.GetLoadedKernels(), x => x.FullName == folder.FullName);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(path);
            root.Delete(true);
        }

        Assert.Throws<ArgumentException>(() => RotationAngleFromIcrfDeg("IOA_G4_FI_DIR"));
    }

    [Fact]
    void LoadKernels_OfADirectoryInfoNamingAFile_LoadsTheFile()
    {
        // The same frame kernel, named by a DirectoryInfo: before, it loaded nothing
        var root = Directory.CreateTempSubdirectory("load-dirinfo-file-");
        var file = TestKernelWriter.WriteFrameKernel(root, "a.tf", 1999971, "IOA_G4_DI_FILE", 90.0);
        var path = new DirectoryInfo(file.FullName);
        try
        {
            SpiceAPI.Instance.LoadKernels(path);

            Assert.Equal(90.0, RotationAngleFromIcrfDeg("IOA_G4_DI_FILE"), 1e-12);
            Assert.Contains(SpiceAPI.Instance.GetLoadedKernels(), x => x.FullName == file.FullName);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(path);
            root.Delete(true);
        }

        Assert.Throws<ArgumentException>(() => RotationAngleFromIcrfDeg("IOA_G4_DI_FILE"));
    }

    [Fact]
    void LoadKernels_OfAMissingPathInsideALoadedFolder_Throws_AndKeepsTheFolder()
    {
        // Before, a path inside a loaded folder reloaded the folder, whether the path existed or not: the folder's entry
        // moved to the end of the list
        var root = Directory.CreateTempSubdirectory("load-missing-inside-");
        var folder = new DirectoryInfo(Path.Combine(root.FullName, "Frames"));
        TestKernelWriter.WriteFrameKernel(folder, "a.tf", 1999972, "IOA_G4_IN_DIR", 90.0);
        try
        {
            SpiceAPI.Instance.LoadKernels(folder);
            var missing = new FileInfo(Path.Combine(folder.FullName, "missing.tf"));
            lock (SpiceAPI.SyncRoot)
            {
                var listed = SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName).ToArray();
                Assert.Contains(folder.FullName, listed);
                int keeper = KeeperCount();

                var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.LoadKernels(missing));

                Assert.Equal("path", e.ParamName);
                Assert.Equal(listed, SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName));
                Assert.Equal(keeper, KeeperCount());
            }

            Assert.Equal(90.0, RotationAngleFromIcrfDeg("IOA_G4_IN_DIR"), 1e-12);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(folder);
            root.Delete(true);
        }
    }

    private static object lockobj = new Object();

    [Fact]
    void UnloadKernels()
    {
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));
        SpiceAPI.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));
        var spk = new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk");
        var epoch = TimeSystem.Time.CreateTDB(668100000.0);
        Assert.True(SpiceAPI.Instance.ReadEphemeris(epoch, 399, -1785, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position.Magnitude() > 6.0e6);
        SpiceAPI.Instance.UnloadKernels(spk);

        // The folder is no longer listed, but its other kernel files, which SPICE still holds (#673): before, the folder
        // stayed listed while SPICE no longer held the SPK of DRAGONFLY32
        var folder = new DirectoryInfo(@"Data/UserDataTest/scn100");
        var listed = SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName)
            .Where(x => x == folder.FullName || x.StartsWith(folder.FullName + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
        Assert.DoesNotContain(folder.FullName, listed);
        Assert.DoesNotContain(spk.FullName, listed);
        // The kernel files the project copies to scn100 (IO.Astrodynamics.Tests.csproj), but the SPK of DRAGONFLY32
        string[] expected =
        [
            new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk").FullName,
            new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Clocks/DRAGONFLY32.tsc").FullName
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), listed.Order(StringComparer.Ordinal));
        // SPICE's reads: the SPK of DRAGONFLY32 (-1785) is gone, that of MySite (399132) is still there (both cover 668085625 to
        // 668174469 s TDB)
        Assert.Throws<SpiceException>(() => SpiceAPI.Instance.ReadEphemeris(epoch, 399, -1785, Frames.Frame.ICRF, Aberration.None));
        Assert.True(SpiceAPI.Instance.ReadEphemeris(epoch, 399, 399132, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position.Magnitude() > 6.0e6);
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
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703", new TimeSystem.Time(2024, 1, 1, TimeScale.UTC));
        
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
        LabelAssert.Equal(ke.Epoch, ke2.Epoch);
        Assert.Equal(ke.Frame, ke2.Frame);
        Assert.Equal(ke.Observer, ke2.Observer);
    }

    // ELARA (507): named by SPICE, no BODY507_GM in gm_de440.tpc (the kernel text, KernelTextValues). Before #711 the native
    // conversions returned the zero state silently (the found flag of their centre read undefined after the failed GM read);
    // they now return a NaN state with SPICE's message, which SpiceAPI throws
    [Fact]
    void ConvertConicElementsToStateVector_AboutABodyWithoutGm_ThrowsSpiceException()
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(507));
        var ke = new KeplerianElements(7.0e6, 0.1, 0.2, 0.3, 0.4, 0.5, new IO.Astrodynamics.Body.CelestialBody(507), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var error = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke));
        // The same message on every run (#714: before, "can't be found" on some runs only, the name read after the failed GM
        // read indeterminate)
        Assert.Equal(ElaraWithoutGmMessage, error.Message);
    }

    private const string ElaraWithoutGmMessage =
        "The elements cannot be converted to a state vector: CelestialItem id507 can't be read: The variable BODY507_GM could not be found in the kernel pool.";

    [Fact]
    void ConvertEquinoctialElementsToStateVector_AboutABodyWithoutGm_ThrowsSpiceException()
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(507));
        var eq = new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(7.0e6, 0.01, 0.0, 0.1, 0.0, 0.5, new IO.Astrodynamics.Body.CelestialBody(507),
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var error = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(eq));
        Assert.Equal(ElaraWithoutGmMessage, error.Message);
    }

    // An open orbit goes to conics_c with the GM of its observer, 0 for Elara (no BODY507_GM in gm_de440.tpc). Before the
    // review of #711, conics_c's SPICE(BADGM) was left pending and the conversion of the result threw "Celestial celestialItem
    // name can't be defined" (measured on a77531f)
    [Fact]
    void ConvertConicElementsToStateVector_OfAnOpenOrbitAboutABodyWithoutGm_ThrowsSpiceException()
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(507));
        var elara = new IO.Astrodynamics.Body.CelestialBody(507);
        var ke = new KeplerianElements(-7.0e6, 1.5, 0.2, 0.3, 0.4, 0.5, elara, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var error = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke));
        Assert.Contains("BODY507_GM", error.Message);
        error = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB));
        Assert.Contains("BODY507_GM", error.Message);
        AssertNothingPending();
    }

    // CONICS needs a GM above 0 (conics.c, SPICE(BADGM)); a GM the caller gives outside it, or not finite, is a bad argument.
    // Before the review of #711, a GM of 0 or below left conics_c's error pending, the conversion of the result throwing
    // "Celestial celestialItem name can't be defined", and a GM not finite returned a state that is not finite (measured on
    // a77531f)
    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.986e14)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    void ConvertConicElementsToStateVector_WithAGmNotFiniteAndAboveZero_ThrowsArgumentOutOfRangeException(double gm)
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB, gm));
        Assert.Equal("gm", error.ParamName);
        AssertNothingPending();
    }

    // CONICS needs a periapsis radius above 0 (conics.c, SPICE(BADPERIAPSEVALUE)), a bad argument on every overload. Before
    // #711 the zero state was returned silently, then InvalidOperationException with SPICE's message (measured on 46e6975 and
    // a77531f). Since #738 an osculating ellipse of negative semi-major axis and a hyperbola of positive one are refused by
    // the KeplerianElements constructor itself (ElementsValidationTests.Issue738_*); the elements that still reach the guard
    // are mean elements of e >= 1, whose semi-major axis comes from the mean motion (FromOMM, a > 0, so a (1 - e) < 0), and a
    // hyperbola whose a (1 - e) overflows to +Infinity
    public static TheoryData<string> ElementsOutsideTheConicsDomain => new() { "mean elements of e = 1.5", "a (1 - e) overflowing" };

    private static KeplerianElements ElementsOutsideTheConicsDomainNamed(string name) => name switch
    {
        "mean elements of e = 1.5" => KeplerianElements.FromOMM(15.5, 1.5, 51.6, 10.0, 20.0, 30.0, TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
        _ => new KeplerianElements(-1.0e300, 1.0e10, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF)
    };

    [Theory]
    [MemberData(nameof(ElementsOutsideTheConicsDomain))]
    void ConvertConicElementsToStateVector_OfAPeriapsisNotFiniteAndAboveZero_ThrowsArgumentException(string name)
    {
        var ke = ElementsOutsideTheConicsDomainNamed(name);
        Assert.False(ke.PerigeeRadius() > 0.0 && double.IsFinite(ke.PerigeeRadius()));
        var gm = TestHelpers.EarthAtJ2000.GM;
        foreach (var conversion in new Func<IO.Astrodynamics.OrbitalParameters.StateVector>[]
                 {
                     () => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke),
                     () => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB),
                     () => SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, TimeSystem.Time.J2000TDB, gm)
                 })
        {
            var error = Assert.Throws<ArgumentException>(conversion);
            Assert.Equal("keplerianElements", error.ParamName);
            Assert.Contains("CONICS", error.Message);
        }

        AssertNothingPending();
    }

    // The osculating elements of the former theory, an ellipse of negative semi-major axis and a hyperbola of positive one,
    // are refused at their construction since #738, before any conversion
    [Theory]
    [InlineData(-7.0e6, 0.1)]
    [InlineData(7.0e6, 1.5)]
    void KeplerianElements_OfASemiMajorAxisOfTheWrongSign_NeverReachTheConversion(double a, double e)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new KeplerianElements(a, e, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        Assert.Equal("semiMajorAxis", error.ParamName);
        AssertNothingPending();
    }

    // EQNCPV needs a semi-major axis above 0 (SPICE(BADSEMIAXIS)) and an eccentricity sqrt(h^2 + k^2) of 0.9 or less
    // (SPICE(ECCOUTOFRANGE)), eqncpv.c. Before #711 the zero state was returned silently, then InvalidOperationException with
    // SPICE's message (measured on 46e6975 and a77531f)
    [Theory]
    [InlineData(-7.0e6, 0.01)]
    [InlineData(7.0e6, 0.95)]
    void ConvertEquinoctialElementsToStateVector_OutsideTheDomainOfEqncpv_ThrowsArgumentException(double p, double f)
    {
        // Since #481 the elements refuse P ≤ 0 when they are built: a semi-major axis ≤ 0 then needs e > 1, beyond 0.9
        if (p <= 0.0)
        {
            var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(p,
                f, 0.0, 0.1, 0.0, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
            Assert.Equal("p", refused.ParamName);
            return;
        }

        var eq = new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(p, f, 0.0, 0.1, 0.0, 0.5, TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Assert.True(eq.SemiMajorAxis() < 0.0 || f > 0.9);
        var error = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(eq));
        Assert.Equal("equinoctialElements", error.ParamName);
        AssertNothingPending();
    }

    // The next native call reads the Earth: a SPICE error left pending makes GetCelestialBodyInfoProxy return the record of
    // Id -1, which GetCelestialBodyInfo throws for
    private static void AssertNothingPending()
    {
        var earth = SpiceAPI.Instance.GetCelestialBodyInfo(399);
        Assert.Equal(399, earth.Id);
        Assert.Equal("EARTH", earth.Name);
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
        LabelAssert.Equal(ke.Epoch, ke2.Epoch);
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
            var start = new TimeSystem.Time(2024, 1, 8, 8, 16, 19, TimeSystem.TimeScale.UTC);
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
            var start = new TimeSystem.Time(2024, 1, 8, 8, 20, 0, TimeScale.TDB);
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

    // #714: Propagate2Bodies and ConvertStateVectorToConicOrbitalElement passed the GM unchecked, and their proxies returned the
    // arrays prop2b_c and oscltx_c never wrote, with no message: about 7e-310 for a GM below 0 or a zero velocity (measured on
    // a80f6a5), or the end of the process as its first native call (ColdStartTests). The domains are those of CSPICE N0067:
    // prop2b.c and oscelt.c, SPICE(NONPOSITIVEMASS) for a GM not above 0; a GM that is not finite and a state that is not
    // finite raise no SPICE error (prop2b_c returns NaN), so they are checked before the native call
    private static StateVector LeoState(Vector3 velocity, IO.Astrodynamics.Body.CelestialItem observer = null) =>
        new(new Vector3(6800000.0, 0.0, 0.0), velocity, observer ?? TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.986e14)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    void TwoBodyCalls_WithAGmNotFiniteAndAboveZero_ThrowArgumentOutOfRangeException(double gm)
    {
        var sv = LeoState(new Vector3(0.0, 7656.2204182967143, 0.0));
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, gm, TimeSpan.FromSeconds(60.0)));
        Assert.Equal("gm", error.ParamName);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv, gm));
        Assert.Equal("gm", error.ParamName);
        AssertNothingPending();
    }

    [Fact]
    void TwoBodyCalls_OfAStateNotFinite_ThrowArgumentException()
    {
        var gm = TestHelpers.EarthAtJ2000.GM;
        foreach (var sv in new[]
                 {
                     new StateVector(new Vector3(double.NaN, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                         Frames.Frame.ICRF),
                     LeoState(new Vector3(0.0, double.PositiveInfinity, 0.0))
                 })
        {
            Assert.Equal("stateVector", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, gm, TimeSpan.FromSeconds(60.0))).ParamName);
            Assert.Equal("stateVector", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, TimeSpan.FromSeconds(60.0))).ParamName);
            Assert.Equal("stateVector", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv, gm)).ParamName);
            Assert.Equal("stateVector", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv)).ParamName);
        }

        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.Propagate2Bodies(null, gm, TimeSpan.FromSeconds(60.0)));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.Propagate2Bodies(null, TimeSpan.FromSeconds(60.0)));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.Propagate2Bodies(null, TimeSystem.Time.J2000TDB));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(null, gm));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(null));
        AssertNothingPending();
    }

    // ELARA (507): named by SPICE, no BODY507_GM in gm_de440.tpc, so its GM is 0: the overloads that take the GM of the observer
    // throw as ConvertConicElementsToStateVector(KeplerianElements, Time) does
    [Fact]
    void TwoBodyCalls_AboutABodyWithoutGm_ThrowInvalidOperationException()
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(507));
        var sv = LeoState(new Vector3(0.0, 7.5e3, 0.0), new IO.Astrodynamics.Body.CelestialBody(507));
        Assert.Contains("BODY507_GM", Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, TimeSpan.FromSeconds(60.0))).Message);
        Assert.Contains("BODY507_GM",
            Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, TimeSystem.Time.J2000TDB.AddSeconds(60.0))).Message);
        Assert.Contains("BODY507_GM", Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv)).Message);
        AssertNothingPending();
    }

    // A state outside the domain SPICE decides (prop2b.c, oscelt.c): SPICE's message, nothing pending, and the next read works
    [Fact]
    void TwoBodyCalls_OfAZeroVelocity_ThrowSpicesMessage()
    {
        var sv = LeoState(Vector3.Zero);
        var gm = TestHelpers.EarthAtJ2000.GM;
        var before = SpiceAPI.Instance.ReadEphemeris(TimeSystem.Time.J2000TDB, 399, 301, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var error = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, gm, TimeSpan.FromSeconds(60.0)));
        _output.WriteLine($"MESSAGE #714 Propagate2Bodies of a zero velocity: {error.Message}");
        Assert.EndsWith(": SPICE(ZEROVELOCITY)", error.Message);
        error = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv, gm));
        _output.WriteLine($"MESSAGE #714 ConvertStateVectorToConicOrbitalElement of a zero velocity: {error.Message}");
        Assert.EndsWith(": Zero vector for input velocity vector.", error.Message);
        var moon = SpiceAPI.Instance.ReadEphemeris(TimeSystem.Time.J2000TDB, 399, 301, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        Assert.Equal(before.Position, moon.Position);
        Assert.Equal(before.Velocity, moon.Velocity);
        AssertNothingPending();
    }

    // The messages of the states SPICE rejects are those of direct prop2b_c and oscltx_c calls (the long message, the short
    // one when prop2b.c sets none; a GM not above 0 is checked before the native call), and an ordinary state gives their
    // results bit for bit
    [Fact]
    [Trait("Category", "Validation")]
    void TwoBodyCalls_AreThoseOfCspice()
    {
        var gm = TestHelpers.EarthAtJ2000.GM;
        var cases = new (Vector3 Position, Vector3 Velocity, double Gm)[]
        {
            (Vector3.Zero, new Vector3(0.0, 7.5e3, 0.0), gm),
            (new Vector3(6800000.0, 0.0, 0.0), Vector3.Zero, gm),
            (new Vector3(6800000.0, 0.0, 0.0), new Vector3(7.5e3, 0.0, 0.0), gm)
        };
        foreach (var (position, velocity, mu) in cases)
        {
            var sv = new StateVector(position, velocity, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            double[] state = [position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z];
            var prop2b = Body.CspiceDirect.Prop2b(mu, state, 60.0);
            var error = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.Propagate2Bodies(sv, mu, TimeSpan.FromSeconds(60.0)));
            Assert.EndsWith(": " + (string.IsNullOrEmpty(prop2b.Long) ? prop2b.Short : prop2b.Long), error.Message);
            var oscltx = Body.CspiceDirect.Oscltx(state, 0.0, mu);
            error = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv, mu));
            Assert.EndsWith(": " + oscltx.Long, error.Message);
            _output.WriteLine($"MESSAGE #714 {prop2b.Short} / {oscltx.Short} {oscltx.Long}");
        }

        var leo = LeoState(new Vector3(0.0, 7656.2204182967143, 1000.0));
        double[] leoState = [leo.Position.X, leo.Position.Y, leo.Position.Z, leo.Velocity.X, leo.Velocity.Y, leo.Velocity.Z];
        var reference = Body.CspiceDirect.Prop2b(gm, leoState, 3600.0).State;
        var propagated = SpiceAPI.Instance.Propagate2Bodies(leo, gm, TimeSpan.FromSeconds(3600.0));
        Assert.Equal(new Vector3(reference[0], reference[1], reference[2]), propagated.Position);
        Assert.Equal(new Vector3(reference[3], reference[4], reference[5]), propagated.Velocity);
        var elements = Body.CspiceDirect.Oscltx(leoState, 0.0, gm).Elements;
        var converted = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(leo, gm);
        Assert.Equal(elements[1], converted.E);
        Assert.Equal(elements[2], converted.I);
        Assert.Equal(elements[3], converted.RAAN);
        Assert.Equal(elements[4], converted.AOP);
        Assert.Equal(elements[5], converted.M);
        Assert.Equal(elements[9], converted.A);
        AssertNothingPending();
    }

    // #714: a transformation SPICE cannot give threw the same message whatever the cause, "the frame data (a PCK, CK or frame
    // kernel) does not cover this epoch or is not loaded", since TransformFrameProxy dropped SPICE's message (measured on
    // a80f6a5). The message keeps the frames and the epoch and gives SPICE's: SPICE(UNKNOWNFRAME) of a frame SPICE does not
    // know, SPICE(FRAMEDATANOTFOUND) of ITRF93 past the coverage of earth_latest_high_prec.bpc (6.3e9 s TDB is 2199-08-22)
    private const string UnknownFrameMessage = "The frame NOT_A_FRAME was not recognized as a known reference frame.";
    private const string Itrf93NotCoveredMessage =
        "PCK data required to compute the orientation of the body-fixed frame ITRF93 for epoch 2199 AUG 22 04:00:00.000 TDB were not found.";

    [Fact]
    void TransformFrame_ThatSpiceCannotGive_ThrowsSpicesMessage()
    {
        // An unknown name is a bad argument since R25 of #668: the error of #714 is the inner exception
        var argument = Assert.Throws<ArgumentException>(() =>
            SpiceAPI.Instance.TransformFrame(TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, new Frames.Frame("NOT_A_FRAME")));
        Assert.Equal("toFrame", argument.ParamName);
        var unknown = Assert.IsType<SpiceException>(argument.InnerException);
        _output.WriteLine($"MESSAGE #714 {unknown.Message}");
        Assert.StartsWith($"SPICE gives no transformation from {Frames.Frame.ICRF.Name} to NOT_A_FRAME at {TimeSystem.Time.J2000TDB}: ", unknown.Message);
        Assert.EndsWith(UnknownFrameMessage, unknown.Message);

        var epoch = TimeSystem.Time.CreateTDB(6.3e9);
        var itrf93 = new Frames.Frame("ITRF93");
        var uncovered = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.TransformFrame(epoch, Frames.Frame.ICRF, itrf93));
        _output.WriteLine($"MESSAGE #714 {uncovered.Message}");
        Assert.StartsWith($"SPICE gives no transformation from {Frames.Frame.ICRF.Name} to ITRF93 at {epoch}: ", uncovered.Message);
        Assert.Contains(Itrf93NotCoveredMessage, uncovered.Message);

        // Through the data provider, the frames reversed
        var provider = Assert.Throws<SpiceException>(() => Frames.Frame.ICRF.ToFrame(itrf93, epoch));
        Assert.Contains(Itrf93NotCoveredMessage, provider.Message);
        AssertNothingPending();
    }

    // The message SpiceAPI gives is that of a direct sxform_c call
    [Fact]
    [Trait("Category", "Validation")]
    void TransformFrame_ThatSpiceCannotGive_ThrowsTheMessageOfSxform()
    {
        foreach (var (to, et) in new[] { ("NOT_A_FRAME", 0.0), ("ITRF93", 6.3e9) })
        {
            var expected = Body.CspiceDirect.SxformError("J2000", to, et);
            Assert.False(string.IsNullOrEmpty(expected));
            var thrown = Record.Exception(() =>
                SpiceAPI.Instance.TransformFrame(TimeSystem.Time.CreateTDB(et), Frames.Frame.ICRF, new Frames.Frame(to)));
            // The unknown name an ArgumentException since R25 of #668, SPICE's message on its inner exception
            var error = Assert.IsType<SpiceException>(to == "NOT_A_FRAME" ? Assert.IsType<ArgumentException>(thrown).InnerException : thrown);
            Assert.EndsWith(": " + expected, error.Message);
        }

        AssertNothingPending();
    }

    // #714: KeplerianElements and EquinoctialElements accepted elements that are not finite, which CONICS and the native
    // ellipse let through (conics.c checks its domain with comparisons NaN passes): a NaN mean anomaly gave a finite state,
    // silently, and a NaN inclination a NaN state that threw "Native proxy call failed", or the message of an earlier call
    // about another body (measured on a80f6a5). They are a bad argument: since #726 the constructors refuse them, naming the
    // element, so the check of SpiceAPI before the native call is no longer reached through these types
    [Theory]
    [InlineData(double.NaN, 0.5, 0.3, 0.4)]
    [InlineData(0.2, double.NaN, 0.3, 0.4)]
    [InlineData(0.2, 0.5, double.NaN, 0.4)]
    [InlineData(0.2, 0.5, 0.3, double.PositiveInfinity)]
    void ConvertConicElementsToStateVector_OfElementsNotFinite_ThrowsArgumentException(double i, double m, double raan, double aop)
    {
        var error = Assert.Throws<ArgumentException>(() => new KeplerianElements(7.0e6, 0.1, i, raan, aop, m, TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        Assert.Equal(!double.IsFinite(i) ? "inclination" : !double.IsFinite(raan) ? "ascendingNode" :
            !double.IsFinite(aop) ? "argumentOfPeriapsis" : "meanAnomaly", error.ParamName);
        AssertNothingPending();
    }

    [Theory]
    [InlineData(double.NaN, 0.0, 0.5)]
    [InlineData(0.1, double.NaN, 0.5)]
    [InlineData(0.1, 0.0, double.NaN)]
    [InlineData(0.1, 0.0, double.PositiveInfinity)]
    void ConvertEquinoctialElementsToStateVector_OfElementsNotFinite_ThrowsArgumentException(double h, double k, double l)
    {
        var error = Assert.Throws<ArgumentException>(() => new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(7.0e6, 0.01, 0.0,
            h, k, l, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        Assert.Equal(!double.IsFinite(h) ? "h" : !double.IsFinite(k) ? "k" : "l0", error.ParamName);
        AssertNothingPending();
    }

    // The reason the elements are checked by the library: conics_c gives a finite state for a NaN mean anomaly and a NaN state
    // for a NaN inclination, with no SPICE error
    [Fact]
    [Trait("Category", "Validation")]
    void Conics_OfElementsNotFinite_SignalsNoError()
    {
        var gm = TestHelpers.EarthAtJ2000.GM;
        var (finite, finiteError) = Body.CspiceDirect.Conics([6.3e6, 0.1, 0.2, 0.3, 0.4, double.NaN, 0.0, gm], 0.0);
        _output.WriteLine($"MEASURE #714 conics_c with M0 = NaN: x = {finite[0]:E16} m, error {finiteError ?? "none"}");
        Assert.Null(finiteError);
        Assert.True(finite.All(double.IsFinite));
        var (nan, nanError) = Body.CspiceDirect.Conics([6.3e6, 0.1, double.NaN, 0.3, 0.4, 0.5, 0.0, gm], 0.0);
        Assert.Null(nanError);
        Assert.True(nan.All(double.IsNaN));
    }

    // #714: ReadOrientation threw "No orientation found" for a CK segment without angular velocity: ckgpav_c skips it with no
    // error (ckgpav_c.c, Restrictions 1), and the native layer fell back to ckgp_c only on an error. A type-3 CK written by
    // ckw03_c with and without rates (11 records 100 s apart, a rotation about z at 1e-3 rad/s, the SPICE quaternion
    // (cos(wt/2), 0, 0, sin(wt/2))), the clock written by Clock.WriteAsync: each record is ckgp_c's C-matrix and the closed form
    // R_z(1e-3 t), with a zero angular velocity without rates and (0, 0, 1e-3) rad/s with them. ckw03_c is not exported by the
    // Windows DLL
    // Twice the worst on Linux x64 of the angle of a record to ckgp_c and to the closed form (the MEASURE lines, 1.570e-16
    // rad for both CKs, CI run 43974478027, 2026-11-16), rounded up at the second digit: the Validation test runs on Linux
    // alone
    private const double CkRecordAngleLimit = 3.2e-16;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Validation")]
    async Task ReadOrientation_OfACkWithOrWithoutAngularVelocity_IsTheCkgpPointing(bool withRates)
    {
        const double rate = 1.0e-3;
        int id = withRates ? -1999717 : -1999716;
        var dir = Directory.CreateTempSubdirectory("ioa714ck");
        var clockFile = new FileInfo(Path.Combine(dir.FullName, "clock.tsc"));
        var ckFile = new FileInfo(Path.Combine(dir.FullName, "pointing.bc"));
        var orbit = new StateVector(new Vector3(7.0e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(id, $"IOA714CK{-id}", 1000.0, 10000.0, new Clock($"IOA714CLOCK{-id}", 65536), orbit);
        await spacecraft.Clock.WriteAsync(clockFile);
        SpiceAPI.Instance.LoadKernels(clockFile);
        try
        {
            var sclk = new double[11];
            var quaternions = new double[44];
            var angularVelocities = new double[33];
            for (int i = 0; i < 11; i++)
            {
                sclk[i] = Body.CspiceDirect.Sce2c(id, 100.0 * i);
                double angle = rate * 100.0 * i;
                quaternions[4 * i] = System.Math.Cos(angle / 2.0);
                quaternions[4 * i + 3] = System.Math.Sin(angle / 2.0);
                angularVelocities[3 * i + 2] = rate;
            }

            Body.CspiceDirect.WriteType3Ck(ckFile.FullName, id * 1000, "J2000", withRates, sclk, quaternions, angularVelocities);
            SpiceAPI.Instance.LoadKernels(ckFile);
            try
            {
                var records = SpiceAPI.Instance.ReadOrientation(new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(1000.0)), id,
                    TimeSpan.FromSeconds(10.0), Frames.Frame.ICRF, TimeSpan.FromSeconds(100.0)).ToArray();
                Assert.Equal(11, records.Length);
                double worstSpice = 0.0, worstClosedForm = 0.0;
                for (int i = 0; i < records.Length; i++)
                {
                    var q = records[i].Rotation;
                    var matrix = Body.CspiceDirect.Q2m(q.W, q.VectorPart.X, q.VectorPart.Y, q.VectorPart.Z);
                    var cmat = Body.CspiceDirect.Ckgp(id * 1000, sclk[i], 0.0, "J2000");
                    Assert.NotNull(cmat);
                    double t = (records[i].Epoch - TimeSystem.Time.J2000TDB).TotalSeconds;
                    double c = System.Math.Cos(rate * t), s = System.Math.Sin(rate * t);
                    worstSpice = System.Math.Max(worstSpice, RotationAngle(matrix, cmat));
                    worstClosedForm = System.Math.Max(worstClosedForm, RotationAngle(matrix, [c, -s, 0.0, s, c, 0.0, 0.0, 0.0, 1.0]));
                    Assert.Equal(withRates ? new Vector3(0.0, 0.0, rate) : Vector3.Zero, records[i].AngularVelocity);
                    Assert.Equal(Frames.Frame.ICRF, records[i].ReferenceFrame);
                }

                _output.WriteLine($"MEASURE #714 CK {(withRates ? "with" : "without")} rates: worst angle to ckgp_c {worstSpice:E3} rad, to the closed form {worstClosedForm:E3} rad");
                Assert.True(worstSpice <= CkRecordAngleLimit, $"{worstSpice:E3} rad");
                Assert.True(worstClosedForm <= CkRecordAngleLimit, $"{worstClosedForm:E3} rad");
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(ckFile);
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(clockFile);
            dir.Delete(true);
        }
    }

    // #724: ReadOrientation read ckgpav_c first and ckgp_c only when it found nothing (#714), but ckgpav_c skips a segment
    // without angular velocity, so that where a higher-priority segment without rates and a lower-priority one with rates
    // cover the same epoch, the read gave the lower-priority segment's pointing and rate, where ckgp_c gives the
    // higher-priority one. The read now follows ckgp_c's segment: its pointing, with ckgpav_c's rate when that segment holds
    // one, a zero angular velocity when it holds none (decided by S. Guillet, 2026-11-18). The kernels of
    // tools/ck_priority_reference (Data/CkPriorityReference): CKPRIO's clock, a type-3 CK with rates (a rotation about z at
    // 1e-3 rad/s every 100 s over [0, 1000] s TDB) and one without (about x at 2e-3 rad/s every 50 s over [300, 700] s),
    // loaded in either order; the references are ckgp_c's C-matrix and epoch at each epoch with a tolerance of 1 s, which the
    // tool writes (299.5 s and 700.5 s, within the tolerance of the CK without rates, take its records at 300 s and 700 s),
    // and the closed form of the segment ckgp_c reads, whose angular velocity is (0, 0, 1e-3) rad/s or zero. The kernels are
    // read, not written, so the test runs on every platform
    // Twice the worst over Linux x64, Windows x64 and macOS arm64 of the angle of a record to ckgp_c and to the closed form
    // (5.551e-16 rad for both on the three, either order, and for the reads after a search with angular velocity below,
    // the MEASURE lines, CI run 44395076184, 2026-11-18), rounded up at the second digit
    private const double CkPriorityAngleLimit = 1.2e-15;

    private static readonly int CkPrioritySpacecraft = -472401;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    void ReadOrientation_FollowsTheCkSegmentPriority(bool withoutRatesLoadedLast)
    {
        var dir = new DirectoryInfo(Path.Combine("Data", "CkPriorityReference"));
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "ck_priority_reference.json")));
        var expected = document.RootElement.GetProperty(withoutRatesLoadedLast ? "norates_last" : "rates_last").EnumerateArray().ToArray();
        var clock = new FileInfo(Path.Combine(dir.FullName, "ckprio.tsc"));
        var rates = new FileInfo(Path.Combine(dir.FullName, "ckprio_rates.bc"));
        var noRates = new FileInfo(Path.Combine(dir.FullName, "ckprio_norates.bc"));
        var first = withoutRatesLoadedLast ? rates : noRates;
        var last = withoutRatesLoadedLast ? noRates : rates;
        SpiceAPI.Instance.LoadKernels(clock);
        SpiceAPI.Instance.LoadKernels(first);
        SpiceAPI.Instance.LoadKernels(last);
        try
        {
            // The grid 0, 50, ..., 1000 s, then 299.5 s and 700.5 s
            var records = SpiceAPI.Instance.ReadOrientation(new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(1000.0)),
                    CkPrioritySpacecraft, TimeSpan.FromSeconds(1.0), Frames.Frame.ICRF, TimeSpan.FromSeconds(50.0))
                .Concat(SpiceAPI.Instance.ReadOrientation(new Window(TimeSystem.Time.CreateTDB(299.5), TimeSystem.Time.CreateTDB(700.5)),
                    CkPrioritySpacecraft, TimeSpan.FromSeconds(1.0), Frames.Frame.ICRF, TimeSpan.FromSeconds(401.0))).ToArray();
            Assert.Equal(expected.Length, records.Length);
            var (worstSpice, worstClosedForm, recordsWithoutRates) =
                CompareCkPriorityRecords(records, expected, et => withoutRatesLoadedLast && et >= 300.0 && et <= 700.0);
            Assert.Equal(withoutRatesLoadedLast ? 11 : 0, recordsWithoutRates);
            _output.WriteLine(
                $"MEASURE #724 CK priority ({(withoutRatesLoadedLast ? "without" : "with")} rates loaded last): worst angle to ckgp_c {worstSpice:E3} rad, to the closed form {worstClosedForm:E3} rad");
            Assert.True(worstSpice <= CkPriorityAngleLimit, $"{worstSpice:E3} rad");
            Assert.True(worstClosedForm <= CkPriorityAngleLimit, $"{worstClosedForm:E3} rad");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(last);
            SpiceAPI.Instance.UnloadKernels(first);
            SpiceAPI.Instance.UnloadKernels(clock);
        }
    }

    // Each record against its reference entry of ck_priority_reference.json (ckgp_c's C-matrix and epoch) and the closed
    // form of the segment ckgp_c reads (about x at 2e-3 rad/s where readsNoRates(et), about z at 1e-3 rad/s otherwise),
    // with its angular velocity (zero, or (0, 0, 1e-3) rad/s exactly); the worst angles and the number of records read
    // from a CK without rates
    private static (double WorstSpice, double WorstClosedForm, int RecordsWithoutRates) CompareCkPriorityRecords(
        StateOrientation[] records, System.Text.Json.JsonElement[] expected, Func<double, bool> readsNoRates)
    {
        double worstSpice = 0.0, worstClosedForm = 0.0;
        int recordsWithoutRates = 0;
        for (int i = 0; i < records.Length; i++)
        {
            double et = expected[i].GetProperty("et").GetDouble();
            var cmat = expected[i].GetProperty("cmat").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            // The C-matrix of the record: its column k is the image of the k-th axis of the reference frame
            var q = records[i].Rotation;
            var columns = new[] { Vector3.VectorX.Rotate(q), Vector3.VectorY.Rotate(q), Vector3.VectorZ.Rotate(q) };
            double[] matrix =
            [
                columns[0].X, columns[1].X, columns[2].X, columns[0].Y, columns[1].Y, columns[2].Y, columns[0].Z, columns[1].Z,
                columns[2].Z
            ];
            bool noRates = readsNoRates(et);
            recordsWithoutRates += noRates ? 1 : 0;
            double angle = noRates ? 2.0e-3 * et : 1.0e-3 * et;
            double c = System.Math.Cos(angle), s = System.Math.Sin(angle);
            double[] closedForm = noRates ? [1.0, 0.0, 0.0, 0.0, c, -s, 0.0, s, c] : [c, -s, 0.0, s, c, 0.0, 0.0, 0.0, 1.0];
            worstSpice = System.Math.Max(worstSpice, RotationAngle(matrix, cmat));
            worstClosedForm = System.Math.Max(worstClosedForm, RotationAngle(matrix, closedForm));
            Assert.Equal(TimeSystem.Time.CreateTDB(et), records[i].Epoch);
            Assert.Equal(noRates ? Vector3.Zero : new Vector3(0.0, 0.0, 1.0e-3), records[i].AngularVelocity);
            Assert.Equal(Frames.Frame.ICRF, records[i].ReferenceFrame);
        }

        return (worstSpice, worstClosedForm, recordsWithoutRates);
    }

    // The record of one epoch, read alone (a window of one epoch)
    private static StateOrientation ReadCkPriorityRecord(double epoch) =>
        SpiceAPI.Instance.ReadOrientation(new Window(TimeSystem.Time.CreateTDB(epoch), TimeSystem.Time.CreateTDB(epoch)),
            CkPrioritySpacecraft, TimeSpan.FromSeconds(1.0), Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0)).Single();

    private static System.Text.Json.JsonElement CkPriorityEntry(System.Text.Json.JsonElement[] entries, double epoch) =>
        entries.Single(e => e.GetProperty("epoch").GetDouble() == epoch);

    // Review of #724: CSPICE keeps, per instrument, the segment of its last search and the interval over which that segment
    // has the highest priority (ckbsr.c, the re-use interval), and a search with angular velocity (ckgpav_c, or sxform_c
    // through ckfxfm) that skips a higher-priority segment without rates does not shrink that interval by it, so that the
    // next search without angular velocity, the one that finds the segment ckgp_c reads, returned the lower-priority segment
    // with rates there. The read now voids that interval before its search, so that every read follows the priority of a
    // search made in a fresh process (decided by S. Guillet, 2026-11-18). The CK with rates and, loaded last, a CK without
    // rates of two interpolation intervals, [300, 400] s and [600, 700] s (ckprio_gap.bc, about x at 2e-3 rad/s): the epochs
    // 450 to 550 s, in its gap, are read from the CK with rates by ckgpav_c, and the epochs read after them from the CK
    // without rates, in one call (the grid 0, 50, ..., 1000 s) and one epoch a call (500 s, then 650 s and 600 s). Before,
    // 600, 650 and 700 s of the grid and 650 s and 600 s read alone took the CK with rates' pointing and rate. References:
    // ckgp_c of tools/ck_priority_reference, each read with the CKs loaded again ("gap_last"), and the closed form
    [Fact]
    void ReadOrientation_FollowsTheCkSegmentPriority_AfterAReadOfALowerPrioritySegment()
    {
        var dir = new DirectoryInfo(Path.Combine("Data", "CkPriorityReference"));
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "ck_priority_reference.json")));
        var expected = document.RootElement.GetProperty("gap_last").EnumerateArray().ToArray();
        var clock = new FileInfo(Path.Combine(dir.FullName, "ckprio.tsc"));
        var rates = new FileInfo(Path.Combine(dir.FullName, "ckprio_rates.bc"));
        var gap = new FileInfo(Path.Combine(dir.FullName, "ckprio_gap.bc"));
        SpiceAPI.Instance.LoadKernels(clock);
        SpiceAPI.Instance.LoadKernels(rates);
        SpiceAPI.Instance.LoadKernels(gap);
        try
        {
            static bool ReadsGap(double et) => et is >= 300.0 and <= 400.0 or >= 600.0 and <= 700.0;
            var grid = SpiceAPI.Instance.ReadOrientation(new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(1000.0)),
                CkPrioritySpacecraft, TimeSpan.FromSeconds(1.0), Frames.Frame.ICRF, TimeSpan.FromSeconds(50.0)).ToArray();
            Assert.Equal(21, grid.Length);
            var (gridSpice, gridClosedForm, gridWithoutRates) = CompareCkPriorityRecords(grid, expected.Take(21).ToArray(), ReadsGap);
            Assert.Equal(6, gridWithoutRates);

            double[] alone = [500.0, 650.0, 600.0];
            var records = alone.Select(ReadCkPriorityRecord).ToArray();
            var (aloneSpice, aloneClosedForm, aloneWithoutRates) =
                CompareCkPriorityRecords(records, alone.Select(e => CkPriorityEntry(expected, e)).ToArray(), ReadsGap);
            Assert.Equal(2, aloneWithoutRates);

            double worstSpice = System.Math.Max(gridSpice, aloneSpice), worstClosedForm = System.Math.Max(gridClosedForm, aloneClosedForm);
            _output.WriteLine(
                $"MEASURE #724 CK priority after a read of a lower-priority segment: worst angle to ckgp_c {worstSpice:E3} rad, to the closed form {worstClosedForm:E3} rad");
            Assert.True(worstSpice <= CkPriorityAngleLimit, $"{worstSpice:E3} rad");
            Assert.True(worstClosedForm <= CkPriorityAngleLimit, $"{worstClosedForm:E3} rad");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(gap);
            SpiceAPI.Instance.UnloadKernels(rates);
            SpiceAPI.Instance.UnloadKernels(clock);
        }
    }

    // Review of #724, the same re-use interval left by a transformation to the spacecraft's CK frame (TransformFrame,
    // sxform_c, which needs the angular velocity and skips the CK without rates): the CK with rates and, loaded last, the one
    // without rates over [300, 700] s, the frame CKPRIO_SC of ckprio.tf; a TransformFrame from ICRF to CKPRIO_SC at 500 s,
    // read from the CK with rates, then the reads at 500 s and 650 s alone, from the CK without rates (before, both took
    // the CK with rates' pointing and rate). References: ckgp_c of tools/ck_priority_reference ("norates_last") and the
    // closed form
    [Fact]
    void ReadOrientation_FollowsTheCkSegmentPriority_AfterATransformFrame()
    {
        var dir = new DirectoryInfo(Path.Combine("Data", "CkPriorityReference"));
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "ck_priority_reference.json")));
        var expected = document.RootElement.GetProperty("norates_last").EnumerateArray().ToArray();
        var clock = new FileInfo(Path.Combine(dir.FullName, "ckprio.tsc"));
        var frame = new FileInfo(Path.Combine(dir.FullName, "ckprio.tf"));
        var rates = new FileInfo(Path.Combine(dir.FullName, "ckprio_rates.bc"));
        var noRates = new FileInfo(Path.Combine(dir.FullName, "ckprio_norates.bc"));
        SpiceAPI.Instance.LoadKernels(clock);
        SpiceAPI.Instance.LoadKernels(frame);
        SpiceAPI.Instance.LoadKernels(rates);
        SpiceAPI.Instance.LoadKernels(noRates);
        try
        {
            var transformation = SpiceAPI.Instance.TransformFrame(TimeSystem.Time.CreateTDB(500.0), Frames.Frame.ICRF, new Frames.Frame("CKPRIO_SC"));
            // sxform_c reads the CK with rates: its angular velocity, (0, 0, 1e-3) rad/s
            Assert.Equal(1.0e-3, transformation.AngularVelocity.Magnitude(), 15);

            double[] alone = [500.0, 650.0];
            var records = alone.Select(ReadCkPriorityRecord).ToArray();
            var (worstSpice, worstClosedForm, recordsWithoutRates) =
                CompareCkPriorityRecords(records, alone.Select(e => CkPriorityEntry(expected, e)).ToArray(), et => et is >= 300.0 and <= 700.0);
            Assert.Equal(2, recordsWithoutRates);
            _output.WriteLine(
                $"MEASURE #724 CK priority after a TransformFrame: worst angle to ckgp_c {worstSpice:E3} rad, to the closed form {worstClosedForm:E3} rad");
            Assert.True(worstSpice <= CkPriorityAngleLimit, $"{worstSpice:E3} rad");
            Assert.True(worstClosedForm <= CkPriorityAngleLimit, $"{worstClosedForm:E3} rad");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(noRates);
            SpiceAPI.Instance.UnloadKernels(rates);
            SpiceAPI.Instance.UnloadKernels(frame);
            SpiceAPI.Instance.UnloadKernels(clock);
        }
    }

    // Review of #724, the re-use interval left by a direct ckgpav_c (CspiceDirect, the CSPICE of the native library; not
    // exported by the Windows DLL): the CK without rates loaded last, ckgpav_c at 500 s (the CK with rates), then the read
    // at 500 s, from the CK without rates (before, the CK with rates' 0.5 rad about z and its rate). References: ckgp_c of
    // tools/ck_priority_reference ("norates_last") and the closed form
    [Fact]
    [Trait("Category", "Validation")]
    void ReadOrientation_FollowsTheCkSegmentPriority_AfterACkgpav()
    {
        var dir = new DirectoryInfo(Path.Combine("Data", "CkPriorityReference"));
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "ck_priority_reference.json")));
        var expected = document.RootElement.GetProperty("norates_last").EnumerateArray().ToArray();
        var clock = new FileInfo(Path.Combine(dir.FullName, "ckprio.tsc"));
        var rates = new FileInfo(Path.Combine(dir.FullName, "ckprio_rates.bc"));
        var noRates = new FileInfo(Path.Combine(dir.FullName, "ckprio_norates.bc"));
        SpiceAPI.Instance.LoadKernels(clock);
        SpiceAPI.Instance.LoadKernels(rates);
        SpiceAPI.Instance.LoadKernels(noRates);
        try
        {
            var lowerPriority = Body.CspiceDirect.Ckgpav(CkPrioritySpacecraft, Body.CspiceDirect.Sce2c(CkPrioritySpacecraft, 500.0), 65536.0, "J2000");
            Assert.NotNull(lowerPriority);
            Assert.Equal(1.0e-3, lowerPriority.Value.AngularVelocity[2]);

            var records = new[] { ReadCkPriorityRecord(500.0) };
            var (worstSpice, worstClosedForm, recordsWithoutRates) =
                CompareCkPriorityRecords(records, [CkPriorityEntry(expected, 500.0)], _ => true);
            Assert.Equal(1, recordsWithoutRates);
            _output.WriteLine(
                $"MEASURE #724 CK priority after a ckgpav_c: worst angle to ckgp_c {worstSpice:E3} rad, to the closed form {worstClosedForm:E3} rad");
            Assert.True(worstSpice <= CkPriorityAngleLimit, $"{worstSpice:E3} rad");
            Assert.True(worstClosedForm <= CkPriorityAngleLimit, $"{worstClosedForm:E3} rad");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(noRates);
            SpiceAPI.Instance.UnloadKernels(rates);
            SpiceAPI.Instance.UnloadKernels(clock);
        }
    }

    // #724 in a frame other than the segments' (ECLIPJ2000), where ckgp_c rotates the C-matrix by refchg and ckgpav_c the
    // state by frmchg: the same kernels, each record ckgp_c's C-matrix (CspiceDirect, the CSPICE of the native library) and,
    // read from the CK with rates, ckgpav_c's angular velocity. CspiceDirect is not exported by the Windows DLL
    // Twice the worst on Linux x64 of the angle of a record to ckgp_c (2.483e-16 rad, the MEASURE lines, CI run 44395076184,
    // 2026-11-18), rounded up at the second digit (the Validation category runs on Linux alone). The angular velocity read
    // from the CK with rates is ckgpav_c's own, bit for bit (0 rad/s measured)
    private const double CkPriorityEclipticAngleLimit = 5.0e-16;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Validation")]
    void ReadOrientation_InAnotherFrame_FollowsTheCkSegmentPriority(bool withoutRatesLoadedLast)
    {
        var dir = new DirectoryInfo(Path.Combine("Data", "CkPriorityReference"));
        var clock = new FileInfo(Path.Combine(dir.FullName, "ckprio.tsc"));
        var rates = new FileInfo(Path.Combine(dir.FullName, "ckprio_rates.bc"));
        var noRates = new FileInfo(Path.Combine(dir.FullName, "ckprio_norates.bc"));
        var first = withoutRatesLoadedLast ? rates : noRates;
        var last = withoutRatesLoadedLast ? noRates : rates;
        SpiceAPI.Instance.LoadKernels(clock);
        SpiceAPI.Instance.LoadKernels(first);
        SpiceAPI.Instance.LoadKernels(last);
        try
        {
            var records = SpiceAPI.Instance.ReadOrientation(new Window(TimeSystem.Time.CreateTDB(0.0), TimeSystem.Time.CreateTDB(1000.0)),
                CkPrioritySpacecraft, TimeSpan.FromSeconds(1.0), Frames.Frame.ECLIPTIC_J2000, TimeSpan.FromSeconds(50.0)).ToArray();
            Assert.Equal(21, records.Length);
            double worstAngle = 0.0, worstRate = 0.0;
            for (int i = 0; i < records.Length; i++)
            {
                double sclk = Body.CspiceDirect.Sce2c(CkPrioritySpacecraft, 50.0 * i);
                var cmat = Body.CspiceDirect.Ckgp(CkPrioritySpacecraft * 1000, sclk, 65536.0, "ECLIPJ2000");
                Assert.NotNull(cmat);
                var q = records[i].Rotation;
                var matrix = Body.CspiceDirect.Q2m(q.W, q.VectorPart.X, q.VectorPart.Y, q.VectorPart.Z);
                worstAngle = System.Math.Max(worstAngle, RotationAngle(matrix, cmat));
                bool readsNoRates = withoutRatesLoadedLast && 50.0 * i >= 300.0 && 50.0 * i <= 700.0;
                if (readsNoRates)
                {
                    Assert.Equal(Vector3.Zero, records[i].AngularVelocity);
                }
                else
                {
                    var reference = Body.CspiceDirect.Ckgpav(CkPrioritySpacecraft, sclk, 65536.0, "ECLIPJ2000");
                    Assert.NotNull(reference);
                    var av = reference.Value.AngularVelocity;
                    worstRate = System.Math.Max(worstRate, (records[i].AngularVelocity - new Vector3(av[0], av[1], av[2])).Magnitude());
                }

                Assert.Equal(Frames.Frame.ECLIPTIC_J2000, records[i].ReferenceFrame);
            }

            _output.WriteLine(
                $"MEASURE #724 CK priority in ECLIPJ2000 ({(withoutRatesLoadedLast ? "without" : "with")} rates loaded last): worst angle to ckgp_c {worstAngle:E3} rad, worst angular velocity to ckgpav_c {worstRate:E3} rad/s");
            Assert.True(worstAngle <= CkPriorityEclipticAngleLimit, $"{worstAngle:E3} rad");
            Assert.Equal(0.0, worstRate);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(last);
            SpiceAPI.Instance.UnloadKernels(first);
            SpiceAPI.Instance.UnloadKernels(clock);
        }
    }

    // The angle of the rotation between two rotation matrices (row-major): ||A - B||_F = 2 sqrt(2) sin(θ / 2), exact near 0
    private static double RotationAngle(double[] a, double[] b)
    {
        double sum = 0.0;
        for (int k = 0; k < 9; k++)
        {
            sum += (a[k] - b[k]) * (a[k] - b[k]);
        }

        return 2.0 * System.Math.Asin(System.Math.Min(1.0, System.Math.Sqrt(sum) / (2.0 * System.Math.Sqrt(2.0))));
    }

    // #714 (the last row of #655): WriteEphemeris checked that the centre of each state is a celestial body (#706) but not
    // that SPICE names it, so that a centre of an identifier no kernel names reached the native writer, which threw
    // InvalidOperationException "CelestialItem id2999991 can't be found" (measured on a80f6a5, with and without a GM in the
    // kernel pool). It is a bad argument, rejected before anything is unloaded or written, as the element conversions reject
    // such a centre (#711, #680's rule); the reference is SPICE's names (GetCelestialBodyInfoProxy gives the record of Id -1)
    private sealed class UnnamedBody() : IO.Astrodynamics.Body.CelestialBody(UnnamedBodyId, "IOA_714_UNNAMED", 1.0e20);

    private const int UnnamedBodyId = 2999991;

    private static StateVector[] StatesAbout(IO.Astrodynamics.Body.CelestialItem centre) =>
        Enumerable.Range(0, 5).Select(i => new StateVector(new Vector3(7.0e6 + i, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), centre,
            TimeSystem.Time.J2000TDB.AddSeconds(60.0 * i), Frames.Frame.ICRF)).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void WriteEphemeris_AboutACentreSpiceDoesNotName_ThrowsArgumentExceptionAndKeepsTheFile(bool withGm)
    {
        Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.GetCelestialBodyInfo(UnnamedBodyId));
        var dir = Directory.CreateTempSubdirectory("ioa714we");
        var gmKernel = new FileInfo(Path.Combine(dir.FullName, "gm.tpc"));
        File.WriteAllText(gmKernel.FullName, $"KPL/PCK\n\\begindata\nBODY{UnnamedBodyId}_GM = ( 1.0E+03 )\n\\begintext\n");
        var target = new FileInfo(Path.Combine(dir.FullName, "existing.bsp"));
        File.WriteAllText(target.FullName, "a file the writer would replace");
        var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(target.FullName));
        if (withGm)
        {
            SpiceAPI.Instance.LoadKernels(gmKernel);
        }

        try
        {
            var error = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteEphemeris(target, -1999718, StatesAbout(new UnnamedBody())));
            Assert.Equal("stateVectors", error.ParamName);
            Assert.StartsWith($"The NAIF identifier {UnnamedBodyId} has no name in SPICE", error.Message);
            Assert.Equal(before, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(target.FullName)));
            AssertNothingPending();
        }
        finally
        {
            if (withGm)
            {
                SpiceAPI.Instance.UnloadKernels(gmKernel);
            }

            dir.Delete(true);
        }
    }

    // ELARA (507), named by SPICE, without BODY507_GM: the native writer reads the centre and throws SPICE's message, the same
    // on every run (#714), and writes nothing
    [Fact]
    void WriteEphemeris_AboutACentreWithoutGm_ThrowsSpicesMessage()
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(507));
        var dir = Directory.CreateTempSubdirectory("ioa714we507");
        var target = new FileInfo(Path.Combine(dir.FullName, "elara.bsp"));
        try
        {
            var error = Assert.Throws<SpiceException>(() =>
                SpiceAPI.Instance.WriteEphemeris(target, -1999719, StatesAbout(new IO.Astrodynamics.Body.CelestialBody(507))));
            Assert.Equal(
                "An error occurred while writing ephemeris: CelestialItem id507 can't be read: The variable BODY507_GM could not be found in the kernel pool.",
                error.Message);
            Assert.False(File.Exists(target.FullName));
            AssertNothingPending();
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
