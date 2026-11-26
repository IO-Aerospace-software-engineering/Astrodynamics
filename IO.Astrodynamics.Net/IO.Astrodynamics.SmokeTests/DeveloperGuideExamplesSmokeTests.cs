using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.SmokeTests;

/// <summary>
/// The two examples of DEVELOPER_GUIDE.md that #483 corrected, the Site example (section "Site") and the TLE tracking
/// example (section "TLE-Based Spacecraft Tracking"), against the packed package. Keep both in step: a change to one
/// is a change to the other.
/// </summary>
[Collection(KernelCollection.Name)]
public class DeveloperGuideExamplesSmokeTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");
    private readonly ITestOutputHelper _output;

    public DeveloperGuideExamplesSmokeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceApi.Instance.LoadKernels(SolarSystemKernelPath);
    }

    /// <summary>
    /// The Moon from the custom site at 2024-01-01T12:00 TDB, LT: 399039.486050 km by CSPICE spkcpo (the site's
    /// ITRF93 position, the kernels of IO.Astrodynamics.Tests/Data/SolarSystem; #483). Linux x64, 2026-11-11: 3.1e-5 m
    /// off; the bound is the resolution of the reference, printed to 1e-6 km. Its window, the site within 400000 km of
    /// the Moon with LT: 757374144.9969522 to 757390843.0285655 s TDB from J2000 by CSPICE spkcpt_c, 60 s samples and a
    /// bisection to adjacent doubles (tools/light_time_reference, "guide_window_lt"; #489: before 11.0.0 about 340 s
    /// wider at each end). Linux x64, 2026-11-17, the library's tests of the same window: 0 s and 1.0e-7 s off.
    /// </summary>
    [Fact]
    public void TheSiteExampleRuns()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;

        // Create site with explicit coordinates
        var mySite = new Site(100, "MySite", earth,
            new Planetodetic(
                longitude: -117.0 * Constants.Deg2Rad,
                latitude: 34.0 * Constants.Deg2Rad,
                altitude: 1000.0  // meters
            ));

        // Get horizontal coordinates to Moon
        var epoch = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var horizontal = mySite.GetHorizontalCoordinates(epoch, moon, Aberration.LT);

        // Find visibility windows
        var windows = moon.FindDistanceWindows(
            new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 1, 2, TimeScale.TDB)),
            mySite, RelationalOperator.LessThan, 400000000,
            new WindowSearchOptions { Step = TimeSpan.FromMinutes(10), Aberration = Aberration.LT });

        _output.WriteLine($"Range {horizontal.Range:R} m, {horizontal.Range - 399039486.050:E3} m from spkcpo; {string.Join(", ", windows)}");
        Assert.Equal(399039486.050, horizontal.Range, RangeBound);
        var window = Assert.Single(windows);
        double start = (window.StartDate - Time.CreateTdb(757374144.9969522)).TotalSeconds;
        double end = (window.EndDate - Time.CreateTdb(757390843.0285655)).TotalSeconds;
        _output.WriteLine($"MEASURE window start {start:E3} s, end {end:E3} s from spkcpt_c");
        Assert.InRange(start, -WindowBound, WindowBound);
        Assert.InRange(end, -WindowBound, WindowBound);
    }

    /// <summary>
    /// The TLE example builds its spacecraft (maximum operating mass 450000 kg, #483) and searches its windows without an
    /// exception; its windows have no independent reference yet, so they are not checked.
    /// </summary>
    [Fact]
    public void TheTleTrackingExampleRuns()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var issTLE = new Tle(
            "ISS (ZARYA)",
            "1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
            "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064"
        );

        // Create spacecraft from TLE
        var clock = new Clock("ISSClock", 65536);
        var iss = new Spacecraft(-25544, "ISS", 420000.0, issTLE, 450000.0, clock);

        // Track from a ground station
        var goldstone = new Site(13, "DSS-13", earth);

        var trackStart = new Time(2024, 6, 3, TimeScale.TDB);
        var trackEnd = trackStart.AddDays(1);

        var visibilityWindows = iss.FindDistanceWindows(
            new Window(trackStart, trackEnd),
            goldstone,
            RelationalOperator.LessThan,
            2000000,  // Within 2000 km
            new WindowSearchOptions { Step = TimeSpan.FromMinutes(1), Aberration = Aberration.LT }
        );

        _output.WriteLine($"{string.Join(", ", visibilityWindows)}");
        Assert.NotEmpty(visibilityWindows);
    }

    private const double RangeBound = 1e-3;

    // Twice the worst of the library's test of the window (LightTimeCorrectionTests.GuideWindowBound): 1.0e-7 s, one tick,
    // on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669); the smoke tests run in cd.yml alone, so this test's
    // own line is in no CI log of the test job
    private const double WindowBound = 2.0e-7;
}
