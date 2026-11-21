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

    [Fact]
    void CreateWindowFromReversedDates()
    {
        Window w = new Window(new TimeSystem.Time(2021, 01, 03, TimeScale.TDB), new TimeSystem.Time(2021, 01, 02, TimeScale.TDB));
        LabelAssert.Equal(w.StartDate, new TimeSystem.Time(2021, 01, 02, TimeScale.TDB));
        LabelAssert.Equal(w.EndDate, new TimeSystem.Time(2021, 01, 03, TimeScale.TDB));
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