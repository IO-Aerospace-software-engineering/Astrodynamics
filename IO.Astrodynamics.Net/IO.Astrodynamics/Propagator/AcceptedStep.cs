// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Represents one accepted integration step, storing the start and end states
/// plus accelerations for cubic Hermite interpolation.
/// </summary>
/// <remarks>
/// <see cref="CumulativeTime"/>, the start of the step from the base epoch of its segment, and <see cref="StepSize"/>
/// are in seconds of the time argument of the propagation: TT seconds about the Earth, TDB seconds about any other
/// central body (#501). The velocities and accelerations are per second of that time argument.
/// </remarks>
public readonly record struct AcceptedStep(
    double CumulativeTime,
    double StepSize,
    Vector3 StartPosition,
    Vector3 StartVelocity,
    Vector3 EndPosition,
    Vector3 EndVelocity,
    Vector3 StartAcceleration,
    Vector3 EndAcceleration);
