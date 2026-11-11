// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The window searches the library hands to CSPICE, on objects of the kernels: the distance of a kernel spacecraft
/// (<c>gfdist_c</c>, #466), its coordinates (<c>gfposc_c</c>) and the illumination of a kernel site or of a point given
/// to <see cref="SpiceAPI.FindWindowsOnIlluminationConstraint"/> (<c>gfilum_c</c>, #488), against the CSPICE windows of
/// <c>tools/geometry_reference/kernel_search_reference.py</c> (<c>kernel_search_reference.json</c>, converged to 1e-9 s;
/// the native searches run at CSPICE's default of 1e-6 s), and the managed search on the same geometry.
/// </summary>
public class KernelWindowSearchTests
{
    private static readonly FileInfo LroSpkPath = new("Data/LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp");
    private static readonly FileInfo OrbiterSpkPath = new("Data/GeometryReference/kernel_search_orbiter.bsp");

    // Provisional, twice the worst value on Linux x64 (4.77e-7 s, 2026-11-11) rounded up, until the CI of the three platforms has
    // printed its MEASURE lines: the native searches at CSPICE's default tolerance of 1e-6 s against the reference at
    // 1e-9 s, and the 100 ns of a Time
    private const double NativeThreshold = 1.0e-6;

    // The managed search at 1 µs against the reference (GeometryReference.ThresholdCrossing)
    private const double ManagedThreshold = GeometryReference.ThresholdCrossing;

