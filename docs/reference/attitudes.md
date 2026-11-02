# Attitudes

Attitudes define spacecraft orientation during propagation. They are processed immediately in the maneuver chain (not via event detection). The leading attitude of the chain is recomputed at each output epoch, and interpolated between them; see [Frames and interpolation](#frames-and-interpolation).

## Attitude Base Class

All attitude maneuvers inherit from the abstract `Attitude` class.

| Member | Type | Description |
|--------|------|-------------|
| `ComputeOrientation(StateVector)` | Method | Compute the orientation quaternion at the given state (abstract, public): the rotation from the spacecraft frame to the frame of the state |
| `StateOrientation` | Property | The computed spacecraft orientation after execution: the rotation from the spacecraft frame to ICRF, labelled ICRF, whatever the frame of the state |
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

Points an instrument boresight at a celestial target: the minimal rotation that takes the boresight, in the spacecraft frame (`Instrument.GetBoresightInSpacecraftFrame()`, through the Euler angles of the frame kernel, [Instrument orientation](spacecraft.md#instrument-orientation)), to the direction of the target. This is a single-vector attitude (roll unconstrained). Before 11.0.0 it returned the inverse rotation, which took the target's direction to the boresight: at zero orientation the boresight landed at twice the angle between them from the target.

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

**Single instrument** — uses instrument boresight as primary body vector and reference vector as secondary, both in the spacecraft frame through the Euler angles of the frame kernel ([Instrument orientation](spacecraft.md#instrument-orientation)), so that the boresight the field-of-view search and SPICE see is the one aimed:

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

## Frames and interpolation

**Computed in ICRF.** Every attitude is computed on the state of the spacecraft in ICRF, whatever the inertial frame
of the initial state, and recorded as the rotation from the spacecraft frame to ICRF
([Attitude and kernels](spacecraft.md#attitude-and-kernels)); `Attitude.TryExecute` and `ImpulseManeuver.TryExecute`
return that rotation, labelled ICRF. One orbit therefore gives one attitude in any frame: the roll left free by a
single-vector or an impulse attitude is the minimal rotation in ICRF. Before 11.0.0 it was taken in the axes of the
initial frame, 0.48 to 0.87 rad apart between ICRF and ECLIPJ2000 on the same orbit.

**Between output epochs.** The leading attitude of the chain (the last attitude before the first impulse) is sampled
at every output epoch; between two samples the attitude is their exact spherical linear interpolation, which a type-3
CK computes. Its residual against the law grows as the square of the output step: on the inclined LEO of the tests
5.7e-7, 5.7e-5 and 2.1e-3 rad at 1, 10 and 60 s outputs (an instrument TRIAD, the worst of the attitudes tested;
[Attitude and kernels](spacecraft.md#attitude-and-kernels) gives the other figures and their sources). Its angular
velocity, that of the CK, is the rate of the interpolation: 2.3e-5 rad/s from the rate of the law at 10 s outputs. Before 11.0.0 the latest sample held over the step, ω·Δt off (6.4e-2 rad at
60 s, a field-of-view edge 53.8 s late).

**At a discrete change.** An attitude activated after an impulse, and the attitude of an impulse (its front along ΔV),
are discrete: each holds until the next entry. Before a discrete change, the leading attitude is sampled once more, 1 µs
before it, so that it is interpolated up to the change: over the partial step before an RK7(8) event between outputs it
is within the interpolation residual (1.2e-4 rad at 60 s outputs), where it was held (7.1e-2 rad before 11.0.0). At an epoch shared
within 2 µs, the attitude activated later in the chain holds: the one chained after an impulse, not the impulse's own.

**Not changed by 11.0.0.** An attitude chained after an impulse is computed once, on the state of the impulse's event
(also when the hold of an earlier attitude puts its activation later), and held inertially; the leading attitude,
sampled at every output, takes over again from the next output epoch after it. The
attitude of an impulse applies from the impulse, not from the opening of its thrust window.

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
