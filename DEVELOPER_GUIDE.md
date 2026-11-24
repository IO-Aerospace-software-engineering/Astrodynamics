# IO.Astrodynamics.Net Developer Guide

## Introduction

IO.Astrodynamics.Net is a .NET 10 astrodynamics framework for orbital mechanics calculations, ephemeris computations, and space mission planning. Built on NASA's CSPICE toolkit, it provides high-precision calculations for spacecraft trajectory analysis, celestial body ephemerides, and mission planning.

### What IO.Astrodynamics Does

- **Orbital Mechanics**: Compute orbital elements (Keplerian, equinoctial), state vectors, orbital periods, anomalies
- **Ephemeris Computation**: Read and write SPICE kernel files for celestial bodies and spacecraft
- **Time Scales**: TDB, TT, TAI, UTC and GPS (`TimeScale`), the scale a required argument, one string grammar (`Time.Parse`)
- **Reference Frames**: Transform between inertial and body-fixed frames (ICRF, ECLIPTIC, IAU frames)
- **Spacecraft Modeling**: Define spacecraft with instruments, engines, fuel tanks, payloads, and configurable body axes
- **Orbital Propagation**: Propagate orbits using numerical integration with perturbation models
- **Maneuver Planning**: Compute delta-V requirements, launch windows, Lambert transfers
- **Geometry Finding**: Search for occultations, eclipses, visibility windows, illumination conditions
- **Atmospheric Modeling**: Compute atmospheric density, temperature, pressure (Earth and Mars)
- **TLE Processing**: Parse, propagate (SGP4/SDP4), and generate Two-Line Element sets
- **Ground Site Operations**: Compute azimuth, elevation, range for tracking stations

### What IO.Astrodynamics Does NOT Do

- Real-time telemetry processing
- Hardware control or flight software
- Launch vehicle design or structural analysis
- Thermal analysis
- Power system modeling
- Communication link budgets (beyond basic geometry)
- Detailed attitude control system design (attitude determination/planning is supported; closed-loop control is not)

## Standards and Units

### Unit System

IO.Astrodynamics uses the **International System of Units (SI)** throughout:

| Quantity | Unit | Symbol |
|----------|------|--------|
| Length | Meters | m |
| Mass | Kilograms | kg |
| Time | Seconds | s |
| Angle | Radians | rad |
| Velocity | Meters per second | m/s |
| Acceleration | Meters per second squared | m/s² |
| Gravitational parameter (GM) | m³/s² | m³/s² |
| Temperature | Celsius | °C |
| Pressure | Kilopascals | kPa |
| Density | Kilograms per cubic meter | kg/m³ |

### Physical Constants

Defined in `IO.Astrodynamics.Constants`:

```csharp
public const double G = 6.67430e-11;           // Gravitational constant (m³/kg/s²)
public const double AU = 149597870700.0;       // Astronomical Unit (m)
public const double C = 299792458.0;           // Speed of light (m/s)
public const double g0 = 9.80665;              // Standard gravity (m/s²)
public const double Deg2Rad = Math.PI / 180.0; // Degrees to radians
public const double Rad2Deg = 180.0 / Math.PI; // Radians to degrees
```

### Reference Standards

- **Time**: managed conversions with no kernel read: the leap second table of the library (TAI − UTC from 10 s on 1972-01-01 to 37 s from 2017-01-01, a fixed 9 s before 1972), TT = TAI + 32.184 s, GPS = TAI − 19 s, and TDB − TT by the one-term series 0.001658 sin(g + 0.0167 sin g) s; CSPICE is given the managed TDB as its ephemeris time (see `docs/reference/time-and-windows.md#time-scales`)
- **Ephemerides**: JPL Development Ephemeris (DE series)
- **Reference Frames**: IAU/IAG standards, IERS conventions
- **Gravity Models**: EGM2008 (Earth Gravitational Model 2008), geodesy-normalized spherical harmonics up to degree/order 70
- **Atmospheric Models**: U.S. Standard Atmosphere 1976, NRLMSISE-00
- **TLE Propagation**: SGP4/SDP4 (AFSPC compatibility)

### NAIF ID Codes

Celestial bodies use NAIF (Navigation and Ancillary Information Facility) ID codes:
- Sun: 10
- Mercury: 199, Venus: 299, Earth: 399, Mars: 499, etc.
- Moon: 301
- Barycenters: 0 (Solar System), 3 (Earth-Moon), etc.

---

## Quick Start Guide

### Installation

Add the IO.Astrodynamics NuGet package to your project:

```bash
dotnet add package IO.Astrodynamics
```

The package targets `net10.0` and bundles the native CSPICE interop library for `win-x64`,
`linux-x64`, `osx-arm64` (Apple Silicon) and `osx-x64` (Intel). The right one is resolved
automatically at build time.

### Loading SPICE Kernels

Before any computation, load the required SPICE kernel files:

```csharp
using IO.Astrodynamics;

// Load kernels from a directory
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("path/to/kernels"));

// Or load a specific kernel file
SpiceAPI.Instance.LoadKernels(new FileInfo("de440.bsp"));
```

### Basic Example: Compute Earth Position

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

// Load kernels
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

// Create celestial bodies
var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);

// Define epoch
var epoch = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);

// Get Earth's state vector relative to the Sun
var stateVector = earth.GetEphemeris(epoch, sun, Frame.ICRF, Aberration.None);

Console.WriteLine($"Position: {stateVector.Position}");
Console.WriteLine($"Velocity: {stateVector.Velocity}");
```

### Basic Example: Create an Orbit

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, 0, 0, 0, TimeScale.TDB);

// Create from Keplerian elements
var orbit = new KeplerianElements(
    7000000.0,                           // Semi-major axis (m)
    0.001,                               // Eccentricity
    51.6 * Constants.Deg2Rad,            // Inclination (rad)
    100.0 * Constants.Deg2Rad,           // RAAN (rad)
    90.0 * Constants.Deg2Rad,            // Argument of periapsis (rad)
    0.0,                                 // Mean anomaly (rad)
    earth,                               // Observer: the center of motion
    epoch,
    Frame.ICRF
);

// Convert to state vector
var sv = orbit.ToStateVector();
Console.WriteLine($"Orbital Period: {orbit.Period().TotalHours:F2} hours");
```

### Basic Example: Parse a TLE

```csharp
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;

var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

// Propagate to a specific time
var epoch = new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB);
var sv = tle.ToStateVector(epoch);

Console.WriteLine($"ISS Position: {sv.Position}");
```

---

## API Reference

### IO.Astrodynamics (Core)

#### SpiceAPI

Singleton class providing access to SPICE operations.

