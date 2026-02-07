// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class SpacecraftPropagatorTests
{
    public SpacecraftPropagatorTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void CheckSymplecticProperty()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, Barycenters.SOLAR_SYSTEM_BARYCENTER, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(30)), spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, TimeSpan.FromSeconds(100.0));
        spacecraftPropagator.Propagate();
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
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2)), spc,
            [PlanetsAndMoons.EARTH_BODY,PlanetsAndMoons.MOON_BODY,Stars.SUN_BODY], false, false, TimeSpan.FromSeconds(1.0));
        spacecraftPropagator.Propagate();
        var orbitalParams = spc.StateVectorsRelativeToICRF.Values.First().RelativeTo(PlanetsAndMoons.EARTH_BODY,Aberration.None) as StateVector;
        Assert.Equal(orbit,orbitalParams,TestHelpers.StateVectorComparer);
    }

    [Fact]
    public void CheckNonFittingStepSize()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, Barycenters.SOLAR_SYSTEM_BARYCENTER, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(5)),
            spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, TimeSpan.FromSeconds(2.0));

        spacecraftPropagator.Propagate();
        var state = spc.StateVectorsRelativeToICRF.Values.ElementAt(0);
        var state1 = spc.StateVectorsRelativeToICRF.Values.ElementAt(1);
        var state2 = spc.StateVectorsRelativeToICRF.Values.ElementAt(2);
        var state3 = spc.StateVectorsRelativeToICRF.Values.ElementAt(3);
    }

    [Fact]
    public void CheckInitialStepWithUTCEpoch()
    {
        // Reproduce user's scenario: StateVector defined with UTC epoch
        Clock clk = new Clock("My clock", 256);

        // User's input (converted from km to m)
        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);
        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),  // Position in meters
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),     // Velocity in m/s
            new CelestialBody(PlanetsAndMoons.EARTH),
            utcEpoch,
            Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);

        var endEpoch = utcEpoch.AddHours(2);
        var propWindow = new Window(utcEpoch, endEpoch);

        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(
            propWindow, spc,
            [PlanetsAndMoons.EARTH_BODY, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false,
            TimeSpan.FromSeconds(1.0));

        spacecraftPropagator.Propagate();

        // Get first ephemeris point and convert back to Earth-relative
        var firstEphemeris = spc.StateVectorsRelativeToICRF.Values.First()
            .RelativeTo(PlanetsAndMoons.EARTH_BODY, Aberration.None) as StateVector;

        // The first ephemeris should match the initial orbit (within numerical tolerance)
        // Position difference should be less than 1 meter
        var posDiff = (firstEphemeris.Position - orbit.Position).Magnitude();
        var velDiff = (firstEphemeris.Velocity - orbit.Velocity).Magnitude();

        Assert.True(posDiff < 1.0, $"Position difference is {posDiff} meters, expected < 1 meter");
        Assert.True(velDiff < 0.001, $"Velocity difference is {velDiff} m/s, expected < 0.001 m/s");
    }

    [Fact]
    public void Propagate24hWithDegree10Geopotential()
    {
        // 24h propagation with EGM2008 degree-10 geopotential
        // Expected results from STK HPOP / Orekit reference
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);

        // Earth with degree-10 geopotential (EGM2008)
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        // Initial state vector (km → m, km/s → m/s)
        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            earth,
            utcEpoch,
            Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);

        var endEpoch = utcEpoch.AddDays(1);
        var propWindow = new Window(utcEpoch, endEpoch);

        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(
            propWindow, spc,
            [earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false,
            TimeSpan.FromSeconds(1.0));

        spacecraftPropagator.Propagate();

        // Get the last ephemeris point and convert to Earth-relative
        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        // Expected position after 24h (from STK HPOP reference, in meters)
        var expectedPosition = new Vector3(-5276159.136, 4263301.774, -404522.560);

        // Compute 3D position error
        var positionError = (lastEphemeris.Position - expectedPosition).Magnitude();

        // With degree-10 geopotential, Sun, and Moon perturbations,
        // position error vs STK HPOP should be within a few km over 24h
        // (differences due to force model details: drag, SRP, higher-degree terms, etc.)
        // 3D position error vs STK HPOP reference should be < 1 km over 24h
        // Residual ~0.26 km is from integrator differences (VV vs RK7/8) and force model details
        Assert.True(positionError < 1000.0,
            $"3D position error after 24h is {positionError / 1000.0:F3} km, expected < 1 km. " +
            $"Actual position: ({lastEphemeris.Position.X / 1000.0:F6}, {lastEphemeris.Position.Y / 1000.0:F6}, {lastEphemeris.Position.Z / 1000.0:F6}) km");
    }
}
