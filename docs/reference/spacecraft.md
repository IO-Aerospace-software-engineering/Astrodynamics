# Spacecraft

`Spacecraft` represents a vehicle with mass properties, instruments, engines, fuel tanks, and either a propagated or kernel-backed ephemeris.

## Standard Constructor

Creates a spacecraft for numerical propagation.

```csharp
var spacecraft = new Spacecraft(
    naifId: -1001, name: "MySat",
    mass: 500.0, maximumOperatingMass: 2000.0,
    clock: new Clock("OnboardClk", 256),
    initialOrbitalParameters: orbit,
    sectionalArea: 10.0, dragCoeff: 2.2,
    cosparId: "2024-001A", solarRadiationCoeff: 1.5);
```

## Kernel-Backed Constructor

Creates a spacecraft whose ephemeris comes from loaded SPK/CK kernels. Use for archived missions (LRO, Cassini, JWST) or when using a spacecraft as an observer without propagation.

```csharp
var lro = new Spacecraft(naifId: -85, name: "LRO",
    epoch: new Time(2024, 6, 1, 12, 0, 0), mass: 1018.0, sectionalArea: 12.0);
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `epoch` | `Time.J2000TDB` | Epoch for initial state lookup |
| `mass` | `0.0` | Dry mass (kg) |
| `maximumOperatingMass` | `double.MaxValue` | Maximum operating mass (kg) |
| `clock` | Auto-created | A new `Clock` named `"{name}_CLK"` |
| `sectionalArea` | `1.0` | Mean sectional area (m2) |
| `dragCoeff` | `2.2` | Drag coefficient |
| `solarRadiationCoeff` | `1.0` | Solar radiation pressure coefficient |
| `centerOfMotion` | the Earth | Centre of the initial orbital parameters (`InitialOrbitalParameters.Observer`), which the conjunction analysis takes as its observer when the spacecraft is protected |

The initial orbital parameters are the state at `epoch` relative to `centerOfMotion`, the Earth by default (#620; the
Solar System barycentre before 11.0.0): pass `new Barycenter(0)`, or the body the spacecraft orbits, otherwise. The
kernels must cover `epoch` and chain the spacecraft to that centre, or the constructor throws
`InvalidOperationException` with SPICE's message (#454).

Kernel-backed spacecraft: `IsSpiceBacked` returns `true`. Calling `Propagate()` throws `InvalidOperationException`.

## Properties

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF ID (negative) |
| `DryOperatingMass` | Mass without fuel (kg) |
| `MaximumOperatingMass` | Maximum operating mass (kg) |
| `SectionalArea` | Cross-sectional area for drag and SRP (m2) |
| `DragCoefficient` | Cd (default 2.2) |
| `SolarRadiationCoeff` | Cr (default 1.0) |
| `CosparId` | COSPAR international designator |
| `HardBodyRadius` | Hard-body radius for conjunction assessment (m) |
| `BodyFront` / `BodyRight` / `BodyUp` | Instance body axes (override static defaults) |
| `Instruments` | Read-only collection of instruments |
| `Engines` | Read-only collection of engines |
| `FuelTanks` | Read-only collection of fuel tanks |
| `Payloads` | Read-only collection of payloads |
| `IsSpiceBacked` | Whether ephemeris comes from SPICE kernels |
| `Frame` | Spacecraft body-fixed reference frame |

## Static Body Axes

Default body-frame directions used by all attitude maneuvers when no instance override is set.

| Field | Direction | Vector |
|-------|-----------|--------|
| `Spacecraft.Front` | Forward | +Y |
| `Spacecraft.Back` | Aft | -Y |
| `Spacecraft.Right` | Starboard | +X |
| `Spacecraft.Left` | Port | -X |
| `Spacecraft.Up` | Zenith-facing | +Z |
| `Spacecraft.Down` | Nadir-facing | -Z |

Instance-level overrides can be set via constructor parameters (`bodyFront`, `bodyRight`, `bodyUp`). Custom axes must be orthogonal and right-handed.

## Methods

| Method | Description |
|--------|-------------|
| `AddCircularInstrument(...)` | Add circular FOV instrument |
| `AddEllipticalInstrument(...)` | Add elliptical FOV instrument |
| `AddRectangularInstrument(...)` | Add rectangular FOV instrument |
| `AddEngine(Engine)` | Add propulsion engine |
| `AddFuelTank(FuelTank)` | Add fuel tank |
| `AddPayload(Payload)` | Add payload |
| `GetTotalMass()` | Total mass including fuel |
| `SetStandbyManeuver(Maneuver, Time?)` | Put a maneuver in standby; a later minimum epoch given here is kept by every propagation: an impulse never fires before it, an attitude never activates before it ([Minimum Epoch](impulse-maneuvers.md#minimum-epoch), [The attitude timeline](attitudes.md#the-attitude-timeline)) |
| `Propagate(Window, IEnumerable, bool, bool, TimeSpan)` | Propagate orbit |
| `Propagate(Window, IEnumerable, Integrator, bool, bool, TimeSpan, CovariancePropagationOptions)` | Propagate the orbit with the integrator, and the state transition matrix and the covariance of the initial orbital parameters ([Covariance Propagation](covariance-propagation.md)); null options for none |
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get state relative to observer |
| `GetOrientation(Frame, Time)` | The rotation from the frame to the spacecraft frame (the C-matrix of a CK), with the angular velocity of the spacecraft relative to the frame, in the frame ([Attitude and kernels](#attitude-and-kernels)) |
| `WriteEphemeris(FileInfo)` | Write the stored states to an SPK file, in J2000, one type-9 segment per arc between impulses ([The stored states across an impulse](#the-stored-states-across-an-impulse)); positions and velocities only, never the covariance |
| `AddPropagatedStates(params StateVector[])` | Add states to the stored states, each to the arc of its epoch, in ICRF; a propagation replaces them |
| `WriteOrientation(FileInfo)` | Write the attitude of the last propagation as a type-3 CK, in J2000 ([Attitude and kernels](#attitude-and-kernels)) |
| `ToOpm(...)` | Export to CCSDS OPM |

Each `Propagate` (and `PropagateAsync`, `Scenario.SimulateAsync` and `BatchPropagator`, which go through it) starts the
maneuver chain again from its head: it refuels the tanks, clears `ExecutedManeuvers`, the windows of the maneuvers and the
gates of the previous propagation (#573). The inputs of a propagation are checked when its propagator is built, before
anything is reset: a propagation rejected there leaves the previous one as it was. A spacecraft propagated again fires
its chain as a spacecraft never propagated.

## Attitude and kernels

A propagated spacecraft keeps the states and the attitude that the library reads (`GetEphemeris`, the instruments, the
field-of-view and occultation searches, sites, conjunctions) and that it writes as an SPK and a CK. Both are in ICRF,
whatever the inertial frame of the initial state (ICRF, GCRF, ECLIPJ2000, B1950, FK4, ECLIPB1950 or GALACTIC): the integration
and the `PropagationSolution` stay in that frame, bit for bit, and each output state is rotated to ICRF when it is stored,
its covariance through the Jacobian of the rotation. The states of a spacecraft propagated from another frame, read back
in that frame, are therefore a rotation round trip, no longer bit for bit those of the solution. Before 11.0.0 they were
stored in the initial frame and read as ICRF: up to 1.18e7 m off from GALACTIC, 1.86e11 m in SSB mode
([#535](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/535),
[#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529)).

### The stored states across an impulse

An impulse is a jump of the velocity at its epoch tc. The stored states are split into **arcs** at each impulse
([#540](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/540)), so that no reader interpolates
across the jump:

- **An arc** holds the state after the impulse that opens it, the output states strictly inside it and the state before
  the impulse that closes it, both at the impulse's epoch: the states of the integrator at the event, before and after
  the maneuver. An output closer to an impulse than a tenth of the output step is not stored, the impulse's state is:
  beside it, two nodes that close make the interpolation ill-conditioned (Velocity-Verlet puts an output at tc or one
  tick after it; an output 3 µs from an RK7(8) impulse put the SPK 301 m off at 10 s outputs). The first and the last
  output, the ends of the window, are always stored: an impulse within 2 µs of the start or of the end keeps them in an
  arc of two states with its own state.
- **A short arc**, bounded by an impulse and holding fewer than 4 states (an impulse within the first two outputs, or
  just before the end), takes 16 states of the solution evenly spaced over it, its two ends kept, when it is 15 µs long
  or more (the states then 1 µs apart, distinct epochs of the SPK; shorter, its interpolation is linear within 3e-10 m).
  Without an impulse the states make one arc, never filled, as before 11.0.0.
- **An arc of zero length** is not stored: two impulses at one event; nor an arc between impulses within 2 µs, one
  epoch. An impulse that no segment of the solution carries (Velocity-Verlet can locate the crossing at the end of its
  last step) closes the last arc: the reads and the SPK end with the state before it, as the solution does.
- **A read** (`GetEphemeris`, `GetGeometricStateRelativeTo`, the managed occultation and field-of-view searches, the
  geometry finders, the conjunctions with an `ILocalizable`) takes the arc that holds its epoch, the later one at tc,
  so that at an impulse it gives the state after it, as `PropagationSolution.InterpolateAt`, SPICE (the later segment of
  an SPK) and GMAT's ephemeris give it. It interpolates the 8 states of that arc nearest to its epoch (Lagrange), in the
  order and arithmetic of the read before 11.0.0: far from the impulses, a read is that of before, to the bit. The arcs
  are immutable arrays read by a binary search: a read takes no lock and sorts nothing, 0.001 to 0.003 ms at 7 000 to
  9 000 states, where the sort of every state at each call took 1.6 to 2.2 ms
  ([#522](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/522); 2 000 reads in one process,
  Linux x64, 2026-11-03, not a benchmark).
- **In covariance mode** every stored state carries its covariance: the state before an impulse the covariance at its
  event, the state after it the covariance after the burn, the covariance of its execution error added
  (`ImpulseManeuver.ExecutionError`), a filled state that of its epoch.
- **`AddPropagatedStates`** puts each state in the arc that holds its epoch (the later one at an impulse), replacing a
  stored state of that arc at the same epoch; without stored states, they make one arc. A propagation by
  `Spacecraft.Propagate` or `Scenario.SimulateAsync` replaces every stored state, those added before it included: before
  11.0.0 they were merged with the propagated ones. `TLEPropagator.Propagate` still adds its states, as before.

**Accuracy across an impulse** (`PropagatedArcsTests`, the worst over Linux x64, Windows and macOS arm64, CI run
41651463194, 2026-11-03, and run 41689602272 for the velocities in SSB mode against the anchored reference; the
cases: #540's LEO, 233.8 m/s at perigee; perigee, plane alignment, combined maneuvers; the chain of four of `ScenarioTests`;
Velocity-Verlet and RK7(8); 1, 10 and 60 s outputs; the paths of `Propagate` and `Scenario`;
the initial frames; SSB mode; the Moon; within ±150 s of each impulse, every 0.25 s and at tc ± 1 µs; against the
solution of the propagation, itself checked against `prop2b` in the two-body cases; and a timed impulse 2.2 µs to 1 s
from an output, on either side):

| Reader | Before 11.0.0 | 11.0.0 |
|---|---:|---:|
| The managed reads, RK7(8) at 1 and 10 s outputs | 23.5 to 1 420 m; 763 m on the chain of four | 6e-9 to 1.7e-8 m; 2.3e-8 m on the chain, their error far from the impulses |
| The same at 60 s outputs (one-sided at the ends of an arc) | 2 120 m to 16.1 km | 3.3e-4 m; 1.8e-2 m on the chain, whose arcs end 7 min apart |
| `spkezr` on the SPK the library writes | 22.4 m to 150 km | the managed reads within 1.2e-3 m (1.1e-3 m on the chain, 1.7e-3 m in SSB mode) |
| The velocity 1 µs before an impulse | 40 to 890 m/s, nearly the whole ΔV | 1.3e-11 m/s managed (RK7(8); 4.9e-12 m/s in SSB mode, against the anchored reference below), 8.4e-7 m/s (Velocity-Verlet), 8.5e-7 m/s by `spkezr` (6.3e-7 m/s in SSB mode, anchored), since #569, where the combined maneuvers fire at their apogee (8.0e-7 m/s before) |
| Velocity-Verlet | 18.5 m to 16 km | its own error far from the impulses (0.16 m at 10 s, 33 m at 60 s outputs, the Lagrange of its outputs against its cubic Hermite); 5.9e-4 m at 1 s steps, the label of its event (up to 1e-7 s from the event) |

In SSB mode the solution's dense output carries the noise of the third-body acceleration of the Earth, which loses about
eight digits there (#576), and the RK7(8) step sequence, so the noise, differs per platform: the reads against the
solution, 2.1e-4 to 3.4e-4 m within ±150 s of the impulse and 3.3e-8 to 2.0e-6 m/s at tc − 1 µs over Linux, Windows and
macOS arm64 (CI run 41651463194), are measured, not asserted, until the PR of #576. The velocity 1 µs before the impulse
is measured instead against the anchored reference: the state before the impulse moved back by 1 µs with the closed-form
acceleration of the Sun and the Earth (point masses, kernel GMs, the Earth from the Sun of `spkezr` at the impulse),
whose floor is a few 1e-12 m/s (the roundings of its 1 µs, an ulp of 4.55e-13 s near 2 963 s, and of the heliocentric
velocity, an ulp of 3.64e-12 m/s), the size of the managed figure: 2.6e-12 to 9.1e-12 m/s over 26 step sequences on
Linux x64. The SPK against it is the resolution of the ET doubles (1.2e-7 s) times the acceleration, 8.67 m/s².

### The attitude record

`Spacecraft.Frame` holds the attitude record of a managed spacecraft: the rotation from the spacecraft frame to ICRF at
each epoch, `vectorIcrf = vectorInSpacecraft.Rotate(q)`, labelled with the spacecraft frame, its source, as
`GetStateOrientationToICRF` returns it.

- **What fills it.** The propagation only, from its attitude timeline ([Attitudes](attitudes.md#the-attitude-timeline)):
  the identity of ICRF at the start, which an attitude activated there replaces; each activation at its epoch (an
  attitude law executed on the state of the solution there, a law resumed after an impulse, the attitude of an impulse,
  its front along ΔV, at the opening of its thrust window); the samples of each law at every output epoch of its
  interval; and the latest attitude at the end of the window. Every attitude is computed
  on the state in ICRF ([Attitudes](attitudes.md#frames-and-interpolation)). `AddStateOrientationToICRF` adds a discrete
  attitude, labelled with the spacecraft frame (`ArgumentException` otherwise); it replaces an entry at the same epoch.
  A read never adds an entry, and reads before, during or after a propagation leave the record and the CK unchanged
  ([#523](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/523)).
- **Between two samples of the same attitude law,** the attitude is the exact spherical linear interpolation of the two,
  the interpolation of a type-3 CK
  ([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529)).
- **A discrete change** (an activation of the timeline, an added attitude): the earlier attitude holds until the
  change. Before a change that follows a continuous law, the propagation samples the law once more, on the state of the solution 1 µs before the change, so that the law is interpolated up to it
  ([#534](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/534)).
- **The same epoch.** Two entries less than 2 µs apart are one epoch: an activation later in the maneuver chain
  replaces an earlier one and keeps its own epoch, and an output sample never replaces an activation. Since #547 the
  holds count TT seconds about the Earth, as the outputs, so an activation at the end of a hold ending on an output is
  that output's epoch. A minimum epoch given as a TDB label falls off the TT grid by the change of TDB − TT from the
  start to it (6.0e-7 s at S + 3 400 s in the tests, about 1.5e-5 s a day later): one epoch with the output sample while
  that stays under 2 µs, two entries beyond.
- **Outside the record** the first or the last entry holds; a spacecraft never propagated has the identity of ICRF, which
  `Frame.GetStateOrientationToICRF` and getters such as `GetBoresightInICRFFrame` return, but which the field-of-view
  search does not take for an attitude: it throws ([Field-of-view search](#field-of-view-search), [#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). An
  ephemeris read in the frame of a managed spacecraft is rotated by its record once the record holds an entry; with an
  empty record the frame is read by name in SPICE, as before 11.0.0, which reads the frame kernel and the CK of that id
  when they are loaded and throws SPICE(UNKNOWNFRAME) otherwise ([Frames](frames.md)).
- **The angular velocity** is that of the CK: at each entry the mean of the rates of the interpolated intervals on either
  side, linear between two samples of a law. Over a hold it is zero between discrete entries; after a sample of a law
  that no lead follows (an attitude added by `AddStateOrientationToICRF` after a propagation, or a lead whose change was
  replaced), it ramps linearly from the law's rate at that sample (about 1.1e-3 rad/s for a nadir attitude in LEO) to
  zero 1 µs before the next entry, the CK's own model. It is returned in the convention of the frames
  ([Frames](frames.md#angular-velocity)): ICRF relative to the spacecraft, in the spacecraft frame. The entries of
  `GetStateOrientationsToICRF` and `GetLatestStateOrientationToICRF` carry the rate that `GetStateOrientationToICRF`
  gives at their epochs; the rate an orientation given to `AddStateOrientationToICRF` carries is not kept. A velocity
  expressed in the spacecraft frame (`ToFrame`, `GetEphemeris`, `GetOrientation`) takes this rotation,
  v' = R v − (R ω) × r': 7.7e3 m/s for the Earth's centre seen from a nadir-pointing LEO (it was 0 before 11.0.0).

A kernel-backed spacecraft reads its frame kernel and its CK through SPICE at every call, and keeps nothing.

### The kernels

- **`WriteEphemeris`** writes the stored states, in J2000: one segment of type 9 (Lagrange, unequal steps) per arc, in
  time order, of degree min(7, N − 1) for an arc of N states, the managed reader's window of 8 states; at an impulse the
  later segment, the state after it, is the one SPICE reads, and 1 µs before it the earlier one, the state before. The
  file is written by the library's managed DAF writer (`LTL-IEEE`), in SSB mode and about any central body alike;
  CSPICE's `spkw09_c`, fed the same segments, writes the same file, byte for byte, up to 24 segments (one summary
  record); from 25 on, where CSPICE appends each new summary record after the data, the same summaries and data, a full
  summary record followed by an empty one, so that CSPICE can append to the file (`spkopa`). An arc whose states are one
  TDB second of the file (an impulse within the resolution of the double, 1.2e-7 s in 2024, of the start or the end of
  the propagation) cannot be a segment and is left out: SPICE reads there the neighbouring segment. The file is built and
  checked before an existing file is unloaded and replaced, so that a refused write leaves it as it was; the new one is
  left unloaded, as before. Before 11.0.0 the native writer wrote one segment of degree
  15 across every impulse, of type 8 for equal steps (SSB mode, other central bodies); `Star` and `Site` keep it.
- **`WriteOrientation`** writes the record as a type-3 CK in J2000, one interval: at each entry the C-matrix, J2000 to
  the spacecraft frame (the conjugate of the record's rotation, as `ckw03_c` takes it), with the angular velocity of the
  spacecraft relative to J2000, in J2000; before each discrete change, the earlier attitude repeated 1 µs before it (the
  law's sample there, when the propagation took one). The CK then gives the library's attitude everywhere but over that
  microsecond, where it turns to the new attitude (up to 0.79 rad in the tests); a field-of-view edge moves by at most
  that microsecond and the rounding of its encoded SCLK. The spacecraft clock kernel must be loaded.
  `InvalidOperationException` for a kernel-backed spacecraft or one never propagated.
- **The encoded SCLK.** The CK's epochs are the spacecraft clock of the library, 65 536 ticks per second from 1957: a
  double whose spacing is 2.4e-7 s up to 2025-01-19 03:13 TDB and 4.8e-7 s from then to 2093. It is the floor of the
  attitude read from the CK.
- **Accuracy** (CSPICE N0067 in the test process on the kernels the library writes, against the closed forms of the
  attitude laws on the solution of the propagation, never on the SPK the library wrote; 8 attitude cases, Velocity-Verlet
  and RK7(8), the six inertial frames, the scenario path in three frames, SSB mode (RK7(8) at 1e-9), an epoch after
  2025-01-19, `AttitudeRecordTests`; the worst over Linux x64, Windows x64 and macOS arm64, CI run 41522747911,
  2026-11-02, the same on the three: the whole grid on Linux, the case of each worst on the three):

  | Quantity | Worst |
  |---|---:|
  | The library's attitude against `pxform` on its CK, outside the 1 µs before the changes | 6.1e-10 rad (2024), 1.3e-9 rad (2026) |
  | The instrument axes against `pxform(instrument, J2000)` | 6.1e-10 rad (2024), 1.3e-9 rad (2026) |
  | The attitude at the entries against the law | 1.3e-15 rad; 3.5e-12 rad in SSB mode, where the reference state relative to the Earth is the difference of two states 1.5e11 m from the Sun |
  | Between the entries, against the law (the interpolation residual at 10 s outputs) | 5.7e-5 rad; 5.9e-5 rad for the TRIAD after an impulse, a law sampled since #539 (the same on Linux, Windows and macOS arm64, CI run 42021804663) |
  | A discrete attitude against its closed form | 4.8e-16 rad; 3.5e-12 rad in SSB mode |
  | The angular velocity against the rate of the law (its closed form over 0.1 s), the library's and the CK's alike | 2.3e-5 rad/s |
  | The stored states against the solution rotated to ICRF by `pxform`, at the outputs | 0, in every frame and in SSB mode |
  | The positions between the outputs against the solution (RK7(8), whose dense output resolves them) | 1.3e-8 m; 7.1e-4 m in SSB mode; across an impulse, 1.2e-8 m (CI run 41651463194, the worst on Linux, in the Validation category; in SSB mode measured, not asserted, the noise of the third body near the Earth, #576) |
  | The state relative to the Earth in the spacecraft frame (`GetEphemeris`) against `spkezr` in that frame | 3.9e-3 m, 1.0e-5 m/s (2024); 8.8e-3 m, 2.1e-5 m/s (2026); 2.35e-3 m, 5.9e-6 m/s in SSB mode, 2.6e-3 m across an impulse (CI run 41651463194; across an impulse the worst on Linux, in the Validation category; 2.3e-2 m and 1.4e-3 m/s with the type-8 segment of the native writer before 11.0.0) |
  | The managed occultation of the Sun by the Earth from GALACTIC, against its ICRF twin and `gfoclt` on the SPK | within one tick (100 ns); 4.0e-7 s |

  The angular velocity of the CK's model is the rate of the SLERP of each interval, its mean at the samples, linear in
  between: a property of the output step, like the interpolation residual. Two further figures are agreements, not
  accuracies: the library's angular velocity against `xf2rav` of `sxform` on the CK it wrote with that rate, 9.9e-13 rad/s
  (1.5e-12 rad/s in 2026), and its positions against `spkezr` on its SPK, two interpolations of the same stored states,
  7.4e-4 m (1.8e-3 m in SSB mode on the three platforms, CI run 41651463194; 2.3e-2 m with the type-8 segment of the
  native writer before 11.0.0); across an impulse, one-sided at the ends of the arcs, 7.8e-4 m (2.6e-3 m in SSB mode; the
  worst on Linux, in the Validation category, CI run 41651463194; these cases were excluded before 11.0.0, #540).

  The interpolation residual grows as the square of the output step: on the orbit of the tests (a = 6 778 km,
  e = 0.001, i = 51.6°, the TRIAD of an instrument, the worst of its attitudes without an impulse) 5.7e-7, 5.7e-5 and
  2.1e-3 rad at 1, 10 and 60 s outputs, the TRIAD after an impulse 5.9e-5 rad at 10 s (the worst at 10 s since #539, on
  the three platforms), a nadir attitude 4.0e-7 and 1.4e-3 rad at 1 and 60 s (`AttitudeRecordTests`); on the worst inclined
  state of the design of #537 (its reader 3, a probe on the DLLs of the base and of the prototype), 8.1e-7, 8.1e-5 and
  2.9e-3 rad; on a circular equatorial orbit, where the attitude turns at a constant rate about a fixed axis, 1.1e-10 rad
  (the same reader). A shorter output step lowers it.

`SpiceAPI.ReadOrientation` and `SpiceAPI.WriteOrientation` are the raw C-matrix pair of `ckgpav_c` and `ckw03_c`
([SpiceAPI](spice-api.md#orientation-kernels)). `ReadOrientation` of a CK written by 11.0.0 returns the C-matrix of the
attitude.

**CKs written before 11.0.0** hold the inverse of the attitude (3.14 rad off for a nadir attitude), in the initial frame
of the propagation, interpolated across the discrete changes (107.5° on the case of #534): every reader, SPICE,
Cosmographia, `ReadOrientation` and a kernel-backed spacecraft, reads them as before. They cannot be repaired from their
content: regenerate them by propagating again. SPKs written before are right (in the initial frame, which SPICE
converts) away from the impulses; across an impulse they interpolate through the jump of the velocity, by up to 150 km
near a 635 m/s burn (#540): regenerate them. A CK of the identity attitude from ICRF is unaffected.

**Not covered.** `TLEPropagator`, which `Spacecraft.Propagate` does not use, records no attitude: its SPK is written (in
J2000), its CK is not (`InvalidOperationException`).

## Components

### Engine

| Property | Description |
|----------|-------------|
| `Name` | Engine name |
| `Model` | Engine model identifier |
| `SerialNumber` | Serial number |
| `ISP` | Specific impulse (s) |
| `FuelFlow` | Fuel mass flow rate (kg/s) |
| `Thrust` | Computed thrust (N) |
| `FuelTank` | Associated fuel tank |

```csharp
var engine = new Engine("MainEngine", "RS-25", "SN001",
    isp: 366.0, fuelFlow: 0.5, fuelTank: tank);
