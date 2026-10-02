// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures candidate screening for one protected spacecraft against a set of secondaries.
/// </summary>
public sealed record ScreeningOptions
{
    /// <summary>
    /// Gets the coarse search cadence used for each candidate pair during screening.
    /// </summary>
    public TimeSpan SampleStep { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the largest interval that SSA is allowed to subdivide internally while refining event detection.
    /// </summary>
    public TimeSpan MaximumEventSearchStep { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the maximum miss distance allowed for a candidate to remain in the ranked results.
    /// </summary>
    public double MaxMissDistanceMeters { get; init; } = 25_000.0;

    /// <summary>
    /// Gets the maximum number of ranked encounters returned by <see cref="ConjunctionAssessment.Screen"/>.
    /// </summary>
    public int MaxResults { get; init; } = 25;

    /// <summary>
    /// Gets the fallback hard-body radius applied to secondaries that do not expose one directly.
    /// </summary>
    public double DefaultSecondaryHardBodyRadiusMeters { get; init; } = 5.0;

    /// <summary>
    /// Gets the relative-speed threshold below which an encounter is flagged as a low-velocity case.
    /// </summary>
    public double LowRelativeSpeedThresholdMetersPerSecond { get; init; } = 1.0;
}
