using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    /// <summary>
    /// An impulse at perigee that moves the apogee, up or down, to a target radius from the centre of the maneuver centre.
    /// </summary>
    public class ApogeeHeightManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The apogee radius after the burn, in m from the centre of the maneuver centre (not an altitude).
        /// </summary>
        public double TargetApogee { get; }

        /// <summary>
        /// Creates the maneuver that moves the apogee to that of <paramref name="targetOrbit"/>, about its observer.
        /// </summary>
        /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
        /// <param name="maneuverHoldDuration">Duration to hold after the maneuver.</param>
        /// <param name="targetOrbit">The orbit whose apogee radius is the target; its observer is the maneuver centre.</param>
        /// <param name="engine">Engine used for the maneuver.</param>
        public ApogeeHeightManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : this(
            targetOrbit.Observer as CelestialItem, minimumEpoch, maneuverHoldDuration, targetOrbit.ApogeeVector().Magnitude(), engine)
        {
        }

        /// <summary>
        /// Creates the maneuver that moves the apogee to <paramref name="apogeeRadius"/>.
        /// </summary>
        /// <param name="maneuverCenter">The body the orbit is about.</param>
        /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
        /// <param name="maneuverHoldDuration">Duration to hold after the maneuver.</param>
        /// <param name="apogeeRadius">The apogee radius after the burn, in m from the centre of
        /// <paramref name="maneuverCenter"/>, not an altitude: for an altitude h, its equatorial radius + h.</param>
        /// <param name="engine">Engine used for the maneuver.</param>
        public ApogeeHeightManeuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, double apogeeRadius, Engine engine) : base(maneuverCenter,
            minimumEpoch,
            maneuverHoldDuration, engine)
        {
            TargetApogee = apogeeRadius;
        }

        /// <summary>
        /// Fires at perigee: r·v = 0, transitioning from negative to positive.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;
        public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

        protected override Vector3 Execute(StateVector stateVector)
        {
            var perigee = stateVector.PerigeeVector().Magnitude();
            double vInit = stateVector.Velocity.Magnitude();
            double vFinal = System.Math.Sqrt(stateVector.Observer.GM * ((2.0 / perigee) - (1.0 / ((perigee + TargetApogee) / 2.0))));
            return stateVector.Velocity.Normalize() * (vFinal - vInit);
        }
    }
}