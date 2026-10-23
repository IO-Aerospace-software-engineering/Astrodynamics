using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

public class SolarRadiationPressure : ForceBase
{
    private readonly CelestialBody _sun = Stars.SUN_BODY;
    private readonly Spacecraft _spacecraft;
    private readonly double _term1;
    private readonly CelestialBody[] _occultingBodies;

    /// <summary>
    /// Cannonball solar radiation pressure on <paramref name="spacecraft"/>, scaled by the shadow of the occulting
    /// bodies.
    /// </summary>
    /// <param name="spacecraft">The spacecraft: sectional area, mass and reflectivity coefficient.</param>
    /// <param name="occultingBodies">
    /// The bodies that can eclipse the Sun. The Sun itself is ignored if it is among them: it cannot eclipse itself,
    /// and treating it as an occulter would put the spacecraft in a permanent total eclipse.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="spacecraft"/> or <paramref name="occultingBodies"/> is
    /// null.</exception>
    public SolarRadiationPressure(Spacecraft spacecraft, IEnumerable<CelestialBody> occultingBodies)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _occultingBodies = (occultingBodies ?? throw new ArgumentNullException(nameof(occultingBodies)))
            .Where(body => body.NaifId != _sun.NaifId)
            .ToArray();
        _term1 = Constants.SolarMeanRadiativeLuminosity / (4.0 * System.Math.PI * Constants.C);
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        return Apply(stateVector, ForceEvaluationContext.FromSpacecraft(_spacecraft));
    }

    internal override bool DependsOnVelocity => false;

    internal override ForceParameters Parameters => ForceParameters.ReflectivityCoefficient;

    /// <summary>
    /// SRP acceleration with the mass and the reflectivity coefficient of <paramref name="context"/>.
    /// </summary>
    internal override Vector3 Apply(StateVector stateVector, in ForceEvaluationContext context)
    {
        if (!TryGetIllumination(stateVector, out var posRelToSun, out double litFraction))
            return Vector3.Zero;

        // SRP acceleration: spacecraft position relative to Sun
        var term2 = posRelToSun / System.Math.Pow(posRelToSun.Magnitude(), 3.0);
        var areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;
        return term2 * _term1 * areaMassRatio * context.ReflectivityCoefficient * litFraction;
    }

    /// <summary>
    /// ∂a/∂r of the cannonball acceleration a = k Cr (A/m) (1 − ν) ρ / |ρ|³, ρ = r − r_sun, ν the shadow fraction:
    /// the analytic block in sunlight, zero in total eclipse, and in the penumbra the analytic block with the derivative
    /// of the shadow function; ∂a/∂v = 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the penumbra (0 &lt; ν &lt; 1), ∂a/∂r = (1 − ν) ∂a_lit/∂r − a_lit (∇ν)ᵀ, a_lit the acceleration without shadow:
    /// eq. 4.79 of the GMAT Mathematical Specification, R2026a, §4.2.4, with ν the eclipse factor of its eq. 4.78. The
    /// second term dominates: ν goes from 0 to 1 across a penumbra some 20 km wide in LEO. ∇ν is that of §4.2.5 (see
    /// <see cref="AddShadowTerm"/>), the gradient of the two-circle area of its §3.2.28, the model of
    /// <see cref="CelestialItem.ShadowFraction(double, double, double)"/> (to be verified by S. Guillet): ν and ∇ν come
    /// from the same geometry of the discs (<see cref="DiscOverlap"/>), so the partials are the derivative of the shadow
    /// fraction that scales the acceleration (#434). Neglected, it
    /// moved Φ by 1.05e-4 after one day in LEO 400 km (decision of S. Guillet, 2026-10-12).
    /// </para>
    /// <para>
    /// The regime comes from the angles, as in eqs. 3.148 and 3.149 of the specification: sunlit when the angular
    /// separation reaches the sum of the angular radii, total when it is at most their difference, the penumbra
    /// otherwise (<see cref="DiscOverlap.Regime"/>, the branches of
    /// <see cref="CelestialItem.ShadowFraction(double, double, double)"/>), never from the value of ν (#423). With the
    /// derivative of the shadow function the partials are continuous across both contacts without any band around them:
    /// it tends to zero there, and so does 1 − ν at the inner contact. Before #434 the shadow fraction itself cancelled
    /// near a contact to a rounding of a few 1e-8, sometimes below zero inside the penumbra; it is now within a few
    /// 1e-16 of the exact value and always in [0, 1].
    /// </para>
    /// <para>
    /// Before #423 the penumbra, and a band of two steps around each contact, took the default central differences
    /// (#379), which divided the rounding of ν by their stencil: 5.0e-7 to 1.6e-3 of the block, which the step control
    /// on Φ and Ψ (<c>IncludeInStepControl</c>) followed with thousands of steps per day on an eclipsing orbit.
    /// </para>
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        var sunFromObserver = GeometricPosition(_sun, stateVector);
        var scPosition = stateVector.ToStateVector().Position;
        var scToSun = sunFromObserver - scPosition;
        double scToSunMag = scToSun.Magnitude();
        double sunRadius = 0.5 * _sun.AngularSize(scToSunMag);

        // The occulting body with the largest shadow fraction, as in the acceleration; ν and its gradient from the same
        // geometry of the discs
        double shadowFraction = 0.0;
        Occultation shadowing = default;
        ShadowRegime shadowRegime = ShadowRegime.None;
        DiscOverlap shadowOverlap = default;
        foreach (var occultingBody in _occultingBodies)
        {
            var occultation = Occult(occultingBody, stateVector, scPosition, scToSun, sunRadius);
            var regime = DiscOverlap.Regime(sunRadius, occultation.Radius, occultation.Separation);
            if (regime == ShadowRegime.None)
                continue;

            if (regime == ShadowRegime.Total)
                return;

            DiscOverlap overlap = default;
            double fraction;
            if (regime == ShadowRegime.Annular)
            {
                fraction = occultation.Radius * occultation.Radius / (sunRadius * sunRadius);
            }
            else
            {
                overlap = new DiscOverlap(sunRadius, occultation.Radius, occultation.Separation);
                fraction = overlap.Fraction;
            }

            if (shadowRegime == ShadowRegime.None || fraction > shadowFraction)
            {
                shadowFraction = fraction;
                shadowing = occultation;
                shadowRegime = regime;
                shadowOverlap = overlap;
            }
        }

        var posRelToSun = scPosition - sunFromObserver;
        AddAnalyticBlock(posRelToSun, 1.0 - shadowFraction, context, dadr);
        if (shadowRegime != ShadowRegime.None)
        {
            AddShadowTerm(posRelToSun, scToSun, scToSunMag, sunRadius, shadowing, shadowRegime, shadowOverlap,
                shadowFraction, context, dadr);
        }
    }

    /// <summary>
    /// Adds −a_lit (∇ν)ᵀ, the term of the derivative of the shadow function, to <paramref name="dadr"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GMAT Mathematical Specification, R2026a, §4.2.5 (to be verified by S. Guillet), with R⊙′ = a, R_B′ = b the angular
    /// radii of the Sun and of the occulting body and D′ = c their angular separation (§3.2.28, eqs. 3.145 to 3.147):
    /// ∇ν = ∇A / (π a²) − 2 ν ∇a / a in a partial eclipse (eq. 4.87, ν = p / 100), A the area of the overlap of the two
    /// discs (eq. 3.150), and ∇ν = 2 b ∇b / a² − 2 b² ∇a / a³ in an annular eclipse (eq. 4.88). At the fixed epoch of
    /// the partials, ∇a = R⊙ ŝ / (d √(d² − R⊙²)) and ∇b = R_B ô / (d_B √(d_B² − R_B²)) (eqs. 4.97 and 4.98 with
    /// ∂d/∂r = −I and ∂s/∂r = I), ŝ and ô the directions of the Sun and of the occulting body from the spacecraft, at
    /// the distances d and d_B; ∇c = ((ô − cos c ŝ) / d + (ŝ − cos c ô) / d_B) / sin c, the derivative of the angle
    /// between the two directions (eq. 4.99).
    /// </para>
    /// <para>
    /// ∇A = −2 c₂ ∇c + 2 a α ∇a + 2 b β ∇b, with c₂ the half chord of the two circles and α, β the half angles that the
    /// chord subtends at the centres of the Sun and of the occulting body: the derivative of eq. 3.150, eq. 4.100
    /// reduced, its terms in ∂c₁ and ∂c₂ cancelling (checked with sympy, <c>tools/shadow_function/area_derivatives.py</c>:
    /// the symbolic difference is 0). The half chord comes from Heron's product in the form of Kahan, and the half angles
    /// from atan2, α = atan2(c₂, c₁) and β = atan2(c₂, c − c₁), c₁ = ((c − b)(c + b) + a²) / (2c) (eq. 3.151): the
    /// geometry of <see cref="DiscOverlap"/>, computed once for ν and ∇A. Written as in eq. 4.100, the derivative
    /// divides by √(1 − c₁² / a²) = c₂ / a, which vanishes at both contacts, and takes the acos of arguments within
    /// 1e-5 of 1 in LEO; this form has neither, and tends to zero at the contacts.
    /// </para>
    /// </remarks>
    private void AddShadowTerm(in Vector3 posRelToSun, in Vector3 scToSun, double scToSunMag, double a,
        in Occultation occultation, ShadowRegime regime, in DiscOverlap overlap, double shadowFraction,
        in ForceEvaluationContext context, Span<double> dadr)
    {
        var sunDirection = scToSun / scToSunMag;
        var bodyDirection = occultation.Direction;
        double sunRadius = _sun.EquatorialRadius;
        double bodyRadius = occultation.BodyRadius;
        double d = scToSunMag;
        double dB = occultation.Distance;
        double b = occultation.Radius;
        double c = occultation.Separation;

        var gradA = sunDirection * (sunRadius / (d * System.Math.Sqrt(d * d - sunRadius * sunRadius)));
        var gradB = bodyDirection * (bodyRadius / (dB * System.Math.Sqrt(dB * dB - bodyRadius * bodyRadius)));
        Vector3 gradNu;
        if (regime == ShadowRegime.Annular)
        {
            // Annular eclipse (eq. 4.88)
            gradNu = gradB * (2.0 * b / (a * a)) - gradA * (2.0 * b * b / (a * a * a));
        }
        else
        {
            // Partial eclipse (eq. 4.87), the area by its reduced derivative
            double cosC = System.Math.Cos(c);
            var gradC = ((bodyDirection - sunDirection * cosC) / d + (sunDirection - bodyDirection * cosC) / dB) /
                        System.Math.Sin(c);
            var (dAdc, dAda, dAdb) = overlap.AreaDerivatives;
            var gradArea = gradC * dAdc + gradA * dAda + gradB * dAdb;
            gradNu = gradArea / (System.Math.PI * a * a) - gradA * (2.0 * shadowFraction / a);
        }

        double areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;
        var litAcceleration = posRelToSun * (_term1 * areaMassRatio * context.ReflectivityCoefficient /
                                             System.Math.Pow(posRelToSun.Magnitude(), 3.0));
        AddOuterProduct(litAcceleration, gradNu, -1.0, dadr);
    }

    /// <summary>
    /// The partial derivatives of the area A of the overlap of two discs of angular radii <paramref name="a"/> and
    /// <paramref name="b"/> whose centres are <paramref name="c"/> apart (GMAT Mathematical Specification, R2026a,
    /// eq. 3.150), when they overlap partially: ∂A/∂c = −2 c₂, ∂A/∂a = 2 a α, ∂A/∂b = 2 b β, the reduced form of its
    /// eq. 4.100 (see <see cref="AddShadowTerm"/>), without cancellation; those of <see cref="DiscOverlap"/>.
    /// </summary>
    /// <returns>∂A/∂c, ∂A/∂a, ∂A/∂b, in rad.</returns>
    internal static (double DAdc, double DAda, double DAdb) OverlapAreaDerivatives(double a, double b, double c)
    {
        return new DiscOverlap(a, b, c).AreaDerivatives;
    }

    // block += factor u vᵀ, row-major
    private static void AddOuterProduct(in Vector3 u, in Vector3 v, double factor, Span<double> block)
    {
        block[0] += factor * u.X * v.X;
        block[1] += factor * u.X * v.Y;
        block[2] += factor * u.X * v.Z;
        block[3] += factor * u.Y * v.X;
        block[4] += factor * u.Y * v.Y;
        block[5] += factor * u.Y * v.Z;
        block[6] += factor * u.Z * v.X;
        block[7] += factor * u.Z * v.Y;
        block[8] += factor * u.Z * v.Z;
    }

    /// <summary>
    /// The analytic block with the shadow fraction held constant, ∂a/∂r = k Cr (A/m) (1 − ν) (I / |ρ|³ − 3 ρ ρᵀ / |ρ|⁵),
    /// whatever the illumination: exact outside the penumbra, without the term of ∇ν inside. Used by the tests to
    /// measure that term.
    /// </summary>
    /// <remarks>
    /// The point-mass block (B2) with μ = −k Cr (A/m) (1 − ν). Reference: Montenbruck and Gill, Satellite Orbits,
    /// Springer (2000), chapter 7 (partials of the solar radiation pressure; confirmed by S. Guillet,
    /// 2026-10-13). Same block as the SRP rows of the A matrix of NASA GMAT (<c>SolarRadiationPressure.cpp</c>,
    /// spherical model), compared on the source without copying code.
    /// </remarks>
    internal void AccumulateAnalyticStatePartials(StateVector stateVector, in ForceEvaluationContext context,
        Span<double> dadr)
    {
        if (TryGetIllumination(stateVector, out var posRelToSun, out double litFraction))
        {
            AddAnalyticBlock(posRelToSun, litFraction, context, dadr);
        }
    }

    private void AddAnalyticBlock(in Vector3 posRelToSun, double litFraction, in ForceEvaluationContext context,
        Span<double> dadr)
    {
        double areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;
        PointMassPartials.Accumulate(posRelToSun,
            -_term1 * areaMassRatio * context.ReflectivityCoefficient * litFraction, dadr);
    }

    /// <summary>
    /// The distance to the nearest occulting body: the width of its penumbra, on which ν goes from 0 to 1, is that
    /// distance times the angular diameter of the Sun, so the step of the default central differences
    /// (<see cref="ForceBase.AccumulateStatePartialsByCentralDifferences"/>) keeps the same ratio to it whatever the
    /// observer of the state (|r| for the Earth in the geocentric mode, bit for bit). Without occulting body, the
    /// default, |r|.
    /// </summary>
    private protected override double PositionStepScale(StateVector stateVector)
    {
        var scPosition = stateVector.ToStateVector().Position;
        double nearest = 0.0;
        foreach (var occultingBody in _occultingBodies)
        {
            double distance = (GeometricPosition(occultingBody, stateVector) - scPosition).Magnitude();
            nearest = nearest == 0.0 ? distance : System.Math.Min(nearest, distance);
        }

        return nearest > 0.0 ? nearest : base.PositionStepScale(stateVector);
    }

    /// <summary>
    /// The position of the spacecraft relative to the Sun and the lit fraction 1 − ν of the solar disc, ν the largest
    /// shadow fraction of the occulting bodies; false in total eclipse.
    /// </summary>
    private bool TryGetIllumination(StateVector stateVector, out Vector3 posRelToSun, out double litFraction)
    {
        // Geometric Sun position relative to the state vector observer
        var sunFromObserver = GeometricPosition(_sun, stateVector);

        var scPosition = stateVector.ToStateVector().Position;

        // Vectors from spacecraft to Sun and occluding bodies
        var scToSun = sunFromObserver - scPosition;
        double scToSunMag = scToSun.Magnitude();

        // Shadow fraction computation
        double maxShadowFraction = 0.0;
        double sunAngSize = _sun.AngularSize(scToSunMag);

        foreach (var occultingBody in _occultingBodies)
        {
            var occultation = Occult(occultingBody, stateVector, scPosition, scToSun, 0.5 * sunAngSize);
            double fraction = CelestialItem.ShadowFraction(occultation.Separation, sunAngSize,
                2.0 * occultation.Radius);
            if (fraction >= 1.0)
            {
                posRelToSun = Vector3.Zero;
                litFraction = 0.0;
                return false;
            }

            if (fraction > maxShadowFraction)
                maxShadowFraction = fraction;
        }

        posRelToSun = scPosition - sunFromObserver;
        litFraction = 1.0 - maxShadowFraction;
        return true;
    }

    /// <summary>
    /// The geometry of one occulting body seen from the spacecraft, shared by the acceleration and its partials: its
    /// direction and distance, its angular radius and its angular separation from the Sun (GMAT Mathematical
    /// Specification, R2026a, eqs. 3.146 and 3.147).
    /// </summary>
    private Occultation Occult(CelestialBody occultingBody, StateVector stateVector, in Vector3 scPosition,
        in Vector3 scToSun, double sunRadius)
    {
        var scToOcc = GeometricPosition(occultingBody, stateVector) - scPosition;
        double occDistance = scToOcc.Magnitude();
        double occAngSize = occultingBody.AngularSize(occDistance);
        double angSep = scToSun.Normalize().Angle(scToOcc.Normalize());
        return new Occultation(scToOcc / occDistance, occDistance, occultingBody.EquatorialRadius, 0.5 * occAngSize,
            angSep);
    }

    /// <summary>
    /// An occulting body seen from the spacecraft: its direction (unit vector), its distance and radius in m, its angular
    /// radius and its angular separation from the Sun in rad.
    /// </summary>
    private readonly record struct Occultation(Vector3 Direction, double Distance, double BodyRadius, double Radius,
        double Separation);
}
