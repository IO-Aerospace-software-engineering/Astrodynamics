// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

public class SpacecraftPropagator : IPropagator
{
    private readonly CelestialItem _originalObserver;
    public Window Window { get; }
    public IEnumerable<CelestialItem> CelestialItems { get; }
    public bool IncludeAtmosphericDrag { get; }
    public bool IncludeSolarRadiationPressure { get; }
    public Spacecraft Spacecraft { get; }
    public Integrator Integrator { get; }
    public TimeSpan DeltaT { get; }

    private uint _svCacheSize;
    private StateVector[] _svCache;
    private Dictionary<DateTime, StateOrientation> _stateOrientation = new Dictionary<DateTime, StateOrientation>();


    /// <summary>
    /// Instantiate propagator
    /// </summary>
    /// <param name="window">Time window</param>
    /// <param name="spacecraft"></param>
    /// <param name="additionalCelestialBodies">Additional celestial bodies</param>
    /// <param name="includeAtmosphericDrag"></param>
    /// <param name="includeSolarRadiationPressure"></param>
    /// <param name="deltaT">Simulation step size</param>
    /// <exception cref="ArgumentNullException"></exception>
    public SpacecraftPropagator(in Window window, Spacecraft spacecraft, IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
    {
        var ssb = new Barycenter(Barycenters.SOLAR_SYSTEM_BARYCENTER.NaifId);
        _originalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        Window = new Window(window.StartDate.ToTDB(),window.EndDate.ToTDB());
        CelestialItems = additionalCelestialBodies ?? Array.Empty<CelestialItem>();
        IncludeAtmosphericDrag = includeAtmosphericDrag;
        IncludeSolarRadiationPressure = includeSolarRadiationPressure;
        DeltaT = deltaT;

        var forces = InitializeForces(IncludeAtmosphericDrag, IncludeSolarRadiationPressure);

        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector().RelativeTo(ssb, Aberration.None).ToStateVector();
        Integrator = new VVIntegrator(forces, DeltaT, initialState);

        _svCacheSize = (uint)Window.Length.TotalSeconds / (uint)DeltaT.TotalSeconds + 1;
        _svCache = new StateVector[_svCacheSize];
        _svCache[0] = initialState;
        for (int i = 1; i < _svCacheSize; i++)
        {
            _svCache[i] = new StateVector(Vector3.Zero, Vector3.Zero, initialState.Observer, Window.StartDate + (i * DeltaT), initialState.Frame);
        }
    }

    private List<ForceBase> InitializeForces(bool includeAtmosphericDrag, bool includeSolarRadiationPressure)
    {
        List<ForceBase> forces = new List<ForceBase>();
        foreach (var celestialBody in CelestialItems.Distinct())
        {
            forces.Add(new GravitationalAcceleration(celestialBody));
        }

        if (includeAtmosphericDrag)
        {
            if (_originalObserver is CelestialBody body)
            {
                forces.Add(new AtmosphericDrag(Spacecraft, body));
            }
        }

        if (includeSolarRadiationPressure)
        {
            forces.Add(new SolarRadiationPressure(Spacecraft));
        }

        return forces;
    }

    /// <summary>
    /// Propagate spacecraft
    /// </summary>
    /// <returns></returns>
    public (IEnumerable<StateVector>stateVectors, IEnumerable<StateOrientation>stateOrientations) Propagate()
    {
        _stateOrientation[_svCache.First().Epoch] = new StateOrientation(Quaternion.Zero, Vector3.Zero, _svCache.First().Epoch, Spacecraft.InitialOrbitalParameters.Frame);
        for (int i = 0; i < _svCacheSize - 1; i++)
        {
            var prvSv = _svCache[i];
            if (Spacecraft.StandbyManeuver?.CanExecute(prvSv) == true)
            {
                var res = Spacecraft.StandbyManeuver.TryExecute(prvSv);
                _stateOrientation[res.so.Epoch] = res.so;
            }

            Integrator.Integrate(_svCache, i + 1);
        }

        _stateOrientation[Window.EndDate] = new StateOrientation(_stateOrientation.Last().Value.Rotation, Vector3.Zero, Window.EndDate, Spacecraft.InitialOrbitalParameters.Frame);


        //Before return result statevectors must be converted back to original observer
        return (_svCache.Select(x => x.RelativeTo(_originalObserver, Aberration.None).ToStateVector()), _stateOrientation.Values);
    }
}