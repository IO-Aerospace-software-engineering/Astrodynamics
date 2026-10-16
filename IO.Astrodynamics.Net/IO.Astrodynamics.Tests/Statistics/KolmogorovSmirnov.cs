// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.Tests.Statistics;

/// <summary>
/// The one-sample Kolmogorov-Smirnov test of a sample against a continuous distribution, and the χ² distribution with
/// 6 degrees of freedom of the squared Mahalanobis distance of a 6-D Gaussian vector (F3).
/// </summary>
/// <remarks>
/// <para>
/// Statistic <c>D = max_i max(i/N − F(x_(i)), F(x_(i)) − (i−1)/N)</c> over the sorted sample, and the significance
/// <c>P(D_N > D) ≈ Q_KS((√N + 0.12 + 0.11/√N) D)</c> with <c>Q_KS(λ) = 2 Σ_{j≥1} (−1)^{j−1} exp(−2 j² λ²)</c>,
/// Kolmogorov's limit distribution with Stephens' correction for a finite N: W. H. Press, S. A. Teukolsky,
/// W. T. Vetterling, B. P. Flannery, <i>Numerical Recipes</i>, 3rd edition, Cambridge University Press, 2007, section
/// 14.3.3, equations (14.3.13) to (14.3.18). Its critical values at α = 0.05 and 0.01, λ = 1.358 and 1.628, are those
/// of M. A. Stephens, <i>EDF statistics for goodness of fit and some comparisons</i>, Journal of the American
/// Statistical Association 69, 1974, table 1A (case 0).
/// </para>
/// <para>
/// χ² with 6 degrees of freedom, in closed form for an even number of degrees of freedom (the Poisson sum):
/// <c>F(x) = 1 − e^(−x/2) (1 + x/2 + x²/8)</c>, M. Abramowitz, I. A. Stegun, <i>Handbook of Mathematical
/// Functions</i>, 1964, 26.4.21.
/// </para>
/// </remarks>
internal static class KolmogorovSmirnov
{
    /// <summary>The CDF of χ² with 6 degrees of freedom.</summary>
    internal static double ChiSquared6Cdf(double x)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        double half = 0.5 * x;
        return 1.0 - System.Math.Exp(-half) * (1.0 + half + 0.5 * half * half);
    }

    /// <summary>The Kolmogorov-Smirnov statistic of <paramref name="sample"/> against <paramref name="cdf"/>.</summary>
    internal static double Statistic(IEnumerable<double> sample, Func<double, double> cdf)
    {
        var sorted = sample.OrderBy(x => x).ToArray();
        int n = sorted.Length;
        if (n == 0)
        {
            throw new ArgumentException("The sample is empty.", nameof(sample));
        }

        double d = 0.0;
        for (int i = 0; i < n; i++)
        {
            double f = cdf(sorted[i]);
            d = System.Math.Max(d, System.Math.Max((i + 1.0) / n - f, f - (double)i / n));
        }

        return d;
    }

    /// <summary><c>Q_KS(λ)</c>, the probability that Kolmogorov's limit variable exceeds λ.</summary>
    internal static double Q(double lambda)
    {
        if (lambda < 1e-3)
        {
            return 1.0;
        }

        double sum = 0.0;
        double sign = 1.0;
        for (int j = 1; j <= 100; j++)
        {
            double term = sign * System.Math.Exp(-2.0 * j * j * lambda * lambda);
            sum += term;
            if (System.Math.Abs(term) < 1e-16 * System.Math.Abs(sum))
            {
                break;
            }

            sign = -sign;
        }

        return System.Math.Clamp(2.0 * sum, 0.0, 1.0);
    }

    /// <summary>The significance of the statistic <paramref name="d"/> for a sample of size <paramref name="n"/>.</summary>
    internal static double PValue(double d, int n)
    {
        double sqrtN = System.Math.Sqrt(n);
        return Q((sqrtN + 0.12 + 0.11 / sqrtN) * d);
    }
}
