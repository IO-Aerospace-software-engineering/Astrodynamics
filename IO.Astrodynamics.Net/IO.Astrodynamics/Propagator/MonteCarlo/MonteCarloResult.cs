// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

/// <summary>
/// Holds the aggregated output of a Monte Carlo propagation campaign, including
/// per-epoch statistics, run counts, and timing information.
/// </summary>
public sealed class MonteCarloResult
{
    /// <summary>Per-epoch statistical summary (mean, covariance, percentiles) across all successful runs. All values in SI units (m, m/s).</summary>
    public IReadOnlyList<EpochStatistics> EpochStatistics { get; }

    /// <summary>Number of runs that completed successfully.</summary>
    public int SuccessCount { get; }

    /// <summary>Number of runs that failed during propagation.</summary>
    public int FailureCount { get; }

    /// <summary>Total number of runs attempted (<see cref="SuccessCount"/> + <see cref="FailureCount"/>).</summary>
    public int TotalCount => SuccessCount + FailureCount;

    /// <summary>Wall-clock time for the entire campaign, including sampling, propagation, and aggregation.</summary>
    public TimeSpan ElapsedTime { get; }

    /// <summary>Random seed used for initial-state sampling, enabling reproducibility.</summary>
    public int Seed { get; }

    internal MonteCarloResult(IReadOnlyList<EpochStatistics> epochStatistics,
        int successCount, int failureCount, TimeSpan elapsedTime, int seed)
    {
        EpochStatistics = epochStatistics;
        SuccessCount = successCount;
        FailureCount = failureCount;
        ElapsedTime = elapsedTime;
        Seed = seed;
    }
}
