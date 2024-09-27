// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Math;

public class LegendreFunctions
{
    public static double Factorial(int n)
    {
        double f = 1.0;
        for (int i = 2; i <= n; i++)
        {
            f *= i;
        }

        return f;
    }

    /// <summary>
    /// Normalized associated Legendre polynomials without Condon-Shortley phase factor
    /// </summary>
    /// <param name="l"></param>
    /// <param name="m"></param>
    /// <param name="theta"></param>
    /// <returns></returns>
    public static double NormalizedAssociatedLegendre(int l, int m, double theta)
    {
        if (m < 0 || m > l || theta < 0 || theta > System.Math.PI)
        {
            return double.NaN;
        }

        double p = AssociatedLegendre(l, m, System.Math.Cos(theta));
        double fact = System.Math.Sqrt((2 * l + 1) / (4 * System.Math.PI) * Factorial(l - m) / Factorial(l + m));
        return fact * p;
    }

    /// <summary>
    /// Associated Legendre polynomials without Condon-Shortley phase factor
    /// </summary>
    /// <param name="l"></param>
    /// <param name="m"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    public static double AssociatedLegendre(int l, int m, double x)
    {
        if (m < 0 || m > l || System.Math.Abs(x) > 1)
        {
            return double.NaN;
        }

        // Precompute constants
        double pmm = 1.0;
        double somx2 = System.Math.Sqrt(1.0 - x * x); // Optimized for calculation
        double fact = 1.0;

        // Calculate Pmm
        for (int i = 1; i <= m; i++)
        {
            pmm *= -fact * somx2;
            fact += 2.0;
        }

        if (l == m)
        {
            return pmm;
        }

        // Compute Pm+1,m
        double pmmp1 = x * (2 * m + 1) * pmm;
        if (l == m + 1)
        {
            return pmmp1;
        }

        // Use iteration to compute Pll
        for (int ll = m + 2; ll <= l; ll++)
        {
            double prevPmm = pmm; // Store the previous pmm
            pmm = pmmp1;
            pmmp1 = (x * (2 * ll - 1) * pmm - (ll + m - 1) * prevPmm) / (ll - m);
        }

        return pmmp1; // The last value of pmmp1 is the desired Pll
    }
}