// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Propagator.Events;

/// <summary>
/// Specifies which direction of g-function zero-crossing triggers an event.
/// </summary>
public enum CrossingDirection
{
    /// <summary>g transitions from negative to positive (e.g., perigee: r·v goes from − to +).</summary>
    NegativeToPositive,

    /// <summary>g transitions from positive to negative (e.g., apogee: r·v goes from + to −).</summary>
    PositiveToNegative,

    /// <summary>Either direction triggers the event.</summary>
    Any
}
