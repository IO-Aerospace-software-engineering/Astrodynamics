using System;
using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

public static class ToleranceComparer
{
    /// <summary>
    /// OR-based tolerance check: PASS if |delta| &lt;= abs_tol OR |delta|/|golden| &lt;= rel_tol
    /// </summary>
    public static bool Passes(double computed, double golden, TolerancePair tol, out double absDelta, out double relDelta)
    {
        absDelta = System.Math.Abs(computed - golden);
        relDelta = golden != 0.0 ? absDelta / System.Math.Abs(golden) : (absDelta == 0.0 ? 0.0 : double.PositiveInfinity);

        return Passes(absDelta, relDelta, tol);
    }

    /// <summary>
    /// The OR rule on deltas already computed: PASS if absDelta &lt;= abs_tol OR relDelta &lt;= rel_tol. A NaN relative
    /// delta, that of a kind without one (an angle, a time) or of a zero golden, passes by abs_tol only.
    /// </summary>
    public static bool Passes(double absDelta, double relDelta, TolerancePair tol)
    {
        return absDelta <= tol.AbsTol || relDelta <= tol.RelTol;
    }

    /// <summary>
    /// Resolve the tolerance of the stm blocks: case override takes precedence over defaults. The categories of the suite
    /// resolve theirs without fallback (<see cref="OutputComparer"/>).
    /// </summary>
    public static TolerancePair ResolveTolerance(
        string metricName,
        Dictionary<string, TolerancePair> defaults,
        Dictionary<string, TolerancePair> overrides)
    {
        if (overrides != null && overrides.TryGetValue(metricName, out var ov))
            return ov;
        if (defaults != null && defaults.TryGetValue(metricName, out var def))
            return def;
        // Fallback: tight tolerance
        return new TolerancePair { AbsTol = 1e-12, RelTol = 1e-12 };
    }
}
