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
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    /// <summary>
    /// The Moon from the custom site at 2024-01-01T12:00 TDB, LT: 399039.486050 km by CSPICE spkcpo (the site's
    /// ITRF93 position, the kernels of IO.Astrodynamics.Tests/Data/SolarSystem; #483). Linux x64, 2026-11-11: 3.1e-5 m
    /// off; the bound is the resolution of the reference, printed to 1e-6 km. Its window is not checked: the custom-site
    /// search is off by about 340 s (#489).
    /// </summary>
    [Fact]
    public void TheSiteExampleRuns()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;

        // Create site with explicit coordinates
        var mySite = new Site(100, "MySite", earth,
            new Planetodetic(
                Longitude: -117.0 * Constants.Deg2Rad,
                Latitude: 34.0 * Constants.Deg2Rad,
                Altitude: 1000.0  // meters
            ));

        // Get horizontal coordinates to Moon
        var epoch = new Time(2024, 1, 1, 12, 0, 0);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var horizontal = mySite.GetHorizontalCoordinates(epoch, moon, Aberration.LT);

        // Find visibility windows
        var windows = mySite.FindWindowsOnDistanceConstraint(
            new Window(new Time(2024, 1, 1), new Time(2024, 1, 2)),
            moon, RelationnalOperator.Lower, 400000000, Aberration.LT, TimeSpan.FromMinutes(10));

        _output.WriteLine($"Range {horizontal.Range:R} m, {horizontal.Range - 399039486.050:E3} m from spkcpo; {string.Join(", ", windows)}");
        Assert.Equal(399039486.050, horizontal.Range, RangeBound);
        Assert.NotEmpty(windows);
    }

    /// <summary>
    /// The TLE example builds its spacecraft (maximum operating mass 450000 kg, #483) and searches its windows without an
    /// exception; its windows have no independent reference yet, so they are not checked.
    /// </summary>
    [Fact]
    public void TheTleTrackingExampleRuns()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var issTLE = new TLE(
            "ISS (ZARYA)",
            "1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
            "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064"
        );

        // Create spacecraft from TLE
        var clock = new Clock("ISSClock", 65536);
        var iss = new Spacecraft(-25544, "ISS", 420000.0, 450000.0, clock, issTLE);

        // Track from a ground station
        var goldstone = new Site(13, "DSS-13", earth);

        var trackStart = new Time(2024, 6, 3);
        var trackEnd = trackStart.AddDays(1);

        var visibilityWindows = goldstone.FindWindowsOnDistanceConstraint(
            new Window(trackStart, trackEnd),
            iss,
            RelationnalOperator.Lower,
            2000000,  // Within 2000 km
            Aberration.LT,
            TimeSpan.FromMinutes(1)
        );

        _output.WriteLine($"{string.Join(", ", visibilityWindows)}");
        Assert.NotEmpty(visibilityWindows);
    }

    private const double RangeBound = 1e-3;
}
