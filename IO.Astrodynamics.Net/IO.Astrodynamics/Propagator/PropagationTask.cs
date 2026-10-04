// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Defines a single spacecraft propagation to be executed as part of a batch.
/// </summary>
/// <param name="Spacecraft">The spacecraft to propagate. Must be a distinct instance per task.</param>
/// <param name="Window">Propagation time window.</param>
/// <param name="CelestialBodies">Celestial bodies used for gravity, third-body perturbations, drag, and SRP.</param>
/// <param name="IncludeAtmosphericDrag">Whether to include atmospheric drag.</param>
/// <param name="IncludeSolarRadiationPressure">Whether to include solar radiation pressure.</param>
/// <param name="DeltaT">Output step size for ephemeris sampling.</param>
/// <param name="IntegratorFactory">
/// Optional factory that creates a fresh <see cref="Integrator"/> for this task.
/// Each invocation must return a new instance (integrators are not thread-safe).
/// When null, the default Velocity-Verlet integrator is used.
/// </param>
/// <remarks>
/// <b>Thread safety:</b> The <paramref name="Spacecraft"/> and its central body
/// (<c>InitialOrbitalParameters.Observer</c>) must not be shared with other concurrent tasks.
/// When the central body is a <see cref="CelestialBody"/> constructed with
/// <c>GeopotentialModelParameters</c>, each task must use its own <see cref="CelestialBody"/>
/// instance because the geopotential field's internal buffers are not thread-safe.
/// See <see cref="BatchPropagator"/> for full thread-safety guidance.
/// </remarks>
public sealed record PropagationTask(
    Spacecraft Spacecraft,
    Window Window,
    IEnumerable<CelestialItem> CelestialBodies,
    bool IncludeAtmosphericDrag,
    bool IncludeSolarRadiationPressure,
    TimeSpan DeltaT,
    Func<Integrator> IntegratorFactory = null);
