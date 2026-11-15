# SpiceAPI & Data Providers

## SpiceAPI

`SpiceAPI` is the singleton entry point for SPICE-backed operations.

| Method | Description |
|--------|-------------|
| `LoadKernels(FileSystemInfo path)` | Load a kernel file or a directory of kernels, the path taken as what it is on disk, `ArgumentException` for one that does not exist; paths compared by components, a path inside a loaded directory reloading it (#673) |
| `UnloadKernels(FileSystemInfo path)` | Unload the files a load handed SPICE, whether the path still exists or not; after a file or subdirectory of a loaded directory, its other files stay listed in its place (#673) |
| `ClearKernels()` | Unload all kernels (`kclear_c`) |
| `GetLoadedKernels()` | Inspect the loaded paths |
| `GetSpiceVersion()` | Read the underlying CSPICE version |
| `ReadEphemeris(...)` | Read state data (throws `InvalidOperationException` with SPICE's message when the kernels give no state, #454) |
| `ReadOrientation(...)` | Read orientation data (throws `InvalidOperationException` on native error) |
| `WriteOrientation(FileInfo, int, IEnumerable<StateOrientation>)` | Write orientation records as a type-3 CK (throws `ArgumentException` for records in more than one frame, #560, and for a path longer than 255 characters, #673) |
| `WriteEphemeris(FileInfo, int, IEnumerable<StateVector>)` | Write states as an SPK through the native writer, under `Star` and `Site` (throws `ArgumentException` for a path longer than 255 characters, #673) |
| `GetCelestialBodyInfo(int)` | Read the name, centre of motion, radii, GM and frame of a celestial body (throws `ArgumentException` for an identifier that is not one, #560, or that SPICE does not name, #672) |

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

A body the kernel pool gives no `BODYnnn_RADII` has radii 0, one it gives no `BODYnnn_GM` a GM of 0, and its frame is
still read (`cnmfrm_c`); a barycentre has no frame (an empty `FrameName`, `FrameId` 0). Before #672 a missing variable
left the GM 0 and the frame name undefined (Pallas, Nix, Hydra, Didymos and Dimorphos read GM 0; Elara a garbage frame).

The observer of `ReadEphemeris` by identifiers is a `Site` for a site identifier, its body's identifier times 1000 plus
its own (1 to 999), the body a planet or a moon (399013 is DSS-13 on the Earth): `new Site(id % 1000, its SPICE name,
new CelestialBody(id / 1000))`, whose constructor reads the site's state at J2000 TDB, so its SPK must cover that epoch.
Any other identifier is built by `CelestialItem.Create`, a barycenter, a Lagrange point or a celestial body (#672: a
site observer was built as a celestial body, which threw or was built from undefined bytes). The state itself is
`spkezr_c`'s, which needs only the identifier. The commands of the CLI build their objects by identifier with the same
rule and the same site (since 11.0.0; before, any identifier from 100001 to 999998 was a site named `site<id>`), so a
site the loaded kernels do not name fails in both with "has no name in SPICE": load a kernel that names it, as the frame
kernel of `Site.WriteFrameAsync` does, with its SPK. The two conditions, measured on a site from coordinates whose SPK
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

`TransformFrame` composes a TEME end through ICRF (#451), and so the frame of a site built from coordinates or of a site
from the kernels whose frame id no loaded kernel defines, the frame only the library holds, whose orientation is
`SiteFrame.GetStateOrientationToICRF`, bit for bit `ToFrame`'s (fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668); before, its name went to SPICE,
which threw): within 9.3e-16 rad and 5.7e-20 rad/s of `sxform_c` in the frame kernel `Site.WriteFrameAsync` writes for
the site (measured on Linux x64). The frame of a site whose frame the kernels define and the frame of a spacecraft are
SPICE's by name; CIRS and TIRS throw ([#656](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/656)).

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
  does the CLI command `find-windows-from-illumination-constraint`. The method is ELLIPSOID, the only one `gfilum_c`
  supports, whatever its case and its leading or trailing whitespace; any other, DSK forms included, throws
  `ArgumentException`, where before 11.0.0 `gfilum_c`'s error was dropped and no window returned
  ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)).

### Errors of the window searches

A search CSPICE cannot run throws `InvalidOperationException` with SPICE's message: a frame name SPICE does not know, a
body or an epoch without ephemeris data, an epoch outside the coverage of the frame data or of the C-kernel, an
instrument whose kernel is not loaded (`INS<id>_FOV_FRAME` not found), a step SPICE rejects, a result cell that
overflows. So do the CSPICE paths of the searches of `CelestialItem`, `Site` and `Instrument`, `Site.FindDayWindows`
and the launch windows of a kernel site, and the CLI commands `find-windows-from-*-constraint` end with it (exit code 1). Before the fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) the native proxies printed the
error on standard output, dropped it and returned no window, which a caller could not tell from a condition that never
holds. The checks made before the native call keep their `ArgumentException`: a frame of the library SPICE does not
know in `FindWindowsOnCoordinateConstraint`, an occultation body without a frame, an illumination method other than
ELLIPSOID. A search SPICE runs is unchanged, and returns at most 1000 windows
([#653](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/653)).

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
states. Missing data throw `InvalidOperationException`, which the library's handlers of missing data catch (#680): a
state read for a target without entries or outside them and an orientation read outside a frame's entries, the type
SPICE throws for the same reads. A bad argument throws `ArgumentException` (#680, decided by S. Guillet, 2026-11-14): a
frame never added, and the information of an identifier never added, whose `ParamName` is `naifId`, the type and the
parameter `SpiceDataProvider.GetCelestialBodyInfo` throws for an identifier SPICE does not name or that is not a
celestial body's (#672); `new CelestialBody(id)` and `CelestialItem.Create(id)` of that identifier throw it too, on both
providers, except `CelestialItem.Create` of a Lagrange point (391 to 395), which reads the point's state and not its
information (above): a provider without that state throws `InvalidOperationException`, as SPICE for L3, whose state
`Data/SolarSystem` lacks. A body whose barycentre's data, information included, the provider lacks builds with
`InitialOrbitalParameters` null and an infinite sphere of influence, as a body the kernels do not chain to its
barycentre (SPICE names every barycentre), and the orientation cache of a propagation keeps the points the entries do not
reach unavailable. Before 11.0.0 the state and orientation reads threw `ArgumentException` and the information of an
identifier never added `KeyNotFoundException`.

The static objects of `Barycenters`, `PlanetsAndMoons` (`EARTH_BODY`, `MOON_BODY`) and `Stars` (`SUN_BODY`) read the
provider at the first read of a member that needs its data (see [Predefined Objects](predefined-objects.md)), so a
provider set before they are first read, and lacking their data, does not leave them unusable: each read throws the
provider's exception (`ArgumentException` naming `naifId` for missing information) until the data are there, and each
read goes to the provider set at its time, so that the data may be added to that provider or another provider set in
its place (a `SpiceDataProvider` and `LoadKernels`, for instance). Once read, an object keeps the provider it read its
data from, for its data and its ephemeris. A provider that gives one of them a GM or, for a body, radii of 0 (SPICE
before `LoadKernels`) makes the read throw `InvalidOperationException` naming the missing variable, and nothing is kept
(third round of #668). On the `SpiceDataProvider` their data hold for the kernel pool they were read from: the first read
after each `LoadKernels`, `UnloadKernels` or `ClearKernels` reads them again (data equal to those held keep the
instances held), so that a kernel loaded later, or unloaded, reaches them (third round of #668). Before
11.0.0 their type initializers read the provider, and one missing datum threw a `TypeInitializationException` that the
runtime keeps for the life of the process.

```csharp
var memoryProvider = new MemoryDataProvider();
Configuration.Instance.SetDataProvider(memoryProvider);
memoryProvider.AddCelestialBodyInfo(customEarth);
```

## See Also

- [Kernel Management](../guides/kernel-management.md)
- [Time & Windows](time-and-windows.md)
