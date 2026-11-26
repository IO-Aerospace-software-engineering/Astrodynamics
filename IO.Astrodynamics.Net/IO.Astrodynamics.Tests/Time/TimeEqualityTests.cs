// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// Equality of <see cref="TimeSystem.Time"/> by instant (#512, decided by S. Guillet, 2026-11-17): two times are equal
/// when <see cref="TimeSystem.Time.CompareTo(TimeSystem.Time)"/> returns 0, the one order of times, by their TAI ticks
/// within a frame and across frames, and equal times have equal hash codes. The instants come from the definitions of
/// the scales: TAI − UTC = 37 s from 2017-01-01 (IERS Bulletin C 52), TT = TAI + 32.184 s, GPS = TAI − 19 s (IS-GPS-200);
/// TDB from the library's own series (<see cref="TimeScaleConversions.TdbMinusTt"/>), the scale whose TAI ticks are not
/// one to one with its own. Every expected value is a boolean or a count set by the contract of
/// <see cref="IEquatable{T}"/>, <see cref="object.GetHashCode"/> and <see cref="IComparable{T}"/>.
/// </summary>
public class TimeEqualityTests
{
    private static readonly TimeSystem.Time Utc = new(new DateTime(2025, 3, 28, 0, 0, 0), TimeScale.UTC);

    /// <summary>
    /// The instant of <see cref="Utc"/> in each frame, from the definitions of the scales.
    /// </summary>
    public static TheoryData<string> Frames => new() { "TAI", "TT", "GPS", "TDB" };

    private static TimeSystem.Time SameInstant(string frame) => frame switch
    {
        "TAI" => new TimeSystem.Time(new DateTime(2025, 3, 28, 0, 0, 37), TimeScale.TAI),
        "TT" => new TimeSystem.Time(new DateTime(2025, 3, 28, 0, 1, 9, 184), TimeScale.TT),
        "GPS" => new TimeSystem.Time(new DateTime(2025, 3, 28, 0, 0, 18), TimeScale.GPS),
        _ => Utc.ToTDB()
    };

    [Theory]
    [MemberData(nameof(Frames))]
    public void TheSameInstantInTwoFrames_IsEqual_WithOneHashCode(string frame)
    {
        // Arrange
        var other = SameInstant(frame);
        Assert.NotEqual(Utc.Scale, other.Scale);

        // Act and assert: the comparison, the equality and the hash code agree
        Assert.Equal(0, Utc.CompareTo(other));
        Assert.True(Utc == other);
        Assert.False(Utc != other);
        Assert.True(Utc.Equals(other));
        Assert.True(other.Equals(Utc));
        Assert.True(Utc.Equals((object)other));
        Assert.True(EqualityComparer<TimeSystem.Time>.Default.Equals(Utc, other));
        Assert.Equal(Utc.GetHashCode(), other.GetHashCode());

        // The collections keyed by hash find what the sorted ones find
        Assert.Contains(other, new HashSet<TimeSystem.Time> { Utc });
        Assert.Equal(1, new Dictionary<TimeSystem.Time, int> { [Utc] = 1 }[other]);
        Assert.True(new SortedSet<TimeSystem.Time> { Utc }.Contains(other));
        Assert.Single(new[] { Utc, other }.Distinct());
        Assert.Equal(0, new List<TimeSystem.Time> { Utc }.IndexOf(other));
    }

    [Theory]
    [MemberData(nameof(Frames))]
    public void AnotherInstant_IsNotEqual(string frame)
    {
        var other = SameInstant(frame).AddTicks(1);

        Assert.Equal(-1, Utc.CompareTo(other));
        Assert.False(Utc == other);
        Assert.True(Utc != other);
        Assert.False(Utc.Equals(other));
        Assert.DoesNotContain(other, new HashSet<TimeSystem.Time> { Utc });
    }

