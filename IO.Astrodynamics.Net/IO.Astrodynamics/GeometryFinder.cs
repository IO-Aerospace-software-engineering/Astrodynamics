// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics;

/// <summary>
/// The managed search of the windows where a geometric quantity satisfies a relation: the search of the distance,
/// coordinate and illumination constraints of the bodies, spacecraft and sites that CSPICE cannot search (a propagated
/// spacecraft, a site built from its coordinates, two natural bodies).
/// </summary>
/// <remarks>
/// <para>
/// <b>Samples.</b> The quantity is sampled at every step from the start of the search window, and at its end; nothing is
/// evaluated outside the search window.
/// </para>
/// <para>
/// <b>Step rule.</b> Between the samples, the search looks for the extremes of a <see cref="double"/> quantity that the
/// samples hide, by the rule of the occultation search: where three consecutive samples show an extreme of the margin of
/// the relation (the quantity minus the value, or the value minus the quantity) that does not reach zero, Brent's
/// <c>localmin</c> locates it between their two neighbors, and the first and last intervals are searched the same way;
/// an extreme on the other side of zero is added as a sample. Every window, gap and crossing, however short, is
/// therefore found when the step is at most half the time between two extremes of the quantity, a jump of a branch cut
/// counting as one (#678). A <see cref="bool"/> quantity has no margin to search between samples: its step must be
/// shorter than every window and every gap. CSPICE's <c>gfdist_c</c>, <c>gfposc_c</c> and <c>gfilum_c</c> ask for a
/// step shorter than the shortest interval where the quantity is monotone (for a longitude or a right ascension, where
/// its sine or cosine is).
/// </para>
/// <para>
/// <b>Transitions.</b> Each change of the relation between two samples is located to 1 µs (CSPICE's
/// <c>SPICE_GF_CNVTOL</c>): by Brent's root finder on the quantity minus the value for <see cref="RelationnalOperator.Greater"/>,
/// <see cref="RelationnalOperator.Lower"/> and <see cref="RelationnalOperator.Equal"/>, by a bisection of the relation
/// for <see cref="RelationnalOperator.GreaterOrEqual"/>, <see cref="RelationnalOperator.LowerOrEqual"/> and a
/// <see cref="bool"/> quantity; the epoch of the transition is then rounded to the 100 ns of a <see cref="Time"/>. A
/// window that holds at the start of the search window starts there, one that holds at its end ends there, and a
/// transition located within 1 µs of them is moved onto them. The windows are closed, as CSPICE's: two windows that one
/// edge would close and open are one window.
/// </para>
/// <para>
/// <b>Equality.</b> <see cref="RelationnalOperator.Equal"/> gives the instants where the quantity crosses the value, as
/// windows of zero length, and the intervals where the samples equal it exactly; a jump of the quantity (the branch cut
/// of a right ascension, a longitude or an azimuth) is no crossing. <see cref="RelationnalOperator.NotEqual"/> gives the
/// rest of the search window, a window from each crossing to the next. A <see cref="bool"/> quantity is compared
/// exactly.
/// </para>
/// <para>
/// <b>Extremes.</b> <see cref="RelationnalOperator.LocalMin"/> and <see cref="RelationnalOperator.LocalMax"/> give the
/// sign changes of the rate of the quantity inside the search window, each located to 1 µs, as windows of zero length;
/// the rate is the Richardson extrapolation of the derivatives of the quartics through five values, a sixteenth and a
/// thirty-second of the step apart, taken at the samples about each extreme the values show and at the first and last
/// two samples. As CSPICE's, the
/// bounds of the search window are no local extremes, while they are candidates of
/// <see cref="RelationnalOperator.AbsoluteMin"/> and <see cref="RelationnalOperator.AbsoluteMax"/>: the lowest (or
/// highest) of the local extremes and of the bounds, the earliest on a tie, as a window of zero length; with a positive
/// adjustment value, the windows where the quantity is within it of that extreme.
/// </para>
/// <para>
/// <b>Branch cuts.</b> A threshold window on a quantity that jumps (a right ascension, a longitude, an azimuth) ends or
/// starts at the jump, and an extreme of the rate there is no extreme.
/// </para>
/// <para>The search keeps no state between calls: two searches may run concurrently.</para>
/// </remarks>
public class GeometryFinder
{
    // The convergence tolerance of the transitions, s: CSPICE's SPICE_GF_CNVTOL
    private const double Tolerance = 1e-6;

