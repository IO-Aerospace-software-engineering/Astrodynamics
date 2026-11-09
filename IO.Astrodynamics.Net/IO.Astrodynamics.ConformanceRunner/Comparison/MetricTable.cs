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
    /// A vector compared component by component (spec.md section 15.1): d = max over the components of
    /// |computed_i − golden_i|; no relative delta.
    /// </summary>
    ScalarPerComponent,

    /// <summary>
    /// A relative delta (spec.md section 15.2): |d| / |golden|, d = computed − golden; a zero golden passes only on an
    /// exact zero (relative delta 0 for d = 0, +∞ otherwise).
    /// </summary>
    Relative,

    /// <summary>
    /// A UTC instant of the TLE categories (spec.md sections 3 and 15), <c>YYYY-MM-DDThh:mm:ss.ffffff UTC</c>: d =
    /// computed − golden on the UTC calendar, 86 400 s a day, so that it is defined for the 1957 and 2056 epochs of the
    /// two-digit year; no relative delta.
    /// </summary>
    CalendarInstant,

    /// <summary>
    /// The two lines of a computed TLE (spec.md sections 6.1 and 15.2): their length, checksums, line numbers, catalog
    /// number and identifiers exact (<c>items[k].tle</c>), then each other field decoded by the column table and compared
    /// by its key (<c>items[k].tle.&lt;FIELD&gt;</c>, <see cref="TleLineComparer"/>).
    /// </summary>
    TleLine,

    /// <summary>
    /// A covariance given by its lower triangle (spec.md sections 6.1 and 16.3): d = max over the elements of
    /// |computed_ij − golden_ij| / √(golden_ii · golden_jj), an error of rotation or of an element scaled by the variances;
    /// no relative delta.
    /// </summary>
    NormalizedCovariance,

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
/// <c>ground_station</c>, <c>orbital_events</c> and <c>attitude</c>, with the second branch of <c>eclipse</c>, and of the
/// TLE categories (spec.md section 15): <c>sgp4</c> and <c>tle_conversion</c>, and of the conjunction categories (spec.md
/// section 16): <c>conjunction</c>, <c>collision_probability</c>, <c>cdm</c> (a decode and a conversion; an export in
/// its own table, <see cref="CdmExportTable"/>) and the final state of <c>stm</c>, compared as the propagator's (its
/// matrix by <see cref="StmComparer"/>), each with its kind and tolerance key. The arrays of the geometry and event categories are indexed
/// (<see cref="IsIndexed"/>): the array is the number of its elements, an exact output, and each element gives its
/// outputs, named <c>&lt;array&gt;[k].&lt;field&gt;</c> or <c>&lt;array&gt;[k]</c>, k from 0, held as
/// <c>&lt;array&gt;[].&lt;field&gt;</c> or <c>&lt;array&gt;[]</c>; a field of an element that is itself an object gives its
/// leaves, <c>states[k].teme.position_km</c>, unless the table holds it as one output (<c>items[k].tle</c>, the lines of
/// a TLE).
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
    private static readonly Metric TleValue = new(MetricKind.Relative, "tle_value_rel", "-");
    private static readonly Metric TleEpoch = new(MetricKind.CalendarInstant, "tle_epoch_s", "s");
    private static readonly Metric TleFieldAngle = new(MetricKind.WrappedAngle, "tle_field_angle_deg", "deg");
    private static readonly Metric TleFieldExponent = new(MetricKind.Relative, "tle_field_exponent_rel", "-");
    private static readonly Metric ConjunctionTca = new(MetricKind.Seconds, "conjunction_tca_s", "s");
    private static readonly Metric Probability = new(MetricKind.Relative, "collision_probability_rel", "-");
    private static readonly Metric CdmValue = new(MetricKind.Relative, "cdm_value_rel", "-");
    private static readonly Metric CdmEpoch = new(MetricKind.Instant, "cdm_epoch_s", "s");
    private static readonly Metric CdmCovariance = new(MetricKind.NormalizedCovariance, "cdm_covariance_corr", "-");

    /// <summary>The output of the two lines of a computed TLE (spec.md section 15.2), an element of <c>items</c>.</summary>
    public const string TleLines = "items[].tle";

    /// <summary>
    /// The decoded fields of <c>tle_to_omm</c> compared by <c>tle_value_rel</c> (spec.md section 15.2), under their OMM
    /// keywords.
    /// </summary>
    public static readonly IReadOnlyList<string> TleValues =
    [
        "MEAN_MOTION", "ECCENTRICITY", "INCLINATION", "RA_OF_ASC_NODE", "ARG_OF_PERICENTER", "MEAN_ANOMALY", "BSTAR",
        "MEAN_MOTION_DOT", "MEAN_MOTION_DDOT"
    ];

    /// <summary>The identifiers of a TLE, compared exactly (spec.md section 15.2), under their OMM keywords.</summary>
    public static readonly IReadOnlyList<string> TleIdentifiers =
        ["NORAD_CAT_ID", "OBJECT_ID", "CLASSIFICATION_TYPE", "ELEMENT_SET_NO", "REV_AT_EPOCH", "EPHEMERIS_TYPE"];

    /// <summary>
    /// The fields of a computed TLE compared by their keys (spec.md section 15.2), <c>items[k].tle.&lt;FIELD&gt;</c>, in
    /// the order of the format.
    /// </summary>
    public static readonly IReadOnlyList<string> TleFields =
    [
        "EPOCH", "MEAN_MOTION_DOT", "MEAN_MOTION_DDOT", "BSTAR", "INCLINATION", "RA_OF_ASC_NODE", "ECCENTRICITY",
        "ARG_OF_PERICENTER", "MEAN_ANOMALY", "MEAN_MOTION"
    ];

    /// <summary>The name of a field of a computed TLE: <c>&lt;tle&gt;.&lt;FIELD&gt;</c>.</summary>
    public static string TleField(string tle, string field) => $"{tle}.{field}";

    // The outputs of a tle_conversion golden: the fields of tle_to_omm and the lines of omm_to_tle with their fields
    private static IEnumerable<KeyValuePair<string, Metric>> TleConversionOutputs()
    {
        yield return new KeyValuePair<string, Metric>("items", Exact);
        foreach (var value in TleValues)
        {
            yield return new KeyValuePair<string, Metric>($"items[].{value}", TleValue);
        }

        yield return new KeyValuePair<string, Metric>("items[].EPOCH", TleEpoch);
        foreach (var identifier in TleIdentifiers)
        {
            yield return new KeyValuePair<string, Metric>($"items[].{identifier}", Exact);
        }

        yield return new KeyValuePair<string, Metric>(TleLines, new(MetricKind.TleLine, null, "-"));
        yield return new KeyValuePair<string, Metric>(TleField(TleLines, "EPOCH"), new(MetricKind.Seconds, "tle_field_epoch_s", "s"));
        yield return new KeyValuePair<string, Metric>(TleField(TleLines, "MEAN_MOTION"),
            new(MetricKind.Scalar, "tle_field_mean_motion_rev_day", "rev/day"));
        yield return new KeyValuePair<string, Metric>(TleField(TleLines, "ECCENTRICITY"), new(MetricKind.Scalar, "tle_field_eccentricity", "-"));
        foreach (var angle in new[] { "INCLINATION", "RA_OF_ASC_NODE", "ARG_OF_PERICENTER", "MEAN_ANOMALY" })
        {
            yield return new KeyValuePair<string, Metric>(TleField(TleLines, angle), TleFieldAngle);
        }

        yield return new KeyValuePair<string, Metric>(TleField(TleLines, "MEAN_MOTION_DOT"),
            new(MetricKind.Scalar, "tle_field_mean_motion_dot", "rev/day2"));
        yield return new KeyValuePair<string, Metric>(TleField(TleLines, "BSTAR"), TleFieldExponent);
        yield return new KeyValuePair<string, Metric>(TleField(TleLines, "MEAN_MOTION_DDOT"), TleFieldExponent);
    }

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
        ["attitude"] = ["attitude_quaternions", "pointing_directions"],
        ["sgp4"] = ["states"],
        ["tle_conversion"] = ["items"],
        ["conjunction"] = ["objects", "minima"],
        ["cdm"] = ["objects"],
        [CdmExportTable] = ["objects"]
    };

    /// <summary>
    /// The table of the outputs of a <c>cdm</c> export (spec.md section 16.3): the outputs of a decode, compared at the
    /// operational thresholds of the fields a framework writes.
    /// </summary>
    public const string CdmExportTable = "cdm:export";

    /// <summary>
    /// The table of the outputs of a case: its category, or for a <c>cdm</c> export <see cref="CdmExportTable"/>, whose
    /// outputs carry the names of a decode with other kinds and keys.
    /// </summary>
    public static string Table(string category, IReadOnlyDictionary<string, object> inputs)
    {
        return category == "cdm" && inputs != null && inputs.TryGetValue("operation", out var operation) && operation?.ToString() == "export"
            ? CdmExportTable
            : category;
    }

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
        // The TLE epoch and the TEME and ICRF states at the samples (spec.md section 15.1)
        ["sgp4"] = new Dictionary<string, Metric>
        {
            ["epoch"] = TleEpoch,
            ["states"] = Exact,
            ["states[].teme.position_km"] = new(MetricKind.ScalarPerComponent, "sgp4_position_km", "km"),
            ["states[].teme.velocity_km_s"] = new(MetricKind.ScalarPerComponent, "sgp4_velocity_km_s", "km/s"),
            ["states[].icrf.position_km"] = new(MetricKind.ScalarPerComponent, "sgp4_icrf_position_km", "km"),
            ["states[].icrf.velocity_km_s"] = new(MetricKind.ScalarPerComponent, "sgp4_icrf_velocity_km_s", "km/s")
        },
        // The fields of a TLE read, and the lines of a TLE written (spec.md section 15.2)
        ["tle_conversion"] = new Dictionary<string, Metric>(TleConversionOutputs()),
        // The TCA, the relative state, the covariances at TCA and the Pc, or every TCA (spec.md section 16.1)
        ["conjunction"] = new Dictionary<string, Metric>
        {
            ["tca_s"] = ConjunctionTca,
            ["miss_distance_km"] = new(MetricKind.Scalar, "conjunction_position_km", "km"),
            ["relative_speed_km_s"] = new(MetricKind.Scalar, "conjunction_velocity_km_s", "km/s"),
            ["relative_position_rtn_km"] = new(MetricKind.ScalarPerComponent, "conjunction_position_km", "km"),
            ["relative_velocity_rtn_km_s"] = new(MetricKind.ScalarPerComponent, "conjunction_velocity_km_s", "km/s"),
            ["objects"] = Exact,
            ["objects[].sigma_rtn"] = new(MetricKind.Relative, "covariance_sigma_rel", "-"),
            ["objects[].correlation_rtn"] = new(MetricKind.ScalarPerComponent, "covariance_correlation", "-"),
            ["collision_probability"] = Probability,
            ["minima"] = Exact,
            ["minima[].tca_s"] = ConjunctionTca,
            ["minima[].miss_distance_km"] = new(MetricKind.Scalar, "conjunction_position_km", "km")
        },
        // The 2D Pc of an encounter at TCA (spec.md section 16.2)
        ["collision_probability"] = new Dictionary<string, Metric>
        {
            ["collision_probability"] = Probability,
            ["maximum_collision_probability"] = Probability,
            ["encounter_plane_sigma_major_km"] = new(MetricKind.Relative, "encounter_plane_sigma_rel", "-"),
            ["encounter_plane_sigma_minor_km"] = new(MetricKind.Relative, "encounter_plane_sigma_rel", "-"),
            ["remediated"] = Exact
        },
        // The fields of a CDM read, and turned to inertial axes (spec.md section 16.3)
        ["cdm"] = new Dictionary<string, Metric>
        {
            ["tca"] = CdmEpoch,
            ["creation_date"] = CdmEpoch,
            ["start_screen_period"] = CdmEpoch,
            ["stop_screen_period"] = CdmEpoch,
            ["miss_distance_km"] = CdmValue,
            ["relative_speed_km_s"] = CdmValue,
            ["relative_position_rtn_km"] = CdmValue,
            ["relative_velocity_rtn_km_s"] = CdmValue,
            ["screen_volume_km"] = CdmValue,
            ["collision_probability"] = CdmValue,
            ["collision_probability_method"] = Exact,
            ["objects"] = Exact,
            ["objects[].ref_frame"] = Exact,
            ["objects[].position_km"] = CdmValue,
            ["objects[].velocity_km_s"] = CdmValue,
            ["objects[].covariance_rtn"] = CdmValue,
            ["objects[].covariance_consider"] = CdmValue,
            ["objects[].time_lastob_start"] = CdmEpoch,
            ["objects[].time_lastob_end"] = CdmEpoch,
            ["objects[].covariance_inertial"] = CdmCovariance
        },
        // The fields of the CDM a framework writes, read back (spec.md section 16.3)
        [CdmExportTable] = new Dictionary<string, Metric>
        {
            ["tca"] = CdmEpoch,
            ["miss_distance_km"] = new(MetricKind.Scalar, "cdm_position_km", "km"),
            ["relative_speed_km_s"] = new(MetricKind.Scalar, "cdm_velocity_km_s", "km/s"),
            ["relative_position_rtn_km"] = new(MetricKind.ScalarPerComponent, "cdm_position_km", "km"),
            ["relative_velocity_rtn_km_s"] = new(MetricKind.ScalarPerComponent, "cdm_velocity_km_s", "km/s"),
            ["collision_probability"] = Probability,
            ["objects"] = Exact,
            ["objects[].position_km"] = new(MetricKind.ScalarPerComponent, "cdm_position_km", "km"),
            ["objects[].velocity_km_s"] = new(MetricKind.ScalarPerComponent, "cdm_velocity_km_s", "km/s"),
            ["objects[].covariance_rtn"] = CdmCovariance
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
