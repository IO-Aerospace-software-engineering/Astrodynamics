# SpiceApi & Data Providers

## SpiceApi

`SpiceApi` is the singleton entry point for SPICE-backed operations.

SPICE reports its errors to the library only: the native layer sets SPICE to return from an error (`erract` RETURN) and
to print nothing (`errprt` NONE), and each method throws with SPICE's message. Before 11.0.0 SPICE also printed the
banner of every error on the standard output of the process, those the library handles included (a read past the
coverage of a kernel; 2888 banners in one conjunction analysis past its SPKs), which corrupted the output of a host
application such as a command-line tool ([#727](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/727),
decided by S. Guillet, 2026-11-17).

### SPICE errors

An error SPICE signals throws `SpiceException` (`IO.Astrodynamics.Exceptions`), an `InvalidOperationException`:
`ShortMessage` is SPICE's code, such as `SPICE(SPKINSUFFDATA)` for a state past the coverage of the SPKs,
`SPICE(FRAMEDATANOTFOUND)` for a frame past that of its data or `SPICE(KERNELVARNOTFOUND)` for a missing kernel variable;
`LongMessage` is SPICE's long message, empty for the errors that carry none (`prop2b_c`'s); `Message` names the call that
failed, then gives SPICE's message. The native layer reports both beside its message (`GetLastErrorShortProxy`,
`GetLastErrorLongProxy`), equal to what CSPICE gives for the same call. Every method below that the table says throws
`InvalidOperationException` with SPICE's message throws it, and so do the paths built on them (`GetEphemeris`, `ToFrame`,
the CSPICE searches of `CelestialItem`, `Site` and `Instrument`); a failure of the native layer that is not SPICE's (a
C++ exception with no SPICE error pending) stays an `InvalidOperationException`, and a frame name SPICE does
not know an `ArgumentException` whose inner exception is the `SpiceException`. Before 11.0.0 every one of them was an
`InvalidOperationException` (R03 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
structuring choice 10, decided by S. Guillet, 2026-11-19); a `catch (InvalidOperationException)` still catches them.

```csharp
try
{
    var moon = SpiceApi.Instance.ReadEphemeris(new Time(3000, 1, 1, TimeScale.TDB), 399, 301, Frame.ICRF, Aberration.None);
}
catch (SpiceException e) when (e.ShortMessage == "SPICE(SPKINSUFFDATA)")
{
    // past the coverage of the loaded SPKs: load one that covers the epoch
}
```

