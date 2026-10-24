# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IO.Astrodynamics is a .NET 10 astrodynamics toolkit with a thin C++ interop layer for NASA/JPL NAIF CSPICE.

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

# The LongValidation category (validations and studies of minutes each) is skipped unless IO_ASTRODYNAMICS_LONG_VALIDATION=1;
# the manual workflow long-validation.yml sets it, before each release and on a PR that changes the dynamics it measures
dotnet test --filter "Category!=LongValidation"

# Skip the Validation category too (bit-for-bit goldens, heavy validations), which CI runs on Linux only
dotnet test --filter "Category!=Validation&Category!=LongValidation"

# Run the LongValidation category (Linux, about 35 min on 4 cores)
IO_ASTRODYNAMICS_LONG_VALIDATION=1 dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj -c Release --filter "Category=LongValidation"

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

The native bridge is accessed via `IO.Astrodynamics.SpiceAPI` (singleton, P/Invoke). All SPICE calls must go through the shared lock object — CSPICE is not thread-safe.

### Native Error Propagation

The C++ proxy layer uses a `bool` return + `GetLastErrorProxy()` pattern instead of throwing C++ exceptions across the P/Invoke boundary:
- `ReadOrientationProxy`, `ReadEphemerisProxy`, `LaunchProxy` return `bool` (`true` = success, `false` = error)
- On failure, call `GetLastErrorProxy()` to retrieve the error message (thread-local buffer)
- The .NET `SpiceAPI` class checks the return value and throws `InvalidOperationException` with the native error message
- Buffer safety: all DTO `SetFrame`/`SetName` methods use `strncpy`; all `FindWindows*` loops are bounds-checked to 1000; `ReadOrientation`/`ReadEphemeris` loops are bounds-checked to 10000

### Key Namespaces

- `IO.Astrodynamics.Body`: Celestial bodies, spacecraft, instruments
- `IO.Astrodynamics.OrbitalParameters`: StateVector, KeplerianElements, TLE, EquinoctialElements
- `IO.Astrodynamics.CCSDS.OMM` / `.OPM`: CCSDS standards (read/write/validate/convert)
- `IO.Astrodynamics.Propagator`: PropagatorBase, CentralBodyPropagator, PropagationSegment, PropagationSolution
- `IO.Astrodynamics.Propagator.Integrators`: IIntegrator, VVIntegrator (Velocity-Verlet, fixed-step), RK78Integrator (adaptive Prince-Dormand 7(8))
- `IO.Astrodynamics.Propagator.Forces`: AtmosphericDrag, SolarRadiationPressure, AlbedoRadiationPressure, ThermalRadiationPressure
- `IO.Astrodynamics.Propagator.Events`: IEventDetector, ManeuverEventDetector, CrossingDirection
- `IO.Astrodynamics.Propagator.Variational`: state transition matrix and covariance propagation (RK7(8) only, off by default): CovariancePropagationOptions (CovariancePropagationMode, RtnProcessNoise, ConsiderParameters, IncludeInStepControl), activated by `CentralBodyPropagatorBuilder.WithCovariancePropagation`, constructor overloads, `PropagationTask.CovariancePropagation`, `Spacecraft.Propagate` and `Scenario.SimulateAsync` overloads; results on `PropagationSolution`; an initial covariance that rounding made indefinite is rejected by the propagation, remediated beforehand by `StateVector.WithRemediatedCovariance()` (the eigenvalue clipping of the conjunction analysis, `CovarianceRemediation`); see `docs/reference/covariance-propagation.md`
- `IO.Astrodynamics.SSA`: ConjunctionAssessment (Screen, Analyze, AnalyzeAll, EvaluateAvoidance); the covariance of a participant propagated in RK7(8) with an initial covariance is propagated to TCA by a replay of the variational equations along its steps (`ConjunctionAnalysisOptions.CovariancePropagation`, on by default, null for the covariance held in RTN), its source on `EncounterState.ProtectedCovariance`/`SecondaryCovariance` (`CovarianceSource`), with the non-linearity index; avoidance options propagated from the covariance at their burn; without a propagated covariance, the options follow the force model of the participant's propagation (RK7(8) or Velocity-Verlet, kept by its spacecraft), the two-body orbit without one, stated by `AvoidanceOption.Trajectory`; see `docs/reference/conjunction-assessment.md`
- `IO.Astrodynamics.Maneuver`: Lambert, impulsive maneuvers, attitude maneuvers, IAttitudeTarget
- `IO.Astrodynamics.Atmosphere`: IAtmosphericModel, EarthStandardAtmosphere, Nrlmsise00Model
- `IO.Astrodynamics.Physics`: GeopotentialModelReader, GeopotentialCoefficient (EGM2008, up to degree 70); `GeopotentialGravitationalField` and `GeopotentialModelParameters` are in `IO.Astrodynamics.Body`
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
SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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

### NORAD Catalog Numbers

