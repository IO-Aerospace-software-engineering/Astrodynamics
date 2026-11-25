using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    /// <summary>
    /// An impulse at apogee that changes the inclination and moves the perigee, up or down, to a target radius from the
    /// centre of the maneuver centre.
    /// </summary>
    public class CombinedManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The perigee radius after the burn, in m from the centre of the maneuver centre (not an altitude).
        /// <c>TargetPerigeeHeight</c> before 11.0.0 (decision 68 of #668, in lot L5b, decided by S. Guillet, 2026-11-23).
        /// </summary>
        public double TargetPerigeeRadius { get; }

        /// <summary>
        /// The inclination after the burn, in radians, in [0, π].
        /// </summary>
        public double TargetInclination { get; }

        /// <summary>
        /// Creates the maneuver.
        /// </summary>
        /// <param name="perigeeRadius">The perigee radius after the burn, in m from the centre of the maneuver centre, not
        /// an altitude: for an altitude h, its equatorial radius + h.</param>
        /// <param name="inclination">The inclination after the burn, in radians, in [0, π].</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
        /// <param name="holdDuration">Duration to hold after the maneuver; zero by default.</param>
        /// <param name="maneuverCenter">The body the orbit is about; null for the centre the run infers, the observer of
        /// the initial orbit (required in SSB mode).</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="perigeeRadius"/> is not finite or not above 0, or
        /// <paramref name="inclination"/> is not finite or outside [0, π] (decision 68 of #668, decided by S. Guillet,
        /// 2026-11-23: before 11.0.0 an inclination above π threw <see cref="ArgumentException"/> at the burn and a negative
        /// one was executed); or <paramref name="holdDuration"/> is negative.</exception>
        public CombinedManeuver(double perigeeRadius, double inclination, Engine engine = null, Time minEpoch = default,
            TimeSpan holdDuration = default, CelestialItem maneuverCenter = null) : base(engine, minEpoch, holdDuration, maneuverCenter)
        {
            TargetPerigeeRadius = ApogeeHeightManeuver.CheckRadius(perigeeRadius, nameof(perigeeRadius), "perigee");
            if (!(inclination >= 0.0 && inclination <= System.Math.PI))
            {
                throw new ArgumentOutOfRangeException(nameof(inclination), inclination, "The target inclination must be finite and in [0, π] rad.");
            }

            TargetInclination = inclination;
        }

        /// <summary>
        /// Fires at apogee: r·v = 0, transitioning from positive to negative.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position.Dot(localState.Velocity);
        public override CrossingDirection EventCrossingDirection => CrossingDirection.PositiveToNegative;

        /// <summary>
        /// Apogee must be nearly aligned with ascending node (dot product > 0.9).
        /// </summary>
        public override bool CheckPreconditions(StateVector state) =>
            System.Math.Abs(state.ApogeeVector().NormalizeOrZero().Dot(state.AscendingNodeVector().NormalizeOrZero())) > 0.9;

        protected override Vector3 Execute(StateVector stateVector)
        {
            return ComputeTargetOrbit(stateVector).ToStateVector().Velocity - stateVector.Velocity;
        }

        /// <summary>
        /// The orbit after the burn: its apsides the current apogee and <see cref="TargetPerigeeRadius"/>, inclined at
        /// <see cref="TargetInclination"/>, through the given state. It carries no cached perigee radius, so its
        /// <see cref="OrbitalParameters.OrbitalParameters.PerigeeRadius"/> is its own, a(1 − e) (#677); it carried the current
        /// orbit's.
        /// </summary>
        internal KeplerianElements ComputeTargetOrbit(StateVector stateVector)
        {
            double e;
            double meanAnomaly = stateVector.MeanAnomaly();
            double periapsisArgument = stateVector.ArgumentOfPeriapsis();
            double apogee = stateVector.ApogeeVector().Magnitude();

            //If target perigee is higher than current apogee
            if (TargetPerigeeRadius > apogee)
            {
                e = 1.0 - (2.0 / ((TargetPerigeeRadius / apogee) + 1.0));

                //Periapse argument will turn by 180°
                meanAnomaly = (meanAnomaly + Constants.PI) % Constants.TwoPi;
                periapsisArgument += Constants.PI;
            }
            else
            {
                e = 1.0 - (2.0 / ((apogee / TargetPerigeeRadius) + 1.0));
            }

            return new KeplerianElements((TargetPerigeeRadius + apogee) * 0.5, e, TargetInclination, stateVector.AscendingNode(), periapsisArgument, meanAnomaly,
                stateVector.Observer, stateVector.Epoch, stateVector.Frame);
        }
    }
}