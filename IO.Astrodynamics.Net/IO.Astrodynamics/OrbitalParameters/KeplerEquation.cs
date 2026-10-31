// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;

namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// Kepler's equation from the mean anomaly (#453): E − e sin E = M on an ellipse (0 ≤ e &lt; 1) and e sinh H − H = M on
/// a hyperbola (e &gt; 1), by Newton's method from an upper bound of the root where the function is convex, so that the
/// iterates decrease to the root and cannot leave it.
/// </summary>
/// <remarks>
/// The ellipse is solved for m = |M − 2πk| (at most π up to about 1e13 rad, at most 4 beyond, where M carries more
/// than the reduction rounds) and the hyperbola for m = |M|; the sign and the turns are restored, so that E is the root
/// for any finite M, |E − M| ≤ e. The loop stops on a step of at most 4ε|E|, or on a step below
/// 1e-6 max(1, |E|) that is no smaller than the previous one, which only rounding makes; it throws on a non-finite
/// input or iterate and after <see cref="MaximumIterations"/> iterations, and never returns NaN. Pure and without
/// state, so thread-safe.
/// </remarks>
internal static class KeplerEquation
{
    /// <summary>
    /// The iteration cap. Measured on 2026-10-25 (Linux): 11 iterations at most on the 111,584 points of the issue's
    /// grids (9 on the grid of KeplerEquationTests), e from 0 to 0.999999 and 1.000002 to 100.
    /// </summary>
    internal const int MaximumIterations = 50;

    // 2π in two doubles (W. J. Cody, W. Waite, Software Manual for the Elementary Functions, Prentice-Hall, 1980,
    // confirmed by S. Guillet, 2026-10-31; four times pio2_1 and pio2_1t of fdlibm's e_rem_pio2.c, Sun Microsystems,
    // 1993, confirmed by S. Guillet, 2026-10-31): TwoPiHigh, 2π truncated to 33 bits (31 significant), so k TwoPiHigh is exact for
    // |k| < 2^22; TwoPiLow = fl(2π − TwoPiHigh); 2π − TwoPiHigh − TwoPiLow = 1.4e-26 (mpmath at 80 digits, #453)
    private const double TwoPiHigh = 6.2831853069365025;
    private const double TwoPiLow = 2.430840202602477e-10;

    // The largest reduced anomaly solved without a second reduction, above the 3.96 reached below 2^52 rad and below 2π
    private const double ReducedBound = 4.0;

    // 2^-52
    private const double Epsilon = 2.220446049250313e-16;

    // A step below 1e-6 max(1, |E|) that does not decrease is the rounding floor of the step: Newton's next step is at
    // most C|Δ| times the last, C = |f''| / (2 f') ≤ 354 for |e − 1| ≥ 1e-6
    private const double StallStep = 1e-6;

    /// <summary>
    /// The eccentric anomaly E of the ellipse, E − e sin E = M.
    /// </summary>
    /// <param name="meanAnomaly">The mean anomaly M, any finite value.</param>
    /// <param name="eccentricity">The eccentricity, 0 ≤ e &lt; 1.</param>
    /// <returns>E, within e of M.</returns>
    /// <exception cref="InvalidOperationException">A non-finite input or iterate, or no convergence.</exception>
    internal static double EllipticAnomaly(double meanAnomaly, double eccentricity)
    {
        return EllipticAnomaly(meanAnomaly, eccentricity, MaximumIterations);
    }

    /// <inheritdoc cref="EllipticAnomaly(double, double)"/>
    /// <param name="meanAnomaly">The mean anomaly M, any finite value.</param>
    /// <param name="eccentricity">The eccentricity, 0 ≤ e &lt; 1.</param>
    /// <param name="maximumIterations">The iteration cap.</param>
    internal static double EllipticAnomaly(double meanAnomaly, double eccentricity, int maximumIterations)
    {
        double e = eccentricity;
        CheckFinite(meanAnomaly, e);

        // r = M − 2πk, |r| ≤ π up to about 1e13 rad: M − k TwoPiHigh is exact for |k| < 2^22 (Sterbenz's lemma:
        // P. H. Sterbenz, Floating-Point Computation, Prentice-Hall, 1974, confirmed by S. Guillet, 2026-10-27), and
        // k TwoPiLow within 4.1e-26 |k| of the rest of 2πk; M is kept as it is in [−π, π]
        double k = System.Math.Abs(meanAnomaly) <= Constants.PI ? 0.0 : System.Math.Round(meanAnomaly / Constants._2PI);
        double r = k == 0.0 ? meanAnomaly : (meanAnomaly - k * TwoPiHigh) - k * TwoPiLow;

        // From about 1e13 rad, k (rounded from M / fl(2π)) can miss the nearest turn and k TwoPiHigh rounds by up to
        // half its ulp, so |r| can exceed π: up to 3.96 below 2^52 rad, but beyond 2π from about 1.2e16 rad, where
        // Newton's method can fail to converge. Beyond 4, r is reduced again, exactly (the IEEE remainder of fl(2π) is
        // exact), to [−π, π], which never happens below 2^52 rad
        if (System.Math.Abs(r) > ReducedBound)
        {
            r = System.Math.IEEERemainder(r, Constants._2PI);
        }

        double m = System.Math.Abs(r);

        // Upper bounds of the root of E − e sin E = m on [0, π], where the function is convex: m + e, π, m / (1 − e),
        // and (6m / (0.95e))^(1/3) when it is at most 1 (each proved, and checked at 60 digits, in #453); for m in
        // (π, 4], the start π is below the root, where the function is concave, so the iterates rise to it
        double start = System.Math.Min(System.Math.Min(m + e, Constants.PI), m / (1.0 - e));
        if (e > 0.0)
        {
            double cubic = System.Math.Cbrt(6.0 * m / (0.95 * e));
            if (cubic <= 1.0)
            {
                start = System.Math.Min(start, cubic);
            }
        }

        double reduced = System.Math.CopySign(Newton(m, e, start, false, maximumIterations, meanAnomaly), r);

        // The turns restored by adding the correction e sin E of the reduced equation to M: exact at e = 0
        return k == 0.0 ? reduced : meanAnomaly + (reduced - r);
    }

