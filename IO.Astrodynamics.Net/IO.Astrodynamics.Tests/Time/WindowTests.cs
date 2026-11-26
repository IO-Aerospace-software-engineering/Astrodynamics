using System;
using System.Linq;
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
    void HullOfWindows()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Window w2 = new Window(new TimeSystem.Time(2021, 01, 04, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB));
        Window res = w1.Hull(w2);

        LabelAssert.Equal(res, new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 12, TimeScale.TDB)));
    }

    // R16 of #668 (decided by S. Guillet, 2026-11-19): Hull is the removed Merge, bit for bit: the earlier start and the
    // later end, compared strictly, so on a tie of instants the argument's end is taken, in its scale (here the TDB label
    // of an instant of the UTC window, at both ends, and the hull of two disjoint windows of two scales)
    [Fact]
    void HullIsTheRemovedMergeBitForBit()
    {
        var a = new TimeSystem.Time(2016, 12, 31, 23, 59, 0, TimeScale.UTC);
        var b = new TimeSystem.Time(2017, 01, 01, 0, 1, 0, TimeScale.UTC);
        var utc = new Window(a, b);
        var tdbTie = new Window(a.ToTDB(), b.ToTDB());
        var tdbInside = new Window(a.ToTDB().Add(TimeSpan.FromSeconds(10)), b.ToTDB());
        var tdbLater = new Window(b.ToTDB().Add(TimeSpan.FromHours(1)), b.ToTDB().Add(TimeSpan.FromHours(2)));
        Assert.Equal(a, a.ToTDB());
        foreach (var (w1, w2) in new[] { (utc, tdbTie), (tdbTie, utc), (utc, tdbInside), (tdbInside, utc), (utc, tdbLater), (tdbLater, utc) })
        {
            // The body of the removed Window.Merge (10.2.0-preview-1 to lot L5c of 11.0.0)
            var min = w1.StartDate < w2.StartDate ? w1.StartDate : w2.StartDate;
            var max = w1.EndDate > w2.EndDate ? w1.EndDate : w2.EndDate;
            LabelAssert.Equal(new Window(min, max), w1.Hull(w2));
        }

        Assert.Equal(TimeScale.TDB, utc.Hull(tdbTie).StartDate.Scale);
        Assert.Equal(TimeScale.TDB, utc.Hull(tdbTie).EndDate.Scale);
        Assert.Equal(TimeScale.UTC, tdbTie.Hull(utc).StartDate.Scale);
    }

    // TryMerge gives the hull of two windows that share an instant, touching ones included (as CSPICE's wninsd merges
    // [0, 1] and [1, 2] into [0, 2]), and false with the default window for disjoint ones
    [Fact]
    void TryMergeOfWindows()
    {
        var t0 = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        var w1 = new Window(t0, TimeSpan.FromHours(2));
        var overlapping = new Window(t0.AddHours(1), TimeSpan.FromHours(2));
        var touching = new Window(t0.AddHours(2), TimeSpan.FromHours(1));
        var disjoint = new Window(t0.AddHours(2).Add(TimeSpan.FromTicks(1)), TimeSpan.FromHours(1));

        Assert.True(w1.TryMerge(overlapping, out var merged));
        LabelAssert.Equal(w1.Hull(overlapping), merged);
        Assert.True(w1.TryMerge(touching, out merged));
        LabelAssert.Equal(new Window(t0, t0.AddHours(3)), merged);
        Assert.False(w1.TryMerge(disjoint, out merged));
        Assert.Equal(default, merged);
    }

    // TryGetIntersection gives GetIntersection's window when the windows share an instant, one instant for touching
    // windows (CSPICE's wnintd of [0, 1] and [1, 2] is [1, 1]), and false where GetIntersection throws
    [Fact]
    void TryGetIntersectionOfWindows()
    {
        var t0 = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        var w1 = new Window(t0, TimeSpan.FromHours(2));
        var overlapping = new Window(t0.AddHours(1), TimeSpan.FromHours(2));
        var touching = new Window(t0.AddHours(2), TimeSpan.FromHours(1));
        var disjoint = new Window(t0.AddHours(2).Add(TimeSpan.FromTicks(1)), TimeSpan.FromHours(1));

        Assert.True(w1.TryGetIntersection(overlapping, out var intersection));
        LabelAssert.Equal(w1.GetIntersection(overlapping), intersection);
        LabelAssert.Equal(new Window(t0.AddHours(1), t0.AddHours(2)), intersection);
        Assert.True(w1.TryGetIntersection(touching, out intersection));
        LabelAssert.Equal(new Window(t0.AddHours(2), t0.AddHours(2)), intersection);
        Assert.False(w1.TryGetIntersection(disjoint, out intersection));
        Assert.Equal(default, intersection);
    }

    // R16 of #668: Merge and Intersects(Time) are removed outright (no forwarder, the amendment of 2026-11-19), replaced by
    // Hull and Contains
    [Fact]
    void MergeAndIntersectsOfATimeAreRemoved()
    {
        Assert.Null(typeof(Window).GetMethod("Merge"));
        Assert.Null(typeof(Window).GetMethod("Intersects", new[] { typeof(TimeSystem.Time) }));
        Assert.NotNull(typeof(Window).GetMethod("Intersects", new[] { typeof(Window) }));
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
    void ContainsATime()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Assert.True(w1.Contains(new TimeSystem.Time(2021, 01, 10, TimeScale.TDB)));
        Assert.True(w1.Contains(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB)));
        Assert.True(w1.Contains(new TimeSystem.Time(2021, 01, 05, TimeScale.TDB)));
    }

    [Fact]
    void DoesNotContainATime()
    {
        Window w1 = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), new TimeSystem.Time(2021, 01, 10, TimeScale.TDB));
        Assert.False(w1.Contains(new TimeSystem.Time(2021, 01, 11, TimeScale.TDB)));
        Assert.False(w1.Contains(new TimeSystem.Time(2021, 01, 10, TimeScale.TDB).Add(TimeSpan.FromTicks(1))));
        Assert.False(w1.Contains(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB).Add(TimeSpan.FromTicks(-1))));
    }

    // The ends are instants: a window of UTC ends contains the TDB labels of its ends, and a zero-length window its instant
    [Fact]
    void ContainsTheInstantsOfItsEndsInAnotherScale()
    {
        var start = new TimeSystem.Time(2024, 03, 01, TimeScale.UTC);
        var w = new Window(start, TimeSpan.FromHours(1));
        Assert.True(w.Contains(start.ToTDB()));
        Assert.True(w.Contains(w.EndDate.ToTDB()));
        Assert.True(new Window(start, start).Contains(start.ToTDB()));
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

    // Window.Sample (R11 of #668, decision 9): start + i·step in the scale of the start, compared with the end as instants
    private static void LabelsEqual(TimeSystem.Time[] expected, TimeSystem.Time[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            LabelAssert.Equal(expected[i], actual[i]);
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    void Sample_StepNotPositive_ThrowsAtTheCall(long ticks)
    {
        var w = new Window(new TimeSystem.Time(2021, 01, 02, TimeScale.TDB), TimeSpan.FromHours(1));
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => w.Sample(TimeSpan.FromTicks(ticks)));
        Assert.Equal("step", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Sample(TimeSpan.FromTicks(ticks), includeEnd: true));
    }

    // The end is a point when it lies on the grid, with or without includeEnd
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void Sample_EndOnTheGrid_IsTheLastPoint(bool includeEnd)
    {
        var start = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        var w = new Window(start, TimeSpan.FromHours(1));
        var points = w.Sample(TimeSpan.FromMinutes(20), includeEnd).ToArray();
        LabelsEqual([start, start.AddMinutes(20), start.AddMinutes(40), start.AddMinutes(60)], points);
    }

    // An end off the grid: the points before it, then the end itself with includeEnd, however close it is (no merge)
    [Fact]
    void Sample_EndOffTheGrid_AppendedWithIncludeEndOnly()
    {
        var start = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        var w = new Window(start, TimeSpan.FromMinutes(40) + TimeSpan.FromTicks(1));
        LabelsEqual([start, start.AddMinutes(20), start.AddMinutes(40)], w.Sample(TimeSpan.FromMinutes(20)).ToArray());
        LabelsEqual([start, start.AddMinutes(20), start.AddMinutes(40), w.EndDate],
            w.Sample(TimeSpan.FromMinutes(20), includeEnd: true).ToArray());
    }

    // A zero-length window is its start; a step longer than the window gives the start, then the end with includeEnd
    [Fact]
    void Sample_ShortWindows()
    {
        var start = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        LabelsEqual([start], new Window(start, start).Sample(TimeSpan.FromSeconds(1)).ToArray());
        LabelsEqual([start], new Window(start, start).Sample(TimeSpan.FromSeconds(1), includeEnd: true).ToArray());
        var w = new Window(start, TimeSpan.FromSeconds(10));
        LabelsEqual([start], w.Sample(TimeSpan.FromDays(1)).ToArray());
        LabelsEqual([start, w.EndDate], w.Sample(TimeSpan.FromDays(1), includeEnd: true).ToArray());
        // A step past the calendar's last tick ends the points instead of throwing
        LabelsEqual([start, w.EndDate], w.Sample(TimeSpan.MaxValue, includeEnd: true).ToArray());
    }

    // Across the leap second of 2016-12-31 the points are the UTC calendar's, start + i·step, and none lies past the end:
    // the window lasts 121 s (Length) for 120 s of calendar
    [Fact]
    void Sample_AcrossALeapSecond_StaysInTheWindow()
    {
        var start = new TimeSystem.Time(2016, 12, 31, 23, 59, 0, TimeScale.UTC);
        var end = new TimeSystem.Time(2017, 01, 01, 0, 1, 0, TimeScale.UTC);
        var w = new Window(start, end);
        Assert.Equal(TimeSpan.FromSeconds(121), w.Length);
        var points = w.Sample(TimeSpan.FromSeconds(1)).ToArray();
        Assert.Equal(121, points.Length);
        LabelAssert.Equal(end, points[^1]);
        LabelsEqual([start, start.Add(TimeSpan.FromSeconds(30)), new TimeSystem.Time(2017, 01, 01, TimeScale.UTC),
                new TimeSystem.Time(2017, 01, 01, 0, 0, 30, TimeScale.UTC), end],
            w.Sample(TimeSpan.FromSeconds(30), includeEnd: true).ToArray());
    }

    // The points take the scale of the start and are compared with an end of another scale as instants: the end in UTC of
    // the instant start + 1 h TDB is that point, not appended again
    [Fact]
    void Sample_EndInAnotherScale_ComparedAsAnInstant()
    {
        var start = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        var end = start.AddHours(1).ToUTC();
        var w = new Window(start, end);
        var points = w.Sample(TimeSpan.FromMinutes(30), includeEnd: true).ToArray();
        LabelsEqual([start, start.AddMinutes(30), start.AddMinutes(60)], points);
        var later = new Window(start, end.Add(TimeSpan.FromTicks(1)));
        var withEnd = later.Sample(TimeSpan.FromMinutes(30), includeEnd: true).ToArray();
        Assert.Equal(4, withEnd.Length);
        LabelAssert.Equal(later.EndDate, withEnd[^1]);
    }

    // Lazy: the points are computed as they are enumerated
    [Fact]
    void Sample_IsLazy()
    {
        var start = new TimeSystem.Time(2021, 01, 02, TimeScale.TDB);
        var w = new Window(start, new TimeSystem.Time(9999, 12, 31, TimeScale.TDB));
        LabelsEqual([start, start.Add(TimeSpan.FromTicks(1))], w.Sample(TimeSpan.FromTicks(1)).Take(2).ToArray());
    }
}