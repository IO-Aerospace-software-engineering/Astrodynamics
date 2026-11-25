// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
    /// <param name="cancellationToken">Stops the campaign (see
    /// <see cref="Propagate(MonteCarloConfiguration, IProgress{BatchProgress}, CancellationToken)"/>).</param>
    /// <returns>Aggregated statistics across all successful runs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> or required fields are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="MonteCarloConfiguration.RunCount"/> is less than
    /// 2.</exception>
    /// <exception cref="ArgumentException">Thrown when the template spacecraft's initial state vector has no covariance, or,
    /// before anything is sampled, when <see cref="MonteCarloConfiguration.Window"/> has zero length, naming <c>window</c>
    /// (decisions 7 and 15 of lot L4 of #668), or when the template spacecraft has a maneuver plan, which the runs do not
    /// execute, naming <c>config</c> (decision 41 of #668, decided by S. Guillet, 2026-11-23; before 11.0.0 it was
    /// dropped silently).</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown, before any run is propagated, when the NAIF ID of a run,
    /// <see cref="MonteCarloConfiguration.BaseNaifId"/> − i, is not in [-2147483, -1] (#676).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
    /// carries it.</exception>
    public static MonteCarloResult Propagate(MonteCarloConfiguration config, CancellationToken cancellationToken = default)
    {
        return Propagate(config, progress: null, cancellationToken);
    }

    /// <summary>
    /// Executes a Monte Carlo propagation campaign synchronously, reporting the number of completed runs after each one.
    /// </summary>
    /// <remarks>
    /// Cancellation (R28 of #668): the token is read before anything is sampled, so that a token already cancelled
    /// changes nothing, then by the batch of the runs, the running ones included (see
    /// <see cref="BatchPropagator.Propagate(IReadOnlyList{PropagationTask}, IProgress{BatchProgress}, BatchOptions, CancellationToken)"/>).
    /// A campaign stopped throws one <see cref="OperationCanceledException"/> carrying the token and returns no
    /// statistics. It replaces <c>PropagateAsync</c> and the progress of <see cref="MonteCarloConfiguration.BatchOptions"/>,
    /// removed in 11.0.0 (decision 4 of lot L4).
    /// </remarks>
    /// <param name="config">Campaign configuration including run count, template spacecraft, and force model options.</param>
    /// <param name="progress">Receives the number of completed runs after each one, propagated or failed; null for none.</param>
    /// <param name="cancellationToken">Stops the campaign, the running runs included.</param>
    /// <returns>Aggregated statistics across all successful runs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> or required fields are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="MonteCarloConfiguration.RunCount"/> is less than
    /// 2, or, before any run is propagated, when the NAIF ID of a run is not in [-2147483, -1] (#676).</exception>
    /// <exception cref="ArgumentException">Thrown when the template spacecraft's initial state vector has no covariance, or,
    /// before anything is sampled, when <see cref="MonteCarloConfiguration.Window"/> has zero length, naming <c>window</c>
    /// (decisions 7 and 15 of lot L4 of #668), or when the template spacecraft has a maneuver plan, which the runs do not
    /// execute, naming <c>config</c> (decision 41 of #668, decided by S. Guillet, 2026-11-23; before 11.0.0 it was
    /// dropped silently).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
    /// carries it.</exception>
    public static MonteCarloResult Propagate(MonteCarloConfiguration config, IProgress<BatchProgress> progress,
        CancellationToken cancellationToken = default)
    {
        Validate(config);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        var nominalSv = config.TemplateSpacecraft.InitialOrbitalParameters.ToStateVector();
        if (!nominalSv.Covariance.HasValue)
            throw new ArgumentException("Template spacecraft's initial state vector must have a covariance matrix.");

        var perturbedStates = StateSampler.Sample(nominalSv, config.RunCount, config.Seed);

        var batchResult = PropagateRuns(config, nominalSv, perturbedStates, 0, progress: progress,
            cancellationToken: cancellationToken);

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
    /// initial states, each with a clone of the template spacecraft (NAIF ID <c>BaseNaifId − run</c>) and the settings of
    /// <see cref="MonteCarloConfiguration.Propagation"/>; the coefficients, when given, replace the drag and reflectivity coefficients of the template,
    /// run by run. The runs of <see cref="Propagate(MonteCarloConfiguration, IProgress{BatchProgress}, CancellationToken)"/>, exposed internally so that the validation of the propagated
    /// covariance can read the state of every run at any epoch, in batches.
    /// </summary>
    internal static BatchResult PropagateRuns(MonteCarloConfiguration config, StateVector nominal,
        IReadOnlyList<StateVector> initialStates, int firstRun,
        IReadOnlyList<(double DragCoefficient, double ReflectivityCoefficient)> coefficients = null,
        IProgress<BatchProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var template = config.TemplateSpacecraft;
        var tasks = new PropagationTask[initialStates.Count];
        for (int k = 0; k < initialStates.Count; k++)
        {
            int i = firstRun + k;
            var runObserver = nominal.Observer;

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
                runState,
                template.MaxOperatingMass,
                clock,
                template.SectionalArea,
                coefficients?[k].DragCoefficient ?? template.DragCoefficient,
                solarRadiationCoefficient: coefficients?[k].ReflectivityCoefficient ?? template.SolarRadiationCoeff);

            tasks[k] = new PropagationTask(sc, config.Window, config.Propagation);
        }

        return BatchPropagator.Propagate(tasks, progress, config.BatchOptions, cancellationToken);
    }

    private static void Validate(MonteCarloConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.RunCount < 2)
            throw new ArgumentOutOfRangeException(nameof(config.RunCount), "RunCount must be at least 2.");
        ArgumentNullException.ThrowIfNull(config.TemplateSpacecraft);

        // The runs are built from the orbit, the dry mass, the area and the coefficients of the template: a plan would be
        // dropped silently, so it throws (decision 41 of #668, decided by S. Guillet, 2026-11-23)
        if (config.TemplateSpacecraft.Maneuvers.Count > 0)
            throw new ArgumentException(
                $"The template spacecraft {config.TemplateSpacecraft.Name} has a maneuver plan, which the runs of a Monte " +
                "Carlo campaign do not execute: each run propagates a spacecraft built from the template's orbit, dry " +
                "mass, area and coefficients. Clear the plan of the template (SetManeuvers()).", nameof(config));

        // A window of zero length throws at the start of the call, as on every propagation path (decisions 7 and 15 of
        // lot L4 of #668): before, each run failed inside the batch and the campaign returned no statistics and no reason
        PropagatorBase.RequireWindowLength(config.Window);
    }
}
