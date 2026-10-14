// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// A white acceleration noise of constant spectral density Qc, in the frame of the propagation.
/// </summary>
internal sealed class ConstantProcessNoise : IProcessNoiseModel
{
    private readonly double[] _qc;

    /// <summary>
    /// Create the noise from its spectral density.
    /// </summary>
    /// <param name="qc">Qc, 3×3 row-major, in m²/s³. Symmetric and positive semi-definite, within 1e-12 relative; it is
    /// stored symmetrized.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="qc"/> does not hold 9 finite values, is not symmetric, or has a negative eigenvalue.
    /// </exception>
    internal ConstantProcessNoise(ReadOnlySpan<double> qc)
    {
        var reason = CovarianceChecks.Validate(qc, 3, "Qc", out _qc);
        if (reason != null)
        {
            throw new ArgumentException(reason, nameof(qc));
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
