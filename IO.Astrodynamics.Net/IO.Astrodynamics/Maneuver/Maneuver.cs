using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public abstract class Maneuver
    {
        public CelestialItem ManeuverCenter { get; }
        /// <summary>
        /// The thrust window of the last run that executed this maneuver: the last run's value, cleared by the re-arm of the
        /// next run (decision 52 of #668, decided by S. Guillet, 2026-11-23); null before any run, and after the re-arm until
        /// the run executes it. The stable value of a run is its record, <see cref="Propagator.ExecutedManeuver.ThrustWindow"/>.
        /// For an <see cref="ImpulseManeuver"/>, this is the window of the equivalent constant-thrust burn: it lasts the burn
        /// duration dt given by the rocket equation, and the impulse epoch t is the centroid of its acceleration,
        /// <c>[t - f dt, t + (1 - f) dt]</c> with f = 1/x + 1/ln(1 - x) for the mass fraction burned x (1/2 for a small burn,
        /// 0.509 at x = 0.1, 0.557 at 0.5; #335, derived from S. Guillet's remark of 2026-11-18, decided by S. Guillet, 2026-11-18; a fixed 0.666 before 11.0.0). The
        /// propagated state receives the full delta-V at the impulse epoch;
        /// the window is reported, and the end of <see cref="ManeuverWindow"/> sets the earliest epoch of the next maneuver.
        /// In a propagation about the Earth, both windows count TT seconds from the time argument of the event (#547).
        /// The attitude of the impulse, its front along the delta-V, is in force from the start of this window to the end
        /// of <see cref="ManeuverWindow"/> (#539).
        /// For an <see cref="Attitude"/>, the window of its activation, of zero length.
        /// </summary>
        /// <value>
        /// The ThrustWindow instance.
        /// </value>
        public Window? ThrustWindow { get; internal set; }

        /// <summary>
        /// The maneuver window of the last run that executed this maneuver: the last run's value, cleared by the re-arm of
        /// the next run (decision 52 of #668, decided by S. Guillet, 2026-11-23); null before any run, and after the re-arm
        /// until the run executes it. The stable value of a run is its record,
        /// <see cref="Propagator.ExecutedManeuver.ManeuverWindow"/>.
        /// For an <see cref="ImpulseManeuver"/>, from the start of <see cref="ThrustWindow"/> to the later of the end of its
        /// hold and the end of the thrust window; for an <see cref="Attitude"/>, from its activation to the end of its hold.
        /// Its end is the gate of the next maneuver of the chain. In a propagation about the Earth, its hold counts TT
        /// seconds (#547).
        /// </summary>
        /// <value>
        /// The ManeuverWindow object for controlling maneuvering tasks.
        /// </value>
        public Window? ManeuverWindow { get; internal set; }

        /// <summary>
        /// Gets the minimum epoch value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The minimum epoch value represents the earliest date and time when the maneuver can be executed.
        /// </para>
        /// <para>
        /// For an <see cref="ImpulseManeuver"/>, it is the gate of its <see cref="Propagator.Events.ManeuverEventDetector"/>:
        /// the impulse fires at the first crossing of its g-function in its direction at or after this epoch, searched from
        /// the integrated state there; a crossing before it never counts (#569, docs/reference/impulse-maneuvers.md, "Minimum
        /// Epoch").
        /// </para>
        /// <para>
        /// For an <see cref="Attitude"/>, it is the earliest epoch of its activation: a propagation activates it at this
        /// epoch or at the end of the window of the previous maneuver of the chain, whichever is later (#539,
        /// docs/reference/attitudes.md).
        /// </para>
        /// <para>
        /// It is the later of the minimum epoch the maneuver was given (its constructor's; before 11.0.0
        /// <c>Spacecraft.SetStandbyManeuver(maneuver, minimumEpoch)</c> could raise it, decision 47 of #668) and the gate of
        /// the current propagation, the end
        /// of the window of the previous maneuver of the chain, which the chain sets when that maneuver executes (an impulse
        /// or an attitude, leading or chained: the last leading attitude gates the first impulse). The gate
        /// belongs to one propagation (#573): it holds after the propagation, with the windows of the maneuver, and the next
        /// <c>Spacecraft.Propagate</c> (and <c>Scenario.Simulate</c> and <c>BatchPropagator</c> through it) clears it
        /// before it runs. A spacecraft propagated again, from another state or over another window, then gates its maneuvers
        /// as a spacecraft never propagated.
        /// </para>
        /// </remarks>
        /// <value>
        /// The minimum epoch value.
        /// </value>
        public Time MinimumEpoch => PropagationGate > GivenMinimumEpoch ? PropagationGate.Value : GivenMinimumEpoch;

        /// <summary>
        /// The minimum epoch the maneuver was given, that of its constructor. A propagation never changes it, and every
        /// propagation keeps it (#573). Before 11.0.0 <c>Spacecraft.SetStandbyManeuver(maneuver, minimumEpoch)</c> could
        /// raise it; a later minimum epoch is now a new maneuver in a new plan (decision 47 of #668, decided by S. Guillet,
        /// 2026-11-23).
        /// </summary>
        internal Time GivenMinimumEpoch { get; }

        /// <summary>
        /// The gate of the current propagation: the end of the window of the previous maneuver of the chain, set through
        /// <see cref="Spacecraft.ChainStandbyManeuver"/> when that maneuver executes; null before it, and after
        /// <see cref="Reset"/>, which the next <c>Spacecraft.Propagate</c> calls before it runs (#573).
        /// </summary>
        internal Time? PropagationGate { get; private set; }

        /// <summary>
        /// Gets the duration for which a maneuver should be held.
        /// </summary>
        /// <remarks>
        /// This property represents the duration as a TimeSpan object.
        /// </remarks>
        /// <returns>The maneuver hold duration as a TimeSpan.</returns>
        /// <remarks>
        /// Zero or positive: the constructor and <see cref="PhasingManeuver"/> set it through one check, a negative hold
        /// throwing <see cref="ArgumentOutOfRangeException"/> (R11 and decision 11 of lot L4 of #668, the setter
        /// <c>private protected</c> by decision 16, decided by S. Guillet, 2026-11-22: before 11.0.0 a protected setter
        /// accepted it, and the window it gave the maneuver mid-propagation ended before it started).
        /// A <see cref="PhasingManeuver"/> takes no hold: a run that executes it sets its hold to 0.9 N T₂, the last run's
        /// value, which the re-arm of the next run restores to 0 (decision 51 of #668, decided by S. Guillet, 2026-11-23,
        /// #784).
        /// </remarks>
        public TimeSpan ManeuverHoldDuration
        {
            get => _maneuverHoldDuration;
            private protected set
            {
                if (value < TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("maneuverHoldDuration", value,
                        "The hold of a maneuver cannot be negative.");
                }

                _maneuverHoldDuration = value;
            }
        }

        private TimeSpan _maneuverHoldDuration;

        /// <summary>
        /// The engine of the maneuver, whose fuel tank's spacecraft gives the body axes of its attitude.
        /// </summary>
        /// <remarks>
        /// Decision 48 of #668 is split (decision 93 of #668, decided by S. Guillet, 2026-11-23): the closed hierarchy, the
        /// internal <c>TryExecute</c>, the <c>private protected</c> constructor and <c>Execute</c> on
        /// <see cref="ImpulseManeuver"/> land in lot L5a; <see cref="Engine"/> moves to <see cref="ImpulseManeuver"/> and a
        /// protected <c>GetBodyAxis(BodyAxis)</c> replaces <see cref="GetBodyFront"/> in lot L5b, with R13 and
        /// <c>BodyAxis</c> (decision 69).
        /// </remarks>
        public Engine Engine { get; }

        /// <summary>
        /// Gets the body front vector from the spacecraft instance (if available),
        /// falling back to the static Spacecraft.Front default. Replaced by a protected <c>GetBodyAxis(BodyAxis)</c> in lot
        /// L5b (decisions 48, 69 and 93 of #668).
        /// </summary>
        protected Vector3 GetBodyFront() => GetBodyAxis(static s => s.BodyFront, Spacecraft.Front);

        /// <summary>
        /// Gets a body axis of the spacecraft that carries the engine's tank, read at the call, or
        /// <paramref name="fallback"/> while the tank is on no spacecraft (#677). A propagation checks first that the tank is
        /// the propagated spacecraft's (<see cref="Spacecraft.CheckManeuverEngines"/>, #676), so only a read outside a
        /// propagation meets the fallback.
        /// </summary>
        /// <param name="axis">The axis of the spacecraft instance, such as <c>s =&gt; s.BodyFront</c>.</param>
        /// <param name="fallback">The vector returned while the tank is on no spacecraft, such as <see cref="Spacecraft.Front"/>.</param>
        private protected Vector3 GetBodyAxis(Func<Spacecraft, Vector3> axis, Vector3 fallback)
        {
            var spacecraft = Engine.FuelTank.Spacecraft;
            return spacecraft != null ? axis(spacecraft) : fallback;
        }

        /// <summary>
        /// The next maneuver of the plan this maneuver is in, null at its end or out of any plan. Set by
        /// <see cref="Spacecraft.SetManeuvers"/> only, the one way to link a plan (decisions 45 and 46 of #668, decided by
        /// S. Guillet, 2026-11-23): before 11.0.0 <c>SetNextManeuver</c> and the protected setter of a subclass linked it
        /// too, outside any plan.
        /// </summary>
        public Maneuver NextManeuver { get; internal set; }

        /// <summary>
        /// The spacecraft whose plan holds this maneuver (<see cref="Spacecraft.SetManeuvers"/>), null out of any plan: a
        /// maneuver is in one plan at a time, and the claim is released when its spacecraft sets another plan without it
        /// (decision 46 of #668, decided by S. Guillet, 2026-11-23).
        /// </summary>
        internal Spacecraft PlanOwner { get; set; }

        /// <summary>
        /// The instruments the law of this maneuver points, none for a maneuver that points no instrument: the plan and the
        /// run check that each is an instrument of the spacecraft propagated (#797, decision 66 of #668, decided by
        /// S. Guillet, 2026-11-23).
        /// </summary>
        internal virtual IEnumerable<Instrument> PointedInstruments => Array.Empty<Instrument>();


        /// <summary>
        /// The fuel the last run that executed this maneuver burned for it, kg: the last run's value, cleared by the re-arm of
        /// the next run (decision 52 of #668, decided by S. Guillet, 2026-11-23); 0 before any run, and after the re-arm until
        /// the run executes it, and 0 for an <see cref="Attitude"/>. The stable value of a run is its record,
        /// <see cref="Propagator.ExecutedManeuver.FuelBurned"/>.
        /// </summary>
        public double FuelBurned { get; internal set; }

        /// <summary>
        /// A maneuver about <paramref name="maneuverCenter"/>, at or after <paramref name="minimumEpoch"/>, held for
        /// <paramref name="maneuverHoldDuration"/>, burning on <paramref name="engine"/>.
        /// </summary>
        /// <param name="maneuverCenter">The centre of the maneuver.</param>
        /// <param name="minimumEpoch">The earliest epoch of the maneuver.</param>
        /// <param name="maneuverHoldDuration">The hold, zero or positive.</param>
        /// <param name="engine">The engine.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maneuverHoldDuration"/> is negative (R11 of #668:
        /// before 11.0.0 it was accepted, and the window it gave the maneuver mid-propagation ended before it
        /// started).</exception>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <remarks>
        /// <c>private protected</c> (decision 48 of #668, decided by S. Guillet, 2026-11-23): the kinds of maneuver a
        /// propagation knows are <see cref="ImpulseManeuver"/> and <see cref="Attitude"/>, the bases of a user maneuver;
        /// before 11.0.0 a direct subclass of <see cref="Maneuver"/> compiled.
        /// </remarks>
        private protected Maneuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine)
        {
            ManeuverCenter = maneuverCenter;
            GivenMinimumEpoch = minimumEpoch;
            ManeuverHoldDuration = maneuverHoldDuration;
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>
        /// The state a preview reads (<see cref="ImpulseManeuver.ComputeDeltaV"/>, <see cref="Attitude.ComputeStateOrientation"/>):
        /// <paramref name="stateVector"/> about <see cref="ManeuverCenter"/>, as the execution takes it, in its own frame,
        /// or about its own observer when the maneuver has no centre (decision 50 of #668, decided by S. Guillet,
        /// 2026-11-23: the given centre, or the state's observer, never the last run's).
        /// </summary>
        private protected StateVector PreviewState(StateVector stateVector)
        {
            return ManeuverCenter is null ? stateVector : stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();
        }

        /// <summary>
        /// The maneuvers of the chain from <paramref name="head"/>, in order, each once.
        /// </summary>
        /// <param name="head">The first maneuver; null for none.</param>
        /// <returns>The maneuvers of the chain.</returns>
        /// <exception cref="InvalidOperationException">The chain is a cycle: a maneuver appears in it twice, which
        /// <see cref="Spacecraft.SetManeuvers"/> rejects, kept as a guard of the walks of the chain (#585).</exception>
        internal static List<Maneuver> Chain(Maneuver head)
        {
            var chain = new List<Maneuver>();
            var visited = new HashSet<Maneuver>(ReferenceEqualityComparer.Instance);
            for (var maneuver = head; maneuver != null; maneuver = maneuver.NextManeuver)
            {
                if (!visited.Add(maneuver))
                {
                    throw new InvalidOperationException(
                        $"The maneuver chain is a cycle: the {maneuver.GetType().Name} appears in it twice, and the chain would never end. " +
                        "A maneuver appears in a chain once; a repeated burn is a new maneuver.");
                }

                chain.Add(maneuver);
            }

            return chain;
        }

        /// <summary>
        /// Clears what a propagation set on the maneuvers of the chain from <paramref name="head"/> (<see cref="Reset"/>),
        /// in a loop, so that a long chain does not use the stack (#585).
        /// </summary>
        /// <exception cref="InvalidOperationException">The chain is a cycle (<see cref="Chain"/>); nothing is reset.</exception>
        internal static void ResetChain(Maneuver head)
        {
            foreach (var maneuver in Chain(head))
            {
                maneuver.Reset();
            }
        }

        /// <summary>
        /// Executes the maneuver from <paramref name="stateVector"/> in a propagation: the state after it and its attitude.
        /// Internal since 11.0.0 (decision 48 of #668, decided by S. Guillet, 2026-11-23): called as a preview, it changed
        /// the maneuver and the spacecraft after a run (fuel, windows, the standby chain; #802); the previews are
        /// <see cref="ImpulseManeuver.ComputeDeltaV"/> and <see cref="Attitude.ComputeStateOrientation"/>.
        /// </summary>
        internal abstract (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector);

        /// <summary>
        /// Raises the gate of the current propagation to <paramref name="gate"/> when there is none yet or it is later, never
        /// lowers it; <see cref="Reset"/> clears it (#573).
        /// </summary>
        /// <param name="gate">The end of the window of the previous maneuver of the chain.</param>
        internal void RaisePropagationGate(Time gate)
        {
            if (!PropagationGate.HasValue || gate > PropagationGate.Value)
            {
                PropagationGate = gate;
            }
        }

        /// <summary>
        /// Sets the gate of the current propagation to <paramref name="gate"/>, earlier or later than the one it replaces;
        /// <see cref="Reset"/> clears it (#573). About the Earth, the propagation sets it to the end of the window of the
        /// impulse before, counted in TT seconds from its event, which replaces the end in TDB seconds that
        /// <see cref="ImpulseManeuver.TryExecute"/> raised it to (#547). In a chain, the maneuver before is the only one to
        /// set it, so the replacement loses no other gate.
        /// </summary>
        /// <param name="gate">The end of the window of the previous maneuver of the chain, in TT seconds from its event.</param>
        internal void SetPropagationGate(Time gate)
        {
            PropagationGate = gate;
        }

        /// <summary>
        /// Clears what a propagation set on this maneuver: the fuel burned, the windows and the gate of the propagation, so
        /// that <see cref="MinimumEpoch"/> is again the minimum epoch the maneuver was given (#573). The next maneuvers of
        /// the chain are reset by <see cref="ResetChain"/>, which <c>Spacecraft.ResetPropagation</c> calls at the start of
        /// every propagation, once the checks of <see cref="Propagator.PropagatorBase.Propagate"/> pass, on every path (#583)
        /// (a loop since #585, no longer a recursion).
        /// </summary>
        internal virtual void Reset()
        {
            FuelBurned = 0.0;
            ManeuverWindow = ThrustWindow = null;
            PropagationGate = null;
        }
    }
}