    // The spacing of the five values of the rate, as a fraction of the step (and half of it, for Richardson's extrapolation)
    private const double RateSpacingPerStep = 1.0 / 16.0;

    public GeometryFinder()
    {
    }

    /// <summary>
    /// Whether a value satisfies a relation with a target value.
    /// </summary>
    /// <param name="actualValue">The value.</param>
    /// <param name="targetValue">The target value.</param>
    /// <param name="operatorType">The relation; the extremes are no relation of two values.</param>
    /// <returns>Whether the relation holds; the equality of two doubles holds within 1e-9.</returns>
    /// <exception cref="ArgumentException">The relation is an extreme, or an order of two booleans, or the type is
    /// neither a double nor a boolean.</exception>
    public bool EvaluateCondition<T>(T actualValue, T targetValue, RelationnalOperator operatorType)
    {
        if (actualValue is double actualDouble && targetValue is double targetDouble)
        {
            switch (operatorType)
            {
                case RelationnalOperator.Greater:
                    return actualDouble > targetDouble;
                case RelationnalOperator.GreaterOrEqual:
                    return actualDouble >= targetDouble;
                case RelationnalOperator.Lower:
                    return actualDouble < targetDouble;
                case RelationnalOperator.LowerOrEqual:
                    return actualDouble <= targetDouble;
                case RelationnalOperator.Equal:
                    return System.Math.Abs(actualDouble - targetDouble) < 1e-9;
                case RelationnalOperator.NotEqual:
                    return System.Math.Abs(actualDouble - targetDouble) >= 1e-9;
                default:
                    throw new ArgumentException("Invalid operator");
            }
        }
        else if (actualValue is bool actualBool && targetValue is bool targetBool)
        {
            switch (operatorType)
            {
                case RelationnalOperator.Equal:
                    return actualBool == targetBool;
                case RelationnalOperator.NotEqual:
                    return actualBool != targetBool;
                default:
                    throw new ArgumentException("Invalid operator");
            }
        }
        else
        {
            throw new ArgumentException("Invalid type");
        }
    }

    /// <summary>
    /// The windows of the search window where the quantity satisfies the relation; see the remarks of the class.
    /// </summary>
    /// <param name="searchWindow">The search window; the windows are in the time frame of its start.</param>
    /// <param name="calculateValue">The quantity at an epoch: a <see cref="double"/>, or a <see cref="bool"/> for
    /// <see cref="RelationnalOperator.Equal"/> and <see cref="RelationnalOperator.NotEqual"/>.</param>
    /// <param name="relationalOperator">The relation.</param>
    /// <param name="targetValue">The value; ignored by the extremes.</param>
    /// <param name="stepSize">The step of the samples, positive: at most half the time between two extremes of a
    /// <see cref="double"/> quantity, shorter than every window and gap of a <see cref="bool"/> one (the step rule of the
    /// remarks of the class).</param>
    /// <returns>The windows, in time order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The step is not positive.</exception>
    /// <exception cref="ArgumentException">The type is neither a double nor a boolean, or the relation is not one of a
    /// boolean.</exception>
    public IEnumerable<Window> FindWindowsWithCondition<T>(Window searchWindow,
        Func<Time, T> calculateValue,
        RelationnalOperator relationalOperator, T targetValue,
        TimeSpan stepSize)
    {
        return FindWindowsWithCondition(searchWindow, calculateValue, relationalOperator, targetValue, 0.0, stepSize);
    }

