# Thread Safety

When propagating multiple spacecraft concurrently (via `BatchPropagator`, `MonteCarloPropagator`, or manual `Parallel.ForEach`), several components have thread-safety constraints that must be respected.

## Constraint Table

| Component | Thread-safe? | Guidance |
|-----------|-------------|----------|
| SPICE API | Yes (global lock) | Safe but serialized — may limit parallel scaling |
| Integrator instances | No | Use `IntegratorFactory` to create one per task |
| `CelestialBody` without geopotential | Yes | Two-body gravity is stateless |
| `CelestialBody` with `GeopotentialModelParameters` | **No** | Mutable Legendre/trig buffers are overwritten on every evaluation |
| `Frame.OrientationCache` | **No** | Propagator sets/clears an unsynchronized cache on the central body's `Frame` |
| Third-body perturbation bodies (Sun, Moon, etc.) | Yes | Stateless — safe to share |
| `Nrlmsise00Model` | Yes | Thread-safe atmospheric model |

## Per-Task CelestialBody Pattern

When using geopotential gravity, create a separate `CelestialBody` instance per task. This gives each task its own `GeopotentialGravitationalField` buffers and `Frame` orientation cache.

```csharp
var tasks = new List<PropagationTask>();

for (int i = 0; i < taskCount; i++)
{
    // Each task gets its own CelestialBody with geopotential
    var earth = new CelestialBody(PlanetsAndMoons.EARTH,
        new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

    var spacecraft = new Spacecraft(-(1000 + i), $"Sat-{i}", 100.0, 10000.0,
        new Clock($"clk-{i}", 256), orbit.AtEpoch(epoch, earth, Frame.ICRF));

    tasks.Add(new PropagationTask(spacecraft, window,
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        IncludeAtmosphericDrag: false, IncludeSolarRadiationPressure: false,
        DeltaT: TimeSpan.FromSeconds(60),
        IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)));
}

var result = BatchPropagator.Propagate(tasks);
```

## Monte Carlo Factory Pattern

`MonteCarloPropagator` requires a `CelestialBodiesFactory` that returns fresh instances per run. Sharing SPICE-backed bodies or geopotential-enabled bodies across parallel runs causes data corruption.

```csharp
var config = new MonteCarloConfiguration
{
    // ...
    CelestialBodiesFactory = () =>
    [
        new CelestialBody(PlanetsAndMoons.EARTH,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)),
        Stars.SUN_BODY,
        PlanetsAndMoons.MOON_BODY
    ],
    IntegratorFactory = () => new RK78Integrator(1e-10, 1e-10, 30.0),
};
```

## BatchPropagator Constraints

- **Duplicate detection**: Reusing the same `Spacecraft` instance across tasks throws `ArgumentException`.
- **Fault isolation**: A failed task does not cancel or corrupt other tasks.
- **Integrator sharing**: Never share integrator instances. Always supply an `IntegratorFactory`.

## See Also

- [Propagators](../reference/propagators.md)
- [Force Models](../reference/force-models.md)
- [High-Fidelity Propagation](high-fidelity-propagation.md)
