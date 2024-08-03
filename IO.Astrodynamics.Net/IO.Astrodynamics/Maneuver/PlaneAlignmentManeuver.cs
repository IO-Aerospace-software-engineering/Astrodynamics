using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver
{
    public class PlaneAlignmentManeuver : ImpulseManeuver
    {
        public bool? IsAscendingNode { get; private set; }
        public double RelativeInclination { get; private set; }

        public PlaneAlignmentManeuver(DateTime minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit,
            Engine engine) : base(minimumEpoch, maneuverHoldDuration, targetOrbit, engine)
        {
        }

        protected override Vector3 ComputeManeuverPoint(StateVector stateVector)
        {
            var an = TargetOrbit.SpecificAngularMomentum().Cross(stateVector.SpecificAngularMomentum()).Normalize();
            if (IsAscendingNode == true)
            {
                return an;
            }

            if (IsAscendingNode == false)
            {
                return an.Inverse();
            }

            if (stateVector.Position.Angle(an, stateVector.SpecificAngularMomentum()) > 0.0)
            {
                IsAscendingNode = true;
                return an;
            }

            IsAscendingNode = false;
            return an.Inverse();
        }

        protected override Vector3 Execute(StateVector stateVector)
        {
            if (IsAscendingNode is null)
            {
                throw new InvalidOperationException("Maneuver point undefined");
            }
            
            var vel = stateVector.Velocity;
            var pos = stateVector.Position;

            //Project vector
            var projectedVector = vel - (pos * ((vel * pos) / (pos * pos)));

            //Compute relative inclination
            RelativeInclination = System.Math.Acos(System.Math.Cos(stateVector.Inclination()) * System.Math.Cos(TargetOrbit.Inclination()) +
                                                   (System.Math.Sin(stateVector.Inclination()) * System.Math.Sin(TargetOrbit.Inclination())) *
                                                   System.Math.Cos(TargetOrbit.AscendingNode() - stateVector.AscendingNode()));

            double rotationAngle = Constants.PI2 + RelativeInclination * 0.5;

            if (IsAscendingNode == true)
            {
                rotationAngle = -rotationAngle;
            }

            //Compute deltaV
            var deltaV = 2.0 * projectedVector.Magnitude() * System.Math.Sin(RelativeInclination * 0.5);

            //Compute the quaternion
            var q = new Quaternion(pos.Normalize(), rotationAngle);

            //Rotate velocity vector
            var rotateVecor = projectedVector.Normalize().Rotate(q);

            //Compute delta V vector
            return rotateVecor * deltaV;
        }

        internal override void Reset()
        {
            IsAscendingNode = null;
            RelativeInclination = 0.0;  
            base.Reset();
        }
    }
}