    /// <summary>
    /// The windows of the search window where the quantity satisfies the relation, with the adjustment value of the
    /// absolute extremes (CSPICE's <c>adjust</c>); see the remarks of the class.
    /// </summary>
    /// <param name="searchWindow">The search window; the windows are in the time frame of its start.</param>
    /// <param name="calculateValue">The quantity at an epoch.</param>
    /// <param name="relationalOperator">The relation.</param>
    /// <param name="targetValue">The value; ignored by the extremes.</param>
    /// <param name="adjustValue">For <see cref="RelationnalOperator.AbsoluteMin"/> and
    /// <see cref="RelationnalOperator.AbsoluteMax"/>, zero for the extreme itself, or positive for the windows where the
    /// quantity is within it of the extreme; ignored by the other relations.</param>
    /// <param name="stepSize">The step of the samples, positive (the step rule of the remarks of the class).</param>
    /// <exception cref="ArgumentOutOfRangeException">The step is not positive, or the adjustment value of an absolute
    /// extreme is negative.</exception>
    internal IEnumerable<Window> FindWindowsWithCondition<T>(Window searchWindow, Func<Time, T> calculateValue,
        RelationnalOperator relationalOperator, T targetValue, double adjustValue, TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(calculateValue);
        if (stepSize <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(stepSize), "The step must be positive.");
        }

        if (calculateValue is Func<Time, double> quantity && targetValue is double value)
        {
            if (relationalOperator is RelationnalOperator.AbsoluteMin or RelationnalOperator.AbsoluteMax && !(adjustValue >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(adjustValue), "The adjustment value must be positive or zero.");
            }

            var search = new Search(searchWindow, stepSize, quantity);
            return search.ToWindows(Find(search, relationalOperator, value, adjustValue));
        }

        if (calculateValue is Func<Time, bool> predicate && targetValue is bool target)
        {
            if (relationalOperator is not (RelationnalOperator.Equal or RelationnalOperator.NotEqual))
            {
                throw new ArgumentException("Invalid operator");
            }

            bool equal = relationalOperator == RelationnalOperator.Equal;
            var search = new Search(searchWindow, stepSize, null);
            Func<double, bool> holds = x => predicate(search.At(x)) == target == equal;
            var offsets = search.Offsets;
            return search.ToWindows(Merge(Threshold(search, offsets, offsets.Select(holds).ToArray(), null, holds, null)));
        }

