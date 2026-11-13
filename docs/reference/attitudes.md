# Attitudes

Attitudes define spacecraft orientation during propagation. They are laid on the attitude timeline of the propagation (not via event detection): each attitude law is in force from its activation to the next one, recomputed at each output epoch of that interval and interpolated between them; see [The attitude timeline](#the-attitude-timeline) and [Frames and interpolation](#frames-and-interpolation).

## Attitude Base Class

All attitude maneuvers inherit from the abstract `Attitude` class.

| Member | Type | Description |
|--------|------|-------------|
| `ComputeOrientation(StateVector)` | Method | Compute the orientation quaternion at the given state (abstract, public): the rotation from the spacecraft frame to the frame of the state |
| `StateOrientation` | Property | The computed spacecraft orientation after execution: the rotation from the spacecraft frame to ICRF, labelled ICRF, whatever the frame of the state |
| `ManeuverCenter` | Property | The central body for the maneuver frame |
| `MinimumEpoch` | Property | Earliest epoch at which the attitude can activate: it activates at the later of this epoch and the end of the window of the previous maneuver of the chain |
| `ManeuverHoldDuration` | Property | How long the attitude is held before the next maneuver of the chain may start; the law stays in force until the next activation. TT seconds about the Earth (#547) |
| `Engine` | Property | The engine associated with this maneuver, whose fuel tank is the propagated spacecraft's: the attitude takes that spacecraft's body axes, and a propagation whose attitude's tank is another spacecraft's, or no spacecraft's, throws `InvalidOperationException` before it runs (#676, #586; before, it ran with the static or the other spacecraft's axes) |

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

## The attitude timeline

A propagation lays its maneuver chain on an attitude timeline (#539): a list of activations in the order of the chain,
each in force from its epoch to the next one.

1. **An attitude** activates at the later of its `MinimumEpoch` and the end of the window of the previous maneuver of
   the chain (the start of the propagation for the first). Its `ThrustWindow` is that epoch, its `ManeuverWindow` runs
   to the end of its hold, which gates the next maneuver of the chain, in that propagation only. A leading attitude
   with a hold therefore delays the first impulse, and an attitude chained after an impulse starts at the end of the
   impulse's `ManeuverWindow`, not at its event.
2. **An impulse** puts its attitude, the front along ΔV, in force from the opening of its thrust window,
   `tc − 0.666 dt`, no earlier than the start of the propagation or the previous activation, to the end of its
   `ManeuverWindow`. Its windows themselves stay `[tc − 0.666 dt, …]` ([Thrust Window
   Convention](impulse-maneuvers.md#thrust-window-convention)).
3. **After an impulse**, the attitude law in force before it resumes at the end of its `ManeuverWindow`, unless an
   attitude chained after the impulse activates there (it is later in the chain, so it holds). With no law before, the
   impulse's attitude holds to the next activation.
4. **An activation after the end of the propagation** is not executed: the attitude keeps null windows, is not in
   `ExecutedManeuvers`, stays the standby maneuver, and the chain stops there.
5. **About the Earth** the holds and the windows count TT seconds from the time argument of the event (#547), as the
   integration and the output grid do (#501); about any other body, SSB mode included, TDB seconds.

Every law is sampled on the solution once the integration is done: at its activation (`Attitude.TryExecute` on the
state of the solution there, or the law itself when it resumes), at each output epoch of its interval, and once more
1 µs before the change that ends it. No value is a sentinel: an attitude that is exactly the identity is recorded like
any other rotation, and the identity of ICRF is the attitude before the first activation. The executed attitudes,
leading, chained and trailing, are in `Spacecraft.ExecutedManeuvers` with the impulses (#570).

Before 11.0.0 the leading attitudes all executed on the initial state, the last one sampled at every output; an
attitude chained after an impulse was computed once on the state of the event and held inertially, the leading attitude
taking over again from the next output; a hold delayed nothing; the impulse's attitude applied from the event.

## Frames and interpolation

**Computed in ICRF.** Every attitude is computed on the state of the spacecraft in ICRF, whatever the inertial frame
of the initial state, and recorded as the rotation from the spacecraft frame to ICRF
([Attitude and kernels](spacecraft.md#attitude-and-kernels)); `Attitude.TryExecute` and `ImpulseManeuver.TryExecute`
return that rotation, labelled ICRF. One orbit therefore gives one attitude in any frame: the roll left free by a
single-vector or an impulse attitude is the minimal rotation in ICRF. Before 11.0.0 it was taken in the axes of the
initial frame, 0.48 to 0.87 rad apart between ICRF and ECLIPJ2000 on the same orbit.

**Between output epochs.** Each attitude law is sampled at every output epoch of its interval; between two samples the attitude is their exact spherical linear interpolation, which a type-3
CK computes. Its residual against the law grows as the square of the output step: on the inclined LEO of the tests
5.7e-7, 5.7e-5 and 2.1e-3 rad at 1, 10 and 60 s outputs (an instrument TRIAD, the worst of the attitudes tested
without an impulse; the TRIAD after an impulse 5.9e-5 rad at 10 s, on Linux, Windows and macOS arm64;
[Attitude and kernels](spacecraft.md#attitude-and-kernels) gives the other figures and their sources). Its angular
velocity, that of the CK, is the rate of the interpolation: 2.3e-5 rad/s from the rate of the law at 10 s outputs. Before 11.0.0 the latest sample held over the step, ω·Δt off (6.4e-2 rad at
60 s, a field-of-view edge 53.8 s late).

**At a discrete change.** An activation is a discrete change of the record; the attitude of an impulse (its front
along ΔV) is discrete and holds until the next activation. Before a discrete change, the law in force is sampled once
more, 1 µs before it, so that it is interpolated up to the change: over the partial step before the opening of an
impulse's thrust window between outputs it is within the interpolation residual (9.3e-5 rad at 60 s outputs, a span of 34.3 s),
where it was held (7.1e-2 rad before 11.0.0). At an epoch shared within 2 µs, the activation later in the chain holds:
an attitude chained after an impulse over the law resumed there, an impulse's attitude over a law of zero length. The
record never holds two entries closer than 2 µs apart but for the lead: about the Earth an activation at the end of a
hold counted in TT seconds falls on the output grid, which is uniform in TT, and the sample of that output is the
activation (#547; before it, a hold counted in TDB seconds put the activation 1e-7 to 2e-7 s from the sample); a minimum
epoch given as a TDB label falls off that grid by the change of TDB − TT from the start to it, and an output sample
within 2 µs of it is dropped (6.0e-7 s at S + 3 400 s in the tests).

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
