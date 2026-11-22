// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

/// <summary>
/// Configuration for a Monte Carlo propagation campaign.
/// The template spacecraft's initial state vector must carry a 6×6 covariance matrix
/// that defines the uncertainty to sample from.
/// </summary>
public sealed record MonteCarloConfiguration
{
    /// <summary>Number of Monte Carlo runs to execute. Must be at least 2.</summary>
    public required int RunCount { get; init; }

    /// <summary>Seed for the random number generator, ensuring reproducible sampling.</summary>
    public required int Seed { get; init; }

    /// <summary>
    /// Spacecraft whose initial state, physical properties, and covariance are used as the
    /// nominal reference. Each run creates a clone with a perturbed initial state.
    /// </summary>
    public required Spacecraft TemplateSpacecraft { get; init; }

    /// <summary>Propagation time window applied to every run.</summary>
    public required Window Window { get; init; }

    /// <summary>
    /// The settings of every run (R06 of #668): null for <see cref="PropagationOptions.Default"/> when no member of the
    /// flags configuration (<see cref="CelestialBodiesFactory"/>, <see cref="DeltaT"/>, <see cref="IntegratorFactory"/>,
    /// <see cref="IncludeAtmosphericDrag"/>, <see cref="IncludeSolarRadiationPressure"/>) is set. Each run builds its own
    /// integrator, forces and geopotential field, so the runs share their bodies
    /// (<see cref="PropagationOptions.ThirdBodies"/> and the template's central body), a geopotential included; each run
    /// starts from its sampled state about the template's observer.
    /// </summary>
    /// <remarks>Set together with a member of the flags configuration, the campaign throws
    /// <see cref="ArgumentException"/> before any run.</remarks>
    public PropagationOptions Propagation { get; init; }

    /// <summary>
    /// Factory that produces a fresh set of celestial bodies for each run of the flags configuration, with
    /// <see cref="DeltaT"/>; null for a campaign of <see cref="Propagation"/>, whose runs share their bodies.
    /// </summary>
    public Func<IEnumerable<CelestialItem>> CelestialBodiesFactory { get; init; }

    /// <summary>Whether to include atmospheric drag in the force model of the flags configuration.</summary>
    public bool IncludeAtmosphericDrag { get; init; }

    /// <summary>Whether to include solar radiation pressure in the force model of the flags configuration.</summary>
    public bool IncludeSolarRadiationPressure { get; init; }

    /// <summary>Output time step of the flags configuration, with <see cref="CelestialBodiesFactory"/>; zero for a
    /// campaign of <see cref="Propagation"/>.</summary>
    public TimeSpan DeltaT { get; init; }

    /// <summary>
    /// Optional factory that creates an integrator for each run of the flags configuration. When <c>null</c>, the
    /// Velocity-Verlet integrator at <see cref="DeltaT"/> is used.
    /// </summary>
    public Func<Integrator> IntegratorFactory { get; init; }

    /// <summary>
    /// Base NAIF ID for generated spacecraft. Run <c>i</c> gets ID <c>BaseNaifId - i</c>, which must stay in
    /// [-2147483, -1], the range of a spacecraft NAIF id (#676): <c>BaseNaifId − (RunCount − 1)</c> ≥ -2147483.
    /// Defaults to −100 000.
    /// </summary>
    public int BaseNaifId { get; init; } = -100_000;

    /// <summary>
    /// Optional batch propagation options (parallelism, error handling).
    /// When <c>null</c>, defaults from <see cref="BatchPropagator"/> are used.
    /// </summary>
    public BatchOptions BatchOptions { get; init; }
}
