// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The solvers of the earlier categories (spec.md section 12 of the suite, conformance-tests#25) on the inputs that
/// conformance-tests#25 added, and on the inputs of the suite before it: the GM of the eclipse trajectory, the declared
/// sidereal period and the GM of the geosynchronous orbit, the ellipsoid of the tracking station, the light-time
/// corrected directions of the TRIAD. The goldens are those of the suite's cases, GMAT R2026a's and CSPICE's.
/// </summary>
public sealed class EarlierCategoriesSolverTests : IDisposable
{
    private const string KernelsPath = "Data/SolarSystem";

    private readonly ITestOutputHelper _output;
    private readonly string _suite;

    public EarlierCategoriesSolverTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(KernelsPath));

        // A suite whose data/ holds the field file that carries the GM of an eclipse case
        _suite = Path.Combine(Path.GetTempPath(), "earlier-categories-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_suite, "data"));
        File.Copy(Path.Combine(KernelsPath, "EGM2008_to70_TideFree"), Path.Combine(_suite, "data", "EGM2008_to70_TideFree"));
    }

    public void Dispose()
    {
        Directory.Delete(_suite, true);
    }

    private static CaseInput Parse(string category, string inputs)
    {
        var yaml = $"""
                    id: "{category}_test"
                    schema_version: "v1"
                    version: "0.1.0"
                    category: "{category}"
                    created: "2026-10-27"
                    description: "test"
                    metadata:
                      ephemeris_kernel: "de440s.bsp"
                      reference_frame: "ICRF"
                      time_scale: "TDB"
                    inputs:
                    {inputs}
                    """;
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build()
            .Deserialize<CaseInput>(yaml);
    }

    private static string Eclipse(string gm) => $"""
                                                   epoch: "2026-03-20T00:00:00.000 TDB"
                                                   orbit:
                                                     type: "keplerian"
                                                     a_km: 7000.0
                                                     e: 0.001
                                                     i_deg: 28.5
                                                     raan_deg: 45.0
                                                     argp_deg: 0.0
                                                     ma_deg: 0.0
                                                   search_window:
                                                     start: "2026-03-20T00:00:00.000 TDB"
                                                     end: "2026-03-20T12:00:00.000 TDB"
                                                   occulting_body: "Earth"
                                                   light_source: "Sun"
                                                 {gm}
                                                 """;

    [Fact]
    public void Eclipse_WithTheGmOfTheCase_ReproducesGmat_AndWithoutItTakesTheKernelGm()
    {
        // Arrange: the golden of eclipse 001 (GMAT R2026a's EclipseLocator, two-body with GM 398600.4415 km^3/s^2, the
        // pck00011 ellipsoid in IAU_EARTH, geometric; epochs printed to the millisecond)
        var golden = new[]
        {
            EpochParserTime("2026-03-20T00:18:37.494 UTC"), EpochParserTime("2026-03-20T00:18:46.864 UTC"),
            EpochParserTime("2026-03-20T00:53:10.862 UTC"), EpochParserTime("2026-03-20T00:53:20.204 UTC")
        };
        string[] instants = ["penumbra_entry", "umbra_entry", "umbra_exit", "penumbra_exit"];

        // Act
        var withGm = new EclipseSolver(_suite).Solve(Parse("eclipse", Eclipse("  gm_km3_s2: 398600.4415")));
        var kernelGm = new EclipseSolver(_suite).Solve(Parse("eclipse", Eclipse("")));

        // Assert: within the millisecond of the golden's print with the case's GM; the kernel GM, 1.5035e-8 of it
        // smaller, slows the mean motion by 7.52e-9 of it and delays the epochs by that fraction of the time since the
        // epoch of the elements (8.4e-6 s at the penumbra entry, 2.4e-5 s at the exit), located to 1 µs
        var epoch = EpochParserTime("2026-03-20T00:00:00.000 TDB");
        for (int i = 0; i < 4; i++)
        {
            double delta = ((Time)withGm[instants[i]] - golden[i]).TotalSeconds;
            double gmEffect = ((Time)kernelGm[instants[i]] - (Time)withGm[instants[i]]).TotalSeconds;
            double expected = 7.52e-9 * ((Time)withGm[instants[i]] - epoch).TotalSeconds;
            _output.WriteLine($"{instants[i]}: {delta:E3} s from GMAT; the kernel GM moves it by {gmEffect:E3} s ({expected:E3} s expected)");
            Assert.True(System.Math.Abs(delta) < 1e-3, $"{instants[i]}: {delta:E3} s");
            Assert.InRange(gmEffect, 0.5 * expected, 1.5 * expected);
        }

        Assert.InRange((double)withGm["penumbra_duration_s"] - 18.7118606991, -1e-3, 1e-3);
        Assert.InRange((double)withGm["umbra_duration_s"] - 2063.9981917, -1e-3, 1e-3);
    }

    [Fact]
    public void Eclipse_OutsideTheWindow_GivesItsOutputsAsErrors()
    {
        // Arrange: a 20-minute window before the first eclipse
        var caseInput = Parse("eclipse", Eclipse("").Replace("2026-03-20T12:00:00.000 TDB", "2026-03-20T00:10:00.000 TDB"));

        // Act
        var outputs = new EclipseSolver(_suite).Solve(caseInput);

        // Assert: each output is the exception the comparer turns into an ERROR when the golden holds it
        Assert.All(outputs.Values, value => Assert.IsType<InvalidOperationException>(value));
    }

    private static Time EpochParserTime(string instant) => Utilities.EpochParser.ParseInstant(instant);

    private static string Geosynchronous(string extra) => $"""
                                                             epoch: "2021-01-01T00:00:00.000 TDB"
                                                             central_body: "Earth"
                                                             longitude_deg: 45.0
                                                             latitude_deg: 30.0
                                                           {extra}
                                                           """;

    [Fact]
    public void Geosynchronous_TakesTheDeclaredPeriod_OrWithoutItThePeriodOfTheEarthAtTheEpoch()
    {
        // Act
        var declared = new GeosynchronousSolver(null).Solve(Parse("geosynchronous", Geosynchronous("""
              gm_km3_s2: 398600.4355070226
              sidereal_period_s: 86164.0998101
            """)));
        var atTheEpoch = new GeosynchronousSolver(null).Solve(Parse("geosynchronous", Geosynchronous("")));

        // Assert: a = (GM T^2 / 4 pi^2)^(1/3) = 42164.17243944305 km (spec.md section 12.5); the rotation of ITRF93 at the
        // epoch gives a period 1.0e-6 s shorter, and a radius 4.8e-4 km smaller
        Assert.InRange((double)declared["semi_major_axis_km"] - 42164.17243944305, -1e-9, 1e-9);
        Assert.InRange((double)atTheEpoch["semi_major_axis_km"] - (double)declared["semi_major_axis_km"], -5e-4, -4e-4);
    }

    [Fact]
    public void Geosynchronous_WithAGmOtherThanTheKernels_IsAnError()
    {
        var caseInput = Parse("geosynchronous", Geosynchronous("""
              gm_km3_s2: 398600.4415
              sidereal_period_s: 86164.0998101
            """));

        var exception = Assert.Throws<InvalidOperationException>(() => new GeosynchronousSolver(null).Solve(caseInput));
        Assert.Contains("398600441500000", exception.Message);
    }

    [Fact]
    public void Tracking_TheStationIsOnTheDeclaredEllipsoid_OnlyWhenItIsTheEllipsoidOfTheKernels()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);

        // The pck00011 Earth, as tracking_data 001 declares it: its polar radius from the flattening, 1e-12 m from the kernel's
        TrackingDataSolver.CheckEllipsoid(new EllipsoidInputs { EquatorialRadiusKm = 6378.1366, Flattening = 0.0033528131084554157 }, earth);
        TrackingDataSolver.CheckEllipsoid(null, earth);

        // The pck00008 Earth of the golden before conformance-tests#25, 3.4 m and 1.9 m away
        var exception = Assert.Throws<InvalidOperationException>(() => TrackingDataSolver.CheckEllipsoid(
            new EllipsoidInputs { EquatorialRadiusKm = 6378.14, Flattening = (6378.14 - 6356.75) / 6378.14 }, earth));
        Assert.Contains("6378140", exception.Message);
    }

    [Fact]
    public void Triad_WithTheLightTimeCorrectedDirections_ReproducesTheGoldenOfSpice()
    {
        // Arrange: pointing_triad 001, whose golden is SPICE_TRIAD.py's (spkpos, abcorr LT, observer the Earth)
        var caseInput = Parse("pointing_triad", """
                                                  epoch: "2026-01-15T12:00:00.000 TDB"
                                                  orbit:
                                                    type: "keplerian"
                                                    a_km: 7000.0
                                                    e: 0.001
                                                    i_deg: 28.5
                                                    raan_deg: 45.0
                                                    argp_deg: 0.0
                                                    ma_deg: 0.0
                                                  primary_target: "Moon"
                                                  secondary_target: "Sun"
                                                  primary_body_vector: [0.0, 0.0, 1.0]
                                                  secondary_body_vector: [0.0, 1.0, 0.0]
                                                  field_of_view:
                                                    half_angle_deg: 15.0
                                                    axis_body: [0.0, 0.0, 1.0]
                                                """);
        double[] golden = [0.31884639218997207, 0.6774833010289654, 0.5216244793796411, -0.40897586423437404];

        // Act
        var outputs = new TriadSolver().Solve(caseInput);

        // Assert: 3.8e-16 rad measured (Linux, 2026-10-27); the geometric directions were 7.8e-5 rad away
        double angle = OutputComparer.RotationAngle((double[])outputs["attitude_quaternion"], golden);
        _output.WriteLine($"pointing_triad 001: {angle:E3} rad from the golden");
        Assert.True(angle < 1e-12, $"{angle:E3} rad");
        Assert.True((bool)outputs["target_in_fov"]);
    }
}
