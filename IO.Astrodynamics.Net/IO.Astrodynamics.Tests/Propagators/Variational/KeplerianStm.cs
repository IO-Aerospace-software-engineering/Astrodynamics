// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The state transition matrix of the two-body problem from its closed-form solution, an independent reference for F2:
/// no integration, no variational equation and no hand-written partial derivative.
/// </summary>
/// <remarks>
/// <para>
/// The state at t0 + Δt is r = f r0 + g v0, v = ḟ r0 + ġ v0, with the Lagrange coefficients in universal variables:
/// f = 1 − χ² c2(ψ) / r0, g = Δt − χ³ c3(ψ) / √μ, ḟ = √μ χ (ψ c3(ψ) − 1) / (r r0), ġ = 1 − χ² c2(ψ) / r, where
/// ψ = α χ², α = 2 / r0 − v0² / μ, and the universal anomaly χ solves the universal Kepler equation
/// √μ Δt = χ³ c3(ψ) + (r0·v0 / √μ) χ² c2(ψ) + r0 χ (1 − ψ c3(ψ)), whose derivative with respect to χ is
/// r = χ² c2(ψ) + (r0·v0 / √μ) χ (1 − ψ c3(ψ)) + r0 (1 − ψ c2(ψ)). The Stumpff functions are
/// c2(ψ) = (1 − cos √ψ) / ψ and c3(ψ) = (√ψ − sin √ψ) / ψ^(3/2).
/// References: Goodyear, Astronomical Journal 70, 189 (1965); Vallado, Fundamentals of Astrodynamics and Applications,
/// 4th ed. (2013), algorithm 8; Bate, Mueller &amp; White, Fundamentals of Astrodynamics (1971), sections 4.4 and 4.5;
/// to be verified by S. Guillet. Compared, without copying code, with the open implementation of hapsira 0.18.0 (MIT
/// license), hapsira/core/propagation/vallado.py and hapsira/_math/special.py, read on 2026-10-08.
/// </para>
/// <para>
/// Φ = ∂(r, v) / ∂(r0, v0) is the gradient of that solution, computed by <see cref="Dual"/> numbers. The universal
/// Kepler equation F(χ, x0) = 0 is solved by Newton's method in double precision, then one more Newton step in dual
/// arithmetic, χ − F(χ, x0) / (∂F/∂χ), carries the derivative of the root: its gradient is −∇F / (∂F/∂χ), the implicit
/// function theorem, since F(χ, x0) vanishes at the converged root.
/// </para>
/// <para>Elliptic orbits only (α &gt; 0), the case of R1.</para>
/// </remarks>
internal static class KeplerianStm
{
    // Below this argument the Stumpff functions use their series, which the closed forms lose to cancellation
    private const double SeriesLimit = 1.0;
    private const int SeriesTerms = 12;
    private const int MaximumIterations = 100;

    /// <summary>
    /// The state Δt seconds after (r0, v0) in the two-body problem of gravitational parameter μ, and Φ (row-major 6×6).
    /// </summary>
    /// <param name="mu">Gravitational parameter, m³/s².</param>
    /// <param name="position">Initial position, m.</param>
    /// <param name="velocity">Initial velocity, m/s.</param>
    /// <param name="dt">Time of flight, s.</param>
    /// <exception cref="ArgumentException">The orbit is not elliptic.</exception>
    internal static (double[] State, double[] Phi) Propagate(double mu, Vector3 position, Vector3 velocity, double dt)
    {
        var r0 = new[]
        {
            Dual.Variable(position.X, 0), Dual.Variable(position.Y, 1), Dual.Variable(position.Z, 2)
        };
        var v0 = new[]
        {
            Dual.Variable(velocity.X, 3), Dual.Variable(velocity.Y, 4), Dual.Variable(velocity.Z, 5)
        };

        double sqrtMu = System.Math.Sqrt(mu);
        var r0Norm = Dual.Sqrt(Dot(r0, r0));
        var sigma = Dot(r0, v0) / sqrtMu;
        var alpha = 2.0 / r0Norm - Dot(v0, v0) / mu;
        if (alpha.Value <= 0.0)
        {
            throw new ArgumentException("The orbit must be elliptic.", nameof(velocity));
        }

        double chi = SolveUniversalAnomaly(sqrtMu, dt, r0Norm.Value, sigma.Value, alpha.Value);

        // One Newton step in dual arithmetic from the converged root: the gradient of the root
        var (residual, slope) = KeplerEquation(Dual.Constant(chi), sqrtMu, dt, r0Norm, sigma, alpha);
        var universalAnomaly = Dual.Constant(chi) - residual / slope.Value;

        var psi = alpha * universalAnomaly * universalAnomaly;
        var c2 = StumpffC2(psi);
        var c3 = StumpffC3(psi);
        var chi2 = universalAnomaly * universalAnomaly;
        var chi3 = chi2 * universalAnomaly;
        var r = chi2 * c2 + sigma * universalAnomaly * (1.0 - psi * c3) + r0Norm * (1.0 - psi * c2);

        var f = 1.0 - chi2 * c2 / r0Norm;
        var g = dt - chi3 * c3 / sqrtMu;
        var fDot = sqrtMu * universalAnomaly * (psi * c3 - 1.0) / (r * r0Norm);
        var gDot = 1.0 - chi2 * c2 / r;

        var state = new Dual[6];
        for (int i = 0; i < 3; i++)
        {
            state[i] = f * r0[i] + g * v0[i];
            state[3 + i] = fDot * r0[i] + gDot * v0[i];
        }

        var values = new double[6];
        var phi = new double[36];
        for (int row = 0; row < 6; row++)
        {
            values[row] = state[row].Value;
            for (int column = 0; column < 6; column++)
            {
                phi[row * 6 + column] = state[row].Derivative(column);
            }
        }

        return (values, phi);
    }

