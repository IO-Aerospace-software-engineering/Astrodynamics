using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
    Seconds,

    /// <summary>An instant (spec.md section 3): d = computed − golden, in seconds; no relative delta.</summary>
    Instant,

    /// <summary>
    /// An attitude, a unit quaternion [w, x, y, z]: d = θ = 4·atan2(‖q − s·g‖, ‖q + s·g‖), s = sign(q·g), the angle of
    /// the rotation between the two attitudes, in rad; no relative delta.
    /// </summary>
    RotationAngle,

    /// <summary>
    /// A direction, a unit vector: d = θ = atan2(‖c × g‖, c·g), the angle between the two directions, in rad; no relative
    /// delta.
    /// </summary>
    DirectionAngle,

    /// <summary>
    /// No delta: the output equals the golden (a boolean, a string, the samples of a grid, the number of elements of an
    /// indexed array) or fails; no key.
    /// </summary>
    Exact
}

/// <summary>
/// An output of a category: its kind, its tolerance key and its unit; for a time output, its time scale, which chooses
/// the key (<see cref="MetricTable.ToleranceKey"/>); for a geodetic output, the need of an ellipsoid; for an output of
/// the earlier categories, the keys of the suite before conformance-tests#25 (<see cref="FormerKeys"/>).
/// </summary>
/// <param name="FormerKeys">
/// The keys by which the suite named the tolerance of the output before conformance-tests#25 gave the earlier
/// categories their keys (spec.md section 12): their cases override them (<c>position_km</c> for a propagated position,
/// for instance), so that the runner passes the suite before and after that change. A case takes them after the
/// overrides of the output and of its key, and their defaults after that of the key.
/// </param>
public sealed record Metric(MetricKind Kind, string Key, string Unit, string TimeScale = null, bool Geodetic = false,
    string[] FormerKeys = null);

