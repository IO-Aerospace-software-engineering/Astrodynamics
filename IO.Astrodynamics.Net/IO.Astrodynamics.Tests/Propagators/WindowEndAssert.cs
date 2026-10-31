// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Propagator;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// That a propagation reaches the end of its window, in the time argument of the propagation (#501): its segments span
/// the length of the window in seconds of the time argument, TT about the Earth, and its end is the end of the window.
/// </summary>
/// <remarks>
/// Before #501 the tests asserted <c>last.BaseEpoch.AddSeconds(last.Duration) == Window.EndDate</c>, which adds TDB
/// seconds: about the Earth the segments count TT seconds, a TDB-tagged 600 s window is 600 − 2e-7 s of TT, and that
/// sum falls one tick before the end label while the propagation ends at the end instant.
/// </remarks>
internal static class WindowEndAssert
{
    // The resolution of the former assertion, an equality of labels
    private const double Tick = 1e-7;

    internal static void ReachesTheEndOf(TimeSystem.Window window, PropagationSolution solution)
    {
        Assert.Equal(window.EndDate.ToTDB(), solution.End);
        Assert.InRange(IntegratedSeconds(solution) - LengthOf(window, solution), -Tick, Tick);
    }

    /// <summary>Whether <see cref="ReachesTheEndOf"/> holds, for the tests that count the propagations that hold it.</summary>
    internal static bool Reaches(TimeSystem.Window window, PropagationSolution solution) =>
        solution.End == window.EndDate.ToTDB() &&
        System.Math.Abs(IntegratedSeconds(solution) - LengthOf(window, solution)) <= Tick;

    /// <summary>The seconds of the time argument from the start of the solution to the end of its last segment.</summary>
    internal static double IntegratedSeconds(PropagationSolution solution)
    {
        var first = solution.Segments[0];
        var last = solution.Segments[^1];
        return solution.TimeArgument.SecondsBetween(first.BaseEpoch, last.BaseEpoch) + last.Duration;
    }

    /// <summary>The length of <paramref name="window"/> in seconds of the time argument of the solution.</summary>
    internal static double LengthOf(TimeSystem.Window window, PropagationSolution solution) =>
        solution.TimeArgument.Length(window, new TimeSystem.Window(window.StartDate.ToTDB(), window.EndDate.ToTDB()));
}
