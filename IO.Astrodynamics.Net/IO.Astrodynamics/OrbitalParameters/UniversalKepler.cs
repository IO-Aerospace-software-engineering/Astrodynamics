// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;

namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// The two-body motion of the near-parabolic band |e − 1| &lt; 1e-6 in universal variables (#753), counted from the
/// periapsis: the universal anomaly χ, z = α χ², α = (1 − e)/q, and the Stumpff functions C(z) = (1 − cos √z)/z and
/// S(z) = (√z − sin √z)/z^(3/2) (their hyperbolic forms for z &lt; 0), which hold through e = 1 without a branch
/// (the form of CSPICE's <c>prop2b_c</c>; D. A. Vallado, <i>Fundamentals of Astrodynamics and Applications</i>, 4th ed.,
/// 2013, algorithm 8, to be verified by S. Guillet).
/// </summary>
/// <remarks>
/// <para>
/// From the periapsis, r0 = q and r0·v0 = 0, so the universal Kepler equation is √μ t = q χ + e χ³ S(z), its derivative
/// r = q + e χ² C(z) &gt; 0; the position is x = q − χ² C(z), y = √p χ (1 − z S(z)), p = q (1 + e). Barker's anomaly
/// D = tan(ν/2) = y / (r + x) = √((1 + e)/q) χ (1 − z S) / (2 − z C), and χ = 2 √(q/(1 + e)) D A(s), s = D² (1 − e)/(1 + e),
/// A(s) = atan(√s)/√s (atanh(√−s)/√−s for s &lt; 0). At e = 1, z = 0, C = 1/2, S = 1/6: √μ t = q χ + χ³/6 with
/// χ = √(2q) D, Barker's equation. Every term is positive for χ &gt; 0, so nothing cancels.
/// </para>
/// <para>Pure and without state, so thread-safe.</para>
/// </remarks>
internal static class UniversalKepler
{
    // Below |z| = 1 the Stumpff functions by their series to z¹⁰ (the next term below 2e-22 of 1/2 and of 1/6), and
    // below |s| = 0.01 A(s) by its series to s⁸ (below 1e-18)
    private const double StumpffSeriesBound = 1.0;
    private const double ArctangentSeriesBound = 0.01;

    // The iteration cap of the safeguarded Newton's method; it converges in a few iterations from Barker's start
    internal const int MaximumIterations = 100;

    private const double Epsilon = 2.220446049250313e-16;

    // 1 / (2k + 2)! and 1 / (2k + 3)!, k = 0 to 10
    private static readonly double[] CosineSeries =
    [
        1.0 / 2.0, 1.0 / 24.0, 1.0 / 720.0, 1.0 / 40320.0, 1.0 / 3628800.0, 1.0 / 479001600.0, 1.0 / 87178291200.0,
        1.0 / 20922789888000.0, 1.0 / 6402373705728000.0, 1.0 / 2432902008176640000.0,
        1.0 / 1124000727777607680000.0
    ];
    private static readonly double[] SineSeries =
    [
        1.0 / 6.0, 1.0 / 120.0, 1.0 / 5040.0, 1.0 / 362880.0, 1.0 / 39916800.0, 1.0 / 6227020800.0,
        1.0 / 1307674368000.0, 1.0 / 355687428096000.0, 1.0 / 121645100408832000.0, 1.0 / 51090942171709440000.0,
        1.0 / 25852016738884976640000.0
    ];

    /// <summary>
    /// The Stumpff functions C(z) and S(z), with 1 − z S(z) = sin √z / √z and 2 − z C(z) = 1 + cos √z computed without
    /// cancellation (their hyperbolic forms for z &lt; 0).
    /// </summary>
    internal static (double C, double S, double SineFactor, double CosineFactor) Stumpff(double z)
    {
        if (System.Math.Abs(z) < StumpffSeriesBound)
        {
            // C = Σ (−z)^k / (2k + 2)!, S = Σ (−z)^k / (2k + 3)!, k = 0 to 10 (Horner)
            double c = CosineSeries[10];
            double s = SineSeries[10];
            for (int k = 9; k >= 0; k--)
            {
                c = CosineSeries[k] - z * c;
                s = SineSeries[k] - z * s;
            }

            return (c, s, 1.0 - z * s, 2.0 - z * c);
        }

        if (z > 0.0)
        {
            double y = System.Math.Sqrt(z);
            double half = System.Math.Sin(0.5 * y);
            double halfCos = System.Math.Cos(0.5 * y);
            return (2.0 * half * half / z, KeplerEquation.AnomalyMinusSine(y) / (z * y), System.Math.Sin(y) / y,
                2.0 * halfCos * halfCos);
        }

        double w = System.Math.Sqrt(-z);
        double halfSinh = System.Math.Sinh(0.5 * w);
        double halfCosh = System.Math.Cosh(0.5 * w);
        return (2.0 * halfSinh * halfSinh / -z, KeplerEquation.HyperbolicSineMinusAnomaly(w) / (-z * w),
            System.Math.Sinh(w) / w, 2.0 * halfCosh * halfCosh);
    }

