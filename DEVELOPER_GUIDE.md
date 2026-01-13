# IO.Astrodynamics.Net Developer Guide

## Introduction

IO.Astrodynamics.Net is a .NET 8/10 astrodynamics framework for orbital mechanics calculations, ephemeris computations, and space mission planning. Built on NASA's CSPICE toolkit, it provides high-precision calculations for spacecraft trajectory analysis, celestial body ephemerides, and mission planning.

### What IO.Astrodynamics Does

- **Orbital Mechanics**: Compute orbital elements (Keplerian, equinoctial), state vectors, orbital periods, anomalies
- **Ephemeris Computation**: Read and write SPICE kernel files for celestial bodies and spacecraft
- **Time Systems**: Handle multiple time frames (UTC, TDB, TAI, TDT, GPS, Local)
- **Reference Frames**: Transform between inertial and body-fixed frames (ICRF, ECLIPTIC, IAU frames)
- **Spacecraft Modeling**: Define spacecraft with instruments, engines, fuel tanks, and payloads
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
- Detailed attitude control system design

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

- **Time**: IERS Conventions, leap seconds from NAIF LSK kernels
- **Ephemerides**: JPL Development Ephemeris (DE series)
- **Reference Frames**: IAU/IAG standards, IERS conventions
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

### Loading SPICE Kernels

Before any computation, load the required SPICE kernel files:

```csharp
using IO.Astrodynamics;

// Load kernels from a directory
API.Instance.LoadKernels(new DirectoryInfo("path/to/kernels"));

// Or load a specific kernel file
API.Instance.LoadKernels(new FileInfo("de440.bsp"));
```

### Basic Example: Compute Earth Position

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

// Load kernels
API.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

// Create celestial bodies
var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);

// Define epoch
var epoch = new Time(2024, 6, 21, 12, 0, 0);

// Get Earth's state vector relative to the Sun
var stateVector = earth.GetEphemeris(epoch, sun, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();

Console.WriteLine($"Position: {stateVector.Position}");
Console.WriteLine($"Velocity: {stateVector.Velocity}");
```

### Basic Example: Create an Orbit

```csharp
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Math;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, 0, 0, 0);

// Create from Keplerian elements
var orbit = new KeplerianElements(
    a: 7000000.0,                        // Semi-major axis (m)
    e: 0.001,                            // Eccentricity
    i: 51.6 * Constants.Deg2Rad,         // Inclination (rad)
    raan: 100.0 * Constants.Deg2Rad,     // RAAN (rad)
    aop: 90.0 * Constants.Deg2Rad,       // Argument of periapsis (rad)
    m: 0.0,                              // Mean anomaly (rad)
    observer: earth,
    epoch: epoch,
    frame: Frames.Frame.ICRF
);

// Convert to state vector
var sv = orbit.ToStateVector();
Console.WriteLine($"Orbital Period: {orbit.Period().TotalHours:F2} hours");
```

### Basic Example: Parse a TLE

```csharp
using IO.Astrodynamics.OrbitalParameters.TLE;

var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

// Propagate to a specific time
var epoch = new Time(2021, 1, 21, 12, 0, 0);
var sv = tle.ToStateVector(epoch);

Console.WriteLine($"ISS Position: {sv.Position}");
```

---

## API Reference

### IO.Astrodynamics (Core)

#### API

Singleton class providing access to SPICE operations.

| Method | Description |
|--------|-------------|
| `LoadKernels(FileSystemInfo path)` | Load SPICE kernel(s) from file or directory |
| `UnloadKernels(FileSystemInfo path)` | Unload SPICE kernel(s) |
| `ClearKernels()` | Unload all kernels |
| `GetLoadedKernels()` | Get list of currently loaded kernels |
| `GetSpiceVersion()` | Get SPICE toolkit version |

```csharp
// Load all kernels from a directory
API.Instance.LoadKernels(new DirectoryInfo("kernels/"));

// Check loaded kernels
foreach (var kernel in API.Instance.GetLoadedKernels())
    Console.WriteLine(kernel);
```

#### Configuration

Configure framework-wide settings.

| Method | Description |
|--------|-------------|
| `SetDataProvider(IDataProvider provider)` | Set the data provider (SPICE or Memory) |

```csharp
// Use memory data provider for testing
Configuration.Instance.SetDataProvider(new MemoryDataProvider());
```

---

### IO.Astrodynamics.TimeSystem

#### Time

Represents a precise instant in time with time frame awareness.

| Constructor / Method | Description |
|---------------------|-------------|
| `Time(int year, int month, int day)` | Create from date (defaults to TDB) |
| `Time(int year, int month, int day, int hour, int minute, int second)` | Create from date and time |
| `Time(DateTime dateTime, ITimeFrame frame)` | Create from DateTime with specified frame |
| `Time(string iso8601)` | Parse from ISO 8601 string |
| `Create(double secondsFromJ2000, ITimeFrame frame)` | Create from seconds since J2000 |
| `CreateTDB(double secondsFromJ2000)` | Create TDB time from J2000 seconds |
| `CreateUTC(double secondsFromJ2000)` | Create UTC time from J2000 seconds |
| `ToTDB()` | Convert to TDB time frame |
| `ToUTC()` | Convert to UTC time frame |
| `ToTAI()` | Convert to TAI time frame |
| `ToJulianDate()` | Get Julian Date |
| `TimeSpanFromJ2000()` | Get TimeSpan from J2000 epoch |
| `Add(TimeSpan span)` | Add duration |
| `AddDays(double days)` | Add days |
| `AddHours(double hours)` | Add hours |
| `AddSeconds(double seconds)` | Add seconds |

```csharp
// Create times
var t1 = new Time(2024, 6, 21, 12, 0, 0);
var t2 = new Time("2024-06-21T12:00:00.000 UTC");
var t3 = Time.CreateTDB(788400000.0);  // Seconds from J2000

