// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// A white acceleration noise of constant spectral density Qc, in the frame of the propagation.
/// </summary>
internal sealed class ConstantProcessNoise : IProcessNoiseModel
{
    // Relative tolerance of the symmetry and positivity checks, scaled by the largest element and by the trace
    private const double RelativeTolerance = 1e-12;

    private readonly double[] _qc = new double[9];

    /// <summary>
    /// Create the noise from its spectral density.
    /// </summary>
    /// <param name="qc">Qc, 3×3 row-major, in m²/s³. Symmetric and positive semi-definite, within 1e-12 relative; it is
    /// stored symmetrized.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="qc"/> does not hold 9 values, is not symmetric, or has a negative eigenvalue.
    /// </exception>
    internal ConstantProcessNoise(ReadOnlySpan<double> qc)
    {
        if (qc.Length != 9)
        {
            throw new ArgumentException($"Qc must hold 9 values, row-major; it holds {qc.Length}.", nameof(qc));
        }

        double largest = 0.0;
        foreach (var value in qc)
        {
            largest = System.Math.Max(largest, System.Math.Abs(value));
        }

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (System.Math.Abs(qc[3 * i + j] - qc[3 * j + i]) > RelativeTolerance * largest)
                {
                    throw new ArgumentException("Qc must be symmetric.", nameof(qc));
                }

                _qc[3 * i + j] = 0.5 * (qc[3 * i + j] + qc[3 * j + i]);
            }
        }

        var eigenvalues = Matrix<double>.Build.Dense(3, 3, (i, j) => _qc[3 * i + j]).Evd(Symmetricity.Symmetric)
            .EigenValues;
        double trace = _qc[0] + _qc[4] + _qc[8];
        if (eigenvalues.Min(e => e.Real) < -RelativeTolerance * System.Math.Abs(trace))
        {
            throw new ArgumentException("Qc must be positive semi-definite.", nameof(qc));
        }
    }

    /// <inheritdoc />
    public void Accumulate(StateVector state, Span<double> qc)
    {
        for (int i = 0; i < 9; i++)
        {
            qc[i] += _qc[i];
        }
    }
}
