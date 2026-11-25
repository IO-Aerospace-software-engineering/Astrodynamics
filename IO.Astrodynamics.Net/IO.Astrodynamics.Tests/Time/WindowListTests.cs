// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The window-list algebra of R16 of #668 (decided by S. Guillet, 2026-11-19): <see cref="WindowList"/> against CSPICE's
/// <c>wnintd</c>, <c>wnunid</c>, <c>wndifd</c> and <c>wncomd</c>, and the ends of each result, which are ends of the
/// inputs with their labels.
/// </summary>
public class WindowListTests
{
    /// <summary>
    /// The references of tools/window_list_reference/window_list_reference.py, as it prints them (CSPICE_N0067, spiceypy 8.4.0: wncomd past its right endpoint in 1 of 50 cases): per case,
    /// the lists A and B, then wnintd(A, B), wnunid(A, B), wndifd(A, B) and the complement of A within [2, 10], each a
    /// list of "start end" pairs in whole seconds from the epoch, separated by ';'.
    /// </summary>
    private static readonly string[] References =
    {
        "0 4|2 6|2 4|0 6|0 2|4 10",
        "0 1|1 2|1 1|0 2|0 1|2 10",
        "0 2|1 1|1 1|0 2|0 2|2 10",
        "1 1|1 1|1 1|1 1||2 10",
        "3 3;5 5|5 8|5 5|3 3;5 8|3 3|2 10",
        "0 10|2 3;5 7|2 3;5 7|0 10|0 2;3 5;7 10|",
        "|1 2||1 2||2 10",
        "6 8;0 2;1 3|4 5;2 2|2 2|0 3;4 5;6 8|0 3;6 8|3 6;8 10",
        "0 3;3 6;6 9|3 3;6 6|3 3;6 6|0 9|0 9|9 10",
        "0 1;11 14|12 13|12 13|0 1;11 14|0 1;11 12;13 14|2 10",
        "1 1;6 6;0 0|0 3;2 4;10 11;2 4|0 0;1 1|0 4;6 6;10 11|6 6|2 10",
        "10 12|1 4;3 6;9 11;7 9;1 4|10 11|1 6;7 12|11 12|2 10",
        "9 10;1 1;11 12|4 5;3 4||1 1;3 5;9 10;11 12|1 1;9 10;11 12|2 9",
        "9 11;7 8;5 5;8 8;3 5|10 11;1 2;9 9;2 5;1 3|3 5;9 9;10 11|1 5;7 8;9 11|7 8;9 10|2 3;5 7;8 9",
        "4 6;4 6;2 2|11 11;2 5;0 1;11 11|2 2;4 5|0 1;2 6;11 11|5 6|2 4;6 10",
        "|11 13||11 13||2 10",
        "9 11;0 1;9 11;0 3;12 14|||0 3;9 11;12 14|0 3;9 11;12 14|3 9",
        "8 8;10 12|4 6;11 11;4 4;10 12;1 2|10 12|1 2;4 6;8 8;10 12|8 8|2 10",
        "5 6;9 9;11 12|5 5;8 8;10 12;3 4;8 10|5 5;9 9;11 12|3 4;5 6;8 12|5 6|2 5;6 10",
        "3 4;4 5;1 1;10 10|4 5;3 4;2 2;7 8|3 5|1 1;2 2;3 5;7 8;10 10|1 1;10 10|2 3;5 10",
        "12 15;12 12;6 8;6 9;7 9|1 3;4 7|6 7|1 3;4 9;12 15|7 9;12 15|2 6;9 10",
        "|5 5||5 5||2 10",
        "5 5;8 8;5 5;12 12;4 4|4 7;0 0|4 4;5 5|0 0;4 7;8 8;12 12|8 8;12 12|2 10",
        "2 3|10 12;9 9;6 9;8 10;4 5||2 3;4 5;6 12|2 3|3 10",
        "8 8;5 8;2 3;1 4|11 13;2 2;8 11;9 12;9 10|2 2;8 8|1 4;5 13|1 4;5 8|4 5;8 10",
        "|7 10;9 9;3 3;10 11;10 10||3 3;7 11||2 10",
        "8 8;1 3;10 12;11 14|9 9;6 9;3 3;3 4|3 3;8 8|1 4;6 9;10 14|1 3;10 14|3 10",
        "3 6|5 7|5 6|3 7|3 5|2 3;6 10",
        "11 11;3 3;6 8;4 4;5 7|10 10;4 5|4 4;5 5|3 3;4 8;10 10;11 11|3 3;5 8;11 11|2 5;8 10",
        "4 4|0 1;6 6;12 13||0 1;4 4;6 6;12 13|4 4|2 10",
        "12 12;2 2;8 10;1 2;9 10|0 3|1 2|0 3;8 10;12 12|8 10;12 12|2 8",
        "3 5;1 3;11 11;12 13;5 5|3 3;10 10;3 5|3 5|1 5;10 10;11 11;12 13|1 3;11 11;12 13|5 10",
        "|||||2 10",
        "4 4;1 4;2 3|||1 4|1 4|4 10",
        "0 2;6 7|5 5;4 4;5 5||0 2;4 4;5 5;6 7|0 2;6 7|2 6;7 10",
        "|5 6;3 6;5 8||3 8||2 10",
        "5 8;5 5;10 11;6 6;3 3|11 12;2 3;3 4|3 3;11 11|2 4;5 8;10 12|5 8;10 11|2 5;8 10",
        "1 3;10 12;10 10|2 3;3 4;4 5;6 7;1 3|1 3|1 5;6 7;10 12|10 12|3 10",
        "1 4;12 15;9 9|||1 4;9 9;12 15|1 4;9 9;12 15|4 10",
        "7 7;8 9|6 6;10 11;4 4;1 1||1 1;4 4;6 6;7 7;8 9;10 11|7 7;8 9|2 8;9 10",
        "4 6;9 11;10 11;11 11|7 10|9 10|4 6;7 11|4 6;10 11|2 4;6 9",
        "|8 8;5 7;10 13||5 7;8 8;10 13||2 10",
        "1 1;3 4;6 6;2 3|10 13;4 4|4 4|1 1;2 4;6 6;10 13|1 1;2 4;6 6|4 10",
        "6 6;3 3|4 7;0 0;0 2;12 14|6 6|0 2;3 3;4 7;12 14|3 3|2 10",
        "5 5;12 12;9 9|4 7;2 4;8 8;2 5|5 5|2 7;8 8;9 9;12 12|9 9;12 12|2 10",
        "12 12;3 6;5 6;8 11|3 3;8 8|3 3;8 8|3 6;8 11;12 12|3 6;8 11;12 12|2 3;6 8",
        "|6 6;0 3;2 2||0 3;6 6||2 10",
        "|||||2 10",
        "0 3;0 0;11 13;3 4|3 5;11 12|3 4;11 12|0 5;11 13|0 3;12 13|4 10",
        "3 5;0 0;11 11;5 8|9 9;0 2|0 0|0 2;3 8;9 9;11 11|3 8;11 11|2 3;8 10",
    };

