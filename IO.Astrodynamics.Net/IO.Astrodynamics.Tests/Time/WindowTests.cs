using System;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;
public class WindowTests
{
    [Fact]
    void CreateWindowFromDates()
    {
        Window w = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 03, TimeScale.TDB));
        LabelAssert.Equal(w.StartDate, new TimeSystem.Time(2021, 01, 02, TimeScale.TDB));
        LabelAssert.Equal(w.EndDate, new TimeSystem.Time(2021, 01, 03, TimeScale.TDB));
    }

    // R11 of #668 (decided by S. Guillet, 2026-11-19): reversed ends throw ArgumentException; before 11.0.0 they were
    // swapped silently
    [Fact]
    void CreateWindowFromReversedDates_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Window(new TimeSystem.Time(2021, 01, 03, TimeScale.TDB), new TimeSystem.Time(2021, 01, 02, TimeScale.TDB)));
        Assert.Equal("endDate", ex.ParamName);
    }

    // Ends in two scales are compared as instants: an end one tick before the start throws, whatever its scale
    [Fact]
    void CreateWindowFromReversedDatesInTwoScales_Throws()
    {
        var start = new TimeSystem.Time(2021, 01, 03, TimeScale.UTC);
        Assert.Throws<ArgumentException>(() => new Window(start, start.ToTDB().Add(TimeSpan.FromTicks(-1))));
    }

    // R11 of #668: a negative length throws ArgumentOutOfRangeException; before 11.0.0 the window ended before it started
    [Fact]
    void CreateWindowFromNegativeLength_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Window(new TimeSystem.Time(2021, 01, 03, TimeScale.TDB), TimeSpan.FromTicks(-1)));
        Assert.Equal("length", ex.ParamName);
    }

    // A window of zero length is accepted by both constructors
    [Fact]
    void CreateWindowOfZeroLength()
    {
        var t = new TimeSystem.Time(2021, 01, 03, TimeScale.TDB);
        var fromDates = new Window(t, t);
        var fromLength = new Window(t, TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, fromDates.Length);
        Assert.Equal(TimeSpan.Zero, fromLength.Length);
        LabelAssert.Equal(t, fromLength.EndDate);
    }

    // Each end keeps its scale (section A of the re-baseline of #668, R11): a UTC start and a TDB end stay as given, and a
    // window built from a length ends in the scale of its start
    [Fact]
    void EachEndKeepsItsScale()
    {
        var start = new TimeSystem.Time(2024, 03, 01, TimeScale.UTC);
        var end = new TimeSystem.Time(2024, 03, 01, 1, 0, 0, TimeScale.TDB);
        var window = new Window(start, end);
        Assert.Equal(TimeScale.UTC, window.StartDate.Scale);
        Assert.Equal(TimeScale.TDB, window.EndDate.Scale);
        LabelAssert.Equal(start, window.StartDate);
        LabelAssert.Equal(end, window.EndDate);
        Assert.Equal(TimeScale.UTC, new Window(start, TimeSpan.FromHours(1)).EndDate.Scale);
    }

    [Fact]
    void CreateWindowFromLength()
    {
        Window w = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), TimeSpan.FromDays(1.0));
        LabelAssert.Equal(w.StartDate, new TimeSystem.Time(2021, 01, 02, TimeScale.TDB));
        LabelAssert.Equal(w.EndDate, new TimeSystem.Time(2021, 01, 03, TimeScale.TDB));
    }

    [Fact]
    void MergeWindows()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Window w2 = new Window(new TimeSystem.Time(2021, 01, 04, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB));
        Window res = w1.Merge(w2);

        LabelAssert.Equal(res, new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB)));
    }

    [Fact]
    void WindowsIntersect()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Window w2 = new Window(new TimeSystem.Time(2021, 01, 04, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB));
        bool res = w1.Intersects(w2);
        Assert.True(res);
    }

    [Fact]
    void WindowsDontIntersect()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Window w2 = new Window(new TimeSystem.Time(2021, 01, 11, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB));
        bool res = w1.Intersects(w2);
        Assert.False(res);
    }

    [Fact]
    void DateTimeIntersect()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        TimeSystem.Time dt = new TimeSystem.Time(2021, 01, 10, TimeScale.TDB);
        bool res = w1.Intersects(dt);
        Assert.True(res);
    }

    [Fact]
    void DateTimeDoesntIntersect()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        TimeSystem.Time dt = new TimeSystem.Time(2021, 01, 11, TimeScale.TDB);
        bool res = w1.Intersects(dt);
        Assert.False(res);
    }

    [Fact]
    void WindowsGetIntersection()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Window w2 = new Window(new TimeSystem.Time(2021, 01, 04, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB));
        LabelAssert.Equal(w1.GetIntersection(w2), new Window(new TimeSystem.Time(2021, 01, 04, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB)));
    }
    
    [Fact]
    void WindowsGetIntersectionException()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Window w2 = new Window(new TimeSystem.Time(2021, 01, 14, TimeScale.TDB), new TimeSystem.Time(2021, 01, 22, TimeScale.TDB));
        Assert.Throws<ArgumentException>(()=>w1.GetIntersection(w2));
    }
    
    [Fact]
    void String()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Assert.Equal("From 2021-01-02T00:00:00.0000000 TDB to 2021-01-10T00:00:00.0000000 TDB - Length 8.00:00:00",w1.ToString());
    }
}