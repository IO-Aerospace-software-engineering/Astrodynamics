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
    /// An impulse at apogee that moves the perigee, up or down, to a target radius from the centre of the maneuver centre.
    /// </summary>
    public class PerigeeHeightManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The perigee radius after the burn, in m from the centre of the maneuver centre (not an altitude).
        /// <c>TargetPerigeeHeight</c> before 11.0.0 (R13 of #668, in lot L5b, decided by S. Guillet, 2026-11-23).
        /// </summary>
        public double TargetPerigeeRadius { get; }

        /// <summary>
        /// Creates the maneuver that moves the perigee to that of <paramref name="targetOrbit"/>, about its observer.
        /// </summary>
        /// <param name="targetOrbit">The orbit whose perigee radius is the target; its observer is the maneuver centre.</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
        /// <param name="holdDuration">Duration to hold after the maneuver; zero by default.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetOrbit"/> is null (before 11.0.0 a null target
        /// orbit threw <see cref="NullReferenceException"/>, #555).</exception>
        /// <exception cref="ArgumentException">The observer of <paramref name="targetOrbit"/> is not a celestial
        /// item.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
        public PerigeeHeightManeuver(OrbitalParameters.OrbitalParameters targetOrbit, Engine engine = null, Time minEpoch = default,
            TimeSpan holdDuration = default) : this(RequireTargetOrbit(targetOrbit).PerigeeVector().Magnitude(), engine, minEpoch,
            holdDuration, CenterOf(targetOrbit))
        {
        }

        /// <summary>
        /// Creates the maneuver that moves the perigee to <paramref name="perigeeRadius"/>.
        /// </summary>
        /// <param name="perigeeRadius">The perigee radius after the burn, in m from the centre of the maneuver centre, not
        /// an altitude: for an altitude h, its equatorial radius + h. A perigee inside the body is legal (a deorbit).</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
        /// <param name="holdDuration">Duration to hold after the maneuver; zero by default.</param>
        /// <param name="maneuverCenter">The body the orbit is about; null for the centre the run infers, the observer of
        /// the initial orbit (required in SSB mode).</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="perigeeRadius"/> is not finite or not above 0
        /// (decision 68 of #668, decided by S. Guillet, 2026-11-23); or <paramref name="holdDuration"/> is negative.</exception>
        public PerigeeHeightManeuver(double perigeeRadius, Engine engine = null, Time minEpoch = default, TimeSpan holdDuration = default,
            CelestialItem maneuverCenter = null) : base(engine, minEpoch, holdDuration, maneuverCenter)
        {
            TargetPerigeeRadius = ApogeeHeightManeuver.CheckRadius(perigeeRadius, nameof(perigeeRadius), "perigee");
        }

        /// <summary>
        /// Fires at apogee: r·v = 0, transitioning from positive to negative.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position.Dot(localState.Velocity);
        public override CrossingDirection EventCrossingDirection => CrossingDirection.PositiveToNegative;

        protected override Vector3 Execute(StateVector stateVector)
        {
            var apogee = stateVector.ApogeeVector().Magnitude();
            double vInit = stateVector.Velocity.Magnitude();
            double vFinal = System.Math.Sqrt(stateVector.Observer.GM * ((2.0 / apogee) - (1.0 / ((apogee + TargetPerigeeRadius) / 2.0))));
            return stateVector.Velocity.NormalizeOrZero() * (vFinal - vInit);
        }
    }
}