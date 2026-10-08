// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// A white acceleration noise, given by its spectral density Qc, whose covariance Q the variational equations integrate:
/// dQ/dt = A Q + Q Aᵀ + B Qc Bᵀ, with B = [0 ; I].
/// </summary>
internal interface IProcessNoiseModel
{
    /// <summary>
    /// Adds Qc at <paramref name="state"/> to <paramref name="qc"/>.
    /// </summary>
    /// <param name="state">A stage state of the integration, relative to the observer of the propagation.</param>
    /// <param name="qc">Qc, 3×3 row-major, in m²/s³, in the frame of <paramref name="state"/>. Symmetric and positive
    /// semi-definite.</param>
    void Accumulate(StateVector state, Span<double> qc);
}
