using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// GravitationalAcceleration force from given celestial body
/// </summary>
public class GravitationalAcceleration : ForceBase
{
    public CelestialItem CelestialItem { get; }

    public GravitationalAcceleration(CelestialItem celestialItem)
    {
        CelestialItem = celestialItem;
    }

    /// <summary>
    /// Evaluate gravitational acceleration at given stateVector
    /// </summary>
    /// <param name="stateVector"></param>
    /// <returns></returns>
    public override Vector3 Apply(StateVector stateVector)
    {
        // In SSB mode, the state vector observer is SSB, not the body.
        // Use cache to avoid SPICE call for body-relative conversion.
        if (EphemerisCache != null && stateVector.Observer as CelestialItem != CelestialItem
            && EphemerisCache.Contains(CelestialItem.NaifId, Aberration.None))
        {
            var (bodyPos, bodyVel) = EphemerisCache.GetState(CelestialItem.NaifId, Aberration.None, stateVector.Epoch);
            var relSv = new StateVector(
                stateVector.Position - bodyPos,
                stateVector.Velocity - bodyVel,
                CelestialItem, stateVector.Epoch, stateVector.Frame);
            return CelestialItem.EvaluateGravitationalAcceleration(relSv);
        }

        return CelestialItem.EvaluateGravitationalAcceleration(stateVector);
    }
}