| Method | Description |
|--------|-------------|
| `LoadKernels(FileSystemInfo path)`, `LoadKernels(string path)` | Load SPICE kernel(s) from file or directory, the path taken as what it is on disk; a file SPICE already holds from a load is not handed to it again, so a repeated load furnishes only the files added since (R01 of #668); `ArgumentException` for a path that does not exist |
| `ReloadKernels(FileSystemInfo path)`, `ReloadKernels(string path)` | Read the kernels of a path again, on top of the kernels loaded before: for a kernel rewritten on disk, or to give a path precedence (what a repeated `LoadKernels` did before 11.0.0) |
| `UnloadKernels(FileSystemInfo path)`, `UnloadKernels(string path)` | Unload the files a load handed SPICE, even after the path was deleted; a file of a loaded directory leaves its other files listed (#673) |
| `ClearKernels()` | Unload all kernels |
| `GetLoadedKernels()` | Get list of currently loaded paths; a deleted SPK, CK, binary PCK or EK leaves it at the next native load, unload or write (#668), a file a writer replaces before the write (#706) |
| `GetSpiceVersion()` | Get SPICE toolkit version |
| `ReadEphemeris(...)` | Read state vectors: `SpiceException` (an `InvalidOperationException`) with SPICE's message when SPICE gives no state, `ArgumentException` (`frame`) for a frame SPICE does not know (R25 of #668) |
| `ReadOrientation(...)` | Read spacecraft orientations: `SpiceException` with SPICE's message when SPICE gives no orientation, `ArgumentException` (`referenceFrame`) for a frame SPICE does not know (R25 of #668) |
| `TransformFrame(epoch, fromFrame, toFrame)` | The orientation between two frames: `SpiceException` outside the coverage of a known frame, `ArgumentException` (`fromFrame` or `toFrame`) for a frame SPICE does not know (R25 of #668) |
| `WriteEphemeris(...)`, `WriteOrientation(...)` | Write an SPK (three states or more, one frame, about a celestial body, else `ArgumentException`) or a CK through the native writer; a file they replace that `LoadKernels` loaded, under its path or a symbolic link that resolves through it, is unloaded first, with every other load of it, and the new file is left unloaded (#706) |

```csharp
// Load all kernels from a directory
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels/"));

// Check loaded kernels
foreach (var kernel in SpiceAPI.Instance.GetLoadedKernels())
    Console.WriteLine(kernel);
```

#### Configuration

Configure framework-wide settings.

| Method | Description |
|--------|-------------|
| `SetDataProvider(IDataProvider provider)` | Set the data provider (SPICE or Memory) |
| `DataProvider` | The data provider set (read only, 11.0.0) |

```csharp
// Use memory data provider for testing
Configuration.Instance.SetDataProvider(new MemoryDataProvider());
```

---

### IO.Astrodynamics.TimeSystem

#### Time

Represents a precise instant: a calendar in a time scale (`TimeScale`: TDB, TT, TAI, UTC, GPS), the scale a required
argument (D3 of #668).

| Constructor / Method | Description |
|---------------------|-------------|
| `Time(int year, int month, int day, TimeScale scale)` | Create from a date in a scale |
| `Time(int year, int month, int day, int hour, int minute, int second, TimeScale scale)` | Create from a date and time in a scale |
| `Time(DateTime dateTime, TimeScale scale)` | Create from a `DateTime` in a scale; a `DateTime` of kind `Local`, or of kind `Utc` with another scale than UTC, throws `ArgumentException` |
| `Parse(string s)` | One grammar (`yyyy-MM-dd` or `yyyy-DDD`, an optional time, then `Z`, `±hh:mm` or a tag `UTC`, `TT`, `TDB`, `TAI`, `GPS`; or `JD n TAG`); an untagged string throws `FormatException` unless `Parse(s, scale)` names its scale, and a tag of another scale than `scale` throws; `TryParse` returns false instead and never throws |
| `FromDayOfYear(int year, int dayOfYear, double seconds, TimeScale scale)` | Create from a day of year and the seconds of that day |
| `Create(double secondsFromJ2000, TimeScale scale)` | Create from seconds since J2000 |
| `CreateTDB(double secondsFromJ2000)` | Create TDB time from J2000 seconds |
| `CreateUTC(double secondsFromJ2000)` | Create UTC time from J2000 seconds |
| `ToTDB()` | Convert to TDB |
| `ToUTC()` | Convert to UTC |
| `ToTAI()` | Convert to TAI |
| `ToTT()` | Convert to TT |
| `ToJulianDate()` | Get Julian Date |
| `TimeSpanFromJ2000()` | Get TimeSpan from J2000 epoch |
| `Add(TimeSpan span)` | Add duration |
| `AddDays(double days)` | Add days, to the nearest tick (#637); a non-finite argument throws `ArgumentOutOfRangeException` (#680) |
| `AddHours(double hours)` | Add hours, to the nearest tick (#637); a non-finite argument throws `ArgumentOutOfRangeException` (#680) |
| `AddSeconds(double seconds)` | Add seconds, to the nearest tick (#625) |
| `==`, `Equals`, `CompareTo` | The same instant, whatever the scales: equality follows `CompareTo`, by the ticks converted to TAI, and equal times hash alike (#512), so `utc == utc.ToTDB()`; the default time is 0001-01-01 TDB; `EqualsExact` (and `Time.ExactComparer`) tells two labels apart, the calendar and the scale (a test of a label: `LabelAssert`, not `Assert.Equal`) |
| `ToString()`, `ToString(string format, IFormatProvider provider)` | The round-trip form, then the tag (`Z`, ` TDB`, ` TT`, ` TAI`, ` GPS`), which `Parse` reads back; another format is a `DateTime` format followed by the tag |

```csharp
// Create times
var t1 = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
var t2 = Time.Parse("2024-06-21T12:00:00.000 UTC");
var t3 = Time.CreateTDB(788400000.0);  // Seconds from J2000

// Convert between time scales
var tdb = t1.ToTDB();
var utc = tdb.ToUTC();

// Arithmetic
var t4 = t1.AddDays(1.0);
var duration = t4 - t1;  // TimeSpan
```

#### Window

Represents a time interval.

| Constructor / Method | Description |
|---------------------|-------------|
| `Window(Time start, Time end)` | Create from start and end times |
| `Window(Time start, TimeSpan duration)` | Create from start time and duration |
| `StartDate` | Get start time |
| `EndDate` | Get end time |
| `Length` | Get window duration |
| `Merge(Window other)` | Merge two overlapping windows |
| `Intersects(Window other)` | Check if windows overlap |
| `Intersects(Time time)` | Check if time is within window |
| `GetIntersection(Window other)` | Get overlapping portion |
| `==`, `Equals` | The same two ends as instants, whatever their frames, `Length` excluded (#512) |

```csharp
var window = new Window(
    new Time(2024, 1, 1, TimeScale.TDB),
    new Time(2024, 1, 31, TimeScale.TDB)
);

Console.WriteLine($"Duration: {window.Length.TotalDays} days");

// Check intersection
var t = new Time(2024, 1, 15, TimeScale.TDB);
if (window.Intersects(t))
    Console.WriteLine("Time is within window");
```

---

### IO.Astrodynamics.Body

#### CelestialBody

Represents a natural celestial body (planet, moon, star).

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF ID code |
| `Name` | Body name |
| `GM` | Gravitational parameter (m³/s²) |
| `Mass` | Mass (kg) |
| `EquatorialRadius` | Equatorial radius (m) |
| `PolarRadius` | Polar radius (m) |
| `Flattening` | Flattening coefficient |
| `Frame` | Body-fixed reference frame |
| `SphereOfInfluence` | Sphere of influence radius (m) |
| `GravitationalField` | Gravity model (point-mass or geopotential) |
| `InitialOrbitalParameters` | Initial orbital state |

| Method | Description |
|--------|-------------|
| `GetEphemeris(Time epoch, ILocalizable observer, Frame frame, Aberration aberration)` | Get state at epoch |
| `GetEphemeris(Window window, ILocalizable observer, Frame frame, Aberration aberration, TimeSpan step)` | Get states over window: every step from the start, in the window's calendar, up to its end (across a leap second the end bounds them, #682) |
| `GetOrientation(Frame referenceFrame, Time epoch)` | Get body orientation |
| `SideralRotationPeriod(Time epoch)` | Get sidereal rotation period |
| `TrueSolarDay(Time epoch)` | Get true solar day duration |
| `GeosynchronousOrbit(double longitude, double latitude, Time epoch)` | Compute geosynchronous orbit |
| `HelioSynchronousOrbit(double sma, double ecc, Time epochAtDescendingNode)` | Compute helio-synchronous orbit |
| `FindWindowsOnDistanceConstraint(...)` | Find windows when distance constraint met |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation/eclipse windows |
| `AngularSeparation(Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)` | Compute angular separation |
| `SubObserverPoint(CelestialBody target, Time epoch, Aberration aberration)` | Get sub-observer point |

```csharp
// Use predefined bodies
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var sun = new CelestialBody(Stars.Sun);

// Get ephemeris
var state = earth.GetEphemeris(Time.J2000TDB, sun, Frame.ICRF, Aberration.None);
var sv = state.ToStateVector();

// Compute geosynchronous orbit
var geoOrbit = earth.GeosynchronousOrbit(0.0, 0.0, new Time(2024, 1, 1, TimeScale.TDB));

// Find solar eclipses: the Sun (this item) behind the Moon, seen from the Earth's center
var eclipses = sun.FindWindowsOnOccultationConstraint(
    new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 12, 31, TimeScale.TDB)),
    earth, ShapeType.Ellipsoid, moon, ShapeType.Ellipsoid,
    OccultationType.Any, Aberration.None, TimeSpan.FromHours(1));
```

#### CelestialItem

Base class for celestial objects (bodies, spacecraft).

| Method | Description |
|--------|-------------|
| `GetGeometricStateRelativeTo(Time epoch, CelestialItem referenceBody)` | Get geometric state relative to a reference body in ICRF (avoids SSB round-trips) |
| `AngularSize(double distance)` | Compute angular diameter at distance |
| `IsOcculted(CelestialItem by, OrbitalParameters from, Aberration aberration)` | Check if occulted |
| `WriteEphemeris(FileInfo outputFile)` | Write ephemeris to SPK file |

**Ephemeris Computation Strategy**: When computing ephemeris between a target and a non-SPICE observer (Spacecraft, Site), the framework uses a "reference body" pattern instead of going through SSB. The reference body is determined automatically (Spacecraft's central body, Site's celestial body, or SSB as fallback). This avoids the numerical issues and overhead of subtracting two ~150 Gm SSB-relative vectors to get a relative position between nearby objects. SSB-relative states are only used internally for aberration correction (light-time displacement of the reference body) and star positions.

---

### IO.Astrodynamics.OrbitalParameters

#### OrbitalElementsType (enum)

Discriminant for orbital element types.

| Value | Description |
|-------|-------------|
| `Osculating` | Instantaneous (true) orbital elements |
| `Mean` | Time-averaged elements (e.g., TLE mean elements) |

Equality of orbital parameters (`==`, `Equals`, `GetHashCode`, typed as `OrbitalParameters` or as their type) is value
equality within one concrete type, computing no state (#475): the type's values (the perigee radius of the parabolic
band |e − 1| < 1e-6 among them, #459), the observer's NAIF id, the epoch (an instant), the frame and `ElementsType`. A set and its own `ToStateVector()`, a mean and an osculating set of the same
numbers, and a TLE and its mean elements are different; to compare two orbits, compare their states.

All orbital parameter classes (`StateVector`, `KeplerianElements`, `EquinoctialElements`, `TLE`) have an `ElementsType` property indicating whether elements are osculating or mean. Most conversions preserve the elements type. **Important:** Mean elements cannot be directly converted to `StateVector` as they require a propagation model (e.g., SGP4 for TLE).

#### StateVector

Cartesian position and velocity state.

| Constructor | Description |
|------------|-------------|
| `StateVector(Vector3 position, Vector3 velocity, CelestialItem observer, Time epoch, Frame frame)` | Create state vector |

| Property | Description |
|----------|-------------|
| `Position` | Position vector (m) |
| `Velocity` | Velocity vector (m/s) |
| `Observer` | Central body |
| `Epoch` | Time of state |
| `Frame` | Reference frame |

| Method | Description |
|--------|-------------|
| `SemiMajorAxis()` | Compute semi-major axis (m): the conic's −μ/(2ξ), within 1e-6 of e = 1 too; +Infinity for e = 1 exactly (#459) |
| `Eccentricity()` | Compute eccentricity |
| `Inclination()` | Compute inclination (rad) |
| `AscendingNode()` | Compute RAAN (rad) |
| `ArgumentOfPeriapsis()` | Compute argument of periapsis (rad) |
| `TrueAnomaly()` | Compute true anomaly (rad) |
| `MeanAnomaly()` | Compute mean anomaly (rad) |
| `EccentricAnomaly()` | Compute eccentric anomaly (rad) |
| `Period()` | Compute orbital period of an ellipse; an open orbit throws `InvalidOperationException` (#663) |
| `MeanMotion()` | Compute mean motion (rad/s), √(μ/\|a\|³); Barker's for a parabola (#459) |
| `SpecificOrbitalEnergy()` | Compute vis-viva energy (m²/s²) |
| `SpecificAngularMomentum()` | Compute specific angular momentum vector |
| `EccentricityVector()` | Compute eccentricity vector |
| `AscendingNodeVector()` | Compute ascending node vector |
| `PerigeeVector()` | Compute perigee position vector, a(1 − e) P̂, P̂ the direction of the periapsis in the orbit plane, defined at e = 0 (#686); a state at the centre has none and throws (#687) |
| `ApogeeVector()` | Compute apogee position vector, −a(1 + e) P̂; an open orbit has none and throws `InvalidOperationException` (#737) |
| `PerigeeVelocity()` | Compute velocity at perigee (m/s) |
| `ApogeeVelocity()` | Compute velocity at apogee (m/s) |
| `ToKeplerianElements()` | Convert to Keplerian elements |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `ToEquatorial()` | Right ascension and declination in ICRF, whatever the frame of the state (rotated at its epoch, #680), and the distance |
| `ToFrame(Frame frame)` | Transform to different reference frame; typed as a `StateVector` on a `StateVector` (11.0.0) |
| `RelativeTo(ILocalizable target, Aberration aberration)` | Transform to different center; typed as a `StateVector` on a `StateVector` (11.0.0) |
| `ToPlanetocentric()` | Planetocentric coordinates in the body-fixed frame of the observer, a `CelestialBody` (11.0.0) |
| `ToPlanetodetic()` | Geodetic coordinates on the ellipsoid of the observer, a `CelestialBody`, in one call (11.0.0) |
| `Inverse()` | Invert position and velocity |
| `ToTLE(Configuration config)` | Convert to TLE format (`IO.Astrodynamics.OrbitalParameters.TLE.Configuration`); an orbit about the Earth only, `InvalidOperationException` otherwise (#607); a null or blank `CosparId` writes blank columns 10-17 (#604) |

A rectilinear state (r × v = 0: a radial motion, a zero velocity, the centre) or a non-finite one has no orbit plane and
no RTN frame: the members that read the plane (inclination, node, argument of periapsis, node vectors, longitudes, the
elements, the two-body motion to another epoch, the RTN rotations, and at the centre the anomalies and apsides) throw
`InvalidOperationException` (#687); see `docs/reference/state-vector.md`.

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, TimeScale.TDB);

// Create state vector
var sv = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),      // Position (m)
    new Vector3(0.0, 8000.0, 0.0),         // Velocity (m/s)
    earth, epoch, Frame.ICRF
);

// Compute orbital elements
Console.WriteLine($"Semi-major axis: {sv.SemiMajorAxis():F0} m");
Console.WriteLine($"Eccentricity: {sv.Eccentricity():F6}");
Console.WriteLine($"Inclination: {sv.Inclination() * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Period: {sv.Period().TotalMinutes:F2} min");

// Convert to Keplerian elements
var kep = sv.ToKeplerianElements();

// Transform to different frame
var svEcliptic = sv.ToFrame(Frame.ECLIPTIC_J2000);
```

#### KeplerianElements

Classical Keplerian orbital elements (osculating or mean).

| Constructor | Description |
|------------|-------------|
| `KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double ascendingNode, double argumentOfPeriapsis, double meanAnomaly, ILocalizable observer, Time epoch, Frame frame)` | Create from elements, outside the near-parabolic band (`rigthAscendingNode` before 11.0.0) |
| `KeplerianElements(..., OrbitalElementsType elementsType)` | Create with explicit element type |

| Factory Method | Description |
|----------------|-------------|
| `FromOMM(double meanMotion, double e, double i, double raan, double aop, double m, ILocalizable observer, Time epoch, Frame frame)` | Create mean elements from OMM data (units: rev/day for mean motion, degrees for angles) |
| `FromTrueAnomaly(double semiMajorAxis, double eccentricity, double inclination, double ascendingNode, double argumentOfPeriapsis, double trueAnomaly, ILocalizable observer, Time epoch, Frame frame)` | Elements given with their true anomaly: M(ν), `TrueAnomaly()` ν reduced to [0, 2π), the state from ν as given (#668, R23) |
| `Parabolic(double perigeeRadius, double inclination, double ascendingNode, double argumentOfPeriapsis, double meanAnomaly, ILocalizable observer, Time epoch, Frame frame, double eccentricity = 1.0)` | Parabola, or orbit within 1e-6 of e = 1, from its perigee radius: a = q / (1 − e), +Infinity at e = 1 (#459; the `perigeeRadius` parameter of the constructor before 11.0.0) |

| Property | Description |
|----------|-------------|
| `A` | Semi-major axis (m) |
| `E` | Eccentricity |
| `I` | Inclination (rad) |
| `RAAN` | Right ascension of ascending node (rad) |
| `AOP` | Argument of periapsis (rad) |
| `M` | Mean anomaly (rad) |
| `ElementsType` | Type indicator (Osculating or Mean) |

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Convert to state vector at epoch |
| `ToStateVector(Time epoch)` | Convert at specified epoch |
| `ToStateVector(double trueAnomaly)` | Convert at specified true anomaly |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `TrueAnomaly()` | Compute true anomaly (rad) |
| `TrueAnomaly(Time epoch)` | Compute true anomaly at epoch |
| `EccentricAnomaly()` | Compute eccentric anomaly (rad) |
| `Period()` | Get orbital period of an ellipse; an open orbit throws `InvalidOperationException`, a period given to the constructor included (#663) |
| `PerigeeRadius()` | Get perigee radius (m) |
| `PerigeeVector()` | Get perigee position vector, a(1 − e) P̂ (q P̂ within 1e-6 of e = 1, #737), P̂ the direction of the periapsis in the orbit plane, defined at e = 0 (#686); mean elements throw, as `ToStateVector()` does (use `PerigeeRadius()`) |
| `ApogeeVector()` | Get apogee position vector, −a(1 + e) P̂ (its `Magnitude()` is the apogee radius, m, a(1 + e)); an open orbit has none and throws `InvalidOperationException` (#737); mean elements throw |
| `PerigeeVelocity()` | Get the speed at perigee (m/s); mean elements throw |
| `ApogeeVelocity()` | Get the speed at apogee (m/s); mean elements throw |
| `IsCircular()` | Check if orbit is circular (e ≈ 0) |
| `IsElliptical()` | Check if orbit is elliptical |
| `IsParabolic()` | Check if orbit is parabolic (e = 1) |
| `IsHyperbolic()` | Check if orbit is hyperbolic (e > 1) |
| `AtEpoch(Time epoch)` | Get elements at different epoch, of the same `ElementsType` (#463) |
| `TimeToRadius(double radius)` | Epoch at a radius after the periapsis: the next on an ellipse (e ≤ 1 − 1e-6), before `Epoch` on an open orbit already past it, `Epoch` on a circular orbit; throws when the orbit never reaches it or Time cannot hold the epoch. Within 1e-6 of e = 1 the time is the conic's in universal variables, the motion of the band (#753; Barker's time before, up to 1.5e-3 s off; before #478 about 8 times off, and possibly before `Epoch`) |
| `TimeToSphereOfInfluence()` | Epoch at the sphere of influence of the center of motion (`TimeToRadius`); throws when it is infinite |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, TimeScale.TDB);

// Create osculating Keplerian elements (ISS-like orbit)
var kep = new KeplerianElements(
    6800000.0,                           // Semi-major axis (m)
    0.001,                               // Eccentricity
    51.6 * Constants.Deg2Rad,            // Inclination
    100.0 * Constants.Deg2Rad,           // RAAN
    90.0 * Constants.Deg2Rad,            // Argument of periapsis
    0.0,                                 // Mean anomaly (at perigee)
    earth,                               // Observer: the center of motion
    epoch,
    Frame.ICRF
);

// Get state at later time
var futureState = kep.ToStateVector(epoch.AddHours(1.5));

// Propagate using 2-body mechanics
var laterKep = kep.AtEpoch(epoch.AddDays(1));

// Create mean Keplerian elements from OMM data
// FromOMM accepts OMM native units: rev/day for mean motion, degrees for angles
var meanKep = KeplerianElements.FromOMM(
    meanMotion: 15.49309423,             // rev/day
    eccentricity: 0.0000493,
    inclination: 51.6423,                // degrees
    raan: 353.0312,                      // degrees
    argumentOfPeriapsis: 320.8755,       // degrees
    meanAnomaly: 39.2360,                // degrees
    observer: earth,
    epoch: epoch,
    frame: Frame.TEME
);

Console.WriteLine($"Elements type: {meanKep.ElementsType}"); // Mean
// Note: Mean elements cannot be directly converted to StateVector
// Use TLE.ToOsculating() or TLE.ToStateVector() for SGP4-based conversion
```

#### EquinoctialElements

Equinoctial orbital elements, without singularity for circular orbits (e = 0) or prograde equatorial ones (i = 0), where the Keplerian ω and Ω are undefined (`ToStateVector()` uses them directly; `ToKeplerianElements()` takes ω = 0 when f = g = 0 exactly and Ω = 0 when h = k = 0 exactly). Their only singular orbit is the retrograde equatorial one (i = 180°), where h = tan(i/2) cos Ω and k = tan(i/2) sin Ω grow without bound.

| Property | Description |
|----------|-------------|
| `P` | Semi-latus rectum a (1 − e²) (m) |
| `F` / `G` | Eccentricity components e cos(ω + Ω) / e sin(ω + Ω) |
| `H` / `K` | Inclination components tan(i/2) cos Ω / tan(i/2) sin Ω |
| `L0` | True longitude Ω + ω + ν (rad) |

The classical equinoctial set (GMAT's EquinoctialH, K, P and Q) is h = G, k = F, p = K and q = H.

| Method | Description |
|--------|-------------|
| `SemiMajorAxis()` | Semi-major axis (m) |
| `Eccentricity()` | Eccentricity |
| `Inclination()` | Inclination (rad) |
| `AscendingNode()` | RAAN (rad) |
| `ArgumentOfPeriapsis()` | Argument of periapsis (rad) |
| `ToStateVector()` | Convert to state vector |
| `ToKeplerianElements()` | Convert to Keplerian elements |
| `EquinoctialEx()` / `EquinoctialEy()` | Eccentricity components, `F` / `G` |
| `Hx()` / `Hy()` | Inclination components, `H` / `K` |
| `Lv()` | True longitude (rad), `L0` |

```csharp
var sv = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 1000.0),
    PlanetsAndMoons.EARTH_BODY, new Time(2024, 1, 1, TimeScale.TDB), Frame.ICRF);
var equinoctial = sv.ToEquinoctial();

// Access equinoctial components
Console.WriteLine($"Ex: {equinoctial.EquinoctialEx()}");
Console.WriteLine($"Ey: {equinoctial.EquinoctialEy()}");
Console.WriteLine($"Lv: {equinoctial.Lv() * Constants.Rad2Deg}°");
```

---

### IO.Astrodynamics.OrbitalParameters.TLE

#### TLE

Two-Line Element set for Earth-orbiting objects. TLE stores **mean** orbital elements that must be propagated using SGP4/SDP4 to obtain physical position/velocity.

| Constructor | Description |
|------------|-------------|
| `TLE(string name, string line1, string line2)` | Parse from standard 3-line format |

| Property | Description |
|----------|-------------|
| `Name` | Object name |
| `Line1` | First line of TLE |
| `Line2` | Second line of TLE |
| `NoradCatalogId` | NORAD catalog number, Alpha-5 decoded (`T5544` → 275544) |
| `Epoch` | Epoch of elements |
| `ElementsType` | Always returns `OrbitalElementsType.Mean` |
| `MeanSemiMajorAxis` | Mean semi-major axis (m) |
| `MeanEccentricity` | Mean eccentricity |
| `MeanInclination` | Mean inclination (rad) |
| `MeanAscendingNode` | Mean RAAN (rad) |
| `MeanArgumentOfPeriapsis` | Mean argument of periapsis (rad) |
| `MeanMeanAnomaly` | Mean mean anomaly (rad) |
| `BallisticCoefficient` | B* drag coefficient |
| `FirstDerivationMeanMotion` | First derivative of mean motion |
| `SecondDerivativeMeanMotion` | Second derivative of mean motion |

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Propagate to TLE epoch using SGP4/SDP4 (returns osculating state) |
| `ToStateVector(Time epoch)` | Propagate to specified epoch using SGP4/SDP4 |
| `ToOsculating()` | Convert to osculating StateVector at TLE epoch (via SGP4) |
| `ToOsculating(Time epoch)` | Convert to osculating StateVector at specified epoch (via SGP4) |
| `ToKeplerianElements()` | Get osculating Keplerian elements (via SGP4) |
| `ToMeanKeplerianElements()` | Get mean Keplerian elements directly from TLE data |
| `AtEpoch(Time epoch)` | Get propagated orbital parameters |
| `Create(OrbitalParameters params, string name, int noradId, string cosparId, ...)` | Create TLE from mean orbital parameters about the Earth in `Frame.TEME` (`ArgumentException` otherwise, #607); a null or blank `cosparId` writes blank columns 10-17 (#604) |

```csharp
// Parse TLE
var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

// TLE always stores mean elements
Console.WriteLine($"Elements type: {tle.ElementsType}"); // Mean

// Get osculating state at TLE epoch (SGP4 propagation)
var sv = tle.ToOsculating();

// Or propagate to specific time
var now = new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB);
var svAtTime = tle.ToStateVector(now);

// Access TLE mean parameters
Console.WriteLine($"NORAD ID: {tle.NoradCatalogId}");
Console.WriteLine($"Inclination: {tle.MeanInclination * Constants.Rad2Deg:F4}°");
Console.WriteLine($"Eccentricity: {tle.MeanEccentricity:F7}");

// Get mean Keplerian elements (preserves mean motion precision)
var meanKep = tle.ToMeanKeplerianElements();
Console.WriteLine($"Mean motion: {meanKep.MeanMotion()} rad/s");

// Create TLE from state vector
var epoch = new Time(2024, 1, 1, TimeScale.TDB);
var state = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5413.8, 5413.8),
    PlanetsAndMoons.EARTH_BODY, epoch, Frame.ICRF);
// The Configuration of IO.Astrodynamics.OrbitalParameters.TLE, not IO.Astrodynamics.Configuration
var config = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(99999, "MY_SAT", "24001A");
var newTle = state.ToTLE(config);

// NORAD catalog numbers above 99999 use the Alpha-5 convention (up to 339999)
var alpha5 = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(275544, "MY_SAT", "24001A");
var alpha5Tle = state.ToTLE(alpha5);
Console.WriteLine(alpha5Tle.Line1.Substring(2, 5)); // T5544
Console.WriteLine(alpha5Tle.NoradCatalogId);        // 275544
```

> **NORAD catalog numbers.** The 5-character TLE field holds plain numbers up to 99999 and
> Alpha-5 encoded numbers up to 339999 (`NoradCatalogNumber.MaxValue`). Values beyond that
> cannot be represented in TLE form at all — use a CCSDS OMM, whose `NORAD_CAT_ID` accepts
> 9 digits. Encoding and decoding are handled by `NoradCatalogNumber.Format` / `.Parse`.

---

### IO.Astrodynamics.CCSDS.OMM

The CCSDS OMM (Orbit Mean-elements Message) module provides support for reading, writing, and converting OMM files conforming to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).

#### Omm

Represents a complete CCSDS Orbit Mean-elements Message.

| Factory Method | Description |
|----------------|-------------|
| `LoadFromFile(string filePath, bool validateSchema, bool validateContent)` | Load OMM from XML file; a document with several OMM messages (#679), or an OMM with several segments, headers, bodies, metadata or data sections, or blocks of a data section, throws `OmmParseException`, as do `LoadFromString` and `LoadFromStream`; with schema validation (the default), checked in the document's form, unqualified or qualified below a root `ndm` or `omm` without a namespace, a document the NDM/XML 4.0.0 schema rejects, a repeated element included, or an `ndm` that holds no `omm`, throws `OmmValidationException` (third round of #668); a keyword whose value the data model refuses throws `OmmParseException` naming the keyword and its value, with or without schema validation for a value the schema accepts (`ArgumentException` of the data model before 11.0.0, fourth round of #668), and so does a numeric value NaN or ±Infinity (`NaN`, `Infinity` in any case, an overflow such as `1e400`; loaded before 11.0.0, #708), a covariance term whose value in m² overflows (above about 1.8e302 km², #729) and a date outside the two CCSDS forms (a time alone, a date alone, a bare number; read by `DateTime.TryParse` before 11.0.0, #619) |
| `LoadFromString(string xml, bool validateSchema, bool validateContent)` | Load OMM from XML string |
| `LoadFromStream(Stream stream, bool validateSchema, bool validateContent)` | Load OMM from stream |
| `CreateForTle(string objectName, string objectId, DateTime epoch, double meanMotion, ...)` | Create OMM for SGP4 propagation (mean elements and TLE parameters) |
| `CreateMinimal(...)` | Create OMM with the required fields only |

| Property | Description |
|----------|-------------|
| `Header` | CCSDS header with originator and creation date |
| `Metadata` | Object metadata (name, ID, reference frame, time system) |
| `Data` | Orbital data (mean elements, TLE parameters, spacecraft parameters) |
| `ObjectName` | Shortcut to metadata object name |
| `ObjectId` | Shortcut to metadata object ID (COSPAR ID format: "1998-067A") |
| `IsTleCompatible` | True if OMM contains TLE parameters and its `MEAN_ELEMENT_THEORY` is SGP4, its `REF_FRAME` TEME and its `CENTER_NAME` the Earth, each read trimmed and in any letter case; otherwise `ToTle()` throws naming the keywords at fault (#679) |

| Method | Description |
|--------|-------------|
| `SaveToFile(string filePath, bool validateBeforeSave)` | Save OMM to XML file |
| `SaveToStream(Stream stream, bool validateBeforeSave)` | Save OMM to a stream |
| `ToXml()` | Write OMM as an XML string (`OmmWriter` for the NDM container and indentation options) |
| `ToTle()` | Convert OMM to TLE (requires TLE parameters in data) |
| `Validate()` | Validate OMM content and return validation result |

> `ToTle()` throws `InvalidOperationException` when a field cannot be represented in TLE form
> rather than truncating it: `NORAD_CAT_ID` above 339999, `REV_AT_EPOCH` above 99999, or `MEAN_MOTION` above the
> 99.99999999 rev/day of its 11-character field (#729: an `ArgumentException` of the line length before 11.0.0, and above
> about 2.86e307 rev/day a TLE whose mean motion was the text `Infinity`).
> Such objects can only be carried in OMM form.

```csharp
using IO.Astrodynamics.CCSDS.OMM;

// Load OMM from file
var omm = Omm.LoadFromFile("satellite.omm", validateSchema: true, validateContent: true);

Console.WriteLine($"Object: {omm.ObjectName}");
Console.WriteLine($"COSPAR ID: {omm.ObjectId}");
Console.WriteLine($"Epoch: {omm.Data.MeanElements.Epoch}");
Console.WriteLine($"Mean Motion: {omm.Data.MeanElements.MeanMotion} rev/day");

// Convert OMM to TLE for propagation
if (omm.IsTleCompatible)
{
    var tle = omm.ToTle();
    var stateVector = tle.ToStateVector();  // Propagate with SGP4
    Console.WriteLine($"Position: {stateVector.Position}");
}

// Create OMM programmatically
var newOmm = Omm.CreateForTle(
    objectName: "MY_SATELLITE",
    objectId: "2024-001A",
    epoch: DateTime.UtcNow,
    meanMotion: 15.5,           // rev/day
    eccentricity: 0.001,
    inclination: 51.6,          // degrees
    raan: 100.0,                // degrees
    argOfPericenter: 90.0,      // degrees
    meanAnomaly: 0.0,           // degrees
    bstar: 0.0001,
    meanMotionDot: 0.00001,
    meanMotionDDot: 0.0
);

// Save to file
newOmm.SaveToFile("output.omm", validateBeforeSave: true);
```

#### TLE.ToOmm() - Convert TLE to OMM

The `TLE` class provides a `ToOmm()` method for converting TLE data to CCSDS OMM format:

```csharp
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.CCSDS.OMM;

// Parse TLE
var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

// Convert to OMM
var omm = tle.ToOmm(originator: "My Organization");

// Save to file for sharing/archiving
omm.SaveToFile("iss.omm");

// Or get as XML string
var xml = omm.ToXml();
```

#### Round-Trip Conversion

OMM and TLE can be converted back and forth. Note that TLE format has precision limitations (4 decimal places for angles, limited BSTAR precision):

```csharp
// Load OMM
var originalOmm = Omm.LoadFromFile("satellite.omm", validateSchema: false);

// Convert to TLE
var tle = originalOmm.ToTle();

// Use TLE for propagation
var sv = tle.ToStateVector(new Time(2024, 6, 15, 12, 0, 0, TimeScale.TDB));

// Convert back to OMM
var newOmm = tle.ToOmm();

// Save new OMM
newOmm.SaveToFile("satellite_updated.omm");

// Note: Due to TLE precision limitations, some values may differ slightly
// from the original OMM (typically 4-6 decimal places for angles)
```

#### OmmWriter / OmmReader

Low-level classes for reading and writing OMM XML files:

```csharp
// OmmReader for parsing
var reader = new OmmReader();
var omm = reader.ReadFromFile("input.omm");

// OmmWriter for custom output options
var writer = new OmmWriter
{
    WrapInNdmContainer = true,  // Wrap in <ndm> element
    IndentOutput = true         // Pretty-print XML
};

var xml = writer.WriteToString(omm);
writer.WriteToFile(omm, "output.omm");
```

#### OmmValidator

Validates OMM content for physical constraints and CCSDS compliance:

```csharp
// omm: an Omm, loaded or converted as above (tle.ToOmm())
var validator = new OmmValidator();
var result = validator.Validate(omm);

if (result.IsValid)
{
    Console.WriteLine("OMM is valid");
}
else
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error at {error.Path}: {error.Message}");
    }
}

// Warnings are also available
foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning: {warning.Message}");
}
```

#### COSPAR ID Format Conversion

OMM uses full COSPAR ID format ("1998-067A") while TLE uses abbreviated format ("98067A"). The conversion is handled automatically:

| Format | Example | Used In |
|--------|---------|---------|
| Full COSPAR ID | 1998-067A | OMM ObjectId |
| TLE COSPAR ID | 98067A | TLE Line 1 |
| No designator | `UNKNOWN` | OMM ObjectId of a TLE whose columns 10-17 are blank (#604) |
| No designator | 8 blanks | TLE Line 1 (`TLE.Create` with a null or blank `cosparId`, `ToTLE` with a null or blank `Configuration.CosparId`) |

```csharp
// OMM automatically converts COSPAR ID format during ToTle()
var omm = Omm.LoadFromFile("iss.omm");  // ObjectId: "1998-067A"
var tle = omm.ToTle();                   // Uses "98067A" internally

// TLE.ToOmm() converts back to full format
var newOmm = tle.ToOmm();                // ObjectId: "1998-067A"
```

---

### IO.Astrodynamics.CCSDS.OPM

The CCSDS OPM (Orbit Parameter Message) module provides support for reading, writing, and converting OPM files conforming to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book). OPM is used for exchanging osculating orbital state data, including state vectors, Keplerian elements, covariance matrices, spacecraft parameters, and maneuver information.

#### Opm

Represents a complete CCSDS Orbit Parameter Message.

| Factory Method | Description |
|----------------|-------------|
| `LoadFromFile(string filePath, bool validateSchema, bool validateContent)` | Load OPM from XML file; a document with several OPM messages (#679), or an OPM with several segments, headers, bodies, metadata or data sections, or blocks of a data section, throws `OpmParseException`, as do `LoadFromString` and `LoadFromStream`; with schema validation (the default), checked in the document's form, unqualified or qualified below a root `ndm` or `opm` without a namespace, a document the NDM/XML 4.0.0 schema rejects, a repeated element included, or an `ndm` that holds no `opm`, throws `OpmValidationException` (third round of #668); a keyword whose value the data model refuses throws `OpmParseException` naming the keyword and its value, with or without schema validation for a value the schema accepts (`ArgumentException` of the data model before 11.0.0, fourth round of #668), and so does a numeric value NaN or ±Infinity (`NaN`, `Infinity` in any case, an overflow such as `1e400`; loaded before 11.0.0, #708), a state or covariance value whose value in SI units overflows (above about 1.8e305 km or 1.8e302 km²; ±Infinity from `ToStateVector` before 11.0.0, #729) and a date outside the two CCSDS forms (a time alone, a date alone, a bare number; read by `DateTime.TryParse` before 11.0.0, #619) |
| `LoadFromString(string xml, bool validateSchema, bool validateContent)` | Load OPM from XML string |
| `LoadFromStream(Stream stream, bool validateSchema, bool validateContent)` | Load OPM from stream |
| `CreateFromStateVector(string objectName, string objectId, StateVector sv, ...)` | Create OPM from state vector; writes `REF_FRAME` as the CCSDS name of the state's frame: `ICRF` for `Frame.ICRF` (SPICE `J2000`), the frame's own name otherwise (`GCRF`, `TEME`, `ECLIPJ2000`, ...); `J2000` before 11.0.0 ([#339](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/339)) |

| Property | Description |
|----------|-------------|
| `Header` | CCSDS header with originator and creation date |
| `Metadata` | Object metadata (name, ID, center, reference frame, time system) |
| `Data` | Orbital data (state vector, Keplerian elements, spacecraft params, covariance, maneuvers) |
| `ObjectName` | Shortcut to metadata object name |
| `ObjectId` | Shortcut to metadata object ID (COSPAR ID format: "1998-067A") |
| `HasKeplerianElements` | True if OPM contains optional Keplerian elements |
| `HasSpacecraftParameters` | True if OPM contains spacecraft parameters (mass, drag, SRP) |
| `HasCovariance` | True if OPM contains covariance matrix |
| `HasManeuvers` | True if OPM contains maneuver parameters |

| Method | Description |
|--------|-------------|
| `SaveToFile(string filePath, bool validateBeforeSave, bool wrapInNdm, bool indent)` | Save OPM to XML file |
| `SaveToStream(Stream stream, bool validateBeforeSave, bool wrapInNdm, bool indent)` | Save OPM to a stream |
| `ToXmlString(bool wrapInNdm, bool indent)` | Convert to XML string without validation |
| `ToStateVector(ILocalizable observer)` | Convert OPM to framework StateVector (km→m, km/s→m/s); without an observer, the centre `CENTER_NAME` names (a barycenter, the Sun, a planet or a moon; another name throws `ArgumentException`), and an observer that contradicts it throws (#679) |
| `ToSpacecraft(int naifId, double maxOperatingMass = double.MaxValue, Clock clock = null, ILocalizable observer = null)` | Convert OPM to Spacecraft object; the observer as on `ToStateVector`; the defaults of the propagated constructor, a MASS above `maxOperatingMass` throwing `ArgumentOutOfRangeException` (decision 77 of #668) |
| `Validate()` | Validate OPM content and return validation result |

```csharp
using IO.Astrodynamics.CCSDS.OPM;

// Load OPM from file
var opm = Opm.LoadFromFile("spacecraft.opm", validateSchema: true, validateContent: true);

Console.WriteLine($"Object: {opm.ObjectName}");
Console.WriteLine($"COSPAR ID: {opm.ObjectId}");
Console.WriteLine($"Epoch: {opm.Data.StateVector.Epoch}");
Console.WriteLine($"Position: {opm.Data.StateVector.X}, {opm.Data.StateVector.Y}, {opm.Data.StateVector.Z} km");

// Convert to framework StateVector
var earth = new CelestialBody(PlanetsAndMoons.EARTH);
var stateVector = opm.ToStateVector(earth);  // Units: m, m/s

// Convert to Spacecraft for mission operations
var spacecraft = opm.ToSpacecraft(
    naifId: -1000,
    maxOperatingMass: 500000.0,
    clock: new Clock("OnboardClock", 256),
    observer: earth);
```

#### Spacecraft.ToOpm() - Convert Spacecraft to OPM

The `Spacecraft` class provides a `ToOpm()` method for exporting spacecraft state to CCSDS OPM format:

```csharp
// clock: a Clock; stateVector: the initial state (an ISS-like orbit)
// Create spacecraft and propagate with maneuvers
var spacecraft = new Spacecraft(-1000, "ISS", 420000.0, stateVector, 500000.0,
    clock, 1600.0, 2.2, "1998-067A", 1.5);

// After propagation with executed maneuvers...
var opm = spacecraft.ToOpm(
    originator: "Mission Control",
    epoch: null,                      // The initial epoch (or one within the propagation, #658)
    includeKeplerianElements: true,   // Include optional Keplerian elements
    includeSpacecraftParameters: true, // Include mass, drag, SRP
    includeManeuvers: true);          // Include the maneuvers ignited by the epoch

// Save for archival/sharing
opm.SaveToFile("iss_state.opm");
```

`REF_FRAME` and each `MAN_REF_FRAME` carry the CCSDS name of the frame of the initial orbital parameters: `ICRF` for
`Frame.ICRF` (SPICE `J2000`, written before 11.0.0), the frame's own name otherwise (#339).

The covariance of the initial state (`StateVector.Covariance`, m², m²/s, m²/s²) is exported in km², km²/s and km²/s²
when the OPM is written at the initial epoch (the default, or the same instant in any time scale). At another epoch the
OPM carries no covariance: moving it there would take the state transition matrix, which the export does not compute
(#551).

#### OpmData Components

OPM data contains mandatory state vector and optional components:

```csharp
// epoch: the epoch of the state; x, y, z (km) and vx, vy, vz (km/s): its components
// Create OPM with all components
var header = CcsdsHeader.Create("My Organization");
var metadata = new OpmMetadata("SAT-1", "2024-001A", "EARTH", "ICRF", "UTC");

// State vector (required) - units: km, km/s
var stateVector = new OpmStateVector(epoch, x, y, z, vx, vy, vz);

// Keplerian elements (optional) - units: km, degrees
var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
    semiMajorAxis: 6878.137,   // km
    eccentricity: 0.001,
    inclination: 51.6,         // degrees
    raan: 120.0,               // degrees
    aop: 90.0,                 // degrees
    trueAnomaly: 45.0,         // degrees
    gm: 398600.4418);          // km³/s²

// Covariance matrix (optional) - units: km², km²/s, km²/s²
var covariance = new CovarianceMatrix(
    cxX: 1.0e-6, cyX: 0.0, cyY: 1.0e-6, czX: 0.0, czY: 0.0, czZ: 1.0e-6,
    cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
    cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
    czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
    referenceFrame: "ICRF");

// Maneuver parameters (optional) - units: s, kg, km/s
var maneuver = new OpmManeuverParameters(
    manEpochIgnition: epoch.AddHours(2),
    manDuration: 120.0,        // seconds
    manDeltaMass: -30.0,       // kg (negative = expelled)
    manRefFrame: "RSW",
    manDv1: 0.05,              // km/s (radial)
    manDv2: 0.0,               // km/s (along-track)
    manDv3: 0.01);             // km/s (cross-track)

// User-defined parameters (optional)
var userParams = new OpmUserDefinedParameters(
    new Dictionary<string, string>
    {
        ["MISSION_ID"] = "MISSION-001",
        ["OPERATOR"] = "Space Agency"
    });

// Assemble OPM data
var data = new OpmData(
    stateVector: stateVector,
    keplerianElements: keplerianElements,
    mass: 2000.0,                         // kg
    dragArea: 30.0,                       // m²
    dragCoefficient: 2.2,
    solarRadiationPressureArea: 35.0,     // m²
    solarRadiationCoefficient: 1.5,
    covariance: covariance,
    maneuvers: new[] { maneuver },
    userDefinedParameters: userParams);

var opm = new Opm(header, metadata, data);
opm.SaveToFile("complete_opm.xml");
```

Every real value of the data model is a real number, as the readers read it: the components of `OpmStateVector`, the
terms of `CovarianceMatrix` and the ΔV components of `OpmManeuverParameters` throw `ArgumentOutOfRangeException` naming
the parameter for NaN or ±Infinity (#717), which the writer would write as `NaN` or `Infinity` and the reader refuse. The
components of `OpmStateVector` and the terms of `CovarianceMatrix` throw it too for a finite value whose value in SI units,
its product by 1000 (m, m/s) or 1e6 (m², m²/s, m²/s²), is not finite: above about 1.8e305 km or km/s and 1.8e302 km²
(#729), in the constructors and the factories `FromFullMatrix`, `FromMatrix` and `FromMatrixWithUnitConversion` alike;
`ToStateVector` and `ToMatrixWithUnitConversion` gave ±Infinity before 11.0.0.

#### ImpulseManeuver.ToOpmManeuverParameters()

After executing maneuvers during propagation, export them to OPM format:

```csharp
// spacecraft: a Spacecraft propagated with impulse maneuvers
// After spacecraft propagation with executed maneuvers
foreach (var maneuver in spacecraft.ExecutedManeuvers)
{
    if (maneuver is ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
    {
        var opmManeuver = impulseManeuver.ToOpmManeuverParameters(
            referenceFrame: "EME2000",
            comments: new[] { "Orbit raising maneuver" });

        Console.WriteLine($"Epoch: {opmManeuver.ManEpochIgnition}");
        Console.WriteLine($"Duration: {opmManeuver.ManDuration} s");
        Console.WriteLine($"Delta-V: {opmManeuver.DeltaVMagnitude:F6} km/s");
        Console.WriteLine($"Delta-Mass: {opmManeuver.ManDeltaMass} kg");
    }
}
```

#### OpmValidator

Validates OPM content for physical constraints and CCSDS compliance:

```csharp
// opm: an Opm, loaded or built as above
var validator = new OpmValidator();
var result = validator.Validate(opm);

if (result.IsValid)
{
    Console.WriteLine("OPM is valid");
}
else
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error at {error.Path}: {error.Message}");
    }
}
```

#### Unit Handling

OPM uses CCSDS units while the framework uses SI units. Conversions are automatic:

| Parameter | OPM Units | Framework Units | Conversion |
|-----------|-----------|-----------------|------------|
| Position | km | m | × 1000 |
| Velocity | km/s | m/s | × 1000 |
| Angles | degrees | radians | × π/180 |
| Mass | kg | kg | (same) |
| Area | m² | m² | (same) |
| Covariance (pos) | km² | m² | × 1e6 |
| Covariance (pos-vel) | km²/s | m²/s | × 1e6 |
| Covariance (vel) | km²/s² | m²/s² | × 1e6 |
| GM | km³/s² | m³/s² | × 1e9 |

The XML output includes `units` attributes on all numeric elements per CCSDS NDM/XML standard.

---

### IO.Astrodynamics.Frames

#### Frame

Reference frame for spatial transformations.

| Static Properties | Description |
|------------------|-------------|
| `Frame.ICRF` | International Celestial Reference Frame (J2000) |
| `Frame.ECLIPTIC_J2000` | Ecliptic plane at J2000 |
| `Frame.TEME` | True Equator Mean Equinox (for TLE) |

| Constructor | Description |
|------------|-------------|
| `Frame(string name)` | Create frame by name (e.g., "IAU_EARTH", "ITRF93") |

| Method | Description |
|--------|-------------|
| `ToFrame(Frame target, Time epoch)` | Get transformation to another frame |
| `GetStateOrientationToICRF(Time date)` | Get orientation relative to ICRF |

```csharp
// Use predefined frames
var icrf = Frame.ICRF;
var ecliptic = Frame.ECLIPTIC_J2000;

// Create body-fixed frame
var earthFrame = new Frame("IAU_EARTH");

// Transform state vector
var svICRF = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0),
    PlanetsAndMoons.EARTH_BODY, Time.J2000TDB, icrf);
var svEcliptic = svICRF.ToFrame(ecliptic);

// Get frame transformation
var rotation = icrf.ToFrame(ecliptic, Time.J2000TDB);
```

---

### IO.Astrodynamics.Surface

#### Site

Ground-based observation site.

| Constructor | Description |
|------------|-------------|
| `Site(int id, string name, CelestialBody body)` | Create site from SPICE data |
| `Site(int id, string name, CelestialBody body, Planetodetic coordinates)` | Create site with explicit coordinates |

| Property | Description |
|----------|-------------|
| `Id` | Site identifier |
| `Name` | Site name |
| `NaifId` | NAIF ID (bodyId * 1000 + siteId) |
| `CelestialBody` | Parent body |
| `InitialOrbitalParameters` | Site position as orbital parameters |

| Method | Description |
|--------|-------------|
| `GetHorizontalCoordinates(Time epoch, ILocalizable target, Aberration aberration)` | Get azimuth, elevation, range |
| `GetEphemeris(Time epoch, ILocalizable observer, Frame frame, Aberration aberration)` | Get site state relative to any observer |
| `GetEphemeris(Window window, ILocalizable observer, Frame frame, Aberration aberration, TimeSpan step)` | Get site states over window |
| `GetGeometricStateRelativeTo(Time epoch, CelestialItem referenceBody)` | Get geometric state relative to a reference body |
| `AngularSeparation(Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)` | Compute angular separation |
| `FindWindowsOnDistanceConstraint(...)` | Find distance constraint windows |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation windows |
| `FindWindowsOnIlluminationConstraint(...)` | Find the windows of the incidence angle of a source; the emission and phase angles need an observer and throw, the method is ELLIPSOID (#682) |
| `FindDayWindows(Window searchWindow, double twilight)` | Find daylight windows |
| `IlluminationIncidence(Time date, ILocalizable source, Aberration aberration)` | Get solar incidence angle |
| `WriteFrameAsync(FileInfo outputFile)` | Write site frame kernel: the frame of its coordinates, or for a site from the kernels whose frame id the loaded kernels define, that kernel frame under its kernel name (#690) |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;

// Create site from SPICE data (DSS-13 Goldstone)
var goldstone = new Site(13, "DSS-13", earth);

// Create site with explicit coordinates
var mySite = new Site(100, "MySite", earth,
    new Planetodetic(
        longitude: -117.0 * Constants.Deg2Rad,
        latitude: 34.0 * Constants.Deg2Rad,
        altitude: 1000.0  // meters
    ));

// Get horizontal coordinates to Moon
var epoch = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var horizontal = mySite.GetHorizontalCoordinates(epoch, moon, Aberration.LT);

Console.WriteLine($"Azimuth: {horizontal.Azimuth * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Elevation: {horizontal.Elevation * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Range: {horizontal.Range / 1000:F0} km");

// Find visibility windows
var windows = mySite.FindWindowsOnDistanceConstraint(
    new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 1, 2, TimeScale.TDB)),
    moon, RelationnalOperator.Lower, 400000000, Aberration.LT, TimeSpan.FromMinutes(10));
// One window, 2024-01-01 09:42:24.997 to 14:20:43.029 TDB, as CSPICE's spkcpt_c corrects the site (#489)
```

#### LaunchSite

Specialized site for launch operations.

| Constructor | Description |
|------------|-------------|
| `LaunchSite(int id, string name, CelestialBody body, Planetodetic coordinates, params AzimuthRange[] allowedAzimuths)` | Create with its coordinates and azimuth constraints |

| Method | Description |
|--------|-------------|
| `IsAzimuthAllowed(double azimuth)` | Check if launch azimuth is allowed |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var launchSite = new LaunchSite(100, "MyLaunchSite", earth,
    new Planetodetic(-81.0 * Constants.Deg2Rad, 28.5 * Constants.Deg2Rad, 0.0),
    new AzimuthRange(45 * Constants.Deg2Rad, 135 * Constants.Deg2Rad));

if (launchSite.IsAzimuthAllowed(90 * Constants.Deg2Rad))
    Console.WriteLine("East launch is allowed");
```

---

### IO.Astrodynamics.Coordinates

#### Planetodetic

Geodetic coordinates (longitude, latitude, altitude).

| Constructor | Description |
|------------|-------------|
| `Planetodetic(double longitude, double latitude, double altitude)` | Create coordinates (11.0.0: camelCase parameters, `longitude:`; `init` properties and `Deconstruct` kept) |

| Property | Description |
|----------|-------------|
| `Longitude` | Longitude (rad) |
| `Latitude` | Latitude (rad) |
| `Altitude` | Altitude above reference ellipsoid (m) |

| Method | Description |
|--------|-------------|
| `ToPlanetocentric(CelestialBody body)` | Convert to planetocentric on the ellipsoid of the body (11.0.0; before, a flattening and a radius) |
| `ToCartesianCoordinates(CelestialBody body)` | Body-fixed position on the ellipsoid of the body, m (`georec_c`; 11.0.0) |

```csharp
// Create geodetic coordinates
var coords = new Planetodetic(
    longitude: -122.0 * Constants.Deg2Rad,
    latitude: 37.0 * Constants.Deg2Rad,
    altitude: 100.0
);
```

#### Planetocentric

Planetocentric coordinates.

| Constructor | Description |
|------------|-------------|
| `Planetocentric(double longitude, double latitude, double radius)` | Create coordinates (11.0.0: camelCase parameters) |

| Method | Description |
|--------|-------------|
| `ToPlanetodetic(CelestialBody body)` | Convert to planetodetic on the ellipsoid of the body (11.0.0; before, a flattening and a radius) |
| `ToCartesianCoordinates()` | Convert to Cartesian |
| `RadiusFromPlanetocentricLatitude(CelestialBody body)` | Get the radius of the body's ellipsoid at the latitude (11.0.0; before, a radius and a flattening) |

#### Equatorial

Right ascension and declination in ICRF: `new Equatorial(stateVector)` and `ToEquatorial()` rotate a state in another
frame to ICRF at its epoch first (#680; before 11.0.0 they gave the angles of the position in the state's frame). For a
light-time corrected state read in a frame centred on its target (the Moon in MOON_ME), SPICE takes that frame at the
epoch minus the light time, while the rotation is taken at the state's epoch (3.05e-6 rad for the Moon): read such
states in ICRF. The coordinate searches of `CoordinateSystem.RaDec` keep the angles in their search frame, as `gfposc`.

| Property | Description |
|----------|-------------|
| `RightAscension` | Right ascension (rad) |
| `Declination` | Declination (rad) |
| `Distance` | Distance from observer (m) |
| `Epoch` | Epoch of coordinates |

| Factory | Description |
|---------|-------------|
| `FromRightAscensionDeclination(double rightAscension, double declination, Time epoch)` | Coordinates of a right ascension and a declination, in the order of the name, without a distance (11.0.0: replaces the constructor `Equatorial(declination, rightAscension, epoch)`) |
| `FromRightAscensionDeclination(double rightAscension, double declination, double distance, Time epoch)` | The same with a distance (m) |

| Method | Description |
|--------|-------------|
| `ToCartesian()` | Convert to Cartesian vector, in the frame of the angles (ICRF for a state's, #680) |
| `ToDirection()` | Get unit direction vector, in the frame of the angles (ICRF for a state's, #680) |

```csharp
// Get equatorial coordinates
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var sv = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None);
var eq = sv.ToEquatorial();

Console.WriteLine($"RA: {eq.RightAscension * Constants.Rad2Deg:F4}°");
Console.WriteLine($"Dec: {eq.Declination * Constants.Rad2Deg:F4}°");
```

#### Horizontal

Azimuth, elevation, range (topocentric).

| Constructor | Description |
|------------|-------------|
| `Horizontal(double azimuth, double elevation, double range, Time epoch)` | Create coordinates (11.0.0: camelCase parameters) |

| Property | Description |
|----------|-------------|
| `Azimuth` | Azimuth from north (rad) |
| `Elevation` | Elevation above horizon (rad) |
| `Range` | Distance to target (m) |

---

### IO.Astrodynamics.Body.Spacecraft

#### Spacecraft

Represents a spacecraft with components.

| Constructor | Description |
|------------|-------------|
| `Spacecraft(int naifId, string name, double mass, OrbitalParameters initialOrbitalParameters, double maxOperatingMass = double.MaxValue, Clock clock = null, double sectionalArea = 1.0, double dragCoefficient = 2.2, string cosparId = null, double solarRadiationCoefficient = 1.0, Vector3? bodyFront = null, Vector3? bodyRight = null, Vector3? bodyUp = null, double hardBodyRadius = 0.0)` | Create spacecraft; the clock by default `name + "_CLK"` at 65536 ticks per second (decisions 70 and 71 of #668) |

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF ID, in [-2147483, -1] so that the frame id NaifId × 1000 is an `int` (#676) |
| `Name` | Spacecraft name; its frame kernel variable `FRAME_<NAME>_FRAME` takes 20 ASCII characters at most, without blank, tab, quote, parenthesis, comma or '=' (#676) |
| `DryOperatingMass` | Mass without fuel (kg) |
| `MaxOperatingMass` | Maximum operating mass (kg): informational, checked against the dry mass only |
| `Clock` | Onboard clock, of this spacecraft's NAIF id alone: a clock attached to a spacecraft of another id is rejected (#676) |
| `SectionalArea` | Mean cross-sectional area for drag and SRP (m²) |
| `DragCoefficient` | Drag coefficient Cd (default 2.2) |
| `SolarRadiationCoeff` | Solar radiation pressure coefficient Cr (default 1.0; typically 1.0 to 2.0, finite and not negative) |
| `InitialOrbitalParameters` | Initial orbit |
| `BodyFront` | Instance body front axis (default: +Y, same as `Spacecraft.Front`) |
| `BodyRight` | Instance body right axis (default: +X, same as `Spacecraft.Right`) |
| `BodyUp` | Instance body up axis (default: +Z, same as `Spacecraft.Up`) |
| `BodyBack` / `BodyLeft` / `BodyDown` | Computed inverses of body axes |

**Configurable body axes**: Pass `bodyFront`, `bodyRight`, `bodyUp` to the constructor to override the default body frame. Custom axes must be finite, non-zero, orthogonal and right-handed (`bodyRight.Cross(bodyFront) == bodyUp`); a zero axis, or one with a component that is not finite, throws `ArgumentException` naming it. The attitudes read the axes of the spacecraft that carries the engine's tank at execution, with the static axes as fallback: the single-vector attitudes and the attitude of an impulse use `BodyFront` via `GetBodyFront()`, `TriadAttitude.CreateLVLH` uses `BodyDown`/`BodyFront` and `CreateProgradeWithSunTracking` `BodyFront`/`BodyUp` (#677); the `TriadAttitude` constructors take literal body vectors.

| Method | Description |
|--------|-------------|
| `AddCircularInstrument(...)` | Add a circular FOV instrument and return it: `(name, fieldOfView, boresight, refVector, orientation = default, naifId = null, model = null)`, the id by default the smallest free NaifId × 1000 − n, n in [1, 999] (`InvalidOperationException` when none is left), required on a kernel-backed spacecraft (`ArgumentNullException`), not another instrument's (`ArgumentException`), the name unique on its invariant upper-cased form (`ArgumentException`; decisions 72 to 74 of #668) (an id other than the spacecraft frame's, NaifId × 1000, #676; a finite non-zero boresight, a finite non-zero reference vector not parallel to it and a finite positive field of view, as `getfov` reads them, and finite Euler angles, or `ArgumentException` naming the parameter, #707, #715) |
| `AddRectangularInstrument(...)` | Add rectangular FOV instrument (same rules, and a finite positive cross angle, or `ArgumentOutOfRangeException`, #707, #715) |
| `AddEllipticalInstrument(...)` | Add elliptical FOV instrument (same rules, and a finite positive cross angle, or `ArgumentOutOfRangeException`, #707, #715) |
| `AddEngine(Engine engine)` | Add propulsion engine, on a tank instance added to the spacecraft (`ArgumentException` otherwise, #676, #586) |
| `AddFuelTank(FuelTank fuelTank)` | Add fuel tank, held by no other spacecraft (#676, #586); a maneuver draws on a tank of the spacecraft it is propagated with, or the propagation throws before it runs |
| `AddPayload(Payload payload)` | Add payload |
| `GetTotalMass()` | Get total mass including fuel |
| `GetInitialFuel()` | The fuel of the spacecraft's own tanks at the start of a run (`GetTotalFuel` before 11.0.0, decision 60 of #668) |
| `GetRemainingFuel()` | The fuel left in the spacecraft's own tanks |
| `GetTotalISP()` | The specific impulse of every engine fired together (Σ thrust / g0 / Σ fuel flow), whatever their tanks hold; NaN without an engine (decision 60 of #668, #801) |
| `GetTotalFuelFlow()` | The fuel flow of every engine, whatever their tanks hold; 0 without an engine |
| `SetManeuvers(params Maneuver[] maneuvers)` | Set the maneuver plan, in order, which every propagation runs from its first maneuver: the one way to link it (a second call replaces the first, no maneuver clears it, #584); it claims each maneuver for the spacecraft, `ArgumentException` for a maneuver of another spacecraft's plan, a repeated one, an engine whose tank is not the spacecraft's or a target orbit in another frame (decisions 45 and 46 of #668) |
| `Maneuvers` | The plan, as `SetManeuvers` set it |
| `Propagate(Window window, PropagationOptions options = null, CancellationToken cancellationToken = default)` | Propagate orbit with the settings of `options` (RK7(8) at 1e-9 about the central body alone, outputs every 60 s, when null), returns `PropagationSolution`; the token stops the run (`OperationCanceledException`, the spacecraft left re-armed and not propagated, R28 of #668) |
| `GetEphemeris(Time epoch, ILocalizable observer, Frame frame, Aberration aberration)` | Get state relative to observer (uses reference-body pattern) |
| `GetGeometricStateRelativeTo(Time epoch, CelestialItem referenceBody)` | Get geometric state relative to a reference body (interpolates from cache) |
| `GetOrientation(Frame frame, Time epoch)` | Get spacecraft orientation |
| `WriteOrientation(FileInfo file)` | Write orientation kernel |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, TimeScale.TDB);

// Define initial orbit
var orbit = new KeplerianElements(
    7000000, 0.001, 51.6 * Constants.Deg2Rad, 0, 0, 0,
    earth, epoch, Frame.ICRF
);

// Create spacecraft
var clock = new Clock("MainClock", 256);
var spacecraft = new Spacecraft(-1001, "MySat", 500.0, orbit, 1000.0, clock);

// Add components: the tank first, then the engine that draws on it (AddEngine requires its tank on the spacecraft)
var tank = new FuelTank("MainTank", "Propellant", "Tank1", 200.0, 100.0);  // capacity, initial quantity (kg)
spacecraft.AddFuelTank(tank);
var engine = new Engine("MainEngine", "RCS", "Engine1", 300.0, 0.05, tank);  // ISP (s), fuel flow (kg/s)
spacecraft.AddEngine(engine);

// Add instrument: its NAIF id by default the first free NaifId × 1000 − n, -1001001, and no model
spacecraft.AddCircularInstrument("Camera",
    10 * Constants.Deg2Rad,              // FOV
    new Vector3(1, 0, 0),                // Boresight
    new Vector3(0, 1, 0));               // Reference vector; the orientation zero by default

// Propagate (returns PropagationSolution for dense output). Velocity-Verlet is of the second order: its error grows as
// the square of the step (about 120 m after 12 h in this LEO at 1 s, 450 km at 60 s); without an integrator the
// options run RK7(8) at 1e-9
var solution = spacecraft.Propagate(
    new Window(epoch, epoch.AddDays(1)),
    new PropagationOptions
    {
        Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1) },
        OutputStep = TimeSpan.FromSeconds(1)
    }
);

// The state at any epoch within the propagation window, in its frame about its central body: position (m) and velocity (m/s)
var halfDay = solution.GetStateVector(epoch.AddHours(12));

// Read the stored states relative to an observer, in ICRF without correction: every minute over the first four
var states = spacecraft.GetEphemeris(new Window(epoch, epoch.AddMinutes(4)), earth, TimeSpan.FromMinutes(1));
foreach (var sv in states)
{
    Console.WriteLine($"{sv.Epoch}: {sv.Position.Magnitude():F0} m");
}
```

#### Engine

Propulsion engine.

| Constructor | Description |
|------------|-------------|
| `Engine(string name, string model, string serialNumber, double isp, double fuelFlow, FuelTank fuelTank)` | Create an engine drawing on a tank, which is added to the spacecraft before the engine (`AddFuelTank`, then `AddEngine`) |

| Property | Description |
|----------|-------------|
| `Name` | Engine name |
| `Model` | Engine model |
| `SerialNumber` | Engine serial number |
| `ISP` | Specific impulse (s) |
| `FuelFlow` | Fuel flow rate (kg/s) |
| `Thrust` | Thrust force (N): ISP × g0 × fuel flow |
| `FuelTank` | The tank the engine draws on, given to the constructor |

The engine burns its fuel in a run only: `Ignite` is internal since 11.0.0 (decision 49 of #668); the fuel of a ΔV is
`Tsiolkovski.DeltaM(isp, mass, deltaV)`.

#### FuelTank

Fuel storage tank.

| Constructor | Description |
|------------|-------------|
| `FuelTank(string name, string model, string serialNumber, double capacity, double initialFuel)` | Create tank |

| Property | Description |
|----------|-------------|
| `Capacity` | Maximum capacity (kg) |
| `InitialQuantity` | Initial fuel mass (kg) |
| `Quantity` | Current fuel mass (kg) |

#### Instrument

Base class for spacecraft instruments.

| Method | Description |
|--------|-------------|
| `IsInFOV(Time date, ILocalizable target, Aberration aberration)` | Whether the target's center is in the field: CSPICE's `fovtrg` with `POINT`; throws for a managed spacecraft without attitude (#682) |
| `FindWindowsInFieldOfViewConstraint(...)` | The windows, in TDB, of CSPICE's `gftfov`: the target as a point or as its ellipsoid, every field shape, on both paths (see `docs/reference/spacecraft.md`, "Field-of-view search"); throws for a managed spacecraft without attitude, never propagated, from a TLE or propagated by `TLEPropagator`, until `AddStateOrientationToICRF` gives it one (#682) |
| `GetBoresightInSpacecraftFrame()` | Get boresight in spacecraft frame |
| `GetBoresightInICRFFrame(Time date)` | Get boresight in ICRF |

---

### IO.Astrodynamics.Maneuver

#### Launch

Launch window computation.

| Constructor | Description |
|------------|-------------|
| `Launch(LaunchSite launchSite, Site recoverySite, OrbitalParameters targetOrbit, double twilight, bool? launchByDay)` | Create launch scenario: launch by day, by night, or either (null) |

| Method | Description |
|--------|-------------|
| `FindLaunchWindows(Window searchWindow)` | Find launch opportunities |
| `GetInertialAscendingAzimuthLaunch()` | Get ascending node launch azimuth |
| `GetInertialDescendingAzimuthLaunch()` | Get descending node launch azimuth |
| `GetInertialInsertionVelocity()` | Get required insertion velocity |

#### Maneuver (Base class)

Base class of the two kinds of maneuver, `ImpulseManeuver` and `Attitude`, the bases of a user maneuver (its constructor is `private protected`, decision 48 of #668).

| Method | Description |
|--------|-------------|
| `NextManeuver` | The next maneuver of its plan, set by `Spacecraft.SetManeuvers` only |
| `DeltaV`, `FuelBurned`, `ThrustWindow`, `ManeuverWindow` | The last run's values, cleared by the re-arm of the next run (decision 52 of #668) |

#### ImpulseManeuver (Abstract)

Base class for impulse maneuvers. Uses industry-standard g-function event detection for precise maneuver triggering.

| Method / Property | Description |
|--------|-------------|
| `ComputeEventValue(StateVector localState)` | Continuous g-function; zero-crossing triggers the maneuver. State is maneuver-center-relative. |
| `EventCrossingDirection` | Which zero-crossing direction triggers this maneuver (`NegativeToPositive`, `PositiveToNegative`, `Any`) |
| `CheckPreconditions(StateVector state)` | Optional additional constraints (default: true). Override for maneuvers with extra conditions. |
| `ComputeDeltaV(StateVector stateVector)` | The delta-V at the state, in its frame, without executing the maneuver: the state taken about the maneuver's centre, as the execution takes it; pure (decision 50 of #668) |
| `DeltaV` | Computed delta-V vector (set after execution) |

#### Attitude maneuvers

- `ProgradeAttitude`: Orient along velocity vector
- `RetrogradeAttitude`: Orient opposite to velocity
- `ZenithAttitude`: Orient away from central body
- `NadirAttitude`: Orient toward central body
- `NormalAttitude`: Orient along orbital normal (h = r × v)
- `AntiNormalAttitude`: Orient opposite to orbital normal (−h)
- `InstrumentPointingAttitude`: Point instrument at target (single-vector, roll unconstrained)
- `TriadAttitude`: Fully-constrained 3-DOF attitude using TRIAD algorithm (eliminates roll ambiguity)

The preview of an attitude is `ComputeStateOrientation(StateVector)`, the rotation from the spacecraft frame to ICRF at the state, pure; `Attitude.StateOrientation` is removed: in a run, the attitude at an activation is in the attitude record (`spacecraft.Frame.GetStateOrientationToICRF(epoch)`) (decisions 50 and 52 of #668).

The single-vector attitudes support configurable body axes via `GetBodyFront()`, which reads per-instance axes from `Spacecraft.BodyFront` with fallback to the static default `Spacecraft.Front` (+Y). The TRIAD factories read `BodyDown`/`BodyFront` and `BodyFront`/`BodyUp` of the same spacecraft, with the same fallback; both go through `GetBodyAxis(axis, fallback)` of `Maneuver` (`private protected`) (#677).

**TRIAD Attitude Determination**

The `TriadAttitude` class uses two non-collinear observation vectors to fully constrain spacecraft attitude. Unlike single-vector pointing, TRIAD eliminates roll ambiguity by constraining all three degrees of freedom.

```csharp
// earth, moon, sun: CelestialBody; camera: an Instrument and engine an Engine of the spacecraft;
// minimumEpoch, epoch: Time; holdDuration, duration: TimeSpan
// Point spacecraft Front at Moon, keep Up toward Sun
var attitude = new TriadAttitude(
    minimumEpoch, TimeSpan.FromMinutes(30),
    Spacecraft.Front, moon,    // Primary: Front points at Moon
    Spacecraft.Up, sun,        // Secondary: Up toward Sun (roll constraint)
    engine);

// Using instruments
var cameraAttitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    camera,                    // Uses camera boresight (primary) and refVector (secondary)
    earth, sun,                // Primary target, secondary target
    engine);

// Using IAttitudeTarget (orbital directions and/or celestial bodies)
var targetAttitude = new TriadAttitude(
    earth, epoch, duration,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,       // Primary: prograde pointing
    Spacecraft.Up, new CelestialAttitudeTarget(sun),         // Secondary: solar panel toward Sun
    engine);

// Factory methods for common operational attitudes, on the axes of the engine's spacecraft
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);
var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(earth, epoch, duration, sun, engine);
```

**IAttitudeTarget system**: The `IAttitudeTarget` interface enables orbital directions and celestial bodies to be used interchangeably as TRIAD targets:
- `OrbitalDirectionTarget` — predefined static instances: `.Prograde`, `.Retrograde`, `.Nadir`, `.Zenith`, `.Normal`, `.AntiNormal`
- `CelestialAttitudeTarget` — wraps `ILocalizable`, computes direction via ephemeris (default: `Aberration.None`; configurable, e.g. `new CelestialAttitudeTarget(moon, Aberration.LT)` for photon-based scenarios, the target as the spacecraft sees it, as `spkcvo_c` corrects it from the spacecraft's state, #740)
- `OrbitalDirection` enum: `Prograde`, `Retrograde`, `Nadir`, `Zenith`, `Normal`, `AntiNormal`

Key features:
- Minimum separation from collinear required between body vectors and reference vectors: `MinimumVectorSeparation`, 5 degrees by default, in [1e-14, π/2] rad (another throws `ArgumentOutOfRangeException`); collinear vectors (to the rounding of their components), zero and non-finite vectors throw at every separation allowed
- Uses spacecraft body frame: Front (+Y), Right (+X), Up (+Z) — or per-instance `BodyFront`, `BodyRight`, `BodyUp` passed explicitly
- Four constructor overloads: single instrument, dual instruments, explicit vectors, IAttitudeTarget; the body vectors given are literal
- Factory methods: `CreateLVLH()` (`BodyDown` to nadir, `BodyFront` toward prograde), `CreateProgradeWithSunTracking()` (`BodyFront` to prograde, `BodyUp` toward the Sun), on the instance axes of the engine's spacecraft, read at execution; the static axes while the tank is on no spacecraft (#677), read outside a propagation only, since a propagation of an attitude whose tank is not the propagated spacecraft's throws (#676)

#### Impulse maneuvers

Each impulse maneuver defines a g-function for event-driven triggering:

| Maneuver | g-function | Crossing Direction | Fires at |
|----------|-----------|-------------------|----------|
| `ApogeeHeightManeuver` | r·v | Negative → Positive | Perigee |
| `PerigeeHeightManeuver` | r·v | Positive → Negative | Apogee |
| `PhasingManeuver` | r·v | Negative → Positive | Perigee |
| `CombinedManeuver` | r·v | Positive → Negative | Apogee (+ precondition: apogee aligned with ascending node) |
| `PlaneAlignmentManeuver` | r·ĥ_target | Adaptive | Nearest orbital node (ascending or descending) |
| `ApsidalAlignmentManeuver` | r·(ĥ×p̂) | Negative → Positive | Nearest orbit intersection point |

The targets of `ApogeeHeightManeuver`, `PerigeeHeightManeuver` and `CombinedManeuver` are radii in m from the centre of
`ManeuverCenter`, not altitudes (for an altitude h, the equatorial radius + h); the burn moves the apsis up or down.

---

### IO.Astrodynamics.Maneuver.Lambert

#### LambertSolver

Solve Lambert's problem for transfer orbits.

| Method | Description |
|--------|-------------|
| `Solve(bool isRetrograde, OrbitalParameters initialOrbitalParameters, OrbitalParameters targetObjectOrbitalParameters, CelestialItem centerOfMotion, ushort maxRevolution)` | Solve Lambert problem; the `LambertResult` holds the solutions (`GetZeroRevolutionSolution()`, `GetMultiRevolutionSolutions(n)`), each with `V1`, `V2`, `DeltaV1`, `DeltaV2` and `DeltaV` |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, TimeScale.TDB);

// Define departure and arrival states: a LEO, and a point of a higher circular orbit 45 minutes later
var departure = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0),
    earth, epoch, Frame.ICRF);
var arrival = new StateVector(new Vector3(-6000000.0, 4000000.0, 0.0), new Vector3(-4124.0, -6187.0, 0.0),
    earth, epoch.AddMinutes(45.0), Frame.ICRF);

// Solve Lambert problem
var solver = new LambertSolver();
var result = solver.Solve(false, departure, arrival, earth, 0);

// Get zero-revolution solution
var solution = result.GetZeroRevolutionSolution();
Console.WriteLine($"Delta-V at departure: {solution.DeltaV1.Magnitude():F1} m/s");
Console.WriteLine($"Delta-V at arrival: {solution.DeltaV2.Magnitude():F1} m/s");
```

---

### IO.Astrodynamics.Propagator

#### CentralBodyPropagator

Numerical orbit propagator using a segment-based architecture with event-driven maneuver execution and dense output via Hermite interpolation. Handles LEO, GEO, lunar, and interplanetary missions — the central body is inferred from the spacecraft's initial orbital parameters observer (Star/Barycenter observers → Sun).

| Constructor | Description |
|------------|-------------|
| `CentralBodyPropagator(Window window, Spacecraft spacecraft, PropagationOptions options = null)` | Create propagator with auto force-building from the settings of `options` (RK7(8) at 1e-9 when given none; R06 and R07 of #668) |
| `CentralBodyPropagator(Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan step)` | Create propagator with custom integrator (forces pre-configured) |
| `CentralBodyPropagator(Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan step, CovariancePropagationOptions covariancePropagation)` | The same, with the state transition matrix, the sensitivity to Cd and Cr and the covariance propagated with the state (RK7(8) only; null for off) |

The constructors of flags (bodies, drag, SRP, with or without an integrator) were removed in 11.0.0 (R07 of #668): their settings are those of `PropagationOptions`, a call without an integrator `new VVIntegratorOptions { Step = step }` with `OutputStep = step`, bit for bit.

| Method | Description |
|--------|-------------|
| `Propagate(CancellationToken cancellationToken = default)` | Execute propagation with event detection and dense output, returns `PropagationSolution`; the token is read before the re-arm, before each segment and at each accepted step of the library's integrators (R28 of #668) |

**State transition matrix and covariance** (`IO.Astrodynamics.Propagator.Variational`; Auto by default on the options
entry points: `PropagationOptions.CovariancePropagation` null propagates a covariance the initial state carries at the
window start under RK7(8), with `new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }`
exactly, so at the cost of that explicit request, from about 1.5 times (point mass) to about 8 times (EGM2008 10x10 and drag) the run without it (the measured cost in
[docs/reference/covariance-propagation.md](docs/reference/covariance-propagation.md)); `CovariancePropagationOptions.None` turns it off, decision 17 of lot L4 of
#668): with
`CovariancePropagationOptions` (`Mode` `StateTransitionOnly` or `Covariance`, `ProcessNoise` an `RtnProcessNoise`,
`ConsiderParameters` the σ of Cd and Cr, `IncludeInStepControl`), activated by `PropagationOptions.CovariancePropagation` (on
every options entry point: `Spacecraft.Propagate`, `CentralBodyPropagator`, `PropagationTask`, `Scenario.Simulate`) or
the constructor of an integrator that holds its forces, an `RK78Integrator` propagates Φ, Ψ and P with the state. They are read from the solution
at any epoch: `GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance`,
`GetStateVector`. `ImpulseManeuver.ExecutionError` (`ManeuverExecutionError`, namespace `IO.Astrodynamics.Maneuver`)
adds the execution error of a burn, `StateVector.WithRemediatedCovariance()` remediates an initial covariance that
rounding made indefinite (a defect deeper than −1e-3 of the trace of its correlation matrix in the frame of the state and
in its RTN frame refused, #443; clipped in RTN when its depth passes there only), and `ConjunctionAssessment.Analyze` propagates the covariance of a participant propagated in
RK7(8) to the time of closest approach. See `docs/reference/covariance-propagation.md`,
`docs/tutorials/covariance-propagation.md` and `docs/reference/conjunction-assessment.md`.

**Propagation Architecture:**
1. **Build event detectors** from the spacecraft's maneuver chain: the leading attitudes on the attitude timeline, each at the later of its minimum epoch and the end of the previous hold, the last hold gating the first impulse (#539)
2. **Segment loop**: Integrator advances with event detection; on event → the detector executes the impulse only, the propagation counts its windows in TT seconds about the Earth (#547), lays its attitude and the attitudes chained after it on the timeline and builds the next detector, reinitialize
3. **Output**: Evaluate the solution (the internal `PropagationSolution.InterpolateAt`, the state `GetStateVector` returns inside the window; decision 54 of #668) every output step from the window start inside the window, then at the window end (in place of a grid point within ΔT/10 of it; #741, decided by S. Guillet, 2026-11-18), whether round(L/ΔT) rounds the window down or up (since 11.0.0: before, a grid that rounded up ran to round(L/ΔT)·ΔT, up to ΔT/2 past the window; R11 of #668, choice 6): a shortened RK7(8) step from the start of the accepted step for RK7(8), at the accuracy of the integrator; cubic Hermite interpolation for Velocity-Verlet
4. **Attitude record**: from the timeline, on the solution: each law at its activation, at each output epoch of its interval and 1 µs before the change that ends it; the impulse's attitude over its window (`docs/reference/attitudes.md#the-attitude-timeline`)

**Key Supporting Types:**
- `PropagationSolution`: Multi-segment container with `GetStateVector(epoch)` for dense output (shortened RK7(8) step for RK7(8), cubic Hermite otherwise; `InterpolateAt` internal since 11.0.0, decision 54 of #668), immutable from outside (decision 55, #796), its `Maneuvers` the `ExecutedManeuver` records of the run (decision 57)
- `PropagationSegment`: Ordered list of `AcceptedStep` records with binary-search Hermite interpolation, built once by `new PropagationSegment(baseEpoch, steps)` outside the library (`AddStep` internal, decision 55 of #668)
- `AcceptedStep`: Stores position, velocity, and acceleration at step start/end for cubic Hermite interpolation
- `IntegrationResult`: Integration segment + optional `EventInfo` (detector index, precise event time and state)

**Integrator Interface (`IIntegrator`):**
- `Initialize(StateVector initialState)` — set observer, frame, workspace and reset every state a run depends on; called by every `CentralBodyPropagator` constructor with the initial state of the propagation (the constructors that take an integrator with its forces included), at the start of every run of `Propagate` (so each run of one propagator instance is a fresh run, and an `Initialize` by the caller between construction and the run has no effect on it, #782) and after each event; an integrator belongs to one propagator, a second one given it throwing `ArgumentException` (`integrator`)
- `IntegrateSegment(pos, vel, baseEpoch, duration, eventDetectors)` → `IntegrationResult`: one event per call, the earliest crossing of the step, ties in list order (#578); a g exactly zero at a step end after a non-zero one is a crossing there (#580)
- Default of the options entry points: `RK78Integrator` at `RK78IntegratorOptions.Default` (1e-9, 1e-9; since 11.0.0)
- `VVIntegrator` (Velocity-Verlet, fixed-step, 1 s by default in `VVIntegratorOptions`, second order with drag too (#439), event detection at step boundaries, the step of an event integrated again to the tick of its label, #572)
- `RK78Integrator` (adaptive Prince-Dormand 7(8), sub-step event refinement via bisection)

**Event Detection (`IEventDetector`):**
- `Evaluate(state)` → continuous scalar g-function
- `Direction` → `CrossingDirection` (`NegativeToPositive`, `PositiveToNegative`, `Any`)
- `HandleEvent()` → `EventResult` (modified state, orientation, action, next detector); the internal `ManeuverEventDetector.HandleEvent` executes the impulse only and returns no next detector (#539)
- `ManeuverEventDetector` (internal since 11.0.0, decision 88 of #668: the propagation takes no user detector) wraps `ImpulseManeuver`, delegates to `ComputeEventValue()`

**Force Models:**
- **Gravitational acceleration** from the central body and each body of `PropagationOptions.ThirdBodies`. If a body has a `GeopotentialModelParameters`, the full spherical harmonic model (EGM2008) is used; otherwise point-mass gravity is applied.
- **Third-body perturbation**: Battin's numerically stable formula for each celestial body beyond the central body, the direct difference within half the distance of the perturbing body to the central body (SSB mode near the Earth, #576).
- **Atmospheric drag** (when `PropagationOptions.AtmosphericDrag` is true): uses the central body's atmosphere with atmosphere-relative velocity (accounts for body co-rotation): its atmospheric model, or NRLMSISE-00 at nominal space weather for an Earth built without one; another central body without a model throws `InvalidOperationException`; the other listed bodies with an atmosphere add their drag, and about the Sun (a star or barycentre observer) they give it, an Earth always having one (#674). Default Cd = 2.2 (free-molecular flow). Mass ratio is dynamic (tracks fuel consumption via `GetTotalMass()`).
- **The central body** takes part whether or not it is listed, with the observer's models; listed bodies of one NAIF identifier, the observer's included, must have the same models (`ArgumentException` otherwise). The integrator of a force-building path must be a new instance, and the step positive (#674).
- **Solar radiation pressure** (when `PropagationOptions.SolarRadiationPressure` is true): cannonball model with reflectivity coefficient Cr (`Spacecraft.SolarRadiationCoeff`, default 1.0). Uses continuous shadow fraction for partial/annular eclipse geometry instead of binary eclipse detection. Mass ratio is dynamic.
- **Albedo radiation pressure** (via `PropagationOptions.AlbedoRadiationPressure`): Lambertian sphere model for reflected sunlight. Requires `CelestialBody` with `albedo > 0`. Phase-angle dependent visibility function naturally zeroes on the dark side.
- **Thermal radiation pressure** (via `PropagationOptions.ThermalRadiationPressure`): Isotropic emitter model for infrared body radiation. Requires `CelestialBody` with `thermalEffectiveTemperature > 0` and `thermalEmissivity > 0`. Always present regardless of Sun geometry or eclipse state.
- **Ephemeris cache**: 60-second grid with 8-point Lagrange interpolation, injected into all force models to eliminate redundant SPICE calls during integration; with the orientation cache of the central body (exact SLERP, #561) it covers the span the propagation integrates, the window, and a read outside its grid reads the data provider (#626).

**Geopotential usage:**

```csharp
var epoch = new Time(2024, 1, 1, TimeScale.TDB);
var clock = new Clock("Clock", 256);

// Create Earth with degree-10 EGM2008 geopotential
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

// Initial state in ICRF
var orbit = new StateVector(
    new Vector3(6800000.0, 0, 0),
    new Vector3(0, 7500.0, 0),
    earth, epoch, Frame.ICRF);

var spacecraft = new Spacecraft(-1001, "Sat", 100.0, orbit, 10000.0, clock);

// Propagate with geopotential, Moon, and Sun perturbations
var propagator = new CentralBodyPropagator(
    new Window(epoch, epoch.AddDays(1)),
    spacecraft,
    new PropagationOptions
    {
        Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
        OutputStep = TimeSpan.FromSeconds(1.0),
        ThirdBodies = [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY]
    });
var solution = propagator.Propagate();

// Use dense output for sub-step reads
var middle = solution.GetStateVector(epoch.AddHours(12));
```

**Accuracy:** With degree-10 EGM2008, Moon, and Sun perturbations, 24h LEO propagation achieves sub-kilometer position accuracy compared to GMAT reference solutions.

#### TLEPropagator

SGP4/SDP4 propagator for TLE elements.

| Constructor | Description |
|------------|-------------|
| `TLEPropagator(Window window, Spacecraft spacecraft, TimeSpan step)` | Create propagator for a spacecraft whose initial orbital parameters are a TLE, sampled every step from the start and at the window end (#598); a step of zero or less throws `ArgumentOutOfRangeException` |

| Method | Description |
|--------|-------------|
| `Propagate(CancellationToken cancellationToken = default)` | Execute propagation, returns `PropagationSolution`; the token is read before the first sample and at each sample (R28 of #668) |

---

### Geopotential Gravity Models

The framework supports high-fidelity Earth gravity modeling using spherical harmonic expansion with the EGM2008 model (up to degree/order 70).

#### GeopotentialModelParameters

Configuration for enabling geopotential gravity on a `CelestialBody`.

| Constructor | Description |
|------------|-------------|
| `GeopotentialModelParameters(string path, int degree = 60)` | Load model from file path, checked here, read once by the first body or propagation |
| `GeopotentialModelParameters(Stream stream, int degree = 60)` | Load model from stream, read to its end here |
| `GeopotentialModelParameters(string path, int degree, double gravitationalParameter, double referenceRadius)` and its `Stream` form | Load a model whose harmonics take the given GM (m³/s²) and reference radius (m) |
| `GeopotentialModelParameters.Egm2008(int degree)` | EGM2008, tide free, embedded in the library to degree 70 (11.0.0, R06 of #668) |

A value since 11.0.0 (R06 of #668): a sealed record equal to another of the same source, degree, constants and central
term; every body and every propagation built from it shares its coefficients, each with its own field (#674).

| Property | Description |
|----------|-------------|
| `GeopotentialDegree` | Maximum degree/order to use (an `int`) |
| `GravitationalParameter`, `ReferenceRadius` | The constants given to the four-argument constructors; null otherwise. The harmonics then take EGM2008's GM (3.986004415e14 m³/s²) and radius (6378136.3 m) for the Earth, the kernel GM and equatorial radius for another body; the central term always takes the kernel GM (#410) |

#### GeopotentialGravitationalField

Computes the full 3D gravitational acceleration including spherical harmonic perturbations using the Montenbruck & Gill spherical coordinate gradient formulation.

| Property | Description |
|----------|-------------|
| `MaxDegree` | Maximum harmonic degree |

| Method | Description |
|--------|-------------|
| `ComputeGravitationalAcceleration(StateVector sv)` | Compute full 3D acceleration including harmonics |

**Algorithm:**
1. Transform position to body-fixed frame
2. Compute geocentric latitude, longitude, and radius
3. Evaluate geodesy-normalized associated Legendre functions and derivatives via stable recursion
4. Sum radial, latitudinal, and longitudinal gradient components over all harmonic degrees/orders
5. Transform spherical acceleration to body-fixed Cartesian, then rotate to inertial frame

**Normalization convention:** Geodesy (fully-normalized) without Condon-Shortley phase:
`P_bar_nm = sqrt((2 - delta_0m)(2n+1)(n-m)!/(n+m)!) * P_nm`

**Supported model file:** EGM2008 (tide-free), included as `Data/SolarSystem/EGM2008_to70_TideFree`. The file provides coefficients C_nm and S_nm from degree 2 to degree 70. The degree of a field is at most the highest degree of its file, whose every coefficient (n, m) with 2 ≤ n ≤ degree must be present: a degree above the file throws `ArgumentOutOfRangeException` (`maxDegrees`), a missing coefficient `ArgumentException`, when the body or the field is built (#689; before 11.0.0 both were read as zeros). Degrees 0 and 1, absent from the file, are the point mass.

**Thread safety:** `GeopotentialGravitationalField` is **not** thread-safe. Pre-allocated buffers (`_P`, `_dP` Legendre tables and `_cosMLambda`, `_sinMLambda` trig arrays) are overwritten on every acceleration evaluation. Create a separate `CelestialBody` instance per thread when propagating concurrently.

The orientation cache of a propagation is active only on the thread that propagates (`Frame.UseOrientationCache`), never set on the shared `Frame`, so propagations sharing a central body without geopotential may run concurrently.

`CelestialBody` instances *without* geopotential are safe to share across threads — two-body gravity is stateless, and the `Frame.GetStateOrientationToICRF` fallback uses a `ConcurrentDictionary` cache. Third-body perturbation bodies (Sun, Moon, etc.) that do not use geopotential can also be shared safely.

**Degree selection guidelines:**

| Degree | Use Case | Typical LEO Accuracy (24h) |
|--------|----------|---------------------------|
| 2 | J2-only, fast preliminary analysis | ~10 km |
| 10 | Good accuracy for most LEO missions | < 1 km vs GMAT high-fidelity propagation |
| 20-30 | High-fidelity geodesy applications | Sub-100 m |
| 70 | Maximum available (EGM2008_to70) | Highest fidelity |

```csharp
var epoch = new Time(2024, 1, 1, TimeScale.TDB);

// Degree-10 geopotential (good balance of accuracy and speed)
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

// Degree-2 (J2-only) for quick analysis
var earthJ2 = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 2));

// Point-mass only (no geopotential)
var earthPointMass = new CelestialBody(PlanetsAndMoons.EARTH);
```

---

### IO.Astrodynamics.Atmosphere

The atmosphere subsystem provides a unified interface for atmospheric modeling across different planets and model complexities.

#### Architecture Overview

```
IAtmosphericModel (Interface)
├── EarthStandardAtmosphere (U.S. Standard 1976, altitude-only)
├── MarsStandardAtmosphere (Simple Mars model, altitude-only)
└── Nrlmsise00Model (High-fidelity Earth model, requires full context)

IAtmosphericContext (Interface)
└── AtmosphericContext (Record with factory methods)

Atmosphere (Result Record)
├── Temperature (°C)
├── Pressure (kPa)
├── Density (kg/m³)
└── Details: IAtmosphericDetails (optional model-specific data)
    └── NrlmsiseDetails (molecular densities, exospheric temp)
```

#### IAtmosphericModel

Unified interface for all atmospheric models.

| Method | Description |
|--------|-------------|
| `GetAtmosphere(IAtmosphericContext context)` | Get complete atmospheric data (returns `Atmosphere` record) |
| `GetTemperature(IAtmosphericContext context)` | Get temperature (°C) |
| `GetPressure(IAtmosphericContext context)` | Get pressure (kPa) |
| `GetDensity(IAtmosphericContext context)` | Get density (kg/m³) |

#### Atmosphere

Result record containing atmospheric properties.

| Property | Type | Description |
|----------|------|-------------|
| `Temperature` | `double` | Temperature in Celsius |
| `Pressure` | `double` | Pressure in kPa |
| `Density` | `double` | Total mass density in kg/m³ |
| `Details` | `IAtmosphericDetails` | Optional model-specific details (null for simple models) |

#### AtmosphericContext

Context record for atmospheric calculations.

| Property | Type | Description |
|----------|------|-------------|
| `Altitude` | `double` | Altitude above reference surface (m) - required |
| `GeodeticLatitude` | `double?` | Geodetic latitude (rad) - optional |
| `GeodeticLongitude` | `double?` | Geodetic longitude (rad) - optional |
| `Epoch` | `Time?` | Time of calculation - optional |

| Factory Method | Description |
|----------------|-------------|
| `FromAltitude(double altitude)` | Create simple context with altitude only |
| `FromPlanetodetic(Planetodetic planetodetic, Time epoch)` | Create full context: the altitude, geodetic latitude and longitude of the coordinates (11.0.0; before, three doubles in the order altitude, latitude, longitude) |

#### EarthStandardAtmosphere

U.S. Standard Atmosphere 1976 (NASA-TM-X-74335) from −5 to 1000 km: its seven layers below 86 km and its thermosphere from 86 to 1000 km (#437); above 1000 km ln ρ is continued with its slope. Uses only altitude from context.

```csharp
var model = new EarthStandardAtmosphere();
var context = AtmosphericContext.FromAltitude(10000);  // 10 km

// Get individual values
var temp = model.GetTemperature(context);     // °C
var pressure = model.GetPressure(context);    // kPa
var density = model.GetDensity(context);      // kg/m³

// Or get complete atmospheric data
var atmosphere = model.GetAtmosphere(context);
Console.WriteLine($"T={atmosphere.Temperature:F1}°C, P={atmosphere.Pressure:F3} kPa, ρ={atmosphere.Density:E3} kg/m³");
```

#### Nrlmsise00Model

NRLMSISE-00 empirical atmosphere model (0-2000+ km). Requires full context with position and time. Thread-safe for concurrent use.

| Constructor | Description |
|------------|-------------|
| `Nrlmsise00Model()` | Create with nominal conditions (F10.7=150, Ap=4) |
| `Nrlmsise00Model(SpaceWeather weather)` | Create with custom space weather |
| `Nrlmsise00Model(NrlmsiseFlags flags, SpaceWeather weather)` | Create with custom flags and weather |

```csharp
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

// Use nominal conditions
var model = new Nrlmsise00Model();

// Or specify space weather
var spaceWeather = new SpaceWeather { F107 = 150, F107A = 150, Ap = 4 };
var weatherModel = new Nrlmsise00Model(spaceWeather);

// Or use preset conditions
var solarMinModel = new Nrlmsise00Model(SpaceWeather.SolarMinimum);
var solarMaxModel = new Nrlmsise00Model(SpaceWeather.SolarMaximum);

// Full context required
var context = AtmosphericContext.FromPlanetodetic(
    new Planetodetic(0, 0, 400000),  // prime meridian, equator, 400 km
    new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));

var atmosphere = model.GetAtmosphere(context);
Console.WriteLine($"Density at 400 km: {atmosphere.Density:E3} kg/m³");

// Access model-specific details via pattern matching
if (atmosphere.Details is NrlmsiseDetails details)
{
    Console.WriteLine($"Atomic Oxygen: {details.AtomicOxygenDensity:E3} m⁻³");
    Console.WriteLine($"Molecular Nitrogen: {details.NitrogenDensity:E3} m⁻³");
    Console.WriteLine($"Exospheric Temp: {details.ExosphericTemperature:F0} K");
}
```

#### NrlmsiseDetails

Model-specific details for NRLMSISE-00 results.

| Property | Type | Description |
|----------|------|-------------|
| `HeliumDensity` | `double` | He number density (m⁻³) |
| `AtomicOxygenDensity` | `double` | O number density (m⁻³) |
| `NitrogenDensity` | `double` | N₂ number density (m⁻³) |
| `MolecularOxygenDensity` | `double` | O₂ number density (m⁻³) |
| `ArgonDensity` | `double` | Ar number density (m⁻³) |
| `HydrogenDensity` | `double` | H number density (m⁻³, zero below 72.5 km) |
| `AtomicNitrogenDensity` | `double` | N number density (m⁻³, zero below 72.5 km) |
| `AnomalousOxygenDensity` | `double` | Anomalous oxygen density (m⁻³) |
| `ExosphericTemperature` | `double` | Exospheric temperature (K) |

#### SpaceWeather

Space weather data for NRLMSISE-00.

| Property | Description |
|----------|-------------|
| `F107` | Daily 10.7 cm solar radio flux |
| `F107A` | 81-day average of F10.7 |
| `Ap` | Daily magnetic index |
| `ApArray` | Optional 7-element AP history array |

| Static Property | F107 | Ap | Description |
|-----------------|------|-----|-------------|
| `Nominal` | 150 | 4 | Typical quiet conditions |
| `SolarMinimum` | 70 | 4 | Solar minimum conditions |
| `SolarMaximum` | 250 | 15 | Solar maximum conditions |
| `Moderate` | 150 | 7 | Moderate activity |

#### MarsStandardAtmosphere

Simple Mars atmospheric model using altitude only: the NASA Glenn curve fit below 49.9 km, the isothermal layer of
Seiff (1982) at 139 K above it (reached over a smooth transition of 5 km). The density is positive and decreasing at
every altitude, within a factor 2.8 of Seiff's nominal model up to 100 km; the thermosphere is not modelled, so drag in
orbit above about 120 km is underestimated (#431).

```csharp
var model = new MarsStandardAtmosphere();
var context = AtmosphericContext.FromAltitude(50000);  // 50 km
var atmosphere = model.GetAtmosphere(context);
Console.WriteLine($"Mars atmosphere: ρ={atmosphere.Density:E3} kg/m³");
```

#### CelestialBody Integration

`CelestialBody` provides `GetAtmosphere` methods that read its atmospheric model. An Earth built without one answers a
full context with NRLMSISE-00 at nominal space weather, the atmosphere of its drag (#674); without the geodetic position
and the epoch, a body without a model gives a NaN temperature and a zero pressure and density.

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;

// Simple: altitude-only; EARTH_BODY has no model, so NaN, 0 and 0 (give the body an EarthStandardAtmosphere for US 1976)
var atm1 = earth.GetAtmosphere(10000);  // 10 km altitude

// Full context: NRLMSISE-00 at nominal space weather for an Earth without a model
var context = AtmosphericContext.FromPlanetodetic(
    new Planetodetic(-75 * Constants.Deg2Rad, 45 * Constants.Deg2Rad, 400000),  // longitude, latitude (rad), altitude (m)
    new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));
var atm2 = earth.GetAtmosphere(context);

// Access NRLMSISE-00 specific data
if (atm2.Details is NrlmsiseDetails details)
{
    Console.WriteLine($"O₂ density: {details.MolecularOxygenDensity:E3} m⁻³");
}
```

#### Model Selection Guidelines

| Scenario | Recommended Model |
|----------|-------------------|
| Quick calculations without time or space weather (mean 1976 standard, to 1000 km) | `EarthStandardAtmosphere` |
| High-altitude modeling (> 100 km) | `Nrlmsise00Model` |
| Time-varying density (drag analysis) | `Nrlmsise00Model` |
| Space weather effects | `Nrlmsise00Model` |
| Mars atmospheric entry (below about 100 km) | `MarsStandardAtmosphere` |
| Via `CelestialBody` with full context | Automatic NRLMSISE-00 |

---

### IO.Astrodynamics.Math

#### Vector3

3D vector operations.

| Constructor | Description |
|------------|-------------|
| `Vector3(double x, double y, double z)` | Create vector |

| Property | Description |
|----------|-------------|
| `X`, `Y`, `Z` | Components |
| `Zero` | Zero vector |
| `VectorX`, `VectorY`, `VectorZ` | Unit vectors |

| Method | Description |
|--------|-------------|
| `Magnitude()` | Get vector length (scaled where the squares would overflow or underflow: the representable length of any finite vector) |
| `MagnitudeSquared()` | Get squared length |
| `Normalize()` | Get unit vector of any finite non-zero vector; `InvalidOperationException` for the zero vector (#680) or a component that is not finite |
| `Cross(Vector3 other)` | Cross product |
| `Angle(Vector3 other)` | Angle between vectors (rad) |
| `Inverse()` | Negate vector |
| `To(Vector3 other)` | Unit quaternion of the shortest rotation from this direction to the direction of `other`; throws for a zero or non-finite vector |
| `Rotate(Quaternion q)` | Rotate by quaternion |
| `LinearInterpolation(Vector3 other, double t)` | Linear interpolation |

| Operators | Description |
|-----------|-------------|
| `+`, `-` | Vector addition/subtraction |
| `-` (unary) | Negation, the same as `Inverse()` |
| `*` (double), double `*` | Scalar multiplication, either order |
| Matrix `*` | Product of a 3 × 3 matrix and the vector |
| `/` | Scalar division |

The dot product is `Dot(Vector3)`: the operator `*` between two vectors is removed in 11.0.0 (#668, R26).

```csharp
var v1 = new Vector3(1, 0, 0);
var v2 = new Vector3(0, 1, 0);

var cross = v1.Cross(v2);           // (0, 0, 1)
var dot = v1.Dot(v2);               // 0
var angle = v1.Angle(v2);           // π/2
var sum = v1 + v2;                  // (1, 1, 0)
var scaled = v1 * 2.0;              // (2, 0, 0)
```

#### Quaternion

Rotation representation.

| Constructor | Description |
|------------|-------------|
| `Quaternion(double w, double x, double y, double z)` | Create quaternion |

| Method | Description |
|--------|-------------|
| `Magnitude()` | Get magnitude |
| `Normalize()` | Get unit quaternion of any finite non-zero quaternion; `InvalidOperationException` for the zero quaternion (#680) or a component that is not finite |
| `Conjugate()` | Get conjugate |
| `ToEuler()` | Convert to Euler angles |
| `SLERP(Quaternion q, double t)` | Spherical linear interpolation of the rotations of the two quaternions, normalized first; throws for a zero or non-finite quaternion |
| `Lerp(Quaternion q, double t)` | Linear interpolation, normalized; throws for a zero or non-finite quaternion and for a chord through zero (q to −q at t = 0.5) |

#### Matrix

Matrix operations.

| Constructor | Description |
|------------|-------------|
| `Matrix(double[,] data)` | Create from 2D array, copied: a later write to the array does not change the matrix (#745) |

A `Matrix` is immutable (#668, R26: the public `Set` is removed in 11.0.0).

| Method | Description |
|--------|-------------|
| `this[int row, int col]`, `Get(int row, int col)` | Get element; `ArgumentOutOfRangeException` outside the matrix |
| `With(int row, int col, double value)` | Copy with one element changed |
| `Create(int rows, int columns, Func<int, int, double> term)` | Matrix of the terms of a function of the indexes |
| `Diagonal(params double[] diagonal)`, `Identity(int dimension)` | Diagonal and identity matrices |
| `Multiply(Matrix other)` | Matrix multiplication |
| `Multiply(double[] vector)` | Matrix-vector multiplication |
| `Inverse()` | Get inverse matrix |
| `ToQuaternion()` | Convert rotation matrix to quaternion |
| `CreateRotationMatrixX(double angle)` | Create X-axis rotation |
| `CreateRotationMatrixY(double angle)` | Create Y-axis rotation |
| `CreateRotationMatrixZ(double angle)` | Create Z-axis rotation |

#### Lagrange

Lagrange interpolation.

| Method | Description |
|--------|-------------|
| `Interpolate((double x, double y)[] data, double idx)` | Interpolate value |
| `Interpolate(StateVector[] stateVectors, Time date, int k)` | Interpolate state vector |

---

### IO.Astrodynamics.Physics

#### Tsiolkovski

Tsiolkovsky rocket equation calculations.

| Method | Description |
|--------|-------------|
| `DeltaV(double isp, double initialMass, double finalMass)` | Compute delta-V (m/s) |
| `DeltaT(double isp, double initialMass, double fuelFlow, double deltaV)` | Compute burn duration (s) |
| `DeltaM(double isp, double initialMass, double deltaV)` | Compute fuel required (kg) |

```csharp
double isp = 300;          // seconds
double m0 = 1000;          // kg initial
double mf = 800;           // kg final

double deltaV = Tsiolkovski.DeltaV(isp, m0, mf);
Console.WriteLine($"Delta-V: {deltaV:F1} m/s");

double fuelNeeded = Tsiolkovski.DeltaM(isp, m0, 500);  // 500 m/s delta-V
Console.WriteLine($"Fuel needed: {fuelNeeded:F1} kg");
```

---

### IO.Astrodynamics.DataProvider

#### SpiceDataProvider

Default data provider using SPICE kernels.

#### MemoryDataProvider

In-memory data provider for testing.

| Method | Description |
|--------|-------------|
| `AddCelestialBodyInfo(params CelestialBodyInfo[] celestialBodyInfos)` | Add the information of bodies (NAIF id, name, radii, GM, frame), a `CelestialBodyInfo` since 11.0.0 (the interop struct, now internal, `IO.Astrodynamics.DTO.CelestialBody` before) |
| `AddStateVector(int naifId, Time date, StateVector sv)` | Add state vector |
| `AddStateOrientationToICRF(Frame frame, Time date, StateOrientation orientation)` | Add orientation; `ArgumentException` (`frame`) for ICRF, GCRF, TEME, CIRS and TIRS, which the provider defines and never reads from stored entries (#744) |

```csharp
// epoch: Time; earthState: a StateVector of the Earth
// For testing without SPICE kernels
var memoryProvider = new MemoryDataProvider();
Configuration.Instance.SetDataProvider(memoryProvider);

memoryProvider.AddCelestialBodyInfo(new CelestialBodyInfo(NaifId: 399, Name: "EARTH", CenterOfMotionId: 10,
    BarycenterOfMotionId: 3, Radii: new Vector3(6378137.0, 6378137.0, 6356752.0), GM: 3.986004418e14,
    FrameName: "ITRF93", FrameId: 13000));
memoryProvider.AddStateVector(399, epoch, earthState);
```

A read at an added epoch returns that entry. A read between entries is interpolated and never stored, so it depends on
the entries only, not on the reads before it (#548): an orientation is the exact spherical linear interpolation of the
two entries around it (`StateOrientation.Interpolate`), a state the 8-point Lagrange interpolation of the nearest added
states. Missing data throw `InvalidOperationException`, as on SPICE (#680): a state read for a target without entries or
outside them, an orientation read outside a frame's entries. A bad argument throws `ArgumentException` (#680, decided by
S. Guillet, 2026-11-14): a frame neither added nor known (below), from `GetEphemeris` too since #559, and the
information of an identifier never added, whose `ParamName` is
`naifId`, as `SpiceDataProvider` for an identifier SPICE does not name, through `GetCelestialBodyInfo`,
`new CelestialBody(id)` and `CelestialItem.Create(id)` alike. A Lagrange point (391 to 395) is the exception of
`CelestialItem.Create`, which builds it from its state without reading its information: its missing state throws
`InvalidOperationException`, as on SPICE. A body whose barycentre's data, information included, the provider lacks builds
with `InitialOrbitalParameters` null and an infinite sphere of influence, as on SPICE.

A read is the difference of the stored states, taken in ICRF, rotated into the frame asked by the provider's orientation
of it; ICRF and GCRF are not rotated, and ICRF, GCRF, ECLIPJ2000 (SPICE's rotation of the J2000 obliquity, the default
frame of the bodies), TEME, CIRS and TIRS need no stored orientation. The provider holds geometric states only: an
aberration other than `Aberration.None` throws `NotSupportedException` (#559, decided by S. Guillet, 2026-11-17).

The static objects of `Barycenters`, `PlanetsAndMoons` (`EARTH_BODY`, `MOON_BODY`) and `Stars` (`SUN_BODY`) read the
provider at the first read of a member that needs its data, not in their type initializer: a provider set before their
first read and lacking their data makes each such read throw the provider's exception until the data are there, where
the type initializer threw before 11.0.0 a `TypeInitializationException` kept for the life of the process. Each read
goes to the provider set at its time: the data may be added to that provider, or another provider set in its place. Once
read, an object keeps the provider it read its data from, for its data and its ephemeris. Their identifiers, names and
predicates need no data. A provider that gives one of them a GM or, for `EARTH_BODY`, `MOON_BODY` and `SUN_BODY`, radii
of 0 (SPICE before `LoadKernels`, which answers 0 for `BODYnnn_GM` or `BODYnnn_RADII` no loaded kernel defines) makes
the read throw `InvalidOperationException` naming the variable, and nothing is kept. On SPICE their data follow the
kernel pool: the first read after each `LoadKernels` or `ReloadKernels` that hands SPICE a file, and after each `UnloadKernels` or `ClearKernels`, reads them again, so that a
kernel loaded later (`geophysical.ker` for `BODY399_J2`, `earth_assoc_itrf93.tf` and `moon_assoc_me.tf` for the frames
`ITRF93` and `MOON_ME`, an SPK for the orbit) reaches them; data equal to those held keep the instances held. To keep a
body's data across changes of the pool, build it (`new CelestialBody(id)`).

---

## Enumerations

### Aberration

Light-time and stellar aberration corrections.

| Value | Description |
|-------|-------------|
| `None` | No correction |
| `LT` | Light-time correction |
| `LTS` | Light-time + stellar aberration |
| `CN` | Converged Newtonian light-time |
| `CNS` | CN + stellar aberration |
| `XLT` | Transmission light-time |
| `XCN` | Transmission converged Newtonian |

### RelationnalOperator

Comparison operators for geometry finding.

| Value | Description |
|-------|-------------|
| `Greater` | Value > threshold (`>`) |
| `Lower` | Value < threshold (`<`) |
| `Equal` | Value = threshold (`=`) |
| `NotEqual` | Value ≠ threshold (`!`) |
| `GreaterOrEqual` | Value ≥ threshold (`>=`) |
| `LowerOrEqual` | Value ≤ threshold (`<=`) |
| `AbsoluteMin` | Absolute minimum (`ABSMIN`) |
| `AbsoluteMax` | Absolute maximum (`ABSMAX`) |
| `LocalMin` | Local minimum (`LOCMIN`) |
| `LocalMax` | Local maximum (`LOCMAX`) |

### OccultationType

Types of occultation events.

| Value | Description |
|-------|-------------|
| `Full` | Total occultation |
| `Partial` | Partial occultation |
| `Annular` | Annular occultation |
| `Any` | Any type of occultation |

### ShapeType

Body shape models.

| Value | Description |
|-------|-------------|
| `Ellipsoid` | Ellipsoidal model |
| `Point` | Point mass model |

---

## Predefined Objects

### SolarSystemObjects.PlanetsAndMoons

| Object | Description |
|--------|-------------|
| `EARTH` | Earth NAIF object (399) |
| `EARTH_BODY` | Earth CelestialBody |
| `MOON` | Moon NAIF object (301) |
| `MOON_BODY` | Moon CelestialBody |
| `MERCURY`, `VENUS`, `MARS`, `JUPITER`, `SATURN`, `URANUS`, `NEPTUNE` | Planets |

### SolarSystemObjects.Barycenters

| Object | Description |
|--------|-------------|
| `SOLAR_SYSTEM_BARYCENTER` | Solar system barycenter (0) |
| `EARTH_MOON_BARYCENTER` | Earth-Moon barycenter (3) |
| `MARS_BARYCENTER` | Mars system barycenter (4) |

### SolarSystemObjects.Stars

| Object | Description |
|--------|-------------|
| `Sun` | Sun NAIF object (10) |

---

## Best Practices

### Kernel Management

```csharp
// Load all required kernels at startup
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels"));

// Required kernel types:
// - LSK (leap seconds): naif0012.tls
// - SPK (ephemeris): de440.bsp, mar097.bsp
// - PCK (planetary constants): pck00010.tpc
// - FK (frames): earth_assoc_itrf93.tf

// Clean up when done
SpiceAPI.Instance.ClearKernels();
```

### Time Handling

```csharp
// The scale is a required argument: no calendar is TDB by default
var tdbNoon = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
var utc = new Time(new DateTime(2024, 1, 1, 12, 0, 0), TimeScale.UTC);

// Convert as needed
var tdb = utc.ToTDB();
var tai = utc.ToTAI();

// Use TDB for dynamical calculations
var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);
var state = earth.GetEphemeris(utc.ToTDB(), sun, Frame.ICRF, Aberration.None);
```

### Reference Frame Awareness

```csharp
// ICRF/J2000: Inertial frame for most calculations
var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0),
    PlanetsAndMoons.EARTH_BODY, new Time(2024, 1, 1, TimeScale.TDB), Frame.ICRF);

// Body-fixed frames for surface operations
var earthFrame = new Frame("IAU_EARTH");
var bodyFixed = sv.ToFrame(earthFrame);

// TEME for TLE propagation (handled automatically)
var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
var tleSv = tle.ToStateVector();  // Returns in TEME
var icrfSv = tleSv.ToFrame(Frame.ICRF);
```

### Error Handling

```csharp
// body: a CelestialItem; epoch: Time; observer: an ILocalizable; frame: a Frame
try
{
    var state = body.GetEphemeris(epoch, observer, frame, Aberration.None);
}
catch (SpiceException ex)
{
    // An error SPICE signals (an InvalidOperationException): a kernel not loaded,
    // an epoch out of coverage; ShortMessage is SPICE's code, such as
    // SPICE(SPKINSUFFDATA), LongMessage its long message.
    Console.WriteLine($"Ephemeris error {ex.ShortMessage}: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // Another failure of the native layer or of the library
    Console.WriteLine($"Ephemeris error: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Invalid parameters
    Console.WriteLine($"Parameter error: {ex.Message}");
}
```

---

## Advanced Scenarios

This section presents complete, real-world scenarios demonstrating how to combine multiple framework capabilities.

### Mission and Scenario Management

The framework provides `Mission` and `Scenario` classes for organizing complex simulations involving multiple spacecraft, ground sites, and celestial bodies.

```csharp
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Surface;

// Create a mission
var mission = new Mission("LunarExplorer");

// Create a scenario with a time window
var start = new Time(2024, 1, 1, TimeScale.TDB);
var end = start.AddDays(7);
var scenario = new Scenario("Phase1", mission, new Window(start, end));

// Add celestial bodies to the scenario
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var sun = new CelestialBody(Stars.Sun);

scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(moon);
scenario.AddCelestialItem(sun);

// Add ground stations
var goldstone = new Site(13, "DSS-13", earth);
var madrid = new Site(65, "DSS-65", earth);
scenario.AddSite(goldstone);
scenario.AddSite(madrid);
```

### Spacecraft Rendezvous Mission

A complete scenario with a chaser spacecraft executing maneuvers to reach a target spacecraft:

```csharp
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = new Time(2024, 3, 1, TimeScale.TDB);
var end = start.AddHours(12);

// Create mission and scenario
var mission = new Mission("Rendezvous");
var scenario = new Scenario("RendezvousPhase", mission, new Window(start, end));
scenario.AddCelestialItem(earth);

// Define target spacecraft orbit
var targetOrbit = new StateVector(
    new Vector3(4390853.7, 5110607.0, 917659.9),
    new Vector3(-4979.5, 3033.3, 6933.2),
    earth, start, Frame.ICRF
);

// Define chaser spacecraft initial orbit (parking orbit)
var chaserOrbit = new StateVector(
    new Vector3(5056554.2, 4395595.5, 0.0),
    new Vector3(-3708.6, 4266.3, 6736.9),
    earth, start, Frame.ICRF
);

// Create target spacecraft
var targetClock = new Clock("TargetClock", 65536);
var target = new Spacecraft(-1001, "Target", 500.0, targetOrbit, 1000.0, targetClock);
scenario.AddSpacecraft(target);

// Create chaser spacecraft with propulsion
var chaserClock = new Clock("ChaserClock", 65536);
var chaser = new Spacecraft(-1002, "Chaser", 1000.0, chaserOrbit, 10000.0, chaserClock);

// Add fuel tank and engine
var fuelTank = new FuelTank("MainTank", "Model1", "SN001", 9000.0, 9000.0);
var engine = new Engine("MainEngine", "Model1", "SN001", 450.0, 50.0, fuelTank);
chaser.AddFuelTank(fuelTank);
chaser.AddEngine(engine);

// The plan: plane alignment -> apsidal alignment -> phasing -> circularization
var maneuver1 = new PlaneAlignmentManeuver(
    new Time(DateTime.MinValue, TimeScale.TDB),
    TimeSpan.Zero, targetOrbit, engine
);

chaser.SetManeuvers(maneuver1,
    new ApsidalAlignmentManeuver(
        new Time(DateTime.MinValue, TimeScale.TDB),
        TimeSpan.Zero, targetOrbit, engine),
    new PhasingManeuver(
        new Time(DateTime.MinValue, TimeScale.TDB),
        targetOrbit, 1, engine),
    new ApogeeHeightManeuver(
        earth, new Time(DateTime.MinValue, TimeScale.TDB),
        TimeSpan.Zero, targetOrbit.SemiMajorAxis(), engine));

scenario.AddSpacecraft(chaser);

// Run the simulation
var summary = scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
    OutputStep = TimeSpan.FromSeconds(1.0)
});

// Read maneuver results
var executedManeuver = chaser.Maneuvers[0];  // the first maneuver of the plan, its values those of the last run
Console.WriteLine($"Maneuver 1 start: {executedManeuver.ManeuverWindow?.StartDate}");
Console.WriteLine($"Maneuver 1 delta-V: {((ImpulseManeuver)executedManeuver).DeltaV.Magnitude():F1} m/s");
Console.WriteLine($"Fuel burned: {executedManeuver.FuelBurned:F1} kg");
Console.WriteLine($"Total fuel consumption: {summary.SpacecraftSummaries.Single(x => x.Spacecraft == chaser).FuelConsumption:F1} kg");
```

### Lambert Transfer (Interplanetary or Lunar)

Compute transfer trajectories using Lambert's problem solver:

```csharp
using IO.Astrodynamics.Maneuver.Lambert;

var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);

// Define departure state (LEO)
var departureEpoch = Time.J2000TDB;
var departureState = new StateVector(
    new Vector3(7000000.0, 0.0, 0.0),
    new Vector3(0.0, 8000.0, 0.0),
    earth, departureEpoch, Frame.ICRF
);

// Define arrival (Moon position in 3 days)
var arrivalEpoch = departureEpoch.AddDays(3);
var moonState = moon.GetEphemeris(arrivalEpoch, earth, Frame.ICRF, Aberration.None);

// Solve Lambert's problem
var solver = new LambertSolver();
var result = solver.Solve(
    isRetrograde: false,
    departureState,
    moonState,
    earth,
    maxRevolution: 0
);

// Get the zero-revolution solution
var solution = result.GetZeroRevolutionSolution();

Console.WriteLine($"Departure velocity: {solution.V1.Magnitude():F1} m/s");
Console.WriteLine($"Arrival velocity: {solution.V2.Magnitude():F1} m/s");
Console.WriteLine($"Departure delta-V: {solution.DeltaV1.Magnitude():F1} m/s");
Console.WriteLine($"Arrival delta-V: {solution.DeltaV2.Magnitude():F1} m/s");

// Create the transfer trajectory
var transferOrbit = new StateVector(
    departureState.Position,
    solution.V1,
    earth, departureEpoch, Frame.ICRF
);

// Verify arrival position
var finalState = transferOrbit.AtEpoch(arrivalEpoch).ToStateVector();
Console.WriteLine($"Position error: {(finalState.Position - moonState.Position).Magnitude():F1} m");
```

### Launch Window Computation

Find optimal launch windows to reach a target orbit:

```csharp
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Coordinates;

var earth = PlanetsAndMoons.EARTH_BODY;
var searchStart = new Time(2024, 6, 2, TimeScale.TDB);

// Create launch site (Kennedy Space Center)
var launchSite = new LaunchSite(
    33,
    "KSC",
    earth,
    new Planetodetic(
        -81.0 * Constants.Deg2Rad,   // Longitude
        28.5 * Constants.Deg2Rad,    // Latitude
        0.0                          // Altitude
    ),
    new AzimuthRange(0.0, 2 * Math.PI)  // All azimuths allowed
);

// Create recovery site
var recoverySite = new Site(34, "Recovery", earth,
    new Planetodetic(-81.0 * Constants.Deg2Rad, 28.5 * Constants.Deg2Rad, 0.0));

// Define target orbit (ISS-like)
var targetOrbit = new StateVector(
    new Vector3(-1.144E+06, 4.905E+06, 4.553E+06),
    new Vector3(-5.588E+03, -4.213E+03, 3.126E+03),
    earth, searchStart, Frame.ICRF
);

// Create launch scenario
var launch = new Launch(
    launchSite,
    recoverySite,
    targetOrbit,
    twilight: Constants.CivilTwilight,  // Only launch during daylight
    launchByDay: true
);

// Search for launch windows over 24 hours
var searchWindow = new Window(searchStart, TimeSpan.FromDays(1.0));
var windows = launch.FindLaunchWindows(searchWindow).ToArray();

foreach (var lw in windows)
{
    Console.WriteLine($"Launch window: {lw.Window.StartDate}");
    Console.WriteLine($"  Inertial azimuth: {lw.InertialAzimuth * Constants.Rad2Deg:F2}°");
    Console.WriteLine($"  Non-inertial azimuth: {lw.NonInertialAzimuth * Constants.Rad2Deg:F2}°");
    Console.WriteLine($"  Insertion velocity: {lw.InertialInsertionVelocity:F1} m/s");
}
```

### Geometry Finder: Occultations and Eclipses

Search for eclipse and occultation events:

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var sun = new CelestialBody(Stars.Sun);

// Search window: year 2024
var searchWindow = new Window(
    new Time(2024, 1, 1, TimeScale.TDB),
    new Time(2024, 12, 31, TimeScale.TDB)
);

// Find lunar eclipses: the Moon (this item) behind the Earth, seen from the Sun
var lunarEclipses = moon.FindWindowsOnOccultationConstraint(
    searchWindow,
    observer: sun,
    targetShape: ShapeType.Ellipsoid,
    frontBody: earth,
    frontShape: ShapeType.Ellipsoid,
    occultationType: OccultationType.Any,
    aberration: Aberration.None,
    stepSize: TimeSpan.FromHours(1)
).ToArray();

Console.WriteLine($"Found {lunarEclipses.Length} lunar eclipses in 2024:");
foreach (var eclipse in lunarEclipses)
{
    Console.WriteLine($"  {eclipse.StartDate} to {eclipse.EndDate}");
    Console.WriteLine($"  Duration: {eclipse.Length.TotalMinutes:F1} minutes");
}

// Find distance constraint windows (Moon farther than 400,000 km from Earth)
var farMoonWindows = earth.FindWindowsOnDistanceConstraint(
    searchWindow,
    moon,
    RelationnalOperator.Greater,
    400000000,  // 400,000 km in meters
    Aberration.None,
    TimeSpan.FromHours(24)
).ToArray();

Console.WriteLine($"\nMoon > 400,000 km from Earth:");
foreach (var w in farMoonWindows.Take(5))
{
    Console.WriteLine($"  {w.StartDate.ToUTC()} to {w.EndDate.ToUTC()}");
}
```

### Instrument Field of View Analysis

Find windows when a target is visible in an instrument's field of view:

```csharp
using IO.Astrodynamics.Body.Spacecraft;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = new Time(2024, 6, 10, TimeScale.TDB);
var end = start.AddHours(2);

// Create mission and scenario
var mission = new Mission("EarthObservation");
var scenario = new Scenario("Observation", mission, new Window(start, end));
scenario.AddCelestialItem(earth);

// Define spacecraft orbit
var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.2, 0.0),
    earth, start, Frame.ICRF
);

// Create spacecraft with camera
var clock = new Clock("Clock1", 65536);
var spacecraft = new Spacecraft(-179, "Observer", 1000.0, orbit, 3000.0, clock);

// Add camera instrument (circular FOV, 43° half-angle, pointing nadir)
spacecraft.AddCircularInstrument(
    naifId: -179100,
    name: "MainCamera",
    model: "HighRes",
    fieldOfView: 0.75,                    // ~43° half-angle
    boresight: Vector3.VectorZ,           // Boresight along Z
    refVector: Vector3.VectorY,           // Reference vector
    orientation: new Vector3(0, Math.PI / 2, 0)  // Point toward nadir
);

scenario.AddSpacecraft(spacecraft);

// Simulate
scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
    OutputStep = TimeSpan.FromSeconds(1.0)
});

// Find windows when Earth is in the camera's field of view: any part of its ellipsoid, oriented by its frame, with the light time.
// The spacecraft is propagated, so the library searches; the windows are those of CSPICE's gftfov, in TDB, to 1 µs
var visibilityWindows = spacecraft.Instruments.First()
    .FindWindowsInFieldOfViewConstraint(
        new Window(start, end),
        spacecraft,
        earth,
        earth.Frame,
        ShapeType.Ellipsoid,
        Aberration.LT,
        TimeSpan.FromSeconds(60)
    ).ToArray();

Console.WriteLine($"Earth visibility windows:");
foreach (var window in visibilityWindows)
{
    Console.WriteLine($"  {window.StartDate} to {window.EndDate}");
    Console.WriteLine($"  Duration: {window.Length.TotalSeconds:F0} seconds");
}

// Check if a specific target is in FOV at a given time
bool isVisible = spacecraft.Instruments.First()
    .IsInFOV(start.AddMinutes(30), earth, Aberration.LT);
Console.WriteLine($"Earth in FOV at T+30min: {isVisible}");
```

### Export to Cosmographia

Export simulation results for visualization in Cosmographia:

```csharp
using IO.Astrodynamics.Cosmographia;

// start, end: the window of the scenario
// After running a scenario simulation...
var mission = new Mission("Visualization");
var scenario = new Scenario("Demo", mission, new Window(start, end));
// ... add celestial items, spacecraft, run simulation ...
scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
    OutputStep = TimeSpan.FromSeconds(1.0)
});

// Export to Cosmographia
var exporter = new CosmographiaExporter();
await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));

// This creates:
// - SPK files for spacecraft trajectories
// - CK files for spacecraft orientations
// - FK files for reference frames
// - JSON catalog for Cosmographia import
```

### TLE-Based Spacecraft Tracking

Track satellites using Two-Line Element sets:

```csharp
using IO.Astrodynamics.OrbitalParameters.TLE;

var earth = PlanetsAndMoons.EARTH_BODY;

// Parse ISS TLE
var issTLE = new TLE(
    "ISS (ZARYA)",
    "1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
    "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064"
);

// Create spacecraft from TLE
var clock = new Clock("ISSClock", 65536);
var iss = new Spacecraft(-25544, "ISS", 420000.0, issTLE, 450000.0, clock);

// Track from a ground station
var goldstone = new Site(13, "DSS-13", earth);

var trackStart = new Time(2024, 6, 3, TimeScale.TDB);
var trackEnd = trackStart.AddDays(1);

// Get ISS position relative to ground station over time
Console.WriteLine("ISS tracking from Goldstone:");
for (var t = trackStart; t < trackEnd; t = t.AddMinutes(10))
{
    var horizontal = goldstone.GetHorizontalCoordinates(t, iss, Aberration.LT);

    if (horizontal.Elevation > 0)  // Above horizon
    {
        Console.WriteLine($"{t.ToUTC()}: " +
            $"Az={horizontal.Azimuth * Constants.Rad2Deg:F1}° " +
            $"El={horizontal.Elevation * Constants.Rad2Deg:F1}° " +
            $"Range={horizontal.Range / 1000:F0} km");
    }
}

// Find visibility windows from ground station
var visibilityWindows = goldstone.FindWindowsOnDistanceConstraint(
    new Window(trackStart, trackEnd),
    iss,
    RelationnalOperator.Lower,
    2000000,  // Within 2000 km
    Aberration.LT,
    TimeSpan.FromMinutes(1)
);
```

### Deep Space Propagation with Multiple Perturbations

High-fidelity propagation including gravitational effects from multiple bodies:

```csharp
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Time.J2000TDB);
var sun = new CelestialBody(Stars.Sun);

// The Moon moves about the Earth under the attraction of both masses, so the Earth's central term is GM_Earth + GM_Moon:
// a field of degree 0 with the GM of the model is a point mass of that GM. With the Earth's GM alone, a massless probe
// started at the Moon's state would end about 54 310 km from the Moon after 25 days
double gmEarthMoon = PlanetsAndMoons.EARTH_BODY.GM + moon.GM;
var earthMoonField = new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 0, gmEarthMoon, 6378136.3)
{
    CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
};
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Time.J2000TDB, earthMoonField);

var start = Time.J2000TDB;
var end = start.AddDays(25);

var mission = new Mission("DeepSpaceTest");
var scenario = new Scenario("LunarOrbit", mission, new Window(start, end));

// Start at the Moon's position and velocity
var moonState = moon.GetEphemeris(start, earth, Frame.ICRF, Aberration.None);

var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-1001, "Probe", 100.0, moonState, 10000.0, clock);

scenario.AddSpacecraft(spacecraft);

// Add gravitational perturbations from multiple bodies
scenario.AddCelestialItem(sun);
scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(new Barycenter(5));  // Jupiter barycenter
scenario.AddCelestialItem(new Barycenter(6));  // Saturn barycenter

// Propagate at a 5-minute step. Velocity-Verlet is of the second order: it ends about 470 m from an exact integration of
// these forces after 25 days
var summary = scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromMinutes(5) },
    OutputStep = TimeSpan.FromMinutes(5)
});

// Compare the propagated Moon with the Moon of the ephemeris (DE440)
var propagatedMoon = spacecraft.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);
var ephemerisMoon = moon.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);

var positionDifference = (propagatedMoon.Position - ephemerisMoon.Position).Magnitude();
var velocityDifference = (propagatedMoon.Velocity - ephemerisMoon.Velocity).Magnitude();

Console.WriteLine($"Propagated Moon - DE440 Moon after 25 days:");
Console.WriteLine($"  Position difference: {positionDifference:F1} m");
Console.WriteLine($"  Velocity difference: {velocityDifference:F4} m/s");
```

The propagated Moon ends about 2.0 km from the Moon of DE440 after 25 days: 1.5 km are the forces the example leaves
out (the figures of the Earth and the Moon, the other planets, relativity), which an independent integration of the
example's forces (point masses with the kernel GMs, the bodies read from SPICE) ends at, and the rest is the error of
Velocity-Verlet at 5 minutes, 470 m from that integration (#704).

### High-Fidelity LEO Propagation with Geopotential

Propagate a LEO satellite with EGM2008 spherical harmonics, Moon, and Sun perturbations:

```csharp
var epoch = new Time(2025, 8, 25, 11, 55, 44, TimeScale.UTC);

// Earth with degree-10 EGM2008 geopotential
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

// LEO orbit (~400 km altitude)
var orbit = new StateVector(
    new Vector3(5442162.59, -4068949.85, -13456.85),   // Position (m)
    new Vector3(2858.20, 3809.79, 6002.13),             // Velocity (m/s)
    earth, epoch, Frame.ICRF);

var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-1001, "LEOSat", 100.0, orbit, 10000.0, clock);

var propagator = new CentralBodyPropagator(
    new Window(epoch, epoch.AddDays(1)),
    spacecraft,
    new PropagationOptions
    {
        Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
        OutputStep = TimeSpan.FromSeconds(1.0),
        ThirdBodies = [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY]
    });

var solution = propagator.Propagate();

// Get the final position and velocity from the dense output
var final = solution.GetStateVector(epoch.AddDays(1));

Console.WriteLine($"Position after 24h: ({final.Position.X/1000:F3}, " +
    $"{final.Position.Y/1000:F3}, {final.Position.Z/1000:F3}) km");
```

### Earth Observation with Attitude Maneuvers

Point instruments at ground targets during observation passes:

```csharp
using IO.Astrodynamics.Maneuver;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = new Time(2024, 1, 1, TimeScale.TDB);
var end = start.AddDays(1);

var mission = new Mission("EarthObs");
var scenario = new Scenario("TargetPointing", mission, new Window(start, end));
scenario.AddCelestialItem(earth);

// Define orbit
var orbit = new KeplerianElements(
    11800000.0, 0.3, 1.0, 0.0, 0.0, 0.0,
    earth, start, Frame.ICRF
);

// Create spacecraft
var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-334, "Observer", 1000.0, orbit, 2000.0, clock);

// Add antenna instrument
spacecraft.AddCircularInstrument("Antenna",
    0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -334100, model: "HighGain");

// Add propulsion
var tank = new FuelTank("Tank1", "Model", "SN1", 2000.0, 2000.0);
var engine = new Engine("Engine1", "Model", "SN1", 450, 50.0, tank);
spacecraft.AddFuelTank(tank);
spacecraft.AddEngine(engine);

// Create ground target
var groundStation = new Site(14, "DSS-14", earth);
scenario.AddSite(groundStation);
scenario.AddSpacecraft(spacecraft);

// Configure attitude maneuver to point antenna at ground station
var pointingManeuver = new InstrumentPointingToAttitude(
    start.AddHours(7.25),           // Start time
    TimeSpan.FromHours(0.5),        // Duration
    spacecraft.Instruments.First(), // Instrument to point
    groundStation,                  // Target
    engine                          // Engine for attitude control
);                                  // The direction is light-time corrected (Aberration.LT, the default, #549);
                                    // pass Aberration.None as a sixth argument for the geometric direction

spacecraft.SetManeuvers(pointingManeuver);

// Simulate
scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
    OutputStep = TimeSpan.FromSeconds(1.0)
});

Console.WriteLine($"Pointing maneuver executed at: {pointingManeuver.ManeuverWindow?.StartDate}");
```

### TRIAD Attitude for Multi-Target Pointing

Use TRIAD attitude when you need fully-constrained orientation (e.g., pointing at target while maintaining solar panel alignment):

```csharp
using IO.Astrodynamics.Maneuver;

var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);
var start = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
var end = start.AddHours(2);

var mission = new Mission("ObservationMission");
var scenario = new Scenario("DualPointing", mission, new Window(start, end));
scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(sun);

// Create spacecraft in LEO
var orbit = new KeplerianElements(
    6778000.0, 0.001, 51.6 * Constants.Deg2Rad, 0.0, 0.0, 0.0,
    earth, start, Frame.ICRF
);

var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-500, "Observer", 500.0, orbit, 1000.0, clock);

// Add Earth observation camera (points down via boresight)
spacecraft.AddCircularInstrument("EarthCam",
    0.5, Vector3.VectorZ.Inverse(), Vector3.VectorY, Vector3.Zero, naifId: -500100, model: "NadirCamera");  // Boresight: -Z (nadir), refVector: +Y

// Add propulsion
var tank = new FuelTank("Tank1", "Model", "SN1", 100.0, 100.0);
var engine = new Engine("Engine1", "Model", "SN1", 220, 10.0, tank);
spacecraft.AddFuelTank(tank);
spacecraft.AddEngine(engine);

scenario.AddSpacecraft(spacecraft);

// Configure TRIAD attitude:
// - Primary: Spacecraft Down (-Z) points at Earth (nadir)
// - Secondary: Spacecraft Right (+X) oriented toward Sun, which sets the roll about nadir (a vector at 5 degrees at
//   least from the primary: Up, opposite to Down, would be rejected)
var triadManeuver = new TriadAttitude(
    start,                          // Start time
    TimeSpan.FromHours(1.5),        // Hold duration
    Spacecraft.Down, earth,         // Primary constraint: nadir pointing
    Spacecraft.Right, sun,          // Secondary constraint: +X toward the Sun
    engine
);

spacecraft.SetManeuvers(triadManeuver);

// Simulate with high time resolution for attitude dynamics
scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
    OutputStep = TimeSpan.FromSeconds(1.0)
});

Console.WriteLine($"TRIAD maneuver executed at: {triadManeuver.ManeuverWindow?.StartDate}");
Console.WriteLine("Spacecraft maintains nadir pointing with roll constrained toward Sun");
```

**When to use TRIAD vs single-vector attitude:**
- Use `InstrumentPointingToAttitude`, `NadirAttitude`, `NormalAttitude`, etc. when roll is unconstrained (simpler, faster)
- Use `NormalAttitude` / `AntiNormalAttitude` for plane-change burns (h = r × v)
- Use `TriadAttitude` when you need specific roll orientation (solar panels, thermal control, dual instruments)
- Use `TriadAttitude` with `IAttitudeTarget` to mix orbital directions and celestial targets:

```csharp
// earth: the CelestialBody of the orbit; sun: the Sun; engine: an Engine of the spacecraft; epoch: Time; duration: TimeSpan
// LVLH attitude using factory method (the spacecraft's BodyDown to nadir, its BodyFront toward prograde)
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);

// Prograde with sun tracking using factory method
var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(earth, epoch, duration, sun, engine);

// Custom: nadir pointing with normal constraint (using OrbitalDirectionTarget)
var customLvlh = new TriadAttitude(
    earth, epoch, duration,
    Spacecraft.Down, OrbitalDirectionTarget.Nadir,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    engine);
```

---

## Disclaimer and Validation

**THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND.**

### Important Notice for Developers

When integrating IO.Astrodynamics into your applications:

1. **Validate all results** - Cross-check computed values against authoritative sources (JPL Horizons, GMAT, FreeFlyer or equivalent tools) before relying on them for critical decisions

2. **Understand limitations** - The software implements standard algorithms but may not account for all perturbations or edge cases relevant to your specific application

3. **Not certified for flight** - This software is NOT certified for flight-critical, safety-critical, or life-critical applications. Independent verification and validation (IV&V) is required before operational use

4. **SPICE kernel quality** - Results depend on the accuracy of loaded SPICE kernels. Ensure you use appropriate, up-to-date kernels for your application

5. **Numerical precision** - Be aware of floating-point precision limits, especially for long propagations or highly elliptical orbits

6. **Time system awareness** - Ensure correct time system usage (UTC, TDB, etc.) as mixing time systems can introduce significant errors

### Recommended Validation Approach

```csharp
// moon, earth: CelestialBody; epoch: Time
// Example: Validate against JPL Horizons for critical calculations
var computed = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None);
// Compare computed.Position with Horizons data
// Acceptable tolerance depends on your application requirements
```

See the [LICENSE](../LICENSE) file for complete warranty disclaimer terms.

---

## Version Information

- NuGet Package: 11.0.0
- CLI Tool: 11.0.0
- Framework: .NET 10.0 (`net10.0` only)
- Supported runtimes: `win-x64`, `linux-x64`, `osx-arm64` (Apple Silicon), `osx-x64` (Intel)
- SPICE Toolkit: CSPICE N0067
- License: LGPL-3.0-or-later

The breaking changes of 11.0.0, each with its migration, are the **Breaking** entries under **Changed** and the entries under **Removed** of the 11.0.0 release notes (`docs/contributing/versioning.md#1100`).

The native proxies of 11.0.0 let no C++ exception cross P/Invoke and leave no SPICE error pending when they fail: a failed proxy returns `false`, or a NaN result for the element conversions (`ConvertConicElementsToStateVectorAtEpochProxy` included) and the TLE proxies (#711), with the exception's message then SPICE's in `GetLastErrorProxy()`, SPICE reset (#706, #711, #712); `Propagate2BodiesProxy` and `ConvertStateVectorToConicOrbitalElementProxy` return a NaN result and `TransformFrameProxy` a NaN rotation and angular velocity, with SPICE's message (#714: before, they dropped it, the two-body proxies aborted a process whose first native call they were and otherwise returned the stack's contents, and the transformation returned the zero quaternion). Each proxy that reports through `GetLastErrorProxy()` clears it when it starts, so the message read after a call is that call's, and a SPICE error with no long message is reported by its short one (#714); `GetLastErrorShortProxy()` and `GetLastErrorLongProxy()` give SPICE's short and long messages of that error, empty when the failure is not SPICE's, which `SpiceAPI` throws as `SpiceException` (R03 of #668); a native caller checks the result before using it. The six exports .NET never called (`LaunchProxy`, `TDBToStringProxy`, `UTCToStringProxy`, `ConvertTDBToUTCProxy`, `ConvertUTCToTDBProxy`, `ConvertStateVectorToEquatorialCoordinatesProxy`) are removed in 11.0.0; a native caller uses the C++ classes they called.

### Breaking Changes in 8.5.0

The native C++ proxy layer has been updated for safety and correctness:

- **Native error propagation**: `ReadOrientationProxy`, `ReadEphemerisProxy`, and `LaunchProxy` now return `bool` instead of `void`. C++ exceptions no longer cross the P/Invoke boundary (which previously could crash the .NET process). Errors are retrieved via `GetLastErrorProxy()` and surfaced as `InvalidOperationException` in .NET.
- **Ephemeris write fix**: `WriteEphemerisProxy` now correctly uses the per-element center of motion ID (`sv[i]`) instead of always using the first element (`sv[0]`).
- **Buffer safety**: All `FindWindows*` functions are bounds-checked to their declared array size (1000). `ReadOrientation`/`ReadEphemeris` loops are bounds-checked to 10000. All DTO string setters use `strncpy` instead of `strcpy`.
- **Memory safety**: `ReadOrientationProxy` no longer uses heap-allocated matrices; uses the stack-based `Matrix(double[3][3])` constructor instead, eliminating memory leak risk.
- **Native library**: Both `libIO.Astrodynamics.so` (Linux) and `IO.Astrodynamics.dll` (Windows) must be rebuilt from this version. Old native binaries are **incompatible** with the new .NET P/Invoke signatures.

### Breaking Changes in 8.6.0

The propagator and maneuver systems have been redesigned:

- **`SpacecraftPropagator` renamed to `CentralBodyPropagator`**: Handles all numerical propagation (LEO through interplanetary). Central body is inferred from the spacecraft's initial observer. `SsbPropagator` has been removed.
- **`Maneuver.CanExecute()` removed**: Maneuver triggering now uses industry-standard g-function event detection. Each `ImpulseManeuver` implements `ComputeEventValue(StateVector)` → continuous scalar and `EventCrossingDirection` → crossing direction.
- **`Maneuver.ComputeManeuverPoint()` removed**: Replaced by g-function zero-crossing in `ComputeEventValue()`.
- **`Attitude.ComputeOrientation()` is now `public`** (was `protected`): Enables continuous attitude recomputation at output epochs.
- **New `ImpulseManeuver` abstract members**: `ComputeEventValue(StateVector)`, `EventCrossingDirection`, and optional `CheckPreconditions(StateVector)` must be implemented by custom maneuver subclasses.
- **New integrator interface**: `IIntegrator` with `Initialize(StateVector)` and `IntegrateSegment()` replaces the previous tight coupling between propagator and integrator.
- **New event detection system**: `IEventDetector`, `ManeuverEventDetector`, `CrossingDirection` in `IO.Astrodynamics.Propagator.Events`.
- **Segment-based propagation**: `PropagationSolution`, `PropagationSegment`, `AcceptedStep` enable cubic Hermite dense output for all integrators.
- **`Spacecraft.Propagate()` simplified**: Always uses `CentralBodyPropagator` — no auto-routing between different propagator types.
- **`Propagate()` returns `PropagationSolution`**: `PropagatorBase.Propagate()`, `CentralBodyPropagator.Propagate()`, `TLEPropagator.Propagate()`, and `Spacecraft.Propagate()` now return a `PropagationSolution` for dense output via `InterpolateAt(epoch)`. Previously callers had to access `StateVectorsRelativeToICRF` directly.
- **SSB-specific methods removed from public API**: `GetGeometricStateFromICRF` removed from `ILocalizable` interface and changed to `internal` on `CelestialItem`, `Spacecraft`, `Star`, and `Site`. `GetEphemerisFromICRF` removed from `IDataProvider` interface and `SpiceDataProvider`. Use `GetEphemeris(epoch, target, observer, frame, aberration)` with SSB as observer for equivalent functionality. SSB-relative states are now an internal implementation detail used only for aberration correction and star positions.
- **`Spacecraft.WriteEphemeris()` writes CB-relative states**: SPK files now contain states relative to the central body instead of SSB. SPICE handles ephemeris chaining automatically via the center body ID in the SPK segment.

Contact: [contact@io-aerospace.org](mailto:contact@io-aerospace.org)
