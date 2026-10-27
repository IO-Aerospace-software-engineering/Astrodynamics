// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Occultation;

/// <summary>
/// The search of the windows of an occultation: the margins of <see cref="OccultationGeometry"/> sampled on the step,
/// their extremes between the samples, and Brent's root finder on each sign change. Pure: it keeps nothing between
/// calls, and two searches may run concurrently.
/// </summary>
/// <remarks>
/// <para>
/// Each condition is the set where a continuous margin m(t) is non-negative: the overlap of the discs for
/// <see cref="OccultationType.Any"/>, the back disc within the front one for <see cref="OccultationType.Full"/>, the
/// front disc within the back one for <see cref="OccultationType.Annular"/>; <see cref="OccultationType.Partial"/> is
/// the overlap without the two others. Each window is then kept only when the front body is the nearer one at its middle,
/// which cannot change while the discs of two disjoint bodies overlap (otherwise it is a transit of the back body).
/// </para>
/// <para>
/// <b>Step rule.</b> m is sampled at every step from the start of the search window, and at its end. Where three
/// consecutive samples show an extreme that does not reach zero (a maximum below zero, a minimum above), Brent's
/// <c>localmin</c> locates it between its neighbors and, when it crosses zero, adds it as a sample; the first and last
/// intervals are searched the same way when the samples beyond them leave room for an extreme. If m is monotone over
/// every interval of more than twice the step, each such bracket holds at most one extreme, so every window and every
/// gap is found, however short: an event shorter than the step is not missed. The rule is on the time between the
/// extremes of the margin, not on the duration of the events (CSPICE's <c>gfoclt</c> asks the step to be shorter than
/// every event and every gap): for an eclipse of a satellite, the margins have one maximum and one minimum per orbit,
/// so a step up to a quarter of the period finds a penumbra of a few seconds.
/// </para>
/// <para>
/// <b>Transitions.</b> Each sign change between two consecutive samples is located by Brent's <c>zero</c> to the
/// convergence tolerance (1 µs by default, CSPICE's <c>SPICE_GF_CNVTOL</c>); the epoch is then rounded to the 100 ns of
/// a <see cref="Time"/>. A window that holds at the start or the end of the search window, or whose transition is
/// located within the tolerance of them, starts or ends there, and no state outside the search window is read.
/// </para>
/// </remarks>
internal static class OccultationFinder
{
    /// <summary>
    /// The windows, in TDB, where the occultation of the given type holds within the search window.
    /// </summary>
    /// <param name="evaluator">The geometry.</param>
    /// <param name="searchWindow">The search window.</param>
    /// <param name="type">The type of occultation.</param>
    /// <param name="step">The step of the samples, positive.</param>
    /// <param name="tolerance">The convergence tolerance of the transitions, positive.</param>
    internal static IReadOnlyList<Window> FindWindows(OccultationEvaluator evaluator, in Window searchWindow,
        OccultationType type, in TimeSpan step, in TimeSpan tolerance)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "The step must be positive.");
        }

        if (tolerance <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "The convergence tolerance must be positive.");
        }

        var margins = MarginsOf(type, evaluator.FrontIsPoint, evaluator.BackIsPoint);
        if (margins == OccultationMargin.None)
        {
            return Array.Empty<Window>();
        }

        var start = searchWindow.StartDate.ToTDB();
        var end = searchWindow.EndDate.ToTDB();
        long totalTicks = (end - start).Ticks;
        if (totalTicks < 0)
        {
            throw new ArgumentException("The search window must not end before it starts.", nameof(searchWindow));
        }

        // The samples: every step from the start, and the end
        var offsets = new List<double>();
        for (long ticks = 0; ticks < totalTicks; ticks += step.Ticks)
        {
            offsets.Add(ticks / (double)TimeSpan.TicksPerSecond);
        }

        offsets.Add(totalTicks / (double)TimeSpan.TicksPerSecond);
        var states = offsets.Select(x => evaluator.Evaluate(OccultationEvaluator.Shift(start, x), margins, false)).ToArray();
        double toleranceSeconds = tolerance.Ticks / (double)TimeSpan.TicksPerSecond;

        List<(double Start, double End)> Search(OccultationMargin margin)
        {
            Func<double, double> f = x => evaluator.Evaluate(OccultationEvaluator.Shift(start, x), margin, false)[margin];
            return PositiveWindows(offsets, states.Select(s => s[margin]).ToArray(), f, toleranceSeconds);
        }

        // Overlap is the condition of Any (and the base of Partial); a point body has one condition, held in Overlap
        var primary = (margins & OccultationMargin.Overlap) != 0 ? OccultationMargin.Overlap : margins;
        var windows = Search(primary)
            .Where(w => evaluator.Evaluate(OccultationEvaluator.Shift(start, 0.5 * (w.Start + w.End)), OccultationMargin.None, true).FrontIsInFront)
            .ToList();

        if (type == OccultationType.Partial && primary == OccultationMargin.Overlap && margins != OccultationMargin.Overlap)
        {
            var inside = Search(OccultationMargin.BackInside).Concat(Search(OccultationMargin.FrontInside)).OrderBy(w => w.Start).ToList();
            windows = Subtract(windows, inside);
        }

        return windows.Select(w => new Window(
            w.Start == 0.0 ? start : OccultationEvaluator.Shift(start, w.Start),
            w.End == offsets[^1] ? end : OccultationEvaluator.Shift(start, w.End))).ToArray();
    }

    /// <summary>
    /// The margins that a type of occultation needs; <see cref="OccultationMargin.None"/> when the type cannot happen.
    /// </summary>
    /// <remarks>
    /// A point back body can only be hidden: <see cref="OccultationType.Full"/> and <see cref="OccultationType.Any"/>
    /// are its occultation, the partial and annular ones never happen. A point front body can only transit the back
    /// body's disc, which is <see cref="OccultationType.Any"/>, and formally <see cref="OccultationType.Annular"/> (its
    /// disc, a point, inside the back body's); it never hides the back body, fully or partially. <c>gfoclt</c> accepts
    /// only <c>ANY</c> with a point (and the managed search, before, returned the full occultation of a point).
    /// </remarks>
    internal static OccultationMargin MarginsOf(OccultationType type, bool frontIsPoint, bool backIsPoint)
    {
        if (backIsPoint)
        {
            return type is OccultationType.Any or OccultationType.Full ? OccultationMargin.Overlap : OccultationMargin.None;
        }

        if (frontIsPoint)
        {
            return type is OccultationType.Any or OccultationType.Annular ? OccultationMargin.Overlap : OccultationMargin.None;
        }

        return type switch
        {
            OccultationType.Any => OccultationMargin.Overlap,
            OccultationType.Full => OccultationMargin.BackInside,
            OccultationType.Annular => OccultationMargin.FrontInside,
            OccultationType.Partial => OccultationMargin.Overlap | OccultationMargin.BackInside | OccultationMargin.FrontInside,
            OccultationType.None => throw new ArgumentException("An occultation search needs a type of occultation, not None.", nameof(type)),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    /// <summary>
    /// The intervals where the margin is non-negative, from its samples, as offsets in seconds from the first sample.
    /// </summary>
    /// <param name="x">The offsets of the samples, increasing.</param>
    /// <param name="m">The margin at the samples.</param>
    /// <param name="f">The margin at any offset.</param>
    /// <param name="tolerance">The convergence tolerance, s.</param>
    internal static List<(double Start, double End)> PositiveWindows(IReadOnlyList<double> x, IReadOnlyList<double> m,
        Func<double, double> f, double tolerance)
    {
        var points = new List<(double X, double M)>(x.Count);
        for (int i = 0; i < x.Count; i++)
        {
            points.Add((x[i], m[i]));
        }

        points.AddRange(HiddenCrossings(x, m, f, tolerance));
        points.Sort((p, q) => p.X.CompareTo(q.X));

        var windows = new List<(double Start, double End)>();
        double? open = points[0].M >= 0.0 ? points[0].X : null;
        for (int i = 1; i < points.Count; i++)
        {
            bool held = points[i - 1].M >= 0.0;
            bool holds = points[i].M >= 0.0;
            if (held == holds)
            {
                continue;
            }

            double a = points[i - 1].X;
            double root = a + Brent.FindRoot(offset => f(a + offset), 0.0, points[i].X - a, points[i - 1].M, points[i].M, tolerance);
            if (holds)
            {
                // A transition located within the tolerance of the start of the search is the start
                open = root - points[0].X <= tolerance ? points[0].X : root;
            }
            else
            {
                windows.Add((open!.Value, points[^1].X - root <= tolerance ? points[^1].X : root));
                open = null;
            }
        }

        if (open.HasValue)
        {
            windows.Add((open.Value, points[^1].X));
        }

        return windows;
    }

    /// <summary>
    /// The extremes between the samples where the margin crosses zero and back without a sample showing it.
    /// </summary>
    private static List<(double X, double M)> HiddenCrossings(IReadOnlyList<double> x, IReadOnlyList<double> m,
        Func<double, double> f, double tolerance)
    {
        var found = new List<(double X, double M)>();
        int last = x.Count - 1;
        if (last < 1)
        {
            return found;
        }

        void Search(int from, int to, bool maximum)
        {
            // A maximum is searched only below zero, a minimum only above: otherwise the samples already bracket a change
            if (maximum ? m[from] >= 0.0 || m[to] >= 0.0 || (to - from == 2 && m[from + 1] >= 0.0)
                    : m[from] < 0.0 || m[to] < 0.0 || (to - from == 2 && m[from + 1] < 0.0))
            {
                return;
            }

            double a = x[from];
            double half = 0.5 * (x[to] - a);
            double middle = a + half;
            double sign = maximum ? -1.0 : 1.0;
            double offset = Brent.Minimize(o => sign * f(middle + o), -half, half, tolerance, out double value, 0.0);
            if (value < 0.0)
            {
                found.Add((middle + offset, sign * value));
            }
        }

        if (last == 1)
        {
            Search(0, 1, m[0] < 0.0);
            return found;
        }

        // Interior extremes of the samples, bracketed by their two neighbors
        for (int k = 1; k < last; k++)
        {
            double before = m[k] - m[k - 1];
            double after = m[k + 1] - m[k];
            if ((before > 0.0 && after <= 0.0) || (before >= 0.0 && after < 0.0))
            {
                Search(k - 1, k + 1, true);
            }
            else if ((before < 0.0 && after >= 0.0) || (before <= 0.0 && after > 0.0))
            {
                Search(k - 1, k + 1, false);
            }
        }

        // The first interval: an extreme inside it shows no pattern at the first interior sample
        double first = m[1] - m[0];
        double second = m[2] - m[1];
        if (second < 0.0 && first <= 0.0)
        {
            Search(0, 1, true);
        }
        else if (second > 0.0 && first >= 0.0)
        {
            Search(0, 1, false);
        }

        // The last interval, likewise
        double penultimate = m[last - 1] - m[last - 2];
        double final = m[last] - m[last - 1];
        if (penultimate > 0.0 && final >= 0.0)
        {
            Search(last - 1, last, true);
        }
        else if (penultimate < 0.0 && final <= 0.0)
        {
            Search(last - 1, last, false);
        }

        return found;
    }

    /// <summary>The parts of <paramref name="windows"/> outside <paramref name="removed"/>, both sorted.</summary>
    internal static List<(double Start, double End)> Subtract(List<(double Start, double End)> windows,
        List<(double Start, double End)> removed)
    {
        var result = new List<(double Start, double End)>();
        foreach (var window in windows)
        {
            double cursor = window.Start;
            foreach (var r in removed)
            {
                if (r.End <= cursor || r.Start >= window.End)
                {
                    continue;
                }

                if (r.Start > cursor)
                {
                    result.Add((cursor, r.Start));
                }

                cursor = System.Math.Max(cursor, r.End);
            }

            if (window.End > cursor)
            {
                result.Add((cursor, window.End));
            }
        }

        return result;
    }
}
