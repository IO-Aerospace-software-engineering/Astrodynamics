// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.TimeSystem;

/// <summary>
/// The algebra of window lists, as CSPICE's <c>wn*</c> routines give it (R16 of #668, decided by S. Guillet, 2026-11-19):
/// each list is read as the set of instants its closed windows hold, and each result is sorted, disjoint and closed, a
/// window of zero length kept.
/// </summary>
/// <remarks>
/// <para>
/// A list is first normalised as CSPICE's <c>wninsd</c> inserts windows: sorted by start and merged where windows overlap
/// or touch, so a list of touching windows (the result of a <c>NotEqual</c> search) is one window. The operations then
/// follow <c>wnintd</c>, <c>wnunid</c>, <c>wndifd</c> and <c>wncomd</c>, compared on lists of tools/window_list_reference.
/// </para>
/// <para>
/// The instants are compared through <see cref="Time.CompareTo(Time)"/>, whatever the scales. Each end of a result is an
/// end of an input, with its label (scale and tick); on a tie of instants it is the end of the first window that holds
/// it, those of the receiver before those of the argument.
/// </para>
/// <para>
/// The names are not LINQ's: <c>Intersect</c>, <c>Union</c> and <c>Except</c> of LINQ compare the windows as values, so
/// that [0, 2 h] and [1 h, 3 h] have no window in common.
/// </para>
/// </remarks>
public static class WindowList
{
    /// <summary>
    /// The instants both lists hold, as CSPICE's <c>wnintd</c>: touching windows share one instant, a window of zero
    /// length.
    /// </summary>
    /// <param name="windows">The first list.</param>
    /// <param name="other">The second list.</param>
    /// <returns>The intersection, sorted and disjoint.</returns>
    /// <exception cref="ArgumentNullException">A list is null.</exception>
    public static IReadOnlyList<Window> IntersectWindows(this IEnumerable<Window> windows, IEnumerable<Window> other)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(other);
        var a = Normalize(windows);
        var b = Normalize(other);
        var result = new List<Window>();
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            Time start = b[j].StartDate > a[i].StartDate ? b[j].StartDate : a[i].StartDate;
            Time end = b[j].EndDate < a[i].EndDate ? b[j].EndDate : a[i].EndDate;
            if (start <= end)
            {
                result.Add(new Window(start, end));
            }

            if (b[j].EndDate < a[i].EndDate) j++;
            else i++;
        }

        return result.ToArray();
    }

    /// <summary>
    /// The instants either list holds, as CSPICE's <c>wnunid</c>: overlapping and touching windows merged.
    /// </summary>
    /// <param name="windows">The first list.</param>
    /// <param name="other">The second list.</param>
    /// <returns>The union, sorted and disjoint.</returns>
    /// <exception cref="ArgumentNullException">A list is null.</exception>
    public static IReadOnlyList<Window> UnionWindows(this IEnumerable<Window> windows, IEnumerable<Window> other)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(other);
        return Normalize(windows.Concat(other)).ToArray();
    }

    /// <summary>
    /// The instants of <paramref name="windows"/> that <paramref name="other"/> does not hold, closed, as CSPICE's
    /// <c>wndifd</c>: the ends of the windows taken out stay, so taking one instant out of a window leaves it whole.
    /// </summary>
    /// <param name="windows">The list taken from.</param>
    /// <param name="other">The list taken out.</param>
    /// <returns>The difference, sorted and disjoint.</returns>
    /// <exception cref="ArgumentNullException">A list is null.</exception>
    public static IReadOnlyList<Window> ExceptWindows(this IEnumerable<Window> windows, IEnumerable<Window> other)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(other);
        var b = Normalize(other);
        var pieces = new List<Window>();
        foreach (var a in Normalize(windows))
        {
            // From the start of the window, each window taken out ends a piece at its start and starts the next at its
            // end; the last piece runs to the end of the window unless that end was taken out with nothing after it
            Time current = a.StartDate;
            bool covered = false;
            foreach (var w in b)
            {
                if (w.EndDate < a.StartDate || w.StartDate > a.EndDate) continue;
                if (w.StartDate > current) pieces.Add(new Window(current, w.StartDate));
                if (w.EndDate >= current)
                {
                    if (w.EndDate > current) current = w.EndDate;
                    covered = true;
                }
            }

            if (current < a.EndDate || (current == a.EndDate && !covered))
            {
                pieces.Add(new Window(current, a.EndDate));
            }
        }

        return Normalize(pieces).ToArray();
    }

    /// <summary>
    /// The instants of <paramref name="window"/> that the list does not hold, closed: CSPICE's <c>wndifd</c> of
    /// <paramref name="window"/> and the list, which is <c>wncomd</c> with respect to the window as its header defines
    /// it (<c>wncomd</c> itself returns a window past its right end where a window of the list ends before the window and
    /// the next one starts after it).
    /// </summary>
    /// <param name="windows">The list.</param>
    /// <param name="window">The window the complement is taken in.</param>
    /// <returns>The complement, sorted and disjoint, within <paramref name="window"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="windows"/> is null.</exception>
    public static IReadOnlyList<Window> ComplementWithin(this IEnumerable<Window> windows, Window window)
    {
        ArgumentNullException.ThrowIfNull(windows);
        return new[] { window }.ExceptWindows(windows);
    }

    /// <summary>
    /// The time the list holds: the sum of <c>EndDate − StartDate</c> over its windows normalised (overlaps counted once),
    /// the leap seconds between UTC ends counted (#508).
    /// </summary>
    /// <param name="windows">The list.</param>
    /// <returns>The total duration, zero for an empty list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="windows"/> is null.</exception>
    public static TimeSpan TotalDuration(this IEnumerable<Window> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var total = TimeSpan.Zero;
        foreach (var w in Normalize(windows))
        {
            total += w.EndDate - w.StartDate;
        }

        return total;
    }

    /// <summary>
    /// The list as CSPICE's <c>wninsd</c> builds it: sorted by start (stable, so the first of equal starts stays first),
    /// overlapping and touching windows merged, the later end kept only when it is a later instant.
    /// </summary>
    private static List<Window> Normalize(IEnumerable<Window> windows)
    {
        var result = new List<Window>();
        foreach (var w in windows.OrderBy(w => w.StartDate))
        {
            if (result.Count > 0 && w.StartDate <= result[^1].EndDate)
            {
                if (w.EndDate > result[^1].EndDate) result[^1] = new Window(result[^1].StartDate, w.EndDate);
            }
            else
            {
                result.Add(w);
            }
        }

        return result;
    }
}
