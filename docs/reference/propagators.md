# Propagators

The propagation subsystem provides numerical and analytical orbit propagation through a segment-based architecture with event detection and dense output interpolation.

## CentralBodyPropagator

`CentralBodyPropagator` is the primary numerical propagator for LEO, GEO, lunar, and interplanetary missions. The central body is inferred from the spacecraft's initial orbital parameters.

### Propagation Frame

The initial state may be given in any inertial frame: ICRF, B1950, FK4, ECLIPJ2000, ECLIPB1950 or GALACTIC. The
propagation runs in that frame, and the output states are in it. Every force input is evaluated in it as well: the
ephemeris cache of the bodies, the third-body positions and the rotation of the atmosphere. The trajectory therefore
does not depend on the inertial frame chosen, apart from rounding and from the step sequence of an adaptive
integrator, whose error control scales each component separately.

### Propagation Loop

1. Build event detectors from the spacecraft maneuver chain.
2. Integrate a segment until the next event or end of window.
3. Execute the maneuver via `IEventDetector.HandleEvent()`, update state.
4. Compute output states on the requested `DeltaT` cadence with `PropagationSolution.InterpolateAt`.

### Supported Perturbations

- Central-body gravity
- Third-body perturbations
- EGM2008 geopotential
- Atmospheric drag
- Solar radiation pressure
- Albedo radiation pressure
- Thermal radiation pressure

### CentralBodyPropagator Constructors

| Constructor | Description |
|-------------|-------------|
| `CentralBodyPropagator(window, spacecraft, integrator, celestialBodies, drag, srp, deltaT)` | Full control: specify integrator, bodies, and perturbation flags |
| `CentralBodyPropagator(window, spacecraft, integrator, deltaT)` | Use a pre-configured integrator (forces already added) |
| `CentralBodyPropagator(window, spacecraft, celestialBodies, drag, srp, deltaT)` | Auto-creates a default VVIntegrator with specified forces |

### PropagationSolution

`PropagationSolution` is the multi-segment container returned by propagation. It holds `PropagationSegment` objects, each containing `AcceptedStep` records that support Hermite interpolation. An RK7(8) solution also keeps the dynamics of the propagation, to compute its states between the steps at the accuracy of the integrator.

| Member | Description |
|--------|-------------|
| `Segments` | Read-only list of `PropagationSegment` objects |
| `StateVectors` | Output state vectors at the requested cadence |
| `InterpolateAt(Time epoch)` | State at any epoch within the solution window; returns `(Vector3 position, Vector3 velocity)`. RK7(8): a shortened RK7(8) step from the start of the accepted step (13 force evaluations inside a step, none at a boundary); Velocity-Verlet: cubic Hermite interpolation. At a maneuver, the state after it |

When conjunction assessment follows propagation, pass `PropagationSolution` directly to the SSA overloads to reuse the dense output.

## TLEPropagator

`TLEPropagator` provides SGP4/SDP4 propagation for TLE-driven workflows. Use it for catalog screening or when working with mean-element data sources.

```csharp
var propagator = new TLEPropagator(window, spacecraft, deltaT);
var solution = propagator.Propagate();
```

| Property | Description |
|----------|-------------|
| `Window` | Propagation time window |
| `Spacecraft` | The spacecraft being propagated |
| `DeltaT` | Output time step |

## BatchPropagator

`BatchPropagator` propagates multiple spacecraft in parallel with fault isolation, cancellation support, and progress reporting.

```csharp
var tasks = new List<PropagationTask>
{
    new PropagationTask(spacecraft1, window,
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        IncludeAtmosphericDrag: false, IncludeSolarRadiationPressure: false,
        DeltaT: TimeSpan.FromSeconds(60),
        IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)),
    new PropagationTask(spacecraft2, window,
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        IncludeAtmosphericDrag: false, IncludeSolarRadiationPressure: false,
        DeltaT: TimeSpan.FromSeconds(60),
        IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0))
};

var result = BatchPropagator.Propagate(tasks, new BatchOptions
{
    MaxDegreeOfParallelism = 4
});

foreach (var r in result.Results)
{
    if (r.Succeeded)
        Console.WriteLine($"{r.Spacecraft.Name}: {r.Solution.StateVectors.Count} states");
    else
        Console.WriteLine($"{r.Spacecraft.Name}: FAILED — {r.Error.Message}");
}
```

