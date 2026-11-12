// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The difference of two UTC times counts the leap seconds between them (#508). The IERS inserted one at the end of
/// 2016-12-31 (2016-12-31T23:59:60, TAI − UTC from 36 s to 37 s, Bulletin C 52, as in <c>latest_leapseconds.tls</c>):
/// 121 s elapse from 23:59:00 to 00:01:00 the next day, while the calendars differ by 120 s. Before #508 the difference
/// of two UTC times was that of their calendars.
/// </summary>
public class LeapSecondDifferenceTests
{
    private static readonly TimeSystem.Time BeforeLeap = new(2016, 12, 31, 23, 59, 0, frame: TimeFrame.UTCFrame);
    private static readonly TimeSystem.Time AfterLeap = new(2017, 1, 1, 0, 1, 0, frame: TimeFrame.UTCFrame);

    public LeapSecondDifferenceTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void TwoUtcTimes_AcrossTheLeapSecond_Differ_By121Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(121.0), AfterLeap - BeforeLeap);
        Assert.Equal(TimeSpan.FromSeconds(-121.0), BeforeLeap - AfterLeap);
    }

    [Fact]
    public void AUtcWindow_AcrossTheLeapSecond_Lasts121Seconds()
    {
        var window = new Window(BeforeLeap, AfterLeap);

        Assert.Equal(TimeSpan.FromSeconds(121.0), window.Length);
        Assert.Equal(TimeSpan.FromSeconds(121.0), new Window(AfterLeap, BeforeLeap).Length);
    }

    [Fact]
    public void TwoUtcTimes_WithoutALeapSecond_DifferByTheirCalendars()
    {
        var start = new TimeSystem.Time(2016, 12, 30, 23, 59, 0, frame: TimeFrame.UTCFrame);
        var end = new TimeSystem.Time(2016, 12, 31, 0, 1, 0, frame: TimeFrame.UTCFrame);

        Assert.Equal(TimeSpan.FromSeconds(120.0), end - start);
        Assert.Equal(new TimeSpan(new DateTime(2017, 6, 1, 0, 0, 0, 1).Ticks - new DateTime(2017, 1, 1).Ticks),
            new TimeSystem.Time(new DateTime(2017, 6, 1, 0, 0, 0, 1), TimeFrame.UTCFrame) -
            new TimeSystem.Time(new DateTime(2017, 1, 1), TimeFrame.UTCFrame));
    }

    [Fact]
    public void TheDifference_IsTheSameInEveryFrameThatCountsTheElapsedSeconds()
    {
        // TAI, TT and GPS are uniform, and the instants differ by whole ticks in them: the same 121 s, exactly
        Assert.Equal(AfterLeap.ToTAI() - BeforeLeap.ToTAI(), AfterLeap - BeforeLeap);
        Assert.Equal(AfterLeap.ToTDT() - BeforeLeap.ToTDT(), AfterLeap - BeforeLeap);
        Assert.Equal(AfterLeap.ToGPS() - BeforeLeap.ToGPS(), AfterLeap - BeforeLeap);
        Assert.Equal(AfterLeap.ToTAI() - BeforeLeap, AfterLeap - BeforeLeap);
    }

    [Fact]
    public void TwoLocalTimes_AcrossTheLeapSecond_Differ_By121Seconds()
    {
        // Local time is UTC moved by the zone: its calendar skips the leap second too
        Assert.Equal(TimeSpan.FromSeconds(121.0), AfterLeap.ToLocal() - BeforeLeap.ToLocal());
    }

    [Fact]
    public void TwoTdbTimes_DifferByTheirCalendars()
    {
        // TDB has no leap second: the difference of its calendar, the expression before #508
        var start = new TimeSystem.Time(2016, 12, 31, 23, 59, 0);
        var end = new TimeSystem.Time(2017, 1, 1, 0, 1, 0);

        Assert.Equal(end.DateTime - start.DateTime, end - start);
    }

    [Fact]
    public void TheReferenceOfTheLeapSeconds_HoldsTheSameLeapSecond()
    {
        // The leap seconds of the reference of the elapsed-time tests, against the kernel's through the framework
        Assert.Equal(DynamicalTimeReference.TaiMinusUtc(AfterLeap.DateTime) - DynamicalTimeReference.TaiMinusUtc(BeforeLeap.DateTime),
            (AfterLeap.ToTAI().DateTime - AfterLeap.DateTime - (BeforeLeap.ToTAI().DateTime - BeforeLeap.DateTime)).TotalSeconds);
        Assert.Equal(121.0, DynamicalTimeReference.TtSecondsBetween(BeforeLeap, AfterLeap));
    }
}
