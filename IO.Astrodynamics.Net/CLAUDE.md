# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IO.Astrodynamics.Net is a comprehensive C# library for astrodynamics computations, orbital mechanics, and spacecraft mission planning. The project includes a core library, CLI tool, test suites, and performance benchmarks. It wraps native C++ libraries (via P/Invoke) for high-performance computations and integrates with NASA's SPICE toolkit for accurate ephemeris calculations.

## Solution Structure

The solution consists of 5 main projects:

- **IO.Astrodynamics** - Core library containing all astrodynamics functionality
- **IO.Astrodynamics.CLI** - Command-line interface tool (published as `astro` NuGet tool)
- **IO.Astrodynamics.Tests** - Unit and integration tests using xUnit
- **IO.Astrodynamics.CLI.Tests** - CLI-specific tests
- **IO.Astrodynamics.Performance** - Performance benchmarking using BenchmarkDotNet

## Build and Test Commands

### Building the Solution
```bash
# Build entire solution
dotnet build IO.Astrodynamics.sln

# Build specific project
dotnet build IO.Astrodynamics/IO.Astrodynamics.csproj

# Build CLI tool
dotnet build IO.Astrodynamics.CLI/IO.Astrodynamics.CLI.csproj
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj

# Run CLI tests
dotnet test IO.Astrodynamics.CLI.Tests/IO.Astrodynamics.CLI.Tests.csproj

# Run single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

### CLI Tool Commands
```bash
# Install CLI globally
dotnet pack IO.Astrodynamics.CLI/IO.Astrodynamics.CLI.csproj
dotnet tool install --global --add-source ./IO.Astrodynamics.CLI/bin/Debug astro

# Available commands
astro ephemeris      # Generate ephemeris data
astro orientation    # Calculate spacecraft orientation
astro geometry       # Geometry finder operations
astro orbital        # Convert orbital parameters
astro frame          # Frame conversions
astro time           # Time system conversions
astro body           # Celestial body information
astro propagate      # Orbit propagation
```

## Architecture Overview

### Core Components

1. **API.cs** - Main native library interface using P/Invoke to communicate with C++ backend
2. **Body/** - Celestial bodies, spacecraft, instruments, and related components
3. **OrbitalParameters/** - State vectors, Keplerian elements, TLE handling
4. **Maneuver/** - Launch, attitude, and orbital maneuvers
5. **TimeSystem/** - Time frame management and conversions
6. **Math/** - Mathematical utilities for astrodynamics
7. **Propagator/** - Orbit propagation algorithms and integrators

### Key Design Patterns

- **Singleton Pattern**: `API.Instance` and `Configuration.Instance` for global state
- **Strategy Pattern**: `IDataProvider` with `SpiceDataProvider` and `MemoryDataProvider`
- **P/Invoke Integration**: All native calls are centralized in `API.cs` with thread-safe locking
- **DTO Pattern**: Separate DTO classes in `DTO/` for native interop

### Native Library Integration

The project uses native libraries for performance-critical calculations:
- `IO.Astrodynamics.dll` (Windows) / `libIO.Astrodynamics.so` (Linux)
- `One_Sgp4.dll` for SGP4 propagation
- Thread-safe access via lock object in `API.cs`

### Data Management

- **Kernel Loading**: SPICE kernels are loaded via `API.LoadKernels()`
- **Data Files**: Solar system data in `Data/SolarSystem/` directories
- **Templates**: SPICE templates in `Templates/` for frame and instrument definitions

## Development Guidelines

### Code Conventions
- Target Framework: .NET 8.0
- Language Version: C# 12
- Nullable disabled, ImplicitUsings disabled
- Follow SOLID principles and software craftsmanship practices
- Maintain >95% code coverage for all new code
- Use feature branches for all development

### Testing Requirements
- Unit tests are mandatory for all new functionality
- Tests use xUnit framework with BenchmarkDotNet for performance tests
- Test data files are included in test projects with `CopyToOutputDirectory` settings
- CLI tests verify command-line functionality end-to-end

### Performance Considerations
- All SPICE toolkit calls are thread-safe via centralized locking in `API.cs`
- Large ephemeris operations are batched in chunks of 10,000 records
- Native library resolution handles cross-platform deployment

### Common Development Tasks

#### Adding New Celestial Bodies
1. Update `SolarSystemObjects/` classes
2. Add NAIF ID constants
3. Update relevant test data

#### Adding New Orbital Parameters
1. Create class in `OrbitalParameters/`
2. Add corresponding DTO in `DTO/`
3. Add conversion methods
4. Implement P/Invoke calls in `API.cs`

#### Adding New Maneuvers
1. Create class in `Maneuver/`
2. Implement base `Maneuver` or `Attitude` class
3. Add integration with `Spacecraft` class
4. Add comprehensive unit tests

## Dependencies

### Core Dependencies
- MathNet.Numerics (5.0.0) - Mathematical computations
- MathNet.Filtering.Kalman (0.7.0) - Filtering algorithms

### Test Dependencies  
- xUnit - Testing framework
- BenchmarkDotNet - Performance benchmarking
- coverlet.collector - Code coverage

### CLI Dependencies
- Cocona (2.2.0) - Command-line framework

## File Organization

- Configuration files and native libraries in `resources/`
- SPICE kernel templates in `Templates/`
- Test data organized by project in `Data/` subdirectories
- PDS (Planetary Data System) schemas and validation in `PDS/`

## Native Library Deployment

The native libraries are embedded as Content with `CopyToOutputDirectory=Always`:
- Cross-platform library loading handled in `API.Resolver()`
- Libraries must be deployed with applications using the framework