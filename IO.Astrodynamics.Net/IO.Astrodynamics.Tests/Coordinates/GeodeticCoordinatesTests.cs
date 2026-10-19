using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Coordinates;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Coordinates;

/// <summary>
/// The exact geodetic conversions (#411) against CSPICE <c>georec_c</c>/<c>recgeo_c</c> and SOFA
/// <c>iauGd2gce</c>/<c>iauGc2gde</c>: <c>Data/GeodeticReference/geodetic_reference.json</c>, written by
/// <c>tools/geodetic_reference/geodetic_ref.c</c> on the Earth, Mars, the Moon (radii of <c>pck00011.tpc</c>) and the
/// prolate comet 103P/Hartley 2 (1000041), from −5 km to 384 400 km of altitude, and inside the evolute.
/// </summary>
public class GeodeticCoordinatesTests
{
    private const string ReferenceFile = "Data/GeodeticReference/geodetic_reference.json";

    private static readonly Lazy<(IReadOnlyList<GridPoint> Grid, IReadOnlyList<InteriorPoint> Interior)> Reference =
        new(Load);

    private readonly ITestOutputHelper _output;

    public GeodeticCoordinatesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed record GridPoint(string Body, double A, double F, double Longitude, double Latitude, double Altitude,
        Vector3 SpiceXyz, double[] SpiceGeodetic, Vector3? SofaXyz, double[] SofaGeodetic);

    private sealed record InteriorPoint(string Body, double A, double F, Vector3 Xyz, double[] SpiceGeodetic,
        Vector3 SpiceBack);