    public static IEnumerable<object[]> Cases => References.Select(r => new object[] { r });

    // The epoch of the cases, a whole UTC second whose TDB label is the same instant (asserted for each end)
    private static readonly TimeSystem.Time Epoch = new(2024, 03, 01, 12, 0, 0, TimeScale.UTC);

    private static List<Window> Parse(string text, bool tdb)
    {
        return text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(pair =>
        {
            var seconds = pair.Split(' ').Select(double.Parse).ToArray();
            var start = Epoch.AddSeconds(seconds[0]);
            var end = Epoch.AddSeconds(seconds[1]);
            if (!tdb) return new Window(start, end);
            Assert.True(start.ToTDB() == start && end.ToTDB() == end);
            return new Window(start.ToTDB(), end.ToTDB());
        }).ToList();
    }

    // A result equals the reference, instant for instant, and each of its ends is an end of an input, label for label
    private static void AssertResult(IEnumerable<Window> expected, IReadOnlyList<Window> actual, params IEnumerable<Window>[] inputs)
    {
        Assert.Equal(expected.ToArray(), actual.ToArray());
        var ends = inputs.SelectMany(l => l).SelectMany(w => new[] { w.StartDate, w.EndDate }).ToList();
        foreach (var w in actual)
        {
            Assert.Contains(ends, e => e.EqualsExact(w.StartDate));
            Assert.Contains(ends, e => e.EqualsExact(w.EndDate));
        }
    }

