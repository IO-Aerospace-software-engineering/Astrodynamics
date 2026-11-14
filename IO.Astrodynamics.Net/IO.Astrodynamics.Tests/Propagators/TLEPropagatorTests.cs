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
    public void Sgp4FailingWithinOneSecondOfTheEnd_LeavesTheSpacecraftAsItWas()
    {
        // 28350 after its decay (Sgp4ReferenceTests.FailedSgp4_Throws: Vallado's error 1, CSPICE's BADMECCENTRICITY at
        // 1500 min): SGP4 runs up to an instant and fails after it. A window that ends half a second before that instant
        // has its samples, and the acceleration at its end reads SGP4 1 s later, which fails (#675)
        var tle = new TLE("ERR", "1 28350U 04020A   06167.21788666  .16154492  76267-5  18678-3 0  8894",
            "2 28350  64.9977 345.6130 0024870 260.7578  99.9590 16.47856722116490");
        bool Fails(in IO.Astrodynamics.TimeSystem.Time epoch)
        {
            try
            {
                tle.ToStateVector(epoch);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        // The last instant SGP4 runs, by bisection on its failure between the epoch and 1500 min, to 1 ms
        var good = tle.Epoch.ToTDB();
        var bad = good.AddMinutes(1500.0);
        Assert.False(Fails(good));
        Assert.True(Fails(bad));
        while ((bad - good).TotalSeconds > 1e-3)
        {
            var middle = good.AddSeconds((bad - good).TotalSeconds / 2.0);
            if (Fails(middle))
            {
                bad = middle;
            }
            else
            {
                good = middle;
            }
        }

        var end = good.AddSeconds(-0.5);
        Assert.False(Fails(end));
        Assert.True(Fails(end.AddSeconds(1.0)));
        var spacecraft = new Spacecraft(-1003, "Decayed", 100.0, 10000.0, new Clock("Decayed clock", 256), tle);
        var step = TimeSpan.FromSeconds(60.0);
        using (var first = new TLEPropagator(new Window(end.AddSeconds(-3600.0), end.AddSeconds(-1800.0)), spacecraft, step))
        {
            first.Propagate();
        }

        var before = spacecraft.PropagatedStates;
        Assert.Equal(31, before.Length);

        // Act: a window whose samples SGP4 gives, the acceleration at its end not
        using var propagator = new TLEPropagator(new Window(end.AddSeconds(-600.0), end), spacecraft, step);
        Assert.Throws<InvalidOperationException>(() => propagator.Propagate());

        // Assert: the rejected run adds no state; the spacecraft holds the states of the first run as they were
        var after = spacecraft.PropagatedStates;
        Assert.Equal(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i].Epoch, after[i].Epoch);
            Assert.Equal(before[i].Position, after[i].Position);
            Assert.Equal(before[i].Velocity, after[i].Velocity);
        }
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