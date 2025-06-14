using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.Maneuver
{
    public class HohmannTransfer : ImpulseManeuver
    {
        private readonly CelestialItem _originBody;
        private readonly CelestialItem _targetBody;
        private readonly Vector3 _deltaV;
        private readonly double _transferTime;

        public HohmannTransfer(
            CelestialItem originBody, // Origin body (e.g., Earth)
            CelestialItem targetBody, // Target body (e.g., Mars)
            Time minimumEpoch,
            TimeSpan maneuverHoldDuration,
            Engine engine) : base(CelestialItem.Create(originBody.CenterOfMotionId), minimumEpoch, maneuverHoldDuration, engine) // Central body (e.g., Sun)
        {
            _originBody = originBody ?? throw new ArgumentNullException(nameof(originBody));
            _targetBody = targetBody ?? throw new ArgumentNullException(nameof(targetBody));

            // Verify both bodies orbit the same central body
            //TODO: How to handle cases where bodies might not orbit the same central body?(eg Earth and Moon)
            if (_originBody.CenterOfMotionId != _targetBody.CenterOfMotionId)
                throw new ArgumentException("Origin and target bodies must orbit the same central body");

            
        }

        private (Vector3 deltaV, double transferTime) CalculateTransferParameters()
        {
            // Get central body's gravitational parameter
            var mu = ManeuverCenter.GM;

            // Get the orbits relative to the central body
            var originOrbit = _originBody.GetGeometricStateFromICRF(MinimumEpoch);
            var targetOrbit = _targetBody.GetGeometricStateFromICRF(MinimumEpoch);

            // For transfer calculation, we need the semi-major axes
            double r1 = originOrbit.SemiMajorAxis();
            double r2 = targetOrbit.SemiMajorAxis();

            // Calculate semi-major axis of the transfer orbit
            double a_transfer = (r1 + r2) / 2.0;

            // Calculate velocity at departure point in the origin orbit
            var originState = originOrbit.ToStateVector();
            double v_origin = originState.Velocity.Magnitude();

            // Calculate velocity needed at departure point in transfer orbit
            double v_transfer = System.Math.Sqrt(mu * (2.0 / r1 - 1.0 / a_transfer));

            // Calculate delta-v direction (aligned with velocity for prograde burn)
            var deltaV = originState.Velocity.Normalize() * (v_transfer - v_origin);

            // Calculate transfer time (half the period of the transfer orbit)
            double transferTime = System.Math.PI * System.Math.Sqrt(System.Math.Pow(a_transfer, 3) / mu);

            return (deltaV, transferTime);
        }

        protected override Vector3 ComputeManeuverPoint(StateVector stateVector)
        {
            // Central body's gravitational parameter
            var mu = ManeuverCenter.GM;

            // Get orbits
            var originState = _originBody.GetGeometricStateFromICRF(stateVector.Epoch).ToStateVector();
            var targetState = _targetBody.GetGeometricStateFromICRF(stateVector.Epoch).ToStateVector();

            // Calculate transfer orbit parameters
            double r1 = originState.SemiMajorAxis();
            double r2 = targetState.SemiMajorAxis();
            double a_transfer = (r1 + r2) / 2.0;

            // Calculate transfer time (half the period of the transfer orbit)
            double transferTime = System.Math.PI * System.Math.Sqrt(System.Math.Pow(a_transfer, 3) / mu);

            // Calculate the angular travel of the target during the transfer
            double targetMeanMotion = System.Math.Sqrt(mu / System.Math.Pow(r2, 3));
            double targetAngularTravel = targetMeanMotion * transferTime;

            // Calculate required phase angle: 180° minus target's angular travel
            double phaseAngle = System.Math.PI - targetAngularTravel;

            // Position vectors from central body
            var originPos = originState.Position;
            var targetPos = targetState.Position;

            // Calculate current phase angle between origin and target
            double currentAngle = originPos.Angle(targetPos);

            // If the current angle is close to the required angle, we're at the maneuver point
            if (System.Math.Abs(currentAngle - phaseAngle) < 0.01)
            {
                return originPos;
            }

            // Otherwise, calculate where the origin body should be for the maneuver
            var centralBodyPos = new Vector3(0, 0, 0);
            var h = originState.SpecificAngularMomentum();

            // Calculate desired position by rotating the target vector
            var desiredDirection = originPos.Rotate(new Quaternion(h.Normalize(), phaseAngle - currentAngle));

            // Return a point at the same orbital radius as the origin
            return desiredDirection.Normalize() * originPos.Magnitude();
        }

        protected override Vector3 Execute(StateVector stateVector)
        {
            (var deltaV, var transferTime) = CalculateTransferParameters();
            // Apply the delta-v
            StateVector newStateVector = new StateVector(
                stateVector.Position,
                stateVector.Velocity+ _deltaV,
                ManeuverCenter,stateVector.Epoch,
                stateVector.Frame);

            return stateVector.Velocity + _deltaV;
        }
        
    }
}