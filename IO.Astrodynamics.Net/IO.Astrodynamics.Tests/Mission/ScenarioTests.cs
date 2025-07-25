using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Mission
{
    public class ScenarioTests
    {
        public ScenarioTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Scenario scenario = new Scenario("Scenario", mission, new Window(new TimeSystem.Time(2021, 1, 1), new TimeSystem.Time(2021, 1, 2)));
            Assert.Equal("Scenario", scenario.Name);
            Assert.Equal(mission, scenario.Mission);
        }

        [Fact]
        public void AddSite()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Scenario scenario = new Scenario("Scenario", mission, new Window(new TimeSystem.Time(2021, 1, 1), new TimeSystem.Time(2021, 1, 2)));
            Assert.Equal("Scenario", scenario.Name);
            Assert.Equal(mission, scenario.Mission);
            var site = new Site(13, "DSS-13", PlanetsAndMoons.EARTH_BODY);
            scenario.AddSite(site);
            Assert.Single(scenario.Sites);
            Assert.Equal(site, scenario.Sites.First());
            Assert.Throws<ArgumentNullException>(() => scenario.AddSite(null));
        }

        [Fact]
        public void AddAdditionalCelestialBody()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Scenario scenario = new Scenario("Scenario", mission, new Window(new TimeSystem.Time(2021, 1, 1), new TimeSystem.Time(2021, 1, 2)));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
            Assert.Single(scenario.AdditionalCelstialBodies);
            Assert.Equal(TestHelpers.MoonAtJ2000, scenario.AdditionalCelstialBodies.First());
            Assert.Throws<ArgumentNullException>(() => scenario.AddCelestialItem(null));
        }

        [Fact]
        public async Task PropagateSite()
        {
            Window window = new Window(new TimeSystem.Time(2000, 1, 1, 12, 0, 0), new TimeSystem.Time(2000, 1, 2, 12, 0, 0));
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("MissionSite");
            Scenario scenario = new Scenario("scnSite", mission, window);
            Site site = new Site(333, "S333", TestHelpers.EarthAtJ2000, new Planetodetic(30 * Astrodynamics.Constants.Deg2Rad, 10 * Astrodynamics.Constants.Deg2Rad, 1000.0));
            scenario.AddSite(site);

            //Propagate site
            await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));
            var res = site.GetEphemeris(window, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1));
            var orbitalParametersEnumerable = res as Astrodynamics.OrbitalParameters.OrbitalParameters[] ?? res.ToArray();
            Assert.Equal(new Vector3(4054783.094777816, -4799280.900382741, 1100391.2394741199), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Position,
                TestHelpers.VectorComparer);
            Assert.Equal(new Vector3(349.9689414487369, 295.67943565441215, 0.00047467276487285595), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Velocity,
                TestHelpers.VectorComparer);
            Assert.Equal(TimeSystem.Time.J2000TDB, orbitalParametersEnumerable.ElementAt(0).Epoch);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(0).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(0).Observer);

            Assert.Equal(5675531.4457497578, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.X, 6);
            Assert.Equal(2694837.3733771634, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Y, 6);
            Assert.Equal(1100644.504707, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Z, 6);
            Assert.Equal(-196.510785, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.X, 6);
            Assert.Equal(413.86626642107882, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Y, 6);
            Assert.Equal(0.00077810438580775993, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Z, 6);
            Assert.Equal(18000.0, orbitalParametersEnumerable.ElementAt(5).Epoch.TimeSpanFromJ2000().TotalSeconds);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(5).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(5).Observer);
        }

        [Fact]
        [Benchmark]
        public async Task PropagateSpacecraft()
        {
            TimeSystem.Time start = TimeSystem.Time.CreateUTC(667915269.18539762).ToTDB();
            TimeSystem.Time startPropagator = TimeSystem.Time.CreateUTC(668085555.829810).ToTDB();
            TimeSystem.Time end = TimeSystem.Time.CreateUTC(668174400.000000).ToTDB();

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission02");
            Scenario scenario = new Scenario("scn1", mission, new Window(startPropagator, end));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
            scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
            scenario.AddCelestialItem(TestHelpers.Sun);

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(5056554.1874925727, 4395595.4942363985, 0.0),
                new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Define target orbit
            StateVector targetOrbit = new StateVector(
                new Vector3(4390853.7278876612, 5110607.0005866792, 917659.86391987884),
                new Vector3(-4979.4693432656513, 3033.2639866911495, 6933.1803797017265), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Create and configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-1783, "DRAGONFLY3", 1000.0, 10000.0, clock, parkingOrbit);

            FuelTank fuelTank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
            Engine engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank);
            spacecraft.AddFuelTank(fuelTank);
            spacecraft.AddEngine(engine);
            spacecraft.AddPayload(new Payload("payload1", 50.0, "pay01"));
            spacecraft.AddCircularInstrument(-1783601, "CAM601", "mod1", 80.0 * IO.Astrodynamics.Constants.Deg2Rad, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);

            var planeAlignmentManeuver =
                new PlaneAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit, spacecraft.Engines.First());
            planeAlignmentManeuver.SetNextManeuver(new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new PhasingManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit, 1, spacecraft.Engines.First()))
                .SetNextManeuver(new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 15866666.666666666,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new ZenithAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new RetrogradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new ProgradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromMinutes(1), engine));
            spacecraft.SetStandbyManeuver(planeAlignmentManeuver);

            scenario.AddSpacecraft(spacecraft);

            var summary = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

            // Read maneuver results
            Astrodynamics.Maneuver.Maneuver maneuver1 = spacecraft.InitialManeuver;
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:43.7323762 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:43.7323762 TDB"), maneuver1.ThrustWindow!.Value.EndDate);
            Assert.Equal(8.1349999999999998, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(-94.14448820356235, 104.5303929755525, -115.98378254209923), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);

            Assert.Equal(406.75719484962326, maneuver1.FuelBurned, 3);

            maneuver1 = maneuver1.NextManeuver;

            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:18.7667342 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:37.1578812 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:18.7667342 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:37.1578812 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(18.391151799999999, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(-373.28620672120724, -125.30410508953423, 201.35110208737478), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(919.55735422358202, maneuver1.FuelBurned, 3);

            maneuver1 = maneuver1.NextManeuver;

            Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:07.4800315 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:19:31.1871324 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:07.4800315 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:17.2926540 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.8126244000000007, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(-140.06837019494708, 85.93104186864682, 195.57303801851774), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(490.63122063919275, maneuver1.FuelBurned, 3);

            maneuver1 = maneuver1.NextManeuver;

            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:24.8248123 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:34.1197460 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:24.8248123 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:34.1197460 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.2949856999999998, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(140.54623544686248, -86.32008177793513, -196.3279176030952), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(464.74671652424558, maneuver1.FuelBurned, 3);

            Assert.Equal(scenario.Window, summary.Window);
            Assert.Single(summary.SpacecraftSummaries);
            var maneuverWindow1 = summary.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow1 != null)
            {
                Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323"), maneuverWindow1.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T08:47:39.0152381"), maneuverWindow1.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(2281.6923637537593, summary.SpacecraftSummaries.First().FuelConsumption, 3);
        }

        [Fact]
        public async Task PropagateSpacecraftWithAttitudeOnly()
        {
            TimeSystem.Time start = new TimeSystem.Time(2024, 7, 30, 13, 48, 30, frame: TimeFrame.UTCFrame);
            TimeSystem.Time end = new TimeSystem.Time(2024, 7, 31, 13, 48, 33, frame: TimeFrame.UTCFrame);

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission02");
            Scenario scenario = new Scenario("scn1", mission, new Window(start, end));
            scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(6800000.0, 0.0, 0.0),
                new Vector3(0.0, 8000.0, 0.0), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Create and configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-1783, "DRAGONFLY3", 1000.0, 10000.0, clock, parkingOrbit);

            FuelTank fuelTank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
            Engine engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank);
            spacecraft.AddFuelTank(fuelTank);
            spacecraft.AddEngine(engine);

            var progradeAttitude = new ProgradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, engine);
            spacecraft.SetStandbyManeuver(progradeAttitude);

            scenario.AddSpacecraft(spacecraft);

            var summary = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

            // Read maneuver results
            Assert.Equal("2024-07-30T13:49:39.1833013 TDB", progradeAttitude.ManeuverWindow?.StartDate.ToString());
            Assert.Equal("2024-07-30T13:49:39.1833013 TDB", progradeAttitude.ManeuverWindow?.EndDate.ToString());
            Assert.Equal("2024-07-30T13:49:39.1833013 TDB", progradeAttitude.ThrustWindow?.StartDate.ToString());
            Assert.Equal("2024-07-30T13:49:39.1833013 TDB", progradeAttitude.ThrustWindow?.EndDate.ToString());
            Assert.Equal(0.0, progradeAttitude.ThrustWindow.Value.Length.TotalSeconds, 3);

            Assert.Equal(0.0, progradeAttitude.FuelBurned, 0);


            Assert.Equal(scenario.Window, summary.Window);
            Assert.Single(summary.SpacecraftSummaries);
            var maneuverWindow = summary.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow != null)
            {
                Assert.Equal(new TimeSystem.Time("2024-07-30T13:49:39.1839998"), maneuverWindow.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2024-07-30T13:49:39.1839998"), maneuverWindow.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(0.0, summary.SpacecraftSummaries.First().FuelConsumption, 3);
        }

        [Fact]
        public void PropagateSpacecraftFromTLE()
        {
            TimeSystem.Time start = new TimeSystem.Time(2023, 03, 01, 12, 0, 0);
            TimeSystem.Time end = start.AddDays(1.0);

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("missionTLE");
            Scenario scenario = new Scenario("scnTLE", mission, new Window(start, end));
            scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);

            //Define parking orbit
            TLE parkingOrbit = new TLE("ISS", "1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
                "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064");

            //Create and configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-1787, "DRAGONFLY7", 1000.0, 10000.0, clock, parkingOrbit);

            scenario.AddSpacecraft(spacecraft);

            //var summary = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));
            var site = new Site(63, "DSS-63", TestHelpers.EarthAtJ2000);
            var initialSV = spacecraft.GetEphemeris(start, site, Frames.Frame.ICRF, Aberration.None);
            var endSV = spacecraft.GetEphemeris(end, site, Frames.Frame.ICRF, Aberration.None);

            Assert.Equal(
                new StateVector(new Vector3(-2203549.8057556152, 6530514.265792847, -8845974.357006073), new Vector3(-4836.122150244806, 5010.836806879401, 2817.6443107142204),
                    site, start, Frames.Frame.ICRF), initialSV.ToStateVector(), TestHelpers.StateVectorComparer);
            Assert.Equal(
                new StateVector(new Vector3(-8860293.65826416, 2490098.7069015503, 1059977.3478050232), new Vector3(961.7950954605913, -7714.248725439211, 1601.9044880627953),
                    site, end, Frames.Frame.ICRF), endSV.ToStateVector(), TestHelpers.StateVectorComparer);
        }

        [Fact]
        [Benchmark]
        public async Task MultipleSpacecraftPropagation()
        {
            TimeSystem.Time start = TimeSystem.Time.CreateUTC(667915269.18539762).ToTDB();
            TimeSystem.Time startPropagator = TimeSystem.Time.CreateUTC(668085555.829810).ToTDB();
            TimeSystem.Time end = TimeSystem.Time.CreateUTC(668174400.000000).ToTDB();

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission02");
            Scenario scenario = new Scenario("scn1", mission, new Window(startPropagator, end));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
            scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
            scenario.AddCelestialItem(TestHelpers.Sun);

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(5056554.1874925727, 4395595.4942363985, 0.0),
                new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Define target orbit
            StateVector targetOrbit = new StateVector(
                new Vector3(4390853.7278876612, 5110607.0005866792, 917659.86391987884),
                new Vector3(-4979.4693432656513, 3033.2639866911495, 6933.1803797017265), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Create and configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-1783, "DRAGONFLY31", 1000.0, 10000.0, clock, parkingOrbit);

            FuelTank fuelTank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
            Engine engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank);
            spacecraft.AddFuelTank(fuelTank);
            spacecraft.AddEngine(engine);
            spacecraft.AddPayload(new Payload("payload1", 50.0, "pay01"));
            spacecraft.AddCircularInstrument(-1783601, "CAM601", "mod1", 80.0 * IO.Astrodynamics.Constants.Deg2Rad, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);

            var planeAlignmentManeuver =
                new PlaneAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit, spacecraft.Engines.First());
            planeAlignmentManeuver.SetNextManeuver(new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new PhasingManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit, 1, spacecraft.Engines.First()))
                .SetNextManeuver(new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 15866666.666666666,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new ZenithAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new RetrogradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new ProgradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromMinutes(1), engine));
            spacecraft.SetStandbyManeuver(planeAlignmentManeuver);

            scenario.AddSpacecraft(spacecraft);

            {
                var summary1 = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

                // Read maneuver results
                Astrodynamics.Maneuver.Maneuver maneuver1 = spacecraft.InitialManeuver;
                Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:43.7323762 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:43.7323762 TDB"), maneuver1.ThrustWindow!.Value.EndDate);
                Assert.Equal(8.1349999999999998, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
                Assert.Equal(new Vector3(-94.14448820356235, 104.5303929755525, -115.98378254209923), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);

                Assert.Equal(406.75719484962326, maneuver1.FuelBurned, 3);

                maneuver1 = maneuver1.NextManeuver;

                Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:18.7667342 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:37.1578812 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:18.7667342 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:37.1578812 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(18.391151799999999, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
                Assert.Equal(new Vector3(-373.28620672120724, -125.30410508953423, 201.35110208737478), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
                Assert.Equal(919.55735422358202, maneuver1.FuelBurned, 3);

                maneuver1 = maneuver1.NextManeuver;

                Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:07.4800315 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T08:19:31.1871324 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:07.4800315 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:17.2926540 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(9.8126244000000007, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
                Assert.Equal(new Vector3(-140.06837019494708, 85.93104186864682, 195.57303801851774), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
                Assert.Equal(490.63122063919275, maneuver1.FuelBurned, 3);

                maneuver1 = maneuver1.NextManeuver;

                Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:24.8248123 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:34.1197460 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:24.8248123 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:34.1197460 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(9.2949856999999998, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 3);
                Assert.Equal(new Vector3(140.54623544686248, -86.32008177793513, -196.3279176030952), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
                Assert.Equal(464.74671652424558, maneuver1.FuelBurned, 3);

                Assert.Equal(scenario.Window, summary1.Window);
                Assert.Single(summary1.SpacecraftSummaries);
                var maneuverWindow1 = summary1.SpacecraftSummaries.First().ManeuverWindow;
                if (maneuverWindow1 != null)
                {
                    Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323"), maneuverWindow1.Value.StartDate, TestHelpers.TimeComparer);
                    Assert.Equal(new TimeSystem.Time("2021-03-04T08:47:39.0152381"), maneuverWindow1.Value.EndDate, TestHelpers.TimeComparer);
                }

                Assert.Equal(2281.6923637537593, summary1.SpacecraftSummaries.First().FuelConsumption, 3);
            }
            var summary2 = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));
            // Read maneuver results
            Astrodynamics.Maneuver.Maneuver maneuver2 = spacecraft.InitialManeuver;
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:43.7323762 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:43.7323762 TDB"), maneuver2.ThrustWindow!.Value.EndDate);
            Assert.Equal(8.1349999999999998, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(-94.14448820356235, 104.5303929755525, -115.98378254209923), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);

            Assert.Equal(406.75719484962326, maneuver2.FuelBurned, 3);

            maneuver2 = maneuver2.NextManeuver;

            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:18.7667342 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:37.1578812 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:18.7667342 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T01:19:37.1578812 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(18.391151799999999, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(-373.28620672120724, -125.30410508953423, 201.35110208737478), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(919.55735422358202, maneuver2.FuelBurned, 3);

            maneuver2 = maneuver2.NextManeuver;

            Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:07.4800315 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:19:31.1871324 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:07.4800315 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T04:36:17.2926540 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.8126244000000007, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(-140.06837019494708, 85.93104186864682, 195.57303801851774), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(490.63122063919275, maneuver2.FuelBurned, 3);

            maneuver2 = maneuver2.NextManeuver;

            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:24.8248123 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:34.1197460 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:24.8248123 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-03-04T08:44:34.1197460 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.2949856999999998, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 3);
            Assert.Equal(new Vector3(140.54623544686248, -86.32008177793513, -196.3279176030952), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(464.74671652424558, maneuver2.FuelBurned, 3);

            Assert.Equal(scenario.Window, summary2.Window);
            Assert.Single(summary2.SpacecraftSummaries);
            var maneuverWindow2 = summary2.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow2 != null)
            {
                Assert.Equal(new TimeSystem.Time("2021-03-04T00:34:35.5972323"), maneuverWindow2.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time("2021-03-04T08:47:39.0152381"), maneuverWindow2.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(2281.6923637537593, summary2.SpacecraftSummaries.First().FuelConsumption, 3);
        }

        [Fact]
        [Benchmark]
        public async Task PropagateWithoutManeuver()
        {
            TimeSystem.Time start = TimeSystem.Time.CreateUTC(667915269.18539762).ToTDB();
            TimeSystem.Time startPropagator = TimeSystem.Time.CreateUTC(668085555.829810).ToTDB();
            TimeSystem.Time end = TimeSystem.Time.CreateUTC(668174400.000000).ToTDB();

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission102");
            Scenario scenario = new Scenario("scn100", mission, new Window(startPropagator, end));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);

            //Define parking orbit
            StateVector parkingOrbit = new StateVector(
                new Vector3(5056554.1874925727, 4395595.4942363985, 0.0),
                new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494), TestHelpers.EarthAtJ2000,
                start, Frames.Frame.ICRF);

            //Create and configure spacecraft
            Clock clock = new Clock("clk1", 65536);
            Spacecraft spacecraft = new Spacecraft(-1785, "DRAGONFLY32", 1000.0, 10000.0, clock, parkingOrbit);

            FuelTank fuelTank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
            Engine engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank);
            spacecraft.AddFuelTank(fuelTank);
            spacecraft.AddEngine(engine);
            spacecraft.AddPayload(new Payload("payload1", 50.0, "pay01"));
            spacecraft.AddCircularInstrument(-1785601, "CAM601", "mod1", 80.0 * IO.Astrodynamics.Constants.Deg2Rad, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);

            scenario.AddSpacecraft(spacecraft);
            var summary = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

            Assert.Equal(scenario.Window, summary.Window);
            Assert.Single(summary.SpacecraftSummaries);
            var maneuverWindow = summary.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow != null)
            {
                Assert.Equal(new TimeSystem.Time(2021, 3, 4, 0, 32, 53, 814), maneuverWindow.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time(2021, 3, 4, 5, 27, 13, 014), maneuverWindow.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(0.0, summary.SpacecraftSummaries.First().FuelConsumption, 3);
        }

        [Fact]
        [Benchmark]
        public async Task PropagateStar()
        {
            var start = new TimeSystem.Time(2001, 1, 1);
            var end = start.AddDays(365 * 4);
            var observer = Barycenters.SOLAR_SYSTEM_BARYCENTER;
            var star = new Star(2, "star2", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, start), 0.1, 0.1, 0, 0, 0, 0, start);

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission102");
            Scenario scenario = new Scenario("scn100", mission, new Window(start, end));
            scenario.AddStar(star);

            var summary = await scenario.SimulateAsync( false, false, TimeSpan.FromDays(365.0));

            Assert.Equal(scenario.Window, summary.Window);

            var eph0 = star.GetEphemeris(start, observer, Frames.Frame.ICRF, Aberration.None);
            var eph1 = star.GetEphemeris(start.Add(TimeSpan.FromDays(365)), observer, Frames.Frame.ICRF, Aberration.None);
            var eph2 = star.GetEphemeris(start.Add(TimeSpan.FromDays(365 + 365)), observer, Frames.Frame.ICRF, Aberration.None);
            Assert.Equal(1.0, eph0.ToEquatorial().RightAscension, 12);
            Assert.Equal(1.0, eph0.ToEquatorial().Declination, 12);
            Assert.Equal(8.1373353929324900E+16, eph0.ToEquatorial().Distance);

            Assert.Equal(1.1, eph1.ToEquatorial().RightAscension, 3);
            Assert.Equal(1.1, eph1.ToEquatorial().Declination, 3);
            Assert.Equal(8.1373353929324896E+16, eph1.ToEquatorial().Distance);

            Assert.Equal(1.2, eph2.ToEquatorial().RightAscension, 3);
            Assert.Equal(1.2, eph2.ToEquatorial().Declination, 3);
            Assert.Equal(8.1373353929324910E+16, eph2.ToEquatorial().Distance);
        }


        [Fact]
        public void Equality()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission02");
            Scenario scenario = new Scenario("scn1", mission,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), new TimeSystem.Time(DateTime.MaxValue, TimeFrame.TDBFrame)));

            Astrodynamics.Mission.Mission mission2 = new Astrodynamics.Mission.Mission("mission03");
            Scenario scenario2 = new Scenario("scn1", mission2,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), new TimeSystem.Time(DateTime.MaxValue, TimeFrame.TDBFrame)));

            Assert.True(scenario != scenario2);
            Assert.False(scenario == scenario2);
            Assert.False(scenario.Equals(scenario2));
            Assert.False(scenario.Equals(null));
            Assert.True(scenario.Equals(scenario));
            Assert.False(scenario.Equals((object)scenario2));
            Assert.False(scenario.Equals((object)null));
            Assert.True(scenario.Equals((object)scenario));
        }

        [Fact]
        public async Task PropagateException()
        {
            TimeSystem.Time startPropagator = TimeSystem.Time.CreateUTC(668085555.829810).ToTDB();
            TimeSystem.Time end = TimeSystem.Time.CreateUTC(668174400.000000).ToTDB();

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission04");
            Scenario scenario = new Scenario("scn1", mission, new Window(startPropagator, end));
            Assert.Throws<ArgumentNullException>(() => scenario.AddSpacecraft(null));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0)));
        }

        [Fact]
        public async Task DeepSpaceMoon3Days()
        {
            var frame = Frames.Frame.ICRF;
            var start = TimeSystem.Time.J2000TDB;
            var end = start.AddDays(3);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, frame, start);
            var moon = new CelestialBody(PlanetsAndMoons.MOON, frame, start);
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission01");
            Scenario scenario = new Scenario("scn01", mission, new Window(start, end));

            StateVector testOrbit = moon.GetEphemeris(start, new CelestialBody(399), frame, Aberration.None).ToStateVector();
            Clock clk = new Clock("My clock", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, testOrbit, 0.5);
            scenario.AddSpacecraft(spc);
            scenario.AddCelestialItem(new CelestialBody(10));
            scenario.AddCelestialItem(new CelestialBody(399));
            scenario.AddCelestialItem(new Barycenter(1));
            scenario.AddCelestialItem(new Barycenter(2));
            scenario.AddCelestialItem(new Barycenter(4));
            scenario.AddCelestialItem(new Barycenter(5));
            scenario.AddCelestialItem(new Barycenter(6));
            scenario.AddCelestialItem(new Barycenter(7));
            scenario.AddCelestialItem(new Barycenter(8));
            var summary = await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(100.0));

            var spcSV = spc.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var moonSV = moon.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            var delta = spcSV.RelativeTo(moon, Aberration.None).ToStateVector();
            var deltaP = delta.Position.Magnitude();
            var deltaV = delta.Velocity.Magnitude();

            Assert.True(deltaP < 35);
            Assert.True(deltaV < 2.7E-04);
        }

        SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        public async Task DeepSpaceMoon25D(int stepSize)
        {
            await semaphore.WaitAsync();

            try
            {
                var step = TimeSpan.FromMinutes(stepSize);
                var frame = Frames.Frame.ICRF;
                var start = TimeSystem.Time.J2000TDB;
                var end = start.AddDays(25);
                var earth = new CelestialBody(PlanetsAndMoons.EARTH, frame, start);
                var moon = new CelestialBody(PlanetsAndMoons.MOON, frame, start);
                Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("missionsdeepspace");
                Scenario scenario = new Scenario("scn" + stepSize, mission, new Window(start, end));

                StateVector testOrbit = moon.GetEphemeris(start, new CelestialBody(399), frame, Aberration.None).ToStateVector();
                Clock clk = new Clock("My clock", 256);
                Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, testOrbit, 0.5);
                scenario.AddSpacecraft(spc);
                scenario.AddCelestialItem(new CelestialBody(10));
                scenario.AddCelestialItem(new CelestialBody(399));
                scenario.AddCelestialItem(new Barycenter(1));
                scenario.AddCelestialItem(new Barycenter(2));
                scenario.AddCelestialItem(new Barycenter(4));
                scenario.AddCelestialItem(new Barycenter(5));
                scenario.AddCelestialItem(new Barycenter(6));
                scenario.AddCelestialItem(new Barycenter(7));
                scenario.AddCelestialItem(new Barycenter(8));
                var summary = await scenario.SimulateAsync( false, false, step);

                var spcSV = spc.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

                var delta = spcSV.RelativeTo(moon, Aberration.None).ToStateVector();
                var deltaP = delta.Position.Magnitude();
                var deltaV = delta.Velocity.Magnitude();

                await File.AppendAllTextAsync("AccuracyDeepSpace.csv", $"{DateTime.Now},{step},{deltaP},{deltaV}{Environment.NewLine}");

                if (stepSize == 1)
                {
                    Assert.True(deltaP < 1203);
                    Assert.True(deltaV < 2.7E-03);
                }
                else if (stepSize == 2)
                {
                    Assert.True(deltaP < 1243);
                    Assert.True(deltaV < 2.8E-03);
                }
                else if (stepSize == 5)
                {
                    Assert.True(deltaP < 1524);
                    Assert.True(deltaV < 3.5E-03);
                }
                else if (stepSize == 10)
                {
                    Assert.True(deltaP < 2563);
                    Assert.True(deltaV < 6.1E-03);
                }
                else if (stepSize == 20)
                {
                    Assert.True(deltaP < 6818);
                    Assert.True(deltaV < 1.7E-02);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        public async Task DeepSpaceMoon3D(int stepSize)
        {
            await semaphore.WaitAsync();

            try
            {
                var step = TimeSpan.FromMinutes(stepSize);
                var frame = Frames.Frame.ICRF;
                var start = TimeSystem.Time.J2000TDB;
                var end = start.AddDays(3);
                var earth = new CelestialBody(PlanetsAndMoons.EARTH, frame, start);
                var moon = new CelestialBody(PlanetsAndMoons.MOON, frame, start);
                Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("missionsdeepspace");
                Scenario scenario = new Scenario("scn" + stepSize, mission, new Window(start, end));

                StateVector testOrbit = moon.GetEphemeris(start, new CelestialBody(399), frame, Aberration.None).ToStateVector();
                Clock clk = new Clock("My clock", 256);
                Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, testOrbit, 0.5);
                scenario.AddSpacecraft(spc);
                scenario.AddCelestialItem(new CelestialBody(10));
                scenario.AddCelestialItem(new CelestialBody(399));
                scenario.AddCelestialItem(new Barycenter(1));
                scenario.AddCelestialItem(new Barycenter(2));
                scenario.AddCelestialItem(new Barycenter(4));
                scenario.AddCelestialItem(new Barycenter(5));
                scenario.AddCelestialItem(new Barycenter(6));
                scenario.AddCelestialItem(new Barycenter(7));
                scenario.AddCelestialItem(new Barycenter(8));
                var summary = await scenario.SimulateAsync( false, false, step);

                var spcSV = spc.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

                var delta = spcSV.RelativeTo(moon, Aberration.None).ToStateVector();
                var deltaP = delta.Position.Magnitude();
                var deltaV = delta.Velocity.Magnitude();

                await File.AppendAllTextAsync("AccuracyDeepSpace.csv", $"{DateTime.Now},{step},{deltaP},{deltaV}{Environment.NewLine}");

                if (stepSize == 1)
                {
                    Assert.True(deltaP < 1203);
                    Assert.True(deltaV < 2.7E-03);
                }
                else if (stepSize == 2)
                {
                    Assert.True(deltaP < 1243);
                    Assert.True(deltaV < 2.8E-03);
                }
                else if (stepSize == 5)
                {
                    Assert.True(deltaP < 1524);
                    Assert.True(deltaV < 3.5E-03);
                }
                else if (stepSize == 10)
                {
                    Assert.True(deltaP < 2563);
                    Assert.True(deltaV < 6.1E-03);
                }
                else if (stepSize == 20)
                {
                    Assert.True(deltaP < 6818);
                    Assert.True(deltaV < 1.7E-02);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }


        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        public async Task LowEarthOrbitAccuracy(int stepSize)
        {
            try
            {
                await semaphore.WaitAsync();

                var frame = Frames.Frame.ICRF;
                var start = TimeSystem.Time.J2000TDB;
                var end = start.AddHours(1.8);
                var earth = new CelestialBody(PlanetsAndMoons.EARTH, frame, start);
                Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("missAccuracyLEO");

                StateVector testOrbit = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 8000.0, 0), earth, start, frame);
                Clock clk = new Clock("My clock", 256);

                //-----------Scenario 10 s-----------------------------------
                Scenario scenario10s = new Scenario($"scn{stepSize}", mission, new Window(start, end));
                Spacecraft spc10s = new Spacecraft(-1002 - stepSize, $"Spc{stepSize}", 100.0, 10000.0, clk, testOrbit, 0.5);
                scenario10s.AddSpacecraft(spc10s);
                scenario10s.AddCelestialItem(earth);
                scenario10s.AddCelestialItem(new CelestialBody(10));
                scenario10s.AddCelestialItem(new CelestialBody(301));
                scenario10s.AddCelestialItem(new Barycenter(5));
                scenario10s.AddCelestialItem(new Barycenter(6));
                var step = TimeSpan.FromSeconds(stepSize);
                var summary2 = await scenario10s.SimulateAsync( false, false, step);

                var spc10SV = spc10s.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

                var delta = spc10SV.RelativeTo(spc10s,Aberration.None).ToStateVector();
                var deltaP = delta.Position.Magnitude();
                var deltaV = delta.Velocity.Magnitude();
                await File.AppendAllTextAsync("AccuracyLEO.csv", $"{DateTime.Now},{stepSize},{deltaP},{deltaV}{Environment.NewLine}");

                if (stepSize == 1)
                {
                    Assert.True(deltaP < 27);
                    Assert.True(deltaV < 0.031);
                }
                else if (stepSize == 2)
                {
                    Assert.True(deltaP < 84);
                    Assert.True(deltaV < 0.095);
                }
                else if (stepSize == 5)
                {
                    Assert.True(deltaP < 482);
                    Assert.True(deltaV < 0.55);
                }
                else if (stepSize == 10)
                {
                    Assert.True(deltaP < 1904);
                    Assert.True(deltaV < 2.17);
                }
                else if (stepSize == 20)
                {
                    Assert.True(deltaP < 7591);
                    Assert.True(deltaV < 8.7);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        [Fact]
        public void CheckHashCode()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission02");
            Scenario scenario = new Scenario("scn1", mission,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), new TimeSystem.Time(DateTime.MaxValue, TimeFrame.TDBFrame)));
            Scenario scenario2 = new Scenario("scn1", mission,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), new TimeSystem.Time(DateTime.MaxValue, TimeFrame.TDBFrame)));

            Assert.Equal(scenario2.GetHashCode(), scenario.GetHashCode());
        }
    }
}