// Convert between time frames
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

```csharp
var window = new Window(
    new Time(2024, 1, 1),
    new Time(2024, 1, 31)
);

Console.WriteLine($"Duration: {window.Length.TotalDays} days");

// Check intersection
var t = new Time(2024, 1, 15);
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
| `InitialOrbitalParameters` | Initial orbital state |

| Method | Description |
|--------|-------------|
| `GetEphemeris(Time epoch, ILocalizable observer, Frame frame, Aberration aberration)` | Get state at epoch |
| `GetEphemeris(Window window, ILocalizable observer, Frame frame, Aberration aberration, TimeSpan step)` | Get states over window |
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
var state = earth.GetEphemeris(Time.J2000TDB, sun, Frames.Frame.ICRF, Aberration.None);
var sv = state.ToStateVector();

// Compute geosynchronous orbit
var geoOrbit = earth.GeosynchronousOrbit(0.0, 0.0, new Time(2024, 1, 1));

// Find lunar eclipses
var eclipses = sun.FindWindowsOnOccultationConstraint(
    new Window(new Time(2024, 1, 1), new Time(2024, 12, 31)),
    earth, ShapeType.Ellipsoid, moon, ShapeType.Ellipsoid,
    OccultationType.Any, Aberration.None, TimeSpan.FromHours(1));
```

#### CelestialItem

Base class for celestial objects (bodies, spacecraft).

| Method | Description |
|--------|-------------|
| `GetGeometricStateFromICRF(Time date)` | Get geometric state in ICRF |
| `AngularSize(double distance)` | Compute angular diameter at distance |
| `IsOcculted(CelestialItem by, OrbitalParameters from, Aberration aberration)` | Check if occulted |
| `WriteEphemeris(FileInfo outputFile)` | Write ephemeris to SPK file |

---

### IO.Astrodynamics.OrbitalParameters

#### OrbitalElementsType (enum)

Discriminant for orbital element types.

| Value | Description |
|-------|-------------|
| `Osculating` | Instantaneous (true) orbital elements |
| `Mean` | Time-averaged elements (e.g., TLE mean elements) |

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
| `SemiMajorAxis()` | Compute semi-major axis (m) |
| `Eccentricity()` | Compute eccentricity |
| `Inclination()` | Compute inclination (rad) |
| `AscendingNode()` | Compute RAAN (rad) |
| `ArgumentOfPeriapsis()` | Compute argument of periapsis (rad) |
| `TrueAnomaly()` | Compute true anomaly (rad) |
| `MeanAnomaly()` | Compute mean anomaly (rad) |
| `EccentricAnomaly()` | Compute eccentric anomaly (rad) |
| `Period()` | Compute orbital period |
| `MeanMotion()` | Compute mean motion (rad/s) |
| `SpecificOrbitalEnergy()` | Compute vis-viva energy (m²/s²) |
| `SpecificAngularMomentum()` | Compute specific angular momentum vector |
| `EccentricityVector()` | Compute eccentricity vector |
| `AscendingNodeVector()` | Compute ascending node vector |
| `PerigeeVector()` | Compute perigee position vector |
| `ApogeeVector()` | Compute apogee position vector |
| `PerigeeVelocity()` | Compute velocity at perigee (m/s) |
| `ApogeeVelocity()` | Compute velocity at apogee (m/s) |
| `ToKeplerianElements()` | Convert to Keplerian elements |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `ToEquatorial()` | Convert to equatorial coordinates |
| `ToFrame(Frame frame)` | Transform to different reference frame |
| `RelativeTo(ILocalizable target, Aberration aberration)` | Transform to different center |
| `Inverse()` | Invert position and velocity |
| `ToTLE(TLE.Configuration config)` | Convert to TLE format |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1);

// Create state vector
var sv = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),      // Position (m)
    new Vector3(0.0, 8000.0, 0.0),         // Velocity (m/s)
    earth, epoch, Frames.Frame.ICRF
);

// Compute orbital elements
Console.WriteLine($"Semi-major axis: {sv.SemiMajorAxis():F0} m");
Console.WriteLine($"Eccentricity: {sv.Eccentricity():F6}");
Console.WriteLine($"Inclination: {sv.Inclination() * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Period: {sv.Period().TotalMinutes:F2} min");

// Convert to Keplerian elements
var kep = sv.ToKeplerianElements();