        throw new ArgumentException("Invalid type");
    }

    /// <summary>The windows of a double quantity, as offsets from the start of the search window.</summary>
    private static List<(double Start, double End)> Find(Search search, RelationnalOperator relation, double value, double adjust)
    {
        switch (relation)
        {
            case RelationnalOperator.Greater:
                return Merge(Strict(search, x => search.F(x) - value));
            case RelationnalOperator.Lower:
                return Merge(Strict(search, x => value - search.F(x)));
            case RelationnalOperator.GreaterOrEqual:
                return Merge(OrEqual(search, x => search.F(x) - value, x => search.F(x) >= value));
            case RelationnalOperator.LowerOrEqual:
                return Merge(OrEqual(search, x => value - search.F(x), x => search.F(x) <= value));
            case RelationnalOperator.Equal:
                return Crossings(search, value);
            case RelationnalOperator.NotEqual:
                return Complement(search, Crossings(search, value));
            case RelationnalOperator.LocalMin:
                return Extremes(search, true).Select(x => (x, x)).ToList();
            case RelationnalOperator.LocalMax:
                return Extremes(search, false).Select(x => (x, x)).ToList();
            case RelationnalOperator.AbsoluteMin:
                return Absolute(search, true, adjust);
            case RelationnalOperator.AbsoluteMax:
                return Absolute(search, false, adjust);
            default:
                throw new ArgumentException("Invalid operator");
        }
    }

    /// <summary>
    /// The margin of a relation at the samples (and at <paramref name="extra"/> offsets), each evaluated once, with the
    /// extremes of the margin between them that the samples hide (<see cref="OccultationFinder.HiddenCrossings"/>): a window,
    /// a gap or a pair of crossings shorter than the step is found when every two consecutive intervals hold at most one
    /// extreme of the quantity.
    /// </summary>
    /// <param name="search">The search.</param>
    /// <param name="margin">The margin at an offset, positive where the relation holds.</param>
    /// <param name="zeroHolds">Whether the relation holds where the margin is zero.</param>
    /// <param name="extra">Offsets sampled in addition to the step, inside the search window.</param>
    /// <returns>The offsets, increasing, and the margin at each.</returns>
    private static (List<double> X, double[] M) Sampled(Search search, Func<double, double> margin, bool zeroHolds, IEnumerable<double> extra = null)
    {
        var x = extra == null ? search.Offsets.ToList() : search.Offsets.Concat(extra).Distinct().OrderBy(o => o).ToList();
        var m = x.Select(margin).ToArray();
        var hidden = OccultationFinder.HiddenCrossings(x, m, margin, Tolerance, zeroHolds);
        if (hidden.Count == 0)
        {
            return (x, m);
        }

        var points = x.Select((o, i) => (X: o, M: m[i])).Concat(hidden).OrderBy(p => p.X).ToList();
        return (points.Select(p => p.X).ToList(), points.Select(p => p.M).ToArray());
    }

    /// <summary>
    /// The windows where a strict relation holds: its margin positive, each change located by Brent's root finder on the
    /// margin.
    /// </summary>
    private static List<(double Start, double End)> Strict(Search search, Func<double, double> margin, IEnumerable<double> extra = null)
    {
        var (x, m) = Sampled(search, margin, false, extra);
        return Threshold(search, x, m.Select(v => v > 0.0).ToArray(), m, null, margin);
    }

    /// <summary>
    /// The windows where a relation that holds at equality holds: its margin zero or positive, each change located by a
    /// bisection of the relation.
    /// </summary>
    private static List<(double Start, double End)> OrEqual(Search search, Func<double, double> margin, Func<double, bool> holds)
    {
        var (x, m) = Sampled(search, margin, true);
        return Threshold(search, x, m.Select(v => v >= 0.0).ToArray(), null, holds, null);
    }

    /// <summary>
    /// The windows where a relation holds, from its value at the samples: each change located by Brent's root finder on a
    /// margin, positive where the relation holds and zero or negative elsewhere, or by a bisection of the relation.
    /// </summary>
    /// <param name="search">The search.</param>
    /// <param name="x">The offsets of the samples, increasing.</param>
    /// <param name="h">The relation at the samples.</param>
    /// <param name="m">The margin at the samples, for Brent's root finder; null when the relation is bisected.</param>
    /// <param name="holds">The relation at an offset, for a bisection; null when the margin is given.</param>
    /// <param name="margin">The margin at an offset, for Brent's root finder; null when the relation is given.</param>
    private static List<(double Start, double End)> Threshold(Search search, IReadOnlyList<double> x, bool[] h, double[] m, Func<double, bool> holds,
        Func<double, double> margin)
    {
        double last = search.Length;

        var windows = new List<(double Start, double End)>();
        double? open = h[0] ? 0.0 : null;
        for (int i = 1; i < x.Count; i++)
        {
            if (h[i] == h[i - 1])
            {
                continue;
            }

            double a = x[i - 1];
            double root = margin == null || double.IsNaN(m[i - 1]) || double.IsNaN(m[i])
                ? Bisect(margin == null ? holds : o => margin(o) > 0.0, a, x[i], h[i])
                : a + Brent.FindRoot(o => margin(a + o), 0.0, x[i] - a, m[i - 1], m[i], Tolerance);
            if (h[i])
            {
                open = root <= Tolerance ? 0.0 : root;
            }
            else
            {
                windows.Add((open!.Value, last - root <= Tolerance ? last : root));
                open = null;
            }
        }

        if (open.HasValue)
        {
            windows.Add((open.Value, last));
        }

        return windows;
    }

    /// <summary>The change of a relation between two offsets, to the tolerance: the middle of the last bracket.</summary>
    /// <param name="holds">The relation.</param>
    /// <param name="a">The offset where the relation is the opposite of <paramref name="holdsAtB"/>.</param>
    /// <param name="b">The other offset.</param>
    /// <param name="holdsAtB">The relation at <paramref name="b"/>.</param>
    private static double Bisect(Func<double, bool> holds, double a, double b, bool holdsAtB)
    {
        while (b - a > Tolerance)
        {
            double middle = 0.5 * (a + b);
            if (middle <= a || middle >= b)
            {
                break;
            }

            if (holds(middle) == holdsAtB)
            {
                b = middle;
            }
            else
            {
                a = middle;
            }
        }

        return 0.5 * (a + b);
    }

    /// <summary>The union of windows in time order, two windows that share an edge made one, as a CSPICE window.</summary>
    private static List<(double Start, double End)> Merge(List<(double Start, double End)> windows)
    {
        var merged = new List<(double Start, double End)>(windows.Count);
        foreach (var window in windows)
        {
            if (merged.Count > 0 && window.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, System.Math.Max(merged[^1].End, window.End));
            }
            else
            {
                merged.Add(window);
            }
        }

        return merged;
    }

    /// <summary>
    /// The crossings of a value: an instant for each sign change of the quantity minus the value between two samples, or
    /// a sample and an extreme between samples, that is no jump of the quantity, and the samples, or runs of samples, where
    /// they are equal.
    /// </summary>
    private static List<(double Start, double End)> Crossings(Search search, double value)
    {
        var (x, g) = Sampled(search, o => search.F(o) - value, true);
        var found = new List<(double Start, double End)>();
        for (int i = 0; i < x.Count; i++)
        {
            if (g[i] == 0.0)
            {
                // A sample, or a run of samples, equal to the value
                int j = i;
                while (j + 1 < x.Count && g[j + 1] == 0.0)
                {
                    j++;
                }

                found.Add((x[i], x[j]));
                i = j;
                continue;
            }

            if (i + 1 < x.Count && g[i + 1] != 0.0 && g[i] > 0.0 != g[i + 1] > 0.0)
            {
                double a = x[i];
                double b = x[i + 1];
                double root = a + Brent.FindRoot(o => search.F(a + o) - value, 0.0, b - a, g[i], g[i + 1], Tolerance);
                if (!IsJump(search, root, a, b, System.Math.Abs(g[i + 1] - g[i])))
                {
                    root = root <= Tolerance ? 0.0 : search.Length - root <= Tolerance ? search.Length : root;
                    found.Add((root, root));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Whether a sign change located at <paramref name="root"/> in [<paramref name="a"/>, <paramref name="b"/>] is a
    /// jump of the quantity (a branch cut): the quantity changes across 20 µs about the root by more than half its change
    /// over the bracket.
    /// </summary>
    private static bool IsJump(Search search, double root, double a, double b, double change)
    {
        const double halfWidth = 10.0 * Tolerance;
        if (b - a < 8.0 * halfWidth)
        {
            return false;
        }

        double before = System.Math.Max(a, root - halfWidth);
        double after = System.Math.Min(b, root + halfWidth);
        return System.Math.Abs(search.F(after) - search.F(before)) > 0.5 * change;
    }

    /// <summary>
    /// The parts of the search window outside the crossings: a window from the start, or from each crossing, to the next
    /// crossing, or to the end; none of zero length.
    /// </summary>
    private static List<(double Start, double End)> Complement(Search search, List<(double Start, double End)> crossings)
    {
        var windows = new List<(double Start, double End)>();
        double cursor = 0.0;
        foreach (var crossing in crossings)
        {
            if (crossing.Start > cursor)
            {
                windows.Add((cursor, crossing.Start));
            }

            cursor = System.Math.Max(cursor, crossing.End);
        }

        if (search.Length > cursor)
        {
            windows.Add((cursor, search.Length));
        }

        return windows;
    }

    /// <summary>
    /// The local minima (or maxima) inside the search window: the sign changes of the rate between the samples, each
    /// located by Brent's root finder to the tolerance, the jumps of the quantity left out.
    /// </summary>
    private static List<double> Extremes(Search search, bool minimum)
    {
        var extremes = new List<double>();
        if (search.RateSpacing < 2.0 * Tolerance)
        {
            return extremes;
        }

        // The rate at the samples about each extreme the values show (three samples that do not increase, or decrease,
        // strictly) and at the first two and the last two samples, where the values show no extreme of the first or the
        // last interval
        var x = search.Offsets;
        int last = x.Count - 1;
        var f = x.Select(search.F).ToArray();
        var candidates = new SortedSet<int> { 0, 1, last - 1, last };
        for (int i = 1; i < last; i++)
        {
            if ((f[i] - f[i - 1]) * (f[i + 1] - f[i]) <= 0.0)
            {
                candidates.Add(i - 1);
                candidates.Add(i);
                candidates.Add(i + 1);
            }
        }

        var rate = new Dictionary<int, double>();
        foreach (var i in candidates.Where(i => i >= 0 && i <= last))
        {
            rate[i] = search.Rate(x[i]);
        }

        // A minimum where the rate goes from negative to zero or positive between two consecutive samples, a maximum the
        // other way; a zero rate at the first sample of the pair belongs to the previous pair
        for (int i = 0; i < last; i++)
        {
            if (!rate.TryGetValue(i, out double ra) || !rate.TryGetValue(i + 1, out double rb) || double.IsNaN(ra) || double.IsNaN(rb))
            {
                continue;
            }

            if (minimum ? ra < 0.0 && rb >= 0.0 : ra > 0.0 && rb <= 0.0)
            {
                double a = x[i];
                double root = a + Brent.FindRoot(o => search.Rate(a + o), 0.0, x[i + 1] - a, ra, rb, Tolerance);
                if (root > Tolerance && search.Length - root > Tolerance && IsExtreme(search, root))
                {
                    extremes.Add(root);
                }
            }
        }

        return extremes;
    }

    /// <summary>
    /// Whether the quantity has an extreme at <paramref name="root"/>, a zero of its rate: at the spacing of the rate,
    /// its change across the root is less than half its curvature term, which a jump of the quantity (a branch cut)
    /// is not.
    /// </summary>
    private static bool IsExtreme(Search search, double root)
    {
        // The extremes lie more than the tolerance inside the search window, and the rate spacing is at least twice it
        double spacing = System.Math.Min(search.RateSpacing, System.Math.Min(root, search.Length - root));
        double before = search.F(root - spacing);
        double after = search.F(root + spacing);
        return System.Math.Abs(after - before) < 0.5 * System.Math.Abs(after + before - 2.0 * search.F(root));
    }

    /// <summary>
    /// The absolute minimum (or maximum): the lowest (or highest) of the local extremes and of the bounds, the earliest on
    /// a tie, as a window of zero length; with a positive adjustment, the windows within it of the extreme.
    /// </summary>
    private static List<(double Start, double End)> Absolute(Search search, bool minimum, double adjust)
    {
        double sign = minimum ? 1.0 : -1.0;
        var locals = Extremes(search, minimum);
        var candidates = new List<double> { 0.0 };
        candidates.AddRange(locals);
        candidates.Add(search.Length);
        double best = candidates[0];
        double bestValue = sign * search.F(best);
        foreach (var candidate in candidates.Skip(1))
        {
            double v = sign * search.F(candidate);
            if (v < bestValue)
            {
                best = candidate;
                bestValue = v;
            }
        }

        if (adjust == 0.0)
        {
            return new List<(double Start, double End)> { (best, best) };
        }

        // The windows where the quantity is within the adjustment of the extreme; the local extremes are sampled, so that
        // a window narrower than the step about one of them is found, and a gap narrower than the step is found as a
        // hidden extreme of the margin
        double threshold = bestValue + adjust;
        return Merge(Strict(search, o => threshold - sign * search.F(o), locals));
    }

    /// <summary>
    /// A search: the samples, as offsets in seconds from the start of the search window, every step and the end, the
    /// quantity and its rate at an offset, and the windows of intervals of offsets.
    /// </summary>
    private sealed class Search
    {
        private readonly Window _window;
        private readonly Time _start;
        private readonly Func<Time, double> _quantity;

        internal Search(in Window searchWindow, in TimeSpan step, Func<Time, double> quantity)
        {
            _window = searchWindow;
            _start = searchWindow.StartDate;
            _quantity = quantity;

            // The end in the time frame of the start, whose calendar the samples follow
            long totalTicks = System.Math.Max(0L, (searchWindow.EndDate.ConvertTo(_start.Frame).DateTime - _start.DateTime).Ticks);
            var offsets = new List<double>();
            for (long ticks = 0; ticks < totalTicks; ticks += step.Ticks)
            {
                offsets.Add(ticks / (double)TimeSpan.TicksPerSecond);
            }

            Length = totalTicks / (double)TimeSpan.TicksPerSecond;
            offsets.Add(Length);
            Offsets = offsets;
            RateSpacing = System.Math.Min(System.Math.Max(step.TotalSeconds * RateSpacingPerStep, 20.0 * Tolerance), 0.25 * Length);
        }

        /// <summary>The offsets of the samples, s, increasing; the first is 0, the last the end of the search window.</summary>
        internal IReadOnlyList<double> Offsets { get; }

        /// <summary>The offset of the end of the search window, s.</summary>
        internal double Length { get; }

        /// <summary>The larger spacing of the five values of the rate, s.</summary>
        internal double RateSpacing { get; }

        /// <summary>The epoch of an offset, to the 100 ns of a <see cref="Time"/>; the bounds exactly.</summary>
        internal Time At(double offset)
        {
            if (offset <= 0.0)
            {
                return _start;
            }

            if (offset >= Length)
            {
                return _window.EndDate;
            }

            return _start.AddTicks(Ticks(offset));
        }

        /// <summary>The quantity at an offset, clamped to the search window.</summary>
        internal double F(double offset)
        {
            return _quantity(At(offset));
        }

        /// <summary>
        /// The rate of the quantity at an offset: the derivatives of the quartics through its values at five epochs, the
        /// rate spacing and half of it apart, centered on the offset or, near a bound, within the search window, combined
        /// by Richardson's extrapolation.
        /// </summary>
        internal double Rate(double offset)
        {
            // Richardson's extrapolation of the five-point derivatives at the spacing and at half of it: their h^4 terms
            // cancel
            return (16.0 * Rate(offset, 0.5 * RateSpacing) - Rate(offset, RateSpacing)) / 15.0;
        }

        private double Rate(double offset, double h)
        {
            double center = System.Math.Clamp(offset, 2.0 * h, Length - 2.0 * h);
            Span<double> nodes = stackalloc double[5];
            Span<double> values = stackalloc double[5];
            for (int k = 0; k < 5; k++)
            {
                // The epochs actually evaluated, to the 100 ns of a Time
                nodes[k] = System.Math.Clamp(Ticks(center + (k - 2) * h), 0L, Ticks(Length)) / (double)TimeSpan.TicksPerSecond;
                values[k] = F(nodes[k]);
            }

            return Derivative(nodes, values, offset);
        }

        /// <summary>The derivative at <paramref name="x"/> of the polynomial through the points, in the Lagrange form.</summary>
        private static double Derivative(ReadOnlySpan<double> nodes, ReadOnlySpan<double> values, double x)
        {
            double derivative = 0.0;
            for (int k = 0; k < nodes.Length; k++)
            {
                // L_k'(x) = sum over m != k of prod over j != k, m of (x - x_j), over prod over j != k of (x_k - x_j)
                double denominator = 1.0;
                double numerator = 0.0;
                for (int j = 0; j < nodes.Length; j++)
                {
                    if (j != k)
                    {
                        denominator *= nodes[k] - nodes[j];
                    }
                }

                for (int m = 0; m < nodes.Length; m++)
                {
                    if (m == k)
                    {
                        continue;
                    }

                    double product = 1.0;
                    for (int j = 0; j < nodes.Length; j++)
                    {
                        if (j != k && j != m)
                        {
                            product *= x - nodes[j];
                        }
                    }

                    numerator += product;
                }

                derivative += values[k] * numerator / denominator;
            }

            return derivative;
        }

        /// <summary>The windows of intervals of offsets; a bound of the search window exactly.</summary>
        internal IEnumerable<Window> ToWindows(IEnumerable<(double Start, double End)> intervals)
        {
            return intervals.Select(w => new Window(At(w.Start), At(w.End))).ToArray();
        }

        private static long Ticks(double offset)
        {
            return (long)System.Math.Round(offset * TimeSpan.TicksPerSecond);
        }
    }
}
