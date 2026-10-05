using System;
using System.ComponentModel;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    /// <summary>
    /// Placeholder for a low-thrust maneuver. It was never implemented: every member throws
    /// <see cref="NotImplementedException"/>.
    /// </summary>
    [Obsolete("LowThrustManeuver was never implemented and will be removed in 11.0. Finite thrust is planned as a separate feature.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class LowThrustManeuver : ImpulseManeuver
    {
        protected LowThrustManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : base(minimumEpoch,
            maneuverHoldDuration, targetOrbit, engine)
        {
        }

        public override double ComputeEventValue(StateVector localState)
        {
            throw new NotImplementedException();
        }

        public override CrossingDirection EventCrossingDirection => throw new NotImplementedException();

        protected override Vector3 Execute(StateVector vector)
        {
            throw new NotImplementedException();
        }
    }
}