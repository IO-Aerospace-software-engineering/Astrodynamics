# Propagators

The propagation subsystem provides numerical and analytical orbit propagation through a segment-based architecture with event detection and dense output interpolation.

## CentralBodyPropagator

`CentralBodyPropagator` is the primary numerical propagator for LEO, GEO, lunar, and interplanetary missions. The central body is inferred from the spacecraft's initial orbital parameters.

### Propagation Frame

The initial state may be given in any inertial frame: ICRF, GCRF, B1950, FK4, ECLIPJ2000, ECLIPB1950 or GALACTIC. The
propagation runs in that frame, and the output states are in it. Every force input is evaluated in it as well: the
ephemeris cache of the bodies, the third-body positions and the rotation of the atmosphere. The trajectory therefore
does not depend on the inertial frame chosen, apart from rounding and from the step sequence of an adaptive
integrator, whose error control scales each component separately. GCRF has the axes of ICRF (the identity rotation,
[ICRF, GCRF And EME2000](frames.md#icrf-gcrf-and-eme2000)): a propagation from a GCRF state is, bit for bit, that of
its ICRF twin, labelled GCRF, with both integrators, a covariance and in SSB mode (#543; before 11.0.0 the propagator
rejected it).

A TLE or an osculating state in TEME is accepted too (#675; before 11.0.0 every path rejected it, "must be defined in
inertial frame"), and rotated once to ICRF, where the propagation runs and its states are: a TLE starts from its SGP4
state at the start of the window, on the Earth of the TLE (its observer); a TEME state from itself at its epoch, its
covariance through the Jacobian (TEME is quasi-inertial: v' = R v, P' = diag(R, R) P diag(R, R)ᵀ,
[TEME](frames.md#teme)). The run is bit for bit that of its ICRF twin, `start.ToFrame(Frame.ICRF)` on the same
observer, on every path: `Spacecraft.Propagate`, `Scenario.Simulate`, `BatchPropagator`, `MonteCarloPropagator` and
the constructors. The target orbit of an impulse of such a spacecraft is given
in ICRF: a target in another frame, TEME included, is rejected when the propagation is built. On every spacecraft the
target orbits of the impulses are given in the frame of the propagation (GCRF and ICRF, which share their axes, counting
as one), and any other frame is rejected when the
propagation is built (`ArgumentException`) and, for a maneuver attached after it was built, when it propagates
(`InvalidOperationException`) (#685; before 11.0.0 only a TLE or TEME spacecraft was checked there,
[The frame of a target orbit](impulse-maneuvers.md#the-frame-of-a-target-orbit)). CIRS, TIRS, the rotating frames and a set of mean elements in TEME
(`tle.ToMeanKeplerianElements()`, which has no state without its theory) are still rejected. For a TLE, see
[Numerical propagation of a TLE](tle.md#numerical-propagation-of-a-tle): its point-mass Earth is not SGP4.

### Propagation Loop

1. Build event detectors from the spacecraft maneuver chain.
2. Integrate a segment until the next event or end of window.
3. Execute the maneuver via `IEventDetector.HandleEvent()`, update state; lay the attitudes chained after an impulse on
   the attitude timeline, from the end of its maneuver window, and build the detector of the next impulse. An impulse
   executed at the end of the window opens a last segment of zero length, so the solution ends with the state after it
   (#575).
4. Compute output states on the requested output step with `PropagationSolution.InterpolateAt`, then the attitude
   record from the timeline on the solution ([Attitudes](attitudes.md#the-attitude-timeline)). The outputs are the
   window start plus i·ΔT inside the window, then the window end, in place of the last grid point when that lies within
   ΔT/10 of the end: round(L/ΔT) + 1 of them over a window of length L, and the propagation runs to the window end, on
   every path (`TLEPropagator` included, one rule), so that the stored states, the reads of the spacecraft and its SPK
   cover the whole window and nothing is read past it: a window of 3 610 s at 60 s holds 62 outputs, to 3 600 s then
   3 610 s, one of 3 603 s 61, the last at 3 603 s, one of 3 590 s 61, to 3 540 s then 3 590 s. A grid that ended
   before the window gained its end in 11.0.0
   ([#741](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/741), decided by S. Guillet,
   2026-11-18; before, the outputs stopped up to ΔT/2 before the window end, where a read of the spacecraft throws,
   #527), and so did a grid that rounds the window up (R11 of #668, structuring choice 6, decided by S. Guillet,
   2026-11-19; before, the propagation ran to its last output, up to ΔT/2 past the window end, #626). Against the former
   grid (decision 8 of lot L4 of #668): in RK7(8) only the last output moves; in Velocity-Verlet the outputs inside the
   last integration step move too when the integration step does not divide ΔT, the last step now ending at the window
   end (67.48 m at the output of 3 540 s of a 3 590 s window, Velocity-Verlet at 100 s about the Earth, 60 s outputs).
   A window of zero length throws `ArgumentException` (`window`) at the start of the call, on every path that builds a
   numerical propagation, in `Scenario.Simulate` before its stars, in `MonteCarloPropagator.Propagate` before anything is
   sampled and in the `TLEPropagator` constructor, one rule on
   every propagation path (decisions 7 and 15 of lot L4, decided by S. Guillet, 2026-11-22; before, a numerical
   propagation re-armed the spacecraft and threw `InvalidOperationException` "Solution contains no segments", and
   `TLEPropagator` returned one sample at the start).

### Supported Perturbations

- Central-body gravity
- Third-body perturbations
- EGM2008 geopotential
- Atmospheric drag
- Solar radiation pressure
- Albedo radiation pressure
- Thermal radiation pressure

### Propagation Options

`PropagationOptions` (R06 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), lot
L4, 11.0.0) holds every setting of a numerical propagation, as values: each run builds its own integrator, forces and
geopotential field from them, so one value serves any number of propagations, on any thread. A variant is
`PropagationOptions.Default with { … }`; a one-property check runs in `init`, and so again in a `with`; the rules between
the settings and the inputs run at the start of the call, before anything changes.

```csharp
// Inputs: spacecraft (Spacecraft), window (Window)
var rk = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 };
var options = PropagationOptions.Default with
{
    Integrator = rk,
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
    AtmosphericDrag = true,
    SolarRadiationPressure = true,
    Geopotential = GeopotentialModelParameters.Egm2008(10)
};
var solution = spacecraft.Propagate(window, options);
Console.WriteLine($"{solution.Options.ThirdBodies.Count} third bodies, {solution.Options.Atmosphere.GetType().Name}");
```

| Property | Default | Description |
|----------|---------|-------------|
| `Integrator` | `RK78IntegratorOptions.Default` | The integrator: `RK78IntegratorOptions` (RK7(8), the tolerances of 1e-9 of `new RK78Integrator()`, `FixedStep` for the fixed-step mode) or `VVIntegratorOptions` (Velocity-Verlet, `Step` 1 s); see [Integrators](integrators.md#integrator-options). Null throws |
| `OutputStep` | 60 s | The step of the output states, an output step only: the integrator takes its own steps |
| `ThirdBodies` | null | The bodies other than the central body: none on `Spacecraft.Propagate`, `CentralBodyPropagator`, `PropagationTask` and `MonteCarloConfiguration.Propagation`; the bodies of the scenario on `Scenario.Simulate`, where a list that differs from them throws. Held as a copy, compared by value |
| `AtmosphericDrag`, `SolarRadiationPressure` | false | The forces of the flags of the constructors |
| `AlbedoRadiationPressure`, `ThermalRadiationPressure` | false | The albedo and thermal radiation pressure of the central body, which needs the coefficients of its `CelestialBody` constructor: a coefficient of 0, the value of every predefined body, throws `ArgumentException`. Another body's goes through the constructor that takes an integrator with its forces |
| `Geopotential` | null | The geopotential of the central body, a fallback: a body without a model takes it, a body with an equal model runs with it, a body with another model throws `ArgumentException` naming both; null, the body's ([Force Models](force-models.md#geopotential-model-parameters)) |
| `Atmosphere` | null | The atmosphere of the central body's drag, the same fallback (an Earth without a model counts as not given: the option replaces its NRLMSISE-00 at nominal space weather); about the Sun it throws |
| `CovariancePropagation` | null (Auto) | The covariance options; Auto propagates the covariance of the initial orbital parameters under RK7(8) when it is given at the window start, and says otherwise why in `GetCovariance`; an indefinite one throws, naming `StateVector.WithRemediatedCovariance()` and `CovariancePropagationOptions.None`, which turns it off ([Covariance Propagation](covariance-propagation.md#activation)) |

!!! warning "Covariance Auto: the cost of an explicit request"
    With `CovariancePropagation` null (Auto), a covariance that the initial orbital parameters carry at the window start
    is propagated under RK7(8), with the options `new CovariancePropagationOptions { Mode =
    CovariancePropagationMode.Covariance }` exactly, so the run costs what asking for the covariance always cost, not a
    new cost: over a LEO day at the RK7(8) defaults, 3 to 4 times the run without covariance about a point-mass Earth,
    about 10 times with EGM2008 10×10 and drag, 1.5 times in SSB mode (`DefaultIntegratorFiguresTests.CovarianceAuto_Cost`,
    Linux), the trajectory unchanged bit for bit. Pass `CovariancePropagation = CovariancePropagationOptions.None` to
    propagate the state alone ([Covariance Propagation](covariance-propagation.md#activation); decision 17 of lot L4 of
    #668, decided by S. Guillet, 2026-11-22).

The options entry points are `Spacecraft.Propagate(window, options)`, `CentralBodyPropagator(window, spacecraft,
options)`, `Scenario.Simulate(options)` (synchronous, the spacecraft in turn), `PropagationTask(spacecraft, window,
options)` with `BatchPropagator.Propagate`, and `MonteCarloConfiguration.Propagation`; no async overload takes them
(resolution C2 of the design). Each default equals its explicit settings (`IntegratorDefaultsTests`): on every entry
point a run without settings is the run of `new RK78Integrator()` about the central body alone, every 60 s, and
`VVIntegratorOptions.Default` is `new VVIntegrator(TimeSpan.FromSeconds(1))`.

The members that took the settings as arguments were removed in 11.0.0 (R07 of #668, section 7 of the re-baseline: no
forwarder): the flag overloads of `Spacecraft.Propagate`, `PropagateAsync`, the flag constructors of
`CentralBodyPropagator`, the three `Scenario.SimulateAsync`, the `CentralBodyPropagatorBuilder`, the flags constructor of
`PropagationTask` and the factories of `MonteCarloConfiguration`. Their runs are those of the options naming the same
integrator, bodies and flags, bit for bit (lot L4, step 1). The members without an integrator stepped Velocity-Verlet
at the output step: pass `new VVIntegratorOptions { Step = step }` with `OutputStep = step` to keep their results bit for
bit (1 442 of 1 442 states over a LEO day at 60 s with the Moon, the Sun, drag, SRP and a burn, on every path), or keep
the default RK7(8): the tutorial's point-mass LEO day ends 974 km from the Keplerian closed form with Velocity-Verlet at
60 s, 1.36 m with RK7(8) at 1e-9.

`PropagationSolution.Options` holds the resolved settings of a run of these entry points, with no null left where a
value applies: the third bodies of the run (the central body aside), the geopotential of the central body (null for a
point mass), the atmosphere of its drag (`CelestialBody.Atmosphere`: its model, NRLMSISE-00 at nominal space weather for
an Earth without one, or null), and the covariance options the run used (`CovariancePropagationOptions.None` when it
propagated none). Passed back to the same entry point, they repeat the run. A run of the other constructors has none.

Each run builds the geopotential field of the central body from its model, over the coefficients the model reads once:
propagations, a batch, a scenario and a Monte Carlo campaign may share one `CelestialBody` with a geopotential, on any
thread, bit for bit the runs of separate bodies (section A of the re-baseline of #668, decided by S. Guillet, 2026-11-19;
before 11.0.0 a propagation evaluated the field of the body, whose buffers are not thread-safe). The body's own
`EvaluateGravitationalAcceleration` still evaluates its field, on one thread at a time.

### CentralBodyPropagator Constructors

| Constructor | Description |
|-------------|-------------|
| `CentralBodyPropagator(window, spacecraft, options)` | The settings of a `PropagationOptions` value, null for `PropagationOptions.Default` (above): the propagator builds its integrator and forces |
| `CentralBodyPropagator(window, spacecraft, integrator, deltaT)` | Use a pre-configured integrator (forces already added), which the propagator initializes with its initial state: the path of a force list of your own (albedo or thermal radiation of a body other than the central body, a force of your own) |
| `CentralBodyPropagator(window, spacecraft, integrator, deltaT, covariancePropagation)` | As the second (the integrator initialized by the propagator), with the covariance propagation; the columns of Ψ follow the forces of the integrator |

When the propagator builds the forces (the options constructor) the central body takes part with the models of the
observer: it is inserted first when no listed body has its NAIF identifier (a run that lists it is unchanged), and a
listed body of its identifier, or two listed bodies of one identifier, must have the same GM, radii, gravitational field
and atmospheric model (`ArgumentException` otherwise). Drag is that of the central body's atmosphere, NRLMSISE-00 at
nominal space weather for an Earth without a model, and of each other listed body with an atmosphere, an Earth always
having one ([Force Models](force-models.md#atmosphericdrag)). A central body whose GM is not above 0, a body of the kernels whose
`BODYnnn_GM` the kernel pool lacks (no GM kernel such as NAIF's `gm_de440.tpc`), throws `ArgumentException`
(`spacecraft`) naming it, on every constructor and so on every propagation path, before the integrator is claimed (11.0.0,
R03 of #668: before, the propagation ran without central gravity, on a straight line; `new CelestialBody(id)` still accepts
the GM of 0); the constructors without celestial bodies use the integrator and its forces as
given and initialize it with the initial state of the propagation: about the central body (the Sun in SSB mode), in the
inertial frame of the propagation, at the TDB start of the window; a later `Initialize` by the caller replaces it. Before
11.0.0 they used it as given, and one its caller had not initialized threw `NullReferenceException` at the first step. A step of zero or less throws `ArgumentOutOfRangeException`: `deltaT` on the
constructors that take an integrator, `OutputStep` (or the integrator's `Step`) where the options are built (#674).

A constructor changes nothing on the spacecraft. `Propagate` re-arms it once its checks pass, on every path, as
`Spacecraft.Propagate` does: the tanks refuelled, `ExecutedManeuvers`, the stored states and the attitude record of the
previous propagation cleared, the maneuver chain started again from its head, and the spacecraft `IsPropagated` after
the run (#583, decided by S. Guillet, 2026-11-17; before 11.0.0 a second run through a propagator built directly fired no
burn and started with the fuel the first left).

The covariance propagation needs an `RK78Integrator` (`NotSupportedException` otherwise). Without it, a covariance of the
initial orbital parameters is not propagated: it stays on the initial state, the output states carry none, and no partial
derivative is evaluated.

### PropagationSolution

`PropagationSolution` is the multi-segment container returned by propagation. It holds `PropagationSegment` objects, each containing `AcceptedStep` records that support Hermite interpolation. An RK7(8) solution also keeps the dynamics of the propagation, to compute its states between the steps at the accuracy of the integrator.

| Member | Description |
|--------|-------------|
| `Segments` | Read-only list of `PropagationSegment` objects |
| `StateVectors` | Output state vectors at the requested cadence |
| `InterpolateAt(Time epoch)` | State at any epoch within the solution window; returns `(Vector3 position, Vector3 velocity)`. RK7(8): a shortened RK7(8) step from the start of the accepted step (13 force evaluations inside a step, none at a boundary); Velocity-Verlet: cubic Hermite interpolation. At a maneuver, the state after it |
| `GetStateVector(Time epoch)` | The state of `InterpolateAt` as a `StateVector`, with its covariance when the propagation propagated it; throws outside the propagation |
| `Options` | The resolved settings of a run of `PropagationOptions` (above); null for the constructors that take an integrator |
| `CovariancePropagation`, `SensitivityParameters`, `ExtendedCovarianceParameters` | The options of the covariance propagation (null when off), the columns of Ψ, the consider parameters of the extended covariance |
| `GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance` | Φ, Ψ, P and the extended covariance at any epoch; see [Covariance Propagation](covariance-propagation.md#results) |

In covariance mode, the output states (`StateVectors`) carry their covariance. The SPK written from the propagated states
(`Spacecraft.WriteEphemeris`) holds positions and velocities only: the covariance is read from the solution.

When conjunction assessment follows propagation, pass `PropagationSolution` directly to the SSA overloads to reuse the dense output.

## TLEPropagator

`TLEPropagator` provides SGP4/SDP4 propagation for TLE-driven workflows. Use it for catalog screening or when working with mean-element data sources.

```csharp
// Inputs: window (Window), spacecraft (Spacecraft), deltaT (TimeSpan)
var propagator = new TLEPropagator(window, spacecraft, deltaT);
var solution = propagator.Propagate();
```

| Property | Description |
|----------|-------------|
| `Window` | Propagation time window |
| `Spacecraft` | The spacecraft being propagated |
| `DeltaT` | Output time step |

The samples follow the rule of the numerical propagation, the window start plus i·`DeltaT`, counted in ticks, then the window end: ceil(L/ΔT) + 1 samples for a
window of length L, the last step shorter when ΔT does not divide L (#598); a grid point closer to the end than ΔT/10
is dropped, the last step then up to 1.1 ΔT (a window given in UTC is a few ticks longer or shorter in TDB, its
samples counted on its TDB start and end). A step of zero or less throws
`ArgumentOutOfRangeException` (`deltaT`), and a window of zero length `ArgumentException` (`window`), as on every
propagation path (decision 15 of lot L4 of #668; before 11.0.0, one sample at the start). Before 11.0.0 the samples were counted in whole seconds: a step below 1 s
threw `DivideByZeroException`, a fractional step ran past the window end (1.5 s over 10 s: to +15 s), and a window
that the step does not divide lost its end (90 s at 60 s: the last sample at +60 s).

The solution holds the SGP4 states at the samples, and between them the cubic Hermite of `PropagationSegment` on the
positions, the velocities and the accelerations of SGP4 at the samples (the central difference of its velocity at
±1 s, #675; zero before 11.0.0, which put the interpolated velocity up to 49.9 m/s off SGP4). For the ISS at 60 s,
`InterpolateAt` and `GetStateVector` are 4.3e-4 m/s and 0.38 m off SGP4 between the samples (the same on Linux x64,
Windows and macOS arm64), and exact at them; `tle.ToStateVector(t)` is the exact SGP4 state at any instant. A TLE whose
SGP4 fails within 1 s of an end of the window throws, before the spacecraft receives any sample.

## BatchPropagator

`BatchPropagator` propagates multiple spacecraft in parallel with fault isolation, cancellation support, and progress reporting.
A task is `new PropagationTask(spacecraft, window, options)`, the settings of a [Propagation Options](#propagation-options)
value that every task may share (since 11.0.0 `PropagationTask` is not a positional record, has no `Deconstruct`, and
its constructor of flags and an integrator factory was removed, R07 of #668).

```csharp
// Inputs: spacecraft1 and spacecraft2 (Spacecraft), window (Window)
var options = new PropagationOptions
{
    Integrator = new RK78IntegratorOptions
    {
        AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
    },
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
};
var tasks = new List<PropagationTask>
{
    new PropagationTask(spacecraft1, window, options),
    new PropagationTask(spacecraft2, window, options)
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

`PropagationOptions.CovariancePropagation` propagates the state transition matrix and the covariance of a task; it needs
`RK78IntegratorOptions`, otherwise the task fails with a `NotSupportedException` in its result.

Key features: fault isolation (a failed task does not corrupt others), a `CancellationToken` that stops the running tasks too ([Cancellation](#cancellation)), an `IProgress<BatchProgress>` for tracking, duplicate `Spacecraft` detection. The token and the progress are parameters, `Propagate(tasks, options, cancellationToken)` and `Propagate(tasks, progress, options, cancellationToken)`; `BatchOptions` holds the parallelism alone, and `PropagateAsync` is removed (11.0.0, R28 of #668). `Propagate(tasks, null)` is ambiguous between the two overloads (CS0121): write `Propagate(tasks, options: null)`.

Each maneuver draws on a fuel tank of the spacecraft of its task, and a tank belongs to one spacecraft
(`Spacecraft.AddFuelTank` rejects a tank held by another): distinct spacecraft imply distinct maneuvers and engines. A
task whose maneuver draws on another spacecraft's tank, or on a tank of none, fails before it runs with an
`InvalidOperationException` in its result (#676, #586; before, it burnt the other spacecraft's fuel and wrote its
records, or threw `NullReferenceException` at the burn).

## MonteCarloPropagator

`MonteCarloPropagator` runs dispersion analysis by sampling initial state covariance, propagating each perturbed state via `BatchPropagator`, and aggregating per-epoch statistics.

### MonteCarloConfiguration

| Property | Type | Description |
|----------|------|-------------|
| `RunCount` | `int` | Number of runs (minimum 2) |
| `Seed` | `int` | Random seed for reproducibility |
| `TemplateSpacecraft` | `Spacecraft` | Nominal spacecraft with 6x6 covariance |
| `Window` | `Window` | Propagation time window |
| `Propagation` | `PropagationOptions` | The settings of every run ([Propagation Options](#propagation-options)); null for `PropagationOptions.Default`. The runs share their bodies, a geopotential included (11.0.0: `CelestialBodiesFactory`, `DeltaT`, `IncludeAtmosphericDrag`, `IncludeSolarRadiationPressure` and `IntegratorFactory` removed, R07 of #668) |
| `BaseNaifId` | `int` | Base NAIF ID (default -100000) |
| `BatchOptions` | `BatchOptions` | The parallelism of the runs; null for `BatchOptions.Default` (the token and the progress are parameters of `MonteCarloPropagator.Propagate` since 11.0.0) |

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
// Inputs: spacecraft (Spacecraft)
var config = new MonteCarloConfiguration
{
    RunCount = 100,
    Seed = 42,
    TemplateSpacecraft = spacecraft,
    Window = new Window(Time.J2000TDB, Time.J2000TDB.AddHours(2)),
    Propagation = new PropagationOptions
    {
        Integrator = new RK78IntegratorOptions
        {
            AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
        },
        ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
    }
};

var result = MonteCarloPropagator.Propagate(config);

Console.WriteLine($"Success: {result.SuccessCount}/{result.TotalCount}");
var last = result.EpochStatistics[^1];
Console.WriteLine($"Position 95th percentile RSS: {last.RssPositionPercentiles.P95:F1} m");
```

## Cancellation

The methods that run take a `CancellationToken` as their last parameter (R28 of #668, decision 4 of lot L4, decided by
S. Guillet, 2026-11-22): `Spacecraft.Propagate(window, options, cancellationToken)`,
`Scenario.Simulate(options, cancellationToken)`, `IPropagator.Propagate(cancellationToken)` (`CentralBodyPropagator`
and `TLEPropagator`), `BatchPropagator.Propagate(…, cancellationToken)` and
`MonteCarloPropagator.Propagate(config, [progress,] cancellationToken)`. No constructor, record or property holds a
token. The work is synchronous: the asynchronous members over it (`Spacecraft.PropagateAsync`, `Star.PropagateAsync`,
`Scenario.SimulateAsync`, `BatchPropagator.PropagateAsync`, `MonteCarloPropagator.PropagateAsync`) are removed in
11.0.0, and a caller that wants a task runs the call in one.

```csharp
// Inputs: spacecraft (Spacecraft), window (Window)
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
try
{
    var solution = spacecraft.Propagate(window, PropagationOptions.Default, cts.Token);
}
catch (OperationCanceledException)
{
    // Not propagated: no stored state, and the next Propagate re-arms the spacecraft
}

// A task of the synchronous call, cancelled with the token
var task = Task.Run(() => spacecraft.Propagate(window, cancellationToken: cts.Token), cts.Token);
```

Where the token is read:

| Path | Read |
|------|------|
| `Spacecraft.Propagate`, `CentralBodyPropagator.Propagate` | Once the checks pass, before the spacecraft is re-armed; before each segment; by `VVIntegrator` and `RK78Integrator` at each accepted step, never between the rejections of a step (at most 101). Not after the last segment, so the outputs, the attitude record and the stored states are written as one block |
| An `IIntegrator` implemented outside the library | Between its segments only (the interface does not change) |
| `TLEPropagator.Propagate` | Before the first sample and at each sample; a stopped run adds nothing to the spacecraft |
| `Scenario.Simulate` | Once the settings are checked, before anything changes; before each star and each spacecraft; inside each propagation |
| `BatchPropagator.Propagate`, `MonteCarloPropagator.Propagate` | Before anything runs; before each task; inside each running propagation (before 11.0.0 the token of `BatchOptions` was read before each task only, and a running task ran to its end) |

A token already cancelled throws before anything changes. Every path throws `OperationCanceledException` carrying the
caller's token: a batch and a Monte Carlo campaign throw one, never an `AggregateException`. The bound is one step: a
propagation stopped inside a step makes only the force evaluations left in it (measured: 2 to 4 for RK7(8) about the
Earth, 8 in SSB mode, none for Velocity-Verlet, whose one evaluation per step is the one in progress;
`CancellationTests`).

What a stopped run leaves (C2 of lot L4):

- the spacecraft it stopped in holds no stored state and no attitude record (those added by hand to a spacecraft never
  propagated, `AddPropagatedStates` and `AddStateOrientationToICRF`, included), is not `IsPropagated`, and keeps nothing
  for the conjunction analysis; a TLE spacecraft reads SGP4 again, any other its initial orbit;
- its fuel, `ExecutedManeuvers` and standby chain are advanced up to the stop: an impulse executed before it has burnt
  its fuel; the next propagation re-arms the spacecraft, on every path, and runs as for a spacecraft never propagated;
- in `Scenario.Simulate` and a batch, the stars and spacecraft completed before the stop are complete, the later ones
  untouched; `IsSimulated` stays false, and no summary or result is returned.

The propagators hold no resource: `IPropagator`, `PropagatorBase` and `TLEPropagator` no longer implement
`IDisposable` (their `Dispose` was empty, or cleared two fields), so `using var propagator = new CentralBodyPropagator(…)`
does not compile (CS1674) and becomes `var propagator = …` (a binary break: a compiled caller is rebuilt).

## See Also

- [Integrators](integrators.md)
- [Force Models](force-models.md)
- [Conjunction Assessment](conjunction-assessment.md)
