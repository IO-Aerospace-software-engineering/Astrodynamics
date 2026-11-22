# Thread Safety

When propagating multiple spacecraft concurrently (via `BatchPropagator`, `MonteCarloPropagator`, or manual `Parallel.ForEach`), several components have thread-safety constraints that must be respected.

## Constraint Table

| Component | Thread-safe? | Guidance |
|-----------|-------------|----------|
| SPICE API | Yes (global lock) | Safe but serialized — may limit parallel scaling |
| Integrator instances | No | The options entry points (`PropagationOptions`) build one per run. Otherwise use `IntegratorFactory` to create one per task; an entry point that builds the forces takes its integrator atomically and rejects one already used or holding forces, so a factory returning one instance gives one run and failed tasks for the others |
| `PropagationOptions`, `RK78IntegratorOptions`, `VVIntegratorOptions` | Yes | Immutable values: one serves any number of runs, each building its own integrator, forces and geopotential field |
| `CelestialBody` without geopotential | Yes | Two-body gravity is stateless |
| `CelestialBody` with `GeopotentialModelParameters` | Yes in propagations, **no** for its own field | Since 11.0.0 each propagation that builds its forces builds its own field from the body's model, over the shared coefficients (R06 of #668), so propagations, batches, scenarios and Monte Carlo runs may share the body, bit for bit the runs of separate bodies. The body's own field, which `EvaluateGravitationalAcceleration` evaluates (and a force list of your own built on `GravitationalAcceleration`), has mutable Legendre/trig buffers, overwritten on every evaluation: one thread at a time |
| `GeopotentialModelParameters` | Yes | An immutable value that builds any number of bodies and propagations, on any thread: the coefficients are read once and shared read only; each body and each propagation gets its own field and buffers |
| Frame orientation cache of a propagation | Yes | Active only on the thread that propagates or evaluates the propagation; the shared `Frame` is never changed |
| Third-body perturbation bodies (Sun, Moon, etc.) | Yes | Stateless — safe to share |
| `Nrlmsise00Model` | Yes | Thread-safe atmospheric model; its calls are serialized on a lock of the instance, so one configured model shared by parallel propagations serializes their densities (4 threads × 200 000 densities: 5.8 s on one instance, 0.9 s on one per thread). The default atmosphere of an Earth built without a model is one instance per thread |
| `NRLMSISE00` (the low-level model) | No | Keeps intermediate values and work arrays between calls; `Nrlmsise00Model` serializes its calls to its own instance |
| `PropagationSolution.InterpolateAt` of an RK7(8) solution | Serialized per solution | Evaluates the forces of the propagation (a shortened RK7(8) step) under a lock of the solution. Solutions may be queried concurrently: each evaluates the forces of its own propagation, with its own geopotential field since 11.0.0 (before, solutions sharing a `CelestialBody` with a geopotential could not) |
| `PropagationSolution.GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance`, `GetStateVector` (and the internal `EvaluateVariational`, `EvaluateCovariance`, `EvaluateExtendedCovariance`) | Serialized per solution | Same lock and the same rule as `InterpolateAt`: they evaluate the forces of their propagation inside a step |
| `ConjunctionAssessment.Analyze`, `AnalyzeAll` with a propagated covariance, `EvaluateAvoidance` | Serialized per solution | The replay of the variational equations, the propagations of the non-linearity index and of the avoidance options use the forces of the participant's propagation (in RK7(8) or by Velocity-Verlet for the avoidance options), under the lock of its solution or of the force model its spacecraft keeps, each with its own geopotential field |
| Managed occultation search (`FindWindowsOnOccultationConstraint`, `IsOcculted`) and field-of-view search (`Instrument.FindWindowsInFieldOfViewConstraint`, `IsInFOV`) | Yes | They keep no state and read the kernels through the shared lock of `SpiceAPI`, so searches on separate propagations may run concurrently. A search reads the attitude without changing it: the attitude record of a managed spacecraft is filled by its propagation only (and `AddStateOrientationToICRF`), and a read stores nothing ([#523](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/523); see the next row), so the attitude a search reads depends neither on earlier reads nor on the order of the threads. The frame of a kernel-backed spacecraft reads SPICE at each call and caches nothing. Searches on the same spacecraft or on fresh ones, concurrently or in sequence, give the same windows to the tick (`FieldOfViewSearchTests.ConcurrentSearches_EqualSequentialSearches`, `OccultationSearchValidationTests.ConcurrentSearches_GiveTheResultsOfSequentialOnes`) |
| Attitude record of a managed spacecraft (`SpacecraftFrame`) and the frames only the library holds | Yes | The record is read under a lock of the record. An ephemeris read in the frame of a managed spacecraft with an attitude record, or of a site built from coordinates, is read in J2000 and rotated by that frame under the shared SPICE lock ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)): the record lock nests inside the SPICE lock, so no region of the record lock calls SPICE, which keeps the two locks free of an inversion. A `SiteFrame` reads its body's frame through SPICE under the same lock, which is re-entrant |
| `CovariancePropagationOptions`, `RtnProcessNoise`, `ConsiderParameters`, `ManeuverExecutionError` | Yes | Immutable |

## Per-Task CelestialBody Pattern

Since 11.0.0 a task need not have its own `CelestialBody` with geopotential: each propagation builds its own field
from the body's model (R06 of #668), so the tasks may share one Earth, and one `PropagationOptions` value
([Propagation Options](../reference/propagators.md#propagation-options)). The pattern below, a body per task, still runs
and gives the same states.

Build each task's initial state on its own Earth: the bodies of one NAIF identifier in a propagation, the centre of the
orbit included, must have the same GM, radii, gravitational field and atmospheric model, so a state about a shared Earth
without the geopotential fails every task with `ArgumentException` (#674).

```csharp
// Inputs: taskCount (int), orbit (StateVector), window (Window)
var tasks = new List<PropagationTask>();

for (int i = 0; i < taskCount; i++)
{
    // Each task gets its own CelestialBody with geopotential
    var earth = new CelestialBody(PlanetsAndMoons.EARTH,
        new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

    // The same state about this task's Earth: the orbit's centre and the listed Earth must have the same models
    var spacecraft = new Spacecraft(-(1000 + i), $"Sat-{i}", 100.0, 10000.0,
        new Clock($"clk-{i}", 256), new StateVector(orbit.Position, orbit.Velocity, earth, orbit.Epoch, orbit.Frame));

    tasks.Add(new PropagationTask(spacecraft, window,
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        includeAtmosphericDrag: false, includeSolarRadiationPressure: false,
        deltaT: TimeSpan.FromSeconds(60),
        integratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)));
}

var result = BatchPropagator.Propagate(tasks);
```

## Monte Carlo Factory Pattern

A campaign of `MonteCarloConfiguration.Propagation` shares its bodies, the template's central body with a geopotential
included: each run builds its own integrator, forces and field (11.0.0, R06 of #668). The configuration of flags below
takes a `CelestialBodiesFactory` that returns fresh instances per run, as before.

```csharp
// Inputs: spacecraft (Spacecraft), window (Window)
var config = new MonteCarloConfiguration
{
    RunCount = 100,
    Seed = 42,
    TemplateSpacecraft = spacecraft,   // its initial state carries the covariance that is sampled
    Window = window,
    DeltaT = TimeSpan.FromSeconds(60),
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
