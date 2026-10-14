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
internal static class CovarianceChecks
{
    /// <summary>
    /// Relative tolerance of the checks: the asymmetry against the largest element, the smallest eigenvalue against the
    /// trace (the criterion of C6 in the specification).
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

        double largest = 0.0;
        foreach (var value in matrix)
        {
            if (!double.IsFinite(value))
            {
                return $"{name} must be finite.";
            }

            largest = System.Math.Max(largest, System.Math.Abs(value));
        }

        var result = new double[length];
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                if (System.Math.Abs(matrix[dimension * i + j] - matrix[dimension * j + i]) > RelativeTolerance * largest)
                {
                    return $"{name} must be symmetric.";
                }

                result[dimension * i + j] = 0.5 * (matrix[dimension * i + j] + matrix[dimension * j + i]);
            }
        }

        var eigenvalues = Matrix<double>.Build.Dense(dimension, dimension, (i, j) => result[dimension * i + j])
            .Evd(Symmetricity.Symmetric).EigenValues;
        double trace = 0.0;
        for (int i = 0; i < dimension; i++)
        {
            trace += result[dimension * i + i];
        }

        double smallest = eigenvalues.Min(e => e.Real);
        if (smallest < -RelativeTolerance * System.Math.Abs(trace))
        {
            return $"{name} must be positive semi-definite; its smallest eigenvalue is {smallest:E3}.";
        }

        symmetrized = result;
        return null;
    }
}