// Transform to different frame
var svEcliptic = sv.ToFrame(Frames.Frame.ECLIPTIC_J2000);
```

#### KeplerianElements

Classical Keplerian orbital elements (osculating or mean).

| Constructor | Description |
|------------|-------------|
| `KeplerianElements(double a, double e, double i, double raan, double aop, double m, CelestialItem observer, Time epoch, Frame frame)` | Create from elements |
| `KeplerianElements(..., double perigeeRadius)` | Create parabolic orbit with perigee radius |
| `KeplerianElements(..., OrbitalElementsType elementsType)` | Create with explicit element type |

| Factory Method | Description |
|----------------|-------------|
| `FromOMM(double meanMotion, double e, double i, double raan, double aop, double m, ILocalizable observer, Time epoch, Frame frame)` | Create mean elements from OMM data (units: rev/day for mean motion, degrees for angles) |

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
| `Period()` | Get orbital period |
| `PerigeeRadius()` | Get perigee radius (m) |
| `ApogeeRadius()` | Get apogee radius (m) |
| `PerigeeVector()` | Get perigee position vector |
| `ApogeeVector()` | Get apogee position vector |
| `IsCircular()` | Check if orbit is circular (e ≈ 0) |
| `IsElliptical()` | Check if orbit is elliptical |
| `IsParabolic()` | Check if orbit is parabolic (e = 1) |
| `IsHyperbolic()` | Check if orbit is hyperbolic (e > 1) |
| `AtEpoch(Time epoch)` | Get elements at different epoch |
| `TimeToRadius(double radius)` | Find time to reach specific radius |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1);

// Create osculating Keplerian elements (ISS-like orbit)
var kep = new KeplerianElements(
    a: 6800000.0,                        // Semi-major axis (m)
    e: 0.001,                            // Eccentricity
    i: 51.6 * Constants.Deg2Rad,         // Inclination
    raan: 100.0 * Constants.Deg2Rad,     // RAAN
    aop: 90.0 * Constants.Deg2Rad,       // Argument of periapsis
    m: 0.0,                              // Mean anomaly (at perigee)
    observer: earth,
    epoch: epoch,
    frame: Frames.Frame.ICRF
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
    frame: Frames.Frame.TEME
);

Console.WriteLine($"Elements type: {meanKep.ElementsType}"); // Mean
// Note: Mean elements cannot be directly converted to StateVector
// Use TLE.ToOsculating() or TLE.ToStateVector() for SGP4-based conversion
```

#### EquinoctialElements

Equinoctial orbital elements (avoid singularities for circular/equatorial orbits).

| Property | Description |
|----------|-------------|
| `SemiMajorAxis()` | Semi-major axis (m) |
| `Eccentricity()` | Eccentricity |
| `Inclination()` | Inclination (rad) |
| `AscendingNode()` | RAAN (rad) |
| `ArgumentOfPeriapsis()` | Argument of periapsis (rad) |

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Convert to state vector |
| `ToKeplerianElements()` | Convert to Keplerian elements |
| `EquinoctialEx()` | Get Ex component |
| `EquinoctialEy()` | Get Ey component |
| `Hx()` | Get Hx component |
| `Hy()` | Get Hy component |
| `Lv()` | Get true longitude (rad) |

```csharp
var sv = new StateVector(...);
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
| `Create(OrbitalParameters params, string name, ushort noradId, string cosparId, ...)` | Create TLE from orbital parameters |

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
var now = new Time(2021, 1, 21, 12, 0, 0);
var svAtTime = tle.ToStateVector(now);

// Access TLE mean parameters
Console.WriteLine($"NORAD ID: 25544");
Console.WriteLine($"Inclination: {tle.MeanInclination * Constants.Rad2Deg:F4}°");
Console.WriteLine($"Eccentricity: {tle.MeanEccentricity:F7}");

// Get mean Keplerian elements (preserves mean motion precision)
var meanKep = tle.ToMeanKeplerianElements();
Console.WriteLine($"Mean motion: {meanKep.MeanMotion()} rad/s");

// Create TLE from state vector
var epoch = new Time(2024, 1, 1);
var state = new StateVector(...);
var config = new TLE.Configuration(99999, "MY_SAT", "24001A");
var newTle = state.ToTLE(config);
```

---

### IO.Astrodynamics.CCSDS.OMM

The CCSDS OMM (Orbit Mean-elements Message) module provides support for reading, writing, and converting OMM files conforming to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).

#### Omm

Represents a complete CCSDS Orbit Mean-elements Message.

| Factory Method | Description |
|----------------|-------------|
| `LoadFromFile(string filePath, bool validateSchema, bool validateContent)` | Load OMM from XML file |
| `LoadFromString(string xml, bool validateSchema, bool validateContent)` | Load OMM from XML string |
| `LoadFromStream(Stream stream, bool validateSchema, bool validateContent)` | Load OMM from stream |
| `CreateForSgp4(string objectName, string objectId, DateTime epoch, ...)` | Create OMM for SGP4 propagation |

| Property | Description |
|----------|-------------|
| `Header` | CCSDS header with originator and creation date |
| `Metadata` | Object metadata (name, ID, reference frame, time system) |
| `Data` | Orbital data (mean elements, TLE parameters, spacecraft parameters) |
| `ObjectName` | Shortcut to metadata object name |
| `ObjectId` | Shortcut to metadata object ID (COSPAR ID format: "1998-067A") |
| `IsTleCompatible` | True if OMM contains TLE parameters for conversion |

