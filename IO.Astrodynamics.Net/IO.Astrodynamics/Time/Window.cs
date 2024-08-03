using System;

namespace IO.Astrodynamics.Time;
public readonly record struct Window
{
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }
    public TimeSpan Length { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public Window(DateTime startDate, DateTime endDate)
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
    public Window(DateTime startDate, TimeSpan length)
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
        DateTime min = StartDate < window.StartDate ? StartDate : window.StartDate;
        DateTime max = EndDate > window.EndDate ? EndDate : window.EndDate;

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
    public bool Intersects(DateTime date)
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

        DateTime min = StartDate > window.StartDate ? StartDate : window.StartDate;
        DateTime max = EndDate < window.EndDate ? EndDate : window.EndDate;
        return new Window(min, max);
    }

    public override string ToString()
    {
        return $"From {StartDate.ToFormattedString()} to {EndDate.ToFormattedString()} - Length {Length.ToString()}";
    }
}