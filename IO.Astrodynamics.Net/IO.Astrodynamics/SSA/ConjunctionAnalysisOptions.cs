// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures how one conjunction pair is searched and evaluated.
/// </summary>
/// <remarks>
/// Read by <c>ConjunctionAssessment.Analyze</c> and <c>AnalyzeAll</c>, and, contained, by
/// <see cref="ConjunctionAssessment.Screen"/> (<see cref="ScreeningOptions.Analysis"/>) and
/// <see cref="ConjunctionAssessment.EvaluateAvoidance"/> (<see cref="AvoidanceSearchOptions.Analysis"/>), the one record of
/// the settings of an analysis (structuring choice 2 of #668, decided by S. Guillet, 2026-11-19). A value out of its range
/// throws where it is set, at init (D1 of #668; before 11.0.0, at the start of the analysis).
/// </remarks>
public sealed record ConjunctionAnalysisOptions
{
    /// <summary>The default settings, those of <c>new ConjunctionAnalysisOptions()</c>.</summary>
    public static ConjunctionAnalysisOptions Default { get; } = new();

    private readonly TimeSpan _sampleStep = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _maximumEventSearchStep = TimeSpan.FromSeconds(60);
    private readonly double? _secondaryHardBodyRadiusMeters;
    private readonly double _nonlinearityIndexThreshold = DefaultNonlinearityIndexThreshold;

    /// <summary>
    /// Gets the coarse search cadence used when sampling for candidate close approaches. Default: 1 min.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan SampleStep
    {
        get => _sampleStep;
        init => _sampleStep = RequirePositive(value, nameof(SampleStep));
    }

    /// <summary>
    /// Gets the largest interval that SSA is allowed to subdivide internally while refining event detection: on the
    /// sampled search, the largest interval between its search epochs; on the polynomial search of two propagations
    /// (the <see cref="PropagationSolution"/> overloads of <c>Analyze</c> and <c>AnalyzeAll</c>), with
    /// <see cref="ReadRangeRateOverWindow"/> only, the largest spacing of the reads of the range rate over the window,
    /// between and at the candidates of the polynomial search (#628), one tick more allowed (about the Earth a step of
    /// 60 TT seconds may span 60 s and one tick of its TDB labels). Without it the polynomial search does not use it.
    /// </summary>
    /// <remarks>
    /// With <see cref="ReadRangeRateOverWindow"/>, a minimum of the distance on two propagations is listed from these reads
    /// when the range rate changes from negative to positive between two of them; a change of sign over an interval
    /// narrower than this spacing, which no search from a minimum of the polynomial search reaches, is not guaranteed to be
    /// found; a smaller spacing resolves a narrower one. Each read costs a state of each propagation (an RK7(8) step inside
    /// a step), so a larger spacing costs less: the measured costs are in <c>docs/reference/conjunction-assessment.md</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan MaximumEventSearchStep
    {
        get => _maximumEventSearchStep;
        init => _maximumEventSearchStep = RequirePositive(value, nameof(MaximumEventSearchStep));
    }

    /// <summary>
    /// Gets whether the polynomial search of two propagations (the <see cref="PropagationSolution"/> overloads of
    /// <c>Analyze</c> and <c>AnalyzeAll</c>) also reads the range rate of the states over the whole window, at every
    /// candidate of the polynomial search and at most <see cref="MaximumEventSearchStep"/> (and one tick) apart between
    /// them, and bisects each change from negative to positive between two reads (#628). Default: false (decision 54 of
    /// #457, S. Guillet, 2026-11-11).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without it, the minima of the cubic Hermite of the stored steps seed the search, and every close approach listed is
    /// a minimum of the states (a zero of the range rate from negative to positive, inside the window, ordered, no
    /// duplicate), but a minimum that no minimum of the Hermite seeds may be lost: two minima within one step of a slow
    /// co-orbital pass, or a minimum between two candidates where the range rate closes at one and recedes at the next,
    /// with long (300 s and more) or mixed steps. <c>Analyze</c> then returns another minimum, 1.7 mm to 9.6 cm farther
    /// in the measured cases (504.837 m for 504.801 m), or a window end (504.658 m for 504.643 m; up to 1.46 m farther). No
    /// loss was measured on fast passes.
    /// </para>
    /// <para>
    /// With it, those minima are listed, but for a change of sign over an interval narrower than
    /// <see cref="MaximumEventSearchStep"/> that no search from a seed reaches. It costs a state of each propagation per
    /// read: over one day of two LEO propagations at the default steps, the dense <c>Analyze</c> takes about 100 ms
    /// against about 18 ms without it at the default 60 s spacing (see <c>docs/reference/conjunction-assessment.md</c>).
    /// Ask for it on slow co-orbital formations (relative speeds of metres per second or less) propagated with long or
    /// mixed steps. The sampled search (<see cref="IO.Astrodynamics.Body.ILocalizable"/> participants, the screening, the
    /// avoidance options and two propagations that do not share their observer and frame) does not use it: true throws
    /// <see cref="System.ArgumentException"/> naming the options before any search there (R18 of #668, decided by
    /// S. Guillet, 2026-11-19; ignored before 11.0.0), but for the options an avoidance inherits from its encounter, which
    /// clear it.
    /// </para>
    /// </remarks>
    public bool ReadRangeRateOverWindow { get; init; }

