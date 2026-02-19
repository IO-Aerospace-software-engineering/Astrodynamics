// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude target that computes direction toward a celestial body or site.
/// Wraps <see cref="ILocalizable"/> for use with the <see cref="IAttitudeTarget"/> interface.
/// </summary>
public class CelestialAttitudeTarget : IAttitudeTarget
{
    public ILocalizable Target { get; }

    public string Name => (Target as INaifObject)?.Name ?? "CelestialTarget";

    public CelestialAttitudeTarget(ILocalizable target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public Vector3 GetDirection(StateVector observerState)
    {
        if (observerState == null) throw new ArgumentNullException(nameof(observerState));

        var ephemeris = Target.GetEphemeris(observerState.Epoch, observerState.Observer, observerState.Frame, Aberration.LT);
        return (ephemeris.ToStateVector().Position - observerState.Position).Normalize();
    }
}
