using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Surface
{
    public class SiteTests
    {

        private readonly ITestOutputHelper _output;

        public SiteTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
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


        // #690: the horizontal coordinates of a site from the kernels are those of the frame of its kernels, against spkezr_c
        // of the Moon from DSS-13 in DSS-13_TOPO by name. Measured on Linux x64: 1.59e-16 rad (2000-01-05); threshold twice
        // that rounded up, provisional until the three platforms have printed their MEASURE lines
        private const double KernelTopocentricThreshold = 3.2e-16;

        private void AssertAgainstKernelTopocentricFrame(in TimeSystem.Time epoch, Horizontal horizontal)
        {
            var expected = SpiceAPI.Instance.ReadEphemeris(epoch, 399013, 301, new Frames.Frame("DSS-13_TOPO"), Aberration.None).ToStateVector().Position;
            double cosElevation = System.Math.Cos(horizontal.Elevation);
            var actual = new Vector3(cosElevation * System.Math.Cos(horizontal.Azimuth), -cosElevation * System.Math.Sin(horizontal.Azimuth),
                System.Math.Sin(horizontal.Elevation));
            double angle = System.Math.Atan2(actual.Cross(expected).Magnitude(), actual * expected);
            _output.WriteLine($"MEASURE #690 horizontal coordinates at {epoch} against spkezr in DSS-13_TOPO: {angle:E3} rad");
            Assert.True(angle <= KernelTopocentricThreshold, $"{angle:E3} rad");
        }

        [Fact]
        public void GetHorizontalCoordinates()
        {
            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in DSS-13_TOPO, the frame of the kernels of DSS-13 (tools/geodetic_reference/dss13_topo.c, #690); before
            // 11.0.0 the frame of the library, built from the geodetic coordinates of DSS-13 at J2000 TDB on the ellipsoid
            // of pck00011, 7.8e-8 rad away (its lines 'pck00011 frame')
            Assert.Equal(117.89631808999324, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(16.790616801126596, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(400552679.30434096, hor.Range, 3);
            AssertAgainstKernelTopocentricFrame(epoch, hor);
        }

        [Fact]
        public void GetHorizontalCoordinates2()
        {
            var epoch = new TimeSystem.Time(2000, 1, 5, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in DSS-13_TOPO, the frame of the kernels of DSS-13 (tools/geodetic_reference/dss13_topo.c, #690); before
            // 11.0.0 the frame of the library, built from the geodetic coordinates of DSS-13 at J2000 TDB on the ellipsoid
            // of pck00011, 7.8e-8 rad away (its lines 'pck00011 frame')
            Assert.Equal(100.01881372444433, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-23.23601238548768, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(408535095.85179836, hor.Range, 3);
            AssertAgainstKernelTopocentricFrame(epoch, hor);
        }

        [Fact]
        public void GetHorizontalCoordinates3()
        {
            var epoch = new TimeSystem.Time(2000, 1, 10, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in DSS-13_TOPO, the frame of the kernels of DSS-13 (tools/geodetic_reference/dss13_topo.c, #690); before
            // 11.0.0 the frame of the library, built from the geodetic coordinates of DSS-13 at J2000 TDB on the ellipsoid
            // of pck00011, 7.8e-8 rad away (its lines 'pck00011 frame')
            Assert.Equal(41.608304711283012, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-63.02074114350669, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(401248015.68690348, hor.Range, 3);
            AssertAgainstKernelTopocentricFrame(epoch, hor);
        }

        [Fact]
        public void GetHorizontalCoordinates4()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);

            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var hor = site.GetHorizontalCoordinates(epoch, TestHelpers.MoonAtJ2000, Aberration.None);
            // SPICE in DSS-13_TOPO, the frame of the kernels of DSS-13 (tools/geodetic_reference/dss13_topo.c, #690); before
            // 11.0.0 the frame of the library, built from the geodetic coordinates of DSS-13 at J2000 TDB on the ellipsoid
            // of pck00011, 7.8e-8 rad away (its lines 'pck00011 frame')
            Assert.Equal(312.54262561680804, hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(-33.618934802402485, hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg, 9);
            Assert.Equal(376638211.1128177, hor.Range, 3);
            AssertAgainstKernelTopocentricFrame(epoch, hor);
        }

        [Fact]
        public void GetHorizontalCoordinates5()
        {
            var epoch = new TimeSystem.Time(2000, 1, 15, 12, 0, 0);
            // A custom site at the geodetic coordinates of DSS-13 on the ellipsoid of pck00011 (SPICE recgeo_c of the
            // kernel station at J2000 TDB, tools/geodetic_reference/dss13_topo.c): the position of the kernel station, in
            // the topocentric frame of those J2000 coordinates. The kernel station saw the same before #690; it now takes
            // its kernel frame, DSS-13_TOPO, and its azimuth is 4.6e-6° away (GetHorizontalCoordinates4). Before #411
            // these coordinates were 1073.24 m and 35.24719501°, and the custom site was 1.3 m away
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

            // CSPICE gfposc (tools/geometry_reference, converged to 1e-9 s) in DSS-13_TOPO, the frame of the kernels of the
            // station (#690): the Moon is up at the start and at the end of the search window, whose bounds close the windows
            var windows = res as Window[] ?? res.ToArray();
            GeometryReference.AssertWindows(null, "dss13_moon_z > 0", windows, GeometryReference.ThresholdCrossing);
            Assert.Equal(TimeSystem.Time.J2000TDB, windows[0].StartDate);
            Assert.Equal(new TimeSystem.Time("2000-01-02 12:00:00").ToTDB(), windows[1].EndDate);
        }

        [Theory]
        [InlineData(Aberration.None, "site_sun_elevation > -0.8 deg NONE")]
        [InlineData(Aberration.LT, "site_sun_elevation > -0.8 deg LT")]
        [InlineData(Aberration.LTS, "site_sun_elevation > -0.8 deg LT+S")]
        public void FindWindowsOnIlluminationConstraint(Aberration aberration, string reference)
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
                new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85059));

            var res = site.FindWindowsOnIlluminationConstraint(new Window(TimeSystem.Time.CreateTDB(674524800), TimeSystem.Time.CreateTDB(674611200)),
                IlluminationAngle.Incidence, RelationnalOperator.Lower, System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0, aberration,
                TimeSpan.FromSeconds(3600), TestHelpers.Sun);

            // CSPICE gfposc (tools/geometry_reference, converged to 1e-9 s): the Sun above -0.8° of elevation in the site's
            // frame, the incidence below 90.8°, with the same correction (LT+S: the 1.13 s of #491 before its fix)
            GeometryReference.AssertWindows(null, reference, res, GeometryReference.ThresholdCrossing);
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
            var spk = new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Sites").CreateSubdirectory(site.Name).FullName, "sitetest339.spk"));
            site.WriteEphemeris(window, spk);
            SpiceAPI.Instance.LoadKernels(spk);
            try
            {
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
            finally
            {
                SpiceAPI.Instance.UnloadKernels(spk);
            }
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

        // #690, second review: the frame kernel written for a site from the kernels, read back by SPICE, against the kernel
        // frame it copies. Measured on Linux x64: 2.128e-16 rad at both dates, for both names; threshold twice that rounded up,
        // provisional until the three platforms have printed their MEASURE lines
        private const double WrittenKernelSiteFrameThreshold = 5.0e-16;

        /// <summary>
        /// The frame kernel written for a site from the kernels whose frame the loaded kernels define is that kernel frame
        /// (#690, second review): its name and id, DSS-13_TOPO (1399013) whatever the name of the site, and its orientation.
        /// It wrote the frame of <see cref="Site.Planetodetic"/> (the J2000 coordinates on pck00011) under the kernel's id,
        /// 7.76e-8 rad from DSS-13_TOPO (6.3e-8 rad at its zenith), and for a site named otherwise renamed the frame of id
        /// 1399013. The file is loaded with its frame and body renamed (1399913, T690_TOPO, 399913), so that it redefines
        /// nothing of Data/SolarSystem, and SPICE reads its rotation to DSS-13_TOPO (pxform), the identity.
        /// </summary>
        [Theory]
        [InlineData("DSS-13")]
        [InlineData("GOLDSTONE")]
        public async Task WriteFrame_KernelSite_WritesTheKernelFrame(string name)
        {
            // Arrange
            var site = new Site(13, name, PlanetsAndMoons.EARTH_BODY);
            var fk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("kernel_site");
            var renamed = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("kernel_site_renamed");
            try
            {
                // Act
                await site.WriteFrameAsync(fk);

                // Assert: the kernel's name and id; SPICE reads the renamed copy as DSS-13_TOPO
                var text = await File.ReadAllTextAsync(fk.FullName);
                Assert.Contains("FRAME_1399013_NAME        = 'DSS-13_TOPO'", text);
                Assert.Contains("FRAME_DSS-13_TOPO        = 1399013", text);
                if (name != "DSS-13")
                {
                    Assert.DoesNotContain(name + "_TOPO", text);
                }

                await File.WriteAllTextAsync(renamed.FullName, text.Replace("1399013", "1399913").Replace("399013", "399913")
                    .Replace("DSS-13_TOPO", "T690_TOPO").Replace($"'{name.ToUpper()}'", "'T690'"));
                Assert.Empty(KernelShadowing.Find([renamed], KernelShadowing.SolarSystem));
                SpiceAPI.Instance.LoadKernels(renamed);
                try
                {
                    foreach (var date in new[] { "2000-01-02T00:00:00", "2024-03-01T00:00:00" })
                    {
                        var epoch = new TimeSystem.Time(date + " TDB");
                        var rotation = SpiceAPI.Instance.TransformFrame(epoch, new Frames.Frame("T690_TOPO"), new Frames.Frame("DSS-13_TOPO")).Rotation;
                        double angle = 2.0 * System.Math.Atan2(rotation.VectorPart.Magnitude(), System.Math.Abs(rotation.W));
                        _output.WriteLine($"MEASURE #690 written frame of {name} against DSS-13_TOPO {date}: {angle:E3} rad");
                        Assert.True(angle <= WrittenKernelSiteFrameThreshold, $"{angle:E3} rad");
                    }
                }
                finally
                {
                    SpiceAPI.Instance.UnloadKernels(renamed);
                }
            }
            finally
            {
                IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(fk);
                IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(renamed);
            }
        }

        /// <summary>
        /// A site from the kernels whose kernel frame is not fixed in the frame of its body cannot be written as a TK frame of
        /// that body (#690, second review): a frame of id 1399777 defined relative to J2000 for a site of an SPK written by
        /// the library. The writer throws <see cref="InvalidOperationException"/> and writes nothing.
        /// </summary>
        [Fact]
        public async Task WriteFrame_KernelSiteWhoseFrameIsNotFixedInItsBody_Throws()
        {
            // Arrange: the SPK of the site and a frame kernel of its id relative to J2000
            var earth = PlanetsAndMoons.EARTH_BODY;
            var spk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site777", ".bsp");
            var frameKernel = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site777_inertial");
            var fk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site777_written");
            new Site(777, "RV690", earth, new Planetodetic(0.1, 0.6, 100.0))
                .WriteEphemeris(new Window(TimeSystem.Time.J2000TDB.AddDays(-1.0), TimeSystem.Time.J2000TDB.AddDays(1.0)), spk);
            await File.WriteAllTextAsync(frameKernel.FullName,
                "KPL/FK\n\\begindata\nFRAME_RV690_TOPO = 1399777\nFRAME_1399777_NAME = 'RV690_TOPO'\nFRAME_1399777_CLASS = 4\n" +
                "FRAME_1399777_CLASS_ID = 1399777\nFRAME_1399777_CENTER = 399777\nTKFRAME_1399777_SPEC = 'MATRIX'\n" +
                "TKFRAME_1399777_RELATIVE = 'J2000'\nTKFRAME_1399777_MATRIX = (1 0 0 0 1 0 0 0 1)\n\\begintext\n");
            Assert.Empty(KernelShadowing.Find([spk, frameKernel], KernelShadowing.SolarSystem));
            SpiceAPI.Instance.LoadKernels(spk);
            SpiceAPI.Instance.LoadKernels(frameKernel);
            try
            {
                var site = new Site(777, "RV690", earth);
                Assert.True(site.IsSpiceBacked);

                // Act
                var exception = await Record.ExceptionAsync(() => site.WriteFrameAsync(fk));

                // Assert
                var rejected = Assert.IsType<InvalidOperationException>(exception);
                Assert.Contains("RV690_TOPO", rejected.Message);
                Assert.Contains("ITRF93", rejected.Message);
                fk.Refresh();
                Assert.False(fk.Exists);
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(frameKernel);
                SpiceAPI.Instance.UnloadKernels(spk);
                IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(spk);
                IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(frameKernel);
                IO.Astrodynamics.Tests.Body.FrameKernelFiles.Delete(fk);
            }
        }

        [Fact]
        public void IlluminationIncidence()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationIncidence(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            // SPICE: the angle of the Sun to the normal of the ellipsoid of pck00011 at the geodetic coordinates of DSS-13 at
            // J2000 TDB, gfilum's normal for the site (tools/geodetic_reference/dss13_topo.c, its zenith of the 'pck00011
            // frame'), not the zenith of DSS-13_TOPO, the frame of the site since #690
            Assert.Equal(2.1862894689515753, res, 12);
        }

        [Fact]
        public void IlluminationEmission()
        {
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
            var res = site.IlluminationEmission(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Aberration.None);
            // SPICE: the angle of the Sun to the normal of the ellipsoid of pck00011 at the geodetic coordinates of DSS-13 at
            // J2000 TDB, gfilum's normal for the site (tools/geodetic_reference/dss13_topo.c, its zenith of the 'pck00011
            // frame'), not the zenith of DSS-13_TOPO, the frame of the site since #690
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
            // SPICE: the angle of the Sun to the normal of the ellipsoid of pck00011 at the geodetic coordinates of DSS-13 at
            // J2000 TDB, gfilum's normal for the site (tools/geodetic_reference/dss13_topo.c, its zenith of the 'pck00011
            // frame'), not the zenith of DSS-13_TOPO, the frame of the site since #690
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

        /// <summary>
        /// Site names whose FRAME_&lt;SITE&gt;_TOPO is no kernel variable SPICE can read (#676): a blank, or more than 32
        /// bytes (Kernel Required Reading, "Variable Name Rules"; measured on main: SPICE(BADVARASSIGN), SPICE(BADVARNAME)).
        /// The writer throws and writes nothing.
        /// </summary>
        [Theory]
        [InlineData("DSS 13")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUV")]
        public async Task WriteFrame_NameSpiceCannotRead_Throws(string name)
        {
            // Arrange
            var site = new Site(801, name, new CelestialBody(PlanetsAndMoons.EARTH), new Planetodetic(0.5, 0.6, 100.0));
            var fk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site_refused");

            // Act
            var exception = await Record.ExceptionAsync(() => site.WriteFrameAsync(fk));

            // Assert
            var rejected = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains(name.ToUpper(), rejected.Message);
            fk.Refresh();
            Assert.False(fk.Exists);
        }

        /// <summary>
        /// A site name of 21 characters, FRAME_ + 21 + _TOPO = 32 bytes: the FK loads in SPICE and its TOPO frame resolves
        /// by its name through <c>sxform_c</c> (#676).
        /// </summary>
        [Fact]
        public async Task WriteFrame_BoundaryNames_LoadInSpice()
        {
            // Arrange
            var site = new Site(802, "ABCDEFGHIJKLMNOPQRSTU", new CelestialBody(PlanetsAndMoons.EARTH), new Planetodetic(0.5, 0.6, 100.0));
            var fk = IO.Astrodynamics.Tests.Body.FrameKernelFiles.NewFile("site_boundary");
            Assert.Equal(32, System.Text.Encoding.UTF8.GetByteCount("FRAME_ABCDEFGHIJKLMNOPQRSTU_TOPO"));

            // Act
            await site.WriteFrameAsync(fk);

            // Assert: loaded, and the frame known to SPICE by its name
            IO.Astrodynamics.Tests.Body.FrameKernelFiles.Load(fk, () =>
            {
                var orientation = SpiceAPI.Instance.TransformFrame(new TimeSystem.Time(2024, 1, 1), Frames.Frame.ICRF, new Frames.Frame("ABCDEFGHIJKLMNOPQRSTU_TOPO"));
                Assert.Equal(1.0, orientation.Rotation.Magnitude(), 12);
            });
        }

        private static readonly string[] MethodsGfilumRejects = ["Dsk", "Elipsoid", "ELLIP SOID", "", "DSK/UNPRIORITIZED", "DSK/UNPRIORITIZED/SURFACES = 1"];

        /// <summary>
        /// #682: <c>gfilum_c</c> supports one method, ELLIPSOID ("The only choice currently supported is "Ellipsoid"",
        /// Detailed_Input <c>method</c>, CSPICE N0067); measured on N0067, each of these strings is rejected
        /// (SPICE(INVALIDMETHOD) "The only supported computation method is ELLIPSOID", SPICE(EMPTYSTRING) for the empty one,
        /// Exceptions 18), which the native proxy dropped: no window on the CSPICE path, the ellipsoid's windows on the
        /// managed path, which never read the method. Each now throws on both paths and in <see cref="SpiceAPI"/>.
        /// </summary>
        [Fact]
        public void IlluminationSearch_MethodOtherThanEllipsoid_ThrowsOnBothPaths()
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var dss13 = new Site(13, "DSS-13", earth);
            var fromCoordinates = new Site(682, "F07DSS13GEO", earth, dss13.Planetodetic);
            var window = new Window(new TimeSystem.Time(2024, 3, 1), new TimeSystem.Time(2024, 3, 3));
            var step = TimeSpan.FromMinutes(10.0);
            var searches = new (string Path, Func<string, Window[]> Search)[]
            {
                ("kernel site, CSPICE", m => dss13.FindWindowsOnIlluminationConstraint(window, IlluminationAngle.Incidence, RelationnalOperator.Lower,
                    IO.Astrodynamics.Constants.PI2, 0.0, Aberration.LTS, step, TestHelpers.Sun, m).ToArray()),
                ("site from coordinates, managed", m => fromCoordinates.FindWindowsOnIlluminationConstraint(window, IlluminationAngle.Incidence,
                    RelationnalOperator.Lower, IO.Astrodynamics.Constants.PI2, 0.0, Aberration.LTS, step, TestHelpers.Sun, m).ToArray()),
                ("SpiceAPI", m => SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(window, 399013, 399, earth.Frame, dss13.Planetodetic,
                    IlluminationAngle.Incidence, RelationnalOperator.Lower, IO.Astrodynamics.Constants.PI2, 0.0, Aberration.LTS, step, 10, m).ToArray())
            };
            foreach (var (path, search) in searches)
            {
                foreach (var method in MethodsGfilumRejects)
                {
                    var exception = Assert.Throws<ArgumentException>(() => search(method));
                    Assert.Equal("method", exception.ParamName);
                    Assert.Contains("ELLIPSOID", exception.Message);
                }

                Assert.Equal("method", Assert.Throws<ArgumentNullException>(() => search(null)).ParamName);
                _output.WriteLine($"{path}: every method but ELLIPSOID rejected");
            }
        }

        /// <summary>
        /// #682: the emission and phase angles are angles at the point towards an observer (<c>gfilum_c</c> Particulars: the
        /// vector from the surface point to the observer); the site's search takes none, so its observer would be the site
        /// itself, where that vector is zero and the angles are undefined. Before, the managed path measured them as 0 (one
        /// window of the whole day) and <c>gfilum_c</c>, given the site as its observer, gave 0 and 2 windows.
        /// </summary>
        [Fact]
        public void IlluminationSearch_EmissionAndPhase_ThrowWithoutObserver()
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var dss13 = new Site(13, "DSS-13", earth);
            var fromCoordinates = new Site(682, "F07DSS13GEO", earth, dss13.Planetodetic);
            var window = new Window(new TimeSystem.Time(2024, 3, 1), new TimeSystem.Time(2024, 3, 2));
            foreach (var site in new[] { dss13, fromCoordinates })
            {
                foreach (var angle in new[] { IlluminationAngle.Emission, IlluminationAngle.Phase })
                {
                    foreach (var relation in new[] { RelationnalOperator.Lower, RelationnalOperator.Greater, RelationnalOperator.LocalMin })
                    {
                        var exception = Assert.Throws<ArgumentException>(() => site.FindWindowsOnIlluminationConstraint(window, angle, relation,
                            IO.Astrodynamics.Constants.PI2, 0.0, Aberration.None, TimeSpan.FromMinutes(10.0), TestHelpers.Sun).ToArray());
                        Assert.Equal("illuminationType", exception.ParamName);
                        Assert.Contains("observer", exception.Message);
                    }
                }

                // An angle that is none of the enumeration's
                if (site == fromCoordinates)
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => site.FindWindowsOnIlluminationConstraint(window, (IlluminationAngle)99,
                        RelationnalOperator.Lower, IO.Astrodynamics.Constants.PI2, 0.0, Aberration.None, TimeSpan.FromMinutes(10.0), TestHelpers.Sun).ToArray());
                }

                // The angle is checked before the method
                Assert.Equal("illuminationType", Assert.Throws<ArgumentException>(() => site.FindWindowsOnIlluminationConstraint(window,
                    IlluminationAngle.Emission, RelationnalOperator.Lower, IO.Astrodynamics.Constants.PI2, 0.0, Aberration.None, TimeSpan.FromMinutes(10.0),
                    TestHelpers.Sun, "DSK").ToArray()).ParamName);
            }
        }

        // #682. Twice the worst over Linux, Windows and macOS arm64 rounded up (CI run 43591652055, 2026-11-14): 4.435e-3 s
        // on each of the three: the managed search takes the geodetic normal at DSS-13, gfilum the normal of surfnm,
        // 0.110" apart at 1070.85 m (#654), over the rate of the emission angle
        private const double EmissionFromTheMoonLimit = 8.9e-3;

        /// <summary>
        /// #682: the way to an emission search the message of the site's search names, the public
        /// <see cref="GeometryFinder"/> on <see cref="Site.IlluminationEmission"/> with an observer, here the Moon, equals
        /// <c>gfilum_c</c> with the Moon as its observer at the point of DSS-13 (06:52:07.61 to 17:06:28.25 TDB).
        /// </summary>
        [Fact]
        public void IlluminationEmission_FromTheMoon_EqualsGfilum()
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var moon = new CelestialBody(PlanetsAndMoons.MOON);
            var dss13 = new Site(13, "DSS-13", earth);
            var fromCoordinates = new Site(682, "F07DSS13GEO", earth, dss13.Planetodetic);
            var window = new Window(new TimeSystem.Time(2024, 3, 1), new TimeSystem.Time(2024, 3, 2));
            var step = TimeSpan.FromMinutes(10.0);
            var managed = new GeometryFinder().FindWindowsWithCondition(window, t => fromCoordinates.IlluminationEmission(t, moon, Aberration.None),
                RelationnalOperator.Lower, IO.Astrodynamics.Constants.PI2, step).ToArray();
            var gfilum = SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(window, 301, 399, earth.Frame, dss13.Planetodetic, IlluminationAngle.Emission,
                RelationnalOperator.Lower, IO.Astrodynamics.Constants.PI2, 0.0, Aberration.None, step, 10).ToArray();
            Assert.Single(gfilum);
            Assert.Equal(gfilum.Length, managed.Length);
            double worst = 0.0;
            for (int i = 0; i < gfilum.Length; i++)
            {
                worst = System.Math.Max(worst, System.Math.Abs((managed[i].StartDate - gfilum[i].StartDate).TotalSeconds));
                worst = System.Math.Max(worst, System.Math.Abs((managed[i].EndDate - gfilum[i].EndDate).TotalSeconds));
            }

            _output.WriteLine($"MEASURE #682 emission from the Moon at DSS-13: worst |managed - gfilum| {worst:E3} s");
            Assert.True(worst < EmissionFromTheMoonLimit, $"{worst:E3} s");
        }

        // The same rotation of the same read, to its rounding: 1 µm and 1 nm/s, or a few ulp of the distance of the Sun
        // (1.5e11 m, an ulp of 3.1e-5 m) and of its velocity in a frame turning with the Earth (1.1e7 m/s)
        private static void AssertSameState(StateVector expected, StateVector actual, Frames.Frame frame, string label)
        {
            Assert.Same(frame, actual.Frame);
            double dr = (actual.Position - expected.Position).Magnitude();
            double dv = (actual.Velocity - expected.Velocity).Magnitude();
            Assert.True(dr < 1e-6 + 1e-15 * expected.Position.Magnitude(), $"{label}: |dr| {dr:E3} m");
            Assert.True(dv < 1e-9 + 1e-15 * expected.Velocity.Magnitude(), $"{label}: |dv| {dv:E3} m/s");
        }

        /// <summary>
        /// #682: a frame only the library holds, the frame of a site built from coordinates, reached <c>spkezr_c</c> by name
        /// (SPICE(UNKNOWNFRAME)) when the site was read from an observer other than its body; it is read in J2000 and rotated
        /// by the managed frame, as #558 reads TEME, CIRS and TIRS. Seen from the Moon and the Sun, with and without light
        /// time, the read in the site's frame is the ICRF read rotated to it, and so are a body read in it and the reads of
        /// <see cref="SpiceAPI"/>; a coordinate search in it runs. A kernel spacecraft as the observer is in
        /// <see cref="KernelWindowSearchTests"/>, whose class loads LRO; the rotation against CSPICE in
        /// <see cref="CustomSite_FromTheMoon_InItsFrame_MatchesSpkezr"/>.
        /// </summary>
        [Fact]
        public void CustomSite_FromNonEarthObserver_InItsOwnFrame_EqualsTheIcrfReadRotated()
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var moon = new CelestialBody(PlanetsAndMoons.MOON);
            var site = new Site(682, "F07CUSTOM", earth, new Planetodetic(-117.0 * IO.Astrodynamics.Constants.Deg2Rad, 34.0 * IO.Astrodynamics.Constants.Deg2Rad, 1000.0));
            var start = new TimeSystem.Time(2024, 3, 1);
            foreach (var observer in new ILocalizable[] { moon, TestHelpers.Sun })
            {
                foreach (var aberration in new[] { Aberration.None, Aberration.LT })
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var t = start.AddSeconds(7.0 * 3600.0 * k);
                        var read = site.GetEphemeris(t, observer, site.Frame, aberration).ToStateVector();
                        var expected = site.GetEphemeris(t, observer, Frames.Frame.ICRF, aberration).ToFrame(site.Frame).ToStateVector();
                        AssertSameState(expected, read, site.Frame, $"site from {observer.Name} {aberration} at {t}");
                    }
                }
            }

            var epoch = start.AddSeconds(3600.0);
            AssertSameState(moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.LT).ToFrame(site.Frame).ToStateVector(),
                moon.GetEphemeris(epoch, earth, site.Frame, Aberration.LT).ToStateVector(), site.Frame, "the Moon from the Earth");
            AssertSameState(SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.None).ToFrame(site.Frame).ToStateVector(),
                SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, site.Frame, Aberration.None).ToStateVector(), site.Frame, "SpiceAPI at an epoch");
            var windowed = SpiceAPI.Instance.ReadEphemeris(new Window(start, start.AddSeconds(7200.0)), 399, 301, site.Frame, Aberration.None, TimeSpan.FromHours(1.0))
                .Select(x => x.ToStateVector()).ToArray();
            Assert.Equal(3, windowed.Length);
            foreach (var state in windowed)
            {
                AssertSameState(SpiceAPI.Instance.ReadEphemeris(state.Epoch, 399, 301, Frames.Frame.ICRF, Aberration.None).ToFrame(site.Frame).ToStateVector(), state,
                    site.Frame, $"SpiceAPI over a window at {state.Epoch}");
            }

            // A coordinate search in the site's frame, the declination of the site from the Moon above 0: the condition holds
            // inside each window on the ICRF read rotated to the frame
            var window = new Window(start, start.AddSeconds(86400.0));
            var windows = site.FindWindowsOnCoordinateConstraint(window, moon, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.Greater,
                0.0, 0.0, Aberration.None, TimeSpan.FromMinutes(10.0)).ToArray();
            Assert.NotEmpty(windows);
            foreach (var w in windows)
            {
                var middle = w.StartDate + (w.EndDate - w.StartDate) / 2.0;
                Assert.True(site.GetEphemeris(middle, moon, Frames.Frame.ICRF, Aberration.None).ToFrame(site.Frame).ToStateVector().Position.Z > 0.0);
            }
        }

        /// <summary>
        /// #682: the read of a site built from coordinates in its frame, from the Moon, against CSPICE with the frame kernel
        /// the library writes for it (<see cref="Site.WriteFrameAsync"/>), loaded after the library value: <c>spkezr_c</c> of
        /// the Earth from the Moon in the site's TK frame, which SPICE builds from the kernel's Euler angles, plus
        /// <c>pxform_c</c>(ITRF93, site frame) · <c>georec_c</c> of the site; its velocity is <c>spkezr_c</c>'s, the site
        /// being fixed in its frame.
        /// </summary>
        [Fact]
        [Trait("Category", "Validation")]
        public void CustomSite_FromTheMoon_InItsFrame_MatchesSpkezr()
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var moon = new CelestialBody(PlanetsAndMoons.MOON);
            var site = new Site(917, "F07SITE", earth, new Planetodetic(-117.0 * IO.Astrodynamics.Constants.Deg2Rad, 34.0 * IO.Astrodynamics.Constants.Deg2Rad, 1000.0));
            var epochs = Enumerable.Range(0, 5).Select(k => new TimeSystem.Time(2024, 3, 1).AddSeconds(7.0 * 3600.0 * k)).ToArray();
            var library = epochs.Select(t => site.GetEphemeris(t, moon, site.Frame, Aberration.None).ToStateVector()).ToArray();

            var directory = Directory.CreateTempSubdirectory("f07site-");
            var frameKernel = new FileInfo(Path.Combine(directory.FullName, "f07site.tf"));
            site.WriteFrameAsync(frameKernel).GetAwaiter().GetResult();
            Assert.Empty(KernelShadowing.Find([frameKernel], KernelShadowing.SolarSystem));
            SpiceAPI.Instance.LoadKernels(frameKernel);
            try
            {
                double re = earth.EquatorialRadius / 1000.0;
                var p = IO.Astrodynamics.Tests.Body.CspiceDirect.Georec(site.Planetodetic.Longitude, site.Planetodetic.Latitude, site.Planetodetic.Altitude / 1000.0,
                    re, earth.Flattening);
                double worstPosition = 0.0, worstVelocity = 0.0;
                for (int k = 0; k < epochs.Length; k++)
                {
                    double et = epochs[k].ToTDB().TimeSpanFromJ2000().TotalSeconds;
                    var st = IO.Astrodynamics.Tests.Body.CspiceDirect.Spkezr("EARTH", et, "F07SITE_TOPO", "NONE", "MOON");
                    var m = IO.Astrodynamics.Tests.Body.CspiceDirect.Pxform("ITRF93", "F07SITE_TOPO", et);
                    var position = new Vector3(st[0] + m[0] * p[0] + m[1] * p[1] + m[2] * p[2], st[1] + m[3] * p[0] + m[4] * p[1] + m[5] * p[2],
                        st[2] + m[6] * p[0] + m[7] * p[1] + m[8] * p[2]) * 1000.0;
                    var velocity = new Vector3(st[3], st[4], st[5]) * 1000.0;
                    worstPosition = System.Math.Max(worstPosition, (library[k].Position - position).Magnitude());
                    worstVelocity = System.Math.Max(worstVelocity, (library[k].Velocity - velocity).Magnitude());
                }

                _output.WriteLine($"MEASURE #682 site frame from the Moon against spkezr + pxform·georec: |dr| {worstPosition:E3} m, |dv| {worstVelocity:E3} m/s");
                Assert.True(worstPosition < SiteFrameReadPositionLimit, $"{worstPosition:E3} m");
                Assert.True(worstVelocity < SiteFrameReadVelocityLimit, $"{worstVelocity:E3} m/s");
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(frameKernel);
                directory.Delete(true);
            }
        }

        // #682. Twice the worst on Linux x64 rounded up (CI run 43591652055, 2026-11-14): 3.992e-7 m, 6.447e-11 m/s; the
        // test runs on Linux only (Validation, CspiceDirect)
        private const double SiteFrameReadPositionLimit = 8.0e-7;
        private const double SiteFrameReadVelocityLimit = 1.3e-10;
    }
}
