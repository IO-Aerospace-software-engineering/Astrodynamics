// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// The settings of a numerical propagation (R06 of #668, D1): the integrator, the output step, the third bodies, the
/// forces, the geopotential and atmosphere of the central body, and the propagation of the covariance. One record for
/// every options entry point: <see cref="Body.Spacecraft.Spacecraft.Propagate(TimeSystem.Window, PropagationOptions, System.Threading.CancellationToken)"/>,
/// <see cref="CentralBodyPropagator(in TimeSystem.Window, Body.Spacecraft.Spacecraft, PropagationOptions)"/>,
/// <see cref="Mission.Scenario.Simulate(PropagationOptions, System.Threading.CancellationToken)"/>, <see cref="PropagationTask"/> (with
/// <see cref="BatchPropagator"/>) and <see cref="MonteCarlo.MonteCarloConfiguration.Propagation"/>.
/// </summary>
/// <remarks>
/// <para>
/// Values only, set at init (<c>PropagationOptions.Default with { … }</c>): each propagation builds its own integrator,
/// forces and geopotential field from them, so one record serves any number of propagations, on any thread. A setting of
/// one property is checked in its <c>init</c> (so a <c>with</c> checks it again); the rules between the settings and the
/// inputs at the start of the call, before anything changes.
/// </para>
/// <para>
/// The central body always takes part, with the models of the observer of the initial orbit (#674): its GM, its
/// geopotential and its atmosphere are the body's. <see cref="Geopotential"/> and <see cref="Atmosphere"/> are fallbacks
/// (D1 rule 6): a body without a model takes the option's; a body with a model and the option equal, that model; a body
/// with another model throws <see cref="ArgumentException"/> naming both; neither, the body's (a point mass; for the drag
/// of an Earth, NRLMSISE-00 at nominal space weather). A body without a model counts as "not given" (R12 of #668), so the
/// option fills the point-mass Earth of a TLE.
/// </para>
/// <para>
/// The resolved settings of a run, with no null left where a value applies, are on
/// <see cref="PropagationSolution.Options"/>, which can be passed back to repeat the run.
/// </para>
/// </remarks>
public sealed record PropagationOptions
{
    private readonly IntegratorOptions _integrator = RK78IntegratorOptions.Default;
    private readonly TimeSpan _outputStep = TimeSpan.FromSeconds(60);
    private readonly ValueList<CelestialItem> _thirdBodies;

    /// <summary>The default settings, equal to <c>new()</c>: RK7(8) at 1e-9, outputs every 60 s, the central body alone,
    /// no other force, the models of the central body and the covariance Auto.</summary>
    public static PropagationOptions Default { get; } = new();

    /// <summary>
    /// The integrator: <see cref="RK78IntegratorOptions.Default"/> by default (RK7(8) at 1e-9/1e-9, as
    /// <c>new RK78Integrator()</c>), or <see cref="VVIntegratorOptions"/> for Velocity-Verlet, whose step is its own,
    /// not <see cref="OutputStep"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public IntegratorOptions Integrator
    {
        get => _integrator;
        init => _integrator = value ?? throw new ArgumentNullException(nameof(Integrator),
            "The integrator cannot be null: RK78IntegratorOptions.Default is the default.");
    }

