// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Propagator.Forces;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// What the variational equations integrate besides the state transition matrix Φ.
/// </summary>
/// <param name="Parameters">
/// The force parameters of the sensitivity Ψ, one column each: Cd, then Cr. <see cref="ForceParameters.None"/> integrates
/// Φ alone.
/// </param>
/// <param name="ProcessNoise">The acceleration noise whose covariance Q is integrated, or null for no Q.</param>
/// <param name="IncludeInStepControl">
/// Whether the error of Φ and Ψ takes part in the step-size control. Off by default: the step is controlled on the
/// state only, so the trajectory is the same with and without the variational equations.
/// </param>
internal sealed record VariationalOptions(
    ForceParameters Parameters = ForceParameters.None,
    IProcessNoiseModel ProcessNoise = null,
    bool IncludeInStepControl = false);
