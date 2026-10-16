// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

/// <summary>
/// Runs a Monte Carlo dispersion analysis by sampling the initial state covariance,
/// propagating each perturbed state independently, and aggregating per-epoch statistics.
/// </summary>
public static class MonteCarloPropagator
{
    /// <summary>
    /// Executes a Monte Carlo propagation campaign synchronously.
    /// </summary>
    /// <param name="config">Campaign configuration including run count, template spacecraft, and force model options.</param>
    /// <returns>Aggregated statistics across all successful runs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> or required fields are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="MonteCarloConfiguration.RunCount"/> is less than 2.</exception>
    /// <exception cref="ArgumentException">Thrown when the template spacecraft's initial state vector has no covariance.</exception>
    public static MonteCarloResult Propagate(MonteCarloConfiguration config)
    {
        Validate(config);

        var stopwatch = Stopwatch.StartNew();

        var nominalSv = config.TemplateSpacecraft.InitialOrbitalParameters.ToStateVector();
        if (!nominalSv.Covariance.HasValue)
            throw new ArgumentException("Template spacecraft's initial state vector must have a covariance matrix.");

        var perturbedStates = StateSampler.Sample(nominalSv, config.RunCount, config.Seed);

        var batchResult = PropagateRuns(config, nominalSv, perturbedStates, 0);

        var successful = batchResult.Results
            .Where(r => r.Succeeded)
            .Select(r => r.Solution)
            .ToList();

        int failureCount = batchResult.Results.Count(r => !r.Succeeded);

        stopwatch.Stop();

        if (successful.Count < 2)
        {
            return new MonteCarloResult(
                Array.Empty<EpochStatistics>(),
                successful.Count,
                failureCount,
                stopwatch.Elapsed,
                config.Seed);
        }

        var epochStats = StatisticsAggregator.Aggregate(successful);

        return new MonteCarloResult(
            epochStats,
            successful.Count,
            failureCount,
            stopwatch.Elapsed,
            config.Seed);
    }

    /// <summary>
    /// Propagates the runs <paramref name="firstRun"/> to <paramref name="firstRun"/> + n − 1 of a campaign, from their
    /// initial states, each with a clone of the template spacecraft (NAIF ID <c>BaseNaifId − run</c>) and a fresh set of
    /// celestial bodies; the coefficients, when given, replace the drag and reflectivity coefficients of the template,
    /// run by run. The runs of <see cref="Propagate"/>, exposed internally so that the validation of the propagated
    /// covariance can read the state of every run at any epoch, in batches.
    /// </summary>
    internal static BatchResult PropagateRuns(MonteCarloConfiguration config, StateVector nominal,
        IReadOnlyList<StateVector> initialStates, int firstRun,
        IReadOnlyList<(double DragCoefficient, double ReflectivityCoefficient)> coefficients = null)
    {
        var template = config.TemplateSpacecraft;
        var tasks = new PropagationTask[initialStates.Count];
        for (int k = 0; k < initialStates.Count; k++)
        {
            int i = firstRun + k;
            var bodies = config.CelestialBodiesFactory();
            var bodiesList = bodies as IList<CelestialItem> ?? bodies.ToList();

            // Find matching observer by NAIF ID in factory output
            var runObserver = bodiesList.FirstOrDefault(b => b.NaifId == nominal.Observer.NaifId) ?? nominal.Observer;

            var runState = new StateVector(
                initialStates[k].Position,
                initialStates[k].Velocity,
                runObserver,
                initialStates[k].Epoch,
                initialStates[k].Frame);

            int naifId = config.BaseNaifId - i;
            var clock = new Clock($"mc_clk_{i}", template.Clock.Resolution);
            var sc = new Spacecraft(
                naifId,
                $"MC_{i:D6}",
                template.DryOperatingMass,
                template.MaximumOperatingMass,
                clock,
                runState,
                template.SectionalArea,
                coefficients?[k].DragCoefficient ?? template.DragCoefficient,
                solarRadiationCoeff: coefficients?[k].ReflectivityCoefficient ?? template.SolarRadiationCoeff);

            tasks[k] = new PropagationTask(
                sc,
                config.Window,
                bodiesList,
                config.IncludeAtmosphericDrag,
                config.IncludeSolarRadiationPressure,
                config.DeltaT,
                config.IntegratorFactory);
        }

        return BatchPropagator.Propagate(tasks, config.BatchOptions);
    }

    /// <summary>
    /// Executes a Monte Carlo propagation campaign asynchronously on a thread-pool thread.
    /// </summary>
    /// <param name="config">Campaign configuration including run count, template spacecraft, and force model options.</param>
    /// <returns>A task that completes with the aggregated statistics across all successful runs.</returns>
    public static Task<MonteCarloResult> PropagateAsync(MonteCarloConfiguration config)
    {
        return Task.Run(() => Propagate(config));
    }

    private static void Validate(MonteCarloConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.RunCount < 2)
            throw new ArgumentOutOfRangeException(nameof(config.RunCount), "RunCount must be at least 2.");
        ArgumentNullException.ThrowIfNull(config.TemplateSpacecraft);
        ArgumentNullException.ThrowIfNull(config.CelestialBodiesFactory);
    }
}
