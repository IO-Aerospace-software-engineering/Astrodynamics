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
| `ManeuverHoldDuration` | Property | How long the attitude is held before the next maneuver of the chain may start; the law stays in force until the next activation. TT seconds about the Earth (#547); zero or more, a negative hold throwing `ArgumentOutOfRangeException` (`maneuverHoldDuration`) on every maneuver constructor (since 11.0.0, R11 of #668); its setter, which applies the same check, is `private protected` (decision 11 of lot L4: `protected` in 10.x) |
| `Engine` | Property | The engine associated with this maneuver, whose fuel tank is the propagated spacecraft's: the attitude takes that spacecraft's body axes, and a propagation whose attitude's tank is another spacecraft's, or no spacecraft's, throws `InvalidOperationException` before it runs (#676, #586; before, it ran with the static or the other spacecraft's axes) |

**Constructor** (used by all concrete attitude types):

```csharp-signature
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
// Inputs: maneuverCenter (CelestialItem), minimumEpoch (Time), holdDuration (TimeSpan), engine (Engine)
new ProgradeAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new RetrogradeAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new NadirAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new ZenithAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new NormalAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
new AntiNormalAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
```

## InstrumentPointingToAttitude

Points an instrument boresight at a celestial target: the minimal rotation that takes the boresight, in the spacecraft frame (`Instrument.GetBoresightInSpacecraftFrame()`, through the Euler angles of the frame kernel, [Instrument orientation](spacecraft.md#instrument-orientation)), to the direction of the target. This is a single-vector attitude (roll unconstrained). Before 11.0.0 it returned the inverse rotation, which took the target's direction to the boresight: at zero orientation the boresight landed at twice the angle between them from the target.

The direction is that of the target as the spacecraft sees it, corrected by the attitude's `Aberration`, `Aberration.LT` by default: the target at t − lt from the spacecraft's position at t, lt the light time of CSPICE's `spkltc` (one iteration for `LT` and `XLT`, converged for `CN` and `XCN`), and for the `+S` corrections the stellar aberration of `stelab` (`stlabx` for transmission) from the spacecraft's velocity relative to the solar system barycenter, as `spkcvo_c` corrects it; a `Star` takes no light time, its `+S` corrections the stellar aberration alone, as `Star.GetEphemeris` reads it; `Aberration.None` points at the geometric direction ([#549](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/549), decided by S. Guillet, 2026-11-17). The boresight agrees with `spkcvo_c` (the Moon from a LEO) and `spkcpt_c` (a site given by its coordinates) to about 1e-13 and 1e-12 rad. Before 11.0.0 the law asked for LT but read a celestial target, the centre of the maneuver, relative to itself, so its direction was geometric: the Moon 4.4e-5 to 9.2e-5 rad from its LT direction from a LEO; a site of the kernels took the light time from its body's centre, and a site given by its coordinates met the correction #489 dropped, so its direction was geometric.

| Property | Description |
|----------|-------------|
| `Instrument` | The instrument whose boresight defines the pointing axis |
| `Target` | The `ILocalizable` target to track |
| `Aberration` | The correction of the target's direction, `Aberration.LT` by default |

```csharp
// Inputs: minimumEpoch (Time), holdDuration (TimeSpan), camera (Instrument), moon (CelestialBody), engine (Engine)
var pointing = new InstrumentPointingToAttitude(
    minimumEpoch, holdDuration, camera, moon, engine);

// The geometric direction
var geometric = new InstrumentPointingToAttitude(
    minimumEpoch, holdDuration, camera, moon, engine, Aberration.None);
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
| `MinimumVectorSeparation` | Minimum angle of the body vectors, and of the reference vectors, from collinear, in [1e-14, π/2] rad (default 5 degrees) |

!!! warning
    The primary and secondary body vectors (and reference vectors) must be at least `MinimumVectorSeparation` (5 degrees by default) from collinear, parallel or antiparallel; otherwise orientation computation is ill-conditioned. Two vectors pass when the sine of their angle, |û × ŵ| of their unit vectors, is at least sin(separation): an angle in [separation, π − separation], free of the rounding of π − separation and of the resolution of acos near ±1, so that collinear vectors that round apart are rejected at every separation allowed: their sine is at most about (3 + √2)·2^-53 = 4.9e-16 for components collinear to their rounding (measured up to 2.4e-16), and the smallest separation is 1e-14 rad, below which such a pair passed with a roll set by rounding. A separation outside [1e-14, π/2], NaN included, throws `ArgumentOutOfRangeException` (`minimumVectorSeparation`) at construction; body vectors too close to collinear, zero or not finite throw `ArgumentException` at construction, and reference vectors too close to collinear, or not finite, `InvalidOperationException` in `ComputeOrientation` at the epoch where they are (fourth round of #668).

### Constructors

**Single instrument** — uses instrument boresight as primary body vector and reference vector as secondary, both in the spacecraft frame through the Euler angles of the frame kernel ([Instrument orientation](spacecraft.md#instrument-orientation)), so that the boresight the field-of-view search and SPICE see is the one aimed:

```csharp
// Inputs: minimumEpoch (Time), holdDuration (TimeSpan), camera (Instrument), primaryTarget and secondaryTarget
//   (ILocalizable), engine (Engine)
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    camera, primaryTarget, secondaryTarget, engine);
```

**Dual instruments** — different instruments for primary and secondary pointing:

```csharp
// Inputs: primaryInstrument and secondaryInstrument (Instrument), primaryTarget and secondaryTarget (ILocalizable),
//   engine (Engine), minimumEpoch (Time), holdDuration (TimeSpan)
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    primaryInstrument, primaryTarget,
    secondaryInstrument, secondaryTarget, engine);
```

**Explicit body vectors + ILocalizable targets** — direct spacecraft-frame vectors with celestial body targets:

```csharp
// Inputs: minimumEpoch (Time), holdDuration (TimeSpan), moon and sun (CelestialBody), engine (Engine)
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    Spacecraft.Front, moon,       // Primary: Front points at Moon
    Spacecraft.Up, sun,           // Secondary: Up toward Sun
    engine);
```

**Explicit body vectors + IAttitudeTarget targets** — supports orbital directions and celestial bodies:

```csharp
// Inputs: earth and sun (CelestialBody), minimumEpoch (Time), holdDuration (TimeSpan), engine (Engine)
var attitude = new TriadAttitude(
    earth, minimumEpoch, holdDuration,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    Spacecraft.Up, new CelestialAttitudeTarget(sun),
    engine);
```

### Factory Methods

| Method | Primary | Secondary | Description |
|--------|---------|-----------|-------------|
| `TriadAttitude.CreateLVLH(maneuverCenter, epoch, duration, engine)` | the spacecraft's `BodyDown` toward Nadir | its `BodyFront` toward Prograde | Local Vertical / Local Horizontal attitude |
| `TriadAttitude.CreateProgradeWithSunTracking(maneuverCenter, epoch, duration, sun, engine)` | the spacecraft's `BodyFront` toward Prograde | its `BodyUp` toward Sun | Prograde with solar-panel Sun tracking |

The axes are those of the spacecraft that carries the engine's tank, read at execution; while the tank is on no
spacecraft, the static `Spacecraft.Down`, `Front` and `Up`. `PrimaryBodyVector` and `SecondaryBodyVector` of a factory
attitude return those axes. Before 11.0.0 the factories pointed the static axes whatever the spacecraft's, 90° off for
a spacecraft whose front is +X or +Z (#677); for that attitude, pass the static axes to the constructor:
`new TriadAttitude(center, epoch, duration, Spacecraft.Down, OrbitalDirectionTarget.Nadir, Spacecraft.Front,
OrbitalDirectionTarget.Prograde, engine)`.

```csharp
// Inputs: earth and sun (CelestialBody), epoch (Time), duration (TimeSpan), engine (Engine)
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

Instance-level overrides can be set via the `Spacecraft` constructor parameters (`bodyFront`, `bodyRight`, `bodyUp`). Custom axes must be finite, non-zero, orthogonal and right-handed; a zero axis, or one with a component that is not finite, throws `ArgumentException` naming it. The axes are read on the spacecraft that carries the engine's tank, at execution, with the static axes as fallback while the tank is on no spacecraft, which a read outside a propagation alone meets (a propagation of an attitude whose tank is not the propagated spacecraft's throws, see `Engine` above, #676): the single-vector attitudes (`NadirAttitude`, `ZenithAttitude`, `ProgradeAttitude`, `RetrogradeAttitude`, `NormalAttitude`, `AntiNormalAttitude`) and the attitude of an impulse point `BodyFront` (`GetBodyFront()`), `TriadAttitude.CreateLVLH` points `BodyDown` and `BodyFront`, and `TriadAttitude.CreateProgradeWithSunTracking` `BodyFront` and `BodyUp` (#677). The `TriadAttitude` constructors take literal body vectors: pass `sc.BodyFront`, `sc.BodyUp`, ... for a spacecraft's own axes. `InstrumentPointingToAttitude` points the instrument's boresight.

## The attitude timeline

A propagation lays its maneuver chain on an attitude timeline (#539): a list of activations in the order of the chain,
each in force from its epoch to the next one.

1. **An attitude** activates at the later of its `MinimumEpoch` and the end of the window of the previous maneuver of
   the chain (the start of the propagation for the first). Its `ThrustWindow` is that epoch, its `ManeuverWindow` runs
   to the end of its hold, which gates the next maneuver of the chain, in that propagation only. A leading attitude
   with a hold therefore delays the first impulse, and an attitude chained after an impulse starts at the end of the
   impulse's `ManeuverWindow`, not at its event.
2. **An impulse** puts its attitude, the front along ΔV, in force from the opening of its thrust window,
   `tc − f dt` (tc the centroid of the acceleration of the burn, #335), no earlier than the start of the propagation or
   the previous activation, to the end of its `ManeuverWindow`. Its windows themselves stay `[tc − f dt, …]` ([Thrust
   Window Convention](impulse-maneuvers.md#thrust-window-convention)).
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
impulse's thrust window between outputs it is within the interpolation residual (1.0e-4 rad at 60 s outputs, a span of 35.6 s),
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

Every correction other than `None` is the target as the spacecraft sees it, as CSPICE's `spkcvo_c` corrects it from
the spacecraft's state: the light time of `spkltc` from the spacecraft's position, and for the `+S` corrections the
stellar aberration of `stelab` from its velocity relative to the solar system barycenter, the rule of
`InstrumentPointingToAttitude` (a `Star` without light time). Before 11.0.0 the correction was the central body's: its
light time, the stellar aberration of its velocity, and none at all for a target that is the central body itself
([#740](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/740): from a LEO, 1.0e-6 rad for the
Moon with `LT`, 2.5e-5 rad with `LT+S`, 1.0e-4 rad for the Earth with `LT`). `None` is the geometric direction, bit for
bit as before.

```csharp
// Inputs: sun and moon (CelestialBody)
// Default: geometric (Aberration.None)
var sunTarget = new CelestialAttitudeTarget(sun);

// Explicit light-time correction
var moonTarget = new CelestialAttitudeTarget(moon, Aberration.LT);
```

## Examples

### Single-vector attitude

```csharp
// Inputs: earth (CelestialBody), epoch (Time), engine (Engine), spacecraft (Spacecraft)
var nadir = new NadirAttitude(earth, epoch, TimeSpan.FromMinutes(30), engine);
spacecraft.SetStandbyManeuver(nadir);
```

### Instrument pointing

```csharp
// Inputs: epoch (Time), telescope (Instrument), targetStar (ILocalizable), engine (Engine), spacecraft (Spacecraft)
var pointing = new InstrumentPointingToAttitude(
    epoch, TimeSpan.FromHours(2), telescope, targetStar, engine);
spacecraft.SetStandbyManeuver(pointing);
```

### TRIAD with orbital directions and celestial target

```csharp
// Inputs: earth and sun (CelestialBody), epoch (Time), engine (Engine), spacecraft (Spacecraft)
var attitude = new TriadAttitude(
    earth, epoch, TimeSpan.FromHours(1.5),
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    Spacecraft.Up, new CelestialAttitudeTarget(sun),
    engine);
spacecraft.SetStandbyManeuver(attitude);
```

### LVLH factory

```csharp
// Inputs: earth (CelestialBody), epoch (Time), duration (TimeSpan), engine (Engine), spacecraft (Spacecraft)
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);
spacecraft.SetStandbyManeuver(lvlh);
```

## See Also

- [Impulse Maneuvers](impulse-maneuvers.md)
- [Spacecraft](spacecraft.md)
- [Propagators](propagators.md)
