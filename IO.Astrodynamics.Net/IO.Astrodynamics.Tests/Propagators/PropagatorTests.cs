// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class PropagatorTests
{
    private readonly ITestOutputHelper _output;

    public PropagatorTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    #region Infrastructure Tests

    [Fact]
    public void CheckSymplecticProperty()
    {
        // VV integrator should conserve energy (symplectic property).
        // Use a GEO orbit around Earth with no perturbations to isolate integrator behavior.
        Clock clk = new Clock("My clock", 256);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var orbit = new KeplerianElements(42164000.0, 0, 0, 0, 0, 0, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        var propagator = new Propagator.CentralBodyPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1)), spc,
            System.Array.Empty<CelestialItem>(), false, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();
        var energy = solution.StateVectors
            .Select(x => x.RelativeTo(earth, Aberration.None).ToStateVector().SpecificOrbitalEnergy())
            .ToArray();
        var min = energy.Min();
        var max = energy.Max();
        var diff = max - min;
        Assert.True(diff < 1e-4, $"Energy variation {diff:E3} exceeds limit 1E-4");
    }

    [Fact]
    public void CheckInitialStep()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7500.0, 0), new CelestialBody(PlanetsAndMoons.EARTH), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        var propagator = new Propagator.CentralBodyPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2)), spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY], false, false, TimeSpan.FromSeconds(1.0));
        var solution = propagator.Propagate();
        var orbitalParams = solution.StateVectors.First().RelativeTo(PlanetsAndMoons.EARTH_BODY, Aberration.None) as StateVector;
        Assert.Equal(orbit, orbitalParams, TestHelpers.StateVectorComparer);
    }

    [Fact]
    public void CheckNonFittingStepSize()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, Barycenters.SOLAR_SYSTEM_BARYCENTER, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        var propagator = new Propagator.CentralBodyPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(5)),
            spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, TimeSpan.FromSeconds(2.0));

        var solution = propagator.Propagate();
        var state = solution.StateVectors[0];
        var state1 = solution.StateVectors[1];
        var state2 = solution.StateVectors[2];
        var state3 = solution.StateVectors[3];
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

        var propagator = new Propagator.CentralBodyPropagator(
            window, spc,
            [Barycenters.SOLAR_SYSTEM_BARYCENTER], false, false, deltaT);

        var solution = propagator.Propagate();

        var stateVectors = solution.StateVectors.ToArray();
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

        var solution = propagator.Propagate();

        var firstEphemeris = solution.StateVectors.First()
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

        var solution1 = propagator1.Propagate();
        var solution2 = propagator2.Propagate();

        var results1 = solution1.StateVectors.ToArray();
        var results2 = solution2.StateVectors.ToArray();

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

        var solution = propagator.Propagate();

        var firstEphemeris = solution.StateVectors.First()
            .RelativeTo(earth, Aberration.None) as StateVector;

        var posDiff = (firstEphemeris!.Position - orbit.Position).Magnitude();
        var velDiff = (firstEphemeris.Velocity - orbit.Velocity).Magnitude();

        Assert.True(posDiff < 1.0, $"Position difference is {posDiff} meters, expected < 1 meter");
        Assert.True(velDiff < 0.001, $"Velocity difference is {velDiff} m/s, expected < 0.001 m/s");
    }

    #endregion

    #region Conformance Case 001: LEO 24h, EGM2008 degree-10, Sun + Moon (Central-body mode)

    [Fact]
    public void Conformance001_Leo24hGrav10SunMoon_CentralBodyMode()
    {
        // Conformance case propagator_24h_leo_grav10_001, Velocity-Verlet with a fixed 1 s step.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });

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

        var solution = propagator.Propagate();

        var expectedPosition = new Vector3(-5276.166249806454, 4263.287696867372, -404.56444929731526) * 1000.0;
        var expectedVelocity = new Vector3(-2.724561254362544, -3.933752425169912, -5.983827910193672) * 1000.0;

        // Against the golden of conformance-tests#25 (GMAT R2026a, EGM2008 with its GM and radius applied to the whole
        // field, ICRF), the GM of the model in the central term (decision 18 of #457). Measured on Linux, Release,
        // 2026-10-27: 281.3 m and 317.3 mm/s, nearly all the truncation of the 1 s step; the limits are twice these values, rounded up at the second significant digit.
        const double PositionLimitMeters = 570.0;
        const double VelocityLimitMetersPerSecond = 0.64;
        ConformanceCaseAssert.FinalStateWithin(_output, "propagator_24h_leo_grav10_001 (Verlet 1 s)", solution, propWindow,
            expectedPosition, expectedVelocity, PositionLimitMeters, VelocityLimitMetersPerSecond);
    }

    #endregion

    #region Conformance Case 002: GEO 24h, EGM2008 degree-70, all bodies (Central-body mode)

    [Fact]
    public void Conformance002_Geo24hGrav70AllBodies_CentralBodyMode()
    {
        // Conformance case propagator_24h_geo_grav70_002, Velocity-Verlet with a fixed 1 s step.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2026, 2, 9, 10, 22, 58, millisecond: 958, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 70)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });

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

        var solution = propagator.Propagate();

        var expectedPosition = new Vector3(22035.05468573585, 36415.074430780434, -382.4219127743028) * 1000.0;
        var expectedVelocity = new Vector3(-2.6179082304622505, 1.584740386819964, 0.051260640408016526) * 1000.0;

        // Against the golden of conformance-tests#25 (GMAT R2026a, EGM2008 with its GM and radius applied to the whole
        // field, ICRF), the GM of the model in the central term (decision 18 of #457). Measured on Linux, Release,
        // 2026-10-27: 0.3936 m and 0.02801 mm/s; the limits are twice these values, rounded up at the second significant digit.
        const double PositionLimitMeters = 0.79;
        const double VelocityLimitMetersPerSecond = 5.7e-5;
        ConformanceCaseAssert.FinalStateWithin(_output, "propagator_24h_geo_grav70_002 (Verlet 1 s)", solution, propWindow,
            expectedPosition, expectedVelocity, PositionLimitMeters, VelocityLimitMetersPerSecond);
    }

    #endregion

    #region Conformance Case 003: SSO 24h, EGM2008 degree-10, Sun + Moon (Central-body mode)

    [Fact]
    public void Conformance003_Sso24hGrav10SunMoon_CentralBodyMode()
    {
        // Conformance case propagator_24h_sso_grav10_003, Velocity-Verlet with a fixed 1 s step.
        Clock clk = new Clock("My clock", 256);

        var utcEpoch = new Astrodynamics.TimeSystem.Time(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame);

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, utcEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });

        var orbit = new StateVector(
            new Vector3(972.5206216644625, -260.5861151976055, 6999.012213235124) * 1000.0,
            new Vector3(-1.9441954872554523, -7.255836338283476, 4.534910638063485e-16) * 1000.0,
            earth, utcEpoch, Frames.Frame.ICRF);

        Spacecraft spc = new Spacecraft(-1001, "SSO_SAT_CB", 100.0, 1000.0, clk, orbit);

        var propWindow = new Window(utcEpoch, utcEpoch.AddDays(1));

        var propagator = new Propagator.CentralBodyPropagator(
            propWindow, spc,
            [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
            false, false, TimeSpan.FromSeconds(1.0));

        var solution = propagator.Propagate();

        var expectedPosition = new Vector3(-608.631165932607, 1650.695661501674, -6887.695996757752) * 1000.0;
        var expectedVelocity = new Vector3(1.9854154176234544, 7.042412295817178, 1.5158616109621321) * 1000.0;

        // Against the golden of conformance-tests#25 (GMAT R2026a, EGM2008 with its GM and radius applied to the whole
        // field, ICRF), the GM of the model in the central term (decision 18 of #457). Measured on Linux, Release,
        // 2026-10-27: 243.0 m and 254.8 mm/s, nearly all the truncation of the 1 s step; the limits are twice these values, rounded up at the second significant digit.
        const double PositionLimitMeters = 490.0;
        const double VelocityLimitMetersPerSecond = 0.51;
        ConformanceCaseAssert.FinalStateWithin(_output, "propagator_24h_sso_grav10_003 (Verlet 1 s)", solution, propWindow,
            expectedPosition, expectedVelocity, PositionLimitMeters, VelocityLimitMetersPerSecond);
    }

    #endregion
}