```

### FuelTank

| Property | Description |
|----------|-------------|
| `Name` | Tank name |
| `Model` | Tank model identifier |
| `SerialNumber` | Serial number |
| `Capacity` | Maximum capacity (kg) |
| `InitialQuantity` | Initial fuel quantity (kg) |
| `Quantity` | Current fuel quantity (kg) |

```csharp
var tank = new FuelTank("MainTank", "FT-100", "SN001",
    capacity: 1000.0, initialQuantity: 800.0);
```

### Instrument

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF instrument ID |
| `Name` | Instrument name |
| `Model` | Instrument model identifier |
| `FieldOfView` | Half-angle in the plane of the boresight and the reference vector, the IK's `FOV_REF_ANGLE` (rad) |
| `CrossAngle` | Half-angle across that plane, the IK's `FOV_CROSS_ANGLE` (rad); `EllipticalInstrument` and `RectangularInstrument` |
| `Shape` | `InstrumentShape` (Circular, Rectangular, Elliptical) |
| `Boresight` | Boresight direction in instrument frame |
| `RefVector` | Reference vector in instrument frame |
| `Orientation` | Instrument orientation relative to spacecraft: the Euler angles (x, y, z), in radians, about the axes 1, 2, 3 of the frame kernel ([Instrument orientation](#instrument-orientation)) |
| `RotationToSpacecraft` | The rotation from the instrument frame to the spacecraft frame of `Orientation`, `vectorInSpacecraft = vectorInInstrument.Rotate(q)` |

| Method | Description |
|--------|-------------|
| `IsInFOV(Time, ILocalizable, Aberration)` | Whether the center of the target is in the field at an epoch: the `POINT` condition of CSPICE's `fovtrg`, with the corrections of the search |
| `FindWindowsInFieldOfViewConstraint(Window, Spacecraft, ILocalizable, Frame, ShapeType, Aberration, TimeSpan)` | The windows, in TDB, where the target is in the field: those of CSPICE's `gftfov` ([Field-of-view search](#field-of-view-search)) |
| `GetBoresightInSpacecraftFrame()` | Transform boresight to spacecraft body frame |
| `GetRefVectorInSpacecraftFrame()` | Transform reference vector to spacecraft body frame |

#### Instrument orientation

`Orientation` holds the Euler angles (x, y, z), in radians, of the frame kernel the library writes for the instrument
(`WriteFrameAsync`): `TKFRAME_<id>_ANGLES = (x, y, z)`, `TKFRAME_<id>_AXES = (1, 2, 3)`, `UNITS = 'RADIANS'`, relative to
the spacecraft frame. NAIF's Frames Required Reading defines them in "Defining a TK Frame Using Euler Angles"
(https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/frames.html): "If M is the matrix that converts vectors relative
to the TK frame to the RELATIVE frame by left multiplication, then the angles and axes must satisfy the following
relationship: M = [angle_1]<sub>axis_1</sub> [angle_2]<sub>axis_2</sub> [angle_3]<sub>axis_3</sub> where the symbol
[ A ]<sub>i</sub> stands for a rotation by the angle A about the i'th axis". Its matrices of [A]<sub>i</sub> are the
rotations of the frame, the transposes of the rotations of a vector by A. So `pxform(instrument, spacecraft)` = [x]1 [y]2 [z]3 = `eul2m(x, y, z, 1, 2, 3)`: a vector of the
instrument frame is rotated by −z about Z, then by −y about Y, then by −x about X, into the spacecraft frame.
`RotationToSpacecraft` is that rotation; the managed field-of-view path, `GetBoresightInSpacecraftFrame`,
`GetRefVectorInSpacecraftFrame`, and the attitudes built from an instrument (`TriadAttitude`,
`InstrumentPointingToAttitude`) all use it, so they agree with SPICE reading the frame kernel (#524).

#### Field-of-view search

`FindWindowsInFieldOfViewConstraint` finds the windows when a target is in the field of view of an instrument, its
spacecraft the observer (the `observer` argument is not used). It computes what CSPICE's `gftfov` computes.

```csharp
var camera = spacecraft.Instruments.First();
var windows = camera.FindWindowsInFieldOfViewConstraint(window, spacecraft, earth, new Frames.Frame("ITRF93"),
    ShapeType.Ellipsoid, Aberration.LT, TimeSpan.FromSeconds(60.0));
