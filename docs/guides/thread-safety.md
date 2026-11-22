# Thread Safety

When propagating multiple spacecraft concurrently (via `BatchPropagator`, `MonteCarloPropagator`, or manual `Parallel.ForEach`), several components have thread-safety constraints that must be respected.

## Constraint Table

| Component | Thread-safe? | Guidance |
|-----------|-------------|----------|
| SPICE API | Yes (global lock) | Safe but serialized — may limit parallel scaling |
| Integrator instances | No | The options entry points (`PropagationOptions`) build one per run, every task of a batch and every run of a Monte Carlo campaign included. The constructor of `CentralBodyPropagator` that takes an integrator holding its forces uses the instance given: give each propagation its own (11.0.0: the factories and the flag members that took an instance were removed, R07 of #668) |
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

## Shared Bodies, One Options Value

Since 11.0.0 a task need not have its own `CelestialBody` with geopotential: each propagation builds its own field
from the body's model (R06 of #668), so the tasks may share one Earth, and one `PropagationOptions` value
([Propagation Options](../reference/propagators.md#propagation-options)), bit for bit the runs of a body per task.

Build each task's initial state on that Earth: the bodies of one NAIF identifier in a propagation, the centre of the
orbit included, must have the same GM, radii, gravitational field and atmospheric model, so a state about another Earth
without the geopotential fails every task with `ArgumentException` (#674).

```csharp
// Inputs: taskCount (int), orbit (StateVector), window (Window)
// One Earth with geopotential, shared by every task
var earth = new CelestialBody(PlanetsAndMoons.EARTH,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
var options = new PropagationOptions
{
    Integrator = new RK78IntegratorOptions
    {
        AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
    },
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
};

var tasks = new List<PropagationTask>();
for (int i = 0; i < taskCount; i++)
{
    // The same state about the shared Earth
    var spacecraft = new Spacecraft(-(1000 + i), $"Sat-{i}", 100.0, 10000.0,
        new Clock($"clk-{i}", 256), new StateVector(orbit.Position, orbit.Velocity, earth, orbit.Epoch, orbit.Frame));
    tasks.Add(new PropagationTask(spacecraft, window, options));
}

var result = BatchPropagator.Propagate(tasks);
```

## Monte Carlo Pattern

A campaign of `MonteCarloConfiguration.Propagation` shares its bodies, the template's central body with a geopotential
included: each run builds its own integrator, forces and field (11.0.0, R06 of #668). The `CelestialBodiesFactory`,
`DeltaT` and `IntegratorFactory` of 10.x were removed in 11.0.0 (R07 of #668).

```csharp
// Inputs: spacecraft (Spacecraft), window (Window)
var config = new MonteCarloConfiguration
{
    RunCount = 100,
    Seed = 42,
    TemplateSpacecraft = spacecraft,   // its initial state carries the covariance that is sampled
    Window = window,
    Propagation = new PropagationOptions
    {
        Integrator = new RK78IntegratorOptions
        {
            AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
        },
        ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
    }
};
```

## BatchPropagator Constraints

- **Duplicate detection**: Reusing the same `Spacecraft` instance across tasks throws `ArgumentException`.
- **Fault isolation**: A failed task does not cancel or corrupt other tasks.
- **Integrators**: each task builds its own integrator from its `PropagationOptions`, so one options value serves
  every task.

## See Also

- [Propagators](../reference/propagators.md)
- [Force Models](../reference/force-models.md)
- [High-Fidelity Propagation](high-fidelity-propagation.md)
