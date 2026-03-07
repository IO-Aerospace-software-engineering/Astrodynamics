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

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Central-body-centered propagator for missions around any celestial body (LEO, GEO, lunar, interplanetary, etc.).
/// The central body is inferred from the spacecraft's initial orbital parameters observer.
/// When the observer is a Star or Barycenter, the Sun is used as the central body.
/// </summary>
public class CentralBodyPropagator : PropagatorBase
{
    private readonly CelestialItem _centralBody;

    /// <summary>
    /// Instantiate central-body propagator with a custom integrator.
    /// Forces (gravity, third-body perturbations, drag, SRP) are built automatically from the
    /// provided celestial bodies and flags.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> celestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT)
    {
        var items = (celestialBodies ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildCentralBodyForces(_centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        InjectEphemerisCache(Window, forces, items, _centralBody);
        InjectFrameOrientationCache(Window, _centralBody);
        foreach (var force in forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(InitialState);
    }

    /// <summary>
    /// Instantiate central-body propagator with a custom integrator that already has forces configured.
    /// No forces are added; the integrator is used as-is.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// When the observer is a Star or Barycenter, the Sun is used as the central body.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
        : base(window, spacecraft, integrator, deltaT)
    {
        _centralBody = ResolveCentralBody(spacecraft);

        InitialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector()
            .RelativeTo(_centralBody, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// Instantiate central-body propagator with default Velocity-Verlet integrator.
    /// Forces (gravity, third-body perturbations, drag, SRP) are built automatically from the
    /// provided celestial bodies and flags.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> celestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft,
            CreateDefaultIntegrator(window, spacecraft, celestialBodies,
                includeAtmosphericDrag, includeSolarRadiationPressure, deltaT),
            deltaT)
    {
    }

    /// <summary>
    /// Resolves the central body from the spacecraft's observer.
    /// For Star or Barycenter observers, returns the Sun.
    /// </summary>
    private static CelestialItem ResolveCentralBody(Spacecraft spacecraft)
    {
        var observer = spacecraft.InitialOrbitalParameters.Observer;

        if (observer is Star or Barycenter)
        {
            return Stars.SUN_BODY;
        }

        return observer as CelestialItem
               ?? throw new ArgumentException(
                   "Spacecraft initial orbital parameters observer must be a CelestialItem",
                   nameof(spacecraft));
    }

    private static IIntegrator CreateDefaultIntegrator(Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> celestialItems, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
    {
        var centralBody = ResolveCentralBody(spacecraft);

        var tdbStartDate = window.StartDate.ToTDB();
        var initialState = spacecraft.InitialOrbitalParameters.AtEpoch(tdbStartDate).ToStateVector()
            .RelativeTo(centralBody, Aberration.None).ToStateVector();
        var items = (celestialItems ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildCentralBodyForces(centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        var tdbWindow = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        InjectEphemerisCache(tdbWindow, forces, items, centralBody);
        InjectFrameOrientationCache(tdbWindow, centralBody);
        return new VVIntegrator(forces, deltaT, initialState);
    }

    protected static List<ForceBase> BuildCentralBodyForces(CelestialItem centralBody,
        CelestialItem[] items, Spacecraft spacecraft,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure)
    {
        List<ForceBase> forces = new List<ForceBase>();

        // Central body gravity (two-body + geopotential if configured)
        forces.Add(new GravitationalAcceleration(centralBody));

        // Third-body perturbations via Battin's formula for all other bodies
        foreach (var body in items)
        {
            if (!body.Equals(centralBody))
            {
                forces.Add(new ThirdBodyPerturbation(body, centralBody));
            }
        }

        if (includeAtmosphericDrag)
        {
            foreach (var celestialBody in items.OfType<CelestialBody>().Where(x => x.HasAtmosphericModel).ToArray())
            {
                forces.Add(new AtmosphericDrag(spacecraft, celestialBody));
            }
        }

        if (includeSolarRadiationPressure)
        {
            forces.Add(new SolarRadiationPressure(spacecraft, items.OfType<CelestialBody>()));
        }

        return forces;
    }

    private static void InjectFrameOrientationCache(Window tdbWindow, CelestialItem centralBody)
    {
        if (centralBody is not CelestialBody celestialBody) return;

        var cache = new PropagationFrameOrientationCache(
            tdbWindow, celestialBody.Frame, TimeSpan.FromSeconds(60));
        celestialBody.Frame.OrientationCache = cache;
    }

    private static void InjectEphemerisCache(Window tdbWindow, List<ForceBase> forces,
        CelestialItem[] celestialBodies, ILocalizable observer)
    {
        if (celestialBodies.Length == 0) return;

        var entries = new List<(CelestialItem, Aberration)>();
        foreach (var body in celestialBodies)
        {
            entries.Add((body, Aberration.None));
            entries.Add((body, Aberration.LT));
        }

        var cache = new PropagationEphemerisCache(tdbWindow, entries, observer, TimeSpan.FromSeconds(60));
        foreach (var force in forces)
        {
            force.EphemerisCache = cache;
        }
    }

    protected override void StorePropagatedStates(StateVector[] outputStates)
    {
        // Store CB-relative states directly; conversion to SSB happens on demand
        Spacecraft.AddPropagatedStates(outputStates);

        // Clear frame orientation cache to avoid stale data on shared Frame instances
        if (_centralBody is CelestialBody cb)
        {
            cb.Frame.OrientationCache = null;
        }
    }
}
