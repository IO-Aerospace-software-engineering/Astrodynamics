---
title: Versioning
---

# Versioning

Current version information and release notes since 8.0. Breaking changes are called out in each release.

## Current Versions

| Component | Version |
|-----------|---------|
| NuGet (`IO.Astrodynamics`) | `10.1.0` |
| CLI tool (`IO.Astrodynamics.CLI`) | `10.1.0` |
| .NET framework | `.NET 10.0` |
| SPICE toolkit | `CSPICE N0067` |
| License | `LGPL-3.0-or-later` |

## Release Notes

### 10.2.0 (in development)

Phase 2, feature 1: propagation of the state transition matrix and of the covariance. Entries are added lot by lot.

**Added**

- **Integrator choice in `Scenario`.** New overload `Scenario.SimulateAsync(includeAtmosphericDrag,
  includeSolarRadiationPressure, propagatorStepSize, integratorFactory)`: the factory creates the integrator of each
  spacecraft, for example `() => new RK78Integrator()`. The existing overload and the default integrator,
  Velocity-Verlet, are unchanged.

### 10.1.0

Corrections from the post-merge review: no new feature, but several results change.

**Changed**

- **`Frame.GCRF` has the axes of `Frame.ICRF`.** SPICE `J2000`, the pivot of every transform, is treated
  as ICRF-aligned, like the DE ephemerides it carries. `GcrfFrame` used to rotate by the IAU 2006 frame
  bias on top of CIRS and TIRS, which already contain it. GCRF to CIRS or TIRS was therefore off by
  about 23 mas. CIRS and TIRS relative to `Frame.ICRF` are unchanged.
- **IAU 2000B nutation.** The P03 adjustment now scales with time (`-2.7774e-6 t`, as in SOFA
  `iauNut06a`), and the series uses the linear Delaunay arguments that define IAU 2000B. Nutation
  changes by up to 1.9e-10 rad and now matches SOFA `iauNut00b` + P03 to 1e-13 rad.
- **CIO locator.** The full IAU 2006 series of `s + XY/2` is evaluated (66 periodic terms). The
  polynomial-only version was off by up to 1.27e-8 rad (2.6 mas).
- **`EncounterQualityFlags.StaleCovarianceUsed`.** It is now raised only when the covariance taken from
  a participant's initial state is older than `ConjunctionAnalysisOptions.StaleCovarianceThreshold`
  (60 s by default). It used to be raised whenever the initial-state covariance was used, even 10 s
  from TCA.
- **Initial-state covariance at TCA.** When no covariance is available at TCA, the initial-state
  covariance is held fixed in the RTN frame instead of being reused unchanged in the inertial frame.
  It is still not propagated.
- **CDM export.** An encounter flagged `StaleCovarianceUsed` is refused unless
  `CdmExportOptions.AllowStaleCovariance` is set. The comments give the age of each exported
  covariance.

**Added**

- `EncounterState.ProtectedCovarianceAge` and `SecondaryCovarianceAge`,
  `ConjunctionAnalysisOptions.StaleCovarianceThreshold`, `CdmExportOptions.AllowStaleCovariance` and
  `StateVector.RotateCovarianceFromRtn`.
- The conformance runner report records the tested framework commit (`framework_commit`,
  `framework_informational_version`).

**Deprecated**

- `LowThrustManeuver` is marked `[Obsolete]`. It was never implemented: every member threw
  `NotImplementedException`. It will be removed in 11.0.

**Removed**

- The unused `MathNet.Filtering.Kalman` package dependency. A project that used it through
  `IO.Astrodynamics` must now reference it directly.

**Fixed**

- **Leap seconds.** UTC to TAI used the previous offset at exactly 00:00:00 UTC on a leap second date
  (for example `2017-01-01T00:00:00Z`), one second short. TAI to UTC was one second off for the first
  seconds after midnight TAI on those dates, and `LocalTimeFrame` looked the offset up from the local
  clock.
- **Sub-millisecond time.** `Time.ToJulianDate()` and `Centuries()` truncated to the millisecond. The
  Earth rotation angle of TIRS could be off by up to 7.3e-8 rad.
- **CLI version.** The CLI reported version `0.0.1` (`astro --version`) whatever the release. Its
  assembly and file versions now derive from the package version.

### 10.0.0

`IO.Astrodynamics.Pro` is discontinued. Everything it contained is now part of
`IO.Astrodynamics`, under LGPL-3.0-or-later. There is one package, one licence, no licence key.

**Migrating from `IO.Astrodynamics.Pro` 1.x**

