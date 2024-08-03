using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Time;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using FuelTank = IO.Astrodynamics.Body.Spacecraft.FuelTank;
using Launch = IO.Astrodynamics.Maneuver.Launch;
using NadirAttitude = IO.Astrodynamics.Maneuver.NadirAttitude;
using Payload = IO.Astrodynamics.Body.Spacecraft.Payload;
using Planetodetic = IO.Astrodynamics.Coordinates.Planetodetic;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Scenario = IO.Astrodynamics.Mission.Scenario;
using Site = IO.Astrodynamics.Surface.Site;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.Time.Window;

namespace IO.Astrodynamics.Tests;


public class APITest
{
    public APITest()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void CheckVersion()
    {
        Assert.Equal("CSPICE_N0067", API.Instance.GetSpiceVersion());
    }

    [Fact]
    public void ExecuteLaunchScenario()
    {
        //Load solar system kernels
        var start = DateTime.Parse("2021-03-02 00:00:00.000000").ToTDB();
        var end = DateTime.Parse("2021-03-05 00:00:00.000000").ToTDB();

        Window window = new Window(start, end);

        //Define launch site
        LaunchSite launchSite = new LaunchSite(399303, "S3", TestHelpers.EarthAtJ2000,
            new Planetodetic(-81.0 * IO.Astrodynamics.Constants.Deg2Rad, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0));

        //Define the targeted parking orbit
        StateVector parkingOrbit = new StateVector(
            new Vector3(5056554.1874925727, 4395595.4942363985, 0.0),
            new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494), TestHelpers.EarthAtJ2000,
            start, Frames.Frame.ICRF);

        //Create launch object
        Launch launch = new Launch(launchSite, launchSite, parkingOrbit, IO.Astrodynamics.Constants.CivilTwilight,
            true);

        //Find launch windows
        var res = API.Instance.FindLaunchWindows(launch, window, Constants.OutputPath).ToArray();

