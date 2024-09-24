using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

public interface IPropagator : IDisposable
{
    Window Window { get; }
    Spacecraft Spacecraft { get; }
    TimeSpan DeltaT { get; }

    /// <summary>
    /// Propagate spacecraft
    /// </summary>
    /// <returns></returns>
    void Propagate();
}