# Integrators

Integrators advance spacecraft state through time under configured force models. Both integrators share a common interface and produce dense-output records.

## IIntegrator Interface

| Method | Description |
|--------|-------------|
| `Initialize(StateVector)` | Set initial conditions and force model context |
| `IntegrateSegment(pos, vel, baseEpoch, duration, eventDetectors)` | Advance state, returning `IntegrationResult` |

`IntegrationResult` contains the completed `PropagationSegment` and optional event information if a detector triggered during the segment.

## AcceptedStep

Both integrators record `AcceptedStep` entries containing position, velocity, and acceleration at the start and end of each step. These records enable cubic Hermite interpolation for dense output between steps.

## VVIntegrator

`VVIntegrator` is the default fixed-step symplectic integrator using the Velocity-Verlet scheme.
`CentralBodyPropagator` creates one with the propagator's `deltaT` as step when no integrator is
given.

| Constructor | Description |
|-------------|-------------|
| `VVIntegrator(TimeSpan deltaT)` | Fixed step; forces are added by the propagator |
| `VVIntegrator(IEnumerable<ForceBase> forces, TimeSpan deltaT, StateVector initialState)` | Fixed step, forces and initial state given directly |

| Property | Description |
|----------|-------------|
| `DeltaT` | The configured step (`TimeSpan`) |
| `DeltaTs` | The step in seconds |

- Symplectic: conserves energy over long durations for conservative systems.
- Second order: the global error grows with the square of the step (see below).
- Event detection occurs at step boundaries only (no sub-step refinement).

```csharp
var integrator = new VVIntegrator(TimeSpan.FromSeconds(1.0));
```

## RK78Integrator

!!! tip "Choosing an integrator"
    `RK78Integrator` is the default choice for high-fidelity work. Prefer `VVIntegrator`
    when a fixed step and symplectic energy behaviour matter more than accuracy per step.

`RK78Integrator` implements the Prince-Dormand 7(8) method: a 13-stage, 8th-order integrator with an embedded 7th-order error estimate for adaptive step control.

### Adaptive Constructor

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `absoluteTolerance` | `double` | `1e-9` | Absolute error tolerance |
| `relativeTolerance` | `double` | `1e-9` | Relative error tolerance |
| `initialStepSize` | `double` | `60.0` | Initial step size (s) |
| `minStepSize` | `double` | `1e-6` | Minimum allowed step size (s) |
| `maxStepSize` | `double` | `86400.0` | Maximum allowed step size (s) |
| `safetyFactor` | `double` | `0.9` | PI controller safety factor |
| `minFactor` | `double` | `0.2` | Minimum step-size reduction factor |
| `maxFactor` | `double` | `5.0` | Maximum step-size growth factor |

### Fixed-Step Constructor

```csharp
var fixedStep = new RK78Integrator(fixedStepSize: 30.0);
```

When created with a fixed step size, `AdaptiveMode` is `false` and the integrator uses constant steps.

### Properties

| Property | Description |
|----------|-------------|
| `AbsoluteTolerance` | Configured absolute tolerance |
| `RelativeTolerance` | Configured relative tolerance |
| `AdaptiveMode` | `true` for adaptive PI control, `false` for fixed step |

### Key Features

- **Adaptive PI step control**: Adjusts step size to maintain error within tolerances.
- **Sub-step event refinement**: Uses `BisectionEventFinder` to locate event times to ~1e-10 s precision within a step via Hermite dense output.
- **Accuracy**: on the 24-hour conformance cases, 3.6 m (SSO) to 13.1 m (LEO) from the GMAT
  references with tolerances 1e-11, the rest being model differences between the two tools; see
  [Validation](../guides/validation.md#measured-errors).

```csharp
// Adaptive (default)
var integrator = new RK78Integrator(
    absoluteTolerance: 1e-10,
    relativeTolerance: 1e-10,
    initialStepSize: 30.0);

// Fixed-step
var fixedIntegrator = new RK78Integrator(fixedStepSize: 10.0);
```

## Choosing the integrator

Velocity-Verlet is the default everywhere, with the step given to the propagator. Each entry point that propagates a
spacecraft can take another integrator:

| Entry point | How to choose the integrator |
|-------------|------------------------------|
| `CentralBodyPropagator` | Constructor taking an `Integrator` (the propagator adds the forces) or an `IIntegrator` already configured |
| `CentralBodyPropagatorBuilder` | `integrator` argument of the constructor |
| `Spacecraft.Propagate` | Overload `Propagate(window, celestialBodies, integrator, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize)` |
| `BatchPropagator` | `PropagationTask.IntegratorFactory` |
| `MonteCarloPropagator` | `MonteCarloConfiguration.IntegratorFactory` |
| `Scenario.SimulateAsync` | Overload `SimulateAsync(includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize, integratorFactory)` |

An integrator receives the forces of the spacecraft it propagates, so it must not be shared between spacecraft:
the batch, Monte Carlo and scenario entry points take a factory that returns a new instance for each spacecraft.
`Scenario.SimulateAsync` refuses a factory that returns the same instance twice.

```csharp
var summary = await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(10.0),
    () => new RK78Integrator(absoluteTolerance: 1e-11, relativeTolerance: 1e-11));
```

## Accuracy And Cost

Velocity-Verlet is second order: halving the step divides the global error by four. Measured on
2026-10-04 on the LEO orbit of conformance case `propagator_24h_leo_grav10_001`:

| Integrator | Two-body position error after 24 h | Time per orbit, EGM2008 10x10 + Moon + Sun |
|------------|------------------------------------|--------------------------------------------|
| Velocity-Verlet, 0.5 s | 69.9 m | 21.0 ms |
| Velocity-Verlet, 1 s | 279.5 m | 10.5 ms |
| Velocity-Verlet, 2 s | 1,118 m | about 5 ms or about 24 ms, bimodal ([#346](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/346)) |
| RK7(8), tolerances 1e-11 | not measured in two-body; 13.1 m from the full-model reference | 5.5 ms |

Sources: errors from `VVIntegratorTests.GlobalErrorIsSecondOrderInTheStep`, against the analytic
Keplerian solution; times from `VVBenchmarks` and `RK78Benchmarks.LeoOneOrbit_EGM10_SunMoon`
(BenchmarkDotNet 0.14.0, medium run, AMD Ryzen 7 5800X, Fedora Linux 44, .NET 10). The step of
`VVIntegrator` is also the output cadence of the propagator; RK7(8) outputs every 10 s here.

With the full force model, the 1 s Velocity-Verlet error on this case is 294 m, of which 280 m is
truncation. For results below about ten meters, use `RK78Integrator`: on this case it is both more
accurate and faster than Velocity-Verlet at 1 s.

## See Also

- [Propagators](propagators.md)
- [Event Detection](event-detection.md)
- [Force Models](force-models.md)
