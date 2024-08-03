using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Propagator;

public interface IPropagator
{
    Window Window { get; }
    Spacecraft Spacecraft { get; }
    TimeSpan DeltaT { get; }

    /// <summary>
    /// Propagate spacecraft
    /// </summary>
    /// <returns></returns>
    (IEnumerable<StateVector>stateVectors, IEnumerable<StateOrientation>stateOrientations) Propagate();
}