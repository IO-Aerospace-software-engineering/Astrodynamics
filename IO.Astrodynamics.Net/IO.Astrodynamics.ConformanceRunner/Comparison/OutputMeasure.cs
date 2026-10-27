using System.Globalization;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// The comparison of one output of a conversion case with its golden.
/// </summary>
/// <param name="Output">The output, as named in the golden.</param>
/// <param name="Kind">How it is compared.</param>
/// <param name="AbsoluteDelta">The absolute delta of its kind: |d|, the norm of d for a vector.</param>
/// <param name="RelativeDelta">|d| / |golden|; NaN for a kind without a relative delta and for a zero golden.</param>
/// <param name="ToleranceName">The output when an override naming it decided, the tolerance key otherwise.</param>
/// <param name="Tolerance">The tolerance resolved for the output.</param>
/// <param name="Threshold">The active threshold of <paramref name="Tolerance"/>: <c>abs_tol</c> or <c>rel_tol</c>.</param>
/// <param name="Ratio">The delta the active threshold applies to, divided by it.</param>
/// <param name="Passed">The OR rule of spec.md section 6.</param>
public sealed record OutputMeasure(
    string Output,
    MetricKind Kind,
    double AbsoluteDelta,
    double RelativeDelta,
    string ToleranceName,
    TolerancePair Tolerance,
    double Threshold,
    double Ratio,
    bool Passed)
{
    /// <summary>
    /// The line the tests print for every compared output, which the CI logs of the three platforms carry (design of
    /// #458, section 7.2; since conformance-tests#19 the suite sets its lot-1 tolerances without them, decision 16 of
    /// #457), in the format of the suite's tolerance tool:
    /// <c>MEASURE &lt;case_id&gt; &lt;output&gt; abs=&lt;a&gt; rel=&lt;r&gt; tol=&lt;t&gt; ratio=&lt;q&gt; key=&lt;k&gt;</c>, every number in the
    /// round-trip format of the invariant culture.
    /// </summary>
    public string ToMeasureLine(string caseId)
    {
        return $"MEASURE {caseId} {Output} abs={Format(AbsoluteDelta)} rel={Format(RelativeDelta)} tol={Format(Threshold)} " +
               $"ratio={Format(Ratio)} key={ToleranceName}";
    }

    private static string Format(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
