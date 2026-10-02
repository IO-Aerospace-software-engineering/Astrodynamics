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

```csharp
var memoryProvider = new MemoryDataProvider();
Configuration.Instance.SetDataProvider(memoryProvider);
memoryProvider.AddCelestialBodyInfo(customEarth);
```

## See Also

- [Kernel Management](../guides/kernel-management.md)
- [Time & Windows](time-and-windows.md)
