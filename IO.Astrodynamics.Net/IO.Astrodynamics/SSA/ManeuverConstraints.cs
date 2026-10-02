// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Defines the operational maneuver envelope used during avoidance trade studies.
/// </summary>
public sealed record ManeuverConstraints
{
    /// <summary>
    /// Gets the maximum allowable impulsive maneuver magnitude, in meters per second.
    /// </summary>
    public double MaxDeltaVMetersPerSecond { get; init; } = 5.0;

    /// <summary>
    /// Gets the minimum acceptable lead time between the burn and the analyzed TCA.
    /// </summary>
    public TimeSpan MinimumLeadTime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the maximum acceptable lead time between the burn and the analyzed TCA.
    /// </summary>
    public TimeSpan MaximumLeadTime { get; init; } = TimeSpan.FromHours(24);
}
