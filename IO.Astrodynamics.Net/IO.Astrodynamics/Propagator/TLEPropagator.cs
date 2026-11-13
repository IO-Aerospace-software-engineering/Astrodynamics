using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

public class TLEPropagator : IPropagator
{
    private uint _svCacheSize;
    private StateVector[] _svCache;
    private Dictionary<Time, StateOrientation> _stateOrientation = new Dictionary<Time, StateOrientation>();
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

    public PropagationSolution Propagate()
    {
        _stateOrientation[Window.StartDate] = new StateOrientation(Quaternion.Identity, Vector3.Zero, Window.StartDate, Frame.ICRF);
        for (int i = 1; i < _svCacheSize; i++)
        {
            _svCache[i] = Spacecraft.InitialOrbitalParameters.ToStateVector(Window.StartDate.Add(DeltaT * i));
        }

        _stateOrientation[Window.EndDate] = new StateOrientation(Quaternion.Identity, Vector3.Zero, Window.EndDate, Frame.ICRF);
        Spacecraft.AddPropagatedStates(_svCache);

        // Build synthetic PropagationSolution from TLE states, with the accelerations of SGP4 at the samples, which the cubic
        // Hermite of a step interpolates the velocity with (#675: zero before, up to 49.9 m/s off SGP4 between samples)
        var accelerations = new Vector3[_svCacheSize];
        for (int i = 0; i < _svCacheSize; i++)
        {
            accelerations[i] = Sgp4Acceleration(Window.StartDate.Add(DeltaT * i));
        }

        var solution = new PropagationSolution();
        var segment = new PropagationSegment(Window.StartDate, (int)_svCacheSize);

        for (int i = 0; i < _svCacheSize - 1; i++)
        {
            double stepSize = DeltaT.TotalSeconds;
            double cumulativeTime = i * stepSize;
            segment.AddStep(new AcceptedStep(
                cumulativeTime, stepSize,
                _svCache[i].Position, _svCache[i].Velocity,
                _svCache[i + 1].Position, _svCache[i + 1].Velocity,
                accelerations[i], accelerations[i + 1]));
        }

        solution.AddSegment(segment);
        solution.SetOutputStates(_svCache);

        return solution;
    }
    // The half-width of the central difference of the SGP4 velocity, s
    private const double Sgp4DifferenceStep = 1.0;

    /// <summary>
    /// The acceleration of SGP4 at <paramref name="epoch"/>: the central difference of its velocity at ±1 s, whose
    /// truncation v‴·δ²/6 is about 1.9e-6 m/s² in LEO (1.7e-5 m/s over a step of 60 s); a difference over the neighbouring
    /// samples grows with the step squared (about 0.06 m/s at 60 s). The rotation of TEME of date over 2 s adds below
    /// 1e-7 m/s².
    /// </summary>
    private Vector3 Sgp4Acceleration(in Time epoch)
    {
        var after = Spacecraft.InitialOrbitalParameters.ToStateVector(epoch.AddSeconds(Sgp4DifferenceStep)).Velocity;
        var before = Spacecraft.InitialOrbitalParameters.ToStateVector(epoch.AddSeconds(-Sgp4DifferenceStep)).Velocity;
        return (after - before) / (2.0 * Sgp4DifferenceStep);
    }

    public void Dispose()
    {
        _svCache = null;
        _stateOrientation = null;
    }
}