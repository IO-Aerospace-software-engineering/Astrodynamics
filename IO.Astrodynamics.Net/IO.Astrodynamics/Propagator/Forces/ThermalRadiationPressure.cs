// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// Thermal radiation pressure force model using an isotropic emitter approximation.
/// Models the perturbation from infrared radiation emitted by a celestial body onto a spacecraft.
/// Unlike albedo, TRP is always present regardless of Sun geometry or eclipse state.
/// </summary>
public class ThermalRadiationPressure : ForceBase
{
    private readonly Spacecraft _spacecraft;
    private readonly CelestialBody _emittingBody;
    private readonly double _thermalTerm; // epsilon * sigma * T^4 * R^2 / c  (precomputed)

    public ThermalRadiationPressure(Spacecraft spacecraft, CelestialBody emittingBody)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _emittingBody = emittingBody ?? throw new ArgumentNullException(nameof(emittingBody));

        if (emittingBody.ThermalEffectiveTemperature <= 0.0)
            throw new ArgumentException(
                $"Emitting body '{emittingBody.Name}' must have a positive effective temperature (got {emittingBody.ThermalEffectiveTemperature}). " +
                "Set thermalEffectiveTemperature in the CelestialBody constructor.",
                nameof(emittingBody));

        if (emittingBody.ThermalEmissivity <= 0.0)
            throw new ArgumentException(
                $"Emitting body '{emittingBody.Name}' must have a positive thermal emissivity (got {emittingBody.ThermalEmissivity}). " +
                "Set thermalEmissivity in the CelestialBody constructor.",
                nameof(emittingBody));

        double t = emittingBody.ThermalEffectiveTemperature;
        double t4 = t * t * t * t;
        double r = emittingBody.EquatorialRadius;
        _thermalTerm = emittingBody.ThermalEmissivity * Constants.StefanBoltzmann * t4 * r * r / Constants.C;
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        return Apply(stateVector, ForceEvaluationContext.FromSpacecraft(_spacecraft));
    }

    internal override bool DependsOnVelocity => false;

    internal override ForceParameters Parameters => ForceParameters.ReflectivityCoefficient;

    /// <summary>
    /// Thermal acceleration with the mass and the reflectivity coefficient of <paramref name="context"/>.
    /// </summary>
    internal override Vector3 Apply(StateVector stateVector, in ForceEvaluationContext context)
    {
        // Get emitting body position relative to observer
        Vector3 bodyFromObserver;
        if (_emittingBody.Equals(stateVector.Observer))
        {
            bodyFromObserver = Vector3.Zero;
        }
        else if (EphemerisCache != null && EphemerisCache.Contains(_emittingBody.NaifId, Aberration.LT))
        {
            bodyFromObserver = EphemerisCache.GetPosition(_emittingBody.NaifId, Aberration.LT, stateVector.Epoch);
        }
        else
        {
            bodyFromObserver = _emittingBody.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT)
                .ToStateVector().Position;
        }

        var scPosition = stateVector.ToStateVector().Position;

        // Vector from body center to spacecraft
        var bodyToSc = scPosition - bodyFromObserver;
        double rSc = bodyToSc.Magnitude();

        if (rSc < 1.0) return Vector3.Zero; // Avoid singularity

        // Area/mass ratio (dynamic mass)
        double areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;

        // Acceleration magnitude: Cr * (A/m) * thermalTerm / r^2
        double acceleration = context.ReflectivityCoefficient * areaMassRatio * _thermalTerm / (rSc * rSc);

        // Direction: radially outward from emitting body (isotropic emitter model)
        var direction = bodyToSc / rSc;

        return direction * acceleration;
    }
}
