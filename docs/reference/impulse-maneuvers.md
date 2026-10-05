# Impulse Maneuvers

Impulse maneuvers model instantaneous delta-V burns triggered by g-function zero-crossings during propagation. Each maneuver defines its own event function and firing condition.

## ImpulseManeuver (Abstract)

| Member | Description |
|--------|-------------|
| `ComputeEventValue(StateVector)` | Scalar g-function evaluated at the current state |
| `EventCrossingDirection` | `CrossingDirection` that triggers the burn |
| `CheckPreconditions(StateVector)` | Optional additional constraints (returns `true` to allow firing) |
| `DeltaV` | Burn delta-V vector, in the inertial frame of the propagated state |
| `ThrustWindow` | Window of the equivalent finite burn (see below) |
| `ManeuverWindow` | Thrust window extended by the maneuver hold duration |

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
