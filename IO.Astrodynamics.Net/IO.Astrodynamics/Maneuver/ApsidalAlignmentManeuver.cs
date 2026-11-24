using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;


namespace IO.Astrodynamics.Maneuver
{
    public class ApsidalAlignmentManeuver : ImpulseManeuver
    {
        /// <summary>
        /// Whether the last execution burnt at the intersection P, a side result of the execution. Internal since 11.0.0
        /// (decision 51 of #668, decided by S. Guillet, 2026-11-23): nothing read it, and every evaluation of the
        /// g-function wrote it (#802).
        /// </summary>
        internal bool IntersectsP { get; private set; }

        /// <summary>Whether the last execution burnt at the intersection Q; internal since 11.0.0, as
        /// <see cref="IntersectsP"/>.</summary>
        internal bool IntersectsQ { get; private set; }

        /// <summary>
        /// An impulse at the nearest intersection of the orbit with <paramref name="targetOrbit"/> that aligns the apsides.
        /// </summary>
        /// <param name="targetOrbit">The orbit whose apsides are the target, in the frame the spacecraft is propagated in;
        /// its observer is the maneuver centre.</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
        /// <param name="holdDuration">Duration to hold after the maneuver; zero by default.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetOrbit"/> is null.</exception>
        /// <exception cref="ArgumentException">The observer of <paramref name="targetOrbit"/> is not a celestial
        /// item.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
        public ApsidalAlignmentManeuver(OrbitalParameters.OrbitalParameters targetOrbit, Engine engine = null, Time minEpoch = default,
            TimeSpan holdDuration = default) : base(targetOrbit, engine, minEpoch, holdDuration)
        {
        }

        /// <summary>
        /// g = r · (ĥ × p̂) : perpendicular component in orbital plane relative to intersection point.
        /// Zero when spacecraft passes through the nearest orbit intersection point.
        /// </summary>
        /// <remarks>Pure (decision 50 of #668, decided by S. Guillet, 2026-11-23): before 11.0.0 each evaluation wrote
        /// <see cref="IntersectsP"/> and <see cref="IntersectsQ"/> (#802).</remarks>
        public override double ComputeEventValue(StateVector localState)
        {
            var (intersectionDir, _) = DetermineIntersectionDirection(localState);
            var perpDir = localState.SpecificAngularMomentum().Cross(intersectionDir).NormalizeOrZero();
            return localState.Position.Dot(perpDir);
        }

        public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

        // The direction of the nearest intersection of the orbits, and whether it is P: pure (decision 50 of #668)
        private (Vector3 Direction, bool IsP) DetermineIntersectionDirection(StateVector stateVector)
        {
            double pv = GetPTrueAnomaly(stateVector);
            double qv = GetQTrueAnomaly(stateVector);
            double v = stateVector.TrueAnomaly();
            double vRelativeToP = AngleDifference(v, pv);
            double vRelativeToQ = AngleDifference(v, qv);

            if (vRelativeToP <= vRelativeToQ)
            {
                return (stateVector.ToStateVector(pv).Position.NormalizeOrZero(), true);
            }

            return (stateVector.ToStateVector(qv).Position.NormalizeOrZero(), false);
        }

        private double AngleDifference(double angleA, double angleB)
        {
            double delta = (angleA - angleB + Constants.TwoPi) % Constants.TwoPi;
            if (delta > Constants.PI)
            {
                delta = Constants.TwoPi - delta;
            }

            return delta;
        }

        /// <inheritdoc />
        /// <remarks>Pure (decision 50 of #668, decided by S. Guillet, 2026-11-23).</remarks>
        protected override Vector3 Execute(StateVector stateVector) => DeltaVAt(stateVector).DeltaV;

        /// <inheritdoc />
        /// <remarks>The intersection burnt at kept in <see cref="IntersectsP"/> and <see cref="IntersectsQ"/>.</remarks>
        internal override Vector3 ExecuteInRun(StateVector localState)
        {
            var (deltaV, isP) = DeltaVAt(localState);
            IntersectsP = isP;
            IntersectsQ = !isP;
            return deltaV;
        }

        // The delta-V at the nearest intersection, and whether it is P: the pure core of the execution (decision 50 of #668)
        private (Vector3 DeltaV, bool IsP) DeltaVAt(StateVector stateVector)
        {
            var (_, isP) = DetermineIntersectionDirection(stateVector);
            var deltaV = isP
                ? TargetOrbit.ToStateVector(GetTargetPTrueAnomaly(stateVector)).Velocity - stateVector.Velocity
                : TargetOrbit.ToStateVector(GetTargetQTrueAnomaly(stateVector)).Velocity - stateVector.Velocity;
            return (deltaV, isP);
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
            var res = stateVector.PerigeeVector().AngleAboutNormalOrZero(TargetOrbit.PerigeeVector(), stateVector.SpecificAngularMomentum());
            if (res < 0.0)
            {
                res += Constants.TwoPi;
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
                res += Constants.TwoPi;
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
                res += Constants.TwoPi;
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