using System;
using System.Collections.Generic;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// The kind "TLE line" (spec.md sections 6.1 and 15.2): the two lines of a computed TLE against those of the golden.
/// </summary>
/// <remarks>
/// <para>
/// The output itself, <c>items[k].tle</c>, is exact: both lines of 69 characters with valid checksums and line numbers,
/// the catalog number of both lines and the decoded identifiers (<c>NORAD_CAT_ID</c>, <c>OBJECT_ID</c>,
/// <c>CLASSIFICATION_TYPE</c>, <c>EPHEMERIS_TYPE</c>, blank as 0, <c>ELEMENT_SET_NO</c>, <c>REV_AT_EPOCH</c>) equal to
/// the golden's. Each other field, <c>items[k].tle.&lt;FIELD&gt;</c>, is decoded from both by the column table
/// (<see cref="TleColumns"/>), differenced in exact decimal and compared by its key: the epoch on the UTC calendar, in
/// seconds; the angles wrap-aware; B* and n̈/6 relative. The text is not compared.
/// </para>
/// <para>
/// Lines that are not a TLE of the format FAIL, the output and every field (no delta), rather than making the case an
/// ERROR; a golden that is not one is an ERROR.
/// </para>
/// </remarks>
public static class TleLineComparer
{
    private const decimal SecondsPerDay = 86400m;

    /// <summary>The measures of the lines of a computed TLE: the output, then its fields in the order of the format.</summary>
    /// <param name="category">The category of the case.</param>
    /// <param name="output">The output, <c>items[k].tle</c>.</param>
    /// <param name="computed">The lines the solver gives, a <see cref="TleText"/>.</param>
    /// <param name="golden">The golden, <c>{line1, line2}</c>.</param>
    /// <param name="tolerance">The tolerance of a field and its name, from the field's output and key.</param>
    /// <exception cref="InvalidOperationException">The solver gives no lines, or the golden is not a TLE.</exception>
    public static IReadOnlyList<OutputMeasure> Compare(string category, string output, object computed, JsonElement golden,
        Func<string, string, (TolerancePair Tolerance, string Name)> tolerance)
    {
        var lines = computed as TleText ?? throw new InvalidOperationException(
            $"The solver gives {output} as {computed?.GetType().Name ?? "null"}, not the two lines of a TLE.");
        TleFields expected;
        try
        {
            expected = TleColumns.Decode(Line(golden, "line1", output), Line(golden, "line2", output));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"The golden of {output} is not a TLE: {exception.Message}", exception);
        }

        TleFields actual = null;
        if (TleColumns.Problems(lines.Line1, lines.Line2).Count == 0)
        {
            try
            {
                actual = TleColumns.Decode(lines.Line1, lines.Line2);
            }
            catch (FormatException)
            {
                // A field its columns cannot hold: the lines FAIL below
            }
        }

        bool exact = actual != null && actual.SameIdentifiers(expected);
        var measures = new List<OutputMeasure>
        {
            new(output, MetricKind.TleLine, exact ? 0.0 : 1.0, double.NaN, "exact", null, double.NaN, double.NaN, exact)
        };

        foreach (var field in MetricTable.TleFields)
        {
            string name = MetricTable.TleField(output, field);
            if (!MetricTable.TryGet(category, name, out var metric))
            {
                throw new InvalidOperationException($"{name} is not an output of the {category} category.");
            }

            var (threshold, thresholdName) = tolerance(name, metric.Key);
            if (actual == null)
            {
                measures.Add(new OutputMeasure(name, metric.Kind, double.NaN, double.NaN, thresholdName, threshold, double.NaN,
                    double.NaN, false));
                continue;
            }

            var (absolute, relative) = Deltas(field, metric.Kind, actual.Value(field), expected.Value(field));
            measures.Add(OutputComparer.Measure(name, metric.Kind, absolute, relative, thresholdName, threshold));
        }

        return measures;
    }

    // The deltas of a field, from the exact decimal difference of its decoded values
    internal static (double Absolute, double Relative) Deltas(string field, MetricKind kind, decimal computed, decimal golden)
    {
        decimal difference = computed - golden;
        if (field == "EPOCH")
        {
            difference *= SecondsPerDay;
        }

        return kind switch
        {
            MetricKind.WrappedAngle => ((double)System.Math.Abs(difference - 360m * decimal.Round(difference / 360m, MidpointRounding.ToEven)),
                double.NaN),
            MetricKind.Relative => OutputComparer.RelativeDeltas((double)System.Math.Abs(difference), (double)System.Math.Abs(golden)),
            MetricKind.Scalar => ((double)System.Math.Abs(difference), golden != 0m ? (double)(System.Math.Abs(difference) / System.Math.Abs(golden)) : double.NaN),
            _ => ((double)System.Math.Abs(difference), double.NaN)
        };
    }

    private static string Line(JsonElement golden, string name, string output)
    {
        return golden.ValueKind == JsonValueKind.Object && golden.TryGetProperty(name, out var line) && line.ValueKind == JsonValueKind.String
            ? line.GetString()
            : throw new InvalidOperationException($"The golden gives {output} as {golden}, not {{line1, line2}}.");
    }
}
