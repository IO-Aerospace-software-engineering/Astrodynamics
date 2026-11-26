// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.Ssa;

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
    /// That covariance is not propagated: it is held fixed in the RTN frame. See the <see cref="ParticipantCovariance.Age"/>
    /// of <see cref="EncounterState.ProtectedCovariance"/> and <see cref="EncounterState.SecondaryCovariance"/>.
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
    NonlinearCovariancePropagation = 1 << 7,

    /// <summary>
    /// The window holds no close approach (no zero of the range rate from negative to positive that the search finds), and
    /// <c>Analyze</c> and <c>Screen</c> return the closest window end, the
    /// absolute minimum of the distance over the window (CSPICE's <c>ABSMIN</c>), where the range rate is in general not
    /// zero: the objects are still closing at the end or receding from the start, and their closest approach lies outside
    /// the window. Widen the window to find it. <c>AnalyzeAll</c> lists no such encounter. Before 11.0.0 the sampled path
    /// returned the window midpoint unflagged, and the search on two propagations the end where the distance grows into the
    /// window (#621, decided by S. Guillet, 2026-11-17). Not raised for a close approach the search finds on an end: the
    /// sampled path returns the tick nearer the range-rate zero (#624), a window end for a zero in the half-tick next to it
    /// or exactly on it, an encounter the window holds, unflagged and listed by <c>AnalyzeAll</c> (a TCA equal to a window
    /// end was flagged in the previews of 11.0.0, adversarial review of #621).
    /// </summary>
    TcaAtWindowEnd = 1 << 8
}
