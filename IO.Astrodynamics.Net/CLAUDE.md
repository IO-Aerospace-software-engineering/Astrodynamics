# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IO.Astrodynamics.Net is a .NET 8/10 astrodynamics framework for orbital mechanics calculations, ephemeris computations, and space mission planning. It consists of:

- **IO.Astrodynamics**: Core framework library with orbital mechanics algorithms
- **IO.Astrodynamics.CLI**: Command-line interface tool (`astro`) for astrodynamics operations
- **IO.Astrodynamics.Tests**: Unit tests using xUnit framework
- **IO.Astrodynamics.CLI.Tests**: CLI tests
- **IO.Astrodynamics.Performance**: Performance benchmarking using BenchmarkDotNet

## Key Commands

### Build
```bash
dotnet build IO.Astrodynamics.sln
```

### Test
```bash
# Run all tests
dotnet test

# Run specific project tests
dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj
dotnet test IO.Astrodynamics.CLI.Tests/IO.Astrodynamics.CLI.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run a single test by fully qualified name
dotnet test --filter "FullyQualifiedName~IO.Astrodynamics.Tests.ClassName.TestName"

# Run tests matching a pattern
dotnet test --filter "DisplayName~TLE"
```

### CLI Tool
```bash
# Build and run the CLI
dotnet run --project IO.Astrodynamics.CLI -- [command]

# Install CLI globally
dotnet tool install --global --add-source ./IO.Astrodynamics.CLI/bin/Debug IO.Astrodynamics.CLI
```

## Architecture

### Core Components

