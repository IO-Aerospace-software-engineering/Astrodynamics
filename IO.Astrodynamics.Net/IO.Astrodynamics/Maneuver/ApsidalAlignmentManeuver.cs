using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;


namespace IO.Astrodynamics.Maneuver
{
    public class ApsidalAlignmentManeuver : ImpulseManeuver
    {
        public bool IntersectsP { get; private set; }
        public bool IntersectsQ { get; private set; }

        public ApsidalAlignmentManeuver(DateTime minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit,
            Engine engine) : base(minimumEpoch, maneuverHoldDuration, targetOrbit, engine)
        {
        }

        protected override Vector3 ComputeManeuverPoint(StateVector stateVector)
        {
            double pv = GetPTrueAnomaly(stateVector);
            double qv = GetQTrueAnomaly(stateVector);
            double v = stateVector.TrueAnomaly();
            double vRelativeToP = AngleDifference(v, pv);
            double vRelativeToQ = AngleDifference(v, qv);

            if (vRelativeToP <= vRelativeToQ)
            {
                IntersectsP = true;
                IntersectsQ = false;
                return stateVector.ToStateVector(pv).Position;
            }

            IntersectsP = false;
            IntersectsQ = true;
            return stateVector.ToStateVector(qv).Position;
        }

        private double AngleDifference(double angleA, double angleB)
        {
            double delta = (angleA - angleB + Constants._2PI) % Constants._2PI;
            if (delta > Constants.PI)
            {
                delta = Constants._2PI - delta;
            }

            return delta;
        }

        protected override Vector3 Execute(StateVector stateVector)
        {
            ComputeManeuverPoint(stateVector);
            Vector3 resVector;
            if (IntersectsP)
            {
                resVector = TargetOrbit.ToStateVector(GetTargetPTrueAnomaly(stateVector)).Velocity - stateVector.Velocity;
            }
            else if (IntersectsQ)
            {
                resVector = TargetOrbit.ToStateVector(GetTargetQTrueAnomaly(stateVector)).Velocity - stateVector.Velocity;
            }
            else
            {
                throw new ArgumentException("To compute orientation, maneuver point must be at orbits intersection");
            }

            return resVector;
        }

        private Dictionary<string, double> GetCoefficients(StateVector stateVector)
        {
            Dictionary<string, double> res = new Dictionary<string, double>();
            double h1 = System.Math.Pow(stateVector.SpecificAngularMomentum().Magnitude(), 2.0);
            double h2 = System.Math.Pow(TargetOrbit.SpecificAngularMomentum().Magnitude(), 2.0);
            double theta = GetTheta(stateVector);

            res["A"] = h2 * stateVector.Eccentricity() - h1 * TargetOrbit.Eccentricity() * System.Math.Cos(theta);
            res["B"] = -h1 * TargetOrbit.Eccentricity() * System.Math.Sin(theta);
            res["C"] = h1 - h2;
            res["alpha"] = System.Math.Atan(res["B"] / res["A"]);

            return res;
        }

        private double GetTheta(StateVector stateVector)
        {
            var res = stateVector.PerigeeVector().Angle(TargetOrbit.PerigeeVector(), stateVector.SpecificAngularMomentum());
            if (res < 0.0)
            {
                res += Constants._2PI;
            }

            return res;
        }

        internal double GetPTrueAnomaly(StateVector stateVector)
        {
            var coef = GetCoefficients(stateVector);
            double res = coef["alpha"] + System.Math.Acos((coef["C"] / coef["A"]) * System.Math.Cos(coef["alpha"]));
            if (double.IsNaN(res))
            {
                throw new ArgumentException("Apsidal alignment requires orbits intersection");
            }

            if (res < 0.0)
            {
                res += Constants._2PI;
            }

            return res;
        }

        internal double GetQTrueAnomaly(StateVector stateVector)
        {
            var coef = GetCoefficients(stateVector);
            double res = coef["alpha"] - System.Math.Acos((coef["C"] / coef["A"]) * System.Math.Cos(coef["alpha"]));
            if (double.IsNaN(res))
            {
                throw new ArgumentException("Apsidal alignment requires orbits intersection");
            }

            if (res < 0.0)
            {
                res += Constants._2PI;
            }

            return res;
        }

        internal double GetTargetPTrueAnomaly(StateVector stateVector)
        {
            return GetPTrueAnomaly(stateVector) - GetTheta(stateVector);
        }

        internal double GetTargetQTrueAnomaly(StateVector stateVector)
        {
            return GetQTrueAnomaly(stateVector) - GetTheta(stateVector);
        }
    }
}