    /// <summary>
    /// Around the leap second of 2016-12-31: the last tick of 2016 UTC is TAI 2017-01-01T00:00:35.9999999, 2017-01-01 UTC
    /// is TAI 00:00:37, and the TAI second 00:00:36 between them, the UTC second 23:59:60, is no UTC time.
    /// </summary>
    [Fact]
    public void LeapSecondEdges()
    {
        var lastUtcOf2016 = new TimeSystem.Time(new DateTime(2016, 12, 31, 23, 59, 59).AddTicks(9_999_999), TimeScale.UTC);
        var firstUtcOf2017 = new TimeSystem.Time(new DateTime(2017, 1, 1), TimeScale.UTC);
        var taiBefore = new TimeSystem.Time(new DateTime(2017, 1, 1, 0, 0, 35).AddTicks(9_999_999), TimeScale.TAI);
        var taiAfter = new TimeSystem.Time(new DateTime(2017, 1, 1, 0, 0, 37), TimeScale.TAI);
        var leapSecond = new TimeSystem.Time(new DateTime(2017, 1, 1, 0, 0, 36), TimeScale.TAI);

        Assert.True(lastUtcOf2016 == taiBefore);
        Assert.Equal(lastUtcOf2016.GetHashCode(), taiBefore.GetHashCode());
        Assert.True(firstUtcOf2017 == taiAfter);
        Assert.Equal(firstUtcOf2017.GetHashCode(), taiAfter.GetHashCode());

        Assert.False(leapSecond == lastUtcOf2016);
        Assert.False(leapSecond == firstUtcOf2017);
        Assert.True(lastUtcOf2016 < leapSecond);
        Assert.True(leapSecond < firstUtcOf2017);
    }

