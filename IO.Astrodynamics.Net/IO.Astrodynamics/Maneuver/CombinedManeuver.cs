using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver
{
    public class CombinedManeuver : ImpulseManeuver
    {
        public double TargetPerigeeHeight { get; } = double.NaN;
        public double TargetInclination { get; } = double.NaN;

        public CombinedManeuver(CelestialItem maneuverCenter, DateTime minimumEpoch, TimeSpan maneuverHoldDuration, double perigeeRadius, double inclination, Engine engine) : base(
            maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
        {
            TargetPerigeeHeight = perigeeRadius;
            TargetInclination = inclination;
        }

        public override bool CanExecute(StateVector stateVector)
        {
            return System.Math.Abs(stateVector.ApogeeVector().Normalize() * stateVector.AscendingNodeVector().Normalize()) > 0.9 && base.CanExecute(stateVector);
        }

        protected override Vector3 ComputeManeuverPoint(StateVector stateVector)
        {
            return stateVector.ApogeeVector();
        }

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
                stateVector.Observer, stateVector.Epoch, stateVector.Frame);

            return targetOrbit.ToStateVector().Velocity - stateVector.Velocity;
        }
    }
}