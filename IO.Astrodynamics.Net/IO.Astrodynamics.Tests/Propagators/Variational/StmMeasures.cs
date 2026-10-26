// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using MathNet.Numerics.LinearAlgebra;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// Measures of a 6×6 state transition matrix Φ (row-major): canonical units, symplectic defect, determinant defect and
/// the error per 3×3 block.
/// </summary>
internal static class StmMeasures
{
    /// <summary>
    /// Φ in canonical units: S Φ S⁻¹ with S = diag(1/L, 1/L, 1/L, T/L, T/L, T/L), so that its components are of order
    /// one, n t at most, for an orbit of radius L and time unit T = √(L³/μ).
    /// </summary>
    /// <remarks>
    /// Symplecticity is invariant under this change of units: S J S = (T/L²) J and S⁻¹ J S⁻¹ = (L²/T) J, so ΦᵀJΦ = J
    /// gives (SΦS⁻¹)ᵀ J (SΦS⁻¹) = S⁻¹ Φᵀ (S J S) Φ S⁻¹ = (T/L²) S⁻¹ J S⁻¹ = J. The determinant is invariant under any
    /// similarity.
    /// </remarks>
    internal static double[] Canonical(double[] phi, double length, double gravitationalParameter)
    {
        double time = System.Math.Sqrt(length * length * length / gravitationalParameter);
        var scale = new double[6];
        for (int i = 0; i < 6; i++)
        {
            scale[i] = i < 3 ? 1.0 / length : time / length;
        }

        var canonical = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                canonical[i * 6 + j] = scale[i] * phi[i * 6 + j] / scale[j];
            }
        }

        return canonical;
    }

    /// <summary>
    /// ‖ΦᵀJΦ − J‖, Frobenius norm, with J = [[0, I], [−I, 0]]: zero for the flow of a Hamiltonian system.
    /// </summary>
    internal static double SymplecticDefect(double[] phi)
    {
        var matrix = Matrix<double>.Build.Dense(6, 6, (i, j) => phi[i * 6 + j]);
        var j = Matrix<double>.Build.Dense(6, 6, (row, column) =>
            column == row + 3 ? 1.0 : row == column + 3 ? -1.0 : 0.0);
        return (matrix.Transpose() * j * matrix - j).FrobeniusNorm();
    }

    /// <summary>
    /// ‖ΦᵀJΦ − J‖ / ‖Φ‖², Frobenius norms: the symplectic defect relative to the size of the products it is computed
    /// from, whose rounding alone makes it of order ε ‖Φ‖².
    /// </summary>
    internal static double RelativeSymplecticDefect(double[] phi)
    {
        double squaredNorm = 0.0;
        foreach (double x in phi)
        {
            squaredNorm += x * x;
        }

        return SymplecticDefect(phi) / squaredNorm;
    }

    /// <summary>|det Φ − 1|: zero for a flow that preserves the phase-space volume.</summary>
    internal static double DeterminantDefect(double[] phi)
    {
        return System.Math.Abs(Matrix<double>.Build.Dense(6, 6, (i, j) => phi[i * 6 + j]).Determinant() - 1.0);
    }

    /// <summary>The largest relative Frobenius error over the four 3×3 blocks of Φ.</summary>
    internal static double WorstBlockError(double[] actual, double[] expected)
    {
        return BlockErrors(actual, expected).Max();
    }

    /// <summary>
    /// The relative Frobenius error of each 3×3 block of Φ, each against its own norm (the blocks have different units):
    /// Φ_rr, Φ_rv, Φ_vr, Φ_vv.
    /// </summary>
    internal static double[] BlockErrors(double[] actual, double[] expected)
    {
        var errors = new double[4];
        for (int block = 0; block < 4; block++)
        {
            int rowOffset = block < 2 ? 0 : 3;
            int columnOffset = block % 2 == 0 ? 0 : 3;
            var a = new double[9];
            var e = new double[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    a[3 * i + j] = actual[6 * (rowOffset + i) + columnOffset + j];
                    e[3 * i + j] = expected[6 * (rowOffset + i) + columnOffset + j];
                }
            }

            errors[block] = RiddersDerivative.RelativeFrobeniusError(a, e);
        }

        return errors;
    }

    /// <summary>The 36 elements of a 6×6 matrix, row-major.</summary>
    internal static double[] Elements(IO.Astrodynamics.Math.Matrix matrix)
    {
        return matrix.ToArray().Cast<double>().ToArray();
    }

    /// <summary>Y = [Φ | Ψ] (6 rows, row-major) of <paramref name="solution"/> at <paramref name="epoch"/>.</summary>
    internal static double[] Y(PropagationSolution solution, TimeSystem.Time epoch)
    {
        var y = new double[6 * solution.Dynamics.ColumnCount];
        solution.EvaluateVariational(epoch, y, Array.Empty<double>());
        return y;
    }

    /// <summary>Φ, the first 6 columns of <paramref name="y"/>.</summary>
    internal static double[] Phi(double[] y)
    {
        int columns = y.Length / 6;
        var phi = new double[36];
        for (int i = 0; i < 6; i++)
        {
            Array.Copy(y, columns * i, phi, 6 * i, 6);
        }

        return phi;
    }

    /// <summary>Column <paramref name="column"/> of <paramref name="y"/>.</summary>
    internal static double[] Column(double[] y, int column)
    {
        int columns = y.Length / 6;
        return Enumerable.Range(0, 6).Select(i => y[columns * i + column]).ToArray();
    }
}
