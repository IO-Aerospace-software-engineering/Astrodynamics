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
    /// An impulse at apogee that changes the inclination and moves the perigee, up or down, to a target radius from the
    /// centre of the maneuver centre.
    /// </summary>
    public class CombinedManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The perigee radius after the burn, in m from the centre of the maneuver centre (not an altitude).
        /// </summary>
        public double TargetPerigeeHeight { get; } = double.NaN;

        /// <summary>
        /// The inclination after the burn, in radians.
        /// </summary>
        public double TargetInclination { get; } = double.NaN;

        /// <summary>
        /// Creates the maneuver.
        /// </summary>
        /// <param name="maneuverCenter">The body the orbit is about.</param>
        /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
        /// <param name="maneuverHoldDuration">Duration to hold after the maneuver.</param>
        /// <param name="perigeeRadius">The perigee radius after the burn, in m from the centre of
        /// <paramref name="maneuverCenter"/>, not an altitude: for an altitude h, its equatorial radius + h.</param>
        /// <param name="inclination">The inclination after the burn, in radians.</param>
        /// <param name="engine">Engine used for the maneuver.</param>
        public CombinedManeuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, double perigeeRadius, double inclination, Engine engine) : base(
            maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
        {
            TargetPerigeeHeight = perigeeRadius;
            TargetInclination = inclination;
        }

        /// <summary>
        /// Fires at apogee: r·v = 0, transitioning from positive to negative.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;
        public override CrossingDirection EventCrossingDirection => CrossingDirection.PositiveToNegative;

        /// <summary>
        /// Apogee must be nearly aligned with ascending node (dot product > 0.9).
        /// </summary>
        public override bool CheckPreconditions(StateVector state) =>
            System.Math.Abs(state.ApogeeVector().Normalize() * state.AscendingNodeVector().Normalize()) > 0.9;

        protected override Vector3 Execute(StateVector stateVector)
        {
            double e;
            double meanAnomaly = stateVector.MeanAnomaly();
            double periapsisArgument = stateVector.ArgumentOfPeriapsis();
            double apogee = stateVector.ApogeeVector().Magnitude();

            //If target perigee is higher than current apogee
            if (TargetPerigeeHeight > apogee)
            {
                e = 1.0 - (2.0 / ((TargetPerigeeHeight / apogee) + 1.0));

                //Periapse argument will turn by 180°
                meanAnomaly = (meanAnomaly + Constants.PI) % Constants._2PI;
                periapsisArgument += Constants.PI;
            }
            else
            {
                e = 1.0 - (2.0 / ((apogee / TargetPerigeeHeight) + 1.0));
            }

            var targetOrbit = new KeplerianElements((TargetPerigeeHeight + apogee) * 0.5, e, TargetInclination, stateVector.AscendingNode(), periapsisArgument, meanAnomaly,
                stateVector.Observer, stateVector.Epoch, stateVector.Frame, perigeeRadius: stateVector.PerigeeRadius());

            return targetOrbit.ToStateVector().Velocity - stateVector.Velocity;
        }
    }
}