    private static (IReadOnlyList<GridPoint>, IReadOnlyList<InteriorPoint>) Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        var grid = document.RootElement.GetProperty("points").EnumerateArray().Select(p => new GridPoint(
            p.GetProperty("body").GetString(), p.GetProperty("equatorial_radius").GetDouble(),
            p.GetProperty("flattening").GetDouble(), p.GetProperty("longitude").GetDouble(),
            p.GetProperty("latitude").GetDouble(), p.GetProperty("altitude").GetDouble(),
            Vector(p.GetProperty("spice_xyz")), Numbers(p.GetProperty("spice_geodetic")),
            p.GetProperty("sofa_xyz").ValueKind == JsonValueKind.Null ? null : Vector(p.GetProperty("sofa_xyz")),
            p.GetProperty("sofa_geodetic").ValueKind == JsonValueKind.Null
                ? null
                : Numbers(p.GetProperty("sofa_geodetic")))).ToArray();
        var interior = document.RootElement.GetProperty("interior").EnumerateArray().Select(p => new InteriorPoint(
            p.GetProperty("body").GetString(), p.GetProperty("equatorial_radius").GetDouble(),
            p.GetProperty("flattening").GetDouble(), Vector(p.GetProperty("xyz")),
            Numbers(p.GetProperty("spice_geodetic")), Vector(p.GetProperty("spice_back")))).ToArray();
        return (grid, interior);
    }

    private static double[] Numbers(JsonElement element) => element.EnumerateArray().Select(e => e.GetDouble()).ToArray();

    private static Vector3 Vector(JsonElement element)
    {
        var v = Numbers(element);
        return new Vector3(v[0], v[1], v[2]);
    }

    // The scale of a position error: the distance to the centre, at least the equatorial radius
    private static double Scale(Vector3 position, double a) => System.Math.Max(position.Magnitude(), a);

    // The scale of an altitude error: the altitude, at least the equatorial radius
    private static double Scale(double altitude, double a) => System.Math.Max(System.Math.Abs(altitude), a);

    // Angles compared on the circle
    private static double AngleError(double a, double b) =>
        System.Math.Abs(System.Math.IEEERemainder(a - b, 2.0 * System.Math.PI));

    [Fact]
    public void ToBodyFixed_AgainstSpiceAndSofa()
    {
        // Arrange
        var grid = Reference.Value.Grid;
        double spice = 0.0, sofa = 0.0;

        // Act
        foreach (var p in grid)
        {
            var position = GeodeticCoordinates.ToBodyFixed(p.Longitude, p.Latitude, p.Altitude, p.A, p.F);
            spice = System.Math.Max(spice, (position - p.SpiceXyz).Magnitude() / Scale(p.SpiceXyz, p.A));
            if (p.SofaXyz.HasValue)
            {
                sofa = System.Math.Max(sofa, (position - p.SofaXyz.Value).Magnitude() / Scale(p.SofaXyz.Value, p.A));
            }
        }

        // Assert: relative to the distance to the centre (at least a), over the 3744 points
        _output.WriteLine($"Geodetic to body-fixed, {grid.Count} points: against SPICE {spice:E2}, against SOFA {sofa:E2}");
        // Measured 2026-10-19: 4.4e-16 and 2.4e-16
        Assert.True(spice < 1e-15, $"against SPICE {spice:E2}");
        Assert.True(sofa < 1e-15, $"against SOFA {sofa:E2}");
    }

    [Fact]
    public void FromBodyFixed_AgainstSpice()
    {
        // Arrange
        var grid = Reference.Value.Grid;
        double longitude = 0.0, latitude = 0.0, altitude = 0.0;

        // Act: the geodetic coordinates of the SPICE position against SPICE recgeo_c of it
        foreach (var p in grid)
        {
            var geodetic = GeodeticCoordinates.FromBodyFixed(p.SpiceXyz, p.A, p.F);
            if (System.Math.Abs(p.Latitude) < System.Math.PI / 2.0)
            {
                longitude = System.Math.Max(longitude, AngleError(geodetic.Longitude, p.SpiceGeodetic[0]));
            }

            latitude = System.Math.Max(latitude, System.Math.Abs(geodetic.Latitude - p.SpiceGeodetic[1]));
            altitude = System.Math.Max(altitude,
                System.Math.Abs(geodetic.Altitude - p.SpiceGeodetic[2]) / Scale(p.Altitude, p.A));
        }

        // Assert
        _output.WriteLine($"Body-fixed to geodetic, against SPICE: longitude {longitude:E2} rad, latitude " +
                          $"{latitude:E2} rad, altitude {altitude:E2} of max(|h|, a)");
        // Measured 2026-10-19: 0, 1.0e-15 rad, 5.7e-16
        Assert.True(longitude < 1e-15, $"longitude {longitude:E2}");
        Assert.True(latitude < 2e-15, $"latitude {latitude:E2}");
        Assert.True(altitude < 1e-15, $"altitude {altitude:E2}");
    }

    [Fact]
    public void FromBodyFixed_AgainstSofa()
    {
        // Arrange: SOFA where it resolves the latitude (its single Halley step: 3.3e-16 rad within 5 km of the
        // surface, 1.3e-11 rad at 800 km and 1.5e-10 rad at 35 786 km on Mars; tools/geodetic_reference/README.md)
        var near = Reference.Value.Grid.Where(p => p.SofaGeodetic != null && System.Math.Abs(p.Altitude) <= 5000.0).ToArray();
        double latitude = 0.0, altitude = 0.0;

        // Act
        foreach (var p in near)
        {
            var geodetic = GeodeticCoordinates.FromBodyFixed(p.SpiceXyz, p.A, p.F);
            latitude = System.Math.Max(latitude, System.Math.Abs(geodetic.Latitude - p.SofaGeodetic[1]));
            altitude = System.Math.Max(altitude, System.Math.Abs(geodetic.Altitude - p.SofaGeodetic[2]) / p.A);
        }

        // Assert
        _output.WriteLine($"Body-fixed to geodetic within 5 km of the surface, {near.Length} points, against SOFA: " +
                          $"latitude {latitude:E2} rad, altitude {altitude:E2} of a");
        // Measured 2026-10-19: 2.2e-16 rad, 3.7e-16
        Assert.True(latitude < 1e-15, $"latitude {latitude:E2}");
        Assert.True(altitude < 1e-15, $"altitude {altitude:E2}");
    }

    [Fact]
    public void RoundTrip_GeodeticToBodyFixedAndBack()
    {
        // Arrange
        var grid = Reference.Value.Grid;
        double latitude = 0.0, altitude = 0.0;

        // Act
        foreach (var p in grid)
        {
            var position = GeodeticCoordinates.ToBodyFixed(p.Longitude, p.Latitude, p.Altitude, p.A, p.F);
            var back = GeodeticCoordinates.FromBodyFixed(position, p.A, p.F);
            latitude = System.Math.Max(latitude, System.Math.Abs(back.Latitude - p.Latitude));
            altitude = System.Math.Max(altitude, System.Math.Abs(back.Altitude - p.Altitude) / Scale(p.Altitude, p.A));
        }

        // Assert
        _output.WriteLine($"Round trip: latitude {latitude:E2} rad, altitude {altitude:E2} of max(|h|, a)");
        // Measured 2026-10-19: 4.4e-16 rad, 6.7e-16
        Assert.True(latitude < 1e-15, $"latitude {latitude:E2}");
        Assert.True(altitude < 1e-15, $"altitude {altitude:E2}");
    }

    [Fact]
    public void InsideTheEvolute_TheGeodeticPointGivesThePositionBack()
    {
        // Arrange: Cartesian points from the centre to half the equatorial radius, where several normals to the
        // ellipsoid meet; the geodetic coordinates must give the position back, and agree with SPICE's
        var interior = Reference.Value.Interior;
        double closure = 0.0, latitude = 0.0, altitude = 0.0;

        // Act
        foreach (var p in interior)
        {
            var geodetic = GeodeticCoordinates.FromBodyFixed(p.Xyz, p.A, p.F);
            var back = GeodeticCoordinates.ToBodyFixed(geodetic.Longitude, geodetic.Latitude, geodetic.Altitude, p.A,
                p.F);
            closure = System.Math.Max(closure, (back - p.Xyz).Magnitude() / p.A);
            if (!(p.F == 0.0 && p.Xyz.Magnitude() == 0.0))
            {
                // Every latitude holds at the centre of a sphere: SPICE gives 0, the library π/2 as on an ellipsoid
                latitude = System.Math.Max(latitude, System.Math.Abs(geodetic.Latitude - p.SpiceGeodetic[1]));
            }

            altitude = System.Math.Max(altitude, System.Math.Abs(geodetic.Altitude - p.SpiceGeodetic[2]) / p.A);
        }

        // Assert
        _output.WriteLine($"Inside the evolute, {interior.Count} points: closure {closure:E2} of a; against SPICE " +
                          $"latitude {latitude:E2} rad, altitude {altitude:E2} of a");
        // Measured 2026-10-19: closure 1.8e-15 of a, altitude 1.8e-15 of a. The latitude departs from SPICE's by
        // 2.4e-10 rad, SPICE's: near the evolute the height is stationary in the latitude, so the latitude is
        // ill-conditioned, and SPICE's own closure there (georec_c of its recgeo_c) is 1.9e-12 of a against the
        // library's 1.8e-15; a record at twice the measurement
        Assert.True(closure < 4e-15, $"closure {closure:E2}");
        Assert.True(latitude < 5e-10, $"latitude {latitude:E2}");
        Assert.True(altitude < 4e-15, $"altitude {altitude:E2}");
    }
}
