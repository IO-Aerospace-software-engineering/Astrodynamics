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
    private readonly double _cr;
    private readonly double _term1;
    private readonly CelestialBody[] _occultingBodies;

    public SolarRadiationPressure(Spacecraft spacecraft, IEnumerable<CelestialBody> occultingBodies)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _occultingBodies = (occultingBodies ?? throw new ArgumentNullException(nameof(occultingBodies))).ToArray();
        _cr = spacecraft.SolarRadiationCoeff;
        _term1 = Constants.SolarMeanRadiativeLuminosity / (4.0 * System.Math.PI * Constants.C);
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        double maxShadowFraction = 0.0;
        foreach (var occultingBody in _occultingBodies)
        {
            var fraction = _sun.ShadowFraction(occultingBody, stateVector);
            if (fraction >= 1.0)
            {
                return Vector3.Zero;
            }

            if (fraction > maxShadowFraction)
            {
                maxShadowFraction = fraction;
            }
        }

        var position = stateVector.RelativeTo(_sun, Aberration.LT).ToStateVector().Position;
        var term2 = position / System.Math.Pow(position.Magnitude(), 3.0);
        var areaMassRatio = _spacecraft.SectionalArea / _spacecraft.GetTotalMass();
        return term2 * _term1 * areaMassRatio * _cr * (1.0 - maxShadowFraction);
    }
}
