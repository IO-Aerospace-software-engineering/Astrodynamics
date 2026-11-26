// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Occultation;

/// <summary>
/// The step of <see cref="WindowSearches.FindOccultationWindows"/> (decision 106 of #668, decided by S. Guillet,
/// 2026-11-25): inferred, and guarded at half the period, only on the library's search, for a propagated or TLE
/// spacecraft whose centre of propagation is the front body: the smallest osculating period about it at the window start
/// and after each impulse inside the window (the TLE's mean motion), over 16, capped at half the window; required
/// everywhere else (<see cref="ArgumentException"/> naming <c>Step</c>).
/// </summary>
public class OccultationStepInferenceTests
{
    // The 16 eclipses of a LEO day at period/16 and at period/2 against a 1 min step, the edges of the three searches at
    // 1 µs: twice the worst value, 2.000e-7 s, identical on Linux x64, Windows x64 and macOS arm64 (CI of #832 at 222ed4be,
    // 2026-11-26, the MEASURE line)
    private const double EdgeThreshold = 4.0e-7;

    private static readonly TimeSystem.Time Start = new(2026, 3, 20, TimeScale.TDB);
    private static readonly Window Day = new(Start, Start.AddDays(1.0));

    private readonly ITestOutputHelper _output;

    public OccultationStepInferenceTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureOccultationReference();
    }

    private static CelestialBody Earth => new(PlanetsAndMoons.EARTH);
    private static CelestialBody Sun => new(Stars.Sun);

    // An equatorial LEO at the equinox, in the Earth's shadow once per orbit, propagated about a point-mass Earth
    private static Spacecraft Leo(int naifId, double semiMajorAxis = 6800000.0, Func<Spacecraft, Engine, ImpulseManeuver> maneuver = null)
    {
        var earth = Earth;
        var orbit = new KeplerianElements(semiMajorAxis, 0.001, 0.0, 0.0, 0.0, 0.0, earth, Start, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(naifId, $"STEP{-naifId}", 1000.0, orbit, 3000.0, new Clock($"STEP{-naifId}_CLK", 65536));
        if (maneuver != null)
        {
            var tank = spacecraft.AddFuelTank(new FuelTank("ft", 1000.0, 1000.0));
            var engine = spacecraft.AddEngine(new Engine("eng", 300.0, 10.0, tank));
            spacecraft.SetManeuvers(maneuver(spacecraft, engine));
        }

        spacecraft.Propagate(Day, new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = TimeSpan.FromSeconds(60.0),
            ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None
        });
        return spacecraft;
    }

    private static TimeSpan PeriodAt(Spacecraft spacecraft, TimeSystem.Time epoch) =>
        spacecraft.GetGeometricStateRelativeTo(epoch, Earth).Period();

    private static double WorstEdge(Window[] actual, Window[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        return expected.Select((e, i) => System.Math.Max(System.Math.Abs((actual[i].StartDate - e.StartDate).TotalSeconds),
            System.Math.Abs((actual[i].EndDate - e.EndDate).TotalSeconds))).DefaultIfEmpty(0.0).Max();
    }

    [Fact]
    public void LeoDay_InferredStepAndHalfPeriod_FindTheSixteenEclipses_AboveHalfThePeriodThrows()
    {
        var leo = Leo(-106001);
        var period = PeriodAt(leo, Start);
        var options = OccultationSearchOptions.Default;
        var inferred = leo.FindOccultationWindows(Day, Sun, Earth, options).ToArray();
        // The inferred step is period/16: the search at that step, bit for bit
        LabelAssert.Equal(leo.FindOccultationWindows(Day, Sun, Earth, options with { Step = period / 16 }), inferred);

        var half = leo.FindOccultationWindows(Day, Sun, Earth, options with { Step = period / 2 }).ToArray();
        var minute = leo.FindOccultationWindows(Day, Sun, Earth, options with { Step = TimeSpan.FromMinutes(1.0) }).ToArray();
        double worst = System.Math.Max(WorstEdge(inferred, minute), WorstEdge(half, minute));
        _output.WriteLine($"MEASURE LEO day, period {period}: {minute.Length} eclipses; period/16 and period/2 against 1 min, worst edge {worst:E3} s");
        Assert.Equal(16, minute.Length);
        Assert.True(worst <= EdgeThreshold, $"{worst:E3} s");

        var above = Assert.Throws<ArgumentOutOfRangeException>(() =>
            leo.FindOccultationWindows(Day, Sun, Earth, options with { Step = period / 2 + TimeSpan.FromMinutes(2.0) }));
        Assert.Equal("options", above.ParamName);
        Assert.Contains("Step", above.Message);
    }

    [Fact]
    public void AfterAnImpulse_ThePeriodOfTheLowerOrbit_InfersAndGuards()
    {
        // From 8000 km to a perigee of 6700 km at the first apogee: the period after the impulse is the smaller one
        var lowered = Leo(-106002, 8000000.0, (_, engine) => new PerigeeHeightManeuver(6700000.0, engine, Start, TimeSpan.Zero, Earth));
        var unchanged = Leo(-106003, 8000000.0);
        var impulse = lowered.ExecutedManeuvers.OfType<ImpulseManeuver>().Single().ThrustWindow!.Value.StartDate;
        var before = PeriodAt(lowered, Start);
        var after = PeriodAt(lowered, Day.EndDate);
        Assert.True(after < before);
        var options = OccultationSearchOptions.Default;
        LabelAssert.Equal(lowered.FindOccultationWindows(Day, Sun, Earth, options with { Step = after / 16 }),
            lowered.FindOccultationWindows(Day, Sun, Earth, options));
        _output.WriteLine($"MEASURE periods {before} then {after}, impulse at {impulse}");

        // A step between the two half periods: accepted on the orbit without the impulse, refused after it
        var between = (before + after) / 4;
        Assert.NotEmpty(unchanged.FindOccultationWindows(Day, Sun, Earth, options with { Step = between }));
        Assert.Throws<ArgumentOutOfRangeException>(() => lowered.FindOccultationWindows(Day, Sun, Earth, options with { Step = between }));
    }

    [Fact]
    public void TleSpacecraft_ThePeriodOfItsMeanMotion()
    {
        var tle = new Tle("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var spacecraft = new Spacecraft(-106004, "STEP106004", 1000.0, tle, 3000.0, new Clock("STEP106004_CLK", 65536));
        var start = tle.Epoch.ToTdb();
        var window = new Window(start, start.AddHours(6.0));
        var period = TimeSystem.Time.SecondsToSpan(IO.Astrodynamics.Constants.TwoPi / tle.MeanMeanMotion);
        var options = OccultationSearchOptions.Default;
        var inferred = spacecraft.FindOccultationWindows(window, Sun, Earth, options);
        Assert.NotEmpty(inferred);
        LabelAssert.Equal(spacecraft.FindOccultationWindows(window, Sun, Earth, options with { Step = period / 16 }), inferred);
        Assert.Throws<ArgumentOutOfRangeException>(() => spacecraft.FindOccultationWindows(window, Sun, Earth, options with { Step = period / 2 + TimeSpan.FromMinutes(1.0) }));
    }

    [Fact]
    public void ShortWindow_TheStepCappedAtHalfTheWindow()
    {
        var leo = Leo(-106005);
        var options = OccultationSearchOptions.Default;
        var tenMinutes = new Window(Start.AddHours(1.0), Start.AddHours(1.0).AddMinutes(10.0));
        LabelAssert.Equal(leo.FindOccultationWindows(tenMinutes, Sun, Earth, options with { Step = TimeSpan.FromMinutes(5.0) }),
            leo.FindOccultationWindows(tenMinutes, Sun, Earth, options));
        var instant = new Window(Start.AddHours(1.0), Start.AddHours(1.0));
        LabelAssert.Equal(leo.FindOccultationWindows(instant, Sun, Earth, options with { Step = TimeSpan.FromTicks(1) }),
            leo.FindOccultationWindows(instant, Sun, Earth, options));
    }

    [Fact]
    public void WithoutAQualifyingSpacecraft_TheStepIsRequired_AndNoGuard()
    {
        var options = OccultationSearchOptions.Default;
        void AssertRequired(Action search)
        {
            var e = Assert.Throws<ArgumentException>(search);
            Assert.Equal("options", e.ParamName);
            Assert.Contains("OccultationSearchOptions.Step", e.Message);
        }

        // A kernel spacecraft (gfoclt_c), a LEO whose front body is not its centre, a site and natural bodies
        var kernelSpacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Start);
        AssertRequired(() => kernelSpacecraft.FindOccultationWindows(new Window(Start, Start.AddHours(12.0)), Sun, Earth, options));
        var leo = Leo(-106006);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        AssertRequired(() => leo.FindOccultationWindows(Day, Sun, moon, options));
        var site = new Site(65, "DSS-65", Earth);
        AssertRequired(() => site.FindOccultationWindows(Day, Sun, moon, options));
        AssertRequired(() => Earth.FindOccultationWindows(Day, Sun, moon, options));

        // No half-period guard there: a step of a day about the Moon
        leo.FindOccultationWindows(Day, Sun, moon, options with { Step = TimeSpan.FromHours(12.0) });
    }
}
