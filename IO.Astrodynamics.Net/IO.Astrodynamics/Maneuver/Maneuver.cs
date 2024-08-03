using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;

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
        public DateTime MinimumEpoch { get; internal set; }

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
        /// Gets or sets the next maneuver.
        /// </summary>
        public Maneuver NextManeuver { get; protected set; }


        /// <summary>
        /// Gets or sets the amount of fuel burned.
        /// </summary>
        public double FuelBurned { get; internal set; }

        private bool IsInbound { get; set; }


        protected Maneuver(CelestialItem maneuverCenter, DateTime minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine)
        {
            ManeuverCenter = maneuverCenter;
            MinimumEpoch = minimumEpoch;
            ManeuverHoldDuration = maneuverHoldDuration;
            Engine = engine;
        }

        public Maneuver SetNextManeuver(Maneuver maneuver)
        {
            NextManeuver = maneuver;
            return maneuver;
        }

        public virtual bool CanExecute(StateVector stateVector)
        {
            var localSv = stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();
            //Evaluate Epoch constraint
            if (localSv.Epoch < MinimumEpoch)
            {
                return false;
            }

            
            //Compute the target point
            var maneuverPoint = ComputeManeuverPoint(localSv);
            if (localSv.Position == maneuverPoint)
            {
                return true;
            }

            //Check if target point is reached
            var isInbound = localSv.Position.Angle(maneuverPoint, localSv.SpecificAngularMomentum()) > 0.0;
            if (isInbound == IsInbound)
            {
                return false;
            }

            IsInbound = isInbound;

            return isInbound == false;
        }

        protected abstract Vector3 ComputeManeuverPoint(StateVector stateVector);
        protected abstract Vector3 Execute(StateVector vector);
        public abstract (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector);

        internal virtual void Reset()
        {
            IsInbound = false;
            FuelBurned = 0.0;
            ManeuverWindow = ThrustWindow = null;
            NextManeuver?.Reset();
        }
    }
}