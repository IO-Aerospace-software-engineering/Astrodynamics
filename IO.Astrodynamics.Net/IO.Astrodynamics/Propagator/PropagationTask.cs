// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Defines a single spacecraft propagation to be executed as part of a batch (<see cref="BatchPropagator"/>): the
/// spacecraft, its window and the settings of its propagation (<see cref="PropagationOptions"/>, R06 of #668).
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread safety:</b> the spacecraft must not be shared with other concurrent tasks. Its central body may be: each
/// propagation builds its own geopotential field (R06 of #668). See <see cref="BatchPropagator"/> for full thread-safety
/// guidance.
/// </para>
/// <para>
/// <b>Maneuvers:</b> each maneuver of the spacecraft draws on one of its own fuel tanks, which belongs to it alone: the
/// propagation rejects a maneuver whose engine's tank is not the spacecraft's with an
/// <see cref="InvalidOperationException"/> in <see cref="PropagationResult.Error"/> (#676, #586). Distinct spacecraft
/// therefore imply distinct maneuvers and engines, built on each spacecraft's tanks.
/// </para>
/// </remarks>
public sealed record PropagationTask
{
    /// <summary>
    /// The task of propagating <paramref name="spacecraft"/> over <paramref name="window"/> with the settings of
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="spacecraft">The spacecraft to propagate. Must be a distinct instance per task.</param>
    /// <param name="window">Propagation time window.</param>
    /// <param name="options">The settings of the propagation; null for <see cref="PropagationOptions.Default"/>. Each
    /// task builds its own integrator, forces and geopotential field from them, so one value serves every task, and the
    /// tasks may share their bodies, a central body with a geopotential included (section A of the re-baseline of
    /// #668).</param>
    public PropagationTask(Spacecraft spacecraft, Window window, PropagationOptions options = null)
    {
        Spacecraft = spacecraft;
        Window = window;
        Options = options;
    }

    /// <summary>The spacecraft to propagate, a distinct instance per task.</summary>
    public Spacecraft Spacecraft { get; init; }

    /// <summary>The propagation window.</summary>
    public Window Window { get; init; }

    /// <summary>The settings of the propagation; null for <see cref="PropagationOptions.Default"/>.</summary>
    public PropagationOptions Options { get; init; }

    /// <summary>
    /// A task of the flags and the integrator factory of 10.x: forces of <paramref name="celestialBodies"/>, drag and
    /// solar radiation pressure as asked, outputs every <paramref name="deltaT"/>, the integrator of
    /// <paramref name="integratorFactory"/>, Velocity-Verlet at <paramref name="deltaT"/> without one.
    /// </summary>
    /// <param name="spacecraft">The spacecraft to propagate. Must be a distinct instance per task.</param>
    /// <param name="window">Propagation time window.</param>
    /// <param name="celestialBodies">Celestial bodies used for gravity, third-body perturbations, drag, and SRP.</param>
    /// <param name="includeAtmosphericDrag">Whether to include atmospheric drag.</param>
    /// <param name="includeSolarRadiationPressure">Whether to include solar radiation pressure.</param>
    /// <param name="deltaT">Output step size for ephemeris sampling.</param>
    /// <param name="integratorFactory">Optional factory that creates a fresh <see cref="Integrator"/> for this task;
    /// each invocation must return a new instance. When null, the default Velocity-Verlet integrator is used.</param>
    public PropagationTask(Spacecraft spacecraft, Window window, IEnumerable<CelestialItem> celestialBodies,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure, TimeSpan deltaT,
        Func<Integrator> integratorFactory = null) : this(spacecraft, window)
    {
        CelestialBodies = celestialBodies;
        IncludeAtmosphericDrag = includeAtmosphericDrag;
        IncludeSolarRadiationPressure = includeSolarRadiationPressure;
        DeltaT = deltaT;
        IntegratorFactory = integratorFactory;
        IsLegacy = true;
    }

    /// <summary>Celestial bodies of a task of the flags constructor; null for a task of <see cref="Options"/>.</summary>
    public IEnumerable<CelestialItem> CelestialBodies { get; }

    /// <summary>Whether a task of the flags constructor includes atmospheric drag.</summary>
    public bool IncludeAtmosphericDrag { get; }

    /// <summary>Whether a task of the flags constructor includes solar radiation pressure.</summary>
    public bool IncludeSolarRadiationPressure { get; }

    /// <summary>The output step of a task of the flags constructor.</summary>
    public TimeSpan DeltaT { get; }

    /// <summary>The integrator factory of a task of the flags constructor, null for its default Velocity-Verlet.</summary>
    public Func<Integrator> IntegratorFactory { get; }

    // Whether the task was built by the flags constructor
    internal bool IsLegacy { get; }

    /// <summary>
    /// The propagation of the state transition matrix and of the covariance of a task of the flags constructor; null
    /// (the default) for none. It needs an <see cref="RK78Integrator"/> from <see cref="IntegratorFactory"/>: with the
    /// default Velocity-Verlet integrator, the task fails with a <see cref="NotSupportedException"/> in its
    /// <see cref="PropagationResult.Error"/>. A task of <see cref="Options"/> takes
    /// <see cref="PropagationOptions.CovariancePropagation"/>, and setting this one throws
    /// <see cref="ArgumentException"/> when it runs.
    /// </summary>
    public CovariancePropagationOptions CovariancePropagation { get; init; }
}
