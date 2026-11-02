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
| `SetStandbyManeuver(Maneuver, Time?)` | Set maneuver to execute |
| `Propagate(Window, IEnumerable, bool, bool, TimeSpan)` | Propagate orbit |
| `Propagate(Window, IEnumerable, Integrator, bool, bool, TimeSpan, CovariancePropagationOptions)` | Propagate the orbit with the integrator, and the state transition matrix and the covariance of the initial orbital parameters ([Covariance Propagation](covariance-propagation.md)); null options for none |
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get state relative to observer |
| `GetOrientation(Frame, Time)` | The rotation from the frame to the spacecraft frame (the C-matrix of a CK), with the angular velocity of the spacecraft relative to the frame, in the frame ([Attitude and kernels](#attitude-and-kernels)) |
| `WriteEphemeris(FileInfo)` | Write ephemeris to SPK file, in J2000; positions and velocities only, never the covariance |
| `WriteOrientation(FileInfo)` | Write the attitude of the last propagation as a type-3 CK, in J2000 ([Attitude and kernels](#attitude-and-kernels)) |
| `ToOpm(...)` | Export to CCSDS OPM |

## Attitude and kernels

A propagated spacecraft keeps the states and the attitude that the library reads (`GetEphemeris`, the instruments, the
field-of-view and occultation searches, sites, conjunctions) and that it writes as an SPK and a CK. Both are in ICRF,
whatever the inertial frame of the initial state (ICRF, ECLIPJ2000, B1950, FK4, ECLIPB1950 or GALACTIC): the integration
and the `PropagationSolution` stay in that frame, bit for bit, and each output state is rotated to ICRF when it is stored,
its covariance through the Jacobian of the rotation. The states of a spacecraft propagated from another frame, read back
in that frame, are therefore a rotation round trip, no longer bit for bit those of the solution. Before 11.0.0 they were
stored in the initial frame and read as ICRF: up to 1.18e7 m off from GALACTIC, 1.86e11 m in SSB mode
([#535](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/535),
[#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529)).

### The attitude record

`Spacecraft.Frame` holds the attitude record of a managed spacecraft: the rotation from the spacecraft frame to ICRF at
each epoch, `vectorIcrf = vectorInSpacecraft.Rotate(q)`, labelled with the spacecraft frame, its source, as
`GetStateOrientationToICRF` returns it.

- **What fills it.** The propagation only: the initial attitude (the identity of ICRF without an attitude maneuver),
  each attitude at its activation, the attitude of an impulse (its front along ΔV) at its event, the samples of the
  leading attitude at every output epoch, and the latest attitude at the end of the window. Every attitude is computed
  on the state in ICRF ([Attitudes](attitudes.md#frames-and-interpolation)). `AddStateOrientationToICRF` adds a discrete
  attitude, labelled with the spacecraft frame (`ArgumentException` otherwise); it replaces an entry at the same epoch.
  A read never adds an entry, and reads before, during or after a propagation leave the record and the CK unchanged
  ([#523](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/523)).
- **Between two samples of the same attitude law,** the attitude is the exact spherical linear interpolation of the two,
  the interpolation of a type-3 CK
  ([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529)).
- **A discrete change** (an attitude activated after an impulse, an impulse's attitude, an added one): the earlier
  attitude holds until the change. Before a change that follows a continuous law, the propagation samples the law once
  more, on the state of the solution 1 µs before the change, so that the law is interpolated up to it
  ([#534](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/534)).
- **The same epoch.** Two entries less than 2 µs apart are one epoch: an attitude activated later in the maneuver chain
  replaces an earlier one and keeps its own epoch, an output sample never replaces an activation, and the attitude of an
  impulse does not replace the attitude chained after it. A chained attitude counts its hold in TDB seconds while the
  outputs about the Earth sit on the TT grid (#501): the two fall 1e-7 to 2e-7 s apart, one epoch.
- **Outside the record** the first or the last entry holds; a spacecraft never propagated has the identity of ICRF.
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

- **`WriteEphemeris`** writes the stored states, in J2000.
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
  | Between the entries, against the law (the interpolation residual at 10 s outputs) | 5.7e-5 rad |
  | A discrete attitude against its closed form | 4.8e-16 rad; 3.5e-12 rad in SSB mode |
  | The angular velocity against the rate of the law (its closed form over 0.1 s), the library's and the CK's alike | 2.3e-5 rad/s |
  | The stored states against the solution rotated to ICRF by `pxform`, at the outputs | 0, in every frame and in SSB mode |
  | The positions between the outputs against the solution (RK7(8), whose dense output resolves them) | 1.3e-8 m; 7.1e-4 m in SSB mode |
  | The state relative to the Earth in the spacecraft frame (`GetEphemeris`) against `spkezr` in that frame | 3.9e-3 m, 1.0e-5 m/s (2024); 8.8e-3 m, 2.1e-5 m/s (2026); 2.3e-2 m, 1.4e-3 m/s in SSB mode |
  | The managed occultation of the Sun by the Earth from GALACTIC, against its ICRF twin and `gfoclt` on the SPK | within one tick (100 ns); 4.0e-7 s |

  The angular velocity of the CK's model is the rate of the SLERP of each interval, its mean at the samples, linear in
  between: a property of the output step, like the interpolation residual. Two further figures are agreements, not
  accuracies: the library's angular velocity against `xf2rav` of `sxform` on the CK it wrote with that rate, 9.9e-13 rad/s
  (1.5e-12 rad/s in 2026), and its positions against `spkezr` on its SPK, two interpolations of the same stored states,
  7.4e-4 m (2.3e-2 m in SSB mode).

  The interpolation residual grows as the square of the output step: on the orbit of the tests (a = 6 778 km,
  e = 0.001, i = 51.6°, the TRIAD of an instrument, its worst attitude) 5.7e-7, 5.7e-5 and 2.1e-3 rad at 1, 10 and
  60 s outputs, a nadir attitude 4.0e-7 and 1.4e-3 rad at 1 and 60 s (`AttitudeRecordTests`); on the worst inclined
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
converts). A CK of the identity attitude from ICRF is unaffected.

**Not covered.** `TLEPropagator`, which `Spacecraft.Propagate` does not use, records no attitude: its SPK is written (in
J2000), its CK is not (`InvalidOperationException`). The attitude laws after an impulse (a TRIAD held inertially, the
leading attitude resumed from the next output) are those of the library before 11.0.0
([Attitudes](attitudes.md#frames-and-interpolation)).

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
milliseconds about the Earth, which is extrapolated), and `InvalidOperationException` when
the observer is inside the ellipsoid of the target. On the CSPICE path an error of `gftfov` (a missing kernel, a
coverage) gives no window, no exception (the native layer drops it).

`IsInFOV` is the `POINT` condition with the same field and corrections, and the same field errors
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
