// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Maneuver;

namespace IO.Astrodynamics.Ssa;

/// <summary>
/// Configures the impulsive avoidance trade study performed for one encounter, read by
/// <see cref="ConjunctionAssessment.EvaluateAvoidance"/>.
/// </summary>
public sealed record AvoidanceSearchOptions
{
    // The default grids, shared read only, so that Default equals new() (D1)
    private static readonly IReadOnlyList<TimeSpan> DefaultLeadTimes = Array.AsReadOnly(new[]
    {
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6)
    });

    private static readonly IReadOnlyList<double> DefaultDeltaVMagnitudes = Array.AsReadOnly(new[]
    {
        0.05,
        0.10,
        0.25
    });

    // After the grids it takes (static initializers run in order)
    /// <summary>The default settings, those of <c>new AvoidanceSearchOptions()</c>.</summary>
    public static AvoidanceSearchOptions Default { get; } = new();

    private readonly IReadOnlyList<TimeSpan> _leadTimes = DefaultLeadTimes;
    private readonly IReadOnlyList<double> _deltaVMagnitudes = DefaultDeltaVMagnitudes;

    /// <summary>
    /// Gets candidate lead times between the burn epoch and the analyzed TCA. Default: 1 h, 3 h and 6 h.
    /// </summary>
    /// <remarks>
    /// The grid is the constraint (R17 of #668, decided by S. Guillet, 2026-11-19): every lead time is evaluated, and one
    /// whose burn falls outside the window is listed in <see cref="AvoidanceStudy.Skipped"/>. Before 11.0.0 the
    /// <c>ManeuverConstraints</c> of the protected spacecraft's profile (10 min to 24 h by default) dropped the others.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value is empty or holds a lead time that is not positive.</exception>
    public IReadOnlyList<TimeSpan> LeadTimes
    {
        get => _leadTimes;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(LeadTimes));
            if (value.Count == 0 || value.Any(lead => lead <= TimeSpan.Zero))
            {
                throw new ArgumentOutOfRangeException(nameof(LeadTimes),
                    "The lead times must be one at least, each positive.");
            }

            _leadTimes = value;
        }
    }

    /// <summary>
    /// Gets candidate maneuver magnitudes to test in each sampled burn direction. Default: 0.05, 0.10 and 0.25 m/s.
    /// </summary>
    /// <remarks>
    /// Every magnitude is evaluated: before 11.0.0 the <c>ManeuverConstraints</c> of the protected spacecraft's profile
    /// (5 m/s by default) dropped those above it, and a magnitude of 0 or less was dropped.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value is empty or holds a magnitude that is not positive and
    /// finite.</exception>
    public IReadOnlyList<double> DeltaVMagnitudesMetersPerSecond
    {
        get => _deltaVMagnitudes;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(DeltaVMagnitudesMetersPerSecond));
            if (value.Count == 0 || value.Any(magnitude => !(magnitude > 0.0) || double.IsInfinity(magnitude)))
            {
                throw new ArgumentOutOfRangeException(nameof(DeltaVMagnitudesMetersPerSecond),
                    "The magnitudes must be one at least, each positive and finite.");
            }

            _deltaVMagnitudes = value;
        }
    }

    /// <summary>
    /// The settings of the re-analysis of each option (structuring choice 2 of #668, decided by S. Guillet, 2026-11-19);
    /// null (the default) for those the encounter was analyzed with (#683).
    /// </summary>
    /// <remarks>
    /// A record given replaces the encounter's whole: start from the encounter's settings to change one value. Its
    /// <see cref="ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters"/> follows the rule of <c>Analyze</c>: a value
    /// that differs from the radius a secondary spacecraft gives throws <see cref="ArgumentException"/> naming
    /// <c>options</c>. The options are analyzed by the sampled search: a record given with
    /// <see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/> true throws <see cref="ArgumentException"/> naming
    /// <c>options</c> (R18 of #668; give <c>encounter.AnalysisOptions with { ReadRangeRateOverWindow = false }</c>), and
    /// only a null <see cref="Analysis"/>, the encounter's settings, has it cleared. The non-linearity index is not
    /// computed for the options (<see cref="ConjunctionAnalysisOptions.ComputeNonlinearityIndex"/> cleared, given or
    /// inherited), and the propagation of the covariance is set by the path of the options. An encounter of <see cref="ConjunctionAssessment.Screen"/> was analyzed with
    /// the settings of the screening (a sample step of 5 min by default). Before 11.0.0 the avoidance took
    /// <c>AnalysisSampleStep</c>, <c>MaxEventSearchStep</c>, <c>SecondaryHardBodyRadiusMeters</c> and
    /// <c>LowRelativeSpeedThresholdMetersPerSecond</c> of its own.
    /// </remarks>
    public ConjunctionAnalysisOptions Analysis { get; init; }

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
    /// Gets the execution error of the avoidance burns, by the model of Gates (<see cref="ManeuverExecutionError"/>);
    /// null (the default) for none. When the covariance of the protected participant was propagated
    /// (<see cref="CovarianceSource.Propagated"/>), its covariance B L_e Bᵀ for the ΔV of each option is added to the
    /// velocity block of the covariance at the burn, and the option is propagated from there; otherwise it is not used.
    /// </summary>
    public ManeuverExecutionError ExecutionError { get; init; }
}
