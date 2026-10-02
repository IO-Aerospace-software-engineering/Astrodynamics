// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

/// <summary>
/// Groups a position vector and a velocity vector into a single value,
/// used for per-component statistics such as standard deviation, minimum, and maximum.
/// </summary>
/// <param name="Position">Position component (m).</param>
/// <param name="Velocity">Velocity component (m/s).</param>
public readonly record struct StateComponents(Vector3 Position, Vector3 Velocity);
