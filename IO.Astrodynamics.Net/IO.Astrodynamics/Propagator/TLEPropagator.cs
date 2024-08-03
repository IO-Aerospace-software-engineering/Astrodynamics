using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Propagator;

public class TLEPropagator : IPropagator
{
    private uint _svCacheSize;
    private StateVector[] _svCache;
    private Dictionary<DateTime, StateOrientation> _stateOrientation = new Dictionary<DateTime, StateOrientation>();
    public Window Window { get; }
    public Spacecraft Spacecraft { get; }
    public TimeSpan DeltaT { get; }

    public TLEPropagator(Window window, Spacecraft spacecraft, TimeSpan deltaT)
    {
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        if (Spacecraft.InitialOrbitalParameters is not TLE)
        {
            throw new ArgumentException("Spacecraft orbital parameters must be a two lines elements");
        }

        DeltaT = deltaT;
        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector();

        _svCacheSize = (uint)Window.Length.TotalSeconds / (uint)DeltaT.TotalSeconds + 1;
        _svCache = new StateVector[_svCacheSize];
        _svCache[0] = initialState;
    }

    public (IEnumerable<StateVector> stateVectors, IEnumerable<StateOrientation> stateOrientations) Propagate()
    {
        _stateOrientation[Window.StartDate] = new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.StartDate, Spacecraft.InitialOrbitalParameters.Frame);
        for (int i = 1; i < _svCacheSize; i++)
        {
            _svCache[i] = Spacecraft.InitialOrbitalParameters.ToStateVector(Window.StartDate.Add(DeltaT * i));
        }

        _stateOrientation[Window.EndDate] = new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.EndDate, Spacecraft.InitialOrbitalParameters.Frame);
        return (_svCache, _stateOrientation.Values);
    }
}