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
        /// Gets or sets the ThrustWindow instance.
        /// For an <see cref="ImpulseManeuver"/>, this is the window of the equivalent finite burn: it lasts the burn duration
        /// given by the rocket equation and starts two thirds of that duration before the impulse epoch
        /// (<c>[t - 0.666 dt, t + 0.334 dt]</c>). The propagated state receives the full delta-V at the impulse epoch;
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
        /// Gets or sets the ManeuverWindow object that represents the window for controlling maneuvering tasks.
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
        /// It is the later of the minimum epoch the maneuver was given (its constructor, raised by
        /// <see cref="Spacecraft.SetStandbyManeuver"/> with a minimum epoch) and the gate of the current propagation, the end
        /// of the window of the previous maneuver of the chain, which the chain sets when that maneuver executes (an impulse
        /// or an attitude, leading or chained: the last leading attitude gates the first impulse). The gate
        /// belongs to one propagation (#573): it holds after the propagation, with the windows of the maneuver, and the next
        /// <c>Spacecraft.Propagate</c> (and <c>Scenario.SimulateAsync</c> and <c>BatchPropagator</c> through it) clears it
        /// before it runs. A spacecraft propagated again, from another state or over another window, then gates its maneuvers
        /// as a spacecraft never propagated.
        /// </para>
        /// </remarks>
        /// <value>
        /// The minimum epoch value.
        /// </value>
        public Time MinimumEpoch => PropagationGate > GivenMinimumEpoch ? PropagationGate.Value : GivenMinimumEpoch;

        /// <summary>
        /// The minimum epoch the maneuver was given: that of its constructor, raised by
        /// <see cref="Spacecraft.SetStandbyManeuver"/> with a later minimum epoch. A propagation never changes it, and every
        /// propagation keeps it (#573).
        /// </summary>
        internal Time GivenMinimumEpoch { get; private set; }

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
        public TimeSpan ManeuverHoldDuration { get; protected set; }

        public Engine Engine { get; }

        /// <summary>
        /// Gets the body front vector from the spacecraft instance (if available),
        /// falling back to the static Spacecraft.Front default.
        /// </summary>
        protected Vector3 GetBodyFront() => Engine.FuelTank.Spacecraft?.BodyFront ?? Spacecraft.Front;

        /// <summary>
        /// Gets or sets the next maneuver.
        /// </summary>
        public Maneuver NextManeuver { get; protected set; }


        /// <summary>
        /// Gets or sets the amount of fuel burned.
        /// </summary>
        public double FuelBurned { get; internal set; }

        protected Maneuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine)
        {
            ManeuverCenter = maneuverCenter;
            GivenMinimumEpoch = minimumEpoch;
            ManeuverHoldDuration = maneuverHoldDuration;
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>
        /// Chains <paramref name="maneuver"/> after this maneuver. A chain is a list: a maneuver appears in it once, and a
        /// repeated burn is a new maneuver (#585).
        /// </summary>
        /// <param name="maneuver">The next maneuver; null to end the chain here.</param>
        /// <returns><paramref name="maneuver"/>, to chain the next one.</returns>
        /// <exception cref="ArgumentException">The chain from <paramref name="maneuver"/> holds this maneuver (the
        /// maneuver itself, or one before it in the chain), or already holds a cycle: the chain would never end. The chain
        /// is left as it was.</exception>
        public Maneuver SetNextManeuver(Maneuver maneuver)
        {
            if (maneuver != null)
            {
                var visited = new HashSet<Maneuver>(ReferenceEqualityComparer.Instance) { this };
                for (var next = maneuver; next != null; next = next.NextManeuver)
                {
                    if (!visited.Add(next))
                    {
                        throw new ArgumentException(
                            $"Chaining this {maneuver.GetType().Name} after this {GetType().Name} would make the maneuver chain a cycle: " +
                            $"the {next.GetType().Name} appears in it twice. A maneuver appears in a chain once; a repeated burn is a new maneuver.",
                            nameof(maneuver));
                    }
                }
            }

            NextManeuver = maneuver;
            return maneuver;
        }

        /// <summary>
        /// The maneuvers of the chain from <paramref name="head"/>, in order, each once.
        /// </summary>
        /// <param name="head">The first maneuver; null for none.</param>
        /// <returns>The maneuvers of the chain.</returns>
        /// <exception cref="InvalidOperationException">The chain is a cycle: a maneuver appears in it twice, which
        /// <see cref="SetNextManeuver"/> rejects but a subclass that sets <see cref="NextManeuver"/> can build (#585).</exception>
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

        protected abstract Vector3 Execute(StateVector vector);
        public abstract (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector);

        /// <summary>
        /// Raises the minimum epoch the maneuver was given to <paramref name="minimumEpoch"/> when it is later, never lowers
        /// it; <see cref="Reset"/> keeps it, so every later propagation does, as the one of the constructor (#573).
        /// </summary>
        /// <param name="minimumEpoch">The minimum epoch given by the user, through
        /// <see cref="Spacecraft.SetStandbyManeuver"/>.</param>
        internal void RaiseGivenMinimumEpoch(Time minimumEpoch)
        {
            if (minimumEpoch > GivenMinimumEpoch)
            {
                GivenMinimumEpoch = minimumEpoch;
            }
        }

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
        /// every <c>Spacecraft.Propagate</c>, after the propagator is built (a loop since #585, no longer a recursion).
        /// </summary>
        internal virtual void Reset()
        {
            FuelBurned = 0.0;
            ManeuverWindow = ThrustWindow = null;
            PropagationGate = null;
        }
    }
}