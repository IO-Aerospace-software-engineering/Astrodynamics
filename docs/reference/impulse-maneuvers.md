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

`ManeuverExecutionError(resolutionSigma, shutoffSigma, autopilotSigma, pointingSigma)`: σ_r, fixed magnitude error, in
m/s; σ_s, magnitude error proportional to |ΔV|, dimensionless; σ_a, fixed pointing error, in m/s on each axis
perpendicular to ΔV; σ_p, pointing error proportional to |ΔV|, in rad on each axis. All finite and non-negative. A ΔV of
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
