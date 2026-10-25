// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using Xunit;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// <see cref="OutputComparer"/> and <see cref="MetricTable"/>: the kinds of spec.md section 6.1, the wrap of the
/// angles by the IEEE 754 remainder, the resolution of the tolerances without fallback, the TDB-crossing rule of the
/// time outputs and the errors of spec.md section 7.
/// </summary>
public class OutputComparerTests
{
    private const string TdbEpoch = "2021-03-20T12:00:00.000 TDB";

    // One active threshold per key, as the lot-1 keys of tolerances.yaml
    private static readonly Dictionary<string, TolerancePair> Defaults = new()
    {
        ["oe_angle_deg"] = Absolute(2.2e-12),
        ["oe_length_km"] = Relative(2.7e-15),
        ["frame_position_km"] = Relative(1.9e-15),
        ["frame_velocity_km_s"] = Absolute(4.4e-15),
        ["geodetic_latitude_deg"] = Absolute(5.2e-8),
        [MetricTable.TimeKey] = Absolute(9.6e-7),
        [MetricTable.TdbTimeKey] = Absolute(7.6e-5)
    };

    private static TolerancePair Absolute(double tolerance) => new() { AbsTol = tolerance, RelTol = 0.0 };
    private static TolerancePair Relative(double tolerance) => new() { AbsTol = 0.0, RelTol = tolerance };

    private static CaseInput Case(string category, string epoch = TdbEpoch, Dictionary<string, TolerancePair> overrides = null,
        bool ellipsoid = false)
    {
        var inputs = new Dictionary<string, object> { ["epoch"] = epoch };
        if (ellipsoid)
        {
            inputs["ellipsoid"] = new Dictionary<object, object> { ["equatorial_radius_km"] = "6378.1366", ["flattening"] = "0.0033528131084554157" };
        }

        return new CaseInput { Id = "test", Category = category, Inputs = inputs, TolerancesOverride = overrides };
    }

    private static JsonElement Golden(string json) => JsonDocument.Parse(json).RootElement;

    private static OutputMeasure CompareOne(string category, string output, object computed, string golden, CaseInput caseInput = null)
    {
        return OutputComparer.Compare(caseInput ?? Case(category), new Dictionary<string, object> { [output] = computed },
            Golden($"{{\"{output}\": {golden}}}"), Defaults).Single();
    }

    [Theory]
    [InlineData(359.99999999999997, "0.0")] // the last double below 360 against 0
    [InlineData(180.0, "-180.0")] // the two ends of (-180, 180]
    [InlineData(359.1400638722338 - 360.0, "359.1400638722338")] // MLONG -0.8599..., not wrapped, against 359.14...
    public void AWrappedAngle_IsComparedModulo360(double computed, string golden)
    {
        var measure = CompareOne("orbital_elements", "raan_deg", computed, golden);

        Assert.Equal(MetricKind.WrappedAngle, measure.Kind);
        Assert.InRange(measure.AbsoluteDelta, 0.0, 1e-13);
        Assert.True(double.IsNaN(measure.RelativeDelta));
        Assert.True(measure.Passed);
    }

    [Fact]
    public void AnAscendingNodeOf2PiExactly_MatchesAZeroGolden()
    {
        // AscendingNode() adds 2π to a negative atan2: one below the ulp of 2π gives 2π exactly
        var measure = CompareOne("orbital_elements", "raan_deg", UnitConversion.RadToDeg(IO.Astrodynamics.Constants._2PI), "0.0");

        Assert.InRange(measure.AbsoluteDelta, 0.0, 1e-13);
        Assert.True(measure.Passed);
    }

    [Fact]
    public void AnUnwrappedAngle_IsNotComparedModulo360()
    {
        // A hyperbolic mean anomaly is signed and unbounded: -17.28 is not 342.72
        var measure = CompareOne("orbital_elements", "hyperbolic_mean_anomaly_deg", -17.28, "342.72");

        Assert.Equal(MetricKind.Angle, measure.Kind);
        Assert.Equal(360.0, measure.AbsoluteDelta, 10);
        Assert.False(measure.Passed);
    }

