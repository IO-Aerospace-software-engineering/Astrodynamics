// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
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

    private CovariancePropagationOptions _covariancePropagation;

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

    /// <summary>
    /// Propagate the state transition matrix Φ and the sensitivity Ψ to the force parameters with the state, and, in
    /// <see cref="CovariancePropagationMode.Covariance"/>, the covariance of the initial orbital parameters of the
    /// spacecraft, read afterwards from the <see cref="PropagationSolution"/>. Needs an <see cref="RK78Integrator"/>.
    /// </summary>
    /// <remarks>
    /// Without this call, a covariance of the initial orbital parameters is not propagated: it stays on the initial
    /// state, the output states carry none, and no partial derivative is evaluated. The options are checked by
    /// <see cref="Build"/>, once the forces are known: the columns of Ψ follow them (Cd with drag, Cr with a radiation
    /// force). In <see cref="CovariancePropagationMode.Covariance"/>, <see cref="Build"/> also checks the covariance P0 of
    /// the initial orbital parameters, the state of the spacecraft rather than an argument, and throws
    /// <see cref="InvalidOperationException"/> when it is missing, given at another epoch than the start of the window,
    /// not 6×6, not symmetric or not positive semi-definite.
    /// </remarks>
    /// <param name="options">The options.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Already called.</exception>
    public CentralBodyPropagatorBuilder WithCovariancePropagation(CovariancePropagationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_covariancePropagation != null)
            throw new InvalidOperationException("WithCovariancePropagation already called");
        _covariancePropagation = options;
        return this;
    }

    /// <summary>
    /// Build the propagator.
    /// </summary>
    /// <exception cref="NotSupportedException">The covariance propagation is asked for with another integrator than
    /// <see cref="RK78Integrator"/>.</exception>
    /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a consider
    /// parameter without its force.</exception>
    /// <exception cref="InvalidOperationException">The covariance is asked for, and the initial orbital parameters carry
    /// none, carry it at another epoch than the start of the window, or carry one that is not symmetric and positive
    /// semi-definite.</exception>
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

        // Once the forces are final: the columns of Ψ follow them
        propagator.EnableCovariancePropagation(_covariancePropagation);
        return propagator;
    }
}
