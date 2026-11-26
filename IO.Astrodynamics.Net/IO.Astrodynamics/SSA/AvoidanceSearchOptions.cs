// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Maneuver;

namespace IO.Astrodynamics.SSA;

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

    /// <summary>
    /// Gets candidate lead times between the burn epoch and the analyzed TCA. Default: 1 h, 3 h and 6 h.
    /// </summary>
    public IReadOnlyList<TimeSpan> LeadTimes { get; init; } = DefaultLeadTimes;

    /// <summary>
    /// Gets candidate maneuver magnitudes to test in each sampled burn direction. Default: 0.05, 0.10 and 0.25 m/s.
    /// </summary>
    public IReadOnlyList<double> DeltaVMagnitudesMetersPerSecond { get; init; } = DefaultDeltaVMagnitudes;

    /// <summary>
    /// The settings of the re-analysis of each option (structuring choice 2 of #668, decided by S. Guillet, 2026-11-19);
    /// null (the default) for those the encounter was analyzed with (#683), the defaults of
    /// <see cref="ConjunctionAnalysisOptions"/> for an encounter built by hand.
    /// </summary>
    /// <remarks>
    /// A record given replaces the encounter's whole: start from the encounter's settings to change one value. Its
    /// <see cref="ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters"/> follows the rule of <c>Analyze</c>: a value
    /// that differs from the radius a secondary spacecraft gives throws <see cref="ArgumentException"/> naming
    /// <c>options</c>. The reads of the range rate over the window and the non-linearity index are not computed for the
    /// options (<see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/> and
    /// <see cref="ConjunctionAnalysisOptions.ComputeNonlinearityIndex"/> cleared), and the propagation of the covariance
    /// is set by the path of the options. An encounter of <see cref="ConjunctionAssessment.Screen"/> was analyzed with
    /// the settings of the screening (a sample step of 5 min by default). Before 11.0.0 the avoidance took
    /// <c>AnalysisSampleStep</c>, <c>MaximumEventSearchStep</c>, <c>SecondaryHardBodyRadiusMeters</c> and
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
