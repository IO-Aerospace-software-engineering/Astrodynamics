// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
namespace IO.Astrodynamics.Propagator.MonteCarlo;

/// <summary>
/// Root-sum-square dispersion percentiles computed across all Monte Carlo runs at a single epoch.
/// </summary>
/// <param name="P50">50th percentile (median).</param>
/// <param name="P95">95th percentile.</param>
/// <param name="P99">99th percentile.</param>
public readonly record struct RssPercentiles(double P50, double P95, double P99);