/// <summary>
/// The outputs of the conversion categories (spec.md section 11): <c>orbital_elements</c>, <c>time_conversion</c> and
/// <c>frame_transformation</c>, and of the earlier categories (spec.md section 12): <c>propagator</c>, <c>eclipse</c>,
/// <c>tracking_data</c>, <c>pointing_triad</c> and <c>geosynchronous</c>, and of the maneuver categories (spec.md section
/// 13): <c>maneuver</c> and <c>lambert</c>, and of the geometry and event categories (spec.md section 14):
/// <c>ground_station</c>, <c>orbital_events</c> and <c>attitude</c>, with the second branch of <c>eclipse</c>, each with
/// its kind and tolerance key; and the final state of the <c>stm</c> category, the extension of the suite tested by the
/// fixture of the runner, compared as the propagator's. The arrays of the geometry and event categories are indexed
/// (<see cref="IsIndexed"/>): the array is the number of its elements, an exact output, and each element gives its
/// outputs, named <c>&lt;array&gt;[k].&lt;field&gt;</c> or <c>&lt;array&gt;[k]</c>, k from 0, held as
/// <c>&lt;array&gt;[].&lt;field&gt;</c> or <c>&lt;array&gt;[]</c>.
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

    private static readonly Metric PropagatedPosition = new(MetricKind.Scalar, "propagator_position_km", "km", FormerKeys: ["position_km"]);
    private static readonly Metric PropagatedVelocity = new(MetricKind.Scalar, "propagator_velocity_km_s", "km/s", FormerKeys: ["velocity_km_s"]);
    private static readonly Metric EclipseInstant = new(MetricKind.Instant, "eclipse_time_s", "s");
    private static readonly Metric Range = new(MetricKind.Scalar, "tracking_range_km", "km", FormerKeys: ["range (km)", "position_km"]);
    private static readonly Metric RangeRate = new(MetricKind.Scalar, "tracking_range_rate_km_s", "km/s",
        FormerKeys: ["doppler (km/s)", "velocity_km_s"]);
    private static readonly Metric Exact = new(MetricKind.Exact, null, "-");
    private static readonly Metric LambertVelocity = new(MetricKind.Vector3, "lambert_velocity_km_s", "km/s");
    private static readonly Metric StationAngle = new(MetricKind.Angle, "station_angle_deg", "deg");
    private static readonly Metric EventEpoch = new(MetricKind.Seconds, "event_epoch_s", "s");
    private static readonly Metric EventRadius = new(MetricKind.Scalar, "event_radius_km", "km");
    private static readonly Metric AttitudeAngle = new(MetricKind.RotationAngle, "attitude_angle_rad", "rad");

    /// <summary>The lists of an <c>orbital_events</c> golden (spec.md section 14.2), named <c>events.&lt;list&gt;</c>.</summary>
    public static readonly IReadOnlyList<string> EventLists =
        ["periapsis", "apoapsis", "ascending_node", "descending_node", "altitude_up", "altitude_down"];

    // The apsides, whose events carry their radius
    private static readonly HashSet<string> Apsides = ["periapsis", "apoapsis"];

    /// <summary>The name of a list of an <c>orbital_events</c> golden: <c>events.&lt;list&gt;</c>.</summary>
    public static string EventList(string list) => $"events.{list}";

    /// <summary>
    /// The arrays of each category whose elements are compared one by one, k from 0 (spec.md sections 12.2 and 14): the
    /// array itself is its number of elements, an exact output; an element that is an object gives one output per
    /// field, <c>&lt;array&gt;[k].&lt;field&gt;</c>, any other element one output, <c>&lt;array&gt;[k]</c>.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> IndexedArrays = new()
    {
        ["ground_station"] = ["passes", "samples"],
        ["eclipse"] = ["events"],
        ["orbital_events"] = EventLists.Select(EventList).ToHashSet(),
        ["attitude"] = ["attitude_quaternions", "pointing_directions"]
    };

    /// <summary>Whether <paramref name="array"/> is an indexed array of the category (<see cref="IndexedArrays"/>).</summary>
    public static bool IsIndexed(string category, string array)
    {
        return category != null && array != null && IndexedArrays.TryGetValue(category, out var arrays) && arrays.Contains(array);
    }

    /// <summary>Whether an object of a golden holds indexed arrays of the category, <c>&lt;object&gt;.&lt;array&gt;</c>.</summary>
    public static bool HoldsIndexed(string category, string name)
    {
        return category != null && name != null && IndexedArrays.TryGetValue(category, out var arrays) &&
               arrays.Any(array => array.StartsWith(name + ".", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// The indexed array of an output of an element, <c>&lt;array&gt;[k].&lt;field&gt;</c> or <c>&lt;array&gt;[k]</c>, when
    /// it is one of the category's; null otherwise.
    /// </summary>
    public static string IndexedArrayOf(string category, string output)
    {
        if (output == null)
        {
            return null;
        }

        int bracket = output.LastIndexOf('[');
        return bracket > 0 && IsIndexed(category, output[..bracket]) ? output[..bracket] : null;
    }

    // The outputs of the lists of an orbital_events golden
    private static IEnumerable<KeyValuePair<string, Metric>> EventOutputs()
    {
        foreach (var list in EventLists)
        {
            yield return new KeyValuePair<string, Metric>(EventList(list), Exact);
            yield return new KeyValuePair<string, Metric>($"{EventList(list)}[].elapsed_s", EventEpoch);
            if (Apsides.Contains(list))
            {
                yield return new KeyValuePair<string, Metric>($"{EventList(list)}[].radius_km", EventRadius);
            }
        }
    }

    /// <summary>The array of the burns of a <c>maneuver</c> golden (spec.md section 13.1).</summary>
    public const string Burns = "burns";

    /// <summary>
    /// The output of a field of a burn, whatever its label: <c>burns[].&lt;field&gt;</c>, the name under which the
    /// table holds <c>burns[&lt;label&gt;].&lt;field&gt;</c>.
    /// </summary>
    private static string BurnOutput(string field) => $"{Burns}[].{field}";

    // An output of a labelled element, <array>[<label>].<field> (OutputComparer.Outputs)
    private static readonly Regex Labelled = new(@"^(?<array>[^\[\]]+)\[[^\]]*\]\.(?<field>[^\[\]]+)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The name under which the table holds an output: <c>&lt;array&gt;[].&lt;field&gt;</c> for an output of a labelled
    /// element, <c>&lt;array&gt;[&lt;label&gt;].&lt;field&gt;</c>, the output itself otherwise.
    /// </summary>
    public static string Generic(string output)
    {
        var match = Labelled.Match(output);
        if (match.Success)
        {
            return $"{match.Groups["array"].Value}[].{match.Groups["field"].Value}";
        }

        match = Element.Match(output);
        return match.Success ? $"{match.Groups["array"].Value}[]" : output;
    }

    // An element of an indexed array that is itself the output, <array>[<k>] (attitude_quaternions[k])
    private static readonly Regex Element = new(@"^(?<array>[^\[\]]+)\[\d+\]$", RegexOptions.CultureInvariant);

    /// <summary>The final state of a propagation (spec.md section 12.1).</summary>
    private static Dictionary<string, Metric> FinalState() => new()
    {
        ["final_x_km"] = PropagatedPosition,
        ["final_y_km"] = PropagatedPosition,
        ["final_z_km"] = PropagatedPosition,
        ["final_vx_km_s"] = PropagatedVelocity,
        ["final_vy_km_s"] = PropagatedVelocity,
        ["final_vz_km_s"] = PropagatedVelocity
    };

    /// <summary>The statistics of a tracking block (spec.md section 12.3), named <c>&lt;block&gt;.&lt;statistic&gt;</c>.</summary>
    private static IEnumerable<KeyValuePair<string, Metric>> Statistics(string block, Metric metric) =>
        new[] { "mean", "median", "min", "max" }.Select(statistic => new KeyValuePair<string, Metric>($"{block}.{statistic}", metric));

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
        },
        ["propagator"] = FinalState(),
        ["stm"] = FinalState(),
        ["eclipse"] = new Dictionary<string, Metric>
        {
            ["penumbra_entry"] = EclipseInstant,
            ["umbra_entry"] = EclipseInstant,
            ["umbra_exit"] = EclipseInstant,
            ["penumbra_exit"] = EclipseInstant,
            ["penumbra_duration_s"] = new(MetricKind.Scalar, "eclipse_duration_s", "s", FormerKeys: ["penumbra_duration_s"]),
            ["umbra_duration_s"] = new(MetricKind.Scalar, "eclipse_duration_s", "s", FormerKeys: ["umbra_duration_s"]),
            // The second branch: every event of the window (spec.md section 12.2)
            ["events"] = Exact,
            ["events[].body"] = Exact,
            ["events[].type"] = Exact,
            ["events[].start"] = EclipseInstant,
            ["events[].stop"] = EclipseInstant,
            ["events[].duration_s"] = new(MetricKind.Scalar, "eclipse_duration_s", "s")
        },
        ["tracking_data"] = new Dictionary<string, Metric>(
            Statistics(TrackingRangeBlock, Range).Concat(Statistics(TrackingRangeRateBlock, RangeRate))
                .Append(new KeyValuePair<string, Metric>("arcs", Exact))),
        ["pointing_triad"] = new Dictionary<string, Metric>
        {
            ["attitude_quaternion"] = new(MetricKind.RotationAngle, "attitude_angle_rad", "rad", FormerKeys: ["attitude_quaternion"]),
            ["target_in_fov"] = Exact
        },
        // The burns of a chain, each output named burns[<label>].<field> (spec.md section 13.1), and the final state
        ["maneuver"] = new Dictionary<string, Metric>(FinalState())
        {
            [BurnOutput("elapsed_s")] = new(MetricKind.Seconds, "maneuver_epoch_s", "s"),
            [BurnOutput("position_km")] = new(MetricKind.Vector3, "maneuver_position_km", "km"),
            [BurnOutput("delta_v_km_s")] = new(MetricKind.Vector3, "maneuver_delta_v_km_s", "km/s"),
            [BurnOutput("mass_after_kg")] = new(MetricKind.Scalar, "maneuver_mass_kg", "kg")
        },
        ["lambert"] = new Dictionary<string, Metric>
        {
            ["v1_km_s"] = LambertVelocity,
            ["v2_km_s"] = LambertVelocity
        },
        // The passes and the samples of a station (spec.md section 14.1)
        ["ground_station"] = new Dictionary<string, Metric>
        {
            ["passes"] = Exact,
            ["passes[].rise"] = new(MetricKind.Instant, "station_event_time_s", "s"),
            ["passes[].set"] = new(MetricKind.Instant, "station_event_time_s", "s"),
            ["passes[].duration_s"] = new(MetricKind.Scalar, "station_duration_s", "s"),
            ["passes[].culmination"] = new(MetricKind.Instant, "station_culmination_time_s", "s"),
            ["passes[].max_elevation_deg"] = StationAngle,
            ["samples"] = Exact,
            ["samples[].epoch"] = Exact,
            ["samples[].azimuth_deg"] = new(MetricKind.WrappedAngle, "station_angle_deg", "deg"),
            ["samples[].elevation_deg"] = StationAngle,
            ["samples[].range_km"] = new(MetricKind.Scalar, "tracking_range_km", "km"),
            ["samples[].range_rate_km_s"] = new(MetricKind.Scalar, "tracking_range_rate_km_s", "km/s")
        },
        ["orbital_events"] = new Dictionary<string, Metric>(EventOutputs()),
        // The attitudes or the pointing directions at the samples (spec.md section 14.3)
        ["attitude"] = new Dictionary<string, Metric>
        {
            ["attitude_quaternions"] = Exact,
            ["attitude_quaternions[]"] = AttitudeAngle,
            ["pointing_directions"] = Exact,
            ["pointing_directions[]"] = new(MetricKind.DirectionAngle, "attitude_angle_rad", "rad")
        },
        // The lot-1 keys of their quantities (spec.md section 12.5)
        ["geosynchronous"] = new Dictionary<string, Metric>
        {
            ["semi_major_axis_km"] = Length,
            ["eccentricity"] = new(MetricKind.Scalar, "oe_eccentricity", "-"),
            ["inclination_deg"] = OrbitAngleUnwrapped,
            ["raan_deg"] = OrbitAngle,
            ["argp_deg"] = OrbitAngle,
            ["mean_anomaly_deg"] = OrbitAngle,
            ["position_x_km"] = new(MetricKind.Scalar, "oe_position_km", "km"),
            ["position_y_km"] = new(MetricKind.Scalar, "oe_position_km", "km"),
            ["position_z_km"] = new(MetricKind.Scalar, "oe_position_km", "km"),
            ["velocity_x_km_s"] = new(MetricKind.Scalar, "oe_velocity_km_s", "km/s"),
            ["velocity_y_km_s"] = new(MetricKind.Scalar, "oe_velocity_km_s", "km/s"),
            ["velocity_z_km_s"] = new(MetricKind.Scalar, "oe_velocity_km_s", "km/s")
        }
    };

    /// <summary>The block of the range statistics of a <c>tracking_data</c> golden (spec.md section 12.3).</summary>
    public const string TrackingRangeBlock = "range (km)";

    /// <summary>The block of the range-rate statistics of a <c>tracking_data</c> golden (spec.md section 12.3).</summary>
    public const string TrackingRangeRateBlock = "doppler (km/s)";

    /// <summary>Whether the category is compared by <see cref="OutputComparer"/>.</summary>
    public static bool Covers(string category)
    {
        return category != null && Categories.ContainsKey(category);
    }

    /// <summary>The metric of an output of a category, if the category has that output.</summary>
    public static bool TryGet(string category, string output, out Metric metric)
    {
        metric = null;
        return Covers(category) && output != null && Categories[category].TryGetValue(Generic(output), out metric);
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

    /// <summary>
    /// The row of <c>accuracy_by_metric</c> of an output: <c>&lt;category&gt;/&lt;output&gt;</c>, the outputs of the
    /// labelled elements of an array in one row, <c>maneuver/burns[].elapsed_s</c>.
    /// </summary>
    public static string SummaryKey(string category, string output)
    {
        return $"{category}/{Generic(output)}";
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
