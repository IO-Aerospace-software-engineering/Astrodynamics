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
        return CelestialItem.EvaluateGravitationalAcceleration(stateVector);
    }
}