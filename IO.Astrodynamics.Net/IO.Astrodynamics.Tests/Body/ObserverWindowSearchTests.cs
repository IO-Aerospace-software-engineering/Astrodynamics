// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The window searches on the observer (R15 of #668, lot L6, choice 1, decided by S. Guillet, 2026-11-19): each equals
/// the former search of the target with the observer as argument, bit for bit, on every path (CSPICE's <c>gfdist_c</c>,
/// <c>gfposc_c</c>, <c>gfoclt_c</c>, <c>gfilum_c</c>, and the library's) and participant (kernel spacecraft, kernel site,
/// site from coordinates, natural bodies), but the occultation of a site observer, which the library searches (choice
/// 4, IC-10: equal to the search of the target from the site); the horizontal coordinates against
/// <see cref="Site.GetHorizontalCoordinates"/>; and every setting that cannot apply throws before the search.
/// </summary>
public class ObserverWindowSearchTests
{
    private static readonly Window DistanceWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(755913900.0));
    private static readonly Window DayWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(755957100.0));
    private static readonly Window Eclipse001Window = new(TimeSystem.Time.Parse("2026-03-20T00:00:00 TDB"), TimeSystem.Time.Parse("2026-03-20T12:00:00 TDB"));
    private static readonly Window Eclipse2005Window = new(TimeSystem.Time.Parse("2005-10-03 00:00:00", TimeScale.TDB), TimeSystem.Time.Parse("2005-10-04 00:00:00", TimeScale.TDB));
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static readonly WindowSearchOptions Options = WindowSearchOptions.Default with { Step = Step };
    private static readonly Frames.Frame IauEarth = new("IAU_EARTH");

    public ObserverWindowSearchTests()
    {
        TestKernels.EnsureLro();
        TestKernels.EnsureOccultationReference();
    }

    private static Spacecraft Lro => new(-85, "LRO", epoch: DistanceWindow.StartDate);
    private static CelestialBody Moon => new(PlanetsAndMoons.MOON);
    private static CelestialBody Earth => new(PlanetsAndMoons.EARTH);
    private static CelestialBody Sun => new(Stars.Sun);
    private static Site Dss13 => new(13, "DSS-13", Earth);
    private static Site Dss13FromCoordinates => new(98, "DSS13COORDINATES", Earth, Dss13.Planetodetic);

    // The windows bit for bit, labels included; an empty reference would prove nothing
    private static void Same(System.Collections.Generic.IEnumerable<Window> expected, System.Collections.Generic.IEnumerable<Window> actual)
    {
        Assert.NotEmpty(expected);
        LabelAssert.Equal(expected, actual);
    }

    // The distance of the Moon from the Earth in the middle of the day: a threshold the day crosses
    private static double MoonDistanceAtMidday(CelestialBody moon) =>
        moon.GetEphemeris(DayWindow.StartDate + (DayWindow.EndDate - DayWindow.StartDate) / 2, Earth, Frames.Frame.ICRF, Aberration.None).Position.Magnitude();

    [Fact]
    public void Distance_EqualsTheTargetsSearch_RolesSwapped()
    {
        var lro = Lro;
        var moon = Moon;
        var dss13 = Dss13;
        var lt = Options with { Aberration = Aberration.LT };
        // gfdist_c: a body and a kernel spacecraft, both ways; a kernel spacecraft observing a kernel site
        Same(lro.FindWindowsOnDistanceConstraint(DistanceWindow, moon, RelationnalOperator.Greater, 1815148.0, Aberration.LT, Step),
            moon.FindDistanceWindows(DistanceWindow, lro, RelationnalOperator.Greater, 1815148.0, lt));
        Same(moon.FindWindowsOnDistanceConstraint(DistanceWindow, lro, RelationnalOperator.Lower, 1815148.0, Aberration.None, Step),
            lro.FindDistanceWindows(DistanceWindow, moon, RelationnalOperator.Lower, 1815148.0, Options));
        Same(dss13.FindWindowsOnDistanceConstraint(DistanceWindow, lro, RelationnalOperator.Greater, 3.72e8, Aberration.None, Step),
            lro.FindDistanceWindows(DistanceWindow, dss13, RelationnalOperator.Greater, 3.72e8, Options));
        // The library's search: a site observing a kernel spacecraft, two natural bodies
        Same(lro.FindWindowsOnDistanceConstraint(DistanceWindow, dss13, RelationnalOperator.Greater, 3.72e8, Aberration.LT, Step),
            dss13.FindDistanceWindows(DistanceWindow, lro, RelationnalOperator.Greater, 3.72e8, lt));
        double middle = MoonDistanceAtMidday(moon);
        Same(moon.FindWindowsOnDistanceConstraint(DayWindow, Earth, RelationnalOperator.Greater, middle, Aberration.None, Step),
            Earth.FindDistanceWindows(DayWindow, moon, RelationnalOperator.Greater, middle, Options));
        Assert.NotEmpty(dss13.FindDistanceWindows(DistanceWindow, lro, RelationnalOperator.Greater, 3.72e8, lt));
    }

    [Fact]
    public void Coordinate_EqualsTheTargetsSearch_RolesSwapped()
    {
        var lro = Lro;
        var moon = Moon;
        var dss13 = Dss13;
        // gfposc_c, the distances in m on both sides
        Same(lro.FindWindowsOnCoordinateConstraint(DistanceWindow, moon, Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z,
                RelationnalOperator.Greater, 500000.0, 0.0, Aberration.None, Step),
            moon.FindCoordinateWindows(DistanceWindow, lro, Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 500000.0,
                Options));
        Same(dss13.FindWindowsOnCoordinateConstraint(DistanceWindow, lro, Frames.Frame.ICRF, CoordinateSystem.RaDec, Coordinate.Range,
                RelationnalOperator.AbsoluteMax, 0.0, 1.0e6, Aberration.None, Step),
            lro.FindCoordinateWindows(DistanceWindow, dss13, Frames.Frame.ICRF, CoordinateSystem.RaDec, Coordinate.Range, RelationnalOperator.AbsoluteMax, 0.0,
                Options with { AdjustValue = 1.0e6 }));
        // The library's: a kernel site and a site from coordinates observing LRO, in their frames
        foreach (var site in new[] { dss13, Dss13FromCoordinates })
        {
            Same(lro.FindWindowsOnCoordinateConstraint(DistanceWindow, site, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
                    RelationnalOperator.Greater, 0.3, 0.0, Aberration.LT, Step),
                site.FindCoordinateWindows(DistanceWindow, lro, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.Greater, 0.3,
                    Options with { Aberration = Aberration.LT }));
        }

        // Two natural bodies, an absolute extreme with its adjustment value
        Same(moon.FindWindowsOnCoordinateConstraint(DayWindow, Earth, Frames.Frame.ICRF, CoordinateSystem.RaDec, Coordinate.Declination,
                RelationnalOperator.AbsoluteMin, 0.0, 1e-4, Aberration.None, Step),
            Earth.FindCoordinateWindows(DayWindow, moon, Frames.Frame.ICRF, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.AbsoluteMin, 0.0,
                Options with { AdjustValue = 1e-4 }));
    }

    [Fact]
    public void Occultation_EqualsTheTargetsSearch_RolesSwapped()
    {
        // gfoclt_c: a kernel spacecraft observer, the Sun behind the Earth's ellipsoid in IAU_EARTH (default shapes: both
        // have radii)
        var kernelSpacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        var sun = Sun;
        var earth = Earth;
        var options = OccultationSearchOptions.Default with { Step = Step, FrontFrame = IauEarth };
        foreach (var type in new[] { OccultationType.Any, OccultationType.Full, OccultationType.None })
        {
            Same(sun.FindWindowsOnOccultationConstraint(Eclipse001Window, kernelSpacecraft, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid, type,
                    Aberration.None, Step, new OccultationSearchOptions { FrontFrame = IauEarth }),
                kernelSpacecraft.FindOccultationWindows(Eclipse001Window, sun, earth, options with { OccultationType = type }));
        }

        // The library's: a site from the kernels and natural bodies, a point target
        var dss65 = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);
        Same(dss65.FindWindowsOnOccultationConstraint(Eclipse2005Window, TestHelpers.Sun, ShapeType.Point, TestHelpers.MoonAtJ2000,
                ShapeType.Ellipsoid, OccultationType.Full, Aberration.LT, TimeSpan.FromSeconds(360.0)),
            dss65.FindOccultationWindows(Eclipse2005Window, TestHelpers.Sun, TestHelpers.MoonAtJ2000, new OccultationSearchOptions
            {
                Step = TimeSpan.FromSeconds(360.0), TargetShape = ShapeType.Point, OccultationType = OccultationType.Full, Aberration = Aberration.LT
            }));

        // A site observing a kernel spacecraft: the library's search, which the search of the target from the site runs
        // (choice 4, IC-10); the site's former search ran gfoclt_c
        var lro = Lro;
        var dss13 = Dss13;
        var moon = Moon;
        var expected = lro.FindWindowsOnOccultationConstraint(DistanceWindow, dss13, ShapeType.Point, moon, ShapeType.Ellipsoid, OccultationType.Any,
            Aberration.LT, Step);
        Assert.NotEmpty(expected);
        Same(expected,
            dss13.FindOccultationWindows(DistanceWindow, lro, moon, OccultationSearchOptions.Default with { Step = Step, Aberration = Aberration.LT }));
    }

    [Fact]
    public void Illumination_EqualsTheFormerSiteSearch()
    {
        foreach (var site in new[] { Dss13, Dss13FromCoordinates })
        {
            Same(site.FindWindowsOnIlluminationConstraint(DayWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, 1.5, 0.0,
                    Aberration.LTS, Step, Sun),
                site.FindIlluminationWindows(DayWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, 1.5, Sun,
                    Options with { Aberration = Aberration.LTS }));
        }

        Assert.Throws<ArgumentException>(() => Dss13FromCoordinates.FindIlluminationWindows(DayWindow, IlluminationAngle.Emission, RelationnalOperator.Lower,
            1.5, Sun, Options));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dss13FromCoordinates.FindIlluminationWindows(DayWindow, IlluminationAngle.Incidence,
            RelationnalOperator.Lower, 85.0, Sun, Options));
    }

    [Fact]
    public void Horizontal_ElevationAndRange_EqualTheDeclinationAndRangeInTheSiteFrame()
    {
        var lro = Lro;
        var dss13 = Dss13;
        var o = Options with { Aberration = Aberration.LT };
        Same(lro.FindWindowsOnCoordinateConstraint(DistanceWindow, dss13, dss13.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
                RelationnalOperator.Greater, 0.2, 0.0, Aberration.LT, Step),
            dss13.FindHorizontalWindows(DistanceWindow, lro, HorizontalCoordinate.Elevation, RelationnalOperator.Greater, 0.2, o));
        Same(lro.FindWindowsOnCoordinateConstraint(DistanceWindow, dss13, dss13.Frame, CoordinateSystem.RaDec, Coordinate.Range,
                RelationnalOperator.Lower, 3.7e8, 0.0, Aberration.LT, Step),
            dss13.FindHorizontalWindows(DistanceWindow, lro, HorizontalCoordinate.Range, RelationnalOperator.Lower, 3.7e8, o));
        Assert.Throws<ArgumentOutOfRangeException>(() => dss13.FindHorizontalWindows(DistanceWindow, lro, HorizontalCoordinate.Elevation,
            RelationnalOperator.Greater, 10.0, o));
        Assert.Throws<ArgumentOutOfRangeException>(() => dss13.FindHorizontalWindows(DistanceWindow, lro, HorizontalCoordinate.Azimuth,
            RelationnalOperator.Greater, -0.1, o));
    }

    [Theory]
    [InlineData(RelationnalOperator.Lower, 1.0)]
    [InlineData(RelationnalOperator.Greater, 5.0)]
    [InlineData(RelationnalOperator.Greater, 1.0)]
    public void Horizontal_Azimuth_AgainstGetHorizontalCoordinates_AcrossTheNorth(RelationnalOperator relation, double azimuth)
    {
        // The Moon from a site over a day: its azimuth turns once, through the north, where it jumps from 2π to 0
        var site = Dss13FromCoordinates;
        var moon = Moon;
        var windows = site.FindHorizontalWindows(DayWindow, moon, HorizontalCoordinate.Azimuth, relation, azimuth, Options);
        Assert.NotEmpty(windows);
        double Azimuth(TimeSystem.Time t) => site.GetHorizontalCoordinates(t, moon, Aberration.None).Azimuth;
        bool Holds(TimeSystem.Time t) => relation == RelationnalOperator.Lower ? Azimuth(t) < azimuth : Azimuth(t) > azimuth;
        bool north = false;
        foreach (var w in windows)
        {
            Assert.True(Holds(w.StartDate + (w.EndDate - w.StartDate) / 2));
            foreach (var edge in new[] { w.StartDate, w.EndDate }.Where(e => e > DayWindow.StartDate && e < DayWindow.EndDate))
            {
                double before = Azimuth(edge.AddSeconds(-1.0));
                double after = Azimuth(edge.AddSeconds(1.0));
                if (System.Math.Abs(before - after) > System.Math.PI)
                {
                    // The crossing of the north, a jump of the azimuth from 2π to 0 within the second
                    north = true;
                }
                else
                {
                    Assert.Equal(azimuth, Azimuth(edge), 1e-6);
                }
            }
        }

        // Each gap between two windows fails the relation
        for (int i = 1; i < windows.Count; i++)
        {
            Assert.False(Holds(windows[i - 1].EndDate + (windows[i].StartDate - windows[i - 1].EndDate) / 2));
        }

        // Below a threshold a window opens at the north, above one it closes there
        Assert.True(north);
    }

    [Fact]
    public void GeometryFinder_FindWindows_EqualsTheInstanceSearch()
    {
        var moon = Moon;
        var earth = Earth;
        Func<TimeSystem.Time, double> distance = t => moon.GetEphemeris(t, earth, Frames.Frame.ICRF, Aberration.None).Position.Magnitude();
        double middle = MoonDistanceAtMidday(moon);
        Func<TimeSystem.Time, bool> far = t => distance(t) > middle;
        var finder = new GeometryFinder();
        Same(finder.FindWindowsWithCondition(DayWindow, distance, RelationnalOperator.Greater, middle, Step),
            GeometryFinder.FindWindows(DayWindow, distance, RelationnalOperator.Greater, middle, Options));
        Same(finder.FindWindowsWithCondition(DayWindow, distance, RelationnalOperator.AbsoluteMax, 0.0, 1000.0, Step),
            GeometryFinder.FindWindows(DayWindow, distance, RelationnalOperator.AbsoluteMax, 0.0, Options with { AdjustValue = 1000.0 }));
        Same(finder.FindWindowsWithCondition(DayWindow, far, RelationnalOperator.Equal, true, Step),
            GeometryFinder.FindWindows(DayWindow, far, RelationnalOperator.Equal, true, Options));
    }

    [Fact]
    public void ConvergenceTolerance_AppliesToTheLibrarysSearch()
    {
        // A crossing of a closed form, sin(2π t / 1 h) > 0.3 over a day: located to the tolerance given
        Func<TimeSystem.Time, double> f = t => System.Math.Sin(IO.Astrodynamics.Constants.TwoPi * (t - DayWindow.StartDate).TotalSeconds / 3600.0);
        double exact = System.Math.Asin(0.3) / IO.Astrodynamics.Constants.TwoPi * 3600.0;
        var fine = GeometryFinder.FindWindows(DayWindow, f, RelationnalOperator.Greater, 0.3, Options);
        var coarse = GeometryFinder.FindWindows(DayWindow, f, RelationnalOperator.Greater, 0.3, Options with { ConvergenceTolerance = TimeSpan.FromSeconds(1.0) });
        Assert.Equal(fine.Count, coarse.Count);
        double fineError = System.Math.Abs((fine[0].StartDate - DayWindow.StartDate).TotalSeconds - exact);
        double coarseError = System.Math.Abs((coarse[0].StartDate - DayWindow.StartDate).TotalSeconds - exact);
        Assert.True(fineError < 1e-6, $"{fineError}");
        Assert.True(coarseError < 1.0, $"{coarseError}");
        Assert.NotEqual(fine[0].StartDate, coarse[0].StartDate);

        // The managed occultation search too: within the tolerance of the default
        var dss65 = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);
        var o = new OccultationSearchOptions { Step = TimeSpan.FromSeconds(360.0) };
        var occFine = dss65.FindOccultationWindows(Eclipse2005Window, TestHelpers.Sun, TestHelpers.MoonAtJ2000, o);
        var occCoarse = dss65.FindOccultationWindows(Eclipse2005Window, TestHelpers.Sun, TestHelpers.MoonAtJ2000, o with { ConvergenceTolerance = TimeSpan.FromSeconds(0.1) });
        Assert.Equal(occFine.Count, occCoarse.Count);
        Assert.All(occFine.Zip(occCoarse), p => Assert.True(System.Math.Abs((p.First.StartDate - p.Second.StartDate).TotalSeconds) <= 0.1));
    }

    [Fact]
    public void MissingStep_Throws()
    {
        var lro = Lro;
        var moon = Moon;
        void AssertStep(Action search, string record)
        {
            var e = Assert.Throws<ArgumentException>(search);
            Assert.Equal("options", e.ParamName);
            Assert.Contains($"{record}.Step", e.Message);
        }

        AssertStep(() => moon.FindDistanceWindows(DistanceWindow, lro, RelationnalOperator.Greater, 1.0), "WindowSearchOptions");
        AssertStep(() => moon.FindCoordinateWindows(DistanceWindow, lro, Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater, 1.0), "WindowSearchOptions");
        AssertStep(() => Dss13.FindHorizontalWindows(DistanceWindow, lro, HorizontalCoordinate.Elevation, RelationnalOperator.Greater, 0.1),
            "WindowSearchOptions");
        AssertStep(() => Dss13.FindIlluminationWindows(DistanceWindow, IlluminationAngle.Incidence, RelationnalOperator.Greater, 0.1, Sun),
            "WindowSearchOptions");
        AssertStep(() => GeometryFinder.FindWindows(DistanceWindow, _ => 1.0, RelationnalOperator.Greater, 0.0), "WindowSearchOptions");
        AssertStep(() => GeometryFinder.FindWindows(DistanceWindow, _ => true, RelationnalOperator.Equal, true), "WindowSearchOptions");
        AssertStep(() => Earth.FindOccultationWindows(DistanceWindow, Sun, moon), "OccultationSearchOptions");
    }

    [Fact]
    public void SettingsThatCannotApply_Throw()
    {
        var lro = Lro;
        var moon = Moon;
        var tolerance = Options with { ConvergenceTolerance = TimeSpan.FromTicks(5) };
        void AssertOptions(Action search, string property)
        {
            var e = Assert.Throws<ArgumentException>(search);
            Assert.Equal("options", e.ParamName);
            Assert.Contains(property, e.Message);
        }

        // A tolerance on a search CSPICE runs
        AssertOptions(() => moon.FindDistanceWindows(DistanceWindow, lro, RelationnalOperator.Greater, 1.0, tolerance), "ConvergenceTolerance");
        AssertOptions(() => moon.FindCoordinateWindows(DistanceWindow, lro, Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater, 1.0, tolerance), "ConvergenceTolerance");
        AssertOptions(() => Dss13.FindIlluminationWindows(DistanceWindow, IlluminationAngle.Incidence, RelationnalOperator.Greater, 0.1, Sun, tolerance),
            "ConvergenceTolerance");
        var kernelSpacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        AssertOptions(() => kernelSpacecraft.FindOccultationWindows(Eclipse001Window, Sun, Earth,
            new OccultationSearchOptions { Step = Step, ConvergenceTolerance = TimeSpan.FromTicks(5) }), "ConvergenceTolerance");

        // An adjustment value off an absolute extreme, on gfdist_c, and on a static search an aberration
        AssertOptions(() => Earth.FindDistanceWindows(DayWindow, moon, RelationnalOperator.Greater, 1.0, Options with { AdjustValue = 0.0 }), "AdjustValue");
        AssertOptions(() => moon.FindDistanceWindows(DistanceWindow, lro, RelationnalOperator.AbsoluteMin, 0.0, Options with { AdjustValue = 0.0 }),
            "AdjustValue");
        AssertOptions(() => GeometryFinder.FindWindows(DayWindow, _ => true, RelationnalOperator.Equal, true, Options with { AdjustValue = 1.0 }),
            "AdjustValue");
        AssertOptions(() => GeometryFinder.FindWindows(DayWindow, _ => 1.0, RelationnalOperator.Greater, 0.0, Options with { Aberration = Aberration.LT }),
            "Aberration");

        // A frame SPICE does not know: the options, SPICE's error inside
        var unknown = Assert.Throws<ArgumentException>(() => kernelSpacecraft.FindOccultationWindows(Eclipse001Window, Sun, Earth,
            new OccultationSearchOptions { Step = Step, FrontFrame = new Frames.Frame("NOT_A_FRAME_668") }));
        Assert.Equal("options", unknown.ParamName);
        Assert.Contains("FrontFrame", unknown.Message);
        Assert.IsType<SpiceException>(unknown.InnerException);

        // The managed distance accepts an adjustment value of an absolute extreme
        Assert.Single(Earth.FindDistanceWindows(DayWindow, moon, RelationnalOperator.AbsoluteMin, 0.0, Options with { AdjustValue = 0.0 }));
    }
}
