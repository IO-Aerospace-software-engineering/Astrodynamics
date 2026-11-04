// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.Events;

/// <summary>
/// Wraps an ImpulseManeuver for event-driven triggering using industry-standard g-functions.
/// </summary>
/// <remarks>
/// The maneuver fires at the first crossing of its g-function in its direction at or after its
/// <see cref="Maneuver.Maneuver.MinimumEpoch"/>, searched from the integrated state there (#569). The minimum epoch is the
/// gate of the detector: <see cref="VVIntegrator"/> and <see cref="RK78Integrator"/> evaluate the g-function at it, in the
/// step that holds it, instead of reading the value <see cref="Evaluate"/> returns before it.
/// </remarks>
public class ManeuverEventDetector : IEventDetector, IGatedEventDetector
{
    private readonly ImpulseManeuver _maneuver;

    public ManeuverEventDetector(ImpulseManeuver maneuver)
    {
        _maneuver = maneuver ?? throw new System.ArgumentNullException(nameof(maneuver));
    }

    /// <summary>The maneuver this detector triggers; its DeltaV is set once it has executed.</summary>
    internal ImpulseManeuver Maneuver => _maneuver;

    public CrossingDirection Direction => _maneuver.EventCrossingDirection;

    public bool IsActive => true;

    /// <summary>
    /// The g-function of the maneuver at <paramref name="state"/>, relative to its <see cref="Maneuver.Maneuver.ManeuverCenter"/>;
    /// +1 before its <see cref="Maneuver.Maneuver.MinimumEpoch"/>.
    /// </summary>
    /// <remarks>
    /// The +1 before the minimum epoch is kept for callers outside the library, such as a custom
    /// <see cref="Integrator"/>, which keep the former behaviour: compared with the first g-value after the minimum epoch, it
    /// gives a false crossing for a positive-to-negative maneuver whose g is already negative there, and misses a
    /// negative-to-positive crossing in the step that holds it (#569). <see cref="VVIntegrator"/> and
    /// <see cref="RK78Integrator"/> never compare it with a g-value after the minimum epoch: in the step that holds it, they
    /// replace it with the g-function evaluated there.
    /// </remarks>
    public double Evaluate(StateVector state)
    {
        // Guard: before MinimumEpoch, return positive value (no crossing)
        if (state.Epoch < _maneuver.MinimumEpoch) return 1.0;

        return _maneuver.ComputeEventValue(Local(state));
    }

    /// <inheritdoc />
    Time IGatedEventDetector.Gate => _maneuver.MinimumEpoch;

    /// <inheritdoc />
    /// <remarks>The g-function of the maneuver from the start of a search
    /// (<see cref="ImpulseManeuver.ComputeEventValueAtSearchStart"/>), whatever the epoch.</remarks>
    double IGatedEventDetector.EvaluateOpen(StateVector state) => _maneuver.ComputeEventValueAtSearchStart(Local(state));

    // The state relative to the center of the maneuver, the frame of its g-function
    private StateVector Local(StateVector state) => state.RelativeTo(_maneuver.ManeuverCenter, Aberration.None).ToStateVector();

    public EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft)
    {
        // Check optional preconditions (e.g., CombinedManeuver's apogee-node alignment)
        if (!_maneuver.CheckPreconditions(Local(stateAtEvent)))
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
                // Later in the chain than the impulse at the same epoch, it replaces its attitude
                if (so.Rotation != Math.Quaternion.Zero)
                {
                    spacecraft.Frame.Record(so, null, true);
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
