// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The CSPICE window searches past 1000 windows (#653): before 11.0.0 each native proxy copied the first 1000 windows of
/// its result and dropped the rest without an error, <c>NotEqual</c> then ending with one window across every dropped
/// crossing, and the result cell of <c>gfilum_c</c> held 1000 windows, so an illumination search of more failed
/// (<c>SPICE(WINDOWEXCESS)</c>). Every search now returns every window its native cell holds, 10000.
/// <para>
/// Two references: on every platform, the same search run over four parts of the window, each under 1000 windows (what the
/// library returned in full before), joined at the seams (a window that ends at a seam joined with the one that starts
/// there), an absolute extreme with an adjustment by the relation it stands for (<c>&gt;</c> the maximum less the
/// adjustment, <c>&lt;</c> the minimum plus it), <c>NotEqual</c> by the complement of the joined <c>=</c>; and, where the
/// test calls CSPICE (Validation, Linux), CSPICE's search itself with a result cell of 20000 windows, equal to the tick.
/// </para>
/// </summary>
public class CspiceWindowCapacityTests
{
    // The joined parts against the whole, both at CSPICE's convergence tolerance of 1e-6 s on other grids: twice the worst on
    // Linux x64 (4.000e-7 s, the X crossings of the Moon and NotEqual, 2026-11-17), rounded up at the second significant
    // digit; provisional, to be set again from the Windows x64 and macOS arm64 CI logs
    private const double JoinedThreshold = 8.0e-7;

