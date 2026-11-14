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
| `SpiceAPI.Instance.LoadKernels(FileSystemInfo)` | Load a single kernel file (a meta-kernel included), or the kernel files of a directory and of its subdirectories (other files, such as a geopotential model, are left out) |
| `SpiceAPI.Instance.UnloadKernels(FileSystemInfo)` | Unload the files a load handed SPICE, whether the path still exists or not; a file or subdirectory of a loaded directory leaves its other files loaded |
| `SpiceAPI.Instance.ClearKernels()` | Unload all kernels (`kclear_c`) |
| `SpiceAPI.Instance.GetLoadedKernels()` | List the loaded paths |

## Paths

- Paths are compared by their components, ordinally ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)):
  loading a path already loaded, or a path inside a loaded directory, reloads that entry; loading a directory first
  unloads the entries inside it. A sibling whose name extends a loaded one (`kernels2` beside `kernels`) is another
  path.
- Each load records the files it handed SPICE, and `UnloadKernels` hands SPICE those names, so a directory deleted or
  moved since its load is still unloaded. Unloading a file or a subdirectory of a loaded directory replaces the
  directory in `GetLoadedKernels` by its remaining files. A path that was never loaded is handed to SPICE as it is,
  which unloads nothing it does not hold (a file a meta-kernel loaded is unloaded by its name).
- SPICE unloads a text kernel or a meta-kernel by clearing the kernel pool and reading again the text kernels still
  loaded (NAIF, Kernel Required Reading, "Unloading Kernels"); while a text kernel deleted since its load is still
  loaded, that read stops at it, the unload fails, and the pool lacks the text kernels loaded after it. The unload of an
  SPK, a CK, a binary PCK, a DSK or an EK opens the other loaded kernels of its kind again (`unload_c` → `SPKLEF`,
  `CKLPF`, `PCKLOF`, `ZZDSKLSF` or `EKLEF`), reads no text kernel, and fails the same way on a kernel of its kind
  deleted since its load; after such a failure the SPKs, CKs or PCKs loaded before the deleted one take precedence over
  those loaded after it, which SPICE did not open again (measured with CSPICE N0067). A meta-kernel's unload also
  unloads the kernels it names and opens again the loaded kernels of each of their kinds. SPICE loads a text kernel as
  a meta-kernel when its data assigns `KERNELS_TO_LOAD`, `PATH_SYMBOLS` or `PATH_VALUES`, not when only its comments
  name them, and an old DAF (`NAIF/DAF`) as an SPK or a CK from its segments. `UnloadKernels` throws
  `InvalidOperationException` for such a failure unless a later unload of the same call, of the same path or of another
  one it matched, read the same kernels again: a text kernel's or a meta-kernel's for the text kernels, a kernel of the
  same kind for an SPK, a CK, a PCK, a DSK or an EK; nothing repairs the failure of a meta-kernel, whose files the library
  does not read, nor that of an old DAF, whose kind it does not tell. Every path the call matched is unloaded before it
  throws. Unload the deleted kernel to restore the pool and the precedence.
- A binary kernel (SPK, CK, binary PCK) that SPICE failed to open again stays open in SPICE for the life of the process:
  KEEPER records the handle 0 for it, so its unload, and `ClearKernels` (`kclear_c`), remove it from the list of loaded
  kernels but close nothing, and SPICE still searches it. Once more than 23 binary kernels are in use (the logical units
  of CSPICE's handle manager), its unit can be released, and a later search of its kind fails to reconnect it
  (`SPICE(FILEOPENFAIL)`): for a PCK, every IAU frame, whose search goes through the binary PCKs first (measured with
  CSPICE N0067). Do not delete a loaded binary kernel; if one was deleted, restart the process.
- SPICE takes a kernel path of at most 255 characters (NAIF, SPICE Kernel Required Reading, "Recommendations on Kernel
  File Naming"), and `LoadKernels` hands it the full path: a kernel at a longer path fails with
  `SPICE(FILENAMETOOLONG)`. Load it through a meta-kernel that names it by a shorter relative path, or move it.
- The command line takes a kernels path that is a directory, a kernel file or a meta-kernel.

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
