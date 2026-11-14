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

    [Fact]
    public void Illumination_SpiceApi_Moon()
    {
        var moon = Moon;
        Assert.Equal("MOON_ME", moon.Frame.Name);
        AssertWindows("moon_point_sun_incidence 1071 m > 90 deg NONE", SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(MoonIlluminationWindow, 399, 301,
            moon.Frame, new Planetodetic(0.5, 0.3, 1071.0), IlluminationAngle.Incidence, RelationnalOperator.Greater, IO.Astrodynamics.Constants.PI2, 0.0,
            Aberration.None, TimeSpan.FromHours(1.0), 10), NativeThreshold, "SpiceAPI Moon");
    }

    /// <summary>
    /// #682 (guard): the spellings of ELLIPSOID that <c>gfilum_c</c> accepts ("Neither case nor whitespaces are significant
    /// in `method'", Detailed_Input <c>method</c>; measured on N0067 for these) give the windows of the default method on
    /// both paths, to the tick, the CSPICE path within <see cref="NativeThreshold"/> of the reference.
    /// </summary>
    [Fact]
    public void Illumination_MethodCaseAndBlanks_SameWindows()
    {
        const string label = "dss13_sun_incidence < 90.8 deg LT+S";
        double angle = IO.Astrodynamics.Constants.PI2 + 0.8 * IO.Astrodynamics.Constants.Deg2Rad;
        var dss13 = Dss13;
        var fromCoordinates = new Site(99, "DSS13GEO", Earth, dss13.Planetodetic);
        foreach (var site in new[] { dss13, fromCoordinates })
        {
            var expected = site.FindWindowsOnIlluminationConstraint(IlluminationWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, angle, 0.0,
                Aberration.LTS, Step, TestHelpers.Sun).ToArray();
            foreach (var method in new[] { "ELLIPSOID", "ellipsoid", " Ellipsoid ", "eLLipsoid  " })
            {
                var actual = site.FindWindowsOnIlluminationConstraint(IlluminationWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, angle, 0.0,
                    Aberration.LTS, Step, TestHelpers.Sun, method).ToArray();
                Assert.Equal(expected, actual);
            }
        }

        AssertWindows(label, dss13.FindWindowsOnIlluminationConstraint(IlluminationWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, angle, 0.0,
            Aberration.LTS, Step, TestHelpers.Sun, " ellipsoid"), NativeThreshold, "kernel site, method ' ellipsoid'");
    }

    // The planetographic longitude east of the prime meridian of the body-fixed frame of the observer's body, in (−π, π]:
    // the angle between a window's edge and the 0/2π cut of the planetographic longitude, whatever its sense
    private static double EastLongitude(ILocalizable target, CelestialBody observer, TimeSystem.Time epoch)
    {
        var position = target.GetEphemeris(epoch, observer, observer.Frame, Aberration.None).ToStateVector().Position;
        return System.Math.Atan2(position.Y, position.X);
    }

    /// <summary>
    /// The managed windows against those of <c>gfposc_c</c> PLANETOGRAPHIC: the same count; an edge on the threshold within
    /// <see cref="NativeThreshold"/>; for a longitude (<paramref name="cutAngle"/> given), an edge at the 0/2π cut (where it
    /// jumps) within <paramref name="cutAngle"/> of the cut, as an angle, on both, and as a time within the time that angle gives at the rate of the longitude (#682,
    /// the Sun from Mercury moves 7e-8 rad/s).
    /// </summary>
    private void AssertPlanetographic(string label, Window[] managed, Window[] gfposc, Window window, ILocalizable target, CelestialBody observer, double? cutAngle)
    {
        Assert.NotEmpty(gfposc);
        Assert.Equal(gfposc.Length, managed.Length);
        double worstThreshold = 0.0, worstCut = 0.0;
        for (int i = 0; i < gfposc.Length; i++)
        {
            foreach (var (m, g) in new[] { (managed[i].StartDate, gfposc[i].StartDate), (managed[i].EndDate, gfposc[i].EndDate) })
            {
                double dt = System.Math.Abs((m - g).TotalSeconds);
                double east = EastLongitude(target, observer, g);
                if (cutAngle.HasValue && g > window.StartDate && g < window.EndDate && System.Math.Abs(east) < 1e-3)
                {
                    // At the cut
                    double managedAngle = System.Math.Abs(EastLongitude(target, observer, m));
                    double rate = System.Math.Abs(EastLongitude(target, observer, g.AddSeconds(1.0)) - EastLongitude(target, observer, g.AddSeconds(-1.0))) / 2.0;
                    worstCut = System.Math.Max(worstCut, managedAngle);
                    Assert.True(managedAngle <= cutAngle, $"{label}: the managed edge {m} is {managedAngle:E3} rad from the cut");
                    Assert.True(System.Math.Abs(east) <= cutAngle, $"{label}: the gfposc edge {g} is {System.Math.Abs(east):E3} rad from the cut");
                    Assert.True(dt <= (managedAngle + System.Math.Abs(east)) / rate + NativeThreshold, $"{label}: {dt:E3} s at the cut, rate {rate:E3} rad/s");
                }
                else
                {
                    worstThreshold = System.Math.Max(worstThreshold, dt);
                }
            }
        }

        _output.WriteLine($"MEASURE #682 planetographic {label}: {gfposc.Length} windows, worst |managed - gfposc| {worstThreshold:E3} s on the threshold, worst managed edge {worstCut:E3} rad from the cut");
        Assert.True(worstThreshold <= NativeThreshold, $"{label}: {worstThreshold:E3} s");
    }

    // Provisional (#682), twice the worst on Linux x64 rounded up (6.6e-11 rad, the Moon from the Earth above 4 rad,
    // 2026-11-13), until the CI of the three platforms has printed its MEASURE lines: the 1 µs of the searches and the
    // 100 ns of a Time at the rate of the longitude, 7.3e-5 rad/s for the Moon in ITRF93
    private const double CutAngle = 1.4e-10;

    private static readonly Window MoonPlanetographicWindow = new(new TimeSystem.Time(2024, 3, 1), new TimeSystem.Time(2024, 3, 11));

    /// <summary>
    /// #682: the managed <see cref="CoordinateSystem.Planetographic"/> longitude is CSPICE's (<c>gfposc_c</c> Detailed_Input
    /// <c>coord</c>: PLANETOGRAPHIC LONGITUDE in [0, 2π), the sense of <c>recpgr_c</c>), the Moon from the Earth in ITRF93,
    /// a frame centred on the observer's body; before, it was the planetocentric (−π, π] (10 windows against 11 above 1 rad,
    /// 0 against 10 above 4 rad).
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void PlanetographicLongitude_ManagedEqualsGfposc(double longitude)
    {
        var earth = Earth;
        var moon = Moon;
        var step = TimeSpan.FromHours(1.0);
        var managed = moon.FindWindowsOnCoordinateConstraint(MoonPlanetographicWindow, earth, earth.Frame, CoordinateSystem.Planetographic, Coordinate.Longitude,
            RelationnalOperator.Greater, longitude, 0.0, Aberration.None, step).ToArray();
        var gfposc = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(MoonPlanetographicWindow, 399, 301, earth.Frame, CoordinateSystem.Planetographic,
            Coordinate.Longitude, RelationnalOperator.Greater, longitude, 0.0, Aberration.None, step).ToArray();
        AssertPlanetographic($"Moon from the Earth, longitude > {longitude} rad", managed, gfposc, MoonPlanetographicWindow, moon, earth, CutAngle);
    }

    public static IEnumerable<object[]> PositiveWestAndRetrograde()
    {
        // Mercury, prograde: positive west; Venus, retrograde: positive east (recpgr_c Particulars)
        yield return [199, 10, 2024, 1, TimeSpan.FromDays(1.0), 3.0];
        yield return [199, 399, 2024, 1, TimeSpan.FromDays(1.0), 3.0];
        yield return [199, 399, 2024, 1, TimeSpan.FromHours(6.0), 0.5];
        yield return [299, 399, 2024, 2, TimeSpan.FromDays(2.0), 3.0];
    }

    /// <summary>
    /// #682: the sense of the planetographic longitude (<c>recpgr_c</c> Particulars: positive west for a prograde body,
    /// positive east for a retrograde one, the Earth, the Moon and the Sun east), the Earth and the Sun from Mercury, the Earth
    /// from Venus, in their IAU frames, against <c>gfposc_c</c>; and on the site's path, DSS-13 from Mercury.
    /// </summary>
    [Theory]
    [MemberData(nameof(PositiveWestAndRetrograde))]
    public void PlanetographicLongitude_PositiveWestAndRetrograde_ManagedEqualsGfposc(int observerId, int targetId, int year, int months, TimeSpan step, double longitude)
    {
        var observer = new CelestialBody(observerId);
        CelestialItem target = targetId == 10 ? TestHelpers.Sun : new CelestialBody(targetId);
        var start = new TimeSystem.Time(year, 1, 1);
        var window = new Window(start, months == 1 ? new TimeSystem.Time(year, 7, 1) : new TimeSystem.Time(year + 1, 1, 1));
        var managed = target.FindWindowsOnCoordinateConstraint(window, observer, observer.Frame, CoordinateSystem.Planetographic, Coordinate.Longitude,
            RelationnalOperator.Greater, longitude, 0.0, Aberration.None, step).ToArray();
        var gfposc = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(window, observerId, targetId, observer.Frame, CoordinateSystem.Planetographic,
            Coordinate.Longitude, RelationnalOperator.Greater, longitude, 0.0, Aberration.None, step).ToArray();
        AssertPlanetographic($"{target.Name} from {observer.Name} in {observer.Frame.Name}, longitude > {longitude} rad, step {step}", managed, gfposc, window,
            target, observer, CutAngle);

        if (observerId == 199 && targetId == 399 && step == TimeSpan.FromDays(1.0))
        {
            var dss13 = Dss13;
            var site = dss13.FindWindowsOnCoordinateConstraint(window, observer, observer.Frame, CoordinateSystem.Planetographic, Coordinate.Longitude,
                RelationnalOperator.Greater, longitude, 0.0, Aberration.None, step).ToArray();
            var siteGfposc = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(window, observerId, dss13.NaifId, observer.Frame, CoordinateSystem.Planetographic,
                Coordinate.Longitude, RelationnalOperator.Greater, longitude, 0.0, Aberration.None, step).ToArray();
            AssertPlanetographic($"DSS-13 from {observer.Name}, longitude > {longitude} rad", site, siteGfposc, window, dss13, observer, CutAngle);
        }
    }

    /// <summary>
    /// #682: the managed planetographic latitude and altitude are CSPICE's (<c>recpgr_c</c> Detailed_Output: the latitude of
    /// the normal to the spheroid, the altitude above it), the Moon from the Earth in ITRF93; before, the planetocentric
    /// latitude (edges 51.5 s and 47.5 s off) and no altitude (it threw). On the site's path, DSS-13 from the Moon in its
    /// body frame (a sphere: the latitude did not move there, the altitude threw).
    /// </summary>
    [Fact]
    public void PlanetographicLatitudeAndAltitude_ManagedEqualsGfposc()
    {
        var earth = Earth;
        var moon = Moon;
        var dss13 = Dss13;
        var step = TimeSpan.FromHours(1.0);
        var window = MoonPlanetographicWindow;
        foreach (var (coordinate, value) in new[] { (Coordinate.Latitude, -0.4), (Coordinate.Altitude, 3.9e8) })
        {
            double spiceValue = coordinate == Coordinate.Altitude ? value / 1000.0 : value;
            var managed = moon.FindWindowsOnCoordinateConstraint(window, earth, earth.Frame, CoordinateSystem.Planetographic, coordinate,
                RelationnalOperator.Greater, value, 0.0, Aberration.None, step).ToArray();
            var gfposc = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(window, 399, 301, earth.Frame, CoordinateSystem.Planetographic, coordinate,
                RelationnalOperator.Greater, spiceValue, 0.0, Aberration.None, step).ToArray();
            AssertPlanetographic($"Moon from the Earth, {coordinate} > {value}", managed, gfposc, window, moon, earth, null);
        }

        // A month: the latitude of the Earth in the Moon's frame, its libration, has a period of 27.2 days
        var siteWindow = new Window(window.StartDate, window.StartDate.AddDays(30.0));
        foreach (var (coordinate, value) in new[] { (Coordinate.Latitude, 0.0), (Coordinate.Altitude, 3.9e8) })
        {
            double spiceValue = coordinate == Coordinate.Altitude ? value / 1000.0 : value;
            var managed = dss13.FindWindowsOnCoordinateConstraint(siteWindow, moon, moon.Frame, CoordinateSystem.Planetographic, coordinate,
                RelationnalOperator.Greater, value, 0.0, Aberration.None, step).ToArray();
            var gfposc = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(siteWindow, 301, dss13.NaifId, moon.Frame, CoordinateSystem.Planetographic, coordinate,
                RelationnalOperator.Greater, spiceValue, 0.0, Aberration.None, step).ToArray();
            AssertPlanetographic($"DSS-13 from the Moon in {moon.Frame.Name}, {coordinate} > {value}", managed, gfposc, siteWindow, dss13, moon, null);
        }
    }

    /// <summary>
    /// #682: the frame of a site built from coordinates, read from LRO, a kernel spacecraft (this class loads its SPK): it
    /// reached <c>spkezr_c</c> by name (SPICE(UNKNOWNFRAME)) and is read in J2000 and rotated by the managed frame, with and
    /// without light time, as from the Moon and the Sun in <see cref="Surface.SiteTests"/>.
    /// </summary>
    [Fact]
    public void CustomSite_FromLro_InItsOwnFrame_EqualsTheIcrfReadRotated()
    {
        var site = new Site(682, "F07CUSTOM", Earth, new Planetodetic(-117.0 * IO.Astrodynamics.Constants.Deg2Rad, 34.0 * IO.Astrodynamics.Constants.Deg2Rad, 1000.0));
        var lro = Lro;
        var start = new TimeSystem.Time(2024, 3, 1);
        foreach (var aberration in new[] { Aberration.None, Aberration.LT })
        {
            for (int k = 0; k < 3; k++)
            {
                var t = start.AddSeconds(7.0 * 3600.0 * k);
                var read = site.GetEphemeris(t, lro, site.Frame, aberration).ToStateVector();
                var expected = site.GetEphemeris(t, lro, Frames.Frame.ICRF, aberration).ToFrame(site.Frame).ToStateVector();
                Assert.Same(site.Frame, read.Frame);
                Assert.InRange((read.Position - expected.Position).Magnitude(), 0.0, 1e-6);
                Assert.InRange((read.Velocity - expected.Velocity).Magnitude(), 0.0, 1e-9);
            }
        }
    }
}
