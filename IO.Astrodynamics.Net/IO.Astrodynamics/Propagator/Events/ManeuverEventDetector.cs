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
/// <para>
/// <see cref="HandleEvent"/> executes the impulse only; the propagation lays the rest of the maneuver chain on its attitude
/// timeline and builds the detector of the next impulse (#539).
/// </para>
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

    /// <summary>
    /// Executes the impulse at <paramref name="stateAtEvent"/> when its preconditions hold
    /// (<see cref="ImpulseManeuver.CheckPreconditions"/>), and only the impulse: the maneuvers chained after it are
    /// activated by the propagation, on its attitude timeline, which builds the detector of the next impulse (#539).
    /// </summary>
    /// <param name="stateAtEvent">The state at the event. It is left unchanged (#588): the state after the impulse is
    /// <see cref="EventResult.ModifiedState"/>.</param>
    /// <param name="spacecraft">The spacecraft of the maneuver.</param>
    /// <returns>
    /// When the preconditions hold: the state after the impulse, a new state (the velocity plus ΔV), its attitude (the
    /// front along ΔV), the action <see cref="EventAction.StopAndRestart"/> and no next detector. Otherwise:
    /// <paramref name="stateAtEvent"/>, no attitude, the action <see cref="EventAction.Continue"/> and this detector,
    /// which waits for the next crossing.
    /// </returns>
    /// <remarks>
    /// Before 11.0.0 it also executed the attitudes chained after the impulse at the epoch of its event, each raising the
    /// gate of the next maneuver, and returned the detector of the next impulse. The propagation now activates them at the
    /// end of the impulse's maneuver window (#539, docs/reference/attitudes.md).
    /// </remarks>
    public EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft)
    {
        // Check optional preconditions (e.g., CombinedManeuver's apogee-node alignment)
        if (!_maneuver.CheckPreconditions(Local(stateAtEvent)))
        {
            // Skip this occurrence, wait for next crossing
            return new EventResult(stateAtEvent, null, EventAction.Continue, this);
        }

        var (sv, so) = _maneuver.TryExecute(stateAtEvent);
        return new EventResult(sv, so, EventAction.StopAndRestart, null);
    }
}