    /// <summary>
    /// Gets the hard-body radius of the secondary object, in m, when the secondary does not give one: a secondary
    /// <see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft"/> with a <see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft.HardBodyRadius"/>
    /// greater than 0 gives its own (#683), and so does a <see cref="IO.Astrodynamics.Body.TrackedObject"/> (R19 of #668). Default: null.
    /// </summary>
    /// <remarks>
    /// The radius of the secondary is that of the secondary spacecraft or tracked object when it is greater than 0 and this
    /// value is null or equal to it; this value when the secondary gives none (a radius of 0, or neither); 5 m when neither gives
    /// one. A value that differs from a radius the secondary gives throws <see cref="ArgumentException"/>, and a value that
    /// is not finite or is negative throws <see cref="ArgumentOutOfRangeException"/>, before any search. Before 11.0.0 this
    /// value (5 m by default) was used whatever the radius of the secondary. In
    /// <see cref="ScreeningOptions.Analysis"/> it is the fallback of the candidates that give no radius, and never
    /// conflicts with a candidate's own (decided by S. Guillet, 2026-11-14, on #683).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite or is negative.</exception>
    public double? SecondaryHardBodyRadiusMeters
    {
        get => _secondaryHardBodyRadiusMeters;
        init
        {
            if (value is { } radius)
            {
                Spacecraft.ThrowIfInvalidHardBodyRadius(radius, nameof(SecondaryHardBodyRadiusMeters));
            }

            _secondaryHardBodyRadiusMeters = value;
        }
    }

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
    /// Gets the propagation of the covariance of the participants to the encounter epoch;
    /// <see cref="CovariancePropagationOptions.None"/> holds the covariance of the initial state in RTN
    /// (<see cref="CovarianceSource.InitialStateHeldInRtn"/>) for every participant.
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
    /// <para>
    /// Null throws (R06 of #668, structuring choice 3, decided by S. Guillet, 2026-11-19): before 11.0.0 it held the
    /// covariance in RTN, which <see cref="CovariancePropagationOptions.None"/> does now, while in
    /// <see cref="PropagationOptions.CovariancePropagation"/> a null value is Auto. The default differs from that of
    /// <see cref="PropagationOptions"/> for that reason, a decided exception to D1's sibling rule.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null: <see cref="CovariancePropagationOptions.None"/> holds
    /// the covariance in RTN.</exception>
    /// <exception cref="ArgumentException">The value propagates in another mode than
    /// <see cref="CovariancePropagationMode.Covariance"/>, or sets <see cref="CovariancePropagationOptions.IncludeInStepControl"/>.</exception>
    public CovariancePropagationOptions CovariancePropagation
    {
        get => _covariancePropagation;
        init
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(CovariancePropagation),
                    "CovariancePropagation cannot be null: pass CovariancePropagationOptions.None to hold the covariance of " +
                    "the initial state in RTN (a null value did so before 11.0.0).");
            }

            if (value.IsActive && value.Mode != CovariancePropagationMode.Covariance)
            {
                throw new ArgumentException(
                    "The conjunction analysis propagates the covariance: CovariancePropagation.Mode must be " +
                    $"{nameof(CovariancePropagationMode.Covariance)}, or CovariancePropagation " +
                    "CovariancePropagationOptions.None to hold the initial covariance in RTN.", nameof(CovariancePropagation));
            }

            if (value.IncludeInStepControl)
            {
                throw new ArgumentException(
                    "The conjunction analysis replays the variational equations along the steps of the propagation of each " +
                    "participant, controlled on the state only: IncludeInStepControl cannot be set. Propagate the " +
                    "participant with the covariance and this option instead, and pass its solution.",
                    nameof(CovariancePropagation));
            }

            _covariancePropagation = value;
        }
    }

    private readonly CovariancePropagationOptions _covariancePropagation =
        new() { Mode = CovariancePropagationMode.Covariance };

    /// <summary>
    /// Gets whether the non-linearity index of each propagated covariance is computed at the encounter epoch
    /// (<see cref="ParticipantCovariance.NonlinearityIndex"/>). It costs twelve propagations of the state transition
    /// matrix per participant, from its initial state to the encounter epoch: on two LEO participants over one day,
    /// <see cref="ConjunctionAssessment.Analyze(IO.Astrodynamics.Body.Spacecraft.Spacecraft, IO.Astrodynamics.Body.ILocalizable, TimeSystem.Window, ConjunctionAnalysisOptions)"/>
    /// takes 10.1 s with it against 1.5 s without. Default: false (decision of S. Guillet, 2026-10-18): the Foster
    /// probability was measured to agree with a Monte Carlo far beyond the threshold, so the index is an indicator to ask
    /// for, not a default alarm.
    /// </summary>
    public bool ComputeNonlinearityIndex { get; init; }

    /// <summary>
    /// Gets the non-linearity index above which <see cref="EncounterQualityFlags.NonlinearCovariancePropagation"/> is
    /// raised. Default: 1.0, the largest index, rounded down, at which the Foster probability of collision was measured to agree with
    /// a Monte Carlo of the probability with enough hits (two-body encounters, see the conjunction assessment
    /// reference): the flag marks a propagated covariance beyond the domain where Foster was validated, not a measured
    /// failure of Foster.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public double NonlinearityIndexThreshold
    {
        get => _nonlinearityIndexThreshold;
        init => _nonlinearityIndexThreshold = value > 0.0 && !double.IsInfinity(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(NonlinearityIndexThreshold), value,
                "The non-linearity index threshold must be positive and finite.");
    }

    /// <summary>The default threshold of the non-linearity index, set by the measurement of
    /// <c>FosterMonteCarloValidationTests</c>.</summary>
    internal const double DefaultNonlinearityIndexThreshold = 1.0;

    private static TimeSpan RequirePositive(TimeSpan value, string property) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(property, value, $"{property} must be positive.");
}
