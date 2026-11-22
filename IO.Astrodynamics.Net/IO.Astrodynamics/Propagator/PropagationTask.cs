// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Defines a single spacecraft propagation to be executed as part of a batch (<see cref="BatchPropagator"/>): the
/// spacecraft, its window and the settings of its propagation (<see cref="PropagationOptions"/>, R06 of #668).
/// </summary>
/// <remarks>
/// The task of flags and an integrator factory of 10.x, with its properties (<c>CelestialBodies</c>,
/// <c>IncludeAtmosphericDrag</c>, <c>IncludeSolarRadiationPressure</c>, <c>DeltaT</c>, <c>IntegratorFactory</c>,
/// <c>CovariancePropagation</c>), was removed in 11.0.0 (R07 of #668): its settings are those of
/// <see cref="PropagationOptions"/>, a task without a factory <see cref="Integrators.VVIntegratorOptions"/> at its step.
/// </remarks>
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
}
