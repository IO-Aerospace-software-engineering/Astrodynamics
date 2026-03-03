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
        /// The minimum epoch value represents the earliest date and time when the maneuver can be executed
        /// </remarks>
        /// <value>
        /// The minimum epoch value.
        /// </value>
        public Time MinimumEpoch { get; internal set; }

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
            MinimumEpoch = minimumEpoch;
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

        internal virtual void Reset()
        {
            FuelBurned = 0.0;
            ManeuverWindow = ThrustWindow = null;
            NextManeuver?.Reset();
        }
    }
}