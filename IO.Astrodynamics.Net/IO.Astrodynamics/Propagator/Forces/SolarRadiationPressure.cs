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
        // Get Sun position relative to the state vector observer
        Vector3 sunFromObserver;
        if (EphemerisCache != null && EphemerisCache.Contains(_sun.NaifId, Aberration.LT))
            sunFromObserver = EphemerisCache.GetPosition(_sun.NaifId, Aberration.LT, stateVector.Epoch);
        else
            sunFromObserver = _sun.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT)
                .ToStateVector().Position;

        var scPosition = stateVector.ToStateVector().Position;

        // Vectors from spacecraft to Sun and occluding bodies
        var scToSun = sunFromObserver - scPosition;
        double scToSunMag = scToSun.Magnitude();

        // Shadow fraction computation
        double maxShadowFraction = 0.0;
        double sunAngSize = _sun.AngularSize(scToSunMag);

        foreach (var occultingBody in _occultingBodies)
        {
            Vector3 occFromObserver;
            if (EphemerisCache != null && EphemerisCache.Contains(occultingBody.NaifId, Aberration.LT))
                occFromObserver = EphemerisCache.GetPosition(occultingBody.NaifId, Aberration.LT, stateVector.Epoch);
            else
                occFromObserver = occultingBody.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT)
                    .ToStateVector().Position;

            var scToOcc = occFromObserver - scPosition;
            double occAngSize = occultingBody.AngularSize(scToOcc.Magnitude());
            double angSep = scToSun.Normalize().Angle(scToOcc.Normalize());

            double fraction = CelestialItem.ShadowFraction(angSep, sunAngSize, occAngSize);
            if (fraction >= 1.0)
                return Vector3.Zero;

            if (fraction > maxShadowFraction)
                maxShadowFraction = fraction;
        }

        // SRP acceleration: spacecraft position relative to Sun
        var posRelToSun = scPosition - sunFromObserver;
        var term2 = posRelToSun / System.Math.Pow(posRelToSun.Magnitude(), 3.0);
        var areaMassRatio = _spacecraft.SectionalArea / _spacecraft.GetTotalMass();
        return term2 * _term1 * areaMassRatio * _cr * (1.0 - maxShadowFraction);
    }
}
