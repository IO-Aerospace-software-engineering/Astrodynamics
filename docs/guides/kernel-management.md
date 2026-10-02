# Kernel Management

SPICE kernels supply the ephemeris, orientation, leap-second, and body-constant data that the framework relies on. Load the full set your workflow needs at startup and clear it when the process is done.

## Kernel Categories

| Type | Extension | Content |
|------|-----------|---------|
| LSK | `.tls` | Leap-second tables (required for UTC conversions) |
| SPK | `.bsp` | Ephemeris data for planets, moons, spacecraft |
| PCK | `.tpc`, `.bpc` | Body constants (radii, GM) and orientation models |
| FK | `.tf` | Frame definitions |
| CK | `.bc` | Attitude/orientation kernels for spacecraft or instruments |

## API

| Method | Description |
|--------|-------------|
| `SpiceAPI.Instance.LoadKernels(FileSystemInfo)` | Load a single kernel file or an entire directory |
| `SpiceAPI.Instance.UnloadKernels(FileSystemInfo)` | Unload a file or directory load |
| `SpiceAPI.Instance.ClearKernels()` | Unload all kernels |
| `SpiceAPI.Instance.GetLoadedKernels()` | List currently loaded kernels |

## Example

```csharp
// Load all kernels from a directory at startup
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels/"));

foreach (var kernel in SpiceAPI.Instance.GetLoadedKernels())
    Console.WriteLine(kernel);

// ... perform analysis ...

// Clean up on shutdown
SpiceAPI.Instance.ClearKernels();
```

## Mission-Specific Kernels

When using the [kernel-backed Spacecraft constructor](../reference/spacecraft.md), load the mission SPK and CK files so that SPICE can resolve the spacecraft NAIF ID.

```csharp
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels/lro"));

var lro = new Spacecraft(naifId: -85, name: "LRO",
    epoch: new Time(2024, 6, 1, 12, 0, 0));
```

## Tips

!!! info "Load once, use many"
    Load the full kernel set at process startup. Repeated load/unload cycles add overhead and risk coverage gaps. The framework queries SPICE frequently during propagation and geometry searches, so all required kernels should be resident before any computation begins.

!!! warning "Coverage gaps"
    Querying an epoch outside the loaded SPK coverage throws `InvalidOperationException`. Verify that your kernels cover the entire analysis window before propagating.

## See Also

- [SpiceAPI & Data Providers](../reference/spice-api.md)
- [Spacecraft](../reference/spacecraft.md)
