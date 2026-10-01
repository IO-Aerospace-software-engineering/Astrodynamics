// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Captures the geometric and covariance state of one encounter at the reported epoch.
/// </summary>
public sealed class EncounterState
{
    /// <summary>
    /// Initializes an encounter-state snapshot.
    /// </summary>
    public EncounterState(
        Time epoch,
        RelativeState relativeState,
        double missDistanceMeters,
        Matrix combinedCovarianceRtn,
        EncounterQualityFlags qualityFlags)
    {
        Epoch = epoch;
        RelativeState = relativeState ?? throw new ArgumentNullException(nameof(relativeState));
        MissDistanceMeters = missDistanceMeters;
        CombinedCovarianceRtn = combinedCovarianceRtn;
        QualityFlags = qualityFlags;
    }

    /// <summary>
    /// Gets the epoch at which the encounter state was evaluated.
    /// </summary>
    public Time Epoch { get; }

    /// <summary>
    /// Gets the relative position and velocity at the encounter epoch.
    /// </summary>
    public RelativeState RelativeState { get; }

    /// <summary>
    /// Gets the scalar miss distance at the encounter epoch, in meters.
    /// </summary>
    public double MissDistanceMeters { get; }

    /// <summary>
    /// Gets the combined 6x6 relative covariance expressed in the encounter RTN frame.
    /// </summary>
    public Matrix CombinedCovarianceRtn { get; }

    /// <summary>
    /// Gets quality flags that indicate missing data, remediation, or regime warnings for this encounter.
    /// </summary>
    public EncounterQualityFlags QualityFlags { get; }
}
