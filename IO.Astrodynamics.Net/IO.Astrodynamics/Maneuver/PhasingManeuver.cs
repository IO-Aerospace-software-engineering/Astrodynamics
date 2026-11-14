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
    /// A phasing maneuver: an impulse along the velocity at the perigee that changes the period so that, after
    /// <see cref="RevolutionNumber"/> revolutions of the phasing orbit, the spacecraft is back at the perigee together
    /// with its target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target is at the true anomaly Δθ ahead of the spacecraft at the burn, Δθ = λ_target − λ_sc, the difference of
    /// their true longitudes (Ω + ω + ν) at the epoch of the burn. With t(Δθ) the time from the perigee to Δθ along the
    /// current orbit by Kepler's equation (negative for a target behind, Δθ beyond π), the phasing orbit has the period
    /// T₂ = T₁ − t(Δθ) / N and the semi-major axis a₂ = (√μ T₂ / 2π)^(2/3); the delta-V at the perigee is
    /// h₂ / r_p − h_target / r_p, h₂ the angular momentum of the phasing orbit and h_target that of the target's orbit.
    /// The hold of the maneuver is set to 0.9 N T₂.
    /// </para>
    /// <para>
    /// The target is either a target orbit, whose true longitude at the epoch of the burn and angular momentum the
    /// maneuver reads, or a true longitude, that of a virtual target on the spacecraft's current orbit, whose angular
    /// momentum is the spacecraft's (#588).
    /// </para>
    /// </remarks>
    public class PhasingManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The true longitude of the target given to the true-longitude constructor, in radians: that of a virtual target
        /// on the spacecraft's current orbit at the epoch of the burn. NaN for a maneuver built on a target orbit.
        /// </summary>
        public double TargetTrueLongitude { get; } = double.NaN;

        /// <summary>
        /// The number N of revolutions of the phasing orbit after which the spacecraft meets its target at the perigee.
        /// </summary>
        public uint RevolutionNumber { get; }

        /// <summary>
        /// A phasing maneuver toward a target orbit: the target is the object of <paramref name="targetOrbit"/>, whose
        /// true longitude at the epoch of the burn and angular momentum set the phasing orbit.
        /// </summary>
        /// <param name="minimumEpoch">The earliest epoch of the burn.</param>
        /// <param name="maneuverHoldDuration">The hold of the maneuver; replaced at the burn by 0.9 N T₂.</param>
        /// <param name="targetOrbit">The orbit of the target, about the maneuver center and in the frame of the
        /// spacecraft.</param>
        /// <param name="revolutionNumber">The number N of revolutions of the phasing orbit.</param>
        /// <param name="engine">The engine of the burn.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetOrbit"/> or <paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException">The spacecraft of the engine and the target orbit are defined in different frames.</exception>
        public PhasingManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, uint revolutionNumber, Engine engine) : base(
            minimumEpoch, maneuverHoldDuration, targetOrbit, engine)
        {
            RevolutionNumber = revolutionNumber;
        }

        /// <summary>
        /// A phasing maneuver toward a phase on the spacecraft's current orbit (#588): the target is a virtual object on
        /// the orbit of the spacecraft at the burn, at the true longitude <paramref name="trueLongitude"/> at the epoch of
        /// the burn, so that Δθ = λ − λ_sc(burn), and its angular momentum is the spacecraft's.
        /// </summary>
        /// <param name="maneuverCenter">The center of the maneuver, the body the true longitudes are measured about.</param>
        /// <param name="minimumEpoch">The earliest epoch of the burn.</param>
        /// <param name="maneuverHoldDuration">The hold of the maneuver; replaced at the burn by 0.9 N T₂.</param>
        /// <param name="trueLongitude">The true longitude λ = Ω + ω + ν of the virtual target at the epoch of the burn, in
        /// radians, relative to <paramref name="maneuverCenter"/> in the frame of the spacecraft's state, the inertial
        /// frame of its initial orbital parameters. The burn is at the perigee, so λ_sc(burn) is the longitude of the
        /// perigee Ω + ω, and λ − (Ω + ω) is the phase of the target ahead of the spacecraft along its orbit, taken
        /// modulo 2π: beyond π the target is behind.</param>
        /// <param name="revolutionNumber">The number N of revolutions of the phasing orbit.</param>
        /// <param name="engine">The engine of the burn.</param>
        public PhasingManeuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, double trueLongitude, uint revolutionNumber, Engine engine) : base(
            maneuverCenter, minimumEpoch,
            maneuverHoldDuration, engine)
        {
            TargetTrueLongitude = trueLongitude;
            RevolutionNumber = revolutionNumber;
        }

        /// <summary>
        /// Fires at perigee: r·v = 0, transitioning from negative to positive.
        /// </summary>
        public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;
        public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

        protected override Vector3 Execute(StateVector stateVector)
        {
            // The target: the target orbit, or a virtual target on the current orbit at the given true longitude (#588)
            double targetTrueLongitude = TargetOrbit?.TrueLongitude(stateVector.Epoch) ?? TargetTrueLongitude;
            double deltaTrueAnomaly = targetTrueLongitude - stateVector.TrueLongitude();
            if (deltaTrueAnomaly < 0.0)
            {
                deltaTrueAnomaly += Constants._2PI;
            }

            double e = stateVector.Eccentricity();

            double E = OrbitalParameters.OrbitalParameters.EllipticAnomaly(deltaTrueAnomaly, e);
            double t1 = stateVector.Period().TotalSeconds;
            double t = t1 / Constants._2PI * (E - e * System.Math.Sin(E));

            double t2 = t1 - t / RevolutionNumber;

            double u = stateVector.Observer.GM;

            double a2 = System.Math.Pow((System.Math.Sqrt(u) * t2 / Constants._2PI), 2.0 / 3.0);

            double rp = stateVector.PerigeeVector().Magnitude();
            double ra = 2 * a2 - rp;

            double h2 = System.Math.Sqrt(2 * u) * System.Math.Sqrt(ra * rp / (ra + rp));

            double dv = h2 / rp - (TargetOrbit ?? stateVector).SpecificAngularMomentum().Magnitude() / rp;

            ManeuverHoldDuration = TimeSpan.FromSeconds(t2 * RevolutionNumber * 0.9);
            return stateVector.Velocity.NormalizeOrZero() * dv;
        }
    }
}