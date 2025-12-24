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

Classical Keplerian orbital elements.

| Constructor | Description |
|------------|-------------|
| `KeplerianElements(double a, double e, double i, double raan, double aop, double m, CelestialItem observer, Time epoch, Frame frame)` | Create from elements |
| `KeplerianElements(..., double perigeeRadius)` | Create parabolic orbit with perigee radius |

| Property | Description |
|----------|-------------|
| `A` | Semi-major axis (m) |
| `E` | Eccentricity |
| `I` | Inclination (rad) |
| `RAAN` | Right ascension of ascending node (rad) |
| `AOP` | Argument of periapsis (rad) |
| `M` | Mean anomaly (rad) |

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

// Create Keplerian elements (ISS-like orbit)
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

Two-Line Element set for Earth-orbiting objects.

| Constructor | Description |
|------------|-------------|
| `TLE(string name, string line1, string line2)` | Parse from standard 3-line format |

| Property | Description |
|----------|-------------|
| `Name` | Object name |
| `Line1` | First line of TLE |
| `Line2` | Second line of TLE |
| `Epoch` | Epoch of elements |
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
| `ToStateVector()` | Propagate to TLE epoch (SGP4/SDP4) |
| `ToStateVector(Time epoch)` | Propagate to specified epoch |
| `ToKeplerianElements()` | Get osculating Keplerian elements |
| `ToMeanKeplerianElements()` | Get mean Keplerian elements |
| `AtEpoch(Time epoch)` | Get propagated orbital parameters |
| `Create(OrbitalParameters params, string name, ushort noradId, string cosparId, ...)` | Create TLE from orbital parameters |

```csharp
// Parse TLE
var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

// Get current state (SGP4 propagation)
var now = new Time(2021, 1, 21, 12, 0, 0);
var sv = tle.ToStateVector(now);

// Access TLE parameters
Console.WriteLine($"NORAD ID: 25544");
Console.WriteLine($"Inclination: {tle.MeanInclination * Constants.Rad2Deg:F4}°");
Console.WriteLine($"Eccentricity: {tle.MeanEccentricity:F7}");

// Create TLE from state vector
var epoch = new Time(2024, 1, 1);
var state = new StateVector(...);
var config = new TLE.Configuration(99999, "MY_SAT", "24001A");
var newTle = state.ToTLE(config);
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

#### IAtmosphericModel

Interface for atmospheric models.

| Method | Description |
|--------|-------------|
| `GetTemperature(IAtmosphericContext context)` | Get temperature (°C) |
| `GetPressure(IAtmosphericContext context)` | Get pressure (kPa) |
| `GetDensity(IAtmosphericContext context)` | Get density (kg/m³) |

#### EarthStandardAtmosphere

U.S. Standard Atmosphere 1976 model.

```csharp
var model = new EarthStandardAtmosphere();
var context = AtmosphericContext.FromAltitude(10000);  // 10 km

var temp = model.GetTemperature(context);    // °C
var pressure = model.GetPressure(context);    // kPa
var density = model.GetDensity(context);      // kg/m³
```

#### Nrlmsise00Model

NRLMSISE-00 empirical atmosphere model (0-2000+ km).

| Constructor | Description |
|------------|-------------|
| `Nrlmsise00Model(SpaceWeather weather)` | Create with space weather data |

```csharp
var spaceWeather = new SpaceWeather { F107 = 150, F107A = 150, Ap = 4 };
var model = new Nrlmsise00Model(spaceWeather);

var context = new AtmosphericContext
{
    Altitude = 400000,  // 400 km
    Epoch = new Time(2024, 6, 21, 12, 0, 0),
    GeodeticLatitude = 0,
    GeodeticLongitude = 0
};

var density = model.GetDensity(context);
Console.WriteLine($"Density at 400 km: {density:E3} kg/m³");
```

#### MarsStandardAtmosphere

Mars atmospheric model.

```csharp
var mars = new CelestialBody(PlanetsAndMoons.MARS);
var model = new MarsStandardAtmosphere();
var context = AtmosphericContext.FromAltitude(50000);
var density = model.GetDensity(context);
```

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

## Version Information

- Framework: .NET 8.0 / .NET 10.0
- SPICE Toolkit: CSPICE N0067
- License: LGPL 3.0

Contact : [contact@io-aerospace.org](contact@io-aerospace.org)