    /// <summary>
    /// The hyperbolic anomaly H, e sinh H − H = M.
    /// </summary>
    /// <param name="meanAnomaly">The mean anomaly M, any finite value, negative before the periapsis.</param>
    /// <param name="eccentricity">The eccentricity, e &gt; 1.</param>
    /// <returns>H, of the sign of M.</returns>
    /// <exception cref="InvalidOperationException">A non-finite input or iterate, or no convergence.</exception>
    /// <remarks>
    /// For e &lt; 2 and |M| above max(3.0e307, 1.8e308 (e − 1)) rad, 6m and m / (e − 1) overflow, the start is infinite
    /// and the solver throws; no orbit reaches such a mean anomaly (1e297 years at n = 1000 rad/s).
    /// </remarks>
    internal static double HyperbolicAnomaly(double meanAnomaly, double eccentricity)
    {
        return HyperbolicAnomaly(meanAnomaly, eccentricity, MaximumIterations);
    }

    /// <inheritdoc cref="HyperbolicAnomaly(double, double)"/>
    /// <param name="meanAnomaly">The mean anomaly M, any finite value, negative before the periapsis.</param>
    /// <param name="eccentricity">The eccentricity, e &gt; 1.</param>
    /// <param name="maximumIterations">The iteration cap.</param>
    internal static double HyperbolicAnomaly(double meanAnomaly, double eccentricity, int maximumIterations)
    {
        double e = eccentricity;
        CheckFinite(meanAnomaly, e);
        double m = System.Math.Abs(meanAnomaly);

        // The root of e sinh H − H = m is below U = (6m / e)^(1/3) and asinh(m / (e − 1)), so below asinh((m + U) / e),
        // and the function is convex on [0, ∞) (each proved, and checked at 60 digits, in #453)
        double u = System.Math.Min(System.Math.Cbrt(6.0 * m / e), System.Math.Asinh(m / (e - 1.0)));
        double start = System.Math.Asinh((m + u) / e);

        return System.Math.CopySign(Newton(m, e, start, true, maximumIterations, meanAnomaly), meanAnomaly);
    }

    // Newton's method on the reduced equation, from x, an upper bound of its root on the convex side (or π below it on
    // the concave side, for an ellipse with m in (π, 4]); m and the iterates are non-negative
    private static double Newton(double m, double e, double x, bool hyperbolic, int maximumIterations,
        double meanAnomaly)
    {
        double previous = double.PositiveInfinity;
        for (int n = 0; n < maximumIterations; n++)
        {
            double step = hyperbolic
                ? (e * System.Math.Sinh(x) - x - m) / (e * System.Math.Cosh(x) - 1.0)
                : (x - e * System.Math.Sin(x) - m) / (1.0 - e * System.Math.Cos(x));
            x -= step;
            if (!double.IsFinite(x))
            {
                throw new InvalidOperationException(Message("reaches a non-finite iterate", meanAnomaly, e));
            }

            double size = System.Math.Abs(step);
            if (size <= 4.0 * Epsilon * System.Math.Abs(x) ||
                (size <= StallStep * System.Math.Max(1.0, System.Math.Abs(x)) && size >= previous))
            {
                return x;
            }

            previous = size;
        }

        string what = FormattableString.Invariant($"does not converge in {maximumIterations} iterations");
        throw new InvalidOperationException(Message(what, meanAnomaly, e));
    }

    private static void CheckFinite(double meanAnomaly, double e)
    {
        if (!double.IsFinite(meanAnomaly) || !double.IsFinite(e))
        {
            const string what = "needs a finite mean anomaly and eccentricity";
            throw new InvalidOperationException(Message(what, meanAnomaly, e));
        }
    }

    private static string Message(string what, double meanAnomaly, double e)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"Kepler's equation {what}: e = {e:R}, M = {meanAnomaly:R} rad (#453).");
    }
}
