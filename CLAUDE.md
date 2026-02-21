# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IO.Astrodynamics is a .NET 8/10 astrodynamics toolkit with a thin C++ interop layer for NASA/JPL NAIF CSPICE.

**Critical**: The C++ layer (`IO.Astrodynamics/`) is **feature-frozen**. All new development targets the .NET projects in `IO.Astrodynamics.Net/`.

## Commands

All .NET commands run from within `IO.Astrodynamics.Net/`:

```bash
# Build
dotnet build IO.Astrodynamics.sln

# Run all tests
dotnet test

# Run tests for a specific project
dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~IO.Astrodynamics.Tests.ClassName.TestName"
dotnet test --filter "DisplayName~TLE"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run the CLI tool
dotnet run --project IO.Astrodynamics.CLI -- --help
```

C++ build (only needed if modifying the native layer, which should not happen):
```bash
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

## Architecture

### Two-Layer Design

```
IO.Astrodynamics/            ← C++ (FROZEN): P/Invoke bridge to CSPICE
IO.Astrodynamics.Net/        ← .NET (ACTIVE): All new development here
  ├── IO.Astrodynamics/      ← Core library
  ├── IO.Astrodynamics.CLI/  ← CLI tool (astro command)
  ├── IO.Astrodynamics.Tests/
  ├── IO.Astrodynamics.CLI.Tests/
  └── IO.Astrodynamics.Performance/  ← BenchmarkDotNet benchmarks
```

The native bridge is accessed via `IO.Astrodynamics.API` (singleton, P/Invoke). All SPICE calls must go through the shared lock object — CSPICE is not thread-safe.

### Key Namespaces

- `IO.Astrodynamics.Body`: Celestial bodies, spacecraft, instruments
- `IO.Astrodynamics.OrbitalParameters`: StateVector, KeplerianElements, TLE, EquinoctialElements
- `IO.Astrodynamics.CCSDS.OMM` / `.OPM`: CCSDS standards (read/write/validate/convert)
- `IO.Astrodynamics.Propagator`: SpacecraftPropagator (Velocity-Verlet), SmallBodyPropagator
- `IO.Astrodynamics.Propagator.Forces`: AtmosphericDrag, SolarRadiationPressure
- `IO.Astrodynamics.Maneuver`: Lambert, impulsive maneuvers, attitude maneuvers, IAttitudeTarget
- `IO.Astrodynamics.Atmosphere`: IAtmosphericModel, EarthStandardAtmosphere, Nrlmsise00Model
- `IO.Astrodynamics.Physics`: GeopotentialGravitationalField (EGM2008, up to degree 70)
- `IO.Astrodynamics.Frames`: Reference frame transformations (ICRF, TEME, ITRF93, etc.)
- `IO.Astrodynamics.TimeSystem`: Time frames (UTC, TDB, TAI, etc.)
- `IO.Astrodynamics.Math`: Vector3, Matrix, Quaternion, Legendre functions

### Data Provider Pattern

```csharp
// Default: SPICE kernel files
Configuration.Instance.SetDataProvider(new SpiceDataProvider());

// Testing: in-memory, no kernel files required
Configuration.Instance.SetDataProvider(new MemoryDataProvider());
```

Test classes load SPICE kernels in their constructor:
```csharp
API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
```
Test data files live in `Data/SolarSystem/` and are copied to the output directory.

## Critical Domain Rules

### Mean vs. Osculating Elements

`OrbitalElementsType` enum: `Osculating` (instantaneous, can convert to StateVector) vs. `Mean` (averaged, require a specific propagator).

- **Mean elements CANNOT call `ToStateVector()`** — throws `InvalidOperationException`
- `TLE.ToStateVector()` works because it uses SGP4/SDP4 internally
- Use `TLE.ToOsculating()` to get a usable StateVector from a TLE
- Use `TLE.ToMeanKeplerianElements()` to get mean elements for TLE creation
- `KeplerianElements.FromOMM()` caches original mean motion to prevent precision loss on round-trip

### Force Model Defaults

- Drag coefficient (Cd) default: **2.2** (free-molecular flow), not 0.3
- Atmospheric drag uses **atmosphere-relative velocity** (body velocity minus co-rotation: `v_rel = v_body - omega × r_body`)
- SRP reflectivity Cr comes from `Spacecraft.SolarRadiationCoeff` (range 1.0–2.0, default 1.0)
- SRP uses **continuous shadow fraction** (partial/annular eclipses scale SRP proportionally)
- Both forces use **dynamic mass** via `GetTotalMass()` (dry + fuel + payload)

### Thread Safety

- CSPICE P/Invoke calls: use shared lock — **not thread-safe**
- `GeopotentialGravitationalField`: **NOT thread-safe** — create one instance per thread/propagator
- `Nrlmsise00Model`: thread-safe, concurrent use OK

### Geopotential

- EGM2008 model file: `Data/SolarSystem/EGM2008_to70_TideFree` (degrees 2–70)
- Pass `GeopotentialModelParameters(filePath, degree)` to `CelestialBody` constructor
- Legendre functions: geodesy normalization, no Condon-Shortley phase
- Degree 0 and 1 are absent from the file; handled as zeros internally

### CCSDS OMM/OPM

- OMM COSPAR ID format: `"1998-067A"` (with hyphen); TLE format: `"98067A"` — conversion is automatic
- Check `omm.IsTleCompatible` before calling `ToTle()`
- OPM uses CCSDS units (km, km/s, deg); framework uses SI (m, m/s, rad) — conversions are automatic
- Maneuver conversion is one-way: `Spacecraft → OPM` (OPM maneuvers are archival)

### Attitude Maneuvers

- **TriadAttitude**: Fully-constrained 3-DOF via TRIAD algorithm — use when roll must be defined
- **Single-vector attitudes** (`NadirAttitude`, `ProgradeAttitude`, etc.): leave roll unconstrained
- Primary + secondary body vectors must be ≥5 degrees apart (prevents numerical instability)
- `IAttitudeTarget` mixes orbital directions (`OrbitalDirectionTarget.Prograde`, etc.) and celestial bodies (`CelestialAttitudeTarget`)
- Factory methods: `TriadAttitude.CreateLVLH()`, `TriadAttitude.CreateProgradeWithSunTracking()`
- Configurable body axes per spacecraft instance (`BodyFront`, `BodyRight`, `BodyUp`); must be orthogonal and right-handed

## Code Quality

- Unit test coverage must exceed 95%
- OOP, SOLID, and DRY principles required
- Feature branches required before merge (see `CONTRIBUTING.md`)
