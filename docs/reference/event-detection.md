# Event Detection

The event detection system uses industry-standard g-function zero-crossing to locate the maneuver trigger points during propagation. The propagation builds its detectors from the maneuver chain only (an internal maneuver detector, `ManeuverEventDetector`, public before 11.0.0) and takes no user detector: the epochs of other events of a propagated spacecraft (apsides, nodes, altitude crossings, passes over a site) are found after the propagation by the window searches, `FindWindowsOnDistanceConstraint` (`LocalMin` and `LocalMax` for the apsides) and `FindWindowsOnCoordinateConstraint` (the edges of its windows), located to 1 µs ([Sites](sites.md#window-searches)).

## IEventDetector Interface

| Member | Description |
|--------|-------------|
| `Evaluate(StateVector)` | Compute the scalar g-function value at the given state |
| `Direction` | `CrossingDirection` specifying which zero-crossing triggers the event |
| `IsActive` | Whether this detector is currently active |
| `HandleEvent(StateVector, Spacecraft)` | Execute the event action and return an `EventResult`. The maneuver detector executes its impulse only and returns no next detector: the propagation lays the rest of the chain on its attitude timeline and builds the detector of the next impulse (#539) |

When `Evaluate` changes sign in the specified `Direction` between two integration steps, the event is triggered. A
g-value exactly zero at the end of a step, after a non-zero one, is a crossing there, its direction read from the sign
before it (#580: before 11.0.0 it was none, and a time-like g whose root falls on a Velocity-Verlet or fixed-step
RK7(8) step end never fired). A zero at the base of a segment followed by a non-zero value is not a crossing of a
detector without a gate (Hipparchus's convention: the zero was the event that started the segment); for a gated
detector whose gate is open at the base, at it or before it, a zero there is a crossing at the base when the first step
end has the target sign (GMAT's convention, #577's choice 6; decided by S. Guillet, 2026-11-17 for #580, the split of the two conventions
decided by S. Guillet, 2026-11-18), so a minimum
epoch at the start and one before it give the same burn.

`IIntegrator.IntegrateSegment` reports one event per call: the earliest crossing of the step that holds crossings, ties
in list order (#578; before 11.0.0, the first detector in list order whose sign changed in the step). `RK78Integrator`
locates the root of each detector that crossed and keeps the earliest; `VVIntegrator` estimates each root on the cubic
Hermite of the step, keeps the earliest and reports it at the end of the step. With Velocity-Verlet another crossing of
the same step falls before that step end: a caller that restarts from the event starts that detector from its g-value
before the step, or loses that crossing (decided by S. Guillet, 2026-11-17). The propagators pass one detector.

## EventResult

`EventResult` is a read-only record struct returned by `HandleEvent` (11.0.0: its properties had public setters before; build another with `with`) containing:

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
transition matrix is continuous across the boundary. The maneuver detector returns `Continue`, with itself as next
detector, when the precondition of its maneuver fails: the maneuver waits for its next crossing. `RK78Integrator`
locates the event on the 100 ns grid of the epochs, possibly just before the root, so the next segment starts the
detector from its g-value past that root (the rule Hipparchus applies to a zero at the start of a step);
`VVIntegrator` locates it at the end of a step, carried onto the tick of its label (#572), so the same rule applies.

## CrossingDirection

| Value | Fires When |
|-------|------------|
| `NegativeToPositive` | g-function crosses zero from negative to positive |
| `PositiveToNegative` | g-function crosses zero from positive to negative |
| `Any` | g-function crosses zero in either direction |

## The maneuver detector

The maneuver detector is internal (decision 88 of #668, [#802](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/802), decided by S. Guillet, 2026-11-23): the propagation builds it from the maneuver plan (`Spacecraft.SetManeuvers`) and takes no user detector, and its public `HandleEvent` executed a maneuver outside a run before 11.0.0 (fuel burned, `DeltaV` and the windows rewritten after the run). The propagation executes the plan; the previews of a maneuver are `ImpulseManeuver.ComputeDeltaV` and `ComputeEventValue`, pure ([Impulse Maneuvers](impulse-maneuvers.md)). A custom `IIntegrator` given to the propagator receives it as an `IEventDetector` within the run, and the detector serves that run only (decision 95 of #668, decided by S. Guillet, 2026-11-24): the run disarms every detector it built when it ends, completed, stopped by its token or thrown, and a detector kept past it throws `InvalidOperationException` from `Evaluate` and `HandleEvent`, naming its maneuver and changing nothing (before, it executed its maneuver outside the run: 472 kg of fuel burned and `DeltaV` rewritten in the probe of the second review of L5a). This holds for the calls that start after the end of the run: a call already in progress when the run ends completes (decision 98 of #668, decided by S. Guillet, 2026-11-24). Only an integrator that keeps a detector past its `IntegrateSegment` call, against the contract below, meets the case (12 of 300 runs of a probe whose thread called `HandleEvent` in a loop burned fuel after `Propagate` returned, in the third review of L5a); no lock is added to the detector. A detector also executes its impulse once at most for calls that do not overlap (decision 97 of #668, decided by S. Guillet, 2026-11-24): when its `HandleEvent` has executed the impulse (`StopAndRestart`), it disarms itself, and a later `HandleEvent` or `Evaluate` within the run throws `InvalidOperationException`, saying that it has already executed its impulse and changing nothing (once the run has ended, the message of a finished run is given first); one that skipped the occurrence (`Continue`, the preconditions not met) stays armed and waits for its next crossing. The detector disarms itself as the execution starts, so a `HandleEvent` whose execution throws disarms it too (before, an execution that threw after the fuel burned, a burn past the range of a `TimeSpan`, left it armed, and each later call burned again: three burns in a run of the fourth review of L5a). The propagation never calls a detector after its execution, replacing it with the detector of the next impulse, so a run is unchanged; an integrator that calls `HandleEvent` on the event it returns makes the propagation's own call throw, so the run throws rather than burning twice; one that executes an event and hides it (returns no event) breaks the contract, and the run returns incomplete, its solution stopped at that event (before, an integrator that kept the detector of an impulse executed it a second time during the run: 6.74 kg burned, `DeltaV` rewritten and nothing in the executed maneuvers, in the probe of the third review of L5a). An integrator uses the detectors it receives within its call of `IntegrateSegment` only.

It wraps an `ImpulseManeuver` and delegates to `ComputeEventValue()` for the g-function and `EventCrossingDirection` for the trigger direction. When the event fires, it executes the impulse; the propagation advances the chain ([#539](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/539)). When the precondition of the maneuver (`CheckPreconditions`) fails, it returns `Continue` and the maneuver waits for its next crossing; if no crossing satisfies it within the window, the maneuver is not executed.

The minimum epoch of the maneuver is the gate of the detector (#569): no crossing counts before it, and the first crossing is searched from the integrated state at it. At the base of a segment, a detector whose gate is open there is evaluated as at the start of a search (`ImpulseManeuver.ComputeEventValueAtSearchStart`, the rule of a gate inside a step), so a `PlaneAlignmentManeuver` whose propagation starts just past a node takes the node ahead (#581; before 11.0.0 it took the node just passed and fired one orbit after it); the detector of an event just handled keeps its g-value past that event's root. `VVIntegrator` and `RK78Integrator` evaluate the g-function at the gate, in the step that holds it (RK7(8) by a shortened step from the start of the step, Velocity-Verlet by the cubic Hermite of the step), and compare that value with the g-value at the step end; a g-value exactly zero at the gate is a crossing there when the sign that follows it is the target sign. Every step that does not hold a gate is unchanged. `Evaluate` returns +1 before the gate, for an integrator that does not know the gate (a custom `IIntegrator`); the library's integrators never compare that value with a g-value after the gate: in the step that holds the gate, they replace it with the g-value at the gate. See [Impulse Maneuvers](impulse-maneuvers.md#minimum-epoch).

## BisectionEventFinder

`BisectionEventFinder` performs sub-step root-finding to locate event times to approximately 1e-10 second precision, by bisection on the zero crossing of the g-function within a step. Its public `FindRoot` evaluates the g-function on the cubic Hermite interpolation of an `AcceptedStep`.

`VVIntegrator` detects events at step boundaries only. The step end is then carried onto the 100 ns grid of the epochs:
the step that holds the event is integrated again to the time of the label of its end, up to half a tick from it, so the
state the next segment starts from is the state of its label (#572; before 11.0.0 the next segment started at the
rounded label with the state of the step end, up to 5e-8 s apart, which moved the trajectory along the track by up to
4e-4 m per event). When the label of the step end rounds to the tick before it, the event is reported up to 5e-8 s
before a root in that last half tick, and the continued root is still skipped (accepted, decided by S. Guillet, 2026-11-18). A step end on the
tick grid keeps its step. `RK78Integrator` refines them within the step by the same bisection, on states computed by shortened RK7(8) steps from the start of the step, so that the event is located on the integrated trajectory. The event time is then moved to the 100 ns grid of the epochs, and the propagation restarts from the state of a shortened step to that time: at the accuracy of the integrator, and the same state from which the state transition matrix at the event comes.

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
