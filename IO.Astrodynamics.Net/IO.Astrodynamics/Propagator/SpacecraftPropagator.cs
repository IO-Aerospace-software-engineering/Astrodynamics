// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
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
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        if (spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.ICRF
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.FK4
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.ECLIPTIC_J2000
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.ECLIPTIC_B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.GALACTIC_SYSTEM2)
        {
            throw new ArgumentException("Spacecraft initial orbital parameters must be defined in inertial frame", nameof(spacecraft));
        }

        _originalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        CelestialItems = additionalCelestialBodies ?? Array.Empty<CelestialItem>();
        IncludeAtmosphericDrag = includeAtmosphericDrag;
        IncludeSolarRadiationPressure = includeSolarRadiationPressure;
        DeltaT = deltaT;

        var forces = InitializeForces(IncludeAtmosphericDrag, IncludeSolarRadiationPressure);

        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector().RelativeTo(ssb, Aberration.None).ToStateVector();
        Integrator = new VVIntegrator(forces, DeltaT, initialState);

        _svCacheSize = (uint)System.Math.Round(Window.Length.TotalSeconds / DeltaT.TotalSeconds, MidpointRounding.AwayFromZero) + 1;
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
            foreach (var celestialBody in CelestialItems.OfType<CelestialBody>().Where(x => x.HasAtmosphericModel).ToArray())
            {
                forces.Add(new AtmosphericDrag(Spacecraft, celestialBody));
            }
        }

        if (includeSolarRadiationPressure)
        {
            forces.Add(new SolarRadiationPressure(Spacecraft, CelestialItems.OfType<CelestialBody>()));
        }

        return forces;
    }

    /// <summary>
    /// Propagate spacecraft
    /// </summary>
    /// <returns></returns>
    public void Propagate()
    {
        Spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.StartDate, Spacecraft.InitialOrbitalParameters.Frame));
        for (int i = 0; i < _svCacheSize - 1; i++)
        {
            var prvSv = _svCache[i];
            if (Spacecraft.StandbyManeuver?.CanExecute(prvSv) == true)
            {
                var res = Spacecraft.StandbyManeuver.TryExecute(prvSv);
                Spacecraft.Frame.AddStateOrientationToICRF(res.so);
            }

            Integrator.Integrate(_svCache, i + 1);
        }

        var latestOrientation = Spacecraft.Frame.GetLatestStateOrientationToICRF();
        Spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(latestOrientation.Rotation, latestOrientation.AngularVelocity, Window.EndDate,
            latestOrientation.ReferenceFrame));

        Spacecraft.AddStateVectorRelativeToICRF(_svCache);
        
        //Before return result state vectors must be converted back to original observer
        // return (Array.Empty<StateVector>(),
        //     Spacecraft.Frame.GetStateOrientationsToICRF().OrderBy(x => x.Epoch)); //Return spacecraft frames
    }

    public void Dispose()
    {
    }
}