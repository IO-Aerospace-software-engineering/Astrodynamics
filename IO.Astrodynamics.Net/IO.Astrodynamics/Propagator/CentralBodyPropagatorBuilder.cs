// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Fluent builder for constructing a <see cref="CentralBodyPropagator"/> with its force models.
/// </summary>
public class CentralBodyPropagatorBuilder
{
    private readonly Window _window;
    private readonly Spacecraft _spacecraft;
    private readonly Integrator _integrator;
    private readonly TimeSpan _deltaT;

    private readonly List<CelestialItem> _perturbingBodies = new();

    private bool _includeAtmosphericDrag;
    private bool _includeSolarRadiationPressure;

    private CelestialBody _albedoBody;
    private CelestialBody _thermalBody;

    public CentralBodyPropagatorBuilder(Window window, Spacecraft spacecraft,
        Integrator integrator, TimeSpan deltaT)
    {
        _window = window;
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
        _deltaT = deltaT;
    }

    public CentralBodyPropagatorBuilder WithPerturbingBody(CelestialItem body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (_perturbingBodies.Contains(body))
            throw new InvalidOperationException($"Perturbing body '{body.Name}' already added");
        _perturbingBodies.Add(body);
        return this;
    }

    public CentralBodyPropagatorBuilder WithPerturbingBodies(IEnumerable<CelestialItem> bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        foreach (var body in bodies) WithPerturbingBody(body);
        return this;
    }

    public CentralBodyPropagatorBuilder IncludeAtmosphericDrag()
    {
        if (_includeAtmosphericDrag)
            throw new InvalidOperationException("IncludeAtmosphericDrag already called");
        _includeAtmosphericDrag = true;
        return this;
    }

    public CentralBodyPropagatorBuilder IncludeSolarRadiationPressure()
    {
        if (_includeSolarRadiationPressure)
            throw new InvalidOperationException("IncludeSolarRadiationPressure already called");
        _includeSolarRadiationPressure = true;
        return this;
    }

    public CentralBodyPropagatorBuilder IncludeAlbedo(CelestialBody reflectingBody)
    {
        if (_albedoBody != null)
            throw new InvalidOperationException("IncludeAlbedo already called");
        _albedoBody = reflectingBody ?? throw new ArgumentNullException(nameof(reflectingBody));
        return this;
    }

    public CentralBodyPropagatorBuilder IncludeThermalRadiation(CelestialBody emittingBody)
    {
        if (_thermalBody != null)
            throw new InvalidOperationException("IncludeThermalRadiation already called");
        _thermalBody = emittingBody ?? throw new ArgumentNullException(nameof(emittingBody));
        return this;
    }

    public CentralBodyPropagator Build()
    {
        // Create propagator via the auto-building constructor
        // (handles gravity, third-body, drag, SRP, ephemeris cache, integrator init)
        var propagator = new CentralBodyPropagator(_window, _spacecraft, _integrator,
            _perturbingBodies, _includeAtmosphericDrag, _includeSolarRadiationPressure, _deltaT);

        // Add the radiation forces after construction (with ephemeris cache from existing forces)
        if (_albedoBody != null)
        {
            var albedoForce = new AlbedoRadiationPressure(_spacecraft, _albedoBody);
            albedoForce.EphemerisCache = _integrator.Forces.FirstOrDefault()?.EphemerisCache;
            _integrator.AddForce(albedoForce);
        }

        if (_thermalBody != null)
        {
            var thermalForce = new ThermalRadiationPressure(_spacecraft, _thermalBody);
            thermalForce.EphemerisCache = _integrator.Forces.FirstOrDefault()?.EphemerisCache;
            _integrator.AddForce(thermalForce);
        }

        return propagator;
    }
}
