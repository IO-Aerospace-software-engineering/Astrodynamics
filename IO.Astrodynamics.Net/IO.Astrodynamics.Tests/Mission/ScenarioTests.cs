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
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Mission
{
    public class ScenarioTests
    {
        // The fuel of the apsidal alignment, in kg: the half unit of the second decimal to which it was compared before
        // #501, an explicit band around the literal rather than a rounding whose boundary a move of 2.3e-4 kg crossed
        private const double FuelTolerance = 5e-3;

        // Bounds of PropagateSpacecraftFromTLE, m and m/s: twice the worst over Linux x64, Windows x64 and macOS arm64
        // (CI run 42490199772, 2026-11-08, the same on the three), 1.263e-2 m and 1.316e-5 m/s at the end (GMAT's TEME,
        // and 2.7 mm of SGP4 at the TDB instant, whose TT the library and CSPICE convert 1 us apart); 0.508 m and
        // 5.72e-4 m/s before #599 (tsince in TDB)
        private const double ScenarioTlePosition = 2.6e-2, ScenarioTleVelocity = 2.7e-5;

        private readonly ITestOutputHelper _output;

        public ScenarioTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Scenario scenario = new Scenario("Scenario", mission, new Window(new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), new TimeSystem.Time(2021, 1, 2, TimeScale.TDB)));
            Assert.Equal("Scenario", scenario.Name);
            Assert.Equal(mission, scenario.Mission);
        }

        [Fact]
        public void AddSite()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Scenario scenario = new Scenario("Scenario", mission, new Window(new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), new TimeSystem.Time(2021, 1, 2, TimeScale.TDB)));
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
            Scenario scenario = new Scenario("Scenario", mission, new Window(new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), new TimeSystem.Time(2021, 1, 2, TimeScale.TDB)));
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
            Assert.Single(scenario.CelestialBodies);
            Assert.Equal(TestHelpers.MoonAtJ2000, scenario.CelestialBodies.First());
            Assert.Throws<ArgumentNullException>(() => scenario.AddCelestialItem(null));
        }

        [Fact]
        public async Task PropagateSite()
        {
            Window window = new Window(new TimeSystem.Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB), new TimeSystem.Time(2000, 1, 2, 12, 0, 0, TimeScale.TDB));
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("MissionSite");
            Scenario scenario = new Scenario("scnSite", mission, window);
            Site site = new Site(333, "S333", TestHelpers.EarthAtJ2000, new Planetodetic(30 * Astrodynamics.Constants.Deg2Rad, 10 * Astrodynamics.Constants.Deg2Rad, 1000.0));
            scenario.AddSite(site);

            //Propagate site
            scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) });
            var res = site.GetEphemeris(window, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromHours(1));
            var orbitalParametersEnumerable = res as Astrodynamics.OrbitalParameters.OrbitalParameters[] ?? res.ToArray();
            Assert.Equal(new Vector3(4054782.9682010966, -4799280.7492771307, 1100392.3676426695), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Position,
                TestHelpers.VectorComparer);
            // Site states from SPICE (georec_c on the ellipsoid of pck00011, sxform_c; tools/spice_reference/spice_ref.c);
            // before #411 the site was 1.15 m away
            Assert.Equal(new Vector3(349.96683087822703, 295.68160057531139, 0.01769212216143207), orbitalParametersEnumerable.ElementAt(0).ToStateVector().Velocity,
                TestHelpers.VectorComparer);
            LabelAssert.Equal(TimeSystem.Time.J2000TDB, orbitalParametersEnumerable.ElementAt(0).Epoch);
            Assert.Equal(Frames.Frame.ICRF, orbitalParametersEnumerable.ElementAt(0).Frame);
            Assert.Equal(TestHelpers.EarthAtJ2000, orbitalParametersEnumerable.ElementAt(0).Observer);

            Assert.Equal(5675531.2672095764, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.X, 3);
            Assert.Equal(2694837.2897740607, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Y, 3);
            Assert.Equal(1100645.6327681679, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Position.Z, 3);
            Assert.Equal(-196.51288168954599, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.X, 3);
            Assert.Equal(413.86842722249719, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Y, 3);
            Assert.Equal(0.0062996653421883994, orbitalParametersEnumerable.ElementAt(5).ToStateVector().Velocity.Z, 3);
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
                new PlaneAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, targetOrbit, spacecraft.Engines.First());
            planeAlignmentManeuver.SetNextManeuver(new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, targetOrbit,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new PhasingManeuver(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, targetOrbit, 1, spacecraft.Engines.First()))
                .SetNextManeuver(new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, 15866666.666666666,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new ZenithAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new RetrogradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new ProgradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromMinutes(1), engine));
            spacecraft.SetStandbyManeuver(planeAlignmentManeuver);

            scenario.AddSpacecraft(spacecraft);

            var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) });

            // Read maneuver results. Each thrust window opens f dt before its impulse, f the lead of the centroid of the
            // burn's acceleration, about 0.504 here (#335): 1.32, 2.90, 1.58 and 1.50 s later than with the fixed 0.666 before
            // 11.0.0, the events, ΔV and fuel unchanged (the gates bind none of them)
            Astrodynamics.Maneuver.Maneuver maneuver1 = spacecraft.InitialManeuver;
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:45.0548076 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:45.0548076 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(8.1351551, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(-94.14445372426307, 104.53035584860734, -115.98373632250973), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);

            Assert.Equal(406.7577570325318, maneuver1.FuelBurned, 2);

            maneuver1 = maneuver1.NextManeuver;

            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:21.6668488 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:40.0565484 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:21.6668488 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:40.0565484 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(18.3896996, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(-373.24910751995867, -125.23695719049852, 201.38103911830422), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
            // 919.4849820926962 kg before #501: the parking orbit is given 47.3 h before the window, and the start state
            // is the two-body motion over the TT seconds to it, 2.96e-5 s fewer than the TDB seconds, 0.169 m along the
            // orbit (choice 8 of #501), then integrated in TT: 2.3e-4 kg more, 1.4e-3 kg per metre of the start. Held to
            // FuelTolerance, not to two decimals, whose rounding boundary the move crossed
            Assert.Equal(919.4852155798366, maneuver1.FuelBurned, FuelTolerance);

            maneuver1 = maneuver1.NextManeuver;

            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:09.0619185 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:19:32.5833681 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:09.0619185 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:18.8739093 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.8119908, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(-140.04335222731913, 85.94149400214036, 195.56134898377059), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(490.5995445453046, maneuver1.FuelBurned, 2);

            maneuver1 = maneuver1.NextManeuver;

            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:26.3228880 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:26.3228880 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.2946977, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(140.54731590683664, -86.30917047986902, -196.31882242781452), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(464.73488954797193, maneuver1.FuelBurned, 2);

            LabelAssert.Equal(scenario.Window, summary.Window);
            Assert.Single(summary.SpacecraftSummaries);
            var maneuverWindow1 = summary.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow1 != null)
            {
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525", TimeScale.TDB), maneuverWindow1.Value.StartDate, TestHelpers.TimeComparer);
                // The three attitudes chained after the last impulse activate at the end of its maneuver window (#539),
                // asserted above, and hold 60 s each, counted in TT seconds about the Earth (#547): 1e-9 s from TDB seconds
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB").AddSeconds(3 * 60.0), maneuverWindow1.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(2281.5771732185044, summary.SpacecraftSummaries.First().FuelConsumption, 2);
        }

        [Fact]
        public async Task PropagateSpacecraftWithAttitudeOnly()
        {
            TimeSystem.Time start = new TimeSystem.Time(2024, 7, 30, 13, 48, 30, TimeScale.UTC);
            TimeSystem.Time end = new TimeSystem.Time(2024, 7, 31, 13, 48, 33, TimeScale.UTC);

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

            var progradeAttitude = new ProgradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, engine);
            spacecraft.SetStandbyManeuver(progradeAttitude);

            scenario.AddSpacecraft(spacecraft);

            var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) });

            // Read maneuver results
            Assert.Equal(TimeSystem.Time.Parse("2024-07-30T13:49:39.1833013 TDB"), progradeAttitude.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2024-07-30T13:49:39.1833013 TDB"), progradeAttitude.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2024-07-30T13:49:39.1833013 TDB"), progradeAttitude.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2024-07-30T13:49:39.1833013 TDB"), progradeAttitude.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(0.0, progradeAttitude.ThrustWindow.Value.Length.TotalSeconds, 3);

            Assert.Equal(0.0, progradeAttitude.FuelBurned, 0);


            LabelAssert.Equal(scenario.Window, summary.Window);
            Assert.Single(summary.SpacecraftSummaries);
            var maneuverWindow = summary.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow != null)
            {
                Assert.Equal(TimeSystem.Time.Parse("2024-07-30T13:49:39.1839998", TimeScale.TDB), maneuverWindow.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2024-07-30T13:49:39.1839998", TimeScale.TDB), maneuverWindow.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(0.0, summary.SpacecraftSummaries.First().FuelConsumption, 2);
        }

        [Fact]
        public void PropagateSpacecraftFromTLE()
        {
            TimeSystem.Time start = new TimeSystem.Time(2024, 06, 03, 0, 0, 0, TimeScale.TDB);
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

            var site = new Site(63, "DSS-63", TestHelpers.EarthAtJ2000);
            var initialSV = spacecraft.GetEphemeris(start, site, Frames.Frame.ICRF, Aberration.None);
            var endSV = spacecraft.GetEphemeris(end, site, Frames.Frame.ICRF, Aberration.None);

            // Reference: the SGP4 state of Vallado's code (sgp4 2.27, WGS-72 old, AFSPC mode, the model of CSPICE's evsgp4_c)
            // through GMAT R2026a's TEME -> EarthICRF, minus DSS-63 from CSPICE (spkezr in J2000, the test kernels):
            // "ISS 2024 start" and "ISS 2024 end" of tools/teme_reference (#451)
            foreach (var (name, state) in new[] { ("ISS 2024 start", initialSV.ToStateVector()), ("ISS 2024 end", endSV.ToStateVector()) })
            {
                var reference = Frame.TemeFrameTests.Reference(name);
                LabelAssert.Equal(Frame.TemeFrameTests.Instant(reference.Instant), state.Epoch);
                var position = Frame.TemeFrameTests.Position(reference.Gmat) - Frame.TemeFrameTests.Position(reference.Dss63J2000);
                var velocity = Frame.TemeFrameTests.Velocity(reference.Gmat) - Frame.TemeFrameTests.Velocity(reference.Dss63J2000);
                double dr = (state.Position - position).Magnitude(), dv = (state.Velocity - velocity).Magnitude();
                // Twice the worst measured on the three platforms, GMAT's TEME (its kinematic terms of the
                // equation of the equinoxes); 100 m and 0.2 m/s before #451, 1.1 m and 1.2e-3 m/s before #599
                _output.WriteLine($"MEASURE ScenarioTests.PropagateSpacecraftFromTLE, {name}: {dr:E3} m, {dv:E3} m/s");
                Assert.InRange(dr, 0.0, ScenarioTlePosition);
                Assert.InRange(dv, 0.0, ScenarioTleVelocity);
            }

            // Verify reference frame and epoch
            Assert.Equal(Frames.Frame.ICRF, initialSV.ToStateVector().Frame);
            Assert.Equal(Frames.Frame.ICRF, endSV.ToStateVector().Frame);
            LabelAssert.Equal(start, initialSV.ToStateVector().Epoch);
            LabelAssert.Equal(end, endSV.ToStateVector().Epoch);
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
                new PlaneAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, targetOrbit, spacecraft.Engines.First());
            planeAlignmentManeuver.SetNextManeuver(new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, targetOrbit,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new PhasingManeuver(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, targetOrbit, 1, spacecraft.Engines.First()))
                .SetNextManeuver(new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, 15866666.666666666,
                    spacecraft.Engines.First()))
                .SetNextManeuver(new ZenithAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new RetrogradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromMinutes(1), engine))
                .SetNextManeuver(new ProgradeAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromMinutes(1), engine));
            spacecraft.SetStandbyManeuver(planeAlignmentManeuver);

            scenario.AddSpacecraft(spacecraft);

            {
                var summary1 = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) });

                // Read maneuver results
                // The windows of PropagateSpacecraft, the burn's acceleration centred on each impulse (#335)
                Astrodynamics.Maneuver.Maneuver maneuver1 = spacecraft.InitialManeuver;
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:45.0548076 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:45.0548076 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(8.1351551, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
                Assert.Equal(new Vector3(-94.14445372426307, 104.53035584860734, -115.98373632250973), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);

                Assert.Equal(406.7577570325318, maneuver1.FuelBurned, 2);

                maneuver1 = maneuver1.NextManeuver;

                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:21.6668488 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:40.0565484 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:21.6668488 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:40.0565484 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(18.3896996, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
                Assert.Equal(new Vector3(-373.24910751995867, -125.23695719049852, 201.38103911830422), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
                // 919.4849820926962 kg before #501: the start state of choice 8, as above
                Assert.Equal(919.4852155798366, maneuver1.FuelBurned, FuelTolerance);

                maneuver1 = maneuver1.NextManeuver;

                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:09.0619185 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:19:32.5833681 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:09.0619185 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:18.8739093 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(9.8119908, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
                Assert.Equal(new Vector3(-140.04335222731913, 85.94149400214036, 195.56134898377059), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
                Assert.Equal(490.5995445453046, maneuver1.FuelBurned, 2);

                maneuver1 = maneuver1.NextManeuver;

                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:26.3228880 TDB"), maneuver1.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB"), maneuver1.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:26.3228880 TDB"), maneuver1.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB"), maneuver1.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
                Assert.Equal(9.2946977, maneuver1.ThrustWindow.Value.Length.TotalSeconds, 2);
                Assert.Equal(new Vector3(140.54731590683664, -86.30917047986902, -196.31882242781452), ((ImpulseManeuver)maneuver1).DeltaV, TestHelpers.VectorComparer);
                Assert.Equal(464.73488954797193, maneuver1.FuelBurned, 2);

                LabelAssert.Equal(scenario.Window, summary1.Window);
                Assert.Single(summary1.SpacecraftSummaries);
                var maneuverWindow1 = summary1.SpacecraftSummaries.First().ManeuverWindow;
                if (maneuverWindow1 != null)
                {
                    Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525", TimeScale.TDB), maneuverWindow1.Value.StartDate, TestHelpers.TimeComparer);
                    // The three attitudes chained after the last impulse activate at the end of its maneuver window (#539),
                    // asserted above, and hold 60 s each, counted in TT seconds about the Earth (#547): 1e-9 s from TDB seconds
                    Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB").AddSeconds(3 * 60.0), maneuverWindow1.Value.EndDate, TestHelpers.TimeComparer);
                }

                Assert.Equal(2281.5771732185044, summary1.SpacecraftSummaries.First().FuelConsumption, 2);
            }
            var summary2 = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) });
            // Read maneuver results
            Astrodynamics.Maneuver.Maneuver maneuver2 = spacecraft.InitialManeuver;
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:45.0548076 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:45.0548076 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(8.1351551, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(-94.14445372426307, 104.53035584860734, -115.98373632250973), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);

            Assert.Equal(406.7577570325318, maneuver2.FuelBurned, 2);

            maneuver2 = maneuver2.NextManeuver;

            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:21.6668488 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:40.0565484 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:21.6668488 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T01:19:40.0565484 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(18.3896996, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(-373.24910751995867, -125.23695719049852, 201.38103911830422), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(919.4852155798366, maneuver2.FuelBurned, FuelTolerance);

            maneuver2 = maneuver2.NextManeuver;

            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:09.0619185 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:19:32.5833681 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:09.0619185 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T04:36:18.8739093 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.8119908, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(-140.04335222731913, 85.94149400214036, 195.56134898377059), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(490.5995445453046, maneuver2.FuelBurned, 2);

            maneuver2 = maneuver2.NextManeuver;

            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:26.3228880 TDB"), maneuver2.ManeuverWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB"), maneuver2.ManeuverWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:26.3228880 TDB"), maneuver2.ThrustWindow!.Value.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB"), maneuver2.ThrustWindow!.Value.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(9.2946977, maneuver2.ThrustWindow.Value.Length.TotalSeconds, 2);
            Assert.Equal(new Vector3(140.54731590683664, -86.30917047986902, -196.31882242781452), ((ImpulseManeuver)maneuver2).DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(464.73488954797193, maneuver2.FuelBurned, 2);

            LabelAssert.Equal(scenario.Window, summary2.Window);
            Assert.Single(summary2.SpacecraftSummaries);
            var maneuverWindow2 = summary2.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow2 != null)
            {
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T00:34:36.9196525", TimeScale.TDB), maneuverWindow2.Value.StartDate, TestHelpers.TimeComparer);
                // The three attitudes chained after the last impulse activate at the end of its maneuver window (#539),
                // asserted above, and hold 60 s each, counted in TT seconds about the Earth (#547): 1e-9 s from TDB seconds
                Assert.Equal(TimeSystem.Time.Parse("2021-03-04T08:44:35.6175857 TDB").AddSeconds(3 * 60.0), maneuverWindow2.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(2281.5771732185044, summary2.SpacecraftSummaries.First().FuelConsumption, 2);
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
            var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) });

            LabelAssert.Equal(scenario.Window, summary.Window);
            Assert.Single(summary.SpacecraftSummaries);
            var maneuverWindow = summary.SpacecraftSummaries.First().ManeuverWindow;
            if (maneuverWindow != null)
            {
                Assert.Equal(new TimeSystem.Time(2021, 3, 4, 0, 32, 53, 814, TimeScale.TDB), maneuverWindow.Value.StartDate, TestHelpers.TimeComparer);
                Assert.Equal(new TimeSystem.Time(2021, 3, 4, 5, 27, 13, 014, TimeScale.TDB), maneuverWindow.Value.EndDate, TestHelpers.TimeComparer);
            }

            Assert.Equal(0.0, summary.SpacecraftSummaries.First().FuelConsumption, 2);
        }

        [Fact]
        [Benchmark]
        public async Task PropagateStar()
        {
            var start = new TimeSystem.Time(2001, 1, 1, TimeScale.TDB);
            var end = start.AddDays(365 * 4);
            var observer = Barycenters.SOLAR_SYSTEM_BARYCENTER;
            var star = new Star(2, "star2", 1E+30, "spec", 2, 0.3792, Equatorial.FromRightAscensionDeclination(1, 1, start), 0.1, 0.1, 0, 0, 0, 0, start);

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission102");
            Scenario scenario = new Scenario("scn100", mission, new Window(start, end));
            scenario.AddStar(star);

            var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromDays(365.0) }, OutputStep = TimeSpan.FromDays(365.0) });

            LabelAssert.Equal(scenario.Window, summary.Window);

            // The straight line of the star (#718) at 0, 365 and 730 days, written in StarTests: before, the angles 1, 1.1 and 1.2
            // rad (to 3 decimals) at the catalog distance
            foreach (var at in new[] { start, start.Add(TimeSpan.FromDays(365)), start.Add(TimeSpan.FromDays(365 + 365)) })
            {
                var eph = star.GetEphemeris(at, observer, Frames.Frame.ICRF, Aberration.None);
                var expected = IO.Astrodynamics.Tests.Body.StarTests.CatalogPosition(star, at);
                double relative = (eph.Position - expected).Magnitude() / expected.Magnitude();
                _output.WriteLine($"MEASURE ScenarioTests.PropagateStar ({(at - start).TotalDays} d): relative position {relative:E3}");
                Assert.True(relative <= IO.Astrodynamics.Tests.Body.StarTests.StraightLinePositionTolerance, $"{relative:E3}");
            }
        }


        [Fact]
        public async Task Simulate_PropagatesTheStar_IsPropagated()
        {
            // Arrange: IsPropagated was always false before (it read an output directory no code set); the SPK of the star
            // against CSPICE is the Validation test below
            var start = new TimeSystem.Time(2001, 1, 1, TimeScale.TDB);
            var end = start.AddDays(365 * 4);
            var star = new Star(668006, "star668006", 1E+30, "spec", 2, 0.3792, Equatorial.FromRightAscensionDeclination(1, 1, start), 0.1, 0.1, 0, 0, 0, 0,
                start);
            var scenario = new Scenario("scn668b", new Astrodynamics.Mission.Mission("mission668b"), new Window(start, end));
            scenario.AddStar(star);
            Assert.False(star.IsPropagated);

            // Act
            scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromDays(365.0) }, OutputStep = TimeSpan.FromDays(365.0) });

            // Assert
            Assert.True(star.IsPropagated);
        }

        [Fact]
        public async Task Simulate_OfAStarPropagatedBefore_ReplacesItsStates()
        {
            // Arrange: one star in two scenarios over different windows, their steps different; before, the second
            // simulation added its states to the first's, so that the SPK of the star covered both windows and the gap
            var star = IO.Astrodynamics.Tests.Body.StarTests.NewStar(668014, new TimeSystem.Time(2001, 1, 1, TimeScale.TDB));
            var firstWindow = new Window(new TimeSystem.Time(2001, 1, 1, TimeScale.TDB), new TimeSystem.Time(2001, 1, 5, TimeScale.TDB));
            var secondWindow = new Window(new TimeSystem.Time(2001, 3, 1, TimeScale.TDB), new TimeSystem.Time(2001, 3, 3, TimeScale.TDB));
            var first = new Scenario("scn668c", new Astrodynamics.Mission.Mission("mission668c"), firstWindow);
            var second = new Scenario("scn668d", new Astrodynamics.Mission.Mission("mission668d"), secondWindow);
            first.AddStar(star);
            second.AddStar(star);

            // Act: the star's step is the propagator's step here (the window over 1000 is shorter)
            first.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromDays(1.0) }, OutputStep = TimeSpan.FromDays(1.0) });
            second.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromHours(12.0) }, OutputStep = TimeSpan.FromHours(12.0) });

            // Assert: the second simulation's states alone, as Spacecraft.Propagate replaces its states
            var replaced = IO.Astrodynamics.Tests.Body.StarTests.Grid(firstWindow, TimeSpan.FromDays(1.0))
                .Append(new TimeSystem.Time(2001, 2, 1, TimeScale.TDB));
            var kept = IO.Astrodynamics.Tests.Body.StarTests.Grid(secondWindow, TimeSpan.FromHours(12.0));
            double worst = IO.Astrodynamics.Tests.Body.StarTests.CheckSpkHoldsOnly(star, kept, replaced,
                "ScenarioTests.Simulate_OfAStarPropagatedBefore", _output);
            double bound = IO.Astrodynamics.Tests.Body.StarTests.CatalogMotionInOneMicrosecond(star, kept);
            Assert.True(worst <= bound, $"{worst:E3} > {bound:E3}");
        }

        // The reference position is CSPICE's latrec_c through CspiceDirect, which the Windows DLL does not export (Linux, Validation)
        [Fact]
        [Trait("Category", "Validation")]
        public async Task Simulate_PropagatesTheStar_AndItsSpkHoldsTheCatalogPositionWithProperMotion()
        {
            // Arrange: IsPropagated was always false before (it read an output directory no code set)
            var start = new TimeSystem.Time(2001, 1, 1, TimeScale.TDB);
            var end = start.AddDays(365 * 4);
            var star = new Star(668005, "star668005", 1E+30, "spec", 2, 0.3792, Equatorial.FromRightAscensionDeclination(1, 1, start), 0.1, 0.1, 0, 0, 0, 0,
                start);
            var scenario = new Scenario("scn668", new Astrodynamics.Mission.Mission("mission668"), new Window(start, end));
            scenario.AddStar(star);
            var step = TimeSpan.FromDays(365.0);
            Assert.False(star.IsPropagated);

            // Act
            scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step });

            // Assert: the stored states exist, and SPICE reads them back from the SPK the star writes, against the catalog
            // position moved along its straight line (#718): after k steps of 365 days, dt = 365 k / 365.25 Julian years,
            // the catalog position by CSPICE (latrec_c, the computation of radrec_c) plus r0 (μα* dt e_α + μδ dt e_δ)
            // (StarTests.SpiceStraightLinePosition)
            Assert.True(star.IsPropagated);
            var directory = Directory.CreateTempSubdirectory("star668");
            var spk = new FileInfo(Path.Combine(directory.FullName, "star668005.bsp"));
            star.WriteEphemeris(spk);
            SpiceAPI.Instance.LoadKernels(spk);
            try
            {
                double worst = 0.0;
                int k = 0;
                for (var epoch = start; epoch <= end; epoch += step, k++)
                {
                    var read = SpiceAPI.Instance.ReadEphemeris(epoch, 0, star.NaifId, Frames.Frame.ICRF, Aberration.None);
                    var catalog = IO.Astrodynamics.Tests.Body.StarTests.SpiceStraightLinePosition(star, 365m * k / 365.25m);
                    double relative = (read.Position - catalog).Magnitude() / catalog.Magnitude();
                    _output.WriteLine($"MEASURE ScenarioTests.Simulate_PropagatesTheStar: step {k}, relative position {relative:E3}");
                    worst = System.Math.Max(worst, relative);
                }

                Assert.Equal(5, k);

                // Twice the worst against the straight line from latrec_c on Linux x64, rounded up at the second digit
                // (Validation category, Linux only): 2.182e-16 (CI run 43974478027, 2026-11-16; 1.982e-16 for the linear angles
                // before #718, CI run 43773937161). The distance is the star's own
                // (Distance, from its parallax): the parsec of the library is not what this test measures
                _output.WriteLine($"MEASURE ScenarioTests.Simulate_PropagatesTheStar: worst relative position {worst:E3}");
                Assert.True(worst <= 4.4e-16, $"{worst:E3}");
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(spk);
                directory.Delete(true);
            }
        }

        [Fact]
        public void Equality()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission02");
            Scenario scenario = new Scenario("scn1", mission,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), new TimeSystem.Time(DateTime.MaxValue, TimeScale.TDB)));

            Astrodynamics.Mission.Mission mission2 = new Astrodynamics.Mission.Mission("mission03");
            Scenario scenario2 = new Scenario("scn1", mission2,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), new TimeSystem.Time(DateTime.MaxValue, TimeScale.TDB)));

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
        public async Task Simulate_WithAnIntegrator_MatchesDirectPropagationWithThatIntegrator()
        {
            // Arrange
            var window = new Window(new TimeSystem.Time(2021, 1, 1, 12, 0, 0, TimeScale.TDB), new TimeSystem.Time(2021, 1, 1, 14, 0, 0, TimeScale.TDB));
            var step = TimeSpan.FromSeconds(10.0);
            var scenario = CreateIntegratorScenario(window);
            var simulated = CreateLeoSpacecraft(-1801, "RK78A", window.StartDate);
            scenario.AddSpacecraft(simulated);
            var reference = CreateLeoSpacecraft(-1802, "RK78B", window.StartDate);
            var velocityVerlet = CreateLeoSpacecraft(-1811, "VVC", window.StartDate);

            // Act
            scenario.Simulate(new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = step, CovariancePropagation = CovariancePropagationOptions.None });
            reference.Propagate(window, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = step, ThirdBodies = scenario.CelestialBodies.ToArray(), CovariancePropagation = CovariancePropagationOptions.None });
            velocityVerlet.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step, ThirdBodies = scenario.CelestialBodies.ToArray() });

            // Assert: the same integrator gives bit-identical states, the default one does not.
            Assert.True(simulated.IsPropagated);
            AssertSameEphemeris(reference, simulated, window);
            var earth = TestHelpers.EarthAtJ2000;
            Assert.NotEqual(velocityVerlet.GetEphemeris(window.EndDate, earth, Frames.Frame.ICRF, Aberration.None).Position,
                simulated.GetEphemeris(window.EndDate, earth, Frames.Frame.ICRF, Aberration.None).Position);
        }

        private static Scenario CreateIntegratorScenario(Window window)
        {
            var scenario = new Scenario("Integrator", new Astrodynamics.Mission.Mission("IntegratorChoice"), window);
            scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
            scenario.AddCelestialItem(TestHelpers.Sun);
            return scenario;
        }

        private static Spacecraft CreateLeoSpacecraft(int naifId, string name, TimeSystem.Time epoch)
        {
            var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5420.0, 5420.0),
                TestHelpers.EarthAtJ2000, epoch, Frames.Frame.ICRF);
            return new Spacecraft(naifId, name, 1000.0, 10000.0, new Clock("clk" + name, 65536), orbit);
        }

        private static void AssertSameEphemeris(Spacecraft expected, Spacecraft actual, Window window)
        {
            var earth = TestHelpers.EarthAtJ2000;
            for (var epoch = window.StartDate; epoch <= window.EndDate; epoch = epoch.AddSeconds(600.0))
            {
                var expectedState = expected.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None);
                var actualState = actual.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None);
                Assert.Equal(expectedState.Position, actualState.Position);
                Assert.Equal(expectedState.Velocity, actualState.Velocity);
            }
        }

        /// <summary>
        /// A plane alignment whose target is in ECLIPJ2000, built before its tank is on a spacecraft propagated in ICRF, is
        /// rejected when the simulation builds the propagation of that spacecraft, as by <see cref="Spacecraft.Propagate(Window, PropagationOptions)"/> (#685):
        /// it burned 2807 m/s for a plane change of 2° (263 m/s), the target's components read in ICRF.
        /// </summary>
        [Fact]
        public async Task Simulate_TargetOrbitInAnotherFrame_IsRejected()
        {
            // Arrange
            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2.0));
            var earth = TestHelpers.EarthAtJ2000;
            var orbit = new KeplerianElements(7000000.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 1.0, earth, window.StartDate,
                Frames.Frame.ICRF);
            var target = new KeplerianElements(7000000.0, 0.0, 32.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 1.0, earth, window.StartDate,
                Frames.Frame.ICRF).ToFrame(Frames.Frame.ECLIPTIC_J2000);
            var tank = new FuelTank("ft685", "ftA", "sn685", 1000.0, 900.0);
            var engine = new Engine("eng685", "engmk1", "sn685", 450, 50, tank);
            var maneuver = new PlaneAlignmentManeuver(window.StartDate, TimeSpan.Zero, target, engine);
            var spacecraft = new Spacecraft(-6855, "Scenario685", 1000.0, 3000.0, new Clock("clk685", 65536), orbit);
            spacecraft.AddFuelTank(tank);
            spacecraft.AddEngine(engine);
            spacecraft.SetStandbyManeuver(maneuver);
            var scenario = new Scenario("scn685", new Astrodynamics.Mission.Mission("mission685"), window);
            scenario.AddSpacecraft(spacecraft);

            // Act
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }, OutputStep = TimeSpan.FromSeconds(10.0) })));

            // Assert
            Assert.Equal("spacecraft", exception.ParamName);
            Assert.Contains("ToFrame(Frame.ICRF)", exception.Message);
            Assert.False(spacecraft.IsPropagated);
            Assert.Null(maneuver.ThrustWindow);
        }

        [Fact]
        public async Task PropagateException()
        {
            TimeSystem.Time startPropagator = TimeSystem.Time.CreateUTC(668085555.829810).ToTDB();
            TimeSystem.Time end = TimeSystem.Time.CreateUTC(668174400.000000).ToTDB();

            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission04");
            Scenario scenario = new Scenario("scn1", mission, new Window(startPropagator, end));
            Assert.Throws<ArgumentNullException>(() => scenario.AddSpacecraft(null));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0) }));
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

            // Use SSB-relative orbit to test deep-space propagation (Sun as central body)
            var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
            StateVector testOrbit = moon.GetEphemeris(start, new CelestialBody(399), frame, Aberration.None).RelativeTo(ssb, Aberration.None).ToStateVector();
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
            var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(100.0) }, OutputStep = TimeSpan.FromSeconds(100.0) });

            var spcSV = spc.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None);
            var moonSV = moon.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None);

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

                // Use SSB-relative orbit to test deep-space propagation (Sun as central body)
                var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
                StateVector testOrbit = moon.GetEphemeris(start, new CelestialBody(399), frame, Aberration.None).RelativeTo(ssb, Aberration.None).ToStateVector();
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
                var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step });

                var spcSV = spc.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None);

                var delta = spcSV.RelativeTo(moon, Aberration.None).ToStateVector();
                var deltaP = delta.Position.Magnitude();
                var deltaV = delta.Velocity.Magnitude();

                await File.AppendAllTextAsync("AccuracyDeepSpace.csv", $"{DateTime.Now},{step},{deltaP},{deltaV}{Environment.NewLine}");
                // Tolerances updated for Sun-centered propagation with Battin's third-body formula.
                // The old SsbPropagator used direct N-body in SSB frame; the new CentralBodyPropagator
                // uses Sun as central body with Earth/planets as Battin perturbations (industry standard).
                // Both are physically equivalent but have slightly different numerical characteristics (~15-25% larger errors).
                if (stepSize == 1)
                {
                    Assert.True(deltaP < 1750);
                    Assert.True(deltaV < 4.2E-03);
                }
                else if (stepSize == 2)
                {
                    Assert.True(deltaP < 1800);
                    Assert.True(deltaV < 4.3E-03);
                }
                else if (stepSize == 5)
                {
                    Assert.True(deltaP < 2200);
                    Assert.True(deltaV < 5.3E-03);
                }
                else if (stepSize == 10)
                {
                    Assert.True(deltaP < 3500);
                    Assert.True(deltaV < 8.5E-03);
                }
                else if (stepSize == 20)
                {
                    Assert.True(deltaP < 9000);
                    Assert.True(deltaV < 2.2E-02);
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

                // Use SSB-relative orbit to test deep-space propagation (Sun as central body)
                var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
                StateVector testOrbit = moon.GetEphemeris(start, new CelestialBody(399), frame, Aberration.None).RelativeTo(ssb, Aberration.None).ToStateVector();
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
                var summary = scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step });

                var spcSV = spc.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None);

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
                var summary2 = scenario10s.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step });

                var spc10SV = spc10s.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None);

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
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), new TimeSystem.Time(DateTime.MaxValue, TimeScale.TDB)));
            Scenario scenario2 = new Scenario("scn1", mission,
                new Window(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), new TimeSystem.Time(DateTime.MaxValue, TimeScale.TDB)));

            Assert.Equal(scenario2.GetHashCode(), scenario.GetHashCode());
        }
    }
}

