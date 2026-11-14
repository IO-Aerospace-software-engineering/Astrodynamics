using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class CombinedManeuverTest
    {
        private readonly ITestOutputHelper _output;

        public CombinedManeuverTest(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        // The relative agreement of the target conic's perigee with min(target, apogee): 1.8e-16 at worst, measured on Linux
        // x64 (2026-11-14), twice that until the three CI platforms have printed their MEASURE lines (provisional)
        private const double TargetPerigeeRelative = 3.6e-16;

        // The relative agreement of |ΔV| with the closed form of a combined burn at an apsis on the line of nodes: 5.0e-16 at
        // worst, measured on Linux x64 (2026-11-14), twice that until the three CI platforms have printed their MEASURE lines
        // (provisional)
        private const double DeltaVClosedFormRelative = 1.0e-15;

        // The orbit of the tests above at its apogee, which lies on the line of nodes (Ω = ω = 0), half a period after the
        // perigee in TT seconds (#501, #508), and the maneuver of the given target radius on a spacecraft that carries it
        private static (StateVector state, CombinedManeuver maneuver) AtApogee(double targetRadius, bool equalToApogee = false)
        {
            var orbitalParams = new KeplerianElements(24420999.959422689, 0.726546824, 28.5 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 10000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 9000.0, 9000.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            var state = orbitalParams.ToStateVector(PropagationTimeArgument.Tt.EpochAt(orbitalParams.Epoch, orbitalParams.Period().TotalSeconds * 0.5));
            // A target equal to the apogee: the apogee radius the maneuver reads from the state
            double target = equalToApogee ? state.ApogeeVector().Magnitude() : targetRadius;
            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, target, 0.0,
                spc.Engines.First());
            return (state, maneuver);
        }

        /// <summary>
        /// The target orbit carries its own perigee: q = a(1 − e) exactly, which is min(target, apogee), the conic whose apsides
        /// are the current apogee and the target radius (#677). It carried the current orbit's perigee (6 678 000 m) as a
        /// cached radius.
        /// </summary>
        [Theory]
        [InlineData(42164000.0, false)] // above the apogee
        [InlineData(6600000.0, false)] // below it
        [InlineData(0.0, true)] // equal to it
        public void ComputeTargetOrbit_PerigeeIsTheTargetConics(double targetRadius, bool equalToApogee)
        {
            var (state, maneuver) = AtApogee(targetRadius, equalToApogee);
            var target = maneuver.ComputeTargetOrbit(state);
            double apogee = state.ApogeeVector().Magnitude();
            double expected = System.Math.Min(maneuver.TargetPerigeeHeight, apogee);

            Assert.Equal(target.SemiMajorAxis() * (1.0 - target.Eccentricity()), target.PerigeeRadius());
            double relative = System.Math.Abs(target.PerigeeRadius() - expected) / expected;
            _output.WriteLine($"MEASURE #677 combined target perigee {maneuver.TargetPerigeeHeight}: {target.PerigeeRadius():R} m against {expected:R} m, relative {relative:E2}");
            Assert.True(relative <= TargetPerigeeRelative, $"target perigee {target.PerigeeRadius():R} m, min(target, apogee) {expected:R} m, relative {relative:E3}");
        }

        /// <summary>
        /// At the apogee on the line of nodes both velocities are horizontal and perpendicular to the line of nodes, so the
        /// burn is the third side of their triangle: |ΔV|² = v1² + v2² − 2 v1 v2 cos Δi, v1 the current speed, v2 that of the
        /// target conic at the same radius by vis-viva (#677). The ΔV is the target orbit's velocity less the state's.
        /// </summary>
        [Theory]
        [InlineData(42164000.0)]
        [InlineData(6600000.0)]
        public void DeltaV_MatchesTheClosedFormAtApogee(double targetRadius)
        {
            var (state, maneuver) = AtApogee(targetRadius);
            maneuver.TryExecute(state);

            double mu = state.Observer.GM;
            double r = state.Position.Magnitude();
            double v1 = state.Velocity.Magnitude();
            double aTarget = (targetRadius + r) / 2.0;
            double v2 = System.Math.Sqrt(mu * (2.0 / r - 1.0 / aTarget));
            double deltaI = 28.5 * Astrodynamics.Constants.Deg2Rad;
            double expected = System.Math.Sqrt(v1 * v1 + v2 * v2 - 2.0 * v1 * v2 * System.Math.Cos(deltaI));
            double relative = System.Math.Abs(maneuver.DeltaV.Magnitude() - expected) / expected;
            _output.WriteLine($"MEASURE #677 combined ΔV {targetRadius}: {maneuver.DeltaV.Magnitude():R} m/s against {expected:R} m/s, relative {relative:E2}");
            Assert.True(relative <= DeltaVClosedFormRelative, $"|ΔV| {maneuver.DeltaV.Magnitude():R} m/s, closed form {expected:R} m/s, relative {relative:E3}");
            Assert.Equal(maneuver.ComputeTargetOrbit(state).ToStateVector().Velocity - state.Velocity, maneuver.DeltaV);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            CombinedManeuver maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0), 151000000.0, 1.0, eng);

            Assert.NotNull(maneuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), maneuver.ManeuverHoldDuration);
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), maneuver.MinimumEpoch);
            Assert.Equal(151000000.0, maneuver.TargetPerigeeHeight);
            Assert.Equal(1.0, maneuver.TargetInclination);
        }

        [Fact]
        public void CheckPreconditions()
        {
            // CombinedManeuver.CheckPreconditions verifies that the apogee vector is aligned with the
            // ascending node vector (|dot| > 0.9). This ensures the maneuver fires at the correct
            // orbital geometry: apogee must be near the ascending node.

            // AOP = 0: apogee is at true anomaly 180°, ascending node at true anomaly 0° → they are
            // anti-parallel, so |dot| of normalized vectors ≈ 1.0 → passes
            var alignedOrbit = new KeplerianElements(11480000.0, 0.5, 28.5 * Astrodynamics.Constants.Deg2Rad,
                0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
                alignedOrbit.ApogeeVector().Magnitude() + 100000.0, 10.0,
                new Engine("eng", "engmk1", "12345", 450, 50,
                    new FuelTank("ft", "ftA", "123456", 1000, 1000)));

            Assert.True(maneuver.CheckPreconditions(alignedOrbit.ToStateVector()));

            // AOP = 90°: apogee is 90° away from ascending node → dot product ≈ 0 → fails
            var perpendicularOrbit = new KeplerianElements(11480000.0, 0.5, 28.5 * Astrodynamics.Constants.Deg2Rad,
                0.0, Astrodynamics.Constants.PI * 0.5, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);

            Assert.False(maneuver.CheckPreconditions(perpendicularOrbit.ToStateVector()));

            // AOP = 45°: apogee is 45° from ascending node → |dot| ≈ cos(45°) ≈ 0.707 → fails (< 0.9)
            var intermediateOrbit = new KeplerianElements(11480000.0, 0.5, 28.5 * Astrodynamics.Constants.Deg2Rad,
                0.0, Astrodynamics.Constants.PI * 0.25, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);

            Assert.False(maneuver.CheckPreconditions(intermediateOrbit.ToStateVector()));
        }

        [Fact]
        public void TryExecuteIncreasePerigee()
        {
            var orbitalParams = new KeplerianElements(24420999.959422689, 0.726546824, 28.5 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 10000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 9000.0, 9000.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 42164000.0, 0.0, spc.Engines.First());

            // The apogee, half a period after the perigee in TT seconds, the time of a two-body motion about the Earth
            // (#501, #508); Epoch + Period() * 0.5 adds TDB seconds, n·δ off the apogee
            var maneuverPoint = orbitalParams.ToStateVector(
                PropagationTimeArgument.Tt.EpochAt(orbitalParams.Epoch, orbitalParams.Period().TotalSeconds * 0.5));
            var res = maneuver.TryExecute(maneuverPoint);
            // The y and z parts of the quaternion are zero to rounding (1e-11): compared, not matched bit for bit
            Assert.Equal(
                new StateOrientation(new Quaternion(0.21458639046372344, 0.9767050122876152, 0.0, 0.0), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame),
                res.so, TestHelpers.StateOrientationComparer);
            // ΔV has no x component, to rounding (1e-8 m/s)
            Assert.True(System.Math.Abs(maneuver.DeltaV.X) < 1e-7, $"ΔV x: {maneuver.DeltaV.X:E2}");
            Assert.Equal(-1661.6798912062677, maneuver.DeltaV.Y);
            Assert.Equal(767.1890047800779, maneuver.DeltaV.Z);
            // The window at the apogee, half a period in TT seconds: 6.4e-6 s after that at the half period in TDB
            // seconds (17:15:44.8323340), the change of TDB − TT over the 5 h 16 min (#501, #508)
            Assert.Equal(new Window(new TimeSystem.Time("2000-01-01T17:15:44.8323404").ToTDB(), new TimeSystem.Time("2000-01-01T17:16:52.7296482").ToTDB()), maneuver.ManeuverWindow);
            Assert.Equal(new Window(new TimeSystem.Time("2000-01-01T17:15:44.8323404").ToTDB(), new TimeSystem.Time("2000-01-01T17:16:52.7296482").ToTDB()), maneuver.ThrustWindow);
            Assert.Equal(3394.8653913932048, maneuver.FuelBurned,3);
        }

        [Fact]
        public void TryExecuteDecreasePerigee()
        {
            var orbitalParams = new KeplerianElements(24420999.959422689, 0.726546824, 28.5 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 10000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 9000.0, 9000.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 6600000.0, 0.0, spc.Engines.First());

            // The apogee, half a period after the perigee in TT seconds, the time of a two-body motion about the Earth
            // (#501, #508); Epoch + Period() * 0.5 adds TDB seconds, n·δ off the apogee
            var maneuverPoint = orbitalParams.ToStateVector(
                PropagationTimeArgument.Tt.EpochAt(orbitalParams.Epoch, orbitalParams.Period().TotalSeconds * 0.5));
            var res = maneuver.TryExecute(maneuverPoint);
            // Equatorial after the maneuver: i, Ω and ω are zero (modulo 2π) to rounding, as SPICE OSCELT gives them
            Assert.Equal(
                new KeplerianElements(24381999.95942269, 0.7293085058246275, 0, 0, 0, 3.141592653589793, maneuverPoint.Observer, maneuverPoint.Epoch,
                    maneuverPoint.Frame), res.sv.ToKeplerianElements(), TestHelpers.KeplerComparer);
            Assert.Equal(
                new StateOrientation(new Quaternion(0.617876643698767, 0.7862750493126097, 0.0, 0.0), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame),
                res.so, TestHelpers.StateOrientationComparer);
            // ΔV has no x component, to rounding (1e-8 m/s)
            Assert.True(System.Math.Abs(maneuver.DeltaV.X) < 1e-7, $"ΔV x: {maneuver.DeltaV.X:E2}");
            Assert.Equal(-186.70162770583374, maneuver.DeltaV.Y);
            Assert.Equal(767.1890047800779, maneuver.DeltaV.Z);
            // 6.4e-6 s after the window at the half period in TDB seconds (17:16:08.2299721) (#501, #508)
            Assert.Equal(new Window(new TimeSystem.Time("2000-01-01T17:16:08.2299785").ToTDB(), new TimeSystem.Time("2000-01-01T17:16:40.9956975").ToTDB()), maneuver.ThrustWindow);
            Assert.Equal(new Window(new TimeSystem.Time("2000-01-01T17:16:08.2299785").ToTDB(), new TimeSystem.Time("2000-01-01T17:16:40.9956975").ToTDB()), maneuver.ManeuverWindow);
            Assert.Equal(1638.2859502950841, maneuver.FuelBurned);
        }
    }
}