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
        if (!TryGetOffsetFromEmitter(stateVector, out var bodyToSc))
            return Vector3.Zero;

        double rSc = bodyToSc.Magnitude();

        // Area/mass ratio (dynamic mass)
        double areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;

        // Acceleration magnitude: Cr * (A/m) * thermalTerm / r^2
        double acceleration = context.ReflectivityCoefficient * areaMassRatio * _thermalTerm / (rSc * rSc);

        // Direction: radially outward from emitting body (isotropic emitter model)
        var direction = bodyToSc / rSc;

        return direction * acceleration;
    }

    /// <summary>
    /// Analytic ∂a/∂r of the isotropic emitter a = K Cr (A/m) ρ / |ρ|³, ρ the position relative to the emitting body:
    /// ∂a/∂r = K Cr (A/m) (I / |ρ|³ − 3 ρ ρᵀ / |ρ|⁵), and ∂a/∂v = 0.
    /// </summary>
    /// <remarks>
    /// The acceleration has the inverse-square form of the cannonball solar radiation pressure, so its partials are those
    /// of Montenbruck and Gill, Satellite Orbits, Springer (2000), chapter 7 (to be verified by S. Guillet, section and
    /// equation numbers), with the emitting body as the source: the point-mass block (B2) with μ = −K Cr (A/m). Within
    /// 1 m of the centre of the body the acceleration is zero, and so are the partials.
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        if (!TryGetOffsetFromEmitter(stateVector, out var bodyToSc))
            return;

        double areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;
        PointMassPartials.Accumulate(bodyToSc, -context.ReflectivityCoefficient * areaMassRatio * _thermalTerm, dadr);
    }

    /// <summary>
    /// The position of the spacecraft relative to the emitting body; false within 1 m of its centre, where the
    /// acceleration is set to zero. Shared by the acceleration and its partials.
    /// </summary>
    private bool TryGetOffsetFromEmitter(StateVector stateVector, out Vector3 bodyToSc)
    {
        // Geometric emitting body position relative to the observer
        var bodyFromObserver = GeometricPosition(_emittingBody, stateVector);

        var scPosition = stateVector.ToStateVector().Position;

        // Vector from body center to spacecraft
        bodyToSc = scPosition - bodyFromObserver;

        return bodyToSc.Magnitude() >= 1.0; // Avoid singularity
    }
}
