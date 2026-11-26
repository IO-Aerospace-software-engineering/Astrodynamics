// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Planetodetic = IO.Astrodynamics.Coordinates.Planetodetic;
using Site = IO.Astrodynamics.Surface.Site;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The operators CSPICE has not, <see cref="RelationalOperator.GreaterThanOrEqual"/>, <see cref="RelationalOperator.LessThanOrEqual"/>
/// and <see cref="RelationalOperator.NotEqual"/>, on the CSPICE path of the window searches (#482): before, the native
/// parser of the relation threw a C++ exception across P/Invoke and the process ended (exit code 134).
/// </summary>
/// <remarks>
/// <para>
/// The expected windows are those of CSPICE N0067 through spiceypy, <c>tools/geometry_reference/operators_reference.py</c>:
/// <c>gfdist</c>, <c>gfposc</c> and <c>gfilum</c> with <c>&gt;</c> for <see cref="RelationalOperator.GreaterThanOrEqual"/>,
/// <c>&lt;</c> for <see cref="RelationalOperator.LessThanOrEqual"/>, and for <see cref="RelationalOperator.NotEqual"/> the
/// search window less the instants of <c>=</c>, a window from each crossing to the next (the managed search's
/// definition), each end the nearest tick of its ET (#625). Compared as the strings of their epochs, as
/// <see cref="SpiceApiTest"/> compares the windows of the same searches.
/// </para>
/// <para>
/// The paths: the three <see cref="SpiceApi"/> searches; <see cref="WindowSearches.FindCoordinateWindows"/> of the Moon
/// observing a kernel spacecraft (LRO) and of LRO observing a kernel site (DSS-13), on the LRO SPK of the tests.
/// <see cref="WindowSearches.FindDistanceWindows"/> and <see cref="Site.FindIlluminationWindows"/> on their CSPICE path are checked against their own
/// <see cref="RelationalOperator.GreaterThan"/>, <see cref="RelationalOperator.LessThan"/> and <see cref="RelationalOperator.Equal"/>
/// searches, CSPICE's: the threshold of the first is in m, as the library reads it since #466, and the surface point
/// of the second in m, its altitude converted to km since #488.
/// </para>
/// </remarks>
public class SpiceOperatorTests
{
    // 2023-12-15 00:00:51 to 12:00:51 UTC, inside the LRO SPK, a step of 60 s (#482's setup on whole TDB seconds)
    private static readonly Window LroWindow = new(TimeSystem.Time.CreateTDB(755870520.0), TimeSystem.Time.CreateTDB(755913720.0));
    private static readonly TimeSpan LroStep = TimeSpan.FromSeconds(60.0);

    public SpiceOperatorTests()
    {
        // Data/LRO, loaded once per process (#668, second round)
        TestKernels.EnsureLro();
    }

    private static string[] Text(IEnumerable<Window> windows) =>
        windows.Select(w => $"[{w.StartDate}, {w.EndDate}]").ToArray();

