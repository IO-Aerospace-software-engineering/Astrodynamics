// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.Math;

/// <summary>
/// The extremes of a sampled margin that the samples hide, shared by the occultation and field-of-view searches
/// (<c>OccultationFinder</c>) and the threshold searches of <see cref="GeometryFinder"/> (#678). Pure: it keeps nothing
/// between calls.
/// </summary>
internal static class HiddenExtremes
{
    /// <summary>
    /// The extremes between the samples where the margin crosses zero and back without a sample showing it: where three
    /// consecutive samples show an extreme on one side of zero, Brent's <c>localmin</c> between its two neighbors, and the
    /// first and last intervals when the samples beyond them leave room for an extreme; each extreme found on the other side
    /// of zero, with the margin there. A bracket with an undefined (NaN) sample is not searched.
    /// </summary>
    /// <param name="x">The offsets of the samples, increasing.</param>
    /// <param name="m">The margin at the samples.</param>
    /// <param name="f">The margin at any offset.</param>
    /// <param name="tolerance">The convergence tolerance of <c>localmin</c>, s.</param>
    /// <param name="zeroHolds">Whether the condition holds where the margin is zero (m ≥ 0: the occultation and
    /// field-of-view searches, and the relations that hold at equality), or only where it is positive (m &gt; 0: the
    /// strict relations).</param>
    internal static List<(double X, double M)> Find(IReadOnlyList<double> x, IReadOnlyList<double> m,
        Func<double, double> f, double tolerance, bool zeroHolds)
    {
        var found = new List<(double X, double M)>();
        int last = x.Count - 1;
        if (last < 1)
        {
            return found;
        }

        bool Holds(double margin) => zeroHolds ? margin >= 0.0 : margin > 0.0;

        void Search(int from, int to, bool maximum)
        {
            // An undefined margin holds no condition: no extreme is searched next to it (the patterns below already fail on
            // a NaN sample; a search of two samples has none)
            for (int i = from; i <= to; i++)
            {
                if (double.IsNaN(m[i]))
                {
                    return;
                }
            }

            // A maximum is searched only where no sample holds, a minimum only where every sample holds: otherwise the
            // samples already bracket a change
            if (maximum ? Holds(m[from]) || Holds(m[to]) || (to - from == 2 && Holds(m[from + 1]))
                    : !Holds(m[from]) || !Holds(m[to]) || (to - from == 2 && !Holds(m[from + 1])))
            {
                return;
            }

            double a = x[from];
            double half = 0.5 * (x[to] - a);
            double middle = a + half;
            double sign = maximum ? -1.0 : 1.0;

            // The search stops at the first point on the other side: a positive margin for a maximum, a negative one (or,
            // when zero fails, zero) for a minimum
            double stopBelow = maximum || zeroHolds ? 0.0 : double.Epsilon;
            double offset = Brent.Minimize(o => sign * f(middle + o), -half, half, tolerance, out double value, stopBelow);
            if (value < stopBelow)
            {
                found.Add((middle + offset, sign * value));
            }
        }

        if (last == 1)
        {
            Search(0, 1, !Holds(m[0]));
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
}
