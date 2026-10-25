using System.Collections.Generic;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// How an output is compared with its golden (spec.md section 6.1).
/// </summary>
public enum MetricKind
{
    /// <summary>d = computed − golden; relative delta |d| / |golden|.</summary>
    Scalar,

    /// <summary>An angle in degrees: d = remainder(computed − golden, 360), the IEEE 754 remainder; no relative delta.</summary>
    WrappedAngle,

    /// <summary>An angle in degrees, not wrapped: d = computed − golden; no relative delta.</summary>
    Angle,

    /// <summary>Three components: ‖computed − golden‖₂; relative delta ‖d‖ / ‖golden‖₂.</summary>
    Vector3,

    /// <summary>A time in seconds: d = computed − golden; no relative delta.</summary>
    Seconds
}

/// <summary>
/// An output of a conversion category: its kind, its tolerance key and its unit; for a time output, its time scale,
/// which chooses the key (<see cref="MetricTable.ToleranceKey"/>); for a geodetic output, the need of an ellipsoid.
/// </summary>
public sealed record Metric(MetricKind Kind, string Key, string Unit, string TimeScale = null, bool Geodetic = false);

/// <summary>
/// The outputs of the conversion categories (spec.md section 11): <c>orbital_elements</c>, <c>time_conversion</c> and
/// <c>frame_transformation</c>, each with its kind and tolerance key. The legacy categories are not in the table.
/// </summary>
public static class MetricTable
{
    /// <summary>The key of a time output that does not cross TDB↔TT.</summary>
    public const string TimeKey = "time_s";

    /// <summary>The key of a time output that crosses TDB↔TT: exactly one of the input and output scales is TDB.</summary>
    public const string TdbTimeKey = "time_tdb_s";

    private const string Tdb = "TDB";

    private static readonly Metric Length = new(MetricKind.Scalar, "oe_length_km", "km");
    private static readonly Metric OrbitAngle = new(MetricKind.WrappedAngle, "oe_angle_deg", "deg");
    private static readonly Metric OrbitAngleUnwrapped = new(MetricKind.Angle, "oe_angle_deg", "deg");
    private static readonly Metric Speed = new(MetricKind.Scalar, "oe_speed_km_s", "km/s");
    private static readonly Metric Equinoctial = new(MetricKind.Scalar, "oe_equinoctial", "-");

