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
    public Spacecraft Spacecraft { get; }
    public IIntegrator Integrator { get; }
    public TimeSpan DeltaT { get; }

    private uint _svCacheSize;
    private StateVector[] _svCache;

    /// <summary>
    /// Instantiate propagator with a custom integrator.
    /// Forces are built automatically from the provided celestial bodies and flags,
    /// then added to the integrator via <see cref="Integrator.AddForce"/>.
    /// </summary>
    /// <param name="window">Time window</param>
    /// <param name="spacecraft">Spacecraft to propagate</param>
    /// <param name="integrator">Numerical integrator (must extend Integrator base class)</param>
    /// <param name="additionalCelestialBodies">Celestial bodies for gravitational perturbations</param>
    /// <param name="includeAtmosphericDrag">Include atmospheric drag for bodies with atmospheric models</param>
    /// <param name="includeSolarRadiationPressure">Include solar radiation pressure with eclipse shadow</param>
    /// <param name="deltaT">Output step size</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public SpacecraftPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT)
    {
        var forces = BuildForces(additionalCelestialBodies ?? Array.Empty<CelestialItem>(), spacecraft,
            includeAtmosphericDrag, includeSolarRadiationPressure);
        foreach (var force in forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(_svCache[0]);
    }

    /// <summary>
    /// Instantiate propagator with a custom integrator that already has forces configured.
    /// No forces are added; the integrator is used as-is.
    /// </summary>
    /// <param name="window">Time window</param>
    /// <param name="spacecraft">Spacecraft to propagate</param>
    /// <param name="integrator">Numerical integrator with forces already configured</param>
    /// <param name="deltaT">Output step size</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public SpacecraftPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
    {
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        Integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
        ValidateInertialFrame(spacecraft);

        _originalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        DeltaT = deltaT;

        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector().RelativeTo(ssb, Aberration.None).ToStateVector();

        _svCacheSize = (uint)System.Math.Round(Window.Length.TotalSeconds / DeltaT.TotalSeconds, MidpointRounding.AwayFromZero) + 1;
        _svCache = new StateVector[_svCacheSize];
        _svCache[0] = initialState;
        for (int i = 1; i < _svCacheSize; i++)
        {
            _svCache[i] = new StateVector(Vector3.Zero, Vector3.Zero, initialState.Observer, Window.StartDate + (i * DeltaT), initialState.Frame);
        }
    }

    /// <summary>
    /// Instantiate propagator with default Velocity-Verlet integrator.
    /// Forces are built automatically from the provided celestial bodies and flags.
    /// </summary>
    /// <param name="window">Time window</param>
    /// <param name="spacecraft">Spacecraft to propagate</param>
    /// <param name="additionalCelestialBodies">Additional celestial bodies</param>
    /// <param name="includeAtmosphericDrag">Include atmospheric drag</param>
    /// <param name="includeSolarRadiationPressure">Include solar radiation pressure</param>
    /// <param name="deltaT">Simulation step size</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public SpacecraftPropagator(in Window window, Spacecraft spacecraft, IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft,
            CreateDefaultIntegrator(window, spacecraft, additionalCelestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, deltaT),
            deltaT)
    {
    }

    private static IIntegrator CreateDefaultIntegrator(Window window, Spacecraft spacecraft, IEnumerable<CelestialItem> celestialItems,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure, TimeSpan deltaT)
    {
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var tdbStartDate = window.StartDate.ToTDB();
        var initialState = spacecraft.InitialOrbitalParameters.AtEpoch(tdbStartDate).ToStateVector().RelativeTo(ssb, Aberration.None).ToStateVector();
        var forces = BuildForces(celestialItems ?? Array.Empty<CelestialItem>(), spacecraft, includeAtmosphericDrag, includeSolarRadiationPressure);
        return new VVIntegrator(forces, deltaT, initialState);
    }

    private static List<ForceBase> BuildForces(IEnumerable<CelestialItem> celestialItems, Spacecraft spacecraft,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure)
    {
        List<ForceBase> forces = new List<ForceBase>();
        foreach (var celestialBody in celestialItems.Distinct())
        {
            forces.Add(new GravitationalAcceleration(celestialBody));
        }

        if (includeAtmosphericDrag)
        {
            foreach (var celestialBody in celestialItems.OfType<CelestialBody>().Where(x => x.HasAtmosphericModel).ToArray())
            {
                forces.Add(new AtmosphericDrag(spacecraft, celestialBody));
            }
        }

        if (includeSolarRadiationPressure)
        {
            forces.Add(new SolarRadiationPressure(spacecraft, celestialItems.OfType<CelestialBody>()));
        }

        return forces;
    }

    private static void ValidateInertialFrame(Spacecraft spacecraft)
    {
        if (spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.ICRF
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.FK4
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.ECLIPTIC_J2000
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.ECLIPTIC_B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frames.Frame.GALACTIC_SYSTEM2)
        {
            throw new ArgumentException("Spacecraft initial orbital parameters must be defined in inertial frame", nameof(spacecraft));
        }
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
    }

    public void Dispose()
    {
    }
}
