// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body.Spacecraft;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Result of a single propagation task within a batch.
/// Contains the spacecraft, its solution (if successful), or the exception (if failed).
/// </summary>
public sealed record PropagationResult(
    Spacecraft Spacecraft,
    PropagationSolution Solution,
    Exception Error)
{
    /// <summary>
    /// True when the propagation completed without error.
    /// </summary>
    public bool Succeeded => Error == null;
}