| Method | Description |
|--------|-------------|
| `SaveToFile(string filePath, bool validateBeforeSave, bool wrapInNdm, bool indent)` | Save OMM to XML file |
| `SaveToString(bool validateBeforeSave, bool wrapInNdm, bool indent)` | Save OMM to XML string |
| `ToTle()` | Convert OMM to TLE (requires TLE parameters in data) |
| `Validate()` | Validate OMM content and return validation result |

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
var newOmm = Omm.CreateForSgp4(
    objectName: "MY_SATELLITE",
    objectId: "2024-001A",
    epoch: DateTime.UtcNow,
    meanMotion: 15.5,           // rev/day
    eccentricity: 0.001,
    inclination: 51.6,          // degrees
    raan: 100.0,                // degrees
    argumentOfPericenter: 90.0, // degrees
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
var xml = omm.SaveToString();
```

#### Round-Trip Conversion

OMM and TLE can be converted back and forth. Note that TLE format has precision limitations (4 decimal places for angles, limited BSTAR precision):

```csharp
// Load OMM
var originalOmm = Omm.LoadFromFile("satellite.omm", validateSchema: false);

// Convert to TLE
var tle = originalOmm.ToTle();

// Use TLE for propagation
var sv = tle.ToStateVector(new Time(2024, 6, 15, 12, 0, 0));

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
// OmmWriter for custom output options
var writer = new OmmWriter
{
    WrapInNdmContainer = true,  // Wrap in <ndm> element
    IndentOutput = true         // Pretty-print XML
};

var xml = writer.WriteToString(omm);
writer.WriteToFile(omm, "output.omm");

// OmmReader for parsing
var reader = new OmmReader();
var omm = reader.ReadFromFile("input.omm");
```

#### OmmValidator

Validates OMM content for physical constraints and CCSDS compliance:

```csharp
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

```csharp
// OMM automatically converts COSPAR ID format during ToTle()
var omm = Omm.LoadFromFile("iss.omm");  // ObjectId: "1998-067A"
var tle = omm.ToTle();                   // Uses "98067A" internally

// TLE.ToOmm() converts back to full format
var newOmm = tle.ToOmm();                // ObjectId: "1998-067A"
```

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
var icrf = Frames.Frame.ICRF;
var ecliptic = Frames.Frame.ECLIPTIC_J2000;

// Create body-fixed frame
var earthFrame = new Frames.Frame("IAU_EARTH");

// Transform state vector
var svICRF = new StateVector(..., frame: icrf);
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
| `GetEphemeris(Time epoch, CelestialBody observer, Frame frame, Aberration aberration)` | Get site state |
| `GetEphemeris(Window window, CelestialBody observer, Frame frame, Aberration aberration, TimeSpan step)` | Get site states |
| `AngularSeparation(Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)` | Compute angular separation |
| `FindWindowsOnDistanceConstraint(...)` | Find distance constraint windows |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation windows |
| `FindWindowsOnIlluminationConstraint(...)` | Find illumination windows |
| `FindDayWindows(Window searchWindow, double twilight)` | Find daylight windows |
| `IlluminationIncidence(Time date, ILocalizable source, Aberration aberration)` | Get solar incidence angle |
| `WriteFrameAsync(FileInfo outputFile)` | Write site frame kernel |

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
var epoch = new Time(2024, 1, 1, 12, 0, 0);
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var horizontal = mySite.GetHorizontalCoordinates(epoch, moon, Aberration.LT);

Console.WriteLine($"Azimuth: {horizontal.Azimuth * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Elevation: {horizontal.Elevation * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Range: {horizontal.Range / 1000:F0} km");

// Find visibility windows
var windows = mySite.FindWindowsOnDistanceConstraint(
    new Window(new Time(2024, 1, 1), new Time(2024, 1, 2)),
    moon, RelationnalOperator.Less, 400000000, Aberration.LT, TimeSpan.FromMinutes(10));
```

#### LaunchSite

Specialized site for launch operations.

| Constructor | Description |
|------------|-------------|
| `LaunchSite(int id, string name, CelestialBody body, IEnumerable<AzimuthRange> allowedAzimuths)` | Create with azimuth constraints |

| Method | Description |
|--------|-------------|
| `IsAzimuthAllowed(double azimuth)` | Check if launch azimuth is allowed |

```csharp
var launchSite = new LaunchSite(100, "MyLaunchSite", earth,
    new[] { new AzimuthRange(45 * Constants.Deg2Rad, 135 * Constants.Deg2Rad) });

if (launchSite.IsAzimuthAllowed(90 * Constants.Deg2Rad))
    Console.WriteLine("East launch is allowed");
```

---

### IO.Astrodynamics.Coordinates

#### Planetodetic

Geodetic coordinates (longitude, latitude, altitude).

| Constructor | Description |
|------------|-------------|
| `Planetodetic(double longitude, double latitude, double altitude)` | Create coordinates |

| Property | Description |
|----------|-------------|
| `Longitude` | Longitude (rad) |
| `Latitude` | Latitude (rad) |
| `Altitude` | Altitude above reference ellipsoid (m) |

| Method | Description |
|--------|-------------|
| `ToPlanetocentric(double flattening, double equatorialRadius)` | Convert to planetocentric |

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

| Method | Description |
|--------|-------------|
| `ToPlanetodetic(double flattening, double equatorialRadius)` | Convert to planetodetic |
| `ToCartesianCoordinates()` | Convert to Cartesian |
| `RadiusFromPlanetocentricLatitude(double equatorialRadius, double flattening)` | Get radius at latitude |

#### Equatorial

Right ascension and declination.

| Property | Description |
|----------|-------------|
| `RightAscension` | Right ascension (rad) |
| `Declination` | Declination (rad) |
| `Distance` | Distance from observer (m) |
| `Epoch` | Epoch of coordinates |

| Method | Description |
|--------|-------------|
| `ToCartesian()` | Convert to Cartesian vector |
| `ToDirection()` | Get unit direction vector |

```csharp
// Get equatorial coordinates
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var sv = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
var eq = sv.ToEquatorial();

Console.WriteLine($"RA: {eq.RightAscension * Constants.Rad2Deg:F4}°");
Console.WriteLine($"Dec: {eq.Declination * Constants.Rad2Deg:F4}°");
```

#### Horizontal

Azimuth, elevation, range (topocentric).

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
| `Spacecraft(int naifId, string name, double dryMass, double maximumThrustPower, Clock clock, OrbitalParameters orbit)` | Create spacecraft |

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF ID (negative) |
| `Name` | Spacecraft name |
| `DryMass` | Mass without fuel (kg) |
| `MaximumThrustPower` | Maximum thrust power (W) |
| `Clock` | Onboard clock |
| `InitialOrbitalParameters` | Initial orbit |
| `StateVectorsRelativeToICRF` | Propagated states |

| Method | Description |
|--------|-------------|
| `AddCircularInstrument(...)` | Add circular FOV instrument |
| `AddRectangularInstrument(...)` | Add rectangular FOV instrument |
| `AddEllipticalInstrument(...)` | Add elliptical FOV instrument |
| `AddEngine(Engine engine)` | Add propulsion engine |
| `AddFuelTank(FuelTank fuelTank)` | Add fuel tank |
| `AddPayload(Payload payload)` | Add payload |
| `GetTotalMass()` | Get total mass including fuel |
| `GetTotalFuel()` | Get total fuel mass |
| `GetTotalISP()` | Get combined specific impulse |
| `GetTotalFuelFlow()` | Get combined fuel flow rate |
| `SetStandbyManeuver(Maneuver maneuver, Time? minEpoch)` | Set maneuver to execute |
| `Propagate(Window window, IEnumerable<CelestialItem> bodies, bool drag, bool srp, TimeSpan step)` | Propagate orbit |
| `PropagateAsync(...)` | Propagate orbit asynchronously |
| `GetOrientation(Frame frame, Time epoch)` | Get spacecraft orientation |
| `WriteOrientation(FileInfo file)` | Write orientation kernel |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1);

// Define initial orbit
var orbit = new KeplerianElements(
    7000000, 0.001, 51.6 * Constants.Deg2Rad, 0, 0, 0,
    earth, epoch, Frames.Frame.ICRF
);

// Create spacecraft
var clock = new Clock("MainClock", 256);
var spacecraft = new Spacecraft(-1001, "MySat", 500.0, 1000.0, clock, orbit);

// Add components
var engine = new Engine("MainEngine", "RCS", 500.0, 300.0);  // thrust, ISP
var tank = new FuelTank("MainTank", "Propellant", "Tank1", 200.0, 100.0);  // capacity, initial
spacecraft.AddEngine(engine);
spacecraft.AddFuelTank(tank);
engine.SetFuelTank(tank);

// Add instrument
spacecraft.AddCircularInstrument(-1001001, "Camera", "Imager",
    10 * Constants.Deg2Rad,              // FOV
    new Vector3(1, 0, 0),                // Boresight
    new Vector3(0, 1, 0),                // Reference vector
    new Vector3(0, 0, 0)                 // Orientation
);

// Propagate
spacecraft.Propagate(
    new Window(epoch, epoch.AddDays(1)),
    new[] { earth },
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    TimeSpan.FromSeconds(60)
);

// Access propagated states
foreach (var sv in spacecraft.StateVectorsRelativeToICRF.Values.Take(5))
{
    Console.WriteLine($"{sv.Epoch}: {sv.Position.Magnitude():F0} m");
}
```

#### Engine

Propulsion engine.

| Constructor | Description |
|------------|-------------|
| `Engine(string name, string model, double thrust, double isp)` | Create engine |

| Property | Description |
|----------|-------------|
| `Name` | Engine name |
| `Model` | Engine model |
| `Thrust` | Thrust force (N) |
| `ISP` | Specific impulse (s) |
| `FuelFlow` | Fuel flow rate (kg/s) |

| Method | Description |
|--------|-------------|
| `SetFuelTank(FuelTank tank)` | Associate fuel tank |
| `Ignite(Vector3 deltaV)` | Execute burn |

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
| `IsInFOV(Time date, ILocalizable target, Aberration aberration)` | Check if target in FOV |
| `FindWindowsInFieldOfViewConstraint(...)` | Find visibility windows |
| `GetBoresightInSpacecraftFrame()` | Get boresight in spacecraft frame |
| `GetBoresightInICRFFrame(Time date)` | Get boresight in ICRF |

---

### IO.Astrodynamics.Maneuver

#### Launch

Launch window computation.

| Constructor | Description |
|------------|-------------|
| `Launch(LaunchSite site, LaunchSite recoverySite, OrbitalParameters targetOrbit, double inclination)` | Create launch scenario |

| Method | Description |
|--------|-------------|
| `FindLaunchWindows(Window searchWindow)` | Find launch opportunities |
| `GetInertialAscendingAzimuthLaunch()` | Get ascending node launch azimuth |
| `GetInertialDescendingAzimuthLaunch()` | Get descending node launch azimuth |
| `GetInertialInsertionVelocity()` | Get required insertion velocity |

#### Maneuver (Base class)

Base class for orbital maneuvers.

| Method | Description |
|--------|-------------|
| `CanExecute(StateVector stateVector)` | Check if maneuver can execute |
| `TryExecute(StateVector stateVector)` | Execute maneuver if conditions met |
| `SetNextManeuver(Maneuver maneuver)` | Chain maneuvers |

#### Attitude maneuvers

- `ProgradeAttitude`: Orient along velocity vector
- `RetrogradeAttitude`: Orient opposite to velocity
- `ZenithAttitude`: Orient away from central body
- `NadirAttitude`: Orient toward central body
- `InstrumentPointingAttitude`: Point instrument at target

#### Impulse maneuvers

- `ApogeeHeightManeuver`: Change apogee altitude
- `PerigeeHeightManeuver`: Change perigee altitude
- `PlaneAlignmentManeuver`: Change orbital plane
- `ApsidalAlignmentManeuver`: Rotate line of apsides
- `PhasingManeuver`: Change orbital period for phasing
- `CombinedManeuver`: Execute combined plane change and height change

---

### IO.Astrodynamics.Maneuver.Lambert

#### LambertSolver

Solve Lambert's problem for transfer orbits.

| Method | Description |
|--------|-------------|
| `Solve(bool isRetrograde, OrbitalParameters initial, OrbitalParameters target, CelestialItem center, ushort maxRevolution)` | Solve Lambert problem |

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1);

