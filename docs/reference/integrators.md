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

`VVIntegrator` is the community fixed-step symplectic integrator using the Velocity-Verlet scheme.

| Parameter | Type | Description |
|-----------|------|-------------|
| `stepSize` | `double` | Fixed time step in seconds |

| Property | Description |
|----------|-------------|
| `StepSize` | The configured step size (s) |

- Symplectic: conserves energy over long durations for conservative systems.
- Event detection occurs at step boundaries only (no sub-step refinement).
- Suitable for quick analyses where adaptive accuracy is not required.

```csharp
var integrator = new VVIntegrator(stepSize: 10.0);
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
- **Conformance-level accuracy**: Validated against reference trajectories (LEO, GEO, SSO).

```csharp
// Adaptive (default)
var integrator = new RK78Integrator(
    absoluteTolerance: 1e-10,
    relativeTolerance: 1e-10,
    initialStepSize: 30.0);

// Fixed-step
var fixedIntegrator = new RK78Integrator(fixedStepSize: 10.0);
```

## See Also

- [Propagators](propagators.md)
- [Event Detection](event-detection.md)
- [Force Models](force-models.md)
