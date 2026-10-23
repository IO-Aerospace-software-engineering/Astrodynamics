// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Propagator.Forces;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// What a propagation integrates besides the state.
/// </summary>
public enum CovariancePropagationMode
{
    /// <summary>
    /// The state transition matrix Φ(t, t0) and the sensitivity Ψ(t) to the force parameters, without a covariance.
    /// </summary>
    StateTransitionOnly,

    /// <summary>
    /// Φ, Ψ and the covariance P(t) = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q(t), from the covariance P0 of the initial orbital parameters
    /// of the spacecraft.
    /// </summary>
    Covariance
}

/// <summary>
/// The propagation of the state transition matrix and of the covariance by the variational equations, integrated by the
/// RK7(8) integrator with the state (<see cref="Integrators.RK78Integrator"/>).
/// </summary>
/// <remarks>
/// <para>
/// Off unless given: a propagation without these options computes no partial derivative, and an initial covariance is
/// not propagated (the output states carry none). See <c>docs/reference/covariance-propagation.md</c> for the equations,
/// the modeling assumptions and the units.
/// </para>
/// <para>
/// The columns of Ψ follow the forces of the propagation: Cd with atmospheric drag, Cr with a radiation force (solar
/// radiation, albedo or thermal radiation pressure), in that order (<see cref="PropagationSolution.SensitivityParameters"/>).
/// </para>
/// </remarks>
public sealed record CovariancePropagationOptions
{
    private readonly CovariancePropagationMode _mode;
    private readonly int? _geopotentialPartialsDegree;

    /// <summary>
    /// What is integrated: Φ and Ψ only (the default), or also the covariance.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined mode.</exception>
    public CovariancePropagationMode Mode
    {
        get => _mode;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Mode), value, "Undefined covariance propagation mode.");
            }

            _mode = value;
        }
    }

    /// <summary>
    /// The process noise, a white acceleration noise in RTN whose covariance Q is added to P; null (the default) for none,
    /// in which case P is the linear propagation of P0 and of the consider parameters only, which is often optimistic.
    /// Covariance mode only.
    /// </summary>
    public RtnProcessNoise ProcessNoise { get; init; }

    /// <summary>
    /// The standard deviations of the consider parameters, Pp = diag(σ²); null (the default) for none. Covariance mode
    /// only, and each needs its force in the propagation (Cd: drag; Cr: a radiation force).
    /// </summary>
    public ConsiderParameters ConsiderParameters { get; init; }

    /// <summary>
    /// Whether the error of Φ and Ψ takes part in the step-size control of the integrator. False by default: the steps
    /// are controlled on the state only, so the trajectory is the same, bit for bit, with and without these options.
    /// True changes the steps, and so the trajectory, within the tolerance of the integrator. Without effect with a
    /// fixed-step integrator (<see cref="Integrators.RK78Integrator(double)"/>), which controls no step.
    /// </summary>
    /// <remarks>
    /// Off, Ψ carries an integration error that the control of the state does not see, largest for Cr across the shadow
    /// crossings of solar radiation pressure: in an eclipsing LEO over 6 h at a tolerance of 1e-12, the error of Ψ_Cr
    /// over the arc, relative to its largest value on the arc, was 6.0e-5 to 1.9e-3 depending on where the steps fell,
    /// against 2.6e-5 at most at 1e-14; relative to the local Ψ_Cr, up to 1.5e-2 at 1e-12 (just after the first shadow
    /// exit, where Ψ_Cr is below a millimetre, and 1.2e-2 at a later crossing, where it is 0.28 m) and 1.6e-4 at 1e-14.
    /// With this option, 7.1e-8 at 1e-12. When Ψ_Cr matters (Cr as a consider parameter, with eclipses), turn it on: a
    /// tighter tolerance alone does not hold Ψ_Cr to 1e-4 at every shadow crossing.
    /// <para>
    /// On, the step follows the error of Φ and Ψ, so it needs partials that are smooth along the trajectory. The density
    /// gradient of the drag partials is taken in geodetic coordinates, without rounding noise of its own, so the steps
    /// stay ordinary in dense air (US 1976 at 200 km over 2 min: 6 to 10 steps from 1e-11 to 1e-13, against 107 290 at
    /// 1e-11 and no end at 1e-13 before #420). The SRP partials take the analytic derivative of the shadow function in
    /// the penumbra, so the steps stay ordinary through eclipses too (a Sun-synchronous LEO over one day: 1 692 to 4 757
    /// steps from 1e-11 to 1e-14, against 23 967 at 1e-13 and a failure at 1e-14 before #423). When the step collapses
    /// all the same, the propagation stops with an
    /// <see cref="InvalidOperationException"/> after 10 000 consecutive accepted steps within 100 s of flight, whose
    /// message names this option. In SSB mode the columns of Φ are scaled by the distance to the barycenter, and the step
    /// control fails (#389).
    /// </para>
    /// </remarks>
    public bool IncludeInStepControl { get; init; }

    /// <summary>
    /// The highest degree of the geopotential harmonics in the partials, at least 2; null (the default) for the degree of
    /// the field. Internal until the decision B3 of the specification.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is below 2.</exception>
    internal int? GeopotentialPartialsDegree
    {
        get => _geopotentialPartialsDegree;
        init
        {
            if (value < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(GeopotentialPartialsDegree), value,
                    "The degree of the geopotential partials must be at least 2.");
            }

            _geopotentialPartialsDegree = value;
        }
    }

    /// <summary>
    /// The internal options of the variational equations for a propagation with <paramref name="forces"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a consider
    /// parameter without its force.</exception>
    internal VariationalOptions ToVariationalOptions(IEnumerable<ForceBase> forces)
    {
        ArgumentNullException.ThrowIfNull(forces);
        bool covariance = Mode == CovariancePropagationMode.Covariance;
        if (!covariance && (ProcessNoise != null || ConsiderParameters != null))
        {
            throw new ArgumentException(
                "Process noise and consider parameters act on the covariance only: set Mode to " +
                $"{nameof(CovariancePropagationMode.Covariance)}, or remove them.");
        }

        var parameters = ForceParameters.None;
        foreach (var force in forces)
        {
            parameters |= force.Parameters;
        }

        var consider = ConsiderParameters?.Parameters ?? ForceParameters.None;
        if ((consider & ~parameters) != 0)
        {
            throw new ArgumentException(
                $"A consider parameter needs its force in the propagation: the consider parameters are {consider}, the " +
                $"parameters of the forces {parameters} (Cd needs atmospheric drag, Cr a radiation force).");
        }

        return new VariationalOptions(parameters, ProcessNoise, IncludeInStepControl, GeopotentialPartialsDegree,
            covariance, ConsiderParameters);
    }
}
