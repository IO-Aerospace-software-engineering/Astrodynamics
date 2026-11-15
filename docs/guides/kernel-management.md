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
| `SpiceAPI.Instance.LoadKernels(FileSystemInfo)` | Load a single kernel file (a meta-kernel included), or the kernel files of a directory and of its subdirectories (other files, such as a geopotential model, are left out); the path is taken as what it is on disk, and one that does not exist throws `ArgumentException` |
| `SpiceAPI.Instance.UnloadKernels(FileSystemInfo)` | Unload the files a load handed SPICE, whether the path still exists or not; a file or subdirectory of a loaded directory leaves its other files loaded |
| `SpiceAPI.Instance.ClearKernels()` | Unload all kernels (`kclear_c`) |
| `SpiceAPI.Instance.GetLoadedKernels()` | List the loaded paths |

## Paths

- `LoadKernels` takes the path as what it is on disk, whatever its .NET type: a `FileInfo` that names a directory loads
  the directory, a `DirectoryInfo` that names a file loads the file. A path that is neither a file nor a directory
  throws `ArgumentException` (parameter `path`) before anything is unloaded or reloaded; before 11.0.0 it loaded nothing
  and the call returned, and the first read that needed the kernel failed.
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
  `CKLPF`, `PCKLOF`, `ZZDSKLSF` or `EKLEF`) and reads no text kernel; for a DSK it fails the same way on a DSK deleted
  since its load. A deleted SPK, CK, binary PCK or EK no longer makes it fail: see the next point. A meta-kernel's unload also
  unloads the kernels it names and opens again the loaded kernels of each of their kinds. SPICE loads a text kernel as
  a meta-kernel when its data assigns `KERNELS_TO_LOAD`, `PATH_SYMBOLS` or `PATH_VALUES`, not when only its comments
  name them, and an old DAF (`NAIF/DAF`) as an SPK or a CK from its segments. `UnloadKernels` throws
  `InvalidOperationException` for such a failure unless a later unload of the same call, of the same path or of another
  one it matched, read the same kernels again: a text kernel's or a meta-kernel's for the text kernels, a kernel of the
  same kind for an SPK, a CK, a PCK, a DSK or an EK; nothing repairs the failure of a meta-kernel, whose files the library
  does not read, nor that of an old DAF, whose kind it does not tell. Every path the call matched is unloaded before it
  throws. Unload the deleted kernel to restore the pool and the precedence.
- Each native load and unload (every file `LoadKernels` hands SPICE, every `UnloadKernels`, and the unloads of the
  writers `WriteEphemeris` and `WriteOrientation`, which `Star.WriteEphemeris`, `Site.WriteEphemeris`,
  `Spacecraft.WriteOrientation` and the Cosmographia export reach) first closes every SPK, CK,
  binary PCK and EK whose file was deleted since its load, by the handle SPICE holds for it (`spkuef_c`, `ckupf_c`,
  `pckuof_c`, `ekuef_c`), then unloads it; the last of those unloads opens the remaining kernels of each kind again in
  their load order, which keeps their precedence. Before 11.0.0, SPICE failed to open such a kernel again after the
  unload of another kernel of its kind (`SPICE(FILENOTFOUND)`): KEEPER recorded the handle 0 for it, so its unload and
  `ClearKernels` (`kclear_c`) closed nothing, it stayed open for the life of the process, and once more than 23 binary
  kernels were in use (the logical units of CSPICE's handle manager) a later search of its kind failed to reconnect it
  (`SPICE(FILEOPENFAIL)`): for a PCK, every IAU frame, whose search goes through the binary PCKs first, even for a deleted
  PCK never unloaded (measured with CSPICE N0067). The data of a deleted kernel are therefore no longer readable after the
  next load, unload or native write; `GetLoadedKernels` lists its path until it is unloaded, which then does nothing. The
  file is tested by the name SPICE holds, which `LoadKernels` gives as a full path. A meta-kernel's `KERNELS_TO_LOAD` may
  name its files relative to the working directory of its load: SPICE holds them under that relative name, which no
  longer reaches the file once the working directory changed, so a kernel held under a relative name is left to SPICE,
  as before 11.0.0, whether its file was deleted or not; a deleted one can still stay open in SPICE. Name the files of a
  meta-kernel by absolute paths, or through `PATH_VALUES` and `PATH_SYMBOLS` with an absolute `PATH_VALUES`, to have
  them covered. A DSK is left to SPICE too (its subsystem has no public routine that closes it by its handle): keep a
  loaded DSK on disk until it is unloaded.
- SPICE takes a kernel path of at most 255 characters (NAIF, SPICE Kernel Required Reading, "Recommendations on Kernel
  File Naming"), and `LoadKernels` hands it the full path: a kernel at a longer path fails with
  `SPICE(FILENAMETOOLONG)`. Load it through a meta-kernel that names it by a shorter relative path, or move it.
- The command line takes a kernels path that is a directory, a kernel file or a meta-kernel; a path that does not exist
  is an argument error that names it.

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
