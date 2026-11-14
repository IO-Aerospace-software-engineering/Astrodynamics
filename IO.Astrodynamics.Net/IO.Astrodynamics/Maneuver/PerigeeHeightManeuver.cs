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
    /// An impulse at apogee that moves the perigee, up or down, to a target radius from the centre of the maneuver centre.
    /// </summary>
    public class PerigeeHeightManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The perigee radius after the burn, in m from the centre of the maneuver centre (not an altitude).
        /// </summary>
        public double TargetPerigeeHeight { get; } = double.NaN;

        /// <summary>
        /// Creates the maneuver that moves the perigee to that of <paramref name="targetOrbit"/>, about its observer.
        /// </summary>
        /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
        /// <param name="maneuverHoldDuration">Duration to hold after the maneuver.</param>
        /// <param name="targetOrbit">The orbit whose perigee radius is the target; its observer is the maneuver centre.</param>
        /// <param name="engine">Engine used for the maneuver.</param>
        public PerigeeHeightManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : this(
            targetOrbit.Observer as CelestialItem, minimumEpoch, maneuverHoldDuration, targetOrbit.PerigeeVector().Magnitude(), engine)
        {
        }

        /// <summary>
        /// Creates the maneuver that moves the perigee to <paramref name="perigeeRadius"/>.
        /// </summary>
        /// <param name="maneuverCenter">The body the orbit is about.</param>
        /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
        /// <param name="maneuverHoldDuration">Duration to hold after the maneuver.</param>
        /// <param name="perigeeRadius">The perigee radius after the burn, in m from the centre of
        /// <paramref name="maneuverCenter"/>, not an altitude: for an altitude h, its equatorial radius + h.</param>
        /// <param name="engine">Engine used for the maneuver.</param>
        public PerigeeHeightManeuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, double perigeeRadius, Engine engine) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration,
            engine)
        {
            TargetPerigeeHeight = perigeeRadius;
        }

        /// <summary>
        /// Fires at apogee: r·v = 0, transitioning from positive to negative.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;
        public override CrossingDirection EventCrossingDirection => CrossingDirection.PositiveToNegative;

        protected override Vector3 Execute(StateVector stateVector)
        {
            var apogee = stateVector.ApogeeVector().Magnitude();
            double vInit = stateVector.Velocity.Magnitude();
            double vFinal = System.Math.Sqrt(stateVector.Observer.GM * ((2.0 / apogee) - (1.0 / ((apogee + TargetPerigeeHeight) / 2.0))));
            return stateVector.Velocity.Normalize() * (vFinal - vInit);
        }
    }
}