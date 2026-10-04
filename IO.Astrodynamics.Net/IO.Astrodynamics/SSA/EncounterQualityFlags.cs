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
    /// The encounter-plane covariance required numerical remediation before risk evaluation.
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
    SingleCovarianceMaximumPcUsed = 1 << 6
}
