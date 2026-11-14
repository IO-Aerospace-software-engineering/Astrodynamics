// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
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

    // Provisional, twice the worst value on Linux x64 (3.58e-7 s, 2026-11-14), until the CI of the three platforms has
    // printed its MEASURE lines: the managed search at 1 µs on the LEO propagated by RK7(8) at 1e-12 about a point-mass
    // Earth, against the CSPICE windows of its two-body orbit (#678)
    private const double PropagatedThreshold = 7.2e-7;

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
        TestKernels.EnsureSolarSystem();
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
    public void Illumination_SiteFromCoordinates_WithinTheNormalOfGfilum()
    {
        // The site from the coordinates of the kernel site takes the managed search, which #488 does not change (this test
        // passes before it): with Illumination_KernelSite, which holds the kernel site to gfilum, it bounds the gap
        // between the two branches, 4.1 s and 5.2 s before #488.
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
    [InlineData(true)]
    [InlineData(false)]
    public void LaunchWindows_KernelRecoverySite(bool byDay)
    {
        // #488 through Launch: the day windows of a recovery site from the kernels (FindDayWindows, civil twilight) are
        // gfilum's; each launch window of the day, or of the night, is the one of every window (launchByDay null) that the
        // day windows of both sites hold, or that neither holds; the launch site from the coordinates of DSS-13
        var dss13 = Dss13;
        var launchSite = new LaunchSite(98, "DSS13LAUNCH", Earth, dss13.Planetodetic, new AzimuthRange(0.0, IO.Astrodynamics.Constants._2PI));
        var target = new KeplerianElements(6.8e6, 0.0, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 1.0, 0.0, 0.0, Earth, IlluminationWindow.StartDate,
            Frames.Frame.ICRF);
        var all = new Maneuver.Launch(launchSite, dss13, target, IO.Astrodynamics.Constants.CivilTwilight, null).FindLaunchWindows(IlluminationWindow).ToArray();
        var recoveryDays = Reference.Value["dss13_sun_incidence < 96 deg LT+S"]
            .Select(w => new Window(TimeSystem.Time.CreateTDB(w[0]), TimeSystem.Time.CreateTDB(w[1]))).ToArray();
        var launchDays = launchSite.FindDayWindows(IlluminationWindow, IO.Astrodynamics.Constants.CivilTwilight).ToArray();
        var expected = all.Where(w =>
        {
            bool launchDay = launchDays.Any(d => d.Intersects(w.Window));
            bool recoveryDay = recoveryDays.Any(d => d.Intersects(w.Window));
            return byDay ? launchDay && recoveryDay : !(launchDay || recoveryDay);
        }).Select(w => w.Window).ToArray();

        var actual = new Maneuver.Launch(launchSite, dss13, target, IO.Astrodynamics.Constants.CivilTwilight, byDay).FindLaunchWindows(IlluminationWindow)
            .Select(w => w.Window).ToArray();
        _output.WriteLine($"MEASURE launch by day {byDay}: {actual.Length} of {all.Length} windows");
        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
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

    // #678: a two-body LEO over 2024-03-01 TDB (a = 6800 km, e = 1e-4, i = 51.6 deg, the Earth's GM of the kernels), the
    // CSPICE windows of the same orbit written as kernel_search_leo.bsp (-67801), each state at the TT seconds from the epoch
    private static readonly FileInfo LeoSpkPath = new("Data/GeometryReference/kernel_search_leo.bsp");
    private static readonly Window LeoWindow = new(TimeSystem.Time.CreateTDB(762523200.0), TimeSystem.Time.CreateTDB(762609600.0));
    private const string LeoElevation = "leo_dss13_elevation > 30 deg";

    // The elevation in the topocentric frame of the library's site, built from the planetodetic coordinates of DSS-13 at
    // J2000 on pck00011, which the frame kernel's DSS-13_TOPO (the coordinates at 2020-10-23 on GRS80) differs from by
    // 7.6e-8 rad in longitude: 1.07e-5 s on these crossings
    private const string LeoSiteFrameElevation = "leo_dss13_j2000_topo_elevation > 30 deg";
    private const string LeoRange = "leo_dss13_distance < 1000 km";
    private static readonly double ThirtyDegrees = 30.0 * IO.Astrodynamics.Constants.Deg2Rad;

    private static KeplerianElements LeoOrbit => new(6800000.0, 1.0e-4, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, Earth, LeoWindow.StartDate,
        Frames.Frame.ICRF);

    private static Spacecraft ManagedLeo => new(-678, "LEO678", 100.0, 10000.0, new Clock("LEO678CLK", 65536), LeoOrbit);

    private void AssertShortPasses(Spacecraft leo, TimeSpan step, string path, double threshold)
    {
        var dss13 = Dss13;
        AssertWindows(LeoSiteFrameElevation, leo.FindWindowsOnCoordinateConstraint(LeoWindow, dss13, dss13.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
            RelationnalOperator.Greater, ThirtyDegrees, 0.0, Aberration.None, step), threshold, path);
        AssertWindows(LeoRange, leo.FindWindowsOnDistanceConstraint(LeoWindow, dss13, RelationnalOperator.Lower, 1.0e6, Aberration.None, step), threshold, path);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void ShortPasses_ManagedAgainstGfposcAndGfdist(double stepMinutes)
    {
        // #678: 1, 0 and 0 of the 2 passes of about 3 min above 30 deg, 2, 0 and 0 of the 3 windows below 1000 km
        AssertShortPasses(ManagedLeo, TimeSpan.FromMinutes(stepMinutes), $"two-body, managed, step {stepMinutes} min", ManagedThreshold);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void ShortPasses_CspiceAtTheSameSteps(double stepMinutes)
    {
        // The same windows from CSPICE at these steps (gfposc on DSS-13 seen from the LEO, below -30 deg, and gfdist): its
        // searches find the extremes of the quantity before its crossings
        SpiceAPI.Instance.LoadKernels(LeoSpkPath);
        try
        {
            var leo = new Spacecraft(-67801, "LEO678SPK", epoch: LeoWindow.StartDate);
            var dss13 = Dss13;
            var step = TimeSpan.FromMinutes(stepMinutes);
            AssertWindows(LeoElevation, dss13.FindWindowsOnCoordinateConstraint(LeoWindow, leo, dss13.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
                RelationnalOperator.Lower, -ThirtyDegrees, 0.0, Aberration.None, step), NativeThreshold, $"kernel site, gfposc, step {stepMinutes} min");
            AssertWindows(LeoRange, dss13.FindWindowsOnDistanceConstraint(LeoWindow, leo, RelationnalOperator.Lower, 1.0e6, Aberration.None, step),
                NativeThreshold, $"kernel site, gfdist, step {stepMinutes} min");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(LeoSpkPath);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShortPasses_PropagatedSpacecraft(bool rk78)
    {
        // #678: no pass and no range window at a 10 min step. The LEO propagated about a point-mass Earth over exactly the
        // search window (no state read past it, #527); RK7(8) at 1e-12 within its integration error, Velocity-Verlet the count
        var leo = ManagedLeo;
        Integrator integrator = rk78 ? new RK78Integrator(1e-12, 1e-12) : new VVIntegrator(TimeSpan.FromSeconds(1.0));
        leo.Propagate(LeoWindow, [Earth], integrator, false, false, TimeSpan.FromSeconds(60.0));
        var step = TimeSpan.FromMinutes(10.0);
        if (rk78)
        {
            AssertShortPasses(leo, step, "RK7(8), managed, step 10 min", PropagatedThreshold);
            return;
        }

        var dss13 = Dss13;
        var passes = leo.FindWindowsOnCoordinateConstraint(LeoWindow, dss13, dss13.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
            RelationnalOperator.Greater, ThirtyDegrees, 0.0, Aberration.None, step).ToArray();
        var ranges = leo.FindWindowsOnDistanceConstraint(LeoWindow, dss13, RelationnalOperator.Lower, 1.0e6, Aberration.None, step).ToArray();
        _output.WriteLine($"MEASURE Velocity-Verlet: {passes.Length} passes, {ranges.Length} range windows");
        Assert.Equal(Reference.Value[LeoSiteFrameElevation].Length, passes.Length);
        Assert.Equal(Reference.Value[LeoRange].Length, ranges.Length);
    }

    [Theory]
    [InlineData(5.5)]
    [InlineData(6.0)]
    public void Illumination_SiteFromCoordinates_ShortWindowsAtACoarseStep(double stepHours)
    {
        // #678: 1 and 0 of the 3 windows. A site at the longitude and latitude of DSS-13 on the ellipsoid, where the geodetic
        // normal is gfilum's (#654); the Sun's incidence below 58.83 deg, its minimum over the 3 days plus 0.3 deg
        var site = new Site(97, "DSS13GROUND", Earth, new Planetodetic(Dss13.Planetodetic.Longitude, Dss13.Planetodetic.Latitude, 0.0));
        var window = new Window(IlluminationWindow.StartDate, IlluminationWindow.StartDate.AddSeconds(3.0 * 86400.0));
        AssertWindows("point_sun_incidence dss13 0 m < 58.83 deg NONE 3 days", site.FindWindowsOnIlluminationConstraint(window, IlluminationAngle.Incidence,
            RelationnalOperator.Lower, 58.83 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, Aberration.None, TimeSpan.FromHours(stepHours), TestHelpers.Sun),
            ManagedThreshold, $"site from coordinates, managed, step {stepHours} h");
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
