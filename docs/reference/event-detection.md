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
| `Continue` | Continue past the event with the state unchanged and the detector of `NextDetector`; the occurrence just handled is not detected again |

On `Continue`, the propagation starts a new segment at the event; `ModifiedState` is ignored, and the state
transition matrix is continuous across the boundary. `ManeuverEventDetector` returns `Continue`, with itself as next
detector, when the precondition of its maneuver fails: the maneuver waits for its next crossing. `RK78Integrator`
locates the event on the 100 ns grid of the epochs, possibly just before the root, so the next segment starts the
detector from its g-value past that root (the rule Hipparchus applies to a zero at the start of a step);
`VVIntegrator` locates it at the end of a step, already past the root.

## CrossingDirection

| Value | Fires When |
|-------|------------|
| `NegativeToPositive` | g-function crosses zero from negative to positive |
| `PositiveToNegative` | g-function crosses zero from positive to negative |
| `Any` | g-function crosses zero in either direction |

## ManeuverEventDetector

`ManeuverEventDetector` wraps an `ImpulseManeuver` and delegates to `ComputeEventValue()` for the g-function and `EventCrossingDirection` for the trigger direction. When the event fires, it applies the maneuver's delta-V and advances the maneuver chain. When the precondition of the maneuver (`CheckPreconditions`) fails, it returns `Continue` and the maneuver waits for its next crossing; if no crossing satisfies it within the window, the maneuver is not executed.

The minimum epoch of the maneuver is the gate of the detector (#569): no crossing counts before it, and the first crossing is searched from the integrated state at it. `VVIntegrator` and `RK78Integrator` evaluate the g-function at the gate, in the step that holds it (RK7(8) by a shortened step from the start of the step, Velocity-Verlet by the cubic Hermite of the step), and compare that value with the g-value at the step end; a g-value exactly zero at the gate is a crossing there when the sign that follows it is the target sign. Every step that does not hold a gate is unchanged. `Evaluate` returns +1 before the gate, for callers outside the library; the library's integrators do not read that value. See [Impulse Maneuvers](impulse-maneuvers.md#minimum-epoch).

## BisectionEventFinder

`BisectionEventFinder` performs sub-step root-finding to locate event times to approximately 1e-10 second precision, by bisection on the zero crossing of the g-function within a step. Its public `FindRoot` evaluates the g-function on the cubic Hermite interpolation of an `AcceptedStep`.

`VVIntegrator` detects events at step boundaries only. `RK78Integrator` refines them within the step by the same bisection, on states computed by shortened RK7(8) steps from the start of the step, so that the event is located on the integrated trajectory. The event time is then moved to the 100 ns grid of the epochs, and the propagation restarts from the state of a shortened step to that time: at the accuracy of the integrator, and the same state from which the state transition matrix at the event comes.

In the step that holds the gate of a detector, `RK78Integrator` bisects [gate, step end] on the g-function, from its value at the gate, and never labels an event before the gate. After an event that continues the propagation (`EventAction.Continue`), the detector of that event is not gated again: the next segment starts from its g-value past the root (#355), and the event decided its gate.

## Maneuver G-Functions

| Maneuver | G-Function | Direction | Fires At |
|----------|-----------|-----------|----------|
| `ApogeeHeightManeuver` | r . v | `NegativeToPositive` | Perigee |
| `PerigeeHeightManeuver` | r . v | `PositiveToNegative` | Apogee |
| `PhasingManeuver` | r . v | `NegativeToPositive` | Perigee |
| `CombinedManeuver` | r . v | `PositiveToNegative` | Apogee (+ precondition) |
| `PlaneAlignmentManeuver` | r . h_target | Adaptive | Nearest orbital node |
| `ApsidalAlignmentManeuver` | r . (h x p) | `NegativeToPositive` | Nearest intersection |

## Events And The State Transition Matrix

When the propagation integrates the state transition matrix ([Covariance Propagation](covariance-propagation.md)), Φ, Ψ
and the covariance at an event come from the same shortened RK7(8) step as the state there. The maneuvers are open loop:
the ΔV is the nominal one and the firing time is not differentiated with respect to the state, so Φ and Ψ are continuous
at the burn; the execution error of the maneuver, if any, is added to the velocity covariance after it.

## See Also

- [Impulse Maneuvers](impulse-maneuvers.md)
- [Integrators](integrators.md)
- [Propagators](propagators.md)
