// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// <see cref="FrameTransformationSolver"/> on each kind of input: a constant rotation (ECLIPJ2000), a change of centre,
/// geodetic coordinates, and a rotating frame computed with the case's kernel.
/// </summary>
/// <remarks>
/// The references are closed forms (R1 of the IAU 1976 obliquity, the geodetic coordinates of a point built from
/// them) and the geometric state of the Moon from the Earth that CSPICE N0067 gives with <c>de440s.bsp</c> (spiceypy
/// 8.3.0 <c>spkezr</c>, J2000, no aberration, 2021-03-20T12:00:00 TDB).
/// </remarks>
public sealed class FrameTransformationSolverTests : IDisposable
{
    private const string Epoch = "2021-03-20T12:00:00.000 TDB";
    private const string Kernel = "earth_latest_high_prec.bpc";
    private static readonly double[] R = { 7000.0, 1000.0, 500.0 };
    private static readonly double[] V = { -0.5, 7.2, 1.1 };

    // spkezr("MOON", 669513600.0, "J2000", "NONE", "EARTH"), km and km/s
    private static readonly double[] MoonPosition = { 86718.1012142888, 359313.09990042244, 158604.73563385216 };
    private static readonly double[] MoonVelocity = { -0.9517757163607233, 0.1289675540355653, 0.14898914905924765 };

    private readonly string _directory;
    private readonly ITestOutputHelper _output;
    private readonly FrameTransformationSolver _solver;

