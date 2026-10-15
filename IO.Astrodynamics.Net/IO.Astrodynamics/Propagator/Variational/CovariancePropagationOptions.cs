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
    /// True changes the steps, and so the trajectory, within the tolerance of the integrator.
    /// </summary>
    /// <remarks>
    /// Off, Ψ carries an integration error that the control of the state does not see, largest for Cr across the shadow
    /// crossings of solar radiation pressure: in an eclipsing LEO over 6 h at a tolerance of 1e-12, Ψ_Cr was off by
    /// 4.0e-5 to 1.3e-3 depending on where the steps fell, against 1.6e-5 and 3.4e-6 at 1e-14. A tighter tolerance or
    /// this option brings it down.
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
