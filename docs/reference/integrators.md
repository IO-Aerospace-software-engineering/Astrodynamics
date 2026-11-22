# Integrators

Integrators advance spacecraft state through time under configured force models. Both integrators share a common interface and produce dense-output records.

## IIntegrator Interface

| Method | Description |
|--------|-------------|
| `Initialize(StateVector)` | Set the observer, the frame and the epoch of the evaluations, and reset every state a run depends on (step, step-control history, event bookkeeping); called by every `CentralBodyPropagator` constructor, at the start of every run and after each event |
| `IntegrateSegment(pos, vel, baseEpoch, duration, eventDetectors)` | Advance state, returning `IntegrationResult` |

`IntegrationResult` contains the completed `PropagationSegment` and optional event information (`EventInfo`) if a detector triggered during the segment; both are read-only record structs (11.0.0: their properties had public setters before).

The `duration` of `IntegrateSegment`, `PropagationSegment.Duration`, the `CumulativeTime` and `StepSize` of an
`AcceptedStep` and the time of an event count the seconds of the time argument of the propagation: TT about the Earth,
TDB about any other central body ([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501),
[Time & Windows](time-and-windows.md#the-time-argument-of-a-propagation)). The base epoch and the epochs the forces
receive are TDB labels. The two built-in integrators map the seconds to the epochs through the time argument the
propagator sets on them. A custom integrator, a bare `IIntegrator` or a subclass of `Integrator` whose
`IntegrateSegment` builds its own epochs from `baseEpoch`, keeps TDB seconds about the Earth too: its forces are then
evaluated up to 3.4e-10 relative in time from the instants of the propagation.

A `duration` of zero gives a segment of one step of zero length, which holds the start state: the propagator asks for it
after an impulse executed with no time left, so that the solution ends with the state after the impulse
([#575](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/575)). A custom integrator that
returns no step for it gets that step from the propagator, with the acceleration of its forces for a subclass of
`Integrator`, the end acceleration of the previous segment for a bare `IIntegrator` (a step of zero length is never
interpolated).

The cancellation token of a propagation ([Propagators](propagators.md#cancellation)) is read by `VVIntegrator` and
`RK78Integrator` at each accepted step, through an internal hook the propagator sets for its run, so the interface does
not change (R28 of #668); a custom integrator, a bare `IIntegrator` or a subclass of `Integrator`, is stopped by the
propagator between its segments, never inside one.

## AcceptedStep

Both integrators record `AcceptedStep` entries containing position, velocity, and acceleration at the start and end of each step. These records enable cubic Hermite interpolation between steps (`PropagationSegment.InterpolateAt`), which is exact at the step ends only.

Between the steps, the states of a propagation depend on the integrator:

- **Velocity-Verlet**: cubic Hermite interpolation of the accepted step.
- **RK7(8)**: a shortened RK7(8) step from the start of the accepted step that contains the epoch, with the accuracy of the integrator. The cubic Hermite interpolation is off by up to about 100 m in LEO at the default tolerance, because its error grows as the fourth power of the step size (#363). The output states, `PropagationSolution.InterpolateAt`, the location of the events and the state the propagation restarts from after a maneuver all use the shortened step.

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
| `DeltaT` | The configured step (`TimeSpan`); its seconds and their half, the internal `DeltaTs` and `HalfDeltaTs`, public before 11.0.0 (R06 of #668) |

- Symplectic: conserves energy over long durations for conservative systems.
- Second order: the global error grows with the square of the step (see below), with drag too: the acceleration at the
  end of a step is evaluated with the predicted end velocity v + h a (#439; the half-step velocity, before 11.0.0, made it
  of first order with a force that depends on the velocity).
- Event detection occurs at step boundaries only (no sub-step refinement); the step that holds an event is integrated
  again to the tick of the label of its end, the state the next segment starts from (#572).

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
- **Sub-step event refinement**: locates event times to ~1e-10 s within a step by bisection on states computed by shortened RK7(8) steps; the event time is then moved to the 100 ns grid of the epochs, so that the state at the event and the epoch the propagation restarts from are the same instant.
- **States between the steps**: each output state inside a step costs one RK7(8) step (13 force evaluations); an output at a step boundary reads the stored state. Over one day at the default tolerance, with EGM2008 10×10, the Moon and the Sun, the propagation takes about the same time with outputs every 600 s, about twice as long with outputs every 60 s, and seven to nine times as long with outputs every 10 s, as with the former cubic interpolation (measured on 2026-10-08, Linux x64, .NET 10).
- **State transition matrix and covariance**: the variational equations are integrated with the state, on the same
  stages, when the propagation asks for them ([Covariance Propagation](covariance-propagation.md)). By default the step is
  controlled on the state only, so the trajectory is the same bit for bit. Velocity-Verlet does not integrate them
  (`NotSupportedException`).
- **Failures**: the adaptive step fails with an `InvalidOperationException` after 100 consecutive rejections; with the
  variational equations in the step control (`IncludeInStepControl`), a step at the minimum step size that their error
  alone rejects is accepted on the error of the state when the minimum step size is below 10 ms
  ([#468](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/468)), and the adaptive step also
  fails after 10 000 consecutive accepted steps within 100 s of flight, a collapse of the step that the rejections
  never catch ([Covariance Propagation](covariance-propagation.md#options)).
- **Accuracy**: on the 24-hour conformance cases, 0.8 cm (GEO) to 2.7 cm (LEO) from the GMAT R2026a
  goldens of conformance-tests#25 with tolerances 1e-11 and the model's GM in the central term, over
  the TT seconds of the day (7 cm to 17 cm over its TDB seconds before #501), the rest being model
  differences between the two tools; see
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

## Integrator options

The options entry points take the integrator as values (`PropagationOptions.Integrator`, R06 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), 11.0.0), from which each run builds
its own integrator: the closed hierarchy `IntegratorOptions` (a record no type outside the library derives from), with
two sealed records. To change one setting of an options record's integrator, go through a typed local: `with` on the
abstract property does not compile (CS0117).

| `RK78IntegratorOptions` | Type | Default | `RK78Integrator` parameter |
|-------------------------|------|---------|----------------------------|
| `AbsoluteTolerance` | `double` | `1e-9` | `absoluteTolerance` |
| `RelativeTolerance` | `double` | `1e-9` | `relativeTolerance` |
| `InitialStep` | `TimeSpan` | 60 s | `initialStepSize` |
| `MinStep` | `TimeSpan` | 1 µs | `minStepSize` |
| `MaxStep` | `TimeSpan` | 1 day | `maxStepSize` |
| `SafetyFactor` | `double` | `0.9` | `safetyFactor` |
| `MinFactor` | `double` | `0.2` | `minFactor` |
| `MaxFactor` | `double` | `5.0` | `maxFactor` |
| `FixedStep` | `TimeSpan?` | null | `fixedStepSize`: the fixed-step mode; with another setting than its default, `ArgumentException` at the start of the call |

`RK78IntegratorOptions.Default` equals `new()` and builds the integrator of `new RK78Integrator()`, bit for bit: the
steps are exact (60 s, 10 ticks, 86400 s), and an entry point that takes no integrator runs RK7(8) at these tolerances
(a point-mass LEO day 1.36 m from Kepler; structuring choice 5 of #668, decided by S. Guillet, 2026-11-19). A step given
through `TimeSpan.FromSeconds` may be a tick short of its decimal value; the exact factories avoid it.
`VVIntegratorOptions.Step` is 1 s by default, the integration step only (the output step is
`PropagationOptions.OutputStep`): a point-mass LEO day ends 271 m from Kepler at 1 s, 27.1 km at 10 s and 974 km at 60 s.
These figures are the MEASURE lines of `DefaultIntegratorFiguresTests` (decision 12 of lot L4 of #668: 974.02 km,
27.12 km, 271.24 m, 1.364 m, and 1.062 mm at 1e-12, on Linux; the CI logs give the other platforms). In SSB mode about the
Earth (a LEO about the Solar System barycentre with the Earth, the Moon and the Sun), RK7(8) costs 7.5 to 13 ms per
propagated hour at 1e-9 (111 steps over 6 h) and 82 to 97 ms at 1e-12, which completes since #576 (93 233 steps over
6 h, the floor of the step control below 1e-10 in SSB mode, which depends on the orbit and the epoch: 5 210 for the hour
of the LEO of #544), and Velocity-Verlet 1.1 to 3.5 ms (four runs on Linux).
A value out of range (not finite, zero or negative) throws `ArgumentOutOfRangeException` naming the property, in `init`.

## Choosing the integrator

RK7(8) at the tolerances of 1e-9 is the default of every options entry point (`Spacecraft.Propagate`,
`CentralBodyPropagator(window, spacecraft, options)`, `Scenario.Simulate`, `PropagationTask` and
`MonteCarloConfiguration.Propagation`; structuring choice 5 of #668): `PropagationOptions.Integrator` takes another
one, as a value, and each run builds its own integrator from it, so one value serves every spacecraft of a scenario, a
batch or a campaign.

| Entry point | How to choose the integrator |
|-------------|------------------------------|
| Every options entry point | `PropagationOptions.Integrator`: `RK78IntegratorOptions` or `VVIntegratorOptions` |
| `CentralBodyPropagator` | The constructor taking an `IIntegrator` already configured with its forces and given to no other propagator, which it initializes with the initial state of the propagation at construction, at the start of every run and after each event (an `Initialize` by the caller has no effect on the run, #782); the integrator is claimed by the first constructor it is given to, and stays claimed when a later check rejects that constructor (a central body of GM 0, covariance options): build a new one (decision 27 of lot L4 of #668) |

The members that took an integrator instance, a factory or no integrator at all were removed in 11.0.0 (R07 of #668:
the flag overloads of `Spacecraft.Propagate`, the flag constructors of `CentralBodyPropagator`, the builder,
`Scenario.SimulateAsync`, `PropagationTask.IntegratorFactory`, `MonteCarloConfiguration.IntegratorFactory`): with no
integrator they stepped Velocity-Verlet at the output step, 974 km from Kepler after a LEO day at 60 s; `new
VVIntegratorOptions { Step = step }` with `OutputStep = step` keeps their results bit for bit. The step of
`VVIntegrator` and every step of the options must be positive (`ArgumentOutOfRangeException` otherwise).

```csharp
// Inputs: scenario (Scenario)
var summary = scenario.Simulate(new PropagationOptions
{
    Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-11, RelativeTolerance = 1e-11 },
    OutputStep = TimeSpan.FromSeconds(10.0)
});
```

## Accuracy And Cost

Velocity-Verlet is second order: halving the step divides the global error by four. Measured on
2026-10-04 on the LEO orbit of conformance case `propagator_24h_leo_grav10_001`:

| Integrator | Two-body position error after 24 h | Time per orbit, EGM2008 10x10 + Moon + Sun |
|------------|------------------------------------|--------------------------------------------|
| Velocity-Verlet, 0.5 s | 69.9 m | 21.0 ms |
| Velocity-Verlet, 1 s | 279.5 m | 10.5 ms |
| Velocity-Verlet, 2 s | 1,118 m | about 5 ms or about 24 ms, bimodal ([#346](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/346)) |
| RK7(8), tolerances 1e-11 | not measured in two-body; 0.027 m from the full-model reference over the TT day (0.11 m over the TDB seconds before #501; 13.2 m from the JGM3 golden before conformance-tests#25) | 5.5 ms |

Sources: errors from `VVIntegratorTests.GlobalErrorIsSecondOrderInTheStep`, against the analytic
Keplerian solution; times from `VVBenchmarks` and `RK78Benchmarks.LeoOneOrbit_EGM10_SunMoon`
(BenchmarkDotNet 0.14.0, medium run, AMD Ryzen 7 5800X, Fedora Linux 44, .NET 10). The step of
`VVIntegrator` is also the output step of the benchmark; RK7(8) outputs every 10 s here.

With drag the order holds: on a dense perigee pass (perigee 20 km and apogee 400 km above the equatorial radius, 45°,
10 min from 300 s before the perigee, 1 000 kg, 1e-4 m², Cd 2.2, the U.S. Standard Atmosphere 1976) the position error
against RK7(8) at 1e-13 is 6.09 m, 6.09e-2 m and 6.11e-4 m at 1, 0.1 and 0.01 s
(`VVIntegratorTests.GlobalErrorIsSecondOrderWithDrag`, Linux x64, 2026-11-17); before 11.0.0, 272.6 m, 27.8 m and 2.78 m,
first order (#439).

With the full force model, the 1 s Velocity-Verlet error on this case is 281 m, of which 280 m is
truncation (281.1 m against the GMAT golden of conformance propagator 001 over the TT day, Linux, Windows and macOS
arm64, 2026-11-01; 294 m against the JGM3 golden before conformance-tests#25). For results below about ten meters, use `RK78Integrator`: on this case it is both more
accurate and faster than Velocity-Verlet at 1 s.

## See Also

- [Propagators](propagators.md)
- [Event Detection](event-detection.md)
- [Force Models](force-models.md)
