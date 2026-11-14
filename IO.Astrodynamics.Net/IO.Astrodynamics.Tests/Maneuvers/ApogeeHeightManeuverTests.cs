using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class ApogeeHeightManeuverTests
    {
        private readonly ITestOutputHelper _output;

        public ApogeeHeightManeuverTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        // Relative agreements with the Hohmann closed form, worst: ΔV 1.33e-14, apogee radius after the burn 1.01e-15, on
        // Linux x64, Windows x64 and macOS arm64 alike (CI run 43595588856, 2026-11-14); twice the worst
        private const double HohmannDeltaVRelative = 2.7e-14;
        private const double HohmannApogeeRelative = 2.1e-15;

        /// <summary>
        /// The target of an <see cref="ApogeeHeightManeuver"/> is a radius from the centre of the maneuver centre, in m, not an
        /// altitude (#677): from a 400 km circular orbit about the Earth, a target of R + 1 000 km gives the first burn of the
        /// Hohmann transfer to that radius and an apogee at that radius after the burn, both computed here by vis-viva.
        /// </summary>
        [Fact]
        public void TargetIsARadiusFromTheCentre()
        {
            var earth = TestHelpers.EarthAtJ2000;
            double mu = earth.GM;
            double r1 = earth.EquatorialRadius + 400e3;
            double r2 = earth.EquatorialRadius + 1000e3;
            var state = new StateVector(new Vector3(r1, 0.0, 0.0), new Vector3(0.0, System.Math.Sqrt(mu / r1), 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-1110, "GenericSpacecraft1110", 1000.0, 3000.0, new Clock("GenericClk1110", 65536), state);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            var maneuver = new ApogeeHeightManeuver(earth, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, r2, spc.Engines.First());

            var after = maneuver.TryExecute(state).sv;

            // The first burn of the Hohmann transfer from r1 to r2
            double hohmann = System.Math.Sqrt(mu / r1) * (System.Math.Sqrt(2.0 * r2 / (r1 + r2)) - 1.0);
            double deltaV = System.Math.Abs(maneuver.DeltaV.Magnitude() - hohmann) / hohmann;
            // The apogee after the burn by vis-viva from the state after it, at the perigee: a = 1 / (2 / r − v² / μ), r_a = 2a − r
            double r = after.Position.Magnitude();
            double v = after.Velocity.Magnitude();
            double a = 1.0 / (2.0 / r - v * v / mu);
            double apogee = System.Math.Abs(2.0 * a - r - r2) / r2;
            _output.WriteLine($"MEASURE #677 apogee radius target: ΔV {maneuver.DeltaV.Magnitude():R} m/s against {hohmann:R} m/s, relative {deltaV:E2}; " +
                              $"apogee {2.0 * a - r:R} m against {r2:R} m, relative {apogee:E2}");
            Assert.True(deltaV <= HohmannDeltaVRelative, $"ΔV relative {deltaV:E3}");
            Assert.True(apogee <= HohmannApogeeRelative, $"apogee relative {apogee:E3}");
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            ApogeeHeightManeuver apogeeHeightManeuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0), 151000000.0, eng);

            Assert.NotNull(apogeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), apogeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), apogeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, apogeeHeightManeuver.TargetApogee);
        }

        [Fact]
        public void Create2()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            ApogeeHeightManeuver apogeeHeightManeuver = new ApogeeHeightManeuver(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0),
                new KeplerianElements(151000000, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), eng);

            Assert.NotNull(apogeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), apogeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), apogeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, apogeeHeightManeuver.TargetApogee);
        }

        [Fact]
        public void TryExecuteIncreaseApogee()
        {
            var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApogeeHeightManeuver maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 42164000.0, spc.Engines.First());

            var res = maneuver.TryExecute(orbitalParams);
            // The state after the impulse, the given state unchanged (#588)
            Assert.Equal(new StateVector(orbitalParams.Position, orbitalParams.Velocity + maneuver.DeltaV, orbitalParams.Observer, orbitalParams.Epoch, orbitalParams.Frame), res.sv);
            Assert.Equal(new StateOrientation(Quaternion.Identity, Vector3.Zero, orbitalParams.Epoch, orbitalParams.Frame), res.so);
            Assert.Equal(new Vector3(0.0, 2424.6084273080614, 0.0), maneuver.DeltaV);
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 11, 59, 49, 301, 844), TimeSpan.FromSeconds(16.0632973)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 11, 59, 49, 301, 844), TimeSpan.FromSeconds(16.0632973)), maneuver.ManeuverWindow);
            Assert.Equal(803.16486707364015, maneuver.FuelBurned);
        }
        
        [Fact]
        public void TryExecuteDecreaseApogee()
        {
            var orbitalParams = new StateVector(new Vector3(42164000.0, 0.0, 0.0), new Vector3(0.0, 3075.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApogeeHeightManeuver maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 6678000.0, spc.Engines.First());

            var res = maneuver.TryExecute(orbitalParams);
            // The state after the impulse, the given state unchanged (#588)
            Assert.Equal(new StateVector(orbitalParams.Position, orbitalParams.Velocity + maneuver.DeltaV, orbitalParams.Observer, orbitalParams.Epoch, orbitalParams.Frame), res.sv);
            Assert.Equal(new StateOrientation(new Quaternion(0.0,-1.0,0.0,0.0), Vector3.Zero, orbitalParams.Epoch, orbitalParams.Frame), res.so);
            Assert.Equal(new Vector3(0.0, -1467.1724438487042, 0.0), maneuver.DeltaV);
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 11, 59, 52, 841, 699).AddTicks(3), TimeSpan.FromSeconds(10.7481993)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 11, 59, 52, 841, 699).AddTicks(3), TimeSpan.FromSeconds(10.7481993)), maneuver.ManeuverWindow);
            Assert.Equal(537.40996740060154, maneuver.FuelBurned);
        }

        /// <summary>
        /// The OPM parameters of an executed impulse are its own ignition, duration, mass and delta-V in CCSDS units (km/s,
        /// the delta-V divided by 1000, the mass expelled negative); an impulse not executed has none.
        /// </summary>
        [Fact]
        public void ToOpmManeuverParameters_AreTheExecutedImpulseInCcsdsUnits()
        {
            var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-1108, "GenericSpacecraft1108", 1000.0, 3000.0, new Clock("GenericClk1108", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            var maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 42164000.0,
                spc.Engines.First());

            Assert.Throws<InvalidOperationException>(() => maneuver.ToOpmManeuverParameters());

            maneuver.TryExecute(orbitalParams);
            var parameters = maneuver.ToOpmManeuverParameters("ICRF", new[] { "apogee" });
            // The ignition in UTC, the time system of the OPM, whatever the frame of the thrust window (#479)
            Assert.Equal(maneuver.ThrustWindow!.Value.StartDate.ToUTC().DateTime, parameters.ManEpochIgnition);
            Assert.Equal(maneuver.ThrustWindow.Value.Length.TotalSeconds, parameters.ManDuration);
            Assert.Equal(-maneuver.FuelBurned, parameters.ManDeltaMass);
            Assert.Equal("ICRF", parameters.ManRefFrame);
            Assert.Equal(maneuver.DeltaV.X / 1000.0, parameters.ManDv1);
            Assert.Equal(maneuver.DeltaV.Y / 1000.0, parameters.ManDv2);
            Assert.Equal(maneuver.DeltaV.Z / 1000.0, parameters.ManDv3);
            Assert.Equal(new[] { "apogee" }, parameters.Comments);
        }

        /// <summary>A target orbit in another frame than the spacecraft's initial state is rejected.</summary>
        [Fact]
        public void ATargetOrbitInAnotherFrame_IsRejected()
        {
            var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-1109, "GenericSpacecraft1109", 1000.0, 3000.0, new Clock("GenericClk1109", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            var target = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ECLIPTIC_J2000);

            Assert.Throws<ArgumentException>(() => new PlaneAlignmentManeuver(TimeSystem.Time.J2000TDB, TimeSpan.Zero, target, spc.Engines.First()));
        }
    }
}
