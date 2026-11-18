using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class PerigeeHeightManeuverTests
    {
        public PerigeeHeightManeuverTests()
        {
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PerigeeHeightManeuver perigeeHeightManeuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0), 151000000.0, eng);

            Assert.NotNull(perigeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), perigeeHeightManeuver.ManeuverHoldDuration);
            LabelAssert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), perigeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, perigeeHeightManeuver.TargetPerigeeHeight);
        }

        [Fact]
        public void Create2()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PerigeeHeightManeuver perigeeHeightManeuver = new PerigeeHeightManeuver(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0),
                new KeplerianElements(151000000, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), eng);

            Assert.NotNull(perigeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), perigeeHeightManeuver.ManeuverHoldDuration);
            LabelAssert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), perigeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, perigeeHeightManeuver.TargetPerigeeHeight);
        }

        // The angle the velocity turns by over a tick, |a| 1e-7 s / |v|: the resolution of an epoch label at an apogee
        private static double ApogeeTurn(StateVector state) =>
            TestHelpers.EarthAtJ2000.GM / state.Position.MagnitudeSquared() * 1e-7 / state.Velocity.Magnitude();

        [Fact]
        public void TryExecuteIncreasePerigee()
        {
            var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 42164000.0, spc.Engines.First());

            // The apogee, half a period after the perigee in TT seconds, the time of a two-body motion about the Earth
            // (#501, #508); Epoch + Period() * 0.5 adds TDB seconds, n·δ off the apogee
            var maneuverPoint = orbitalParams.ToStateVector(
                PropagationTimeArgument.Tt.EpochAt(orbitalParams.Epoch, orbitalParams.Period().TotalSeconds * 0.5));
            var res = maneuver.TryExecute(maneuverPoint);
            // The y component of the quaternion is zero at the apogee: the label of the half period holds the apogee to
            // the tick, 1e-7 s, over which the direction of the velocity turns by |a| 1e-7 s / |v| (2.72e-11 on main,
            // 3.98e-11 with the label of TT, #501)
            Assert.Equal(-1.0, res.so.Rotation.VectorPart.X);
            Assert.InRange(System.Math.Abs(res.so.Rotation.VectorPart.Y), 0.0, ApogeeTurn(maneuverPoint));
            Assert.Equal((0.0, 0.0), (res.so.Rotation.W, res.so.Rotation.VectorPart.Z));
            LabelAssert.Equal(maneuverPoint.Epoch, res.so.Epoch);
            Assert.Equal(maneuverPoint.Frame, res.so.ReferenceFrame);
            // The x component of ΔV likewise, to |a| 1e-7 s (6.6e-8 m/s on main, 9.7e-8 m/s with the label of TT)
            Assert.InRange(System.Math.Abs(maneuver.DeltaV.X), 0.0,
                ApogeeTurn(maneuverPoint) * maneuverPoint.Velocity.Magnitude());
            Assert.Equal((-2425.783652387101, 0.0), (maneuver.DeltaV.Y, maneuver.DeltaV.Z));
            // The window at the apogee, half a period in TT seconds: 9e-7 s after that at the half period in TDB seconds
            // (12:45:06.0273038), the change of TDB − TT over the 45 min (#501, #508)
            LabelAssert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 12, 45, 06, 27, 304).AddTicks(7), TimeSpan.FromSeconds(16.0691385)), maneuver.ThrustWindow);
            LabelAssert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 12, 45, 06, 27, 304).AddTicks(7), TimeSpan.FromSeconds(16.0691385)), maneuver.ManeuverWindow);
            Assert.Equal(803.45692655552136, maneuver.FuelBurned,3);
        }

        [Fact]
        public void TryExecuteDecreasePerigee()
        {
            var orbitalParams = new StateVector(new Vector3(42164000.0, 0.0, 0.0), new Vector3(0.0, 3075.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 6678000.0, spc.Engines.First());

            // The apogee, half a period after the perigee in TT seconds, the time of a two-body motion about the Earth
            // (#501, #508); Epoch + Period() * 0.5 adds TDB seconds, n·δ off the apogee
            var maneuverPoint = orbitalParams.ToStateVector(
                PropagationTimeArgument.Tt.EpochAt(orbitalParams.Epoch, orbitalParams.Period().TotalSeconds * 0.5));
            var res = maneuver.TryExecute(maneuverPoint);
            // The state after the impulse, the given state unchanged (#588)
            LabelAssert.Equal(new StateVector(maneuverPoint.Position, maneuverPoint.Velocity + maneuver.DeltaV, maneuverPoint.Observer, maneuverPoint.Epoch, maneuverPoint.Frame), res.sv);
            LabelAssert.Equal(new StateOrientation(new Quaternion(1.0, 0.0, 0.0, 0.0), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame), res.so);
            // The x component of ΔV is zero at the apogee, to the radial velocity of a tick, |a| 1e-7 s (1.6e-10 m/s on
            // main, 5.7e-9 m/s with the label of TT, #501)
            Assert.InRange(System.Math.Abs(maneuver.DeltaV.X), 0.0,
                ApogeeTurn(maneuverPoint) * maneuverPoint.Velocity.Magnitude());
            Assert.Equal((1466.487832468652, 0.0), (maneuver.DeltaV.Y, maneuver.DeltaV.Z));
            // The window at the apogee, half a period in TT seconds: 1.44e-5 s after that at the half period in TDB seconds
            // (23:58:08.6636582), the change of TDB − TT over the 12 h (#501, #508); the label of the apogee is the nearest
            // tick of 43095.81915755211 s from J2000 TDB, 0.52 tick past a tick, so a tick later than its truncation (#625)
            LabelAssert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 23, 58, 08, 663, 672).AddTicks(7), TimeSpan.FromSeconds(10.7439713)), maneuver.ThrustWindow);
            LabelAssert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 23, 58, 08, 663, 672).AddTicks(7), TimeSpan.FromSeconds(10.7439713)), maneuver.ManeuverWindow);
            Assert.Equal(537.19856491695327, maneuver.FuelBurned,3);
        }

        // #686, the state of the issue: a circular orbit of 7178 km inclined by 30°, r = (r, 0, 0), v = √(GM / r)(0, cos 30°,
        // sin 30°), GM the Earth's of gm_de440.tpc, whose Keplerian elements have e = 1.1e-16, at the rounding level, where
        // the eccentricity vector cancels to zero: the perigee vector was zero and the apsis velocities infinite
        internal static KeplerianElements Issue686Elements()
        {
            var earth = TestHelpers.EarthAtJ2000;
            double r = 7178e3;
            double v = System.Math.Sqrt(earth.GM / r);
            double i = 30.0 * IO.Astrodynamics.Constants.Deg2Rad;
            return new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, v * System.Math.Cos(i), v * System.Math.Sin(i)), earth, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF).ToKeplerianElements();
        }

        // #686: the worst relative difference of the apsis radii and velocities of the issue's elements from the closed form
        // of the circular orbit, r = 7178 km and v = √(GM / r), MEASURE 2.220e-16, one ulp, on each of Linux x64, Windows x64
        // and macOS arm64 (CI run 43745883709, 2026-11-15); twice the worst, rounded up at the second significant digit
        internal const double Issue686Tolerance = 4.5e-16;

        [Fact]
        public void Create_FromNearCircularElements_TargetsTheirPerigeeRadius()
        {
            var ke = Issue686Elements();
            FuelTank fuelTank = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
            var maneuver = new PerigeeHeightManeuver(TimeSystem.Time.J2000TDB, TimeSpan.Zero, ke, eng);

            double r = 7178e3;
            double v = System.Math.Sqrt(TestHelpers.EarthAtJ2000.GM / r);
            double worst = new[]
            {
                System.Math.Abs(ke.PerigeeVector().Magnitude() / r - 1.0), System.Math.Abs(ke.ApogeeVector().Magnitude() / r - 1.0),
                System.Math.Abs(maneuver.TargetPerigeeHeight / r - 1.0), System.Math.Abs(ke.PerigeeVelocity() / v - 1.0),
                System.Math.Abs(ke.ApogeeVelocity() / v - 1.0)
            }.Max();
            System.Console.WriteLine($"MEASURE #686 issue elements e = {ke.E:E3}: worst apsis radius and velocity against 7178 km and sqrt(GM/r) {worst:E3}");
            Assert.True(worst <= Issue686Tolerance, $"{worst:E3}");
        }
}
}