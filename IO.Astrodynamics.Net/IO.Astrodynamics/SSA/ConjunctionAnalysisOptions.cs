// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;

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
    /// is available at the encounter epoch, and the encounter epoch; that covariance is not propagated, it is held fixed
    /// in the RTN frame (<see cref="CovarianceSource.InitialStateHeldInRtn"/>). A propagated covariance is at the
    /// encounter epoch and is never stale. Default: 60 seconds.
    /// </summary>
    public TimeSpan StaleCovarianceThreshold { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the propagation of the covariance of the participants to the encounter epoch; null restores the covariance
    /// of the initial state held in RTN (<see cref="CovarianceSource.InitialStateHeldInRtn"/>) for every participant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The covariance of a participant is propagated when the library propagates it in RK7(8) (a
    /// <see cref="PropagationSolution"/> passed to the analysis, or a spacecraft whose last propagation was RK7(8)), its
    /// initial state, at the start of that propagation, carries a covariance, and the analysis computes a probability of
    /// collision (<see cref="ConjunctionAssessment.Screen"/> never propagates it). The variational equations are then
    /// replayed along the steps of that propagation, from its start to the last encounter retained, without a second
    /// propagation; a propagation that already propagated the covariance is read as it is. Other participants keep the
    /// fallback (<see cref="CovarianceSource.InitialStateHeldInRtn"/>).
    /// </para>
    /// <para>
    /// Default: <see cref="CovariancePropagationMode.Covariance"/>, without process noise and without consider
    /// parameters, the same options for every participant. <see cref="CovariancePropagationMode.StateTransitionOnly"/>
    /// and <see cref="CovariancePropagationOptions.IncludeInStepControl"/>, which would change the steps of the
    /// propagation, are rejected; a consider parameter needs its force in the propagation of each participant whose
    /// covariance is propagated.
    /// </para>
    /// </remarks>
    public CovariancePropagationOptions CovariancePropagation { get; init; } =
        new() { Mode = CovariancePropagationMode.Covariance };

    /// <summary>
    /// Gets whether the non-linearity index of each propagated covariance is computed at the encounter epoch
    /// (<see cref="ParticipantCovariance.NonlinearityIndex"/>). It costs twelve propagations of the state transition
    /// matrix per participant, from its initial state to the encounter epoch: on two LEO participants over one day,
    /// <see cref="ConjunctionAssessment.Analyze(ProtectedSpacecraftProfile, IO.Astrodynamics.Body.ILocalizable, TimeSystem.Window, ConjunctionAnalysisOptions)"/>
    /// takes 10.1 s with it against 1.5 s without. Default: false (decision of S. Guillet, 2026-10-18): the Foster
    /// probability was measured to agree with a Monte Carlo far beyond the threshold, so the index is an indicator to ask
    /// for, not a default alarm.
    /// </summary>
    public bool ComputeNonlinearityIndex { get; init; }

    /// <summary>
    /// Gets the non-linearity index above which <see cref="EncounterQualityFlags.NonlinearCovariancePropagation"/> is
    /// raised. Default: 0.75, the largest index at which the Foster probability of collision was measured to agree with
    /// a Monte Carlo of the probability with enough hits (two-body encounters, see the conjunction assessment
    /// reference): the flag marks a propagated covariance beyond the domain where Foster was validated, not a measured
    /// failure of Foster.
    /// </summary>
    public double NonlinearityIndexThreshold { get; init; } = DefaultNonlinearityIndexThreshold;

    /// <summary>The default threshold of the non-linearity index, set by the measurement of
    /// <c>FosterMonteCarloValidationTests</c>.</summary>
    internal const double DefaultNonlinearityIndexThreshold = 0.75;
}
