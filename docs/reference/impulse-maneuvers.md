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
`ManeuverWindow` runs from the start of the thrust window to the later of the end of the hold and the end of the
thrust window: `[t - 0.666 dt, t - 0.666 dt + max(hold, dt)]`.

The end of `ManeuverWindow` is the earliest epoch at which the next maneuver of the chain may fire, an attitude
included: an attitude chained after an impulse activates there (#539).

In a propagation about the Earth both windows, and the holds of the attitudes, count TT seconds from the time argument
of the event (#547), as the integration does (#501); about any other body, SSB mode included, TDB seconds, bit for bit
as before 11.0.0. `ImpulseManeuver.TryExecute` called outside a propagation sets them in TDB seconds from the epoch of
its state. It returns the state after the impulse as a new state, its velocity plus ΔV, and leaves the state it is
given unchanged (before 11.0.0 it added ΔV to that state's velocity, #588). The attitude of the impulse, its front along ΔV, is in force over `[t - 0.666 dt, end of ManeuverWindow]`,
its start no earlier than the start of the propagation or the previous activation of the chain
([Attitudes](attitudes.md#the-attitude-timeline)).

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
- A chained impulse is gated by the end of the previous maneuver's window, the same way, in whichever step holds it:
  an impulse, or an attitude (#539). The last leading attitude gates the first impulse by the end of its hold, and an
  attitude chained after an impulse delays the next impulse by its hold from the end of the impulse's window. About the
  Earth the end of an impulse's window is counted in TT seconds and replaces, as the gate of the next maneuver, the TDB
  end `TryExecute` raised it to (#547).
- That gate belongs to the propagation that set it (#573). `MinimumEpoch` reports the later of the minimum epoch the
  maneuver was given (its constructor, or `Spacecraft.SetStandbyManeuver(maneuver, minimumEpoch)`, which lasts) and that
  gate, which holds after the propagation with the windows of the maneuver; the next `Spacecraft.Propagate` (and
  `Scenario.SimulateAsync` and `BatchPropagator` through it) clears it before it runs. A spacecraft propagated again,
  from another state or over another window, so fires its maneuvers as a spacecraft never propagated would.
- With Velocity-Verlet, the decision follows its own trajectory, whose crossings lag the Keplerian epoch: with 10 s
  steps, up to 0.096 s at a node, 0.18 s at an apsis of the orbit after a Velocity-Verlet burn and 1.5 s at the
  intersection of an apsidal alignment (worsts 0.0953 s, 0.176 s and 1.44 s on Linux x64, Windows and macOS arm64). A
  minimum epoch within that lag of a crossing is decided on that trajectory.
- The public `ManeuverEventDetector.Evaluate` still returns +1 before the minimum epoch. A custom `Integrator` that compares
  that value with the first g-value after the minimum epoch keeps the behaviour before 11.0.0: a false crossing at the
  minimum epoch for a positive-to-negative maneuver whose g is already negative there, and a missed negative-to-positive
  crossing in the step that holds it.

Before 11.0.0 the integrators compared that +1 with the first step end after the minimum epoch: a `PerigeeHeightManeuver`
gated where r·v < 0 fired at the minimum epoch, and an `ApogeeHeightManeuver` gated in the step of its perigee, before
it, fired one orbit later (#569).

## Concrete Maneuvers

### ApogeeHeightManeuver

Moves the apogee, up or down, to `TargetApogee`, a radius in metres from the centre of the maneuver centre (not an
altitude: R + h for an altitude h). Fires at perigee (r . v crosses zero from negative to positive).

### PerigeeHeightManeuver

Moves the perigee, up or down, to `TargetPerigeeHeight`, a radius in metres from the centre of the maneuver centre (not an
altitude). Fires at apogee (r . v crosses zero from positive to negative).

### PhasingManeuver

Adjusts orbital period for phasing. Fires at perigee (r . v, negative to positive).

The burn, along the velocity, puts the spacecraft on a phasing orbit after `RevolutionNumber` (N) revolutions of which
it is back at the perigee together with its target. The target is Δθ ahead of the spacecraft at the burn, the
difference of their true longitudes (Ω + ω + ν) at its epoch, beyond π a target behind. With t(Δθ) the time from the
perigee to Δθ along the current orbit by Kepler's equation (negative for a target behind), the phasing orbit has the
period T₂ = T₁ − t(Δθ) / N and the semi-major axis a₂ = (√μ T₂ / 2π)^(2/3), and ΔV = h₂ / r_p − h_target / r_p at the
perigee. The hold is set to 0.9 N T₂ at the burn. Two constructors set the target:

| Constructor | Target | λ_target at the burn | h_target |
|-------------|--------|----------------------|----------|
| `PhasingManeuver(minimumEpoch, hold, targetOrbit, revolutionNumber, engine)` | the object on `targetOrbit`, about the maneuver center (its observer) in the frame of the spacecraft | `targetOrbit.TrueLongitude(burn epoch)` | the target orbit's |
| `PhasingManeuver(maneuverCenter, minimumEpoch, hold, trueLongitude, revolutionNumber, engine)` | a virtual target on the spacecraft's current orbit (#588) | `trueLongitude`, in radians, about `maneuverCenter` in the frame of the spacecraft's state | the current orbit's |

With a true longitude, Δθ = λ − λ_sc(burn), and since the burn is at the perigee λ_sc(burn) is the longitude of the
perigee Ω + ω: λ − (Ω + ω) is the phase of the target ahead of the spacecraft along its orbit, taken modulo 2π. For
instance, with a perigee longitude of 80°, 110° asks to catch up a target 30° ahead, 50° to let a target 30° behind
catch up. `TargetTrueLongitude` holds the value given, NaN for the target-orbit constructor. Before 11.0.0 the
true-longitude constructor threw `NullReferenceException` at its burn (#588).

### CombinedManeuver

Combined plane and apsis change. Fires at apogee (r . v, positive to negative) with an additional precondition that the apogee is aligned with the ascending node.

### PlaneAlignmentManeuver

Changes orbital plane to match a target. Uses an adaptive crossing direction on the g-function r . h_target to fire at the nearest orbital node (ascending or descending).

### ApsidalAlignmentManeuver

Rotates the line of apsides. Fires at the nearest orbit intersection point using the g-function r . (h x p) with negative-to-positive crossing.

## Maneuver Chain

Maneuvers are linked in a chain on the spacecraft:

1. **Leading attitudes** are laid on the attitude timeline: each activates at the later of its `MinimumEpoch` and the end
   of the previous one's hold, from the start of the propagation; the last one's hold gates the first impulse.
2. The first `ImpulseManeuver` is wrapped in a `ManeuverEventDetector`.
3. When the event fires, the detector executes the burn, and only the burn (`HandleEvent` returns no next detector).
   The propagation then lays the attitudes chained after it on the timeline, each from the end of the previous window,
   and builds the detector of the next impulse.
4. The process repeats until all maneuvers in the chain have executed. An attitude whose activation falls after the end
   of the propagation is not executed, and the chain stops there.

Every executed maneuver, attitudes included, is in `Spacecraft.ExecutedManeuvers`, once, in the order of the chain
(#570).

A chain is a list: a maneuver appears in it once, and a repeated burn is a new maneuver object (#585). Each maneuver
keeps one record (its windows, `DeltaV`, `FuelBurned`), so a maneuver met twice would lose the first. A cycle is
rejected where it is made: `SetNextManeuver` throws `ArgumentException` when the chain from the next maneuver comes back
to the maneuver itself or already holds a cycle, and leaves the chain as it was. A cycle built past it, by a subclass
that sets the protected `NextManeuver`, is rejected by `Spacecraft.SetStandbyManeuver` (`ArgumentException`), by the
construction of the propagator (`Spacecraft.Propagate` and so the scenario, the batch and the Monte Carlo propagators, the builder's
`Build`, a `CentralBodyPropagator` built directly: `InvalidOperationException`, before the states of an earlier
propagation are cleared) and by `Propagate` (`InvalidOperationException`). Before 11.0.0 a cycle ended the process with
a stack overflow at the first `Spacecraft.Propagate`, or never returned through the builder.

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