    private static readonly Dictionary<string, Dictionary<string, Metric>> Categories = new()
    {
        ["orbital_elements"] = new Dictionary<string, Metric>
        {
            ["semi_major_axis_km"] = Length,
            ["eccentricity"] = new(MetricKind.Scalar, "oe_eccentricity", "-"),
            ["inclination_deg"] = OrbitAngleUnwrapped,
            ["raan_deg"] = OrbitAngle,
            ["argp_deg"] = OrbitAngle,
            ["true_anomaly_deg"] = OrbitAngle,
            ["eccentric_anomaly_deg"] = OrbitAngle,
            ["mean_anomaly_deg"] = OrbitAngle,
            ["hyperbolic_anomaly_deg"] = OrbitAngleUnwrapped,
            ["hyperbolic_mean_anomaly_deg"] = OrbitAngleUnwrapped,
            ["argument_of_latitude_deg"] = OrbitAngle,
            ["longitude_of_periapsis_deg"] = OrbitAngle,
            ["true_longitude_deg"] = OrbitAngle,
            ["mean_longitude_deg"] = OrbitAngle,
            ["periapsis_radius_km"] = Length,
            ["apoapsis_radius_km"] = Length,
            ["semi_latus_rectum_km"] = Length,
            ["period_s"] = new(MetricKind.Scalar, "oe_period_s", "s"),
            ["specific_energy_km2_s2"] = new(MetricKind.Scalar, "oe_energy_km2_s2", "km2/s2"),
            ["angular_momentum_km2_s"] = new(MetricKind.Vector3, "oe_angular_momentum_km2_s", "km2/s"),
            ["periapsis_speed_km_s"] = Speed,
            ["apoapsis_speed_km_s"] = Speed,
            ["mean_motion_rad_s"] = new(MetricKind.Scalar, "oe_mean_motion_rad_s", "rad/s"),
            ["mod_equinoctial_f"] = Equinoctial,
            ["mod_equinoctial_g"] = Equinoctial,
            ["mod_equinoctial_h"] = Equinoctial,
            ["mod_equinoctial_k"] = Equinoctial,
            ["right_ascension_deg"] = OrbitAngle,
            ["declination_deg"] = OrbitAngleUnwrapped,
            ["position_km"] = new(MetricKind.Vector3, "oe_position_km", "km"),
            ["velocity_km_s"] = new(MetricKind.Vector3, "oe_velocity_km_s", "km/s")
        },
        ["time_conversion"] = new Dictionary<string, Metric>
        {
            ["tai_s"] = TimeOutput("TAI"),
            ["utc_s"] = TimeOutput("UTC"),
            ["tt_s"] = TimeOutput("TT"),
            ["tdb_s"] = TimeOutput(Tdb),
            ["gps_s"] = TimeOutput("GPS")
        },
        ["frame_transformation"] = new Dictionary<string, Metric>
        {
            ["position_km"] = new(MetricKind.Vector3, "frame_position_km", "km"),
            ["velocity_km_s"] = new(MetricKind.Vector3, "frame_velocity_km_s", "km/s"),
            ["latitude_deg"] = new(MetricKind.Angle, "geodetic_latitude_deg", "deg", Geodetic: true),
            ["longitude_deg"] = new(MetricKind.WrappedAngle, "geodetic_longitude_deg", "deg", Geodetic: true),
            ["altitude_km"] = new(MetricKind.Scalar, "geodetic_altitude_km", "km", Geodetic: true)
        }
    };

    /// <summary>Whether the category is compared by <see cref="OutputComparer"/>.</summary>
    public static bool Covers(string category)
    {
        return category != null && Categories.ContainsKey(category);
    }

    /// <summary>The metric of an output of a category, if the category has that output.</summary>
    public static bool TryGet(string category, string output, out Metric metric)
    {
        metric = null;
        return Covers(category) && Categories[category].TryGetValue(output, out metric);
    }

    /// <summary>
    /// The tolerance key of an output (spec.md section 11.2 for the time outputs): <see cref="TdbTimeKey"/> when exactly
    /// one of the input scale and the output scale is TDB, <see cref="TimeKey"/> for the other time outputs, and the
    /// key of the metric otherwise.
    /// </summary>
    /// <param name="metric">The metric of the output.</param>
    /// <param name="inputScale">The scale of the input epoch (UTC, TAI, TT or TDB); used for time outputs only.</param>
    public static string ToleranceKey(Metric metric, string inputScale)
    {
        if (metric.TimeScale == null)
        {
            return metric.Key;
        }

        return (inputScale == Tdb) != (metric.TimeScale == Tdb) ? TdbTimeKey : TimeKey;
    }

    /// <summary>The row of <c>accuracy_by_metric</c> of an output: <c>&lt;category&gt;/&lt;output&gt;</c>.</summary>
    public static string SummaryKey(string category, string output)
    {
        return $"{category}/{output}";
    }

    /// <summary>The unit of a row of <c>accuracy_by_metric</c> named by <see cref="SummaryKey"/>.</summary>
    public static bool TryGetSummaryUnit(string summaryKey, out string unit)
    {
        unit = null;
        int slash = summaryKey.IndexOf('/');
        if (slash < 0 || !TryGet(summaryKey[..slash], summaryKey[(slash + 1)..], out var metric))
        {
            return false;
        }

        unit = metric.Unit;
        return true;
    }

    private static Metric TimeOutput(string scale)
    {
        return new Metric(MetricKind.Seconds, null, "s", scale);
    }
}
