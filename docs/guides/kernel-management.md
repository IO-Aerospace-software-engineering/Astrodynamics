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
| `SpiceAPI.Instance.LoadKernels(FileSystemInfo)` | Load a single kernel file (a meta-kernel included), or the kernel files of a directory and of its subdirectories in ordinal order, the later file winning (other files, such as a geopotential model, are left out); the path is taken as what it is on disk, and one that does not exist throws `ArgumentException`; a file already loaded is not loaded again |
| `SpiceAPI.Instance.LoadKernels(string)` | The same, the path given as text |
| `SpiceAPI.Instance.ReloadKernels(FileSystemInfo)` | Read a path again, on top of the kernels loaded after it: its loaded files unloaded, then all its files loaded (a kernel rewritten on disk); a path never loaded is loaded |
| `SpiceAPI.Instance.ReloadKernels(string)` | The same, the path given as text |
| `SpiceAPI.Instance.UnloadKernels(FileSystemInfo)` | Unload the files a load handed SPICE, whether the path still exists or not; a file or subdirectory of a loaded directory leaves its other files loaded; null throws `ArgumentNullException` |
| `SpiceAPI.Instance.UnloadKernels(string)` | The same, the path given as text |
| `SpiceAPI.Instance.ClearKernels()` | Unload all kernels (`kclear_c`) |
| `SpiceAPI.Instance.GetLoadedKernels()` | List the loaded paths |

## Paths

- `LoadKernels` takes the path as what it is on disk, whatever its .NET type: a `FileInfo` that names a directory loads
  the directory, a `DirectoryInfo` that names a file loads the file. A path that is neither a file nor a directory
  throws `ArgumentException` (parameter `path`) before anything is unloaded or reloaded; before 11.0.0 it loaded nothing
  and the call returned, and the first read that needed the kernel failed.
- Paths are compared by their components, ordinally ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)).
  A file SPICE holds from a load of `SpiceAPI` is not handed to it again (11.0.0): loading a path already loaded, or a
  path inside a loaded directory, furnishes only its files written since, which join that entry, and keeps the
  precedence of the others; loading a directory that holds loaded entries furnishes its other files and replaces those
  entries by its own, listed last. Before 11.0.0 such a load unloaded the entry and loaded it again, on top of every
  kernel loaded after it, at 4 to 7 times the cost (211 to 250 ms a repeat on `Data/SolarSystem`). `ReloadKernels` does
  that now, explicitly: for a kernel rewritten on disk, or to give a path precedence again; a path inside a loaded
  directory is read again inside that entry, which keeps its place, so the directory stays the unit of precedence: a
  file meant to win over the kernels loaded after its directory is kept outside it and loaded on its own (decided by
  S. Guillet, 2026-11-19). A sibling whose name extends a loaded one
  (`kernels2` beside `kernels`) is another path.
