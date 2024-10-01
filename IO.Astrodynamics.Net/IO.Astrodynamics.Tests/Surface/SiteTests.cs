using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Surface
{
    public class SiteTests
    {
        public SiteTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void StateVector()
        {
            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            Assert.Equal(
                new StateVector(new Vector3(-4998233.547629601, 1489959.565879058, 3660827.7952649333),
                    new Vector3(-108.65703014473667, -364.46975244800444, -0.013117011914007314), earth, epoch,
                    Frames.Frame.ICRF), sv, TestHelpers.StateVectorComparer);
        }


        [Fact]
        public void GetHorizontalCoordinates()
        {
            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(117.89632948355492, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(16.790605205512946, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(400552679.30503547, hor.Range, 3);
        }

        [Fact]
        public void GetHorizontalCoordinates2()
        {
            var epoch = new TimeSystem.Time(2000, 1, 5, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(100.01880265664781, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(-23.236014363706865, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(408535095.8513869, hor.Range, 6);
        }

        [Fact]
        public void GetHorizontalCoordinates3()
        {
            var epoch = new TimeSystem.Time(2000, 1, 10, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(41.608271377272878, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(-63.020715259069014, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(401248015.68691534, hor.Range, 6);
        }

        [Fact]
        public void GetHorizontalCoordinates4()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(312.54264512355752, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(-33.61891613059521, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(376638211.11212999, hor.Range, 6);
        }

        [Fact]
        public void GetHorizontalCoordinates5()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);
            var site = new Site(339, "TestSite", TestHelpers.EarthAtJ2000, new Planetodetic(-2.0384478466737517, 0.61517960506340708, 1073.2434632601216));
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(312.54264512355752, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(-33.61891613059521, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(376638212.43274897, hor.Range, 6);
        }

        [Fact]
        public void Create()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            Assert.Equal(13, site.Id);
            Assert.Equal(TestHelpers.EarthAtJ2000.NaifId * 1000 + site.Id, site.NaifId);
            Assert.Equal("DSS-13", site.Name);
            Assert.Equal(TestHelpers.EarthAtJ2000, site.CelestialBody);
        }

        [Fact]
        public void CreateException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Site(-13, "DSS-13", TestHelpers.EarthAtJ2000));
            Assert.Throws<ArgumentException>(() => new Site(13, "", TestHelpers.EarthAtJ2000));
            Assert.Throws<ArgumentNullException>(() => new Site(13, "DSS-13", null));
        }

        [Fact]
        public void GetEphemeris()
        {
            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(new Window(epoch, epoch + TimeSpan.FromDays(1.0)), earth, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromDays(1.0))
                .Select(x => x.ToStateVector()).ToArray();

            Assert.Equal(2, sv.Length);
            Assert.Equal(new StateVector(new Vector3(-4998233.547629601, 1489959.565879058, 3660827.7952649333),
                new Vector3(-108.65703014473667, -364.46975244800444, -0.013117011914007314), earth, epoch, Frames.Frame.ICRF), sv[0], TestHelpers.StateVectorComparer);
            Assert.Equal(new StateVector(new Vector3(-5023123.881856798, 1403764.4769290173, 3660826.4110731576),
                    new Vector3(-102.37160945689291, -366.2848679089919, -0.01299987338117344), earth, epoch + TimeSpan.FromDays(1.0), Frames.Frame.ICRF), sv[1],
                TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void GetPosition()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.InitialOrbitalParameters.ToStateVector().Position;

            Assert.Equal(new Vector3(-2351112.6050050138, -4655530.65549371, 3660912.739397665), sv);
        }

        [Fact]
        public void GetVelocity()
        {
            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Velocity;

            Assert.Equal(new Vector3(-108.65703014473667, -364.46975244800444, -0.013117011914007314), sv, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetAngularSepartion()
        {
            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
            CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var separation = site.AngularSeparation(epoch, moon, TestHelpers.Sun, Aberration.None);

            Assert.Equal(0.98449746814084405, separation, 6);
        }

        [Fact]
        public void FindWindowsOnDistanceConstraint()
        {
            CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnDistanceConstraint(new Window(TimeSystem.Time.CreateTDB(220881665.18391809), TimeSystem.Time.CreateTDB(228657665.18565452)),
                TestHelpers.MoonAtJ2000, RelationnalOperator.Greater, 400000000, Aberration.None, TimeSpan.FromSeconds(3600.0));
            var windows = res as Window[] ?? res.ToArray();
            Assert.Equal(24, windows.Count());
            Assert.Equal(new TimeSystem.Time("2007-01-06T21:21:57.6253242 TDB"), windows.ElementAt(0).StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2007-01-07T01:04:58.0905336 TDB"), windows.ElementAt(0).EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2007-01-07T18:15:42.3323555 TDB"), windows.ElementAt(1).StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2007-01-08T05:43:12.5176513 TDB"), windows.ElementAt(1).EndDate, TestHelpers.TimeComparer);
        }

        [Fact]
        public void FindWindowsOnOccultationConstraint()
        {
            CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnOccultationConstraint(new Window(new TimeSystem.Time("2005-10-03 00:00:00"), new TimeSystem.Time("2005-10-04 00:00:00")),
                TestHelpers.Sun, ShapeType.Ellipsoid, TestHelpers.MoonAtJ2000, ShapeType.Ellipsoid, OccultationType.Partial, Aberration.None, TimeSpan.FromSeconds(360.0));
            var windows = res as Window[] ?? res.ToArray();
            Assert.Single(windows);
            Assert.Equal("2005-10-03T08:37:48.2812500 TDB", windows.ElementAt(0).StartDate.ToString());
            Assert.Equal("2005-10-03T10:15:20.0201799 TDB", windows.ElementAt(0).EndDate.ToString());
        }

        [Fact]
        public void FindWindowsOnCoordinateConstraint()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnCoordinateConstraint(new Window(new TimeSystem.Time("2000-01-01 12:00:00").ToTDB(), new TimeSystem.Time("2000-02-01 12:00:00").ToTDB()),
                TestHelpers.MoonAtJ2000,
                TestHelpers.MoonAtJ2000.Frame, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
                TimeSpan.FromSeconds(60.0));

            var windows = res as Window[] ?? res.ToArray();
            Assert.Single(windows);
            Assert.Equal(new TimeSystem.Time("2000-01-07T17:57:30.0000000 TDB"), windows[0].StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2000-01-21T20:19:21.6632940 TDB"), windows[0].EndDate, TestHelpers.TimeComparer);
        }

        [Fact]
        public void FindWindowsWhenMoonIsVisibleFromSite()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = TestHelpers.MoonAtJ2000.FindWindowsOnCoordinateConstraint(
                new Window(new TimeSystem.Time("2000-01-01 12:00:00").ToTDB(), new TimeSystem.Time("2000-01-02 12:00:00").ToTDB()),
                site,
                site.Frame, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
                TimeSpan.FromSeconds(60.0));

            var windows = res as Window[] ?? res.ToArray();
            Assert.Equal(2, windows.Length);
            Assert.Equal(new TimeSystem.Time("2000-01-01T11:59:00.0000000 TDB"), windows[0].StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2000-01-01T21:32:59.4744873 TDB"), windows[0].EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2000-01-02T11:23:57.1875000 TDB"), windows[1].StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2000-01-02T11:59:59.4719697 TDB"), windows[1].EndDate, TestHelpers.TimeComparer);
        }

        [Fact]
        public void FindWindowsOnIlluminationConstraint()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));

            var res = site.FindWindowsOnIlluminationConstraint(new Window(TimeSystem.Time.CreateTDB(674524800), TimeSystem.Time.CreateTDB(674611200)), TestHelpers.Sun,
                IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.LTS,
                TimeSpan.FromSeconds(3600), TestHelpers.Sun);

            var windows = res as Window[] ?? res.ToArray();
            Assert.Single(windows);
            Assert.Equal(new TimeSystem.Time("2021-05-17T12:51:05.6250000 TDB"), windows[0].StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-05-18T02:55:43.5250283 TDB"), windows[0].EndDate, TestHelpers.TimeComparer);
        }

        [Fact]
        public void Equality()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));

            Site site2 = new Site(14, "DSS-14", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));

            Assert.True(site != site2);
            Assert.False(site == site2);
            Assert.False(site.Equals(site2));
            Assert.False(site.Equals(null));
            Assert.True(site.Equals(site));
            Assert.False(site.Equals((object)site2));
            Assert.False(site.Equals((object)null));
            Assert.True(site.Equals((object)site));
        }

        [Fact]
        public void Mass()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));
            Assert.Equal(0.0, site.Mass);
            Assert.Equal(0.0, site.GM);
        }

        [Fact]
        public void CenterOfMotion()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));
            var res = site.GetCentersOfMotion().ToArray();
            Assert.Equal(3, res.Count());
            Assert.Equal(399, res[0].NaifId);
            Assert.Equal(3, res[1].NaifId);
            Assert.Equal(0, res[2].NaifId);
        }

        [Fact]
        public void WriteEphemeris()
        {
            Window window = new Window(new TimeSystem.Time(2000, 1, 1, 12, 0, 0), new TimeSystem.Time(2000, 1, 2, 12, 0, 0));

            Site site = new Site(339, "S339", TestHelpers.EarthAtJ2000, new Planetodetic(30 * Astrodynamics.Constants.Deg2Rad, 10 * Astrodynamics.Constants.Deg2Rad, 1000.0));
            site.WriteEphemeris(window,new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Sites").CreateSubdirectory(site.Name).FullName,"sitetest339.spk")));
            API.Instance.LoadKernels(new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Sites").CreateSubdirectory(site.Name).FullName, "sitetest339.spk")));
            var orbitalParametersEnumerable = API.Instance.ReadEphemeris(window, site.CelestialBody, site, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1));
            Assert.Equal(new Vector3(4054783.094777815, -4799280.900382741, 1100391.2394741199), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Position,
                TestHelpers.VectorComparer);
            Assert.Equal(new Vector3(349.9668418905813, 295.68160979710234, 0.01769212826071784), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Velocity,
                TestHelpers.VectorComparer);
            Assert.Equal(TimeSystem.Time.J2000TDB, orbitalParametersEnumerable.ElementAt(0).Epoch);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(0).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(0).Observer);

            Assert.Equal(5675531.4457497578, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.X, 6);
            Assert.Equal(2694837.3733771644, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Y, 6);
            Assert.Equal(1100644.5047067825, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Z, 6);
            Assert.Equal(-196.51288777966315, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.X, 6);
            Assert.Equal(413.86844022899953, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Y, 6);
            Assert.Equal(0.0062996708938248122, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Z, 6);
            Assert.Equal(18000.0, orbitalParametersEnumerable.ElementAt(5).Epoch.TimeSpanFromJ2000().TotalSeconds);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(5).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(5).Observer);
        }

        [Fact]
        public async Task WriteFrame()
        {
            Site site = new Site(33, "S33", TestHelpers.EarthAtJ2000, new Planetodetic(30 * Astrodynamics.Constants.Deg2Rad, 10 * Astrodynamics.Constants.Deg2Rad, 1000.0));
            await site.WriteFrameAsync(new FileInfo("sites33.tf"));
            TextReader tr = new StreamReader("sites33.tf");
            var res = await tr.ReadToEndAsync();
            Assert.Equal(
                $"KPL/FK{Environment.NewLine}\\begindata{Environment.NewLine}NAIF_BODY_NAME              += 'S33'{Environment.NewLine}NAIF_BODY_CODE              += 399033{Environment.NewLine}FRAME_S33_TOPO        = 1399033{Environment.NewLine}FRAME_1399033_NAME        = 'S33_TOPO'{Environment.NewLine}FRAME_1399033_CLASS       = 4{Environment.NewLine}FRAME_1399033_CLASS_ID    = 1399033{Environment.NewLine}FRAME_1399033_CENTER      = 399033{Environment.NewLine}OBJECT_399033_FRAME       = 'S33_TOPO'{Environment.NewLine}TKFRAME_1399033_SPEC      = 'ANGLES'{Environment.NewLine}TKFRAME_1399033_RELATIVE  = 'ITRF93'{Environment.NewLine}TKFRAME_1399033_ANGLES    = (-0.5235987755982988,-1.3962634015954636,3.141592653589793116){Environment.NewLine}TKFRAME_1399033_AXES      = (3,2,3){Environment.NewLine}TKFRAME_1399033_UNITS     = 'RADIANS'{Environment.NewLine}\\begintext{Environment.NewLine}",
                res);
        }

        [Fact]
        public void IlluminationIncidence()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationIncidence(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            Assert.Equal(2.1862895154409667, res);
        }

        [Fact]
        public void IlluminationEmission()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationEmission(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            Assert.Equal(2.1862895154409667, res);
        }

        [Fact]
        public void IlluminationPhase()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationPhase(TimeSystem.Time.J2000TDB, TestHelpers.Sun, TestHelpers.Sun, Aberration.None);
            Assert.Equal(0.0, res);
        }
        
        [Fact]
        public void IlluminationIncidenceFromMoon()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationIncidence(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            Assert.Equal(2.1862895154409667, res);
        }

        [Fact]
        public void IlluminationEmissionFromMoon()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationEmission(TimeSystem.Time.J2000TDB, TestHelpers.Moon, Aberration.None);
            Assert.Equal(1.2777449825556335, res);
        }

        [Fact]
        public void IlluminationPhaseFromMoon()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationPhase(TimeSystem.Time.J2000TDB, TestHelpers.Sun, TestHelpers.Moon, Aberration.None);
            Assert.Equal(0.98449746814087213, res);
        }
    }
}