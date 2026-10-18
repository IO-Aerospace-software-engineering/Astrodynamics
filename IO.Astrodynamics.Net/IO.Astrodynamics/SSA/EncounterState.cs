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
        : this(epoch, relativeState, missDistanceMeters, combinedCovarianceRtn, qualityFlags, null, null)
    {
    }

    /// <summary>
    /// Initializes an encounter-state snapshot, with the age of each participant's covariance.
    /// </summary>
    public EncounterState(
        Time epoch,
        RelativeState relativeState,
        double missDistanceMeters,
        Matrix combinedCovarianceRtn,
        EncounterQualityFlags qualityFlags,
        TimeSpan? protectedCovarianceAge,
        TimeSpan? secondaryCovarianceAge)
        : this(epoch, relativeState, missDistanceMeters, combinedCovarianceRtn,
            ParticipantCovariance.FromAge(protectedCovarianceAge), ParticipantCovariance.FromAge(secondaryCovarianceAge),
            qualityFlags)
    {
    }

    /// <summary>
    /// Initializes an encounter-state snapshot, with the covariance of each participant: its source, its age, whether a
    /// process noise was applied and its non-linearity index. The quality flags come last, so that this overload and the
    /// one with the ages never compete for the same arguments.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="relativeState"/>, <paramref name="protectedCovariance"/>
    /// or <paramref name="secondaryCovariance"/> is null.</exception>
    public EncounterState(
        Time epoch,
        RelativeState relativeState,
        double missDistanceMeters,
        Matrix combinedCovarianceRtn,
        ParticipantCovariance protectedCovariance,
        ParticipantCovariance secondaryCovariance,
        EncounterQualityFlags qualityFlags)
    {
        Epoch = epoch;
        RelativeState = relativeState ?? throw new ArgumentNullException(nameof(relativeState));
        MissDistanceMeters = missDistanceMeters;
        CombinedCovarianceRtn = combinedCovarianceRtn;
        QualityFlags = qualityFlags;
        ProtectedCovariance = protectedCovariance ?? throw new ArgumentNullException(nameof(protectedCovariance));
        SecondaryCovariance = secondaryCovariance ?? throw new ArgumentNullException(nameof(secondaryCovariance));
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

    /// <summary>
    /// Gets the time between the epoch of the protected object's covariance and the encounter epoch:
    /// zero when the covariance was available at, or propagated to, the encounter epoch, the distance to the initial
    /// state epoch when the initial covariance was held in RTN, null when the protected object has no covariance.
    /// </summary>
    public TimeSpan? ProtectedCovarianceAge => ProtectedCovariance.Age;

    /// <summary>
    /// Gets the time between the epoch of the secondary object's covariance and the encounter epoch, with the same
    /// conventions as <see cref="ProtectedCovarianceAge"/>.
    /// </summary>
    public TimeSpan? SecondaryCovarianceAge => SecondaryCovariance.Age;

    /// <summary>
    /// Gets the covariance of the protected object at the encounter epoch: its source, its age, whether a process noise
    /// was applied and its non-linearity index.
    /// </summary>
    public ParticipantCovariance ProtectedCovariance { get; }

    /// <summary>
    /// Gets the covariance of the secondary object at the encounter epoch, with the same conventions as
    /// <see cref="ProtectedCovariance"/>.
    /// </summary>
    public ParticipantCovariance SecondaryCovariance { get; }
}
