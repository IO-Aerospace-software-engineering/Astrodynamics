// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.Tests.Occultation;

/// <summary>
/// A closed-form margin whose windows and gaps of any width are known exactly, for the resolution of the searches that
/// look for the extremes the samples hide (the occultation and field-of-view searches, and the threshold searches of
/// <see cref="GeometryFinder"/>, #668): m(t) = 2 sin²(π W / 2P) − 2 sin²(π (t − t0) / P), non-negative exactly on
/// |t − t0 − kP| ≤ W / 2, with one maximum and one minimum per period P, P / 2 apart. Its negation has gaps of width W at
/// the same epochs. Written so that its rounding resolves widths far below the 100 ns of a <c>Time</c>.
/// </summary>
internal static class ShortWindowMargin
{
    /// <summary>The period of the margin, s: a LEO's, its extremes 2700 s apart.</summary>
    internal const double Period = 5400.0;

    /// <summary>The length of a search window, s: eight periods.</summary>
    internal const double SearchLength = 8.0 * Period;

    /// <summary>The convergence tolerance of the searches, s: 1 µs, CSPICE's <c>SPICE_GF_CNVTOL</c>.</summary>
    internal const double Tolerance = 1.0e-6;

    /// <summary>The tolerance of an edge: the convergence tolerance and half the 100 ns of a <c>Time</c>.</summary>
    internal const double EdgeTolerance = Tolerance + 0.5e-7;

    /// <summary>
    /// The width below which a window or a gap can be missed, s: 2 √ε h + 2τ/3, the final bracket of Brent's
    /// <c>localmin</c> (its abscissa tolerance √ε |x| + τ/3, |x| up to the step h from the middle of a bracket of two
    /// steps; Brent 1973, as <c>Math/Brent.cs</c> implements it), which stops only at a strictly positive margin.
    /// </summary>
    internal static double ResolutionBound(double step, double tolerance = Tolerance) =>
        2.0 * System.Math.Sqrt(MachineEpsilon) * step + 2.0 * tolerance / 3.0;

    // The machine epsilon of a double, 2^-52
    private const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>The margin of windows of width <paramref name="width"/> centred on t0 + kP.</summary>
    internal static Func<double, double> Windows(double t0, double width)
    {
        double half = System.Math.Sin(System.Math.PI * width / (2.0 * Period));
        double c = 2.0 * half * half;
        return t =>
        {
            double s = System.Math.Sin(System.Math.PI * (t - t0) / Period);
            return c - 2.0 * s * s;
        };
    }

    /// <summary>The negated margin: gaps of width <paramref name="width"/> centred on t0 + kP.</summary>
    internal static Func<double, double> Gaps(double t0, double width)
    {
        var windows = Windows(t0, width);
        return t => -windows(t);
    }

    /// <summary>The margin of a tangency: a maximum exactly at zero at t0 + kP, so no window.</summary>
    internal static Func<double, double> Tangency(double t0) => t =>
    {
        double s = System.Math.Sin(System.Math.PI * (t - t0) / Period);
        return -2.0 * s * s;
    };

    /// <summary>The centres t0 + kP inside the search window, t0 between 3P and 4P.</summary>
    internal static double[] Centres(double t0) =>
        Enumerable.Range(-3, 8).Select(k => t0 + k * Period).Where(x => x > 0.0 && x < SearchLength).ToArray();

    /// <summary>The exact windows of <see cref="Windows"/>, s from the start of the search window.</summary>
    internal static List<(double Start, double End)> ExpectedWindows(double t0, double width) =>
        Centres(t0).Select(x => (x - 0.5 * width, x + 0.5 * width)).ToList();

    /// <summary>The exact windows of <see cref="Gaps"/>: the search window less the gaps.</summary>
    internal static List<(double Start, double End)> ExpectedGapWindows(double t0, double width)
    {
        var expected = new List<(double Start, double End)>();
        double start = 0.0;
        foreach (var x in Centres(t0))
        {
            expected.Add((start, x - 0.5 * width));
            start = x + 0.5 * width;
        }

        expected.Add((start, SearchLength));
        return expected;
    }

    /// <summary>A centre t0 between 3P and 4P, away from the samples' grid by nothing in particular.</summary>
    internal static double RandomCentre(Random random) => 3.0 * Period + (0.001 + 0.998 * random.NextDouble()) * Period;

    /// <summary>Whether the windows found are the expected ones, each edge within <see cref="EdgeTolerance"/>.</summary>
    internal static bool Match(IReadOnlyList<(double Start, double End)> expected, IReadOnlyList<(double Start, double End)> found)
    {
        if (expected.Count != found.Count)
        {
            return false;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            if (System.Math.Abs(found[i].Start - expected[i].Start) > EdgeTolerance || System.Math.Abs(found[i].End - expected[i].End) > EdgeTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The windows and gaps a search misses over a ladder of widths: for each width, <paramref name="runs"/> searches of
    /// random centres (a fixed seed), the windows (or gaps) counted as missed when the search does not give the expected
    /// list; returns, per width, the number of runs that failed and the number of runs.
    /// </summary>
    internal static IReadOnlyList<(double Width, int Failed, int Runs)> Ladder(
        Func<Func<double, double>, double, IReadOnlyList<(double Start, double End)>> search, double step,
        IEnumerable<double> widths, bool gaps, int runs, int seed)
    {
        var random = new Random(seed);
        var result = new List<(double Width, int Failed, int Runs)>();
        foreach (var width in widths)
        {
            int failed = 0;
            for (int k = 0; k < runs; k++)
            {
                double t0 = RandomCentre(random);
                var margin = gaps ? Gaps(t0, width) : Windows(t0, width);
                var expected = gaps ? ExpectedGapWindows(t0, width) : ExpectedWindows(t0, width);
                if (!Match(expected, search(margin, step)))
                {
                    failed++;
                }
            }

            result.Add((width, failed, runs));
        }

        return result;
    }

    /// <summary>The widths of the measurement, from 100 µs down to 100 ns.</summary>
    internal static readonly double[] MeasuredWidths = { 1.0e-4, 3.0e-5, 1.0e-5, 3.0e-6, 1.0e-6, 3.0e-7, 1.0e-7 };

    /// <summary>
    /// The narrowest width of the ladder found in every run, with every wider one, and the widest width missed at least
    /// once (0 when none is).
    /// </summary>
    internal static (double NarrowestFound, double WidestMissed) Summary(IReadOnlyList<(double Width, int Failed, int Runs)> ladder)
    {
        double narrowest = double.NaN;
        foreach (var row in ladder.OrderByDescending(x => x.Width))
        {
            if (row.Failed > 0)
            {
                break;
            }

            narrowest = row.Width;
        }

        double widest = ladder.Where(x => x.Failed > 0).Select(x => x.Width).DefaultIfEmpty(0.0).Max();
        return (narrowest, widest);
    }
}
