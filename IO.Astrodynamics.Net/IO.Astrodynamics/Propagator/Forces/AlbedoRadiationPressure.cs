// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// Albedo radiation pressure force model using a Lambertian sphere approximation.
/// Models the perturbation from sunlight reflected off a celestial body onto a spacecraft.
/// </summary>
public class AlbedoRadiationPressure : ForceBase
{
    private readonly Spacecraft _spacecraft;
    private readonly CelestialBody _reflectingBody;
    private readonly CelestialBody _sun = Stars.SUN_BODY;
    private readonly double _albedo;
    private readonly double _luminosityTerm;
    private readonly double _bodyRadius;

    /// <summary>
    /// Lambertian geometric factor: 2 / (3 * pi)
    /// </summary>
    private static readonly double LambertianFactor = 2.0 / (3.0 * System.Math.PI);

    public AlbedoRadiationPressure(Spacecraft spacecraft, CelestialBody reflectingBody)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _reflectingBody = reflectingBody ?? throw new ArgumentNullException(nameof(reflectingBody));

        if (reflectingBody.Albedo <= 0.0)
            throw new ArgumentException(
                $"Reflecting body '{reflectingBody.Name}' must have a positive albedo (got {reflectingBody.Albedo}). " +
                "Set albedo in the CelestialBody constructor.",
                nameof(reflectingBody));

        _albedo = reflectingBody.Albedo;
        _luminosityTerm = Constants.SolarMeanRadiativeLuminosity / (4.0 * System.Math.PI * Constants.C);
        _bodyRadius = reflectingBody.EquatorialRadius;
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        return Apply(stateVector, ForceEvaluationContext.FromSpacecraft(_spacecraft));
    }

    internal override bool DependsOnVelocity => false;

    internal override ForceParameters Parameters => ForceParameters.ReflectivityCoefficient;

    /// <summary>
    /// Albedo acceleration with the mass and the reflectivity coefficient of <paramref name="context"/>.
    /// </summary>
    internal override Vector3 Apply(StateVector stateVector, in ForceEvaluationContext context)
    {
        // Get Sun position relative to observer
        Vector3 sunFromObserver;
        if (EphemerisCache != null && EphemerisCache.Contains(_sun.NaifId, Aberration.LT))
            sunFromObserver = EphemerisCache.GetPosition(_sun.NaifId, Aberration.LT, stateVector.Epoch);
        else
            sunFromObserver = _sun.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT)
                .ToStateVector().Position;

        // Get reflecting body position relative to observer
        Vector3 bodyFromObserver;
        if (_reflectingBody.Equals(stateVector.Observer))
        {
            bodyFromObserver = Vector3.Zero;
        }
        else if (EphemerisCache != null && EphemerisCache.Contains(_reflectingBody.NaifId, Aberration.LT))
        {
            bodyFromObserver = EphemerisCache.GetPosition(_reflectingBody.NaifId, Aberration.LT, stateVector.Epoch);
        }
        else
        {
            bodyFromObserver = _reflectingBody.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT)
                .ToStateVector().Position;
        }

        var scPosition = stateVector.ToStateVector().Position;

        // Vector from body center to spacecraft
        var bodyToSc = scPosition - bodyFromObserver;
        double rSc = bodyToSc.Magnitude();

        if (rSc < 1.0) return Vector3.Zero; // Avoid singularity

        // Vector from body center to Sun
        var bodyToSun = sunFromObserver - bodyFromObserver;
        double rSun = bodyToSun.Magnitude();

        if (rSun < 1.0) return Vector3.Zero; // Avoid singularity

        // Phase angle: angle at reflecting body between Sun and spacecraft directions
        double phi = bodyToSun.Angle(bodyToSc);

        // Lambertian visibility factor: (pi - phi) * cos(phi) + sin(phi)
        // Naturally goes to zero as phi -> pi (spacecraft on dark side)
        double lambertianVisibility = (System.Math.PI - phi) * System.Math.Cos(phi) + System.Math.Sin(phi);

        if (lambertianVisibility <= 0.0) return Vector3.Zero;

        // Area/mass ratio (dynamic mass)
        double areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;

        // Full acceleration magnitude:
        // a = (L_sun / (4*pi*c)) * alpha * Cr * (A/m) * (2/(3*pi)) * (R_body/r_sun)^2 * [(pi-phi)*cos(phi)+sin(phi)] / r_sc^2
        double bodyRadiusOverRSun = _bodyRadius / rSun;
        double acceleration = _luminosityTerm * _albedo * context.ReflectivityCoefficient * areaMassRatio
                              * LambertianFactor
                              * bodyRadiusOverRSun * bodyRadiusOverRSun
                              * lambertianVisibility
                              / (rSc * rSc);

        // Direction: radially outward from reflecting body (cannonball model)
        var direction = bodyToSc / rSc;

        return direction * acceleration;
    }
}