// Define departure and arrival states
var departure = new StateVector(...);
var arrival = new StateVector(...);

// Solve Lambert problem
var solver = new LambertSolver();
var result = solver.Solve(false, departure, arrival, earth, 0);

// Get zero-revolution solution
var solution = result.GetZeroRevolutionSolution();
Console.WriteLine($"Delta-V at departure: {solution.DepartureVelocity.Magnitude():F1} m/s");
Console.WriteLine($"Delta-V at arrival: {solution.ArrivalVelocity.Magnitude():F1} m/s");
```

---

### IO.Astrodynamics.Propagator

#### SpacecraftPropagator

Numerical orbit propagator with perturbations.

| Constructor | Description |
|------------|-------------|
| `SpacecraftPropagator(Window window, Spacecraft spacecraft, IEnumerable<CelestialItem> bodies, bool drag, bool srp, TimeSpan step)` | Create propagator |

| Method | Description |
|--------|-------------|
| `Propagate()` | Execute propagation |

#### TLEPropagator

SGP4/SDP4 propagator for TLE elements.

| Constructor | Description |
|------------|-------------|
| `TLEPropagator(Window window, TLE tle, TimeSpan step)` | Create propagator |

| Method | Description |
|--------|-------------|
| `Propagate()` | Execute propagation |

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
| `FromPlanetodetic(altitude, latitude, longitude, epoch)` | Create full context |

#### EarthStandardAtmosphere

U.S. Standard Atmosphere 1976 model. Simple analytical model valid up to ~86 km. Uses only altitude from context.

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
// Use nominal conditions
var model = new Nrlmsise00Model();

// Or specify space weather
var spaceWeather = new SpaceWeather { F107 = 150, F107A = 150, Ap = 4 };
var model = new Nrlmsise00Model(spaceWeather);

// Or use preset conditions
var solarMinModel = new Nrlmsise00Model(SpaceWeather.SolarMinimum);
var solarMaxModel = new Nrlmsise00Model(SpaceWeather.SolarMaximum);

// Full context required
var context = AtmosphericContext.FromPlanetodetic(
    altitude: 400000,                           // 400 km
    geodeticLatitude: 0,                        // Equator (rad)
    geodeticLongitude: 0,                       // Prime meridian (rad)
    epoch: new Time(2024, 6, 21, 12, 0, 0)
);

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

Simple Mars atmospheric model using altitude only.

```csharp
var model = new MarsStandardAtmosphere();
var context = AtmosphericContext.FromAltitude(50000);  // 50 km
var atmosphere = model.GetAtmosphere(context);
Console.WriteLine($"Mars atmosphere: ρ={atmosphere.Density:E3} kg/m³");
```

#### CelestialBody Integration

`CelestialBody` provides convenient `GetAtmosphere` methods that automatically select the best available model.

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;

// Simple: altitude-only (uses EarthStandardAtmosphere)
var atm1 = earth.GetAtmosphere(10000);  // 10 km altitude

// Full context: automatically uses NRLMSISE-00 for Earth
var context = AtmosphericContext.FromPlanetodetic(
    altitude: 400000,
    geodeticLatitude: 45 * Constants.Deg2Rad,
    geodeticLongitude: -75 * Constants.Deg2Rad,
    epoch: new Time(2024, 6, 21, 12, 0, 0)
);
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
| Quick calculations below 86 km | `EarthStandardAtmosphere` |
| High-altitude modeling (> 100 km) | `Nrlmsise00Model` |
| Time-varying density (drag analysis) | `Nrlmsise00Model` |
| Space weather effects | `Nrlmsise00Model` |
| Mars atmospheric entry | `MarsStandardAtmosphere` |
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
| `Magnitude()` | Get vector length |
| `MagnitudeSquared()` | Get squared length |
| `Normalize()` | Get unit vector |
| `Cross(Vector3 other)` | Cross product |
| `Angle(Vector3 other)` | Angle between vectors (rad) |
| `Inverse()` | Negate vector |
| `To(Vector3 other)` | Vector from this to other |
| `Rotate(Quaternion q)` | Rotate by quaternion |
| `LinearInterpolation(Vector3 other, double t)` | Linear interpolation |

| Operators | Description |
|-----------|-------------|
| `+`, `-` | Vector addition/subtraction |
| `*` (double) | Scalar multiplication |
| `*` (Vector3) | Dot product |
| `/` | Scalar division |

```csharp
var v1 = new Vector3(1, 0, 0);
var v2 = new Vector3(0, 1, 0);

