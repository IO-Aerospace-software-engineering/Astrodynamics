// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Integrators;

public class RK78IntegratorTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    private readonly ITestOutputHelper _output;

    public RK78IntegratorTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    #region Butcher Tableau Verification

    [Fact]
    public void ButcherTableauRowSumConsistency()
    {
        // For each stage s, sum of A[s][j] for j=0..s-1 should equal C[s]
        for (int s = 1; s < RK78ButcherTableau.Stages; s++)
        {
            double rowSum = 0.0;
            for (int j = 0; j < s; j++)
                rowSum += RK78ButcherTableau.A[s][j];

            Assert.True(System.Math.Abs(rowSum - RK78ButcherTableau.C[s]) < 1e-14,
                $"Row sum for stage {s}: expected {RK78ButcherTableau.C[s]:E16}, got {rowSum:E16}, " +
                $"diff = {System.Math.Abs(rowSum - RK78ButcherTableau.C[s]):E3}");
        }
    }

    [Fact]
    public void ButcherTableau8thOrderWeightsSum()
    {
        double sum = RK78ButcherTableau.B.Sum();
        Assert.True(System.Math.Abs(sum - 1.0) < 1e-14,
            $"8th-order weights sum: expected 1.0, got {sum:E16}");
    }

    [Fact]
    public void ButcherTableau7thOrderWeightsSum()
    {
        double sum = RK78ButcherTableau.BHat.Sum();
        Assert.True(System.Math.Abs(sum - 1.0) < 1e-14,
            $"7th-order weights sum: expected 1.0, got {sum:E16}");
    }

    [Fact]
    public void ButcherTableauErrorCoefficients()
    {
        for (int i = 0; i < RK78ButcherTableau.Stages; i++)
        {
            double expected = RK78ButcherTableau.B[i] - RK78ButcherTableau.BHat[i];
            Assert.True(System.Math.Abs(RK78ButcherTableau.E[i] - expected) < 1e-16,
                $"Error coefficient E[{i}]: expected {expected:E16}, got {RK78ButcherTableau.E[i]:E16}");
        }
    }

    [Fact]
    public void ButcherTableauStageCount()
    {
        Assert.Equal(13, RK78ButcherTableau.Stages);
        Assert.Equal(13, RK78ButcherTableau.C.Length);
        Assert.Equal(13, RK78ButcherTableau.A.Length);
        Assert.Equal(13, RK78ButcherTableau.B.Length);
        Assert.Equal(13, RK78ButcherTableau.BHat.Length);
        Assert.Equal(13, RK78ButcherTableau.E.Length);
    }

    #endregion

    #region Two-Body Convergence Order

    [Fact]
    public void FixedStepConvergenceOrder8()
    {
        // Two-body circular orbit: known analytic solution (cos/sin).
        // Use large step sizes so the 8th-order truncation error dominates
        // the ~0.2 mm floating-point error floor.
        // Duration is chosen to be exactly divisible by all three step sizes
        // to avoid partial-step artifacts.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double r0 = 7000000.0; // 7000 km
        double v0 = System.Math.Sqrt(mu / r0); // circular velocity
        double n = System.Math.Sqrt(mu / (r0 * r0 * r0)); // mean motion (rad/s)

        // 1600s is exactly divisible by 800, 400, 200
        double totalTime = 1600.0;

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);

        // Analytic solution at t=1600s for a circular orbit
        double theta = n * totalTime;
        var expectedPos = new Vector3(r0 * System.Math.Cos(theta), r0 * System.Math.Sin(theta), 0);

        // Step sizes large enough that truncation error >> round-off floor.
        // n*h for largest step: 1.078e-3 * 800 = 0.86 rad (well within RK78 stability).
        double[] stepSizes = { 800.0, 400.0, 200.0 };
        double[] errors = new double[3];

        for (int k = 0; k < 3; k++)
        {
            var integrator = new RK78Integrator(fixedStepSize: stepSizes[k]);
            integrator.AddForce(new GravitationalAcceleration(earth));
            integrator.Initialize(initialState);

            var result = integrator.IntegrateSegment(
                initialState.Position, initialState.Velocity, epoch, totalTime);

            var (finalPos, _) = result.Segment.InterpolateAt(totalTime);
            errors[k] = (finalPos - expectedPos).Magnitude();
        }

        // Check convergence: error ratio h -> h/2 should approach 2^8 = 256
        double ratio1 = errors[0] / errors[1]; // h=800 vs h=400
        double ratio2 = errors[1] / errors[2]; // h=400 vs h=200

        // For 8th-order, expect ratio near 256. Allow generous tolerance
        // since finite-step and higher-order terms reduce the ideal ratio.
        Assert.True(ratio1 > 50.0,
            $"Convergence ratio (h=800/h=400): {ratio1:F1}, expected > 50 for 8th-order. " +
            $"Errors: {errors[0]:E3}, {errors[1]:E3}, {errors[2]:E3}");
        Assert.True(ratio2 > 50.0,
            $"Convergence ratio (h=400/h=200): {ratio2:F1}, expected > 50 for 8th-order. " +
            $"Errors: {errors[0]:E3}, {errors[1]:E3}, {errors[2]:E3}");
    }

    #endregion

    #region Two-Body Energy Conservation

    [Fact]
    public void AdaptiveTwoBodyCircularOrbitConservesEnergy()
    {
        // Circular orbit: propagate 10 orbits, check energy drift
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double r0 = 7000000.0;
        double v0 = System.Math.Sqrt(mu / r0);
        double period = 2.0 * System.Math.PI * System.Math.Sqrt(r0 * r0 * r0 / mu);
        double totalTime = 10.0 * period;
        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-12, relativeTolerance: 1e-12,
            initialStepSize: 30.0);
        integrator.AddForce(new GravitationalAcceleration(earth));
        integrator.Initialize(initialState);

        var result = integrator.IntegrateSegment(
            initialState.Position, initialState.Velocity, epoch, totalTime);

        // Compute specific orbital energy at actual step endpoints (not interpolated points)
        // to verify the integrator itself conserves energy.
        double initialEnergy = v0 * v0 / 2.0 - mu / r0;
        double maxEnergyDrift = 0.0;

        foreach (var step in result.Segment.Steps)
        {
            double r = step.EndPosition.Magnitude();
            double v = step.EndVelocity.Magnitude();
            double energy = v * v / 2.0 - mu / r;
            double drift = System.Math.Abs(energy - initialEnergy);
            if (drift > maxEnergyDrift)
                maxEnergyDrift = drift;
        }

        // RK78 with 1e-12 tolerance should preserve energy extremely well
        double relativeEnergyDrift = maxEnergyDrift / System.Math.Abs(initialEnergy);
        Assert.True(relativeEnergyDrift < 1e-10,
            $"Relative energy drift after 10 orbits: {relativeEnergyDrift:E3}, expected < 1e-10");
    }

    #endregion

    #region Elliptical Orbit Accuracy

    [Fact]
    public void AdaptiveEllipticalOrbitReturnsToStartAfterOnePeriod()
    {
        // Elliptical orbit (e=0.5): propagate 1 full period, final state should match initial
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double a = 10000000.0; // 10,000 km semi-major axis
        double e = 0.5;
        double rPeri = a * (1.0 - e); // 5,000 km periapsis
        double vPeri = System.Math.Sqrt(mu * (2.0 / rPeri - 1.0 / a));
        double period = 2.0 * System.Math.PI * System.Math.Sqrt(a * a * a / mu);

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(rPeri, 0, 0), new Vector3(0, vPeri, 0),
            earth, epoch, Frames.Frame.ICRF);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-12, relativeTolerance: 1e-12,
            initialStepSize: 30.0);
        integrator.AddForce(new GravitationalAcceleration(earth));
        integrator.Initialize(initialState);

        var result = integrator.IntegrateSegment(
            initialState.Position, initialState.Velocity, epoch, period);

        // After one full period, state should return close to initial
        var (finalPos, finalVel) = result.Segment.InterpolateAt(period);
        var posError = (finalPos - initialState.Position).Magnitude();
        var velError = (finalVel - initialState.Velocity).Magnitude();

        Assert.True(posError < 6e-04,
            $"Position error after 1 period (e=0.5): {posError:F6} m, expected < 1.0 m");
        Assert.True(velError < 9e-7,
            $"Velocity error after 1 period (e=0.5): {velError:E3} m/s, expected < 1e-3 m/s");
    }

    #endregion


    #region Adaptive vs Fixed Mode

    [Fact]
    public void FixedModeProducesReproducibleResults()
    {
        // Run the same problem twice in fixed mode, verify identical results
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double r0 = 7000000.0;
        double v0 = System.Math.Sqrt(mu / r0);
        double totalTime = 1000.0; // 100 steps * 10s

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);

        IntegrationResult RunOnce()
        {
            var integrator = new RK78Integrator(fixedStepSize: 10.0);
            integrator.AddForce(new GravitationalAcceleration(earth));
            integrator.Initialize(initialState);

            return integrator.IntegrateSegment(
                initialState.Position, initialState.Velocity, epoch, totalTime);
        }

        var run1 = RunOnce();
        var run2 = RunOnce();

        // Compare at each 10s output point
        for (int i = 0; i <= 100; i++)
        {
            double t = i * 10.0;
            var (pos1, vel1) = run1.Segment.InterpolateAt(t);
            var (pos2, vel2) = run2.Segment.InterpolateAt(t);

            Assert.Equal(pos1.X, pos2.X);
            Assert.Equal(pos1.Y, pos2.Y);
            Assert.Equal(pos1.Z, pos2.Z);
            Assert.Equal(vel1.X, vel2.X);
            Assert.Equal(vel1.Y, vel2.Y);
            Assert.Equal(vel1.Z, vel2.Z);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void HandlesSmallOutputStep()
    {
        // Output step smaller than default adaptive step: integrator should handle gracefully
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double r0 = 7000000.0;
        double v0 = System.Math.Sqrt(mu / r0);
        double totalTime = 1.0; // 10 * 0.1s

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10, relativeTolerance: 1e-10,
            initialStepSize: 100.0); // initial step much larger than output
        integrator.AddForce(new GravitationalAcceleration(earth));
        integrator.Initialize(initialState);

        var result = integrator.IntegrateSegment(
            initialState.Position, initialState.Velocity, epoch, totalTime);

        // Just verify it completes without error and position is reasonable
        var (pos, _) = result.Segment.InterpolateAt(totalTime);
        double r = pos.Magnitude();
        Assert.True(System.Math.Abs(r - r0) < 1.0,
            $"Radius after 1s: {r:F1} m, expected near {r0:F1} m");
    }

    [Fact]
    public void HandlesLargeOutputStep()
    {
        // Output step much larger than adaptive step: many sub-steps
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double r0 = 7000000.0;
        double v0 = System.Math.Sqrt(mu / r0);
        double totalTime = 3600.0; // 1 hour

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10, relativeTolerance: 1e-10,
            initialStepSize: 10.0);
        integrator.AddForce(new GravitationalAcceleration(earth));
        integrator.Initialize(initialState);

        var result = integrator.IntegrateSegment(
            initialState.Position, initialState.Velocity, epoch, totalTime);

        // Verify orbit radius is still in the right ballpark
        var (pos, _) = result.Segment.InterpolateAt(totalTime);
        double r = pos.Magnitude();
        Assert.True(r is > 6999999.9 and < 7000000.1,
            $"Radius after 1 hour: {r:F1} m, expected between 6999999.9 and 7000000.1 m");
    }

    [Fact]
    public void HighEccentricityOrbitHandlesStepRejection()
    {
        // Highly eccentric orbit where step rejection is likely near periapsis
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double a = 30000000.0; // 30,000 km semi-major axis
        double e = 0.9; // very eccentric
        double rPeri = a * (1.0 - e); // 3,000 km periapsis (below surface, but gravity works)
        double vPeri = System.Math.Sqrt(mu * (2.0 / rPeri - 1.0 / a));
        double period = 2.0 * System.Math.PI * System.Math.Sqrt(a * a * a / mu);

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(rPeri, 0, 0), new Vector3(0, vPeri, 0),
            earth, epoch, Frames.Frame.ICRF);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10, relativeTolerance: 1e-10,
            initialStepSize: 30.0, minStepSize: 1e-3);
        integrator.AddForce(new GravitationalAcceleration(earth));
        integrator.Initialize(initialState);

        // Propagate for 1/2 period
        double halfPeriod = period / 2.0;

        // Should complete without throwing (step rejection + recovery near periapsis)
        var result = integrator.IntegrateSegment(
            initialState.Position, initialState.Velocity, epoch, halfPeriod);

        // At half period, should be near apoapsis
        double rApo = a * (1.0 + e);
        var (finalPos, _) = result.Segment.InterpolateAt(halfPeriod);
        double rFinal = finalPos.Magnitude();
        Assert.True(System.Math.Abs(rFinal - rApo) / rApo < 1e-7,
            $"Radius at half period: {rFinal:F1} m, expected near apoapsis {rApo:F1} m (relative error: {System.Math.Abs(rFinal - rApo) / rApo:E3})");
    }

    #endregion

    #region Single Step Accuracy

    [Fact]
    public void SingleStepMatchesForceModel()
    {
        // Verify that a single RK78 step produces physically reasonable results
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double mu = earth.GM;
        double r0 = 6800000.0;
        double v0 = System.Math.Sqrt(mu / r0);

        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);

        var integrator = new RK78Integrator(fixedStepSize: 1.0);
        integrator.AddForce(new GravitationalAcceleration(earth));
        integrator.Initialize(initialState);

        var result = integrator.IntegrateSegment(
            initialState.Position, initialState.Velocity, epoch, 1.0);

        // After 1 second on a circular orbit: position should shift slightly,
        // velocity should rotate, magnitude should be preserved
        var (pos, vel) = result.Segment.InterpolateAt(1.0);
        double r1 = pos.Magnitude();
        double v1 = vel.Magnitude();

        Assert.True(System.Math.Abs(r1 - r0) < 0.01, // radius preserved to cm level
            $"Radius after 1s: {r1:F6}, expected {r0:F6}");
        Assert.True(System.Math.Abs(v1 - v0) < 1e-6, // speed preserved to um/s level
            $"Speed after 1s: {v1:F9}, expected {v0:F9}");
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void AdaptiveConstructorRejectsInvalidTolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RK78Integrator(absoluteTolerance: -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RK78Integrator(relativeTolerance: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RK78Integrator(initialStepSize: -10.0));
    }

    [Fact]
    public void FixedConstructorRejectsInvalidStepSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RK78Integrator(fixedStepSize: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RK78Integrator(fixedStepSize: -5.0));
    }

    #endregion

    #region Propagator Integration (CentralBody mode, Star/Barycenter observer)

    [Fact]
    public void IntegratesWithCentralBodyPropagatorStarObserver()
    {
        // Verify RK78 works correctly when injected into CentralBodyPropagator
        // with Star/Barycenter observer (replaces former SsbPropagator).
        // 2h circular orbit sanity check — radius should stay near 6800 km.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);

        Clock clk = new Clock("My clock", 256);
        var orbit = new StateVector(
            new Vector3(6800000.0, 0, 0), new Vector3(0, 7656.2204182967143, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "TestSat", 100.0, 10000.0, clk, orbit);

        var propWindow = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2));

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10, relativeTolerance: 1e-10,
            initialStepSize: 30.0);

        var propagator = new CentralBodyPropagator(propWindow, spc, integrator,
            new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            false, false, TimeSpan.FromSeconds(10.0));
        var res=propagator.Propagate();

        Assert.True(res.StateVectors.Count > 0);
        var firstState = res.StateVectors.First()
            .RelativeTo(earth, Aberration.None) as StateVector;
        var lastState = res.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        Assert.Equal(6800000.0, firstState!.Position.Magnitude());
        Assert.True(lastState!.Position.Magnitude() > 6799998.0 && lastState.Position.Magnitude() < 6800002.0,
            $"Position magnitude: {lastState.Position.Magnitude():F6} m (expected 6800000 ± 2 m)");
    }

    #endregion

    #region Propagator Integration (Central-body mode)

    [Fact]
    public void IntegratesWithCentralBodyPropagator()
    {
        // Central-body-centered propagation (Battin's formula), 2 h circular orbit sanity check.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);

        Clock clk = new Clock("My clock", 256);
        var orbit = new StateVector(
            new Vector3(6800000.0, 0, 0), new Vector3(0, 7656.2204182967143, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "TestSat_CB", 100.0, 10000.0, clk, orbit);

        var propWindow = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2));

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10, relativeTolerance: 1e-10,
            initialStepSize: 30.0);

        var propagator = new CentralBodyPropagator(propWindow, spc, integrator,
            new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            false, false, TimeSpan.FromSeconds(10.0));
        var res=propagator.Propagate();

        Assert.True(res.StateVectors.Count > 0);
        var firstState = res.StateVectors.First()
            .RelativeTo(earth, Aberration.None) as StateVector;
        var lastState = res.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        Assert.Equal(6800000.0, firstState!.Position.Magnitude());
        Assert.True(lastState!.Position.Magnitude() > 6799998.0 && lastState.Position.Magnitude() < 6800002.0,
            $"Position magnitude: {lastState.Position.Magnitude():F6} m (expected 6800000 ± 2 m)");
    }

    [Fact]
    public void IntegratesWithDragAndSrpViaCustomIntegrator()
    {
        // Verify RK78 works with drag and SRP via CentralBodyPropagator API
        var epoch = new TimeSystem.Time(2025, 6, 1, 12, 0, 0, frame: TimeFrame.UTCFrame);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new EarthStandardAtmosphere());

        Clock clk = new Clock("My clock", 256);
        double r0 = 6800000.0;
        double v0 = System.Math.Sqrt(earth.GM / r0);
        var orbit = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, epoch, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "TestSat", 100.0, 10000.0, clk, orbit,
            sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);

        var propWindow = new Window(epoch, epoch.AddHours(2));

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10, relativeTolerance: 1e-10,
            initialStepSize: 30.0);

        // Earth must be in the body list for atmospheric drag (BuildCentralBodyForces checks the list)
        var propagator = new CentralBodyPropagator(
            propWindow, spc, integrator,
            new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            includeAtmosphericDrag: true, includeSolarRadiationPressure: true,
            TimeSpan.FromSeconds(30.0));

        var res=propagator.Propagate();

        Assert.True(res.StateVectors.Count > 0);

        Assert.Contains(integrator.Forces, f => f is GravitationalAcceleration);
        Assert.Contains(integrator.Forces, f => f is AtmosphericDrag);
        Assert.Contains(integrator.Forces, f => f is SolarRadiationPressure);

        var lastState = res.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;
        double rFinal = lastState!.Position.Magnitude();
        Assert.True(rFinal > 6000000.0 && rFinal < 8000000.0,
            $"Final radius: {rFinal:F1} m, expected LEO range");
    }

    #endregion

    #region RK78 Conformance Case 001: LEO 24h, EGM2008 degree-10, Sun + Moon (Central-body mode)

    [Fact]
    public void Conformance001_Leo24hGrav10SunMoon_RK78_CentralBodyMode()
    {
        // Conformance case propagator_24h_leo_grav10_001, RK7(8) with tolerances 1e-11, as the conformance runner runs it.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "LEO_SAT_CB", 100.0, 10000.0, clk, orbit);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

        var propagator = new CentralBodyPropagator(
            propWindow, spc, integrator,
            new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            false, false, TimeSpan.FromSeconds(10.0));

        var res = propagator.Propagate();

        var expectedPosition = new Vector3(-5276164.48141924, 4263291.396350933, -404558.956106471);
        var expectedVelocity = new Vector3(-2724.567057501992, -3933.747338841648, -5983.827775625323);

        // Measured on 2026-10-04: 13.13 m and 14.81 mm/s.
        const double PositionLimitMeters = 13.5;
        const double VelocityLimitMetersPerSecond = 0.0155;
        ConformanceCaseAssert.FinalStateWithin(_output, "propagator_24h_leo_grav10_001 (RK7(8))", res, propWindow,
            expectedPosition, expectedVelocity, PositionLimitMeters, VelocityLimitMetersPerSecond);
    }

    #endregion

    #region RK78 Conformance Case 002: GEO 24h, EGM2008 degree-70, all bodies (Central-body mode)

    [Fact]
    public void Conformance002_Geo24hGrav70AllBodies_RK78_CentralBodyMode()
    {
        // Conformance case propagator_24h_geo_grav70_002, RK7(8) with tolerances 1e-11, as the conformance runner runs it.
        Clock clk = new Clock("My clock", 256);

        // Initial state of the conformance case (inputs.yaml), from which the golden was produced.
        var utcEpoch = new TimeSystem.Time(2026, 2, 9, 10, 22, 58, millisecond: 958, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 70));

        var orbit = new StateVector(
            new Vector3(19283848.018390323, 37944390.563573960, -328553.51550521),
            new Vector3(-2727.809889171343, 1386.957738048448, 52.987319351738),
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "INTELSAT901_CB", 3000.0, 5000.0, clk, orbit,
            sectionalArea: 50.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

        var propagator = new CentralBodyPropagator(
            propWindow, spc, integrator,
            new CelestialItem[]
            {
                PlanetsAndMoons.MOON_BODY,
                Stars.SUN_BODY,
                Barycenters.MERCURY_BARYCENTER,
                Barycenters.VENUS_BARYCENTER,
                Barycenters.MARS_BARYCENTER,
                Barycenters.JUPITER_BARYCENTER,
                Barycenters.SATURN_BARYCENTER,
                Barycenters.URANUS_BARYCENTER,
                Barycenters.NEPTUNE_BARYCENTER,
                Barycenters.PLUTO_BARYCENTER
            },
            false, false, TimeSpan.FromSeconds(10.0));

        var res= propagator.Propagate();

        var expectedPosition = new Vector3(22035054.64841816, 36415074.44453181, -382421.9052105268);
        var expectedVelocity = new Vector3(-2617.90823218342, 1584.740384557747, 51.26063967862107);

        // Measured on 2026-10-04: 8.02 m and 0.573 mm/s.
        const double PositionLimitMeters = 8.3;
        const double VelocityLimitMetersPerSecond = 0.0006;
        ConformanceCaseAssert.FinalStateWithin(_output, "propagator_24h_geo_grav70_002 (RK7(8))", res, propWindow,
            expectedPosition, expectedVelocity, PositionLimitMeters, VelocityLimitMetersPerSecond);
    }

    #endregion

    #region RK78 Conformance Case 003: SSO 24h, EGM2008 degree-10, Sun + Moon (Central-body mode)

    [Fact]
    public void Conformance003_Sso24hGrav10SunMoon_RK78_CentralBodyMode()
    {
        // Conformance case propagator_24h_sso_grav10_003, RK7(8) with tolerances 1e-11, as the conformance runner runs it.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new TimeSystem.Time(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        var keplerianOrbit = new KeplerianElements(
            7078137.0, 0.001,
            98.186 * System.Math.PI / 180.0,
            75.0 * System.Math.PI / 180.0,
            90.0 * System.Math.PI / 180.0,
            0.0,
            earth, utcEpoch, Frames.Frame.ICRF);
        var orbit = keplerianOrbit.ToStateVector();

        Spacecraft spc = new Spacecraft(-1001, "SSO_SAT_CB", 100.0, 1000.0, clk, orbit);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

        var propagator = new CentralBodyPropagator(
            propWindow, spc, integrator,
            new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            false, false, TimeSpan.FromSeconds(10.0));

        var res=propagator.Propagate();

        var expectedPosition = new Vector3(-608631.5307021005, 1650694.083265209, -6887696.349104228);
        var expectedVelocity = new Vector3(1985.415757873638, 7042.412568889923, 1515.859902259437);

        // Measured on 2026-10-04: 3.56 m and 3.84 mm/s.
        const double PositionLimitMeters = 4.0;
        const double VelocityLimitMetersPerSecond = 0.004;
        ConformanceCaseAssert.FinalStateWithin(_output, "propagator_24h_sso_grav10_003 (RK7(8))", res, propWindow,
            expectedPosition, expectedVelocity, PositionLimitMeters, VelocityLimitMetersPerSecond);
    }

    #endregion
}