    public FrameTransformationSolverTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(Lot1Fixture.KernelsPath));
        _directory = Directory.CreateTempSubdirectory("frame-solver-").FullName;
        Directory.CreateDirectory(Path.Combine(_directory, "data"));
        File.Copy(Path.Combine(Lot1Fixture.Root, "data", Kernel), Path.Combine(_directory, "data", Kernel));
        _solver = new FrameTransformationSolver(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    private static CaseInput Case(string fromFrame, string toFrame, double[] r, double[] v, string fromCenter = "Earth",
        string toCenter = "Earth", string kernels = "[]", string ellipsoid = "")
    {
        var yaml = $"""
                    id: "frame_transformation_test"
                    schema_version: "v1"
                    version: "0.1.0"
                    category: "frame_transformation"
                    created: "2026-10-25"
                    description: "test"
                    metadata:
                      ephemeris_kernel: "none"
                      reference_frame: "{toFrame}"
                      time_scale: "TDB"
                    inputs:
                      epoch: "{Epoch}"
                      from_frame: "{fromFrame}"
                      to_frame: "{toFrame}"
                      from_center: "{fromCenter}"
                      to_center: "{toCenter}"
                      state:
                        position_km: [{r[0]:R}, {r[1]:R}, {r[2]:R}]
                        velocity_km_s: [{v[0]:R}, {v[1]:R}, {v[2]:R}]
                      kernels: {kernels}
                    {ellipsoid}
                    """;
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build().Deserialize<CaseInput>(yaml);
    }

    private static double Distance(double[] a, double[] b) => System.Math.Sqrt(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum());

    // R1(eps): the rotation about x from ICRF to the ecliptic of J2000
    private static double[] Ecliptic(double[] u, double sign)
    {
        double epsilon = 84381.448 / 3600.0 * System.Math.PI / 180.0;
        double c = System.Math.Cos(epsilon), s = sign * System.Math.Sin(epsilon);
        return new[] { u[0], c * u[1] + s * u[2], -s * u[1] + c * u[2] };
    }

    [Fact]
    public void Category_IsFrameTransformation()
    {
        Assert.Equal("frame_transformation", _solver.Category);
    }

    [Fact]
    public void IcrfToEclipticAndBack_IsTheRotationOfTheObliquityOfJ2000()
    {
        // Act
        var forward = _solver.Solve(Case("ICRF", "ECLIPJ2000", R, V));
        var backward = _solver.Solve(Case("ECLIPJ2000", "ICRF", R, V));

        // Assert: a constant rotation, no transport term
        var differences = new[]
        {
            Distance(Ecliptic(R, 1.0), (double[])forward["position_km"]) / 7088.0,
            Distance(Ecliptic(V, 1.0), (double[])forward["velocity_km_s"]) / 7.3,
            Distance(Ecliptic(R, -1.0), (double[])backward["position_km"]) / 7088.0,
            Distance(Ecliptic(V, -1.0), (double[])backward["velocity_km_s"]) / 7.3
        };
        _output.WriteLine($"relative differences to R1(eps): {string.Join(", ", differences.Select(d => d.ToString("E2")))}");
        Assert.Equal(new[] { "position_km", "velocity_km_s" }, forward.Keys.OrderBy(k => k));
        Assert.All(differences, d => Assert.InRange(d, 0.0, 2e-15));
    }

    [Fact]
    public void AChangeOfCentre_IsTheGeometricTranslationInIcrf()
    {
        // Arrange: the state of a probe 7000 km from the Earth, from the Moon, and back
        var fromMoon = R.Zip(MoonPosition, (r, m) => r - m).ToArray();
        var fromMoonVelocity = V.Zip(MoonVelocity, (v, m) => v - m).ToArray();

        // Act
        var toMoon = _solver.Solve(Case("ICRF", "ICRF", R, V, toCenter: "Moon"));
        var toEarth = _solver.Solve(Case("ICRF", "ICRF", fromMoon, fromMoonVelocity, fromCenter: "Moon"));

        // Assert: within the rounding of 4e5 km and of the km/m conversions
        _output.WriteLine($"to the Moon: {Distance(fromMoon, (double[])toMoon["position_km"]):E2} km, " +
                          $"{Distance(fromMoonVelocity, (double[])toMoon["velocity_km_s"]):E2} km/s; back: " +
                          $"{Distance(R, (double[])toEarth["position_km"]):E2} km, {Distance(V, (double[])toEarth["velocity_km_s"]):E2} km/s");
        Assert.InRange(Distance(fromMoon, (double[])toMoon["position_km"]), 0.0, 1e-9);
        Assert.InRange(Distance(fromMoonVelocity, (double[])toMoon["velocity_km_s"]), 0.0, 1e-14);
        Assert.InRange(Distance(R, (double[])toEarth["position_km"]), 0.0, 1e-9);
        Assert.InRange(Distance(V, (double[])toEarth["velocity_km_s"]), 0.0, 1e-14);
    }

    [Fact]
    public void AnEllipsoid_GivesTheGeodeticCoordinatesOfThePosition()
    {
        // Arrange: the body-fixed point of latitude 45 deg, longitude -150 deg and altitude 400 km on the ellipsoid of
        // pck00011, x = (N + h) cos(lat) cos(lon), y = (N + h) cos(lat) sin(lon), z = (N (1 - e^2) + h) sin(lat)
        const double a = 6378.1366, f = 0.0033528131084554157, latitude = 45.0, longitude = -150.0, altitude = 400.0;
        double e2 = f * (2.0 - f), phi = latitude * System.Math.PI / 180.0, lambda = longitude * System.Math.PI / 180.0;
        double n = a / System.Math.Sqrt(1.0 - e2 * System.Math.Sin(phi) * System.Math.Sin(phi));
        var r = new[]
        {
            (n + altitude) * System.Math.Cos(phi) * System.Math.Cos(lambda), (n + altitude) * System.Math.Cos(phi) * System.Math.Sin(lambda),
            (n * (1.0 - e2) + altitude) * System.Math.Sin(phi)
        };

        // Act
        var outputs = _solver.Solve(Case("ITRF93", "ITRF93", r, new[] { 0.0, 0.0, 0.0 }, ellipsoid: $"""
              ellipsoid:
                equatorial_radius_km: {a:R}
                flattening: {f:R}
            """));

        // Assert: the frame and the centre do not change, so neither does the state
        _output.WriteLine($"latitude {(double)outputs["latitude_deg"] - latitude:E2} deg, longitude {(double)outputs["longitude_deg"] - longitude:E2} deg, " +
                          $"altitude {(double)outputs["altitude_km"] - altitude:E2} km");
        Assert.InRange(Distance(r, (double[])outputs["position_km"]), 0.0, 1e-12);
        Assert.Equal(latitude, (double)outputs["latitude_deg"], 1e-11);
        Assert.Equal(longitude, (double)outputs["longitude_deg"], 1e-11);
        Assert.Equal(altitude, (double)outputs["altitude_km"], 1e-9);
    }

    [Fact]
    public void ARotatingFrame_IsComputedWithTheCaseKernel_WhichIsThenUnloaded()
    {
        // Act: the same state with the suite's bpc, and with the runner's
        var withCaseKernel = _solver.Solve(Case("ICRF", "ITRF93", R, V, kernels: $"[\"{Kernel}\"]"));
        var withRunnerKernel = _solver.Solve(Case("ICRF", "ITRF93", R, V));

        // Assert: 4.68e-12 rad between the two files at this epoch, 3.3e-8 km at 7088 km (design of #458, section 1)
        double difference = Distance((double[])withCaseKernel["position_km"], (double[])withRunnerKernel["position_km"]);
        _output.WriteLine($"suite bpc - runner bpc: {difference:E2} km");
        Assert.InRange(difference, 1e-9, 1e-6);
        Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(),
            k => k.FullName == new FileInfo(Path.Combine(_directory, "data", Kernel)).FullName);
    }

    [Theory]
    [InlineData("EME2000", "ICRF", "Earth")]
    [InlineData("ICRF", "TIRS", "Earth")]
    [InlineData("ICRF", "ICRF", "Mars")]
    public void AFrameOrACentreOutsideTheCategory_IsAnError(string fromFrame, string toFrame, string toCenter)
    {
        Assert.Throws<ArgumentException>(() => _solver.Solve(Case(fromFrame, toFrame, R, V, toCenter: toCenter)));
    }
}
