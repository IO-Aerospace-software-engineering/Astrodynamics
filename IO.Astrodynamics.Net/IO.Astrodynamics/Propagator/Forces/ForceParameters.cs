// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// The force model parameters an acceleration depends on, for the parameter partials.
/// </summary>
[Flags]
internal enum ForceParameters
{
    /// <summary>No parameter.</summary>
    None = 0,

    /// <summary>Drag coefficient Cd (<see cref="ForceEvaluationContext.DragCoefficient"/>).</summary>
    DragCoefficient = 1,

    /// <summary>Reflectivity coefficient Cr (<see cref="ForceEvaluationContext.ReflectivityCoefficient"/>).</summary>
    ReflectivityCoefficient = 2
}
