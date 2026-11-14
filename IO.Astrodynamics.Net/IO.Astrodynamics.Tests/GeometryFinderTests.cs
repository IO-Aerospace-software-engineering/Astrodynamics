// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The managed window search (#590, #461, #486, #487) on closed forms, whose windows are known exactly, and against the
/// CSPICE windows of <c>tools/geometry_reference</c> (<c>gfdist_c</c> and <c>gfposc_c</c> at 1e-9 s).
/// </summary>
public class GeometryFinderTests
{
    // The convergence tolerance of the search, 1 µs, and half the 100 ns of a Time
    private const double RootTolerance = 1.0e-6 + 0.5e-7;


    private static readonly TimeSystem.Time Start = TimeSystem.Time.CreateTDB(762480000.0);
    private static readonly Window SearchWindow = new(Start, Start.AddSeconds(600.0));
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);

    private readonly ITestOutputHelper _output;

    public GeometryFinderTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }


    private static double Seconds(TimeSystem.Time epoch) => (epoch - Start).TotalSeconds;

    private static Func<TimeSystem.Time, double> Of(Func<double, double> f) => t => f(Seconds(t));

    private static Window[] Find(Func<double, double> f, RelationnalOperator relation, double value, double adjust = 0.0) =>
        new GeometryFinder().FindWindowsWithCondition(SearchWindow, Of(f), relation, value, adjust, Step).ToArray();

    private static void AssertWindows(IReadOnlyList<(double Start, double End)> expected, IReadOnlyList<Window> actual, double tolerance)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.InRange(Seconds(actual[i].StartDate) - expected[i].Start, -tolerance, tolerance);
            Assert.InRange(Seconds(actual[i].EndDate) - expected[i].End, -tolerance, tolerance);
        }
    }

    [Fact]
    public void Greater_RootNotTheFailingSide()
    {
        // #590: the 1 s bisection returned [30, 70.078125] s
        var windows = Find(t => System.Math.Min(t - 30.3, 70.6 - t), RelationnalOperator.Greater, 0.0);
        AssertWindows([(30.3, 70.6)], windows, RootTolerance);
    }

    [Fact]
    public void Greater_HoldsAtTheEnd()
    {
        // #461: the last edge stopped at 599.443 s
        var windows = Find(t => t - 30.3, RelationnalOperator.Greater, 0.0);
        AssertWindows([(30.3, 600.0)], windows, RootTolerance);
        Assert.Equal(SearchWindow.EndDate, windows[0].EndDate);
    }

    [Fact]
    public void AlwaysTrue_IsTheSearchWindow_NothingReadOutside()
    {
        // #461: [-60, 599.355] s, with 6 evaluations before the start
        var read = new List<TimeSystem.Time>();
        var windows = new GeometryFinder().FindWindowsWithCondition(SearchWindow, t =>
        {
            read.Add(t);
            return 1.0;
        }, RelationnalOperator.Greater, 0.0, Step).ToArray();
        Assert.Equal([SearchWindow], windows);
        Assert.All(read, t => Assert.InRange(t, SearchWindow.StartDate, SearchWindow.EndDate));
    }

    [Fact]
    public void Lower()
    {
        var windows = Find(t => (t - 300.0) * (t - 300.0), RelationnalOperator.Lower, 100.0 * 100.0);
        AssertWindows([(200.0, 400.0)], windows, RootTolerance);
    }

    [Fact]
    public void GreaterOrEqualAndLowerOrEqual()
    {
        AssertWindows([(30.3, 600.0)], Find(t => t - 30.3, RelationnalOperator.GreaterOrEqual, 0.0), RootTolerance);
        AssertWindows([(0.0, 30.3)], Find(t => t - 30.3, RelationnalOperator.LowerOrEqual, 0.0), RootTolerance);
        AssertWindows([(30.3, 70.6)], Find(t => System.Math.Min(t - 30.3, 70.6 - t), RelationnalOperator.GreaterOrEqual, 0.0),
            RootTolerance);
    }

    [Fact]
    public void Lower_HoldsAtTheStart()
    {
        var windows = Find(t => t - 30.3, RelationnalOperator.Lower, 0.0);
        AssertWindows([(0.0, 30.3)], windows, RootTolerance);
        Assert.Equal(SearchWindow.StartDate, windows[0].StartDate);
    }

    [Fact]
    public void NeverTrue_NoWindow()
    {
        Assert.Empty(Find(t => -1.0, RelationnalOperator.Greater, 0.0));
    }

    [Fact]
    public void Equal_AndNotEqual()
    {
        // #487: no window. sin(2π t / 200) = 0.5 at t = 200 (k + 1/12) and 200 (k + 5/12)
        Func<double, double> f = t => System.Math.Sin(2.0 * System.Math.PI * t / 200.0);
        var crossings = new[] { 1.0 / 12.0, 5.0 / 12.0, 13.0 / 12.0, 17.0 / 12.0, 25.0 / 12.0, 29.0 / 12.0 }.Select(k => 200.0 * k).ToArray();
        AssertWindows(crossings.Select(c => (c, c)).ToArray(), Find(f, RelationnalOperator.Equal, 0.5), RootTolerance);

        var gaps = new List<(double, double)>();
        double previous = 0.0;
        foreach (var c in crossings)
        {
            gaps.Add((previous, c));
            previous = c;
        }

        gaps.Add((previous, 600.0));
        AssertWindows(gaps, Find(f, RelationnalOperator.NotEqual, 0.5), RootTolerance);
    }

    [Fact]
    public void Equal_ConstantAtTheValue_IsTheSearchWindow()
    {
        AssertWindows([(0.0, 600.0)], Find(t => 2.0, RelationnalOperator.Equal, 2.0), 0.0);
        Assert.Empty(Find(t => 2.0, RelationnalOperator.NotEqual, 2.0));
    }

    [Fact]
    public void Equal_AtASample()
    {
        // The crossing falls on the sample at 120 s exactly
        AssertWindows([(120.0, 120.0)], Find(t => t - 120.0, RelationnalOperator.Equal, 0.0), 0.0);
    }

    [Fact]
    public void BranchCut()
    {
        // A right ascension: 2π frac(t / 200), which jumps from 2π to 0 every 200 s
        Func<double, double> ra = t => 2.0 * System.Math.PI * (t / 200.0 - System.Math.Floor(t / 200.0));
        double crossing = 200.0 * 3.0 / (2.0 * System.Math.PI);

        // A threshold window ends at the jump
        var greater = Find(ra, RelationnalOperator.Greater, 3.0);
        AssertWindows(Enumerable.Range(0, 3).Select(k => (200.0 * k + crossing, 200.0 * (k + 1))).ToArray(), greater, RootTolerance);

        // The jumps are no crossing, nor extremes
        var equal = Find(ra, RelationnalOperator.Equal, 3.0);
        AssertWindows(Enumerable.Range(0, 3).Select(k => (200.0 * k + crossing, 200.0 * k + crossing)).ToArray(), equal, RootTolerance);
        Assert.Empty(Find(ra, RelationnalOperator.LocalMax, 0.0));
        Assert.Empty(Find(ra, RelationnalOperator.LocalMin, 0.0));
    }

    [Fact]
    public void LocalExtremes()
    {
        // #486: an exception. f = exp(-t / 1000) cos(ω t), ω = 2π / 250: f' = 0 where ω t = k π - δ, δ = atan(1 / (1000 ω));
        // the maxima at even k, the minima at odd k; the maximum at the start is a bound, no local maximum
        double omega = 2.0 * System.Math.PI / 250.0;
        double delta = System.Math.Atan(1.0 / (1000.0 * omega));
        Func<double, double> f = t => System.Math.Exp(-t / 1000.0) * System.Math.Cos(omega * t);
        double At(int k) => (k * System.Math.PI - delta) / omega;

        var maxima = Find(f, RelationnalOperator.LocalMax, 0.0);
        var minima = Find(f, RelationnalOperator.LocalMin, 0.0);
        LogDeltas("damped cosine maxima", maxima, [At(2), At(4)]);
        LogDeltas("damped cosine minima", minima, [At(1), At(3)]);
        AssertWindows([(At(2), At(2)), (At(4), At(4))], maxima, RootTolerance);
        AssertWindows([(At(1), At(1)), (At(3), At(3))], minima, RootTolerance);

        // The absolute maximum is at the start (a bound), the absolute minimum the first local minimum
        AssertWindows([(0.0, 0.0)], Find(f, RelationnalOperator.AbsoluteMax, 0.0), 0.0);
        AssertWindows([(At(1), At(1))], Find(f, RelationnalOperator.AbsoluteMin, 0.0), RootTolerance);
    }

    [Fact]
    public void Extremes_AtTheBounds()
    {
        // A decreasing quantity: no local extreme (CSPICE's gfdist neither), its absolute extremes at the bounds
        Func<double, double> f = t => -t;
        Assert.Empty(Find(f, RelationnalOperator.LocalMin, 0.0));
        Assert.Empty(Find(f, RelationnalOperator.LocalMax, 0.0));
        var minimum = Find(f, RelationnalOperator.AbsoluteMin, 0.0);
        Assert.Equal([new Window(SearchWindow.EndDate, SearchWindow.EndDate)], minimum);
        var maximum = Find(f, RelationnalOperator.AbsoluteMax, 0.0);
        Assert.Equal([new Window(SearchWindow.StartDate, SearchWindow.StartDate)], maximum);

        // With an adjustment, the windows within it of the extreme
        AssertWindows([(550.0, 600.0)], Find(f, RelationnalOperator.AbsoluteMin, 0.0, 50.0), RootTolerance);
        AssertWindows([(0.0, 50.0)], Find(f, RelationnalOperator.AbsoluteMax, 0.0, 50.0), RootTolerance);
    }

    [Fact]
    public void AbsoluteMin_WithAdjustment()
    {
        // (t - 301)^2 within 100 of its minimum 0 at 301 s: [291, 311] s, between the samples at 240, 300 and 360 s
        Func<double, double> f = t => (t - 301.0) * (t - 301.0);
        AssertWindows([(301.0, 301.0)], Find(f, RelationnalOperator.LocalMin, 0.0), RootTolerance);
        AssertWindows([(301.0, 301.0)], Find(f, RelationnalOperator.AbsoluteMin, 0.0), RootTolerance);
        AssertWindows([(291.0, 311.0)], Find(f, RelationnalOperator.AbsoluteMin, 0.0, 100.0), RootTolerance);
    }

    [Fact]
    public void Boolean_EqualAndNotEqual()
    {
        Func<TimeSystem.Time, bool> inside = t => Seconds(t) >= 30.3 && Seconds(t) <= 70.6;
        var finder = new GeometryFinder();
        AssertWindows([(30.3, 70.6)], finder.FindWindowsWithCondition(SearchWindow, inside, RelationnalOperator.Equal, true, Step).ToArray(),
            RootTolerance);
        AssertWindows([(0.0, 30.3), (70.6, 600.0)],
            finder.FindWindowsWithCondition(SearchWindow, inside, RelationnalOperator.NotEqual, true, Step).ToArray(), RootTolerance);
        Assert.Throws<ArgumentException>(() => finder.FindWindowsWithCondition(SearchWindow, inside, RelationnalOperator.Greater, true, Step));
    }

    [Fact]
    public void WindowsInTheFrameOfTheStart()
    {
        var start = new TimeSystem.Time(2024, 2, 29, 12, 0, 0, frame: TimeFrame.UTCFrame);
        var window = new Window(start, start.AddSeconds(600.0));
        var windows = new GeometryFinder().FindWindowsWithCondition(window, t => (t - start).TotalSeconds - 30.3, RelationnalOperator.Greater, 0.0,
            Step).ToArray();
        Assert.Single(windows);
        Assert.Equal(TimeFrame.UTCFrame, windows[0].StartDate.Frame);
        Assert.InRange((windows[0].StartDate - start).TotalSeconds - 30.3, -RootTolerance, RootTolerance);
        Assert.Equal(window.EndDate, windows[0].EndDate);
    }

    [Fact]
    public void ZeroLengthSearchWindow()
    {
        var window = new Window(Start, Start);
        var finder = new GeometryFinder();
        Assert.Equal([window], finder.FindWindowsWithCondition(window, t => 1.0, RelationnalOperator.Greater, 0.0, Step));
        Assert.Empty(finder.FindWindowsWithCondition(window, t => 1.0, RelationnalOperator.LocalMax, 0.0, Step));
        Assert.Equal([window], finder.FindWindowsWithCondition(window, t => 1.0, RelationnalOperator.AbsoluteMax, 0.0, Step));
    }

    [Fact]
    public void InvalidArguments()
    {
        var finder = new GeometryFinder();
        Assert.Throws<ArgumentOutOfRangeException>(() => finder.FindWindowsWithCondition(SearchWindow, t => 1.0, RelationnalOperator.Greater, 0.0,
            TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => finder.FindWindowsWithCondition(SearchWindow, t => 1.0, RelationnalOperator.AbsoluteMin, 0.0,
            -1.0, Step));
        Assert.Throws<ArgumentException>(() => finder.FindWindowsWithCondition(SearchWindow, t => 1, RelationnalOperator.Greater, 0, Step));
        Assert.Throws<ArgumentNullException>(() => finder.FindWindowsWithCondition<double>(SearchWindow, null, RelationnalOperator.Greater, 0.0, Step));
    }

    [Fact]
    public void UndefinedQuantity_IsNoWindow()
    {
        // NaN over [60, 61) s: the transitions about it are bisected on the relation
        var windows = Find(t => t is >= 60.0 and < 61.0 ? double.NaN : 1.0, RelationnalOperator.Greater, 0.0);
        AssertWindows([(0.0, 60.0), (61.0, 600.0)], windows, RootTolerance);
    }

    [Fact]
    public void Tangency_AtASample_IsOneClosedWindow()
    {
        // (t - 120)^2 > 0 everywhere but at the sample at 120 s: the two windows it closes and opens are one
        AssertWindows([(0.0, 600.0)], Find(t => (t - 120.0) * (t - 120.0), RelationnalOperator.Greater, 0.0), 0.0);
    }

    [Fact]
    public void Equal_InAShortLastInterval()
    {
        // The last interval, 50 µs long, holds the crossing
        var window = new Window(Start, Start.AddTicks(6000000500L));
        var windows = new GeometryFinder().FindWindowsWithCondition(window, Of(t => t - 600.00002), RelationnalOperator.Equal, 0.0, Step).ToArray();
        AssertWindows([(600.00002, 600.00002)], windows, RootTolerance);
    }

    [Fact]
    public void EvaluateCondition()
    {
        var finder = new GeometryFinder();
        Assert.True(finder.EvaluateCondition(2.0, 1.0, RelationnalOperator.Greater));
        Assert.True(finder.EvaluateCondition(1.0, 1.0, RelationnalOperator.GreaterOrEqual));
        Assert.True(finder.EvaluateCondition(0.0, 1.0, RelationnalOperator.Lower));
        Assert.True(finder.EvaluateCondition(1.0, 1.0, RelationnalOperator.LowerOrEqual));
        Assert.True(finder.EvaluateCondition(1.0, 1.0 + 1e-10, RelationnalOperator.Equal));
        Assert.True(finder.EvaluateCondition(1.0, 2.0, RelationnalOperator.NotEqual));
        Assert.True(finder.EvaluateCondition(true, true, RelationnalOperator.Equal));
        Assert.True(finder.EvaluateCondition(true, false, RelationnalOperator.NotEqual));
        Assert.Throws<ArgumentException>(() => finder.EvaluateCondition(1.0, 1.0, RelationnalOperator.LocalMin));
        Assert.Throws<ArgumentException>(() => finder.EvaluateCondition(true, true, RelationnalOperator.Greater));
        Assert.Throws<ArgumentException>(() => finder.EvaluateCondition(1, 1, RelationnalOperator.Greater));
        Assert.Throws<ArgumentException>(() => Find(t => t, (RelationnalOperator)99, 0.0));
    }

    [Fact]
    public void EarthMoonDistance_AgainstGfdist()
    {
                var window = new Window(TimeSystem.Time.J2000TDB, new TimeSystem.Time("2000-04-01 12:00:00.0000 TDB"));
        var step = TimeSpan.FromDays(1.0);
        var moon = TestHelpers.MoonAtJ2000;
        var earth = TestHelpers.EarthAtJ2000;
        IReadOnlyList<Window> Distance(RelationnalOperator relation, double value = 0.0) =>
            moon.FindWindowsOnDistanceConstraint(window, earth, relation, value, Aberration.None, step).ToArray();

        GeometryReference.AssertWindows(_output, "earth_moon_distance LOCMIN", Distance(RelationnalOperator.LocalMin), GeometryReference.ThresholdDistanceExtreme);
        GeometryReference.AssertWindows(_output, "earth_moon_distance LOCMAX", Distance(RelationnalOperator.LocalMax), GeometryReference.ThresholdDistanceExtreme);
        GeometryReference.AssertWindows(_output, "earth_moon_distance ABSMIN", Distance(RelationnalOperator.AbsoluteMin), GeometryReference.ThresholdDistanceExtreme);
        GeometryReference.AssertWindows(_output, "earth_moon_distance ABSMAX", Distance(RelationnalOperator.AbsoluteMax), GeometryReference.ThresholdDistanceExtreme);

        // The adjustment, which the distance search does not take, on the same quantity
        Func<TimeSystem.Time, double> distance = t => moon.GetEphemeris(t, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position.Magnitude();
        GeometryReference.AssertWindows(_output, "earth_moon_distance ABSMIN adjust 1000 km",
            new GeometryFinder().FindWindowsWithCondition(window, distance, RelationnalOperator.AbsoluteMin, 0.0, 1000000.0, step).ToArray(),
            GeometryReference.ThresholdCrossing);
    }

    [Fact]
    public void MoonFromSite_AgainstGfposc()
    {
                Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
        var window = new Window(TimeSystem.Time.J2000TDB, new TimeSystem.Time("2000-01-02 12:00:00.0000 TDB"));
        var step = TimeSpan.FromSeconds(60.0);
        var moon = TestHelpers.MoonAtJ2000;
        IReadOnlyList<Window> Search(CoordinateSystem system, Coordinate coordinate, RelationnalOperator relation) =>
            moon.FindWindowsOnCoordinateConstraint(window, site, site.Frame, system, coordinate, relation, 0.0, 0.0, Aberration.None, step).ToArray();

        GeometryReference.AssertWindows(_output, "dss13_moon_z = 0", Search(CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Equal), GeometryReference.ThresholdCrossing);
        GeometryReference.AssertWindows(_output, "dss13_moon_elevation LOCMAX", Search(CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.LocalMax),
            GeometryReference.ThresholdElevationExtreme);
        GeometryReference.AssertWindows(_output, "dss13_moon_elevation LOCMIN", Search(CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.LocalMin),
            GeometryReference.ThresholdElevationExtreme);
    }

    // #678: sin(2π t / 5400 s) over a day, whose extremes are 2700 s apart; it exceeds 0.995 (and falls below -0.995) 16
    // times, for 171.959 s each, from (5400 s / 2π) asin 0.995 = 1264.020 s after the start of each period (2700 s after it)
    private const double Period = 5400.0;
    private static readonly Window Day = new(Start, Start.AddSeconds(86400.0));
    private static readonly double Rise = Period / (2.0 * System.Math.PI) * System.Math.Asin(0.995);
    private static double Sine(double t) => System.Math.Sin(2.0 * System.Math.PI * t / Period);

    private static Window[] FindOverTheDay(Func<double, double> f, RelationnalOperator relation, double value, double stepMinutes, double adjust = 0.0) =>
        new GeometryFinder().FindWindowsWithCondition(Day, Of(f), relation, value, adjust, TimeSpan.FromMinutes(stepMinutes)).ToArray();

    private void AssertClosedForm(string label, IReadOnlyList<(double Start, double End)> expected, IReadOnlyList<Window> actual)
    {
        _output.WriteLine($"{label}: {actual.Count} windows, {expected.Count} expected");
        Assert.Equal(expected.Count, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < expected.Count; i++)
        {
            worst = System.Math.Max(worst, System.Math.Abs(Seconds(actual[i].StartDate) - expected[i].Start));
            worst = System.Math.Max(worst, System.Math.Abs(Seconds(actual[i].EndDate) - expected[i].End));
        }

        _output.WriteLine($"{label}: worst |search - closed form| {worst:E3} s");
        AssertWindows(expected, actual, RootTolerance);
    }

    [Theory]
    [InlineData(RelationnalOperator.Greater, 0.995, 3.0)]
    [InlineData(RelationnalOperator.Greater, 0.995, 10.0)]
    [InlineData(RelationnalOperator.Greater, 0.995, 22.0)]
    [InlineData(RelationnalOperator.GreaterOrEqual, 0.995, 3.0)]
    [InlineData(RelationnalOperator.GreaterOrEqual, 0.995, 10.0)]
    [InlineData(RelationnalOperator.GreaterOrEqual, 0.995, 22.0)]
    [InlineData(RelationnalOperator.Lower, -0.995, 3.0)]
    [InlineData(RelationnalOperator.Lower, -0.995, 10.0)]
    [InlineData(RelationnalOperator.Lower, -0.995, 22.0)]
    [InlineData(RelationnalOperator.LowerOrEqual, -0.995, 3.0)]
    [InlineData(RelationnalOperator.LowerOrEqual, -0.995, 10.0)]
    [InlineData(RelationnalOperator.LowerOrEqual, -0.995, 22.0)]
    public void Threshold_WindowsShorterThanTheStep_FoundUpToHalfTheTimeBetweenExtremes(RelationnalOperator relation, double value, double stepMinutes)
    {
        // #678: no window at 3 and 10 min, 2 at 22 min; a step of 22 min is less than half the 2700 s between extremes.
        // The windows of the maxima, or of the minima half a period later
        double shift = value > 0.0 ? 0.0 : 0.5 * Period;
        var expected = Enumerable.Range(0, 16).Select(k => (k * Period + shift + Rise, k * Period + shift + 0.5 * Period - Rise)).ToArray();
        AssertClosedForm($"{relation} {value} at {stepMinutes} min", expected, FindOverTheDay(Sine, relation, value, stepMinutes));
    }

    [Fact]
    public void Equal_CrossingsCloserThanTheStep_AndNotEqual()
    {
        // #678: at a 10 min step, Equal gave none of the 32 crossings and NotEqual the whole day
        var crossings = Enumerable.Range(0, 16).SelectMany(k => new[] { k * Period + Rise, k * Period + 0.5 * Period - Rise }).ToArray();
        AssertClosedForm("Equal 0.995 at 10 min", crossings.Select(c => (c, c)).ToArray(), FindOverTheDay(Sine, RelationnalOperator.Equal, 0.995, 10.0));

        var gaps = new List<(double, double)>();
        double previous = 0.0;
        foreach (var c in crossings)
        {
            gaps.Add((previous, c));
            previous = c;
        }

        gaps.Add((previous, 86400.0));
        AssertClosedForm("NotEqual 0.995 at 10 min", gaps, FindOverTheDay(Sine, RelationnalOperator.NotEqual, 0.995, 10.0));
    }

    private static (double Start, double End)[] AroundGaps(double first, double halfWidth)
    {
        // The windows of the day outside 16 gaps, centred every period from the first
        var windows = new List<(double Start, double End)>();
        double previous = 0.0;
        for (int k = 0; k < 16; k++)
        {
            double centre = first + k * Period;
            windows.Add((previous, centre - halfWidth));
            previous = centre + halfWidth;
        }

        windows.Add((previous, 86400.0));
        return windows.ToArray();
    }

    [Fact]
    public void Threshold_GapShorterThanTheStep_Found()
    {
        // #678: one window over the whole day. sin > -0.995 everywhere but 171.959 s about each minimum, at 4050 s + k 5400 s
        AssertClosedForm("Greater -0.995 at 10 min", AroundGaps(0.75 * Period, 0.25 * Period - Rise),
            FindOverTheDay(Sine, RelationnalOperator.Greater, -0.995, 10.0));
    }

    [Fact]
    public void AbsoluteMin_Adjust_GapShorterThanTheStep()
    {
        // #678: one window over the whole day. -cos(2π t / 5400 s) has its absolute minimum -1 at the start; within 1.995 of
        // it, below 0.995, everywhere but 171.959 s about each maximum, at 2700 s + k 5400 s
        Func<double, double> f = t => -System.Math.Cos(2.0 * System.Math.PI * t / Period);
        AssertClosedForm("AbsoluteMin adjust 1.995 at 10 min", AroundGaps(0.5 * Period, 0.25 * Period - Rise),
            FindOverTheDay(f, RelationnalOperator.AbsoluteMin, 0.0, 10.0, 1.995));
    }

    [Fact]
    public void Greater_HiddenWindowFromASampleAtTheValue()
    {
        // #678: 1/64 - ((t - 640 s) / 320 s)^2 > 0 over (600, 680) s; at a 120 s step the samples at 480 and 720 s are negative
        // and the one at 600 s exactly zero, which fails the strict relation: no window before
        var window = new Window(Start, Start.AddSeconds(1200.0));
        Func<double, double> f = t => 1.0 / 64.0 - (t - 640.0) / 320.0 * ((t - 640.0) / 320.0);
        Assert.Equal(0.0, f(600.0));
        var windows = new GeometryFinder().FindWindowsWithCondition(window, Of(f), RelationnalOperator.Greater, 0.0, TimeSpan.FromSeconds(120.0)).ToArray();
        AssertClosedForm("parabola Greater 0", [(600.0, 680.0)], windows);
    }

    [Theory]
    [InlineData(7.0)]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void BranchCut_ShortWindowBeforeTheJump(double stepMinutes)
    {
        // #678: no window. A right ascension (2π (t + 1000 s) / 5400 s) mod 2π jumps from 2π to 0 at 4400 s + k 5400 s, between
        // the samples; it exceeds 2π - 0.01 over the 0.01 (5400 s) / 2π = 8.594 s before each jump, and the window ends there
        Func<double, double> ra = t =>
        {
            double turns = (t + 1000.0) / Period;
            return 2.0 * System.Math.PI * (turns - System.Math.Floor(turns));
        };
        double width = 0.01 * Period / (2.0 * System.Math.PI);
        var expected = Enumerable.Range(0, 16).Select(k => (4400.0 + k * Period - width, 4400.0 + k * Period)).ToArray();
        AssertClosedForm($"sawtooth at {stepMinutes} min", expected, FindOverTheDay(ra, RelationnalOperator.Greater, 2.0 * System.Math.PI - 0.01, stepMinutes));
    }

    [Fact]
    public void UndefinedQuantity_InsideAFailingStretch_IsNoWindow()
    {
        // An undefined quantity holds no relation: NaN over [90, 91) s inside a stretch where it fails
        Assert.Empty(Find(t => t is >= 90.0 and < 91.0 ? double.NaN : -1.0, RelationnalOperator.Greater, 0.0));

        // Every sample undefined, in a search window of one interval: no extreme is searched between them
        var window = new Window(Start, Start.AddSeconds(30.0));
        var finder = new GeometryFinder();
        Assert.Empty(finder.FindWindowsWithCondition(window, t => double.NaN, RelationnalOperator.Greater, 0.0, Step));
        Assert.Empty(finder.FindWindowsWithCondition(window, t => double.NaN, RelationnalOperator.Lower, 0.0, Step));
    }

    [Theory]
    [InlineData(RelationnalOperator.Greater)]
    [InlineData(RelationnalOperator.GreaterOrEqual)]
    [InlineData(RelationnalOperator.Equal)]
    public void UndefinedSample_NextToAHiddenExtreme_IsNotSearched(RelationnalOperator relation)
    {
        // A search window of one interval, the quantity undefined at its start and 1 - ((t - 15 s) / 5 s)^2 elsewhere: the
        // samples are NaN and -8, the hidden maximum 1 at 15 s. A bracket with an undefined sample is not searched, so no
        // window comes from it, whatever the quantity does inside (the interior and edge brackets are guarded by their
        // pattern, which a NaN sample fails; this one is not)
        var window = new Window(Start, Start.AddSeconds(30.0));
        Func<double, double> f = t => t <= 0.0 ? double.NaN : 1.0 - (t - 15.0) / 5.0 * ((t - 15.0) / 5.0);
        Assert.True(double.IsNaN(f(0.0)));
        Assert.Equal(-8.0, f(30.0));
        Assert.Empty(new GeometryFinder().FindWindowsWithCondition(window, Of(f), relation, 0.0, Step));
    }

    private void LogDeltas(string label, IReadOnlyList<Window> actual, double[] expected)
    {
        for (int i = 0; i < System.Math.Min(actual.Count, expected.Length); i++)
        {
            _output.WriteLine($"{label}[{i}]: {Seconds(actual[i].StartDate) - expected[i]:E3} s");
        }
    }
}

/// <summary>
/// The CSPICE windows of <c>tools/geometry_reference</c> (<c>geometry_reference.json</c>, <c>gfdist_c</c> and
/// <c>gfposc_c</c> at 1e-9 s), and their comparison with the managed search.
/// </summary>
internal static class GeometryReference
{
    // Twice the worst value over Linux x64, Windows x64 and macOS arm64 (the same on the three, CI of #596 at cde5d3f5,
    // 2026-11-07), the managed search at 1 µs against CSPICE at 1e-9 s. Crossings and threshold edges: 2.38e-7 s (the Moon from
    // DSS-13 beyond 400 000 km); the Earth-Moon distance extremes at a 1-day step: 4.81e-7 s (LOCMIN); the Moon's
    // elevation extremes from DSS-13 at a 60 s step: 4.51e-9 s (LOCMAX)
    internal const double ThresholdCrossing = 4.8e-7;
    internal const double ThresholdDistanceExtreme = 1.0e-6;
    internal const double ThresholdElevationExtreme = 1.0e-8;

    private static readonly Lazy<Dictionary<string, double[][]>> Windows = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, double[][]>>(File.ReadAllText("Data/GeometryReference/geometry_reference.json")));

    /// <summary>The worst |managed − CSPICE| of the edges, s, after checking the count of windows.</summary>
    internal static double WorstDelta(string label, IReadOnlyList<Window> actual)
    {
        var expected = Windows.Value[label];
        Assert.Equal(expected.Length, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < expected.Length; i++)
        {
            double ds = actual[i].StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - expected[i][0];
            double de = actual[i].EndDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - expected[i][1];
            worst = System.Math.Max(worst, System.Math.Max(System.Math.Abs(ds), System.Math.Abs(de)));
        }

        return worst;
    }

    /// <summary>The windows of the search against those of CSPICE, every edge within the threshold.</summary>
    internal static void AssertWindows(ITestOutputHelper output, string label, IEnumerable<Window> actual, double threshold)
    {
        double worst = WorstDelta(label, actual as IReadOnlyList<Window> ?? actual.ToArray());
        output?.WriteLine($"{label}: worst |managed - CSPICE| {worst:E3} s");
        Assert.True(worst <= threshold, $"{label}: {worst:E3} s");
    }
}
