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
        if (!TryGetIllumination(stateVector, out var posRelToSun, out double litFraction, out _))
            return Vector3.Zero;

        // SRP acceleration: spacecraft position relative to Sun
        var term2 = posRelToSun / System.Math.Pow(posRelToSun.Magnitude(), 3.0);
        var areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;
        return term2 * _term1 * areaMassRatio * context.ReflectivityCoefficient * litFraction;
    }

    /// <summary>
    /// ∂a/∂r of the cannonball acceleration a = k Cr (A/m) (1 − ν) ρ / |ρ|³, ρ = r − r_sun, ν the shadow fraction:
    /// analytic in sunlight, zero in total eclipse, the default central differences in the penumbra and wherever their
    /// stencil reaches a shadow boundary; ∂a/∂v = 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In sunlight (ν = 0), the analytic block of <see cref="AccumulateAnalyticStatePartials"/> is exact. In total
    /// eclipse the acceleration is zero, and so are the partials.
    /// </para>
    /// <para>
    /// In the penumbra (0 &lt; ν &lt; 1), ∂a/∂r = (1 − ν) ∂a_lit/∂r − a_lit (∇ν)ᵀ, and the second term dominates: ν goes
    /// from 0 to 1 across a penumbra some 20 km wide in LEO, against 1.5e8 km for the scale of a_lit. No published
    /// derivative of the shadow function of this model was found; NASA GMAT neglects it (its A matrix warns that it
    /// lacks the shadow partials), Orekit differentiates the lighting ratio. Neglected, it moved Φ by 1.05e-4 after one
    /// day in LEO 400 km (A/m = 0.1 m²/kg, against central differences of whole propagations: 5.3e-6 with the term,
    /// 1.1e-6 to 2.4e-6 without SRP). So the penumbra goes through the default central differences, which differentiate
    /// ν with the rest (decision of S. Guillet, 2026-10-12): about 1 % of a LEO orbit, at the cost of six evaluations of
    /// the acceleration. Their step is scaled by the distance to the nearest occulting body
    /// (<see cref="PositionStepScale"/>), not by the distance to the observer of the state, which is the Sun in SSB
    /// mode.
    /// </para>
    /// <para>
    /// The central differences also run within two steps of a shadow boundary, sunlit or in total eclipse, so that the
    /// partials are continuous along the trajectory. At the boundary of the umbra, 1 − ν vanishes as d^(3/2), d the
    /// distance to the boundary, so the exact partials tend to zero; a central difference whose stencil straddles the
    /// boundary gives about a(r + h) / 2h instead, 5.5e-13 /s² in the umbra of R3, and switching to zero at the
    /// boundary made the partials jump there. The jump does not move Φ measurably, but the step-size control on Φ and
    /// Ψ (<c>IncludeInStepControl</c>) cannot resolve it: it rejected 100 steps down to 1e-6 s at the first umbra
    /// entry of R3 (H3 review of #378). With the central differences on both sides of each boundary, the partials are
    /// continuous: zero where the stencil lies in the umbra, and the analytic block to the truncation of the central
    /// difference, of order (h / |ρ|)², where it lies in sunlight.
    /// </para>
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        bool illuminated = TryGetIllumination(stateVector, out var posRelToSun, out double litFraction,
            out var shadow);
        double step = DefaultPositionRelativeStep * System.Math.Max(StepScale(stateVector, shadow), 1.0);
        bool stencilInOneRegime = shadow.BoundaryDistance > 2.0 * step;

        if (illuminated && litFraction == 1.0 && stencilInOneRegime)
        {
            AddAnalyticBlock(posRelToSun, litFraction, context, dadr);
            return;
        }

        if (!illuminated && stencilInOneRegime)
        {
            return;
        }

        base.AccumulateStatePartialsCore(stateVector, context, dadr, dadv);
    }

    /// <summary>
    /// The analytic block with the shadow fraction held constant, ∂a/∂r = k Cr (A/m) (1 − ν) (I / |ρ|³ − 3 ρ ρᵀ / |ρ|⁵),
    /// whatever the illumination: exact outside the penumbra, without the term of ∇ν inside. Used by
    /// <see cref="AccumulateStatePartialsCore"/> in sunlight, and by the tests to measure that term.
    /// </summary>
    /// <remarks>
    /// The point-mass block (B2) with μ = −k Cr (A/m) (1 − ν). Reference: Montenbruck and Gill, Satellite Orbits,
    /// Springer (2000), chapter 7 (partials of the solar radiation pressure; to be verified by S. Guillet, section and
    /// equation numbers). Same block as the SRP rows of the A matrix of NASA GMAT (<c>SolarRadiationPressure.cpp</c>,
    /// spherical model), compared on the source without copying code.
    /// </remarks>
    internal void AccumulateAnalyticStatePartials(StateVector stateVector, in ForceEvaluationContext context,
        Span<double> dadr)
    {
        if (TryGetIllumination(stateVector, out var posRelToSun, out double litFraction, out _))
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
    /// distance times the angular diameter of the Sun, so the step of the default central differences keeps the same
    /// ratio to it whatever the observer of the state (|r| for the Earth in the geocentric mode, bit for bit). Without
    /// occulting body, the default, |r|.
    /// </summary>
    private protected override double PositionStepScale(StateVector stateVector)
    {
        TryGetIllumination(stateVector, out _, out _, out var shadow);
        return StepScale(stateVector, shadow);
    }

    private double StepScale(StateVector stateVector, in ShadowGeometry shadow)
    {
        return shadow.OccultingDistance > 0.0 ? shadow.OccultingDistance : base.PositionStepScale(stateVector);
    }

    /// <summary>
    /// The position of the spacecraft relative to the Sun and the lit fraction 1 − ν of the solar disc, ν the largest
    /// shadow fraction of the occulting bodies; false in total eclipse. Shared by the acceleration and its partials,
    /// which also read the geometry of the shadow boundaries in <paramref name="shadow"/>.
    /// </summary>
    /// <remarks>
    /// The distance to the nearest boundary is a first-order lower bound: a displacement δ of the spacecraft turns the
    /// directions of the Sun and of an occulting body by at most δ / D_sun and δ / D_occ, and changes their angular radii
    /// asin(R / D) by at most δ R / (D √(D² − R²)), so the angular margin m to a boundary (the outer contact
    /// d = r_sun + r_occ in sunlight, the inner contact d = r_occ − r_sun in total eclipse, d the angular separation)
    /// is not crossed before δ = m / (1 / D_sun + R_sun / (D_sun √(D_sun² − R_sun²)) + 1 / D_occ +
    /// R_occ / (D_occ √(D_occ² − R_occ²))). It is not computed in the penumbra, where the partials always take the
    /// central differences, and is NaN inside an occulting body.
    /// </remarks>
    private bool TryGetIllumination(StateVector stateVector, out Vector3 posRelToSun, out double litFraction,
        out ShadowGeometry shadow)
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
        double sunRate = AngularRate(scToSunMag, _sun.EquatorialRadius);
        double occultingDistance = 0.0;
        double boundaryDistance = double.PositiveInfinity;

        foreach (var occultingBody in _occultingBodies)
        {
            var occFromObserver = GeometricPosition(occultingBody, stateVector);

            var scToOcc = occFromObserver - scPosition;
            double occDistance = scToOcc.Magnitude();
            double occAngSize = occultingBody.AngularSize(occDistance);
            double angSep = scToSun.Normalize().Angle(scToOcc.Normalize());
            occultingDistance = occultingDistance == 0.0 ? occDistance : System.Math.Min(occultingDistance, occDistance);
            double rate = sunRate + AngularRate(occDistance, occultingBody.EquatorialRadius);

            double fraction = CelestialItem.ShadowFraction(angSep, sunAngSize, occAngSize);
            if (fraction >= 1.0)
            {
                // Margin to the inner contact of this occulting body, which alone sets the total eclipse
                posRelToSun = Vector3.Zero;
                litFraction = 0.0;
                shadow = new ShadowGeometry(occultingDistance,
                    (0.5 * (occAngSize - sunAngSize) - angSep) / rate);
                return false;
            }

            if (fraction > maxShadowFraction)
                maxShadowFraction = fraction;

            // Margin to the outer contact
            boundaryDistance = System.Math.Min(boundaryDistance, (angSep - 0.5 * (sunAngSize + occAngSize)) / rate);
        }

        posRelToSun = scPosition - sunFromObserver;
        litFraction = 1.0 - maxShadowFraction;
        shadow = new ShadowGeometry(occultingDistance, boundaryDistance);
        return true;
    }

    // Bound on the rate of change, per metre of displacement, of the direction of a body at distance D (1 / D) and of
    // its angular radius asin(R / D) (R / (D √(D² − R²))).
    private static double AngularRate(double distance, double radius)
    {
        return 1.0 / distance + radius / (distance * System.Math.Sqrt(distance * distance - radius * radius));
    }

    /// <summary>
    /// The distance to the nearest occulting body (0 without occulting body) and a lower bound on the displacement
    /// that reaches a shadow boundary (infinite without occulting body), in m.
    /// </summary>
    private readonly record struct ShadowGeometry(double OccultingDistance, double BoundaryDistance);
}
