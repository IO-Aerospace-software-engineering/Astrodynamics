// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Globalization;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Captures the geometric and covariance state of one encounter at the reported epoch.
/// </summary>
/// <remarks>
/// A result of the library: <see cref="ConjunctionAssessment"/> and <see cref="IO.Astrodynamics.CCSDS.CDM.Cdm.ToEncounter"/> build
/// it, its constructors internal (R17 of #668, choice 4, lot L7; public before 11.0.0).
/// </remarks>
public sealed class EncounterState
{
    /// <summary>
    /// Initializes an encounter-state snapshot.
    /// </summary>
    internal EncounterState(
        Time epoch,
        RelativeState relativeState,
        double missDistanceMeters,
        Matrix combinedCovarianceRtn,
        EncounterQualityFlags qualityFlags)
        : this(epoch, relativeState, missDistanceMeters, combinedCovarianceRtn, ParticipantCovariance.None,
            ParticipantCovariance.None, qualityFlags)
    {
    }

    /// <summary>
    /// Initializes an encounter-state snapshot, with the covariance of each participant: its source, its age, whether a
    /// process noise was applied and its non-linearity index.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="relativeState"/>, <paramref name="protectedCovariance"/>
    /// or <paramref name="secondaryCovariance"/> is null.</exception>
    internal EncounterState(
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
        _combinedCovarianceRtn = combinedCovarianceRtn.Copy();
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
    /// Gets the combined 6x6 relative covariance expressed in the encounter RTN frame: a copy of the one the state holds,
    /// which the constructor copied too, so that a term set on the matrix given or returned does not reach it (#722).
    /// </summary>
    public Matrix CombinedCovarianceRtn => _combinedCovarianceRtn.Copy();

    private readonly Matrix _combinedCovarianceRtn;

    /// <summary>
    /// Gets quality flags that indicate missing data, remediation, or regime warnings for this encounter.
    /// </summary>
    public EncounterQualityFlags QualityFlags { get; }

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

    /// <summary>
    /// Gets how the trajectory of the protected object was computed: propagated (a solution, or the states a numerical
    /// propagation stored), the two-body orbit of its initial orbital parameters, SGP4, or read from the kernels (R17 of
    /// #668, decided by S. Guillet, 2026-11-19).
    /// </summary>
    public TrajectorySource ProtectedTrajectory { get; internal init; }

    /// <summary>
    /// Gets how the trajectory of the secondary object was computed, with the conventions of
    /// <see cref="ProtectedTrajectory"/>.
    /// </summary>
    public TrajectorySource SecondaryTrajectory { get; internal init; }

    /// <summary>
    /// The epoch, the miss distance, the relative speed and the quality flags, in invariant culture, for instance
    /// "2024-01-01T12:00:00.0000000 TDB, miss 250.000 m, relative speed 14523.112 m/s, None" (R17 of #668).
    /// </summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Epoch}, miss {MissDistanceMeters:F3} m, relative speed {RelativeState.RelativeVelocityInertial.Magnitude():F3} m/s, {QualityFlags}");
}
