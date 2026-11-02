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
| `WriteEphemeris(FileInfo)` | Write ephemeris to SPK file; positions and velocities only, never the covariance |
| `ToOpm(...)` | Export to CCSDS OPM |

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
| `Orientation` | Instrument orientation relative to spacecraft |

| Method | Description |
|--------|-------------|
| `IsInFOV(Time, ILocalizable, Aberration)` | Whether the center of the target is in the field at an epoch: the `POINT` condition of CSPICE's `fovtrg`, with the corrections of the search |
| `FindWindowsInFieldOfViewConstraint(Window, Spacecraft, ILocalizable, Frame, ShapeType, Aberration, TimeSpan)` | The windows, in TDB, where the target is in the field: those of CSPICE's `gftfov` ([Field-of-view search](#field-of-view-search)) |
| `GetBoresightInSpacecraftFrame()` | Transform boresight to spacecraft body frame |
| `GetRefVectorInSpacecraftFrame()` | Transform reference vector to spacecraft body frame |

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
| The same, with the light time and `+S` | same | 2.2e-6 s |
| A rectangle against the Earth's ellipsoid near the pole, where `gftfov` stops on `SPICE(INVALIDELLIPSE)` | a scan of the rays of the pyramid's faces (`surfpt`), `gfudb` converged to 1e-9 s | 4.8e-7 s |
| A star, `None`, `S`, `XS` | `gfrfov` converged to 1e-9 s | 4.8e-7 s |
| The CSPICE path on the same kernels (`gftfov` at its 1 µs) | `gftfov` converged to 1e-9 s | 9.5e-7 s |
| Propagated spacecraft, the public managed path and its kernel-backed twin: 26 fields, both target shapes, the Earth, the Moon, the Sun, DSS-13, a site given by its coordinates, a spacecraft, the Earth-Moon barycenter, L1, a star; `None`, `LT` and `LT+S` on every field, the nine aberrations on a circle and a narrow field (221 searches off the grazing and slow edges) | `gftfov` at 1 µs on the kernels the library writes | 1.3e-6 s |
| RK7(8), SSB mode, a TLE | same (for the TLE, an SPK of its states, which the library does not write for a TLE that is not propagated) | 1.2e-6 s |
| An impulse followed by a TRIAD attitude, an initial state in ECLIPJ2000 | `gftfov` at 1 µs on kernels of the states and orientations the library reads | 1.0e-6 s |
| The same, on the kernels the library writes | `gftfov` at 1 µs | 9.7e-3 to 972 s, or another window count (below) |
| Fields that graze the Earth's limb (a margin of slope 1.2e-6 rad/s at the contact) | `gftfov` at a step of 1 s | 2.5e-4 s |
| A slow edge (an ellipse, slope 6.1e-4 per second, `LT+S`) | same | 4.4e-6 s |
| A nadir attitude | `gftfov` on kernels of the orientations the library reads | 0.885 s |
| `IsInFOV` on a grid of epochs more than 1 ms from an edge | `fovtrg` and `fovray` | every epoch equal |

Off the grazing and slow edges, the differences are the rounding of the light-time corrected epochs to a double ET on
both sides (up to 1.8 mm on the Earth's position). At a grazing contact the margin changes slowly, and the same
millimetres cost up to 2.5e-4 s. The nadir attitude row is the attitude model of the library: it holds each orientation
of its output steps, where the CK that `gftfov` reads interpolates between them, so an edge moves by up to the output
step.

The kernels the library writes describe what it reads for a propagated spacecraft whose attitude is the identity of
ICRF and whose initial state is in ICRF. Otherwise they do not, because of three defects outside the field-of-view
search, each measured (2026-11-01) and each its own issue ([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529), [#534](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/534) and
[#535](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/535), in the order below), and `gftfov` on those kernels differs from both paths of the
library:

- the CK that `Spacecraft.WriteOrientation` writes holds the inverse of the library's attitude: for a nadir attitude
  `gftfov` finds no window where the library finds the Sun and the Moon entering a wide field, and the CK points the
  front at the zenith;
- the CK interpolates across a discrete attitude change, such as a `TriadAttitude` maneuver, which the library holds from
  the maneuver on: 268 to 972 s on the edges, and one window where the library finds two;
- a propagated spacecraft whose initial state is in ECLIPJ2000 stores its states in that frame and reads them as ICRF,
  up to 2.5e6 m from the SPK the library writes from them, which CSPICE reads right: 9.7e-3 s to 0.77 s on the Earth's
  edges, 216 s and 233 s on DSS-13's.

Before 10.2.0 the managed search tested the direction of the target's center, whatever its shape, the circle as a
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