```

**The field.** The field of the instrument kernel: `FieldOfView` is the half-angle in the plane of the boresight and the
reference vector, `CrossAngle` the half-angle across it (`getfov`). The reference vector is made perpendicular to the
boresight, as `getfov` does, and the axes are read at each epoch through the spacecraft's attitude.

**The condition.**

| Field \ target | `ShapeType.Point` | `ShapeType.Ellipsoid` |
|---|---|---|
| Circle | the direction of the center within the cone of half-angle `FieldOfView` | the apparent disc of the ellipsoid overlaps the field, partly or whole |
| Ellipse | the ray through the plane at unit distance along the boresight within the ellipse of semi-axes tan `FieldOfView` and tan `CrossAngle` | the same |
| Rectangle | the ray within the rectangle of half-sides tan `FieldOfView` and tan `CrossAngle` | a ray of the pyramid meets the ellipsoid in front of the observer |

An ellipsoid target is the triaxial ellipsoid of the radii of a `CelestialBody` (`BODYnnn_RADII`), oriented by
`targetFrame` at the light-time corrected epoch. The frame is used as given, without a check of its center; `gftfov`
requires a body-fixed frame centered on the target. It is ignored for a point, as `gftfov` ignores it.

**Two paths.**

- **CSPICE.** When the instrument's spacecraft and the target both come from the kernels, `gftfov` searches, with the
  instrument and frame kernels loaded under the instrument's NAIF identifier, and its convergence tolerance of 1 µs.
- **Managed.** Otherwise the library searches: a propagated spacecraft or a spacecraft from a TLE, a kernel-backed
  spacecraft with a propagated spacecraft as target, a site given by its coordinates, or a star. It reads the states and
  the orientations, and never writes a kernel. The margin of the target in the field, continuous, positive in view and
  zero at the contact, is sampled at every step; between three samples that show an extreme not reaching zero, the
  extreme is searched, and Brent's method locates each zero to 1 µs, `gftfov`'s tolerance. For a circle or an ellipse
  against an ellipsoid, the margin is `gftfov`'s construction, an ellipsoid whose limb is the cone of the field, and the
  overlap margin of the [occultation search](occultations.md), with the target mapped to the unit sphere; for a
  rectangle against an ellipsoid, the largest discriminant over the rectangle of the ray through the ellipsoid, in
  closed form.

Both paths return TDB windows, whatever the time frame of the search window, and count the step in TDB seconds. A
window that holds at the start or the end of the search window, or whose transition lies within 1 µs of them, starts or
ends there. The managed path reads no state outside the search window, except the target at its light-time corrected
epoch: up to its light time before the start (reception, `LT` and `CN`) or after the end (transmission, `XLT` and
`XCN`).

**Domain.** The overlap margin samples the limb of the field in the space of the target. It was measured for targets of
aspect ratio up to 1.0034 (the Earth, the Moon, the Sun) and fields of aspect ratio up to 10 311: no sign error. An
elongated target stretches the limb of the field by its own aspect ratio.

**Step.** `gftfov` asks the step to be shorter than every window and every gap. The managed search finds an event or a
gap shorter than the step as long as the step is at most half the time between the extremes of the margin: with a step
longer than an event, the managed path may find a window that `gftfov` misses.

**Aberration.** The light time of `spkltc` (`LT`, `CN` and their transmission forms `XLT`, `XCN`); for the `+S`
corrections, the stellar aberration of `stelab` (`stlabx` for transmission), as `gftfov` applies them. A `Star` is a
direction, as `gfrfov` reads a ray: no light time (`LT` and `CN` read as `None`, their `+S` forms as the stellar
aberration alone). `CelestialItem.GetEphemeris` does not yet apply the stellar aberration as `stelab` does
([#491](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/491)), so it can differ from the
search for a `+S` correction.

**Errors.** Both paths check the same rules before the search, and throw:

- `ArgumentOutOfRangeException`: a step that is not positive;
- `ArgumentException`: a search window that ends before it starts, a target that is the instrument's spacecraft, an
  ellipsoid target without radii (a site, a spacecraft, a barycenter, a Lagrange point, a star);
- `InvalidOperationException`: a boresight that is zero or parallel to the reference vector, a half-angle that is not
  positive, a field whose angular radius (the larger half-angle of a circle or an ellipse, the angle of the corner of a
  rectangle) exceeds π/2 − 1e-6, as `gftfov` rejects it, or a shape other than a circle on an instrument that is neither
  an `EllipticalInstrument` nor a `RectangularInstrument`.

The managed path also throws `ArgumentException` when the search window leaves the propagated states of the
instrument's spacecraft or of a spacecraft target, which would be extrapolated (a spacecraft target is read up to its
light time before the start for the reception corrections, or after the end for the transmission ones, `XLT` and `XCN`,
milliseconds about the Earth, which is extrapolated), and `InvalidOperationException` when the instrument's spacecraft
has no attitude, or when the observer is inside the ellipsoid of the target. A managed spacecraft has no attitude while
its attitude record is empty: never propagated and with no orientation added, built from a TLE, or propagated by
`TLEPropagator` ([#546](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/546)); its CK cannot be written either. Propagate it, or give it its attitude,
`spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(q, Vector3.Zero, epoch, spacecraft.Frame))`, held from
then on ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682); before 11.0.0 the identity of ICRF was searched). This check comes after those of the
arguments and of the field, whose errors come first. On the CSPICE path an error of `gftfov` (a missing kernel, a
coverage) gives no window, no exception (the native layer drops it).

`IsInFOV` is the `POINT` condition with the same field and corrections, and the same field and attitude errors
(`InvalidOperationException`) and target error (`ArgumentException`, the instrument's own spacecraft); the search does
not call it, so an override does not change the search.

##### Accuracy

Against CSPICE N0067, the worst over Linux x64, Windows x64 and macOS arm64 (CI run 41365060160, 2026-11-02): every
row the same on the three platforms. The grids of the kernel-backed observers and of the propagated spacecraft run on
Linux only (the `Validation` category), their worst rows on the three. The limit of each test is twice the worst of the
rows it bounds, rounded up at the second significant digit.

| Scenario | Reference | Worst edge |
|----------|-----------|-----------:|
| Kernel-backed observers, managed search (`tools/fov_reference`: an inclined LEO with a turning attitude and a polar LEO, 30 fields, circles, ellipses and rectangles; the Earth, the Moon, the Sun, DSS-13, a spacecraft, the Earth-Moon barycenter, L1), geometric | `gftfov` converged to 1e-9 s | 8.3e-7 s |
| The same, with the light time and `+S` | same | 2.1e-6 s |
| A rectangle against the Earth's ellipsoid near the pole, where `gftfov` stops on `SPICE(INVALIDELLIPSE)` | a scan of the rays of the pyramid's faces (`surfpt`), `gfudb` converged to 1e-9 s | 4.8e-7 s |
| A star, `None`, `S`, `XS` | `gfrfov` converged to 1e-9 s | 4.8e-7 s |
| The CSPICE path on the same kernels (`gftfov` at its 1 µs) | `gftfov` converged to 1e-9 s | 9.5e-7 s |
| Propagated spacecraft, the public managed path and its kernel-backed twin: 26 fields, both target shapes, the Earth, the Moon, the Sun, DSS-13, a site given by its coordinates, a spacecraft, the Earth-Moon barycenter, L1, a star; `None`, `LT` and `LT+S` on every field, the nine aberrations on a circle and a narrow field (221 searches off the grazing and slow edges) | `gftfov` at 1 µs on the kernels the library writes | 1.3e-6 s |
| RK7(8), SSB mode, a TLE | same (for the TLE, an SPK of its states, which the library does not write for a TLE that is not propagated) | 1.2e-6 s |
| An impulse followed by a TRIAD attitude, a nadir attitude, the six inertial initial frames, the path of `Scenario.SimulateAsync` (CI run 41522747911, 2026-11-02: the variants of #515 the same on the three platforms, those of #537 on Linux only) | `gftfov` at 1 µs on the kernels the library writes | 1.2e-6 s (SSB mode); TRIAD 1.0e-6 s, nadir 4e-7 s |
| Fields that graze the Earth's limb (a margin of slope 1.2e-6 rad/s at the contact) | `gftfov` at a step of 1 s | 2.5e-4 s |
| A slow edge (an ellipse, slope 6.1e-4 per second, `LT+S`) | same | 4.4e-6 s |
| `IsInFOV` on a grid of epochs more than 1 ms from an edge | `fovtrg` and `fovray` | every epoch equal |

Off the grazing and slow edges, the differences are the rounding of the light-time corrected epochs to a double ET on
both sides (up to 1.8 mm on the Earth's position). At a grazing contact the margin changes slowly, and the same
millimetres cost up to 2.5e-4 s.

The kernels the library writes describe what it reads ([Attitude and kernels](#attitude-and-kernels)): the CK repeats
the earlier attitude 1 µs before a discrete change, which moves an edge by at most that microsecond and the rounding of
its encoded SCLK. Before 11.0.0 they did not, for an attitude other than the identity or an initial state outside ICRF,
and `gftfov` on them differed from both paths of the library: the CK held the inverse attitude
([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529)), interpolated across a discrete
change (971.8 s on an edge,
[#534](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/534)), a spacecraft from ECLIPJ2000 was
read 2.5e6 m off its SPK (232.6 s,
[#535](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/535)), and the library held a nadir
attitude over each output step where the CK interpolates (0.885 s).

Before 11.0.0 the managed search tested the direction of the target's center, whatever its shape, the circle as a
square, the ellipse with its half-angles swapped, the rectangle on the axes of ICRF, and the reference vector without
making it perpendicular to the boresight; it sampled a boolean condition, refined it to about 1 s, and could start a
window one step before the search window. On the cases of the tests its windows were off by up to 1 480 s, and it
found no window for a rectangle where `gftfov` finds two.

### Payload

```csharp
var payload = new Payload("Camera", 25.0, "SN-42");
```

## See Also

- [Numerical Propagation](../tutorials/numerical-propagation.md)
- [Maneuvers](../tutorials/maneuvers.md)
- [Attitudes](attitudes.md)
- [OPM](opm.md)
