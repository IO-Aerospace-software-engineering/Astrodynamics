// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// An impulse fixed in epoch and in ΔV: the open-loop model of Φ (no saltation matrix). Its g-function is the time
/// to the burn epoch, so the burn is located at that epoch whatever the trajectory.
/// </summary>
internal sealed class FixedImpulse : ImpulseManeuver
{
    private readonly TimeSystem.Time _burnEpoch;
    private readonly Vector3 _deltaV;

    internal FixedImpulse(CelestialItem center, TimeSystem.Time minimumEpoch, TimeSystem.Time burnEpoch, Vector3 deltaV,
        Engine engine) : base(center, minimumEpoch, TimeSpan.Zero, engine)
    {
        _burnEpoch = burnEpoch;
        _deltaV = deltaV;
    }

    public override double ComputeEventValue(StateVector localState) => (localState.Epoch - _burnEpoch).TotalSeconds;

    public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

    protected override Vector3 Execute(StateVector stateVector) => _deltaV;
}
