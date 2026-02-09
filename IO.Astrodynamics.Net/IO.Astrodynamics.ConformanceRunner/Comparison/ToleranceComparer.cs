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

        return absDelta <= tol.AbsTol || relDelta <= tol.RelTol;
    }

    /// <summary>
    /// Compare quaternion arrays (element-wise). Returns max deltas across all components.
    /// </summary>
    public static bool CompareQuaternion(double[] computed, double[] golden, TolerancePair tol, out double maxAbsDelta, out double maxRelDelta)
    {
        maxAbsDelta = 0;
        maxRelDelta = 0;
        bool allPass = true;

        for (int i = 0; i < 4; i++)
        {
            bool pass = Passes(computed[i], golden[i], tol, out double abs, out double rel);
            maxAbsDelta = System.Math.Max(maxAbsDelta, abs);
            maxRelDelta = System.Math.Max(maxRelDelta, rel);
            if (!pass) allPass = false;
        }

        return allPass;
    }

    /// <summary>
    /// Compare time values numerically (as seconds difference).
    /// </summary>
    public static bool CompareTime(TimeSystem.Time computed, TimeSystem.Time golden, TolerancePair tol, out double absDelta, out double relDelta)
    {
        double computedSeconds = (computed - TimeSystem.Time.J2000TDB).TotalSeconds;
        double goldenSeconds = (golden - TimeSystem.Time.J2000TDB).TotalSeconds;
        return Passes(computedSeconds, goldenSeconds, tol, out absDelta, out relDelta);
    }

    /// <summary>
    /// Exact match for string values (e.g., eclipse type).
    /// </summary>
    public static bool ExactMatch(string computed, string golden, out double absDelta, out double relDelta)
    {
        bool match = string.Equals(computed, golden, StringComparison.OrdinalIgnoreCase);
        absDelta = match ? 0.0 : 1.0;
        relDelta = match ? 0.0 : 1.0;
        return match;
    }

    /// <summary>
    /// Exact match for boolean values.
    /// </summary>
    public static bool ExactMatch(bool computed, bool golden, out double absDelta, out double relDelta)
    {
        bool match = computed == golden;
        absDelta = match ? 0.0 : 1.0;
        relDelta = match ? 0.0 : 1.0;
        return match;
    }

    /// <summary>
    /// Resolve tolerance for a metric: case override takes precedence over defaults.
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