| Method | Description |
|--------|-------------|
| `LoadKernels(FileSystemInfo path)` | Load a kernel file or a directory of kernels, the path taken as what it is on disk, `ArgumentException` for one that does not exist; paths compared by components (#673); the files of a directory in ordinal order, component by component, the later file winning; only the files SPICE does not hold from a load of `SpiceApi` (11.0.0: before, a repeated load reloaded) |
| `LoadKernels(string path)` | The same, the path given as text |
| `ReloadKernels(FileSystemInfo path)` | Unload the loaded files of the path and load all its files again, on top (11.0.0: what a repeated `LoadKernels` did) |
| `ReloadKernels(string path)` | The same, the path given as text |
| `UnloadKernels(FileSystemInfo path)` | Unload the files a load handed SPICE, whether the path still exists or not; after a file or subdirectory of a loaded directory, its other files stay listed in its place (#673); a deleted SPK, CK, binary PCK or EK already dropped by an earlier native call is no longer listed, and its unload does nothing (#668); null throws `ArgumentNullException` (11.0.0: before, it returned) |
| `UnloadKernels(string path)` | The same, the path given as text |
| `ClearKernels()` | Unload all kernels (`kclear_c`) |
| `GetLoadedKernels()` | Inspect the loaded paths; a deleted SPK, CK, binary PCK or EK leaves the listing at the next native load, unload or write (#668), and a file a writer replaces leaves it before the write (#706) |
| `GetSpiceVersion()` | Read the underlying CSPICE version |
| `ReadEphemeris(...)` | Read state data, a `StateVector` (over a window, `StateVector`s; 11.0.0: typed `OrbitalParameters` before, R20 of #668) (throws `InvalidOperationException` with SPICE's message when the kernels give no state, #454); over a window, at the points of `Window.Sample(step)`, start + i·step in the time frame of the window start (UTC steps for a UTC window), i from 0 while within the window, the end included when it falls on the grid, each state the read of its epoch alone and labelled with it in that frame (#563; 11.0.0, #829: before, a UTC window across a leap second was read one step past its end) |
| `ReadOrientation(...)` | Read orientation data (throws `InvalidOperationException` on native error); a record of a CK segment without angular velocity has a zero angular velocity (#714); at the points of `Window.Sample(step)`, start + i·step in the time frame of the window start, i from 0 while within the window, in a buffer of the epochs read, each record labelled with the TDB epoch of the CK's record (#563; 11.0.0, #829: before, a UTC window across a leap second was read one step past its end) |
| `WriteOrientation(FileInfo, int, IEnumerable<StateOrientation>)` | Write orientation records as a type-3 CK (throws `ArgumentException` for records in more than one frame, #560, and for a path longer than 255 characters, #673; `InvalidOperationException` with SPICE's message when the writing or the last unload of the written CK fails, #668, or with the file system's for a path it rejects, #706); a file it replaces that `LoadKernels` loaded is unloaded first (#706) |
| `WriteEphemeris(FileInfo, int, IEnumerable<StateVector>)` | Write states as an SPK through the native writer, under `Star` and `Site` (throws `ArgumentException` for fewer than three states, states in more than one frame or a centre that is not a celestial body, #706, or that SPICE does not name, #714, and for a path longer than 255 characters, #673; `InvalidOperationException` with SPICE's message when the writing or the last unload of the written SPK fails, #668, or with the file system's for a path it rejects, #706); a file it replaces that `LoadKernels` loaded is unloaded first (#706) |
| `GetCelestialBodyInfo(int)` | Read the name, centre of motion, radii, GM and frame of a celestial body as a `CelestialBodyInfo` (11.0.0: the interop struct IO.Astrodynamics.DTO.CelestialBody, now internal, before, the same values; throws `ArgumentException` for an identifier that is not one, #560, or that SPICE does not name, #672) |
| `TransformFrame(Time, Frame, Frame)` | The rotation and angular velocity from one frame to another (throws `ArgumentException` naming `fromFrame` or `toFrame` for a name SPICE does not know, the error below its inner exception, 11.0.0; `InvalidOperationException` naming the frames and the epoch, then SPICE's message, when SPICE gives none for frames it knows: frame data that do not cover the epoch, #714) |
| `TransformFrame(Window, Frame, Frame, TimeSpan)` | The orientations at the points of `Window.Sample(step)`, start + i·step up to the window end, read lazily, the end read when it lies on the grid (11.0.0, #781: before, it was dropped even on the grid); a step of zero or less throws `ArgumentOutOfRangeException` (`stepSize`) at the call (decision 10 of lot L4 of #668: before, a zero step enumerated without end) |

`GetCelestialBodyInfo`, and so `new CelestialBody(id)` and the command `celestial-body-info`, take the identifier of a
celestial body: a barycenter (0 to 9), the Sun (10), a planet, a moon or a Lagrange point (101 to 999), or an identifier
above 1000 (an asteroid, a comet, a star of the library). Any other identifier, a spacecraft or an instrument (negative)
among them, throws `ArgumentException`: the native layer reads the centre of motion of any named identifier and ended the
process for a named spacecraft such as CASSINI (-82) before 11.0.0. So does an identifier of those classes that SPICE
does not name, by its built-in names or the loaded kernels (12345678, 2000003): its record held the name of a previous
call or undefined bytes (#672). The same checks guard the ellipsoid of an occultation search whose frame is read from the
body (`FindWindowsOnOccultationConstraint` without frames, `ArgumentException` naming `frontBodyId` or `targetId`) and the
observer of `ReadEphemeris` by identifiers built as a celestial body. A Lagrange point (391 to 395) is the exception:
`CelestialItem.Create`, and so the observer of `ReadEphemeris`, builds it as a `LagrangePoint` without reading its body
information, whether the kernels name it or not (`Data/SolarSystem` names none of them; the `LagrangePoint` constructor
reads its state at J2000 TDB, so its SPK must cover that epoch), while `GetCelestialBodyInfo` and `new CelestialBody` of
an unnamed one throw.

The record of a site identifier, its body's identifier times 1000 plus its own (399013 for DSS-13, the body a planet or a
moon SPICE names), has its body as centre of motion and its body's barycentre as barycentre, 399 and 3 for DSS-13 as its
SPK segment ([#651](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/651): before 11.0.0 the Sun
and 0, the native layer classing every identifier above 1000 as an asteroid); `CelestialItem.IsAsteroid` is false for it.
An identifier of that form whose body SPICE does not name keeps 10 and 0 and stays an asteroid: SPICE's built-in names
include two ESA stations, NOTO (398989) and NEW NORCIA (398990), whose identifiers do not follow the rule, 398 being no
body.

A body the kernel pool gives no `BODYnnn_RADII` has radii 0, one it gives no `BODYnnn_GM` a GM of 0, and its frame is
still read (`cnmfrm_c`); a barycentre has no frame (an empty `FrameName`, `FrameId` 0). Before #672 a missing variable
left the GM 0 and the frame name undefined (Pallas, Nix, Hydra, Didymos and Dimorphos read GM 0; Elara a garbage frame).

The observer of `ReadEphemeris` by identifiers is a `Site` for a site identifier, its body's identifier times 1000 plus
its own (1 to 999), the body a planet or a moon that SPICE names (399013 is DSS-13 on the Earth): `new Site(id % 1000,
its SPICE name, new CelestialBody(id / 1000))`, whose constructor reads the site's state at J2000 TDB, so its SPK must
cover that epoch. An identifier of that form whose body SPICE does not name, NOTO (398989) and NEW NORCIA (398990), is an
object of the kernels read by its identifier, named as SPICE names it, so the read is `spkezr_c`'s from that identifier:
the state when its SPK is loaded, SPICE's missing-data error (`InvalidOperationException`) otherwise
([#750](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/750): before 11.0.0 it was built as a
site of the body 398 and threw `ArgumentException`, "The NAIF identifier 398 has no name in SPICE"); so is a frame
centre of that form on the reference-body path, and the commands of the CLI build the same object (`ephemeris`,
`sub-point`). Any other identifier is built by `CelestialItem.Create`, a barycenter, a Lagrange point or a celestial body (#672: a
site observer was built as a celestial body, which threw or was built from undefined bytes). The state itself is
`spkezr_c`'s, which needs only the identifier. The commands of the CLI build their objects by identifier with the same
rule and the same site (since 11.0.0; before, any identifier from 100001 to 999998 was a site named `site<id>`), so a
site the loaded kernels do not name fails in both with "has no name in SPICE": load a kernel that names it, as the frame
kernel of `Site.WriteFrame` does, with its SPK. The two conditions, measured on a site from coordinates whose SPK
`Site.WriteEphemeris` wrote over two days of 2024 (fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)): loaded alone, a read from the
site throws `ArgumentException` (`naifId`, "The NAIF identifier 399786 has no name in SPICE"); with the naming frame
kernel loaded too, `InvalidOperationException` with SPICE's message ("Insufficient ephemeris data ... at the ephemeris
epoch 2000 JAN 01 12:00:00.000"), the `Site` constructor reading the site at J2000 TDB. The read with the site as target
(`ReadEphemeris(epoch, 399, 399786, ...)`) needs neither: with `Aberration.None`, read the site as target from the
observer and negate the state (with LT, CN or +S the negation is not the state `spkezr` gives).

SPICE takes a kernel path of at most 255 characters (NAIF, SPICE Kernel Required Reading, "Recommendations on Kernel
File Naming"). `LoadKernels` hands it the full path, so a longer one fails with `SPICE(FILENAMETOOLONG)`; the native
writers `WriteEphemeris` and `WriteOrientation` throw `ArgumentException` for such a target before they write, where
CSPICE wrote a file under the name truncated to 255 and then failed before 11.0.0. The length counted is that of the
bytes handed to CSPICE, UTF-8 on Linux and macOS. `Spacecraft.WriteEphemeris` (the managed SPK writer) and the text
kernel writers write any path.

Each native load and unload, and the native writers, first close and unload the SPKs, CKs, binary PCKs and EKs whose
file, named by an absolute path, was deleted since its load. A name SPICE holds relative to the working directory, from
a meta-kernel's `KERNELS_TO_LOAD` or a direct C++ caller of `LoadKernelsProxy`, is left to SPICE whether its file was
deleted or not: after a change of working directory the name no longer tells, and KEEPER's own reopening of such a
kernel then fails with `SPICE(FILENOTFOUND)` once the working directory changed or the file was deleted. `LoadKernels`
always passes full paths. The native writers unload the file they wrote, which opens the other loaded SPKs or CKs
again: when that fails on such a kernel, `WriteEphemeris` and `WriteOrientation` throw `InvalidOperationException`
with SPICE's message and leave no error pending (fourth round of #668; before, they returned and the next kernel call
failed with a wrong error). A new file is written by then; a file the call replaces is unloaded before its removal, an
unload that fails the same way, and is then left on disk as it was, no longer loaded, nothing written (before, it was
removed and nothing written in its place). See [Kernel Management](../guides/kernel-management.md).

No C++ exception of the native writers or loaders crosses P/Invoke any more (#706): before 11.0.0 each ended the process
(`std::terminate`, exit code 134). `WriteEphemeris` (and so `Star.WriteEphemeris` and `Site.WriteEphemeris`) throws
`ArgumentException` naming `stateVectors`, before anything is written, for what the native writer rejects or cannot read:
fewer than three states (its own rule, whose message said "2 items or more"), states not all in the frame of the first
one (compared by name, ordinal), or a centre of motion that is not a celestial body (`IsCelestialBodyId`: a barycenter, the
Sun, a planet, a moon, a Lagrange point or an identifier above 1000; not a spacecraft nor a site), whose GM the native layer
read from a failed read, or that SPICE does not name, by its built-in names or the loaded kernels (a body built from custom
parameters of an identifier no kernel names; #714, where the native writer threw `InvalidOperationException`), an existing
file then kept byte for byte. A centre SPICE names whose GM the kernel pool lacks (ELARA, 507) reaches the native writer,
which throws `InvalidOperationException` "CelestialItem id507 can't be read: The variable BODY507_GM could not be found in
the kernel pool.", nothing written. A site's 1-minute step gives three states over two minutes, so `Site.WriteEphemeris` over a
shorter window throws. A path the native file system calls reject, a loop of symbolic links, makes the writers and
`LoadKernels` throw `InvalidOperationException` with the file system's message (POSIX `ELOOP`, "Too many levels of
symbolic links"). `LoadKernels` and `UnloadKernels` give SPICE's message of a failed native call in their
`InvalidOperationException` (#706; before, "You can have more details on standard output").

A writer that replaces a file `LoadKernels` loaded, by itself or in a loaded directory, first unloads it as
`UnloadKernels` does (#706): `GetLoadedKernels` no longer lists it, a directory being replaced by its remaining files, and
the new file is left unloaded, as the writers leave every file they write: load it again to read it. Before 11.0.0 the
native writers (`WriteEphemeris`, `WriteOrientation`, `Star.WriteEphemeris`, `Site.WriteEphemeris`,
`Spacecraft.WriteOrientation`) unloaded the file once after loading it once more, so the caller's load stayed in SPICE,
which held the replaced file, deleted but open, and read its data until the file was unloaded (on Windows, from the code,
not measured, the removal of the open file ended the process); `Spacecraft.WriteEphemeris` unloaded it in SPICE but left it listed. Every
writer now also unloads every other load of the file before the removal (a meta-kernel's, a direct C++ caller's, two
meta-kernels' included; `Spacecraft.WriteEphemeris` made one unload, which left the second load holding the replaced
data), whether its name is the writer's path or an absolute name whose symbolic links resolve through it, followed one
at a time: a symbolic link to the file, or the file in a directory loaded through a link, unloaded and no longer listed
(a directory replaced by its remaining files). Kept, as they still name what they named: a hard link to the file (the
replaced data stay on disk under it) and the file a symbolic link given to the writer names (the writer replaces the
link). Not found, so still loaded, and the replaced data still read: a relative name other than the writer's path (a
meta-kernel's `KERNELS_TO_LOAD` without an absolute path; relative to the working directory of its load) and a name that
differs only by the case of its letters on a file system that ignores it. A failed unload
of the file (the reopening of the other SPKs or CKs, on a deleted one SPICE holds under a relative name) throws
`InvalidOperationException` with SPICE's message and keeps the file on disk as it was, no longer loaded, nothing written:
`Spacecraft.WriteEphemeris` ignored that failure and replaced the file before #706.

The text kernel writers (`Clock.Write`, `SpacecraftFrame.Write`, `Instrument.WriteFrame`, `Instrument.WriteKernel`,
`Site.WriteFrame`, `SiteFrame.Write`) follow the same rule since decision 103 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) (decided by S. Guillet,
2026-11-25): they rewrote a loaded file under SPICE's load, which a later `LoadKernels` skipped as a file already
loaded, so SPICE read the replaced content (a frame kernel rewritten for another spacecraft still named the first).
`Spacecraft.WriteKernels` and `CosmographiaExporter.ExportAsync` replace nothing SPICE holds: they refuse, with
`InvalidOperationException` before anything is written or deleted, a file or a directory SPICE holds, from
`LoadKernels`, a live `SpacecraftKernels` handle or a loaded meta-kernel, whose files the registry of `SpiceApi` does not
list: a native query reads SPICE's own list by the rule above, unloading nothing (decisions 80, 102 and 103 of #668), so
a meta-kernel's relative name other than the path is not found there either.

`TransformFrame` composes a TEME end through ICRF (#451), and so the frame of a site built from coordinates or of a site
from the kernels whose frame id no loaded kernel defines, the frame only the library holds, whose orientation is
`SiteFrame.GetStateOrientationToIcrf`, bit for bit `ToFrame`'s (fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668); before, its name went to SPICE,
which threw): within 9.3e-16 rad and 5.7e-20 rad/s of `sxform_c` in the frame kernel `Site.WriteFrame` writes for
the site (the same on Linux x64, Windows x64 and macOS arm64, CI run 43773937161). The frame of a site whose frame the kernels define and the frame of a spacecraft are
SPICE's by name. A CIRS or TIRS end, `Frame.CIRS`, `Frame.TIRS`, any `CirsFrame` or `TirsFrame` or a frame of those names
in any case, is composed through ICRF by its managed orientation, as TEME's
([#656](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/656); before, it threw).
A transformation SPICE cannot give between two frames it knows throws `SpiceException` (an `InvalidOperationException`)
"SPICE gives no transformation from {from} to {to} at {epoch}: " followed by SPICE's message, such as
`SPICE(FRAMEDATANOTFOUND)`'s "PCK data required to compute the orientation of the body-fixed frame ITRF93 for epoch 2199
AUG 22 04:00:00.000 TDB were not found. ..." past the coverage of the frame data
([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714); before, the native layer dropped
SPICE's message and the call named the coverage whatever the cause). A frame SPICE does not know throws
`ArgumentException` naming `fromFrame` or `toFrame`, also when the other end is TEME, CIRS, TIRS or a site frame only the
library holds, that `SpiceException`, with `SPICE(UNKNOWNFRAME)`'s "The frame NOT_A_FRAME was not recognized as a known
reference frame.", its inner exception (R25 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668); see
[Frames](frames.md#names-spice-does-not-know)). A native caller of
`TransformFrameProxy` gets a NaN rotation and angular velocity with SPICE's message in `GetLastErrorProxy()` (the zero
quaternion with no message before).

SPICE has no GCRF frame. `ReadEphemeris`, `ReadOrientation`, `TransformFrame` and `FindWindowsOnCoordinateConstraint`
hand a frame named GCRF (`Frame.GCRF` or `new Frame("GCRF")`, in any case) to SPICE as `J2000`, the axes of GCRF, and
label what SPICE returns with the frame given (#543).

### Units of the window searches

The window searches take the units of the library, except the coordinate search:

- `FindWindowsOnDistanceConstraint`: the distance in m, as `FindDistanceWindows` takes it (the native layer hands
  km to `gfdist_c`). Before 11.0.0 the kernel-spacecraft branch of `CelestialItem` and `Site` divided it by 1000 once
  more ([#466](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/466)).
- `FindWindowsOnCoordinateConstraint`: the value as `gfposc_c` takes it, km for a distance coordinate (X, Y, Z, range,
  radius, altitude) and rad for an angle; `FindCoordinateWindows` converts its metres.
- `FindWindowsOnIlluminationConstraint`: the surface point as `Planetodetic`, longitude and latitude in rad and the
  altitude in m above the ellipsoid of the body (equatorial radius and flattening of the kernels); before 11.0.0 the
  altitude was read in km ([#488](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/488)). So
  does the CLI command `find-windows-from-illumination-constraint`. The method is ELLIPSOID, the only one `gfilum_c`
  supports, whatever its case and its leading or trailing whitespace; any other, DSK forms included, throws
  `ArgumentException`, where before 11.0.0 `gfilum_c`'s error was dropped and no window returned
  ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)).

### Errors of the window searches

A search CSPICE cannot run throws `InvalidOperationException` with SPICE's message: a frame name SPICE does not know, a
body or an epoch without ephemeris data, an epoch outside the coverage of the frame data or of the C-kernel, an
instrument whose kernel is not loaded (`INS<id>_FOV_FRAME` not found), a step SPICE rejects, a result cell that
overflows. So do the CSPICE paths of the searches on an observer (`WindowSearches`, `Site`, `Instrument`), `Site.FindDayWindows`
and the launch windows of a kernel site, and the CLI commands `find-windows-from-*-constraint` end with it (exit code 1). Before the fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) the native proxies printed the
error on standard output, dropped it and returned no window, which a caller could not tell from a condition that never
holds. The checks made before the native call keep their `ArgumentException`: a frame of the library SPICE does not
know in `FindWindowsOnCoordinateConstraint`, an occultation body without a frame, an illumination method other than
ELLIPSOID. A search SPICE runs returns every window it finds, up to the 10000 of its result cell; more throw
`InvalidOperationException` with the long message of CSPICE's `SPICE(WINDOWEXCESS)`, "Window has size, 20000,
cardinality 20000. Cannot insert an additional interval into the window." (the size counts the endpoints; the short code
is not in the message): before 11.0.0 the native layer copied the first 1000 and dropped the rest without an error, and
the cell of `gfilum_c` held 1000 ([#653](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/653)).

### Orientation kernels

`ReadOrientation` and `WriteOrientation` are the C-matrix pair of CSPICE's `ckgpav_c` and `ckw03_c`. Each record is
the rotation from its `ReferenceFrame` (the reference frame of the CK, the source of the rotation) to the spacecraft
frame, `vectorInSpacecraft = vectorInReference.Rotate(Rotation)`, with the angular velocity of the spacecraft relative
to the reference frame, expressed in the reference frame: the convention of `Frame.ToFrame` for that rotation, and of
`Spacecraft.GetOrientation`. On the external CK of the tests (PHSRM) it equals `pxform` from J2000 to its bus frame to 0 rad.

A CK segment without angular velocity (a type-1 CK, or a type-2 or type-3 CK written without rates) is read by `ckgp_c`,
which `ckgpav_c` skips with no error ("Segments containing only pointing data will be skipped over", `ckgpav_c` Restrictions
1): its records have the pointing `ckgp_c` gives and a zero angular velocity
([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714); before, the read threw "No
orientation found"). The CKs the library writes carry their rates.

Where several segments cover an epoch, each record is that of the segment `ckgp_c` reads: the one of highest priority
(the CK loaded last, and in a file its last segment) that gives a pointing within the tolerance. Its angular velocity is
the one `ckgpav_c` gives when that segment holds angular velocity (`ckgpav_c` then finds the same segment: its
candidates are `ckgp_c`'s that hold rates, in the same order), and zero when it holds none; the pointing or the rate of
a lower-priority segment is never returned (decided by S. Guillet, 2026-11-18). Before
[#724](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/724), `ckgpav_c` was read first, so
that a segment without rates loaded over one with rates gave the record of the one with rates: 1.54 rad from `ckgp_c`'s
pointing on two CKs as those of `tools/ck_priority_reference` (C++ `API.ReadOrientationProxyReadsAHigherPrioritySegmentWithoutAngularVelocity`),
and, at an epoch within the tolerance of the higher-priority segment, a record 0.5 s from the one `ckgp_c` takes. A CK holding rates
everywhere reads as before, bit for bit.

The priority is that of a search made in a fresh process, whatever the calls before the read. CSPICE keeps, for each
instrument, the segment of its last search and the interval over which that segment has the highest priority (the re-use
interval of `ckbsr.c`), and a search inside that interval takes the segment first. A search with angular velocity
(`ckgpav_c`, or `sxform_c` through `ckfxfm`, as `TransformFrame` to the spacecraft's CK frame) skips a segment without
rates that holds the epoch and does not shrink the interval by it, so that `ckgp_c` itself, called after it, can read
the lower-priority segment with rates. The read first voids that interval (a search at an encoded clock of -dpmax, before
every segment, lies outside it), then searches from the highest priority. In the first implementation of #724 it did
not: after a `TransformFrame` or a `ckgpav_c` at 500 s on the kernels of `tools/ck_priority_reference`, or after the
epochs of the same call in the gap of a CK without rates whose interpolation intervals are [300, 400] s and
[600, 700] s (`ckprio_gap.bc`), a read took the record of the CK with rates, 1.43 to 1.54 rad from `ckgp_c` in a fresh
process (C++ `API.ReadOrientationProxyFollowsTheSegmentPriorityAfter*`, `SpiceApiTest.ReadOrientation_FollowsTheCkSegmentPriority_After*`).
`TransformFrame` and CSPICE's own routines keep CSPICE's re-use interval.

`WriteOrientation` writes its records unchanged, in one interval of one segment tagged with the frame of the first
record: every record carries the same frame (another frame throws `ArgumentException` before the native call, which
ended the process before 11.0.0, #560), in increasing order of their encoded SCLK, and the spacecraft clock kernel
is loaded. To write the attitude of a propagated spacecraft, use `Spacecraft.WriteOrientation`, which writes its
attitude record in this convention, in J2000 ([Attitude and kernels](spacecraft.md#attitude-and-kernels)).

### Two-body calls and element conversions

The ten raw conversions of `SpiceApi` that twinned the object API left the public API in 11.0.0, outright, with no
forwarder (R04 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), section 7 of the
re-baseline of #671, decided by S. Guillet, 2026-11-19); the library keeps them internal, the SPICE references of its
tests. Use the object API, whose results are the library's own computation, not CSPICE's: they agree with `prop2b_c`,
`oscltx_c`, `conics_c`, `eqncpv_c` and `evsgp4_c` to the tolerances of the tests, not bit for bit, and a two-body motion
about the Earth counts the TT seconds between its instants (#508), where `Propagate2Bodies` counted the difference of
the labels.

| Removed | Object API |
|---------|------------|
| internal `ConvertTleToStateVector(name, line1, line2, epoch)` | `new Tle(name, line1, line2).ToStateVector(epoch)` (SGP4 in `Frame.TEME`, the same `evsgp4_c`) |
| internal `ConvertStateVectorToConicOrbitalElement(stateVector)` | `stateVector.ToKeplerianElements()` |
| internal `ConvertStateVectorToConicOrbitalElement(stateVector, gm)` | `stateVector.ToKeplerianElements()` about an observer of that GM, a `CelestialBody` built from custom parameters |
| internal `ConvertEquinoctialElementsToStateVector(elements)` | `elements.ToStateVector()` |
| internal `ConvertConicElementsToStateVector(elements)` | `elements.ToStateVector()` |
| internal `ConvertConicElementsToStateVector(elements, epoch)` | `elements.ToStateVector(epoch)` |
| internal `ConvertConicElementsToStateVector(elements, epoch, gm)` | `elements.ToStateVector(epoch)` about an observer of that GM |
| internal `Propagate2Bodies(stateVector, dt)` | `stateVector.AtEpoch(stateVector.Epoch + dt).ToStateVector()` |
| internal `Propagate2Bodies(stateVector, gm, dt)` | the same about an observer of that GM |
| internal `Propagate2Bodies(stateVector, targetEpoch)` | `stateVector.AtEpoch(targetEpoch).ToStateVector()` |

Each proxy that reports through `GetLastErrorProxy()` clears it when it starts (#714), so the message read after a call is
that call's: before, it held the message of the last failure, about another call, until another failure replaced it.
`SpiceApi` reads it before any other native call: `ReadEphemeris` over a window checks every state before it rotates any
into a managed frame (TEME, CIRS, TIRS, the frame of a site from coordinates), since the rotation calls the native layer. A
SPICE error with no long message is reported by its short one (`SPICE(ZEROVELOCITY)`), where the proxies that report through
the shared routine reported such an error as a success.

### Body names

`SpiceApi.Instance.TryGetNaifId(name, out naifId)` gives the NAIF identifier SPICE gives to a body name (`bodn2c_c`):
its built-in names (the planets, their moons and barycentres, the Sun, the solar system barycentre, the stations of the
DSN, many spacecraft) and those the loaded kernels assign (`NAIF_BODY_NAME`, `NAIF_BODY_CODE`), in any case, the
blanks compressed (choice 12 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
decided by S. Guillet, 2026-11-19). It returns false for a name SPICE does not know, a blank name and the text of an
identifier (`"399"`, which is not a name), throws `ArgumentNullException` for null, and reads the kernels loaded at
the time of the call, under the lock. The command-line tool names its objects through it. The CCSDS readers keep their
own kernel-free table of centre names.

### Example

```csharp
SpiceApi.Instance.LoadKernels(new DirectoryInfo("kernels/"));

foreach (var kernel in SpiceApi.Instance.GetLoadedKernels())
    Console.WriteLine(kernel);
```

## Configuration

Use `Configuration.Instance.SetDataProvider(...)` to swap between SPICE-backed and memory-backed data access, and
`Configuration.Instance.DataProvider` (public and read only since 11.0.0) to read the provider set.

## Data Providers

| Provider | Description |
|----------|-------------|
| `SpiceDataProvider` | Default provider using SPICE kernels for production use |
| `MemoryDataProvider` | In-memory provider for testing without kernel files |

### The provider seam

A provider implements `IDataProvider`, three members in domain types:

| Member | Returns |
|--------|---------|
| `FrameTransformationToIcrf(in Time epoch, Frame source)` | The orientation of the frame relative to ICRF, a `StateOrientation` labelled with the frame |
| `GetEphemeris(in Time epoch, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)` | The state of the target from the observer, a `StateVector` (11.0.0: `OrbitalParameters` before) |
| `GetCelestialBodyInfo(int naifId)` | The information of a celestial body, a `CelestialBodyInfo` (11.0.0: the interop struct IO.Astrodynamics.DTO.CelestialBody, now internal, before), never null |

`CelestialBodyInfo` is a sealed record of SI values: `NaifId`, `Name`, `CenterOfMotionId`, `BarycenterOfMotionId`,
`Radii` (a `Vector3`, m), `GM` (m³/s²), `FrameName`, and the optional `FrameId` (0 when not given) and `J2`, `J3`, `J4`
(NaN when not given, as SPICE gives them for a body without them); two records are equal when every value is. The record
`SpiceApi.GetCelestialBodyInfo` reads is the native one field for field, bit for bit. The interop structs of the native
layer, `IO.Astrodynamics.DTO`, are internal since 11.0.0 (R04 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)): eleven of them bore the names of
domain types (`CelestialBody`, `StateVector`, `Quaternion`, ...), so that a `using IO.Astrodynamics.DTO` made them
ambiguous.

A provider follows one exception contract, on which the library's handlers of missing data rely: an unknown name (a
frame) or identifier (a body) throws `ArgumentException` naming the parameter; a known item without data at the date
throws `InvalidOperationException` (`SpiceException` on SPICE). It may run under the SPICE lock (an ephemeris read in
TEME, CIRS or TIRS), so it must not read `PlanetsAndMoons`, `Stars` or `Barycenters` there (#623).

### Which provider an object reads

`Configuration.Instance.DataProvider` is the provider of the process, which `SetDataProvider` replaces. The objects
read it by three rules (structuring choice 1 of #668, decided by S. Guillet, 2026-11-19); there is no scoped provider in
11.0.0.

| Object | Provider it reads |
|--------|-------------------|
| An object the caller builds: `new CelestialBody(...)`, `new Site(...)`, `new Frame(name)`, a spacecraft | The provider set when it is built, for its life |
| A static body: `PlanetsAndMoons.EARTH_BODY`, `MOON_BODY`, `Stars.SUN_BODY`, the `Barycenters` | The provider of its first read that succeeds, for its life (see below) |
| A static frame: `Frame.ICRF`, `Frame.ECLIPJ2000`, `Frame.ITRF93` and the other SPICE frames of `Frame` | The provider of the process at each transform (11.0.0; before, the provider set when the type `Frame` was first used, for the life of the process) |

### MemoryDataProvider

| Method | Description |
|--------|-------------|
| `AddCelestialBodyInfo(params CelestialBodyInfo[] celestialBodyInfos)` | Add the information of bodies, each under its `NaifId` (`ArgumentNullException` for a null array or record); a `CelestialBodyInfo` since 11.0.0, the interop struct IO.Astrodynamics.DTO.CelestialBody, now internal, before |
| `AddStateVector(int naifId, Time epoch, StateVector sv)` | Add state vector |
| `AddStateOrientationToIcrf(Frame frame, Time epoch, StateOrientation orientation)` | Add orientation; `ArgumentException` (`frame`) for ICRF, GCRF, TEME, CIRS and TIRS, which the provider defines (#744) |

A read at an added epoch returns that entry. A read between entries is interpolated and never stored, so it depends on
the entries only, not on the reads before it (#548): an orientation is the exact spherical linear interpolation of the two
entries around it (`StateOrientation.Interpolate`), a state the 8-point Lagrange interpolation of the nearest added
states. Missing data throw `InvalidOperationException`, which the library's handlers of missing data catch (#680): a
state read for a target without entries or outside them and an orientation read outside a frame's entries, the type
SPICE throws for the same reads. A bad argument throws `ArgumentException` (#680, decided by S. Guillet, 2026-11-14): a
frame neither added nor known (below), from `GetEphemeris` too since #559, and the information of an identifier never
added, whose `ParamName` is `naifId`, the type and the
parameter `SpiceDataProvider.GetCelestialBodyInfo` throws for an identifier SPICE does not name or that is not a
celestial body's (#672); `new CelestialBody(id)` and `CelestialItem.Create(id)` of that identifier throw it too, on both
providers, except `CelestialItem.Create` of a Lagrange point (391 to 395), which reads the point's state and not its
information (above): a provider without that state throws `InvalidOperationException`, as SPICE for L3, whose state
`Data/SolarSystem` lacks. A body whose barycentre's data, information included, the provider lacks builds with
`InitialOrbitalParameters` null and an infinite sphere of influence, as a body the kernels do not chain to its
barycentre (SPICE names every barycentre), and the orientation cache of a propagation keeps the points the entries do not
reach unavailable. Before 11.0.0 the state and orientation reads threw `ArgumentException` and the information of an
identifier never added `KeyNotFoundException`.

`GetEphemeris` takes the stored states about the solar system barycentre in ICRF (a state stored in another frame is
rotated by the provider's orientation of it), subtracts them and rotates the difference into the frame asked by the
provider's orientation of that frame, r' = R r and v' = R v + ω × r' as `ToFrame`; a read in ICRF or GCRF is the
stored difference, unrotated. Besides the frames whose orientations were added, the provider knows ICRF and GCRF (the
identity), ECLIPJ2000 (SPICE's rotation of the J2000 obliquity, 84381.448″ about x; the default frame of the bodies)
and the managed TEME, CIRS and TIRS, in any case; another frame throws `ArgumentException`. No orientation can be added
under ICRF, GCRF, TEME, CIRS or TIRS, the frames it defines and never reads from stored entries: `AddStateOrientationToIcrf`
throws `ArgumentException` naming `frame` for them, by name in any case, and stores nothing (#744, decided by S. Guillet,
2026-11-18; before, an orientation stored under ICRF or GCRF was returned by `FrameTransformationToIcrf` while
`GetEphemeris` took the identity, and one stored under TEME, CIRS or TIRS was never read). It holds geometric states
only: an aberration other than `Aberration.None` throws `NotSupportedException` (#559, decided by S. Guillet,
2026-11-17). Before 11.0.0 the ICRF difference came back labelled with the frame asked, and an aberrated read was
geometric.

The static objects of `Barycenters`, `PlanetsAndMoons` (`EARTH_BODY`, `MOON_BODY`) and `Stars` (`SUN_BODY`) read the
provider at the first read of a member that needs its data (see [Predefined Objects](predefined-objects.md)), so a
provider set before they are first read, and lacking their data, does not leave them unusable: each read throws the
provider's exception (`ArgumentException` naming `naifId` for missing information) until the data are there, and each
read goes to the provider set at its time, so that the data may be added to that provider or another provider set in
its place (a `SpiceDataProvider` and `LoadKernels`, for instance). Once read, an object keeps the provider it read its
data from, for its data and its ephemeris. A provider that gives one of them a GM or, for a body, radii of 0 (SPICE
before `LoadKernels`) makes the read throw `InvalidOperationException` naming the missing variable, and nothing is kept
(third round of #668). On the `SpiceDataProvider` their data hold for the kernel pool they were read from: the first read
after each `LoadKernels` or `ReloadKernels` that hands SPICE a file (a repeated load hands it nothing, 11.0.0), and after each `UnloadKernels` or `ClearKernels`, reads them again (data equal to those held keep the
instances held), so that a kernel loaded later, or unloaded, reaches them (third round of #668). Before
11.0.0 their type initializers read the provider, and one missing datum threw a `TypeInitializationException` that the
runtime keeps for the life of the process.

```csharp
var memoryProvider = new MemoryDataProvider();
memoryProvider.AddCelestialBodyInfo(new CelestialBodyInfo(NaifId: 399, Name: "EARTH", CenterOfMotionId: 10,
    BarycenterOfMotionId: 3, Radii: new Vector3(6378137.0, 6378137.0, 6356752.0), GM: 3.986004418e14,
    FrameName: "ITRF93", FrameId: 13000));
Configuration.Instance.SetDataProvider(memoryProvider);
```

## See Also

- [Kernel Management](../guides/kernel-management.md)
- [Time & Windows](time-and-windows.md)
