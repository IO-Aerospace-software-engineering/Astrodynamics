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
| `ReadEphemeris(...)` | Read state data (throws `InvalidOperationException` on native error) |
| `ReadOrientation(...)` | Read orientation data (throws `InvalidOperationException` on native error) |
| `WriteOrientation(FileInfo, int, IEnumerable<StateOrientation>)` | Write orientation records as a type-3 CK |

### Orientation kernels

`ReadOrientation` and `WriteOrientation` are the C-matrix pair of CSPICE's `ckgpav_c` and `ckw03_c`. Each record is
the rotation from its `ReferenceFrame` (the reference frame of the CK, the source of the rotation) to the spacecraft
frame, `vectorInSpacecraft = vectorInReference.Rotate(Rotation)`, with the angular velocity of the spacecraft relative
to the reference frame, expressed in the reference frame: the convention of `Frame.ToFrame` for that rotation, and of
`Spacecraft.GetOrientation`. On the external CK of the tests (PHSRM) it equals `pxform` from J2000 to its bus frame to 0 rad.

`WriteOrientation` writes its records unchanged, in one interval of one segment tagged with the frame of the first
record: every record carries the same frame, in increasing order of their encoded SCLK, and the spacecraft clock kernel
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
