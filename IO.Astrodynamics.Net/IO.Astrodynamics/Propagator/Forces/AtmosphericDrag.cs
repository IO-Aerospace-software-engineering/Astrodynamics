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

    public AtmosphericDrag(Spacecraft spacecraft, CelestialBody celestialBody)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _celestialBody = celestialBody ?? throw new ArgumentNullException(nameof(celestialBody));
        _areaMassRatio = _spacecraft.SectionalArea / _spacecraft.Mass;
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        var planetodetic = stateVector.RelativeTo(_celestialBody,Aberration.None).ToPlanetocentric(Aberration.None).ToPlanetodetic(_celestialBody!.Flattening, _celestialBody.EquatorialRadius);
        var density = _celestialBody.GetAirDensity(planetodetic.Altitude);
        return stateVector.Velocity * -0.5 * density * _areaMassRatio * _spacecraft.DragCoefficient * stateVector.Velocity.Magnitude();
    }
}