# IO.Astrodynamics Framework - Developer Guide

## Table of Contents

1. [Introduction](#introduction)
2. [What IO.Astrodynamics Does](#what-ioastrodynamics-does)
3. [What IO.Astrodynamics Doesn't Do](#what-ioastrodynamics-doesnt-do)
4. [Quick Start](#quick-start)
5. [Core Features](#core-features)
   - [Working with Time Systems](#working-with-time-systems)
   - [Celestial Bodies and Solar System Objects](#celestial-bodies-and-solar-system-objects)
   - [Orbital Parameters](#orbital-parameters)
   - [State Vectors and Propagation](#state-vectors-and-propagation)
   - [Spacecraft Modeling](#spacecraft-modeling)
   - [Maneuvers and Orbital Changes](#maneuvers-and-orbital-changes)
   - [Launch Windows](#launch-windows)
   - [Atmospheric Modeling](#atmospheric-modeling)
   - [Reference Frames and Coordinates](#reference-frames-and-coordinates)
   - [Ephemeris Calculations](#ephemeris-calculations)
   - [Occultations and Illumination](#occultations-and-illumination)
   - [Field of View and Visibility](#field-of-view-and-visibility)
6. [Real-World Examples](#real-world-examples)
7. [FAQ and Common Issues](#faq-and-common-issues)

---

## Introduction

**IO.Astrodynamics** is a comprehensive .NET framework for astrodynamics, orbital mechanics, and space mission planning. Built on NASA's proven SPICE toolkit, it provides high-fidelity calculations for orbital mechanics, ephemeris computations, spacecraft modeling, and mission analysis.

### Key Characteristics

- **High-Fidelity**: Uses NASA's SPICE (CSPICE) for precise ephemeris and geometry calculations
- **.NET Native**: Full C# implementation targeting .NET 8/10
- **Thread-Safe**: Safe for concurrent operations with proper locking mechanisms
- **Production-Ready**: 95%+ test coverage with comprehensive unit tests
- **Cross-Platform**: Works on Windows, Linux, and macOS

### Target Audience

This framework is designed for:
- Aerospace engineers building mission planning tools
- Scientists performing orbital mechanics analysis
- Developers creating space simulation software
- Researchers studying celestial mechanics

---

## What IO.Astrodynamics Does

IO.Astrodynamics provides comprehensive capabilities for astrodynamics calculations:

### Orbital Mechanics & Propagation
- **Orbital Element Representations**: Keplerian, Equinoctial, TLE (Two-Line Elements), State Vectors with conversions between all types
- **Orbit Propagation**: 2-body analytical propagation and N-body numerical integration
- **Numerical Integrators**: VV (Velocity Verlet) integrator for high-precision propagation
- **TLE Propagation**: SGP4/SDP4 propagation from Two-Line Element sets
- **Initial Orbit Determination**: Gauss method from optical observations (right ascension/declination)
- **Orbit Queries**: Period, apogee, perigee, mean motion, eccentric anomaly, true anomaly calculations

### Spacecraft Systems
- **Spacecraft Modeling**: Complete spacecraft definition with mass properties, dry mass tracking
- **Propulsion Systems**: Multi-tank fuel management, engines with ISP and thrust characteristics
- **Instruments & Sensors**: FOV modeling (circular, elliptical, rectangular) with boresight and orientation
- **Spacecraft Clocks**: Onboard clock simulation for time tagging
- **Payloads**: Custom payload definitions
- **Attitude Control**: Prograde, retrograde, nadir, zenith, instrument pointing, custom quaternion attitudes
- **Spacecraft Frames**: Body-fixed reference frames for each spacecraft

### Orbital Maneuvers
- **Impulsive Maneuvers**: Instantaneous delta-V application
- **Apogee/Perigee Maneuvers**: Orbit circularization and altitude changes
- **Apsidal Alignment**: Argument of periapsis modifications
- **Plane Changes**: Inclination and RAAN modifications
- **Phasing Maneuvers**: Orbital phasing for rendezvous
- **Combined Maneuvers**: Optimized simultaneous altitude and plane changes
- **Low Thrust Maneuvers**: Continuous thrust trajectory optimization
- **Lambert Solvers**: Two-point boundary value problem solver for transfer trajectories
- **Launch Windows**: Optimal launch opportunity calculations with azimuth and velocity
- **Maneuver Execution**: TryExecute with fuel consumption tracking and attitude computation

### Ephemeris & SPICE Integration
- **High-Precision Ephemeris**: Read position and velocity of any object relative to any observer
- **SPICE Kernel Management**: Load/unload kernels (SPK, PCK, LSK, FK, IK, CK, SCLK)
- **Ephemeris Writing**: Generate SPICE SPK kernels from state vectors
- **Orientation Writing**: Generate SPICE CK kernels from attitude data
- **Batch Ephemeris Reading**: Efficient bulk ephemeris queries over time windows
- **Frame Transformations**: Transform state vectors between any reference frames
- **Aberration Corrections**: None, Light-Time, Light-Time+Stellar, Transmission cases
- **NAIF ID Management**: Support for official and custom NAIF body IDs

### Geometry Finders (Event Search)
- **Distance Constraints**: Find when distance between bodies meets conditions (>, <, =, ≥, ≤, ≠)
- **Occultation Searches**: Detect eclipses, transits, occultations (full, annular, partial, any)
- **Coordinate Constraints**: Search for specific coordinate values (latitude, longitude, altitude, RA, Dec, range, etc.)
- **Illumination Constraints**: Solar illumination angle searches (incidence, emission, phase angles)
- **Field of View Searches**: Determine when targets are visible in instrument FOV
- **Custom Geometry Searches**: Generic window finder with user-defined conditions
- **High-Precision Refinement**: Binary search refinement for event timing

### Time Systems & Windows
- **Time Frames**: UTC, TDB (Barycentric Dynamical), TAI (Atomic), GPS, TDT (Terrestrial Dynamical), Local Time
- **Time Conversions**: Seamless conversion between all time systems with leap second handling
- **Time Windows**: Period definitions for searches and analysis
- **Julian Date Support**: J2000 epoch reference and Julian date calculations
- **Epoch Arithmetic**: Add/subtract time spans, compute durations

### Coordinate Systems & Reference Frames
- **Reference Frames**: ICRF, J2000, Galactic, body-fixed frames, custom frames
- **Planetodetic Coordinates**: Latitude, longitude, altitude (geodetic)
- **Planetocentric Coordinates**: Spherical coordinates relative to body center
- **Horizontal Coordinates**: Azimuth, elevation, range (observer-local)
- **Equatorial Coordinates**: Right ascension, declination (celestial sphere)
- **Cartesian Conversions**: Convert between all coordinate systems
- **Frame Transformations**: State vector and orientation transformations with angular velocity

### Atmospheric Modeling
- **Earth Standard Atmosphere**: U.S. Standard Atmosphere 1976 (valid 0-86 km)
- **NRLMSISE-00**: High-fidelity empirical model (valid 0-2000+ km)
  - Temperature, pressure, density from ground to exosphere
  - Space weather effects (F10.7 solar flux, Ap geomagnetic index)
  - Configurable switches for atmospheric variations
  - Direct access for advanced users (Calculate, CalculateThermosphere, etc.)
  - Altitude finding at target pressure
- **Mars Standard Atmosphere**: Simplified analytical Mars atmosphere model
- **Context Support**: Full atmospheric context (altitude, lat/lon, time, local solar time)
- **Atmospheric Drag Calculations**: Integration with propagator forces

### Gravitational Models & Forces
- **Geopotential Models**: High resolution Spherical harmonic gravity models
- **Geopotential Coefficients**: Read and apply coefficient files
- **N-Body Gravity**: Multi-body gravitational acceleration
- **Atmospheric Drag**: Altitude-dependent drag force with coefficient and area
- **Solar Radiation Pressure**: SRP force modeling with reflectivity coefficients
- **Custom Forces**: Extensible force model framework
- **Gravitational Parameters**: GM (gravitational parameter) for all bodies

### Celestial Bodies & Solar System Objects
- **Planets & Moons**: All major planets and 200+ moons with physical properties
- **Barycenters**: Solar system, planetary system barycenters
- **Lagrange Points**: L1-L5 for Earth-Moon, Sun-Earth systems
- **Asteroids**: Support for asteroid ephemeris
- **Comets**: Support for comet ephemeris
- **Stars**: Sun and stellar objects
- **Ground Stations**: Site definitions with coordinates
- **Launch Sites**: Launch complex modeling with azimuth constraints
- **Body Properties**: Mass, radius, GM, flattening, frame information

### Mathematical Utilities
- **Vector Mathematics**: Vector3 operations (dot, cross, magnitude, normalization)
- **Quaternion Mathematics**: Rotations, conjugates, SLERP interpolation
- **Matrix Operations**: Matrix multiplication, inversion, transpose
- **Plane Geometry**: Plane definitions and intersections
- **Numerical Solvers**:
  - Newton-Raphson method
  - Bisection method
  - Jacobian matrix computation
- **Interpolation**: Lagrange polynomial interpolation
- **Special Functions**: Legendre polynomials, special mathematical functions
- **Angle Utilities**: Conversions between degrees/radians, angle normalization

### Mission Planning & Simulation
- **Mission Scenarios**: Complete mission timeline definition
- **Scenario Simulation**: Execute missions with propagation and maneuvers
- **Launch Analysis**: Launch window calculations with azimuth and insertion velocity
- **Recovery Site Planning**: Splashdown and landing site prediction
- **Mission Summaries**: Automated reporting of scenario results
- **Multi-Spacecraft Missions**: Coordinate multiple spacecraft in single scenario

### Data Export & Interoperability
- **Cosmographia Export**: 3D visualization scene generation for Cosmographia tool
  - Spacecraft trajectories
  - Sensor FOV visualization
  - Ground site markers
  - Observation windows
- **SPICE Kernel Generation**: Create SPK (ephemeris) and CK (orientation) kernels
- **PDS V4 Export**: Planetary Data System archive-compliant metadata
- **JSON Serialization**: Export mission data in JSON format
- **Profile Management**: Configuration profiles for different mission types

### Physics & Astrodynamics Calculations
- **Tsiolkovsky Equation**: Rocket equation calculations for delta-V and mass ratios
- **Orbital Energy**: Specific orbital energy computation
- **Angular Momentum**: Orbital angular momentum vectors
- **Vis-Viva Equation**: Velocity at any point in orbit
- **Sphere of Influence**: SOI calculations for multi-body transfers
- **Escape Velocity**: Body escape velocity calculations
- **Circular Velocity**: Local circular orbit velocity

---

## What IO.Astrodynamics Doesn't Do

To set proper expectations, here's what this framework does **not** provide:

### Not Included

- **Trajectory Visualization**: No built-in GUI or 3D rendering (use Cosmographia exporter for visualization)
- **Orbit Determination from Real Telemetry**: No TLE fetching from APIs or real-time telemetry processing
- **Astrochemistry/Spectroscopy**: Pure mechanics focus, no chemical or spectral analysis
- **Attitude Dynamics Simulation**: Only kinematic attitudes, no full dynamics with moments of inertia
- **Re-entry Heating Calculations**: No thermal analysis or material stress calculations
- **Communication Link Budget**: No RF propagation or antenna gain calculations
- **Ground Segment Operations**: No mission control system features
- **Real-Time Operations**: Not designed for closed-loop real-time spacecraft control

### Design Philosophy

IO.Astrodynamics focuses on:
- **Accuracy** over speed (though performance is optimized)
- **Precision** over approximations (uses proven NASA algorithms)
- **Completeness** over simplicity (comprehensive feature set)
- **Library** over application (you build the application)

---

## Quick Start

### Installation

```bash
# Build from source
git clone <repository-url>
cd IO.Astrodynamics.Net
dotnet build IO.Astrodynamics.sln

# Or install as NuGet package (if published)
dotnet add package IO.Astrodynamics
```

### Prerequisites

1. **SPICE Kernels**: Download required ephemeris kernels from [NAIF](https://naif.jpl.nasa.gov/naif/data.html)
2. **.NET 8 or 10**: Ensure you have the SDK installed

### Your First Program

```csharp
using System;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

// Load SPICE kernels (required for all operations)
API.Instance.LoadKernels(new DirectoryInfo("path/to/kernels"));

// Get Earth and Moon as CelestialBody objects
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF,
    new TimeSystem.Time(2000, 1, 1, 12, 0, 0));

// Define epoch in TDB (used for ephemeris calculations)
var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);  // Defaults to TDB

// Get Moon's state vector relative to Earth
var ephemeris = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None);

Console.WriteLine($"Moon position: {ephemeris.Position}");
Console.WriteLine($"Moon velocity: {ephemeris.Velocity}");
```

### Essential Patterns

**Always load kernels first:**
```csharp
API.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
```

**Use appropriate time frames:**
```csharp
// Create time in specific frame
var utcTime = new TimeSystem.Time(2025, 1, 1, frame: TimeFrame.UTCFrame);
var tdbTime = utcTime.ToTDB();  // Convert to TDB for ephemeris calculations

// Create from string (auto-detects frame from suffix)
var fromString = new TimeSystem.Time("2025-01-01T12:00:00.0000000Z");  // UTC
var tdbFromString = new TimeSystem.Time("2025-01-01T12:00:00.0000000 TDB");

// Create from elapsed seconds since J2000
var fromSeconds = TimeSystem.Time.CreateTDB(0.0);  // J2000 epoch
```

**Specify reference frames explicitly:**
```csharp
var state = new StateVector(position, velocity, earth, epoch, Frames.Frame.ICRF);
```

---

## Core Features

### Working with Time Systems

Astrodynamics requires precise time handling. IO.Astrodynamics supports multiple time systems used in space operations.

#### Time Systems Available

- **UTC**: Coordinated Universal Time (includes leap seconds)
- **TDB**: Barycentric Dynamical Time (for ephemeris calculations)
- **TAI**: International Atomic Time (monotonic, no leap seconds)
- **GPS**: GPS Time
- **TDT/TT**: Terrestrial Dynamical Time
- **Local Time**: Site-specific local time

#### Basic Time Operations

```csharp
using IO.Astrodynamics.TimeSystem;

// Create time using year, month, day, hour, minute, second components
var utcTime = new TimeSystem.Time(2025, 1, 15, 10, 30, 0, frame: TimeFrame.UTCFrame);

// Create time from DateTime
var fromDateTime = new TimeSystem.Time(new DateTime(2025, 1, 15, 10, 30, 0), TimeFrame.UTCFrame);

// Create from elapsed seconds since J2000
var tdbFromSeconds = TimeSystem.Time.CreateTDB(631152000.0);
var utcFromSeconds = TimeSystem.Time.CreateUTC(631152000.0);

// Convert between time systems
var tdbTime = utcTime.ToTDB();
var taiTime = utcTime.ToTAI();
var gpsTime = utcTime.ToGPS();
var tdtTime = utcTime.ToTDT();

// Time arithmetic
var futureTime = utcTime.Add(TimeSpan.FromDays(7));
var futureTime2 = utcTime.AddSeconds(3600.0);
var duration = futureTime - utcTime;

// Parse from string (format includes time frame suffix)
var parsedUtc = new TimeSystem.Time("2025-01-15T10:30:00.0000000Z");  // UTC
var parsedTdb = new TimeSystem.Time("2025-01-15T10:30:00.0000000 TDB");  // TDB
var parsedTai = new TimeSystem.Time("2025-01-15T10:30:00.0000000 TAI");  // TAI

// Get seconds since J2000
var secondsFromJ2000 = utcTime.TimeSpanFromJ2000().TotalSeconds;

// Convert to Julian date
var julianDate = utcTime.ToJulianDate();
```

#### Time Windows

Time windows represent intervals for search operations:

```csharp
using IO.Astrodynamics.TimeSystem;

var start = new TimeSystem.Time(2025, 1, 1, frame: TimeFrame.TDBFrame);
var end = new TimeSystem.Time(2025, 12, 31, frame: TimeFrame.TDBFrame);
var searchWindow = new Window(start, end);

// Access window boundaries
TimeSystem.Time windowStart = searchWindow.StartDate;
TimeSystem.Time windowEnd = searchWindow.EndDate;

// Get window duration
TimeSpan duration = searchWindow.Length;
```

#### Real-World Example: Mission Timeline

```csharp
// Mars mission timeline planning
var launchWindowStart = new TimeSystem.Time(2026, 9, 1, frame: TimeFrame.UTCFrame);
var launchWindowEnd = new TimeSystem.Time(2026, 10, 31, frame: TimeFrame.UTCFrame);
var launchWindow = new Window(launchWindowStart.ToTDB(), launchWindowEnd.ToTDB());

var marsArrival = launchWindow.StartDate.Add(TimeSpan.FromDays(210)); // ~7 month cruise
Console.WriteLine($"Expected Mars arrival: {marsArrival}");
```

---

### Celestial Bodies and Solar System Objects

Access planets, moons, asteroids, and other solar system objects.

#### Built-in Solar System Objects

```csharp
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;

// Major bodies - EARTH_BODY returns a CelestialBody directly
var earth = PlanetsAndMoons.EARTH_BODY;

// Sun (from Stars namespace)
var sun = new CelestialBody(Stars.Sun);

// Create CelestialBody with specific frame and epoch
var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, epoch);
var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, epoch);
var jupiter = new CelestialBody(PlanetsAndMoons.JUPITER, Frames.Frame.ICRF, epoch);

// Moons
var europa = new CelestialBody(PlanetsAndMoons.EUROPA, Frames.Frame.ICRF, epoch);
var titan = new CelestialBody(PlanetsAndMoons.TITAN, Frames.Frame.ICRF, epoch);

// Barycenters
var earthMoonBarycenter = new Barycenter(Barycenters.EARTH_BARYCENTER);
var solarSystemBarycenter = new Barycenter(Barycenters.SOLAR_SYSTEM_BARYCENTER);

// Lagrange points
var l1 = new LagrangePoint(LagrangePoints.EARTH_MOON_L1);
var l2 = new LagrangePoint(LagrangePoints.EARTH_MOON_L2);
```

#### Body Properties

```csharp
// Physical properties
double mass = earth.Mass;           // kg
double radius = earth.EquatorialRadius;  // m (equatorial radius)
double mu = earth.GM;               // Gravitational parameter (m³/s²)
double flattening = earth.Flattening;
int naifId = earth.NaifId;          // NAIF ID (399 for Earth)

// Get body-fixed frame
var earthFixedFrame = earth.Frame;
```

#### Ground Stations and Sites

```csharp
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Coordinates;

// Site using known DSS station (requires SPICE station kernels)
var dss13 = new Site(13, "DSS-13", earth);

// Site with custom coordinates (note: Planetodetic takes longitude, latitude, altitude)
var customSite = new Site(
    399001,                          // Custom NAIF ID
    "Kennedy Space Center",
    earth,
    new Planetodetic(
        -80.6480 * IO.Astrodynamics.Constants.Deg2Rad,  // Longitude (radians)
        28.5721 * IO.Astrodynamics.Constants.Deg2Rad,   // Latitude (radians)
        0.0                                              // Altitude (meters)
    )
);

// Launch site
var launchSite = new LaunchSite(
    399002,
    "Launch Complex 39A",
    earth,
    new Planetodetic(
        -80.6041 * IO.Astrodynamics.Constants.Deg2Rad,  // Longitude
        28.6083 * IO.Astrodynamics.Constants.Deg2Rad,   // Latitude
        0.0
    )
);
```

#### Real-World Example: Distance Between Bodies

```csharp
// Calculate Earth-Mars distance
var epoch = new TimeSystem.Time(2025, 1, 16, frame: TimeFrame.TDBFrame);
var earth = PlanetsAndMoons.EARTH_BODY;
var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, epoch);

var marsState = mars.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.LT);
double distance = marsState.Position.Magnitude();  // meters
double distanceKm = distance / 1000.0;
double distanceAU = distance / IO.Astrodynamics.Constants.AU;

Console.WriteLine($"Earth-Mars distance: {distanceKm:F0} km ({distanceAU:F3} AU)");
```

---

### Orbital Parameters

IO.Astrodynamics supports multiple representations of orbital elements.

#### Keplerian Elements

Classical orbital elements (semi-major axis, eccentricity, inclination, etc.)

```csharp
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Math;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame);

// Create from Keplerian elements (a, e, i, RAAN, AOP, mean anomaly)
var keplerianElements = new KeplerianElements(
    7000000.0,                                    // Semi-major axis (meters)
    0.001,                                        // Eccentricity
    51.6 * IO.Astrodynamics.Constants.Deg2Rad,   // Inclination (radians)
    0.0,                                          // RAAN (radians)
    0.0,                                          // Argument of periapsis (radians)
    0.0,                                          // Mean anomaly (radians)
    earth,                                        // Observer (central body)
    epoch,                                        // Epoch
    Frames.Frame.ICRF                            // Reference frame
);

// Access orbital properties via methods
double sma = keplerianElements.SemiMajorAxis();
double ecc = keplerianElements.Eccentricity();
double inc = keplerianElements.Inclination();
TimeSpan period = keplerianElements.Period();
double apogee = keplerianElements.ApogeeVector().Magnitude();
double perigee = keplerianElements.PerigeeVector().Magnitude();
double meanMotion = keplerianElements.MeanMotion();
```

#### State Vectors

Cartesian position and velocity representation.

```csharp
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Math;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

// Create from position and velocity vectors
var position = new Vector3(6800000.0, 0.0, 0.0);  // meters
var velocity = new Vector3(0.0, 7656.0, 0.0);     // m/s

var stateVector = new StateVector(
    position,
    velocity,
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Convert to Keplerian
var keplerian = stateVector.ToKeplerianElements();
Console.WriteLine($"Semi-major axis: {keplerian.SemiMajorAxis() / 1000.0:F2} km");
Console.WriteLine($"Eccentricity: {keplerian.Eccentricity():F6}");

// Propagate to future epoch
var futureEpoch = epoch.Add(TimeSpan.FromHours(1));
var futureState = stateVector.AtEpoch(futureEpoch).ToStateVector();
```

#### Two-Line Elements (TLE)

Standard format for distributing Earth satellite orbital data.

```csharp
using IO.Astrodynamics.OrbitalParameters.TLE;

// Parse TLE data (e.g., from Celestrak)
string line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

var tle = new TLE("ISS", line1, line2);

// Access TLE mean elements
double meanSma = tle.MeanSemiMajorAxis;
double meanEcc = tle.MeanEccentricity;
double meanInc = tle.MeanInclination;
double meanRaan = tle.MeanAscendingNode;
var tleEpoch = tle.Epoch;

// Convert TLE to state vector at specific epoch
var targetEpoch = TimeSystem.Time.CreateTDB(664440682.84760022);
var stateFromTLE = tle.ToStateVector(targetEpoch).ToFrame(Frames.Frame.ICRF);

// Propagate TLE to new epoch
var propagated = tle.AtEpoch(targetEpoch);
```

#### Equinoctial Elements

Non-singular orbital elements useful for low eccentricity and inclination.

```csharp
// Convert from Keplerian to Equinoctial
var equinoctialElements = keplerianElements.ToEquinoctialElements();

// Access equinoctial element properties via methods
double sma = equinoctialElements.SemiMajorAxis();
double ecc = equinoctialElements.Eccentricity();
double inc = equinoctialElements.Inclination();
```

#### Real-World Example: ISS Orbit Analysis

```csharp
// Typical ISS orbital parameters from TLE
var issTle = new TLE("ISS",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

// Or create Keplerian elements directly
var earth = PlanetsAndMoons.EARTH_BODY;
var issOrbit = new KeplerianElements(
    6793000.0,                                     // ~420 km altitude
    0.0001,                                        // Nearly circular
    51.64 * IO.Astrodynamics.Constants.Deg2Rad,   // ISS inclination
    0.0, 0.0, 0.0,
    earth,
    TimeSystem.Time.J2000TDB,
    Frames.Frame.ICRF
);

// Calculate orbital period
var period = issOrbit.Period();
Console.WriteLine($"ISS orbital period: {period.TotalMinutes:F1} minutes");

// Get apogee and perigee altitudes
double apogeeAlt = issOrbit.ApogeeVector().Magnitude() - earth.EquatorialRadius;
double perigeeAlt = issOrbit.PerigeeVector().Magnitude() - earth.EquatorialRadius;
Console.WriteLine($"Apogee altitude: {apogeeAlt / 1000:F1} km");
Console.WriteLine($"Perigee altitude: {perigeeAlt / 1000:F1} km");
```

---

### State Vectors and Propagation

Propagate orbits forward in time using numerical integration.

#### Basic Propagation

```csharp
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Math;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

// Initial orbit
var initialState = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.0, 0.0),
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Propagate forward using 2-body Keplerian motion
var futureEpoch = epoch.Add(TimeSpan.FromDays(1));
var futureState = initialState.AtEpoch(futureEpoch).ToStateVector();

Console.WriteLine($"Position after 1 day: {futureState.Position}");
```

#### Propagation with Scenario

```csharp
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.Body.Spacecraft;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = new TimeSystem.Time(2021, 3, 2, 0, 0, 0).ToTDB();
var end = start.Add(TimeSpan.FromDays(3));

// Create mission scenario
var scenario = new Scenario("Propagation_Test",
    new IO.Astrodynamics.Mission.Mission("TestMission"),
    new Window(start, end));

// Define orbit
var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.0, 0.0),
    earth,
    start,
    Frames.Frame.ICRF
);

// Create spacecraft (dryMass, maxOperatingMass)
var spacecraft = new Spacecraft(-178, "TESTSAT", 1000.0, 3000.0,
    new Clock("clk1", 65536), orbit);

scenario.AddSpacecraft(spacecraft);

// Execute scenario simulation
await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(60.0));
```

#### Real-World Example: Satellite Altitude Tracking

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

// Low Earth orbit satellite
var leo = new StateVector(
    new Vector3(6600000.0, 0.0, 0.0),  // ~220 km altitude
    new Vector3(0.0, 7780.0, 0.0),
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Track altitude over 30 days using 2-body propagation
for (int day = 0; day < 30; day++)
{
    var nextEpoch = epoch.Add(TimeSpan.FromDays(day));
    var currentState = leo.AtEpoch(nextEpoch).ToStateVector();

    double altitude = currentState.Position.Magnitude() - earth.EquatorialRadius;
    Console.WriteLine($"Day {day}: Altitude = {altitude / 1000:F1} km");
}
```

---

### Spacecraft Modeling

Model spacecraft with engines, fuel tanks, instruments, and sensors.

#### Basic Spacecraft

```csharp
using IO.Astrodynamics.Body.Spacecraft;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.0, 0.0),
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Create spacecraft: (naifId, name, dryOperatingMass, maxOperatingMass, clock, orbit)
// Note: dryMass comes BEFORE maxMass in the constructor
var spacecraft = new Spacecraft(
    -1001,                           // NAIF ID (must be negative for spacecraft)
    "ExplorerSat",                   // Name
    1000.0,                          // Dry operating mass (kg)
    3000.0,                          // Maximum operating mass (kg)
    new Clock("SC_CLOCK", 65536),    // Onboard clock
    orbit                            // Initial orbital parameters
);
```

#### Propulsion System

```csharp
// Add fuel tank: (name, model, serialNumber, capacity, quantity)
var fuelTank = new FuelTank("Main Tank", "TankModel-500", "TK-001", 2000.0, 2000.0);
spacecraft.AddFuelTank(fuelTank);

// Add engine: (name, model, serialNumber, isp, fuelFlow, fuelTank)
// Note: Engine must be added AFTER the fuel tank it uses
var engine = new Engine("Main Engine", "EngineModel-450", "EN-001", 450.0, 50.0, fuelTank);
spacecraft.AddEngine(engine);

// Add payload
spacecraft.AddPayload(new Payload("Science Package", 150.0, "PL-001"));
```

#### Instruments and Sensors

Instruments are added via spacecraft methods, not created as separate objects:

```csharp
// Add circular instrument (camera, telescope)
// Parameters: naifId, name, model, fieldOfView, boresight, refVector, orientation
spacecraft.AddCircularInstrument(
    -1001600,                        // Instrument NAIF ID
    "HighResCam",                    // Name
    "CAM-2000",                      // Model
    1.5,                             // Field of view (radians)
    Vector3.VectorZ,                 // Boresight direction
    Vector3.VectorX,                 // Reference vector
    Vector3.VectorX                  // Orientation (Euler angles)
);

// Add rectangular instrument
// Uses AddRectangularInstrument with cross-angle and along-track angle
```

#### Real-World Example: Earth Observation Satellite

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

// Create sun-synchronous orbit
var ssoOrbit = new KeplerianElements(
    7078000.0,                                     // ~700 km altitude
    0.001,                                         // Nearly circular
    98.2 * IO.Astrodynamics.Constants.Deg2Rad,    // Sun-synchronous inclination
    0.0, 0.0, 0.0,
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Create Earth observation satellite (dryMass, maxMass)
var eoSat = new Spacecraft(-200, "EarthObserver", 700.0, 1000.0,
    new Clock("EO_CLK", 65536), ssoOrbit);

// Add fuel tank and engine
eoSat.AddFuelTank(new FuelTank("ft1", "model1", "sn1", 300.0, 300.0));
eoSat.AddEngine(new Engine("engine1", "model1", "sn1", 300.0, 10.0,
    eoSat.FuelTanks.First()));

// Add nadir-pointing camera instrument
eoSat.AddCircularInstrument(-200600, "MultiSpectralImager", "MSI-3000",
    0.087,                           // ~5° FOV in radians
    Vector3.VectorZ,                 // Boresight
    Vector3.VectorY,                 // Reference
    new Vector3(0.0, System.Math.PI * 0.5, 0.0)  // Nadir orientation
);

// Calculate ground swath width
double altitude = ssoOrbit.SemiMajorAxis() - earth.EquatorialRadius;
double fovHalfAngle = 0.087 / 2.0;
double swathWidth = 2.0 * altitude * System.Math.Tan(fovHalfAngle);
Console.WriteLine($"Ground swath width: {swathWidth / 1000:F1} km");
```

---

### Maneuvers and Orbital Changes

Plan and execute orbital maneuvers.

#### Combined Maneuver

Simultaneous altitude and plane change (optimized).

```csharp
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Body.Spacecraft;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

// Create elliptical orbit
var orbitalParams = new KeplerianElements(
    24420999.959422689,                           // Semi-major axis
    0.726546824,                                  // Eccentricity (elliptical)
    28.5 * IO.Astrodynamics.Constants.Deg2Rad,   // Inclination
    0.0, 0.0, 0.0,
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Create spacecraft with propulsion
var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 10000.0,
    new Clock("GenericClk", 65536), orbitalParams);
spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 9000.0, 9000.0));
spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

// CombinedManeuver: (observer, minimumEpoch, holdDuration, targetPerigeeHeight, targetInclination, engine)
var maneuver = new CombinedManeuver(
    earth,
    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
    TimeSpan.Zero,
    42164000.0,                                   // Target perigee height (GEO radius)
    0.0,                                          // Target inclination (equatorial)
    spc.Engines.First()
);

// Execute at apogee
var apogeeEpoch = orbitalParams.Epoch.Add(orbitalParams.Period() * 0.5);
var maneuverPoint = orbitalParams.ToStateVector(apogeeEpoch);

// Check if maneuver can execute, then execute
if (maneuver.CanExecute(maneuverPoint))
{
    var (newState, orientation) = maneuver.TryExecute(maneuverPoint);
    Console.WriteLine($"Delta-V required: {maneuver.DeltaV.Magnitude():F1} m/s");
    Console.WriteLine($"Fuel burned: {maneuver.FuelBurned:F1} kg");
    Console.WriteLine($"Maneuver window: {maneuver.ManeuverWindow}");
}
```

#### Attitude Maneuvers

```csharp
// Attitude maneuvers define spacecraft orientation
// They are typically used in mission scenarios

// Nadir pointing (for Earth observation)
var nadirAttitude = new NadirAttitude(epoch, earth, engine);

// Prograde attitude
var progradeAttitude = new ProgradeAttitude(epoch, engine);

// Retrograde attitude (for deorbit burns)
var retrogradeAttitude = new RetrogradeAttitude(epoch, engine);

// Zenith pointing (away from Earth)
var zenithAttitude = new ZenithAttitude(epoch, earth, engine);
```

#### Real-World Example: Geostationary Transfer Orbit (GTO) to GEO

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = TimeSystem.Time.J2000TDB;

// Initial GTO orbit after launch
var gto = new KeplerianElements(
    24420999.959422689,                           // GTO semi-major axis
    0.726546824,                                  // Highly elliptical
    28.5 * IO.Astrodynamics.Constants.Deg2Rad,   // Cape Canaveral launch inclination
    0.0,
    0.0,
    0.0,
    earth,
    epoch,
    Frames.Frame.ICRF
);

// Create satellite with propulsion (dryMass, maxMass)
var comSat = new Spacecraft(-300, "CommSat", 1500.0, 4500.0,
    new Clock("CS_CLK", 65536), gto);
comSat.AddFuelTank(new FuelTank("Main", "TK-500", "001", 3000.0, 3000.0));
comSat.AddEngine(new Engine("Apogee Motor", "AM-450", "001", 450.0, 50.0,
    comSat.FuelTanks.First()));

// Combined maneuver: Circularize + reduce inclination
var circularizeBurn = new CombinedManeuver(
    earth,
    new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
    TimeSpan.Zero,
    42164000.0,    // Raise perigee to GEO altitude
    0.0,           // Target equatorial inclination
    comSat.Engines.First()
);

// Execute at apogee
var apogeeEpoch = gto.Epoch.Add(gto.Period() * 0.5);
var maneuverPoint = gto.ToStateVector(apogeeEpoch);
var (finalState, finalOrientation) = circularizeBurn.TryExecute(maneuverPoint);

Console.WriteLine($"Apogee burn delta-V: {circularizeBurn.DeltaV.Magnitude():F0} m/s");
Console.WriteLine($"Fuel required: {circularizeBurn.FuelBurned:F0} kg");
```

---

### Launch Windows

Calculate optimal launch opportunities.

#### Launch Planning with API

```csharp
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Surface;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = new TimeSystem.Time("2021-03-02 00:00:00.000000").ToTDB();
var end = new TimeSystem.Time("2021-03-05 00:00:00.000000").ToTDB();
var window = new Window(start, end);

// Define launch site (longitude, latitude, altitude)
var launchSite = new LaunchSite(399303, "LaunchSite",
    earth,
    new Planetodetic(
        -81.0 * IO.Astrodynamics.Constants.Deg2Rad,   // Longitude
        28.5 * IO.Astrodynamics.Constants.Deg2Rad,    // Latitude
        0.0                                            // Altitude
    ));

// Define target parking orbit
var parkingOrbit = new StateVector(
    new Vector3(5056554.1874925727, 4395595.4942363985, 0.0),
    new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494),
    earth,
    start,
    Frames.Frame.ICRF
);

// Create launch object
var launch = new Launch(launchSite, launchSite, parkingOrbit,
    IO.Astrodynamics.Constants.CivilTwilight, true);

// Find launch windows using API
var launchWindows = API.Instance.FindLaunchWindows(launch, window,
    new DirectoryInfo("output")).ToArray();

foreach (var lw in launchWindows)
{
    Console.WriteLine($"Launch window: {lw.Window}");
    Console.WriteLine($"Inertial azimuth: {lw.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg:F2}°");
    Console.WriteLine($"Insertion velocity: {lw.InertialInsertionVelocity:F2} m/s");
}
```

#### Real-World Example: Interplanetary Distance

```csharp
// Check Earth-Mars distance for launch window planning
var epoch = new TimeSystem.Time(2026, 9, 15, frame: TimeFrame.TDBFrame);
var sun = new CelestialBody(Stars.Sun);
var earth = PlanetsAndMoons.EARTH_BODY;
var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, epoch);

var earthState = earth.GetEphemeris(epoch, sun, Frames.Frame.ICRF, Aberration.None);
var marsState = mars.GetEphemeris(epoch, sun, Frames.Frame.ICRF, Aberration.None);

// Calculate relative position
var earthToMars = marsState.Position - earthState.Position;
double distance = earthToMars.Magnitude();
double distanceAU = distance / IO.Astrodynamics.Constants.AU;

Console.WriteLine($"Earth-Mars distance: {distanceAU:F3} AU");

// Mars launch windows occur approximately every 26 months
// Optimal when distance is near minimum (~0.5 AU)
```

---

### Atmospheric Modeling

Calculate atmospheric properties for Earth and Mars.

#### Earth Standard Atmosphere

Simple model valid to ~86 km altitude.

```csharp
using IO.Astrodynamics.Atmosphere;

// Create context with altitude only
var context = AtmosphericContext.FromAltitude(50000.0); // 50 km

// Use standard atmosphere model
var stdAtm = new EarthStandardAtmosphere();

double temperature = stdAtm.GetTemperature(context); // Celsius
double pressure = stdAtm.GetPressure(context);       // kPa
double density = stdAtm.GetDensity(context);         // kg/m³

Console.WriteLine($"At 50 km altitude:");
Console.WriteLine($"  Temperature: {temperature:F1}°C");
Console.WriteLine($"  Pressure: {pressure:F3} kPa");
Console.WriteLine($"  Density: {density:F6} kg/m³");
```

#### NRLMSISE-00 Model

High-fidelity empirical model for 0-2000+ km altitude with space weather effects.

```csharp
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

// Create full context with time and position
var fullContext = new AtmosphericContext
{
    Altitude = 400000.0,  // 400 km (ISS altitude)
    Epoch = new TimeSystem.Time(2025, 3, 21, 12, 0, 0, frame: TimeFrame.UTCFrame),
    GeodeticLatitude = 45.0 * IO.Astrodynamics.Constants.Deg2Rad,
    GeodeticLongitude = 0.0
};

// Create space weather data
var spaceWeather = new SpaceWeather(150.0, 150.0, 15.0);  // F107, F107A, Ap

// Create NRLMSISE-00 model with space weather
var nrlmsise = new Nrlmsise00Model(spaceWeather);

// Or use default nominal conditions
var nrlmsiseDefault = new Nrlmsise00Model();  // Uses F107=150, F107A=150, Ap=4

double temp = nrlmsise.GetTemperature(fullContext);  // Celsius
double pres = nrlmsise.GetPressure(fullContext);     // kPa
double dens = nrlmsise.GetDensity(fullContext);      // kg/m³

Console.WriteLine($"At 400 km (ISS altitude):");
Console.WriteLine($"  Temperature: {temp:F1}°C");
Console.WriteLine($"  Pressure: {pres:E3} kPa");
Console.WriteLine($"  Density: {dens:E6} kg/m³");
```

#### Mars Standard Atmosphere

```csharp
// Mars atmospheric model
var marsAtm = new MarsStandardAtmosphere();
var marsContext = AtmosphericContext.FromAltitude(10000.0); // 10 km

double marsTemp = marsAtm.GetTemperature(marsContext);
double marsPres = marsAtm.GetPressure(marsContext);
double marsDens = marsAtm.GetDensity(marsContext);
```

#### Real-World Example: Atmospheric Drag Calculation

```csharp
// Calculate drag force on ISS
double altitude = 420000.0; // meters
var atmContext = AtmosphericContext.FromAltitude(altitude);
var atmosphere = new EarthStandardAtmosphere();

double density = atmosphere.GetDensity(atmContext);

// ISS properties
double crossSectionalArea = 500.0;  // m² (approximate)
double dragCoefficient = 2.2;
double velocity = 7660.0;           // m/s (orbital velocity)

// Drag force: F = 0.5 * ρ * v² * Cd * A
double dragForce = 0.5 * density * velocity * velocity * dragCoefficient * crossSectionalArea;
double dragAcceleration = dragForce / 420000.0; // ISS mass ~420,000 kg

Console.WriteLine($"Atmospheric density at {altitude/1000:F0} km: {density:E6} kg/m³");
Console.WriteLine($"Drag force: {dragForce:F2} N");
Console.WriteLine($"Drag acceleration: {dragAcceleration:E6} m/s²");

// Calculate altitude loss per orbit
double orbitalPeriod = 5520.0; // seconds (~92 minutes)
double altitudeLossPerOrbit = 0.5 * dragAcceleration * orbitalPeriod * orbitalPeriod;
Console.WriteLine($"Altitude loss per orbit: ~{altitudeLossPerOrbit:F1} m");
```

---

### Reference Frames and Coordinates

Work with various coordinate systems and reference frames.

#### Reference Frames

```csharp
using IO.Astrodynamics.Frames;

// Inertial frames
Frame icrf = Frame.ICRF;          // International Celestial Reference Frame
Frame j2000 = Frame.GALACTIC;     // Galactic frame

// Body-fixed frames (rotate with the body)
Frame earthFixed = earth.Frame;   // ITRF (Earth-fixed)
Frame marsFixed = mars.Frame;     // Mars body-fixed

// Transform between frames
var stateInICRF = someState;
var stateInEarthFixed = stateInICRF.ToFrame(earthFixed, epoch);
```

#### Coordinate Systems

**Planetodetic (Geodetic) Coordinates:**

```csharp
using IO.Astrodynamics.Coordinates;

// IMPORTANT: Planetodetic constructor takes (longitude, latitude, altitude)
var geodetic = new Planetodetic(
    -80.5 * IO.Astrodynamics.Constants.Deg2Rad,   // Longitude (radians)
    28.5 * IO.Astrodynamics.Constants.Deg2Rad,    // Latitude (radians)
    100000.0                                       // Altitude (meters)
);

// Access properties
double lon = geodetic.Longitude;  // radians
double lat = geodetic.Latitude;   // radians
double alt = geodetic.Altitude;   // meters
```

**Planetocentric Coordinates:**

```csharp
// Spherical coordinates (longitude, latitude, radius)
var planetocentric = new Planetocentric(
    0.0,                                           // Longitude (radians)
    45.0 * IO.Astrodynamics.Constants.Deg2Rad,    // Latitude (radians)
    6878000.0                                      // Radius from center (meters)
);
```

**Horizontal Coordinates (Azimuth/Elevation/Range):**

```csharp
// Horizontal coordinates from site observation
// Get horizontal coordinates using Site.GetHorizontalCoordinates()
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, epoch);

var site = new Site(13, "DSS-13", earth);
var hor = site.GetHorizontalCoordinates(epoch, moon, Aberration.None);

Console.WriteLine($"Azimuth: {hor.Azimuth * IO.Astrodynamics.Constants.Rad2Deg:F2}°");
Console.WriteLine($"Elevation: {hor.Elevation * IO.Astrodynamics.Constants.Rad2Deg:F2}°");
Console.WriteLine($"Range: {hor.Range / 1000.0:F1} km");
```

#### Real-World Example: Ground Station Visibility

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, epoch);

// Create site with coordinates (longitude, latitude, altitude)
var groundStation = new Site(
    399001,
    "Tracking Station",
    earth,
    new Planetodetic(
        -106.0 * IO.Astrodynamics.Constants.Deg2Rad,  // Longitude
        35.0 * IO.Astrodynamics.Constants.Deg2Rad,    // Latitude
        2000.0                                         // Altitude
    )
);

// Get horizontal coordinates to target
var horizontal = groundStation.GetHorizontalCoordinates(epoch, moon, Aberration.None);

Console.WriteLine($"Azimuth: {horizontal.Azimuth * IO.Astrodynamics.Constants.Rad2Deg:F2}°");
Console.WriteLine($"Elevation: {horizontal.Elevation * IO.Astrodynamics.Constants.Rad2Deg:F2}°");
Console.WriteLine($"Range: {horizontal.Range / 1000.0:F1} km");

if (horizontal.Elevation > 10.0 * Constants.Deg2Rad)
{
    Console.WriteLine("Satellite is visible (above 10° elevation)");
}
```

---

### Ephemeris Calculations

High-precision position and velocity calculations.

#### Basic Ephemeris Query

```csharp
// Get position and velocity of Moon relative to Earth
var moonState = moon.GetEphemeris(
    epoch: epoch,
    observer: earth,
    frame: Frames.Frame.ICRF,
    aberration: Aberration.LT  // Correct for light travel time
);

Console.WriteLine($"Position: {moonState.Position}");
Console.WriteLine($"Velocity: {moonState.Velocity}");
```

#### Aberration Corrections

```csharp
// No correction (geometric position)
Aberration.None

// Light-time correction (apparent position)
Aberration.LT

// Light-time + stellar aberration
Aberration.LTS

// Transmission case (for transmitting to a moving target)
Aberration.XLT    // Transmission light-time
Aberration.XLTS   // Transmission light-time + stellar
```

#### Writing Custom Ephemeris

```csharp
// Create ephemeris file for spacecraft
var states = new List<StateVector>();
for (int i = 0; i < 100; i++)
{
    var t = epoch + TimeSpan.FromMinutes(i * 10);
    states.Add(spacecraft.GetEphemeris(t, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector());
}

// Write to SPICE SPK kernel
API.Instance.WriteEphemeris("spacecraft_ephemeris.bsp", spacecraft.NaifId, states.ToArray());
```

#### Real-World Example: Satellite Ground Track

```csharp
// Calculate ground track of satellite over 1 orbit
var groundTrack = new List<Planetodetic>();

var orbitPeriod = satelliteOrbit.Period();
int numPoints = 100;
var timeStep = orbitPeriod / numPoints;

for (int i = 0; i <= numPoints; i++)
{
    var t = epoch + timeStep * i;
    var state = satelliteOrbit.ToStateVector(t);

    // Transform to Earth-fixed frame
    var stateEarthFixed = state.ToFrame(earth.Frame, t);

    // Convert to lat/lon
    var geodetic = Planetodetic.FromCartesian(stateEarthFixed.Position, earth);
    groundTrack.Add(geodetic);

    Console.WriteLine($"Time: {t}, Lat: {geodetic.Latitude * Constants.Rad2Deg:F2}°, " +
                      $"Lon: {geodetic.Longitude * Constants.Rad2Deg:F2}°");
}
```

---

### Occultations and Illumination

Calculate eclipses, shadows, and illumination conditions.

#### Eclipse Detection (Occultation Search)

```csharp
using IO.Astrodynamics;

var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF,
    new TimeSystem.Time(2001, 12, 14));

// Define search window
var searchWindow = new Window(
    TimeSystem.Time.CreateTDB(61473664.183390938),
    TimeSystem.Time.CreateTDB(61646464.183445148)
);

// Find when the Sun is occulted by the Moon (solar eclipse as seen from Earth)
var eclipseWindows = API.Instance.FindWindowsOnOccultationConstraint(
    searchWindow,
    earth,                      // Observer
    sun,                        // Target being occulted
    ShapeType.Ellipsoid,        // Target shape
    moon,                       // Front body (occluding)
    ShapeType.Ellipsoid,        // Front body shape
    OccultationType.Any,        // Any occultation type
    Aberration.LT,              // Light-time correction
    TimeSpan.FromSeconds(3600)  // Step size
);

foreach (var window in eclipseWindows)
{
    Console.WriteLine($"Eclipse: {window.StartDate} to {window.EndDate}");
    Console.WriteLine($"Duration: {window.Length.TotalMinutes:F1} minutes");
}
```

#### Distance Constraint Search

```csharp
// Find when Moon is more than 400,000 km from Earth
var distanceWindows = API.Instance.FindWindowsOnDistanceConstraint(
    new Window(
        TimeSystem.Time.CreateTDB(220881665.18391809),
        TimeSystem.Time.CreateTDB(228657665.18565452)
    ),
    earth,
    moon,
    RelationnalOperator.Greater,    // Distance > value
    400000000,                      // 400,000 km in meters
    Aberration.None,
    TimeSpan.FromSeconds(86400)     // 1 day step
);

foreach (var window in distanceWindows)
{
    Console.WriteLine($"Moon far: {window.StartDate} to {window.EndDate}");
}
```

#### Real-World Example: Solar Panel Power Generation

```csharp
// Calculate solar panel power over one orbit
var orbit = satelliteOrbit;
var orbitPeriod = orbit.Period();
var numSteps = 50;

for (int i = 0; i <= numSteps; i++)
{
    var t = epoch + (orbitPeriod * i / numSteps);
    var satState = orbit.ToStateVector(t);

    // Check if spacecraft is in Earth's shadow
    var sunState = sun.GetEphemeris(t, earth, Frames.Frame.ICRF, Aberration.None);
    var satStateFromEarth = satState;

    // Simplified eclipse check: if Earth is between sat and sun
    var earthToSat = satStateFromEarth.Position;
    var earthToSun = sunState.Position;

    double angle = Vector3.Angle(earthToSat, earthToSun);
    bool inSunlight = angle > Math.PI / 2;

    // Solar panel area and efficiency
    double panelArea = 20.0; // m²
    double efficiency = 0.25; // 25%
    double solarConstant = 1361.0; // W/m² at Earth distance

    double power = inSunlight ? solarConstant * panelArea * efficiency : 0.0;

    Console.WriteLine($"T+{(t - epoch).TotalMinutes:F0}min: " +
                      $"Power = {power:F0}W {(inSunlight ? "(sunlight)" : "(eclipse)")}");
}
```

---

### Field of View and Visibility

Calculate instrument visibility and target tracking.

#### FOV Constraint Search

```csharp
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.Body.Spacecraft;

var earth = PlanetsAndMoons.EARTH_BODY;
var start = TimeSystem.Time.CreateUTC(676555130.80).ToTDB();
var end = start.AddSeconds(6448.0);

// Create scenario
var scenario = new Scenario("FOV_Test",
    new IO.Astrodynamics.Mission.Mission("TestMission"),
    new Window(start, end));
scenario.AddCelestialItem(earth);

// Create spacecraft with instrument
var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.2204182967143, 0.0),
    earth, start, Frames.Frame.ICRF
);

var spacecraft = new Spacecraft(-179, "SC179", 1000.0, 3000.0,
    new Clock("clk1", 65536), orbit);
spacecraft.AddCircularInstrument(-179789, "CAMERA789", "mod1", 0.75,
    Vector3.VectorZ, Vector3.VectorY,
    new Vector3(0.0, System.Math.PI * 0.5, 0.0));
scenario.AddSpacecraft(spacecraft);

// Execute scenario
await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));

// Find windows when Earth is in camera FOV
var fovWindows = spacecraft.Instruments.First().FindWindowsInFieldOfViewConstraint(
    new Window(TimeSystem.Time.CreateTDB(676555200.0), TimeSystem.Time.CreateTDB(676561646.0)),
    spacecraft,
    earth,
    earth.Frame,
    ShapeType.Ellipsoid,
    Aberration.LT,
    TimeSpan.FromSeconds(360.0)
).ToArray();

foreach (var window in fovWindows)
{
    Console.WriteLine($"Target visible: {window.StartDate} to {window.EndDate}");
}
```

#### Real-World Example: Ground Station Visibility Check

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, epoch);

// Ground station (using known DSS station)
var groundStation = new Site(13, "DSS-13", earth);

// Or create with custom coordinates (longitude, latitude, altitude)
var customStation = new Site(
    399003,
    "Mission Control",
    earth,
    new Planetodetic(
        -80.6 * IO.Astrodynamics.Constants.Deg2Rad,   // Longitude
        28.5 * IO.Astrodynamics.Constants.Deg2Rad,    // Latitude
        10.0                                           // Altitude
    )
);

// Get horizontal coordinates to check visibility
var horizontal = groundStation.GetHorizontalCoordinates(epoch, moon, Aberration.None);

Console.WriteLine($"Azimuth: {horizontal.Azimuth * IO.Astrodynamics.Constants.Rad2Deg:F1}°");
Console.WriteLine($"Elevation: {horizontal.Elevation * IO.Astrodynamics.Constants.Rad2Deg:F1}°");
Console.WriteLine($"Range: {horizontal.Range / 1000:F0} km");

if (horizontal.Elevation > 10.0 * IO.Astrodynamics.Constants.Deg2Rad)
{
    Console.WriteLine("Target is visible (above 10° elevation)");
}
```

---

## Real-World Examples

### Example 1: Mars Mission Planning

Complete end-to-end Mars transfer trajectory.

```csharp
using System;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

// Load kernels
API.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

// Define mission parameters
var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);

// Launch date (2026 launch opportunity)
var launchDate = new TimeSystem.Time(2026, 9, 15, frame: TimeFrame.UTCFrame);
var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, launchDate);

// Get Earth and Mars states at launch
var earthAtLaunch = earth.GetEphemeris(launchDate, sun, Frames.Frame.ICRF, Aberration.None);
var marsAtArrival = mars.GetEphemeris(
    launchDate + TimeSpan.FromDays(210),  // 7-month cruise
    sun,
    Frames.Frame.ICRF,
    Aberration.None
);

// Create interplanetary spacecraft (dryMass, maxMass)
var spacecraft = new Spacecraft(
    -500,
    "Mars Explorer",
    2000.0,  // Dry mass
    5000.0,  // Max mass (includes fuel)
    new Clock("ME_CLK", 65536),
    new StateVector(earthAtLaunch.Position, earthAtLaunch.Velocity, sun, launchDate, Frames.Frame.ICRF)
);

// Add propulsion
var fuelTank = new FuelTank("Main Tank", "TK-3000", "001", 3000.0, 3000.0);
var engine = new Engine("Main Engine", "EN-450", "001", 450.0, 50.0, fuelTank);
spacecraft.AddFuelTank(fuelTank);
spacecraft.AddEngine(engine);

// Calculate transfer orbit using Lambert solver
// (would use Lambert class for detailed trajectory)

Console.WriteLine("=== Mars Mission Profile ===");
Console.WriteLine($"Launch date: {launchDate}");
Console.WriteLine($"Earth position: {earthAtLaunch.Position.Magnitude() / 1e9:F3} billion km from Sun");
Console.WriteLine($"Mars position at arrival: {marsAtArrival.Position.Magnitude() / 1e9:F3} billion km from Sun");

double transferDistance = (marsAtArrival.Position - earthAtLaunch.Position).Magnitude();
Console.WriteLine($"Transfer distance: {transferDistance / 1e9:F1} billion km");

// Estimate delta-V requirements
double departureV = 33000.0;  // m/s (approximate for Trans-Mars Injection)
double arrivalV = 5500.0;     // m/s (Mars Orbit Insertion)
double totalDeltaV = departureV + arrivalV;

Console.WriteLine($"\nDelta-V Budget:");
Console.WriteLine($"  Trans-Mars Injection: {departureV:F0} m/s");
Console.WriteLine($"  Mars Orbit Insertion: {arrivalV:F0} m/s");
Console.WriteLine($"  Total: {totalDeltaV:F0} m/s");

// Calculate fuel requirements using Tsiolkovsky equation
// m_fuel = m_dry * (exp(Δv / (Isp * g0)) - 1)
double isp = 450.0;
double g0 = 9.80665;
double massRatio = Math.Exp(totalDeltaV / (isp * g0));
double fuelRequired = spacecraft.DryOperatingMass * (massRatio - 1);

Console.WriteLine($"\nFuel Requirements:");
Console.WriteLine($"  Mass ratio: {massRatio:F2}");
Console.WriteLine($"  Fuel required: {fuelRequired:F0} kg");
Console.WriteLine($"  Fuel available: {fuelTank.Quantity:F0} kg");
Console.WriteLine($"  Margin: {(fuelTank.Quantity - fuelRequired) / fuelRequired * 100:F1}%");
```

### Example 2: Satellite Constellation Design

Design a communications constellation like Starlink.

```csharp
using System.Collections.Generic;

// Constellation parameters (Starlink-like)
int numPlanes = 6;
int satsPerPlane = 10;
double altitude = 550000.0;  // 550 km
double inclination = 53.0 * Constants.Deg2Rad;

var constellation = new List<Spacecraft>();

// Create constellation
for (int plane = 0; plane < numPlanes; plane++)
{
    double raan = plane * (360.0 / numPlanes) * Constants.Deg2Rad;

    for (int sat = 0; sat < satsPerPlane; sat++)
    {
        double meanAnomaly = sat * (360.0 / satsPerPlane) * Constants.Deg2Rad;

        // KeplerianElements: (semiMajorAxis, eccentricity, inclination, raan, aop, meanAnomaly, observer, epoch, frame)
        var orbit = new KeplerianElements(
            earth.EquatorialRadius + altitude,  // Semi-major axis
            0.0001,                              // Eccentricity
            inclination,                         // Inclination
            raan,                                // Right ascension of ascending node
            0.0,                                 // Argument of periapsis
            meanAnomaly,                         // Mean anomaly (distributes satellites in plane)
            earth,
            epoch,
            Frames.Frame.ICRF
        );

        // Spacecraft constructor: (naifId, name, dryMass, maxMass, clock, orbit)
        var spacecraft = new Spacecraft(
            -(1000 + plane * 100 + sat),
            $"ConstellationSat-{plane}-{sat}",
            250.0,   // Dry mass
            260.0,   // Max mass (includes fuel)
            new Clock($"SAT_{plane}_{sat}_CLK", 65536),
            orbit
        );

        constellation.Add(spacecraft);
    }
}

Console.WriteLine($"Created constellation: {numPlanes} planes × {satsPerPlane} sats = {constellation.Count} satellites");
Console.WriteLine($"Altitude: {altitude / 1000:F0} km");
Console.WriteLine($"Inclination: {inclination * Constants.Rad2Deg:F1}°");

// Calculate coverage metrics
var period = constellation[0].InitialOrbitalParameters.Period();
Console.WriteLine($"Orbital period: {period.TotalMinutes:F1} minutes");

// Minimum elevation angle for coverage
double minElevation = 25.0 * Constants.Deg2Rad;
double swathWidth = 2.0 * altitude * Math.Tan(Math.PI / 2 - minElevation);
Console.WriteLine($"Ground swath width per satellite: {swathWidth / 1000:F0} km");
```

### Example 3: Orbital Debris Conjunction Analysis

Detect close approaches between satellites and debris.

```csharp
// Primary satellite (dryMass, maxMass)
var primarySat = new Spacecraft(
    -600,
    "OperationalSatellite",
    900.0,   // Dry mass
    1000.0,  // Max mass
    new Clock("OPSAT_CLK", 65536),
    new KeplerianElements(
        7000000.0, 0.001, 51.6 * Constants.Deg2Rad,
        0.0, 0.0, 0.0, earth, epoch, Frames.Frame.ICRF
    )
);

// Debris object (from TLE)
string debrisLine1 = "1 12345U 80001A   25015.50000000  .00000000  00000-0  00000-0 0    10";
string debrisLine2 = "2 12345  51.5000  10.0000 0010000  90.0000 270.0000 15.50000000    08";
var debrisTLE = new TLE("Debris Object", debrisLine1, debrisLine2);
var debrisOrbit = debrisTLE.ToStateVector(earth, Frames.Frame.ICRF);

// Search for close approaches over 7 days
var conjunctionSearchWindow = new Window(epoch, epoch + TimeSpan.FromDays(7));

double minDistance = double.MaxValue;
Time closestApproachTime = epoch;

// Check every minute
for (var t = epoch; t < conjunctionSearchWindow.EndDate; t += TimeSpan.FromMinutes(1))
{
    var satState = primarySat.InitialOrbitalParameters.ToStateVector(t);
    var debrisState = debrisOrbit.AtEpoch(t);

    var relativePosition = debrisState.Position - satState.Position;
    double distance = relativePosition.Magnitude();

    if (distance < minDistance)
    {
        minDistance = distance;
        closestApproachTime = t;
    }
}

Console.WriteLine("=== Conjunction Analysis ===");
Console.WriteLine($"Closest approach: {closestApproachTime}");
Console.WriteLine($"Miss distance: {minDistance / 1000:F3} km");

if (minDistance < 5000.0)  // 5 km warning threshold
{
    Console.WriteLine("WARNING: Close approach detected!");
    Console.WriteLine("Recommend collision avoidance maneuver assessment.");
}
```

---

## FAQ and Common Issues

### General Questions

**Q: Do I need to download SPICE kernels separately?**

A: Yes. SPICE kernels are not included with the framework due to size. Download from [NAIF](https://naif.jpl.nasa.gov/naif/data.html):
- Planetary ephemeris (SPK): `de440.bsp` or similar
- Leap seconds (LSK): `naif0012.tls` or latest
- Planetary constants (PCK): `pck00011.tpc` or latest

**Q: Which time system should I use?**

A:
- **TDB (Barycentric Dynamical Time)**: For ephemeris calculations and orbital mechanics
- **UTC**: For human-readable times and mission planning
- **TAI**: When you need monotonic time without leap seconds
- Always convert UTC → TDB for calculations, then TDB → UTC for display

**Q: What coordinate frame should I use for orbital mechanics?**

A: Use **ICRF** (International Celestial Reference Frame) for most orbital mechanics. It's an inertial frame aligned with J2000 but more precisely defined. Use body-fixed frames only when working with surface coordinates.

**Q: How accurate are the calculations?**

A: Accuracy depends on:
- SPICE kernels: Planetary ephemeris accurate to meters
- Orbital propagation: Integrator-dependent, typically sub-meter for short durations
- Atmospheric models: NRLMSISE-00 accurate to ~15% in thermosphere
- TLE propagation: Accuracy degrades; best for near-term predictions (<7 days)

**Q: Is this framework thread-safe?**

A: The native SPICE calls use internal locking, but you should avoid concurrent modifications to the same objects. Reading ephemeris from multiple threads is safe.

### Common Errors

**Error: "Kernel not loaded" or "Insufficient ephemeris data"**

**Solution:**
```csharp
// Always load kernels at startup
API.Instance.LoadKernels(new DirectoryInfo("path/to/kernels"));

// Verify kernels are loaded
// Check that your kernel directory contains:
// - SPK files (ephemeris)
// - LSK files (leap seconds)
// - PCK files (planetary constants)
```

**Error: "Frame not recognized"**

**Solution:** Ensure you're using built-in frames or have loaded frame kernels:
```csharp
// Use standard frames
Frames.Frame.ICRF
earth.Frame  // Body-fixed frames from CelestialBody
```

**Error: "Time out of bounds for ephemeris"**

**Solution:** Your requested time is outside the range of loaded kernels:
```csharp
// Check kernel coverage
// Most planetary kernels cover ~1900-2100
// TLE ephemeris is only valid near the epoch

// Use appropriate time ranges
var epoch = new TimeSystem.Time(2025, 1, 1, frame: TimeFrame.UTCFrame);
// Not: years before 1900 or after 2100 (outside kernel coverage)
```

**Error: "Insufficient fuel" exception during maneuver**

**Solution:**
```csharp
// Check fuel before executing maneuver
if (spacecraft.FuelTanks.Sum(t => t.Quantity) >= maneuver.FuelRequired)
{
    maneuver.TryExecute(currentState);
}
else
{
    Console.WriteLine($"Insufficient fuel: {maneuver.FuelRequired:F1} kg required, " +
                      $"{spacecraft.FuelTanks.Sum(t => t.Quantity):F1} kg available");
}
```

### Performance Tips

**Tip 1: Minimize kernel loads**
```csharp
// Load once at application startup
// DON'T reload in loops or frequently called methods
API.Instance.LoadKernels(kernelPath);  // Once per application lifetime
```

**Tip 2: Reuse objects**
```csharp
// Create bodies once and reuse
var earth = PlanetsAndMoons.EARTH_BODY;  // Create once
var moon = new CelestialBody(PlanetsAndMoons.MOON);  // Reuse

// Don't recreate in loops
for (int i = 0; i < 1000; i++)
{
    // Good: Reuse earth
    var state = satellite.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None);
}
```

**Tip 3: Choose appropriate step sizes**
```csharp
// For searches, use reasonable step sizes
// Too small: Slow performance
// Too large: May miss events

// For LEO (90-minute period): 10-60 second steps
API.Instance.FindWindowsInFieldOfViewConstraint(..., stepSize: 30.0);

// For deep space: larger steps acceptable
API.Instance.FindWindowsOnOccultationConstraint(..., stepSize: 300.0);
```

**Tip 4: Use appropriate propagation methods**
```csharp
// For short durations (<hours), simple propagation is fine
var futureState = currentState.AtEpoch(epoch + TimeSpan.FromMinutes(10));

// For long durations or high-fidelity, use numerical integrators
// with perturbation forces
var propagator = new VVIntegrator();
// Configure forces (gravity, drag, SRP, etc.)
```

### Best Practices

**1. Always specify units in comments:**
```csharp
double altitude = 550000.0;  // meters
double inclination = 51.6 * Constants.Deg2Rad;  // radians (converted from degrees)
double isp = 450.0;  // seconds
```

**2. Use meaningful NAIF IDs:**
```csharp
// Negative IDs for spacecraft (avoids conflicts with planetary bodies)
// Use organized numbering scheme:
// -100xxx: Earth satellites
// -200xxx: Mars satellites
// -300xxx: Deep space probes
var spacecraft = new Spacecraft(-100001, "EarthSat1", ...);
```

**3. Check maneuver feasibility:**
```csharp
// Before executing maneuver
if (maneuver.CanExecute(currentState))
{
    var (newState, orientation) = maneuver.TryExecute(currentState);
    // Use newState
}
```

**4. Handle aberration appropriately:**
```csharp
// For display/visualization: use geometric (None)
var ephemeris = body.GetEphemeris(epoch, observer, frame, Aberration.None);

// For observations: use light-time correction
var ephemeris = body.GetEphemeris(epoch, observer, frame, Aberration.LT);

// For stellar observations: use light-time + stellar aberration
var ephemeris = star.GetEphemeris(epoch, observer, frame, Aberration.LTS);
```

**5. Validate inputs:**
```csharp
// Check for physically impossible orbits
if (keplerianElements.Eccentricity() >= 1.0)
{
    throw new ArgumentException("Eccentricity must be < 1.0 for elliptical orbits");
}

if (keplerianElements.SemiMajorAxis() < earth.EquatorialRadius)
{
    throw new ArgumentException("Orbit intersects Earth surface");
}
```

### Debugging Tips

**Enable detailed error messages:**
```csharp
try
{
    // Your astrodynamics code
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    // Check inner exceptions for SPICE errors
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}
```

**Verify orbital elements:**
```csharp
// Print orbital elements for debugging
Console.WriteLine($"SMA: {orbit.SemiMajorAxis() / 1000:F1} km");
Console.WriteLine($"ECC: {orbit.Eccentricity():F6}");
Console.WriteLine($"INC: {orbit.Inclination() * Constants.Rad2Deg:F2}°");
Console.WriteLine($"Period: {orbit.Period().TotalMinutes:F1} min");
Console.WriteLine($"Apogee: {orbit.Apogee() / 1000:F1} km");
Console.WriteLine($"Perigee: {orbit.Perigee() / 1000:F1} km");
```

**Check for NaN values:**
```csharp
if (double.IsNaN(result) || double.IsInfinity(result))
{
    Console.WriteLine("WARNING: Invalid calculation result");
    // Check inputs for validity
}
```

---

## Additional Resources

- **SPICE Toolkit Documentation**: https://naif.jpl.nasa.gov/naif/documentation.html
- **Orbital Mechanics References**:
  - Vallado, "Fundamentals of Astrodynamics and Applications"
  - Curtis, "Orbital Mechanics for Engineering Students"
- **NRLMSISE-00 Model**: https://www.brodo.de/space/nrlmsise/
- **Celestrak (TLE data)**: https://celestrak.org/

---

## Contributing

Found an issue? Have a suggestion? See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

LGPL-3.0 License. See [LICENSE](https://www.gnu.org/licenses/lgpl-3.0.fr.html) for details.

---

**Happy orbiting!**
