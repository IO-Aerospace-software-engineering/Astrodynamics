# Attitudes

Attitudes define spacecraft orientation during propagation. They are processed immediately in the maneuver chain (not via event detection) and recomputed at each output epoch.

## Attitude Base Class

All attitude maneuvers inherit from the abstract `Attitude` class.

| Member | Type | Description |
|--------|------|-------------|
| `ComputeOrientation(StateVector)` | Method | Compute the orientation quaternion at the given state (abstract, public) |
| `StateOrientation` | Property | The computed spacecraft orientation after execution |
| `ManeuverCenter` | Property | The central body for the maneuver frame |
| `MinimumEpoch` | Property | Earliest epoch at which the attitude can activate |
| `ManeuverHoldDuration` | Property | How long the attitude is held |
| `Engine` | Property | The engine associated with this maneuver |

**Constructor** (used by all concrete attitude types):

```csharp
Attitude(CelestialItem maneuverCenter, Time minimumEpoch,
         TimeSpan maneuverHoldDuration, Engine engine)
```

## Single-Vector Attitudes

These attitudes constrain one spacecraft body axis to a geometric direction, leaving roll unconstrained.

| Attitude | Direction | Description |
|----------|-----------|-------------|
| `ProgradeAttitude` | +v | Body front aligns with the velocity vector |
| `RetrogradeAttitude` | -v | Body front aligns opposite to the velocity vector |
| `NadirAttitude` | -r | Body front points toward the central body center |
| `ZenithAttitude` | +r | Body front points away from the central body center |
| `NormalAttitude` | h = r x v | Body front aligns with the orbital angular momentum vector |
| `AntiNormalAttitude` | -h | Body front aligns opposite to the orbital angular momentum vector |

All six types share the same constructor signature:

```csharp
new ProgradeAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new RetrogradeAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new NadirAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new ZenithAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new NormalAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new AntiNormalAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
```

## InstrumentPointingToAttitude

Points an instrument boresight at a celestial target. This is a single-vector attitude (roll unconstrained).

| Property | Description |
|----------|-------------|
| `Instrument` | The instrument whose boresight defines the pointing axis |
| `Target` | The `ILocalizable` target to track |

```csharp
var pointing = new InstrumentPointingToAttitude(
    minimumEpoch, holdDuration, camera, moon, engine);
```

## TriadAttitude

`TriadAttitude` provides full 3-DOF orientation using the TRIAD algorithm with a primary and secondary direction constraint. The primary constraint is satisfied exactly; the secondary is satisfied as closely as possible in the remaining plane. This eliminates roll ambiguity.

| Property | Description |
|----------|-------------|
| `PrimaryTarget` | Primary `ILocalizable` target (null when using `IAttitudeTarget`) |
| `SecondaryTarget` | Secondary `ILocalizable` target (null when using `IAttitudeTarget`) |
| `PrimaryAttitudeTarget` | Primary `IAttitudeTarget` (null when using `ILocalizable`) |
| `SecondaryAttitudeTarget` | Secondary `IAttitudeTarget` (null when using `ILocalizable`) |
| `PrimaryBodyVector` | Spacecraft body-frame vector for primary constraint |
| `SecondaryBodyVector` | Spacecraft body-frame vector for secondary constraint |
| `MinimumVectorSeparation` | Minimum angle between body vectors (default 5 degrees) |

!!! warning
    The primary and secondary body vectors (and reference vectors) must be at least 5 degrees apart; otherwise orientation computation is ill-conditioned.

### Constructors

**Single instrument** — uses instrument boresight as primary body vector and reference vector as secondary:

```csharp
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    camera, primaryTarget, secondaryTarget, engine);
```

**Dual instruments** — different instruments for primary and secondary pointing:

```csharp
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    primaryInstrument, primaryTarget,
    secondaryInstrument, secondaryTarget, engine);
```

**Explicit body vectors + ILocalizable targets** — direct spacecraft-frame vectors with celestial body targets:

```csharp
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    Spacecraft.Front, moon,       // Primary: Front points at Moon
    Spacecraft.Up, sun,           // Secondary: Up toward Sun
    engine);
```

**Explicit body vectors + IAttitudeTarget targets** — supports orbital directions and celestial bodies:

```csharp
var attitude = new TriadAttitude(
    earth, minimumEpoch, holdDuration,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    Spacecraft.Up, new CelestialAttitudeTarget(sun),
    engine);
```

