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
/// <para>
/// Internal since 11.0.0 (decision 88 of #668, #802, decided by S. Guillet, 2026-11-23): the propagation builds its
/// detectors from the maneuver plan and takes no user detector, and the public <see cref="HandleEvent"/> executed a
/// maneuver outside a run (fuel burned, <see cref="ImpulseManeuver.DeltaV"/> and the windows rewritten after it). The
/// propagation executes the plan; the previews are <see cref="ImpulseManeuver.ComputeDeltaV"/> and
/// <see cref="ImpulseManeuver.ComputeEventValue"/>.
/// </para>
/// <para>
/// A detector serves the run that built it only (decision 95 of #668, #802, decided by S. Guillet, 2026-11-24): the run
/// disarms every detector it built when it ends, completed, stopped by its token or thrown
/// (<see cref="PropagatorBase.Propagate"/>), and <see cref="Evaluate"/> and <see cref="HandleEvent"/> then throw
/// <see cref="System.InvalidOperationException"/> and change nothing. A custom <see cref="IIntegrator"/> receives the
/// detectors as <see cref="IEventDetector"/> within the run, so decision 88 alone did not close the route: a detector
/// kept past the run burned fuel and rewrote <see cref="ImpulseManeuver.DeltaV"/> and the windows. This holds for the
/// calls that start after the end of the run: a call already in progress when the run ends completes (decision 98 of
/// #668, decided by S. Guillet, 2026-11-24). Only an integrator that keeps a detector past its
/// <see cref="IIntegrator.IntegrateSegment"/> call, against the documented contract, meets the case (12 of 300 runs of a
/// probe whose thread called <see cref="HandleEvent"/> in a loop burned fuel after the run returned, in the third review
/// of L5a); no lock is added to the detector.
/// </para>
/// <para>
/// A detector executes its impulse once at most (decision 97 of #668, #802, decided by S. Guillet, 2026-11-24): when
/// <see cref="HandleEvent"/> has executed the impulse (<see cref="EventAction.StopAndRestart"/>), the detector disarms
/// itself, and a later <see cref="Evaluate"/> or <see cref="HandleEvent"/> throws
/// <see cref="System.InvalidOperationException"/>, saying that it has already executed its impulse, and changes nothing;
/// one that skipped the occurrence (<see cref="EventAction.Continue"/>, the preconditions not met) stays armed. The
/// detector disarms itself as the execution starts, so an execution that throws disarms it too: before, one that threw
/// after the fuel burned (a burn past the range of a <see cref="System.TimeSpan"/>) left it armed, and each later call
/// burned again (three burns in a run of the fourth review of L5a). The
/// propagation never calls a detector after its execution, replacing it with the detector of the next impulse, so the
/// path of a run is unchanged; before, a custom <see cref="IIntegrator"/> that kept the detector of an impulse executed
/// it a second time during the run (6.74 kg burned, <see cref="ImpulseManeuver.DeltaV"/> rewritten, nothing in the
/// executed maneuvers, in the probe of the third review of L5a). The end of the run is named first: a detector that
/// executed its impulse throws the message of a finished run once its run has ended.
/// </para>
/// </remarks>
internal sealed class ManeuverEventDetector : IEventDetector, IGatedEventDetector
{
    private readonly ImpulseManeuver _maneuver;

    // Set once by the run that built the detector, when it ends, and read by any thread a caller's integrator calls from
    private volatile bool _disarmed;

    public ManeuverEventDetector(ImpulseManeuver maneuver)
    {
        _maneuver = maneuver ?? throw new System.ArgumentNullException(nameof(maneuver));
    }

    /// <summary>The maneuver this detector triggers; its DeltaV is set once it has executed.</summary>
    internal ImpulseManeuver Maneuver => _maneuver;

    /// <summary>
    /// Ends the service of this detector: called by the run that built it when it ends, completed, stopped or thrown
    /// (decision 95 of #668, decided by S. Guillet, 2026-11-24). <see cref="Evaluate"/>, <see cref="HandleEvent"/> and
    /// the gated evaluation throw afterwards.
    /// </summary>
    internal void Disarm() => _disarmed = true;

    // Set once by HandleEvent as it starts to execute the impulse (decision 97 of #668)
    private volatile bool _executed;

    // Throws once the run that built this detector has ended (decision 95 of #668), or once the detector has executed
    // its impulse (decision 97 of #668), before anything is read or changed; the end of the run is named first
    private void RequireArmed()
    {
        if (_disarmed)
        {
            throw new System.InvalidOperationException(
                $"{Description()} belongs to a finished run: a maneuver's detector serves the run that built it only, " +
                "and the propagation executes the plan. Propagate again to execute the maneuver; " +
                "ImpulseManeuver.ComputeDeltaV and ComputeEventValue are the previews.");
        }

        if (_executed)
        {
            throw new System.InvalidOperationException(
                $"{Description()} has already executed its impulse (an execution that threw included): a maneuver's " +
                "detector executes its impulse once at most, and the propagation builds the detector of the next impulse of the plan. Propagate again to " +
                "execute the plan again; ImpulseManeuver.ComputeDeltaV and ComputeEventValue are the previews.");
        }
    }

