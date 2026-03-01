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
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Solar System Barycenter (SSB) centered propagator for interplanetary travel.
/// All states are propagated relative to the SSB using direct N-body gravitational acceleration.
/// </summary>
public class SsbPropagator : PropagatorBase
{
    /// <summary>
    /// Instantiate SSB propagator with a custom integrator.
    /// Forces are built automatically from the provided celestial bodies and flags,
    /// then added to the integrator via <see cref="Integrator.AddForce"/>.
    /// </summary>
    public SsbPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT)
    {
        var items = (additionalCelestialBodies ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildForces(items, spacecraft, includeAtmosphericDrag, includeSolarRadiationPressure);
        InjectEphemerisCache(Window, forces, items);
        foreach (var force in forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(SvCache[0]);
    }

    /// <summary>
    /// Instantiate SSB propagator with a custom integrator that already has forces configured.
    /// No forces are added; the integrator is used as-is.
    /// </summary>
    public SsbPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
        : base(window, spacecraft, integrator, deltaT)
    {
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector()
            .RelativeTo(ssb, Aberration.None).ToStateVector();
        InitializeCache(initialState);
    }

    /// <summary>
    /// Instantiate SSB propagator with default Velocity-Verlet integrator.
    /// Forces are built automatically from the provided celestial bodies and flags.
    /// </summary>
    public SsbPropagator(in Window window, Spacecraft spacecraft, IEnumerable<CelestialItem> additionalCelestialBodies,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft,
            CreateDefaultIntegrator(window, spacecraft, additionalCelestialBodies, includeAtmosphericDrag,
                includeSolarRadiationPressure, deltaT),
            deltaT)
    {
    }

    private static IIntegrator CreateDefaultIntegrator(Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> celestialItems, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
    {
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var tdbStartDate = window.StartDate.ToTDB();
        var initialState = spacecraft.InitialOrbitalParameters.AtEpoch(tdbStartDate).ToStateVector()
            .RelativeTo(ssb, Aberration.None).ToStateVector();
        var items = (celestialItems ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildForces(items, spacecraft, includeAtmosphericDrag, includeSolarRadiationPressure);
        var tdbWindow = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        InjectEphemerisCache(tdbWindow, forces, items);
        return new VVIntegrator(forces, deltaT, initialState);
    }

    private static List<ForceBase> BuildForces(CelestialItem[] celestialItems, Spacecraft spacecraft,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure)
    {
        List<ForceBase> forces = new List<ForceBase>();
        foreach (var celestialBody in celestialItems)
        {
            forces.Add(new GravitationalAcceleration(celestialBody));
        }

        if (includeAtmosphericDrag)
        {
            foreach (var celestialBody in celestialItems.OfType<CelestialBody>().Where(x => x.HasAtmosphericModel)
                         .ToArray())
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

    private static void InjectEphemerisCache(Window tdbWindow, List<ForceBase> forces,
        CelestialItem[] celestialBodies)
    {
        if (celestialBodies.Length == 0) return;

        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var entries = new List<(CelestialItem, Aberration)>();
        foreach (var body in celestialBodies)
        {
            entries.Add((body, Aberration.None));
            entries.Add((body, Aberration.LT));
        }

        var cache = new PropagationEphemerisCache(tdbWindow, entries, ssb, TimeSpan.FromSeconds(60));
        foreach (var force in forces)
        {
            force.EphemerisCache = cache;
        }
    }

    protected override void StorePropagatedStates()
    {
        Spacecraft.AddStateVectorRelativeToICRF(SvCache);
    }
}
