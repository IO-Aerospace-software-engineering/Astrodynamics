// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures candidate screening for one protected spacecraft against a set of secondaries.
/// </summary>
public sealed record ScreeningOptions
{
    /// <summary>
    /// Gets the coarse search cadence used for each candidate pair during screening.
    /// </summary>
    public TimeSpan SampleStep { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the largest interval that SSA is allowed to subdivide internally while refining event detection: the largest
    /// interval between the search epochs of the sampled search, which the screening uses (it reads the candidates as
    /// <see cref="IO.Astrodynamics.Body.ILocalizable"/>, never by the polynomial search of two propagations).
    /// </summary>
    public TimeSpan MaximumEventSearchStep { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the maximum miss distance allowed for a candidate to remain in the ranked results.
    /// </summary>
    public double MaxMissDistanceMeters { get; init; } = 25_000.0;

    /// <summary>
    /// Gets the maximum number of ranked encounters returned by <see cref="ConjunctionAssessment.Screen"/>.
    /// </summary>
    public int MaxResults { get; init; } = 25;

    /// <summary>
    /// Gets the hard-body radius, in m, of the candidates that do not give one: a candidate
    /// <see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft"/> with a <see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft.HardBodyRadius"/>
    /// greater than 0 gives its own (#683). Default: null, 5 m for a candidate that gives none.
    /// </summary>
    /// <remarks>
    /// The rule of <see cref="ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters"/>, applied to every candidate
    /// before the first analysis: a value that differs from the radius a candidate spacecraft gives throws
    /// <see cref="ArgumentException"/> naming that candidate, and a value that is not finite or is negative throws
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    public double? DefaultSecondaryHardBodyRadiusMeters { get; init; }

    /// <summary>
    /// Gets the relative-speed threshold below which an encounter is flagged as a low-velocity case.
    /// </summary>
    public double LowRelativeSpeedThresholdMetersPerSecond { get; init; } = 1.0;
}