    // The detector and its maneuver, as the exceptions of RequireArmed name them
    private string Description()
    {
        var owner = _maneuver.PlanOwner;
        return $"The event detector of the {_maneuver.GetType().Name} (minimum epoch {_maneuver.MinimumEpoch}, engine " +
               $"{_maneuver.Engine.Name}{(owner != null ? $", in the plan of {owner.Name}" : string.Empty)})";
    }

    public CrossingDirection Direction => _maneuver.EventCrossingDirection;

    public bool IsActive => true;

    /// <summary>
    /// The g-function of the maneuver at <paramref name="state"/>, relative to its <see cref="Maneuver.Maneuver.ManeuverCenter"/>;
    /// +1 before its <see cref="Maneuver.Maneuver.MinimumEpoch"/>. The first evaluation after the minimum epoch locks the
    /// node a <see cref="PlaneAlignmentManeuver"/> waits for, which its public
    /// <see cref="ImpulseManeuver.ComputeEventValue"/> no longer does (decision 50 of #668, decided by S. Guillet,
    /// 2026-11-23).
    /// </summary>
    /// <exception cref="System.InvalidOperationException">The run that built this detector has ended (decision 95 of
    /// #668), or the detector has executed its impulse (decision 97 of #668); nothing changes, the node of a
    /// <see cref="PlaneAlignmentManeuver"/> included.</exception>
    /// <remarks>
    /// The +1 before the minimum epoch is what an integrator that does not know the gate reads, such as a custom
    /// <see cref="IIntegrator"/> given to the propagator: compared with the first g-value after the minimum epoch, it gives
    /// a false crossing for a positive-to-negative maneuver whose g is already negative there, and misses a
    /// negative-to-positive crossing in the step that holds it (#569). <see cref="VVIntegrator"/> and
    /// <see cref="RK78Integrator"/> never compare it with a g-value after the minimum epoch: in the step that holds it, they
    /// replace it with the g-function evaluated there.
    /// </remarks>
    public double Evaluate(StateVector state)
    {
        RequireArmed();

        // Guard: before MinimumEpoch, return positive value (no crossing)
        if (state.Epoch < _maneuver.MinimumEpoch) return 1.0;

        // The g-function of the run: a maneuver whose first evaluation decides which crossing it waits for locks it here
        // (decision 50 of #668: the public ComputeEventValue is pure)
        return _maneuver.ComputeEventValueInRun(Local(state));
    }

    /// <inheritdoc />
    Time IGatedEventDetector.Gate => _maneuver.MinimumEpoch;

    /// <inheritdoc />
    /// <remarks>The g-function of the maneuver from the start of a search
    /// (<see cref="ImpulseManeuver.ComputeEventValueAtSearchStart"/>), whatever the epoch.</remarks>
    double IGatedEventDetector.EvaluateOpen(StateVector state)
    {
        RequireArmed();
        return _maneuver.ComputeEventValueAtSearchStart(Local(state));
    }

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
    /// <exception cref="System.InvalidOperationException">The run that built this detector has ended (decision 95 of
    /// #668), or the detector has already executed its impulse, a later call after a
    /// <see cref="EventAction.StopAndRestart"/> or after a call whose execution threw (decision 97 of #668, decided by
    /// S. Guillet, 2026-11-24); nothing
    /// changes: no fuel burned, <see cref="ImpulseManeuver.DeltaV"/> and the windows untouched.</exception>
    /// <remarks>
    /// Before 11.0.0 it also executed the attitudes chained after the impulse at the epoch of its event, each raising the
    /// gate of the next maneuver, and returned the detector of the next impulse. The propagation now activates them at the
    /// end of the impulse's maneuver window (#539, docs/reference/attitudes.md).
    /// </remarks>
    public EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft)
    {
        RequireArmed();

        // Check optional preconditions (e.g., CombinedManeuver's apogee-node alignment)
        if (!_maneuver.CheckPreconditions(Local(stateAtEvent)))
        {
            // Skip this occurrence, wait for next crossing
            return new EventResult(stateAtEvent, null, EventAction.Continue, this);
        }

        // The detector disarms itself as the execution starts, so that its impulse executes once at most (decision 97 of
        // #668), an execution that throws after the fuel burned included (the fourth review of L5a)
        _executed = true;
        var (sv, so) = _maneuver.TryExecute(stateAtEvent);
        return new EventResult(sv, so, EventAction.StopAndRestart, null);
    }
}
