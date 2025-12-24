using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

public class CelestialBodyTests
{
    public CelestialBodyTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

        Assert.Equal(399, earth.NaifId);
        Assert.Equal("EARTH", earth.Name);
        Assert.Equal("ITRF93", earth.Frame.Name);
        Assert.Equal(3.9860043550702262E+14, earth.GM);
        Assert.Equal(5.9721683997875828E+24, earth.Mass);
        Assert.Equal(6356751.9, earth.PolarRadius);
        Assert.Equal(6378136.6, earth.EquatorialRadius);
        Assert.Equal(0.0033528131084554157, earth.Flattening);
        Assert.Equal(1, (earth.InitialOrbitalParameters.Observer as Astrodynamics.Body.CelestialItem)?.Satellites.Count);
    }

    [Fact]
    public void Create2()
    {
        CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ECLIPTIC_J2000, TimeSystem.Time.J2000TDB);

        Assert.Equal(399, earth.NaifId);
        Assert.Equal("EARTH", earth.Name);
        Assert.Equal("ITRF93", earth.Frame.Name);
        Assert.Equal(3.9860043550702262E+14, earth.GM);
        Assert.Equal(5.9721683997875828E+24, earth.Mass);
        Assert.Equal(6356751.9, earth.PolarRadius);
        Assert.Equal(6378136.6, earth.EquatorialRadius);
        Assert.Equal(0.0033528131084554157, earth.Flattening);
        Assert.Equal(1, (earth.InitialOrbitalParameters.Observer as Astrodynamics.Body.CelestialItem)?.Satellites.Count);
    }

    [Fact]
    public void CreateFromNaifObject()
    {
        CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
        Assert.Equal("MOON", moon.Name);
        Assert.Equal(301, moon.NaifId);
        Assert.Equal(0.0, moon.Flattening);
        Assert.Equal(1737400.0, moon.EquatorialRadius);
        Assert.Equal(4902800118457.5488, moon.GM);
        Assert.Equal(1737400.0, moon.PolarRadius);
        Assert.Equal(66482232.21562586, moon.SphereOfInfluence,6);
        Assert.Equal(7.3457892489962231E+22, moon.Mass);
        Assert.NotNull(moon.InitialOrbitalParameters);
        Assert.Equal(3, moon.InitialOrbitalParameters.Observer.NaifId);
        Assert.Equal(TimeSystem.Time.J2000TDB, moon.InitialOrbitalParameters.Epoch);
        Assert.Equal(new Vector3(-288065172.3454155, -271638576.61683005, 35830480.397877164), moon.InitialOrbitalParameters.ToStateVector().Position, TestHelpers.VectorComparer);
        Assert.Equal(new Vector3(635.7121052811876, -722.1021012684569, -11.36665431333672), moon.InitialOrbitalParameters.ToStateVector().Velocity, TestHelpers.VectorComparer);
        Assert.Equal(Frames.Frame.ECLIPTIC_J2000, moon.InitialOrbitalParameters.Frame);
    }

    [Fact]
    public void FindOccultationsEclipse()
    {
        var moon = TestHelpers.MoonAt20011214;
        var earth = TestHelpers.EarthAtJ2000;
        var sun = TestHelpers.Sun;
        var res = sun.FindWindowsOnOccultationConstraint(
            new Window(TimeSystem.Time.CreateTDB(61473664.183390938), TimeSystem.Time.CreateTDB(61646464.183445148)), earth,
            ShapeType.Ellipsoid, moon, ShapeType.Ellipsoid, OccultationType.Any, Aberration.None, TimeSpan.FromMinutes(60.0)).ToArray();
        Assert.Single(res);

        Assert.Equal(new TimeSystem.Time(new DateTime(2001, 12, 14, 20, 10, 50, 413), TimeFrame.TDBFrame), res[0].StartDate, TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new DateTime(2001, 12, 14, 21, 36, 32, 393), TimeFrame.TDBFrame), res[0].EndDate, TestHelpers.TimeComparer);
    }

    [Fact]
    public void FindWindowsOnDistanceConstraint()
    {
        var res = TestHelpers.EarthAtJ2000.FindWindowsOnDistanceConstraint(
            new Window(new TimeSystem.Time("2000-01-01 12:00:00.0000 TDB"), new TimeSystem.Time("2000-04-01 12:00:00.0000 TDB")),
            TestHelpers.MoonAtJ2000, RelationnalOperator.Greater, 400000000, Aberration.None, TimeSpan.FromSeconds(86400.0));
        var windows = res as Window[] ?? res.ToArray();
        Assert.Equal(4, windows.Count());
        Assert.Equal(new TimeSystem.Time("1999-12-31T17:30:03.7353516 TDB"), windows.ElementAt(0).StartDate);
        Assert.Equal(new TimeSystem.Time("2000-01-09T00:53:37.1295723 TDB"), windows.ElementAt(0).EndDate);
        Assert.Equal(new TimeSystem.Time("2000-01-28T22:27:48.1640625 TDB"), windows.ElementAt(1).StartDate);
        Assert.Equal(new TimeSystem.Time("2000-02-04T17:08:52.3502741 TDB"), windows.ElementAt(1).EndDate);
        Assert.Equal(new TimeSystem.Time("2000-02-26T04:15:44.3847657 TDB"), windows.ElementAt(2).StartDate);
        Assert.Equal(new TimeSystem.Time("2000-03-02T17:41:33.0589197 TDB"), windows.ElementAt(2).EndDate);
        Assert.Equal(new TimeSystem.Time("2000-03-25T02:59:30.9960938 TDB"), windows.ElementAt(3).StartDate);
        Assert.Equal(new TimeSystem.Time("2000-03-30T07:01:07.2974489 TDB"), windows.ElementAt(3).EndDate);
    }

    [Fact]
    public void AngularSize()
    {
        var sun = TestHelpers.Sun;
        var earth = TestHelpers.EarthAtJ2000;
        var res = sun.AngularSize(earth.GetEphemeris(TimeSystem.Time.J2000TDB, sun, Frames.Frame.ECLIPTIC_J2000, Aberration.None).ToStateVector().Position.Magnitude());
        Assert.Equal(0.009459, res, 6);
    }

    [Fact]
    public void GetMass()
    {
        var earth = TestHelpers.EarthAtJ2000;
        Assert.Equal(5.9721683997875828E+24, earth.Mass);
    }

    [Fact]
    public void CelestialBodyToString()
    {
        var earth = TestHelpers.EarthAtJ2000;
        var res = earth.ToString();
        Assert.Equal(
            $"                          Type : Planet                          {Environment.NewLine}                    Identifier : 399                             {Environment.NewLine}                          Name : EARTH                           {Environment.NewLine}                     Mass (kg) : 5.972168E+024                   {Environment.NewLine}                   GM (m^3.s^2): 3.986004E+014                   {Environment.NewLine}                   Fixed frame : ITRF93                          {Environment.NewLine}         Equatorial radius (m) : 6.378137E+006                   {Environment.NewLine}              Polar radius (m) : 6.356752E+006                   {Environment.NewLine}                    Flattening : 0.0033528131084554157           {Environment.NewLine}                            J2 : 0.001082616                     {Environment.NewLine}                            J3 : -2.5388099999999996E-06         {Environment.NewLine}                            J4 : -1.65597E-06                    {Environment.NewLine}",
            earth.ToString());
    }

    [Fact]
    public void Equality()
    {
        var earth1 = PlanetsAndMoons.EARTH_BODY;
        var earth2 = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        Assert.Equal(earth1, earth2);
        Assert.NotEqual(earth1, moon);
        Assert.False(earth1 == null);
        Assert.True(earth1.Equals(earth2));
        Assert.True(earth1.Equals(earth1));
        Assert.True(earth1.Equals((object)earth2));
        Assert.True(earth1.Equals((object)earth1));
        Assert.False(earth1.Equals(null));
        Assert.False(earth1.Equals((object)null));
        Assert.False(earth1.Equals(null));
    }

    [Fact]
    public void GetEphemeris()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var res = earth.GetEphemeris(new Window(TimeSystem.Time.J2000TDB, TimeSpan.FromDays(1.0)), TestHelpers.Sun, Frames.Frame.ICRF, Aberration.None,
            TimeSpan.FromDays(1.0)).ToArray();
        Assert.Equal(2, res.Length);
        Assert.Equal(
            new StateVector(new Vector3(-29069076368.64741, 132303142494.37561, 57359794320.98976), new Vector3(-29695.854459557304, -5497.347182651619, -2382.9422283991967),
                TestHelpers.Sun, TimeSystem.Time.J2000TDB + TimeSpan.FromDays(1.0), Frames.Frame.ICRF), res.ElementAt(1).ToStateVector(), TestHelpers.StateVectorComparer);
    }
    
    [Fact]
    public void GetEphemerisUTC()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var res = earth.GetEphemeris(new Window(TimeSystem.Time.J2000UTC, TimeSpan.FromDays(1.0)), TestHelpers.Sun, Frames.Frame.ICRF, Aberration.None,
            TimeSpan.FromDays(1.0)).ToArray();
        Assert.Equal(2, res.Length);
        Assert.Equal(
            new StateVector(new Vector3(-29069076368.647408, 132303142494.37561, 57359794320.98976), new Vector3(-29695.854459557304, -5497.347182651618, -2382.9422283991967),
                TestHelpers.Sun, TimeSystem.Time.J2000UTC + TimeSpan.FromDays(1.0), Frames.Frame.ICRF), res.ElementAt(1).ToStateVector(), TestHelpers.StateVectorComparer);
    }
    
    [Fact]
    public void GetEphemerisLT()
    {
        var earth = PlanetsAndMoons.MOON_BODY;
        var res = earth.GetEphemeris(new Window(TimeSystem.Time.J2000TDB, TimeSpan.FromDays(1.0)), TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.LT,
            TimeSpan.FromDays(1.0)).ToArray();
        Assert.Equal(2, res.Length);
        Assert.Equal(
            new StateVector(new Vector3(-291569264.4915199, -266709187.16285706, -76099155.2442627), new Vector3(643.5274874728093, -666.082425667777, -301.3230984482848),
                TestHelpers.Earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), res.ElementAt(0).ToStateVector(), TestHelpers.StateVectorComparer);
    }

    [Fact]
    public void AngularSeparation()
    {
        var res = TestHelpers.EarthAtJ2000.AngularSeparation(TimeSystem.Time.J2000TDB, TestHelpers.MoonAtJ2000, TestHelpers.Sun, Aberration.None);
        Assert.Equal(0.9984998794278185, res,6);
    }

    [Fact]
    public void AngularSeparationFromOrbitalParameters()
    {
        var res = TestHelpers.Sun.AngularSeparation(TimeSystem.Time.J2000TDB, TestHelpers.MoonAtJ2000, TestHelpers.EarthAtJ2000.InitialOrbitalParameters, Aberration.None);
        Assert.Equal(0.9984998794278185, res, 12);
    }

    [Fact]
    public void SubObserverPoint()
    {
        var moon = TestHelpers.MoonAtJ2000;
        var res = moon.SubObserverPoint(TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Aberration.None);
        Assert.Equal(-10.898058559403337, res.Latitude * Constants.RAD_DEG,6);
        Assert.Equal(-57.746601395904747, res.Longitude * Constants.RAD_DEG,6);
        Assert.Equal(402448639.88732123, res.Radius,6);
    }

    [Fact]
    public void RadiusFromLatitude()
    {
        var earth = TestHelpers.EarthAtJ2000;
        var res1 = earth.RadiusFromPlanetocentricLatitude(0.0);
        var res2 = earth.RadiusFromPlanetocentricLatitude(Astrodynamics.Constants.PI2);
        var res3 = earth.RadiusFromPlanetocentricLatitude(-Astrodynamics.Constants.PI2);
        Assert.Equal(earth.EquatorialRadius, res1, 6);
        Assert.Equal(earth.PolarRadius, res2, 6);
        Assert.Equal(earth.PolarRadius, res3, 6);
    }

    [Fact]
    public void GetOrientation()
    {
        var orientation = TestHelpers.EarthAtJ2000.GetOrientation(Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
        Assert.Equal(new Vector3(-1.9637713280171745E-09, -2.0389347198634933E-09, 7.29211506433339E-05), orientation.AngularVelocity,TestHelpers.VectorComparer);
        Assert.Equal(new Quaternion(0.7671312120778745, -1.8618836714990174E-05, 8.468840548096465E-07, 0.6414902205868405), orientation.Rotation,TestHelpers.QuaternionComparer);
        Assert.Equal(TimeSystem.Time.J2000TDB, orientation.Epoch);
        Assert.Equal(Frames.Frame.ICRF, orientation.ReferenceFrame);
    }

    [Fact]
    public void EarthSideralRotationPerdiod()
    {
        var duration = TestHelpers.EarthAtJ2000.SideralRotationPeriod(TimeSystem.Time.J2000TDB);
        Assert.Equal(TimeSpan.FromTicks(861640998101), duration);
    }

    [Fact]
    public void MoonSideralRotationPerdiod()
    {
        var duration = TestHelpers.MoonAtJ2000.SideralRotationPeriod(TimeSystem.Time.J2000TDB);
        Assert.Equal(TimeSpan.FromTicks(23603596749416), duration);
    }

    [Fact]
    public void GeosynchronousOrbit()
    {
        var orbit = TestHelpers.EarthAtJ2000.GeosynchronousOrbit(0.0, 0.0, new TimeSystem.Time(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame));
        Assert.Equal(42164171.95991531, orbit.ToStateVector().Position.Magnitude(),3);
        Assert.Equal(3074.6599900324436, orbit.ToStateVector().Velocity.Magnitude(),3);
        Assert.Equal(Frames.Frame.ICRF, orbit.Frame);
    }

    [Fact]
    public void GeosynchronousOrbit2()
    {
        var orbit = TestHelpers.EarthAtJ2000.GeosynchronousOrbit(1.0, 1.0, new TimeSystem.Time(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame));
        Assert.Equal(42164171.95991531, orbit.ToStateVector().Position.Magnitude(), 3);
        Assert.Equal(3074.6599898500763, orbit.ToStateVector().Velocity.Magnitude(), 3);
        Assert.Equal(Frames.Frame.ICRF, orbit.Frame);
        Assert.Equal(42164171.95991531, orbit.SemiMajorAxis(), 3);
        Assert.Equal(0.0, orbit.Eccentricity());
        Assert.Equal(1.0, orbit.Inclination(), 2);
        Assert.Equal(1.1804318466570587, orbit.AscendingNode(), 2);
        Assert.Equal(1.569, orbit.ArgumentOfPeriapsis(), 2);
        Assert.Equal(0.0, orbit.MeanAnomaly(), 2);
        Assert.Equal(new Vector3(-20992029.305278853, 8679264.324252827, 35522140.60951446), orbit.ToStateVector().Position, TestHelpers.VectorComparer);
        Assert.Equal(new Vector3(-1171.3783810266016, -2842.7805399479103, 2.354430257176734), orbit.ToStateVector().Velocity, TestHelpers.VectorComparer);
    }


    [Fact]
    public void TrueSolarDayJan()
    {
        var res1 = TestHelpers.Earth.TrueSolarDay(new TimeSystem.Time(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame));
        Assert.Equal(86407.306035452566, res1.TotalSeconds, 3);
    }

    [Fact]
    public void TrueSolarDayJMar()
    {
        var res1 = TestHelpers.Earth.TrueSolarDay(new TimeSystem.Time(new DateTime(2021, 3, 26, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame));
        Assert.Equal(86400.359507999994, res1.TotalSeconds, 3);
    }

    [Fact]
    public void TrueSolarDayJul()
    {
        var res1 = TestHelpers.Earth.TrueSolarDay(new TimeSystem.Time(new DateTime(2021, 7, 25, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame));
        Assert.Equal(86392.011764653842, res1.TotalSeconds, 3);
    }

    [Fact]
    public void TrueSolarDayDec()
    {
        var res1 = TestHelpers.Earth.TrueSolarDay(new TimeSystem.Time(new DateTime(2021, 12, 22, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame));
        Assert.Equal(86407.114275442393, res1.TotalSeconds, 3);
    }

    [Fact]
    public void HelioSynchronousOrbit()
    {
        var epoch = new TimeSystem.Time(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame);
        var res = TestHelpers.Earth.HelioSynchronousOrbit(7080636.3, 0.0001724, epoch);
        Assert.Equal(7080636.3, res.A);
        Assert.Equal(0.0001724, res.E, 6);
        Assert.Equal(98.208156353447507, res.I * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(11.457000000000001, res.RAAN * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(270.0, res.AOP * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(270.0, res.TrueAnomaly() * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(270.01999999999998, res.MeanAnomaly() * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(epoch, res.Epoch);
    }

    [Fact]
    public void PhaseHelioSynchronousOrbit()
    {
        var epoch = new TimeSystem.Time(new DateTime(2021, 11, 22, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame);
        var res = TestHelpers.Earth.HelioSynchronousOrbit(0.0001724, epoch, 14);
        Assert.Equal(7272221.8764740741, res.A, 3);
        Assert.Equal(0.0001724, res.E, 6);
        Assert.Equal(99.018, res.I * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(327.43000000000001, res.RAAN * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(270.0, res.AOP * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(270.0, res.TrueAnomaly() * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(270.01999999999998, res.MeanAnomaly() * Astrodynamics.Constants.Rad2Deg, 3);
        Assert.Equal(epoch, res.Epoch);
    }

    [Fact]
    public void GeopotentialModelReader()
    {
        GeopotentialModelReader geopotentialModelReader =
            new GeopotentialModelReader(new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Assert.Equal(new GeopotentialCoefficient(4, 1, -0.536157389388867E-06, -0.473567346518086E-06, 0.4568074333E-11, 0.4684043490E-11),
            geopotentialModelReader.ReadCoefficient(4, 1));
    }

    [Fact]
    public void GeopotentialModelReaderException()
    {
        GeopotentialModelReader geopotentialModelReader =
            new GeopotentialModelReader(new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Assert.Throws<ArgumentException>(() => geopotentialModelReader.ReadCoefficient(4, 5));
    }

    [Fact]
    public void IsOccultedNone()
    {
        Assert.Equal(OccultationType.None, CelestialItem.IsOcculted(3.0, 2.0, 4.0));
        Assert.Equal(OccultationType.None, CelestialItem.IsOcculted(3.0, 4.0, 2.0));
    }

    [Fact]
    public void IsOccultedPartial()
    {
        Assert.Equal(OccultationType.Partial, CelestialItem.IsOcculted(2.0, 2.0, 4.0));
        Assert.Equal(OccultationType.Partial, CelestialItem.IsOcculted(2.0, 4.0, 2.0));
    }

    [Fact]
    public void IsOccultedFull()
    {
        Assert.Equal(OccultationType.Full, CelestialItem.IsOcculted(1.0, 2.0, 4.0));
    }

    [Fact]
    public void IsOccultedAnnular()
    {
        Assert.Equal(OccultationType.Annular, CelestialItem.IsOcculted(1.0, 4.0, 2.0));
    }

    [Fact]
    public void EarthAirTemperature()
    {
        Assert.Equal(15.04, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirTemperature(0.0), 9);
        Assert.Equal(-56.46, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirTemperature(12000.0), 9);
        Assert.Equal(-41.51, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirTemperature(30000.0), 9);
    }

    [Fact]
    public void EarthAirPressure()
    {
        Assert.Equal(101.49344845410143, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirPressure(0.0));
        Assert.Equal(19.417211275909512, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirPressure(12000.0));
        Assert.Equal(1.1583293266743089, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirPressure(30000.0));
    }

    [Fact]
    public void EarthAirDensity()
    {
        Assert.Equal(1.2275199342947976, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirDensity(0.0));
        Assert.Equal(0.3123326876175731, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirDensity(12000.0));
        Assert.Equal(0.017429621153374267, TestHelpers.EarthWithAtmAndGeoAtJ2000.GetAirDensity(30000.0));
    }

    [Fact]
    public void MarsAirTemperature()
    {
        MarsAtmosphericModel model = new MarsAtmosphericModel();
        Assert.Equal(-31.00, model.GetTemperature(0.0), 9);
        Assert.Equal(-90.00, model.GetTemperature(30000.0), 9);
    }

    [Fact]
    public void MarsAirPressure()
    {
        MarsAtmosphericModel model = new MarsAtmosphericModel();
        Assert.Equal(0.699, model.GetPressure(0.0));
        Assert.Equal(0.046976653405085077, model.GetPressure(30000.0));
    }

    [Fact]
    public void MarsAirDensity()
    {
        MarsAtmosphericModel model = new MarsAtmosphericModel();
        Assert.Equal(0.015026759563140498, model.GetDensity(0.0));
        Assert.Equal(0.0013352044980975983, model.GetDensity(30000.0));
    }
    
    [Fact]
    public void FindBarycenterOfMotionId_ReturnsZero_ForSun()
    {
        var celestialItem = TestHelpers.Sun;
        Assert.Equal(0, celestialItem.BarycenterOfMotionId);
    }

    [Fact]
    public void FindBarycenterOfMotionId_ReturnsZero_ForBarycenter()
    {
        var celestialItem = new Barycenter(0);
        Assert.Equal(0, celestialItem.BarycenterOfMotionId);
    }

    [Fact]
    public void FindBarycenterOfMotionId_ReturnsZero_ForAsteroid()
    {
        var celestialItem = new CelestialBody(2000001);
        Assert.Equal(0, celestialItem.BarycenterOfMotionId);
    }

    [Fact]
    public void FindBarycenterOfMotionId_ReturnsCorrectValue_ForPlanet()
    {
        var celestialItem = TestHelpers.EarthAtJ2000;
        Assert.Equal(3, celestialItem.BarycenterOfMotionId);
    }

    [Fact]
    public void FindBarycenterOfMotionId_ReturnsCorrectValue_ForMoon()
    {
        var celestialItem = TestHelpers.MoonAtJ2000;
        Assert.Equal(3, celestialItem.BarycenterOfMotionId);
    }

    [Fact]
    public void FindBarycenterOfMotionId_ReturnsCorrectValue_ForLagrangePoint391()
    {
        var celestialItem = new LagrangePoint(LagrangePoints.L1);
        Assert.Equal(3, celestialItem.BarycenterOfMotionId);
    }

    [Fact]
    public void FindBarycenterOfMotionId_ReturnsZero_ForLagrangePoint394()
    {
        var celestialItem = new LagrangePoint(LagrangePoints.L4);
        Assert.Equal(3, celestialItem.BarycenterOfMotionId);
    }


[Fact]
    public void FindCenterOfMotionId_ReturnsZero_ForBarycenter()
    {
        var celestialItem = new Barycenter(0);
        Assert.Equal(0, celestialItem.CenterOfMotionId);
    }

    [Fact]
    public void FindCenterOfMotionId_ReturnsTen_ForSun()
    {
        var celestialItem = TestHelpers.Sun;
        Assert.Equal(10, celestialItem.CenterOfMotionId);
    }

    [Fact]
    public void FindCenterOfMotionId_ReturnsTen_ForPlanet()
    {
        var celestialItem = TestHelpers.EarthAtJ2000;
        Assert.Equal(10, celestialItem.CenterOfMotionId);
    }

    [Fact]
    public void FindCenterOfMotionId_ReturnsTen_ForAsteroid()
    {
        var celestialItem = new CelestialBody(2000001);
        Assert.Equal(10, celestialItem.CenterOfMotionId);
    }

    [Fact]
    public void FindCenterOfMotionId_ReturnsCorrectValue_ForMoon()
    {
        var celestialItem = TestHelpers.MoonAtJ2000;
        Assert.Equal(399, celestialItem.CenterOfMotionId);
    }

    [Fact]
    public void FindCenterOfMotionId_ReturnsCorrectValue_ForLagrangePoint391()
    {
        var celestialItem = new LagrangePoint(LagrangePoints.L1);
        Assert.Equal(10, celestialItem.CenterOfMotionId);
    }

    [Fact]
    public void FindCenterOfMotionId_ReturnsTen_ForLagrangePoint394()
    {
        var celestialItem = new LagrangePoint(LagrangePoints.L4);
        Assert.Equal(10, celestialItem.CenterOfMotionId);
    }
    
    [Fact]
    public void Create_ReturnsBarycenter_ForNaifIdLessThan10()
    {
        var result = CelestialItem.Create(5);
        Assert.IsType<Barycenter>(result);
    }

    [Fact]
    public void Create_ReturnsLagrangePoint_ForNaifIdBetweenL1AndL5()
    {
        var result = CelestialItem.Create(391);
        Assert.IsType<LagrangePoint>(result);
    }

    [Fact]
    public void Create_ReturnsCelestialBody_ForNaifIdGreaterThan10AndNotLagrangePoint()
    {
        var result = CelestialItem.Create(199);
        Assert.IsType<CelestialBody>(result);
    }

    [Fact]
    public void Create_ReturnsLagrangePoint_WithCorrectName()
    {
        var result = (LagrangePoint)CelestialItem.Create(392);
        Assert.Equal("L2", result.Name);
    }
}