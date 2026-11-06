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
            SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
            // SPICE in the topocentric frame of the library, built from the geodetic coordinates of DSS-13 on the
            // ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c); before #411 the frame took the latitude of
            // the relation of the surface, 3.05e-5° too far north
            Assert.Equal(117.89632135112616, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(16.790619489720385, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(400552679.30400002, hor.Range, 3);
        }

        [Fact]
        public void GetHorizontalCoordinates2()
        {
            var epoch = new TimeSystem.Time(2000, 1, 5, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in the topocentric frame of the library, built from the geodetic coordinates of DSS-13 on the
            // ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c); before #411 the frame took the latitude of
            // the relation of the surface, 3.05e-5° too far north
            Assert.Equal(100.01881556283226, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-23.236009058726474, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(408535095.85179406, hor.Range, 3);
        }

        [Fact]
        public void GetHorizontalCoordinates3()
        {
            var epoch = new TimeSystem.Time(2000, 1, 10, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in the topocentric frame of the library, built from the geodetic coordinates of DSS-13 on the
            // ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c); before #411 the frame took the latitude of
            // the relation of the surface, 3.05e-5° too far north
            Assert.Equal(41.608311183520655, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-63.020738075776627, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(401248015.68691534, hor.Range, 3);
        }

        [Fact]
        public void GetHorizontalCoordinates4()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in the topocentric frame of the library, built from the geodetic coordinates of DSS-13 on the
            // ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c); before #411 the frame took the latitude of
            // the relation of the surface, 3.05e-5° too far north
            Assert.Equal(312.54263018434767, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-33.618936771066764, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(376638211.11299998, hor.Range, 3);
        }

        [Fact]
        public void GetHorizontalCoordinates5()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);
            // A custom site at the geodetic coordinates of DSS-13 on the ellipsoid of pck00011 (SPICE recgeo_c of the
            // kernel station at J2000 TDB, tools/geodetic_reference/dss13_topo.c) sees what the kernel station sees;
            // before #411 these coordinates were 1073.24 m and 35.24719501°, and the custom site was 1.3 m away
            var site = new Site(339, "TestSite", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837193671 * IO.Astrodynamics.Constants.Deg2Rad,
                    35.247164496550241 * IO.Astrodynamics.Constants.Deg2Rad, 1070.8505951920122));
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(312.54263018434767, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-33.618936771066764, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(376638211.1128177, hor.Range, 3);
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
            Assert.Equal(new StateVector(new Vector3(-5023123.882343292, 1403764.4753112793, 3660826.4110731576),
                    new Vector3(-102.37160945689291, -366.2848679089919, -0.01299987338117344), earth, epoch + TimeSpan.FromDays(1.0), Frames.Frame.ICRF), sv[1],
                TestHelpers.StateVectorComparer);
        }
        
        [Fact]
        public void GetEphemerisLT()
        {
            var epoch = TimeSystem.Time.J2000TDB;
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.GetEphemeris(new Window(epoch, epoch + TimeSpan.FromDays(1.0)), earth, Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromDays(1.0))
                .Select(x => x.ToStateVector()).ToArray();

            Assert.Equal(2, sv.Length);
            Assert.Equal(new StateVector(new Vector3(-4997598.156070709, 1490074.2206420898, 3660874.145385742),
                new Vector3(-108.65761807425588, -364.46946596885664, -0.013065898133845621), earth, epoch, Frames.Frame.ICRF), sv[0], TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void GetPosition()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Site site = new Site(13, "DSS-13", earth);

            var sv = site.InitialOrbitalParameters.ToStateVector().Position;

            Assert.Equal(new Vector3(-2351112.6050050138, -4655530.65549371, 3660912.739397665), sv, TestHelpers.VectorComparer);
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
            // CSPICE gfdist (tools/geometry_reference, converged to 1e-9 s): 24 windows
            GeometryReference.AssertWindows(null, "dss13_moon_distance > 400000 km", res, GeometryReference.ThresholdCrossing);
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
            // CSPICE gfoclt (tools/occultation_reference, converged to 1e-9 s), TDB seconds past J2000
            Assert.Equal(TimeSystem.Time.CreateTDB(181600668.40108371), windows.ElementAt(0).StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.CreateTDB(181606520.06240487), windows.ElementAt(0).EndDate, TestHelpers.TimeComparer);
        }

        [Fact]
        public void FindWindowsOnCoordinateConstraint()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var res = site.FindWindowsOnCoordinateConstraint(new Window(new TimeSystem.Time("2000-01-01 12:00:00").ToTDB(), new TimeSystem.Time("2000-02-01 12:00:00").ToTDB()),
                TestHelpers.MoonAtJ2000,
                TestHelpers.MoonAtJ2000.Frame, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
                TimeSpan.FromSeconds(60.0));

            // CSPICE gfposc (tools/geometry_reference, converged to 1e-9 s): DSS-13 above the Moon's equator
            GeometryReference.AssertWindows(null, "moon_dss13_declination > 0", res, GeometryReference.ThresholdCrossing);
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

            // CSPICE gfposc (tools/geometry_reference, converged to 1e-9 s) in the frame the library builds for the
            // station: the Moon is up at the start and at the end of the search window, whose bounds close the windows
            var windows = res as Window[] ?? res.ToArray();
            GeometryReference.AssertWindows(null, "dss13_moon_z > 0", windows, GeometryReference.ThresholdCrossing);
            Assert.Equal(TimeSystem.Time.J2000TDB, windows[0].StartDate);
            Assert.Equal(new TimeSystem.Time("2000-01-02 12:00:00").ToTDB(), windows[1].EndDate);
        }

        [Fact]
        public void FindWindowsOnIlluminationConstraint()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));

            var res = site.FindWindowsOnIlluminationConstraint(new Window(TimeSystem.Time.CreateTDB(674524800), TimeSystem.Time.CreateTDB(674611200)),
                IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, Aberration.None,
                TimeSpan.FromSeconds(3600), TestHelpers.Sun);

            // CSPICE gfposc (tools/geometry_reference, converged to 1e-9 s): the Sun above -0.8° of elevation in the site's
            // frame, the incidence below 90.8°. Geometric: a site's LT+S read of the Sun is #491's (1.13 s on this sunrise
            // against gfposc LT+S, which the reference holds with LT)
            GeometryReference.AssertWindows(null, "site_sun_elevation > -0.8 deg NONE", res, GeometryReference.ThresholdCrossing);
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
            SpiceAPI.Instance.LoadKernels(new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Sites").CreateSubdirectory(site.Name).FullName, "sitetest339.spk")));
            var orbitalParametersEnumerable = SpiceAPI.Instance.ReadEphemeris(window, site.CelestialBody.NaifId, site.NaifId, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1));
            // SPICE georec_c on the ellipsoid of pck00011, then sxform_c from ITRF93 to J2000, the site of
            // ScenarioTests.PropagateSite (tools/spice_reference/spice_ref.c); before #411 the site was 1.15 m away
            Assert.Equal(new Vector3(4054782.9682010966, -4799280.7492771307, 1100392.3676426695), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Position,
                TestHelpers.VectorComparer);
            Assert.Equal(new Vector3(349.96683087822703, 295.68160057531139, 0.01769212216143207), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Velocity,
                TestHelpers.VectorComparer);
            Assert.Equal(TimeSystem.Time.J2000TDB, orbitalParametersEnumerable.ElementAt(0).Epoch);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(0).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(0).Observer);

            Assert.Equal(5675531.2672095764, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.X, 6);
            Assert.Equal(2694837.2897740607, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Y, 6);
            Assert.Equal(1100645.6327681679, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Z, 6);
            Assert.Equal(-196.51288168954599, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.X, 6);
            Assert.Equal(413.86842722249719, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Y, 6);
            Assert.Equal(0.0062996653421883994, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Z, 6);
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
            // SPICE: the angle of the Sun to the zenith of the frame of the library, from the geodetic coordinates of DSS-13
            // on the ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c)
            Assert.Equal(2.1862894689515753, res, 12);
        }

        [Fact]
        public void IlluminationEmission()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationEmission(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            // SPICE: the angle of the Sun to the zenith of the frame of the library, from the geodetic coordinates of DSS-13
            // on the ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c)
            Assert.Equal(2.1862894689515753, res, 12);
        }

        [Fact]
        public void IlluminationPhase()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationPhase(TimeSystem.Time.J2000TDB, TestHelpers.Sun, TestHelpers.Sun, Aberration.None);
            Assert.Equal(0.0, res,6);
        }
        
        [Fact]
        public void IlluminationIncidenceFromMoon()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationIncidence(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            // SPICE: the angle of the Sun to the zenith of the frame of the library, from the geodetic coordinates of DSS-13
            // on the ellipsoid of pck00011 (tools/geodetic_reference/dss13_topo.c)
            Assert.Equal(2.1862894689515753, res, 12);
        }

        [Fact]
        public void IlluminationEmissionFromMoon()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationEmission(TimeSystem.Time.J2000TDB, TestHelpers.Moon, Aberration.None);
            // SPICE, as for the Sun (tools/geodetic_reference/dss13_topo.c)
            Assert.Equal(1.2777447332497456, res, 12);
        }

        [Fact]
        public void IlluminationPhaseFromMoon()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationPhase(TimeSystem.Time.J2000TDB, TestHelpers.Sun, TestHelpers.Moon, Aberration.None);
            Assert.Equal(0.98449746814087213, res,6 );
        }
    }
}