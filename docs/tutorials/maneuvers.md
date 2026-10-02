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

Attitude maneuvers orient the spacecraft before an impulse fires. They execute
immediately when the maneuver chain reaches them --- no event detection is needed.

### Single-vector attitudes

Single-vector attitudes constrain one spacecraft body axis to a geometric direction.
The remaining degrees of freedom (roll around that axis) are uncontrolled.

```csharp
// Point the thrust axis along the velocity vector
var prograde = new ProgradeAttitude(earth, epoch, TimeSpan.FromMinutes(30), engine);

// Point the instrument axis toward the sub-satellite point
var nadir = new NadirAttitude(earth, epoch, TimeSpan.FromMinutes(30), engine);
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
// Primary: spacecraft -Z toward Earth (nadir pointing)
// Secondary: spacecraft +Z toward Sun (solar panels)
var triad = new TriadAttitude(epoch, TimeSpan.FromHours(1.5),
    Spacecraft.Down, earth,    // Primary: nadir pointing
    Spacecraft.Up, sun,        // Secondary: solar panels toward Sun
    engine, 0.0);
```

When using `CelestialAttitudeTarget`, the default aberration is `Aberration.None`
(geometric direction), consistent with the attitude pipeline and other industry tools.
Use `Aberration.LT` for photon-based pointing scenarios (e.g., optical sensors):

```csharp
// Default geometric direction
var sunTarget = new CelestialAttitudeTarget(sun);

// Light-time corrected direction for sensor pointing
var moonTarget = new CelestialAttitudeTarget(moon, Aberration.LT);

var triad = new TriadAttitude(earth, epoch, duration,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    Spacecraft.Up, moonTarget,
    engine, 0.0);
```

Factory methods create common attitude profiles in one call:

```csharp
// LVLH frame (nadir + velocity tracking)
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);

// Prograde thrust with solar panels tracking the Sun
var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(
    earth, epoch, duration, sun, engine);
```

## Impulse maneuvers

Each impulse maneuver computes the required delta-v and defines a g-function that
determines **when** the burn fires.

### Raising apogee height

`ApogeeHeightManeuver` fires at perigee to raise the apogee to a target altitude:

```csharp
var raiseApogee = new ApogeeHeightManeuver(
    earth, epoch, TimeSpan.Zero, targetApogeeRadius, engine);
```

### Lowering perigee height

`PerigeeHeightManeuver` fires at apogee to lower the perigee:

```csharp
var lowerPerigee = new PerigeeHeightManeuver(
    earth, epoch, TimeSpan.Zero, targetPerigeeRadius, engine);
```

### Phasing maneuver

`PhasingManeuver` adjusts the orbital period so the spacecraft arrives at a
particular relative position after a given number of revolutions:

```csharp
var phasing = new PhasingManeuver(
    epoch, TimeSpan.Zero, targetOrbit, revolutions, engine);
```

### Plane alignment

`PlaneAlignmentManeuver` rotates the orbital plane to match a target orbit.
It fires at the nearest orbital node (ascending or descending) using an
adaptive crossing direction:

```csharp
var planeChange = new PlaneAlignmentManeuver(
    epoch, TimeSpan.Zero, targetOrbit, engine);
```

### Apsidal alignment

`ApsidalAlignmentManeuver` rotates the line of apsides to match a target orbit.
It fires at the nearest orbit intersection point using the g-function r . (h x p):

```csharp
var apsidalAlign = new ApsidalAlignmentManeuver(
    epoch, TimeSpan.Zero, targetOrbit, engine);
```

### Combined maneuver

`CombinedManeuver` performs a combined plane change and perigee height adjustment
at apogee. It includes a precondition that the apogee is aligned with the ascending
node before firing:

```csharp
var combined = new CombinedManeuver(
    earth, epoch, TimeSpan.Zero, targetPerigeeRadius, targetInclination, engine);
```

## Maneuver chaining

Multiple maneuvers execute in sequence via `SetNextManeuver`. Leading attitudes
execute immediately; the first impulse maneuver arms an event detector.
After that impulse fires, the chain advances to the next maneuver.

```csharp
var maneuver1 = new PlaneAlignmentManeuver(
    new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
    targetOrbit, engine);

maneuver1
    .SetNextManeuver(new ApsidalAlignmentManeuver(...))
    .SetNextManeuver(new PhasingManeuver(...))
    .SetNextManeuver(new ApogeeHeightManeuver(...));

spacecraft.SetStandbyManeuver(maneuver1);
```

`SetNextManeuver` returns the newly added maneuver, so calls chain naturally.
The spacecraft begins propagation with the first maneuver armed.

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
- Chain maneuvers with `SetNextManeuver` to build multi-burn sequences.
- Use `TriadAttitude` when you need fully-constrained pointing (no roll ambiguity).
