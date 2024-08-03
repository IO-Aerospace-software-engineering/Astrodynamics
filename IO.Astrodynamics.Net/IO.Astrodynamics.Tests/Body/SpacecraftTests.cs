using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;
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
                    DateTime.MinValue, Frames.Frame.ICRF));
            Assert.Equal(-1001, spc.NaifId);
            Assert.Equal("MySpacecraft", spc.Name);
            Assert.Equal(1000.0, spc.DryOperatingMass);
            Assert.Equal(10000.0, spc.MaximumOperatingMass);
        }

        [Fact]
        public void CreateExceptions()
        {
            Clock clk = new Clock("My clock", 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Spacecraft(1001, "MySpacecraft", 1000.0, 10000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    DateTime.MinValue, Frames.Frame.ICRF)));
            Assert.Throws<ArgumentException>(() => new Spacecraft(-1001, "", 1000.0, 10000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    DateTime.MinValue, Frames.Frame.ICRF)));
            Assert.Throws<ArgumentNullException>(() => new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, null,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    DateTime.MinValue, Frames.Frame.ICRF)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Spacecraft(-1001, "MySpacecraft", 10000.0, 1000.0, clk,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    DateTime.MinValue, Frames.Frame.ICRF)));
        }

        [Fact]
        public void Create2()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, sun, DateTime.UtcNow,
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
            DateTime start = DateTimeExtension.CreateUTC(676555130.80).ToTDB();
            DateTime end = start.AddSeconds(6448.0);

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
            await scenario.SimulateAsync(root, false, false, TimeSpan.FromSeconds(1.0));

            var orientation = spacecraft.GetOrientation(Frames.Frame.ICRF, start);
            Vector3.VectorY.Rotate(orientation.Rotation);
        }


        [Fact]
        public void AddEngine()
        {
            Clock clk = new Clock("My clock", 256);
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var sv2 = new StateVector(new Vector3(8800000.0, 0.0, 0.0), new Vector3(0.0, 7000.0, 0.0), earth,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, DateTime.UtcNow,
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
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            Clock clk1 = new Clock("My clock", 256);
            var spacecraft = new Spacecraft(-845, "moonlander", 1200, 5000, clk1, sv);
            var res = spacecraft.GetCentersOfMotion();
            Assert.Equal(3, res.Count());
        }

        [Fact]
        public void CreateSpacecraftFrame()
        {
            var frame = new SpacecraftFrame("test", -350, "spc1");
            Assert.Equal("test", frame.Name);
            Assert.Equal(-350, frame.SpacecraftId);
            Assert.Equal(-350000, frame.Id);
        }

        [Fact]
        public async Task WriteSpacecraftFrame()
        {
            var frame = new SpacecraftFrame("test", -350, "spc1");
            await frame.WriteAsync(new FileInfo("test.tf"));
            TextReader tr = new StreamReader("test.tf");
            var res = await tr.ReadToEndAsync();
            Assert.Equal(
                $"KPL/FK{Environment.NewLine}\\begindata{Environment.NewLine}FRAME_TEST   = -350000{Environment.NewLine}FRAME_-350000_NAME      = 'TEST'{Environment.NewLine}FRAME_-350000_CLASS     =  3{Environment.NewLine}FRAME_-350000_CLASS_ID  = -350000{Environment.NewLine}FRAME_-350000_CENTER    = -350{Environment.NewLine}CK_-350000_SCLK         = -350{Environment.NewLine}CK_-350000_SPK          = -350{Environment.NewLine}OBJECT_-350_FRAME       = 'TEST'{Environment.NewLine}NAIF_BODY_NAME              += 'TEST'{Environment.NewLine}NAIF_BODY_CODE              += -350000{Environment.NewLine}NAIF_BODY_NAME              += 'SPC1'{Environment.NewLine}NAIF_BODY_CODE              += -350{Environment.NewLine}\\begintext{Environment.NewLine}",
                res);
        }
    }
}