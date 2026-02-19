using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body
{
    public class SpacecraftTests
    {
        public SpacecraftTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF));
            Assert.Equal(-1001, spc.NaifId);
            Assert.Equal("MySpacecraft", spc.Name);
            Assert.Equal(1000.0, spc.DryOperatingMass);
            Assert.Equal(10000.0, spc.MaximumOperatingMass);
            Assert.True(spc.IsSpacecraft);
        }

        [Fact]
        public void CreateExceptions()
        {
            Clock clk = new Clock("My clock", 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Spacecraft(1001, "MySpacecraft", 1000.0, 10000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF)));
            Assert.Throws<ArgumentException>(() => new Spacecraft(-1001, "", 1000.0, 10000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF)));
            Assert.Throws<ArgumentNullException>(() => new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, null,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Spacecraft(-1001, "MySpacecraft", 10000.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF)));
        }

        [Fact]
        public void Create2()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk1 = new Clock("My clock", 256);
            Payload pl1 = new Payload("pl1", 300, "sn1");
            Spacecraft spc1 = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk1, ke);
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000);
            FuelTank fuelTank11 = new FuelTank("My fuel tank11", "ft2021", "sn1", 4000.0, 4000.0);
            spc1.AddFuelTank(fuelTank10);
            spc1.AddFuelTank(fuelTank11);
            spc1.AddPayload(pl1);
            Assert.Null(spc1.StandbyManeuver);
            Assert.Equal(ke, spc1.InitialOrbitalParameters);
            Assert.Equal(clk1, spc1.Clock);
        }

        [Fact]
        public async Task GetOrientation()
        {
            TimeSystem.Time start =  TimeSystem.Time.Create(676555130.80,TimeFrame.UTCFrame).ToTDB();
            TimeSystem.Time end = start.Add(TimeSpan.FromSeconds(6448.0));

            //Configure scenario
            Scenario scenario = new Scenario("Scenario_A", new IO.Astrodynamics.Mission.Mission("mission05"),
                new Window(start, end));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);

            //Define parking orbit

            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft =
                new Spacecraft(-178, "DRAGONFLY", 1000.0, 10000.0, clock, parkingOrbit);

            FuelTank fuelTank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
            Engine engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank);
            spacecraft.AddFuelTank(fuelTank);
            spacecraft.AddEngine(engine);
            spacecraft.AddPayload(new Payload("payload1", 50.0, "pay01"));
            spacecraft.AddCircularInstrument(-178600, "CAM600", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);
            scenario.AddSpacecraft(spacecraft);

            var root = Constants.OutputPath.CreateSubdirectory(scenario.Mission.Name).CreateSubdirectory(scenario.Name);

            //Execute scenario
            await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

            var orientation = spacecraft.GetOrientation(Frames.Frame.ICRF, start);
            Vector3.VectorY.Rotate(orientation.Rotation);
        }


        [Fact]
        public void AddEngine()
        {
            Clock clk = new Clock("My clock", 256);
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);

            FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn1", 4000.0, 4000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
            spc.AddFuelTank(fuelTank);
            spc.AddEngine(eng);
            Assert.Equal(eng, spc.Engines.Single());
            Assert.Equal(fuelTank, spc.FuelTanks.Single());
            Assert.Throws<ArgumentNullException>(() => spc.AddEngine(null));
            Assert.Throws<ArgumentException>(() => spc.AddEngine(eng));
        }

        [Fact]
        public void AddEngineWithUnknowFuelTank()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);

            FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn1", 4000.0, 4000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
            Assert.Throws<InvalidOperationException>(() => spc.AddEngine(eng));
        }

        [Fact]
        public void AddFuelTank()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);

            FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn1", 4000.0, 4000.0);
            spc.AddFuelTank(fuelTank);
            Assert.Equal(fuelTank, spc.FuelTanks.Single());
            Assert.Throws<ArgumentNullException>(() => spc.AddFuelTank(null));
            Assert.Throws<ArgumentException>(() => spc.AddFuelTank(fuelTank));
        }

        [Fact]
        public void AddPayload()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);

            Payload pl = new Payload("My payload", 1000.0, "sn0");
            spc.AddPayload(pl);
            Assert.Equal(pl, spc.Payloads.Single());
            Assert.Throws<ArgumentNullException>(() => spc.AddPayload(null));
            Assert.Throws<ArgumentException>(() => spc.AddPayload(pl));
        }

        [Fact]
        public void AddInstrument()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);

            spc.AddCircularInstrument(-1001600, "My instrument", "Model", 1.57, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);
            Assert.Single(spc.Instruments);
            Assert.Throws<ArgumentException>(() => spc.AddCircularInstrument(-1001600, "My instrument", "Model", 1.57, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX));
        }

        [Fact]
        public void GetTotalMass()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk = new Clock("My clock", 256);

            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);
            Payload pl1 = new Payload("pl1", 300, "sn1");

            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn1", 4000.0, 3000.0);
            FuelTank fuelTank11 = new FuelTank("My fuel tank11", "ft2021", "sn2", 4000.0, 4000.0);
            spc.AddFuelTank(fuelTank10);
            spc.AddFuelTank(fuelTank11);
            spc.AddPayload(pl1);

            Payload pl2 = new Payload("pl2", 400, "sn0");
            new Clock("My clock", 256);
            Spacecraft spc2 = new Spacecraft(-1002, "MySpacecraft", 2000.0, 10000.0, clk, ke);
            FuelTank fuelTank20 = new FuelTank("My fuel tank20", "ft2021", "sn1", 4000.0, 2000.0);
            FuelTank fuelTank21 = new FuelTank("My fuel tank21", "ft2021", "sn2", 4000.0, 3000.0);
            spc2.AddFuelTank(fuelTank20);
            spc2.AddFuelTank(fuelTank21);
            spc2.AddPayload(pl2);

            Payload pl3 = new Payload("pl3", 50, "sn0");
            Payload pl31 = new Payload("pl31", 150, "sn1");
            new Clock("My clock3", 256);
            Spacecraft spc3 = new Spacecraft(-1003, "MySpacecraft", 3000.0, 10000.0, clk, ke);
            FuelTank fuelTank30 = new FuelTank("My fuel tank30", "ft2021", "sn0", 4000.0, 1000.0);
            FuelTank fuelTank31 = new FuelTank("My fuel tank31", "ft2021", "sn1", 4000.0, 3000.0);
            spc3.AddFuelTank(fuelTank30);
            spc3.AddFuelTank(fuelTank31);
            spc3.AddPayload(pl3);
            spc3.AddPayload(pl31);

            spc3.SetChild(spc2);
            spc2.SetChild(spc);

            double mass3 = spc3.GetTotalMass();
            double mass2 = spc2.GetTotalMass();
            double mass1 = spc.GetTotalMass();

            Assert.Equal(22900.0, mass3);
            Assert.Equal(15700.0, mass2);
            Assert.Equal(8300.0, mass1);
        }

        [Fact]
        public void Dependancies()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk1 = new Clock("My clock", 256);
            Payload pl1 = new Payload("pl1", 300, "sn1");
            Spacecraft spc1 = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk1, ke);


            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            FuelTank fuelTank11 = new FuelTank("My fuel tank11", "ft2021", "sn1", 4000.0, 4000.0);
            spc1.AddFuelTank(fuelTank10);
            spc1.AddFuelTank(fuelTank11);
            spc1.AddPayload(pl1);

            Payload pl2 = new Payload("pl2", 400, "sn1");
            Clock clk2 = new Clock("My clock2", 256);
            Spacecraft spc2 = new Spacecraft(-1002, "MySpacecraft", 2000.0, 10000.0, clk2, ke);
            FuelTank fuelTank20 = new FuelTank("My fuel tank20", "ft2021", "sn0", 4000.0, 2000.0);
            FuelTank fuelTank21 = new FuelTank("My fuel tank21", "ft2021", "sn1", 4000.0, 3000.0);
            spc2.AddFuelTank(fuelTank20);
            spc2.AddFuelTank(fuelTank21);
            spc2.AddPayload(pl2);

            Payload pl3 = new Payload("pl3", 50, "sn1");
            Payload pl31 = new Payload("pl31", 150, "sn2");
            Clock clk3 = new Clock("My clock3", 256);
            Spacecraft spc3 = new Spacecraft(-1003, "MySpacecraft", 3000.0, 10000.0, clk3, ke);
            FuelTank fuelTank30 = new FuelTank("My fuel tank30", "ft2021", "sn0", 4000.0, 1000.0);
            FuelTank fuelTank31 = new FuelTank("My fuel tank31", "ft2021", "sn1", 4000.0, 3000.0);

            spc3.AddFuelTank(fuelTank30);
            spc3.AddFuelTank(fuelTank31);
            spc3.AddPayload(pl3);
            spc3.AddPayload(pl31);

            spc3.SetChild(spc2);
            spc2.SetChild(spc1);

            Assert.Null(spc3.Parent);
            Assert.Equal(spc2, spc3.Child);
            Assert.Equal(spc3, spc2.Parent);
            Assert.Equal(spc1, spc2.Child);
            Assert.Equal(spc2, spc1.Parent);
            Assert.Null(spc1.Child);
        }

        [Fact]
        void SetInitialOrbitalParameters()
        {
            var earth = TestHelpers.EarthAtJ2000;
            var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var sv2 = new StateVector(new Vector3(8800000.0, 0.0, 0.0), new Vector3(0.0, 7000.0, 0.0), earth,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, sv);
            Assert.Equal(spc, (sv.Observer as CelestialBody)?.Satellites.First());
            Assert.True((sv.Observer as CelestialBody)?.Satellites?.Count == 1);

            spc.SetInitialOrbitalParameters(sv2);
            Assert.True((sv.Observer as CelestialBody)?.Satellites.Count == 1);
            Assert.Equal(spc, (sv.Observer as CelestialBody)?.Satellites.First());
            Assert.Throws<ArgumentNullException>(() => spc.SetInitialOrbitalParameters(null));
        }

        [Fact]
        public void ComputeProperties()
        {
            Clock clk = new Clock("My clock", 256);
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk, ke);

            FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn1", 4000.0, 4000.0);
            FuelTank fuelTank2 = new FuelTank("My fuel tank", "ft2022", "sn2", 4000.0, 4000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
            Engine eng2 = new Engine("My engine", "model 2", "sn2", 300.0, 40.0, fuelTank2);
            spc.AddFuelTank(fuelTank);
            spc.AddFuelTank(fuelTank2);
            spc.AddEngine(eng);
            spc.AddEngine(eng2);

            Assert.Equal(327.77777777777777, spc.GetTotalISP());
            Assert.Equal(90.0, spc.GetTotalFuelFlow());
            Assert.Equal(8000.0, spc.GetTotalFuel());
        }

        [Fact]
        public void SetParent()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk1 = new Clock("My clock", 256);
            Payload pl1 = new Payload("pl1", 300, "sn1");
            Spacecraft spc1 = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk1, ke);


            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            FuelTank fuelTank11 = new FuelTank("My fuel tank11", "ft2021", "sn1", 4000.0, 4000.0);
            spc1.AddFuelTank(fuelTank10);
            spc1.AddFuelTank(fuelTank11);
            spc1.AddPayload(pl1);

            Payload pl2 = new Payload("pl2", 400, "sn1");
            Clock clk2 = new Clock("My clock2", 256);
            Spacecraft spc2 = new Spacecraft(-1002, "MySpacecraft", 2000.0, 10000.0, clk2, ke);
            FuelTank fuelTank20 = new FuelTank("My fuel tank20", "ft2021", "sn0", 4000.0, 2000.0);
            FuelTank fuelTank21 = new FuelTank("My fuel tank21", "ft2021", "sn1", 4000.0, 3000.0);
            spc2.AddFuelTank(fuelTank20);
            spc2.AddFuelTank(fuelTank21);
            spc2.AddPayload(pl2);
            spc2.SetParent(spc1);

            Assert.Equal(spc1, spc2.Parent);
            Assert.Equal(spc2, spc1.Child);
            Assert.Null(spc2.Child);
        }

        [Fact]
        public void GetCentersOfMotion()
        {
            var moon = TestHelpers.MoonAtJ2000;
            var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 3000.0, 0.0), moon,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            Clock clk1 = new Clock("My clock", 256);
            var spacecraft = new Spacecraft(-845, "moonlander", 1200, 5000, clk1, sv);
            var res = spacecraft.GetCentersOfMotion();
            Assert.Equal(3, res.Count());
        }

        [Fact]
        public void CreateSpacecraftFrame()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk1 = new Clock("My clock", 256);
            Spacecraft spc1 = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk1, ke);
            var frame = new SpacecraftFrame(spc1);
            Assert.Equal("MySpacecraft_FRAME", frame.Name);
            Assert.Equal(-1001, frame.Spacecraft.NaifId);
            Assert.Equal(-1001000, frame.Id);
        }

        [Fact]
        public async Task WriteSpacecraftFrame()
        {
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new TimeSystem.Time(DateTime.UtcNow,TimeFrame.UTCFrame),
                Frames.Frame.ECLIPTIC_J2000);
            Clock clk1 = new Clock("My clock", 256);
            Spacecraft spc1 = new Spacecraft(-350, "MySpacecraft", 1000.0, 10000.0, clk1, ke);
            var frame = new SpacecraftFrame(spc1);
            await frame.WriteAsync(new FileInfo("test.tf"));
            TextReader tr = new StreamReader("test.tf");
            var res = await tr.ReadToEndAsync();
            Assert.Equal(
                $"KPL/FK{Environment.NewLine}\\begindata{Environment.NewLine}FRAME_MYSPACECRAFT_FRAME   = -350000{Environment.NewLine}FRAME_-350000_NAME      = 'MYSPACECRAFT_FRAME'{Environment.NewLine}FRAME_-350000_CLASS     =  3{Environment.NewLine}FRAME_-350000_CLASS_ID  = -350000{Environment.NewLine}FRAME_-350000_CENTER    = -350{Environment.NewLine}CK_-350000_SCLK         = -350{Environment.NewLine}CK_-350000_SPK          = -350{Environment.NewLine}OBJECT_-350_FRAME       = 'MYSPACECRAFT_FRAME'{Environment.NewLine}NAIF_BODY_NAME              += 'MYSPACECRAFT_FRAME'{Environment.NewLine}NAIF_BODY_CODE              += -350000{Environment.NewLine}NAIF_BODY_NAME              += 'MYSPACECRAFT'{Environment.NewLine}NAIF_BODY_CODE              += -350{Environment.NewLine}\\begintext{Environment.NewLine}",
                res);
        }
        #region Body Axes Tests

        [Fact]
        public void DefaultBodyAxes_MatchStaticConstants()
        {
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF));

            Assert.Equal(Vector3.VectorY, spc.BodyFront);
            Assert.Equal(Vector3.VectorX, spc.BodyRight);
            Assert.Equal(Vector3.VectorZ, spc.BodyUp);
            Assert.Equal(Vector3.VectorY.Inverse(), spc.BodyBack);
            Assert.Equal(Vector3.VectorX.Inverse(), spc.BodyLeft);
            Assert.Equal(Vector3.VectorZ.Inverse(), spc.BodyDown);
        }

        [Fact]
        public void CustomBodyAxes_XForward()
        {
            Clock clk = new Clock("My clock", 256);
            // +X forward, -Y right, +Z up (right-handed: Right x Front = Up → -Y x X = Z ✓)
            Spacecraft spc = new Spacecraft(-1002, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);

            Assert.Equal(Vector3.VectorX, spc.BodyFront);
            Assert.Equal(Vector3.VectorY.Inverse(), spc.BodyRight);
            Assert.Equal(Vector3.VectorZ, spc.BodyUp);
        }

        [Fact]
        public void CustomBodyAxes_NonOrthogonal_ThrowsArgumentException()
        {
            Clock clk = new Clock("My clock", 256);
            Assert.Throws<ArgumentException>(() => new Spacecraft(-1003, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: new Vector3(1.0, 1.0, 0.0), bodyRight: Vector3.VectorX, bodyUp: Vector3.VectorZ));
        }

        [Fact]
        public void CustomBodyAxes_LeftHanded_ThrowsArgumentException()
        {
            Clock clk = new Clock("My clock", 256);
            // Left-handed: swap right to make X x Y = -Z (not +Z)
            Assert.Throws<ArgumentException>(() => new Spacecraft(-1004, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: Vector3.VectorY, bodyRight: Vector3.VectorX.Inverse(), bodyUp: Vector3.VectorZ));
        }

        [Fact]
        public void CustomBodyAxes_NormalizesInput()
        {
            Clock clk = new Clock("My clock", 256);
            // Non-unit vectors should be normalized: (2,0,0) → (1,0,0)
            // Right-handed: -Y x X = Z
            Spacecraft spc = new Spacecraft(-1005, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: new Vector3(2.0, 0.0, 0.0), bodyRight: new Vector3(0.0, -2.0, 0.0), bodyUp: new Vector3(0.0, 0.0, 2.0));

            Assert.True(System.Math.Abs(spc.BodyFront.Magnitude() - 1.0) < 1e-10);
            Assert.True(System.Math.Abs(spc.BodyRight.Magnitude() - 1.0) < 1e-10);
            Assert.True(System.Math.Abs(spc.BodyUp.Magnitude() - 1.0) < 1e-10);
        }

        [Fact]
        public void CustomBodyAxes_PartialSpecification_ThrowsWhenInvalid()
        {
            // Providing bodyFront=+X (without bodyRight/bodyUp) triggers validation.
            // Defaults: bodyRight=+X, bodyUp=+Z.
            // BodyFront (+X) is parallel to BodyRight (+X), so validation throws ArgumentException.
            Clock clk = new Clock("My clock", 256);
            Assert.Throws<ArgumentException>(() => new Spacecraft(-1006, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: Vector3.VectorX)); // bodyRight defaults to +X == bodyFront → not orthogonal
        }

        [Fact]
        public void CustomBodyAxes_InverseProperties_AreCorrect()
        {
            // Create spacecraft with custom axes: front=+X, right=-Y, up=+Z (right-handed: -Y x X = Z ✓)
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1007, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);

            // Verify forward axes
            Assert.Equal(Vector3.VectorX, spc.BodyFront);
            Assert.Equal(Vector3.VectorY.Inverse(), spc.BodyRight);
            Assert.Equal(Vector3.VectorZ, spc.BodyUp);

            // Verify computed inverse axes
            Assert.Equal(Vector3.VectorX.Inverse(), spc.BodyBack);
            Assert.Equal(Vector3.VectorY, spc.BodyLeft);
            Assert.Equal(Vector3.VectorZ.Inverse(), spc.BodyDown);
        }

        [Fact]
        public void CustomBodyAxes_NormalizesInput_PreservesDirection()
        {
            // Providing a non-unit vector (2,0,0) for bodyFront should result in (1,0,0) after normalization.
            // The direction must be preserved, not just the magnitude normalized.
            Clock clk = new Clock("My clock", 256);
            // Right-handed: -Y x X = Z
            Spacecraft spc = new Spacecraft(-1008, "SC", 100.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), Frames.Frame.ICRF),
                bodyFront: new Vector3(2.0, 0.0, 0.0),
                bodyRight: new Vector3(0.0, -2.0, 0.0),
                bodyUp: new Vector3(0.0, 0.0, 2.0));

            // Direction should be preserved and magnitude should be 1
            Assert.Equal(Vector3.VectorX, spc.BodyFront);
            Assert.Equal(Vector3.VectorY.Inverse(), spc.BodyRight);
            Assert.Equal(Vector3.VectorZ, spc.BodyUp);
        }

        [Fact]
        public void GetBodyFront_FallsBackToStaticDefault_WhenNoSpacecraft()
        {
            // Create FuelTank and Engine WITHOUT adding to a spacecraft.
            // FuelTank.Spacecraft will be null, so GetBodyFront() returns Spacecraft.Front (+Y).
            // Verify NormalAttitude uses the static default by comparing with a spacecraft-attached version.
            var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0,
                TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            // Engine NOT attached to any spacecraft
            FuelTank unattachedTank = new FuelTank("ft_detached", "ftA", "sn-detached", 1000.0, 900.0);
            Engine unattachedEngine = new Engine("eng_detached", "engmk1", "sn-eng-detached", 450, 50, unattachedTank);

            // Maneuver using the detached engine — GetBodyFront() returns Spacecraft.Front (+Y)
            NormalAttitude detachedManeuver = new NormalAttitude(
                TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
                TimeSpan.Zero,
                unattachedEngine);

            var sv = orbitalParams.ToStateVector();
            detachedManeuver.TryExecute(sv);

            // Create reference maneuver with spacecraft that has default axes (+Y front)
            var spcDefault = new Spacecraft(-1009, "DefaultAxesSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spcDefault.AddFuelTank(new FuelTank("ft_ref", "ftA", "sn-ref", 1000.0, 900.0));
            spcDefault.AddEngine(new Engine("eng_ref", "engmk1", "sn-eng-ref", 450, 50, spcDefault.FuelTanks.First()));
            NormalAttitude referenceManeuver = new NormalAttitude(
                TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
                TimeSpan.Zero,
                spcDefault.Engines.First());

            referenceManeuver.TryExecute(sv);

            // Both should use +Y as body front, producing the same quaternion
            var q1 = detachedManeuver.StateOrientation.Rotation;
            var q2 = referenceManeuver.StateOrientation.Rotation;

            // Account for quaternion double-cover
            double dot = q1.W * q2.W + q1.VectorPart.X * q2.VectorPart.X +
                         q1.VectorPart.Y * q2.VectorPart.Y + q1.VectorPart.Z * q2.VectorPart.Z;
            double sign = dot >= 0 ? 1.0 : -1.0;

            Assert.True(System.Math.Abs(sign * q2.W - q1.W) < 1e-10,
                $"Detached engine should use static +Y front (same quaternion). W: {q1.W} vs {sign * q2.W}");
            Assert.True(System.Math.Abs(sign * q2.VectorPart.X - q1.VectorPart.X) < 1e-10,
                "Quaternion X components should match.");
            Assert.True(System.Math.Abs(sign * q2.VectorPart.Y - q1.VectorPart.Y) < 1e-10,
                "Quaternion Y components should match.");
            Assert.True(System.Math.Abs(sign * q2.VectorPart.Z - q1.VectorPart.Z) < 1e-10,
                "Quaternion Z components should match.");
        }

        #endregion
    }
}