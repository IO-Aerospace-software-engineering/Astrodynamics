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
/// <param name="GeopotentialPartialsDegree">
/// The highest degree of the geopotential harmonics in the partials, at least 2; null (the default) for the degree of
/// the field, so that the partials are those of the acceleration. A degree above that of the field is that of the field,
/// and the option is ignored without a geopotential. Reducing it trades accuracy of the state transition matrix for
/// speed; the trajectory does not change.
/// </param>
internal sealed record VariationalOptions(
    ForceParameters Parameters = ForceParameters.None,
    IProcessNoiseModel ProcessNoise = null,
    bool IncludeInStepControl = false,
    int? GeopotentialPartialsDegree = null);