1. Replace the package reference with `IO.Astrodynamics` version `10.0.0` or later. Keep it, or drop
   it, if you already referenced `IO.Astrodynamics` as well: it is now the only package you need.
2. Delete the licence file at `~/.ioaerospace/astrodynamics/license.key`. Nothing reads it any more,
   and no environment variable replaces it.
3. Remove the private NuGet source, and any `IOAstroInternalBuild` property or
   `IOASTRO_INTERNAL_BUILD` variable you set to bypass the build-time check. The MSBuild targets
   that enforced the licence are gone.
4. Rebuild. Namespaces are unchanged, so your source compiles as it is. The assembly name differs,
   so a recompilation is required: a pre-built binary referencing `IO.Astrodynamics.Pro` will not
   load against 10.0.0.

**One API change**

The static `Frames` accessor class is removed. It existed only to gather the frames from two
assemblies in one place. `GCRF`, `CIRS` and `TIRS` are now static members of `Frame`, like
`Frame.ICRF`:

```csharp
// before
var cirs = Frames.CIRS;
// after
var cirs = Frame.CIRS;
```

`CirsFrame(IEarthOrientationParameters)` is removed as well; it ignored its argument. CIRS depends
on precession-nutation only. Use `new TirsFrame(eop)` where Earth orientation parameters matter.

### 9.1.1

**Added**

- NORAD catalog numbers above 99999 in TLE form, through the Alpha-5 encoding (leading letter,
  `A` = 10 to `Z` = 33, without `I` and `O`), up to 339999: `NoradCatalogNumber.Format` and
  `NoradCatalogNumber.Parse`, and `TLE.NoradCatalogId` for the decoded value.
- `Omm.ToTle()` throws for a catalog number that a TLE cannot represent, instead of truncating it.

**Changed**

- `TLE.Create` and the TLE `Configuration` record take `int` instead of `ushort` for the NORAD
  identifier, the revolution number at epoch and the element set number. Source code compiles
  unchanged; binaries built against 9.1.0 must be recompiled.

### 9.1.0

**Added**

- macOS support: native libraries for `osx-arm64` (Apple Silicon) and `osx-x64` (Intel) are shipped
  in the package, next to `linux-x64` and `win-x64`.

### 9.0.0

!!! note "Reading the entries below"
    Entries marked **(Pro)** shipped in the separate `IO.Astrodynamics.Pro` package at the time.
    That package is discontinued and its content is part of `IO.Astrodynamics` since 10.0.0.

**Breaking changes**

- **.NET 8.0 removed**. All projects now target `net10.0` only.
- **SemVer 3-part versioning**: Migrated from 4-part version numbers to standard SemVer.
- **Build-time license enforcement** (Pro): consumer builds required a signed licence token. Removed in 10.0.0.

**Added**

- **Batch propagation** (Pro): Added `BatchPropagator` with fault isolation and progress reporting.
- **Albedo radiation pressure** (Pro): Lambertian sphere model for reflected sunlight. Requires `CelestialBody.Albedo > 0`.
- **Thermal radiation pressure** (Pro): Isotropic emitter model for infrared body radiation. Requires `CelestialBody.ThermalEffectiveTemperature > 0` and `ThermalEmissivity > 0`.
- **CIO-based Earth orientation frames** (Pro): Added GCRF, CIRS, and TIRS frames with `IEarthOrientationParameters` interface.
- **Kernel-backed spacecraft**: Spacecraft can now be backed by SPICE SPK kernels for ephemeris retrieval.
- **Conjunction assessment** (Pro): Screening, encounter analysis, and avoidance trade studies.
- **CCSDS CDM** (Pro): Conjunction Data Message generation, reading, writing, and XML schema validation.
- **Matrix operations**: Added matrix addition and RTN (Radial-Transverse-Normal) transformations.

### 8.7.0

- Added `MonteCarloPropagator` for covariance-based dispersion analysis.
- Added `MonteCarloConfiguration`, `MonteCarloResult`, `EpochStatistics`, `StateComponents`, and `RssPercentiles` types.
- Statistics use typed records (`StateComponents`, `RssPercentiles`) instead of raw `double[]` arrays.

### 8.6.0

- `SpacecraftPropagator` was renamed to `CentralBodyPropagator`.
- Maneuver triggering moved to event-detection g-functions.
- The integrator interface was redesigned around segmented propagation.
- `Propagate()` now returns `PropagationSolution`.
- Several SSB-specific APIs were removed from the public surface.

### 8.5.0

- Native error propagation now returns managed failures safely across the P/Invoke boundary.
- Ephemeris write behavior was corrected to use the per-element center of motion.
- Native buffer and memory handling were tightened.
- Native binaries must be rebuilt to match the new signatures.
