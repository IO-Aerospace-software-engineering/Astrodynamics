// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Flags that describe data quality issues or applicability warnings attached to an encounter.
/// </summary>
[Flags]
public enum EncounterQualityFlags
{
    /// <summary>
    /// No quality issue was detected.
    /// </summary>
    None = 0,

    /// <summary>
    /// The protected object covariance was unavailable at analysis time.
    /// </summary>
    MissingProtectedCovariance = 1 << 0,

    /// <summary>
    /// The secondary object covariance was unavailable at analysis time.
    /// </summary>
    MissingSecondaryCovariance = 1 << 1,

    /// <summary>
    /// No collision-probability covariance product could be constructed.
    /// </summary>
    CovarianceUnavailable = 1 << 2,

    /// <summary>
    /// The encounter-plane covariance required numerical remediation before risk evaluation, or the initial covariance of
    /// a participant, not positive semi-definite (as from rounded values), was remediated by eigenvalue clipping before
    /// its propagation to the encounter.
    /// </summary>
    CovarianceRemediated = 1 << 3,

    /// <summary>
    /// A participant had no covariance at the encounter epoch, and the covariance of its initial state, used
    /// instead, is older than <see cref="ConjunctionAnalysisOptions.StaleCovarianceThreshold"/> (60 s by default).
    /// That covariance is not propagated: it is held fixed in the RTN frame. See
    /// <see cref="EncounterState.ProtectedCovarianceAge"/> and <see cref="EncounterState.SecondaryCovarianceAge"/>.
    /// </summary>
    StaleCovarianceUsed = 1 << 4,

    /// <summary>
    /// The encounter relative speed fell below the configured low-velocity threshold.
    /// </summary>
    LowRelativeVelocityEncounter = 1 << 5,

    /// <summary>
    /// A single-covariance maximum-probability model was used because only one participant covariance was available.
    /// </summary>
    SingleCovarianceMaximumPcUsed = 1 << 6,

    /// <summary>
    /// The covariance of a participant was propagated to the encounter epoch (<see cref="CovarianceSource.Propagated"/>)
    /// past the domain where its linear propagation holds: its non-linearity index
    /// (<see cref="ParticipantCovariance.NonlinearityIndex"/>) exceeds the threshold measured against a Monte Carlo of
    /// the probability of collision (<see cref="ConjunctionAnalysisOptions.NonlinearityIndexThreshold"/>). The encounter
    /// is then beyond the domain where the Foster probability was validated against a Monte Carlo; the probability is
    /// not necessarily wrong: confirm one that matters by a Monte Carlo.
    /// </summary>
    NonlinearCovariancePropagation = 1 << 7
}