- The TLE catalog number field is 5 characters: plain digits up to 99999, then **Alpha-5** (leading letter, `A`=10 … `Z`=33, excluding `I` and `O`) up to **339999**
- `NoradCatalogNumber.Format` / `.Parse` handle both forms; `TLE.NoradCatalogId` exposes the decoded value
- Identifiers are `int` throughout (`Configuration`, `TLE.Create`) — never `ushort`, the catalog passed 65535 long ago
- Values above 339999 cannot be represented in TLE form at all; `Omm.ToTle()` throws rather than truncating. Use OMM (`NORAD_CAT_ID`, 9 digits) for those objects

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
- Force partials (internal, `ForceBase.AccumulateStatePartials`): a force keeps a work `StateVector` for its finite differences, so the partials path of one force instance is **not reentrant**, unlike `Apply`. Forces belong to one integrator, as the propagator builds them
- Frame orientation cache: active only on the thread that propagates or evaluates a propagation (`Frame.UseOrientationCache`), never set on the shared `Frame`; propagations and RK7(8) evaluations sharing a central body without a geopotential may run concurrently
- RK7(8) evaluations after a propagation (`PropagationSolution.InterpolateAt`, `GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance`, `GetStateVector`, and the internal `EvaluateVariational`, `EvaluateCovariance` and `EvaluateExtendedCovariance`): serialized by a lock per solution; they evaluate the forces, so they must not run concurrently with another propagation or evaluation that shares a `CelestialBody` with a geopotential
- Conjunction analysis with a propagated covariance (`ConjunctionAssessment.Analyze`, `AnalyzeAll`) and `EvaluateAvoidance`: the replay of the variational equations, the propagations of the non-linearity index and of the avoidance options (with or without a propagated covariance, RK7(8) or Velocity-Verlet) use the forces of the participant's propagation under the lock of its solution or of the force model its spacecraft keeps, with the same rule

### Geopotential

- EGM2008 model file: `Data/SolarSystem/EGM2008_to70_TideFree` (degrees 2–70)
- Pass `GeopotentialModelParameters(filePath, degree)` to `CelestialBody` constructor
- Legendre functions: geodesy normalization, no Condon-Shortley phase
- Degree 0 and 1 are absent from the file; handled as zeros internally
- Constants (#410): the harmonics take the GM and reference radius of the model, EGM2008's (3.986004415e14 m³/s², 6378136.3 m) for the Earth by default, the kernel GM and equatorial radius for any other body, or those given to `GeopotentialModelParameters(path, degree, gravitationalParameter, referenceRadius)`; the central term always takes the kernel GM. The `.cof` given to GMAT (`tools/gmat_reference`) carries the kernel GM, the model radius and coefficients times GM_model / GM_kernel

### CCSDS OMM/OPM

- OMM COSPAR ID format: `"1998-067A"` (with hyphen); TLE format: `"98067A"` — conversion is automatic
- Check `omm.IsTleCompatible` before calling `ToTle()`
- OPM uses CCSDS units (km, km/s, deg); framework uses SI (m, m/s, rad) — conversions are automatic
- Maneuver conversion is one-way: `Spacecraft → OPM` (OPM maneuvers are archival)

### Attitude Maneuvers

- **TriadAttitude**: Fully-constrained 3-DOF via TRIAD algorithm — use when roll must be defined
- **Single-vector attitudes** (`NadirAttitude`, `ProgradeAttitude`, etc.): leave roll unconstrained
- Primary + secondary body vectors must be ≥5 degrees apart (prevents numerical instability)
- `IAttitudeTarget` mixes orbital directions (`OrbitalDirectionTarget.Prograde`, etc.) and celestial bodies (`CelestialAttitudeTarget`); `CelestialAttitudeTarget` defaults to `Aberration.None` (geometric), configurable to `Aberration.LT` for photon-based scenarios
- Factory methods: `TriadAttitude.CreateLVLH()`, `TriadAttitude.CreateProgradeWithSunTracking()`
- Configurable body axes per spacecraft instance (`BodyFront`, `BodyRight`, `BodyUp`); must be orthogonal and right-handed

## Validation and Thresholds

Rules learned during the state transition matrix and covariance work (10.2.0, tracking issue #364 and its "Lessons learned"):

- Expected values come from SPICE or an independent reference (GMAT, a closed form, an extended-precision computation), never from a snapshot of the code. A golden is recaptured only for an intended change of results, which the PR states.
- A reference must resolve better than what it measures. Central differences of whole propagations take a tolerance of 1e-14 and start from the exact perturbed state; a sensitivity at the noise floor (Ψ_Cr) is measured against the same propagation with the variational equations in the step control. Otherwise the test measures its reference.
- Thresholds are set from a measurement and never relaxed to make a test pass: a failing test is first a bug to understand. A record at the noise level is twice the worst value over Linux, Windows and macOS arm64, read from the CI logs (integration noise differs between platforms); a figure measured on Linux alone has already failed on macOS arm64.
- Test every combination the public surface allows: both integrators, every activation path, participants propagated or not, maneuvers, SSB mode, a non-ICRF frame. Test a new coordinate rule where its coordinates are singular (poles, polar axis, equator).
- A defect found outside the scope of a change gets its own issue and its own PR; the change is not widened.
- A change of the dynamics is checked by the manual workflow `long-validation.yml`, never in a session container, and by `gmat-reference.yml` when what GMAT reads changes. A published figure that a change moves is measured again in the same PR.

## Repository Process

- **Authorship.** Commits use the git identity of S. Guillet, as in the history. No Claude attribution (co-author lines, "Generated with/by Claude Code") in commits, PRs, issues or comments, and no model identifier. The GitHub connector appends a "Generated by Claude Code" line to a PR, issue or comment it creates: edit it out right after posting.
- **Design first.** A significant change opens a draft PR whose description is the design, with its measurements and one recommendation per open choice, approved by S. Guillet before the implementation. A long feature keeps one tracking issue (as #364) holding the working rules, the dated decisions, the status, the thresholds and the lessons.
- **Citations.** A reference that cannot be read from the environment is cited with "to be verified by S. Guillet"; once he confirms it, the marker becomes "confirmed by S. Guillet, <date>" ("Confirmed by S. Guillet (<date>)" on the provenance page). Every formula of the covariance work has its row on `docs/reference/covariance-propagation-provenance.md`. No marker is left at a release.
- **Releases.** See "Releasing" in `docs/contributing/versioning.md`: the tag is the version, exactly.

## Code Quality

- Unit test coverage must exceed 95%
- OOP, SOLID, and DRY principles required
- Feature branches required before merge (see `CONTRIBUTING.md`)
