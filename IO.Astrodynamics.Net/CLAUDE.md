# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IO.Astrodynamics.Net is a .NET 8 astrodynamics framework for orbital mechanics calculations, ephemeris computations, and space mission planning. It consists of:

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

**External Dependencies**
- MathNet.Numerics: Linear algebra operations
- Cocona: CLI framework (CLI project only)
- xUnit + BenchmarkDotNet: Testing and benchmarking

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