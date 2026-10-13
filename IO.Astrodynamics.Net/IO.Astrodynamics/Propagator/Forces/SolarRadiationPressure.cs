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
    /// analytic outside the penumbra, the default central differences inside it; ∂a/∂v = 0.
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
    /// the acceleration.
    /// </para>
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        if (!TryGetIllumination(stateVector, out var posRelToSun, out double litFraction))
            return;

        if (litFraction < 1.0)
        {
            base.AccumulateStatePartialsCore(stateVector, context, dadr, dadv);
            return;
        }

        AddAnalyticBlock(posRelToSun, litFraction, context, dadr);
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
    /// The position of the spacecraft relative to the Sun and the lit fraction 1 − ν of the solar disc, ν the largest
    /// shadow fraction of the occulting bodies; false in total eclipse. Shared by the acceleration and its partials.
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
            var occFromObserver = GeometricPosition(occultingBody, stateVector);

            var scToOcc = occFromObserver - scPosition;
            double occAngSize = occultingBody.AngularSize(scToOcc.Magnitude());
            double angSep = scToSun.Normalize().Angle(scToOcc.Normalize());

            double fraction = CelestialItem.ShadowFraction(angSep, sunAngSize, occAngSize);
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
}
