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
    /// Geodesy-normalized associated Legendre polynomials without Condon-Shortley phase factor.
    /// Uses the geodesy (fully-normalized) convention: N_lm = sqrt((2 - delta_0m)(2l+1)(l-m)!/(l+m)!)
    /// </summary>
    /// <param name="l">Degree</param>
    /// <param name="m">Order</param>
    /// <param name="theta">Colatitude in radians [0, PI]</param>
    /// <returns>Geodesy-normalized associated Legendre value</returns>
    public static double NormalizedAssociatedLegendre(int l, int m, double theta)
    {
        if (m < 0 || m > l || theta < 0 || theta > System.Math.PI)
        {
            return double.NaN;
        }

        double p = AssociatedLegendre(l, m, System.Math.Cos(theta));
        double delta0m = (m == 0) ? 1.0 : 0.0;
        double fact = System.Math.Sqrt((2.0 - delta0m) * (2 * l + 1) * Factorial(l - m) / Factorial(l + m));
        return fact * p;
    }

    /// <summary>
    /// Associated Legendre polynomials without Condon-Shortley phase factor
    /// </summary>
    /// <param name="l">Degree</param>
    /// <param name="m">Order</param>
    /// <param name="x">Argument (cos(theta))</param>
    /// <returns>Unnormalized associated Legendre value P_lm(x)</returns>
    public static double AssociatedLegendre(int l, int m, double x)
    {
        if (m < 0 || m > l || System.Math.Abs(x) > 1)
        {
            return double.NaN;
        }

        // Precompute constants
        double pmm = 1.0;
        double somx2 = System.Math.Sqrt(1.0 - x * x);
        double fact = 1.0;

        // Calculate Pmm (without Condon-Shortley phase)
        for (int i = 1; i <= m; i++)
        {
            pmm *= fact * somx2;
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

        return pmmp1;
    }

    /// <summary>
    /// Allocate jagged arrays for Legendre table computation.
    /// </summary>
    /// <param name="maxDegree">Maximum degree</param>
    /// <returns>Tuple of (P, dP) jagged arrays indexed [n][m]</returns>
    public static (double[][] P, double[][] dP) AllocateLegendreTable(int maxDegree)
    {
        var P = new double[maxDegree + 1][];
        var dP = new double[maxDegree + 1][];
        for (int n = 0; n <= maxDegree; n++)
        {
            P[n] = new double[n + 1];
            dP[n] = new double[n + 1];
        }

        return (P, dP);
    }

    /// <summary>
    /// Compute geodesy-normalized associated Legendre functions and their derivatives
    /// with respect to geocentric latitude φ using stable recursion.
    ///
    /// The geodesy normalization convention is:
    ///   P̄_nm(sinφ) = sqrt((2-δ₀ₘ)(2n+1)(n-m)!/(n+m)!) · P_nm(sinφ)
    ///
    /// No Condon-Shortley phase is included.
    /// </summary>
    /// <param name="maxDegree">Maximum degree (n) to compute</param>
    /// <param name="sinPhi">sin(φ) where φ is geocentric latitude</param>
    /// <param name="cosPhi">cos(φ) where φ is geocentric latitude</param>
    /// <param name="P">Output: P[n][m] = P̄_nm(sinφ)</param>
    /// <param name="dP">Output: dP[n][m] = dP̄_nm/dφ</param>
    public static void ComputeGeodesyNormalizedLegendreTable(int maxDegree, double sinPhi, double cosPhi,
        double[][] P, double[][] dP)
    {
        // Seed: P̄₀₀ = 1
        P[0][0] = 1.0;
        dP[0][0] = 0.0;

        if (maxDegree == 0) return;

        // Build sectoral (n=m) and sub-diagonal (n=m+1) terms, then general recursion
        for (int m = 0; m <= maxDegree; m++)
        {
            // Sectoral term P̄_mm
            if (m > 0)
            {
                // P̄_mm = sqrt((2-δ₀ₘ)/(2-δ₀,ₘ₋₁) * (2m+1)/(2m)) * cosPhi * P̄_{m-1,m-1}
                // For m=1: δ₀ₘ=0, δ₀,ₘ₋₁=1 → extra factor √2
                // For m≥2: δ₀ₘ=0, δ₀,ₘ₋₁=0 → factor = 1
                double deltaRatio = m == 1 ? 2.0 : 1.0;
                P[m][m] = System.Math.Sqrt(deltaRatio * (2.0 * m + 1.0) / (2.0 * m)) * cosPhi * P[m - 1][m - 1];
            }

            // Sub-diagonal term P̄_{m+1,m}
            if (m + 1 <= maxDegree)
            {
                P[m + 1][m] = System.Math.Sqrt(2.0 * m + 3.0) * sinPhi * P[m][m];
            }

            // General recursion for n >= m+2
            for (int n = m + 2; n <= maxDegree; n++)
            {
                double n2 = (double)n * n;
                double m2 = (double)m * m;
                double a_nm = System.Math.Sqrt((4.0 * n2 - 1.0) / (n2 - m2));
                double b_nm = System.Math.Sqrt((2.0 * n + 1.0) * ((n - 1.0) * (n - 1.0) - m2) /
                                               ((2.0 * n - 3.0) * (n2 - m2)));
                P[n][m] = a_nm * sinPhi * P[n - 1][m] - b_nm * P[n - 2][m];
            }
        }

        // Compute derivatives dP̄_nm/dφ using the relation derived from:
        //   (1-x²)·P'_nm(x) = -n·x·P_nm(x) + (n+m)·P_{n-1,m}(x)
        // With x=sinφ, dP̄_nm/dφ = cosφ·P̄'_nm(sinφ):
        //   dP̄_nm/dφ = (-n·sinφ·P̄_nm + c_nm·P̄_{n-1,m}) / cosφ
        //   where c_nm = sqrt((2n+1)(n+m)(n-m)/(2n-1))
        const double epsilon = 1.0e-15;
        bool nearPole = System.Math.Abs(cosPhi) < epsilon;

        if (nearPole)
        {
            // At poles, derivatives require special handling
            for (int n = 1; n <= maxDegree; n++)
            {
                for (int m = 0; m <= n; m++)
                {
                    dP[n][m] = 0.0;
                }
            }
        }
        else
        {
            for (int n = 1; n <= maxDegree; n++)
            {
                for (int m = 0; m <= n; m++)
                {
                    if (m == n)
                    {
                        // For sectoral harmonics, differentiate the recursion:
                        // P̄_nn = factor · cosPhi · P̄_{n-1,n-1}
                        // dP̄_nn/dφ = factor · (-sinPhi · P̄_{n-1,n-1} + cosPhi · dP̄_{n-1,n-1}/dφ)
                        double deltaRatio = n == 1 ? 2.0 : 1.0;
                        double factor = System.Math.Sqrt(deltaRatio * (2.0 * n + 1.0) / (2.0 * n));
                        dP[n][n] = factor * (-sinPhi * P[n - 1][n - 1] + cosPhi * dP[n - 1][n - 1]);
                    }
                    else
                    {
                        // General formula (works for all n >= 1, m < n):
                        // dP̄_nm/dφ = (-n·sinφ·P̄_nm + c_nm·P̄_{n-1,m}) / cosφ
                        double c_nm = System.Math.Sqrt((2.0 * n + 1.0) * (n + m) * (n - m) / (2.0 * n - 1.0));
                        dP[n][m] = (-n * sinPhi * P[n][m] + c_nm * P[n - 1][m]) / cosPhi;
                    }
                }
            }
        }
    }
}
