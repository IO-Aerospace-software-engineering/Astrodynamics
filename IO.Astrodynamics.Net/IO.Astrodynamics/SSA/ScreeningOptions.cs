// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures candidate screening for one protected spacecraft against a set of secondaries, read by
/// <see cref="ConjunctionAssessment.Screen"/>.
/// </summary>
public sealed record ScreeningOptions
{
    /// <summary>The default settings, those of <c>new ScreeningOptions()</c>.</summary>
    public static ScreeningOptions Default { get; } = new();

    /// <summary>
    /// The settings of the screening's analysis of each candidate (structuring choice 2 of #668, decided by S. Guillet,
    /// 2026-11-19), honoured as given; null (the default) for the analysis's defaults (<see cref="ConjunctionAnalysisOptions.Default"/>)
    /// with a sample step of 5 min and the covariance held in RTN (<see cref="CovariancePropagationOptions.None"/>), the
    /// screening of 10.x bit for bit.
    /// </summary>
    /// <remarks>
    /// Its <see cref="ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters"/> is a fallback over the catalogue: a
    /// candidate <see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft"/> with a
    /// <see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft.HardBodyRadius"/> greater than 0 keeps its own, as a
    /// <see cref="IO.Astrodynamics.Body.TrackedObject"/> with one (R19 of #668), a candidate
    /// that gives none takes it, or 5 m when it is null (decided by S. Guillet, 2026-11-14, on #683). The analysis of each
    /// candidate carries its resolved radius, and <see cref="ConjunctionAssessment.EvaluateAvoidance"/> re-analyzes with it.
    /// A record that propagates the covariance (<see cref="ConjunctionAnalysisOptions.Default"/>) gives each candidate the
    /// Pc of <c>Analyze</c>, at its cost. Before 11.0.0 the screening took <c>SampleStep</c>, <c>MaxEventSearchStep</c>,
    /// <c>DefaultSecondaryHardBodyRadiusMeters</c> and <c>LowRelativeSpeedThresholdMetersPerSecond</c> of its own.
    /// </remarks>
    public ConjunctionAnalysisOptions Analysis { get; init; }

    /// <summary>
    /// Gets the maximum miss distance allowed for a candidate to remain in the ranked results.
    /// </summary>
    public double MaxMissDistanceMeters { get; init; } = 25_000.0;

    /// <summary>
    /// Gets the maximum number of ranked encounters returned by <see cref="ConjunctionAssessment.Screen"/>.
    /// </summary>
    public int MaxResults { get; init; } = 25;

    /// <summary>
    /// The analysis of a screening given no <see cref="Analysis"/>: <see cref="ConjunctionAnalysisOptions.Default"/> with a
    /// sample step of 5 min and the covariance held in RTN; a screening ranks many candidates.
    /// </summary>
    internal static ConjunctionAnalysisOptions ScreeningDefault { get; } = ConjunctionAnalysisOptions.Default with
    {
        SampleStep = TimeSpan.FromMinutes(5),
        CovariancePropagation = CovariancePropagationOptions.None
    };
}
