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
        // PROVISIONAL, twice the worst of this test's edges on Linux x64 (2026-11-01: POINT 4e-7, 4e-7 and 6e-7 s, ELLIPSOID
        // 5e-7 and 4e-7 s), until the three platforms of the CI have printed their MEASURE lines. Before #515 it was 1 s by
        // construction, the bisection of GeometryFinder, the first start was not compared, and the ellipsoid was a point
        // (up to 1 080 s)
        private const double FieldOfViewTolerance = 1.2e-6;

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

        /// <summary>An instrument of a user's subclass that reads the deprecated <c>PositionInFOV</c>.</summary>
        private sealed class LegacyInstrument : Instrument
        {
            public LegacyInstrument(Spacecraft spacecraft, Vector3 refVector) : base(spacecraft, spacecraft.NaifId * 1000 - 1, "LEGACY1", "model", 0.3,
                InstrumentShape.Circular, Vector3.VectorZ, refVector, new Vector3(0.0, System.Math.PI * 0.5, 0.0))
            {
            }

#pragma warning disable CS0618
            public (double azimuth, double elevation, bool isInFov) Position(TimeSystem.Time date, ILocalizable target) => PositionInFOV(date, target, Aberration.None);
#pragma warning restore CS0618

            public override Task WriteKernelAsync(FileInfo outputFile) => Task.CompletedTask;
        }

        /// <summary>
        /// The deprecated <c>PositionInFOV</c> (#515, kept until 11.0) against a closed form: an observer and a target at
        /// the epoch of their initial states, the boresight along -X of ICRF (Z turned about Y), the azimuth and elevation of
        /// the target's offset (-1 000, 1 000, 500) km on the right vector b × ref and on the reference vector as given,
        /// not made perpendicular; a target behind the instrument is not in view.
        /// </summary>
        [Fact]
        public void PositionInFOV_ReadsTheReferenceVectorAsGiven()
        {
            var start = TimeSystem.Time.CreateTDB(676555199.9846829);
            var earth = TestHelpers.EarthAtJ2000;
            StateVector At(double x, double y, double z) => new(new Vector3(x, y, z), new Vector3(0.0, 7656.2204182967143, 0.0), earth, start, Frames.Frame.ICRF);
            var observer = new Spacecraft(-5201, "LEGACY5201", 1000.0, 3000.0, new Clock("LEGACY5201_CLK", 65536), At(6800000.0, 0.0, 0.0));
            var target = new Spacecraft(-5202, "LEGACY5202", 1000.0, 3000.0, new Clock("LEGACY5202_CLK", 65536), At(5800000.0, 1000000.0, 500000.0));
            var behind = new Spacecraft(-5203, "LEGACY5203", 1000.0, 3000.0, new Clock("LEGACY5203_CLK", 65536), At(7800000.0, 1000000.0, 500000.0));

            // The reference vector Y, perpendicular to the boresight: the right vector is -X × Y = -Z
            var (azimuth, elevation, inView) = new LegacyInstrument(observer, Vector3.VectorY).Position(start, target);
            Assert.True(inView);
            Assert.Equal(System.Math.Atan2(-500.0, 1000.0), azimuth, 1e-12);
            Assert.Equal(System.Math.PI / 4.0, elevation, 1e-12);

            // Y + Z in the instrument frame, -X + Y in ICRF, 45° from the boresight, used without being made perpendicular:
            // its right vector is (0, 0, -1), its elevation the projection on (-1, 1, 0)/√2
            (azimuth, elevation, inView) = new LegacyInstrument(observer, new Vector3(0.0, 1.0, 1.0)).Position(start, target);
            Assert.True(inView);
            Assert.Equal(System.Math.Atan2(-500.0, 1000.0), azimuth, 1e-12);
            Assert.Equal(System.Math.Atan2(2000.0 / System.Math.Sqrt(2.0), 1000.0), elevation, 1e-12);

            (azimuth, elevation, inView) = new LegacyInstrument(observer, Vector3.VectorY).Position(start, behind);
            Assert.False(inView);
            Assert.True(double.IsNaN(azimuth) && double.IsNaN(elevation));
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
            spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(new Quaternion(Vector3.VectorZ, -Astrodynamics.Constants.PI2), Vector3.Zero, start, Frames.Frame.ICRF));

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