- The files of a directory load in ordinal order of their paths relative to it, component by component: the entries of
  each directory sorted by name (`StringComparer.Ordinal`, capitals before lower case), a subdirectory walked at its
  place, so `x/y.tf` loads before `x0.tf` on every platform, where a sort of the whole paths would swap them between
  `/` (0x2F) and `\` (0x5C). Where two files assign the same variable or cover the same object, the later file wins
  (NAIF, Kernel Required Reading, "Kernel Precedence Rule"). Before 11.0.0 they loaded in the order of the directory
  listing, which depends on the file system (the hash of the names on ext4, the creation order on others), so a folder
  of conflicting kernels could give a different winner on another machine. No file of `Data/SolarSystem` overlaps
  another, so no result there moves.
- A load or a reload that hands SPICE a file raises the generation of the kernel pool, after which the predefined
  bodies read their data again; one that hands SPICE nothing (a repeated load, a path that does not exist) leaves it.
  When SPICE fails to load a file, the files of the call loaded before it stay loaded and are listed under the path,
  so that its unload removes them (before 11.0.0 they were loaded and not listed).
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
  `ClearKernels` (`kclear_c`) closed nothing, and it stayed open for the life of the process (measured for SPKs, CKs and
  PCKs; the EK is not measured). Once more than 23 binary kernels were in use (the logical units of CSPICE's handle
  manager), the searches that reached it failed to reconnect it (`SPICE(FILEOPENFAIL)`): for a PCK, every IAU frame,
  whose search goes through the binary PCKs first, even for a deleted PCK never unloaded; for a CK, the reads at epochs
  not already read. For an SPK the orphan handle stayed open, but the reads of the Moon (301), L1 (391), L2 (392) and
  Mars (4) after 25 more DAFs did not fail in the measured layout (measured with CSPICE N0067). The data of a deleted kernel are therefore no longer readable after the
  next load, unload or native write, and `GetLoadedKernels` no longer lists it from then on (a directory whose files were
  all dropped leaves the listing, one with remaining files stays listed); a later unload of its path does nothing. A call
  that hands SPICE nothing (a load of a path that does not exist or of a directory without kernel files) leaves the
  listing as it is, and a symbolic link counts as deleted once its target is.
  Before the fourth round of #668 the listing kept its path until it was unloaded. The
  file is tested by the name SPICE holds, which `LoadKernels` gives as a full path, and only for a name SPICE holds as an
  absolute path. A meta-kernel's `KERNELS_TO_LOAD` may name its files relative to the working directory of its load, and
  a direct C++ caller of `LoadKernelsProxy` may pass a relative name: SPICE holds them under that relative name, which no
  longer reaches the file once the working directory changed, so a kernel held under a relative name is left to SPICE,
  as before 11.0.0, whether its file was deleted or not; a deleted one can still stay open in SPICE, and KEEPER's own
  reopening of it, after the unload of another kernel of its kind, fails with `SPICE(FILENOTFOUND)` once the working
  directory changed or the file was deleted: `UnloadKernels` then reports it, and so do `WriteEphemeris` and
  `WriteOrientation`, whose native writer unloads the file it wrote (`InvalidOperationException` with SPICE's message, a
  new file written, a file the call replaces left on disk as it was and nothing written, since its unload before the
  removal fails the same way; before the fourth round of #668 they returned and the next kernel call failed with a wrong
  error, and a replaced file was lost). Name the files of a
  meta-kernel by absolute paths, or through `PATH_VALUES` and `PATH_SYMBOLS` with an absolute `PATH_VALUES`, to have
  them covered. A DSK is left to SPICE too (its subsystem has no public routine that closes it by its handle): keep a
  loaded DSK on disk until it is unloaded.
- A writer that replaces a file `LoadKernels` loaded, by itself or in a loaded directory, first unloads it as
  `UnloadKernels` does (#706): `GetLoadedKernels` no longer lists it (a directory is replaced by its remaining files), and
  the new file is not loaded. Every other load of the file is unloaded too (a meta-kernel's), under the writer's path or
  an absolute name whose symbolic links resolve through it; a hard link and a relative name of a meta-kernel are not
  (`docs/reference/spice-api.md`). Load it again after the write to read it. Before 11.0.0 the native writers left the caller's
  load in SPICE, which went on reading the replaced file's data, and the managed `Spacecraft.WriteEphemeris` left the path
  listed. A failed unload of the file throws `InvalidOperationException` with SPICE's message, and the file is kept as it
  was; so does a path the file system rejects (a loop of symbolic links), given to a writer or to `LoadKernels`, which
  ended the process before 11.0.0.
- `Spacecraft.WriteKernels(directory)` writes the kernels of a spacecraft and loads each file by its own path, one entry of
  `GetLoadedKernels` each; the `SpacecraftKernels` it returns unloads exactly those files at `Dispose` (R14 and decisions
  79 and 80 of #668). It refuses, before it writes anything, a directory that is or lies inside a loaded directory, and a
  file it would write that SPICE holds, from `LoadKernels`, another handle or a loaded meta-kernel (whose files the
  registry does not list: a native query reads SPICE's own list, decision 102). A load of the directory made while the
  handle lives furnishes nothing for its files and does not take them over: `Dispose` unloads them and replaces that
  entry by its other files; load them again after it to keep them ([Spacecraft](../reference/spacecraft.md#the-kernels)).
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
    epoch: new Time(2024, 6, 1, 12, 0, 0, TimeScale.TDB));
```

## Tips

!!! info "Load once, use many"
    Load the full kernel set at process startup. Repeated load/unload cycles add overhead and risk coverage gaps. The framework queries SPICE frequently during propagation and geometry searches, so all required kernels should be resident before any computation begins.

!!! warning "Coverage gaps"
    Querying an epoch outside the loaded SPK coverage throws `InvalidOperationException`. Verify that your kernels cover the entire analysis window before propagating.

## See Also

- [SpiceAPI & Data Providers](../reference/spice-api.md)
- [Spacecraft](../reference/spacecraft.md)
