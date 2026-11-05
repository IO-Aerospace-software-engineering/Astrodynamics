using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Tests.Propagators;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class PhasingManeuverTests
    {
        private const double Deg = Astrodynamics.Constants.Deg2Rad;

        private readonly ITestOutputHelper _output;

        public PhasingManeuverTests(ITestOutputHelper output)
        {
            _output = output;
            SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PhasingManeuver maneuver = new PhasingManeuver(TestHelpers.EarthAtJ2000,new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), TimeSpan.FromDays(1.0), 3.0, 2, eng);

            Assert.NotNull(maneuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), maneuver.ManeuverHoldDuration);
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01),TimeFrame.TDBFrame), maneuver.MinimumEpoch);
            Assert.Equal(3.0, maneuver.TargetTrueLongitude);
            Assert.Equal((uint)2, maneuver.RevolutionNumber);
        }

        [Fact]
        public void Execute()
        {
            var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 345.0 * Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PhasingManeuver maneuver = new PhasingManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targtOrbitalParams, 3, spc.Engines.First());
            maneuver.TryExecute(orbitalParams.ToStateVector());

            Assert.Equal(new Vector3(0.0, 14.039767793719875, 0.0), maneuver.DeltaV,TestHelpers.VectorComparer);
            Assert.Equal(6.0351723087570868, maneuver.FuelBurned,3);
            Assert.Equal(new Window(new TimeSystem.Time("2000-01-01T11:59:59.9196115"), TimeSpan.Parse("2.17:31:12.7762440")), maneuver.ManeuverWindow);
            Assert.Equal(new Window(new TimeSystem.Time("2000-01-01T11:59:59.9196115"), TimeSpan.FromSeconds(0.1207034)), maneuver.ThrustWindow);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The true-longitude constructor (#588): a virtual target on the spacecraft's current orbit, Δθ = λ − λ_sc(burn)
        // ---------------------------------------------------------------------------------------------------------------

        // The orbits of the closed-form tests: the circular orbit of Execute, and an inclined eccentric orbit
        private static KeplerianElements Orbit(string orbit, double meanAnomaly, CelestialItem center, TimeSystem.Time epoch) => orbit switch
        {
            "circular" => new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, meanAnomaly, center, epoch, Frames.Frame.ICRF),
            "eccentric" => new KeplerianElements(10000000.0, 0.2, 0.5, 0.3, 1.1, meanAnomaly, center, epoch, Frames.Frame.ICRF),
            _ => throw new ArgumentOutOfRangeException(nameof(orbit))
        };

        // The longitude of the perigee Ω + ω of the orbits above, the true longitude of the spacecraft at the burn
        private static double PerigeeLongitude(string orbit) => orbit == "circular" ? 0.0 : 0.3 + 1.1;

        private static Spacecraft NewSpacecraft(int naifId, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
        {
            var spc = new Spacecraft(naifId, $"Phasing{-naifId}", 1000.0, 3000.0, new Clock($"Phasing{-naifId}Clk", 65536), orbit);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            return spc;
        }

        /// <summary>
        /// The phasing orbit of the two-body closed form, written here: the target Δθ ahead on the orbit
        /// <paramref name="k"/> (behind beyond π) is reached after N revolutions of the orbit of period
        /// T₂ = T₁ − t(Δθ) / N, t(Δθ) the time from the perigee to Δθ by Kepler's equation in (−T₁/2, T₁/2], of semi-major
        /// axis a₂ = (√μ T₂ / 2π)^(2/3); the burn at the perigee, ΔV = √(μ (2 / r_p − 1 / a₂)) − v_p by vis-viva.
        /// </summary>
        private static (double T2, double A2, double DeltaV) ClosedFormPhasing(KeplerClosedForm k, double deltaTheta, uint revolutions)
        {
            double m = k.MeanOfTrue(KeplerClosedForm.Normalized(deltaTheta));
            if (m > System.Math.PI) m -= 2.0 * System.Math.PI;
            double t2 = k.T - m / k.N / revolutions;
            double a2 = System.Math.Cbrt(k.Mu * System.Math.Pow(t2 / (2.0 * System.Math.PI), 2.0));
            double rp = k.PerigeeRadius;
            return (t2, a2, System.Math.Sqrt(k.Mu * (2.0 / rp - 1.0 / a2)) - k.SpeedAt(rp));
        }

        private static KeplerClosedForm ClosedForm(StateVector sv, double t0)
        {
            var local = sv.RelativeTo(sv.Observer, Aberration.None).ToStateVector();
            return new KeplerClosedForm(sv.Observer.GM, local.Position, local.Velocity, t0);
        }

        /// <summary>
        /// The true-longitude constructor no longer throws at its burn (#588: TargetOrbit was null and Execute dereferenced
        /// it): the circular orbit of <see cref="Execute"/> with a target at 345°, 15° behind, after 3 revolutions, the
        /// delta-V of the closed form, the same as the target-orbit constructor's with the target at 345° on that orbit.
        /// </summary>
        [Fact]
        public void ExecuteFromTargetTrueLongitude()
        {
            var orbitalParams = Orbit("circular", 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB);
            var spc = NewSpacecraft(-666, orbitalParams);
            PhasingManeuver maneuver = new PhasingManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
                345.0 * Deg, 3, spc.Engines.First());
            Assert.Null(maneuver.TargetOrbit);

            // Before the burn: TryExecute changes the velocity of the state it is given, which the elements cache
            var (_, _, dv) = ClosedFormPhasing(ClosedForm(orbitalParams.ToStateVector(), 0.0), 345.0 * Deg, 3);
            maneuver.TryExecute(orbitalParams.ToStateVector());

            double error = System.Math.Abs(maneuver.DeltaV.Y - dv);
            _output.WriteLine($"MEASURE #588 345 deg, 3 rev: dV {maneuver.DeltaV.Y:R} m/s, closed form {dv:R} m/s, |delta| {error:E2} m/s");
            Assert.True(error < DeltaVTolerance, $"{error:E2}");
            Assert.Equal(0.0, maneuver.DeltaV.X);
            Assert.Equal(0.0, maneuver.DeltaV.Z);
        }

        /// <summary>
        /// Through TryExecute at the perigee, the delta-V and the hold of the true-longitude constructor against the
        /// two-body closed form, on a circular and an eccentric inclined orbit, the target ahead (30°) and behind (330°),
        /// after 1 and 3 revolutions; and the target-orbit constructor with the target on the same orbit at that phase
        /// gives the same delta-V, the formula of #588 being the target-orbit formula with the current orbit's angular
        /// momentum.
        /// </summary>
        [Theory]
        [InlineData("circular", 30.0, 1u)]
        [InlineData("circular", 30.0, 3u)]
        [InlineData("circular", 330.0, 1u)]
        [InlineData("circular", 330.0, 3u)]
        [InlineData("eccentric", 30.0, 1u)]
        [InlineData("eccentric", 30.0, 3u)]
        [InlineData("eccentric", 330.0, 1u)]
        [InlineData("eccentric", 330.0, 3u)]
        public void TryExecute_TheTwoBodyClosedForm(string orbit, double phaseDeg, uint revolutions)
        {
            var center = TestHelpers.EarthAtJ2000;
            var epoch = TimeSystem.Time.J2000TDB;
            var initial = Orbit(orbit, 0.0, center, epoch);
            double deltaTheta = phaseDeg * Deg;
            var k = ClosedForm(initial.ToStateVector(), 0.0);
            var (t2, _, dv) = ClosedFormPhasing(k, deltaTheta, revolutions);

            var spc = NewSpacecraft(-5881, initial);
            var maneuver = new PhasingManeuver(center, TimeSystem.Time.J2000TDB, TimeSpan.Zero, PerigeeLongitude(orbit) + deltaTheta, revolutions, spc.Engines.First());
            var sv = initial.ToStateVector();
            var velocity = sv.Velocity;
            maneuver.TryExecute(sv);

            double dvError = System.Math.Abs(maneuver.DeltaV * velocity.Normalize() - dv);
            double alongError = (maneuver.DeltaV - velocity.Normalize() * (maneuver.DeltaV * velocity.Normalize())).Magnitude();
            double holdError = System.Math.Abs(maneuver.ManeuverHoldDuration.TotalSeconds - 0.9 * revolutions * t2);

            // The target-orbit constructor with the target on the same orbit at Δθ: its mean anomaly from the closed form
            var target = Orbit(orbit, k.MeanOfTrue(deltaTheta), center, epoch);
            var unburnt = Orbit(orbit, 0.0, center, epoch);
            var reference = new PhasingManeuver(epoch, TimeSpan.Zero, target, revolutions, NewSpacecraft(-5882, unburnt).Engines.First());
            reference.TryExecute(unburnt.ToStateVector());
            double pathsError = (maneuver.DeltaV - reference.DeltaV).Magnitude();

            _output.WriteLine($"MEASURE #588 TryExecute {orbit} {phaseDeg} deg {revolutions} rev: dV {dv:F6} m/s, |dV - closed form| {dvError:E2} m/s, " +
                              $"off the velocity {alongError:E2} m/s, |hold - 0.9 N T2| {holdError:E2} s, |dV - target-orbit path| {pathsError:E2} m/s");
            Assert.True(dvError < DeltaVTolerance, $"{dvError:E2}");
            Assert.True(alongError < RoundingTolerance, $"{alongError:E2}");
            Assert.True(holdError < HoldTolerance, $"{holdError:E2}");
            Assert.True(pathsError < RoundingTolerance, $"{pathsError:E2}");
        }

        /// <summary>
        /// In a propagation about a point-mass Earth, on both integrators: the spacecraft of the eccentric orbit, from
        /// 949.999 s before its perigee, burns at the perigee of the closed form with the closed-form delta-V,
        /// and after N revolutions of the phasing orbit is where the virtual target is, the target on the initial orbit
        /// Δθ ahead at the burn carried by Kepler's equation, back at the perigee.
        /// </summary>
        [Theory]
        [InlineData("RK", 30.0, 1u)]
        [InlineData("RK", 30.0, 3u)]
        [InlineData("RK", 330.0, 1u)]
        [InlineData("RK", 330.0, 3u)]
        [InlineData("VV", 30.0, 1u)]
        [InlineData("VV", 30.0, 3u)]
        [InlineData("VV", 330.0, 1u)]
        [InlineData("VV", 330.0, 3u)]
        public void Propagation_MeetsTheVirtualTarget(string integrator, double phaseDeg, uint revolutions)
        {
            var center = TestHelpers.EarthAtJ2000;
            var start = TimeSystem.Time.J2000TDB;
            double deltaTheta = phaseDeg * Deg;
            // The perigee 1 ms before the end of the Velocity-Verlet step of 950 s, which fires at the end of the step that
            // holds the crossing
            double n = System.Math.Sqrt(center.GM / System.Math.Pow(10000000.0, 3.0));
            var initial = Orbit("eccentric", 2.0 * System.Math.PI - n * (950.0 - 1.0e-3), center, start);
            var k = ClosedForm(initial.ToStateVector(), 0.0);
            double tBurn = k.NextApsis(0.0, false);
            var (t2, _, dv) = ClosedFormPhasing(k, deltaTheta, revolutions);
            double tArrival = tBurn + revolutions * t2;

            // The virtual target: Δθ ahead on the initial orbit at the burn, carried by Kepler's equation
            var (rt, vt) = k.StateAtTrue(deltaTheta);
            var (targetAtArrival, _) = new KeplerClosedForm(k.Mu, rt, vt, tBurn).StateAt(tArrival);

            var spc = NewSpacecraft(integrator == "RK" ? -5883 : -5884, initial);
            var maneuver = new PhasingManeuver(center, start, TimeSpan.Zero, PerigeeLongitude("eccentric") + deltaTheta, revolutions, spc.Engines.First());
            spc.SetStandbyManeuver(maneuver);
            var window = new Window(start, PropagationTimeArgument.Tt.EpochAt(start, tArrival + 600.0));
            Integrator NewIntegrator() => integrator == "RK" ? new RK78Integrator(1e-12, 1e-12) : new VVIntegrator(TimeSpan.FromSeconds(1.0));
            var solution = spc.Propagate(window, [center], NewIntegrator(), false, false, TimeSpan.FromSeconds(60.0));

            Assert.Equal(2, solution.Segments.Count);
            double burnError = PropagationTimeArgument.Tt.SecondsBetween(start, solution.Segments[1].BaseEpoch) - tBurn;
            double dvError = System.Math.Abs(maneuver.DeltaV.Magnitude() - System.Math.Abs(dv));
            var (position, _) = solution.InterpolateAtElapsed(tArrival);
            double separation = (position - targetAtArrival).Magnitude();
            double angle = position.Angle(targetAtArrival);

            _output.WriteLine($"MEASURE #588 propagation {integrator} {phaseDeg} deg {revolutions} rev: burn - closed form {burnError:E2} s, " +
                              $"|dV - closed form| {dvError:E2} m/s (dV {dv:F6} m/s), at N T2 {separation:E2} m and {angle:E2} rad from the virtual target");
            if (integrator == "RK")
            {
                // RK7(8) fires within 1.5e-7 s of the Keplerian epoch, the 100 ns tick of the label included (#569)
                Assert.True(System.Math.Abs(burnError) < 1.5e-7, $"{burnError:E2}");
                Assert.True(dvError < RkDeltaVTolerance, $"{dvError:E2}");
                Assert.True(separation < RkSeparationTolerance, $"{separation:E2}");
            }
            else
            {
                // Velocity-Verlet fires at the end of the step that holds the perigee, 950 s, to the tick of the label
                Assert.True(System.Math.Abs(burnError - 1.0e-3) < 1.0e-7, $"{burnError:E2}");
                Assert.True(dvError < VvDeltaVTolerance, $"{dvError:E2}");
                Assert.True(separation < VvSeparationTolerance, $"{separation:E2}");
            }
        }

        // Twice the worst value, Linux x64, .NET 10, Release, 2026-11-05: the delta-V of TryExecute against the closed
        // form 9.23e-9 m/s (eccentric orbit; 5.19e-10 m/s circular), against the target-orbit path 4.55e-13 m/s, off the
        // velocity 1.48e-14 m/s, the hold 2.22e-7 s
        private const double DeltaVTolerance = 2.0e-8;
        private const double HoldTolerance = 5.0e-7;
        private const double RoundingTolerance = 1.0e-12;

        // The propagations, Linux x64, .NET 10, Release, 2026-11-05: RK7(8) at 1e-12, the delta-V 1.29e-8 m/s and the
        // separation from the virtual target 1.36e-3 m; Velocity-Verlet at 1 s, 2.01e-4 m/s and 49.1 m (the burn 1 ms after
        // the perigee, 7.7 m along the orbit). Twice the worst value, rounded up
        private const double RkDeltaVTolerance = 3.0e-8;
        private const double RkSeparationTolerance = 3.0e-3;
        private const double VvDeltaVTolerance = 5.0e-4;
        private const double VvSeparationTolerance = 100.0;
    }
}
