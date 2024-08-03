// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

public abstract class ForceBase
{
    public abstract Vector3 Apply(StateVector stateVector);
}