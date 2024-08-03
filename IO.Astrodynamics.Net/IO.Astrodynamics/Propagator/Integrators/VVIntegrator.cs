// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

public sealed class VVIntegrator : Integrator
{
    public TimeSpan DeltaT { get; }
    public double DeltaTs { get; }
    public double HalfDeltaTs { get; }
    private Vector3 _acceleration;
    private Vector3 _position;
    private Vector3 _velocity;

    public VVIntegrator(IEnumerable<ForceBase> forces, TimeSpan deltaT, StateVector initialState) : base(forces)
    {
        DeltaT = deltaT;
        DeltaTs = DeltaT.TotalSeconds;
        HalfDeltaTs = DeltaTs * 0.5;
        if (_acceleration == Vector3.Zero)
        {
            _acceleration = ComputeAcceleration(initialState);
        }
    }

    public override void Integrate(StateVector[] result, int idx)
    {
        //Set initial parameters
        var previousElement = result[idx - 1];
        _position = previousElement.Position;
        _velocity = previousElement.Velocity;

        result[idx].UpdateVelocity(_velocity + _acceleration * HalfDeltaTs);
        result[idx].UpdatePosition(_position + result[idx].Velocity * DeltaTs);
        _acceleration = ComputeAcceleration(result[idx]);

        result[idx].UpdateVelocity(result[idx].Velocity + _acceleration * HalfDeltaTs);
    }
}