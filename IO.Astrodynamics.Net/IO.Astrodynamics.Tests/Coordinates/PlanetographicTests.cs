// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Tests.Body;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Coordinates;

/// <summary>
/// The planetographic coordinates of the managed searches (#682) against CSPICE <c>recpgr_c</c>, called in the process
/// (<see cref="CspiceDirect"/>), with the radii of the bodies in <c>pck00011.tpc</c>: the longitude in [0, 2π), positive
/// west for a prograde body, positive east for a retrograde one and for the Earth, the Moon and the Sun (<c>recpgr_c</c>
/// Detailed_Output <c>lon</c> and Particulars); the latitude and the altitude on the spheroid (Detailed_Output <c>lat</c>,
/// <c>alt</c>). No kernel of <c>Data/SolarSystem</c> defines <c>BODY&lt;id&gt;_PGR_POSITIVE_LON</c>, the override that
/// <c>recpgr_c</c> reads and the managed searches do not.
/// </summary>
/// <remarks>
/// The Linux build of the native library exports the CSPICE symbols, the Windows DLL only the proxies: the Validation
/// category, which the CI runs on Linux.
/// </remarks>
public class PlanetographicTests
{
    // #682. Twice the worst on Linux x64 rounded up (CI run 43591652055, 2026-11-14), the test running on Linux only:
    // 1.665e-16 rad (Uranus) and 4.381e-16 relative (the Earth); the angles within an ulp of π/2, the altitude within two ulp
    private const double AngleLimit = 3.4e-16;
    private const double AltitudeRelativeLimit = 8.8e-16;

    private readonly ITestOutputHelper _output;

    public PlanetographicTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    public static IEnumerable<object[]> Bodies()
    {
        // The sense of recpgr_c, measured on the kernels of Data/SolarSystem: west for Mercury, Mars and Pluto (prograde),
        // east for Venus and Uranus (retrograde) and for the Sun, the Earth and the Moon (its exceptions)
        yield return [10, "SUN", false];
        yield return [199, "MERCURY", true];
        yield return [299, "VENUS", false];
        yield return [301, "MOON", false];
        yield return [399, "EARTH", false];
        yield return [499, "MARS", true];
        yield return [799, "URANUS", false];
        yield return [999, "PLUTO", true];
    }

    // The points, in units of the equatorial radius: on the equator at longitudes 0, ±π/2 and π (with a signed zero),
    // at mid latitudes in the four quadrants, near the poles, on the polar axis, below and above the spheroid
    private static IEnumerable<Vector3> Points(double polarRatio)
    {
        foreach (double scale in new[] { 0.5, 1.0, 1.2, 60.0 })
        {
            yield return new Vector3(scale, 0.0, 0.0);
            yield return new Vector3(0.0, scale, 0.0);
            yield return new Vector3(0.0, -scale, 0.0);
            yield return new Vector3(-scale, 0.0, 0.0);
            yield return new Vector3(-scale, -0.0, 0.0);
            yield return new Vector3(0.6 * scale, 0.5 * scale, 0.4 * scale);
            yield return new Vector3(-0.6 * scale, 0.5 * scale, -0.4 * scale);
            yield return new Vector3(-0.6 * scale, -0.5 * scale, 0.4 * scale);
            yield return new Vector3(0.6 * scale, -0.5 * scale, -0.4 * scale);
            yield return new Vector3(1e-3 * scale, 2e-3 * scale, polarRatio * scale);
            yield return new Vector3(-1e-3 * scale, -2e-3 * scale, -polarRatio * scale);
            yield return new Vector3(0.0, 0.0, polarRatio * scale * 1.5);
            yield return new Vector3(0.0, 0.0, -polarRatio * scale * 1.5);
        }
    }

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(Bodies))]
    public void Evaluation_EqualsRecpgr(int naifId, string name, bool positiveWest)
    {
        var body = new CelestialBody(naifId);
        var epoch = new TimeSystem.Time(2024, 3, 1);
        Assert.Equal(positiveWest, GeodeticCoordinates.IsPlanetographicLongitudePositiveWest(body, epoch));
        double re = body.EquatorialRadius;
        double f = body.Flattening;
        double worstAngle = 0.0, worstAltitude = 0.0;
        foreach (var unit in Points(1.0 - f))
        {
            var position = unit * re;
            var managed = GeodeticCoordinates.ToPlanetographic(position, re, f, GeodeticCoordinates.IsPlanetographicLongitudePositiveWest(body, epoch));
            var (lon, lat, alt) = CspiceDirect.Recpgr(name, [position.X / 1000.0, position.Y / 1000.0, position.Z / 1000.0], re / 1000.0, f);
            Assert.InRange(managed.Longitude, 0.0, IO.Astrodynamics.Constants._2PI);
            // The longitudes as angles: 0 and 2π are one direction
            double dLon = System.Math.Abs(managed.Longitude - lon);
            dLon = System.Math.Min(dLon, IO.Astrodynamics.Constants._2PI - dLon);
            worstAngle = System.Math.Max(worstAngle, System.Math.Max(dLon, System.Math.Abs(managed.Latitude - lat)));
            worstAltitude = System.Math.Max(worstAltitude, System.Math.Abs(managed.Altitude - alt * 1000.0) / System.Math.Max(re, System.Math.Abs(alt * 1000.0)));
            Assert.True(dLon <= AngleLimit, $"{name} {unit}: longitude {managed.Longitude} against {lon}");
            Assert.True(System.Math.Abs(managed.Latitude - lat) <= AngleLimit, $"{name} {unit}: latitude {managed.Latitude} against {lat}");
            Assert.True(System.Math.Abs(managed.Altitude - alt * 1000.0) <= AltitudeRelativeLimit * System.Math.Max(re, System.Math.Abs(alt * 1000.0)),
                $"{name} {unit}: altitude {managed.Altitude} m against {alt} km");
        }

        _output.WriteLine($"MEASURE #682 planetographic {name}: worst angle {worstAngle:E3} rad, worst altitude {worstAltitude:E3} relative");
    }
}
