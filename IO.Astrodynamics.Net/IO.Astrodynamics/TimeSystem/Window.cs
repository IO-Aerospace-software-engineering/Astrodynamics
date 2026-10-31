using System;

namespace IO.Astrodynamics.TimeSystem;
public readonly record struct Window
{
    public Time StartDate { get; }
    public Time EndDate { get; }

    /// <summary>
    /// The time elapsed from <see cref="StartDate"/> to <see cref="EndDate"/> (<c>EndDate - StartDate</c>): the leap
    /// seconds between two UTC ends count (#508), so a window from 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z lasts
    /// 121 s; the length given to the constructor that takes one.
    /// </summary>
    public TimeSpan Length { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public Window(Time startDate, Time endDate)
    {
        if (startDate > endDate)
        {
            (endDate, startDate) = (startDate, endDate);
        }
        StartDate = startDate;
        EndDate = endDate;
        Length = endDate - startDate;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="length"></param>
    public Window(Time startDate, TimeSpan length)
    {
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

    public override string ToString()
    {
        return $"From {StartDate.ToString()} to {EndDate.ToString()} - Length {Length.ToString()}";
    }
}