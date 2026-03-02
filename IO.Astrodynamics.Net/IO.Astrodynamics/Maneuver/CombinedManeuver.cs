using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public class CombinedManeuver : ImpulseManeuver
    {
        public double TargetPerigeeHeight { get; } = double.NaN;
        public double TargetInclination { get; } = double.NaN;

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