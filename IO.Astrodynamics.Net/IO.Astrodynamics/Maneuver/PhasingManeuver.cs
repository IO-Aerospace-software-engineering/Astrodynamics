using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver
{
    public class PhasingManeuver : ImpulseManeuver
    {
        public double TargetTrueLongitude { get; } = double.NaN;
        public uint RevolutionNumber { get; }

        public PhasingManeuver(DateTime minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, uint revolutionNumber, Engine engine) : base(
            minimumEpoch, maneuverHoldDuration, targetOrbit, engine)
        {
            RevolutionNumber = revolutionNumber;
        }

        public PhasingManeuver(CelestialItem maneuverCenter, DateTime minimumEpoch, TimeSpan maneuverHoldDuration, double trueLongitude, uint revolutionNumber, Engine engine) : base(maneuverCenter, minimumEpoch,
            maneuverHoldDuration, engine)
        {
            TargetTrueLongitude = trueLongitude;
            RevolutionNumber = revolutionNumber;
        }

        protected override Vector3 ComputeManeuverPoint(StateVector stateVector)
        {
            return stateVector.PerigeeVector();
        }

        protected override Vector3 Execute(StateVector stateVector)
        {
            double deltaTrueAnomaly = TargetOrbit.TrueLongitude(stateVector.Epoch) - stateVector.TrueLongitude();
            if (deltaTrueAnomaly < 0.0)
            {
                deltaTrueAnomaly += Constants._2PI;
            }

            double e = stateVector.Eccentricity();

            double E = 2 * System.Math.Atan((System.Math.Sqrt((1 - e) / (1 + e))) * System.Math.Tan(deltaTrueAnomaly / 2.0));
            double t1 = stateVector.Period().TotalSeconds;
            double t = t1 / Constants._2PI * (E - e * System.Math.Sin(E));

            double t2 = t1 - t / RevolutionNumber;

            double u = stateVector.Observer.GM;

            double a2 = System.Math.Pow((System.Math.Sqrt(u) * t2 / Constants._2PI), 2.0 / 3.0);

            double rp = stateVector.PerigeeVector().Magnitude();
            double ra = 2 * a2 - rp;

            double h2 = System.Math.Sqrt(2 * u) * System.Math.Sqrt(ra * rp / (ra + rp));

            double dv = h2 / rp - TargetOrbit.SpecificAngularMomentum().Magnitude() / rp;

            ManeuverHoldDuration = TimeSpan.FromSeconds(t2 * RevolutionNumber * 0.9);
            return stateVector.Velocity.Normalize() * dv;
        }
    }
}