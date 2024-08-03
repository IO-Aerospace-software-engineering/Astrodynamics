using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

public class SolarRadiationPressure : ForceBase
{
    private readonly CelestialBody _sun = new CelestialBody(10);
    private readonly double _areaMassRatio;
    private readonly double _term1;

    public SolarRadiationPressure(Spacecraft spacecraft)
    {
        _areaMassRatio = spacecraft.SectionalArea / spacecraft.Mass;
        _term1 = Constants.SolarMeanRadiativeLuminosity / (4.0 * System.Math.PI * Constants.C);
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        if (_sun.IsOcculted(stateVector.Observer as CelestialItem, stateVector) == OccultationType.Full)
        {
            return Vector3.Zero;
        }

        var position = stateVector.RelativeTo(_sun, Aberration.LT).ToStateVector().Position;
        var term2 = position / System.Math.Pow(position.Magnitude(), 3.0);
        return term2 * _term1 * _areaMassRatio;
    }
}