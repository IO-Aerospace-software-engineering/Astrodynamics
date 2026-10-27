using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// Compares the outputs of a case (<see cref="MetricTable"/>) with its golden, by the kinds of spec.md section 6.1 and
/// the OR rule of <see cref="ToleranceComparer.Passes(double, double, TolerancePair)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The case checks the outputs present in its golden (spec.md section 7); the leaves of an object of the golden (the
/// <c>statistics</c> of <c>tracking_data</c>) are its outputs, named <c>&lt;block&gt;.&lt;leaf&gt;</c>. The tolerance of an
/// output is, in order, an override of the case naming the output, an override naming its key, then an override naming
/// one of its former keys (<see cref="Metric.FormerKeys"/>), the key's default, then the default of a former key; there
/// is no fallback. An exact output has no tolerance.
/// </para>
/// <para>
/// The case is an ERROR, by an <see cref="InvalidOperationException"/> naming the output, when an output of the golden
/// is not an output of the category, is geodetic while the inputs give no ellipsoid, is not produced by the solver, could
/// not be computed by it, or has no tolerance.
/// </para>
/// </remarks>
public static class OutputComparer
{
    /// <summary>
    /// The comparison of every output of the golden, in its order.
    /// </summary>
    /// <param name="caseInput">The case: its category, its inputs (epoch, ellipsoid) and its overrides.</param>
    /// <param name="computed">
    /// The outputs of the solver: a number, three numbers for a vector, or the exception that kept the solver from
    /// computing the output.
    /// </param>
    /// <param name="golden">The <c>outputs</c> object of the expected result.</param>
    /// <param name="defaults">The defaults of <c>tolerances.yaml</c>.</param>
    /// <exception cref="InvalidOperationException">The case cannot be compared (see the remarks).</exception>
    public static IReadOnlyList<OutputMeasure> Compare(
        CaseInput caseInput,
        IReadOnlyDictionary<string, object> computed,
        JsonElement golden,
        IReadOnlyDictionary<string, TolerancePair> defaults)
    {
        var measures = new List<OutputMeasure>();
        foreach (var (output, goldenValue) in Outputs(golden))
        {
            if (!MetricTable.TryGet(caseInput.Category, output, out var metric))
            {
                throw new InvalidOperationException(
                    $"The golden holds {output}, which is not an output of the {caseInput.Category} category.");
            }

            if (metric.Geodetic && !caseInput.Inputs.ContainsKey("ellipsoid"))
            {
                throw new InvalidOperationException(
                    $"The golden holds the geodetic output {output}, but the inputs give no ellipsoid.");
            }

            if (!computed.TryGetValue(output, out var value))
            {
                throw new InvalidOperationException(
                    $"The golden holds {output}, which the {caseInput.Category} solver does not produce for these inputs.");
            }

            if (value is Exception failure)
            {
                throw new InvalidOperationException(
                    $"The golden holds {output}, which the {caseInput.Category} solver cannot compute for these inputs: {failure.Message}",
                    failure);
            }

            if (metric.Kind == MetricKind.Exact)
            {
                measures.Add(ExactMeasure(output, value, goldenValue));
                continue;
            }

            string inputScale = metric.TimeScale == null ? null : EpochParser.Scale(caseInput.Inputs["epoch"]?.ToString());
            string key = MetricTable.ToleranceKey(metric, inputScale);
            var (tolerance, toleranceName) = ResolveTolerance(output, key, caseInput.TolerancesOverride, defaults, metric.FormerKeys);
            var (absolute, relative) = Deltas(metric.Kind, value, goldenValue, output);
            measures.Add(Measure(output, metric.Kind, absolute, relative, toleranceName, tolerance));
        }

        return measures;
    }

