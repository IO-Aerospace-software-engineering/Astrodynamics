// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    /// Factory that produces a fresh set of celestial bodies for each run.
    /// A factory is required because SPICE-backed objects are not thread-safe and each
    /// parallel propagation needs its own instances.
    /// </summary>
    public required Func<IEnumerable<CelestialItem>> CelestialBodiesFactory { get; init; }

    /// <summary>Whether to include atmospheric drag in the force model.</summary>
    public bool IncludeAtmosphericDrag { get; init; }

    /// <summary>Whether to include solar radiation pressure in the force model.</summary>
    public bool IncludeSolarRadiationPressure { get; init; }

    /// <summary>Output time step for the propagation dense output.</summary>
    public required TimeSpan DeltaT { get; init; }

    /// <summary>
    /// Optional factory that creates an integrator for each run. When <c>null</c>, the
    /// default integrator is used.
    /// </summary>
    public Func<Integrator> IntegratorFactory { get; init; }

    /// <summary>
    /// Base NAIF ID for generated spacecraft. Run <c>i</c> gets ID <c>BaseNaifId - i</c>.
    /// Defaults to −100 000.
    /// </summary>
    public int BaseNaifId { get; init; } = -100_000;

    /// <summary>
    /// Optional batch propagation options (parallelism, error handling).
    /// When <c>null</c>, defaults from <see cref="BatchPropagator"/> are used.
    /// </summary>
    public BatchOptions BatchOptions { get; init; }
}