    [Fact]
    public void AScalar_HasARelativeDelta_ButNotAgainstAZeroGolden()
    {
        var measure = CompareOne("orbital_elements", "semi_major_axis_km", 7000.0 * (1.0 + 2e-15), "7000.0");
        var zero = CompareOne("orbital_elements", "semi_major_axis_km", 1e-20, "0.0");
        var exactZero = CompareOne("orbital_elements", "semi_major_axis_km", 0.0, "0.0");

        Assert.InRange(measure.RelativeDelta, 1.8e-15, 2.2e-15);
        Assert.True(measure.Passed);
        Assert.Equal(measure.RelativeDelta / 2.7e-15, measure.Ratio, 12);
        Assert.True(double.IsNaN(zero.RelativeDelta));
        Assert.False(zero.Passed);
        Assert.True(exactZero.Passed);
    }

    [Fact]
    public void AVector_IsComparedByTheNormOfItsDifference()
    {
        var measure = CompareOne("frame_transformation", "position_km", new[] { 3.0, 4.0 + 1e-12, 0.0 }, "[3.0, 4.0, 0.0]");

        Assert.Equal(MetricKind.Vector3, measure.Kind);
        Assert.InRange(measure.AbsoluteDelta, 0.99e-12, 1.01e-12);
        Assert.Equal(measure.AbsoluteDelta / 5.0, measure.RelativeDelta);
        Assert.False(measure.Passed);
    }

    [Fact]
    public void AZeroGoldenVector_HasNoRelativeDelta_AndPassesByAnAbsoluteKeyOnly()
    {
        var velocity = CompareOne("frame_transformation", "velocity_km_s", new[] { 1e-16, 0.0, 0.0 }, "[0.0, 0.0, 0.0]");
        var position = CompareOne("frame_transformation", "position_km", new[] { 1e-16, 0.0, 0.0 }, "[0.0, 0.0, 0.0]");

        Assert.True(double.IsNaN(velocity.RelativeDelta));
        Assert.True(velocity.Passed);
        Assert.True(double.IsNaN(position.RelativeDelta));
        Assert.False(position.Passed);
        Assert.True(double.IsNaN(position.Ratio));
    }

    [Fact]
    public void ATime_IsComparedByItsAbsoluteDeltaOnly()
    {
        // Even an override with a relative threshold does not apply: a time has no relative delta
        var caseInput = Case("time_conversion", overrides: new Dictionary<string, TolerancePair>
        {
            ["tdb_s"] = new() { AbsTol = 1e-7, RelTol = 1e-3, Justification = "a relative threshold that must not apply" }
        });
        var measure = CompareOne("time_conversion", "tdb_s", 669513600.0 + 1e-6, "669513600.0", caseInput);

        Assert.Equal(MetricKind.Seconds, measure.Kind);
        Assert.True(double.IsNaN(measure.RelativeDelta));
        Assert.False(measure.Passed);
        Assert.Equal(("tdb_s", 1e-7), (measure.ToleranceName, measure.Threshold));
        Assert.Same(caseInput.TolerancesOverride["tdb_s"], measure.Tolerance);
    }

    [Theory]
    [InlineData("TDB", "tdb_s", MetricTable.TimeKey)]
    [InlineData("TDB", "tt_s", MetricTable.TdbTimeKey)]
    [InlineData("TDB", "tai_s", MetricTable.TdbTimeKey)]
    [InlineData("TDB", "utc_s", MetricTable.TdbTimeKey)]
    [InlineData("TDB", "gps_s", MetricTable.TdbTimeKey)]
    [InlineData("UTC", "tdb_s", MetricTable.TdbTimeKey)]
    [InlineData("UTC", "tai_s", MetricTable.TimeKey)]
    [InlineData("TAI", "gps_s", MetricTable.TimeKey)]
    [InlineData("TT", "tdb_s", MetricTable.TdbTimeKey)]
    [InlineData("TT", "utc_s", MetricTable.TimeKey)]
    public void ATimeOutput_CrossingTdb_TakesTheTdbKey(string inputScale, string output, string key)
    {
        // Through the table
        Assert.True(MetricTable.TryGet("time_conversion", output, out var metric));
        Assert.Equal((MetricKind.Seconds, "s"), (metric.Kind, metric.Unit));
        Assert.Equal(key, MetricTable.ToleranceKey(metric, inputScale));

        // Through the comparer, from the tag of the epoch
        var measure = CompareOne("time_conversion", output, 0.0, "0.0", Case("time_conversion", $"2021-03-20T12:00:00 {inputScale}"));
        Assert.Equal(key, measure.ToleranceName);
    }

