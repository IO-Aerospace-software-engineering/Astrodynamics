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
using IO.Astrodynamics.Time;
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
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0);
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            Assert.Equal(
                new StateVector(new Vector3(-4998235.411348605, 1489960.124482482, 3660829.160736662),
                    new Vector3(-108.65008748008586, -364.4771217160694, -0.0006383925320366459), earth, epoch,
                    Frames.Frame.ICRF), sv, TestHelpers.StateVectorComparer);
        }


        [Fact]
        public void GetHorizontalCoordinates()
        {
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(117.89631806108865, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(16.79061677201462, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(400552679.30743355, hor.Range, 3);
        }

        [Fact]
        public void GetHorizontalCoordinates2()
        {
            var epoch = new DateTime(2000, 1, 5, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(100.01881371927551, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(-23.23601238553318, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(408535095.85180473, hor.Range, 6);
        }

        [Fact]
        public void GetHorizontalCoordinates3()
        {
            var epoch = new DateTime(2000, 1, 10, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(41.60830471508871, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(-63.02074114148227, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(401248015.68680006, hor.Range, 6);
        }

        [Fact]
        public void GetHorizontalCoordinates4()
        {
            var epoch = new DateTime(2000, 1, 15, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(312.5426255803723, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg);
            Assert.Equal(-33.618934779034475, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg);
            Assert.Equal(376638211.1106281, hor.Range);
        }

        [Fact]
        public void Create()
        {
            var epoch = new DateTime(2000, 1, 15, 12, 0, 0);

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
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0);
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(new Window(epoch, epoch + TimeSpan.FromDays(1.0)), earth, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromDays(1.0))
                .Select(x => x.ToStateVector()).ToArray();

            Assert.Equal(2, sv.Length);
            Assert.Equal(new StateVector(new Vector3(-4998235.411348605, 1489960.124482482, 3660829.160736662),
                new Vector3(-108.65008748008586, -364.4771217160694, -0.0006383925320366459), earth, epoch, Frames.Frame.ICRF), sv[0], TestHelpers.StateVectorComparer);
            Assert.Equal(new StateVector(new Vector3(-5023125.754942155, 1403765.003415661, 3660827.77649115),
                    new Vector3(-102.36463933210791, -366.2921524677261, -0.0006423586774293476), earth, epoch + TimeSpan.FromDays(1.0), Frames.Frame.ICRF), sv[1],
                TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void GetPosition()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.InitialOrbitalParameters.ToStateVector().Position;

            Assert.Equal(new Vector3(-2351112.6050123204, -4655530.655495551, 3660912.7393973987), sv);
        }

        [Fact]
        public void GetVelocity()
        {
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0);
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Velocity;

            Assert.Equal(new Vector3(-108.65008748008586, -364.4771217160694, -0.0006383925320366459), sv, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetAngularSepartion()
        {
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0);
            CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var separation = site.AngularSeparation(epoch, moon, TestHelpers.Sun, Aberration.None);

            Assert.Equal(0.9844974681377541, separation);
        }

        [Fact]
        public void FindWindowsOnDistanceConstraint()
        {
            CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnDistanceConstraint(new Window(DateTimeExtension.CreateTDB(220881665.18391809), DateTimeExtension.CreateTDB(228657665.18565452)),
                TestHelpers.MoonAtJ2000, RelationnalOperator.Greater, 400000000, Aberration.None, TimeSpan.FromSeconds(86400.0));
            var windows = res as Window[] ?? res.ToArray();
            Assert.Equal(2, windows.Count());
            Assert.Equal("2007-02-03T17:02:04.4461595 (TDB)", windows.ElementAt(0).StartDate.ToFormattedString());
            Assert.Equal("2007-02-09T10:31:41.4309832 (TDB)", windows.ElementAt(0).EndDate.ToFormattedString());
            Assert.Equal("2007-03-30T11:09:38.6987997 (TDB)", windows.ElementAt(1).StartDate.ToFormattedString());
            Assert.Equal("2007-04-01T00:01:05.1856544 (TDB)", windows.ElementAt(1).EndDate.ToFormattedString());
        }

        [Fact]
        public void FindWindowsOnOccultationConstraint()
        {
            CelestialBody moon = new CelestialBody(PlanetsAndMoons.MOON);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnOccultationConstraint(new Window(DateTime.Parse("2005-10-03 00:00:00"), DateTime.Parse("2005-10-04 00:00:00")),
                TestHelpers.Sun, ShapeType.Ellipsoid, TestHelpers.MoonAtJ2000, ShapeType.Ellipsoid, OccultationType.Partial, Aberration.None, TimeSpan.FromSeconds(360.0));
            var windows = res as Window[] ?? res.ToArray();
            Assert.Single(windows);
            Assert.Equal("2005-10-03T08:37:48.4010840 (TDB)", windows.ElementAt(0).StartDate.ToFormattedString());
            Assert.Equal("2005-10-03T10:15:20.0624047 (TDB)", windows.ElementAt(0).EndDate.ToFormattedString());
        }

        [Fact]
        public void FindWindowsOnCoordinateConstraint()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnCoordinateConstraint(new Window(DateTime.Parse("2005-10-03").ToTDB(), DateTime.Parse("2005-11-03").ToTDB()), TestHelpers.MoonAtJ2000,
                TestHelpers.MoonAtJ2000.Frame, CoordinateSystem.Latitudinal, Coordinate.Latitude, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
                TimeSpan.FromSeconds(60.0));

            var windows = res as Window[] ?? res.ToArray();
            Assert.Equal(2, windows.Length);
            Assert.Equal("2005-10-03T13:52:57.9512678 (TDB)", windows[0].StartDate.ToFormattedString());
            Assert.Equal("2005-10-17T03:42:05.5379366 (TDB)", windows[0].EndDate.ToFormattedString());
            Assert.Equal("2005-10-30T16:50:14.1421977 (TDB)", windows[1].StartDate.ToFormattedString());
            Assert.Equal("2005-11-03T00:00:00.0000000 (TDB)", windows[1].EndDate.ToFormattedString());
        }

        [Fact]
        public void FindWindowsOnIlluminationConstraint()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));

            var res = site.FindWindowsOnIlluminationConstraint(new Window(DateTimeExtension.CreateTDB(674524800), DateTimeExtension.CreateTDB(674611200)), TestHelpers.Sun,
                IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.CNS,
                TimeSpan.FromSeconds(3600), TestHelpers.Sun);

            var windows = res as Window[] ?? res.ToArray();
            Assert.Single(windows);
            Assert.Equal("2021-05-17T12:51:01.1096973 (TDB)", windows[0].StartDate.ToFormattedString());
            Assert.Equal("2021-05-18T02:55:45.3297069 (TDB)", windows[0].EndDate.ToFormattedString());
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
            Window window = new Window(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 1, 2, 12, 0, 0, DateTimeKind.Unspecified));

            Site site = new Site(339, "S339", TestHelpers.EarthAtJ2000, new Planetodetic(30 * Astrodynamics.Constants.Deg2Rad, 10 * Astrodynamics.Constants.Deg2Rad, 1000.0));
            var res = site.GetEphemeris(window, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1)).Select(x => x.ToStateVector());
            site.WriteEphemeris(new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Sites").CreateSubdirectory(site.Name).FullName, "sitetest339.spk")),
                res);
            API.Instance.LoadKernels(new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Sites").CreateSubdirectory(site.Name).FullName, "sitetest339.spk")));
            var orbitalParametersEnumerable = API.Instance.ReadEphemeris(window, site.CelestialBody, site, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1));
            Assert.Equal(new Vector3(4054783.0920394, -4799280.902678638, 1100391.2395513842), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Position,
                TestHelpers.VectorComparer);
            Assert.Equal(new Vector3(349.9689414487369, 295.67943565441215, 0.00047467276487285595), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Velocity,
                TestHelpers.VectorComparer);
            Assert.Equal(DateTimeExtension.J2000, orbitalParametersEnumerable.ElementAt(0).Epoch);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(0).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(0).Observer);

            Assert.Equal(5675531.4473050004, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.X, 6);
            Assert.Equal(2694837.3700879999, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Y, 6);
            Assert.Equal(1100644.504743, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Z, 6);
            Assert.Equal(-196.510785, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.X, 6);
            Assert.Equal(413.86626653238068, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Y, 6);
            Assert.Equal(0.00077810438580775993, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Z, 6);
            Assert.Equal(18000.0, orbitalParametersEnumerable.ElementAt(5).Epoch.SecondsFromJ2000TDB());
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
    }
}