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
            SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PerigeeHeightManeuver perigeeHeightManeuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0), 151000000.0, eng);

            Assert.NotNull(perigeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), perigeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), perigeeHeightManeuver.MinimumEpoch);
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
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), perigeeHeightManeuver.MinimumEpoch);
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
            Assert.Equal((maneuverPoint.Epoch, maneuverPoint.Frame), (res.so.Epoch, res.so.ReferenceFrame));
            // The x component of ΔV likewise, to |a| 1e-7 s (6.6e-8 m/s on main, 9.7e-8 m/s with the label of TT)
            Assert.InRange(System.Math.Abs(maneuver.DeltaV.X), 0.0,
                ApogeeTurn(maneuverPoint) * maneuverPoint.Velocity.Magnitude());
            Assert.Equal((-2425.783652387101, 0.0), (maneuver.DeltaV.Y, maneuver.DeltaV.Z));
            // The window at the apogee, half a period in TT seconds: 9e-7 s after that at the half period in TDB seconds
            // (12:45:06.0273038), the change of TDB − TT over the 45 min (#501, #508)
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 12, 45, 06, 27, 304).AddTicks(7), TimeSpan.FromSeconds(16.0691385)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 12, 45, 06, 27, 304).AddTicks(7), TimeSpan.FromSeconds(16.0691385)), maneuver.ManeuverWindow);
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
            Assert.Equal(new StateVector(maneuverPoint.Position, maneuverPoint.Velocity + maneuver.DeltaV, maneuverPoint.Observer, maneuverPoint.Epoch, maneuverPoint.Frame), res.sv);
            Assert.Equal(new StateOrientation(new Quaternion(1.0, 0.0, 0.0, 0.0), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame), res.so);
            // The x component of ΔV is zero at the apogee, to the radial velocity of a tick, |a| 1e-7 s (1.6e-10 m/s on
            // main, 5.7e-9 m/s with the label of TT, #501)
            Assert.InRange(System.Math.Abs(maneuver.DeltaV.X), 0.0,
                ApogeeTurn(maneuverPoint) * maneuverPoint.Velocity.Magnitude());
            Assert.Equal((1466.487832468652, 0.0), (maneuver.DeltaV.Y, maneuver.DeltaV.Z));
            // The window at the apogee, half a period in TT seconds: 1.44e-5 s after that at the half period in TDB seconds
            // (23:58:08.6636582), the change of TDB − TT over the 12 h (#501, #508)
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 23, 58, 08, 663, 672).AddTicks(6), TimeSpan.FromSeconds(10.7439713)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new TimeSystem.Time(2000, 01, 01, 23, 58, 08, 663, 672).AddTicks(6), TimeSpan.FromSeconds(10.7439713)), maneuver.ManeuverWindow);
            Assert.Equal(537.19856491695327, maneuver.FuelBurned,3);
        }
    }
}