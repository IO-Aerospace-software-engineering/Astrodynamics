using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public class ApogeeHeightManeuver : ImpulseManeuver
    {
        public double TargetApogee { get; }

        public ApogeeHeightManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : this(
            targetOrbit.Observer as CelestialItem, minimumEpoch, maneuverHoldDuration, targetOrbit.ApogeeVector().Magnitude(), engine)
        {
        }

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