    /// <summary>
    /// The outputs of a golden, in its order: its properties, and for a property that is an object, the leaves below it,
    /// named by their path below it joined by dots (<c>range (km).mean</c>).
    /// </summary>
    internal static IEnumerable<(string Output, JsonElement Golden)> Outputs(JsonElement golden)
    {
        foreach (var property in golden.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                yield return (property.Name, property.Value);
                continue;
            }

            foreach (var leaf in Leaves(property.Value, null))
            {
                yield return leaf;
            }
        }
    }

    private static IEnumerable<(string Output, JsonElement Golden)> Leaves(JsonElement element, string prefix)
    {
        foreach (var property in element.EnumerateObject())
        {
            string name = prefix == null ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var leaf in Leaves(property.Value, name))
                {
                    yield return leaf;
                }
            }
            else
            {
                yield return (name, property.Value);
            }
        }
    }

    /// <summary>
    /// The measure of an exact output (spec.md section 6.1): an absolute delta of 0 when the output equals the golden, 1
    /// otherwise; no tolerance, a ratio of NaN. A boolean equals its golden; the arcs of a grid (an array of
    /// <c>{rise, fall}</c> instants) equal theirs when they are as many and each instant is the same sample of the grid,
    /// within 1 µs.
    /// </summary>
    internal static OutputMeasure ExactMeasure(string output, object computed, JsonElement golden)
    {
        bool passed = computed switch
        {
            bool flag when golden.ValueKind is JsonValueKind.True or JsonValueKind.False => flag == golden.GetBoolean(),
            IReadOnlyList<Window> arcs when golden.ValueKind == JsonValueKind.Array => SameArcs(arcs, golden, output),
            _ => throw new InvalidOperationException(
                $"The solver gives {output} as {computed?.GetType().Name ?? "null"} and the golden as {golden.ValueKind}: they cannot be compared.")
        };
        return new OutputMeasure(output, MetricKind.Exact, passed ? 0.0 : 1.0, double.NaN, "exact", null, double.NaN, double.NaN, passed);
    }

    private static bool SameArcs(IReadOnlyList<Window> arcs, JsonElement golden, string output)
    {
        if (arcs.Count != golden.GetArrayLength())
        {
            return false;
        }

        int i = 0;
        foreach (var arc in golden.EnumerateArray())
        {
            if (arc.ValueKind != JsonValueKind.Object || !arc.TryGetProperty("rise", out var rise) || !arc.TryGetProperty("fall", out var fall))
            {
                throw new InvalidOperationException($"The golden gives an arc of {output} as {arc}, not a rise and a fall.");
            }

            if (!SameSample(arcs[i].StartDate, Instant(rise, output)) || !SameSample(arcs[i].EndDate, Instant(fall, output)))
            {
                return false;
            }

            i++;
        }

        return true;
    }

    private static bool SameSample(Time computed, Time golden)
    {
        return System.Math.Abs((computed - golden).TotalSeconds) < SameSampleSeconds;
    }

    // Two instants of a grid are the same sample within 1 µs, far below any step
    private const double SameSampleSeconds = 1e-6;

    /// <summary>
    /// The tolerance of an output and its name: an override naming the output, then an override naming its key, then an
    /// override naming one of its former keys, in their order, then the key's default, then the default of a former key.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="key">Its key.</param>
    /// <param name="overrides">The overrides of the case.</param>
    /// <param name="defaults">The defaults of <c>tolerances.yaml</c>.</param>
    /// <param name="formerKeys">The keys of the output before conformance-tests#25 (<see cref="Metric.FormerKeys"/>),
    /// or null.</param>
    /// <exception cref="InvalidOperationException">None of them exists.</exception>
    internal static (TolerancePair Tolerance, string Name) ResolveTolerance(
        string output,
        string key,
        IReadOnlyDictionary<string, TolerancePair> overrides,
        IReadOnlyDictionary<string, TolerancePair> defaults,
        IReadOnlyList<string> formerKeys = null)
    {
        if (overrides != null && overrides.TryGetValue(output, out var outputOverride))
        {
            return (outputOverride, output);
        }

        var keys = new[] { key }.Concat(formerKeys ?? Array.Empty<string>()).ToList();
        foreach (var source in new[] { overrides, defaults })
        {
            foreach (var name in keys)
            {
                if (source != null && source.TryGetValue(name, out var tolerance))
                {
                    return (tolerance, name);
                }
            }
        }

        throw new InvalidOperationException(
            $"No tolerance for {output}: its key {key} is neither overridden by the case nor defined in tolerances.yaml.");
    }

    /// <summary>
    /// The measure of an output: the OR rule, and its active threshold, the one of a single-threshold key (spec.md
    /// section 6.2) or, for a pair with both thresholds, the one whose ratio is the smaller.
    /// </summary>
    internal static OutputMeasure Measure(
        string output, MetricKind kind, double absolute, double relative, string toleranceName, TolerancePair tolerance)
    {
        bool passed = ToleranceComparer.Passes(absolute, relative, tolerance);
        bool absoluteIsActive = tolerance.RelTol == 0.0 ||
                                (tolerance.AbsTol != 0.0 && !(relative / tolerance.RelTol < absolute / tolerance.AbsTol));
        double threshold = absoluteIsActive ? tolerance.AbsTol : tolerance.RelTol;
        double ratio = (absoluteIsActive ? absolute : relative) / threshold;
        return new OutputMeasure(output, kind, absolute, relative, toleranceName, tolerance, threshold, ratio, passed);
    }

    /// <summary>
    /// The absolute and relative deltas of spec.md section 6.1; the relative delta is NaN for the angles, the times
    /// and a zero golden.
    /// </summary>
    internal static (double Absolute, double Relative) Deltas(MetricKind kind, object computed, JsonElement golden, string output)
    {
        if (kind == MetricKind.Vector3)
        {
            var c = Vector(computed, output);
            var g = Vector(golden, output);
            double difference = 0.0;
            double norm = 0.0;
            for (int i = 0; i < 3; i++)
            {
                difference += (c[i] - g[i]) * (c[i] - g[i]);
                norm += g[i] * g[i];
            }

            double absoluteDelta = System.Math.Sqrt(difference);
            return (absoluteDelta, Relative(absoluteDelta, System.Math.Sqrt(norm)));
        }

        if (kind == MetricKind.Instant)
        {
            var computedInstant = computed as Time? ??
                                  throw new InvalidOperationException($"The solver gives {output} as {computed?.GetType().Name ?? "null"}, not an instant.");
            return (System.Math.Abs((computedInstant - Instant(golden, output)).TotalSeconds), double.NaN);
        }

        if (kind == MetricKind.RotationAngle)
        {
            return (RotationAngle(Quaternion(computed, output), Quaternion(golden, output)), double.NaN);
        }

        double computedValue = Scalar(computed, output);
        double goldenValue = Scalar(golden, output);
        return kind switch
        {
            MetricKind.Scalar => (System.Math.Abs(computedValue - goldenValue),
                Relative(System.Math.Abs(computedValue - goldenValue), System.Math.Abs(goldenValue))),
            MetricKind.WrappedAngle => (System.Math.Abs(System.Math.IEEERemainder(computedValue - goldenValue, 360.0)), double.NaN),
            _ => (System.Math.Abs(computedValue - goldenValue), double.NaN)
        };
    }

    /// <summary>
    /// The angle of the rotation between two attitudes given as unit quaternions (spec.md section 6.1), in rad, in
    /// [0, π]: θ = 4·atan2(‖q − s·g‖, ‖q + s·g‖), s = sign(q·g), which is accurate at small angles, unlike 2·acos|q·g|.
    /// </summary>
    internal static double RotationAngle(double[] q, double[] g)
    {
        double dot = 0.0;
        for (int i = 0; i < 4; i++)
        {
            dot += q[i] * g[i];
        }

        double s = dot < 0.0 ? -1.0 : 1.0;
        double difference = 0.0;
        double sum = 0.0;
        for (int i = 0; i < 4; i++)
        {
            difference += (q[i] - s * g[i]) * (q[i] - s * g[i]);
            sum += (q[i] + s * g[i]) * (q[i] + s * g[i]);
        }

        return 4.0 * System.Math.Atan2(System.Math.Sqrt(difference), System.Math.Sqrt(sum));
    }

    private static Time Instant(JsonElement golden, string output)
    {
        return golden.ValueKind == JsonValueKind.String
            ? EpochParser.ParseInstant(golden.GetString())
            : throw new InvalidOperationException($"The golden gives {output} as {golden.ValueKind}, not an instant.");
    }

    private static double[] Quaternion(object computed, string output)
    {
        return computed is double[] { Length: 4 } quaternion
            ? quaternion
            : throw new InvalidOperationException($"The solver gives {output} as {computed?.GetType().Name ?? "null"}, not four numbers.");
    }

    private static double[] Quaternion(JsonElement golden, string output)
    {
        if (golden.ValueKind != JsonValueKind.Array || golden.GetArrayLength() != 4 ||
            golden.EnumerateArray().Any(component => component.ValueKind != JsonValueKind.Number))
        {
            throw new InvalidOperationException($"The golden gives {output} as {golden}, not four numbers.");
        }

        return golden.EnumerateArray().Select(component => component.GetDouble()).ToArray();
    }

    private static double Relative(double absolute, double goldenMagnitude)
    {
        return goldenMagnitude != 0.0 ? absolute / goldenMagnitude : double.NaN;
    }

    private static double Scalar(object computed, string output)
    {
        return computed is double value
            ? value
            : throw new InvalidOperationException($"The solver gives {output} as {computed?.GetType().Name ?? "null"}, not a number.");
    }

    private static double Scalar(JsonElement golden, string output)
    {
        return golden.ValueKind == JsonValueKind.Number
            ? golden.GetDouble()
            : throw new InvalidOperationException($"The golden gives {output} as {golden.ValueKind}, not a number.");
    }

    private static double[] Vector(object computed, string output)
    {
        return computed is double[] { Length: 3 } vector
            ? vector
            : throw new InvalidOperationException($"The solver gives {output} as {computed?.GetType().Name ?? "null"}, not three numbers.");
    }

    private static double[] Vector(JsonElement golden, string output)
    {
        if (golden.ValueKind != JsonValueKind.Array || golden.GetArrayLength() != 3 ||
            golden.EnumerateArray().Any(component => component.ValueKind != JsonValueKind.Number))
        {
            throw new InvalidOperationException($"The golden gives {output} as {golden}, not three numbers.");
        }

        return golden.EnumerateArray().Select(component => component.GetDouble()).ToArray();
    }
}
