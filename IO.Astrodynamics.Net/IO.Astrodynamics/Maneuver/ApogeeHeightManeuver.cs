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
    /// An impulse at perigee that moves the apogee, up or down, to a target radius from the centre of the maneuver centre.
    /// </summary>
    public class ApogeeHeightManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The apogee radius after the burn, in m from the centre of the maneuver centre (not an altitude).
        /// <c>TargetApogee</c> before 11.0.0 (R13 of #668, in lot L5b, decided by S. Guillet, 2026-11-23).
        /// </summary>
        public double TargetApogeeRadius { get; }

        /// <summary>
        /// Creates the maneuver that moves the apogee to that of <paramref name="targetOrbit"/>, about its observer.
        /// </summary>
        /// <param name="targetOrbit">The orbit whose apogee radius is the target; its observer is the maneuver centre.</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
        /// <param name="holdDuration">Duration to hold after the maneuver; zero by default.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetOrbit"/> is null (before 11.0.0 a null target
        /// orbit threw <see cref="NullReferenceException"/>, #555).</exception>
        /// <exception cref="ArgumentException"><paramref name="targetOrbit"/> is not an ellipse: a parabola or a hyperbola
        /// has no apoapsis (#737; before 11.0.0, the radius of a point of the other branch of a hyperbola, or
        /// +Infinity); or its observer is not a celestial item; or its apogee radius is below the equatorial radius of its
        /// observer, a celestial body (decision 68 of #668).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
        public ApogeeHeightManeuver(OrbitalParameters.OrbitalParameters targetOrbit, Engine engine = null, Time minEpoch = default,
            TimeSpan holdDuration = default) : this(ApogeeRadiusOf(targetOrbit), engine, minEpoch, holdDuration, CenterOf(targetOrbit),
            nameof(targetOrbit))
        {
        }

        // The apogee radius of the target orbit, which only an ellipse has: a bad argument otherwise (#737)
        private static double ApogeeRadiusOf(OrbitalParameters.OrbitalParameters targetOrbit)
        {
            RequireTargetOrbit(targetOrbit);
            try
            {
                return targetOrbit.ApogeeVector().Magnitude();
            }
            catch (InvalidOperationException ex) when (!(double.IsFinite(targetOrbit.SemiMajorAxis()) && targetOrbit.SemiMajorAxis() > 0.0))
            {
                throw new ArgumentException($"The target orbit has no apoapsis: {ex.Message}", nameof(targetOrbit), ex);
            }
        }

        /// <summary>
        /// Creates the maneuver that moves the apogee to <paramref name="apogeeRadius"/>.
        /// </summary>
        /// <param name="apogeeRadius">The apogee radius after the burn, in m from the centre of the maneuver centre, not an
        /// altitude: for an altitude h, its equatorial radius + h.</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
        /// <param name="holdDuration">Duration to hold after the maneuver; zero by default.</param>
        /// <param name="maneuverCenter">The body the orbit is about; null for the centre the run infers, the observer of
        /// the initial orbit (required in SSB mode).</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="apogeeRadius"/> is not finite or not above 0
        /// (decision 68 of #668, decided by S. Guillet, 2026-11-23); or <paramref name="holdDuration"/> is negative.</exception>
        /// <exception cref="ArgumentException"><paramref name="apogeeRadius"/> is below the equatorial radius of
        /// <paramref name="maneuverCenter"/>, a celestial body (decision 68 of #668): an apogee inside the body, an
        /// altitude given for a radius. Reads the radius of a deferred body (<c>PlanetsAndMoons.EARTH_BODY</c>), which
        /// reads its provider data. For a centre the run infers, the run checks the rule
        /// (<see cref="InvalidOperationException"/>).</exception>
        public ApogeeHeightManeuver(double apogeeRadius, Engine engine = null, Time minEpoch = default, TimeSpan holdDuration = default,
            CelestialItem maneuverCenter = null) : this(apogeeRadius, engine, minEpoch, holdDuration, maneuverCenter, nameof(apogeeRadius))
        {
        }

        private ApogeeHeightManeuver(double apogeeRadius, Engine engine, Time minEpoch, TimeSpan holdDuration, CelestialItem maneuverCenter,
            string paramName) : base(engine, minEpoch, holdDuration, maneuverCenter)
        {
            TargetApogeeRadius = CheckRadius(apogeeRadius, nameof(apogeeRadius), "apogee");
            if (maneuverCenter != null && CenterError(maneuverCenter) is { } error)
            {
                throw new ArgumentException(error, paramName);
            }
        }

        /// <summary>
        /// A radius finite and above 0, or <see cref="ArgumentOutOfRangeException"/> naming <paramref name="paramName"/>
        /// (decision 68 of #668, decided by S. Guillet, 2026-11-23: before 11.0.0 a radius of 0, negative or not finite was
        /// accepted, and 1000e3 m, an altitude, ended the run in an <c>InsufficientFuelException</c>).
        /// </summary>
        internal static double CheckRadius(double radius, string paramName, string apsis)
        {
            if (!double.IsFinite(radius) || radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(paramName, radius,
                    $"The {apsis} radius must be finite and above 0: a radius from the centre of the maneuver centre, not an altitude; for a height h pass R + h.");
            }

            return radius;
        }

        /// <inheritdoc />
        /// <remarks>The apogee is at or above the equatorial radius of a celestial body centre (decision 68 of #668): a
        /// perigee inside the body stays legal (a deorbit), an apogee inside it is an altitude given for a radius.</remarks>
        internal override string CenterError(CelestialItem center)
        {
            if (center is CelestialBody body && TargetApogeeRadius < body.EquatorialRadius)
            {
                return $"The target apogee radius, {TargetApogeeRadius:R} m, is below the equatorial radius of {body.Name}, {body.EquatorialRadius:R} m: " +
                       "the target is a radius from the centre of the body, not an altitude; for a height h pass R + h.";
            }

            return null;
        }

        /// <summary>
        /// Fires at perigee: r·v = 0, transitioning from negative to positive.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position.Dot(localState.Velocity);
        public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

        protected override Vector3 Execute(StateVector stateVector)
        {
            var perigee = stateVector.PerigeeVector().Magnitude();
            double vInit = stateVector.Velocity.Magnitude();
            double vFinal = System.Math.Sqrt(stateVector.Observer.GM * ((2.0 / perigee) - (1.0 / ((perigee + TargetApogeeRadius) / 2.0))));
            return stateVector.Velocity.NormalizeOrZero() * (vFinal - vInit);
        }
    }
}