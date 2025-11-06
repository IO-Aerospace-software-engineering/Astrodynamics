using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Forces;

public class AtmosphericDrag : ForceBase
{
    private readonly Spacecraft _spacecraft;
    private readonly CelestialBody _celestialBody;
    private readonly double _areaMassRatio;
    private readonly double _dragCoefficient;

    public AtmosphericDrag(Spacecraft spacecraft, CelestialBody celestialBody)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _celestialBody = celestialBody ?? throw new ArgumentNullException(nameof(celestialBody));
        if (!_celestialBody.HasAtmosphericModel)
        {
            throw new ArgumentException($"The celestial body {_celestialBody.Name} does not have an atmospheric model.");
        }

        _areaMassRatio = _spacecraft.SectionalArea / _spacecraft.Mass;
        _dragCoefficient = _spacecraft.DragCoefficient;
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        var planetodetic = stateVector.RelativeTo(_celestialBody, Aberration.None).ToPlanetocentric(Aberration.None)
            .ToPlanetodetic(_celestialBody!.Flattening, _celestialBody.EquatorialRadius);
        var density = _celestialBody.GetAirDensity(planetodetic.Altitude);

        // Cache velocity to avoid accessing stateVector.Velocity multiple times
        var velocity = stateVector.Velocity;
        var velocityMagnitude = velocity.Magnitude();

        // Formula: F_drag = -0.5 * rho * (A/m) * C_d * v * |v|
        // Match exact operation order for numerical precision
        return velocity * -0.5 * density * _areaMassRatio * _dragCoefficient * velocityMagnitude;
    }
}