    // The rows of spec.md sections 11.1 and 11.3 (design of #458, sections 3.1 and 3.3): kind S, A, U or V, and key
    [Theory]
    [InlineData("orbital_elements", "semi_major_axis_km", MetricKind.Scalar, "oe_length_km", "km")]
    [InlineData("orbital_elements", "eccentricity", MetricKind.Scalar, "oe_eccentricity", "-")]
    [InlineData("orbital_elements", "inclination_deg", MetricKind.Angle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "raan_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "argp_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "true_anomaly_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "eccentric_anomaly_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "mean_anomaly_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "hyperbolic_anomaly_deg", MetricKind.Angle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "hyperbolic_mean_anomaly_deg", MetricKind.Angle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "argument_of_latitude_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "longitude_of_periapsis_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "true_longitude_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "mean_longitude_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "periapsis_radius_km", MetricKind.Scalar, "oe_length_km", "km")]
    [InlineData("orbital_elements", "apoapsis_radius_km", MetricKind.Scalar, "oe_length_km", "km")]
    [InlineData("orbital_elements", "semi_latus_rectum_km", MetricKind.Scalar, "oe_length_km", "km")]
    [InlineData("orbital_elements", "period_s", MetricKind.Scalar, "oe_period_s", "s")]
    [InlineData("orbital_elements", "specific_energy_km2_s2", MetricKind.Scalar, "oe_energy_km2_s2", "km2/s2")]
    [InlineData("orbital_elements", "angular_momentum_km2_s", MetricKind.Vector3, "oe_angular_momentum_km2_s", "km2/s")]
    [InlineData("orbital_elements", "periapsis_speed_km_s", MetricKind.Scalar, "oe_speed_km_s", "km/s")]
    [InlineData("orbital_elements", "apoapsis_speed_km_s", MetricKind.Scalar, "oe_speed_km_s", "km/s")]
    [InlineData("orbital_elements", "mean_motion_rad_s", MetricKind.Scalar, "oe_mean_motion_rad_s", "rad/s")]
    [InlineData("orbital_elements", "mod_equinoctial_f", MetricKind.Scalar, "oe_equinoctial", "-")]
    [InlineData("orbital_elements", "mod_equinoctial_g", MetricKind.Scalar, "oe_equinoctial", "-")]
    [InlineData("orbital_elements", "mod_equinoctial_h", MetricKind.Scalar, "oe_equinoctial", "-")]
    [InlineData("orbital_elements", "mod_equinoctial_k", MetricKind.Scalar, "oe_equinoctial", "-")]
    [InlineData("orbital_elements", "right_ascension_deg", MetricKind.WrappedAngle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "declination_deg", MetricKind.Angle, "oe_angle_deg", "deg")]
    [InlineData("orbital_elements", "position_km", MetricKind.Vector3, "oe_position_km", "km")]
    [InlineData("orbital_elements", "velocity_km_s", MetricKind.Vector3, "oe_velocity_km_s", "km/s")]
    [InlineData("frame_transformation", "position_km", MetricKind.Vector3, "frame_position_km", "km")]
    [InlineData("frame_transformation", "velocity_km_s", MetricKind.Vector3, "frame_velocity_km_s", "km/s")]
    [InlineData("frame_transformation", "latitude_deg", MetricKind.Angle, "geodetic_latitude_deg", "deg")]
    [InlineData("frame_transformation", "longitude_deg", MetricKind.WrappedAngle, "geodetic_longitude_deg", "deg")]
    [InlineData("frame_transformation", "altitude_km", MetricKind.Scalar, "geodetic_altitude_km", "km")]
    public void EachOutput_HasTheKindKeyAndUnitOfTheSpec(string category, string output, MetricKind kind, string key, string unit)
    {
        Assert.True(MetricTable.TryGet(category, output, out var metric));
        Assert.Equal((kind, key, unit), (metric.Kind, metric.Key, metric.Unit));
        Assert.Equal(key, MetricTable.ToleranceKey(metric, "TDB"));
        Assert.Equal(output is "latitude_deg" or "longitude_deg" or "altitude_km", metric.Geodetic);
    }

    [Fact]
    public void TheTolerance_IsTheOverrideOfTheOutput_ThenThatOfTheKey_ThenTheDefault_AndNeverAFallback()
    {
        var output = new TolerancePair { AbsTol = 1.0 };
        var key = new TolerancePair { AbsTol = 2.0 };
        var both = new Dictionary<string, TolerancePair> { ["raan_deg"] = output, ["oe_angle_deg"] = key };
        var keyOnly = new Dictionary<string, TolerancePair> { ["oe_angle_deg"] = key };

        Assert.Equal((output, "raan_deg"), OutputComparer.ResolveTolerance("raan_deg", "oe_angle_deg", both, Defaults));
        Assert.Equal((key, "oe_angle_deg"), OutputComparer.ResolveTolerance("raan_deg", "oe_angle_deg", keyOnly, Defaults));
        Assert.Equal((Defaults["oe_angle_deg"], "oe_angle_deg"), OutputComparer.ResolveTolerance("raan_deg", "oe_angle_deg", null, Defaults));
        var missing = Assert.Throws<InvalidOperationException>(() => OutputComparer.ResolveTolerance("period_s", "oe_period_s", keyOnly, Defaults));
        Assert.Contains("oe_period_s", missing.Message);
        Assert.Throws<InvalidOperationException>(() => OutputComparer.ResolveTolerance("period_s", "oe_period_s", null, null));
    }

    [Fact]
    public void TheActiveThreshold_IsThatOfTheKey_OrForTwoThresholdsTheOneOfTheSmallerRatio()
    {
        var both = new TolerancePair { AbsTol = 1e-3, RelTol = 1e-6 };

        // abs 1e-4 (ratio 0.1), rel 2e-7 (ratio 0.2): absolute; abs 5e-4 (0.5), rel 1e-7 (0.1): relative
        var absolute = OutputComparer.Measure("x", MetricKind.Scalar, 1e-4, 2e-7, "k", both);
        var relative = OutputComparer.Measure("x", MetricKind.Scalar, 5e-4, 1e-7, "k", both);
        var noRelative = OutputComparer.Measure("x", MetricKind.Seconds, 5e-4, double.NaN, "k", both);
        var exact = OutputComparer.Measure("x", MetricKind.Scalar, 0.0, 0.0, "k", new TolerancePair());

        Assert.Equal(1e-3, absolute.Threshold);
        Assert.Equal(0.1, absolute.Ratio, 12);
        Assert.Equal(1e-6, relative.Threshold);
        Assert.Equal(0.1, relative.Ratio, 12);
        Assert.Equal(1e-3, noRelative.Threshold);
        Assert.Equal(0.5, noRelative.Ratio, 12);
        Assert.True(exact.Passed);
        Assert.Equal(0.0, exact.Threshold);
    }

    [Fact]
    public void TheMeasureLine_HasTheFormatTheToleranceToolParses()
    {
        var measure = new OutputMeasure("tai_s", MetricKind.Seconds, 2.384185791015625E-07, double.NaN, "time_tdb_s",
            Absolute(7.6e-5), 7.6e-5, 0.0031370865671258223, true);

        Assert.Equal(
            "MEASURE time_conversion_tdb_001 tai_s abs=2.384185791015625E-07 rel=NaN tol=7.6E-05 ratio=0.0031370865671258223 key=time_tdb_s",
            measure.ToMeasureLine("time_conversion_tdb_001"));
    }

    [Fact]
    public void AGoldenOutputThatIsNotAnOutputOfTheCategory_IsAnError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CompareOne("time_conversion", "position_km", new[] { 0.0, 0.0, 0.0 }, "[0.0, 0.0, 0.0]"));
        Assert.Contains("position_km", exception.Message);
    }

