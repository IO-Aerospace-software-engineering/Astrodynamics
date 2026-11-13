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
    }
}
