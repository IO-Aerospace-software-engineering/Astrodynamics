// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Events;

/// <summary>
/// Industry-standard event detector using a continuous g-function.
/// A zero-crossing of Evaluate() in the specified Direction triggers the event.
/// </summary>
public interface IEventDetector
{
    /// <summary>
    /// Continuous scalar g-function. A zero-crossing triggers the event.
    /// Must be smooth and suitable for root-finding.
    /// </summary>
    double Evaluate(StateVector state);

    /// <summary>
    /// Which direction of zero-crossing triggers this event.
    /// </summary>
    CrossingDirection Direction { get; }

    /// <summary>
    /// Handle the detected event. Returns modified state and next detector.
    /// </summary>
    EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft);

    /// <summary>
    /// Whether this detector is still active.
    /// </summary>
    bool IsActive { get; }
}

/// <summary>
/// Action to take after an event is handled.
/// </summary>
public enum EventAction
{
    /// <summary>Stop the current segment and restart integration (e.g., impulse maneuver changes velocity).</summary>
    StopAndRestart,

    /// <summary>Continue integration (e.g., attitude-only event with no state change).</summary>
    Continue
}

/// <summary>
/// Result of handling a detected event.
/// </summary>
public record struct EventResult(
    StateVector ModifiedState,
    StateOrientation Orientation,
    EventAction Action,
    IEventDetector NextDetector);