    [Fact]
    public void AGeodeticGoldenOutput_NeedsAnEllipsoid()
    {
        Assert.Throws<InvalidOperationException>(() => CompareOne("frame_transformation", "latitude_deg", 10.0, "10.0"));
        Assert.True(CompareOne("frame_transformation", "latitude_deg", 10.0, "10.0", Case("frame_transformation", ellipsoid: true)).Passed);
    }

    [Fact]
    public void AGoldenOutputTheSolverDoesNotGive_IsAnError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => OutputComparer.Compare(Case("orbital_elements"),
            new Dictionary<string, object> { ["raan_deg"] = 10.0 }, Golden("{\"raan_deg\": 10.0, \"argp_deg\": 20.0}"), Defaults));
        Assert.Contains("argp_deg", exception.Message);
    }

    [Theory]
    [InlineData("raan_deg", "1.0", "\"ten\"")] // a golden string
    [InlineData("position_km", "[1.0, 2.0]", "[1.0, 2.0, 3.0]")] // a computed 2-vector
    [InlineData("position_km", "[1.0, 2.0, 3.0]", "[1.0, 2.0]")] // a golden 2-vector
    [InlineData("position_km", "[1.0, 2.0, 3.0]", "[1.0, \"two\", 3.0]")] // a golden component that is not a number
    [InlineData("raan_deg", "[1.0, 2.0, 3.0]", "1.0")] // a vector where a number is expected
    public void AnOutputOfTheWrongShape_IsAnError(string output, string computed, string golden)
    {
        object value = computed.StartsWith('[') ? JsonSerializer.Deserialize<double[]>(computed) : double.Parse(computed, CultureInfo.InvariantCulture);
        var category = output == "position_km" ? "frame_transformation" : "orbital_elements";

        Assert.Throws<InvalidOperationException>(() => CompareOne(category, output, value, golden));
    }

    [Fact]
    public void TheTable_CoversTheConversionCategoriesOnly_AndNamesTheirSummaryRows()
    {
        Assert.All(Lot1Fixture.Categories, category => Assert.True(MetricTable.Covers(category)));
        Assert.False(MetricTable.Covers("geosynchronous"));
        Assert.False(MetricTable.Covers(null));
        Assert.False(MetricTable.TryGet("geosynchronous", "semi_major_axis_km", out _));

        Assert.Equal("orbital_elements/period_s", MetricTable.SummaryKey("orbital_elements", "period_s"));
        Assert.True(MetricTable.TryGetSummaryUnit("orbital_elements/specific_energy_km2_s2", out var unit));
        Assert.Equal("km2/s2", unit);
        Assert.False(MetricTable.TryGetSummaryUnit("semi_major_axis_km", out _));
        Assert.False(MetricTable.TryGetSummaryUnit("orbital_elements/c3_energy_km2_s2", out _));
    }

    [Fact]
    public void TheOrRule_OnDeltas_PassesANaNRelativeDeltaByTheAbsoluteThresholdOnly()
    {
        var tolerance = new TolerancePair { AbsTol = 1e-3, RelTol = 1e-3 };

        Assert.True(ToleranceComparer.Passes(1e-3, double.NaN, tolerance));
        Assert.False(ToleranceComparer.Passes(2e-3, double.NaN, tolerance));
        Assert.True(ToleranceComparer.Passes(2e-3, 1e-3, tolerance));
    }
}
