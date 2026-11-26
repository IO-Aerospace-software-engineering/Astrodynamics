// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.Ssa;

/// <summary>
/// The result of <see cref="ConjunctionAssessment.EvaluateAvoidance"/>: the avoidance options, ranked, and the lead times
/// of the grid that gave none, with the reason (R17 of #668, decided by S. Guillet, 2026-11-19; before 11.0.0 the options
/// alone, a lead time without one dropped without a word).
/// </summary>
public sealed class AvoidanceStudy
{
    internal AvoidanceStudy(IReadOnlyList<AvoidanceOption> options, IReadOnlyList<SkippedLeadTime> skipped)
    {
        Options = options;
        Skipped = skipped;
    }

    /// <summary>
    /// The avoidance options, ordered by their ranking score, lower first, at most
    /// <see cref="AvoidanceSearchOptions.MaxReturnedOptions"/>.
    /// </summary>
    public IReadOnlyList<AvoidanceOption> Options { get; }

    /// <summary>The lead times of the grid that gave no option, in the order of the grid, each with its reason.</summary>
    public IReadOnlyList<SkippedLeadTime> Skipped { get; }
}

/// <summary>A lead time of the avoidance grid that gave no option, and why.</summary>
public sealed class SkippedLeadTime
{
    internal SkippedLeadTime(TimeSpan leadTime, AvoidanceSkipReason reason)
    {
        LeadTime = leadTime;
        Reason = reason;
    }

    /// <summary>The lead time, from the burn to the TCA of the encounter.</summary>
    public TimeSpan LeadTime { get; }

    /// <summary>Why the lead time gave no option.</summary>
    public AvoidanceSkipReason Reason { get; }

    /// <summary>The lead time and its reason, for instance "1.06:00:00: before the window".</summary>
    public override string ToString() => Reason switch
    {
        AvoidanceSkipReason.BeforeWindow => $"{LeadTime}: before the window",
        _ => $"{LeadTime}: outside the trajectory"
    };
}

/// <summary>Why a lead time of the avoidance grid gave no option.</summary>
public enum AvoidanceSkipReason
{
    /// <summary>The burn falls outside the window of the encounter, before its start.</summary>
    BeforeWindow,

    /// <summary>The burn falls outside the propagation of the protected spacecraft the options are propagated on.</summary>
    OutsideTrajectory
}
