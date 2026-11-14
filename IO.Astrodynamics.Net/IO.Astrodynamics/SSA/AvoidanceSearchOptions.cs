// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Maneuver;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Configures the impulsive avoidance trade study performed for one encounter.
/// </summary>
public sealed record AvoidanceSearchOptions
{
    /// <summary>
    /// Gets candidate lead times between the burn epoch and the analyzed TCA.
    /// </summary>
    public IReadOnlyList<TimeSpan> LeadTimes { get; init; } = new[]
    {
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6)
    };

    /// <summary>
    /// Gets candidate maneuver magnitudes to test in each sampled burn direction.
    /// </summary>
    public IReadOnlyList<double> DeltaVMagnitudesMetersPerSecond { get; init; } = new[]
    {
        0.05,
        0.10,
        0.25
    };

    /// <summary>
    /// Gets the coarse analysis cadence used when reassessing each post-maneuver trajectory; null (the default) for the
    /// <see cref="ConjunctionAnalysisOptions.SampleStep"/> the encounter was analyzed with (#683).
    /// </summary>
    public TimeSpan? AnalysisSampleStep { get; init; }

    /// <summary>
    /// Gets the largest interval that SSA is allowed to subdivide internally while refining post-maneuver event detection:
    /// the largest interval between the search epochs of the sampled search, which the reassessment of each option uses
    /// (it reads the participants as <see cref="IO.Astrodynamics.Body.ILocalizable"/>, never by the polynomial search of
    /// two propagations); null (the default) for the <see cref="ConjunctionAnalysisOptions.MaximumEventSearchStep"/> the
    /// encounter was analyzed with (#683).
    /// </summary>
    public TimeSpan? MaximumEventSearchStep { get; init; }

    /// <summary>
    /// Gets the hard-body radius of the secondary object, in m, during the post-maneuver reassessment; null (the default)
    /// for the radius the encounter was analyzed with (#683).
    /// </summary>
    /// <remarks>
    /// The rule of <see cref="ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters"/>: a value that differs from the
    /// radius a secondary spacecraft gives throws <see cref="ArgumentException"/>, and a value that is not finite or is
    /// negative throws <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    public double? SecondaryHardBodyRadiusMeters { get; init; }

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
    /// Gets the relative-speed threshold below which a post-maneuver encounter is flagged as low velocity; null (the
    /// default) for the <see cref="ConjunctionAnalysisOptions.LowRelativeSpeedThresholdMetersPerSecond"/> the encounter was
    /// analyzed with (#683).
    /// </summary>
    /// <remarks>
    /// The other settings of the reassessment are those the encounter was analyzed with:
    /// <see cref="ConjunctionAnalysisOptions.StaleCovarianceThreshold"/>,
    /// <see cref="ConjunctionAnalysisOptions.BisectionToleranceSeconds"/> and
    /// <see cref="ConjunctionAnalysisOptions.NonlinearityIndexThreshold"/> (before 11.0.0, their defaults). An encounter of
    /// <see cref="ConjunctionAssessment.Screen"/> was analyzed with the settings of the screening (a sample step of 5 min by
    /// default); an encounter built by hand with the defaults of <see cref="ConjunctionAnalysisOptions"/>.
    /// </remarks>
    public double? LowRelativeSpeedThresholdMetersPerSecond { get; init; }

    /// <summary>
    /// Gets the execution error of the avoidance burns, by the model of Gates (<see cref="ManeuverExecutionError"/>);
    /// null (the default) for none. When the covariance of the protected participant was propagated
    /// (<see cref="CovarianceSource.Propagated"/>), its covariance B L_e Bᵀ for the ΔV of each option is added to the
    /// velocity block of the covariance at the burn, and the option is propagated from there; otherwise it is not used.
    /// </summary>
    public ManeuverExecutionError ExecutionError { get; init; }
}
