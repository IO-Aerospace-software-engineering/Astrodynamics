// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The assertions of the labels of times: the calendar and the frame of each time, as <see cref="Assert.Equal{T}(T, T)"/>
/// compared them before 11.0.0. Since #512 two times are equal when they are the same instant, whatever their frames, so
/// <see cref="Assert.Equal{T}(T, T)"/> of two times, two windows, two orbital parameters or two records holding a time no
/// longer checks a label: a test of a label uses these (the labels of the expected and the actual values compared, then the
/// values by their own equality).
/// </summary>
internal static class LabelAssert
{
    public static void Equal(TimeSystem.Time expected, TimeSystem.Time actual)
    {
        Assert.Equal((expected.DateTime, expected.Frame), (actual.DateTime, actual.Frame));
    }

    public static void Equal(TimeSystem.Time? expected, TimeSystem.Time? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue)
        {
            Equal(expected.Value, actual.Value);
        }
    }

    public static void Equal(Window expected, Window actual)
    {
        Equal(expected.StartDate, actual.StartDate);
        Equal(expected.EndDate, actual.EndDate);
        Assert.Equal(expected.Length, actual.Length);
    }

    public static void Equal(Window? expected, Window? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue)
        {
            Equal(expected.Value, actual.Value);
        }
    }

    public static void Equal<T>(T expected, T actual) where T : IO.Astrodynamics.OrbitalParameters.OrbitalParameters
    {
        Assert.Equal(expected, actual);
        if (expected is not null && actual is not null)
        {
            Equal(expected.Epoch, actual.Epoch);
        }
    }

    public static void Equal(StateOrientation expected, StateOrientation actual)
    {
        Assert.Equal(expected, actual);
        if (expected is not null && actual is not null)
        {
            Equal(expected.Epoch, actual.Epoch);
        }
    }

    public static void Equal(Equatorial expected, Equatorial actual)
    {
        Assert.Equal(expected, actual);
        Equal(expected.Epoch, actual.Epoch);
    }

    public static void Equal(IEnumerable<TimeSystem.Time> expected, IEnumerable<TimeSystem.Time> actual)
    {
        Assert.Equal(expected.Select(x => (x.DateTime, x.Frame)), actual.Select(x => (x.DateTime, x.Frame)));
    }

    public static void Equal(IEnumerable<Window> expected, IEnumerable<Window> actual)
    {
        var e = expected.ToArray();
        var a = actual.ToArray();
        Assert.Equal(e.Length, a.Length);
        for (int i = 0; i < e.Length; i++)
        {
            Equal(e[i], a[i]);
        }
    }

    public static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual) where T : IO.Astrodynamics.OrbitalParameters.OrbitalParameters
    {
        var e = expected.ToArray();
        var a = actual.ToArray();
        Assert.Equal(e, a);
        for (int i = 0; i < e.Length; i++)
        {
            Equal(e[i].Epoch, a[i].Epoch);
        }
    }

    public static void Equal(IEnumerable<StateOrientation> expected, IEnumerable<StateOrientation> actual)
    {
        var e = expected.ToArray();
        var a = actual.ToArray();
        Assert.Equal(e, a);
        for (int i = 0; i < e.Length; i++)
        {
            Equal(e[i].Epoch, a[i].Epoch);
        }
    }

    public static void Contains(TimeSystem.Time expected, IEnumerable<TimeSystem.Time> collection)
    {
        Assert.Contains(collection, x => x.DateTime.Equals(expected.DateTime) && Equals(x.Frame, expected.Frame));
    }

    public static void Contains(Window expected, IEnumerable<Window> collection)
    {
        Assert.Contains(collection, x => x.StartDate.DateTime.Equals(expected.StartDate.DateTime) && Equals(x.StartDate.Frame, expected.StartDate.Frame) &&
                                         x.EndDate.DateTime.Equals(expected.EndDate.DateTime) && Equals(x.EndDate.Frame, expected.EndDate.Frame) &&
                                         x.Length == expected.Length);
    }
}
