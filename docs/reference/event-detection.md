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

`BisectionEventFinder` performs sub-step root-finding to locate event times to approximately 1e-10 second precision, by bisection on the zero crossing of the g-function within a step. Its public `FindRoot` evaluates the g-function on the cubic Hermite interpolation of an `AcceptedStep`.

`VVIntegrator` detects events at step boundaries only. `RK78Integrator` refines them within the step by the same bisection, on states computed by shortened RK7(8) steps from the start of the step, so that the event is located on the integrated trajectory. The event time is then moved to the 100 ns grid of the epochs, and the propagation restarts from the state of a shortened step to that time: at the accuracy of the integrator, and the same state from which the state transition matrix at the event comes.

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
