using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        // The windows of the library against gftfov on the kernels of the same propagation, s: the precision of the
        // bisection of the search of the library (GeometryFinder, 1 s); gftfov converges to 1e-6 s. A point target: the
        // search of the library takes the direction of the centre of the target, whatever its shape, within the field
        // of view in azimuth and in elevation, which is the circular field of view of the kernel where the Earth crosses
        // it, the Earth moving in elevation only in the frame of the instrument (measured 0.91 s, 0.21 s and 0.18 s, Linux,
        // 2026-10-31)
        private const double FieldOfViewTolerance = 1.0;

        private readonly ITestOutputHelper _output;

        public InstrumentTests(ITestOutputHelper output)
        {
            _output = output;
            SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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

        [Fact]
        public async Task FindWindowInFieldOfView()
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
            // before the search of the library, whose evaluations add orientations to the frame of the spacecraft, among
            // them at instants it holds in another frame, which the CK writer then rejects (SPICE(TIMESOUTOFORDER), #512)
            var searchWindow = new Window(start, end);
            var instrument = spacecraft.Instruments.First();
            var expected = FieldOfViewWindowsBySpice(spacecraft, instrument, searchWindow, TimeSpan.FromSeconds(360.0));

            // Find windows when the earth is in field of view of the camera, over the propagated states: the search
            // starts at the first state (choice 16 of #501). It started 0.02 s after it, and its bisection then read the
            // trajectory 6 minutes before it, an extrapolation of the 8-point Lagrange interpolation 1.18e6 m apart
            // between the two time arguments, a literal no time argument made a reference
            var res = instrument.FindWindowsInFieldOfViewConstraint(searchWindow, spacecraft,
                TestHelpers.EarthAtJ2000, TestHelpers.EarthAtJ2000.Frame,
                ShapeType.Ellipsoid, Aberration.LT,
                TimeSpan.FromSeconds(360.0)).ToArray();

            //Read results: the windows of gftfov, to the 1 s to which the search of the library brackets its
            // transitions (GeometryFinder); the first starts at the first state, where the Earth is in the field of view,
            // and the library looks for that transition one step before the search window, on the trajectory before the
            // propagation (design of #501, §7), so that start is not compared
            Assert.Equal(2, expected.Length);
            Assert.Equal(expected.Length, res.Length);
            Assert.Equal(searchWindow.StartDate, expected[0].StartDate);
            for (int i = 0; i < res.Length; i++)
            {
                double startError = (res[i].StartDate - expected[i].StartDate).TotalSeconds;
                double endError = (res[i].EndDate - expected[i].EndDate).TotalSeconds;
                _output.WriteLine($"MEASURE window {i}: {res[i].StartDate} to {res[i].EndDate}, gftfov " +
                                  $"{expected[i].StartDate} to {expected[i].EndDate}: {startError:E2} s and {endError:E2} s");
                if (i > 0)
                {
                    Assert.InRange(startError, -FieldOfViewTolerance, FieldOfViewTolerance);
                }

                Assert.InRange(endError, -FieldOfViewTolerance, FieldOfViewTolerance);
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
            TimeSpan step)
        {
            // Relative paths, as the kernels of the other tests: the native writers take a bounded file name
            const string directory = "Data/User/FieldOfViewReference";
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
                    ShapeType.Point, Aberration.LT, step).ToArray();
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
            Spacecraft target = new Spacecraft(-181, "SC181", 1000.0, 3000.0, clock, targetOrbit);

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
            spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(new Quaternion(Vector3.VectorZ, -Astrodynamics.Constants.PI2), Vector3.Zero, start, Frames.Frame.ICRF));


            var boresightICRF = spacecraft.Instruments.First().GetBoresightInICRFFrame(start);
            Assert.Equal(new Vector3(0.0, 1.0, 0.0), boresightICRF, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetRefVectorInSpacecraftFrame()
        {
            var spc = TestHelpers.Spacecraft;

            // Instrument with no orientation - refVector should remain unchanged
            spc.AddCircularInstrument(-1600, "inst1", "model", 1.57, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            var refVector1 = spc.Instruments.First().GetRefVectorInSpacecraftFrame();
            Assert.Equal(Vector3.VectorY, refVector1, TestHelpers.VectorComparer);

            // Instrument with 90-degree rotation around Z - refVector (X) should rotate to +Y
            spc.AddCircularInstrument(-1700, "inst2", "model", 1.57, Vector3.VectorZ, Vector3.VectorX,
                new Vector3(0.0, 0.0, Astrodynamics.Constants.PI2));
            var refVector2 = spc.Instruments.ElementAt(1).GetRefVectorInSpacecraftFrame();
            Assert.Equal(new Vector3(0.0, 1.0, 0.0), refVector2, TestHelpers.VectorComparer);
        }

        [Fact]
        public void GetRefVectorInSpacecraftFrame_WithOrientation()
        {
            var spc = TestHelpers.Spacecraft;

            // Instrument with 90-degree rotation around Y axis
            // Original refVector is VectorX, rotating 90 deg around Y maps X -> -Z
            spc.AddCircularInstrument(-1600, "inst", "model", 1.57, Vector3.VectorZ, Vector3.VectorX,
                new Vector3(0.0, Astrodynamics.Constants.PI2, 0.0));
            var refVector = spc.Instruments.First().GetRefVectorInSpacecraftFrame();
            Assert.Equal(new Vector3(0.0, 0.0, -1.0), refVector, TestHelpers.VectorComparer);
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
    }
}