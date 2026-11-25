# Thread Safety

When propagating multiple spacecraft concurrently (via `BatchPropagator`, `MonteCarloPropagator`, or manual `Parallel.ForEach`), several components have thread-safety constraints that must be respected.

## Constraint Table

| Component | Thread-safe? | Guidance |
|-----------|-------------|----------|
| SPICE API | Yes (global lock) | Safe but serialized — may limit parallel scaling |
| Integrator instances | No | The options entry points (`PropagationOptions`) build one per run, every task of a batch and every run of a Monte Carlo campaign included. The constructor of `CentralBodyPropagator` that takes an integrator holding its forces uses the instance given: an integrator belongs to one propagator, a second propagator given it throwing `ArgumentException` (`integrator`, #782); give each propagator its own (11.0.0: the factories and the flag members that took an instance were removed, R07 of #668) |
| `PropagationOptions`, `RK78IntegratorOptions`, `VVIntegratorOptions` | Yes | Immutable values: one serves any number of runs, each building its own integrator, forces and geopotential field |
| `CelestialBody` without geopotential | Yes | Two-body gravity is stateless |
| `CelestialBody` with `GeopotentialModelParameters` | Yes in propagations, **no** for its own field | Since 11.0.0 each propagation that builds its forces builds its own field from the body's model, over the shared coefficients (R06 of #668), so propagations, batches, scenarios and Monte Carlo runs may share the body, bit for bit the runs of separate bodies. The body's own field, which `EvaluateGravitationalAcceleration` evaluates (and a force list of your own built on `GravitationalAcceleration`), has mutable Legendre/trig buffers, overwritten on every evaluation: one thread at a time |
| `GeopotentialModelParameters` | Yes | An immutable value that builds any number of bodies and propagations, on any thread: the coefficients are read once and shared read only; each body and each propagation gets its own field and buffers |
| Frame orientation cache of a propagation | Yes | Active only on the thread that propagates or evaluates the propagation; the shared `Frame` is never changed |
| Third-body perturbation bodies (Sun, Moon, etc.) | Yes | Stateless — safe to share |
| `Nrlmsise00Model` | Yes | Thread-safe atmospheric model; its calls are serialized on a lock of the instance, so one configured model shared by parallel propagations serializes their densities (4 threads × 200 000 densities: 5.8 s on one instance, 0.9 s on one per thread). The default atmosphere of an Earth built without a model is one instance per thread |
| `NRLMSISE00` (the low-level model) | No | Keeps intermediate values and work arrays between calls; `Nrlmsise00Model` serializes its calls to its own instance |
| `CentralBodyPropagator.Propagate` (and `PropagatorBase.Propagate` of a subclass) on one instance | Serialized per propagator instance | A run holds the lock of the forces of its instance for the whole run, the forces you added included, so two concurrent `Propagate` calls on one instance and the evaluations of its earlier solutions serialize with it (decision 28 of lot L4 of #668); see [The Lock of a Propagator Instance](#the-lock-of-a-propagator-instance) for the rule on code a run calls. Separate instances run concurrently |
| A run on a `Spacecraft` (`Spacecraft.Propagate`, a `CentralBodyPropagator`, `TLEPropagator`, `Scenario.Simulate`, a batch task) | **No**: one run at a time per spacecraft | A run re-arms its spacecraft and writes its stored states, attitude record, tanks and executed maneuvers; nothing stops a second run on the same spacecraft from interleaving with it, through another propagator, `Spacecraft.Propagate`, a scenario or a batch, and it can charge a burn twice with no exception ([#793](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/793)). Run one at a time per spacecraft, and do not change its plan (`SetManeuvers`) during a run on it (decision 96 of #668); see [One Run at a Time per Spacecraft](#one-run-at-a-time-per-spacecraft) |
| The maneuver detectors a custom `IIntegrator` receives (`IEventDetector`, in `IntegrateSegment`) | For its `IntegrateSegment` call only | A run disarms every detector it built when it ends, completed, stopped or thrown, and a later `Evaluate` or `HandleEvent` throws `InvalidOperationException`, changing nothing (decision 95 of #668). The rule holds for the calls that start after the end of the run: a call already in progress on another thread when the run ends completes, and no lock is added to the detector (decision 98 of #668, decided by S. Guillet, 2026-11-24: 12 of 300 runs of a probe whose thread called `HandleEvent` in a loop burned fuel after `Propagate` returned, in the third review of L5a). A detector that has executed its impulse, or started to and thrown, throws `InvalidOperationException` at a later call (decision 97 of #668). Use the detectors on the thread of the call, within it, and leave `HandleEvent` to the propagation; see [Event Detection](../reference/event-detection.md#the-maneuver-detector) |
| `PropagationSolution.GetStateVector` of an RK7(8) solution | Serialized per propagator instance | Evaluates the forces of the propagation (a shortened RK7(8) step) under the lock of the forces of its propagator, one per instance, shared by the solutions of all its runs and held by each run (#782: before, a lock per solution, so an evaluation of an earlier solution could run concurrently with a later run of the same instance over the same forces). Solutions of different propagators may be queried concurrently: each evaluates the forces of its own propagation, with its own geopotential field since 11.0.0 (before, solutions sharing a `CelestialBody` with a geopotential could not) |
| `PropagationSolution.GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance` (and the internal `InterpolateAt`, `EvaluateVariational`, `EvaluateCovariance`, `EvaluateExtendedCovariance`) | Serialized per propagator instance | Same lock and the same rule as `GetStateVector`: they evaluate the forces of their propagation inside a step |
| A published `PropagationSolution` and `Spacecraft.ExecutedManeuvers` | Yes | Immutable from outside the library: its segments, steps, output states and records are read-only views (decision 55 of #668), and `ExecutedManeuvers` is a new read-only list per run, swapped in at the re-arm (empty) and at each maneuver the run executes (a new list that adds it, decision 58), so a reader on another thread during a run sees the maneuvers executed so far and a list once read never changes |
| `ConjunctionAssessment.Analyze`, `AnalyzeAll` with a propagated covariance, `EvaluateAvoidance` | Serialized per propagator instance | The replay of the variational equations, the propagations of the non-linearity index and of the avoidance options use the forces of the participant's propagation (in RK7(8) or by Velocity-Verlet for the avoidance options), under the lock of the forces of its propagator, which its solution and the force model its spacecraft keeps carry, each with its own geopotential field |
| Managed occultation search (`FindWindowsOnOccultationConstraint`, `IsOcculted`) and field-of-view search (`Instrument.FindWindowsInFieldOfViewConstraint`, `IsInFOV`) | Yes | They keep no state and read the kernels through the shared lock of `SpiceAPI`, so searches on separate propagations may run concurrently. A search reads the attitude without changing it: the attitude record of a managed spacecraft is filled by its propagation only (and `AddStateOrientationToICRF`), and a read stores nothing ([#523](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/523); see the next row), so the attitude a search reads depends neither on earlier reads nor on the order of the threads. The frame of a kernel-backed spacecraft reads SPICE at each call and caches nothing. Searches on the same spacecraft or on fresh ones, concurrently or in sequence, give the same windows to the tick (`FieldOfViewSearchTests.ConcurrentSearches_EqualSequentialSearches`, `OccultationSearchValidationTests.ConcurrentSearches_GiveTheResultsOfSequentialOnes`) |
| Attitude record of a managed spacecraft (`SpacecraftFrame`) and the frames only the library holds | Yes | The record is read under a lock of the record. An ephemeris read in the frame of a managed spacecraft with an attitude record, or of a site built from coordinates, is read in J2000 and rotated by that frame under the shared SPICE lock ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)): the record lock nests inside the SPICE lock, so no region of the record lock calls SPICE, which keeps the two locks free of an inversion. A `SiteFrame` reads its body's frame through SPICE under the same lock, which is re-entrant |
| `CovariancePropagationOptions`, `RtnProcessNoise`, `ConsiderParameters`, `ManeuverExecutionError` | Yes | Immutable |

## One Run at a Time per Spacecraft

A spacecraft takes one run at a time (decision 43 of #668, decided by S. Guillet, 2026-11-23). Every run re-arms its
spacecraft before it integrates (stored states and attitude record dropped, tanks refuelled, `ExecutedManeuvers`
emptied, the plan run again from its first maneuver) and writes its results on it as it goes, and the lock a run holds
is that of its propagator instance, not of the spacecraft. So two runs on one spacecraft through different paths, two
`Spacecraft.Propagate` calls, two propagator instances, a `TLEPropagator` and a numerical run, a scenario that holds the
spacecraft and a direct run, interleave with no exception: in the gap analysis of lot L5, two concurrent
`Spacecraft.Propagate` calls with an apogee raise left the tank at 841.263915 kg in 1 trial of 12, against
917.541305 kg for a run alone, the burn charged twice, while both returned solutions were those of a run alone
([#793](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/793)).

- Run a spacecraft from one thread at a time, and read what a run writes on it (its stored states, attitude record,
  fuel, `ExecutedManeuvers`) once the run has returned. Two concurrent `Propagate` calls on one propagator instance
  serialize ([The Lock of a Propagator Instance](#the-lock-of-a-propagator-instance)), but a second instance, or
  `Spacecraft.Propagate`, on the same spacecraft does not.
- Propagate separate spacecraft in parallel: a `BatchPropagator` refuses a spacecraft given to two of its tasks
  (`ArgumentException`, [BatchPropagator Constraints](#batchpropagator-constraints)), a Monte Carlo campaign builds a
  spacecraft per run from its template, and `Scenario.Simulate` runs its spacecraft one after the other.
- Do not change the plan of a spacecraft during a run on it (decision 96 of #668, decided by S. Guillet, 2026-11-24):
  the run reads the plan as it goes, its head at the re-arm and each next maneuver when the previous one ends, so a
  `SetManeuvers` from another thread, or from code the run calls, mixes the two plans (the plan [m1, m2] set again
  during a run of [m1, m2] left `ExecutedManeuvers` reading m1, m1 in the second review of L5a). Set it before the run,
  or once the run has returned.
- Nothing checks the rule yet: a guard that refuses a second run on a spacecraft while one is in progress, with
  `InvalidOperationException` before anything changes, comes after lot L5 in #793, and the same guard makes
  `SetManeuvers` throw `InvalidOperationException` while a run holds the spacecraft.

## The Lock of a Propagator Instance

A propagator instance has one lock for its forces, kept across its runs (#782, decisions 25 and 28 of lot L4 of #668,
decided by S. Guillet, 2026-11-22). Each run of the instance holds it from its start to its end, the forces you added to
an integrator (a `ForceBase` of your own) and a bare `IIntegrator` included; the evaluations of the solutions of all its
runs (`GetStateVector`, `GetStateTransitionMatrix`, `GetCovariance`, …, the replay of a conjunction analysis) take it.
Three consequences follow:

- **Lock order.** Code a run calls (a `ForceBase`, a bare `IIntegrator`) must not evaluate the solution of another
  propagator that may be running: two reused instances whose forces evaluate each other's earlier solutions deadlock,
  each run holding its own lock and waiting for the other's. The library itself evaluates no solution under the SPICE
  lock or the lock of an attitude record, so it cannot deadlock by itself.
- **An evaluation waits for a running run.** An evaluation of an earlier solution of an instance, on another thread,
  waits for the end of the run of that instance in progress, and then gives the values it gave before that run
  (`PropagatorReuseTests`); query the solutions of an instance between its runs, or build a new propagator per run, as
  `Spacecraft.Propagate`, `Scenario.Simulate`, `BatchPropagator` and `MonteCarloPropagator` do.
- **A waiting `Propagate` reads its token once it holds the lock.** A second `Propagate` on an instance that is running
  waits for the first to end before it reads its `CancellationToken`, so a token cancelled while it waits stops it only
  once it holds the lock, before anything changes ([Cancellation](../reference/propagators.md#cancellation)).

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
    var spacecraft = new Spacecraft(-(1000 + i), $"Sat-{i}", 100.0, new StateVector(orbit.Position, orbit.Velocity, earth, orbit.Epoch, orbit.Frame),
        10000.0, new Clock($"clk-{i}", 256));
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

- **Duplicate detection**: Reusing the same `Spacecraft` instance across tasks throws `ArgumentException`. The
  check is within one batch: a run of the same spacecraft outside the batch while it runs is not refused, and breaks
  the rule of [one run at a time per spacecraft](#one-run-at-a-time-per-spacecraft).
- **Fault isolation**: A failed task does not cancel or corrupt other tasks.
- **Integrators**: each task builds its own integrator from its `PropagationOptions`, so one options value serves
  every task.

## See Also

- [Propagators](../reference/propagators.md)
- [Force Models](../reference/force-models.md)
- [High-Fidelity Propagation](high-fidelity-propagation.md)