    private static void AssertWindows(string[] expected, IEnumerable<Window> actual)
    {
        Assert.Equal(expected, Text(actual));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // SpiceApi
    // ---------------------------------------------------------------------------------------------------------------------

    private static readonly Dictionary<RelationalOperator, string[]> Distance = new()
    {
        [RelationalOperator.GreaterThanOrEqual] =
        [
            "[2007-01-08T00:11:07.6285911 TDB, 2007-01-13T06:37:47.9481441 TDB]",
            "[2007-02-04T07:02:35.2843759 TDB, 2007-02-10T09:31:01.8379413 TDB]",
            "[2007-03-03T00:20:25.1894418 TDB, 2007-03-10T14:04:38.4915548 TDB]",
            "[2007-03-29T22:53:58.1518964 TDB, 2007-04-01T00:01:05.1856545 TDB]"
        ],
        [RelationalOperator.LessThanOrEqual] =
        [
            "[2007-01-01T00:01:05.1839181 TDB, 2007-01-08T00:11:07.6285911 TDB]",
            "[2007-01-13T06:37:47.9481441 TDB, 2007-02-04T07:02:35.2843759 TDB]",
            "[2007-02-10T09:31:01.8379413 TDB, 2007-03-03T00:20:25.1894418 TDB]",
            "[2007-03-10T14:04:38.4915548 TDB, 2007-03-29T22:53:58.1518964 TDB]"
        ],
        [RelationalOperator.NotEqual] =
        [
            "[2007-01-01T00:01:05.1839181 TDB, 2007-01-08T00:11:07.6285907 TDB]",
            "[2007-01-08T00:11:07.6285907 TDB, 2007-01-13T06:37:47.9481439 TDB]",
            "[2007-01-13T06:37:47.9481439 TDB, 2007-02-04T07:02:35.2843763 TDB]",
            "[2007-02-04T07:02:35.2843763 TDB, 2007-02-10T09:31:01.8379413 TDB]",
            "[2007-02-10T09:31:01.8379413 TDB, 2007-03-03T00:20:25.1894417 TDB]",
            "[2007-03-03T00:20:25.1894417 TDB, 2007-03-10T14:04:38.4915546 TDB]",
            "[2007-03-10T14:04:38.4915546 TDB, 2007-03-29T22:53:58.1518965 TDB]",
            "[2007-03-29T22:53:58.1518965 TDB, 2007-04-01T00:01:05.1856545 TDB]"
        ]
    };

    [Theory]
    [InlineData(RelationalOperator.GreaterThanOrEqual)]
    [InlineData(RelationalOperator.LessThanOrEqual)]
    [InlineData(RelationalOperator.NotEqual)]
    public void SpiceApi_Distance(RelationalOperator relation)
    {
        // The Earth-Moon distance against 400 000 km, as SpiceApiTest.FindWindowsOnDistanceConstraintProxy
        var res = SpiceApi.Instance.FindWindowsOnDistanceConstraint(
            new Window(TimeSystem.Time.CreateTDB(220881665.18391809), TimeSystem.Time.CreateTDB(228657665.18565452)),
            399, 301, relation, 400000000, Aberration.None, TimeSpan.FromSeconds(86400.0));
        AssertWindows(Distance[relation], res);
    }

    private static readonly Dictionary<RelationalOperator, string[]> Coordinate = new()
    {
        [RelationalOperator.GreaterThanOrEqual] = ["[2023-02-19T14:33:08.9179869 TDB, 2023-02-20T00:00:00.0000000 TDB]"],
        [RelationalOperator.LessThanOrEqual] = ["[2023-02-19T00:00:00.0000000 TDB, 2023-02-19T14:33:08.9179869 TDB]"],
        [RelationalOperator.NotEqual] =
        [
            "[2023-02-19T00:00:00.0000000 TDB, 2023-02-19T14:33:08.9179871 TDB]",
            "[2023-02-19T14:33:08.9179871 TDB, 2023-02-20T00:00:00.0000000 TDB]"
        ]
    };

    [Theory]
    [InlineData(RelationalOperator.GreaterThanOrEqual)]
    [InlineData(RelationalOperator.LessThanOrEqual)]
    [InlineData(RelationalOperator.NotEqual)]
    public void SpiceApi_Coordinate(RelationalOperator relation)
    {
        // The Moon's Z in DSS-13's topocentric frame against 0, as SpiceApiTest.FindWindowsOnCoordinateConstraint
        var site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad, 35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad, 0.107));
        var res = SpiceApi.Instance.FindWindowsOnCoordinateConstraint(
            new Window(TimeSystem.Time.CreateTDB(730036800.0), TimeSystem.Time.CreateTDB(730123200)), site.NaifId, 301, site.Frame,
            CoordinateSystem.Rectangular, Astrodynamics.Coordinate.Z, relation, 0.0, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0));
        AssertWindows(Coordinate[relation], res);
    }

    private static readonly Dictionary<RelationalOperator, string[]> Illumination = new()
    {
        [RelationalOperator.GreaterThanOrEqual] = ["[2021-05-17T19:35:24.9088323 TDB, 2021-05-18T04:18:32.4437503 TDB]"],
        [RelationalOperator.LessThanOrEqual] =
        [
            "[2021-05-17T12:00:00.0000000 TDB, 2021-05-17T19:35:24.9088323 TDB]",
            "[2021-05-18T04:18:32.4437503 TDB, 2021-05-18T12:00:00.0000000 TDB]"
        ],
        [RelationalOperator.NotEqual] =
        [
            "[2021-05-17T12:00:00.0000000 TDB, 2021-05-17T19:35:24.9088323 TDB]",
            "[2021-05-17T19:35:24.9088323 TDB, 2021-05-18T04:18:32.4437503 TDB]",
            "[2021-05-18T04:18:32.4437503 TDB, 2021-05-18T12:00:00.0000000 TDB]"
        ]
    };

    [Theory]
    [InlineData(RelationalOperator.GreaterThanOrEqual)]
    [InlineData(RelationalOperator.LessThanOrEqual)]
    [InlineData(RelationalOperator.NotEqual)]
    public void SpiceApi_Illumination(RelationalOperator relation)
    {
        // The Sun's incidence at a point against 90.8 deg, as SpiceApiTest.FindWindowsOnIlluminationConstraint
        var res = SpiceApi.Instance.FindWindowsOnIlluminationConstraint(
            new Window(TimeSystem.Time.CreateTDB(674524800), TimeSystem.Time.CreateTDB(674611200)), 10, 399, new Frames.Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, relation, System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad), 0.0,
            Aberration.CNS, TimeSpan.FromHours(4.5), 10);
        AssertWindows(Illumination[relation], res);
    }

    [Fact]
    public void SpiceApi_AValueOutsideTheEnumeration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiceApi.Instance.FindWindowsOnDistanceConstraint(
            new Window(TimeSystem.Time.CreateTDB(220881665.0), TimeSystem.Time.CreateTDB(220981665.0)), 399, 301, (RelationalOperator)42,
            400000000, Aberration.None, TimeSpan.FromSeconds(86400.0)));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // CelestialItem and Site, kernel objects
    // ---------------------------------------------------------------------------------------------------------------------

    private static readonly Dictionary<RelationalOperator, string[]> LroDeclination = new()
    {
        [RelationalOperator.GreaterThanOrEqual] =
        [
            "[2023-12-15T00:54:42.2206856 TDB, 2023-12-15T01:52:49.4762495 TDB]",
            "[2023-12-15T02:51:40.2377942 TDB, 2023-12-15T03:49:46.0359914 TDB]",
            "[2023-12-15T04:48:38.1345551 TDB, 2023-12-15T05:46:42.5971847 TDB]",
            "[2023-12-15T06:45:35.8390821 TDB, 2023-12-15T07:43:39.1467032 TDB]",
            "[2023-12-15T08:42:33.2981179 TDB, 2023-12-15T09:40:35.6565859 TDB]",
            "[2023-12-15T10:39:30.5217185 TDB, 2023-12-15T11:37:32.1141390 TDB]"
        ],
        [RelationalOperator.LessThanOrEqual] =
        [
            "[2023-12-15T00:02:00.0000000 TDB, 2023-12-15T00:54:42.2206856 TDB]",
            "[2023-12-15T01:52:49.4762495 TDB, 2023-12-15T02:51:40.2377942 TDB]",
            "[2023-12-15T03:49:46.0359914 TDB, 2023-12-15T04:48:38.1345551 TDB]",
            "[2023-12-15T05:46:42.5971847 TDB, 2023-12-15T06:45:35.8390821 TDB]",
            "[2023-12-15T07:43:39.1467032 TDB, 2023-12-15T08:42:33.2981179 TDB]",
            "[2023-12-15T09:40:35.6565859 TDB, 2023-12-15T10:39:30.5217185 TDB]",
            "[2023-12-15T11:37:32.1141390 TDB, 2023-12-15T12:02:00.0000000 TDB]"
        ],
        [RelationalOperator.NotEqual] =
        [
            "[2023-12-15T00:02:00.0000000 TDB, 2023-12-15T00:54:42.2206858 TDB]",
            "[2023-12-15T00:54:42.2206858 TDB, 2023-12-15T01:52:49.4762497 TDB]",
            "[2023-12-15T01:52:49.4762497 TDB, 2023-12-15T02:51:40.2377937 TDB]",
            "[2023-12-15T02:51:40.2377937 TDB, 2023-12-15T03:49:46.0359914 TDB]",
            "[2023-12-15T03:49:46.0359914 TDB, 2023-12-15T04:48:38.1345546 TDB]",
            "[2023-12-15T04:48:38.1345546 TDB, 2023-12-15T05:46:42.5971842 TDB]",
            "[2023-12-15T05:46:42.5971842 TDB, 2023-12-15T06:45:35.8390821 TDB]",
            "[2023-12-15T06:45:35.8390821 TDB, 2023-12-15T07:43:39.1467032 TDB]",
            "[2023-12-15T07:43:39.1467032 TDB, 2023-12-15T08:42:33.2981179 TDB]",
            "[2023-12-15T08:42:33.2981179 TDB, 2023-12-15T09:40:35.6565855 TDB]",
            "[2023-12-15T09:40:35.6565855 TDB, 2023-12-15T10:39:30.5217190 TDB]",
            "[2023-12-15T10:39:30.5217190 TDB, 2023-12-15T11:37:32.1141390 TDB]",
            "[2023-12-15T11:37:32.1141390 TDB, 2023-12-15T12:02:00.0000000 TDB]"
        ]
    };

    [Theory]
    [InlineData(RelationalOperator.GreaterThanOrEqual)]
    [InlineData(RelationalOperator.LessThanOrEqual)]
    [InlineData(RelationalOperator.NotEqual)]
    public void KernelSpacecraft_Coordinate(RelationalOperator relation)
    {
        // The declination of LRO seen from the Moon in J2000 against 0 rad: gfposc
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var lro = new Spacecraft(-85, "LRO", epoch: LroWindow.StartDate);
        var res = moon.FindCoordinateWindows(LroWindow, lro, Frames.Frame.ICRF, CoordinateSystem.RaDec, Astrodynamics.Coordinate.Declination, relation, 0.0, new WindowSearchOptions { Step = LroStep });
        AssertWindows(LroDeclination[relation], res);
    }

    private static readonly Dictionary<RelationalOperator, string[]> Dss13Range = new()
    {
        [RelationalOperator.GreaterThanOrEqual] =
        [
            "[2023-12-15T03:43:21.9833910 TDB, 2023-12-15T04:19:46.2154557 TDB]",
            "[2023-12-15T05:19:30.3108535 TDB, 2023-12-15T12:02:00.0000000 TDB]"
        ],
        [RelationalOperator.LessThanOrEqual] =
        [
            "[2023-12-15T00:02:00.0000000 TDB, 2023-12-15T03:43:21.9833910 TDB]",
            "[2023-12-15T04:19:46.2154557 TDB, 2023-12-15T05:19:30.3108535 TDB]"
        ],
        [RelationalOperator.NotEqual] =
        [
            "[2023-12-15T00:02:00.0000000 TDB, 2023-12-15T03:43:21.9833908 TDB]",
            "[2023-12-15T03:43:21.9833908 TDB, 2023-12-15T04:19:46.2154561 TDB]",
            "[2023-12-15T04:19:46.2154561 TDB, 2023-12-15T05:19:30.3108535 TDB]",
            "[2023-12-15T05:19:30.3108535 TDB, 2023-12-15T12:02:00.0000000 TDB]"
        ]
    };

    [Theory]
    [InlineData(RelationalOperator.GreaterThanOrEqual)]
    [InlineData(RelationalOperator.LessThanOrEqual)]
    [InlineData(RelationalOperator.NotEqual)]
    public void KernelSite_Coordinate(RelationalOperator relation)
    {
        // The range of DSS-13 seen from LRO against 372 000 km: gfposc
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var dss13 = new Site(13, "DSS-13", earth);
        var lro = new Spacecraft(-85, "LRO", epoch: LroWindow.StartDate);
        var res = lro.FindCoordinateWindows(LroWindow, dss13, Frames.Frame.ICRF, CoordinateSystem.RaDec, Astrodynamics.Coordinate.Range, relation, 3.72e8, new WindowSearchOptions { Step = LroStep });
        AssertWindows(Dss13Range[relation], res);
    }

    /// <summary>The search window less the instants of <paramref name="equal"/>, a window from each crossing to the next.</summary>
    private static string[] Complement(Window search, Window[] equal)
    {
        var windows = new List<Window>();
        var cursor = search.StartDate;
        foreach (var crossing in equal)
        {
            if (crossing.StartDate > cursor) windows.Add(new Window(cursor, crossing.StartDate));
            if (crossing.EndDate > cursor) cursor = crossing.EndDate;
        }

        if (search.EndDate > cursor) windows.Add(new Window(cursor, search.EndDate));
        return Text(windows);
    }

    private static void AssertAgainstOwnSearches(Window search, Func<RelationalOperator, IEnumerable<Window>> find)
    {
        var greater = find(RelationalOperator.GreaterThan).ToArray();
        var lower = find(RelationalOperator.LessThan).ToArray();
        var equal = find(RelationalOperator.Equal).ToArray();
        Assert.NotEmpty(equal);
        Assert.Equal(Text(greater), Text(find(RelationalOperator.GreaterThanOrEqual)));
        Assert.Equal(Text(lower), Text(find(RelationalOperator.LessThanOrEqual)));
        Assert.Equal(Complement(search, equal), Text(find(RelationalOperator.NotEqual)));
    }

    [Fact]
    public void KernelSpacecraft_Distance()
    {
        // LRO and the Moon, both ways, at 1 815.148 km (the threshold in m since #466; before it the library divided it by
        // 1000 twice, and this test passed 1 815 148 000 m to reach gfdist with 1 815.148 km)
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var lro = new Spacecraft(-85, "LRO", epoch: LroWindow.StartDate);
        AssertAgainstOwnSearches(LroWindow, r => moon.FindDistanceWindows(LroWindow, lro, r, 1815148.0, new WindowSearchOptions { Step = LroStep }));
        AssertAgainstOwnSearches(LroWindow, r => lro.FindDistanceWindows(LroWindow, moon, r, 1815148.0, new WindowSearchOptions { Step = LroStep }));
    }

    [Fact]
    public void KernelSite_Illumination()
    {
        // The Sun's incidence at DSS-13 against 90 deg over a day: gfilum at the surface point the native layer builds
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var dss13 = new Site(13, "DSS-13", earth);
        var search = new Window(TimeSystem.Time.CreateTDB(755870520.0), TimeSystem.Time.CreateTDB(755956920.0));
        AssertAgainstOwnSearches(search, r => dss13.FindIlluminationWindows(search, IlluminationAngle.Incidence, r, System.Math.PI * 0.5, new CelestialBody(Stars.Sun), new WindowSearchOptions { Step = LroStep }));
    }
}
