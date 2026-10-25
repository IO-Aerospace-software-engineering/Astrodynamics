using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// Compares the outputs of a conversion case (<see cref="MetricTable"/>) with its golden, by the kinds of spec.md
/// section 6.1 and the OR rule of <see cref="ToleranceComparer.Passes(double, double, TolerancePair)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The case checks the outputs present in its golden (spec.md section 7). The tolerance of an output is, in order, an
/// override of the case naming the output, an override naming its key, then the key's default; there is no fallback.
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
        foreach (var property in golden.EnumerateObject())
        {
            string output = property.Name;
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

            string inputScale = metric.TimeScale == null ? null : EpochParser.Scale(caseInput.Inputs["epoch"]?.ToString());
            string key = MetricTable.ToleranceKey(metric, inputScale);
            var (tolerance, toleranceName) = ResolveTolerance(output, key, caseInput.TolerancesOverride, defaults);
            var (absolute, relative) = Deltas(metric.Kind, value, property.Value, output);
            measures.Add(Measure(output, metric.Kind, absolute, relative, toleranceName, tolerance));
        }

        return measures;
    }

    /// <summary>
    /// The tolerance of an output and its name: an override naming the output, then an override naming its key, then
    /// the key's default.
    /// </summary>
    /// <exception cref="InvalidOperationException">None of the three exists.</exception>
    internal static (TolerancePair Tolerance, string Name) ResolveTolerance(
        string output,
        string key,
        IReadOnlyDictionary<string, TolerancePair> overrides,
        IReadOnlyDictionary<string, TolerancePair> defaults)
    {
        if (overrides != null && overrides.TryGetValue(output, out var outputOverride))
        {
            return (outputOverride, output);
        }

        if (overrides != null && overrides.TryGetValue(key, out var keyOverride))
        {
            return (keyOverride, key);
        }

        if (defaults != null && defaults.TryGetValue(key, out var keyDefault))
        {
            return (keyDefault, key);
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
