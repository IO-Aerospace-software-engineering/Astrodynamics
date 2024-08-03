using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using Xunit;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body
{
    public class InstrumentTests
    {
        public InstrumentTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
            DateTime start = DateTimeExtension.CreateUTC(676555130.80).ToTDB();
            DateTime end = start.AddSeconds(6448.0);

            //Configure scenario
            Scenario scenario = new Scenario("Scenario_A", new Astrodynamics.Mission.Mission("mission06"),
                new Astrodynamics.Time.Window(start, end));
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

            var root = Constants.OutputPath;

            //Execute scenario
            await scenario.SimulateAsync(root, false, false, TimeSpan.FromSeconds(1.0));
            
            //Find windows when the earth is in field of view of camera 600 
            var res = spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Window(DateTimeExtension.CreateTDB(676555200.0), DateTimeExtension.CreateTDB(676561646.0)), spacecraft,
                TestHelpers.EarthAtJ2000, TestHelpers.EarthAtJ2000.Frame,
                ShapeType.Ellipsoid, Aberration.LT,
                TimeSpan.FromSeconds(360.0)).ToArray();

            //Read results
            Assert.Equal(2, res.Count());
            Assert.Equal(DateTime.Parse("2021-06-10T00:00:00.0000000"), res.ElementAt(0).StartDate,TimeSpan.FromMilliseconds(1));
            Assert.Equal("2021-06-10T00:29:06.9433499 (TDB)", res.ElementAt(0).EndDate.ToFormattedString());
            Assert.Equal("2021-06-10T01:03:53.6109902 (TDB)", res.ElementAt(1).StartDate.ToFormattedString());
            Assert.Equal("2021-06-10T01:47:26.0000000 (TDB)", res.ElementAt(1).EndDate.ToFormattedString());

            Assert.Throws<ArgumentNullException>(() => spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Astrodynamics.Time.Window(DateTimeExtension.CreateTDB(676555200.0), DateTimeExtension.CreateTDB(676561647.0)), null,
                TestHelpers.EarthAtJ2000, TestHelpers.EarthAtJ2000.Frame, ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromHours(1.0)));
            Assert.Throws<ArgumentNullException>(() => spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Astrodynamics.Time.Window(DateTimeExtension.CreateTDB(676555200.0), DateTimeExtension.CreateTDB(676561647.0)), spacecraft,
                null, TestHelpers.EarthAtJ2000.Frame, ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromHours(1.0)));
            Assert.Throws<ArgumentNullException>(() => spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
                new Astrodynamics.Time.Window(DateTimeExtension.CreateTDB(676555200.0), DateTimeExtension.CreateTDB(676561647.0)), spacecraft,
                TestHelpers.EarthAtJ2000, null, ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromHours(1.0)));
        }

        [Fact]
        public async Task WriteFrame()
        {
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, new Clock("clk1", 256), new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0),
                TestHelpers.EarthAtJ2000, DateTime.MinValue, Frames.Frame.ICRF));
            spc.AddCircularInstrument(-1001600, "CAM600", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
            await spc.Instruments.First().WriteFrameAsync(new FileInfo("instrumentFrame.tf"));
            TextReader tr = new StreamReader("instrumentFrame.tf");
            var res = await tr.ReadToEndAsync();
            Assert.Equal($"KPL/FK{Environment.NewLine}\\begindata{Environment.NewLine}FRAME_MYSPACECRAFT_CAM600             = -1001600{Environment.NewLine}FRAME_-1001600_NAME        = 'MYSPACECRAFT_CAM600'{Environment.NewLine}FRAME_-1001600_CLASS       = 4{Environment.NewLine}FRAME_-1001600_CLASS_ID    = -1001600{Environment.NewLine}FRAME_-1001600_CENTER      = -1001{Environment.NewLine}TKFRAME_-1001600_SPEC      = 'ANGLES'{Environment.NewLine}TKFRAME_-1001600_RELATIVE  = 'MYSPACECRAFT_FRAME'{Environment.NewLine}TKFRAME_-1001600_ANGLES    = ( 0,-1.5707963267948966,0 ){Environment.NewLine}TKFRAME_-1001600_AXES      = ( 1,    2,   3   ){Environment.NewLine}TKFRAME_-1001600_UNITS     = 'RADIANS'{Environment.NewLine}NAIF_BODY_NAME              += 'MYSPACECRAFT_CAM600'{Environment.NewLine}NAIF_BODY_CODE              += -1001600{Environment.NewLine}\\begintext{Environment.NewLine}",res);
        }
        
        [Fact]
        public async Task WriteKernel()
        {
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, new Clock("clk1", 256), new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0),
                TestHelpers.EarthAtJ2000, DateTime.MinValue, Frames.Frame.ICRF));
            spc.AddCircularInstrument(-1001600, "CAM600", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
            await spc.Instruments.First().WriteKernelAsync(new FileInfo("instrumentKernel.ti"));
            TextReader tr = new StreamReader("instrumentKernel.ti");
            var res = await tr.ReadToEndAsync();
            Assert.Equal($"KPL/IK{Environment.NewLine}\\begindata{Environment.NewLine}INS-1001600_FOV_CLASS_SPEC       = 'ANGLES'{Environment.NewLine}INS-1001600_FOV_SHAPE            = 'CIRCLE'{Environment.NewLine}INS-1001600_FOV_FRAME            = 'MYSPACECRAFT_CAM600'{Environment.NewLine}INS-1001600_BORESIGHT            = ( 0, 0, 1 ){Environment.NewLine}INS-1001600_FOV_REF_VECTOR       = ( 0, 1, 0 ){Environment.NewLine}INS-1001600_FOV_REF_ANGLE        = 1.5{Environment.NewLine}INS-1001600_FOV_ANGLE_UNITS      = 'RADIANS'{Environment.NewLine}\\begintext{Environment.NewLine}",res);
        }
    }
}