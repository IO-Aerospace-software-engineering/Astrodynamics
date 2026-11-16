using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body
{
    public class InstrumentTests
    {
        // The windows of the library against gftfov on the kernels of the same propagation, s: the managed search converges
        // to 1 µs and gftfov to 1e-6 s, and their apparent Earths differ by the rounding of the light-time corrected epoch to
        // a double ET, up to half its 119 ns ulp in each (1.8 mm of the Earth's barycentric motion; #515, choice 10).
        // Twice the worst of this test's edges over Linux, Windows and macOS arm64 (CI run 41365060160, 2026-11-02):
        // 6e-7 / 6e-7 / 6e-7 s (POINT 4e-7, 4e-7 and 6e-7 s, ELLIPSOID 5e-7 and 4e-7 s on each). Before #515 it was 1 s by
        // construction, the bisection of GeometryFinder, the first start was not compared, and the ellipsoid was a point
        // (up to 1 080 s)
        private const double FieldOfViewTolerance = 1.2e-6;

        private readonly ITestOutputHelper _output;

        public InstrumentTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            var spc = TestHelpers.Spacecraft;
            spc.AddCircularInstrument(-1600, "inst", "model", 1.57, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0));
            Assert.Equal("inst", spc.Instruments.First().Name);
            Assert.Equal("model", spc.Instruments.First().Model);
            Assert.Equal(1.57, spc.Instruments.First().FieldOfView);
            Assert.Equal(InstrumentShape.Circular, spc.Instruments.First().Shape);
            Assert.Equal(-1600, spc.Instruments.First().NaifId);
            Assert.Equal(new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0), spc.Instruments.First().Orientation);
        }

        [Fact]
        public void CreateInvalid()
        {
            var spc = TestHelpers.Spacecraft;
            Assert.Throws<ArgumentException>(() =>
                spc.AddCircularInstrument(-1600, "", "model", 1.57, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0)));
            Assert.Throws<ArgumentException>(() =>
                spc.AddCircularInstrument(-1600, "inst", "", 1.57, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0)));
            Assert.Throws<ArgumentException>(() =>
                spc.AddCircularInstrument(-1600, "inst", "model", 0.0, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0)));
        }

        [Fact]
        public void Equality()
        {
            var spc = TestHelpers.Spacecraft;
            spc.AddCircularInstrument(-1600, "inst", "model", 1.57, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0));
            spc.AddCircularInstrument(-1700, "inst", "model", 1.57, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0));
            var instrument = spc.Instruments.First();
            var instrument2 = spc.Instruments.ElementAt(1);
            Assert.False(instrument == instrument2);
            Assert.True(instrument != instrument2);
            Assert.False(instrument == null);
            Assert.False(instrument.Equals((object)instrument2));
            Assert.True(instrument.Equals((object)instrument));
            Assert.False(instrument.Equals((object)null));
        }

        [Theory]
        [InlineData(ShapeType.Point)]
        [InlineData(ShapeType.Ellipsoid)]
        public async Task FindWindowInFieldOfView(ShapeType shape)
        {
            TimeSystem.Time start = TimeSystem.Time.CreateUTC(676555130.80).ToTDB();
            TimeSystem.Time end = start.AddSeconds(6448.0);

            //Configure scenario
            Scenario scenario = new Scenario("Scenario_A", new Astrodynamics.Mission.Mission("mission06"),
                new Window(start, end));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
            scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
            scenario.AddCelestialItem(TestHelpers.Sun);

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0, clock, parkingOrbit);
            spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, System.Math.PI * 0.5, 0.0));
            scenario.AddSpacecraft(spacecraft);

            //Execute scenario
            await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

            // The reference: SPICE gftfov on the kernels that the library writes from the same propagation, the SPK of
            // the spacecraft, its clock, frame and orientation, and the frame and the kernel of the instrument; written
            // before the search of the library, whose evaluations add orientations to the frame of the spacecraft
            var searchWindow = new Window(start, end);
            var instrument = spacecraft.Instruments.First();
            var expected = FieldOfViewWindowsBySpice(spacecraft, instrument, searchWindow, shape, TimeSpan.FromSeconds(360.0));

            // Find windows when the earth is in field of view of the camera, over the propagated states: the search
            // starts at the first state (choice 16 of #501); it reads nothing before it (#515)
            var res = instrument.FindWindowsInFieldOfViewConstraint(searchWindow, spacecraft,
                TestHelpers.EarthAtJ2000, TestHelpers.EarthAtJ2000.Frame,
                shape, Aberration.LT,
                TimeSpan.FromSeconds(360.0)).ToArray();

            //Read results: the windows of gftfov, every edge, the first start included: the Earth is in the field of view
            // at the first state, and both searches start there. As an ellipsoid, the Earth is in view 1 080 s longer
            // (gftfov: [S, 676556946.944] [676559033.610, E])
            Assert.Equal(2, expected.Length);
            Assert.Equal(expected.Length, res.Length);
            Assert.Equal(searchWindow.StartDate, expected[0].StartDate);
            Assert.Equal(searchWindow.StartDate, res[0].StartDate);
            for (int i = 0; i < res.Length; i++)
            {
                double startError = (res[i].StartDate - expected[i].StartDate).TotalSeconds;
                double endError = (res[i].EndDate - expected[i].EndDate).TotalSeconds;
                _output.WriteLine($"MEASURE #515 {shape} window {i}: {res[i].StartDate} to {res[i].EndDate}, gftfov " +
                                  $"{expected[i].StartDate} to {expected[i].EndDate}: {startError:E2} s and {endError:E2} s");
                Assert.InRange(startError, -FieldOfViewTolerance, FieldOfViewTolerance);
                Assert.InRange(endError, -FieldOfViewTolerance, FieldOfViewTolerance);
            }

            if (shape == ShapeType.Ellipsoid)
            {
                Assert.Equal(searchWindow.EndDate, res[1].EndDate);
            }

            Assert.Throws<ArgumentNullException>(() => spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Window(TimeSystem.Time.Create(676555200.0, TimeFrame.TDBFrame), TimeSystem.Time.Create(676561647.0, TimeFrame.TDBFrame)), null,
                TestHelpers.EarthAtJ2000, TestHelpers.EarthAtJ2000.Frame, ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromHours(1.0)));
            Assert.Throws<ArgumentNullException>(() => spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Window(TimeSystem.Time.Create(676555200.0, TimeFrame.TDBFrame), TimeSystem.Time.Create(676561647.0, TimeFrame.TDBFrame)), spacecraft,
                null, TestHelpers.EarthAtJ2000.Frame, ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromHours(1.0)));
            Assert.Throws<ArgumentNullException>(() => spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Window(TimeSystem.Time.Create(676555200.0, TimeFrame.TDBFrame), TimeSystem.Time.Create(676561647.0, TimeFrame.TDBFrame)), spacecraft,
                TestHelpers.EarthAtJ2000, null, ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromHours(1.0)));
        }

        // The windows of SPICE gftfov over the kernels written from the propagation of the spacecraft, unloaded after
        private static Window[] FieldOfViewWindowsBySpice(Spacecraft spacecraft, Instrument instrument, Window searchWindow,
            ShapeType shape, TimeSpan step)
        {
            // Relative paths, as the kernels of the other tests: the native writers take a bounded file name
            string directory = $"Data/User/FieldOfViewReference/{shape}";
            Directory.CreateDirectory(directory);
            var files = new[] { "sc179.tsc", "sc179.tf", "camera789.tf", "camera789.ti", "sc179.bsp", "sc179.ck" }
                .Select(name => new FileInfo(Path.Combine(directory, name))).ToArray();
            // Deleted by path: a FileInfo keeps the state it first read, and LoadKernels skips a kernel whose FileInfo
            // read it as missing, as on the first run in a new output directory (SPICE(KERNELVARNOTFOUND) at the CK)
            foreach (var file in files)
            {
                File.Delete(file.FullName);
            }

            // The clock and the frames first: the orientation is written in ticks of the clock
            spacecraft.Clock.WriteAsync(files[0]).GetAwaiter().GetResult();
            ((SpacecraftFrame)spacecraft.Frame).WriteAsync(files[1]).GetAwaiter().GetResult();
            instrument.WriteFrameAsync(files[2]).GetAwaiter().GetResult();
            instrument.WriteKernelAsync(files[3]).GetAwaiter().GetResult();
            foreach (var file in files.Take(4))
            {
                SpiceAPI.Instance.LoadKernels(file);
            }

            spacecraft.WriteEphemeris(files[4]);
            SpiceAPI.Instance.LoadKernels(files[4]);

            spacecraft.WriteOrientation(files[5]);
            SpiceAPI.Instance.LoadKernels(files[5]);

            try
            {
                return SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(searchWindow, spacecraft.NaifId,
                    instrument.NaifId, TestHelpers.EarthAtJ2000.NaifId, TestHelpers.EarthAtJ2000.Frame,
                    shape, Aberration.LT, step).ToArray();
            }
            finally
            {
                foreach (var file in files.Reverse())
                {
                    SpiceAPI.Instance.UnloadKernels(file);
                }
            }
        }

        [Fact]
        public void IsInFieldOfView()
        {
            TimeSystem.Time start = TimeSystem.Time.J2000TDB;

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0, clock, parkingOrbit);
            spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, System.Math.PI * 0.5, 0.0));

            StateVector targetOrbit = new StateVector(
                new Vector3(6790000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);
            Spacecraft target = new Spacecraft(-181, "SC181", 1000.0, 3000.0, new Clock("clk181", 65536), targetOrbit);

            // Never propagated, it has no attitude to test with (#682); with the identity given by hand, the target 10 km
            // below it is in the field
            Assert.Throws<InvalidOperationException>(() => spacecraft.Instruments.First().IsInFOV(start, target, Aberration.None));
            spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, start, spacecraft.Frame));
            var res = spacecraft.Instruments.First().IsInFOV(start, target, Aberration.None);
            Assert.True(res);
        }

        [Fact]
        public void GetBoresightInICRF()
        {
            TimeSystem.Time start = TimeSystem.Time.J2000TDB;

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0, clock, parkingOrbit);
            spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, System.Math.PI * 0.5, 0.0));


            var boresightICRF = spacecraft.Instruments.First().GetBoresightInICRFFrame(start);
            Assert.Equal(new Vector3(-1.0, 0.0, 0.0), boresightICRF, TestHelpers.VectorComparer);
        }
        
        [Fact]
        public void GetBoresightInICRF2()
        {
            TimeSystem.Time start = TimeSystem.Time.J2000TDB;

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0, clock, parkingOrbit);
            spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, System.Math.PI * 0.5, 0.0));
            spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(new Quaternion(Vector3.VectorZ, -Astrodynamics.Constants.PI2), Vector3.Zero, start, spacecraft.Frame));


            var boresightICRF = spacecraft.Instruments.First().GetBoresightInICRFFrame(start);
            Assert.Equal(new Vector3(0.0, 1.0, 0.0), boresightICRF, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetRefVectorInICRF()
        {
            TimeSystem.Time start = TimeSystem.Time.J2000TDB;
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);
            Spacecraft spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0, new Clock("clk1", 65536), parkingOrbit);
            spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, System.Math.PI * 0.5, 0.0));

            // The rotation of pi/2 about +Y leaves the reference vector +Y, and the attitude is the identity
            var instrument = spacecraft.Instruments.First();
            var refVectorICRF = instrument.GetRefVectorInICRFFrame(start);
            Assert.Equal(Vector3.VectorY, refVectorICRF, TestHelpers.VectorComparer);
            Assert.Equal(refVectorICRF, instrument.GetFieldOfViewAxesInICRFFrame(start).Ref, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetRefVectorInICRF2()
        {
            TimeSystem.Time start = TimeSystem.Time.J2000TDB;
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);
            Spacecraft spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0, new Clock("clk1", 65536), parkingOrbit);
            spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, System.Math.PI * 0.5, 0.0));
            spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(new Quaternion(Vector3.VectorZ, -Astrodynamics.Constants.PI2), Vector3.Zero, start, spacecraft.Frame));

            // The attitude takes the boresight -X to +Y (GetBoresightInICRF2): a rotation of -pi/2 about +Z, which takes +Y to +X
            var instrument = spacecraft.Instruments.First();
            var refVectorICRF = instrument.GetRefVectorInICRFFrame(start);
            Assert.Equal(Vector3.VectorX, refVectorICRF, TestHelpers.VectorComparer);
            var axes = instrument.GetFieldOfViewAxesInICRFFrame(start);
            Assert.Equal(Vector3.VectorY, axes.Boresight, TestHelpers.VectorComparer);
            Assert.Equal(refVectorICRF, axes.Ref, TestHelpers.VectorComparer);
            Assert.Equal(new Vector3(0.0, 0.0, -1.0), axes.Cross, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetRefVectorInSpacecraftFrame()
        {
            var spc = TestHelpers.Spacecraft;

            // Instrument with no orientation - refVector should remain unchanged
            spc.AddCircularInstrument(-1600, "inst1", "model", 1.57, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            var refVector1 = spc.Instruments.First().GetRefVectorInSpacecraftFrame();
            Assert.Equal(Vector3.VectorY, refVector1, TestHelpers.VectorComparer);

            // Euler angles (0, 0, pi/2) of the frame kernel (#524): pxform(instrument, spacecraft) = eul2m(0, 0, pi/2, 1, 2, 3),
            // the frame rotation [pi/2]_3, takes the reference vector X to -Y
            spc.AddCircularInstrument(-1700, "inst2", "model", 1.57, Vector3.VectorZ, Vector3.VectorX,
                new Vector3(0.0, 0.0, Astrodynamics.Constants.PI2));
            var refVector2 = spc.Instruments.ElementAt(1).GetRefVectorInSpacecraftFrame();
            Assert.Equal(new Vector3(0.0, -1.0, 0.0), refVector2, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetRefVectorInSpacecraftFrame_WithOrientation()
        {
            var spc = TestHelpers.Spacecraft;

            // Euler angles (0, pi/2, 0) of the frame kernel (#524): eul2m(0, pi/2, 0, 1, 2, 3), the frame rotation [pi/2]_2,
            // takes the reference vector X to +Z (and the boresight Z to -X, the managed path's value before #524)
            spc.AddCircularInstrument(-1600, "inst", "model", 1.57, Vector3.VectorZ, Vector3.VectorX,
                new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0));
            var refVector = spc.Instruments.First().GetRefVectorInSpacecraftFrame();
            Assert.Equal(new Vector3(0.0, 0.0, 1.0), refVector, TestHelpers.VectorComparer);
            Assert.Equal(new Vector3(-1.0, 0.0, 0.0), spc.Instruments.First().GetBoresightInSpacecraftFrame(), TestHelpers.VectorComparer);
        }

        [Fact]
        public async Task WriteFrame()
        {
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, new Clock("clk1", 256), new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0),
                TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF));
            spc.AddCircularInstrument(-1001600, "CAM600", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
            await spc.Instruments.First().WriteFrameAsync(new FileInfo("instrumentFrame.tf"));
            TextReader tr = new StreamReader("instrumentFrame.tf");
            var res = await tr.ReadToEndAsync();
            Assert.Equal(
                $"KPL/FK{Environment.NewLine}\\begindata{Environment.NewLine}FRAME_MYSPACECRAFT_CAM600             = -1001600{Environment.NewLine}FRAME_-1001600_NAME        = 'MYSPACECRAFT_CAM600'{Environment.NewLine}FRAME_-1001600_CLASS       = 4{Environment.NewLine}FRAME_-1001600_CLASS_ID    = -1001600{Environment.NewLine}FRAME_-1001600_CENTER      = -1001{Environment.NewLine}TKFRAME_-1001600_SPEC      = 'ANGLES'{Environment.NewLine}TKFRAME_-1001600_RELATIVE  = 'MYSPACECRAFT_FRAME'{Environment.NewLine}TKFRAME_-1001600_ANGLES    = ( 0,-1.5707963267948966,0 ){Environment.NewLine}TKFRAME_-1001600_AXES      = ( 1,    2,   3   ){Environment.NewLine}TKFRAME_-1001600_UNITS     = 'RADIANS'{Environment.NewLine}NAIF_BODY_NAME              += 'MYSPACECRAFT_CAM600'{Environment.NewLine}NAIF_BODY_CODE              += -1001600{Environment.NewLine}\\begintext{Environment.NewLine}",
                res);
        }

        [Fact]
        public async Task WriteKernel()
        {
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, new Clock("clk1", 256), new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0),
                TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF));
            spc.AddCircularInstrument(-1001600, "CAM600", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
            await spc.Instruments.First().WriteKernelAsync(new FileInfo("instrumentKernel.ti"));
            TextReader tr = new StreamReader("instrumentKernel.ti");
            var res = await tr.ReadToEndAsync();
            Assert.Equal(
                $"KPL/IK{Environment.NewLine}\\begindata{Environment.NewLine}INS-1001600_FOV_CLASS_SPEC       = 'ANGLES'{Environment.NewLine}INS-1001600_FOV_SHAPE            = 'CIRCLE'{Environment.NewLine}INS-1001600_FOV_FRAME            = 'MYSPACECRAFT_CAM600'{Environment.NewLine}INS-1001600_BORESIGHT            = ( 0, 0, 1 ){Environment.NewLine}INS-1001600_FOV_REF_VECTOR       = ( 0, 1, 0 ){Environment.NewLine}INS-1001600_FOV_REF_ANGLE        = 1.5{Environment.NewLine}INS-1001600_FOV_ANGLE_UNITS      = 'RADIANS'{Environment.NewLine}\\begintext{Environment.NewLine}",
                res);
        }

        // Twice the worst over the orientations below over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02):
        // 3.04e-16 / 4.67e-16 / 2.94e-16 rad, the vectors against pxform, of (-0.7, 1.1, 2.3) on Linux and macOS arm64, of
        // (0.3, +/-pi/2, +/-0.2) on Windows, whose pxform at y = +/-pi/2 is 4.4e-16 to 4.7e-16 rad from RotationToSpacecraft
        // (2.2e-16 to 2.5e-16 rad on the other two)
        private const double EulerAgainstFrameKernel = 9.4e-16;

        /// <summary>
        /// The rotation of <see cref="Instrument.Orientation"/> (#524) against the definition of Frames Required Reading,
        /// written in the test (M = [x]1 [y]2 [z]3 = Rx(-x) Ry(-y) Rz(-z), "Defining a TK Frame Using Euler Angles"), and
        /// against <c>pxform(instrument, spacecraft)</c> on the frame kernels the library writes: each axis alone in both
        /// senses, y = +/-pi/2 (where the angles about X and Z share an axis), and two general orientations; the boresight
        /// and the reference vector in the spacecraft frame with it.
        /// </summary>
        [Fact]
        public async Task RotationToSpacecraft_IsTheFrameKernel()
        {
            Vector3[] orientations =
            [
                Vector3.Zero, new(0.4, 0.0, 0.0), new(-0.4, 0.0, 0.0), new(0.0, 0.4, 0.0), new(0.0, -0.4, 0.0), new(0.0, 0.0, 0.4), new(0.0, 0.0, -0.4),
                new(0.0, Astrodynamics.Constants.PI2, 0.0), new(0.0, -Astrodynamics.Constants.PI2, 0.0), new(0.3, Astrodynamics.Constants.PI2, 0.2),
                new(0.3, -Astrodynamics.Constants.PI2, -0.2), new(0.3, 0.5, 0.2), new(-0.7, 1.1, 2.3)
            ];
            var start = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
            var spacecraft = new Spacecraft(-8601, "EUL8601", 1000.0, 3000.0, new Clock("EUL8601_CLK", 65536),
                new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000, start, Frames.Frame.ICRF));
            for (int i = 0; i < orientations.Length; i++)
            {
                spacecraft.AddCircularInstrument(-8601001 - i, $"E{i}", "m", 0.5, Vector3.VectorZ, Vector3.VectorY, orientations[i]);
            }

            string directory = "Data/User/AttitudeRecord537/EUL8601";
            Directory.CreateDirectory(directory);
            var files = new System.Collections.Generic.List<FileInfo> { new(Path.Combine(directory, "eul8601.tf")) };
            await ((SpacecraftFrame)spacecraft.Frame).WriteAsync(files[0]);
            foreach (var instrument in spacecraft.Instruments)
            {
                var file = new FileInfo(Path.Combine(directory, $"eul8601_{instrument.Name.ToLowerInvariant()}.tf"));
                await instrument.WriteFrameAsync(file);
                files.Add(file);
            }

            files.ForEach(x => SpiceAPI.Instance.LoadKernels(x));
            try
            {
                double worst = 0.0;
                foreach (var instrument in spacecraft.Instruments)
                {
                    var o = instrument.Orientation;
                    double definition = AttitudeRecordTests.RotationAngle(v => AttitudeRecordTests.EulerToSpacecraft(o, v), v => v.Rotate(instrument.RotationToSpacecraft));
                    var pxform = SpiceAPI.Instance.TransformFrame(start, new Frames.Frame("EUL8601_" + instrument.Name), spacecraft.Frame).Rotation;
                    double kernel = AttitudeRecordTests.RotationAngle(pxform, instrument.RotationToSpacecraft);
                    double vectors = System.Math.Max(AttitudeRecordTests.Angle(instrument.GetBoresightInSpacecraftFrame(), instrument.Boresight.Rotate(pxform)),
                        AttitudeRecordTests.Angle(instrument.GetRefVectorInSpacecraftFrame(), instrument.RefVector.Rotate(pxform)));
                    _output.WriteLine($"MEASURE #524 Euler ({o.X}, {o.Y}, {o.Z}): RotationToSpacecraft - Frames Required Reading {definition:E2} rad, - pxform {kernel:E2} rad, the vectors - pxform {vectors:E2} rad");
                    worst = System.Math.Max(worst, System.Math.Max(definition, System.Math.Max(kernel, vectors)));
                }

                Assert.True(worst <= EulerAgainstFrameKernel, $"{worst:E3} rad");
            }
            finally
            {
                files.ForEach(x => SpiceAPI.Instance.UnloadKernels(x));
            }
        }

        private static Spacecraft Host(int naifId, string name) => new(naifId, name, 100.0, 1000.0, new Clock(name + "_CLK", 65536),
            new StateVector(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0), new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH),
                new TimeSystem.Time(2024, 1, 1), Frames.Frame.ICRF));

        /// <summary>
        /// The id of the spacecraft frame, NaifId × 1000, is not an instrument id (#676): the instrument FK would define the
        /// spacecraft frame relative to itself (SPICE(BADFRAMESPEC2), measured on main). The three methods reject it.
        /// </summary>
        [Theory]
        [InlineData("Circular")]
        [InlineData("Rectangular")]
        [InlineData("Elliptical")]
        public void AddInstrument_SpacecraftFrameId_Throws(string shape)
        {
            // Arrange
            var spc = Host(-1401, "KSAT");
            Assert.Equal(-1401000, spc.Frame.Id);

            // Act
            var exception = Record.Exception(() =>
            {
                switch (shape)
                {
                    case "Circular":
                        spc.AddCircularInstrument(-1401000, "CAM", "M", 0.1, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                        break;
                    case "Rectangular":
                        spc.AddRectangularInstrument(-1401000, "CAM", "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                        break;
                    default:
                        spc.AddEllipticalInstrument(-1401000, "CAM", "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                        break;
                }
            });

            // Assert
            var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
            Assert.Equal("naifId", rejected.ParamName);
            Assert.Contains("-1401000", rejected.Message);
            Assert.Empty(spc.Instruments);
        }

        /// <summary>
        /// Instrument and spacecraft names whose FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt; is no kernel variable SPICE
        /// can read (#676): a blank, or more than 32 bytes (Kernel Required Reading, "Variable Name Rules"). The writer throws
        /// and writes nothing.
        /// </summary>
        [Theory]
        [InlineData("HOST", "WIDE CAM")]
        [InlineData("ABCDEFGHIJKLM", "NOPQRSTUVWXYZ")]
        public async Task WriteFrame_NameSpiceCannotRead_Throws(string spacecraftName, string instrumentName)
        {
            // Arrange
            var spc = Host(-1321, spacecraftName);
            spc.AddCircularInstrument(-1321001, instrumentName, "M", 0.1, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            var fk = FrameKernelFiles.NewFile("instrument_refused");

            // Act
            var exception = await Record.ExceptionAsync(() => spc.Instruments.Single().WriteFrameAsync(fk));

            // Assert
            var rejected = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains(instrumentName, rejected.Message);
            fk.Refresh();
            Assert.False(fk.Exists);
        }

        /// <summary>
        /// The instrument kernels name the frame of the instrument FK, &lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;, in
        /// INS&lt;id&gt;_FOV_FRAME (#676): the three writers take the FK's rule. A quote ends the string value: the IK main
        /// writes for 'CAM'1' stops SPICE with SPICE(TYPEMISMATCH) at <c>furnsh_c</c> (measured on CSPICE N0067, which
        /// ended the process); a blank or a name over the FK's 32 bytes loads, but names a frame no FK can define. The
        /// writer throws and writes nothing.
        /// </summary>
        [Theory]
        [InlineData("Circular", "HOST", "CAM'1")]
        [InlineData("Rectangular", "HOST", "WIDE CAM")]
        [InlineData("Elliptical", "ABCDEFGHIJKLM", "NOPQRSTUVWXYZ")]
        [InlineData("Circular", "ABCDEFGHIJKLM", "NOPQRSTUVWXYZ")]
        [InlineData("Rectangular", "HOST", "CAM'1")]
        [InlineData("Elliptical", "HOST", "CAM=1")]
        public async Task WriteKernel_NameSpiceCannotRead_Throws(string shape, string spacecraftName, string instrumentName)
        {
            // Arrange
            var spc = Host(-1323, spacecraftName);
            AddInstrument(spc, shape, -1323001, instrumentName);
            var ik = FrameKernelFiles.NewFile("ik_refused", ".ti");

            // Act
            var exception = await Record.ExceptionAsync(() => spc.Instruments.Single().WriteKernelAsync(ik));

            // Assert
            var rejected = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains(instrumentName, rejected.Message);
            ik.Refresh();
            Assert.False(ik.Exists);
        }

        /// <summary>
        /// Control (#676): at the bound, FRAME_ + 12 + _ + 13 = 32 bytes, the three IKs are written and load in SPICE, their
        /// FOV frame the name the instrument FK defines.
        /// </summary>
        [Theory]
        [InlineData("Circular")]
        [InlineData("Rectangular")]
        [InlineData("Elliptical")]
        public async Task WriteKernel_BoundaryNames_LoadInSpice(string shape)
        {
            // Arrange
            var spc = Host(-1324, "ABCDEFGHIJKL");
            AddInstrument(spc, shape, -1324001, "NOPQRSTUVWXYZ");
            var ik = FrameKernelFiles.NewFile("ik_boundary", ".ti");

            // Act
            await spc.Instruments.Single().WriteKernelAsync(ik);

            // Assert
            Assert.Contains("INS-1324001_FOV_FRAME            = 'ABCDEFGHIJKL_NOPQRSTUVWXYZ'", await System.IO.File.ReadAllTextAsync(ik.FullName));
            FrameKernelFiles.Load(ik);
        }

        private static void AddInstrument(Spacecraft spc, string shape, int naifId, string name)
        {
            switch (shape)
            {
                case "Circular":
                    spc.AddCircularInstrument(naifId, name, "M", 0.1, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                    break;
                case "Rectangular":
                    spc.AddRectangularInstrument(naifId, name, "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                    break;
                default:
                    spc.AddEllipticalInstrument(naifId, name, "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                    break;
            }
        }

        /// <summary>
        /// A pair of 25 characters, FRAME_ + 12 + _ + 13 = 32 bytes: the instrument FK loads in SPICE with the spacecraft FK
        /// it is relative to (#676).
        /// </summary>
        [Fact]
        public async Task WriteFrame_BoundaryNames_LoadInSpice()
        {
            // Arrange
            var spc = Host(-1322, "ABCDEFGHIJKL");
            spc.AddCircularInstrument(-1322001, "NOPQRSTUVWXYZ", "M", 0.1, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            var spacecraftFk = FrameKernelFiles.NewFile("instrument_host");
            var fk = FrameKernelFiles.NewFile("instrument_boundary");
            Assert.Equal(32, System.Text.Encoding.UTF8.GetByteCount("FRAME_ABCDEFGHIJKL_NOPQRSTUVWXYZ"));

            // Act
            await spc.Frame.WriteAsync(spacecraftFk);
            await spc.Instruments.Single().WriteFrameAsync(fk);

            // Assert: both load without error
            FrameKernelFiles.Load(spacecraftFk, () => FrameKernelFiles.Load(fk));
        }

        // The shapes of the three methods that add an instrument, all of which go through the constructor of Instrument
        public static readonly string[] Shapes = { "Circular", "Rectangular", "Elliptical" };

        private static void AddInstrument(Spacecraft spc, string shape, int naifId, double fieldOfView, Vector3 boresight, Vector3 refVector)
        {
            switch (shape)
            {
                case "Circular":
                    spc.AddCircularInstrument(naifId, "CAM", "M", fieldOfView, boresight, refVector, Vector3.Zero);
                    break;
                case "Rectangular":
                    spc.AddRectangularInstrument(naifId, "CAM", "M", fieldOfView, 0.05, boresight, refVector, Vector3.Zero);
                    break;
                default:
                    spc.AddEllipticalInstrument(naifId, "CAM", "M", fieldOfView, 0.05, boresight, refVector, Vector3.Zero);
                    break;
            }
        }

        // A vector with no direction: zero, or a component that is not finite, in each component
        private static readonly Vector3[] VectorsWithoutDirection =
        {
            Vector3.Zero, new(-0.0, -0.0, -0.0),
            new(double.NaN, 0.0, 1.0), new(0.0, double.NaN, 1.0), new(0.0, 0.0, double.NaN),
            new(double.PositiveInfinity, 0.0, 1.0), new(0.0, double.PositiveInfinity, 1.0), new(0.0, 0.0, double.PositiveInfinity),
            new(double.NegativeInfinity, 0.0, 1.0), new(0.0, double.NegativeInfinity, 1.0), new(0.0, 0.0, double.NegativeInfinity),
        };

        public static TheoryData<string, int> ShapesAndVectorsWithoutDirection()
        {
            var data = new TheoryData<string, int>();
            foreach (var shape in Shapes)
            for (int i = 0; i < VectorsWithoutDirection.Length; i++)
                data.Add(shape, i);
            return data;
        }

        /// <summary>
        /// #707: a boresight of magnitude 0 or with a component that is not finite has no direction. <c>getfov</c> rejects the
        /// zero boresight (SPICE(ZEROBORESIGHT), <see cref="RejectedFields_AreThoseGetfovRejects"/>) and SPICE reads no NaN or
        /// infinity in a kernel; the rule of the body axes of a spacecraft and of the body vectors of a TRIAD (#703). Before,
        /// the instrument was built: its getters returned the zero or NaN vector, InstrumentPointingToAttitude the identity or a
        /// NaN attitude, the IK writer a kernel SPICE rejects, and the search threw late (an unrelated ArgumentException for
        /// NaN and infinity). The three methods throw, naming the boresight, and add nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(ShapesAndVectorsWithoutDirection))]
        public void AddInstrument_BoresightNotFiniteOrZero_Throws(string shape, int vector)
        {
            // Arrange
            var spc = Host(-7071, "B707");

            // Act
            var exception = Record.Exception(() => AddInstrument(spc, shape, -7071001, 0.1, VectorsWithoutDirection[vector], Vector3.VectorX));

            // Assert
            var rejected = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("boresight", rejected.ParamName);
            Assert.Empty(spc.Instruments);
        }

        /// <summary>
        /// #707: a reference vector of magnitude 0 or with a component that is not finite: <c>getfov</c> rejects the zero
        /// vector as parallel to the boresight (|b × 0| = 0, SPICE(BADREFVECTORSPEC)) and SPICE reads no NaN or infinity in
        /// a kernel. The three methods throw, naming the reference vector, and add nothing; the boresight is checked first.
        /// </summary>
        [Theory]
        [MemberData(nameof(ShapesAndVectorsWithoutDirection))]
        public void AddInstrument_RefVectorNotFiniteOrZero_Throws(string shape, int vector)
        {
            // Arrange
            var spc = Host(-7072, "R707");
            var refVector = VectorsWithoutDirection[vector];
            // Its components along X, so that a finite one is never parallel to the boresight +Z
            refVector = new Vector3(refVector.Z, refVector.Y, refVector.X);

            // Act
            var exception = Record.Exception(() => AddInstrument(spc, shape, -7072001, 0.1, Vector3.VectorZ, refVector));
            var both = Record.Exception(() => AddInstrument(spc, shape, -7072002, 0.1, Vector3.Zero, refVector));

            // Assert
            var rejected = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("refVector", rejected.ParamName);
            Assert.Equal("boresight", Assert.IsType<ArgumentException>(both).ParamName);
            Assert.Empty(spc.Instruments);
        }

        public static TheoryData<string, double, double, double, double, double, double> ShapesAndParallelPairs()
        {
            var data = new TheoryData<string, double, double, double, double, double, double>();
            foreach (var shape in Shapes)
            {
                data.Add(shape, 0.0, 0.0, 1.0, 0.0, 0.0, 2.0);
                data.Add(shape, 0.0, 0.0, 1.0, 0.0, 0.0, -3.0);
                data.Add(shape, 1.0, 1.0, 1.0, 3.0, 3.0, 3.0);
                data.Add(shape, 0.0, 0.0, 1.0, 0.0, 0.0, 1e-300);
            }

            return data;
        }

        /// <summary>
        /// #707: a reference vector parallel to the boresight, |b × r| = 0, the rule of <c>getfov</c> (N0067, "The reference
        /// vector ... is parallel to the instrument boresight vector. This is not allowed by the ANGLES FOV specification",
        /// SPICE(BADREFVECTORSPEC)), defines no plane of the field. Before, the instrument was built, the frame of its field
        /// was the zero vector, its IK one getfov rejects, and only the search threw. The three methods throw, naming the
        /// reference vector, and add nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(ShapesAndParallelPairs))]
        public void AddInstrument_RefVectorParallelToBoresight_Throws(string shape, double bx, double by, double bz, double rx, double ry, double rz)
        {
            // Arrange
            var spc = Host(-7073, "P707");

            // Act
            var exception = Record.Exception(() => AddInstrument(spc, shape, -7073001, 0.1, new Vector3(bx, by, bz), new Vector3(rx, ry, rz)));

            // Assert
            var rejected = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("refVector", rejected.ParamName);
            Assert.Empty(spc.Instruments);
        }

        /// <summary>
        /// #707, control: a reference vector whose cross product with the boresight is not zero, however small, is accepted,
        /// as <c>getfov</c> accepts it (<see cref="RejectedFields_AreThoseGetfovRejects"/>): 1e-300 off the boresight, the
        /// smallest double off it, and a vector opposite the boresight but for 1e-300.
        /// </summary>
        [Theory]
        [InlineData("Circular", 1e-300)]
        [InlineData("Rectangular", 1e-300)]
        [InlineData("Elliptical", 1e-300)]
        [InlineData("Circular", double.Epsilon)]
        [InlineData("Rectangular", double.Epsilon)]
        [InlineData("Elliptical", double.Epsilon)]
        public void AddInstrument_RefVectorNearlyParallel_IsAccepted(string shape, double offset)
        {
            // Arrange
            var spc = Host(-7074, "N707");
            Assert.True(Vector3.VectorZ.Cross(new Vector3(0.0, offset, 1.0)).Magnitude() > 0.0);

            // Act
            AddInstrument(spc, shape, -7074001, 0.1, Vector3.VectorZ, new Vector3(0.0, offset, 1.0));
            AddInstrument(spc, shape, -7074002, 0.1, Vector3.VectorZ, new Vector3(0.0, offset, -1.0));

            // Assert
            Assert.Equal(2, spc.Instruments.Count);
            Assert.Equal(new Vector3(0.0, offset, 1.0), spc.Instruments.Single(x => x.NaifId == -7074001).RefVector);
        }

        public static TheoryData<string, double> ShapesAndFieldsOfViewNotFinitePositive()
        {
            var data = new TheoryData<string, double>();
            foreach (var shape in Shapes)
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, -0.1 })
                data.Add(shape, value);
            return data;
        }

        /// <summary>
        /// #707: the half-angle of the field of view is a finite positive number. NaN passed the former check (NaN &lt;= 0 is
        /// false) and +∞ was not checked: the instrument was built, the search threw late and the IK held
        /// <c>FOV_REF_ANGLE = NaN</c> or <c>Infinity</c>, which SPICE cannot read. The three methods throw, naming the field
        /// of view, and add nothing; 0 and negative values threw before, without the parameter's name.
        /// </summary>
        [Theory]
        [MemberData(nameof(ShapesAndFieldsOfViewNotFinitePositive))]
        public void AddInstrument_FieldOfViewNotFinitePositive_Throws(string shape, double fieldOfView)
        {
            // Arrange
            var spc = Host(-7075, "F707");

            // Act
            var exception = Record.Exception(() => AddInstrument(spc, shape, -7075001, fieldOfView, Vector3.VectorZ, Vector3.VectorX));

            // Assert
            var rejected = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("fieldOfView", rejected.ParamName);
            Assert.Empty(spc.Instruments);
        }

        public static TheoryData<string, long> CrossShapesAndCrossAnglesNotFinite()
        {
            var data = new TheoryData<string, long>();
            foreach (var shape in new[] { "Rectangular", "Elliptical" })
            foreach (var bits in new[]
                     {
                         BitConverter.DoubleToInt64Bits(double.PositiveInfinity), BitConverter.DoubleToInt64Bits(double.NegativeInfinity),
                         BitConverter.DoubleToInt64Bits(double.NaN), 0x7FF8000000000000L
                     })
                data.Add(shape, bits);
            return data;
        }

        /// <summary>
        /// #707: the half-angle across the reference plane of a rectangle or an ellipse is finite. +∞ and a NaN of positive
        /// sign (0x7FF8000000000000; <c>double.NaN</c> has the sign bit set) passed <c>ThrowIfNegative</c>: the instrument was
        /// built and its IK held <c>FOV_CROSS_ANGLE = Infinity</c> or <c>NaN</c>, which SPICE cannot read (loading such an IK
        /// signals SPICE(NUMBEREXPECTED), "Encountered 'Infinity' while attempting to parse a number"), the search throwing only
        /// when called. Both methods throw <see cref="ArgumentOutOfRangeException"/> naming the cross angle, as for a negative
        /// one, and add nothing; a cross angle of 0 too since #715 (<see cref="AddInstrument_CrossAngleNotPositive_Throws"/>).
        /// </summary>
        [Theory]
        [MemberData(nameof(CrossShapesAndCrossAnglesNotFinite))]
        public void AddInstrument_CrossAngleNotFinite_Throws(string shape, long bits)
        {
            // Arrange
            var spc = Host(-7078, "C707");
            double crossAngle = BitConverter.Int64BitsToDouble(bits);

            // Act
            var exception = Record.Exception(() =>
            {
                if (shape == "Rectangular")
                    spc.AddRectangularInstrument(-7078001, "CAM", "M", 0.1, crossAngle, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                else
                    spc.AddEllipticalInstrument(-7078001, "CAM", "M", 0.1, crossAngle, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            });

            // Assert
            var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
            Assert.Equal("crossAngle", rejected.ParamName);
            Assert.Empty(spc.Instruments);
        }

        public static TheoryData<string, long> CrossShapesAndCrossAnglesNotPositive()
        {
            var data = new TheoryData<string, long>();
            foreach (var shape in new[] { "Rectangular", "Elliptical" })
            foreach (var value in new[] { 0.0, -0.0, -double.Epsilon, -0.1 })
                data.Add(shape, BitConverter.DoubleToInt64Bits(value));
            return data;
        }

        /// <summary>
        /// #715: the half-angle across the reference plane of a rectangle or an ellipse is positive, as the field of view is
        /// (#707). A cross angle of +0 was accepted: the instrument was built and its IK written with
        /// <c>FOV_CROSS_ANGLE = 0</c>, which CSPICE's <c>gftfov</c> rejects (SPICE(DEGENERATECASE),
        /// <see cref="ZeroHalfAngle_IsDegenerateForGftfov"/>), and every search threw late, "its half-angles must be positive";
        /// −0 was rejected by <c>ThrowIfNegative</c> with another message. Both methods throw
        /// <see cref="ArgumentOutOfRangeException"/> naming the cross angle, with one message for +0, −0 and a negative value,
        /// and add nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(CrossShapesAndCrossAnglesNotPositive))]
        public void AddInstrument_CrossAngleNotPositive_Throws(string shape, long bits)
        {
            // Arrange
            var spc = Host(-7151, "Z715");
            double crossAngle = BitConverter.Int64BitsToDouble(bits);

            // Act
            var exception = Record.Exception(() =>
            {
                if (shape == "Rectangular")
                    spc.AddRectangularInstrument(-7151001, "CAM", "M", 0.1, crossAngle, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
                else
                    spc.AddEllipticalInstrument(-7151001, "CAM", "M", 0.1, crossAngle, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            });

            // Assert
            var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
            Assert.Equal("crossAngle", rejected.ParamName);
            Assert.StartsWith("The cross angle of an instrument must be a finite positive number", rejected.Message);
            Assert.Empty(spc.Instruments);
        }

        /// <summary>
        /// #715, control: the smallest positive cross angle and the largest finite one are accepted, as the field of view's
        /// rule accepts them (#707); the bound of <c>gftfov</c>, π/2 − 1e-6, is the search's.
        /// </summary>
        [Theory]
        [InlineData("Rectangular", double.Epsilon)]
        [InlineData("Elliptical", double.Epsilon)]
        [InlineData("Rectangular", double.MaxValue)]
        [InlineData("Elliptical", double.MaxValue)]
        public void AddInstrument_CrossAnglePositive_IsAccepted(string shape, double crossAngle)
        {
            // Arrange
            var spc = Host(-7152, "P715");

            // Act
            if (shape == "Rectangular")
                spc.AddRectangularInstrument(-7152001, "CAM", "M", 0.1, crossAngle, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            else
                spc.AddEllipticalInstrument(-7152001, "CAM", "M", 0.1, crossAngle, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);

            // Assert
            var instrument = Assert.Single(spc.Instruments);
            Assert.Equal(crossAngle, instrument is RectangularInstrument r ? r.CrossAngle : ((EllipticalInstrument)instrument).CrossAngle);
        }

        // Euler angles with a component that is not finite, in each component
        private static readonly Vector3[] OrientationsNotFinite =
        {
            new(double.NaN, 0.0, 0.0), new(0.0, double.NaN, 0.0), new(0.0, 0.0, double.NaN),
            new(double.PositiveInfinity, 0.0, 0.0), new(0.0, double.PositiveInfinity, 0.0), new(0.0, 0.0, double.PositiveInfinity),
            new(double.NegativeInfinity, 0.0, 0.0), new(0.0, double.NegativeInfinity, 0.0), new(0.0, 0.0, double.NegativeInfinity),
            new(double.NaN, double.NaN, double.NaN), new(0.1, BitConverter.Int64BitsToDouble(0x7FF8000000000000L), 0.3),
        };

        public static TheoryData<string, int> ShapesAndOrientationsNotFinite()
        {
            var data = new TheoryData<string, int>();
            foreach (var shape in Shapes)
            for (int i = 0; i < OrientationsNotFinite.Length; i++)
                data.Add(shape, i);
            return data;
        }

        /// <summary>
        /// #715: the Euler angles of the orientation are finite. A NaN or an infinite angle was accepted: the instrument was
        /// built with a NaN <see cref="Instrument.RotationToSpacecraft"/>, its spacecraft-frame getters returned NaN vectors,
        /// and its frame kernel held <c>TKFRAME_..._ANGLES = ( NaN,0,0 )</c>, which SPICE cannot read (SPICE(NUMBEREXPECTED)
        /// at <c>furnsh_c</c>, the variables before the line left in the pool). The three methods throw
        /// <see cref="ArgumentException"/> naming the orientation, the rule of the boresight and the reference vector (#707)
        /// and of the body axes (#703), and add nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(ShapesAndOrientationsNotFinite))]
        public void AddInstrument_OrientationNotFinite_Throws(string shape, int orientation)
        {
            // Arrange
            var spc = Host(-7153, "O715");
            var angles = OrientationsNotFinite[orientation];

            // Act
            var exception = Record.Exception(() =>
            {
                switch (shape)
                {
                    case "Circular":
                        spc.AddCircularInstrument(-7153001, "CAM", "M", 0.1, Vector3.VectorZ, Vector3.VectorX, angles);
                        break;
                    case "Rectangular":
                        spc.AddRectangularInstrument(-7153001, "CAM", "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, angles);
                        break;
                    default:
                        spc.AddEllipticalInstrument(-7153001, "CAM", "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, angles);
                        break;
                }
            });

            // Assert
            var rejected = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("orientation", rejected.ParamName);
            Assert.Empty(spc.Instruments);
        }

        /// <summary>
        /// #715, control: finite Euler angles stay accepted, the zero, the angles of a turn and above, and the largest finite
        /// angles (question 2 of the issue: SPICE's TK frame then agrees with <see cref="Instrument.RotationToSpacecraft"/> to a
        /// few ulps of the angle, which the documentation states). The rotation is <c>eul2m(x, y, z, 1, 2, 3)</c>, its closed
        /// form [x]1 [y]2 [z]3 checked on a vector by <see cref="RotationToSpacecraft_IsTheFrameKernel"/>; here it is finite.
        /// </summary>
        [Theory]
        [InlineData(0.0, 0.0, 0.0)]
        [InlineData(-0.0, -0.0, -0.0)]
        [InlineData(7.0, -10.0, 100.0)]
        [InlineData(double.MaxValue, double.MinValue, double.Epsilon)]
        public void AddInstrument_OrientationFinite_IsAccepted(double x, double y, double z)
        {
            // Arrange
            var spc = Host(-7154, "F715");

            // Act
            foreach (var (shape, n) in Shapes.Select((s, i) => (s, i)))
            {
                switch (shape)
                {
                    case "Circular":
                        spc.AddCircularInstrument(-7154001 - n, "C" + n, "M", 0.1, Vector3.VectorZ, Vector3.VectorX, new Vector3(x, y, z));
                        break;
                    case "Rectangular":
                        spc.AddRectangularInstrument(-7154001 - n, "C" + n, "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, new Vector3(x, y, z));
                        break;
                    default:
                        spc.AddEllipticalInstrument(-7154001 - n, "C" + n, "M", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, new Vector3(x, y, z));
                        break;
                }
            }

            // Assert
            Assert.Equal(3, spc.Instruments.Count);
            foreach (var instrument in spc.Instruments)
            {
                Assert.Equal(new Vector3(x, y, z), instrument.Orientation);
                var q = instrument.RotationToSpacecraft;
                Assert.True(double.IsFinite(q.W) && double.IsFinite(q.VectorPart.X) && double.IsFinite(q.VectorPart.Y) && double.IsFinite(q.VectorPart.Z));
                Assert.Equal(1.0, q.Magnitude(), 15);
            }
        }

        // An instrument kernel of the layout of the library's rectangular and elliptical writers, in J2000
        private static string AnglesKernel(int id, string shape, Vector3 boresight, Vector3 refVector, double refAngle, double crossAngle)
        {
            string N(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return $"KPL/IK{Environment.NewLine}\\begindata{Environment.NewLine}" +
                   $"INS{id}_FOV_CLASS_SPEC       = 'ANGLES'{Environment.NewLine}" +
                   $"INS{id}_FOV_SHAPE            = '{shape}'{Environment.NewLine}" +
                   $"INS{id}_FOV_FRAME            = 'J2000'{Environment.NewLine}" +
                   $"INS{id}_BORESIGHT            = ( {N(boresight.X)}, {N(boresight.Y)}, {N(boresight.Z)} ){Environment.NewLine}" +
                   $"INS{id}_FOV_REF_VECTOR       = ( {N(refVector.X)}, {N(refVector.Y)}, {N(refVector.Z)} ){Environment.NewLine}" +
                   $"INS{id}_FOV_REF_ANGLE        = {N(refAngle)}{Environment.NewLine}" +
                   $"INS{id}_FOV_CROSS_ANGLE      = {N(crossAngle)}{Environment.NewLine}" +
                   $"INS{id}_FOV_ANGLE_UNITS      = 'RADIANS'{Environment.NewLine}\\begintext{Environment.NewLine}";
        }

        /// <summary>
        /// #715, the reference: a half-angle of 0, across (<c>FOV_CROSS_ANGLE</c>) or in the reference plane
        /// (<c>FOV_REF_ANGLE</c>), is a field CSPICE's <c>gftfov</c> rejects, for a point and an ellipsoid target, with
        /// SPICE(DEGENERATECASE): consecutive boundary vectors of the rectangle whose cross product is the zero vector
        /// (<c>zzfovaxi</c>, N0067), whatever the boresight, and a semi-axis of the ellipse of zero length (<c>zzgffvu</c>,
        /// N0067) for the boresight +Z. The IKs are written here in J2000, in the layout of the library's writers, for an
        /// instrument of the Earth observing the Moon; a control field of half-angles 0.1 with the boresight at the Moon holds
        /// it for the whole search window. The ellipse's test is exact: with the boresight at the Moon (a unit vector rounded
        /// from its ephemeris), the computed semi-axis of zero width is not exactly of zero length and <c>gftfov</c> searches a
        /// segment: a window of 1 µs for the Moon's centre and of 59.8 min for its ellipsoid (measured on Linux, 2026-11-16). Through the
        /// library's proxy of <c>gftfov_c</c>, which carries SPICE's long message (fourth round of #668), so the test runs on
        /// the three platforms; <c>fovtrg_c</c> is not linked into the native library.
        /// </summary>
        [Theory]
        [InlineData("RECTANGLE", true, 0.1, 0.0, ShapeType.Point, "vectors at indices 2 and 3 have cross product equal to the zero vector")]
        [InlineData("RECTANGLE", true, 0.1, 0.0, ShapeType.Ellipsoid, "vectors at indices 2 and 3 have cross product equal to the zero vector")]
        [InlineData("RECTANGLE", true, 0.0, 0.1, ShapeType.Point, "vectors at indices 1 and 2 have cross product equal to the zero vector")]
        [InlineData("RECTANGLE", false, 0.1, 0.0, ShapeType.Point, "vectors at indices 2 and 3 have cross product equal to the zero vector")]
        [InlineData("ELLIPSE", false, 0.1, 0.0, ShapeType.Point, "FOV semi-axis #2 for -7155001 has zero length")]
        [InlineData("ELLIPSE", false, 0.1, 0.0, ShapeType.Ellipsoid, "FOV semi-axis #2 for -7155001 has zero length")]
        [InlineData("ELLIPSE", false, 0.0, 0.1, ShapeType.Point, "FOV semi-axis #1 for -7155001 has zero length")]
        [InlineData("RECTANGLE", true, 0.1, 0.1, ShapeType.Point, null)]
        [InlineData("ELLIPSE", true, 0.1, 0.1, ShapeType.Ellipsoid, null)]
        public async Task ZeroHalfAngle_IsDegenerateForGftfov(string shape, bool atMoon, double refAngle, double crossAngle, ShapeType targetShape, string error)
        {
            // Arrange
            var epoch = new TimeSystem.Time(2024, 1, 1, frame: TimeFrame.TDBFrame);
            var earth = new CelestialBody(399);
            var moon = new CelestialBody(301);
            var boresight = atMoon ? moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position.Normalize() : Vector3.VectorZ;
            var refVector = atMoon ? boresight.Cross(Vector3.VectorZ).Normalize() : Vector3.VectorX;
            int id = -7155001;
            var ik = FrameKernelFiles.NewFile("ik715", ".ti");
            await File.WriteAllTextAsync(ik.FullName, AnglesKernel(id, shape, boresight, refVector, refAngle, crossAngle));
            var window = new Window(epoch.AddSeconds(-3600.0), epoch.AddSeconds(3600.0));

            // Act
            Exception exception = null;
            System.Collections.Generic.List<Window> windows = null;
            FrameKernelFiles.Load(ik, () => exception = Record.Exception(() => windows = SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(window, 399, id, 301,
                new Frames.Frame("IAU_MOON"), targetShape, Aberration.None, TimeSpan.FromSeconds(60.0)).ToList()));

            // Assert
            _output.WriteLine($"MEASURE gftfov {shape} boresight {boresight} ref {refAngle} cross {crossAngle} {targetShape}: {exception?.Message ?? $"{windows.Count} window(s) " + string.Join(", ", windows.Select(w => $"[{w.StartDate}, {w.EndDate}]"))}");
            if (error == null)
            {
                Assert.Null(exception);
                Assert.Equal(window, Assert.Single(windows));
            }
            else
            {
                var failed = Assert.IsType<InvalidOperationException>(exception);
                Assert.Contains(error, failed.Message);
            }
        }

        /// <summary>
        /// #715, question 1: the managed search of a circle or an ellipse builds an ellipsoid whose limb is the cone of the
        /// field, of semi-axes D sin REF and D sin REF · tan CROSS / tan REF at the distance D of the target
        /// (<c>FieldOfViewGeometry.Cone</c>). For half-angles this small the square of a semi-axis underflows to 0 (IEEE 754:
        /// at the Moon's distance, D ≈ 3.6e8 m, D sin 1e-200 ≈ 4e-192 m, whose square is below the smallest subnormal,
        /// 4.9e-324), and the ellipsoid of the occultation search rejected it with <see cref="ArgumentException"/>, "The
        /// semi-axes of an ellipsoid must have positive lengths", not the <see cref="InvalidOperationException"/> of a field
        /// that cannot be searched. <c>gftfov</c> is already degenerate far above (SPICE(DEGENERATECASE) up to about 1.5e-17 rad
        /// with the boresight at the Moon, #715). <see cref="Instrument.IsInFOV"/> and the managed search, for a point and an
        /// ellipsoid target, throw <see cref="InvalidOperationException"/> naming the underflow. Control: a half-angle of 1e-10
        /// is searched, the Moon at the boresight in view (its margin is the half-angle, less the rounding of the axes,
        /// about 1e-16 rad).
        /// </summary>
        [Theory]
        [InlineData("Circular", 1e-200, 1e-200, false)]
        [InlineData("Circular", double.Epsilon, double.Epsilon, false)]
        [InlineData("Elliptical", 0.1, 1e-200, false)]
        [InlineData("Elliptical", 1e-200, 0.1, false)]
        [InlineData("Elliptical", 0.1, double.Epsilon, false)]
        [InlineData("Elliptical", double.Epsilon, 0.1, false)]
        [InlineData("Circular", 1e-10, 1e-10, true)]
        [InlineData("Elliptical", 0.1, 1e-10, true)]
        [InlineData("Elliptical", 1e-10, 0.1, true)]
        public void TinyHalfAngle_ManagedSearchThrowsInvalidOperation(string shape, double refAngle, double crossAngle, bool searched)
        {
            // Arrange: a managed spacecraft with an attitude, its instrument's boresight at the Moon
            var epoch = new TimeSystem.Time(2024, 1, 1);
            var spc = Host(-7156, "T715");
            spc.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, epoch, spc.Frame));
            var moon = new CelestialBody(301);
            var boresight = moon.GetEphemeris(epoch, spc, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position.Normalize();
            var refVector = boresight.Cross(Vector3.VectorZ).Normalize();
            if (shape == "Circular")
                spc.AddCircularInstrument(-7156001, "CAM", "M", refAngle, boresight, refVector, Vector3.Zero);
            else
                spc.AddEllipticalInstrument(-7156001, "CAM", "M", refAngle, crossAngle, boresight, refVector, Vector3.Zero);
            var instrument = spc.Instruments.Single();
            var window = new Window(epoch, epoch.AddSeconds(120.0));

            // Act
            bool? inView = null;
            var point = Record.Exception(() => inView = instrument.IsInFOV(epoch, moon, Aberration.None));
            System.Collections.Generic.List<Window> windows = null;
            var ellipsoid = Record.Exception(() => windows = instrument.FindWindowsInFieldOfViewConstraint(window, spc, moon, moon.Frame, ShapeType.Ellipsoid,
                Aberration.None, TimeSpan.FromSeconds(60.0)).ToList());

            // Assert
            if (searched)
            {
                Assert.Null(point);
                Assert.True(inView);
                Assert.Null(ellipsoid);
                Assert.Equal(window, Assert.Single(windows));
            }
            else
            {
                Assert.Contains("underflow", Assert.IsType<InvalidOperationException>(point).Message);
                Assert.Contains("underflow", Assert.IsType<InvalidOperationException>(ellipsoid).Message);
            }
        }

        /// <summary>
        /// #715, review: a point target at the observer, distance exactly 0 (two managed spacecraft of the same state), has no
        /// direction, so it is neither in nor out of the field (the angle between the zero vector and the boresight is
        /// undefined). The cone of a circle or an ellipse at that distance has semi-axes of 0 m: on a80f6a5 the ellipsoid of the
        /// occultation search threw an unrelated <see cref="ArgumentException"/>, "The semi-axes of an ellipsoid must have
        /// positive lengths", and after the underflow guard of #715 an <see cref="InvalidOperationException"/> that blamed
        /// the half-angles of a normal field (0.1 rad). <see cref="Instrument.IsInFOV"/> throws
        /// <see cref="InvalidOperationException"/> naming the target at the observer, not the field.
        /// </summary>
        [Theory]
        [InlineData("Circular")]
        [InlineData("Elliptical")]
        public void PointTargetAtTheObserver_ThrowsInvalidOperationNamingTheDistance(string shape)
        {
            // Arrange: two managed spacecraft of the same state, the observer with an attitude and a field of 0.1 rad
            var epoch = new TimeSystem.Time(2024, 1, 1);
            var spc = Host(-7157, "T715A");
            var twin = Host(-7158, "T715B");
            spc.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, epoch, spc.Frame));
            if (shape == "Circular")
                spc.AddCircularInstrument(-7157001, "CAM", "M", 0.1, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            else
                spc.AddEllipticalInstrument(-7157001, "CAM", "M", 0.1, 0.2, Vector3.VectorZ, Vector3.VectorX, Vector3.Zero);
            var instrument = spc.Instruments.Single();
            Assert.Equal(0.0, twin.GetEphemeris(epoch, spc, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position.Magnitude());

            // Act
            var exception = Record.Exception(() => instrument.IsInFOV(epoch, twin, Aberration.None));

            // Assert
            var failed = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("at the observer", failed.Message);
            Assert.DoesNotContain("half-angles", failed.Message);
        }

        // The instrument kernel the library's circular writer would write for these vectors, written here
        private static string CircularKernel(int id, string boresight, string refVector) =>
            $"KPL/IK{Environment.NewLine}\\begindata{Environment.NewLine}" +
            $"INS{id}_FOV_CLASS_SPEC       = 'ANGLES'{Environment.NewLine}" +
            $"INS{id}_FOV_SHAPE            = 'CIRCLE'{Environment.NewLine}" +
            $"INS{id}_FOV_FRAME            = 'G707_CAM'{Environment.NewLine}" +
            $"INS{id}_BORESIGHT            = ( {boresight} ){Environment.NewLine}" +
            $"INS{id}_FOV_REF_VECTOR       = ( {refVector} ){Environment.NewLine}" +
            $"INS{id}_FOV_REF_ANGLE        = 0.1{Environment.NewLine}" +
            $"INS{id}_FOV_ANGLE_UNITS      = 'RADIANS'{Environment.NewLine}\\begintext{Environment.NewLine}";

        /// <summary>
        /// #707, the reference: the fields the constructor now rejects are those CSPICE's <c>getfov_c</c> rejects, on the
        /// IKs the library wrote for them before (written here, as the library can no longer build such an instrument): the
        /// zero boresight SPICE(ZEROBORESIGHT), the zero and the parallel reference vectors SPICE(BADREFVECTORSPEC). The
        /// instruments the constructor accepts at the bound, a reference vector 1e-300 and the smallest double off the
        /// boresight, written by the library's writers for the three shapes, are read by getfov. Validation category:
        /// <c>getfov_c</c> through <see cref="CspiceDirect"/>, which the Windows DLL does not export; the cross-platform tests
        /// above assert the constructor's rule.
        /// </summary>
        [Fact]
        [Trait("Category", "Validation")]
        public async Task RejectedFields_AreThoseGetfovRejects()
        {
            // Arrange: the rejected fields, each with the error getfov signals
            var rejected = new (int Id, string Boresight, string RefVector, string Error)[]
            {
                (-7076001, "0, 0, 0", "1, 0, 0", "SPICE(ZEROBORESIGHT)"),
                (-7076002, "-0, -0, -0", "1, 0, 0", "SPICE(ZEROBORESIGHT)"),
                (-7076003, "0, 0, 1", "0, 0, 0", "SPICE(BADREFVECTORSPEC)"),
                (-7076004, "0, 0, 1", "0, 0, 2", "SPICE(BADREFVECTORSPEC)"),
                (-7076005, "0, 0, 1", "-0, -0, -3", "SPICE(BADREFVECTORSPEC)"),
                (-7076006, "1, 1, 1", "3, 3, 3", "SPICE(BADREFVECTORSPEC)"),
                (-7076007, "0, 0, 1", "0, 0, 1E-300", "SPICE(BADREFVECTORSPEC)"),
            };
            var spc = Host(-7077, "G707");
            foreach (var (offset, n) in new[] { (1e-300, 0), (double.Epsilon, 1) })
            {
                AddInstrument(spc, "Circular", -7077001 - 10 * n, 0.1, Vector3.VectorZ, new Vector3(0.0, offset, 1.0));
                AddInstrument(spc, "Rectangular", -7077002 - 10 * n, 0.1, Vector3.VectorZ, new Vector3(0.0, offset, 1.0));
                AddInstrument(spc, "Elliptical", -7077003 - 10 * n, 0.1, Vector3.VectorZ, new Vector3(0.0, offset, -1.0));
            }

            var files = new System.Collections.Generic.List<FileInfo>();
            foreach (var field in rejected)
            {
                var ik = FrameKernelFiles.NewFile($"ik707_{-field.Id}", ".ti");
                await File.WriteAllTextAsync(ik.FullName, CircularKernel(field.Id, field.Boresight, field.RefVector));
                files.Add(ik);
            }

            foreach (var instrument in spc.Instruments)
            {
                var ik = FrameKernelFiles.NewFile($"ik707_{-instrument.NaifId}", ".ti");
                await instrument.WriteKernelAsync(ik);
                files.Add(ik);
            }

            // Each kernel loaded inside the try, so that a failed load still unloads those already loaded and deletes every file
            var loaded = new System.Collections.Generic.List<FileInfo>();
            try
            {
                foreach (var file in files)
                {
                    file.Refresh();
                    SpiceAPI.Instance.LoadKernels(file);
                    loaded.Add(file);
                }

                // Act and assert
                foreach (var field in rejected)
                {
                    var read = CspiceDirect.Getfov(field.Id);
                    _output.WriteLine($"MEASURE getfov {field.Id} ( {field.Boresight} ) ( {field.RefVector} ): {read.Error ?? "read"}");
                    Assert.Equal(field.Error, read.Error);
                }

                foreach (var instrument in spc.Instruments)
                {
                    var read = CspiceDirect.Getfov(instrument.NaifId);
                    _output.WriteLine($"MEASURE getfov {instrument.NaifId} {instrument.Shape} ref ( {instrument.RefVector} ): {read.Error ?? read.Shape}");
                    Assert.Null(read.Error);
                    Assert.Equal(new[] { 0.0, 0.0, 1.0 }, read.Boresight);
                }
            }
            finally
            {
                foreach (var file in loaded)
                {
                    SpiceAPI.Instance.UnloadKernels(file);
                }

                foreach (var file in files)
                {
                    FrameKernelFiles.Delete(file);
                }
            }
        }
    }
}
