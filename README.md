# IO.Astrodynamics

[![IO Astrodynamics CI](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/ci.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/ci.yml)
[![IO Astrodynamics CD](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cd.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cd.yml)

A modern .NET astrodynamics toolkit powered by NASA/JPL NAIF SPICE. IO.Astrodynamics delivers SPICE accuracy with a productive .NET API; a thin, stable C++ layer provides fast interop with CSPICE.

Important: The C++ layer is feature-frozen and exists only for communication with SPICE. All new features and APIs will land in the .NET projects.


## What you can do

- Work with SPICE kernels and star catalogs
- Export simulations to Cosmographia
- Manage PDS archives (generate, materialize objects, validate against XML schemas)
- Ephemerides and propagation
  - Spacecraft propagator: geopotential models, simplified atmospheres (Earth, Mars), solar radiation pressure, n-body perturbations, impulsive maneuvers, fuel balance
  - Small-body propagator: geopotential (Earth), simplified atmospheres (Earth, Mars), SRP, n-body
- Orbital parameters: compute/convert State Vector, TLE, Equinoctial, Keplerian
- Frames and coordinates: ICRF/J2000, Ecliptic (J2000/B1950), TEME, Galactic, FK4, body-fixed/ITRF93, Equatorial/Horizontal/Planetodetic/Planetographic
- Spacecraft configuration: clocks, fuel tanks, engines, instruments
- Impulsive maneuvers: Lambert transfers, apogee/perigee height changes, plane/apsidal alignment, phasing, combined maneuvers
- Attitudes: instrument pointing, nadir/zenith, prograde/retrograde
- Time systems: Calendar, Julian, TDB, UTC, local; conversions included
- Event finding: distance, occultation, coordinate, illumination constraints; instrument field-of-view windows
- Kernel management utilities
- Math helpers: vectors, matrices, planes, quaternions, Jacobians, interpolation (Lagrange), Legendre polynomials, SLERP/LERP
- Use via CLI for common tasks (props, conversions, info queries)


## How it works

- SPICE data: You supply NAIF kernels (ephemerides, planetary constants, leap seconds, mission data)
- Native bridge: A small C++ interop library talks to CSPICE for performance-critical calls (frozen API)
- .NET SDK: Rich, evolving API surface for scenarios, modeling, and utilities
- CLI: Command-line tools built on the .NET SDK for quick analyses

Links:
- SPICE concept: https://naif.jpl.nasa.gov/naif/spiceconcept.html
- SPICE data: https://naif.jpl.nasa.gov/naif/data.html


## Install (.NET)

This package is on NuGet: https://www.nuget.org/packages/IO.Astrodynamics/

Install into your project:

```bash
 dotnet add package IO.Astrodynamics
```

Requirements:
- Supported OS: Windows and Linux x64 (native CSPICE interop is bundled)
- SPICE kernels: you must load the kernels required for your computations


## Quick start (C#)

Load kernels once at startup, then query ephemerides, frames, and more.

```csharp
// Load required kernels (recursively) from your data directory
API.Instance.LoadKernels(new DirectoryInfo("<your path containing data>"));

// Example: Moon ephemeris at 2000-01-01T12:00Z in ICRF with Earth as center
var earth = new CelestialBody(PlanetsAndMoons.EARTH);
var moon  = new CelestialBody(PlanetsAndMoons.MOON);
var epoch = new TimeSystem.Time(new DateTime(2000, 1, 1, 12, 0, 0), TimeFrame.UTCFrame);
var eph   = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None);
```

More examples: https://github.com/IO-Aerospace-software-engineering/Astrodynamics.Net/wiki/Examples


## Using the CLI

A CLI is included in the repository for quick tasks (propagation, conversions, info queries, event finding).

- Build and show help:

```bash
 dotnet run --project IO.Astrodynamics.Net/IO.Astrodynamics.CLI -- --help
```

- Typical commands include: sub-observer point, angular separation, orientation, orbital/frame/time conversions, event window finding (distance, occultation, FoV, illumination).


## Getting SPICE data

You need to provide the kernels relevant to your scenario. Common sets:
- Leap seconds (e.g., naif0012.tls)
- Planetary constants (e.g., pck files)
- Ephemerides (e.g., de430.bsp, planetary/spacecraft SPKs)
- Frames and spacecraft clocks as needed

Organize them in a directory and load them recursively:

```csharp
API.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
```


## Native C++ interop (frozen)

The native C++ library exists only as a high-performance bridge to CSPICE. It won’t receive new features. If you need to link it directly:
- Download prebuilt binaries from Releases: https://github.com/IO-Aerospace-software-engineering/Astrodynamics/releases
- Linux: install headers to /usr/local/include/IO and libIO.Astrodynamics.so to /usr/local/lib
- Windows: place IO.Astrodynamics.dll/.lib alongside your app and include headers

Note: Prefer the .NET SDK for all new work.


## Build status and coverage

[![C++ CMake CI](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cmake.yml)

[![Code coverage](https://img.shields.io/badge/Code%20coverage-Passing-Green.svg)](https://htmlpreview.github.io/?https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/develop/coverage/index.html)


## Sponsoring

Building and sustaining a .NET-first astrodynamics toolkit on top of SPICE takes significant effort across:
- Keeping pace with SPICE releases and kernel updates
- Evolving the .NET API, CLI, and documentation
- Cross-platform packaging and CI (Windows/Linux)
- Extensive tests, validation against reference data, and performance work
- New features and scenarios (propagators, event finders, attitudes, PDS tooling, Cosmographia export)
- Community support, triage, and example curation

How you can help:
- Sponsor the project via GitHub Sponsors to fund ongoing development and maintenance
- Company sponsorships to prioritize features, integrations, or support
- Back specific issues or improvements; contribute kernel sets, scenarios, or docs
- Provide infrastructure (CI minutes/compute) or test hardware when relevant

Where funds go:
- .NET SDK and CLI development (the native C++ bridge is feature-frozen)
- Documentation, tutorials, and examples
- Validation datasets and automated QA
- Release engineering, packaging, and platform support

Sponsor: https://github.com/sponsors/IO-Aerospace-software-engineering


## Contributing

- We welcome contributions to the .NET SDK, CLI, docs, and tests. The native layer accepts only fixes and perf/stability improvements.
- Please read CONTRIBUTING.md and follow the code of conduct.


## License and acknowledgments

- License: See LICENSE
- Security: See SECURITY.md
- Built on NASA/JPL NAIF SPICE. SPICE is developed by the Navigation and Ancillary Information Facility (NAIF) at JPL.
