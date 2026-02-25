// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Defines the contract for numerical integrators used by propagators.
/// </summary>
public interface IIntegrator
{
    /// <summary>
    /// Integrate from result[idx-1] to result[idx].
    /// The target epoch is determined by the pre-allocated result[idx].Epoch.
    /// </summary>
    /// <param name="result">Pre-allocated state vector array</param>
    /// <param name="idx">Index of the state vector to compute</param>
    void Integrate(StateVector[] result, int idx);
}
