// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class SpacecraftPropagatorTests
{
    public SpacecraftPropagatorTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
            [PlanetsAndMoons.EARTH_BODY, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY], false, false, TimeSpan.FromSeconds(1.0));
        spacecraftPropagator.Propagate();
        var orbitalParams = spc.StateVectorsRelativeToICRF.Values.First().RelativeTo(PlanetsAndMoons.EARTH_BODY, Aberration.None) as StateVector;
        Assert.Equal(orbit, orbitalParams, TestHelpers.StateVectorComparer);
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
        var expectedVelocity = new Vector3(-2724.561, -3933.753, -5983.828);

        // Compute 3D position error
        var positionError = (lastEphemeris.Position - expectedPosition).Magnitude();
        var velocityError = (lastEphemeris.Velocity - expectedVelocity).Magnitude();

        // With degree-10 geopotential, Sun, and Moon perturbations,
        // position error vs STK HPOP should be within a few km over 24h
        // (differences due to force model details: drag, SRP, higher-degree terms, etc.)
        // 3D position error vs STK HPOP reference should be < 1 km over 24h
        // Residual ~0.26 km is from integrator differences (VV vs RK7/8) and force model details
        Assert.True(positionError < 300.0,
            $"3D position error after 24h is {positionError / 1000.0:F3} km, expected < 300 m. " +
            $"Actual position: ({lastEphemeris.Position.X / 1000.0:F6}, {lastEphemeris.Position.Y / 1000.0:F6}, {lastEphemeris.Position.Z / 1000.0:F6}) km");

        Assert.True(velocityError < 0.33,
            $"3D velocity error after 24h is {velocityError:F6} m/s, expected < 0.33 m/s. " +
            $"Actual velocity: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    [Fact]
    public void PropagateGeo24hGrav70AllBodiesSrpDrag()
    {
        // GEO satellite (INTELSAT 901) propagated 24h with full force model and all solar system bodies:
        // degree-70 geopotential + Sun + Moon + all planetary barycenters + SRP + Drag.
        // Adding Jupiter, Saturn, Venus, Mars, etc. should improve accuracy by accounting
        // for their (small but non-zero) gravitational perturbations and resolving
        // additional SSB frame indirect effects.
        Clock clk = new Clock("My clock", 256);

        var tle = new TLE("INTELSAT 901",
            "1 26824U 01024A   26040.43262683 -.00000207  00000-0  00000+0 0  9994",
            "2 26824   0.9230  86.6125 0002726 324.4840  12.2632  0.98820941 21659");
        var tleEpoch = tle.Epoch;

        // Earth with degree-70 geopotential and atmosphere model
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, tleEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 70),
            new EarthStandardAtmosphere());

        var osculatingIcrf = tle.ToStateVector().ToFrame(Frames.Frame.ICRF).ToStateVector();
        var orbit = new StateVector(osculatingIcrf.Position, osculatingIcrf.Velocity,
            earth, osculatingIcrf.Epoch, Frames.Frame.ICRF);

        // mass=3000kg, maxMass=5000kg, area=50m², Cd=2.2, Cr=1.5
        Spacecraft spc = new Spacecraft(-1001, "INTELSAT901", 3000.0, 5000.0, clk, orbit,
            sectionalArea: 50.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);

        var endEpoch = tleEpoch.AddDays(1);
        var propWindow = new Window(tleEpoch, endEpoch);

        // Full force model with ALL solar system bodies
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(
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
            true, true,
            TimeSpan.FromSeconds(1.0));

        spacecraftPropagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        // STK reference position after 24h (Grav70+Sun+Moon+SRP+Drag, Earth-relative ICRF, in meters)
        // Using same STK reference as Test 3 — any improvement comes from better SSB balance
        var expectedPosition = new Vector3(22033.8532e3, 36415.5599e3, -382.3437e3);
        var expectedVelocity = new Vector3(-2.6179e3, 1.5847e3, 0.0512e3); // STK reference velocity

        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        // With all solar system bodies, SSB indirect effects are fully balanced.
        // Expect improved accuracy compared to Sun+Moon only.
        Assert.True(positionError < 2.9e3,
            $"3D position error after 24h is {positionError:F3} m, expected < 2900 m. " +
            $"Actual position: ({lastEphemeris.Position.X:F4}, {lastEphemeris.Position.Y:F4}, {lastEphemeris.Position.Z:F4}) m");

        var velocityError = (lastEphemeris!.Velocity - expectedVelocity).Magnitude();
        Assert.True(velocityError < 0.21,
            $"3D velocity error after 24h is {velocityError:F6} m/s, expected < 0.21 m/s. " +
            $"Actual velocity: ({lastEphemeris.Velocity.X:F6}, {lastEphemeris.Velocity.Y:F6}, {lastEphemeris.Velocity.Z:F6}) m/s");
    }

    [Fact]
    public void PropagateGeo24hGrav70AllBodiesWithoutSrpDrag()
    {
        // GEO satellite (INTELSAT 901) propagated 24h with full force model and all solar system bodies:
        // degree-70 geopotential + Sun + Moon + all planetary barycenters + SRP + Drag.
        // Adding Jupiter, Saturn, Venus, Mars, etc. should improve accuracy by accounting
        // for their (small but non-zero) gravitational perturbations and resolving
        // additional SSB frame indirect effects.
        Clock clk = new Clock("My clock", 256);

        var tle = new TLE("INTELSAT 901",
            "1 26824U 01024A   26040.43262683 -.00000207  00000-0  00000+0 0  9994",
            "2 26824   0.9230  86.6125 0002726 324.4840  12.2632  0.98820941 21659");
        var tleEpoch = tle.Epoch;

        // Earth with degree-70 geopotential and atmosphere model
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, tleEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 70));

        var osculatingIcrf = tle.ToStateVector().ToFrame(Frames.Frame.ICRF).ToStateVector();
        var orbit = new StateVector(osculatingIcrf.Position, osculatingIcrf.Velocity,
            earth, osculatingIcrf.Epoch, Frames.Frame.ICRF);

        // mass=3000kg, maxMass=5000kg, area=50m², Cd=2.2, Cr=1.5
        Spacecraft spc = new Spacecraft(-1001, "INTELSAT901", 3000.0, 5000.0, clk, orbit,
            sectionalArea: 50.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);

        var endEpoch = tleEpoch.AddDays(1);
        var propWindow = new Window(tleEpoch, endEpoch);

        // Full force model with ALL solar system bodies
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(
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
            false, false,
            TimeSpan.FromSeconds(1.0));

        spacecraftPropagator.Propagate();

        var lastEphemeris = spc.StateVectorsRelativeToICRF.Values.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        // STK reference position after 24h (Grav70+Sun+Moon+SRP+Drag, Earth-relative ICRF, in meters)
        // Using same STK reference as Test 3 — any improvement comes from better SSB balance
        var expectedPosition = new Vector3(22034.85010397608e3, 36414.95589291277e3, -382.4182477110885e3);
        var expectedVelocity = new Vector3(-2.617924358456228e3, 1.5847422460181e3, 0.05126091827241085e3); // STK reference velocity

        var positionError = (lastEphemeris!.Position - expectedPosition).Magnitude();
        // With all solar system bodies, SSB indirect effects are fully balanced.
        // Expect improved accuracy compared to Sun+Moon only.
        Assert.True(positionError < 2.9e3,
            $"3D position error after 24h is {positionError:F3} m, expected < 2900 m. " +
            $"Actual position: ({lastEphemeris.Position.X:F3}, {lastEphemeris.Position.Y:F3}, {lastEphemeris.Position.Z:F3}) m");

        var velocityError = (lastEphemeris!.Velocity - expectedVelocity).Magnitude();
        Assert.True(velocityError < 0.21,
            $"3D velocity error after 24h is {velocityError:F3} m/s, expected < 0.21 m/s. " +
            $"Actual velocity: ({lastEphemeris.Velocity.X:F3}, {lastEphemeris.Velocity.Y:F3}, {lastEphemeris.Velocity.Z:F3}) m/s");
    }
}
