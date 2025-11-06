// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

public abstract class Integrator
{
    private readonly ForceBase[] _forces;
    public IReadOnlyCollection<ForceBase> Forces { get; }

    public abstract void Integrate(StateVector[] result, int idx);

    public Integrator(IEnumerable<ForceBase> forces)
    {
        _forces = forces.ToArray();
        Forces = _forces;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ComputeAcceleration(StateVector stateVector)
    {
        Vector3 res = Vector3.Zero;
        // Use array-based iteration for better performance
        for (int i = 0; i < _forces.Length; i++)
        {
            res += _forces[i].Apply(stateVector);
        }

        return res;
    }
}