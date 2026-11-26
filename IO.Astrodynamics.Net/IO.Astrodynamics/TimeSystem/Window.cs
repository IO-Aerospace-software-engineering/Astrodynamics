using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.TimeSystem;
public readonly record struct Window
{
    public Time StartDate { get; }
    public Time EndDate { get; }

    /// <summary>
    /// The time elapsed from <see cref="StartDate"/> to <see cref="EndDate"/> (<c>EndDate - StartDate</c>): the leap
    /// seconds between two UTC ends count (#508), so a window from 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z lasts
    /// 121 s; the length given to the constructor that takes one, whose end is the start plus the length in calendar
    /// arithmetic (<see cref="Time.Add"/>): across a leap second a UTC window built from a length keeps that length,
    /// while <c>EndDate - StartDate</c> counts the leap second.
    /// </summary>
    public TimeSpan Length { get; }

    /// <summary>
    /// The window from <paramref name="startDate"/> to <paramref name="endDate"/>, each end in its own scale.
    /// </summary>
    /// <remarks>
    /// The ends are compared as instants (#512), and an end equal to the start gives a window of zero length. Reversed
    /// ends throw since 11.0.0 (R11 of #668, decided by S. Guillet, 2026-11-19): before, they were swapped silently, so an
    /// argument-order mistake went unnoticed.
    /// </remarks>
    /// <param name="startDate">The start.</param>
    /// <param name="endDate">The end, at or after the start.</param>
    /// <exception cref="ArgumentException"><paramref name="endDate"/> is before <paramref name="startDate"/>.</exception>
    public Window(Time startDate, Time endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException(
                $"The end of the window, {endDate}, is before its start, {startDate}: give the start first.",
                nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
        Length = endDate - startDate;
    }

    /// <summary>
    /// The window of <paramref name="length"/> from <paramref name="startDate"/>, its end in the scale of its start.
    /// </summary>
    /// <remarks>
    /// A length of zero is accepted. A negative length throws since 11.0.0 (R11 of #668, decided by S. Guillet,
    /// 2026-11-19): before, the window ended before it started.
    /// </remarks>
    /// <param name="startDate">The start.</param>
    /// <param name="length">The length, zero or positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public Window(Time startDate, TimeSpan length)
    {
        if (length < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "The length of a window cannot be negative.");
        }

        StartDate = startDate;
        EndDate = startDate + length;
        Length = length;
    }

    /// <summary>
    /// The smallest window that holds both windows: the earlier start and the later end, compared as instants.
    /// </summary>
    /// <remarks>
    /// The comparisons are strict, so on a tie of instants the end of <paramref name="window"/> is taken, in its scale.
    /// It replaces <c>Merge</c> since 11.0.0, bit for bit (R16 of #668, decided by S. Guillet, 2026-11-19). Two disjoint
    /// windows give the window that spans the gap between them; <see cref="TryMerge"/> merges only windows that share an
    /// instant.
    /// </remarks>
    /// <param name="window">The other window.</param>
    /// <returns>The hull of the two windows.</returns>
    public Window Hull(Window window)
    {
        Time min = StartDate < window.StartDate ? StartDate : window.StartDate;
        Time max = EndDate > window.EndDate ? EndDate : window.EndDate;

        return new Window(min, max);
    }

    /// <summary>
    /// The hull of two windows that share at least one instant (<see cref="Intersects(Window)"/>), touching windows
    /// included, as CSPICE's <c>wninsd</c> merges [0, 1] and [1, 2] into [0, 2] (R16 of #668, decided by S. Guillet,
    /// 2026-11-19).
    /// </summary>
    /// <param name="window">The other window.</param>
    /// <param name="merged">The hull (<see cref="Hull"/>) when the windows intersect, the default window otherwise.</param>
    /// <returns>Whether the windows intersect.</returns>
    public bool TryMerge(Window window, out Window merged)
    {
        bool intersects = Intersects(window);
        merged = intersects ? Hull(window) : default;
        return intersects;
    }

    /// <summary>
    /// Whether two windows share at least one instant: the ends are closed, so touching windows intersect.
    /// </summary>
    /// <param name="window">The other window.</param>
    /// <returns>Whether the windows intersect.</returns>
    public bool Intersects(Window window)
    {
        return !(window.StartDate > EndDate || window.EndDate < StartDate);
    }

    /// <summary>
    /// Whether the window holds the instant <paramref name="epoch"/>, its ends included, whatever the scales
    /// (R16 of #668, decided by S. Guillet, 2026-11-19: <c>Intersects(Time)</c> before 11.0.0).
    /// </summary>
    /// <param name="epoch">The instant.</param>
    /// <returns>Whether <paramref name="epoch"/> is at or after the start and at or before the end.</returns>
    public bool Contains(Time epoch)
    {
        return epoch >= StartDate && epoch <= EndDate;
    }

    /// <summary>
    /// The instants two windows share: the later start and the earlier end, compared strictly, so on a tie of instants
    /// the end of <paramref name="window"/> is taken, in its scale. Touching windows share one instant, a window of zero
    /// length, as CSPICE's <c>wnintd</c> gives [1, 1] for [0, 1] and [1, 2].
    /// </summary>
    /// <param name="window">The other window.</param>
    /// <returns>The intersection.</returns>
    /// <exception cref="ArgumentException">The windows share no instant (<see cref="TryGetIntersection"/> returns false).</exception>
    public Window GetIntersection(Window window)
    {
        if (!TryGetIntersection(window, out var intersection))
        {
            throw new ArgumentException("Windows don't intersect");
        }

        return intersection;
    }

    /// <summary>
    /// The intersection of two windows (<see cref="GetIntersection"/>) when they share an instant (R16 of #668, decided
    /// by S. Guillet, 2026-11-19).
    /// </summary>
    /// <param name="window">The other window.</param>
    /// <param name="intersection">The intersection when the windows intersect, the default window otherwise.</param>
    /// <returns>Whether the windows intersect.</returns>
    public bool TryGetIntersection(Window window, out Window intersection)
    {
        if (!Intersects(window))
        {
            intersection = default;
            return false;
        }

        Time min = StartDate > window.StartDate ? StartDate : window.StartDate;
        Time max = EndDate < window.EndDate ? EndDate : window.EndDate;
        intersection = new Window(min, max);
        return true;
    }

    /// <summary>
    /// The instants <c>StartDate + i · step</c>, i from 0, at or before <see cref="EndDate"/>: each point is the start plus
    /// i steps in the calendar of the start's scale (<see cref="Time.Add"/>, exact to the tick), compared with the end as
    /// an instant, so the end is a point when it lies on the grid; with <paramref name="includeEnd"/> the end, in its own
    /// scale, is also appended when the last point precedes it, however close (R11 of #668, decision 9, decided by
    /// S. Guillet, 2026-11-22).
    /// </summary>
    /// <remarks>
    /// The grid of the library's window reads (<c>GetEphemeris</c> of a <see cref="Body.CelestialItem"/> or a
    /// <see cref="Surface.Site"/>, <c>Star.Propagate</c>, <c>SpiceAPI.TransformFrame</c>, <c>SpiceAPI.ReadEphemeris</c>,
    /// <c>SpiceAPI.ReadOrientation</c>), each without the end off the grid. Across a UTC leap second the points follow the
    /// UTC calendar, so a window from 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z (121 s, <see cref="Length"/>) gives 121
    /// points at 1 s, the last its end. The points are computed as they are enumerated; a point past the last tick of the
    /// calendar ends them.
    /// </remarks>
    /// <param name="step">The step between two points, positive.</param>
    /// <param name="includeEnd">Whether the end is appended when it lies off the grid.</param>
    /// <returns>The points, in time order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is zero or negative, at the call.</exception>
    public IEnumerable<Time> Sample(TimeSpan step, bool includeEnd = false)
    {
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "The step of a sampling must be positive.");
        }

        return Points(StartDate, EndDate, step.Ticks, includeEnd);

        static IEnumerable<Time> Points(Time start, Time end, long stepTicks, bool includeEnd)
        {
            // The number of steps from the start to the calendar's last tick, past which no point exists
            long maximumIndex = (DateTime.MaxValue.Ticks - start.DateTime.Ticks) / stepTicks;
            Time last = start;
            for (long i = 0; i <= maximumIndex; i++)
            {
                var point = start.Add(TimeSpan.FromTicks(stepTicks * i));
                if (point > end)
                {
                    break;
                }

                last = point;
                yield return point;
            }

            if (includeEnd && last < end)
            {
                yield return end;
            }
        }
    }

    /// <summary>
    /// Whether two windows have the same ends, instants compared whatever their frames (<see cref="Time.Equals(Time)"/>,
    /// #512, decided by S. Guillet, 2026-11-17): the same instants in UTC and in TDB are one window. <see cref="Length"/>
    /// does not take part: it follows from the ends, but for a UTC window built from a length across a leap second,
    /// whose length is the one given.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 the record's default equality compared the ends with their frames, and the length.
    /// </remarks>
    public bool Equals(Window other)
    {
        return StartDate.Equals(other.StartDate) && EndDate.Equals(other.EndDate);
    }

    /// <summary>
    /// The hash code of the two instants, consistent with <see cref="Equals(Window)"/>.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(StartDate, EndDate);
    }

    public override string ToString()
    {
        return $"From {StartDate.ToString()} to {EndDate.ToString()} - Length {Length.ToString()}";
    }
}