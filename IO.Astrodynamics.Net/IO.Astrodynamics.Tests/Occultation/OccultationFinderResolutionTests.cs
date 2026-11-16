// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Occultation;

/// <summary>
/// The resolution of the search of the occultation and field-of-view windows (<c>OccultationFinder</c>, which
/// <c>FieldOfViewSearch</c> calls) on the closed-form margin of <see cref="ShortWindowMargin"/> (#668): with the step at most
/// half the time between the extremes, every window and gap at least 2 √ε h + 2τ/3 wide is found, its edges within the
/// convergence tolerance of the closed form; a narrower one can be missed, and a tangency gives no window. The docs said
/// "every window and every gap, however short"; the search is unchanged, these tests pass on the base.
/// </summary>
public class OccultationFinderResolutionTests
{
    private static readonly TimeSystem.Time Start = TimeSystem.Time.CreateTDB(762480000.0);

    // The steps: 1 min, 10 min, and a quarter of the period, the limit of the step rule (the extremes are P/2 apart)
    public static IEnumerable<object[]> Steps() => new[] { new object[] { 60.0 }, new object[] { 600.0 }, new object[] { 1350.0 } };

    private readonly ITestOutputHelper _output;

    public OccultationFinderResolutionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // The search of the windows where the margin is non-negative, in seconds from the start of the search window
    internal static IReadOnlyList<(double Start, double End)> Search(Func<double, double> margin, double step)
    {
        var window = new Window(Start, Start.AddSeconds(ShortWindowMargin.SearchLength));
        return OccultationFinder.FindWindows(t => margin((t - Start).TotalSeconds), window, TimeSpan.FromSeconds(step),
                TimeSpan.FromTicks((long)(ShortWindowMargin.Tolerance * TimeSpan.TicksPerSecond)))
            .Select(w => ((w.StartDate - Start).TotalSeconds, (w.EndDate - Start).TotalSeconds))
            .ToArray();
    }

    // The widths asserted: the stated bound and above it
    private static double[] AssertedWidths(double step)
    {
        double bound = ShortWindowMargin.ResolutionBound(step);
        return new[] { bound, 2.0 * bound, 10.0 * bound, 1.0e-3 };
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public void Windows_AtLeastTheResolutionBound_AreFound(double step)
    {
        var ladder = ShortWindowMargin.Ladder(Search, step, AssertedWidths(step), false, 25, 668);
        foreach (var row in ladder)
        {
            _output.WriteLine($"step {step} s, width {row.Width:E3} s: {row.Failed} of {row.Runs} runs missed or misplaced a window");
            Assert.True(row.Failed == 0, $"step {step} s, width {row.Width:E3} s: {row.Failed} of {row.Runs} runs");
        }

        var measured = ShortWindowMargin.Summary(ShortWindowMargin.Ladder(Search, step, ShortWindowMargin.MeasuredWidths, false, 25, 669));
        _output.WriteLine($"MEASURE #668 occultation windows, step {step} s: bound {ShortWindowMargin.ResolutionBound(step):E3} s, " +
                          $"narrowest width found every time {measured.NarrowestFound:E1} s, widest missed at least once {measured.WidestMissed:E1} s");
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public void Gaps_AtLeastTheResolutionBound_AreFound(double step)
    {
        var ladder = ShortWindowMargin.Ladder(Search, step, AssertedWidths(step), true, 25, 670);
        foreach (var row in ladder)
        {
            _output.WriteLine($"step {step} s, gap {row.Width:E3} s: {row.Failed} of {row.Runs} runs missed or misplaced a gap");
            Assert.True(row.Failed == 0, $"step {step} s, gap {row.Width:E3} s: {row.Failed} of {row.Runs} runs");
        }

        var measured = ShortWindowMargin.Summary(ShortWindowMargin.Ladder(Search, step, ShortWindowMargin.MeasuredWidths, true, 25, 671));
        _output.WriteLine($"MEASURE #668 occultation gaps, step {step} s: bound {ShortWindowMargin.ResolutionBound(step):E3} s, " +
                          $"narrowest width found every time {measured.NarrowestFound:E1} s, widest missed at least once {measured.WidestMissed:E1} s");
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public void Tangency_GivesNoWindow(double step)
    {
        // A maximum exactly at zero between the samples: the search stops only at a positive margin, so no window, a limit
        // the docs state
        var random = new Random(672);
        for (int k = 0; k < 8; k++)
        {
            Assert.Empty(Search(ShortWindowMargin.Tangency(ShortWindowMargin.RandomCentre(random)), step));
        }
    }
}