    /// <summary>
    /// The step of the output states, 60 s by default: an output step only, the integrator choosing its own steps
    /// (RK7(8)) or taking <see cref="VVIntegratorOptions.Step"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan OutputStep
    {
        get => _outputStep;
        init => _outputStep = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(OutputStep), value, "The output step must be positive.");
    }

    /// <summary>
    /// The bodies other than the central body that take part: their third-body perturbation, their shadow for solar
    /// radiation pressure, and the drag of those with an atmosphere (an Earth always has one). Null by default, which
    /// means none on <see cref="Body.Spacecraft.Spacecraft.Propagate(TimeSystem.Window, PropagationOptions, System.Threading.CancellationToken)"/>,
    /// <see cref="CentralBodyPropagator"/>, <see cref="PropagationTask"/> and
    /// <see cref="MonteCarlo.MonteCarloConfiguration.Propagation"/> (the central body alone, D4 sub-choice 4), and the
    /// bodies added to the scenario on <see cref="Mission.Scenario.Simulate(PropagationOptions, System.Threading.CancellationToken)"/>, where a list that
    /// differs from them throws (section A of the re-baseline of #668).
    /// </summary>
    /// <remarks>
    /// Held as a copy, compared by value: changing the caller's list after init does not change the options. A body of
    /// the central body's NAIF identifier may be listed, with the models of the central body (#674). The list of a run,
    /// without the central body, is on <see cref="PropagationSolution.Options"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">The list holds a null body.</exception>
    public IReadOnlyList<CelestialItem> ThirdBodies
    {
        get => _thirdBodies;
        init => _thirdBodies = value == null ? null : new ValueList<CelestialItem>(value, nameof(ThirdBodies));
    }

    /// <summary>
    /// Whether atmospheric drag takes part, false by default: the drag of the central body's atmosphere (its model,
    /// <see cref="Atmosphere"/>, or NRLMSISE-00 at nominal space weather for an Earth without one; another central body
    /// without one throws), and of each third body with an atmosphere.
    /// </summary>
    public bool AtmosphericDrag { get; init; }

    /// <summary>Whether solar radiation pressure takes part, false by default, the shadow of every body of the run.</summary>
    public bool SolarRadiationPressure { get; init; }

    /// <summary>
    /// Whether the albedo radiation pressure of the central body takes part, false by default. It needs the central
    /// body's albedo (the <c>albedo</c> parameter of its <see cref="CelestialBody"/> constructor): a coefficient of 0, the
    /// value of every predefined body, throws <see cref="ArgumentException"/> (section A of the re-baseline of #668). The
    /// albedo of another body goes through the <see cref="CentralBodyPropagator"/> constructor that takes an integrator
    /// with its forces.
    /// </summary>
    public bool AlbedoRadiationPressure { get; init; }

    /// <summary>
    /// Whether the thermal radiation pressure of the central body takes part, false by default. It needs the central
    /// body's effective temperature and emissivity (the <c>thermalEffectiveTemperature</c> and <c>thermalEmissivity</c>
    /// parameters of its <see cref="CelestialBody"/> constructor): a coefficient of 0 throws
    /// <see cref="ArgumentException"/>.
    /// </summary>
    public bool ThermalRadiationPressure { get; init; }

    /// <summary>
    /// The geopotential of the central body, null by default for the body's: a fallback (see the remarks of the type).
    /// Each run builds its own field from the model, over its shared coefficients, so propagations sharing a body never
    /// share a field (section A of the re-baseline of #668).
    /// </summary>
    public GeopotentialModelParameters Geopotential { get; init; }

    /// <summary>
    /// The atmosphere of the central body's drag, null by default for the body's: a fallback (see the remarks of the
    /// type), compared as <see cref="Mission.Scenario.AddCelestialItem"/> compares models (NRLMSISE-00 by its switches and space weather). An
    /// Earth without a model counts as not given: the option replaces its NRLMSISE-00 at nominal space weather.
    /// </summary>
    public IAtmosphericModel Atmosphere { get; init; }

    /// <summary>
    /// The propagation of the state transition matrix and of the covariance; null by default for Auto (R06 of #668,
    /// structuring choice 3, decided by S. Guillet, 2026-11-19): the covariance of the initial orbital parameters is
    /// propagated (<see cref="CovariancePropagationMode.Covariance"/>) when the integrator is RK7(8) and the covariance is
    /// given at the start of the window; a covariance at another epoch, under Velocity-Verlet, or none, is not, and
    /// <see cref="PropagationSolution.GetCovariance"/> says why; a covariance that is not positive semi-definite throws
    /// <see cref="InvalidOperationException"/> naming <see cref="OrbitalParameters.StateVector.WithRemediatedCovariance"/>
    /// and <see cref="CovariancePropagationOptions.None"/>, which turns the propagation off.
    /// </summary>
    /// <remarks>
    /// Auto leaves the trajectory unchanged (the steps are controlled on the state only) and costs the integration of the
    /// variational equations: over a LEO day at the RK7(8) defaults, 1.4 to 1.5 times the run about a point-mass Earth,
    /// 7.5 to 8.7 times with EGM2008 10×10 and drag, 1.4 to 1.5 times in SSB mode (R06 of #668, best of five, measured
    /// alone in the CI of #789 at aefd6c2b on Linux x64, Windows x64 and macOS arm64, <c>DefaultIntegratorFiguresTests</c>,
    /// no threshold; decision 30 of lot L4).
    /// </remarks>
    public CovariancePropagationOptions CovariancePropagation { get; init; }

    /// <summary>A list held by value: a copy of the caller's, equal to another of the same items in the same order.</summary>
    private sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
    {
        private readonly T[] _items;

        internal ValueList(IEnumerable<T> items, string paramName)
        {
            _items = items.ToArray();
            if (_items.Any(item => item == null))
            {
                throw new ArgumentException("The list cannot hold a null item.", paramName);
            }
        }

        public T this[int index] => _items[index];

        public int Count => _items.Length;

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public bool Equals(ValueList<T> other) => other != null && _items.SequenceEqual(other._items);

        public override bool Equals(object obj) => Equals(obj as ValueList<T>);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var item in _items)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }
    }
}