    private static readonly Lazy<Dictionary<string, double[][]>> Reference = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, double[][]>>(File.ReadAllText("Data/GeometryReference/kernel_search_reference.json")));

    private static readonly Window DistanceWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(755913900.0));
    private static readonly Window IlluminationWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(755957100.0));
    private static readonly Window MoonIlluminationWindow = new(TimeSystem.Time.CreateTDB(755870700.0), TimeSystem.Time.CreateTDB(758462700.0));
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private const double LroMoonDistance = 1815148.0;

    private readonly ITestOutputHelper _output;

    public KernelWindowSearchTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        SpiceAPI.Instance.LoadKernels(LroSpkPath);
        SpiceAPI.Instance.LoadKernels(OrbiterSpkPath);
    }

    private static Spacecraft Lro => new(-85, "LRO", epoch: DistanceWindow.StartDate);
    private static Spacecraft Orbiter => new(-85901, "ORBITER", epoch: DistanceWindow.StartDate);
    private static CelestialBody Moon => new(PlanetsAndMoons.MOON);
    private static CelestialBody Earth => new(PlanetsAndMoons.EARTH);
    private static Site Dss13 => new(13, "DSS-13", Earth);

    private void AssertWindows(string label, IEnumerable<Window> actual, double threshold, string path)
    {
        var expected = Reference.Value[label];
        var windows = actual.ToArray();
        Assert.Equal(expected.Length, windows.Length);
        double worst = 0.0;
        for (int i = 0; i < expected.Length; i++)
        {
            worst = System.Math.Max(worst, System.Math.Abs(windows[i].StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - expected[i][0]));
            worst = System.Math.Max(worst, System.Math.Abs(windows[i].EndDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - expected[i][1]));
        }

        _output.WriteLine($"MEASURE {label} ({path}): worst |library - CSPICE| {worst:E3} s");
        Assert.True(worst <= threshold, $"{label} ({path}): {worst:E3} s");
    }

    [Theory]
    [InlineData(RelationnalOperator.Greater, ">")]
    [InlineData(RelationnalOperator.Lower, "<")]
    [InlineData(RelationnalOperator.Equal, "=")]
    public void Distance_SpacecraftAndBody_BothReceivers(RelationnalOperator relation, string label)
    {
        // #466: 1 window, 0 and 0 before, gfdist at 1.815148 km
        string key = $"lro_moon_distance {label} 1815.148 km";
        AssertWindows(key, Lro.FindWindowsOnDistanceConstraint(DistanceWindow, Moon, relation, LroMoonDistance, Aberration.None, Step), NativeThreshold,
            "spacecraft, body observer");
        AssertWindows(key, Moon.FindWindowsOnDistanceConstraint(DistanceWindow, Lro, relation, LroMoonDistance, Aberration.None, Step), NativeThreshold,
            "body, spacecraft observer");
    }

    [Fact]
    public void Distance_SpacecraftAndBarycenter()
    {
        var emb = new Barycenter(3);
        AssertWindows("lro_emb_distance > 364500 km", Lro.FindWindowsOnDistanceConstraint(DistanceWindow, emb, RelationnalOperator.Greater, 364500000.0,
            Aberration.None, Step), NativeThreshold, "spacecraft, barycenter observer");
        AssertWindows("lro_emb_distance > 364500 km", emb.FindWindowsOnDistanceConstraint(DistanceWindow, Lro, RelationnalOperator.Greater, 364500000.0,
            Aberration.None, Step), NativeThreshold, "barycenter, spacecraft observer");
    }

    [Fact]
    public void Distance_TwoSpacecraft()
    {
        AssertWindows("lro_orbiter_distance < 3000 km", Lro.FindWindowsOnDistanceConstraint(DistanceWindow, Orbiter, RelationnalOperator.Lower, 3000000.0,
            Aberration.None, Step), NativeThreshold, "LRO, orbiter observer");
        AssertWindows("lro_orbiter_distance < 3000 km", Orbiter.FindWindowsOnDistanceConstraint(DistanceWindow, Lro, RelationnalOperator.Lower, 3000000.0,
            Aberration.None, Step), NativeThreshold, "orbiter, LRO observer");
    }

    [Fact]
    public void Distance_KernelSiteAndSpacecraft_BothBranches()
    {
        // The site's search goes to gfdist (#466: 1 window before, gfdist at 372 km); the spacecraft's, with a site
        // observer, is the managed search
        AssertWindows("lro_dss13_distance > 372000 km", Dss13.FindWindowsOnDistanceConstraint(DistanceWindow, Lro, RelationnalOperator.Greater, 3.72e8,
            Aberration.None, Step), NativeThreshold, "kernel site, spacecraft observer");
        AssertWindows("lro_dss13_distance > 372000 km", Lro.FindWindowsOnDistanceConstraint(DistanceWindow, Dss13, RelationnalOperator.Greater, 3.72e8,
            Aberration.None, Step), ManagedThreshold, "spacecraft, site observer, managed");
    }

    [Fact]
    public void Distance_SpiceApi_TakesMetres()
    {
        AssertWindows("lro_moon_distance > 1815.148 km", SpiceAPI.Instance.FindWindowsOnDistanceConstraint(DistanceWindow, 301, -85, RelationnalOperator.Greater,
            LroMoonDistance, Aberration.None, Step), NativeThreshold, "SpiceAPI");
    }

    [Fact]
    public void Coordinates_KernelSpacecraft_DistancesInKm()
    {
        // The coordinate branch converts its distances to km for gfposc, which takes them as they are: the same windows
        // as the distance search at the same threshold
        AssertWindows("lro_moon_z > 500 km", Lro.FindWindowsOnCoordinateConstraint(DistanceWindow, Moon, Frames.Frame.ICRF, CoordinateSystem.Rectangular,
            Coordinate.Z, RelationnalOperator.Greater, 500000.0, 0.0, Aberration.None, Step), NativeThreshold, "spacecraft Z");
        AssertWindows("lro_moon_range > 1815.148 km", Lro.FindWindowsOnCoordinateConstraint(DistanceWindow, Moon, Frames.Frame.ICRF, CoordinateSystem.RaDec,
            Coordinate.Range, RelationnalOperator.Greater, LroMoonDistance, 0.0, Aberration.None, Step), NativeThreshold, "spacecraft range");
        AssertWindows("lro_dss13_range > 372000 km", Dss13.FindWindowsOnCoordinateConstraint(DistanceWindow, Lro, Frames.Frame.ICRF, CoordinateSystem.RaDec,
            Coordinate.Range, RelationnalOperator.Greater, 3.72e8, 0.0, Aberration.None, Step), NativeThreshold, "kernel site range");
        Assert.Equal(Reference.Value["lro_moon_range > 1815.148 km"], Reference.Value["lro_moon_distance > 1815.148 km"]);
    }

    public static IEnumerable<object[]> KernelSiteIlluminations()
    {
        double halfPi = IO.Astrodynamics.Constants.PI2;
        double deg = IO.Astrodynamics.Constants.Deg2Rad;
        yield return ["dss13_sun_incidence > 90 deg NONE", RelationnalOperator.Greater, halfPi, Aberration.None];
        yield return ["dss13_sun_incidence < 90.8 deg LT+S", RelationnalOperator.Lower, halfPi + 0.8 * deg, Aberration.LTS];
        yield return ["dss13_sun_incidence > 60 deg CN+S", RelationnalOperator.Greater, 60.0 * deg, Aberration.CNS];
    }

    [Theory]
    [MemberData(nameof(KernelSiteIlluminations))]
    public void Illumination_KernelSite(string label, RelationnalOperator relation, double angle, Aberration aberration)
    {
        // #488: the point 1070.85 km above DSS-13, 3.1 to 29.6 s off
        AssertWindows(label, Dss13.FindWindowsOnIlluminationConstraint(IlluminationWindow, IlluminationAngle.Incidence, relation, angle, 0.0, aberration, Step,
            TestHelpers.Sun), NativeThreshold, "kernel site");
    }

    [Theory]
    [InlineData(0.8, "dss13_sun_incidence < 90.8 deg LT+S")]
    [InlineData(6.0, "dss13_sun_incidence < 96 deg LT+S")]
    public void DayWindows_KernelSite(double twilight, string label)
    {
        AssertWindows(label, Dss13.FindDayWindows(IlluminationWindow, twilight * IO.Astrodynamics.Constants.Deg2Rad), NativeThreshold, "FindDayWindows");
    }

    [Fact]
    public void Illumination_KernelSiteAndSiteFromCoordinates_Agree()
    {
        // #488: 4.1 s and 5.2 s apart before; the site from the coordinates of the kernel site takes the managed search.
        // The two differ by the normal at a point above the ellipsoid: the managed search takes the geodetic normal,
        // gfilum the normal of surfnm at the point, tilted from it by atan(k tan φ) − φ = 0.110″ at 1070.85 m and the
        // latitude of DSS-13 (k = (N(1 − e²) + h) / ((1 − e²)(N + h))), 0.053″ along the incidence, 4.96e-3 s at its
        // 10.7″/s at sunrise (measured, Linux x64, 2026-11-11); twice that
        var dss13 = Dss13;
        var fromCoordinates = new Site(99, "DSS13GEO", Earth, dss13.Planetodetic);
        AssertWindows("dss13_sun_incidence > 90 deg NONE", fromCoordinates.FindWindowsOnIlluminationConstraint(IlluminationWindow, IlluminationAngle.Incidence,
            RelationnalOperator.Greater, IO.Astrodynamics.Constants.PI2, 0.0, Aberration.None, Step, TestHelpers.Sun), 1.0e-2, "site from coordinates");
    }

    [Theory]
    [InlineData("point_sun_incidence dss13 0 m > 90 deg NONE", double.NaN, 0.0)]
    [InlineData("point_sun_incidence dss13 1071 m > 90 deg NONE", double.NaN, 1071.0)]
    [InlineData("point_sun_incidence dss13 5000 m > 90 deg NONE", double.NaN, 5000.0)]
    [InlineData("point_sun_incidence lat 0 1071 m > 90 deg NONE", 0.0, 1071.0)]
    [InlineData("point_sun_incidence lat 60 1071 m > 90 deg NONE", 60.0, 1071.0)]
    public void Illumination_SpiceApi_AltitudeInMetres(string label, double latitudeDeg, double altitude)
    {
        // #488: 1071 m read as 1071 km, 4.4 s off at the latitude of DSS-13
        var dss13 = Dss13.Planetodetic;
        double latitude = double.IsNaN(latitudeDeg) ? dss13.Latitude : latitudeDeg * IO.Astrodynamics.Constants.Deg2Rad;
        var earth = Earth;
        AssertWindows(label, SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(IlluminationWindow, 399013, 399, earth.Frame,
            new Planetodetic(dss13.Longitude, latitude, altitude), IlluminationAngle.Incidence, RelationnalOperator.Greater, IO.Astrodynamics.Constants.PI2, 0.0,
            Aberration.None, Step, 10), NativeThreshold, "SpiceAPI");
    }

    [Fact]
    public void Illumination_SpiceApi_Moon()
    {
        var moon = Moon;
        Assert.Equal("MOON_ME", moon.Frame.Name);
        AssertWindows("moon_point_sun_incidence 1071 m > 90 deg NONE", SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(MoonIlluminationWindow, 399, 301,
            moon.Frame, new Planetodetic(0.5, 0.3, 1071.0), IlluminationAngle.Incidence, RelationnalOperator.Greater, IO.Astrodynamics.Constants.PI2, 0.0,
            Aberration.None, TimeSpan.FromHours(1.0), 10), NativeThreshold, "SpiceAPI Moon");
    }
}
