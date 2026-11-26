// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The range guards of the angular thresholds of the window searches (R11 of #668, lot L6): a threshold of a comparison
/// operator outside the range of its angle, or NaN, throws <see cref="ArgumentOutOfRangeException"/> before any search, on
/// the managed and the CSPICE paths, the raw <see cref="SpiceApi"/> searches, <see cref="Site.FindDayWindows"/> and the
/// twilight of <see cref="Maneuver.Launch"/>; the four extremes, whose value is not a threshold, and the lengths are not
/// checked; each range is closed, its bounds accepted.
/// </summary>
public class WindowSearchAngleGuardTests
{
    private static readonly Window SearchWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(755957100.0));
    private static readonly TimeSpan Step = TimeSpan.FromHours(1.0);
    private const double Tiny = 1.0e-9;

    public WindowSearchAngleGuardTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody Earth => new(PlanetsAndMoons.EARTH);
    private static CelestialBody Moon => new(PlanetsAndMoons.MOON);
    private static Site SiteFromCoordinates => new(99, "GUARDSITE", Earth, new Planetodetic(-116.79 * IO.Astrodynamics.Constants.Deg2Rad, 35.25 * IO.Astrodynamics.Constants.Deg2Rad, 1000.0));

    public static TheoryData<CoordinateSystem, Coordinate, double> OutOfRange => new()
    {
        { CoordinateSystem.RaDec, Coordinate.RightAscension, -Tiny },
        { CoordinateSystem.RaDec, Coordinate.RightAscension, IO.Astrodynamics.Constants.TwoPi + Tiny },
        { CoordinateSystem.RaDec, Coordinate.RightAscension, 360.0 },
        { CoordinateSystem.RaDec, Coordinate.Declination, -IO.Astrodynamics.Constants.HalfPi - Tiny },
        { CoordinateSystem.RaDec, Coordinate.Declination, IO.Astrodynamics.Constants.HalfPi + Tiny },
        { CoordinateSystem.RaDec, Coordinate.Declination, double.NaN },
        { CoordinateSystem.Geodetic, Coordinate.Latitude, -IO.Astrodynamics.Constants.HalfPi - Tiny },
        { CoordinateSystem.Geodetic, Coordinate.Latitude, IO.Astrodynamics.Constants.HalfPi + Tiny },
        { CoordinateSystem.Geodetic, Coordinate.Longitude, -System.Math.PI - Tiny },
        { CoordinateSystem.Geodetic, Coordinate.Longitude, System.Math.PI + Tiny },
        { CoordinateSystem.Planetographic, Coordinate.Latitude, -IO.Astrodynamics.Constants.HalfPi - Tiny },
        { CoordinateSystem.Planetographic, Coordinate.Latitude, IO.Astrodynamics.Constants.HalfPi + Tiny },
        { CoordinateSystem.Planetographic, Coordinate.Longitude, -Tiny },
        { CoordinateSystem.Planetographic, Coordinate.Longitude, IO.Astrodynamics.Constants.TwoPi + Tiny }
    };

    public static TheoryData<CoordinateSystem, Coordinate, double> Bounds => new()
    {
        { CoordinateSystem.RaDec, Coordinate.RightAscension, 0.0 },
        { CoordinateSystem.RaDec, Coordinate.RightAscension, IO.Astrodynamics.Constants.TwoPi },
        { CoordinateSystem.RaDec, Coordinate.Declination, -IO.Astrodynamics.Constants.HalfPi },
        { CoordinateSystem.RaDec, Coordinate.Declination, IO.Astrodynamics.Constants.HalfPi },
        { CoordinateSystem.Geodetic, Coordinate.Latitude, IO.Astrodynamics.Constants.HalfPi },
        { CoordinateSystem.Geodetic, Coordinate.Longitude, -System.Math.PI },
        { CoordinateSystem.Geodetic, Coordinate.Longitude, System.Math.PI },
        { CoordinateSystem.Planetographic, Coordinate.Latitude, -IO.Astrodynamics.Constants.HalfPi },
        { CoordinateSystem.Planetographic, Coordinate.Longitude, IO.Astrodynamics.Constants.TwoPi }
    };

    private static readonly RelationalOperator[] Comparisons =
    [
        RelationalOperator.GreaterThan, RelationalOperator.LessThan, RelationalOperator.Equal, RelationalOperator.NotEqual,
        RelationalOperator.GreaterThanOrEqual, RelationalOperator.LessThanOrEqual
    ];

    private static readonly RelationalOperator[] Extremes =
    [
        RelationalOperator.AbsoluteMin, RelationalOperator.AbsoluteMax, RelationalOperator.LocalMin, RelationalOperator.LocalMax
    ];

    private static void AssertGuard(Action search, string parameter)
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(search);
        Assert.Equal(parameter, e.ParamName);
        Assert.Contains("rad", e.Message);
    }

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Coordinate_OutOfRange_Throws_OnEveryPath(CoordinateSystem system, Coordinate coordinate, double value)
    {
        var earth = Earth;
        var site = SiteFromCoordinates;
        foreach (var relation in Comparisons)
        {
            // The managed search of a celestial item and of a site
            AssertGuard(() => earth.FindCoordinateWindows(SearchWindow, Moon, earth.Frame, system, coordinate, relation, value, new WindowSearchOptions { Step = Step }), "value");
            AssertGuard(() => Moon.FindCoordinateWindows(SearchWindow, site, Frames.Frame.ICRF, system, coordinate, relation, value, new WindowSearchOptions { Step = Step }), "value");
            // The raw search of CSPICE
            AssertGuard(() => SpiceApi.Instance.FindWindowsOnCoordinateConstraint(SearchWindow, 399, 301, earth.Frame, system, coordinate, relation,
                value, 0.0, Aberration.None, Step), "value");
        }
    }

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Coordinate_OutOfRange_Throws_OnTheCspicePathOfAKernelSpacecraft(CoordinateSystem system, Coordinate coordinate, double value)
    {
        TestKernels.EnsureLro();
        var lro = new Spacecraft(-85, "LRO", epoch: SearchWindow.StartDate);
        var moon = Moon;
        AssertGuard(() => moon.FindCoordinateWindows(SearchWindow, lro, moon.Frame, system, coordinate, RelationalOperator.GreaterThan, value, new WindowSearchOptions { Step = Step }), "value");
    }

    [Theory]
    [MemberData(nameof(Bounds))]
    public void Coordinate_AtTheBounds_Searches(CoordinateSystem system, Coordinate coordinate, double value)
    {
        var earth = Earth;
        // A closed range: the bound is a threshold, as gfposc_c takes it
        Assert.NotNull(earth.FindCoordinateWindows(SearchWindow, Moon, earth.Frame, system, coordinate, RelationalOperator.GreaterThan, value, new WindowSearchOptions { Step = Step }));
    }

    [Fact]
    public void Coordinate_Extremes_AndLengths_NotChecked()
    {
        var earth = Earth;
        // The value of an extreme is no threshold; a length has no range
        foreach (var relation in Extremes)
        {
            Assert.NotNull(earth.FindCoordinateWindows(SearchWindow, Moon, earth.Frame, CoordinateSystem.RaDec, Coordinate.Declination, relation, 10.0, new WindowSearchOptions { Step = Step }));
        }

        Assert.Empty(earth.FindCoordinateWindows(SearchWindow, Moon, earth.Frame, CoordinateSystem.RaDec, Coordinate.Range, RelationalOperator.GreaterThan, 1.0e12, new WindowSearchOptions { Step = Step }));
        Assert.Empty(earth.FindCoordinateWindows(SearchWindow, Moon, earth.Frame, CoordinateSystem.Rectangular, Coordinate.Z, RelationalOperator.GreaterThan, 1.0e12, new WindowSearchOptions { Step = Step }));
    }

    [Theory]
    [InlineData(-Tiny)]
    [InlineData(System.Math.PI + Tiny)]
    [InlineData(100.0)]
    [InlineData(double.NaN)]
    public void Illumination_OutOfRange_Throws(double value)
    {
        var site = SiteFromCoordinates;
        var earth = Earth;
        foreach (var relation in Comparisons)
        {
            AssertGuard(() => site.FindIlluminationWindows(SearchWindow, IlluminationAngle.Incidence, relation, value, TestHelpers.Sun, new WindowSearchOptions { Step = Step }), "value");
            foreach (var angle in new[] { IlluminationAngle.Incidence, IlluminationAngle.Emission, IlluminationAngle.Phase })
            {
                AssertGuard(() => SpiceApi.Instance.FindWindowsOnIlluminationConstraint(SearchWindow, 301, 399, earth.Frame, site.Planetodetic, angle, relation,
                    value, 0.0, Aberration.None, Step, 10), "value");
            }
        }
    }

    [Fact]
    public void Illumination_AtTheBounds_AndExtremes_Search()
    {
        var site = SiteFromCoordinates;
        Assert.NotNull(site.FindIlluminationWindows(SearchWindow, IlluminationAngle.Incidence, RelationalOperator.LessThan, 0.0, TestHelpers.Sun, new WindowSearchOptions { Step = Step }));
        Assert.NotNull(site.FindIlluminationWindows(SearchWindow, IlluminationAngle.Incidence, RelationalOperator.LessThan, System.Math.PI, TestHelpers.Sun, new WindowSearchOptions { Step = Step }));
        Assert.NotEmpty(site.FindIlluminationWindows(SearchWindow, IlluminationAngle.Incidence, RelationalOperator.AbsoluteMin, 100.0, TestHelpers.Sun, new WindowSearchOptions { Step = Step }));
    }

    [Theory]
    [InlineData(-IO.Astrodynamics.Constants.HalfPi - Tiny)]
    [InlineData(IO.Astrodynamics.Constants.HalfPi + Tiny)]
    [InlineData(6.0)]
    [InlineData(double.NaN)]
    public void Twilight_OutOfRange_Throws(double twilight)
    {
        var site = SiteFromCoordinates;
        AssertGuard(() => site.FindDayWindows(SearchWindow, twilight), "twilight");
        var launchSite = new LaunchSite(98, "GUARDLAUNCH", Earth, site.Planetodetic, new AzimuthRange(0.0, IO.Astrodynamics.Constants.TwoPi));
        var target = new KeplerianElements(6.8e6, 0.0, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 1.0, 0.0, 0.0, Earth, SearchWindow.StartDate, Frames.Frame.ICRF);
        AssertGuard(() => _ = new Maneuver.Launch(launchSite, site, target, twilight, true), "twilight");
    }

    [Fact]
    public void Twilight_AtTheBounds_Searches()
    {
        var site = SiteFromCoordinates;
        Assert.Empty(site.FindDayWindows(SearchWindow, -IO.Astrodynamics.Constants.HalfPi));
        Assert.Equal(SearchWindow, site.FindDayWindows(SearchWindow, IO.Astrodynamics.Constants.HalfPi).Single());
    }
}