**Native Interop Layer (IO.Astrodynamics/API.cs)**
- P/Invoke wrapper around native SPICE toolkit (NASA's CSPICE)
- Platform-specific libraries: `IO.Astrodynamics.dll` (Windows), `libIO.Astrodynamics.so` (Linux)
- Thread-safe access through lock synchronization

**Data Provider Pattern**
- `IDataProvider` interface with implementations:
  - `SpiceDataProvider`: Default, uses SPICE kernel files
  - `MemoryDataProvider`: In-memory for testing
- Configured via `Configuration.Instance.SetDataProvider()`

**Key Namespaces**
- `IO.Astrodynamics.Body`: Celestial bodies, spacecraft, instruments
- `IO.Astrodynamics.OrbitalParameters`: State vectors, Keplerian elements, TLE, mean/osculating elements
- `IO.Astrodynamics.OrbitalParameters.TLE`: Two-Line Element sets and OMM support
- `IO.Astrodynamics.CCSDS.OMM`: CCSDS Orbit Mean-elements Message support (read/write/validate/convert)
- `IO.Astrodynamics.Maneuver`: Lambert solvers, launch windows, maneuver planning
- `IO.Astrodynamics.Frames`: Reference frames and transformations
- `IO.Astrodynamics.TimeSystem`: Time frames (UTC, TDB, TAI, etc.)
- `IO.Astrodynamics.Propagator`: Orbital propagation and integration
- `IO.Astrodynamics.Atmosphere`: Atmospheric density, temperature, and pressure models for Earth and Mars

**External Dependencies**
- MathNet.Numerics: Linear algebra operations
- MathNet.Filtering.Kalman: Kalman filtering for state estimation
- Cocona: CLI framework (CLI project only)
- xUnit + BenchmarkDotNet: Testing and benchmarking

### Orbital Elements: Mean vs Osculating

The framework distinguishes between two types of orbital elements using `OrbitalElementsType`:

**OrbitalElementsType Enum**
- `Osculating`: Instantaneous Keplerian orbit at a specific epoch (default)
- `Mean`: Averaged elements used by analytical propagators (TLE/SGP4, OMM)

**Key Concepts**
- **Osculating elements** represent the instantaneous two-body orbit that would result if all perturbations vanished. They can be directly converted to position/velocity (StateVector).
- **Mean elements** are averaged over time to remove short-periodic variations. They require a specific propagator (SGP4/SDP4) to compute actual position/velocity.

**Important Conversion Rules**
1. **Mean elements CANNOT be directly converted to StateVector** - attempting to call `ToStateVector()` on mean KeplerianElements throws `InvalidOperationException`
2. **TLE overrides this behavior** - `TLE.ToStateVector()` uses SGP4/SDP4 propagation internally
3. **ElementsType propagates** through conversions (e.g., mean KeplerianElements → mean EquinoctialElements)

**Creating Mean Elements from OMM Data**
```csharp
// FromOMM accepts OMM native units: rev/day for mean motion, degrees for angles
var meanKep = KeplerianElements.FromOMM(
    meanMotion: 15.49309423,        // rev/day
    eccentricity: 0.0000493,
    inclination: 51.6423,           // degrees
    raan: 353.0312,                 // degrees
    argumentOfPeriapsis: 320.8755,  // degrees
    meanAnomaly: 39.2360,           // degrees
    observer: earth,
    epoch: epoch,
    frame: Frame.TEME);

// The mean motion is cached internally to preserve precision
// when converting back to TLE format
Assert.Equal(OrbitalElementsType.Mean, meanKep.ElementsType);

// This would throw InvalidOperationException:
// var sv = meanKep.ToStateVector();  // ERROR!
```

**Working with TLE**
```csharp
// Parse TLE - elements are mean by definition
var tle = new TLE("ISS",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

Assert.Equal(OrbitalElementsType.Mean, tle.ElementsType);

// Get osculating state vector via SGP4 propagation
var osculating = tle.ToOsculating();           // At TLE epoch
var osculatingLater = tle.ToOsculating(epoch); // At specific epoch
Assert.Equal(OrbitalElementsType.Osculating, osculating.ElementsType);

// Get mean Keplerian elements (for creating new TLEs)
var meanKep = tle.ToMeanKeplerianElements();
Assert.Equal(OrbitalElementsType.Mean, meanKep.ElementsType);

// Create TLE from mean elements (preserves mean motion precision)
var newTle = TLE.Create(meanKep, "ISS", 25544, "98067A", 2570,
    Classification.Unclassified, bstar: 0.0001027);
```

**Mean Motion Precision Preservation**
When converting OMM → KeplerianElements → TLE, mean motion is cached to avoid precision loss from round-trip conversions:
```
OMM: meanMotion = 15.49309423 rev/day
  → FromOMM() caches original mean motion
  → MeanMotion() returns cached value (not recomputed from semi-major axis)
  → TLE output: 15.49309423 rev/day (EXACT)
```

**Osculating vs Mean Accessors in TLE**
| Accessor | Returns | Use Case |
|----------|---------|----------|
| `MeanSemiMajorAxis`, `MeanEccentricity`, etc. | Mean elements | Creating TLEs, orbit comparison |
| `SemiMajorAxis()`, `Eccentricity()`, etc. | Osculating (via SGP4) | Physical calculations |
| `ToOsculating()` | Osculating StateVector | Position/velocity calculations |
| `ToMeanKeplerianElements()` | Mean KeplerianElements | TLE creation |

### CCSDS OMM (Orbit Mean-elements Message)

The `IO.Astrodynamics.CCSDS.OMM` namespace provides full support for CCSDS Orbit Mean-elements Message format (CCSDS 502.0-B-3, NDM/XML Blue Book).

**Key Classes**
- `Omm`: Main class representing a complete OMM document
- `OmmReader`: Parses OMM from XML files/strings/streams
- `OmmWriter`: Writes OMM to XML files/strings/streams
- `OmmValidator`: Validates OMM content for CCSDS compliance
- `MeanElements`, `TleParameters`, `OmmMetadata`: Data structures

**Loading and Saving OMM**
```csharp
// Load OMM from file (with optional validation)
var omm = Omm.LoadFromFile("satellite.omm", validateSchema: true, validateContent: true);

// Access data
Console.WriteLine($"Object: {omm.ObjectName}");
Console.WriteLine($"COSPAR ID: {omm.ObjectId}");  // Format: "1998-067A"
Console.WriteLine($"Mean Motion: {omm.Data.MeanElements.MeanMotion} rev/day");

// Save OMM to file
omm.SaveToFile("output.omm", validateBeforeSave: true, wrapInNdm: true, indent: true);
```

**OMM ↔ TLE Bidirectional Conversion**
```csharp
// OMM to TLE (for SGP4 propagation)
var omm = Omm.LoadFromFile("iss.omm");
if (omm.IsTleCompatible)
{
    var tle = omm.ToTle();
    var stateVector = tle.ToStateVector();  // Propagate with SGP4
}

// TLE to OMM (for archiving/sharing)
var tle = new TLE("ISS", line1, line2);
var omm = tle.ToOmm(originator: "My Organization");
omm.SaveToFile("iss.omm");
```

**COSPAR ID Format Handling**
- OMM uses full format: `"1998-067A"` (9 chars with hyphen)
- TLE uses abbreviated format: `"98067A"` (6-8 chars)
- Conversion is automatic in `ToTle()` and `ToOmm()` methods

**TLE Precision Limitations**
When converting OMM → TLE → OMM, expect some precision loss:
- Angles: ~4 decimal places (TLE format constraint)
- BSTAR: ~6 decimal places
- Mean motion is preserved exactly (cached internally)

### Atmospheric Modeling

The framework provides a unified atmospheric modeling system with support for multiple planets and models of varying complexity.

**Architecture**
- `IAtmosphericModel` interface: Unified API for all atmospheric models
- `IAtmosphericContext` interface: Encapsulates position, time, and environmental data
- `AtmosphericContext` record: Helper with factory methods `FromAltitude()` and `FromPlanetodetic()`
- `Atmosphere` record: Result type with Temperature, Pressure, Density, and optional Details
- `IAtmosphericDetails` interface: Marker for model-specific details
- `NrlmsiseDetails` record: NRLMSISE-00 specific data (molecular densities, exospheric temp)

All models implement `IAtmosphericModel` with these methods:
- `GetAtmosphere(context)`: Returns `Atmosphere` record with all data + optional model-specific details
- `GetTemperature(context)`: Temperature in Celsius
- `GetPressure(context)`: Pressure in kPa
- `GetDensity(context)`: Density in kg/m³

**Available Models**

**Earth Models:**
1. **EarthStandardAtmosphere** (U.S. Standard Atmosphere 1976)
   - Simple analytical model valid up to ~86 km altitude
   - Uses only altitude (ignores time and space weather)
   - Fast and suitable for preliminary analysis
   - Details property is null

2. **Nrlmsise00Model** (NRLMSISE-00)
   - Full empirical model valid from ground to exosphere (0-2000+ km)
   - Complete C# implementation of NRLMSISE-00 algorithm
   - Requires full context: altitude, position, time, and space weather data (F10.7, Ap indices)
   - Thread-safe for concurrent use
   - Returns `NrlmsiseDetails` with molecular densities (He, O, N₂, O₂, Ar, H, N, anomalous O) and exospheric temperature
   - Advanced features:
     - Configurable via `NrlmsiseFlags` for enabling/disabling variations
     - `SpaceWeather` presets: `Nominal`, `SolarMinimum`, `SolarMaximum`, `Moderate`
     - Direct access via `NRLMSISE00` class for low-level control

**Mars Models:**
- **MarsStandardAtmosphere**: Simplified analytical model using altitude only

**Usage Pattern**
```csharp
// Simple case - altitude only (standard atmosphere)
var context = AtmosphericContext.FromAltitude(10000);  // 10 km
var model = new EarthStandardAtmosphere();
var atmosphere = model.GetAtmosphere(context);
// atmosphere.Temperature, .Pressure, .Density, .Details (null for simple models)

// Full context with NRLMSISE-00
var context = AtmosphericContext.FromPlanetodetic(
    altitude: 400000,                           // 400 km
    geodeticLatitude: 45 * Constants.Deg2Rad,
    geodeticLongitude: -75 * Constants.Deg2Rad,
    epoch: new Time(2024, 6, 21, 12, 0, 0)
);
var model = new Nrlmsise00Model(SpaceWeather.Nominal);
var atmosphere = model.GetAtmosphere(context);

// Access model-specific details via pattern matching
if (atmosphere.Details is NrlmsiseDetails details)
{
    var o2Density = details.MolecularOxygenDensity;  // m⁻³
    var exoTemp = details.ExosphericTemperature;     // K
}

// Via CelestialBody (auto-selects NRLMSISE-00 for Earth with full context)
var earth = PlanetsAndMoons.EARTH_BODY;
var atm = earth.GetAtmosphere(context);  // Uses NRLMSISE-00 automatically
```

**Model Selection Guidelines**
- Use `EarthStandardAtmosphere` for quick calculations below 86 km without time-dependent effects
- Use `Nrlmsise00Model` for accurate high-altitude modeling, time-varying conditions, or space weather effects
- Use `CelestialBody.GetAtmosphere(context)` with full context for automatic NRLMSISE-00 selection
- Use `MarsStandardAtmosphere` for preliminary Mars mission analysis

### Testing Approach

All test classes load SPICE kernels in constructor:
```csharp
API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
```

Test data files are in `Data/SolarSystem/` and copied to output directory.

## Development Guidelines

1. **Thread Safety**: CSPICE operations are not thread-safe - all API calls must use the shared lock object
2. **Kernel Management**: Load/unload SPICE kernels appropriately to avoid memory leaks
3. **Native Resources**: Properly free unmanaged memory returned from native calls
4. **Time Systems**: Be explicit about time frames when working with epochs
5. **Coordinate Systems**: Always specify reference frames for state vectors and transformations
6. **Mean vs Osculating Elements**: Always check `ElementsType` when working with orbital parameters:
   - Use `ToOsculating()` for TLE position/velocity calculations
   - Never call `ToStateVector()` directly on mean KeplerianElements
   - Use `TLE.Create()` only with mean elements (validates `ElementsType.Mean`)
7. **CCSDS OMM Handling**: When working with OMM files:
   - Use `Omm.LoadFromFile()` with validation for production code
   - Check `IsTleCompatible` before calling `ToTle()`
   - Use `TLE.ToOmm()` to convert TLE data for CCSDS-compliant archiving
   - COSPAR ID format conversion is automatic (OMM: "1998-067A" ↔ TLE: "98067A")

## Code Quality Standards

Per CONTRIBUTING.md:
- OOP, SOLID, and DRY principles required
- Unit test coverage must exceed 95%
- Feature branches required before merge