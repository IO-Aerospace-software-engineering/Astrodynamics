using System;
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
        /// </summary>
        /// <value>
        /// The ThrustWindow instance.
        /// </value>
        public Window? ThrustWindow { get; internal set; }

        /// <summary>
        /// Gets or sets the ManeuverWindow object that represents the window for controlling maneuvering tasks.
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
        /// It is the later of the minimum epoch the maneuver was given (its constructor, raised by
        /// <see cref="Spacecraft.SetStandbyManeuver"/> with a minimum epoch) and the gate of the current propagation, the end
        /// of the window of the previous maneuver of the chain, which the chain sets when that maneuver executes. The gate
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

        public Maneuver SetNextManeuver(Maneuver maneuver)
        {
            NextManeuver = maneuver;
            return maneuver;
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
        /// Clears what a propagation set on this maneuver and the next ones of the chain: the fuel burned, the windows and
        /// the gate of the propagation, so that <see cref="MinimumEpoch"/> is again the minimum epoch the maneuver was given
        /// (#573). Called by <c>Spacecraft.ResetPropagation</c> at the start of every <c>Spacecraft.Propagate</c>, after
        /// the propagator is built.
        /// </summary>
        internal virtual void Reset()
        {
            FuelBurned = 0.0;
            ManeuverWindow = ThrustWindow = null;
            PropagationGate = null;
            NextManeuver?.Reset();
        }
    }
}