// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Linq;
using IO.Astrodynamics.Propagator.Variational;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The checks of C6 on a published covariance, and the conversions the covariance tests share.
/// </summary>
internal static class CovarianceAssert
{
    /// <summary>
    /// The smallest eigenvalue over the trace, by the symmetric eigenvalue decomposition of MathNet.
    /// </summary>
    internal static double SmallestEigenvalueOverTrace(double[] matrix, int dimension)
    {
        var m = ToMathNet(matrix, dimension);
        double smallest = m.Evd(Symmetricity.Symmetric).EigenValues.Min(e => e.Real);
        return smallest / m.Trace();
    }

    /// <summary>
    /// C6: <paramref name="matrix"/> is symmetric, element for element, and its smallest eigenvalue is at least
    /// −1e-12 × its trace.
    /// </summary>
    internal static void SymmetricPositiveSemiDefinite(double[] matrix, int dimension, string what)
    {
        for (int i = 0; i < dimension; i++)
        {
            for (int j = i + 1; j < dimension; j++)
            {
                Assert.True(matrix[dimension * i + j] == matrix[dimension * j + i],
                    $"{what}: ({i}, {j}) {matrix[dimension * i + j]:R} against ({j}, {i}) {matrix[dimension * j + i]:R}");
            }
        }

        double ratio = SmallestEigenvalueOverTrace(matrix, dimension);
        Assert.True(ratio >= -1e-12, $"{what}: smallest eigenvalue {ratio:E2} of the trace");
    }

    /// <summary>A row-major matrix as a MathNet matrix.</summary>
    internal static Matrix<double> ToMathNet(double[] matrix, int dimension)
    {
        return Matrix<double>.Build.Dense(dimension, dimension, (i, j) => matrix[dimension * i + j]);
    }

    /// <summary>A MathNet matrix as a row-major array.</summary>
    internal static double[] ToArray(Matrix<double> matrix)
    {
        var result = new double[matrix.RowCount * matrix.ColumnCount];
        for (int i = 0; i < matrix.RowCount; i++)
        {
            for (int j = 0; j < matrix.ColumnCount; j++)
            {
                result[matrix.ColumnCount * i + j] = matrix[i, j];
            }
        }

        return result;
    }

    /// <summary>The full 6×6 matrix of the 21 packed upper elements of Q.</summary>
    internal static double[] Full(double[] packed)
    {
        var full = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                full[6 * i + j] = packed[VariationalEquations.CovarianceIndex(System.Math.Min(i, j),
                    System.Math.Max(i, j))];
            }
        }

        return full;
    }

    /// <summary>‖actual − expected‖ / ‖expected‖, Frobenius norms.</summary>
    internal static double RelativeError(double[] actual, double[] expected)
    {
        double difference = 0.0;
        double norm = 0.0;
        for (int i = 0; i < expected.Length; i++)
        {
            difference += (actual[i] - expected[i]) * (actual[i] - expected[i]);
            norm += expected[i] * expected[i];
        }

        return System.Math.Sqrt(difference / norm);
    }
}
