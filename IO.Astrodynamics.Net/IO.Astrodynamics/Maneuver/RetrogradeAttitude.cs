// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver;

public class RetrogradeAttitude : Attitude
{
    public RetrogradeAttitude(CelestialItem maneuverCenter, DateTime minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
    {
    }

    protected override Quaternion ComputeOrientation(StateVector stateVector)
    {
        return Spacecraft.Front.To(stateVector.Velocity.Inverse());
    }
}