// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures how one conjunction pair is searched and evaluated.
/// </summary>
public sealed record ConjunctionAnalysisOptions
{
    /// <summary>
    /// Gets the coarse search cadence used when sampling for candidate close approaches.
    /// </summary>
    public TimeSpan SampleStep { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the largest interval that SSA is allowed to subdivide internally while refining event detection.
    /// </summary>
    public TimeSpan MaximumEventSearchStep { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the hard-body radius assumed for the secondary object when it does not expose one directly.
    /// </summary>
    public double SecondaryHardBodyRadiusMeters { get; init; } = 5.0;

    /// <summary>
    /// Gets the time tolerance used when polishing close-approach epochs.
    /// </summary>
    public double BisectionToleranceSeconds { get; init; } = 1.0e-9;

    /// <summary>
    /// Gets the relative-speed threshold below which the encounter is flagged as a low-velocity case.
    /// </summary>
    public double LowRelativeSpeedThresholdMetersPerSecond { get; init; } = 1.0;

    /// <summary>
    /// Gets the covariance age beyond which <see cref="EncounterQualityFlags.StaleCovarianceUsed"/> is raised.
    /// The age is the time between the epoch of a participant's initial-state covariance, used when no covariance
    /// is available at the encounter epoch, and the encounter epoch. Covariance is not propagated: it is held fixed
    /// in the RTN frame. Default: 60 seconds.
    /// </summary>
    public TimeSpan StaleCovarianceThreshold { get; init; } = TimeSpan.FromSeconds(60);
}
