using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// A symmetric matrix by its lower triangle in the order of CCSDS 508.0 (spec.md section 16): row by row, each row up
/// to the diagonal (CR_R, CT_R, CT_T, CN_R, CN_T, CN_N, CRDOT_R, ...), and the sigmas and correlations of a covariance.
/// </summary>
public static class LowerTriangle
{
    /// <summary>The order of the matrix of a lower triangle of <paramref name="length"/> numbers: 3 for 6, 6 for 21.</summary>
    /// <exception cref="ArgumentException">The length is not that of a lower triangle.</exception>
    public static int Order(int length)
    {
        int n = (int)System.Math.Round((System.Math.Sqrt(8.0 * length + 1.0) - 1.0) / 2.0);
        return n * (n + 1) / 2 == length && n > 0
            ? n
            : throw new ArgumentException($"{length} numbers are not the lower triangle of a symmetric matrix.");
    }

    /// <summary>The symmetric matrix of a lower triangle, each number times <paramref name="scale"/>.</summary>
    public static Matrix ToMatrix(double[] lower, double scale = 1.0)
    {
        int n = Order(lower.Length);
        var matrix = new Matrix(n, n);
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                matrix.Set(i, j, lower[k] * scale);
                matrix.Set(j, i, lower[k] * scale);
                k++;
            }
        }

        return matrix;
    }

    /// <summary>The lower triangle of a square matrix, each number times <paramref name="scale"/>.</summary>
    public static double[] Of(Matrix matrix, double scale = 1.0)
    {
        int n = matrix.Rows;
        var lower = new double[n * (n + 1) / 2];
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                lower[k++] = matrix.Get(i, j) * scale;
            }
        }

        return lower;
    }

    /// <summary>
    /// The sigmas of a covariance, √C_ii times <paramref name="scale"/>, and its correlations C_ij / √(C_ii C_jj), the
    /// strict lower triangle row by row (CT_R, CN_R, CN_T, CRDOT_R, ...).
    /// </summary>
    public static (double[] Sigmas, double[] Correlations) SigmasAndCorrelations(Matrix covariance, double scale = 1.0)
    {
        int n = covariance.Rows;
        var sigmas = new double[n];
        for (int i = 0; i < n; i++)
        {
            sigmas[i] = System.Math.Sqrt(covariance.Get(i, i));
        }

        var correlations = new double[n * (n - 1) / 2];
        int k = 0;
        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < i; j++)
            {
                correlations[k++] = covariance.Get(i, j) / (sigmas[i] * sigmas[j]);
            }
        }

        for (int i = 0; i < n; i++)
        {
            sigmas[i] *= scale;
        }

        return (sigmas, correlations);
    }
}
