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
    /// Compare quaternions by rotation angle distance.
    /// Both arrays are scalar-first [w, x, y, z].
    /// Handles q vs -q ambiguity via |dot|.
    /// Reports angular distance in radians as the delta.
    /// </summary>
    public static bool CompareQuaternion(double[] computed, double[] golden, TolerancePair tol, out double maxAbsDelta, out double maxRelDelta)
    {
        // 4D dot product — |dot| = 1 means identical rotation, 0 means 180° apart
        double dot = 0;
        for (int i = 0; i < 4; i++)
            dot += computed[i] * golden[i];

        // Angular distance in radians (handles q/-q ambiguity via |dot|)
        double absDot = System.Math.Min(System.Math.Abs(dot), 1.0);
        double angularDistance = 2.0 * System.Math.Acos(absDot);

        maxAbsDelta = angularDistance;
        maxRelDelta = angularDistance; // no meaningful relative delta for angles

        return angularDistance <= tol.AbsTol || angularDistance <= tol.RelTol;
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
