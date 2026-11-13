// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using Xunit;
using Xunit.Abstractions;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class TLEPropagatorTests
{
    // Twice the worst of InterpolatedVelocity_FollowsSgp4 on Linux x64 (2026-11-13): 4.28e-4 m/s and 0.376 m. Provisional,
    // to be set from the three platforms
    private const double InterpolatedVelocityBound = 8.6e-4;
    private const double InterpolatedPositionBound = 0.76;

    private readonly ITestOutputHelper _output;

    public TLEPropagatorTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void InterpolatedVelocity_FollowsSgp4()
    {
        // ISS, 1 h at 60 s: between the samples the solution is the cubic Hermite of the step, whose velocity needs the
        // accelerations at both ends (#675). Before the fix they were zero, and the velocity was up to 49.9 m/s off SGP4
        // near the third points of a step. The reference is SGP4 at the exact instant through CSPICE evsgp4_c, not the
        // interpolation under test; the leading-order Hermite error with exact end accelerations, h⁴/384·v·n⁴ = 4.17e-4
        // m/s, is a plausibility check only (measured above it), so the bounds come from the measurement
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        var spacecraft = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, new Clock("My clock", 256), tle);
        var solution = new TLEPropagator(window, spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();
        var start = window.StartDate.ToTDB();

        // At each sample, the SGP4 state bit for bit
        for (int i = 0; i <= 60; i++)
        {
            var epoch = start.AddSeconds(60.0 * i);
            var sgp4 = tle.ToStateVector(epoch);
            var (position, velocity) = solution.InterpolateAt(epoch);
            Assert.Equal(sgp4.Position, position);
            Assert.Equal(sgp4.Velocity, velocity);
        }

        double worstVelocity = 0.0, worstPosition = 0.0;
        for (double s = 1.0; s < 3600.0; s += 7.0)
        {
            var epoch = start.AddSeconds(s);
            var sgp4 = tle.ToStateVector(epoch);
            var (position, velocity) = solution.InterpolateAt(epoch);
            worstVelocity = System.Math.Max(worstVelocity, (velocity - sgp4.Velocity).Magnitude());
            worstPosition = System.Math.Max(worstPosition, (position - sgp4.Position).Magnitude());
        }

        _output.WriteLine($"MEASURE TLEPropagatorTests.InterpolatedVelocity_FollowsSgp4: velocity {worstVelocity:E3} m/s, " +
                          $"position {worstPosition:E3} m");
        Assert.InRange(worstVelocity, 0.0, InterpolatedVelocityBound);
        Assert.InRange(worstPosition, 0.0, InterpolatedPositionBound);
    }

    [Fact]
    public void CheckPropagation()
    {
        Clock clk = new Clock("My clock", 256);
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, tle);
        var start = spc.InitialOrbitalParameters.Epoch.ToTDB();
        var end = start.AddDays(1.0);
        TLEPropagator propagator = new TLEPropagator(new Window(start, end), spc, TimeSpan.FromHours(1.0));
        var solution = propagator.Propagate();
        Assert.Equal(tle.ToStateVector(start), solution.StateVectors.First(x => x.Epoch == start));
        Assert.Equal(tle.ToStateVector(end), solution.StateVectors.First(x => x.Epoch == end));
    }
}