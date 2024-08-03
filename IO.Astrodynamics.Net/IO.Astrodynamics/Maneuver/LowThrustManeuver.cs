using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver
{
    public abstract class LowThrustManeuver : ImpulseManeuver
    {
        protected LowThrustManeuver(DateTime minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : base(minimumEpoch,
            maneuverHoldDuration, targetOrbit, engine)
        {
        }

        protected override Vector3 ComputeManeuverPoint(StateVector stateVector)
        {
            throw new NotImplementedException();
        }

        protected override Vector3 Execute(StateVector vector)
        {
            throw new NotImplementedException();
        }
    }
}