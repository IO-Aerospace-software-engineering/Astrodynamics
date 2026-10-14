// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// The checks of a covariance given to the propagation (an initial covariance, a noise spectral density): finite,
/// symmetric and positive semi-definite. A matrix that fails them is rejected, never corrected.
/// </summary>
/// <remarks>
/// A covariance of the state mixes units (m², m²/s, m²/s²), so the checks are made on the correlation matrix
/// C = D^(-1/2) M D^(-1/2), D = diag(M), whatever the units: the criterion of C6 (smallest eigenvalue at least −1e-12 of
/// the trace) applied to C, and each asymmetry against √(M_ii M_jj). Scaled by the trace and the largest element of M,
/// the position variances (m²) set the tolerance of the velocity block (m²/s²): a negative velocity variance could pass
/// (H3 review of #384, decision of S. Guillet, 2026-10-14). A zero variance needs a zero row and column, which C then
/// leaves out.
/// </remarks>
internal static class CovarianceChecks
{
    /// <summary>
    /// Relative tolerance of the checks: each asymmetry against √(M_ii M_jj), the smallest eigenvalue of the correlation
    /// matrix against its trace (the criterion of C6 in the specification, on the correlation matrix).
    /// </summary>
    internal const double RelativeTolerance = 1e-12;

    /// <summary>
    /// Check <paramref name="matrix"/>, n×n row-major, and return it symmetrized, (M + Mᵀ) / 2.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="dimension">Its dimension n.</param>
    /// <param name="name">The name of the matrix in the messages.</param>
    /// <param name="symmetrized">The symmetrized matrix, n² values; null when the check fails.</param>
    /// <returns>Null when the matrix passes, the reason otherwise.</returns>
    internal static string Validate(ReadOnlySpan<double> matrix, int dimension, string name, out double[] symmetrized)
    {
        symmetrized = null;
        int length = dimension * dimension;
        if (matrix.Length != length)
        {
            return $"{name} must hold {length} values, row-major; it holds {matrix.Length}.";
        }

        foreach (var value in matrix)
        {
            if (!double.IsFinite(value))
            {
                return $"{name} must be finite.";
            }
        }

        for (int i = 0; i < dimension; i++)
        {
            double variance = matrix[dimension * i + i];
            if (variance < 0.0)
            {
                return $"{name} must be positive semi-definite; its diagonal element ({i}, {i}) is {variance:E3}.";
            }
        }

        var result = new double[length];
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                double scale = System.Math.Sqrt(matrix[dimension * i + i] * matrix[dimension * j + j]);
                if (System.Math.Abs(matrix[dimension * i + j] - matrix[dimension * j + i]) > RelativeTolerance * scale)
                {
                    return $"{name} must be symmetric.";
                }

                result[dimension * i + j] = 0.5 * (matrix[dimension * i + j] + matrix[dimension * j + i]);
            }
        }

        // A zero variance with a non-zero covariance has a 2×2 minor −M_ij² < 0
        var positive = Enumerable.Range(0, dimension).Where(i => result[dimension * i + i] > 0.0).ToArray();
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                if (result[dimension * i + i] == 0.0 && result[dimension * i + j] != 0.0)
                {
                    return $"{name} must be positive semi-definite; its variance {i} is zero and its covariance " +
                           $"({i}, {j}) is not.";
                }
            }
        }

        if (positive.Length > 0)
        {
            var correlation = Matrix<double>.Build.Dense(positive.Length, positive.Length, (a, b) =>
            {
                int i = positive[a];
                int j = positive[b];
                return result[dimension * i + j] /
                       System.Math.Sqrt(result[dimension * i + i] * result[dimension * j + j]);
            });
            double smallest = correlation.Evd(Symmetricity.Symmetric).EigenValues.Min(e => e.Real);
            if (smallest < -RelativeTolerance * correlation.Trace())
            {
                return $"{name} must be positive semi-definite; the smallest eigenvalue of its correlation matrix is " +
                       $"{smallest:E3}.";
            }
        }

        symmetrized = result;
        return null;
    }
}
