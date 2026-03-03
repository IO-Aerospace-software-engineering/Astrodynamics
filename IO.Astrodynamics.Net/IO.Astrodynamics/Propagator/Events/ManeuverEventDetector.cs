// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Events;

/// <summary>
/// Wraps an ImpulseManeuver for event-driven triggering using industry-standard g-functions.
/// </summary>
public class ManeuverEventDetector : IEventDetector
{
    private readonly ImpulseManeuver _maneuver;

    public ManeuverEventDetector(ImpulseManeuver maneuver)
    {
        _maneuver = maneuver ?? throw new System.ArgumentNullException(nameof(maneuver));
    }

    public CrossingDirection Direction => _maneuver.EventCrossingDirection;

    public bool IsActive => true;

    public double Evaluate(StateVector state)
    {
        // Guard: before MinimumEpoch, return positive value (no crossing)
        if (state.Epoch < _maneuver.MinimumEpoch) return 1.0;

        // Transform to maneuver-center-relative coordinates
        var localSv = state.RelativeTo(_maneuver.ManeuverCenter, Aberration.None).ToStateVector();
        return _maneuver.ComputeEventValue(localSv);
    }

    public EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft)
    {
        // Check optional preconditions (e.g., CombinedManeuver's apogee-node alignment)
        var localSv = stateAtEvent.RelativeTo(_maneuver.ManeuverCenter, Aberration.None).ToStateVector();
        if (!_maneuver.CheckPreconditions(localSv))
        {
            // Skip this occurrence, wait for next crossing
            return new EventResult(stateAtEvent, null, EventAction.Continue, this);
        }

        var (sv, so) = _maneuver.TryExecute(stateAtEvent);

        // Walk the maneuver chain to find the next impulse maneuver
        // (attitude maneuvers are processed immediately by the propagation loop)
        var nextImpulse = FindNextImpulseManeuver(_maneuver.NextManeuver, sv, spacecraft);
        var nextDetector = nextImpulse != null ? new ManeuverEventDetector(nextImpulse) : null;

        return new EventResult(sv, so, EventAction.StopAndRestart, nextDetector);
    }

    /// <summary>
    /// Process chained attitude maneuvers immediately and return the next impulse maneuver (if any).
    /// Each attitude maneuver's epoch is advanced by the previous one's hold duration to maintain sequential timing.
    /// </summary>
    private static ImpulseManeuver FindNextImpulseManeuver(Maneuver.Maneuver next, StateVector state, Spacecraft spacecraft)
    {
        var currentState = state;
        while (next != null)
        {
            if (next is ImpulseManeuver impulse)
                return impulse;

            if (next is Attitude attitude)
            {
                // Process attitude maneuver at the current (advanced) epoch
                var (_, so) = attitude.TryExecute(currentState);
                if (so.Rotation != Math.Quaternion.Zero)
                {
                    spacecraft.Frame.AddStateOrientationToICRF(so);
                }

                // Track the attitude maneuver as executed (adds to _executedManeuvers)
                spacecraft.SetStandbyManeuver(attitude.NextManeuver,
                    attitude.ManeuverWindow?.EndDate);

                // Advance epoch by ManeuverHoldDuration for next maneuver in chain
                if (attitude.ManeuverWindow.HasValue)
                {
                    var nextEpoch = attitude.ManeuverWindow.Value.EndDate;
                    currentState = new StateVector(currentState.Position, currentState.Velocity,
                        currentState.Observer, nextEpoch, currentState.Frame);
                }

                next = attitude.NextManeuver;
            }
            else
            {
                break;
            }
        }

        return null;
    }
}