var cross = v1.Cross(v2);           // (0, 0, 1)
var dot = v1 * v2;                  // 0
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
| `Normalize()` | Get unit quaternion |
| `Conjugate()` | Get conjugate |
| `ToEuler()` | Convert to Euler angles |
| `SLERP(Quaternion q, double t)` | Spherical linear interpolation |
| `Lerp(Quaternion q, double t)` | Linear interpolation |

#### Matrix

Matrix operations.

| Constructor | Description |
|------------|-------------|
| `Matrix(double[,] data)` | Create from 2D array |

| Method | Description |
|--------|-------------|
| `Get(int row, int col)` | Get element |
| `Set(int row, int col, double value)` | Set element |
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
| `AddCelestialBodyInfo(params CelestialBody[] bodies)` | Add celestial body data |
| `AddStateVector(int naifId, Time date, StateVector sv)` | Add state vector |
| `AddStateOrientationToICRF(Frame frame, Time date, StateOrientation orientation)` | Add orientation |

```csharp
// For testing without SPICE kernels
var memoryProvider = new MemoryDataProvider();
Configuration.Instance.SetDataProvider(memoryProvider);

memoryProvider.AddCelestialBodyInfo(customEarth);
memoryProvider.AddStateVector(399, epoch, earthState);
```

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
| `Greater` | Value > threshold |
| `Less` | Value < threshold |
| `Equal` | Value = threshold |
| `AbsMin` | Absolute minimum |
| `AbsMax` | Absolute maximum |
| `LocalMin` | Local minimum |
| `LocalMax` | Local maximum |

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
API.Instance.LoadKernels(new DirectoryInfo("kernels"));

