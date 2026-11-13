# Thread Safety

When propagating multiple spacecraft concurrently (via `BatchPropagator`, `MonteCarloPropagator`, or manual `Parallel.ForEach`), several components have thread-safety constraints that must be respected.

## Constraint Table

| Component | Thread-safe? | Guidance |
|-----------|-------------|----------|
| SPICE API | Yes (global lock) | Safe but serialized — may limit parallel scaling |
| Integrator instances | No | Use `IntegratorFactory` to create one per task; an entry point that builds the forces takes its integrator atomically and rejects one already used or holding forces, so a factory returning one instance gives one run and failed tasks for the others |
| `CelestialBody` without geopotential | Yes | Two-body gravity is stateless |
| `CelestialBody` with `GeopotentialModelParameters` | **No** | Mutable Legendre/trig buffers, and the Cunningham tables of the partials when the state transition matrix is integrated, are overwritten on every evaluation |
| `GeopotentialModelParameters` | Yes | Builds any number of bodies, on any thread: the coefficients are read once and shared read only; each body gets its own field and buffers |
| Frame orientation cache of a propagation | Yes | Active only on the thread that propagates or evaluates the propagation; the shared `Frame` is never changed |
| Third-body perturbation bodies (Sun, Moon, etc.) | Yes | Stateless — safe to share |
| `Nrlmsise00Model` | Yes | Thread-safe atmospheric model; its calls are serialized on a lock of the instance, so one configured model shared by parallel propagations serializes their densities (4 threads × 200 000 densities: 5.8 s on one instance, 0.9 s on one per thread). The default atmosphere of an Earth built without a model is one instance per thread |
| `NRLMSISE00` (the low-level model) | No | Keeps intermediate values and work arrays between calls; `Nrlmsise00Model` serializes its calls to its own instance |
| `PropagationSolution.InterpolateAt` of an RK7(8) solution | Serialized per solution | Evaluates the forces of the propagation (a shortened RK7(8) step) under a lock of the solution. Solutions that share a central body without a geopotential may be queried concurrently. Like a propagation, it must not run concurrently with another propagation or RK7(8) solution that shares a `CelestialBody` with a geopotential |
| `PropagationSolution.GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance`, `GetStateVector` (and the internal `EvaluateVariational`, `EvaluateCovariance`, `EvaluateExtendedCovariance`) | Serialized per solution | Same lock and the same rule as `InterpolateAt`: they evaluate the forces inside a step, so they must not run concurrently with another propagation or evaluation that shares a `CelestialBody` with a geopotential |
| `ConjunctionAssessment.Analyze`, `AnalyzeAll` with a propagated covariance, `EvaluateAvoidance` | Serialized per solution | The replay of the variational equations, the propagations of the non-linearity index and of the avoidance options use the forces of the participant's propagation (in RK7(8) or by Velocity-Verlet for the avoidance options), under the lock of its solution or of the force model its spacecraft keeps: an analysis must not run concurrently with another propagation or evaluation that shares a `CelestialBody` with a geopotential |
| Managed occultation search (`FindWindowsOnOccultationConstraint`, `IsOcculted`) and field-of-view search (`Instrument.FindWindowsInFieldOfViewConstraint`, `IsInFOV`) | Yes | They keep no state and read the kernels through the shared lock of `SpiceAPI`, so searches on separate propagations may run concurrently. A spacecraft's frame stores every orientation it is asked for, and later reads propagate from the stored ones: the attitude a search reads can depend, in its last bits, on earlier reads of the same spacecraft and on the order of the threads. Searches on fresh spacecraft give the same windows, to the tick, concurrently and in sequence |
| `CovariancePropagationOptions`, `RtnProcessNoise`, `ConsiderParameters`, `ManeuverExecutionError` | Yes | Immutable |

## Per-Task CelestialBody Pattern

When using geopotential gravity, create a separate `CelestialBody` instance per task. This gives each task its own `GeopotentialGravitationalField` buffers. A `CelestialBody` without geopotential, with or without an atmospheric model, can be shared.

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
- **Integrator sharing**: Never share integrator instances. Always supply an `IntegratorFactory` that returns a new
  instance: a shared one is taken by one task, and the others fail with `ArgumentException` (`integrator`).

## See Also

- [Propagators](../reference/propagators.md)
- [Force Models](../reference/force-models.md)
- [High-Fidelity Propagation](high-fidelity-propagation.md)