    // A in UTC, B in TDB: the instants are those of the reference, whatever the scales
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheAlgebra_IsCspicesWindowAlgebra(string reference)
    {
        var f = reference.Split('|');
        var a = Parse(f[0], false);
        var b = Parse(f[1], true);
        var within = new Window(Epoch.AddSeconds(2.0).ToTDB(), Epoch.AddSeconds(10.0).ToTDB());

        AssertResult(Parse(f[2], false), a.IntersectWindows(b), a, b);
        AssertResult(Parse(f[3], false), a.UnionWindows(b), a, b);
        AssertResult(Parse(f[4], false), a.ExceptWindows(b), a, b);
        AssertResult(Parse(f[5], false), a.ComplementWithin(within), a, new[] { within });
        Assert.Equal(Parse(f[3], false).Sum(w => (w.EndDate - w.StartDate).Ticks), a.UnionWindows(b).TotalDuration().Ticks);
    }

    // On a tie of instants an end keeps the label of the first window that holds it: the receiver's before the argument's
    [Fact]
    public void OnATieTheReceiversLabelIsKept()
    {
        var utc = new Window(Epoch, Epoch.AddHours(1));
        var tdb = new Window(Epoch.ToTDB(), Epoch.AddHours(1).ToTDB());

        Assert.True(new[] { utc }.UnionWindows(new[] { tdb }).Single().StartDate.EqualsExact(utc.StartDate));
        Assert.True(new[] { utc }.UnionWindows(new[] { tdb }).Single().EndDate.EqualsExact(utc.EndDate));
        Assert.True(new[] { tdb }.UnionWindows(new[] { utc }).Single().StartDate.EqualsExact(tdb.StartDate));
        Assert.True(new[] { utc }.IntersectWindows(new[] { tdb }).Single().StartDate.EqualsExact(utc.StartDate));
        Assert.True(new[] { tdb }.IntersectWindows(new[] { utc }).Single().EndDate.EqualsExact(tdb.EndDate));
    }

    // Two consecutive TDB ticks of one TAI tick are one instant (#512): windows that end and start there touch
    [Fact]
    public void WindowsThatMeetOnTwoTdbTicksOfOneInstant_Touch()
    {
        var (first, second) = TimeEqualityTests.TdbTicksOfOneTaiTick(new DateTime(2025, 1, 4));
        var before = new Window(first.AddSeconds(-60.0), first);
        var after = new Window(second, second.AddSeconds(60.0));

        var union = new[] { before }.UnionWindows(new[] { after });
        Assert.Single(union);
        Assert.True(union[0].StartDate.EqualsExact(before.StartDate) && union[0].EndDate.EqualsExact(after.EndDate));
        var intersection = Assert.Single(new[] { before }.IntersectWindows(new[] { after }));
        Assert.True(intersection.StartDate == intersection.EndDate);
        Assert.Single(new[] { before }.ExceptWindows(new[] { after }));
    }

    // TotalDuration counts the leap seconds between UTC ends (#508), over the union: overlaps count once
    [Fact]
    public void TotalDuration_CountsTheUnionAndTheLeapSeconds()
    {
        var start = new TimeSystem.Time(2016, 12, 31, 23, 59, 0, TimeScale.UTC);
        var end = new TimeSystem.Time(2017, 01, 01, 0, 1, 0, TimeScale.UTC);
        var windows = new[] { new Window(start, end), new Window(start.AddSeconds(30.0), end), new Window(end, end) };

        Assert.Equal(TimeSpan.FromSeconds(121.0), windows.TotalDuration());
        Assert.Equal(TimeSpan.Zero, Array.Empty<Window>().TotalDuration());
    }

    // A result is a list a caller can index and count, sorted and disjoint whatever the order of the input
    [Fact]
    public void AResult_IsSortedAndDisjoint()
    {
        var w1 = new Window(Epoch.AddHours(3), TimeSpan.FromHours(1));
        var w2 = new Window(Epoch, TimeSpan.FromHours(1));
        var w3 = new Window(Epoch.AddMinutes(30), TimeSpan.FromHours(1));

        var union = new[] { w1, w2 }.UnionWindows(new[] { w3 });
        Assert.Equal(new[] { new Window(Epoch, Epoch.AddMinutes(90)), w1 }, union);
    }

    [Fact]
    public void ANullList_Throws()
    {
        IEnumerable<Window> none = null;
        var some = new[] { new Window(Epoch, TimeSpan.FromHours(1)) };
        Assert.Equal("windows", Assert.Throws<ArgumentNullException>(() => none.UnionWindows(some)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => some.IntersectWindows(none)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => some.ExceptWindows(none)).ParamName);
        Assert.Equal("windows", Assert.Throws<ArgumentNullException>(() => none.ComplementWithin(some[0])).ParamName);
        Assert.Equal("windows", Assert.Throws<ArgumentNullException>(() => none.TotalDuration()).ParamName);
    }
}
