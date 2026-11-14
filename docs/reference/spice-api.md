# SpiceAPI & Data Providers

## SpiceAPI

`SpiceAPI` is the singleton entry point for SPICE-backed operations.

| Method | Description |
|--------|-------------|
| `LoadKernels(FileSystemInfo path)` | Load a kernel file or a directory of kernels; paths compared by components, a path inside a loaded directory reloading it (#673) |
| `UnloadKernels(FileSystemInfo path)` | Unload the files a load handed SPICE, whether the path still exists or not; after a file or subdirectory of a loaded directory, its other files stay listed in its place (#673) |
| `ClearKernels()` | Unload all kernels (`kclear_c`) |
| `GetLoadedKernels()` | Inspect the loaded paths |
| `GetSpiceVersion()` | Read the underlying CSPICE version |
| `ReadEphemeris(...)` | Read state data (throws `InvalidOperationException` with SPICE's message when the kernels give no state, #454) |
| `ReadOrientation(...)` | Read orientation data (throws `InvalidOperationException` on native error) |
| `WriteOrientation(FileInfo, int, IEnumerable<StateOrientation>)` | Write orientation records as a type-3 CK (throws `ArgumentException` for records in more than one frame, #560, and for a path longer than 255 characters, #673) |
| `WriteEphemeris(FileInfo, int, IEnumerable<StateVector>)` | Write states as an SPK through the native writer, under `Star` and `Site` (throws `ArgumentException` for a path longer than 255 characters, #673) |
| `GetCelestialBodyInfo(int)` | Read the name, centre of motion, radii, GM and frame of a celestial body (throws `ArgumentException` for an identifier that is not one, #560) |

`GetCelestialBodyInfo`, and so `new CelestialBody(id)` and the command `celestial-body-info`, take the identifier of a
celestial body: a barycenter (0 to 9), the Sun (10), a planet, a moon or a Lagrange point (101 to 999), or an identifier
above 1000 (an asteroid, a comet, a star of the library). Any other identifier, a spacecraft or an instrument (negative)
among them, throws `ArgumentException`: the native layer reads the centre of motion of any named identifier and ended the
process for a named spacecraft such as CASSINI (-82) before 11.0.0; an identifier without a name returned an empty
record. The same check guards the ellipsoid of an occultation search whose frame is read from the body
(`FindWindowsOnOccultationConstraint` without frames) and the observer of `ReadEphemeris` by identifiers, which is built
as a celestial body.

SPICE takes a kernel path of at most 255 characters (NAIF, SPICE Kernel Required Reading, "Recommendations on Kernel
File Naming"). `LoadKernels` hands it the full path, so a longer one fails with `SPICE(FILENAMETOOLONG)`; the native
writers `WriteEphemeris` and `WriteOrientation` throw `ArgumentException` for such a target before they write, where
CSPICE wrote a file under the name truncated to 255 and then failed before 11.0.0. The length counted is that of the
bytes handed to CSPICE, UTF-8 on Linux and macOS. `Spacecraft.WriteEphemeris` (the managed SPK writer) and the text
kernel writers write any path.

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
