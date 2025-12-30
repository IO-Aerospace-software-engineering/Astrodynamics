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
- `IO.Astrodynamics.OrbitalParameters`: State vectors, Keplerian elements, TLE
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

## Code Quality Standards

Per CONTRIBUTING.md:
- OOP, SOLID, and DRY principles required
- Unit test coverage must exceed 95%
- Feature branches required before merge