// Required kernel types:
// - LSK (leap seconds): naif0012.tls
// - SPK (ephemeris): de440.bsp, mar097.bsp
// - PCK (planetary constants): pck00010.tpc
// - FK (frames): earth_assoc_itrf93.tf

// Clean up when done
API.Instance.ClearKernels();
```

### Time Handling

```csharp
// Always be explicit about time frames
var utc = new Time(2024, 1, 1, 12, 0, 0);       // Defaults to TDB
var explicit = new Time(new DateTime(2024, 1, 1), TimeFrame.UTCFrame);

// Convert as needed
var tdb = utc.ToTDB();
var tai = utc.ToTAI();

// Use TDB for dynamical calculations
var state = earth.GetEphemeris(epoch.ToTDB(), sun, Frame.ICRF, Aberration.None);
```

### Reference Frame Awareness

```csharp
// ICRF/J2000: Inertial frame for most calculations
var sv = new StateVector(..., frame: Frames.Frame.ICRF);

// Body-fixed frames for surface operations
var earthFrame = new Frames.Frame("IAU_EARTH");
var bodyFixed = sv.ToFrame(earthFrame);

// TEME for TLE propagation (handled automatically)
var tleSv = tle.ToStateVector();  // Returns in TEME
var icrfSv = tleSv.ToFrame(Frames.Frame.ICRF);
```

### Error Handling

```csharp
try
{
    var state = body.GetEphemeris(epoch, observer, frame, Aberration.None);
}
catch (InvalidOperationException ex)
{
    // Likely kernel not loaded or epoch out of coverage
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
var start = new Time(2024, 1, 1);
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
var start = new Time(2024, 3, 1);
var end = start.AddHours(12);

// Create mission and scenario
var mission = new Mission("Rendezvous");
var scenario = new Scenario("RendezvousPhase", mission, new Window(start, end));
scenario.AddCelestialItem(earth);

// Define target spacecraft orbit
var targetOrbit = new StateVector(
    new Vector3(4390853.7, 5110607.0, 917659.9),
    new Vector3(-4979.5, 3033.3, 6933.2),
    earth, start, Frames.Frame.ICRF
);

// Define chaser spacecraft initial orbit (parking orbit)
var chaserOrbit = new StateVector(
    new Vector3(5056554.2, 4395595.5, 0.0),
    new Vector3(-3708.6, 4266.3, 6736.9),
    earth, start, Frames.Frame.ICRF
);

// Create target spacecraft
var targetClock = new Clock("TargetClock", 65536);
var target = new Spacecraft(-1001, "Target", 500.0, 1000.0, targetClock, targetOrbit);
scenario.AddSpacecraft(target);

// Create chaser spacecraft with propulsion
var chaserClock = new Clock("ChaserClock", 65536);
var chaser = new Spacecraft(-1002, "Chaser", 1000.0, 10000.0, chaserClock, chaserOrbit);

// Add fuel tank and engine
var fuelTank = new FuelTank("MainTank", "Model1", "SN001", 9000.0, 9000.0);
var engine = new Engine("MainEngine", "Model1", "SN001", 450.0, 50.0, fuelTank);
chaser.AddFuelTank(fuelTank);
chaser.AddEngine(engine);

// Chain maneuvers: plane alignment -> apsidal alignment -> phasing -> circularization
var maneuver1 = new PlaneAlignmentManeuver(
    new Time(DateTime.MinValue, TimeFrame.TDBFrame),
    TimeSpan.Zero, targetOrbit, engine
);

maneuver1
    .SetNextManeuver(new ApsidalAlignmentManeuver(
        new Time(DateTime.MinValue, TimeFrame.TDBFrame),
        TimeSpan.Zero, targetOrbit, engine))
    .SetNextManeuver(new PhasingManeuver(
        new Time(DateTime.MinValue, TimeFrame.TDBFrame),
        TimeSpan.Zero, targetOrbit, 1, engine))
    .SetNextManeuver(new ApogeeHeightManeuver(
        earth, new Time(DateTime.MinValue, TimeFrame.TDBFrame),
        TimeSpan.Zero, targetOrbit.SemiMajorAxis(), engine));

// Set the maneuver sequence
chaser.SetStandbyManeuver(maneuver1);
scenario.AddSpacecraft(chaser);

// Run the simulation
var summary = await scenario.SimulateAsync(
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    TimeSpan.FromSeconds(1.0)
);

// Read maneuver results
var executedManeuver = chaser.InitialManeuver;
Console.WriteLine($"Maneuver 1 start: {executedManeuver.ManeuverWindow?.StartDate}");
Console.WriteLine($"Maneuver 1 delta-V: {((ImpulseManeuver)executedManeuver).DeltaV.Magnitude():F1} m/s");
Console.WriteLine($"Fuel burned: {executedManeuver.FuelBurned:F1} kg");
Console.WriteLine($"Total fuel consumption: {summary.SpacecraftSummaries.First().FuelConsumption:F1} kg");
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
    earth, departureEpoch, Frames.Frame.ICRF
);

