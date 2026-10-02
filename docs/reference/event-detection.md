# Event Detection

The event detection system uses industry-standard g-function zero-crossing to locate maneuver trigger points and other state transitions during propagation.

## IEventDetector Interface

| Member | Description |
|--------|-------------|
| `Evaluate(StateVector)` | Compute the scalar g-function value at the given state |
| `Direction` | `CrossingDirection` specifying which zero-crossing triggers the event |
| `IsActive` | Whether this detector is currently active |
| `HandleEvent(StateVector, Spacecraft)` | Execute the event action and return an `EventResult` |

When `Evaluate` changes sign in the specified `Direction` between two integration steps, the event is triggered.

## EventResult

`EventResult` is a record returned by `HandleEvent` containing:

| Property | Description |
|----------|-------------|
| `ModifiedState` | The spacecraft state after the event (e.g., post-burn) |
| `Orientation` | The spacecraft orientation after the event |
| `Action` | `EventAction` — what the propagator should do next |
| `NextDetector` | The next event detector to arm (if chaining maneuvers) |

## EventAction

| Value | Description |
|-------|-------------|
| `StopAndRestart` | Stop the current segment and begin a new one with updated state |
| `Continue` | Continue integration without interruption |

## CrossingDirection

| Value | Fires When |
|-------|------------|
| `NegativeToPositive` | g-function crosses zero from negative to positive |
| `PositiveToNegative` | g-function crosses zero from positive to negative |
| `Any` | g-function crosses zero in either direction |

## ManeuverEventDetector

`ManeuverEventDetector` wraps an `ImpulseManeuver` and delegates to `ComputeEventValue()` for the g-function and `EventCrossingDirection` for the trigger direction. When the event fires, it applies the maneuver's delta-V and advances the maneuver chain.

## BisectionEventFinder

`BisectionEventFinder` performs sub-step root-finding to locate event times to approximately 1e-10 second precision. It uses Hermite dense output from `AcceptedStep` records to evaluate the g-function at arbitrary points within a step, then applies bisection to converge on the zero crossing.

`VVIntegrator` detects events at step boundaries only. `RK78Integrator` uses `BisectionEventFinder` for sub-step refinement.

## Maneuver G-Functions

| Maneuver | G-Function | Direction | Fires At |
|----------|-----------|-----------|----------|
| `ApogeeHeightManeuver` | r . v | `NegativeToPositive` | Perigee |
| `PerigeeHeightManeuver` | r . v | `PositiveToNegative` | Apogee |
| `PhasingManeuver` | r . v | `NegativeToPositive` | Perigee |
| `CombinedManeuver` | r . v | `PositiveToNegative` | Apogee (+ precondition) |
| `PlaneAlignmentManeuver` | r . h_target | Adaptive | Nearest orbital node |
| `ApsidalAlignmentManeuver` | r . (h x p) | `NegativeToPositive` | Nearest intersection |

## See Also

- [Impulse Maneuvers](impulse-maneuvers.md)
- [Integrators](integrators.md)
- [Propagators](propagators.md)
