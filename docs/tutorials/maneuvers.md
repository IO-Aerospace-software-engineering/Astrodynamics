# Maneuvers

This tutorial covers the maneuver system: attitude pointing, impulse maneuvers, and
chaining multiple maneuvers into a complete mission sequence.

## Maneuver system overview

IO.Astrodynamics uses an **event-driven** maneuver system based on g-functions --- the
industry-standard zero-crossing approach. Each impulse maneuver defines a continuous
scalar function (the g-function) whose sign change triggers execution. This eliminates
geometric proximity heuristics and works naturally with adaptive integrators.

The propagation loop watches for g-function zero crossings during integration.
When a crossing is detected the integrator refines the event time and applies the
maneuver's delta-v at that precise epoch.

## Attitude maneuvers

Attitude maneuvers orient the spacecraft before an impulse fires. No event detection is needed: each one activates at
its minimum epoch or at the end of the previous maneuver's window, whichever is later, and stays in force until the
next activation of the chain. Its hold delays the next maneuver: a leading attitude held 30 minutes moves the earliest
epoch of the first impulse 30 minutes after it. See [The attitude timeline](../reference/attitudes.md#the-attitude-timeline).

### Single-vector attitudes

Single-vector attitudes constrain one spacecraft body axis to a geometric direction.
The remaining degrees of freedom (roll around that axis) are uncontrolled.

```csharp
// Inputs: earth (CelestialBody), epoch (Time), engine (Engine)
// Point the thrust axis along the velocity vector; the next maneuver of the chain starts 30 minutes later at the earliest
var prograde = new ProgradeAttitude(epoch, TimeSpan.FromMinutes(30), earth);

// Point the instrument axis toward the sub-satellite point, also held 30 minutes
var nadir = new NadirAttitude(epoch, TimeSpan.FromMinutes(30), earth);
```

Available single-vector attitudes:

- `ProgradeAttitude` — velocity direction (+v)
- `RetrogradeAttitude` — anti-velocity (-v)
- `NadirAttitude` — toward central body (-r)
- `ZenithAttitude` — away from central body (+r)
- `NormalAttitude` — orbital angular momentum (h = r x v)
- `AntiNormalAttitude` — anti-normal (-h)
- `InstrumentPointingToAttitude` — point an instrument boresight at a target

### TriadAttitude --- fully-constrained pointing

`TriadAttitude` uses the TRIAD algorithm to resolve all three axes by specifying a
primary and secondary body-axis/target pair. The primary constraint is satisfied
exactly; the secondary is satisfied in the plane perpendicular to the primary.
This eliminates roll ambiguity.

```csharp
// Inputs: epoch (Time), earth and sun (CelestialBody), engine (Engine)
// Primary: spacecraft -Z toward Earth (nadir pointing)
// Secondary: spacecraft +X toward Sun, which sets the roll about nadir (+Z, opposite to -Z, would leave the roll
// undetermined: the body vectors must be at least 5 degrees apart)
var triad = new TriadAttitude(
    Spacecraft.Down, new CelestialAttitudeTarget(earth),  // Primary: nadir pointing
    Spacecraft.Right, new CelestialAttitudeTarget(sun),
    epoch, TimeSpan.FromHours(1.5));
```

When using `CelestialAttitudeTarget`, the default aberration is `Aberration.None`
(geometric direction), consistent with the attitude pipeline and other industry tools.
Use `Aberration.LT` for photon-based pointing scenarios (e.g., optical sensors); the corrected direction is the
target as the spacecraft sees it, as CSPICE's `spkcvo_c` corrects it from the spacecraft's state (#740):

```csharp
// Inputs: engine (Engine), sun, moon and earth (CelestialBody), epoch (Time), duration (TimeSpan)
// Default geometric direction
var sunTarget = new CelestialAttitudeTarget(sun);

// Light-time corrected direction for sensor pointing
var moonTarget = new CelestialAttitudeTarget(moon, Aberration.LT);

var triad = new TriadAttitude(Spacecraft.Front,
    OrbitalDirectionTarget.Prograde,
    Spacecraft.Up,
    moonTarget,
    epoch,
    duration,
    earth);
```

Factory methods create common attitude profiles in one call. They point the axes of the spacecraft that carries the
engine (`BodyDown` and `BodyFront` for LVLH, `BodyFront` and `BodyUp` for Sun tracking), so a spacecraft built with
its own `bodyFront`, `bodyRight` and `bodyUp` gets its own axes pointed:

```csharp
// Inputs: earth and sun (CelestialBody), epoch (Time), duration (TimeSpan), engine (Engine)
// LVLH frame (the spacecraft's down axis to nadir, its front axis toward the velocity)
var lvlh = TriadAttitude.CreateLvlh(epoch, duration, earth);

// Prograde thrust with solar panels tracking the Sun
var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(
    sun, epoch, duration, earth);
```

## Impulse maneuvers

Each impulse maneuver computes the required delta-v and defines a g-function that
determines **when** the burn fires.

### Moving the apogee

`ApogeeHeightManeuver` fires at perigee and moves the apogee, up or down, to a target **radius**: a distance in metres
from the centre of `ManeuverCenter`, not an altitude. For an altitude h above the Earth, pass the equatorial radius
plus h:

```csharp
// Inputs: earth (CelestialBody), epoch (Time), engine (Engine)
// An apogee 1 000 km above the equator: R + 1 000 km from the Earth's centre
double targetApogeeRadius = earth.EquatorialRadius + 1000e3;
var raiseApogee = new ApogeeHeightManeuver(
    targetApogeeRadius, engine, epoch, TimeSpan.Zero, earth);
```

### Moving the perigee

`PerigeeHeightManeuver` fires at apogee and moves the perigee, up or down, to a target radius from the centre, in
metres:

```csharp
// Inputs: earth (CelestialBody), epoch (Time), targetPerigeeRadius (double), engine (Engine)
var lowerPerigee = new PerigeeHeightManeuver(
    targetPerigeeRadius, engine, epoch, TimeSpan.Zero, earth);
```

### Phasing maneuver

`PhasingManeuver` adjusts the orbital period so the spacecraft arrives at a
particular relative position after a given number of revolutions:

```csharp
// Inputs: epoch (Time), targetOrbit (OrbitalParameters), revolutions (uint), engine (Engine)
var phasing = new PhasingManeuver(
    targetOrbit, revolutions, engine, epoch);
```

### Plane alignment

`PlaneAlignmentManeuver` rotates the orbital plane to match a target orbit.
It fires at the nearest orbital node (ascending or descending) using an
adaptive crossing direction:

```csharp
// Inputs: epoch (Time), targetOrbit (OrbitalParameters), engine (Engine)
var planeChange = new PlaneAlignmentManeuver(
    targetOrbit, engine, epoch, TimeSpan.Zero);
```

### Apsidal alignment

`ApsidalAlignmentManeuver` rotates the line of apsides to match a target orbit.
It fires at the nearest orbit intersection point using the g-function r . (h x p):

```csharp
// Inputs: epoch (Time), targetOrbit (OrbitalParameters), engine (Engine)
var apsidalAlign = new ApsidalAlignmentManeuver(
    targetOrbit, engine, epoch, TimeSpan.Zero);
```

### Combined maneuver

`CombinedManeuver` performs a combined plane change and perigee change at apogee, the perigee
moved to a target radius from the centre, in metres. It includes a precondition that the apogee is aligned with the ascending
node before firing:

```csharp
// Inputs: earth (CelestialBody), epoch (Time), targetPerigeeRadius and targetInclination (double), engine (Engine)
var combined = new CombinedManeuver(
    targetPerigeeRadius, targetInclination, engine, epoch, TimeSpan.Zero, earth);
```

## Maneuver chaining

Multiple maneuvers execute in sequence: `Spacecraft.SetManeuvers` sets the plan, in order. Leading attitudes
activate one after the other from the start, each held for its hold; the first impulse
maneuver arms an event detector gated by the end of the last hold. After that impulse
fires, the attitudes chained after it activate at the end of its maneuver window, and the
next impulse is gated by the end of their holds.

```csharp
// Inputs: targetOrbit (OrbitalParameters), engine (Engine), revolutions (uint), earth (CelestialBody),
//   targetApogeeRadius (double), spacecraft (Spacecraft)
var maneuver1 = new PlaneAlignmentManeuver(
    targetOrbit,
    engine,
    new Time(DateTime.MinValue, TimeScale.TDB),
    TimeSpan.Zero);

// The plan, in order; each maneuver takes its own arguments, as in the sections above
spacecraft.SetManeuvers(maneuver1,
    new ApsidalAlignmentManeuver(
        targetOrbit, engine, new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero),
    new PhasingManeuver(
        targetOrbit, revolutions, engine, new Time(DateTime.MinValue, TimeScale.TDB)),
    new ApogeeHeightManeuver(
        targetApogeeRadius, engine, new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, earth));
```

`SetManeuvers` links each maneuver to the next and is the one way to link a plan; every propagation runs the plan
from its first maneuver. A maneuver is in one plan at a time, so add the tank and the engine before the plan.
`ComputeDeltaV` previews the delta-V of an impulse at a state, and `ComputeStateOrientation` the attitude of an
attitude law, without executing them.

## g-function reference

The table below lists each maneuver's g-function, the crossing direction that
triggers execution, and the orbital location where it fires.

| Maneuver | g-function | Crossing Direction | Fires at |
|----------|-----------|-------------------|----------|
| `ApogeeHeightManeuver` | r . v | Negative to Positive | Perigee |
| `PerigeeHeightManeuver` | r . v | Positive to Negative | Apogee |
| `PhasingManeuver` | r . v | Negative to Positive | Perigee |
| `CombinedManeuver` | r . v | Positive to Negative | Apogee (+ precondition) |
| `PlaneAlignmentManeuver` | r . h_target | Adaptive | Nearest orbital node |
| `ApsidalAlignmentManeuver` | r . (h x p) | Negative to Positive | Nearest orbit intersection |

**Reading the table:**

- **r . v** is the dot product of position and velocity. It equals zero at apses.
- **h_target** is the unit angular-momentum vector of the target orbit.
- **h x p** is the cross product of the angular-momentum and eccentricity vectors.
- **Adaptive** means the detector picks ascending or descending node based on
  which is closer.

!!! tip "Event timing precision"
    The RK78 integrator uses the `BisectionEventFinder` to refine g-function
    zero crossings to approximately 1e-10 seconds, giving sub-step precision
    for maneuver timing. `VVIntegrator` detects events only at step boundaries.

## Summary

- Attitudes orient the spacecraft; impulse maneuvers change the orbit.
- Every impulse maneuver defines a scalar g-function whose sign change triggers
  the burn.
- Set the plan with `SetManeuvers` to build multi-burn sequences.
- Use `TriadAttitude` when you need fully-constrained pointing (no roll ambiguity).
