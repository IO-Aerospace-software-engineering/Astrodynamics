using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public class PlaneAlignmentManeuver : ImpulseManeuver
    {
        public bool? IsAscendingNode { get; private set; }
        public double RelativeInclination { get; private set; }

        private CrossingDirection _crossingDirection = CrossingDirection.Any;

        public PlaneAlignmentManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit,
            Engine engine) : base(minimumEpoch, maneuverHoldDuration, targetOrbit, engine)
        {
        }

        /// <summary>
        /// g = r · ĥ_target : height above target orbital plane.
        /// Zero at both ascending and descending nodes.
        /// CrossingDirection distinguishes which node triggers the maneuver.
        /// </summary>
        public override double ComputeEventValue(StateVector localState)
        {
            var targetH = TargetOrbit.SpecificAngularMomentum().Normalize();
            double g = localState.Position * targetH;

            // On first evaluation, determine which node is closer and lock the direction
            if (IsAscendingNode == null)
            {
                // Ascending node: spacecraft crosses from below to above target plane (g: - → +, NegToPos)
                // Descending node: spacecraft crosses from above to below target plane (g: + → -, PosToNeg)
                double threshold = 1e-6 * localState.Position.Magnitude();

                if (System.Math.Abs(g) < threshold)
                {
                    // At/near the node: use rate of change dg/dt = v · ĥ_target
                    // Positive dg/dt → ascending through the plane → ascending node
                    // Negative dg/dt → descending through the plane → descending node
                    double dgdt = localState.Velocity * targetH;
                    IsAscendingNode = dgdt > 0;
                }
                else if (g > 0)
                {
                    // Above target plane: next zero-crossing going negative is descending node
                    IsAscendingNode = false;
                }
                else
                {
                    // Below target plane: next zero-crossing going positive is ascending node
                    IsAscendingNode = true;
                }

                _crossingDirection = IsAscendingNode == true
                    ? CrossingDirection.NegativeToPositive
                    : CrossingDirection.PositiveToNegative;
            }

            return g;
        }

        public override CrossingDirection EventCrossingDirection => _crossingDirection;

        protected override Vector3 Execute(StateVector stateVector)
        {
            // Determine ascending node direction from angular momentum cross product
            var an = TargetOrbit.SpecificAngularMomentum().Cross(stateVector.SpecificAngularMomentum()).Normalize();
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