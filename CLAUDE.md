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
- `ReadEphemerisAtGivenEpochProxy` writes a NaN state when `spkezr_c` fails and keeps the SPICE message for `GetLastErrorProxy()` (#454, lot 6, decided by S. Guillet, 2026-11-09); `SpiceAPI.ReadEphemeris` (both) throws on a non-finite state, `SpiceAPI.TransformFrame` on a non-unit quaternion (the zero quaternion of a failed `TransformFrameProxy`); the orientation cache of a propagation keeps a point SPICE cannot give as unavailable (a point mass runs past the PCK), reads the frame once at a window end whose next grid point SPICE cannot give, and reads the frame at the epoch, past the cache, where its grid cannot interpolate (a window start inside the coverage included), so only an epoch SPICE does not cover throws, which a propagation whose output step does not divide its window reaches when it runs past the window end (to round(L/ΔT)·ΔT, up to ΔT/2 past it) beyond the coverage; a planet or moon the kernels do not chain to its barycenter has `InitialOrbitalParameters` null and its `SphereOfInfluence` +Infinity, as a body built from custom parameters; the other proxies still drop their SPICE errors
- Buffer safety: all DTO `SetFrame`/`SetName` methods use `strncpy`; all `FindWindows*` loops are bounds-checked to 1000; `ReadOrientation`/`ReadEphemeris` loops are bounds-checked to 10000

### Key Namespaces

- `IO.Astrodynamics.Body`: Celestial bodies, spacecraft, instruments; `Instrument.FindWindowsInFieldOfViewConstraint` gives the windows of CSPICE's `gftfov` on both paths (#515): CSPICE when the spacecraft and the target both come from kernels, otherwise a managed search (`FieldOfViewEvaluator`, the continuous margin of `FieldOfViewGeometry`, the root finder of the occultations to 1 µs, TDB windows), the target as a point or as its ellipsoid; see `docs/reference/spacecraft.md`
- `IO.Astrodynamics.GeometryFinder`: the managed window search of the distance, coordinate and illumination constraints of `CelestialItem` and `Site` when CSPICE cannot search (#590, #461, #486, #487): samples every step from the start and at the end, nothing read outside the search window, each transition to 1 µs (Brent on f − v, a bisection for the `OrEqual` operators and booleans), the root returned; local extremes as the sign changes of the rate (Richardson of five-point derivatives), absolute ones with the bounds and `adjustValue`; `Equal` as zero-length crossings, a branch-cut jump none; see `docs/reference/sites.md#window-searches`
- `IO.Astrodynamics.OrbitalParameters`: StateVector, KeplerianElements, TLE, EquinoctialElements
- `IO.Astrodynamics.CCSDS.OMM` / `.OPM` / `.CDM`: CCSDS standards (read/write/validate/convert); the three readers share `CCSDS/Common/CcsdsEpochParser`, the `epochType` date in calendar or day-of-year form (#490); a CDM `lengthTypeUO` length without `units` is in m (#613)
- `IO.Astrodynamics.Propagator`: PropagatorBase, CentralBodyPropagator, PropagationSegment, PropagationSolution
- `IO.Astrodynamics.Propagator.Integrators`: IIntegrator, VVIntegrator (Velocity-Verlet, fixed-step), RK78Integrator (adaptive Prince-Dormand 7(8))
- `IO.Astrodynamics.Propagator.Forces`: AtmosphericDrag, SolarRadiationPressure, AlbedoRadiationPressure, ThermalRadiationPressure
- `IO.Astrodynamics.Propagator.Events`: IEventDetector, ManeuverEventDetector, CrossingDirection
- `IO.Astrodynamics.Propagator.Variational`: state transition matrix and covariance propagation (RK7(8) only, off by default): CovariancePropagationOptions (CovariancePropagationMode, RtnProcessNoise, ConsiderParameters, IncludeInStepControl), activated by `CentralBodyPropagatorBuilder.WithCovariancePropagation`, constructor overloads, `PropagationTask.CovariancePropagation`, `Spacecraft.Propagate` and `Scenario.SimulateAsync` overloads; results on `PropagationSolution`; an initial covariance that rounding made indefinite is rejected by the propagation, remediated beforehand by `StateVector.WithRemediatedCovariance()` (the eigenvalue clipping of the conjunction analysis, `CovarianceRemediation`); see `docs/reference/covariance-propagation.md`
- `IO.Astrodynamics.SSA`: ConjunctionAssessment (Screen, Analyze, AnalyzeAll, EvaluateAvoidance); its observer is the centre of the protected spacecraft's initial orbital parameters, the Earth for a kernel-backed spacecraft unless its constructor's `centerOfMotion` says otherwise (#620); on two propagations a close approach is an interior local minimum, `AnalyzeAll` lists none at a window end (#621's dense part), not even a zero of the range rate exactly there, which `Analyze` returns as that end, whether a minimum lies inside the window decided on the range rate of the states, the minima of the Hermite and the window ends only seeding the search (S1 of lot 6's review), a window flat within the tolerance of the seeds bisected on its range rate, negative at the start and positive at the end (F1 of the second review), a seed the secants cannot bracket searched on its own range rate toward the side it points to (reads 1 s apart at most, round 6), then between the candidates, then followed over them to the nearest change of the range rate from negative to positive, or dropped, never listed unrefined, one minimum per pair of consecutive candidates (rounds 4 and 5 of the review), and the midpoint never returned on that path; the range rate then read at every candidate and between them every `MaximumEventSearchStep` at most, each change from negative to positive between two reads that holds no zero bisected, one per pair of reads (#628: the minima no seed reached, at mixed or long steps; the search from a seed still reading 1 s apart up to its neighbouring candidate; an interval of one sign narrower than the reads that no such search reaches not guaranteed); every refined TCA is the tick nearer the root of the range rate (#624: the bisection carries the range rates of its bracket, its middles in ticks, and on one tick returns the end where |g| is smaller, the lower one on a tie), a zero in the first or last tick that rounds onto a window end the inner tick on the dense path, the sampled path returning an end for a zero in its last half-tick; the `ILocalizable` overloads throw on a window past a propagated spacecraft's states (#527); the 2D Pc is CARA's Pc2D (internal `EncounterPlaneProbability`, #610 to #612): the clipped eigenvalues and the rotation passed as they are (λ2 = det/λ1, Kahan's determinant), the integral in θ along the disc split at the Gaussian's ±kσ, adaptive Simpson at 1e-12 of the whole, fdlibm's erf and erfc private to it (the public `SpecialFunctions.ErrorFunction` stays A&S 7.1.26), exact Pc of `tools/collision_probability` (mpmath) within 2.0e-14, for σ ≫ R off-axis within the erfc difference's cancellation, noise the quadrature accepts, bounded by about ε σ/R relative (5.0e-11 at σ = 1e6 R, 2.4e-8 at 1e8 R, 6.6e-7 at 1e10 R); the covariance of a participant propagated in RK7(8) with an initial covariance is propagated to TCA by a replay of the variational equations along its steps (`ConjunctionAnalysisOptions.CovariancePropagation`, on by default, null for the covariance held in RTN), its source on `EncounterState.ProtectedCovariance`/`SecondaryCovariance` (`CovarianceSource`), with the non-linearity index; avoidance options propagated from the covariance at their burn; without a propagated covariance, the options follow the force model of the participant's propagation (RK7(8) or Velocity-Verlet, kept by its spacecraft), the two-body orbit without one, stated by `AvoidanceOption.Trajectory`; see `docs/reference/conjunction-assessment.md`
- `IO.Astrodynamics.Maneuver`: Lambert, impulsive maneuvers, attitude maneuvers, IAttitudeTarget
- `IO.Astrodynamics.Atmosphere`: IAtmosphericModel, EarthStandardAtmosphere, Nrlmsise00Model
- `IO.Astrodynamics.Physics`: GeopotentialModelReader, GeopotentialCoefficient (EGM2008, up to degree 70); `GeopotentialGravitationalField` and `GeopotentialModelParameters` are in `IO.Astrodynamics.Body`
- `IO.Astrodynamics.Frames`: Reference frame transformations (ICRF, TEME, ITRF93, etc.); `Frame.TEME` is the managed `TemeFrame` (#451): TEME → ICRF = Q(t)ᵀ · Rz(ERA − GMST82), IAU 2006/2000B, UT1 = UTC, quasi-inertial (v' = R v), no kernel read; a frame named TEME is routed to it by both data providers and `SpiceAPI.TransformFrame` (the native TEME of the C++ layer is unused); an ephemeris read in TEME, CIRS or TIRS is read in J2000 and rotated (#558); see `docs/reference/frames.md#teme`
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
- `TLE.ToStateVector()` works because it uses SGP4/SDP4 internally; its states are in `Frame.TEME`, a failed SGP4 throws (#454: the native `TLE::ToStateVector` returns a zero state when `evsgp4_c` signals, the one C++ change of lot 5, decided by S. Guillet, 2026-11-08; before, its unwritten state was the stack's), and the TLE's Earth is built in an inertial frame (#597)
- SGP4 is CSPICE `evsgp4_c` (Vallado's 2006 code, AFSPC mode, WGS-72 old, KE 7.43669161e-2; no other mode or constants), at tsince = the TT seconds from the TLE epoch (UTC, read exactly to the tick, #603) to the instant: the `getelm_c` epoch, read once per TLE, plus those seconds (#599); reference `tools/sgp4_reference` (Vallado's code, `sgp4` 2.27, WGS72OLD, mode `a`)
- `TLE.Create` writes each field as the nearest value its columns hold, the epoch as the UTC of the instant (#479) with the carry (#600), B* and n̈/6 with a one-digit exponent (#601), e from 0 (#602), zeros unsigned (#480); `FirstDerivationMeanMotion`/`SecondDerivativeMeanMotion` hold ṅ/2 and n̈/6 (#605); `ToTLE` fits elements already in TEME (#464)
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

### Time Argument

- A numerical propagation whose resolved central body is the Earth integrates over the TT seconds between the instants of its window (#501): the forces at t are those of TT₀ + t (bodies and orientation at its TDB, drag at its UTC). Any other central body, SSB mode included, integrates over TDB seconds, bit for bit as before
- Labels (segment bases, stages, events, outputs) stay TDB; `Duration`, `CumulativeTime`, the event time and the `IIntegrator` duration count the seconds of the argument; `AtEpoch`, `EpochAtMeanAnomaly`, Lambert and IOD count TT seconds about the Earth, TDB otherwise, whatever the tags (#508)
- The maneuver chain counts the seconds of the argument (#547): about the Earth the thrust and maneuver windows of an impulse count TT seconds from the time argument of its event (τ, the sum of the event times), the hold of an attitude TT seconds from its activation; otherwise the TDB arithmetic of the maneuvers, bit for bit

### Minimum Epoch of an Impulse

- An impulse fires at the first crossing of its g-function in its direction at or after its `MinimumEpoch`, searched from the integrated state there (#569): `VVIntegrator` and `RK78Integrator` evaluate g at the gate in the step that holds it (internal `IGatedEventDetector`, `EventGates`; RK7(8) by a shortened step and a bisection over [gate, step end], Velocity-Verlet by the cubic Hermite of the step); a g exactly zero at the gate counts there when the sign after it is the target sign (inside a step, on a step end, at the base); `PlaneAlignmentManeuver` takes the node it moves toward at the gate (`ComputeEventValueAtSearchStart`). The public `ManeuverEventDetector.Evaluate` keeps its +1 before the gate, which the library's integrators never compare with a g-value after the gate (they replace it with the g-value at the gate); every step without a gate is unchanged. See `docs/reference/impulse-maneuvers.md#minimum-epoch`
- The gate of a chained maneuver, the end of the previous maneuver's window, belongs to one propagation (#573): `Spacecraft.ChainStandbyManeuver` raises `Maneuver.PropagationGate`, which `Maneuver.Reset` clears at the start of each `Spacecraft.Propagate` (`ResetPropagation`, after the propagator is built); the public `SetStandbyManeuver(maneuver, minimumEpoch)` raises the given value (`GivenMinimumEpoch`), which lasts; `MinimumEpoch` is the later of the two, with no setter. The library's raises of a chain go through `ChainStandbyManeuver`, never the two-argument `SetStandbyManeuver`
- The attitude timeline raises gates too (#539): each executed attitude, leading or chained, raises the gate of the next maneuver to the end of its hold through `ChainStandbyManeuver` (the last leading hold gates the first impulse); about the Earth the TT end of an impulse's window replaces, through the internal overwrite `Maneuver.SetPropagationGate`, the TDB end `ImpulseManeuver.TryExecute` raised the next gate to (#547). `ManeuverEventDetector.HandleEvent` executes the impulse only and returns no next detector; the propagation advances the chain

### Thread Safety

- CSPICE P/Invoke calls: use shared lock — **not thread-safe**
- `GeopotentialGravitationalField`: **NOT thread-safe** — create one instance per thread/propagator
- `Nrlmsise00Model`: thread-safe, concurrent use OK
- Force partials (internal, `ForceBase.AccumulateStatePartials`): a force keeps a work `StateVector` for its finite differences, so the partials path of one force instance is **not reentrant**, unlike `Apply`. Forces belong to one integrator, as the propagator builds them
- Managed occultation, field-of-view and window (`GeometryFinder`) searches: no state, kernels read under the shared lock; searches on separate propagations may run concurrently
- Attitude record of a managed spacecraft (`SpacecraftFrame`): filled by its propagation only (and `AddStateOrientationToICRF`), read under a lock of the record; a read stores nothing (#523), so reads from any thread, before, during or after the propagation, leave the attitude and its CK unchanged. A kernel-backed spacecraft frame reads SPICE at each call, under the shared lock, and caches nothing
- Frame orientation cache: active only on the thread that propagates or evaluates a propagation (`Frame.UseOrientationCache`), never set on the shared `Frame`; propagations and RK7(8) evaluations sharing a central body without a geopotential may run concurrently
- RK7(8) evaluations after a propagation (`PropagationSolution.InterpolateAt`, `GetStateTransitionMatrix`, `GetParameterSensitivity`, `GetCovariance`, `GetExtendedCovariance`, `GetStateVector`, and the internal `EvaluateVariational`, `EvaluateCovariance` and `EvaluateExtendedCovariance`): serialized by a lock per solution; they evaluate the forces, so they must not run concurrently with another propagation or evaluation that shares a `CelestialBody` with a geopotential
- Conjunction analysis with a propagated covariance (`ConjunctionAssessment.Analyze`, `AnalyzeAll`) and `EvaluateAvoidance`: the replay of the variational equations, the propagations of the non-linearity index and of the avoidance options (with or without a propagated covariance, RK7(8) or Velocity-Verlet) use the forces of the participant's propagation under the lock of its solution or of the force model its spacecraft keeps, with the same rule

### Geopotential

- EGM2008 model file: `Data/SolarSystem/EGM2008_to70_TideFree` (degrees 2–70)
- Pass `GeopotentialModelParameters(filePath, degree)` to `CelestialBody` constructor
- Legendre functions: geodesy normalization, no Condon-Shortley phase
- Degree 0 and 1 are absent from the file; handled as zeros internally. A field of degree 0 or 1 is the point mass of its central term computed in the frame of the state, reading no orientation of the body (lot 4 of #457: it held only within the coverage of the body's orientation data)
- Constants (#410): the harmonics take the GM and reference radius of the model, EGM2008's (3.986004415e14 m³/s², 6378136.3 m) for the Earth by default, the kernel GM and equatorial radius for any other body, or those given to `GeopotentialModelParameters(path, degree, gravitationalParameter, referenceRadius)`; the central term takes the kernel GM by default (D2-A of #444). The `.cof` given to GMAT (`tools/gmat_reference`) carries the kernel GM, the model radius and coefficients times GM_model / GM_kernel
- Central term with the model GM (opt-in, decision 18 of #457, decided by S. Guillet, 2026-10-27): `new GeopotentialModelParameters(...) { CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter }` gives the whole field the constants of the model, as GMAT applies a `.cof` header, in the acceleration and in the point-mass partials (Φ, covariance) alike; a degree-0 field is then the point mass of the model GM. `CelestialItem.GM`, third bodies and element conversions keep the kernel GM. For the Earth with EGM2008 it moves the acceleration by 1.5e-8, a LEO by about 20 m a day; the default path is bit for bit unchanged. Reproduces GMAT R2026a's EGM2008 runs of conformance propagators 001-003 (`Data/GmatConformancePropagators`)

### CCSDS OMM/OPM

- OMM COSPAR ID format: `"1998-067A"` (with hyphen); TLE format: `"98067A"` — conversion is automatic
- Check `omm.IsTleCompatible` before calling `ToTle()`
- OPM uses CCSDS units (km, km/s, deg); framework uses SI (m, m/s, rad) — conversions are automatic
- Maneuver conversion is one-way: `Spacecraft → OPM` (OPM maneuvers are archival)
- `EPOCH` is read in `TIME_SYSTEM` (UTC, TAI, TDB, TT, GPS; another throws) by `Opm.ToStateVector` and `Omm.ToTle` (#485); the writers write UTC under `TIME_SYSTEM = UTC`, `MAN_EPOCH_IGNITION` included (#479)

### Attitude Maneuvers

- **TriadAttitude**: Fully-constrained 3-DOF via TRIAD algorithm — use when roll must be defined
- **Single-vector attitudes** (`NadirAttitude`, `ProgradeAttitude`, etc.): leave roll unconstrained
- Primary + secondary body vectors must be ≥5 degrees apart (prevents numerical instability)
- `IAttitudeTarget` mixes orbital directions (`OrbitalDirectionTarget.Prograde`, etc.) and celestial bodies (`CelestialAttitudeTarget`); `CelestialAttitudeTarget` defaults to `Aberration.None` (geometric), configurable to `Aberration.LT` for photon-based scenarios
- Factory methods: `TriadAttitude.CreateLVLH()`, `TriadAttitude.CreateProgradeWithSunTracking()`
- Configurable body axes per spacecraft instance (`BodyFront`, `BodyRight`, `BodyUp`); must be orthogonal and right-handed
- In a propagation an attitude law is in force from its activation to the next activation of the chain; its hold delays the next maneuver; an impulse's attitude is in force over its thrust and maneuver windows (#539, `docs/reference/attitudes.md#the-attitude-timeline`)
- `Instrument.Orientation` is the Euler angles (x, y, z) about the axes 1, 2, 3 of the frame kernel: `pxform(instrument, spacecraft) = eul2m(x, y, z, 1, 2, 3)` (Frames Required Reading, "Defining a TK Frame Using Euler Angles"), `Instrument.RotationToSpacecraft`, used by the managed field of view, the spacecraft-frame getters, `TriadAttitude` and `InstrumentPointingToAttitude` (#524)

### Spacecraft Attitude and Kernels

- A propagated spacecraft stores its states and its attitude in ICRF whatever the inertial frame of the initial state; the integration and `PropagationSolution` stay in that frame (#529, #535). Attitudes are computed on the ICRF state
- The attitude record is filled from the attitude timeline of the propagation (#539, internal `AttitudeTimeline`, `PropagatorBase.RecordAttitudeTimeline`): an attitude activates at max(`MinimumEpoch`, end of the previous window) and its law is in force to the next activation, sampled on the solution at its activation, at each output of its interval and 1 µs before the change that ends it; the impulse's attitude (front along ΔV) over [tc − 0.666 dt, end of its maneuver window], clipped to the start and to the previous activation; the law before an impulse resumed after it unless an attitude chained after it activates there; no activation past the end; no `Quaternion.Zero` sentinel (the identity is recorded). Every executed maneuver, attitudes included, is in `ExecutedManeuvers` (#570). See `docs/reference/attitudes.md#the-attitude-timeline`
- The attitude record (`SpacecraftFrame`) holds spacecraft → ICRF, labelled with the spacecraft frame (its source); the exact SLERP between samples of the same law, the earlier attitude held up to a discrete change, the law sampled 1 µs (`DiscreteChangeLead`) before a change that follows it, entries within 2 µs one epoch (#523, #529, #534)
- The stored states are split into arcs at each impulse (#540, `PropagatedEphemeris`): each arc the state after the impulse that opens it, the outputs strictly inside it and the state before the impulse that closes it, both at its epoch (an output closer to an impulse than a tenth of the output step dropped, the first and last outputs always kept; an arc bounded by an impulse with fewer than 4 states, 15 µs long or more, filled with 16 states of the solution; no arc of zero length; an impulse that no segment carries closes the last arc; in covariance mode every stored state carries its covariance). A read takes the arc of its epoch (the later one at an impulse, the state after it) and its 8 nearest states by binary search, bit for bit the former sorted read far from impulses (#522: no sort, no lock). `Spacecraft.Propagate` and `Scenario.SimulateAsync` replace the stored states (`TLEPropagator` still adds its own); `AddPropagatedStates` adds to the arc of each epoch
- `WriteOrientation` writes the C-matrix (J2000 → spacecraft, the conjugate of the record) in J2000 with the CK's rate, the earlier attitude repeated 1 µs before each discrete change; `WriteEphemeris` writes J2000, one type-9 segment per arc, degree min(7, N − 1), through the managed DAF writer `Kernels/SpkWriter` (up to 24 segments byte for byte what `spkw09_c` writes, from 25 on the same summaries and data, a full summary record followed by an empty one; built and checked before an existing file is replaced; the native writer stays for `Star` and `Site`). `SpiceAPI.ReadOrientation`/`WriteOrientation` stay the raw C-matrix pair of `ckgpav_c`/`ckw03_c`
- Tests compare rotation angles, never quaternion components (the sign of a quaternion is free), against `pxform`/`sxform`/`spkezr` on the kernels the library writes and the closed forms of the laws; see `docs/reference/spacecraft.md#attitude-and-kernels`

## Validation and Thresholds

Rules learned during the state transition matrix and covariance work (10.2.0-preview-1, tracking issue #364 and its "Lessons learned"):

- Expected values come from SPICE or an independent reference (GMAT, a closed form, an extended-precision computation), never from a snapshot of the code. A golden is recaptured only for an intended change of results, which the PR states.
- A reference about the Earth counts TT seconds between its epochs (`PropagationTimeArgument.Tt.SecondsBetween`, or an exact elapsed time through `InterpolateAtElapsed`), never a TDB epoch difference; the SPICE occultation references of `tools/occultation_reference`, which count ET within their bounds, are the stated exception.
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
