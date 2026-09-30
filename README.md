# IO.Astrodynamics

[![CI](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/ci.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/ci.yml)
[![CD](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cd.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cd.yml)
[![NuGet](https://img.shields.io/nuget/v/IO.Astrodynamics.svg)](https://www.nuget.org/packages/IO.Astrodynamics/)
[![Downloads](https://img.shields.io/nuget/dt/IO.Astrodynamics.svg)](https://www.nuget.org/packages/IO.Astrodynamics/)
[![License: LGPL-3.0](https://img.shields.io/badge/license-LGPL--3.0--or--later-blue.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/LICENSE)

A modern .NET astrodynamics toolkit powered by NASA/JPL NAIF SPICE. IO.Astrodynamics delivers SPICE accuracy with a productive .NET API; a thin, stable C++ layer provides fast interop with CSPICE.

## Documentation

The reference documentation lives at **[docs.io-aerospace.org](https://docs.io-aerospace.org/)**:

- [Quick Start](https://docs.io-aerospace.org/get-started/) — install, load kernels, first calculations
- [Tutorials](https://docs.io-aerospace.org/tutorials/) — ephemeris queries, coordinate conversions, orbit creation, TLE & OMM, numerical propagation, maneuvers, geometry & visibility, mission design
- [API Reference](https://docs.io-aerospace.org/reference/) — types, methods, and parameters, edition by edition
- [Guides](https://docs.io-aerospace.org/guides/) — kernel management, thread safety, high-fidelity propagation, Cosmographia export, validation
- [Standards & Units](https://docs.io-aerospace.org/standards-and-units/) — SI units, frame and time conventions

A single-file companion reference is also kept in this repository: [DEVELOPER_GUIDE.md](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/DEVELOPER_GUIDE.md).

## Editions

- **Community Edition** — this repository, published as [`IO.Astrodynamics`](https://www.nuget.org/packages/IO.Astrodynamics/) under LGPL-3.0-or-later. Full core API: SPICE integration, orbital parameters, propagation, maneuvers, attitudes, geometry searches, CCSDS OMM/OPM.
- **Professional Edition** — `IO.Astrodynamics.Pro`, proprietary, built on top of this core. It adds the adaptive Prince-Dormand RK7(8) integrator with sub-step event refinement, a fluent propagator builder, albedo and thermal radiation pressure, CIO-based Earth orientation frames (GCRF/CIRS/TIRS), batch propagation, Monte Carlo dispersion analysis, conjunction assessment and CCSDS CDM. Pro features are marked **(Pro)** throughout the documentation; see [Community & Licensing](https://docs.io-aerospace.org/contributing/).

Issues and pull requests are welcome on the Community Edition. The Professional Edition is maintained by IO Aerospace.

## Install

```bash
dotnet add package IO.Astrodynamics
```

Requirements:

- **.NET 10** — the package targets `net10.0` only
- **Supported runtimes**: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`. Native CSPICE interop is bundled for all four and resolved automatically by the NuGet runtime asset layout
- **SPICE kernels**: you must supply the kernels required for your computations (see [Getting SPICE data](#getting-spice-data))

## Quick start (C#)

Load kernels once at startup, then query ephemerides, frames, and more.

```csharp
using System;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

// Load required kernels (recursively) from your data directory
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

// Moon state relative to Earth at 2000-01-01T12:00Z, expressed in ICRF
var earth = new CelestialBody(PlanetsAndMoons.EARTH);
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var epoch = new Time(new DateTime(2000, 1, 1, 12, 0, 0), TimeFrame.UTCFrame);

var state = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None).ToStateVector();

Console.WriteLine($"Position: {state.Position} m");
Console.WriteLine($"Velocity: {state.Velocity} m/s");
```

More worked examples: [docs.io-aerospace.org/tutorials](https://docs.io-aerospace.org/tutorials/).

## What you can do

- **Orbital parameters** — compute and convert State Vector, Keplerian, Equinoctial, TLE (mean and osculating). Mean elements keep their precision; use `ToOsculating()` before any state-vector math. NORAD catalog numbers beyond the 5-digit range are handled through the Alpha-5 convention, up to 339999.
- **Ephemerides** — SPICE-backed ephemeris for bodies, spacecraft, sites and stars, with light-time and stellar aberration corrections. A reference-body strategy avoids solar-system-barycenter round-trips, cutting SPICE calls and allocations for spacecraft- or site-relative ephemeris.
- **Numerical propagation** — central-body propagator with a segment-based loop and event-driven maneuver execution: EGM2008 geopotential gravity (degree/order up to 70, validated against GMAT), atmospheric drag with co-rotation (Earth, Mars), solar radiation pressure with `Cr` and continuous shadow fraction, Battin's third-body perturbations, dynamic mass and fuel balance, cubic Hermite dense output. A 60-second ephemeris grid with 8-point Lagrange interpolation feeds the force models.
- **Integrator** — Velocity-Verlet symplectic fixed-step (the adaptive RK7(8) ships with the Pro edition).
- **Maneuvers** — Lambert transfers, apogee/perigee height changes, plane and apsidal alignment, phasing, combined maneuvers, all triggered by industry-standard g-function zero-crossing event detection.
- **Attitudes** — instrument pointing, nadir/zenith, prograde/retrograde, normal/anti-normal, TRIAD fully-constrained 3-DOF pointing with orbital and celestial targets, configurable body axes.
- **Launch and mission analysis** — launch windows from a launch site to a target orbit, scenario definition and simulation, fuel budgets.
- **Atmospheric modeling** — unified `IAtmosphericModel` interface returning an `Atmosphere` record. Earth: U.S. Standard Atmosphere 1976 and NRLMSISE-00 with space weather. Mars: standard analytical model. Earth picks NRLMSISE-00 automatically when the full context is available.
- **Frames and coordinates** — ICRF/J2000, Ecliptic (J2000/B1950), TEME, Galactic, FK4, body-fixed/ITRF93, plus Equatorial, Horizontal, Planetodetic and Planetographic coordinates.
- **Time systems** — Calendar, Julian, TDB, TAI, TDT, UTC, GPS and local time, with conversions.
- **Event finding** — distance, occultation, coordinate and illumination constraints; instrument field-of-view windows.
- **CCSDS messages** — OMM (Orbit Mean-elements Message) and OPM (Orbit Parameter Message) reading, writing and schema validation, with bidirectional OMM↔TLE and OPM↔Spacecraft conversion.
- **Spacecraft configuration** — clocks, fuel tanks, engines, instruments, configurable body axes.
- **Interoperability** — SPICE kernel and star-catalog handling, Cosmographia export, PDS archive tooling (generate, materialize objects, validate against XML schemas).
- **Math helpers** — vectors, matrices, planes, quaternions, Jacobians, Lagrange interpolation, geodesy-normalized Legendre functions with derivatives, SLERP/LERP.
- **CLI** — common tasks straight from the shell (see below).

## How it works

- **SPICE data**: you supply the NAIF kernels (ephemerides, planetary constants, leap seconds, mission data)
- **Native bridge**: a small C++ interop library talks to CSPICE for performance-critical calls (frozen API)
- **.NET SDK**: the rich, evolving API surface for modeling, propagation and analysis
- **CLI**: command-line tools built on the .NET SDK for quick analyses

Background reading: [SPICE concept](https://naif.jpl.nasa.gov/naif/spiceconcept.html) and [SPICE data](https://naif.jpl.nasa.gov/naif/data.html).

> **The C++ layer is feature-frozen** and exists only to communicate with SPICE. All new features and APIs land in the .NET projects.

## Getting SPICE data

You provide the kernels relevant to your scenario. A common set:

- Leap seconds (e.g. `naif0012.tls`)
- Planetary constants (`.tpc` / PCK files)
- Ephemerides (e.g. `de440.bsp`, plus planetary and spacecraft SPKs)
- Frame kernels and spacecraft clocks as needed

Organize them in a directory and load them recursively:

```csharp
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
```

A single file works too:

```csharp
SpiceAPI.Instance.LoadKernels(new FileInfo("de440.bsp"));
```

See the [kernel management guide](https://docs.io-aerospace.org/guides/kernel-management/) for layout and precedence rules.

## Using the CLI

The CLI ships as a .NET global tool named `astro`:

```bash
dotnet tool install -g IO.Astrodynamics.CLI
astro --help
```

Or run it from a clone of this repository:

```bash
dotnet run --project IO.Astrodynamics.Net/IO.Astrodynamics.CLI -- --help
```

Available commands: `ephemeris`, `propagate`, `orientation`, `sub-point`, `angular-separation`, `celestial-body-info`, `orbital-parameters-converter`, `frame-converter`, `time-converter`, and the window finders `find-windows-from-distance-constraint`, `find-windows-from-occultation-constraint`, `find-windows-from-coordinate-constraint`, `find-windows-from-illumination-constraint`, `find-windows-from-FOV-constraint`.

## Native C++ interop (frozen)

The native C++ library exists only as a high-performance bridge to CSPICE. It won't receive new features (only safety and correctness fixes). If you need to link it directly:

- Download prebuilt binaries from [Releases](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/releases)
- **Linux**: install the headers to `/usr/local/include/IO` and `libIO.Astrodynamics.so` to `/usr/local/lib`
- **macOS**: place `libIO.Astrodynamics.dylib` alongside your app (arm64 and x64 builds are provided) and include the headers
- **Windows**: place `IO.Astrodynamics.dll` / `.lib` alongside your app and include the headers

The native library and the .NET assembly must come from the same release — P/Invoke signatures are version-coupled. Prefer the .NET SDK for all new work.

## Contributing

Contributions to the .NET SDK, CLI, docs and tests are welcome; the native layer accepts only fixes and performance or stability improvements. Please read [CONTRIBUTING.md](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/CONTRIBUTING.md) and the [code of conduct](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/CODE_OF_CONDUCT.md). Security reports: see [SECURITY.md](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/SECURITY.md).

## Sponsoring

Building and sustaining a .NET-first astrodynamics toolkit on top of SPICE takes significant effort: keeping pace with SPICE releases and kernel updates, evolving the .NET API, CLI and documentation, cross-platform packaging and CI, extensive tests and validation against reference data, new features and scenarios, and community support.

You can help by sponsoring the project through [GitHub Sponsors](https://github.com/sponsors/IO-Aerospace-software-engineering), through a company sponsorship to prioritize features, integrations or support, by backing specific issues, or by contributing kernel sets, scenarios and docs. Funds go to .NET SDK and CLI development, documentation and examples, validation datasets and automated QA, and release engineering across platforms.

## Disclaimer

**THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND.**

IO.Astrodynamics is distributed under the GNU Lesser General Public License v3.0 or later (LGPL-3.0-or-later). By using this software, you acknowledge and agree that:

- The software is provided without any warranty, express or implied, including but not limited to the implied warranties of merchantability and fitness for a particular purpose
- The authors and contributors are not liable for any damages, losses, or consequences arising from the use or inability to use this software
- **Users are solely responsible for validating all results** before use in any application
- This software is **NOT certified for flight-critical, safety-critical, or life-critical applications**
- Independent verification and validation (IV&V) is required before use in operational mission planning or spacecraft operations
- Results should be cross-checked against authoritative sources (e.g. JPL Horizons, STK, GMAT) for critical applications

The algorithms implemented are based on publicly available literature and the NAIF SPICE toolkit. While we strive for accuracy, computational results may contain errors. **You assume all risk** associated with using this software.

See the [LICENSE](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/LICENSE) file, together with [GPL-3.0.txt](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/GPL-3.0.txt), for complete terms.

## License and acknowledgments

- License: LGPL-3.0-or-later — see [LICENSE](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/LICENSE). The LGPL v3 incorporates the terms of the GNU GPL v3, supplied verbatim in [GPL-3.0.txt](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/GPL-3.0.txt)
- Built on NASA/JPL NAIF SPICE, developed by the Navigation and Ancillary Information Facility (NAIF) at JPL