    // The Moon's X in IAU_EARTH from the Earth crosses 0 about twice a day: 2126 crossings over 1100 days (#653)
    private static readonly Window CoordinateWindow = new(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1100.0));

    // LRO's SPK of the tests, 91 days: 1121 orbits of about 2 h (gfdist LOCMIN), 1121 passes behind the Moon from the Sun
    private static readonly Window LroWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(763732800.0));

    private const double LroMoonDistance = 1815148.0;

    private readonly ITestOutputHelper _output;

    public CspiceWindowCapacityTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        TestKernels.EnsureLro();
    }

    private static IEnumerable<Window> Coordinate(Window window, RelationnalOperator relation, double value, double adjust = 0.0) =>
        SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(window, 399, 301, new Frames.Frame("IAU_EARTH"), CoordinateSystem.Rectangular,
            IO.Astrodynamics.Coordinate.X, relation, value, adjust, Aberration.None, TimeSpan.FromHours(1.0));

    private static IEnumerable<Window> Distance(Window window, RelationnalOperator relation, double value) =>
        SpiceAPI.Instance.FindWindowsOnDistanceConstraint(window, 301, -85, relation, value, Aberration.None, TimeSpan.FromSeconds(60.0));

    private static IEnumerable<Window> Illumination(Window window, RelationnalOperator relation) =>
        SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(window, 301, 399, new Frames.Frame("IAU_EARTH"), new Planetodetic(0.0, 0.0, 0.0),
            IlluminationAngle.Incidence, relation, System.Math.PI / 2.0, 0.0, Aberration.None, TimeSpan.FromHours(1.0), 10);

    private static IEnumerable<Window> Occultation(Window window) =>
        SpiceAPI.Instance.FindWindowsOnOccultationConstraint(window, -85, 10, ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid, OccultationType.Any,
            Aberration.None, TimeSpan.FromSeconds(300.0));

    public static TheoryData<string, RelationnalOperator, int> CoordinateRelations => new()
    {
        { "=", RelationnalOperator.Equal, 2126 },
        { ">", RelationnalOperator.Greater, 1064 },
        { "<", RelationnalOperator.Lower, 1063 },
        { ">=", RelationnalOperator.GreaterOrEqual, 1064 },
        { "<=", RelationnalOperator.LowerOrEqual, 1063 },
        { "LOCMAX", RelationnalOperator.LocalMax, 1062 },
        { "LOCMIN", RelationnalOperator.LocalMin, 1063 },
        { "!=", RelationnalOperator.NotEqual, 2127 }
    };

    [Theory]
    [MemberData(nameof(CoordinateRelations))]
    public void Coordinate_PastAThousandWindows_EveryWindow(string label, RelationnalOperator relation, int count)
    {
        var whole = Coordinate(CoordinateWindow, relation, 0.0).ToArray();
        var joined = relation == RelationnalOperator.NotEqual
            ? Complement(Joined(CoordinateWindow, w => Coordinate(w, RelationnalOperator.Equal, 0.0)), CoordinateWindow)
            : Joined(CoordinateWindow, w => Coordinate(w, relation, 0.0));
        Compare($"coordinate {label}", whole, joined, count);
    }

    [Theory]
    [InlineData(RelationnalOperator.AbsoluteMax)]
    [InlineData(RelationnalOperator.AbsoluteMin)]
    public void Coordinate_AbsoluteExtremeWithAnAdjustment_PastAThousandWindows_EveryWindow(RelationnalOperator relation)
    {
        // An adjustment of 1e5 km: every window where X is within 1e5 km of its absolute extreme, about one per day. The
        // reference is the relation it stands for, at the extreme of the search without an adjustment (one window)
        const double adjust = 1.0e5;
        double extreme = Coordinate(CoordinateWindow, relation, 0.0).Single().StartDate.TimeSpanFromJ2000().TotalSeconds;
        double x = SpiceAPI.Instance.ReadEphemeris(TimeSystem.Time.CreateTDB(extreme), 399, 301, new Frames.Frame("IAU_EARTH"), Aberration.None)
            .ToStateVector().Position.X / 1000.0;
        var whole = Coordinate(CoordinateWindow, relation, 0.0, adjust).ToArray();
        var joined = relation == RelationnalOperator.AbsoluteMax
            ? Joined(CoordinateWindow, w => Coordinate(w, RelationnalOperator.Greater, x - adjust))
            : Joined(CoordinateWindow, w => Coordinate(w, RelationnalOperator.Lower, x + adjust));
        Compare($"coordinate {relation} adjust 1e5 km", whole, joined, relation == RelationnalOperator.AbsoluteMax ? 1062 : 1063);
    }

    [Theory]
    [InlineData(RelationnalOperator.Equal, 1904)]
    [InlineData(RelationnalOperator.NotEqual, 1905)]
    [InlineData(RelationnalOperator.LocalMin, 1121)]
    [InlineData(RelationnalOperator.LocalMax, 1121)]
    public void Distance_PastAThousandWindows_EveryWindow(RelationnalOperator relation, int count)
    {
        var whole = Distance(LroWindow, relation, LroMoonDistance).ToArray();
        var joined = relation == RelationnalOperator.NotEqual
            ? Complement(Joined(LroWindow, w => Distance(w, RelationnalOperator.Equal, LroMoonDistance)), LroWindow)
            : Joined(LroWindow, w => Distance(w, relation, LroMoonDistance));
        Compare($"distance {relation}", whole, joined, count);
    }

    [Fact]
    public void Illumination_PastAThousandWindows_EveryWindow()
    {
        // The night at the point (0°, 0°) of the Earth's ellipsoid, one window a day: 1100 windows, more than the 1000 of
        // gfilum_c's former cell (SPICE(WINDOWEXCESS) before 11.0.0)
        var whole = Illumination(CoordinateWindow, RelationnalOperator.Greater).ToArray();
        var joined = Joined(CoordinateWindow, w => Illumination(w, RelationnalOperator.Greater));
        Compare("illumination >", whole, joined, 1100);
    }

    [Fact]
    public void Occultation_PastAThousandWindows_EveryWindow()
    {
        var whole = Occultation(LroWindow).ToArray();
        var joined = Joined(LroWindow, Occultation);
        Compare("occultation any", whole, joined, 1121);
    }

    [Fact]
    public void PastTheNativeCell_ThrowsSpicesError()
    {
        // More windows than the 10000 of the native cell: the X crossings of the Moon over 6000 days, 11594 (gfposc_c with a
        // larger cell): SPICE(WINDOWEXCESS), with its message, never a partial list
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(6000.0));
        var exception = Assert.Throws<InvalidOperationException>(() => Coordinate(window, RelationnalOperator.Equal, 0.0).ToArray());
        Assert.Contains("Cannot insert an additional interval into the window", exception.Message);
    }

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(CoordinateRelations))]
    public void Coordinate_PastAThousandWindows_EqualsGfposc(string label, RelationnalOperator relation, int count)
    {
        var whole = Coordinate(CoordinateWindow, relation, 0.0).ToArray();
        var reference = relation == RelationnalOperator.NotEqual
            ? Complement(Gfposc("=", 0.0), CoordinateWindow)
            : Gfposc(label is ">=" ? ">" : label is "<=" ? "<" : label, 0.0);
        Assert.Equal(count, reference.Length);
        Assert.Equal(reference, whole);
    }

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData(RelationnalOperator.AbsoluteMax, "ABSMAX", 1062)]
    [InlineData(RelationnalOperator.AbsoluteMin, "ABSMIN", 1063)]
    public void Coordinate_AbsoluteExtremeWithAnAdjustment_EqualsGfposc(RelationnalOperator relation, string spiceRelation, int count)
    {
        var whole = Coordinate(CoordinateWindow, relation, 0.0, 1.0e5).ToArray();
        var reference = Gfposc(spiceRelation, 1.0e5);
        Assert.Equal(count, reference.Length);
        Assert.Equal(reference, whole);
    }

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData(RelationnalOperator.Equal, "=", 1904)]
    [InlineData(RelationnalOperator.LocalMin, "LOCMIN", 1121)]
    [InlineData(RelationnalOperator.LocalMax, "LOCMAX", 1121)]
    public void Distance_PastAThousandWindows_EqualsGfdist(RelationnalOperator relation, string spiceRelation, int count)
    {
        var whole = Distance(LroWindow, relation, LroMoonDistance).ToArray();
        var reference = ToWindows(CspiceDirect.Gfdist(Seconds(LroWindow.StartDate), Seconds(LroWindow.EndDate), "-85", "NONE", "301",
            spiceRelation, LroMoonDistance / 1000.0, 0.0, 60.0), LroWindow);
        Assert.Equal(count, reference.Length);
        Assert.Equal(reference, whole);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void IlluminationAndOccultation_PastAThousandWindows_EqualGfilumAndGfoclt()
    {
        // The point as the native layer builds it: georec_c at (0, 0, 0) on the equatorial radius in km, read in m and scaled
        double radius = SpiceAPI.Instance.GetCelestialBodyInfo(399).Radii.X * 0.001;
        var point = CspiceDirect.Georec(0.0, 0.0, 0.0, radius, 0.0);
        var illumination = ToWindows(CspiceDirect.Gfilum(Seconds(CoordinateWindow.StartDate), Seconds(CoordinateWindow.EndDate), "INCIDENCE",
            "399", "10", "IAU_EARTH", "NONE", "301", point, ">", System.Math.PI / 2.0, 0.0, 3600.0), CoordinateWindow);
        Assert.Equal(1100, illumination.Length);
        Assert.Equal(illumination, Illumination(CoordinateWindow, RelationnalOperator.Greater).ToArray());

        var occultation = ToWindows(CspiceDirect.Gfoclt(Seconds(LroWindow.StartDate), Seconds(LroWindow.EndDate), "ANY", "301", "ELLIPSOID",
            SpiceAPI.Instance.GetCelestialBodyInfo(301).FrameName, "10", "ELLIPSOID", SpiceAPI.Instance.GetCelestialBodyInfo(10).FrameName, "NONE",
            "-85", 300.0), LroWindow);
        Assert.Equal(1121, occultation.Length);
        Assert.Equal(occultation, Occultation(LroWindow).ToArray());
    }

    private static Window[] Gfposc(string relation, double adjust) =>
        ToWindows(CspiceDirect.Gfposc(Seconds(CoordinateWindow.StartDate), Seconds(CoordinateWindow.EndDate), "301", "IAU_EARTH", "NONE", "399",
            "RECTANGULAR", "X", relation, 0.0, adjust, 3600.0), CoordinateWindow);

    private static double Seconds(TimeSystem.Time time) => time.ToTDB().TimeSpanFromJ2000().TotalSeconds;

    // CSPICE's windows labelled as SpiceAPI labels them: an end at an end of the search window is that end, a transition
    // inside the nearest tick of its seconds
    private static Window[] ToWindows(double[][] windows, Window searchWindow)
    {
        var start = searchWindow.StartDate.ToTDB();
        var end = searchWindow.EndDate.ToTDB();
        return windows.Select(w => new Window(Label(w[0]), Label(w[1]))).ToArray();

        TimeSystem.Time Label(double seconds) =>
            seconds <= Seconds(start) ? start : seconds >= Seconds(end) ? end : TimeSystem.Time.CreateTDB(seconds);
    }

    // The search over four equal parts of the window, joined: a window that ends at a seam and the one that starts there are
    // one window
    private static Window[] Joined(Window window, Func<Window, IEnumerable<Window>> search)
    {
        var start = window.StartDate.ToTDB();
        long part = (window.EndDate.ToTDB() - start).Ticks / 4;
        var windows = new List<Window>();
        for (int i = 0; i < 4; i++)
        {
            var partStart = start + TimeSpan.FromTicks(i * part);
            var partEnd = i == 3 ? window.EndDate.ToTDB() : start + TimeSpan.FromTicks((i + 1) * part);
            var found = search(new Window(partStart, partEnd)).ToArray();
            Assert.True(found.Length < 1000, $"{found.Length} windows in a part");
            foreach (var w in found)
            {
                if (windows.Count > 0 && windows[^1].EndDate == partStart && w.StartDate == partStart && w.StartDate != w.EndDate)
                {
                    windows[^1] = new Window(windows[^1].StartDate, w.EndDate);
                    continue;
                }

                windows.Add(w);
            }
        }

        return windows.ToArray();
    }

    // The search window less the windows, which are sorted and disjoint, as SpiceAPI forms NotEqual
    private static Window[] Complement(IEnumerable<Window> windows, Window searchWindow)
    {
        var cursor = searchWindow.StartDate.ToTDB();
        var result = new List<Window>();
        foreach (var w in windows)
        {
            if (w.StartDate > cursor) result.Add(new Window(cursor, w.StartDate));
            if (w.EndDate > cursor) cursor = w.EndDate;
        }

        if (searchWindow.EndDate.ToTDB() > cursor) result.Add(new Window(cursor, searchWindow.EndDate.ToTDB()));
        return result.ToArray();
    }

    private void Compare(string label, Window[] whole, Window[] joined, int count)
    {
        _output.WriteLine($"MEASURE #653 {label}: {whole.Length} windows, {joined.Length} joined");
        Assert.Equal(count, joined.Length);
        Assert.Equal(count, whole.Length);
        double worst = whole.Zip(joined, (a, b) => System.Math.Max(System.Math.Abs((a.StartDate - b.StartDate).TotalSeconds),
            System.Math.Abs((a.EndDate - b.EndDate).TotalSeconds))).Max();
        _output.WriteLine($"MEASURE #653 {label}, whole - joined parts: worst edge {worst:E3} s");
        Assert.True(worst < JoinedThreshold, $"{worst:E2} s");
    }
}
