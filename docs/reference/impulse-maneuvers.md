# Impulse Maneuvers

Impulse maneuvers model instantaneous delta-V burns triggered by g-function zero-crossings during propagation. Each maneuver defines its own event function and firing condition.

## ImpulseManeuver (Abstract)

| Member | Description |
|--------|-------------|
| `ComputeEventValue(StateVector)` | Scalar g-function evaluated at the current state |
| `EventCrossingDirection` | `CrossingDirection` that triggers the burn |
| `CheckPreconditions(StateVector)` | Optional additional constraints (returns `true` to allow firing). When it returns `false`, the occurrence is skipped and the maneuver waits for its next crossing; if none satisfies it within the window, the maneuver is not executed (`ThrustWindow` stays null) |
| `DeltaV` | Burn delta-V vector, in the inertial frame of the propagated state |
| `ThrustWindow` | Window of the equivalent finite burn (see below) |
| `ManeuverWindow` | Thrust window extended by the maneuver hold duration |
| `ExecutionError` | Optional execution error by the model of Gates (`ManeuverExecutionError`), set with an object initializer; added to the velocity covariance at the burn when the propagation propagates the covariance, ignored otherwise. See [Covariance Propagation](covariance-propagation.md#execution-error-of-an-impulsive-maneuver) |

`ManeuverExecutionError` takes its four standard deviations by name, so that two of different units cannot be swapped:
`new ManeuverExecutionError { ResolutionSigma = 0.01, PointingSigma = 0.001 }`. `ResolutionSigma` (σ_r), fixed magnitude
error, in m/s; `ShutoffSigma` (σ_s), magnitude error proportional to |ΔV|, dimensionless; `AutopilotSigma` (σ_a), fixed
pointing error, in m/s on each axis perpendicular to ΔV; `PointingSigma` (σ_p), pointing error proportional to |ΔV|, in
rad on each axis. Each finite and non-negative, zero when left out. A ΔV of
exactly zero adds no error; any non-zero ΔV takes the whole model, the fixed terms included. The trajectory follows the
commanded ΔV (open loop).

## Thrust Window Convention

The burn is impulsive: the propagated state receives the whole delta-V at the event epoch `t`.
The library also reports the finite burn the impulse stands for. Its duration `dt` follows from
the rocket equation (engine ISP, fuel flow and total mass), and the window is placed on
`[t - 0.666 dt, t + 0.334 dt]`: two thirds of the burn before the impulse, one third after.

The end of `ManeuverWindow` is the earliest epoch at which the next maneuver of the chain may fire.

The two-thirds split dates back to the initial .NET implementation and no derivation of it is on
record. The usual impulsive approximation of a finite burn centres the burn on the impulse (50 %).
The value is kept unchanged for now; it will be revisited together with finite-thrust modelling.

## Minimum Epoch

`MinimumEpoch` is the earliest epoch at which the maneuver may fire (#569). The maneuver fires at the first crossing of
its g-function in its direction at or after the minimum epoch, searched from the integrated state there, as GMAT's
`Propagate` to the epoch followed by a `Propagate` to the apsis does:

- `VVIntegrator` and `RK78Integrator` evaluate the g-function at the minimum epoch, in the integration step that holds
  it: RK7(8) on the state of a shortened step from the start of that step (13 force evaluations), Velocity-Verlet on the
  cubic Hermite of the step (none). RK7(8) then locates the crossing between the minimum epoch and the end of the step,
  and never labels it before the minimum epoch; Velocity-Verlet fires at the end of the step that holds it, as for any
  event.
- A crossing before the minimum epoch never counts. A user `ImpulseManeuver` whose g-function has no later crossing (a
  time-like g, `t − t*`) therefore never fires when its root precedes its minimum epoch.
- A g-function exactly zero at the minimum epoch is a crossing there when the sign that follows it is the target sign,
  a minimum epoch on a step end or equal to the start of the propagation included (an apsis given by Keplerian elements
  at the start, where r·v is 0.0): RK7(8) fires at the minimum epoch; Velocity-Verlet at the first step end whose sign is
  known after it, the next one when the minimum epoch is a step end.
- `PlaneAlignmentManeuver` takes the node it moves toward from the state at a minimum epoch after the start of the
  segment, never a node just passed: within 1e-6 |r| of a node it moves away from, it waits for the next node. With the
  minimum epoch at or before the start of the segment, the first evaluation there still locks the node by the sign of
  its rate, so a start less than 1e-6 |r| past a node takes that node, a period later (#581).
- A chained impulse is gated by the end of the previous maneuver's window, the same way, in whichever step holds it.
- With Velocity-Verlet, the decision follows its own trajectory, whose crossings lag the Keplerian epoch: with 10 s
  steps, up to 0.096 s at a node, 0.18 s at an apsis of the orbit after a Velocity-Verlet burn and 1.5 s at the
  intersection of an apsidal alignment (Linux x64 worsts 0.0953 s, 0.176 s and 1.44 s; to be confirmed on Windows and
  macOS arm64 by the CI). A minimum epoch within that lag of a crossing is decided on that trajectory.
- The public `ManeuverEventDetector.Evaluate` still returns +1 before the minimum epoch. A custom `Integrator` that compares
  that value with the first g-value after the minimum epoch keeps the behaviour before 11.0.0: a false crossing at the
  minimum epoch for a positive-to-negative maneuver whose g is already negative there, and a missed negative-to-positive
  crossing in the step that holds it.

Before 11.0.0 the integrators compared that +1 with the first step end after the minimum epoch: a `PerigeeHeightManeuver`
gated where r·v < 0 fired at the minimum epoch, and an `ApogeeHeightManeuver` gated in the step of its perigee, before
it, fired one orbit later (#569).

## Concrete Maneuvers

### ApogeeHeightManeuver

Raises apogee height. Fires at perigee (r . v crosses zero from negative to positive).

### PerigeeHeightManeuver

Raises perigee height. Fires at apogee (r . v crosses zero from positive to negative).

### PhasingManeuver

Adjusts orbital period for phasing. Fires at perigee (r . v, negative to positive).

### CombinedManeuver

Combined plane and apsis change. Fires at apogee (r . v, positive to negative) with an additional precondition that the apogee is aligned with the ascending node.

### PlaneAlignmentManeuver

Changes orbital plane to match a target. Uses an adaptive crossing direction on the g-function r . h_target to fire at the nearest orbital node (ascending or descending).

### ApsidalAlignmentManeuver

Rotates the line of apsides. Fires at the nearest orbit intersection point using the g-function r . (h x p) with negative-to-positive crossing.

## Maneuver Chain

Maneuvers are linked in a chain on the spacecraft:

1. **Leading attitudes** execute immediately at the initial state.
2. The first `ImpulseManeuver` is wrapped in a `ManeuverEventDetector`.
3. When the event fires, the burn is applied and the chain advances to the next impulse maneuver.
4. The process repeats until all maneuvers in the chain have executed.

## Example

```csharp
// Raise the apogee radius to 42,164 km (meters), at the first perigee after J2000 TDB.
var maneuver = new ApogeeHeightManeuver(
    earth,
    Time.J2000TDB,
    TimeSpan.Zero,
    42164000.0,
    engine);

spacecraft.SetStandbyManeuver(maneuver);
var solution = spacecraft.Propagate(window, perturbingBodies, false, false, deltaT);
```

## See Also

- [Event Detection](event-detection.md)
- [Attitudes](attitudes.md)
- [Propagators](propagators.md)