| Property | Description |
|----------|-------------|
| `AllSucceeded` | Whether every task completed without error |
| `Failed` | Number of failed tasks |
| `ElapsedTime` | Wall-clock time for the batch |
| `Results` | Per-task results with `Succeeded`, `Solution`, and `Error` |

Key features: fault isolation (a failed task does not corrupt others), `CancellationToken` via `BatchOptions`, `IProgress<BatchProgress>` for tracking, duplicate `Spacecraft` detection.

## MonteCarloPropagator

`MonteCarloPropagator` runs dispersion analysis by sampling initial state covariance, propagating each perturbed state via `BatchPropagator`, and aggregating per-epoch statistics.

### MonteCarloConfiguration

| Property | Type | Description |
|----------|------|-------------|
| `RunCount` | `int` | Number of runs (minimum 2) |
| `Seed` | `int` | Random seed for reproducibility |
| `TemplateSpacecraft` | `Spacecraft` | Nominal spacecraft with 6x6 covariance |
| `Window` | `Window` | Propagation time window |
| `CelestialBodiesFactory` | `Func<IEnumerable<CelestialItem>>` | Factory for thread-safe body creation |
| `DeltaT` | `TimeSpan` | Output time step |
| `IncludeAtmosphericDrag` | `bool` | Enable drag |
| `IncludeSolarRadiationPressure` | `bool` | Enable SRP |
| `IntegratorFactory` | `Func<Integrator>` | Optional per-run integrator factory |
| `BaseNaifId` | `int` | Base NAIF ID (default -100000) |
| `BatchOptions` | `BatchOptions` | Parallelism and cancellation options |

### MonteCarloResult

| Property | Type | Description |
|----------|------|-------------|
| `EpochStatistics` | `IReadOnlyList<EpochStatistics>` | Per-epoch statistical summary |
| `SuccessCount` | `int` | Runs completed successfully |
| `FailureCount` | `int` | Runs that failed |
| `TotalCount` | `int` | Total runs |
| `ElapsedTime` | `TimeSpan` | Wall-clock time |
| `Seed` | `int` | Seed used |

### EpochStatistics

| Property | Type | Description |
|----------|------|-------------|
| `Epoch` | `Time` | Propagation epoch |
| `MeanPosition` | `Vector3` | Mean position (m, ICRF) |
| `MeanVelocity` | `Vector3` | Mean velocity (m/s, ICRF) |
| `SampleCovariance` | `Matrix` | 6x6 Bessel-corrected covariance |
| `StandardDeviation` | `StateComponents` | Per-component sigma |
| `Min` / `Max` | `StateComponents` | Per-component extremes |
| `RssPositionPercentiles` | `RssPercentiles` | P50, P95, P99 position (m) |
| `RssVelocityPercentiles` | `RssPercentiles` | P50, P95, P99 velocity (m/s) |

Sampling uses Cholesky decomposition with Box-Muller random generation. A fixed seed guarantees identical samples.

### Example

```csharp
var config = new MonteCarloConfiguration
{
    RunCount = 100,
    Seed = 42,
    TemplateSpacecraft = spacecraft,
    Window = new Window(Time.J2000TDB, Time.J2000TDB.AddHours(2)),
    CelestialBodiesFactory = () =>
    [
        new CelestialBody(PlanetsAndMoons.EARTH),
        Stars.SUN_BODY,
        PlanetsAndMoons.MOON_BODY
    ],
    DeltaT = TimeSpan.FromSeconds(60),
    IntegratorFactory = () => new RK78Integrator(1e-10, 1e-10, 30.0),
};

var result = MonteCarloPropagator.Propagate(config);

Console.WriteLine($"Success: {result.SuccessCount}/{result.TotalCount}");
var last = result.EpochStatistics[^1];
Console.WriteLine($"Position 95th percentile RSS: {last.RssPositionPercentiles.P95:F1} m");
```

## See Also

- [Integrators](integrators.md)
- [Force Models](force-models.md)
- [Propagator Builder](propagator-builder.md)
- [Conjunction Assessment](conjunction-assessment.md)
