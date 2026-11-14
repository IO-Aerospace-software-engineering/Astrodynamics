# SpiceAPI & Data Providers

## SpiceAPI

`SpiceAPI` is the singleton entry point for SPICE-backed operations.

| Method | Description |
|--------|-------------|
| `LoadKernels(FileSystemInfo path)` | Load a kernel file or a directory of kernels |
| `UnloadKernels(FileSystemInfo path)` | Unload one kernel or a directory load |
| `ClearKernels()` | Unload all kernels |
| `GetLoadedKernels()` | Inspect the current kernel set |
| `GetSpiceVersion()` | Read the underlying CSPICE version |
| `ReadEphemeris(...)` | Read state data (throws `InvalidOperationException` with SPICE's message when the kernels give no state, #454) |
| `ReadOrientation(...)` | Read orientation data (throws `InvalidOperationException` on native error) |
| `WriteOrientation(FileInfo, int, IEnumerable<StateOrientation>)` | Write orientation records as a type-3 CK (throws `ArgumentException` for records in more than one frame, #560) |
| `GetCelestialBodyInfo(int)` | Read the name, centre of motion, radii, GM and frame of a celestial body (throws `ArgumentException` for an identifier that is not one, #560, or that SPICE does not name, #672) |

`GetCelestialBodyInfo`, and so `new CelestialBody(id)` and the command `celestial-body-info`, take the identifier of a
celestial body: a barycenter (0 to 9), the Sun (10), a planet, a moon or a Lagrange point (101 to 999), or an identifier
above 1000 (an asteroid, a comet, a star of the library). Any other identifier, a spacecraft or an instrument (negative)
among them, throws `ArgumentException`: the native layer reads the centre of motion of any named identifier and ended the
process for a named spacecraft such as CASSINI (-82) before 11.0.0. So does an identifier of those classes that SPICE
does not name, by its built-in names or the loaded kernels (12345678, 2000003): its record held the name of a previous
call or undefined bytes (#672). The same checks guard the ellipsoid of an occultation search whose frame is read from the
body (`FindWindowsOnOccultationConstraint` without frames, `ArgumentException` naming `frontBodyId` or `targetId`) and the
observer of `ReadEphemeris` by identifiers.

A body the kernel pool gives no `BODYnnn_RADII` has radii 0, one it gives no `BODYnnn_GM` a GM of 0, and its frame is
still read (`cnmfrm_c`); a barycentre has no frame (an empty `FrameName`, `FrameId` 0). Before #672 a missing variable
left the GM 0 and the frame name undefined (Pallas, Nix, Hydra, Didymos and Dimorphos read GM 0; Elara a garbage frame).

The observer of `ReadEphemeris` by identifiers is a `Site` for a site identifier, its body's identifier times 1000 plus
its own (1 to 999), the body a planet or a moon (399013 is DSS-13 on the Earth): `new Site(id % 1000, its SPICE name,
new CelestialBody(id / 1000))`, whose constructor reads the site's state at J2000 TDB, so its SPK must cover that epoch.
Any other identifier is built by `CelestialItem.Create`, a barycenter, a Lagrange point or a celestial body (#672: a
site observer was built as a celestial body, which threw or was built from undefined bytes). The state itself is
`spkezr_c`'s, which needs only the identifier.

SPICE has no GCRF frame. `ReadEphemeris`, `ReadOrientation`, `TransformFrame` and `FindWindowsOnCoordinateConstraint`
hand a frame named GCRF (`Frame.GCRF` or `new Frame("GCRF")`, in any case) to SPICE as `J2000`, the axes of GCRF, and
label what SPICE returns with the frame given (#543).

### Units of the window searches

The window searches take the units of the library, except the coordinate search:

- `FindWindowsOnDistanceConstraint`: the distance in m, as `CelestialItem` and `Site` take it (the native layer hands
  km to `gfdist_c`). Before 11.0.0 the kernel-spacecraft branch of `CelestialItem` and `Site` divided it by 1000 once
  more ([#466](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/466)).
- `FindWindowsOnCoordinateConstraint`: the value as `gfposc_c` takes it, km for a distance coordinate (X, Y, Z, range,
  radius, altitude) and rad for an angle; `CelestialItem` and `Site` convert their metres.
- `FindWindowsOnIlluminationConstraint`: the surface point as `Planetodetic`, longitude and latitude in rad and the
  altitude in m above the ellipsoid of the body (equatorial radius and flattening of the kernels); before 11.0.0 the
  altitude was read in km ([#488](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/488)). So
  does the CLI command `find-windows-from-illumination-constraint`.

### Orientation kernels

`ReadOrientation` and `WriteOrientation` are the C-matrix pair of CSPICE's `ckgpav_c` and `ckw03_c`. Each record is
the rotation from its `ReferenceFrame` (the reference frame of the CK, the source of the rotation) to the spacecraft
frame, `vectorInSpacecraft = vectorInReference.Rotate(Rotation)`, with the angular velocity of the spacecraft relative
to the reference frame, expressed in the reference frame: the convention of `Frame.ToFrame` for that rotation, and of
`Spacecraft.GetOrientation`. On the external CK of the tests (PHSRM) it equals `pxform` from J2000 to its bus frame to 0 rad.

`WriteOrientation` writes its records unchanged, in one interval of one segment tagged with the frame of the first
record: every record carries the same frame (another frame throws `ArgumentException` before the native call, which
ended the process before 11.0.0, #560), in increasing order of their encoded SCLK, and the spacecraft clock kernel
is loaded. To write the attitude of a propagated spacecraft, use `Spacecraft.WriteOrientation`, which writes its
attitude record in this convention, in J2000 ([Attitude and kernels](spacecraft.md#attitude-and-kernels)).

### Example

```csharp
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels/"));

foreach (var kernel in SpiceAPI.Instance.GetLoadedKernels())
    Console.WriteLine(kernel);
```

## Configuration

Use `Configuration.Instance.SetDataProvider(...)` to swap between SPICE-backed and memory-backed data access.

## Data Providers

| Provider | Description |
|----------|-------------|
| `SpiceDataProvider` | Default provider using SPICE kernels for production use |
| `MemoryDataProvider` | In-memory provider for testing without kernel files |

### MemoryDataProvider

| Method | Description |
|--------|-------------|
| `AddCelestialBodyInfo(params CelestialBody[] bodies)` | Add celestial body data |
| `AddStateVector(int naifId, Time date, StateVector sv)` | Add state vector |
| `AddStateOrientationToICRF(Frame frame, Time date, StateOrientation orientation)` | Add orientation |

A read at an added epoch returns that entry. A read between entries is interpolated and never stored, so it depends on
the entries only, not on the reads before it (#548): an orientation is the exact spherical linear interpolation of the two
entries around it (`StateOrientation.Interpolate`), a state the 8-point Lagrange interpolation of the nearest added
states. A read outside the entries throws `ArgumentException`.

```csharp
var memoryProvider = new MemoryDataProvider();
Configuration.Instance.SetDataProvider(memoryProvider);
memoryProvider.AddCelestialBodyInfo(customEarth);
```

## See Also

- [Kernel Management](../guides/kernel-management.md)
- [Time & Windows](time-and-windows.md)
