// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

public class ZenithAttitude : Attitude
{
    public ZenithAttitude(CelestialItem maneuverCenter,Time minimumEpoch, TimeSpan maneuverHoldDuration) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration)
    {
    }

    public override Quaternion ComputeOrientation(StateVector stateVector)
    {
        return GetBodyAxis(BodyAxis.Front).ToOrIdentity(stateVector.Position);
    }
}