// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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
/// state only, so the trajectory is the same with and without the variational equations. Ψ then carries an integration
/// error the state control does not see, largest for Cr across the shadow crossings of SRP: in an eclipsing LEO over 6 h
/// at a tolerance of 1e-12, the error of Ψ_Cr over the arc, relative to its largest value on the arc, was 6.0e-5 to
/// 1.9e-3 depending on where the steps fell, against 2.6e-5 at most at 1e-14 (H3 reviews of lots A to C and of step 8).
/// Relative to the local Ψ_Cr, up to 1.5e-2 at 1e-12 and 1.6e-4 at 1e-14; with this option, 9.6e-8 at 1e-12. This
/// option, not a tighter tolerance, holds Ψ_Cr to 1e-4 at every shadow crossing.
/// </param>
/// <param name="GeopotentialPartialsDegree">
/// The highest degree of the geopotential harmonics in the partials, at least 2; null (the default) for the degree of
/// the field, so that the partials are those of the acceleration. A degree above that of the field is that of the field,
/// and the option is ignored without a geopotential. Reducing it trades accuracy of the state transition matrix for
/// speed; the trajectory does not change.
/// </param>
/// <param name="PropagateCovariance">
/// Whether the covariance P(t) = Φ P0 Φᵀ + Ψ Pp Ψᵀ + Q is propagated (C2). P0 is the covariance of the initial
/// orbital parameters of the spacecraft, which must be given at the start of the propagation. Off by default: Φ, Ψ and
/// the noise covariance Q only.
/// </param>
/// <param name="ConsiderParameters">
/// The standard deviations of the consider parameters, Pp = diag(σ²) (C4); null for none. Each needs the covariance and
/// its column of Ψ in <paramref name="Parameters"/>.
/// </param>
internal sealed record VariationalOptions(
    ForceParameters Parameters = ForceParameters.None,
    IProcessNoiseModel ProcessNoise = null,
    bool IncludeInStepControl = false,
    int? GeopotentialPartialsDegree = null,
    bool PropagateCovariance = false,
    ConsiderParameters ConsiderParameters = null);

/// <summary>
/// The force parameters taken as consider parameters: uncertain, not estimated, constant, with their standard
/// deviations. Their covariance Pp = diag(σ²) enters the propagated covariance through the sensitivity Ψ,
/// P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q, and the extended covariance [[P, Ψc Pp], [Pp Ψcᵀ, Pp]]
/// (<see cref="PropagationSolution.GetExtendedCovariance"/>).
/// </summary>
public sealed record ConsiderParameters
{
    /// <summary>
    /// Create the consider parameters.
    /// </summary>
    /// <param name="dragCoefficientSigma">Standard deviation of Cd, dimensionless; null when Cd is not one.</param>
    /// <param name="reflectivityCoefficientSigma">Standard deviation of Cr, dimensionless; null when Cr is not one.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A standard deviation is negative or not finite.</exception>
    public ConsiderParameters(double? dragCoefficientSigma = null, double? reflectivityCoefficientSigma = null)
    {
        DragCoefficientSigma = Validate(dragCoefficientSigma, nameof(dragCoefficientSigma));
        ReflectivityCoefficientSigma = Validate(reflectivityCoefficientSigma, nameof(reflectivityCoefficientSigma));
    }

    /// <summary>Standard deviation of Cd, dimensionless; null when Cd is not a consider parameter.</summary>
    public double? DragCoefficientSigma { get; }

    /// <summary>Standard deviation of Cr, dimensionless; null when Cr is not a consider parameter.</summary>
    public double? ReflectivityCoefficientSigma { get; }

    /// <summary>The parameters with a standard deviation.</summary>
    internal ForceParameters Parameters =>
        (DragCoefficientSigma.HasValue ? ForceParameters.DragCoefficient : ForceParameters.None) |
        (ReflectivityCoefficientSigma.HasValue ? ForceParameters.ReflectivityCoefficient : ForceParameters.None);

    private static double? Validate(double? sigma, string name)
    {
        if (sigma.HasValue && (!(sigma.Value >= 0.0) || double.IsPositiveInfinity(sigma.Value)))
        {
            throw new ArgumentOutOfRangeException(name, sigma,
                "A standard deviation must be finite and non-negative.");
        }

        return sigma;
    }
}
