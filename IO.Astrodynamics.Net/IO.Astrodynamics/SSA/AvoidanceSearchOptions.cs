// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures the impulsive avoidance trade study performed for one encounter.
/// </summary>
public sealed record AvoidanceSearchOptions
{
    /// <summary>
    /// Gets candidate lead times between the burn epoch and the analyzed TCA.
    /// </summary>
    public IReadOnlyList<TimeSpan> LeadTimes { get; init; } = new[]
    {
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6)
    };

    /// <summary>
    /// Gets candidate maneuver magnitudes to test in each sampled burn direction.
    /// </summary>
    public IReadOnlyList<double> DeltaVMagnitudesMetersPerSecond { get; init; } = new[]
    {
        0.05,
        0.10,
        0.25
    };

    /// <summary>
    /// Gets the coarse analysis cadence used when reassessing each post-maneuver trajectory.
    /// </summary>
    public TimeSpan AnalysisSampleStep { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the largest interval that SSA is allowed to subdivide internally while refining post-maneuver event detection.
    /// </summary>
    public TimeSpan MaximumEventSearchStep { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the fallback hard-body radius applied to the secondary object during post-maneuver reassessment.
    /// </summary>
    public double SecondaryHardBodyRadiusMeters { get; init; } = 5.0;

    /// <summary>
    /// Gets the maximum number of avoidance options returned after ranking.
    /// </summary>
    public int MaxReturnedOptions { get; init; } = 10;

    /// <summary>
    /// Gets the collision-probability threshold used by the ranking heuristic.
    /// Lower values are preferred, but this is not a hard acceptance filter.
    /// </summary>
    public double PcThreshold { get; init; } = 1.0e-4;

    /// <summary>
    /// Gets the relative-speed threshold below which a post-maneuver encounter is flagged as low velocity.
    /// </summary>
    public double LowRelativeSpeedThresholdMetersPerSecond { get; init; } = 1.0;
}