### Factory Methods

| Method | Primary | Secondary | Description |
|--------|---------|-----------|-------------|
| `TriadAttitude.CreateLVLH(maneuverCenter, epoch, duration, engine)` | Down toward Nadir | Front toward Prograde | Local Vertical / Local Horizontal attitude |
| `TriadAttitude.CreateProgradeWithSunTracking(maneuverCenter, epoch, duration, sun, engine)` | Front toward Prograde | Up toward Sun | Prograde with solar-panel Sun tracking |

```csharp
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);

var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(
    earth, epoch, duration, sun, engine);
```

## Body Axes

Each spacecraft has configurable body axes that define the physical orientation of the vehicle relative to the attitude frame.

| Static Field | Direction | Default Vector |
|-------------|-----------|----------------|
| `Spacecraft.Front` | Forward | +Y |
| `Spacecraft.Back` | Aft | -Y |
| `Spacecraft.Right` | Starboard | +X |
| `Spacecraft.Left` | Port | -X |
| `Spacecraft.Up` | Zenith-facing | +Z |
| `Spacecraft.Down` | Nadir-facing | -Z |

Instance-level overrides can be set via the `Spacecraft` constructor parameters (`bodyFront`, `bodyRight`, `bodyUp`). Custom axes must be orthogonal and right-handed. All attitude maneuvers use `GetBodyFront()` which reads per-instance axes with fallback to the static default.

## IAttitudeTarget

`IAttitudeTarget` is an interface for objects that can serve as attitude pointing targets. It enables orbital directions and celestial bodies to be used interchangeably with `TriadAttitude`.

| Member | Description |
|--------|-------------|
| `GetDirection(StateVector)` | Returns a unit direction vector in the inertial frame |
| `Name` | Human-readable target name |

### OrbitalDirectionTarget

Computes direction from the spacecraft orbital state. Predefined static instances:

| Instance | Direction | Description |
|----------|-----------|-------------|
| `OrbitalDirectionTarget.Prograde` | +v | Velocity direction |
| `OrbitalDirectionTarget.Retrograde` | -v | Anti-velocity direction |
| `OrbitalDirectionTarget.Nadir` | -r | Toward central body center |
| `OrbitalDirectionTarget.Zenith` | +r | Away from central body center |
| `OrbitalDirectionTarget.Normal` | h = r x v | Orbital angular momentum |
| `OrbitalDirectionTarget.AntiNormal` | -h | Anti-normal |

### CelestialAttitudeTarget

Wraps an `ILocalizable` (celestial body, site) for use as an attitude target.

| Property | Description |
|----------|-------------|
| `Target` | The `ILocalizable` to track |
| `Aberration` | Aberration correction applied when computing direction |

`CelestialAttitudeTarget` accepts an optional `Aberration` parameter (default: `Aberration.None`):

- **`Aberration.None`** (default) — geometric instantaneous direction, consistent with the attitude pipeline and industry practice.
- **`Aberration.LT`** — light-time corrected apparent direction, for photon-based scenarios (e.g., optical sensors observing distant targets).

```csharp
// Default: geometric (Aberration.None)
var sunTarget = new CelestialAttitudeTarget(sun);

// Explicit light-time correction
var moonTarget = new CelestialAttitudeTarget(moon, Aberration.LT);
```

## Examples

### Single-vector attitude

```csharp
var nadir = new NadirAttitude(earth, epoch, TimeSpan.FromMinutes(30), engine);
spacecraft.SetStandbyManeuver(nadir);
```

### Instrument pointing

```csharp
var pointing = new InstrumentPointingToAttitude(
    epoch, TimeSpan.FromHours(2), telescope, targetStar, engine);
spacecraft.SetStandbyManeuver(pointing);
```

### TRIAD with orbital directions and celestial target

```csharp
var attitude = new TriadAttitude(
    earth, epoch, TimeSpan.FromHours(1.5),
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    Spacecraft.Up, new CelestialAttitudeTarget(sun),
    engine);
spacecraft.SetStandbyManeuver(attitude);
```

### LVLH factory

```csharp
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);
spacecraft.SetStandbyManeuver(lvlh);
```

## See Also

- [Impulse Maneuvers](impulse-maneuvers.md)
- [Spacecraft](spacecraft.md)
- [Propagators](propagators.md)