    // Newton's method in double precision, from the elliptic first guess χ = √μ Δt α, to the last change of a few ulps
    private static double SolveUniversalAnomaly(double sqrtMu, double dt, double r0, double sigma, double alpha)
    {
        double chi = sqrtMu * dt * alpha;
        for (int iteration = 0; iteration < MaximumIterations; iteration++)
        {
            var (residual, slope) = KeplerEquation(Dual.Constant(chi), sqrtMu, dt, Dual.Constant(r0),
                Dual.Constant(sigma), Dual.Constant(alpha));
            double step = residual.Value / slope.Value;
            chi -= step;
            if (System.Math.Abs(step) <= 4.0 * double.Epsilon + 1e-15 * System.Math.Abs(chi))
            {
                return chi;
            }
        }

        throw new InvalidOperationException("The universal Kepler equation did not converge.");
    }

    // F(χ) = χ³ c3 + σ χ² c2 + r0 χ (1 − ψ c3) − √μ Δt and ∂F/∂χ = r, with σ = r0·v0 / √μ
    private static (Dual Residual, Dual Slope) KeplerEquation(Dual chi, double sqrtMu, double dt, Dual r0, Dual sigma,
        Dual alpha)
    {
        var psi = alpha * chi * chi;
        var c2 = StumpffC2(psi);
        var c3 = StumpffC3(psi);
        var chi2 = chi * chi;
        var residual = chi2 * chi * c3 + sigma * chi2 * c2 + r0 * chi * (1.0 - psi * c3) - sqrtMu * dt;
        var slope = chi2 * c2 + sigma * chi * (1.0 - psi * c3) + r0 * (1.0 - psi * c2);
        return (residual, slope);
    }

    // c2(ψ) = (1 − cos √ψ) / ψ, or Σ (−ψ)^k / (2k + 2)! for small ψ
    private static Dual StumpffC2(Dual psi)
    {
        if (psi.Value > SeriesLimit)
        {
            var root = Dual.Sqrt(psi);
            return (1.0 - Dual.Cos(root)) / psi;
        }

        return Series(psi, 2);
    }

    // c3(ψ) = (√ψ − sin √ψ) / ψ^(3/2), or Σ (−ψ)^k / (2k + 3)! for small ψ
    private static Dual StumpffC3(Dual psi)
    {
        if (psi.Value > SeriesLimit)
        {
            var root = Dual.Sqrt(psi);
            return (root - Dual.Sin(root)) / (psi * root);
        }

        return Series(psi, 3);
    }

    // Σ_k (−ψ)^k / (2k + offset)!, k = 0 … SeriesTerms − 1: the first omitted term is below 1e-25 for |ψ| ≤ 1
    private static Dual Series(Dual psi, int offset)
    {
        double factorial = 1.0;
        for (int i = 2; i <= offset; i++)
        {
            factorial *= i;
        }

        Dual term = 1.0 / factorial;
        Dual sum = term;
        for (int k = 1; k < SeriesTerms; k++)
        {
            term = -term * psi / ((2.0 * k + offset - 1.0) * (2.0 * k + offset));
            sum += term;
        }

        return sum;
    }

    private static Dual Dot(Dual[] a, Dual[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}
