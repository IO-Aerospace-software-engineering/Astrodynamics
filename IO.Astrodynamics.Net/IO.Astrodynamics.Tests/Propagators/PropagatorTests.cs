// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class PropagatorTests
{
    public PropagatorTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    #region Infrastructure Tests

    [Fact]
    public void CheckSymplecticProperty()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, Barycenters.SOLAR_SYSTEM_BARYCENTER, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        var propagator = new Propagator.SsbPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(30)), spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, TimeSpan.FromSeconds(100.0));
        propagator.Propagate();
        var energy = spc.StateVectorsRelativeToICRF.Values.Select(x => x.SpecificOrbitalEnergy()).ToArray();
        var min = energy.Min();
        var max = energy.Max();
        var diff = max - min;
        Assert.True(diff < 9.8E-05);
    }

    [Fact]
    public void CheckInitialStep()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7500.0, 0), new CelestialBody(PlanetsAndMoons.EARTH), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        var propagator = new Propagator.CentralBodyPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2)), spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY], false, false, TimeSpan.FromSeconds(1.0));
        propagator.Propagate();
        var orbitalParams = spc.StateVectorsRelativeToICRF.Values.First().RelativeTo(PlanetsAndMoons.EARTH_BODY, Aberration.None) as StateVector;
        Assert.Equal(orbit, orbitalParams, TestHelpers.StateVectorComparer);
    }

    [Fact]
    public void CheckNonFittingStepSize()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, Barycenters.SOLAR_SYSTEM_BARYCENTER, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        var propagator = new Propagator.SsbPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(5)),
            spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, TimeSpan.FromSeconds(2.0));

        propagator.Propagate();
        var state = spc.StateVectorsRelativeToICRF.Values.ElementAt(0);
        var state1 = spc.StateVectorsRelativeToICRF.Values.ElementAt(1);
        var state2 = spc.StateVectorsRelativeToICRF.Values.ElementAt(2);
        var state3 = spc.StateVectorsRelativeToICRF.Values.ElementAt(3);
    }

    [Theory]
    [InlineData(3600, 1.0)]    // 1 hour, step 1s — evenly divides
    [InlineData(7200, 1.0)]    // 2 hours, step 1s — evenly divides
    [InlineData(5, 2.0)]       // 5s window, step 2s — non-fitting
    [InlineData(10, 3.0)]      // 10s window, step 3s — non-fitting
    [InlineData(100, 7.0)]     // 100s window, step 7s — non-fitting
    [InlineData(86400, 100.0)] // 24h, step 100s — evenly divides
    public void CacheSizeAndTimeSpanAreConsistent(double windowSeconds, double stepSeconds)
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, Barycenters.SOLAR_SYSTEM_BARYCENTER, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);

        var windowStart = TimeSystem.Time.J2000TDB;
        var windowEnd = windowStart.AddSeconds(windowSeconds);
        var window = new Window(windowStart, windowEnd);
        var deltaT = TimeSpan.FromSeconds(stepSeconds);

        var propagator = new Propagator.SsbPropagator(
            window, spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, deltaT);

        propagator.Propagate();

        var stateVectors = spc.StateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray();
        var expectedCount = (uint)System.Math.Round(windowSeconds / stepSeconds, MidpointRounding.AwayFromZero) + 1;

        Assert.Equal((int)expectedCount, stateVectors.Length);

        var firstEpoch = stateVectors.First().Epoch;
        var lastEpoch = stateVectors.Last().Epoch;

        Assert.Equal(window.StartDate.TimeSpanFromJ2000().TotalSeconds, firstEpoch.TimeSpanFromJ2000().TotalSeconds, 6);

        var actualSpan = (lastEpoch - firstEpoch).TotalSeconds;
        var expectedSpan = (expectedCount - 1) * stepSeconds;
        Assert.Equal(expectedSpan, actualSpan, 6);

        Assert.True(lastEpoch <= window.EndDate.AddSeconds(stepSeconds),
            $"Last epoch {lastEpoch} exceeds window end {window.EndDate} + one step");

        for (int i = 1; i < stateVectors.Length; i++)
        {
            var dt = (stateVectors[i].Epoch - stateVectors[i - 1].Epoch).TotalSeconds;
            Assert.Equal(stepSeconds, dt, 6);
        }
    }

    [Fact]
    public void CheckInitialStepWithUTCEpoch()
    {
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);
        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            new CelestialBody(PlanetsAndMoons.EARTH),
            utcEpoch,
            Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);

        var endEpoch = utcEpoch.AddHours(2);
        var propWindow = new Window(utcEpoch, endEpoch);

        var propagator = new Propagator.CentralBodyPropagator(
            propWindow, spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false,
            TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var firstEphemeris = spc.StateVectorsRelativeToICRF.Values.First()
            .RelativeTo(PlanetsAndMoons.EARTH_BODY, Aberration.None) as StateVector;

        var posDiff = (firstEphemeris.Position - orbit.Position).Magnitude();
        var velDiff = (firstEphemeris.Velocity - orbit.Velocity).Magnitude();

        Assert.True(posDiff < 1.0, $"Position difference is {posDiff} meters, expected < 1 meter");
        Assert.True(velDiff < 0.001, $"Velocity difference is {velDiff} m/s, expected < 0.001 m/s");
    }

    [Fact]
    public void PropagateWithInjectedIntegratorMatchesConvenienceConstructor()
    {
        Clock clk1 = new Clock("My clock", 256);
        Clock clk2 = new Clock("My clock", 256);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var orbit1 = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7500.0, 0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var orbit2 = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7500.0, 0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc1 = new Spacecraft(-1001, "MySpacecraft1", 100.0, 10000.0, clk1, orbit1);
        Spacecraft spc2 = new Spacecraft(-1002, "MySpacecraft2", 100.0, 10000.0, clk2, orbit2);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2));
        var deltaT = TimeSpan.FromSeconds(1.0);
        CelestialItem[] bodies = [PlanetsAndMoons.EARTH_BODY, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY];

        var propagator1 = new Propagator.CentralBodyPropagator(window, spc1, bodies, false, false, deltaT);

        var initialState = spc2.InitialOrbitalParameters.AtEpoch(window.StartDate.ToTDB()).ToStateVector()
            .RelativeTo(earth, Aberration.None).ToStateVector();
        var forces = new List<ForceBase>();
        forces.Add(new GravitationalAcceleration(earth));
        foreach (var body in bodies.Where(b => !b.Equals(earth)))
        {
            forces.Add(new ThirdBodyPerturbation(body, earth));
        }

        IIntegrator integrator = new VVIntegrator(forces, deltaT, initialState);
        var propagator2 = new Propagator.CentralBodyPropagator(window, spc2, integrator, deltaT);

        propagator1.Propagate();
        propagator2.Propagate();

        var results1 = spc1.StateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray();
        var results2 = spc2.StateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray();

        Assert.Equal(results1.Length, results2.Length);
        for (int i = 0; i < results1.Length; i++)
        {
            Assert.Equal(results1[i].Position.X, results2[i].Position.X, 6);
            Assert.Equal(results1[i].Position.Y, results2[i].Position.Y, 6);
            Assert.Equal(results1[i].Position.Z, results2[i].Position.Z, 6);
            Assert.Equal(results1[i].Velocity.X, results2[i].Velocity.X, 6);
            Assert.Equal(results1[i].Velocity.Y, results2[i].Velocity.Y, 6);
            Assert.Equal(results1[i].Velocity.Z, results2[i].Velocity.Z, 6);
        }
    }

    [Fact]
    public void CentralBodyModeInitialStateMatchesInput()
    {
        Clock clk = new Clock("My clock", 256);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var orbit = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7500.0, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);

        var propagator = new Propagator.CentralBodyPropagator(
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2)),
            spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var firstEphemeris = spc.StateVectorsRelativeToICRF.Values.First()
            .RelativeTo(earth, Aberration.None) as StateVector;

        var posDiff = (firstEphemeris!.Position - orbit.Position).Magnitude();
        var velDiff = (firstEphemeris.Velocity - orbit.Velocity).Magnitude();

        Assert.True(posDiff < 1.0, $"Position difference is {posDiff} meters, expected < 1 meter");
        Assert.True(velDiff < 0.001, $"Velocity difference is {velDiff} m/s, expected < 0.001 m/s");
    }

    #endregion

    #region Conformance Case 001: LEO 24h, EGM2008 degree-10, Sun + Moon (SSB mode)

    [Fact]
    public void Conformance001_Leo24hGrav10SunMoon_SsbMode()
    {
        // Conformance case propagator_24h_geo10_001:
        // 24h propagation with EGM2008 degree-10 geopotential, Sun and Moon perturbations.
        // No drag, no SRP.
        // Reference: GMAT R2025a, JGM3 10x10, de440s, PrinceDormand78 (RK7/8), accuracy 1e-13.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "LEO_SAT", 100.0, 10000.0, clk, orbit);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var propagator = new Propagator.SsbPropagator(
            propWindow, spc,
            [earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        // GMAT R2025a reference (JGM3 10x10, de440s, PrinceDormand78 RK7/8)
        var expectedPosition = new Vector3(-5276164.48141924, 4263291.396350933, -404558.956106471);
        var expectedVelocity = new Vector3(-2724.567057501992, -3933.747338841648, -5983.827775625323);

        // Conformance tolerances: position 300 m, velocity 0.3 m/s (JGM3 matching).
        // Widened to 0.35 m/s for velocity to account for EGM2008 vs JGM3 geopotential difference.
        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        Assert.True(positionError < 300.0,
            $"Position error: {positionError:F3} m (limit: 300 m). " +
            $"Actual: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        Assert.True(velocityError < 0.35,
            $"Velocity error: {velocityError:F6} m/s (limit: 0.35 m/s). " +
            $"Actual: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    #endregion

    #region Conformance Case 001: LEO 24h, EGM2008 degree-10, Sun + Moon (Central-body mode)

    [Fact]
    public void Conformance001_Leo24hGrav10SunMoon_CentralBodyMode()
    {
        // Same as Conformance001 but using central-body-centered propagation with Battin's formula.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "LEO_SAT_CB", 100.0, 10000.0, clk, orbit);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var propagator = new Propagator.CentralBodyPropagator(
            propWindow, spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        var expectedPosition = new Vector3(-5276164.48141924, 4263291.396350933, -404558.956106471);
        var expectedVelocity = new Vector3(-2724.567057501992, -3933.747338841648, -5983.827775625323);

        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        Assert.True(positionError < 300.0,
            $"Position error: {positionError:F3} m (limit: 300 m). " +
            $"Actual: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        Assert.True(velocityError < 0.35,
            $"Velocity error: {velocityError:F6} m/s (limit: 0.35 m/s). " +
            $"Actual: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    #endregion

    #region Conformance Case 002: GEO 24h, EGM2008 degree-70, all bodies (SSB mode)

    [Fact]
    public void Conformance002_Geo24hGrav70AllBodies_SsbMode()
    {
        // Conformance case propagator_24h_geo70_full_002:
        // GEO satellite 24h propagation with EGM2008 degree-70 geopotential,
        // Sun, Moon, and all planetary barycenters. No SRP, no drag.
        // Reference: GMAT R2025a, JGM3 70x70, de440s, PrinceDormand78 (RK7/8), accuracy 1e-13.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2026, 2, 9, 10, 22, 58, millisecond: 958, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 70));

        // State vector from conformance case (km -> m, km/s -> m/s)
        var orbit = new StateVector(
            new Vector3(19283848.018390323, 37944390.563573960, -328553.51550521),
            new Vector3(-2727.809889171343, 1386.957738048448, 52.987319351738),
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "GEO_SAT", 3000.0, 5000.0, clk, orbit,
            sectionalArea: 50.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var propagator = new Propagator.SsbPropagator(
            propWindow, spc,
            [
                earth,
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
            ],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        // GMAT R2025a reference (JGM3 70x70, de440s, PrinceDormand78 RK7/8)
        var expectedPosition = new Vector3(22035054.64841816, 36415074.44453181, -382421.9052105268);
        var expectedVelocity = new Vector3(-2617.90823218342, 1584.740384557747, 51.26063967862107);

        // Tolerances from conformance case: position 50 m, velocity 0.005 m/s
        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        Assert.True(positionError < 9.0,
            $"Position error: {positionError:F3} m (limit: 9 m). " +
            $"Actual: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        Assert.True(velocityError < 0.0007,
            $"Velocity error: {velocityError:F6} m/s (limit: 0.0007 m/s). " +
            $"Actual: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    #endregion

    #region Conformance Case 002: GEO 24h, EGM2008 degree-70, all bodies (Central-body mode)

    [Fact]
    public void Conformance002_Geo24hGrav70AllBodies_CentralBodyMode()
    {
        // Same as Conformance002 but using central-body-centered propagation with Battin's formula.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2026, 2, 9, 10, 22, 58, millisecond: 958, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 70));

        var orbit = new StateVector(
            new Vector3(19283848.018390323, 37944390.563573960, -328553.51550521),
            new Vector3(-2727.809889171343, 1386.957738048448, 52.987319351738),
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "GEO_SAT_CB", 3000.0, 5000.0, clk, orbit,
            sectionalArea: 50.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var propagator = new Propagator.CentralBodyPropagator(
            propWindow, spc,
            [
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
            ],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        var expectedPosition = new Vector3(22035054.64841816, 36415074.44453181, -382421.9052105268);
        var expectedVelocity = new Vector3(-2617.90823218342, 1584.740384557747, 51.26063967862107);

        // Central-body mode should match SSB mode accuracy (both are mathematically equivalent)
        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        Assert.True(positionError < 9.0,
            $"Position error: {positionError:F3} m (limit: 50 m). " +
            $"Actual: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        Assert.True(velocityError < 0.0007,
            $"Velocity error: {velocityError:F6} m/s (limit: 0.005 m/s). " +
            $"Actual: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    #endregion

    #region Conformance Case 003: SSO 24h, EGM2008 degree-10, Sun + Moon (SSB mode)

    [Fact]
    public void Conformance003_Sso24hGrav10SunMoon_SsbMode()
    {
        // Conformance case propagator_24h_sso10_003:
        // Sun-Synchronous Orbit 24h propagation with EGM2008 degree-10 geopotential,
        // Sun and Moon perturbations. No drag, no SRP.
        // Frozen orbit: e=0.001, AoP=90 deg, i=98.186 deg (J2 nodal precession ~+0.9857 deg/day).
        // Reference: GMAT R2025a, JGM3 10x10, de440s, PrinceDormand78 (RK7/8), accuracy 1e-13.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame);

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

        Spacecraft spc = new Spacecraft(-1001, "SSO_SAT", 100.0, 1000.0, clk, orbit);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var propagator = new Propagator.SsbPropagator(
            propWindow, spc,
            [earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        // GMAT R2025a reference (JGM3 10x10, de440s, PrinceDormand78 RK7/8)
        var expectedPosition = new Vector3(-608631.5307021005, 1650694.083265209, -6887696.349104228);
        var expectedVelocity = new Vector3(1985.415757873638, 7042.412568889923, 1515.859902259437);

        // Tolerances from conformance case: position 300 m, velocity 0.3 m/s
        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        Assert.True(positionError < 260.0,
            $"Position error: {positionError:F3} m (limit: 300 m). " +
            $"Actual: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        Assert.True(velocityError < 0.28,
            $"Velocity error: {velocityError:F6} m/s (limit: 0.28 m/s). " +
            $"Actual: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    #endregion

    #region Conformance Case 003: SSO 24h, EGM2008 degree-10, Sun + Moon (Central-body mode)

    [Fact]
    public void Conformance003_Sso24hGrav10SunMoon_CentralBodyMode()
    {
        // Same as Conformance003 but using central-body-centered propagation with Battin's formula.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame);

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

        var propagator = new Propagator.CentralBodyPropagator(
            propWindow, spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false, TimeSpan.FromSeconds(1.0));

        propagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        var expectedPosition = new Vector3(-608631.5307021005, 1650694.083265209, -6887696.349104228);
        var expectedVelocity = new Vector3(1985.415757873638, 7042.412568889923, 1515.859902259437);

        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        Assert.True(positionError < 250.0,
            $"Position error: {positionError:F3} m (limit: 300 m). " +
            $"Actual: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        Assert.True(velocityError < 0.26,
            $"Velocity error: {velocityError:F6} m/s (limit: 0.3 m/s). " +
            $"Actual: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    #endregion
}
