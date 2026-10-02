# IO.Astrodynamics

IO.Astrodynamics is a .NET astrodynamics framework for orbital mechanics, ephemeris computation, mission analysis, and high-accuracy numerical propagation.

It is free and open source under LGPL-3.0-or-later, published as a single package,
[`IO.Astrodynamics`](https://www.nuget.org/packages/IO.Astrodynamics), with the sources on
[GitHub](https://github.com/IO-Aerospace-software-engineering/Astrodynamics).

!!! info "One edition since 10.0.0"
    Everything that used to ship in the separate `IO.Astrodynamics.Pro` package is now part of
    `IO.Astrodynamics`. Replace the package reference and your code compiles unchanged; see
    [Versioning](contributing/versioning.md) for the migration notes.

## Capabilities

- Orbital mechanics with Keplerian, equinoctial, Cartesian, and TLE-based workflows
- SPICE kernel loading, ephemeris access, and frame transforms
- Lambert transfers, launch windows, and geometry searches
- Time systems including UTC, TDB, TAI, TDT, GPS, and local time handling
- Spacecraft, instruments, maneuvers, and event-driven propagation
- Attitude control: single-vector attitudes, TRIAD fully-constrained pointing, instrument-based pointing
- Geometry searches such as occultations, eclipses, visibility windows, and distance constraints
- Atmospheric drag (NRLMSISE-00, U.S. Standard), geopotential (EGM2008), solar radiation pressure, and third-body perturbations
- Velocity-Verlet fixed-step symplectic integrator, and the adaptive RK7(8) Prince-Dormand integrator with sub-step event refinement
- Fluent propagator builder for declarative force-model assembly
- CIO-based Earth orientation frames GCRF, CIRS, and TIRS
- Albedo and thermal radiation pressure modeling
- Batch propagation with fault isolation and progress reporting
- Monte Carlo dispersion analysis with covariance-based initial-state sampling and per-epoch statistics
- Conjunction screening, conjunction assessment, and simple avoidance trade studies
- CCSDS OMM, OPM and CDM reading, writing, and validation

## Non-Goals

- Real-time telemetry processing
- Hardware control or flight software
- Detailed thermal, structural, or power subsystem design
- Closed-loop GNC implementation

## Where To Go Next

This documentation is organized into five tabs:

- **Home** — you are here
    - [Quick Start](get-started.md) — install, load kernels, first calculations
    - [Standards & Units](standards-and-units.md) — SI units, frame conventions, reference standards
- **[Tutorials](tutorials/index.md)** — step-by-step use cases from ephemeris queries to Monte Carlo
- **[API Reference](reference/index.md)** — systematic type and method documentation
- **[Guides](guides/index.md)** — cross-cutting best practices (kernels, thread safety, validation)
- **[Contributing](contributing/index.md)** — how to contribute, versioning policy, and the paid services around the framework