        //Read results
        Assert.Equal(3, res.Count());
        Assert.Equal(
            new Window(DateTime.Parse("2021-03-02 23:12:54.4214382", CultureInfo.InvariantCulture).ToTDB(),
                DateTime.Parse("2021-03-02 23:12:54.4214382", CultureInfo.InvariantCulture).ToTDB()), res.ElementAt(0).Window);
        Assert.Equal(
            new Window(DateTime.Parse("2021-03-03 23:08:58.2431654", CultureInfo.InvariantCulture).ToTDB(),
                DateTime.Parse("2021-03-03 23:08:58.2431654", CultureInfo.InvariantCulture).ToTDB()), res.ElementAt(1).Window);
        Assert.Equal(
            new Window(DateTime.Parse("2021-03-04 23:05:01.7235513", CultureInfo.InvariantCulture).ToTDB(),
                DateTime.Parse("2021-03-04 23:05:01.7235513", CultureInfo.InvariantCulture).ToTDB()), res.ElementAt(2).Window);
        Assert.Equal(47.00618445347153, res.ElementAt(0).InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
        Assert.Equal(45.125545665662884, res.ElementAt(0).NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
        Assert.Equal(8794.33812148836, res.ElementAt(0).InertialInsertionVelocity, 9);
        Assert.Equal(8499.725885450212, res.ElementAt(0).NonInertialInsertionVelocity, 9);
    }

    [Fact]
    public void FindWindowsOnDistanceConstraintProxy()
    {
        //Find time windows when the moon will be 400000 km away from the Earth
        var res = API.Instance.FindWindowsOnDistanceConstraint(
            new Window(DateTimeExtension.CreateTDB(220881665.18391809),
                DateTimeExtension.CreateTDB(228657665.18565452)),
            TestHelpers.EarthAtJ2000, TestHelpers.MoonAtJ2000, RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0));
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(4, windows.Count());
        Assert.Equal("2007-01-08T00:11:07.6285910 (TDB)", windows.ElementAt(0).StartDate.ToFormattedString());
        Assert.Equal("2007-01-13T06:37:47.9481440 (TDB)", windows.ElementAt(0).EndDate.ToFormattedString());
        Assert.Equal("2007-03-29T22:53:58.1518963 (TDB)", windows.ElementAt(3).StartDate.ToFormattedString());
        Assert.Equal("2007-04-01T00:01:05.1856544 (TDB)", windows.ElementAt(3).EndDate.ToFormattedString());
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnDistanceConstraint(new Window(
                DateTimeExtension.CreateTDB(220881665.18391809),
                DateTimeExtension.CreateTDB(228657665.18565452)), null, TestHelpers.MoonAtJ2000,
            RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0)));
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnDistanceConstraint(new Window(
                DateTimeExtension.CreateTDB(220881665.18391809),
                DateTimeExtension.CreateTDB(228657665.18565452)), TestHelpers.EarthAtJ2000, null,
            RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0)));
    }

    [Fact]
    public void FindWindowsOnOccultationConstraint()
    {
        //Find time windows when the Sun will be occulted by the moon
        var res = API.Instance.FindWindowsOnOccultationConstraint(
            new Window(DateTimeExtension.CreateTDB(61473664.183390938),
                DateTimeExtension.CreateTDB(61646464.183445148)), TestHelpers.EarthAtJ2000, TestHelpers.Sun,
            ShapeType.Ellipsoid, TestHelpers.MoonAtJ2000, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0));
        var windows = res as Window[] ?? res.ToArray();
        Assert.Single(windows);
        Assert.Equal("2001-12-14T20:10:15.4105881 (TDB)", windows[0].StartDate.ToFormattedString());
        Assert.Equal("2001-12-14T21:35:49.1005208 (TDB)", windows[0].EndDate.ToFormattedString());
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnOccultationConstraint(
            new Window(DateTimeExtension.CreateTDB(61473664.183390938),
                DateTimeExtension.CreateTDB(61646464.183445148)), null, TestHelpers.Sun,
            ShapeType.Ellipsoid, TestHelpers.MoonAtJ2000, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0)));
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnOccultationConstraint(
            new Window(DateTimeExtension.CreateTDB(61473664.183390938),
                DateTimeExtension.CreateTDB(61646464.183445148)), TestHelpers.EarthAtJ2000, null,
            ShapeType.Ellipsoid, TestHelpers.MoonAtJ2000, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0)));
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnOccultationConstraint(
            new Window(DateTimeExtension.CreateTDB(61473664.183390938),
                DateTimeExtension.CreateTDB(61646464.183445148)), TestHelpers.EarthAtJ2000, TestHelpers.Sun,
            ShapeType.Ellipsoid, null, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0)));
    }

    [Fact]
    public void FindWindowsOnCoordinateConstraint()
    {
        Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad,
                35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad, 0.107));
        //Find time windows when the moon will be above the horizon relative to Deep Space Station 13
        var res = API.Instance.FindWindowsOnCoordinateConstraint(
            new Window(DateTimeExtension.CreateTDB(730036800.0), DateTimeExtension.CreateTDB(730123200)), site,
            TestHelpers.MoonAtJ2000, site.Frame, CoordinateSystem.Latitudinal, Coordinate.Latitude,
            RelationnalOperator.Greater,
            0.0, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0));

        var windows = res as Window[] ?? res.ToArray();
        Assert.Single(windows);
        Assert.Equal("2023-02-19T14:33:08.9180986 (TDB)", windows[0].StartDate.ToFormattedString());
        Assert.Equal("2023-02-20T00:00:00.0000000 (TDB)", windows[0].EndDate.ToFormattedString());
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnCoordinateConstraint(
            new Window(DateTimeExtension.CreateTDB(730036800.0), DateTimeExtension.CreateTDB(730123200)), null,
            TestHelpers.MoonAtJ2000, site.Frame, CoordinateSystem.Latitudinal, Coordinate.Latitude,
            RelationnalOperator.Greater, 0.0, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0)));
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnCoordinateConstraint(
            new Window(DateTimeExtension.CreateTDB(730036800.0), DateTimeExtension.CreateTDB(730123200)), site,
            null, site.Frame, CoordinateSystem.Latitudinal, Coordinate.Latitude,
            RelationnalOperator.Greater, 0.0, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0)));
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnCoordinateConstraint(
            new Window(DateTimeExtension.CreateTDB(730036800.0), DateTimeExtension.CreateTDB(730123200)), site,
            TestHelpers.MoonAtJ2000, null, CoordinateSystem.Latitudinal, Coordinate.Latitude,
            RelationnalOperator.Greater, 0.0, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0)));
    }

    [Fact]
    public void FindWindowsOnIlluminationConstraint()
    {
        //Find time windows when the planetodetic point is illuminated by the sun (Official twilight 0.8° bellow horizon)
        var res = API.Instance.FindWindowsOnIlluminationConstraint(
            new Window(DateTimeExtension.CreateTDB(674524800), DateTimeExtension.CreateTDB(674611200)),
            TestHelpers.Sun, TestHelpers.EarthAtJ2000, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), TestHelpers.Sun);
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(2, windows.Count());
        Assert.Equal("2021-05-17T12:00:00.0000000 (TDB)", windows[0].StartDate.ToFormattedString());
        Assert.Equal("2021-05-17T19:35:24.9088348 (TDB)", windows[0].EndDate.ToFormattedString());
        Assert.Equal("2021-05-18T04:18:32.4437509 (TDB)", windows[1].StartDate.ToFormattedString());
        Assert.Equal("2021-05-18T12:00:00.0000000 (TDB)", windows[1].EndDate.ToFormattedString());
        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnIlluminationConstraint(
            new Window(DateTimeExtension.CreateTDB(674524800), DateTimeExtension.CreateTDB(674611200)),
            null, TestHelpers.EarthAtJ2000, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), TestHelpers.Sun));

        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnIlluminationConstraint(
            new Window(DateTimeExtension.CreateTDB(674524800), DateTimeExtension.CreateTDB(674611200)),
            TestHelpers.Sun, null, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), TestHelpers.Sun));

        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnIlluminationConstraint(
            new Window(DateTimeExtension.CreateTDB(674524800), DateTimeExtension.CreateTDB(674611200)),
            TestHelpers.Sun, TestHelpers.EarthAtJ2000, null,
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), TestHelpers.Sun));

        Assert.Throws<ArgumentNullException>(() => API.Instance.FindWindowsOnIlluminationConstraint(
            new Window(DateTimeExtension.CreateTDB(674524800), DateTimeExtension.CreateTDB(674611200)),
            TestHelpers.Sun, TestHelpers.EarthAtJ2000, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
            TimeSpan.FromHours(4.5), null));
    }

    [Fact]
    public void ReadEphemeris()
    {
        var searchWindow = new Window(DateTimeExtension.CreateTDB(0.0), DateTimeExtension.CreateTDB(100.0));
        var res = API.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000, TestHelpers.MoonAtJ2000,
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
        Assert.Equal(0.0, stateVectors[0].Epoch.SecondsFromJ2000TDB());

        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadEphemeris(searchWindow, null,
            TestHelpers.MoonAtJ2000,
            Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0)).Select(x => x.ToStateVector()));
        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000,
            null,
            Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0)).Select(x => x.ToStateVector()));
        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000,
            TestHelpers.MoonAtJ2000,
            null, Aberration.LT, TimeSpan.FromSeconds(10.0)).Select(x => x.ToStateVector()));
        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadEphemeris(searchWindow.StartDate, null,
            TestHelpers.MoonAtJ2000,
            Frames.Frame.ICRF, Aberration.LT));
        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadEphemeris(searchWindow.StartDate,
            TestHelpers.EarthAtJ2000, null,
            Frames.Frame.ICRF, Aberration.LT));
        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadEphemeris(searchWindow.StartDate,
            TestHelpers.EarthAtJ2000, TestHelpers.MoonAtJ2000,
            null, Aberration.LT));
    }

    [Fact]
    public void ReadLongEphemeris()
    {
        var searchWindow = new Window(DateTimeExtension.CreateTDB(0.0), DateTimeExtension.CreateTDB(15000.0));
        var res = API.Instance.ReadEphemeris(searchWindow, TestHelpers.EarthAtJ2000, TestHelpers.MoonAtJ2000,
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
        Assert.Equal(0.0, stateVectors[0].Epoch.SecondsFromJ2000TDB());
    }

    [Fact]
    public async Task ReadOrientation()
    {
        DateTime start = DateTimeExtension.CreateTDB(662778000.0);
        DateTime end = start.AddSeconds(60.0);
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

        spacecraft.SetStandbyManeuver(new NadirAttitude(TestHelpers.EarthAtJ2000, DateTime.MinValue, TimeSpan.Zero,
            spacecraft.Engines.First()));

        scenario.AddSpacecraft(spacecraft);

        //Execute scenario
        var root = Constants.OutputPath.CreateSubdirectory(scenario.Mission.Name).CreateSubdirectory(scenario.Name);
        await scenario.SimulateAsync(root, false, false, TimeSpan.FromSeconds(1.0));

        //Read spacecraft orientation
        var res = API.Instance.ReadOrientation(window, spacecraft, TimeSpan.FromSeconds(10.0), Frames.Frame.ICRF,
            TimeSpan.FromSeconds(10.0)).ToArray();

        //Read results
        Assert.Equal(0.70710678118654757, res.ElementAt(0).Rotation.W);
        Assert.Equal(0.0, res.ElementAt(0).Rotation.VectorPart.X, 6);
        Assert.Equal(0.0, res.ElementAt(0).Rotation.VectorPart.Y, 6);
        Assert.Equal(0.70710678118654746, res.ElementAt(0).Rotation.VectorPart.Z);
        Assert.Equal(0.0, res.ElementAt(0).AngularVelocity.X, 6);
        Assert.Equal(0.0, res.ElementAt(0).AngularVelocity.Y, 6);
        Assert.Equal(0.0, res.ElementAt(0).AngularVelocity.Z, 6);
        Assert.Equal(window.StartDate, res.ElementAt(0).Epoch);
        Assert.Equal(Frames.Frame.ICRF, res.ElementAt(0).ReferenceFrame);

        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadOrientation(window, null,
            TimeSpan.FromSeconds(10.0), Frames.Frame.ICRF,
            TimeSpan.FromSeconds(10.0)).ToArray());
        Assert.Throws<ArgumentNullException>(() => API.Instance.ReadOrientation(window, spacecraft,
            TimeSpan.FromSeconds(10.0), null,
            TimeSpan.FromSeconds(10.0)).ToArray());
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
            DateTimeExtension.CreateTDB(0.0), Frames.Frame.ICRF));

        var sv = new StateVector[size];
        for (int i = 0; i < size; ++i)
        {
            sv[i] = new StateVector(new Vector3(6800 + i, i, i), new Vector3(i, 8.0 + i * 0.001, i),
                TestHelpers.EarthAtJ2000,
                DateTimeExtension.CreateTDB(i), Frames.Frame.ICRF);
        }

        //Write ephemeris file
        FileInfo file = new FileInfo("EphemerisTestFile.spk");

        Assert.Throws<ArgumentNullException>(() => API.Instance.WriteEphemeris(null, spacecraft, sv));
        Assert.Throws<ArgumentNullException>(() => API.Instance.WriteEphemeris(file, null, sv));
        Assert.Throws<ArgumentNullException>(() => API.Instance.WriteEphemeris(file, spacecraft, null));
        API.Instance.WriteEphemeris(file, spacecraft, sv);

        //Load ephemeris file
        API.Instance.LoadKernels(file);

        var window = new Window(DateTimeExtension.J2000, DateTimeExtension.J2000.AddSeconds(9.0));
        var stateVectors = API.Instance.ReadEphemeris(window, TestHelpers.EarthAtJ2000, spacecraft, Frames.Frame.ICRF,
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
            Assert.Equal(i, stateVectors[i].Epoch.SecondsFromJ2000TDB());
            Assert.Equal(PlanetsAndMoons.EARTH.NaifId, stateVectors[i].Observer.NaifId);
            Assert.Equal(Frames.Frame.ICRF, stateVectors[i].Frame);
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
            DateTimeExtension.CreateTDB(0.0), Frames.Frame.ICRF));

        var so = new StateOrientation[size];
        for (int i = 0; i < size; ++i)
        {
            so[i] = new StateOrientation(new Quaternion(i, 1 + i * 0.1, 1 + i * 0.2, 1 + i * 0.3), Vector3.Zero, DateTimeExtension.CreateTDB(i), Frames.Frame.ICRF);
        }

        var clockFile = new FileInfo("OrientationClockTest.tsc");
        await clock.WriteAsync(clockFile);
        API.Instance.LoadKernels(clockFile);
        //Write ephemeris file
        FileInfo file = new FileInfo("OrientationTestFile.ck");

        Assert.Throws<ArgumentNullException>(() => API.Instance.WriteOrientation(null, spacecraft, so));
        Assert.Throws<ArgumentNullException>(() => API.Instance.WriteOrientation(file, null, so));
        Assert.Throws<ArgumentNullException>(() => API.Instance.WriteOrientation(file, spacecraft, null));
        API.Instance.WriteOrientation(file, spacecraft, so);

        //Load ephemeris file
        API.Instance.LoadKernels(file);

        var window = new Window(DateTimeExtension.J2000, DateTimeExtension.J2000.AddSeconds(9.0));
        var stateOrientation = API.Instance.ReadOrientation(window, spacecraft, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0))
            .Select(x => x).ToArray();

        Assert.Equal(0.0, stateOrientation[0].Rotation.W, 9);
        Assert.Equal(new Vector3(-0.57735026918962573, -0.57735026918962573, -0.57735026918962573), stateOrientation[0].Rotation.VectorPart);
        Assert.Equal(Vector3.Zero, stateOrientation[0].AngularVelocity);
        Assert.Equal(0.0, stateOrientation[0].Epoch.SecondsFromJ2000TDB());
        Assert.Equal(Frames.Frame.ICRF, stateOrientation[0].ReferenceFrame);

        Assert.Equal(0.78386180166962049, stateOrientation[4].Rotation.W, 9);
        Assert.Equal(new Vector3(0.27435163058436718, 0.35273781075132921, 0.43112399091829129), stateOrientation[4].Rotation.VectorPart);
        Assert.Equal(Vector3.Zero, stateOrientation[4].AngularVelocity);
        Assert.Equal(4.0, stateOrientation[4].Epoch.SecondsFromJ2000TDB());
        Assert.Equal(Frames.Frame.ICRF, stateOrientation[4].ReferenceFrame);

        Assert.Equal(0.87358057364767872, stateOrientation[9].Rotation.W, 9);
        Assert.Equal(new Vector3(0.18442256554784328, 0.27178062291261118, 0.359138680277379), stateOrientation[9].Rotation.VectorPart);
        Assert.Equal(Vector3.Zero, stateOrientation[9].AngularVelocity);
        Assert.Equal(9.0, stateOrientation[9].Epoch.SecondsFromJ2000TDB());
        Assert.Equal(Frames.Frame.ICRF, stateOrientation[9].ReferenceFrame);
    }

    [Fact]
    void GetCelestialBodyInformation()
    {
        //Read celestial celestialItem information from spice kernels
        var res = API.Instance.GetCelestialBodyInfo(TestHelpers.EarthAtJ2000.NaifId);
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
        var res = API.Instance.GetCelestialBodyInfo(TestHelpers.MoonAtJ2000.NaifId);
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
        var res = API.Instance.TransformFrame(Frames.Frame.ICRF, new Frames.Frame(PlanetsAndMoons.EARTH.Frame),
            DateTimeExtension.J2000);
        Assert.Equal(0.76713121189662548, res.Rotation.W);
        Assert.Equal(-1.8618846012434252e-05, res.Rotation.VectorPart.X);
        Assert.Equal(8.468919252183845e-07, res.Rotation.VectorPart.Y);
        Assert.Equal(0.64149022080358797, res.Rotation.VectorPart.Z);
        Assert.Equal(-1.9637714059853662e-09, res.AngularVelocity.X);
        Assert.Equal(-2.0389340573814659e-09, res.AngularVelocity.Y);
        Assert.Equal(7.2921150642488516e-05, res.AngularVelocity.Z);
    }

    [Fact]
    void TransformFrameExceptions()
    {
        //Get the quaternion to transform
        Assert.Throws<ArgumentNullException>(() =>
            API.Instance.TransformFrame(Frames.Frame.ICRF, null, DateTimeExtension.J2000));
        Assert.Throws<ArgumentNullException>(() =>
            API.Instance.TransformFrame(null, new Frames.Frame(PlanetsAndMoons.EARTH.Frame), DateTimeExtension.J2000));
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
        Assert.Throws<ArgumentNullException>(() => API.Instance.LoadKernels(null));
    }

    private static object lockobj = new Object();
    [Fact]
    void UnloadKernels()
    {
        lock (lockobj)
        {
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
            API.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));
            API.Instance.UnloadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
            var kernels = API.Instance.GetLoadedKernels().ToArray();
            Assert.Equal(1, @kernels.Count(x => x.FullName.Contains("scn100")));
        }
    }
    
    [Fact]
    void UnloadKernels2()
    {
        lock (lockobj)
        {
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Clocks/DRAGONFLY32.tsc"));
            
            API.Instance.UnloadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Clocks/DRAGONFLY32.tsc"));
            var kernels = API.Instance.GetLoadedKernels().ToArray();
            Assert.Equal(0, kernels.Count(x => x.FullName.Contains("DRAGONFLY32.tsc")));
        }
    }

    [Fact]
    void LoadKernels()
    {
        lock (lockobj)
        {
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
            API.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));

            var kernels = API.Instance.GetLoadedKernels().ToArray();
            Assert.Equal(1, @kernels.Count(x => x.FullName.Contains("scn100")));
        }
    }

    [Fact]
    void LoadKernels2()
    {
        lock (lockobj)
        {
            API.Instance.LoadKernels(new DirectoryInfo(@"Data/UserDataTest/scn100"));
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Spacecrafts/DRAGONFLY32/Ephemeris/DRAGONFLY32.spk"));
            API.Instance.LoadKernels(new FileInfo(@"Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));

            var kernels = API.Instance.GetLoadedKernels().ToArray();
            Assert.Equal(1, @kernels.Count(x => x.FullName.Contains("scn100")));
        }
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
}