    /// <summary>The universal anomaly χ of Barker's anomaly D = tan(ν/2) on the conic of q and e.</summary>
    internal static double FromBarkerAnomaly(double barkerAnomaly, double perigeeRadius, double e)
    {
        double s = barkerAnomaly * barkerAnomaly * ((1.0 - e) / (1.0 + e));
        double ratio;
        if (System.Math.Abs(s) < ArctangentSeriesBound)
        {
            // Σ (−s)^k / (2k + 1), k = 0 to 8
            ratio = 1.0 / 17.0;
            for (int k = 7; k >= 0; k--)
            {
                ratio = 1.0 / (2 * k + 1) - s * ratio;
            }
        }
        else if (s > 0.0)
        {
            double w = System.Math.Sqrt(s);
            ratio = System.Math.Atan(w) / w;
        }
        else
        {
            double w = System.Math.Sqrt(-s);
            ratio = System.Math.Atanh(w) / w;
        }

        return 2.0 * System.Math.Sqrt(perigeeRadius / (1.0 + e)) * barkerAnomaly * ratio;
    }

    /// <summary>The time from the periapsis to the universal anomaly χ, (q χ + e χ³ S(z)) / √μ, s.</summary>
    internal static double TimeFromPeriapsis(double universalAnomaly, double perigeeRadius, double e, double gm)
    {
        double z = (1.0 - e) / perigeeRadius * universalAnomaly * universalAnomaly;
        var (_, s, _, _) = Stumpff(z);
        return (perigeeRadius * universalAnomaly + e * universalAnomaly * universalAnomaly * universalAnomaly * s) /
               System.Math.Sqrt(gm);
    }

    /// <summary>
    /// The universal anomaly χ at a time from the periapsis: the root of q χ + e χ³ S(z) − √μ t, which increases with χ
    /// (its derivative is r &gt; 0), by Newton's method from Barker's root, kept inside the bracket [0, √μ t / q] (of the
    /// sign of t) by bisection, to a step of 4ε|χ| or less.
    /// </summary>
    /// <exception cref="InvalidOperationException">A non-finite input, or no convergence.</exception>
    internal static double Solve(double timeFromPeriapsis, double perigeeRadius, double e, double gm)
    {
        double target = System.Math.Sqrt(gm) * timeFromPeriapsis;
        if (!double.IsFinite(target) || !double.IsFinite(perigeeRadius) || !double.IsFinite(e))
        {
            throw new InvalidOperationException(Message("needs a finite time, perigee radius and eccentricity", timeFromPeriapsis, e));
        }

        if (target == 0.0)
        {
            return 0.0;
        }

        // The bracket: e χ³ S ≥ 0 for χ of the sign of t, so |χ| ≤ √μ |t| / q
        double lo = System.Math.Min(0.0, target / perigeeRadius);
        double hi = System.Math.Max(0.0, target / perigeeRadius);

        // Barker's root, χ = √(2q) D, D + D³/3 = √μ t / √(2q³)
        double barker = 2.0 * System.Math.Sinh(System.Math.Asinh(1.5 * target / System.Math.Sqrt(2.0 * perigeeRadius * perigeeRadius * perigeeRadius)) / 3.0);
        double x = System.Math.Clamp(System.Math.Sqrt(2.0 * perigeeRadius) * barker, lo, hi);
        double alpha = (1.0 - e) / perigeeRadius;

        for (int n = 0; n < MaximumIterations; n++)
        {
            double z = alpha * x * x;
            var (c, s, _, _) = Stumpff(z);
            double residual = perigeeRadius * x + e * x * x * x * s - target;
            if (residual == 0.0)
            {
                return x;
            }

            if (residual > 0.0)
            {
                hi = x;
            }
            else
            {
                lo = x;
            }

            double derivative = perigeeRadius + e * x * x * c;
            double next = x - residual / derivative;
            if (!(next > lo && next < hi))
            {
                next = 0.5 * (lo + hi);
            }

            double step = System.Math.Abs(next - x);
            x = next;
            if (step <= 4.0 * Epsilon * System.Math.Abs(x) || hi - lo <= 4.0 * Epsilon * System.Math.Abs(x))
            {
                return x;
            }
        }

        throw new InvalidOperationException(Message(
            FormattableString.Invariant($"does not converge in {MaximumIterations} iterations"), timeFromPeriapsis, e));
    }

    /// <summary>
    /// Barker's anomaly D = tan(ν/2) and the true anomaly ν of the universal anomaly χ on the conic of q and e: D =
    /// √((1 + e)/q) χ (1 − z S) / (2 − z C), ν = 2 atan2(√((1 + e)/q) χ (1 − z S), 2 − z C), in (−2π, 2π].
    /// </summary>
    internal static (double BarkerAnomaly, double TrueAnomaly) Anomalies(double universalAnomaly, double perigeeRadius,
        double e)
    {
        double z = (1.0 - e) / perigeeRadius * universalAnomaly * universalAnomaly;
        var (_, _, sineFactor, cosineFactor) = Stumpff(z);
        double numerator = System.Math.Sqrt((1.0 + e) / perigeeRadius) * universalAnomaly * sineFactor;
        return (numerator / cosineFactor, 2.0 * System.Math.Atan2(numerator, cosineFactor));
    }

    private static string Message(string what, double time, double e)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"The universal Kepler equation {what}: e = {e:R}, t = {time:R} s from the periapsis (#753).");
    }
}
