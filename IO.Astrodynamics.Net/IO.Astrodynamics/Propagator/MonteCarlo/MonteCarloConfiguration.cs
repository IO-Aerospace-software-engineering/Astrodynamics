// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Body.Spacecraft;
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
    /// The settings of every run (R06 of #668); null for <see cref="PropagationOptions.Default"/>, RK7(8) at 1e-9 every
    /// 60 s about the template's central body alone. Each run builds its own integrator, forces and geopotential field,
    /// so the runs share their bodies (<see cref="PropagationOptions.ThirdBodies"/> and the template's central body), a
    /// geopotential included; each run starts from its sampled state about the template's observer.
    /// </summary>
    /// <remarks>The members of the flags configuration of 10.x (<c>CelestialBodiesFactory</c>,
    /// <c>IncludeAtmosphericDrag</c>, <c>IncludeSolarRadiationPressure</c>, <c>DeltaT</c>, <c>IntegratorFactory</c>)
    /// were removed in 11.0.0 (R07 of #668): their settings are those of this property, a configuration without an
    /// integrator factory <see cref="Integrators.VVIntegratorOptions"/> at its <c>DeltaT</c>, the bodies of the factory
    /// given once in <see cref="PropagationOptions.ThirdBodies"/> (a geopotential central body as the template's
    /// observer, or in <see cref="PropagationOptions.Geopotential"/>).</remarks>
    public PropagationOptions Propagation { get; init; }

    /// <summary>
    /// Base NAIF ID for generated spacecraft. Run <c>i</c> gets ID <c>BaseNaifId - i</c>, which must stay in
    /// [-2147483, -1], the range of a spacecraft NAIF id (#676): <c>BaseNaifId − (RunCount − 1)</c> ≥ -2147483.
    /// Defaults to −100 000.
    /// </summary>
    public int BaseNaifId { get; init; } = -100_000;

    /// <summary>
    /// Optional batch propagation options (the parallelism of the runs); null for <see cref="Propagator.BatchOptions.Default"/>.
    /// The token and the progress of a campaign are parameters of <see cref="MonteCarloPropagator.Propagate(MonteCarloConfiguration, System.IProgress{BatchProgress}, System.Threading.CancellationToken)"/>
    /// since 11.0.0 (R28 of #668).
    /// </summary>
    public BatchOptions BatchOptions { get; init; }
}