// Define arrival (Moon position in 3 days)
var arrivalEpoch = departureEpoch.AddDays(3);
var moonState = moon.GetEphemeris(arrivalEpoch, earth, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();

// Solve Lambert's problem
var solver = new LambertSolver();
var result = solver.Solve(
    isRetrograde: false,
    departureState,
    moonState,
    earth,
    maxRevolutions: 0
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
    earth, departureEpoch, Frames.Frame.ICRF
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
var searchStart = new Time(2024, 6, 2);

// Create launch site (Kennedy Space Center)
var launchSite = new LaunchSite(
    id: 33,
    name: "KSC",
    body: earth,
    coordinates: new Planetodetic(
        -81.0 * Constants.Deg2Rad,   // Longitude
        28.5 * Constants.Deg2Rad,    // Latitude
        0.0                           // Altitude
    ),
    azimuthRange: new AzimuthRange(0.0, 2 * Math.PI)  // All azimuths allowed
);

// Create recovery site
var recoverySite = new Site(34, "Recovery", earth,
    new Planetodetic(-81.0 * Constants.Deg2Rad, 28.5 * Constants.Deg2Rad, 0.0));

// Define target orbit (ISS-like)
var targetOrbit = new StateVector(
    new Vector3(-1.144E+06, 4.905E+06, 4.553E+06),
    new Vector3(-5.588E+03, -4.213E+03, 3.126E+03),
    earth, searchStart, Frames.Frame.ICRF
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
    new Time(2024, 1, 1),
    new Time(2024, 12, 31)
);

// Find lunar eclipses (Moon occulted by Earth as seen from Sun)
var lunarEclipses = sun.FindWindowsOnOccultationConstraint(
    searchWindow,
    occultingBody: earth,
    occultingShape: ShapeType.Ellipsoid,
    occultedBody: moon,
    occultedShape: ShapeType.Ellipsoid,
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
var start = new Time(2024, 6, 10);
var end = start.AddHours(2);

// Create mission and scenario
var mission = new Mission("EarthObservation");
var scenario = new Scenario("Observation", mission, new Window(start, end));
scenario.AddCelestialItem(earth);

// Define spacecraft orbit
var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.2, 0.0),
    earth, start, Frames.Frame.ICRF
);

// Create spacecraft with camera
var clock = new Clock("Clock1", 65536);
var spacecraft = new Spacecraft(-179, "Observer", 1000.0, 3000.0, clock, orbit);

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
await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));

// Find windows when Earth is in the camera's field of view
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

// After running a scenario simulation...
var mission = new Mission("Visualization");
var scenario = new Scenario("Demo", mission, new Window(start, end));
// ... add celestial items, spacecraft, run simulation ...
await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));

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
var iss = new Spacecraft(-25544, "ISS", 420000.0, 1000.0, clock, issTLE);

// Track from a ground station
var goldstone = new Site(13, "DSS-13", earth);

var trackStart = new Time(2024, 6, 3);
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
    RelationnalOperator.Less,
    2000000,  // Within 2000 km
    Aberration.LT,
    TimeSpan.FromMinutes(1)
);
```

### Deep Space Propagation with Multiple Perturbations

High-fidelity propagation including gravitational effects from multiple bodies:

```csharp
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Time.J2000TDB);
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Time.J2000TDB);
var sun = new CelestialBody(Stars.Sun);

var start = Time.J2000TDB;
var end = start.AddDays(25);

var mission = new Mission("DeepSpaceTest");
var scenario = new Scenario("LunarFlyby", mission, new Window(start, end));

// Start at Moon's position to test N-body accuracy
var moonState = moon.GetEphemeris(start, earth, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();

var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-1001, "Probe", 100.0, 10000.0, clock, moonState);

scenario.AddSpacecraft(spacecraft);

// Add gravitational perturbations from multiple bodies
scenario.AddCelestialItem(sun);
scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(new Barycenter(5));  // Jupiter barycenter
scenario.AddCelestialItem(new Barycenter(6));  // Saturn barycenter

// Propagate with 5-minute step for accuracy
var summary = await scenario.SimulateAsync(
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    TimeSpan.FromMinutes(5)
);

// Compare spacecraft to Moon position at end
var spcFinal = spacecraft.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();
var moonFinal = moon.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();

var positionError = (spcFinal.Position - moonFinal.Position).Magnitude();
var velocityError = (spcFinal.Velocity - moonFinal.Velocity).Magnitude();

Console.WriteLine($"25-day propagation accuracy:");
Console.WriteLine($"  Position error: {positionError:F1} m");
Console.WriteLine($"  Velocity error: {velocityError:F4} m/s");
```

### Earth Observation with Attitude Maneuvers

Point instruments at ground targets during observation passes:

```csharp
using IO.Astrodynamics.Maneuver;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = new Time(2024, 1, 1);
var end = start.AddDays(1);

var mission = new Mission("EarthObs");
var scenario = new Scenario("TargetPointing", mission, new Window(start, end));
scenario.AddCelestialItem(earth);

// Define orbit
var orbit = new KeplerianElements(
    11800000.0, 0.3, 1.0, 0.0, 0.0, 0.0,
    earth, start, Frames.Frame.ICRF
);

// Create spacecraft
var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-334, "Observer", 1000.0, 2000.0, clock, orbit);

// Add antenna instrument
spacecraft.AddCircularInstrument(-334100, "Antenna", "HighGain",
    0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

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
);

spacecraft.SetStandbyManeuver(pointingManeuver);

// Simulate
await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));

Console.WriteLine($"Pointing maneuver executed at: {pointingManeuver.ManeuverWindow?.StartDate}");
```

---

## Version Information

- Framework: .NET 8.0 / .NET 10.0
- SPICE Toolkit: CSPICE N0067
- License: LGPL 3.0

Contact : [contact@io-aerospace.org](contact@io-aerospace.org)
