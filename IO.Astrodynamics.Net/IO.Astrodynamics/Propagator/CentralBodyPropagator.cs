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
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Central-body-centered propagator for missions around a celestial body (LEO, GEO, lunar, etc.).
/// The central body is inferred from the spacecraft's initial orbital parameters observer.
/// </summary>
public class CentralBodyPropagator : PropagatorBase
{
    private readonly CelestialItem _centralBody;

    /// <summary>
    /// Instantiate central-body propagator with a custom integrator.
    /// Forces are built automatically from the provided third-body perturbers and flags.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT)
    {
        var items = (additionalCelestialBodies ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildCentralBodyForces(_centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        InjectEphemerisCache(Window, forces, items, _centralBody);
        foreach (var force in forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(SvCache[0]);
    }

    /// <summary>
    /// Instantiate central-body propagator with a custom integrator that already has forces configured.
    /// No forces are added; the integrator is used as-is.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
        : base(window, spacecraft, integrator, deltaT)
    {
        _centralBody = spacecraft.InitialOrbitalParameters.Observer as CelestialItem
                       ?? throw new ArgumentException(
                           "Spacecraft initial orbital parameters observer must be a CelestialItem",
                           nameof(spacecraft));

        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector()
            .RelativeTo(_centralBody, Aberration.None).ToStateVector();
        InitializeCache(initialState);
    }

    /// <summary>
    /// Instantiate central-body propagator with default Velocity-Verlet integrator.
    /// Forces are built automatically from the provided third-body perturbers and flags.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft,
            CreateDefaultIntegrator(window, spacecraft, additionalCelestialBodies,
                includeAtmosphericDrag, includeSolarRadiationPressure, deltaT),
            deltaT)
    {
    }

    private static IIntegrator CreateDefaultIntegrator(Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> celestialItems, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
    {
        var centralBody = spacecraft.InitialOrbitalParameters.Observer as CelestialItem
                          ?? throw new ArgumentException(
                              "Spacecraft initial orbital parameters observer must be a CelestialItem",
                              nameof(spacecraft));

        var tdbStartDate = window.StartDate.ToTDB();
        var initialState = spacecraft.InitialOrbitalParameters.AtEpoch(tdbStartDate).ToStateVector()
            .RelativeTo(centralBody, Aberration.None).ToStateVector();
        var items = (celestialItems ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildCentralBodyForces(centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        var tdbWindow = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        InjectEphemerisCache(tdbWindow, forces, items, centralBody);
        return new VVIntegrator(forces, deltaT, initialState);
    }

    private static List<ForceBase> BuildCentralBodyForces(CelestialItem centralBody,
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

    protected override void StorePropagatedStates()
    {
        // Central-body mode: convert propagated states to SSB-relative for storage
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var ssbStates = new StateVector[SvCacheSize];
        for (int i = 0; i < SvCacheSize; i++)
        {
            var cbSv = SvCache[i];
            var cbStateFromSsb = _centralBody.GetGeometricStateFromICRF(cbSv.Epoch).ToStateVector();
            ssbStates[i] = new StateVector(
                cbStateFromSsb.Position + cbSv.Position,
                cbStateFromSsb.Velocity + cbSv.Velocity,
                ssb, cbSv.Epoch, Frame.ICRF);
        }

        Spacecraft.AddStateVectorRelativeToICRF(ssbStates);
    }
}
