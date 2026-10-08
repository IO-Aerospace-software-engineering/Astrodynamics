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
  
**Changed**

- **Covariance towards a rotating frame.** `ToFrame` now transforms a state covariance with the Jacobian of the
  state transformation, `J = [[R, 0], [-[(R ω)×] R, R]]`, instead of the block-diagonal rotation `diag(R, R)`.
  Towards ITRF93, TIRS or any body-fixed frame, the velocity covariance now includes the position uncertainty
  carried by the rotation of the frame (about 7 mm/s for 100 m of position uncertainty in an Earth-fixed frame); it
  used to be wrong by that amount. Between inertial frames the result is unchanged, apart from the symmetrization of
  the output. `Matrix.TransformCovariance` is documented as valid between inertial frames only, and the RTN rotation
  of the conjunction analysis and of the CDM export keeps its pure-rotation convention.

**Fixed**

- **States between the steps of an RK7(8) propagation (#363).** The output states, `PropagationSolution.InterpolateAt`,
  the location of the events and the state the propagation restarts from after an impulsive maneuver came from a cubic
  Hermite interpolation between the accepted steps, whose error grows as the fourth power of the step size. In a
  400 km LEO with outputs every 10 s, the output states were off by up to 101 m at the default tolerance (18.5 m at
  1e-11, 1.8 m at 1e-13), while the step ends were within 1.5 cm. Each of these states is now computed by a shortened
  RK7(8) step from the start of the accepted step that contains its epoch: it has the accuracy of the integrator, and
  the worst output error equals the worst error at the step ends (1.5 cm, 0.1 mm and 0.8 µm at the three tolerances).
  Every RK7(8) propagation is concerned: its output states move (by up to 13 m, 77 m and 63 m in the three
  propagations of the golden of `PropagationGoldenTests`, recaptured), and after a maneuver its whole trajectory, since
  the maneuver now fires on the integrated trajectory (99 µs later in the golden). The accepted steps before the first
  event are bit-identical. The event time is put on the 100 ns grid of the epochs, so the state at the event, the epoch
  of the next segment and the state transition matrix at the event refer to the same instant. Velocity-Verlet, TLE and
  SPICE propagations are unchanged, and so are `PropagationSegment.InterpolateAt` and the public
  `BisectionEventFinder.FindRoot`, which keep the cubic Hermite interpolation. Cost: one RK7(8) step, 13 force
  evaluations, per output epoch inside a step, none at a step boundary. Over one day with EGM2008 10×10, the Moon and
  the Sun at the default tolerance, the propagation takes about as long with outputs every 600 s, about twice as long
  every 60 s, and seven to nine times as long every 10 s. `PropagationSolution.InterpolateAt` of an RK7(8) solution
  evaluates the forces under a lock of the solution: like a propagation, it must not run concurrently with another one
  that shares a `CelestialBody` with a geopotential.
- **TCA of the conjunction analysis of two propagated trajectories (#363).** The polynomial search of the
  `PropagationSolution` overloads located the TCA on the cubic Hermite interpolation of the positions. Each TCA is now
  refined by bisection of the range rate on the states of the trajectories. On RK7(8) trajectories at the default
  tolerance, the TCA moves from 2.6e-6 s to 1.3e-4 s away from the Keplerian reference to less than 1e-7 s; the miss
  distance, already computed on the states, changes at second order only (by up to 2.8 mm in the test cases).
- **Velocity into and out of a rotating frame.** `ToFrame` computed `v' = R v - ω × r'` with the angular velocity
  `ω` expressed in the source frame and `r'` in the target frame. It now uses `R ω` and matches the states SPICE
  computes directly in ITRF93 to a few 1e-16. The error grew with the precession since J2000: in 2021, up to about
  1 m/s for a LEO state or a ground site, and 34.5 m/s for the geocentric Moon in ITRF93 (1.1 m/s in 2000).
  Positions were right. Every conversion to or from a body-fixed frame is concerned, including the velocities of
  custom `Site` objects and the `GetEphemeris` of non-SPICE objects asked in a body-fixed frame.
- **Angular velocity of TIRS, CIRS and site frames.** These frames, computed in .NET, now return their angular
  velocity in the convention of the SPICE frames: the angular velocity of ICRF relative to the frame, expressed in the
  frame. TIRS returned the Earth rotation in ICRF axes with the opposite sign, so a state converted to or from TIRS
  had its rotation term reversed: twice the rotation velocity, up to 1 km/s for a LEO state. CIRS had the same sign
  error on its 6e-12 rad/s rate (about 5 mm/s at lunar distance). The topocentric frame of a `Site` expressed the
  angular velocity of its body in the wrong axes: 1.5e-7 rad/s relative to ITRF93 in 2021, 44 m/s on the Moon seen
  in the site frame. `StateOrientation.AtDate` keeps its attitude convention and does not apply to frame transforms.
- **Numerical propagation in an inertial frame other than ICRF** ([#357](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/357)).
  A propagation may start from a state in B1950, FK4, ECLIPJ2000, ECLIPB1950 or GALACTIC, and runs in that frame.
  Three force inputs were nevertheless in ICRF and were combined with the state as they were: the ephemeris cache of
  the bodies, the third-body positions read from SPICE, and the angular velocity of the atmosphere in drag. They are
  now in the frame of the state, and `PropagationEphemerisCache` has a constructor overload that takes the frame. A
  400 km LEO with geopotential, Moon, Sun, drag and SRP, propagated for a day from ECLIPJ2000, ended 10 km away from
  the same propagation from ICRF (49 m from B1950); it now ends within 3e-5 m with a fixed step. Propagations from
  ICRF are unchanged.
- **Solar radiation pressure with the Sun among the bodies** ([#356](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/356)).
  `SolarRadiationPressure` took every body it was given as an occulter, the Sun included, which then eclipsed itself:
  the acceleration was exactly zero. The propagator passes all its bodies to SRP, so SRP had no effect in any
  propagation, scenario, batch or Monte Carlo run whose bodies included the Sun, which is the usual case. The Sun is
  now ignored as an occulter. Every such propagation with SRP on changes: for a LEO spacecraft with an area-to-mass
  ratio of 0.1 m²/kg and Cr = 1.5, the position differs by about 25 m after 3 h.

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