    /// <summary>
    /// Two TDB ticks whose TAI tick is one: TDB − TT, rounded to the tick, steps up by one tick where TDB steps by one, so
    /// both TDB times are one instant of TAI. They are equal, as each is to that TAI time and to its UTC time, so equality
    /// stays transitive; their neighbours are not.
    /// </summary>
    [Fact]
    public void TwoTdbTicksOfOneTaiTick_AreEqual_AndEqualityIsTransitive()
    {
        // Arrange: the first TDB tick after 2025-01-04 where TDB − TT, rising, gains a tick (about every 300 s there)
        var (first, second) = TdbTicksOfOneTaiTick(new DateTime(2025, 1, 4));
        var tai = first.ToTAI();
        var utc = first.ToUTC();
        Assert.Equal(tai.DateTime, second.ToTAI().DateTime);
        Assert.Equal(1, second.DateTime.Ticks - first.DateTime.Ticks);

        // Act and assert: one instant, in three frames
        Assert.True(first == second);
        Assert.Equal(0, first.CompareTo(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first == tai && second == tai);
        Assert.True(first == utc && second == utc);
        Assert.Equal(tai.GetHashCode(), first.GetHashCode());
        Assert.Single(new[] { first, second, tai, utc }.Distinct());

        // The neighbours are other instants, in order
        var before = first.AddTicks(-1);
        var after = second.AddTicks(1);
        Assert.True(before < first);
        Assert.True(second < after);
        Assert.False(before == first);
        Assert.False(after == second);
        Assert.True(before < tai && tai < after);
    }

    /// <summary>
    /// Over random times in every frame, near one another, the comparison is the order of their TAI ticks, equality is a
    /// zero comparison, and equal times share a hash code.
    /// </summary>
    [Fact]
    public void CompareToEqualsAndHashCode_FollowTheTaiTicks()
    {
        var frames = new TimeScale[] { TimeScale.TDB, TimeScale.TT, TimeScale.TAI, TimeScale.GPS, TimeScale.UTC };
        var random = new Random(512);
        var start = new DateTime(1990, 1, 1).Ticks;
        var span = new DateTime(2040, 1, 1).Ticks - start;
        int equalPairs = 0;
        for (int i = 0; i < 200_000; i++)
        {
            // A TAI instant, read in two frames, the second moved by -2 to +2 ticks
            var taiTicks = start + (long)(random.NextDouble() * span);
            var tai = new TimeSystem.Time(new DateTime(taiTicks), TimeScale.TAI);
            var a = tai.ConvertTo(frames[random.Next(frames.Length)]);
            var b = tai.ConvertTo(frames[random.Next(frames.Length)]).AddTicks(random.Next(-2, 3));

            long ta = a.ToTAI().DateTime.Ticks;
            long tb = b.ToTAI().DateTime.Ticks;
            Assert.Equal(System.Math.Sign(ta.CompareTo(tb)), System.Math.Sign(a.CompareTo(b)));
            Assert.Equal(ta == tb, a == b);
            Assert.Equal(ta == tb, a.Equals(b));
            if (ta == tb)
            {
                equalPairs++;
                Assert.Equal(a.GetHashCode(), b.GetHashCode());
            }
        }

        Assert.True(equalPairs > 10_000, $"{equalPairs} equal pairs");
    }

    /// <summary>
    /// A window is its two ends: the same instants in UTC and in TDB are one window, with one hash code.
    /// </summary>
    [Fact]
    public void WindowsOfTheSameInstants_AreEqual()
    {
        var utc = new Window(Utc, Utc.AddDays(1.0));
        var tdb = new Window(Utc.ToTDB(), Utc.AddDays(1.0).ToTDB());

        Assert.True(utc == tdb);
        Assert.True(utc.Equals(tdb));
        Assert.Equal(utc.GetHashCode(), tdb.GetHashCode());
        Assert.False(utc == new Window(Utc, Utc.AddDays(2.0)));
    }

    /// <summary>
    /// A UTC window across a leap second, from its two ends or from its start and a length: the same ends, one window,
    /// whatever <see cref="Window.Length"/> each holds.
    /// </summary>
    [Fact]
    public void AWindowFromALength_EqualsTheWindowOfItsEnds()
    {
        var start = new TimeSystem.Time(new DateTime(2016, 12, 31, 23, 59, 0), TimeScale.UTC);
        var fromLength = new Window(start, TimeSpan.FromSeconds(120.0));
        var fromEnds = new Window(start, fromLength.EndDate);
        Assert.NotEqual(fromLength.Length, fromEnds.Length);

        Assert.True(fromLength == fromEnds);
        Assert.Equal(fromLength.GetHashCode(), fromEnds.GetHashCode());
    }

    /// <summary>
    /// The default time, 0001-01-01T00:00:00 TDB since 11.0.0 (D3.1: <see cref="TimeScale.TDB"/> is 0; before, a time
    /// without a frame), compares without a throw: equal to itself and to that TDB calendar, before every later time.
    /// </summary>
    [Fact]
    public void TheDefaultTime_ComparesWithoutAThrow()
    {
        var none = default(TimeSystem.Time);

        Assert.True(none == default(TimeSystem.Time));
        Assert.False(none == Utc);
        Assert.False(Utc.Equals(none));
        Assert.Equal(none.GetHashCode(), default(TimeSystem.Time).GetHashCode());

        // The order: the default time is the earliest TDB calendar, so that CompareTo returns 0 exactly when Equals holds
        Assert.Equal(0, none.CompareTo(default(TimeSystem.Time)));
        Assert.Equal(0, none.CompareTo((object)default(TimeSystem.Time)));
        Assert.Equal(-1, none.CompareTo(Utc));
        Assert.Equal(1, Utc.CompareTo(none));
        Assert.Equal(0, none.CompareTo(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB)));
        Assert.True(none.EqualsExact(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB)));
        Assert.True(none < Utc && Utc > none && none <= default(TimeSystem.Time) && none >= default(TimeSystem.Time));
        Assert.Equal(new[] { none, Utc }, new SortedSet<TimeSystem.Time> { Utc, none, default(TimeSystem.Time) });
    }

    /// <summary>
    /// The first pair of consecutive TDB ticks from <paramref name="from"/> (TDB) that convert to one TAI tick: TDB − TT,
    /// rounded to the tick, gains one where the TDB time gains one. Bisects on the rounded offset, which rises over the
    /// 600 s searched there.
    /// </summary>
    internal static (TimeSystem.Time First, TimeSystem.Time Second) TdbTicksOfOneTaiTick(DateTime from)
    {
        long Offset(long ticks) => new TimeSystem.Time(new DateTime(ticks), TimeScale.TDB).ToTAI().DateTime.Ticks - ticks;

        long lo = from.Ticks;
        long hi = lo + 600L * TimeSpan.TicksPerSecond;
        long offset = Offset(lo);
        Assert.True(Offset(hi) < offset, "TDB − TT rises over the window searched");
        while (hi - lo > 1)
        {
            long mid = lo + (hi - lo) / 2;
            if (Offset(mid) == offset) lo = mid;
            else hi = mid;
        }

        return (new TimeSystem.Time(new DateTime(lo), TimeScale.TDB), new TimeSystem.Time(new DateTime(hi), TimeScale.TDB));
    }
}
