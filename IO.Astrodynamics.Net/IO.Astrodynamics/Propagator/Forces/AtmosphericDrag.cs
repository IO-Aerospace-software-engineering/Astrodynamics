using System;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Forces;

public class AtmosphericDrag : ForceBase
{
    private readonly Spacecraft _spacecraft;
    private readonly CelestialBody _celestialBody;

    public AtmosphericDrag(Spacecraft spacecraft, CelestialBody celestialBody)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _celestialBody = celestialBody ?? throw new ArgumentNullException(nameof(celestialBody));
        if (!_celestialBody.HasAtmosphericModel)
        {
            throw new ArgumentException($"The celestial body {_celestialBody.Name} does not have an atmospheric model.");
        }
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        // Get body-centered state vector (position and velocity relative to the celestial body)
        var bodyCentered = stateVector.RelativeTo(_celestialBody, Aberration.None).ToStateVector();

        // Get the angular velocity of the body's rotation in ICRF
        var omega = _celestialBody.GetOrientation(Frame.ICRF, stateVector.Epoch).AngularVelocity;

        // Compute atmosphere-relative velocity: v_rel = v_body_centered - omega x r_body_centered
        var vRel = bodyCentered.Velocity - omega.Cross(bodyCentered.Position);

        // Compute planetodetic coordinates for density lookup
        var planetodetic = bodyCentered.ToPlanetocentric(Aberration.None)
            .ToPlanetodetic(_celestialBody.Flattening, _celestialBody.EquatorialRadius);

        // Create rich atmospheric context with time and position for complex models
        var context = AtmosphericContext.FromPlanetodetic(
            planetodetic.Altitude,
            planetodetic.Latitude,
            planetodetic.Longitude,
            stateVector.Epoch
        );

        var density = _celestialBody.GetAirDensity(context);
        var areaMassRatio = _spacecraft.SectionalArea / _spacecraft.GetTotalMass();
        return vRel * -0.5 * density * areaMassRatio * _spacecraft.DragCoefficient * vRel.Magnitude();
    }
}
