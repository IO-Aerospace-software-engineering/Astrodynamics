// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Exceptions;
using Xunit;
using Xunit.Abstractions;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class TLEPropagatorTests
{
    // Twice the worst of InterpolatedVelocity_FollowsSgp4 over Linux x64, Windows and macOS arm64 (CI run 43591654097, 2026-11-14), rounded up at
    // the second significant digit: velocity 4.282e-4, 4.282e-4 and 4.282e-4 m/s; position 0.3762, 0.3762 and 0.3762 m
    private const double InterpolatedVelocityBound = 8.6e-4;
    private const double InterpolatedPositionBound = 0.76;

    // Twice the worst of LastShortStep_InterpolatesSgp4 over Linux x64, Windows x64 and macOS arm64 alike (CI run
    // 44225490669), rounded up at the second significant digit: velocity 2.701e-5 m/s, position 3.726e-2 m
    private const double LastShortStepVelocityBound = 5.5e-5;
    private const double LastShortStepPositionBound = 0.075;

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

        // At each sample, the SGP4 state bit for bit: every 60 s from the TDB start, the last at the TDB end of the window,
        // 11 ticks after +3600 s (#598: a UTC hour is a few ticks longer in TDB; before, the last sample was at +3600 s)
        var end = window.EndDate.ToTDB();
        for (int i = 0; i <= 60; i++)
        {
            var epoch = i == 60 ? end : start.AddSeconds(60.0 * i);
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
        new TLEPropagator(new Window(end.AddSeconds(-3600.0), end.AddSeconds(-1800.0)), spacecraft, step).Propagate();

        var before = spacecraft.PropagatedStates;
        Assert.Equal(31, before.Length);

        // Act: a window whose samples SGP4 gives, the acceleration at its end not
        var propagator = new TLEPropagator(new Window(end.AddSeconds(-600.0), end), spacecraft, step);
        Assert.Throws<SpiceException>(() => propagator.Propagate());

        // Assert: the rejected run adds no state; the spacecraft holds the states of the first run as they were
        var after = spacecraft.PropagatedStates;
        Assert.Equal(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++)
        {
            LabelAssert.Equal(before[i].Epoch, after[i].Epoch);
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
        LabelAssert.Equal(tle.ToStateVector(start), solution.StateVectors.First(x => x.Epoch == start));
        LabelAssert.Equal(tle.ToStateVector(end), solution.StateVectors.First(x => x.Epoch == end));
    }

    private static readonly TLE Iss = new("ISS",
        "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

    /// <summary>
    /// The number of samples by the closed form of the grid (#598): ceil(L/ΔT) + 1, less one when the last grid point
    /// before the end lies closer to it than ΔT/10.
    /// </summary>
    private static int GridCount(double windowSeconds, double stepSeconds)
    {
        int intervals = (int)System.Math.Ceiling(windowSeconds / stepSeconds);
        if (intervals > 1 && windowSeconds - (intervals - 1) * stepSeconds < stepSeconds / 10.0) intervals--;
        return intervals + 1;
    }

    /// <summary>
    /// The samples of a <see cref="TLEPropagator"/> run and its solution, checked against the closed form of the grid
    /// (#598): sample i at start + i·ΔT for every sample but the last, the last at the window end, each the SGP4 state of
    /// its epoch (CSPICE evsgp4_c), the steps of the solution the intervals between them, the last ending at the window end.
    /// </summary>
    private static void AssertSampledAtTheGrid(int naifId, double windowSeconds, double stepSeconds, int expectedCount)
    {
        var start = Iss.Epoch.ToTDB();
        var end = start.AddSeconds(windowSeconds);
        var spacecraft = new Spacecraft(naifId, $"S{-naifId}", 100.0, 10000.0, new Clock($"c{-naifId}", 256), Iss);
        var step = TimeSpan.FromSeconds(stepSeconds);
        var solution = new TLEPropagator(new IO.Astrodynamics.TimeSystem.Window(start, end), spacecraft, step).Propagate();

        Assert.Equal(expectedCount, GridCount(windowSeconds, stepSeconds));
        Assert.Equal(expectedCount, solution.StateVectors.Count);
        for (int i = 0; i < expectedCount; i++)
        {
            var epoch = i == expectedCount - 1 ? end : start.Add(TimeSpan.FromTicks(step.Ticks * i));
            var state = solution.StateVectors[i];
            Assert.Equal(epoch, state.Epoch);
            var sgp4 = Iss.ToStateVector(epoch);
            Assert.Equal(sgp4.Position, state.Position);
            Assert.Equal(sgp4.Velocity, state.Velocity);
        }

        // The steps of the synthetic solution join the samples and end at the window end, where it reads SGP4 bit for bit
        var segment = Assert.Single(solution.Segments);
        Assert.Equal(expectedCount - 1, segment.Steps.Count);
        Assert.Equal(windowSeconds, segment.Duration, 9);
        Assert.Equal(windowSeconds - stepSeconds * (expectedCount - 2), segment.Steps[^1].StepSize, 9);
        var (endPosition, endVelocity) = solution.InterpolateAt(end);
        Assert.Equal(Iss.ToStateVector(end).Position, endPosition);
        Assert.Equal(Iss.ToStateVector(end).Velocity, endVelocity);

        // The spacecraft stores them, in ICRF, the last at the window end
        var stored = spacecraft.PropagatedStates;
        Assert.Equal(expectedCount, stored.Length);
        Assert.Equal(start, stored[0].Epoch);
        Assert.Equal(end, stored[^1].Epoch);
    }

    [Fact]
    public void SubSecondStep_SamplesEveryHalfSecond()
    {
        // #598: the sample count was (uint)L / (uint)ΔT + 1, so a step below 1 s divided by zero
        AssertSampledAtTheGrid(-1002, 60.0, 0.5, 121);
    }

    [Fact]
    public void WindowNotAMultipleOfTheStep_EndsAtTheWindowEnd()
    {
        // #598: a 90 s window at 60 s held 2 samples, the last at +60 s; the last sample is now the window end, after a
        // step of 30 s
        AssertSampledAtTheGrid(-1003, 90.0, 60.0, 3);
    }

    [Fact]
    public void FractionalStep_StaysInsideTheWindow()
    {
        // #598: a step of 1.5 s was counted as 1 s, so 11 samples ran to +15 s, past the end of a 10 s window
        AssertSampledAtTheGrid(-1004, 10.0, 1.5, 8);
    }

    [Fact]
    public void WindowAMultipleOfTheStep_KeepsItsGrid()
    {
        // The grid of the former count where it was right: 1 h at 60 s, 61 samples
        AssertSampledAtTheGrid(-1005, 3600.0, 60.0, 61);
    }

    [Fact]
    public void GridPointWithinATenthOfTheStepOfTheEnd_IsDropped()
    {
        // 3600.5 s at 60 s: the grid point +3600 s lies 0.5 s from the end, less than 6 s, so the last step runs from
        // +3540 s to the end, 60.5 s, instead of a step of 0.5 s
        AssertSampledAtTheGrid(-1008, 3600.5, 60.0, 61);
    }

    [Fact]
    public void WindowGivenInUtc_EndsAtItsTdbEnd()
    {
        // One hour of UTC from the TLE epoch is 3600 s and a few ticks of TDB: 61 samples every 60 s from the TDB start,
        // the last at the TDB end, not a 62nd a few ticks after +3600 s
        var window = new IO.Astrodynamics.TimeSystem.Window(Iss.Epoch, Iss.Epoch.AddHours(1.0));
        var start = window.StartDate.ToTDB();
        var end = window.EndDate.ToTDB();
        var spacecraft = new Spacecraft(-1009, "S1009", 100.0, 10000.0, new Clock("c1009", 256), Iss);
        var solution = new TLEPropagator(window, spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();
        _output.WriteLine($"MEASURE TLEPropagatorTests.WindowGivenInUtc_EndsAtItsTdbEnd: TDB length − 3600 s = {(end - start).Ticks - 36000000000L} ticks");
        Assert.Equal(61, solution.StateVectors.Count);
        for (int i = 0; i < 60; i++)
        {
            Assert.Equal(start.Add(TimeSpan.FromSeconds(60.0 * i)), solution.StateVectors[i].Epoch);
        }

        Assert.Equal(end, solution.StateVectors[^1].Epoch);
    }

    [Fact]
    public void LastShortStep_InterpolatesSgp4()
    {
        // Between the last two samples (+60 s and +90 s) the solution is the cubic Hermite of a 30 s step, its error that
        // of the 60 s steps of InterpolatedVelocity_FollowsSgp4 divided by about 16 in velocity and 10 in position
        // (measured on Linux x64, 2026-11-17: 4.282e-4 / 2.701e-5 m/s and 0.3762 / 3.726e-2 m; 2⁴ for an h⁴ error).
        // The reference is SGP4 at the exact instant (CSPICE evsgp4_c); before #598 the solution ended at +60 s
        var start = Iss.Epoch.ToTDB();
        var spacecraft = new Spacecraft(-1006, "S1006", 100.0, 10000.0, new Clock("c1006", 256), Iss);
        var solution = new TLEPropagator(new IO.Astrodynamics.TimeSystem.Window(start, start.AddSeconds(90.0)), spacecraft,
            TimeSpan.FromSeconds(60.0)).Propagate();
        double worstVelocity = 0.0, worstPosition = 0.0;
        for (double s = 61.0; s < 90.0; s += 1.0)
        {
            var epoch = start.AddSeconds(s);
            var sgp4 = Iss.ToStateVector(epoch);
            var (position, velocity) = solution.InterpolateAt(epoch);
            worstVelocity = System.Math.Max(worstVelocity, (velocity - sgp4.Velocity).Magnitude());
            worstPosition = System.Math.Max(worstPosition, (position - sgp4.Position).Magnitude());
        }

        _output.WriteLine($"MEASURE TLEPropagatorTests.LastShortStep_InterpolatesSgp4: velocity {worstVelocity:E3} m/s, " +
                          $"position {worstPosition:E3} m");
        Assert.InRange(worstVelocity, 0.0, LastShortStepVelocityBound);
        Assert.InRange(worstPosition, 0.0, LastShortStepPositionBound);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-600000000L)]
    public void StepNotPositive_Throws(long ticks)
    {
        // #598: a zero step divided by zero; a step of zero or less is rejected as the integrators reject it (#674)
        var start = Iss.Epoch.ToTDB();
        var spacecraft = new Spacecraft(-1007, "S1007", 100.0, 10000.0, new Clock("c1007", 256), Iss);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TLEPropagator(new IO.Astrodynamics.TimeSystem.Window(start, start.AddSeconds(60.0)), spacecraft, TimeSpan.FromTicks(ticks)));
        Assert.Equal("deltaT", ex.ParamName);
    }
}