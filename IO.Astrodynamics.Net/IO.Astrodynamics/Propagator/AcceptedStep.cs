// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Represents one accepted integration step, storing the start and end states
/// plus accelerations for cubic Hermite interpolation.
/// </summary>
public readonly record struct AcceptedStep(
    double CumulativeTime,
    double StepSize,
    Vector3 StartPosition,
    Vector3 StartVelocity,
    Vector3 EndPosition,
    Vector3 EndVelocity,
    Vector3 StartAcceleration,
    Vector3 EndAcceleration);
