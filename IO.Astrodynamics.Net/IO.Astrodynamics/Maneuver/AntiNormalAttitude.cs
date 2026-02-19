using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// Attitude maneuver that orients the spacecraft body front axis along the anti-normal direction (-h = -(r x v)).
/// </summary>
public class AntiNormalAttitude : Attitude
{
    public AntiNormalAttitude(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine)
        : base(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
    {
    }

    protected override Quaternion ComputeOrientation(StateVector stateVector)
    {
        var normal = stateVector.Position.Cross(stateVector.Velocity);
        return GetBodyFront().To(normal.Inverse());
    }
}
