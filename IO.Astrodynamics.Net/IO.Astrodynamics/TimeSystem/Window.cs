using System;

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
    /// Merge two windows
    /// </summary>
    /// <param name="window"></param>
    /// <returns></returns>
    public Window Merge(Window window)
    {
        Time min = StartDate < window.StartDate ? StartDate : window.StartDate;
        Time max = EndDate > window.EndDate ? EndDate : window.EndDate;

        return new Window(min, max);
    }

    /// <summary>
    /// Know if two windows intersects
    /// </summary>
    /// <param name="window"></param>
    /// <returns></returns>
    public bool Intersects(Window window)
    {
        return !(window.StartDate > EndDate || window.EndDate < StartDate);
    }


    /// <summary>
    /// Know if datetime intersects with window
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public bool Intersects(Time date)
    {
        return date >= StartDate && date <= EndDate;
    }

    /// <summary>
    /// Get intersection between two windows
    /// </summary>
    /// <param name="window"></param>
    /// <returns></returns>
    public Window GetIntersection(Window window)
    {
        if (!Intersects(window))
        {
            throw new ArgumentException("Windows don't intersect");
        }

        Time min = StartDate > window.StartDate ? StartDate : window.StartDate;
        Time max = EndDate < window.EndDate ? EndDate : window.EndDate;
        return new Window(min, max);
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