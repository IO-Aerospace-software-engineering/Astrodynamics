// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

public abstract class Integrator
{
    public IReadOnlyCollection<ForceBase> Forces { get; }

    public abstract void Integrate(StateVector[] result, int idx);

    public Integrator(IEnumerable<ForceBase> forces)
    {
        Forces = new List<ForceBase>(forces);
    }

    public Vector3 ComputeAcceleration(StateVector stateVector)
    {
        Vector3 res = Vector3.Zero;
        foreach (var force in Forces)
        {
            res += force.Apply(stateVector);
        }

        return res;
    }
}