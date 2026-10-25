// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// <see cref="OrbitalElementsSolver"/> on each input path: a state, whose outputs are the elements defined for it, and
/// elements with the mean anomaly, the true anomaly or as modified equinoctial elements, whose outputs are the state.
/// </summary>
/// <remarks>
/// The references are closed forms of spec.md section 11.1 evaluated here, and the state of
/// <c>orbital_elements_generic_001</c>, whose elements as GMAT R2026a gives them (its golden) must give it back.
/// </remarks>
public class OrbitalElementsSolverTests
{
    private const string Gm = "398600.4355070226";
    private const double Mu = 398600.4355070226;
    private static readonly double[] R = { 7000.0, 1000.0, 500.0 };
    private static readonly double[] V = { -0.5, 7.2, 1.1 };

    private readonly OrbitalElementsSolver _solver = new();
    private readonly ITestOutputHelper _output;

    public OrbitalElementsSolverTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(Lot1Fixture.KernelsPath));
    }

    private static CaseInput Case(string orbit, string gm = Gm, string centralBody = "Earth")
    {
        var yaml = $"""
                    id: "orbital_elements_test"
                    schema_version: "v1"
                    version: "0.1.0"
                    category: "orbital_elements"
                    created: "2026-10-25"
                    description: "test"
                    metadata:
                      ephemeris_kernel: "none"
                      reference_frame: "ICRF"
                      time_scale: "TDB"
                    inputs:
                      epoch: "2021-03-20T12:00:00.000 TDB"
                      central_body: "{centralBody}"
                      gm_km3_s2: {gm}
                      orbit:
                    {orbit}
                    """;
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build().Deserialize<CaseInput>(yaml);
    }

    private static string State(double[] r, double[] v) =>
        $"""
             type: "state_vector"
             position_km: [{r[0]:R}, {r[1]:R}, {r[2]:R}]
             velocity_km_s: [{v[0]:R}, {v[1]:R}, {v[2]:R}]
         """;

    private static double[] Cross(double[] a, double[] b) =>
        new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

    private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    private static double Norm(double[] a) => System.Math.Sqrt(Dot(a, a));

    private void AssertRelative(double[] expected, double[] actual, double tolerance)
    {
        double relative = Norm(expected.Zip(actual, (e, a) => e - a).ToArray()) / Norm(expected);
        _output.WriteLine($"relative difference {relative:E2}, bound {tolerance:E1}");
        Assert.InRange(relative, 0.0, tolerance);
    }

    [Fact]
    public void Category_IsOrbitalElements()
    {
        Assert.Equal("orbital_elements", _solver.Category);
    }

    [Fact]
    public void AnEllipse_GivesTheElementsOfSpecSection11_ByTheirDefinitions()
    {
        // Act
        var outputs = _solver.Solve(Case(State(R, V)));

        // Assert: the 27 outputs of an elliptic, inclined orbit (spec.md section 11.1), no state, no hyperbolic anomaly
        Assert.Equal(27, outputs.Count);
        Assert.DoesNotContain("position_km", outputs.Keys);
        Assert.DoesNotContain("hyperbolic_anomaly_deg", outputs.Keys);

        // h = r x v, xi = v^2/2 - mu/r, a = -mu/(2 xi), p = |h|^2/mu, i from h, right ascension and declination of r
        var h = Cross(R, V);
        double xi = Dot(V, V) / 2.0 - Mu / Norm(R);
        AssertRelative(h, (double[])outputs["angular_momentum_km2_s"], 1e-15);
        Assert.Equal(xi, (double)outputs["specific_energy_km2_s2"], 1e-13);
        Assert.Equal(-Mu / (2.0 * xi), (double)outputs["semi_major_axis_km"], 1e-9);
        Assert.Equal(Dot(h, h) / Mu, (double)outputs["semi_latus_rectum_km"], 1e-9);
        Assert.Equal(System.Math.Atan2(System.Math.Sqrt(h[0] * h[0] + h[1] * h[1]), h[2]) * 180.0 / System.Math.PI,
            (double)outputs["inclination_deg"], 1e-12);
        Assert.Equal(System.Math.Atan2(R[1], R[0]) * 180.0 / System.Math.PI, (double)outputs["right_ascension_deg"], 1e-12);
        Assert.Equal(System.Math.Asin(R[2] / Norm(R)) * 180.0 / System.Math.PI, (double)outputs["declination_deg"], 1e-12);
        Assert.Equal(2.0 * System.Math.PI * System.Math.Sqrt(System.Math.Pow(-Mu / (2.0 * xi), 3) / Mu), (double)outputs["period_s"], 1e-6);
    }

    [Fact]
    public void AHyperbola_GivesItsSignedAnomalies_AndNoneOfTheEllipse()
    {
        // Arrange: outbound (r.v > 0) and inbound (r.v < 0) at the same distance and speed
        var outbound = _solver.Solve(Case(State(new[] { 7000.0, 0.0, 0.0 }, new[] { 3.0, 11.0, 1.0 })));
        var inbound = _solver.Solve(Case(State(new[] { 7000.0, 0.0, 0.0 }, new[] { -3.0, 11.0, 1.0 })));

        // Assert
        foreach (var outputs in new[] { outbound, inbound })
        {
            Assert.All(new[] { "mean_anomaly_deg", "eccentric_anomaly_deg", "mean_longitude_deg", "apoapsis_radius_km", "period_s", "apoapsis_speed_km_s" },
                name => Assert.DoesNotContain(name, outputs.Keys));
            Assert.True((double)outputs["semi_major_axis_km"] < 0.0);

            // N = e sinh H - H, with tanh(H/2) = sqrt((e-1)/(e+1)) tan(nu/2)
            double e = (double)outputs["eccentricity"];
            double nu = (double)outputs["true_anomaly_deg"] * System.Math.PI / 180.0;
            double hyperbolic = 2.0 * System.Math.Atanh(System.Math.Sqrt((e - 1.0) / (e + 1.0)) * System.Math.Tan(nu / 2.0));
            Assert.Equal(hyperbolic * 180.0 / System.Math.PI, (double)outputs["hyperbolic_anomaly_deg"], 1e-11);
            Assert.Equal((e * System.Math.Sinh(hyperbolic) - hyperbolic) * 180.0 / System.Math.PI, (double)outputs["hyperbolic_mean_anomaly_deg"], 1e-11);
        }

        Assert.True((double)outbound["hyperbolic_anomaly_deg"] > 0.0);
        Assert.True((double)inbound["hyperbolic_anomaly_deg"] < 0.0);
        Assert.True((double)inbound["hyperbolic_mean_anomaly_deg"] < 0.0);
    }

    [Fact]
    public void AnEquatorialOrbit_HasNoNode()
    {
        var prograde = _solver.Solve(Case(State(new[] { 7000.0, 1000.0, 0.0 }, new[] { -0.5, 7.6, 0.0 })));
        var retrograde = _solver.Solve(Case(State(new[] { 7000.0, 1000.0, 0.0 }, new[] { 0.5, -7.6, 0.0 })));

        Assert.All(new[] { prograde, retrograde }, outputs => Assert.All(new[] { "raan_deg", "argp_deg", "argument_of_latitude_deg" },
            name => Assert.DoesNotContain(name, outputs.Keys)));
        Assert.Contains("longitude_of_periapsis_deg", prograde.Keys);
        Assert.Equal(0.0, (double)prograde["mod_equinoctial_h"]);
        Assert.Equal(0.0, (double)prograde["mod_equinoctial_k"]);

        // i = 180: h and k are not defined, f and g are
        Assert.Equal(180.0, (double)retrograde["inclination_deg"], 12);
        Assert.DoesNotContain("mod_equinoctial_h", retrograde.Keys);
        Assert.DoesNotContain("mod_equinoctial_k", retrograde.Keys);
        Assert.Contains("mod_equinoctial_f", retrograde.Keys);
    }

    [Fact]
    public void APositionOnThePolarAxis_HasNoRightAscension()
    {
        var outputs = _solver.Solve(Case(State(new[] { 0.0, 0.0, 7000.0 }, new[] { 7.5, 0.0, 0.0 })));

        Assert.DoesNotContain("right_ascension_deg", outputs.Keys);
        Assert.Equal(90.0, (double)outputs["declination_deg"], 12);
    }

    // The elements of orbital_elements_generic_001 as GMAT R2026a gives them (its golden), which must give back its state
    public static IEnumerable<object[]> ElementsOfTheGenericCase()
    {
        yield return new object[]
        {
            """
                type: "keplerian_true_anomaly"
                a_km: 6737.619214181522
                e: 0.09716583155334296
                i_deg: 9.298489347799013
                raan_deg: 342.5434418663331
                argp_deg: 258.8626422249566
                ta_deg: 127.02044358469809
            """
        };
        yield return new object[]
        {
            """
                type: "keplerian"
                a_km: 6737.619214181522
                e: 0.09716583155334296
                i_deg: 9.298489347799013
                raan_deg: 342.5434418663331
                argp_deg: 258.8626422249566
                ma_deg: 117.73397978094407
            """
        };
        yield return new object[]
        {
            """
                type: "modified_equinoctial"
                p_km: 6674.008011597196
                f: -0.04650343332891728
                g: -0.08531488445796294
                h: 0.07757782884550424
                k: -0.02439554366210825
                true_longitude_deg: 8.426527675987785
            """
        };
    }

    [Theory]
    [MemberData(nameof(ElementsOfTheGenericCase))]
    public void Elements_GiveTheirState_AndNothingElse(string orbit)
    {
        // Act
        var outputs = _solver.Solve(Case(orbit));

        // Assert: GMAT's 17 digits and the conditioning of the conversion allow a few 1e-16 (5.3e-16 at most, Linux);
        // the Kepler solver of the mean-anomaly input, which stops at a correction of 1e-6 rad (#453), is exact enough
        // at e = 0.097
        Assert.Equal(new[] { "position_km", "velocity_km_s" }, outputs.Keys.OrderBy(k => k));
        AssertRelative(R, (double[])outputs["position_km"], 1e-14);
        AssertRelative(V, (double[])outputs["velocity_km_s"], 1e-14);
    }

    [Fact]
    public void AHyperbolaGivenWithItsTrueAnomaly_GivesTheStateOfTheConic()
    {
        // Arrange: a < 0 for e > 1; r = p / (1 + e cos nu), p = a (1 - e^2), and the energy -mu / (2a)
        const double a = -20000.0, e = 1.5, nu = 60.0;
        var outputs = _solver.Solve(Case($"""
                                              type: "keplerian_true_anomaly"
                                              a_km: {a:R}
                                              e: {e:R}
                                              i_deg: 30.0
                                              raan_deg: 40.0
                                              argp_deg: 50.0
                                              ta_deg: {nu:R}
                                          """));

        // Assert
        var r = (double[])outputs["position_km"];
        var v = (double[])outputs["velocity_km_s"];
        double p = a * (1.0 - e * e);
        Assert.Equal(p / (1.0 + e * System.Math.Cos(nu * System.Math.PI / 180.0)), Norm(r), 1e-9);
        Assert.Equal(-Mu / (2.0 * a), Dot(v, v) / 2.0 - Mu / Norm(r), 1e-12);
    }

    [Fact]
    public void AnOutputTheFrameworkCannotCompute_IsAnErrorOnlyWhenTheGoldenHoldsIt()
    {
        // Arrange: at periapsis 0.1 m/s below the escape speed, e = r v² / μ - 1 = 1 - 5e-7, an ellipse whose period
        // spec.md section 11.1 defines; the framework takes it as parabolic (|e - 1| < 1e-6), so Period() overflows
        const double speed = 10.671729487052813;
        var caseInput = Case(State(new[] { 7000.0, 0.0, 0.0 }, new[] { 0.0, speed, 0.0 }));
        double eccentricity = 7000.0 * speed * speed / Mu - 1.0;
        var defaults = new Dictionary<string, TolerancePair>
        {
            ["oe_eccentricity"] = new() { AbsTol = 1.4e-15, RelTol = 0.0 },
            ["oe_period_s"] = new() { AbsTol = 2.0e-7, RelTol = 0.0 }
        };

        // Act
        var outputs = _solver.Solve(caseInput);

        // Assert: the other outputs are computed, and a golden without the period passes
        Assert.IsType<OverflowException>(outputs["period_s"]);
        var measure = OutputComparer.Compare(caseInput, outputs,
            JsonDocument.Parse($"{{\"eccentricity\": {eccentricity.ToString("R", CultureInfo.InvariantCulture)}}}").RootElement,
            defaults).Single();
        _output.WriteLine($"eccentricity {(double)outputs["eccentricity"]:R}, closed form {eccentricity:R}, ratio {measure.Ratio:R}");
        Assert.True(measure.Passed);

        // A golden holding the period is an ERROR naming it, with the framework's exception
        var exception = Assert.Throws<InvalidOperationException>(() => OutputComparer.Compare(caseInput, outputs,
            JsonDocument.Parse("{\"eccentricity\": 1.0, \"period_s\": 1.0e10}").RootElement, defaults));
        Assert.Contains("period_s", exception.Message);
        Assert.Contains("cannot compute", exception.Message);
        Assert.IsType<OverflowException>(exception.InnerException);
    }

    [Fact]
    public void AGravitationalParameterOtherThanTheKernels_IsAnError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _solver.Solve(Case(State(R, V), gm: "398600.43550702266")));

        Assert.Contains("398600435507022.7", exception.Message);
        Assert.Contains("398600435507022.6", exception.Message);
    }

    [Fact]
    public void ACentralBodyOtherThanTheEarth_IsAnError()
    {
        Assert.Throws<ArgumentException>(() => _solver.Solve(Case(State(R, V), centralBody: "Moon")));
    }

    [Fact]
    public void AnOrbitTypeOfAnotherCategory_IsAnError()
    {
        var exception = Assert.Throws<ArgumentException>(() => _solver.Solve(Case("""
                                                                                      type: "tle"
                                                                                      name: "ISS"
                                                                                      line1: "1"
                                                                                      line2: "2"
                                                                                  """)));

        Assert.Contains("TleOrbit", exception.Message);
    }
}
