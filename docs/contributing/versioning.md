---
title: Versioning
---

# Versioning

Current version information and release notes since 8.0. Breaking changes are called out in each release.

## Current Versions

| Component | Version |
|-----------|---------|
| NuGet (`IO.Astrodynamics`) | `11.0.0` |
| CLI tool (`IO.Astrodynamics.CLI`) | `11.0.0` |
| .NET framework | `.NET 10.0` |
| SPICE toolkit | `CSPICE N0067` |
| License | `LGPL-3.0-or-later` |

## Releasing

1. **The release PR**, `chore(release): prepare <version>`, sets the version in `IO.Astrodynamics.csproj`,
   `IO.Astrodynamics.CLI.csproj`, the table above and the "Version Information" of `DEVELOPER_GUIDE.md`. The CI docs
   job runs `tools/ci/check-versions.sh`, which fails when they disagree. The PR also finalizes the release notes,
   with the issues still open under **Known limitations**.
2. **Before it merges**:
   - `long-validation.yml` green on the tree being released, or on a tree that differs from it only in comments
     and documentation;
   - `gmat-reference.yml` green if what GMAT reads has changed;
   - the conformance job green on the three platforms;
   - no citation left "to be verified".
3. **After the merge**, tag the merge commit with the version itself, without a `v`: `10.1.0`, or
   `10.2.0-preview-1` for a preview (the suffix of the earlier previews).
   - The CD workflow (`cd.yml`) takes the version from the tag and writes it into both csproj. A tag that differs
     from the documented version (`10.2.0-preview1` for `10.2.0-preview-1`) therefore publishes that other version.
     nuget.org can unlist a version but never delete it, so cancel such a run before its `publish` job.
   - Push the tag once: every push of a tag starts a run. The publish step skips a version that is already on
     nuget.org, so a second run publishes nothing.
4. **A tag with a hyphen is a prerelease.** The packages are published as prereleases, and the CD does not deploy
   the documentation site.
5. **The packages appear on nuget.org** a few minutes after the `publish` job. The versions listed in
   `https://api.nuget.org/v3-flatcontainer/io.astrodynamics/index.json` (and `io.astrodynamics.cli`) confirm it.

## Release Notes

### 11.0.0

Changes since 10.2.0-preview-1: the conformance runner of the six lots of
[#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) and the defects its references
found, the integration over TT seconds about the Earth, the managed occultation, field-of-view and window searches to
1 µs, the managed TEME, the attitude timeline of a propagation and the kernels it writes, the 2D Pc and the CDM.

A major version, because it breaks the API. The entries marked **Breaking** under **Changed** list each change a
caller can meet, with its migration: among them the maneuver chain on the attitude timeline, the kernels and the
stored states of a propagated spacecraft, `Instrument.Orientation`, `InstrumentPointingToAttitude`, the reads that now
throw outside the coverage, the TT seconds of a propagation about the Earth, `Quaternion.Identity` and the `readonly`
public statics, the equality of frames, the axes the TRIAD factories point, and the straight-line motion of a star and
its proper motion in right ascension as a true angle. **Deprecated** lists
`Star.PropagationOutput`, always null. **Removed** lists `Instrument.PositionInFOV`, `LowThrustManeuver` and
`Quaternion.Zero`. Several results change: see **Fixed** and **Changed**. The open defects a caller can meet at this
release are listed under **Known limitations**.

**Added**

- **The central term of a geopotential with the GM of the model, on request.** `GeopotentialModelParameters.CentralTerm`
  (`init`) and `GeopotentialGravitationalField.CentralTerm` take a `GeopotentialCentralTerm`: `BodyGravitationalParameter`,
  the default, keeps the GM of the kernels in the central term as before, bit for bit; `ModelGravitationalParameter`
  gives the whole field the constants of the model, as GMAT applies the GM of a `.cof` header to its central term and to
  its harmonics (decided by S. Guillet, 2026-10-27). The acceleration and its partials take the same GM, so that the
  state transition matrix and the covariance follow: Φ against central differences of whole propagations at 1e-14,
  2.45e-9 (worst block, LEO 400 km, 10×10, 6 h). A field of degree 0 is then the point mass of the model's GM. For the
  Earth with EGM2008 the TT-compatible 3.986004415e14 m³/s² replaces the 3.98600435507e14 m³/s² of the kernels in the
  central term: 1.5e-8 of the acceleration, 15.6 m, 6.8 m and 18.5 m over the day of the propagators 001 to 003 of the
  conformance suite, whose GMAT runs with EGM2008's GM the library then reproduces to 8.8 cm, 6.0 cm and 15.6 cm
  (RK7(8) at 1e-11), and to 1.9 cm, 0.83 cm and 1.1 cm over the TT seconds of the day (#501, under Changed; the GM of
  the kernels 15.5 m, 6.9 m and 18.4 m away). `CelestialItem.GM`, the third bodies and the conversions of orbital parameters keep the GM of the
  kernels; for another body without constants of the model both choices give the same field. A value outside the
  enumeration throws `ArgumentOutOfRangeException`. See
  [Force Models](../reference/force-models.md#the-central-term-with-the-gm-of-the-model)
  ([#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457), decision 18).
- **`CelestialBody.GeosynchronousOrbit(longitude, latitude, epoch, siderealRotationPeriod)`: a geosynchronous orbit of
  a declared period.** The radius is a = (GM T² / 4π²)^(1/3) with the period given (for instance 86164.0998101 s for
  the Earth), so that it does not depend on the orientation data at the epoch; the rest is the method without the
  period: position from the planetocentric (longitude, latitude, a) rotated to ICRF, velocity √(GM / a) along
  ẑ × r, argument of periapsis equal to the argument of latitude and mean anomaly 0. The method without the period
  is unchanged (2π / |ω| of the body frame at the epoch); with the Earth's binary PCK of the tests, the two radii
  differ by 0.48 m at 2021-01-01. Against CSPICE (geosynchronous 001 of the conformance suite, the test kernels), the
  state agrees to 3.8e-8 m and 2.4e-12 m/s over Linux x64, Windows x64 and macOS arm64 (CI run 43773937161; 3.3e-8 m
  and 2.3e-12 m/s on Linux and macOS). Decision 18 of #457.
- **`Instrument.RotationToSpacecraft`**: the rotation from the instrument frame to the spacecraft frame of
  `Orientation`, `vectorInSpacecraft = vectorInInstrument.Rotate(q)`, the Euler angles of the frame kernel
  (`eul2m(x, y, z, 1, 2, 3)`), which the managed field of view, `GetBoresightInSpacecraftFrame`,
  `GetRefVectorInSpacecraftFrame`, `TriadAttitude` and `InstrumentPointingToAttitude` use ([#524](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/524), under Changed).
- **The range rate read over the whole window of the dense search, on request** ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628)).
  `ConjunctionAnalysisOptions.ReadRangeRateOverWindow` (`init`, default false; decision 54 of #457, S. Guillet,
  2026-11-11): on two propagations, `Analyze` and `AnalyzeAll` also read the range rate of the states at every candidate
  and at most `MaximumEventSearchStep` apart between them, and bisect each change from negative to positive, which finds
  the minima that no minimum of the Hermite of the stored steps seeds (slow co-orbital passes at long or mixed steps;
  under Fixed). Without it the search is unchanged, bit for bit, and so is its cost.
- **`OccultationSearchOptions`** (`FrontFrame`, `TargetFrame`, `ConvergenceTolerance`) and the overloads of
  `CelestialItem.FindWindowsOnOccultationConstraint` and `Site.FindWindowsOnOccultationConstraint` that take it: the
  body-fixed frames of the ellipsoids and the tolerance of the managed occultation search
  ([#277](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/277), [#495](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/495); under Changed).
- **`TemeFrame`**, public and sealed, the managed TEME of `Frame.TEME`; `new TemeFrame(eop)` takes a provider of
  UT1 − UTC ([#451](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/451), under Fixed).
- **`centerOfMotion`**, an optional parameter of the kernel-backed `Spacecraft(int, string, Time?, ...)` constructor:
  the centre about which it reads its initial orbital parameters, the Earth by default
  ([#620](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/620), under Fixed and Changed).
- **`Frame.AddStateOrientationToICRF`, `GetStateOrientationsToICRF`, `GetLatestStateOrientationToICRF` and
  `ClearStateOrientations` are virtual** ([#537](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/537), under Changed: `SpacecraftFrame`).

**Fixed**

- **The probability of collision was 0 when the hard-body radius was much larger than the covariance** ([#610](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/610)). The
  adaptive Simpson rule of the Foster integral started from 5 points R/2 apart over the disc, which all missed a Gaussian
  narrower than about R/29: C2D diag(1, 0.25) m², R 100 m, miss (30, 0) m gave 3.9e-86 for 1, miss (99.5, 0) m gave 0
  for 0.691, with no flag, through `Analyze`, `AnalyzeAll` (sorted last), `EvaluateAvoidance` (a manoeuvre into such an
  encounter passed the Pc threshold) and `ToCdm`. The integral now runs in θ along the disc (x = x_c + R sin θ, smooth at
  its edges), split at the Gaussian's mean and ±1, 2, 4, 8 and 16 σ, at the chords that reach the minor axis's ±kσ and at
  the disc's centre, so that the Gaussian is always sampled however narrow it is; nothing of the disc is dropped. The
  internal `EncounterPlaneProbability` holds it.
- **The probability of collision was accurate to 1.5e-7 absolute, and 4.6 % low below 1e-10** ([#611](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/611)). The chord
  integral took the difference of two values of `SpecialFunctions.ErrorFunction` (Abramowitz and Stegun 7.1.26, 1.5e-7
  absolute), and the Simpson rule stopped at an absolute tolerance of 1e-12, which a Pc below about 1.5e-10 met at its
  first level (−4.62 % whatever the Pc; 0 in the far tails from the erf difference). The chord probability is now an erfc
  difference on the side away from zero (CARA's `erf_vec_dif`), erf where the chord straddles the minor axis, with
  fdlibm's erf and erfc (about one ulp) private to the Pc path, and the quadrature stops at 1e-12 of the whole integral,
  shared over the segments by width, a difference at the noise of the integrand accepted and the evaluations bounded.
  That noise includes the cancellation of the erfc difference when σ ≫ the chord (bounded by about ε σ/R relative, at each sample):
  without it, a covariance far wider than the disc with a miss off its axes (σ 1e5 m, R 1 m, miss (1e5, 3e5) m) spent
  the 200 000 evaluations and returned a Pc 1.3e-6 off, with no flag. The public
  `SpecialFunctions.ErrorFunction` is unchanged.
  - **Measured** (Linux x64, Windows x64 and macOS arm64, the bounds twice the worst over the three, CI of 7f5e6dfb) against the exact Pc of the same double
    inputs, CARA's `Pc2D` at 50 digits (`tools/collision_probability`, tanh-sinh and Gauss-Legendre agreeing to 1e-30):
    the seven `collision_probability` encounters of lot 6 of #457 (Pc 2.1e-2 to 5.1e-12, a hard-body radius much larger
    than the covariance, a covariance not positive definite, Frisbee's maximum) within 2.0e-14, 9.0e-9 through `Analyze`
    (the covariance held in RTN over the tick by which the refined TCA was early; 3.2e-10 since #624, the same on the three, CI of #629 at 8c864b23); the 25 reproductions of #610 to #612 within 7.6e-15;
    NASA CARA's Omitron case 01 and Alfano case 03 within 2.6e-15, Frisbee's table 1 within 3.2e-16 (their tests now
    take the exact values: the published ones are 2.9e-6 and 2.3e-6 from them, Frisbee's given to two or three digits).
    σ ≫ R with the miss off the axes (σ 1e5 to 1e6 m, R 1 m; mpmath at 70 digits): within 5.0e-11, the noise of the
    integrand itself, at 9 to 1 425 evaluations (200 000 before); that noise is bounded by about ε σ/R relative, 0.05
    to 1 times it as measured (Marcum's closed form, a circular covariance: 1.2e-10 at σ = 1e7 R, 2.4e-8 at 1e8 R,
    6.6e-7 at 1e10 R, 2.1e-6 before); a random sweep of 500 encounters (σ/R 1e-4 to 1e6, misses to 12 σ) reaches
    neither the budget nor the depth cap (14 budget hits before, at most 9 217 evaluations after). One evaluation takes a median of 0.2 ms over the sweep, 2.2 ms
    in the deep tails (a Pc near 1e-300, 13 765 evaluations).
  - **What moves:** every Pc, `CollisionRisk.ProbabilityOfCollision` through `Analyze`, `AnalyzeAll`, `Screen`,
    `EvaluateAvoidance` and the `COLLISION_PROBABILITY` that `ToCdm` writes: by 1e-8 to 1e-5 relative above 1e-7, up to
    3.3e-4 near 1e-8, +4.8 % below 1e-10 (to the exact value), and from 0 to the risk at a large hard-body radius (#610).
- **The probability of collision was null for a covariance not positive definite in the encounter plane** ([#612](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/612)).
  The clipped eigenvalues (CARA's (1e-4 R)²) were rebuilt into a matrix and decomposed again, and λ2 = (tr − √disc)/2
  loses everything below λ1·ε: with σ_major 1000 km, R 20 m and λ2 at or below the clip (1 mm, 0 or −1 m², the
  rounding of a CDM covariance), the Pc was null with only `CovarianceRemediated` raised. The integral now takes the
  clipped eigenvalues and the rotation as they are; λ2 is det/λ1, the determinant by Kahan's fused form, so that a
  minor eigenvalue between the clip and λ1·ε is kept rather than clipped (the 30° rows of #612: `CovarianceRemediated`
  is no longer raised there); Frisbee's maximum-Pc covariance is built in the principal axes of the known one, whose
  determinant is then a sum of positive terms. `CollisionRisk.ProjectedCovariance` is still the clipped matrix, rebuilt
  for display. A null Pc becomes the clipped Pc (1.5957691e-5 on #612's case, exact to 2e-16); every other Pc moves only
  by rounding.
- **A CDM length without `units` was read in km** ([#613](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/613)). `MISS_DISTANCE`, `RELATIVE_POSITION_R/T/N`
  and `SCREEN_VOLUME_X/Y/Z` are `lengthTypeUO` in the NDM/XML schema (units optional, `m` the only unit; m in
  CCSDS 508.0 table 3-2), and the reader took km when the attribute was absent: 715 read as 715 000 m, with no error and
  the schema check passing. The default is now m. A length with `units="m"` is now the double of its text exactly (it was
  divided by 1000 and multiplied back, one ulp off at most); `units="km"`, outside the schema, is still accepted.
- **The CDM, OPM and OMM readers rejected a day-of-year date** ([#490](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/490)). The `yyyy-DDD` patterns of the
  three readers read the literal text `DDD` (`D` is no .NET specifier): every `YYYY-DDDThh:mm:ss` date, which the
  schema's `epochType` allows and the library's own schema check passed, threw, in every date field and on every load
  path, and `2021-DDDT12:00:00` loaded as 1 January. One internal parser of `CCSDS/Common`, `CcsdsEpochParser`, now
  reads the date branch of `epochType` for the three readers: `-MM-DD` or `-DDD` (1 to 365, 366 in a leap year, as
  CSPICE's `str2et`), any fraction (up to 7 digits exact, more rounded to the 100 ns tick as before), `Z` or `±hh:mm`
  applied, UTC. Measured against `tools/ccsds_epoch_reference` (CSPICE `str2et` and `et2utc` on 25 day-of-year dates,
  2000 to 2026, leap days, offsets, 0 to 7 fraction digits): every instant exact to the tick; the calendar dates give
  the `DateTime` of the former readers bit for bit; the literal `DDD`, a day 0 or a day 366 of a common year throw the
  reader's parse exception. Other text keeps the former fallback (`DateTime.TryParse`, #619).
- **A kernel-backed protected spacecraft got the RTN axes of its orbit about the Solar System barycentre** ([#620](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/620)).
  `new Spacecraft(naifId, name, epoch, ...)` read its initial state about the SSB, and the conjunction analysis takes the
  protected spacecraft's centre as its observer: `RelativePositionRtn`, `RelativeVelocityRtn`, `CombinedCovarianceRtn`
  and the `RELATIVE_POSITION_*` and `RELATIVE_VELOCITY_*` that `ToCdm` writes were rotated by the axes of an orbit about
  the SSB, up to 316.6 m and 7557.5 m/s off on a 250 m encounter. The constructor takes an optional `centerOfMotion`,
  the Earth by default, about which it reads its initial orbital parameters; the SSB, or any body the kernels chain to,
  may be given. Measured on two SPKs written by the library: the RTN relative state within 2.8e-14 m and 9.1e-13 m/s of
  CSPICE's `spkezr_c` about the Earth (and about the SSB when it is given). The TCA, the miss distance and the inertial
  relative state do not move, nor do the managed light-time, occultation and field-of-view reads of a kernel-backed
  spacecraft, which keep the SSB as their reference body, as CSPICE.
- **`AnalyzeAll` on two propagations listed a window end as a close approach** ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621), its dense-search part).
  The polynomial search took the window ends as candidates and kept an end where the distance grows into the window as a
  local minimum: over 3 h the list began with the window start, 6277 km away and receding. The window ends are no longer
  minima of the dense path: `AnalyzeAll(profile, solution, secondary, solution, window)` lists the interior minima only,
  an empty list when there are none, and `Analyze` on that path returns the closest interior minimum when there is one,
  never an end closer than it. Measured against CSPICE's `gfdist_c` (LOCMIN) on the SPKs of the propagations, refined
  on the range rate of `spkezr_c`: the four minima of 3 h within 6.5e-8 s of CSPICE's zero below the tick (1.4e-7 s before #624) and 1.1e-8 m. Without an interior
  minimum, `Analyze` still returns the end where the distance grows into the window, and `Screen` and the
  `ILocalizable` overloads keep their midpoint: #621's own change decides them. Whether a minimum lies inside the window
  is decided on the range rate of the states, the minima of the cubic Hermite of the stored steps, the window ends
  included, only seeding the search: the Hermite misses the zero by its error (#363, 12 to
  212 µs at the default steps of `new RK78Integrator()` on this crossing), within which a window ending before a minimum
  listed its unrefined Hermite epoch, a point still closing (as before 11.0.0), and a window starting before one listed
  nothing (an intermediate state of this change). A window end is now bracketed when the range rate says the minimum
  lies inside, and a minimum whose range-rate zero lies outside the window is dropped. A window end is never listed,
  not even a zero of the range rate exactly there, which `Analyze` returns as that end. A window whose candidates all lie within the flatness tolerance of the seeds (a slow pass, a
  window of a few ticks about a minimum) listed nothing and `Analyze` returned its midpoint, although the range rate
  went from negative to positive inside (1 ms off on a pass at 0.113 m/s and 50 m); the
  zero is now bisected over the window, and `Analyze` on this path never returns the midpoint. Against `gfdist_c` on
  windows ending or starting 1 µs to 1 ms before or after each of the four minima, at 60 s and at the default steps,
  and on windows of 2 and 3 ticks on each side of each minimum at 60 s steps, the same minima within the bounds above
  (9.0e-8 s and 2.7e-7 m at the default steps, their states against their SPK of 60 s outputs). A seed that the refinement could not
  bracket on the range rate within two moves of its bracket was kept unrefined: on a slow pass at the default steps
  (50 m along-track, a drift of 100 m across the plane), the Hermite seeded minima near the maxima of the distance, which
  `AnalyzeAll` listed (t0 + 4246.66 s, 111.79 m, a range rate of +0.0012 m/s, where `gfdist_c` lists no local minimum)
  and the dense `Analyze` returned instead of the closer end, 53.06 m against 111.79 m (before this change it returned that end); on a
  pass with two minima per orbit at the default steps, a seed 1.6 s past its minimum was listed unrefined, 1.35e-4 m off. A seed the secants
  cannot bracket is now searched on its own range rate, from the seed toward the side it points to (1 ms, then distances
  doubling up to 1.024 s and 1 s apart after that, up to the neighbouring candidate: an interval of the other sign 1 s
  wide or more holds a read, where distances doubling all the way stepped over a shallow minimum next to a fold of the
  range rate, 2.9e-7 m deep and positive over 1.6 s, lost in 204 to 229 windows of each propagation of a probe (#628)
  while the search before this change listed its seed unrefined, 4.85 s off at 60 s steps), then between the candidates, moved twice at
  most, then by following the range rate over the candidates to the nearest change from negative to positive, or
  dropped: every TCA of the dense path is such a zero, one per pair of consecutive candidates. Against `gfdist_c` on
  1187 to 1524 windows of each slow pass, of each pass with two minima per orbit and of each pass with the fold (windows
  ending or starting at the zero included, the reference refined below the tick), the local minima, each once, and no
  maximum, within 1.5e-7 s and 3.14e-10 m at 60 s steps, 2.9e-5 s and 1.15e-6 m at the default steps, 5.05e-2 s and
  2.1e-7 m with a drift of 1 m at mixed steps (the states and their SPK placing the zero apart by the difference of
  their range rates over its slope, 2.6e-6 m/s per second), 6.5e-7 s to 3.3e-6 s and 4.8e-8 to 9.3e-8 m with two minima
  per orbit, and at the fold 3.3e-5 s and 3.9e-8 m at 60 s steps, 2.1e-3 s and 9.7e-7 m at the default steps (its
  states and their SPK 2.1 ms apart).
- **SPICE failures of the ephemeris reads and of the frame transformations were swallowed** ([#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454), the
  ephemeris read and `TransformFrame`). `ReadEphemerisAtGivenEpochProxy` left its state unwritten when `spkezr_c`
  failed (r = (4.9e-321, 6.95e-307, 6.95e-307) m past de440s on Linux, whatever the stack held elsewhere), and
  `TransformFrameProxy` returned the zero quaternion: `Analyze` of two kernel-backed spacecraft past their SPKs gave a
  TCA with a miss of 0 m and 2888 SPICE messages on stdout. The native read now writes NaN and keeps the SPICE message
  for `GetLastErrorProxy` (4 lines, the second change of the frozen C++ layer in 11.0.0, decision of S. Guillet,
  2026-11-09), and `SpiceAPI.ReadEphemeris` (at an epoch and over a window), every `GetEphemeris` of a kernel-backed
  object and the kernel-backed `Spacecraft` constructor throw `InvalidOperationException` with that message
  ("Insufficient ephemeris data has been loaded ..."). `SpiceAPI.TransformFrame` throws when SPICE gives no rotation
  (a quaternion whose norm is not 1 to 1e-9), naming the frames and the epoch, for every frame (only the TEME path did
  before). Reads inside the coverage do not move. A read at the very first instant of an SPK can fall a fraction of a
  tick before it (the 100 ns of a `Time` in TDB) and now throws: the LRO tracking test starts 5 minutes after its SPK,
  as the conformance case tracking_data 001 does. A planet or a moon that the loaded kernels do not chain to its system
  barycenter (Mars to Pluto and their moons with de440s alone) is built with `InitialOrbitalParameters` null instead of
  an undefined state, and its `SphereOfInfluence` is then +Infinity, as for a body built from custom parameters (before,
  a value of the undefined state: 0 for Mars and Jupiter in a probe); a read of its position throws. The orientation cache of a propagation's central
  body (`PropagationFrameOrientationCache`) keeps a grid point SPICE cannot give as unavailable, so a point-mass
  propagation about the Earth still runs past the Earth's PCK, and a geopotential or drag there throws (they used the
  zero rotation before). A window that ends (or starts) inside the coverage, its next grid point outside, no longer
  throws for that point: the point after the window end is replaced by the orientation read there, and a read the grid
  cannot interpolate reads the frame at its epoch, so only an epoch SPICE does not cover throws, with SPICE's message (a
  propagation up to the last instant of `earth_latest_high_prec.bpc`, or 30 s before it, threw at its last minute).
  A propagation still reads past its window end when its output step does not divide the window:
  it runs to round(L/ΔT)·ΔT, up to ΔT/2 past the end, and a geopotential or drag read there past the coverage throws
  (a window of 3570 s at 60 s steps ending at the end of the coverage throws 30 s past it). The other proxies keep #454.
- **The `ILocalizable` conjunction overloads extrapolated past a propagated spacecraft's states** ([#527](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/527), its
  guard). `Analyze`, `AnalyzeAll` and `Screen` read a propagated protected or secondary spacecraft through its stored
  states without a coverage check, so a window past them was extrapolated (567 km off 6 min out); they now throw
  `ArgumentException` (`screeningWindow`) when the window leaves the stored states, as the `PropagationSolution`
  overloads did. The check, `Spacecraft.CheckInterpolatedStatesCover`, is the one of the managed field-of-view search.
- **The refined TCA was up to one tick early** ([#624](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/624)).
  The bisection of the range rate that refines every TCA of `Analyze`, `AnalyzeAll` and `EvaluateAvoidance`, on the
  sampled path of the `ILocalizable` overloads and on the dense path of two propagations, stops on a bracket one tick
  wide (100 ns, the resolution of `Time`; the default tolerance, 1e-9 s, lies below it), and returned its middle through
  `TimeSpan.FromSeconds`, which truncates to the tick: always the lower tick, half a tick before the zero on average and
  up to one tick, a zero exactly on a tick one tick early. It now carries the range rates at both ends of its bracket,
  which every caller already holds (no extra evaluation), and returns the tick nearer the zero of their chord: the upper
  one when its range rate is smaller in magnitude, the lower one otherwise (a tie included), so half a tick at most from
  the zero of a range rate linear over the tick, and a zero exactly on a tick is that tick. A bracket left wider than a
  tick by a tolerance of a tick or more gives its middle, in ticks, as does each step of the bisection, whose middle
  `TimeSpan.FromSeconds` also truncated (the same last bracket about a single zero, another zero of a bracket holding
  several at the noise level). On the dense path, a zero within the first or the
  last tick of the window that rounds onto a window end is the tick next to that end inside the window, a window end
  being no close approach (#621); the sampled path keeps the nearer tick, an end included. `Screen` does not refine.
  - **Measured** (Linux x64, Windows x64 and macOS arm64, the bounds twice the worst over the three, CI of #629 at
    8c864b23, 2026-11-10; the figures the same on the three): linear range rates whose zero lies 0 to 1 tick after a
    tick, evaluated in exact tick arithmetic, give the nearest tick on every route of the refinement (the secants, the
    seed's own range rate forward and backward, the widened bracket, the range rate followed over the candidates, the
    sampled search) and at the window ends. The seven `collision_probability` encounters through `Analyze`, whose exact
    TCA lies within 4.3e-7 tick of a tick: the Pc within 3.15e-10 of its exact value (005, at its exact tick; 9.03e-9
    before, 003 and 005 returned one tick early), asserted at 6.4e-10 (6.3e-10 until #668, whose three-digit prints give
    3.1549e-10 on Linux x64, Windows x64 and macOS arm64, CI run 43745883709, twice which is above 6.3e-10; 1.9e-8
    before #624). Against `gfdist_c` on the
    crossing LEOs, the TCA within 6.4e-8 s of CSPICE's zero below the tick at 60 s steps and 9.0e-8 s at the default
    steps (1.5e-7 s before), asserted at 1.8e-7 s (4e-7 s, when the reference was CSPICE's minimum on the grid of ticks,
    up to 0.96 tick from its zero); the #620 RTN position within 1.5e-4 m of the design (6.3e-4 m before), asserted at
    3.1e-4 m. The runner on the suite's main (33286d2, Linux x64, 2026-11-10), 201 of 201: cdm 007 from 0.664 to 0.019
    of its tolerance (the relative position of the written CDM 6.6e-7 km to 4.3e-13 km; its TCA written
    12:00:00.0000000Z, 11:59:59.9999999Z before), conjunction 007 from 0.497 to 0.027 (its TCA 1.27e-7 s to 2.66e-8 s
    from CSPICE's).
  - **What moves:** every refined TCA, by at most one tick toward the zero, and what is computed there: the states at
    TCA by v·δt (up to 0.77 mm at 7.7 km/s), the miss distance at second order, the RTN axes and the covariance held in
    RTN, the Pc, and the `TCA` and states that `ToCdm` writes.
- **The conformance runner counted its TT seconds through `ToTDT`** ([#630](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/630)).
  `tca_s` of the propagated objects (`ConjunctionSolver`), `burns[].elapsed_s` (`ManeuverSolver`) and
  `events.*[].elapsed_s` (`OrbitalEventsSolver`) were `(t.ToTDT() - epoch.ToTDT()).TotalSeconds`, and the TDB → TT
  conversion of a TDB label subtracts TDB − TT through `TimeSpan.FromSeconds`, which truncates it toward zero to the
  100 ns tick: a TDB label came out late by 0 to 1 tick when TDB − TT is positive (+0.47 ms on 2026-01-20, the date of
  the conjunction cases), early when it is negative. The three now count with `PropagationTimeArgument.Tt.SecondsBetween`
  (the runner's `TerrestrialSeconds`, the internals of the library visible to the runner), the rule of the library's
  references about the Earth. The library is unchanged.
  - **Measured** (Linux x64, 2026-11-10): against mpmath at 50 digits on the library's TDB − TT series
    (`tools/conformance/terrestrial_seconds_reference.py`), 100 TDB labels in the 6 h after 2026-01-20T06:00:00 UTC and
    100 after 2026-10-20T06:00:00 UTC (−1.61 ms) within 3.64e-12 s, one ulp near 2.16e4 s, asserted at 7.3e-12 s
    (twice the worst, the same on Linux x64, Windows x64 and macOS arm64, CI of #632 at 01d14e2c); the former expression
    was off there by +0.0004 to +0.992 tick
    and by −0.995 to −0.006 tick. CSPICE (`unitim`) confirms the TT of both UTC epochs to the tick. The runner on the
    suite's main (33286d2), 201 of 201: 20 `tca_s` and 17 `elapsed_s` deltas move, each value by −0.05 to −0.98 tick,
    and no other key (4727 unchanged; the events' epochs are UTC, exact either way); no status changes.
- **A test swapped the global data provider** ([#631](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/631)).
  `TemeFrameTests.FrameByName_IsRoutedByTheMemoryDataProvider` set `Configuration.Instance` to an empty
  `MemoryDataProvider` for a few milliseconds, so that a body, frame, spacecraft or site another test class built in
  parallel kept the empty provider and threw `KeyNotFoundException` (a probe building the Earth in a loop beside it:
  41 to 155 failed builds in each of 5 runs, none without the test). It now calls a `MemoryDataProvider` directly, as
  the other tests of the provider do, with the same two assertions; no other test changes the global provider.
  The library is unchanged.
- **A TLE converted on one thread while another first used `PlanetsAndMoons` hung the process** ([#623](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/623)).
  `SpiceAPI.ConvertTleToStateVector`, so `TLE.ToStateVector`, `TLE.ToOsculating` and every SGP4 propagation, built the
  Earth of its state, `new CelestialBody(PlanetsAndMoons.EARTH, ...)`, while it held the SpiceAPI lock. Reading
  `PlanetsAndMoons` runs its type initializer, which builds `EARTH_BODY` and `MOON_BODY` and so takes the same lock. When
  another thread was running that initializer, the first thread waited for it while holding the lock, and the other
  thread waited for the lock: neither ended, and the runtime, which detects only cycles between type initializers, raised
  nothing. The audit of the other locked sections found the same pattern in both `SpiceAPI.ReadEphemeris` overloads by
  NAIF ids: their observer, `CelestialItem.Create(observerId)`, was built under the lock, and for a Lagrange point (391 to
  395) the constructor of `LagrangePoint` reads `Barycenters.EARTH_BARYCENTER`, whose initializer takes the lock. Now
  only the native call runs under the lock in `ConvertTleToStateVector`, the zero-state check and the Earth after it,
  and `ReadEphemeris` builds its observer before it; the states are the same bit for bit (same native calls, same
  inputs). No other code of the library under the lock reaches `PlanetsAndMoons`, `Stars` or `Barycenters`, the only type
  initializers of the library that read SPICE. Code a caller supplies can still run under it: a custom `IDataProvider`,
  through the bodies the element conversions of `SpiceAPI` build and the frames of an ephemeris read in TEME, CIRS or
  TIRS, and the enumeration of the states given to `WriteEphemeris` or `WriteOrientation`; it must not read the three
  classes.
  - **Measured** (Linux x64, 4 cores, Release, 2026-11-11), each run a fresh process, two threads started behind a
    barrier, a run hung when it had not ended 3 s after its start: before the fix, `ConvertTleToStateVector` against the
    first read of `PlanetsAndMoons.EARTH_BODY` hung 120 of 120 runs, `TLE.ToStateVector` 35 of 200, `ReadEphemeris` from
    L1 against the first read of `Barycenters.EARTH_BARYCENTER` 120 of 120; with the first thread started once the second
    is inside the initializer (a data provider holds it there 200 ms), every run of the three hung (40, 40 and 30 runs).
    The window overload of `ReadEphemeris` from L1, with only its fix reverted, hung 3 of 3 runs of each kind, while the
    single-epoch overload ended. After it, none of 1200 runs (200 per case) hung, nor 40 runs of the window overload; a
    run takes 0.11 to 0.40 s, 0.12 to 0.53 s with the 4 cores kept busy.
  - **Test:** `ColdStartTests` starts the console project `IO.Astrodynamics.Tests.ColdStart`, which the test project
    builds and copies to `ColdStart/` next to its output, with the dotnet host that runs the tests: four cases
    (`ConvertTleToStateVector`, `TLE.ToStateVector` and both `ReadEphemeris` overloads from L1), 3 children of each
    with the initializer held and 3 behind the barrier, each required to exit 0 within 60 s, a child that does not
    killed with its process tree. A test inside the test process cannot see the defect, since another test has already
    run the initializers. With the fix reverted the cases fail at their first child.
- **A number of seconds became a `Time` truncated toward zero** ([#625](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/625)).
  `Time.Create` (both overloads), `CreateTDB`, `CreateUTC`, `AddSeconds`, `AddMilliseconds`, `AddMicroseconds` and the
  TDB − TT offset of the TDB conversions went through `DateTime.AddSeconds` or `TimeSpan.FromSeconds`, which truncate
  to the 100 ns tick: up to a tick early after J2000 and late before it. Every epoch that comes back from SPICE as
  seconds goes through `Time.Create`, so an ET from 2017 to 2028 came back an ulp earlier for 40.6 % of the ET, and a
  kernel-backed spacecraft built at the first instant of its SPK (`spkcov`) read 84 ns before its coverage and threw
  SPKINSUFFDATA (since #454). Each now takes the nearest tick of the exact value of its double, ties to even, through
  one internal helper, `Time.SecondsToTicks`: the magnitude of the value, its whole units apart, the fraction times the
  ticks per unit rounded once, and its residual, exact by a fused multiply-add, deciding the side of a half tick, the
  sign put back last (rounding the product alone, as the issue's sketch did, misses the nearest tick of about 5 doubles
  in a million near a half tick; a floor of a negative value above −1 unit leaves a fraction 1 − |value| that double
  cannot hold, which missed half of the near ties there, −2.5e-7 s giving −3 ticks for −2). The step epochs of a
  propagation about a body other than the Earth, as the conjunction analysis reads them, and the epoch of a two-body
  motion (`EpochAtMeanAnomaly`, so `TimeToRadius`, `TimeToSphereOfInfluence` and `ToStateVector(trueAnomaly)`) take
  the labels of the propagation, `AddSeconds`, where they added `TimeSpan.FromSeconds`. The TDB − TT
  offset rounds too, the open choice of the issue: measured on the conformance runner, rounding it moves 798 more delta
  entries than rounding the other conversions alone (1915 against 1117 of 4764), most of them smaller (the attitudes,
  748 smaller against 308 larger, mean ratio 0.93; the propagators 0.94; the time conversions 0.91), so that every
  conversion of seconds follows one rule. `PropagationTimeArgument.Tt.SecondsBetween` and the runner's
  `TerrestrialSeconds` count from tick-exact labels and the series in double, so they do not depend on it.
  What a double holds limits the cure. From 2017 to 2028 (and 1971 to 1983) an ET comes back exactly, but a tick made
  seconds and back is the neighbouring tick for about 16 % of ticks (its double up to 0.6 tick from it), so
  `SpiceAPI.ReadEphemeris(Time …)` and the initial state of a kernel-backed spacecraft, which labelled the state with
  the epoch SPICE returned, now carry the epoch asked, as `SpiceDataProvider.GetEphemeris` does, and a window end of the
  CSPICE searches of `SpiceAPI` at an end of the search window is that end (for example:
  2024-01-11T00:35:02.8486908 TDB was labelled .8486907, and a search ending 2024-02-29T03:34:37.2052700 gave a window
  ending .2052701, outside it; a start could come back a tick inside). The other epochs SPICE gives as seconds (the grid
  of `ReadEphemeris(Window …)` and `ReadOrientation`, a transition inside a window, the element conversions) are the
  nearest tick of their double. From 1983 to 2017 a tick is coarser than a double, so no tick holds every ET: the
  nearest tick of a coverage bound lies outside the coverage for about a third of the bounds, at either end (30 % from
  2000 to 2017, 37 % from 1990 to 2000, on the double SPICE receives), where the truncation always kept a start from
  1983 to J2000 and an end from 2000 to 2017 inside (and put 80 % of the starts from 2000 to 2017 and 87 % of the ends
  from 1990 to 2000 outside).
  - **Measured** (Linux x64, 2026-11-10): the helper, `Create`, `AddSeconds`, `AddMilliseconds` and `AddMicroseconds`
    give the exact nearest tick (Python `Fraction`, `tools/seconds_to_ticks`) of 1978 values: the issue's (±0.12345678999 s,
    1234568 ticks; the start of the LRO SPK, 7558704691834258 ticks), exact ties, near ties, 8 doubles where rounding the
    product misses, 1500 ET from 1968 to 2031, 234 near ties above −1 unit and their mirrors (968 of them not their
    truncation); a span and its opposite give a time back; 200 000 ET from 2017-01-05 to
    2028-07-17 come back exactly; `new Spacecraft(-85, "LRO", Time.CreateTDB(a))` at the start of its SPK builds and its
    state is spiceypy's `spkezr(-85, a, J2000, NONE, 399)` to the bit, at both ends of the coverage; a tick made seconds
    and back is that tick from 1983 to 2017 and within one tick from 1971 to 1983 and 2017 to 2028 (100 000 each, the
    exact nearest tick of the double); 2001 states of the Moon read at ticks of 2024 and 301 LRO spacecraft built at
    ticks of its SPK carry the epoch asked (10 036 of 20 000 and 1 441 of 3 000 did not before the fix, 3 267 and 474 at an
    intermediate state of it), and 201 `gfdist` searches of 2024 give back their search window tick for tick (28 of 300
    ends past it and 22 starts before it at that state, 146 starts before it before the fix).
  - **What moves:** every epoch built from seconds, by a tick at most, and what is computed there. The window epochs of
    CSPICE's searches (the same searches in spiceypy, `tools/seconds_to_ticks/spice_windows_reference.py`, whose
    truncated mode gives the former strings), the labels of the stages and outputs of a propagation, so the three
    propagation goldens are recaptured (the step control takes other steps from the first one, the same counts and the
    same maneuver epoch; 1.37 mm, 0.31 mm and 0.82 mm at most), and the TDB round trip of a time, which now moves a tick
    at 1 of the 935 041 instants of #510 (2 when it truncated). The runner on the suite's main (33286d2), 201 of 201:
    1915 of the 4764 delta entries move, no status changes; the times by a tick (2.0e-7 s at most for the burns,
    5.0e-7 s for the events of `orbital_events`), the eclipse and station transitions and culminations within the
    microsecond of their root finders (up to 2.3e-6 s), the states at the level of a tick of motion and of the
    integration error of other steps (the propagators' final states by 8.2e-6 km at most, the maneuvers' by 4.6e-6 km).
    Two bounds of the tests measured again (Linux x64, Windows x64 and macOS arm64, CI run 42940000708 at 481ff2a2:
    the same on the three but the miss, 3.13e-9 m on Windows x64 and 2.46e-9 m on macOS arm64), and a third set from its
    analytic bound and asserted on the three platforms: the grazing fields of
    the managed field-of-view search (801 ELLIPSOID LT on its kernels 1.20e-3 s, an ill-conditioned edge that the same
    search moves by 9.6e-4 s between the propagated states and their kernels; 802 LT 6.7e-6 s, LT+S 8.84e-5 s), the miss
    of a conjunction read through stored states (3.12e-9 m, a few ulps of a LEO position), and, for the third, a
    Velocity-Verlet output a tick before an impulse, which reads the earlier arc, whose closing state carries the label of
    the impulse, the nearest tick of its instant: half a tick of motion at most (3.96e-4 m at 8.6 km/s, [#638](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/638)). The noise of the central
    differences of the F1 validation is drawn again (run 42945251492 of `long-validation.yml`, which failed on the former
    records; run 42997217987 passes with those below), so the full degree of
    `GeopotentialPartialsDegreeTests` is held below an upper record (6.7e-8 for R4, 2.5e-7 for R2) and Ψ_Cd of R5 of
    `WholePropagationStepStudyTests` at its chosen 5 % to a record of 4.1e-7, each twice the worst of eleven starts moved
    by −5 to +5 ticks, where a factor of 2 about one draw broke, and the records of `StmValidationTests` and
    `ManeuverStmValidationTests` stay (decision 53 of #457, S. Guillet, 2026-11-11). The other figures of the central
    differences and of the rounding of the integration are another draw, measured again from the CI of #634: the F1
    figures of `NonConservativeVariationalTests`, `CovariancePropagationTests` and `PenumbraStepControlTests`, the GMAT
    Φ table of the validation guide (its models and the library's computation; the totals do not move), the symplectic
    defects of `StructuralPropertiesTests` on the three platforms and the avoidance covariances, all within their bounds. A coverage start from 1983 to J2000 or
    an end from 2000 to 2017 given as seconds, which the truncation put inside, now falls outside about a third of the
    time, and a read there throws: the coverage of ITRF93 in the test kernels starts at −43135.816087188054 s, whose
    nearest tick is 11.9 ns before it (the truncation gave the tick 88.1 ns after it), so the test of the orientation
    cache of a window starting inside the coverage starts one tick later; read from the first tick inside a bound.
- **`TimeToRadius` returned an apsis for a radius the orbit never reaches, and failed with NaN or an overflow far out** ([#627](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/627)).
  It clamped cos ν to [−1, 1], so a radius below the periapsis gave the periapsis and a radius above the apoapsis of an
  ellipse the apoapsis, both silently, and an infinite radius on a hyperbola, the asymptote, gave a NaN mean anomaly
  that `TimeSpan.FromSeconds` refused (`ArgumentException`); `TimeToSphereOfInfluence` passed it the +Infinity sphere
  of influence of the Sun, of a body built from custom parameters and of a planet the kernels do not chain to its
  barycenter (#454). `TimeToRadius` now checks the radius first: not finite, below the periapsis p / (1 + e) or above
  the apoapsis p / (1 − e) of an ellipse, it throws `ArgumentOutOfRangeException` naming the radius and the reachable
  range (a radius within 1e-12 relative of an apsis is that apsis: its other expressions differ from p / (1 ± e) by 6e-16
  to 7e-16); a radius reached outside the epochs a `Time` holds throws an `ArgumentOutOfRangeException` that says so,
  after the last or before the first. A circular orbit (e = 0), at its radius at every instant, returns its epoch, where
  0 / 0 gave NaN (and a radius an ulp from p an arbitrary epoch). On an open orbit already past the radius the epoch is
  its outbound crossing, before the epoch of the orbit, as before. An ellipse with 1 − 1e-6 < e < 1, whose anomalies the
  library takes as parabolic, keeps a time about 8 times off, and its epoch can be before the epoch of the orbit even for
  a radius not yet reached (EpochAtMeanAnomaly adds a period only for e ≤ 1 − 1e-6); pre-existing, see **Known limitations**
  ([#478](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/478)).
  `TimeToSphereOfInfluence` throws `InvalidOperationException` ("<body> has no finite sphere of influence; an orbit
  about it never leaves it") for an infinite sphere of influence. The recommendation of the issue: throw, no `Try`
  method.
  - **Measured** (Linux x64, 2026-11-10) against the closed form of the hyperbola at 50 digits
    (`tools/time_to_radius`, mpmath 1.4.1 and spiceypy 8.3.0): about the Sun (a = −1.5e11 m, e = 2) 1e12 m within
    6.3e-8 s and 1e15 m within 1.04e-2 s, where the true anomaly is 2.6e-4 rad from the asymptote and one ulp of it moves
    the time by 5.0e-2 to 6.2e-2 s, so the figure is the rounding of the platform's libm (up to 3.944e-2 s for a libm
    within one ulp per call, `tools/time_to_radius/time_to_radius_libm_envelope.py`, asserted at twice it); the Moon's
    sphere of influence, one ulp from spiceypy's, reached within 4.0e-8 s; the past crossings, at M0 = 10 rad (1e12 m, −2.33e7 s)
    and M0 = 1e3 rad (the Moon's sphere, −4.99e6 s), within 1.12e-7 s and 2.4e-8 s.
- **The dense search lost minima that no minimum of the Hermite seeded** ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628)).
  On two propagations, `AnalyzeAll` and `Analyze` read the range rate of the states only near the minima of the cubic
  Hermite of the stored steps, so a minimum of the states with no such minimum near it was not listed: with mixed steps
  (the default for one trajectory and 60 s for the other), the range rate closing at one candidate and receding at the
  next with no seed near (form A); with long steps, one step holding a minimum, a maximum and a second minimum, its
  Hermite one minimum only (form B). `Analyze` then returned a window end or another minimum (504.6578 m against the
  missed minimum's 504.6428 m). Resolved by an option, opt-in (decision 54 of #457, S. Guillet, 2026-11-11, after
  weighing the cost below against a gain measured on slow co-orbital passes only): by default the search is that of
  before, bit for bit, on a probe of 149 scenarios (#628; 141 837 windows: every list of
  `AnalyzeAll`, every result of `Analyze` and the SPKs), at its cost (16.5 to 21.5 ms against 16.8 to 18.9 ms on the day
  below, the same 31 minima), and every TCA it lists is a minimum of the states (inside the window, in order, none twice;
  new tests `Dense_ByDefault_*` against CSPICE), but a minimum no seed reaches may be lost: `Analyze` then returns another
  minimum, 1.7 mm to 9.6 cm farther (median 3.5 cm, 4039 of the 141 837 windows), or a window end (1275 windows: farther
  in 845, by up to 1.46 m, median 4.5 cm; closer in 430, by up to 1.36 m, an end being no close approach). With
  `ConjunctionAnalysisOptions.ReadRangeRateOverWindow = true` (under Added), the search reads the range rate over the
  window as first written for #628, with the same results bit for bit on the same windows: the range rate is also read
  at every candidate and, between two candidates more
  than `MaximumEventSearchStep` (60 s by default) apart, at the fewest epochs evenly spaced that leave no gap wider by
  more than one tick (about the Earth a step of 60 TT seconds may span 60 s and one tick of its TDB labels), as the
  sampled search does, and each change from negative to positive between two consecutive reads that holds no TCA
  yet is bisected to the tick nearer its zero, one TCA per pair of reads. The search from a seed still reads 1 s apart
  up to its neighbouring candidate: capped at `MaximumEventSearchStep`, it read 29 to 33 % fewer states at fixed 600 s
  steps for one trajectory, but the reads up to 60 s apart then stepped over a positive interval of 2.3 to 43 s lying
  more than 60 s past its seed, lost in 21 to 135 of 843 windows (#628). Each epoch is read once, with the
  option or without it.
  - **Measured with the option**, against the default (Linux x64, 2026-11-10 and 2026-11-11, against CSPICE's `gfdist_c` LOCMIN on the SPKs of the
    propagations, refined on `spkezr_c`'s range rate, and against a scan of the range rate of the states every 0.25 s):
    on the passes with two minima per orbit (a circular LEO and a Clohessy-Wiltshire ellipse of 1000 m about it, 843 to
    1084 windows per propagation), the windows losing a minimum go from 10 to 95 by default to none at mixed default and 60 s steps
    (the default for the first trajectory: y0 = 1490, 1495, 1497, 1499 and 1500 m; 60 s for the first: y0 = 1495 and
    1497 m); from 116 to 318 to none at fixed 300 s steps (y0 = 1495 to 1499 and 1500 m), at fixed 600 s steps
    (y0 = 1480, 1481, 1483, 1485, 1486, 1488, 1490, 1492, 1494, 1495, 1498, 1499, 1499.5 and 1500 m) and at 600 s with
    the default (y0 = 1490, 1495 to 1497, 1499 and 1500 m). The passes measured that lose none by default lose none with the option:
    y0 = 1480 m at mixed steps, 1490, 1499 and 1500 m with 60 s for the first trajectory; 1480, 1486, 1490 and 1492 m
    at fixed 300 s steps; 1480 and 1486 m at 600 s with the default. The two minima of one 600 s step (t0 + 1317.45 s
    and 1526.28 s at y0 = 1490 m) are both listed; at fixed 600 s steps with y0 = 1501 to 1502.33 m (a positive
    interval of 2.3 to 43 s between the two minima of one step), from 165 to 170 to none; at fixed 600 s steps with
    y0 = 1496 and 1497 m, from 309 and 341 to 1 and 152 of 948 (not changed, below). Over 81 scenarios of the
    probe of #628, no TCA listed by default moves but at the level of noise below, and no entry is
    spurious, a window end, a duplicate or out of order. The new fixtures of `ConjunctionKernelAndWindowTests`, over
    1353 to 1727 windows each, give 6.1e-6 to 6.5e-5 s and 6.6e-7 to 2.0e-6 m at mixed steps, 3.1e-5 to 1.0e-4 s and
    1.3e-5 to 2.1e-5 m at 600 s steps (the states against their SPK), asserted at twice the worst over Linux x64, Windows
    x64 and macOS arm64, which give the same figures but CW600's miss on Windows x64 (1.278e-5 m). The conformance runner on the suite's main (33286d2), 201 of 201, its 4764 deltas
    unchanged bit for bit, with the reads (in 26.7 to 27.3 s instead of 25.7 to 26.0 s) and by default alike.
  - **Not changed by the option.** An interval of either sign of the range rate narrower than the spacing of the reads, a positive one
    between two closing reads or a negative one between a zero found from a seed and a later minimum in the same pair of
    reads, which no seed's reads 1 s apart reach (a seed whose secants are not checked and whose range rate points toward
    it, up to its neighbouring candidate; a seed whose secants are checked reads nothing between itself and their zero),
    is still not guaranteed to be found: the shallow minimum next to a fold of the range rate (2.9e-7 m deep,
    about 1 s wide) is lost in 200 to 205 of 843 windows without a seed before it, as before; at fixed 600 s steps with
    y0 = 1497 m, the first of the two minima of one step (t0 + 1373.31 s, 502.851 m), the range rate positive over
    27.9 s after it and both reads around that interval closing, the step's only seed refining to the second (1483.67 s,
    502.824 m), in 152 of 948 windows, the whole window included, and at 1496 m (46.1 s) in 1. A smaller
    `MaximumEventSearchStep` resolves the intervals wider than it (at 10 s, the first minimum at y0 = 1497 m is lost
    in none of 948 windows; at 1 s, the fold of 2.35 s at mixed steps in none of 843). Below
    about 1e-8 m/s of range rate the minima listed follow the noise of the states: on the slow passes (range rates of
    1e-12 to 1e-13 m/s), zeros of 26 windows ending within the first 0.35 s are reached by the reads in place of the
    seeds (by up to 1.2e-2 s); at a drift of 0.01 m a few more are listed.
  - **Cost of the option.** One state per candidate and per sample: on two LEO trajectories over one day with EGM2008 10×10, the Moon
    and the Sun at the default steps (31 minima), `Analyze` reads 2063 states instead of 218 and takes 103 to 111 ms
    instead of 16.5 to 21.5 ms (about 6×); 76 to 80 ms at a `MaximumEventSearchStep` of 120 s (1350 states), 53 to 57 ms
    at 300 s or more (1001 states; the run of the default figures above, three runs of each build in turn, 2026-11-11). At 60 s steps for
    both trajectories the reads add no sample (a step of 60 TT seconds may span 60 s and one tick of its TDB labels,
    which is not read at its middle), and those at the candidates take the states read per window of a probe of 149 scenarios (#628)
    from 54 to 78 on average; at fixed 600 s steps for one trajectory and the default for the other, 469 to 554
    against 481 to 564.
- **A cyclic maneuver chain ended the process, or a propagation that never returned** ([#585](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/585)).
  `Maneuver.SetNextManeuver` accepted the maneuver itself or an earlier one of its chain, and nothing walked the chain
  with a record of the maneuvers seen: `Maneuver.Reset` recursed along `NextManeuver`, so the first
  `Spacecraft.Propagate` (and the scenario, the batch and the Monte Carlo propagators through it) ended with a stack
  overflow that no `catch` sees (exit code 134, measured on `apo → apo`, `apo → peri → apo`, `nadir → nadir` and
  `apo → nadir → apo`); through the builder, which never resets, a cycle with an attitude never returned and a
  self-gating impulse fired again at one epoch until the memory ran out. Now `SetNextManeuver` throws
  `ArgumentException` when the chain from the next maneuver comes back to the maneuver or already holds a cycle, and
  leaves the chain as it was; a cycle built through a subclass that sets the protected `NextManeuver` is rejected by
  `Spacecraft.SetStandbyManeuver` (`ArgumentException`), by the construction of the propagator (`InvalidOperationException`, before the states of an
  earlier propagation are cleared) and by `PropagatorBase.Propagate`. `Maneuver.Reset` resets one maneuver; the chain is
  reset in a loop (`Maneuver.ResetChain`), so a long chain uses no stack. A chain is a list: a maneuver appears in it
  once, a repeated burn is a new maneuver. A valid chain propagates as before (the same calls in the same order).
  - **Test:** `ManeuverChainCycleTests` (33 cases): `SetNextManeuver` closing five kinds of cycle throws and keeps the
    chain, valid links and shared tails are kept; a cycle through a subclass makes `SetStandbyManeuver` throw, and
    `Spacecraft.Propagate` (three overloads, RK7(8) and Velocity-Verlet, a leading attitude, an impulse, an attitude after
    an impulse) throw with the previous states kept, the builder throw at `Build` or, for a cycle made after it, at
    `Propagate`, the batch report the error; a chain of 200 000 attitudes resets without overflow. With the maneuver
    code of 3eb1edc0 the test host crashes ("Stack overflow").
- **A CSPICE window search with `NotEqual`, `GreaterOrEqual` or `LowerOrEqual` ended the process** ([#482](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/482)).
  The native layer parses the relation before it calls `gfdist`, `gfposc` or `gfilum`, and threw a C++ exception that
  nothing caught for `!`, `>=` and `<=` (exit code 134, measured on every CSPICE path: `CelestialItem` and `Site`
  distance and coordinate searches with kernel objects, `Site.FindWindowsOnIlluminationConstraint` of a kernel site, the
  three `SpiceAPI` searches and the three command-line searches). `SpiceAPI` now hands CSPICE only the relations it
  knows: `GreaterOrEqual` and `LowerOrEqual` are searched as `>` and `<`, whose windows are closed, so for a quantity that
  crosses the value they are the windows the managed search gives (it bisects the relation itself; the two differ only
  where the quantity stays equal to the value over an interval, which `>` leaves out); `NotEqual`, which CSPICE has
  not, is the search window less the instants of `=`, a window from each crossing to the next, the managed search's
  definition since lot 4. Both paths so accept every operator and give the same windows; throwing for `NotEqual` on the
  CSPICE path alone (#482's first recommendation) would have made a call that works with a propagated spacecraft throw
  once the spacecraft comes from a kernel. A value outside the enumeration throws `ArgumentOutOfRangeException`. The
  command-line help lists `<=`, `>=` and `!`.
  - **Test:** `SpiceOperatorTests` (18 cases) against `gfdist`, `gfposc` and `gfilum` in spiceypy
    (`tools/geometry_reference/operators_reference.py`), equal to the tick: the three `SpiceAPI` searches, the coordinate
    search of a kernel spacecraft (LRO from the Moon) and of a kernel site (DSS-13 from LRO), with the three operators;
    the distance search of a kernel spacecraft both ways and the illumination search of a kernel site against their own
    `>`, `<` and `=` searches (their thresholds carry #466's factor and the native surface point's altitude defect); the
    command line with `>=`, `<=` and `!` (`GeometryFinderTests.DistanceConstraint_OperatorsCspiceHasNot`). With the
    `SpiceAPI` of 3eb1edc0 the test host crashes ("Invalid relational operator : <=").
- **`new CelestialBody(-82)`, and orientations in several frames written to a CK, ended the process** ([#560](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/560), the comment on [#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454) of 2026-11-02).
  The native body information reads the centre of motion of any identifier SPICE names, and threw a C++ exception that
  nothing caught for one that is not a celestial body, a spacecraft such as CASSINI (-82, a name built into SPICE) or an
  instrument; the native CK writer threw one for records in more than one frame. Both ended the process (exit code 134)
  with no exception to catch. `SpiceAPI.GetCelestialBodyInfo` now throws `ArgumentException` for an identifier that is
  not a celestial body (a barycenter 0 to 9, the Sun 10, a planet, a moon or a Lagrange point 101 to 999, or above 1000:
  the classes of the native layer), named or not, before the native call; so do `new CelestialBody(id)`, the command
  `celestial-body-info`, the observer of `SpiceAPI.ReadEphemeris` by identifiers (built as a celestial body, a spacecraft
  observer ended the process there too) and the ellipsoid of `SpiceAPI.FindWindowsOnOccultationConstraint` whose frame is
  read from the body. An identifier without a name, which returned an empty record (and a celestial item then threw
  `InvalidOperationException`), throws the same `ArgumentException` when it is not a celestial body (Changed below).
  `SpiceAPI.WriteOrientation` throws `ArgumentException` when a record is not in the frame of the first, and writes
  nothing. `Spacecraft.WriteOrientation` writes one frame, J2000, and is unchanged.
  - **Test:** `NativeAbortGuardTests` (19 cases): -82, -999, -1, 11, 50, 100 and 1000 throw from `GetCelestialBodyInfo`
    and `new CelestialBody`, the barycenters, the Sun, the Moon, the Earth and Mars are read; `ReadEphemeris` from -82,
    an occultation with -82 as an ellipsoid front or target, and ten records with one in ECLIPJ2000 (first, middle,
    last) throw; `BodyInformationsTests.SpacecraftIdentifier_IsAnArgumentError` on the command. With the `SpiceAPI` of
    3eb1edc0 the test host crashes on the first of them ("Invalid Naif Id : -82", "different frames : J2000 -
    ECLIPJ2000").
- **A site occultation of a `Star` with `LT` or `CN` read the star one light time back, and threw beyond about 54 pc** ([#533](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/533)).
  The managed occultation search read a star, and the reference body of the observer, at t − lt, the star's light time
  (65.2 years at 20 pc, 326 years at 100 pc), and `Star.GetEphemeris` did the same through its light-time correction:
  the proper motion was applied over the light time (a lunar occultation moved by −9.641 s and −9.074 s, the direction
  by 6.2″, for 0.1″/yr at 20 pc), and from a site the Earth was read before the start of de440s beyond about 54 pc (after
  its end with `XLT` and `XCN` beyond about 38 pc). In 10.2.0-preview-1 the two failed reads cancelled; between the
  throwing reads of #454 and this fix, never released, the search threw `InvalidOperationException` (SPKINSUFFDATA, the
  Earth in 1699 for a star at 100 pc). A star is now a direction, read at the epoch whatever the correction, in the occultation search
  as the field-of-view search reads it since #517, and in `Star.GetEphemeris`: `LT`, `CN`, `XLT` and `XCN` read as
  `None`, their `+S` forms as the stellar aberration alone (`stelab`, `stlabx`; Changed below). A catalog position at an epoch is the
  direction of the light that reaches the barycenter then, as SOFA's `iauPmsafe` reads it (confirmed by S.
  Guillet, 2026-11-11). The other bodies of a search are read as before, and a star without proper motion within the measurements
  below of its former windows.
  - **Measured** (Linux x64, Release, 2026-11-11) against the closed form of
    `tools/occultation_reference/star_occultation_reference.py` (spiceypy: the star at its catalog position at t minus
    DSS-13 at t, the Moon from `spkpos` with the correction, a sphere of 1737.4 km, bisection to 1e-9 s), a star on the
    Moon's LT direction from DSS-13 at 2026-03-01 12:00 TDB, search ± 3 h, step 60 s: before, 20 pc with 0.1″/yr and
    `LT` or `CN` −9.641 s / −9.074 s, with `XLT` or `XCN` +9.620 s / +9.105 s, 100 pc with `LT` or `CN` an exception
    (the Earth in 1699), with `XLT` or `XCN` an exception (the Earth in 2352); after, every edge of the 20 cases (20 and
    100 pc, 0 and 0.1″/yr, `None`, `LT`, `CN`, `XLT`, `XCN`) within 2.38 µs (1.43 µs without `XLT` and `XCN`).
    `Star.GetEphemeris` at S + 1 h: before 3.0e-5 rad (20 pc) and 1.5e-4 rad (100 pc) from the reference with `LT`,
    `CN`, `XLT`, `LTS` and `CNS`; after within 2.2e-16 rad (atan2 of the cross and dot products; 1.7e-16 rad on Linux and Windows), `stelab` for `+S`.
  - **Test:** `StarOccultationTests` (30 cases), the edges within 4.8e-6 s and the
    directions within 4.4e-16 rad (twice the worst over Linux x64, Windows x64 and macOS arm64).
- **TEME was not the TEME of SGP4** ([#451](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/451)). `Frame.TEME` was ITRF93 of the Earth's binary PCK composed with
  R3(GAST06) of UTC taken as UT1: a frame close to the true equator and equinox of date, with the pole of ITRF93 and a
  spurious rotation rate (1.39e-10 rad/s), 0.3″ to 23.3″ from the definition over 2000–2026 (797 m and 812 mm/s on a
  LEO state at 2012-02-17, 4.75 km at the geostationary radius), and the zero rotation outside the PCK.
  - **The fix.** `Frame.TEME` is the managed `TemeFrame`, Vallado et al. (2006), appendix C: TEME → ICRF =
    Q(t)ᵀ · Rz(ERA − GMST82), the IAU 2006/2000B chain of `Frame.CIRS` and `Frame.TIRS`, GMST82 of SOFA `iauGmst82`,
    UT1 = UTC (UT1 − UTC cancels to 0.2 mm at GEO; `new TemeFrame(eop)` takes a provider), quasi-inertial (v' = R v,
    as Vallado's `teme2eci` and GMAT). It reads no kernel: TEME ↔ ICRF works at any epoch. The native TEME of the frozen
    C++ layer is no longer used.
  - **Measured** against `tools/teme_reference` (a LEO and a GEO state at 13 epochs, 1980 to 2026): 0.9 mm (LEO) and
    18 mm (GEO) from ERFA's CIP route with IAU 2006/2000B; 19 mm and 106 mm from IAU 2006/2000A (0.55 mas, IAU 2000B's
    accuracy); 9.4 cm and 56 cm from GMAT R2026a's TEME → EarthICRF, the kinematic terms of the 1994 equation of the
    equinoxes that GMAT applies from 1997-02-27, and 0.094 mm/s. Five TLEs at their epoch (1994 to 2026, LEO, GTO, GEO),
    from Vallado's code (`sgp4` 2.27, WGS-72 old, AFSPC mode) through GMAT: 1.0 cm to 0.37 m (TDRS 6, GEO).
- **A frame named TEME took the native frame** (the routing of [#562](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/562)). `SpiceDataProvider` and `MemoryDataProvider`
  route a frame named TEME, in any case (`new Frame("TEME")` from a CCSDS file, the CLI or user code), to `Frame.TEME`,
  and `SpiceAPI.TransformFrame` composes a TEME end through ICRF; the TLE and `SpiceAPI.ConvertTleToStateVector` label
  their states with `Frame.TEME`. The equality of a frame built by name with `Frame.TEME` is #562's (Changed below).
- **An ephemeris read in a managed frame handed its name to `spkezr_c`** ([#558](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/558), the ephemeris reads). A read in
  `Frame.TEME` (or a frame named TEME), `Frame.CIRS` or `Frame.TIRS`, through `GetEphemeris` or the three
  `SpiceAPI.ReadEphemeris`, is read in J2000 and rotated by the frame at the epoch of the read: the numbers of
  `ToFrame` on the ICRF read, to 1e-6 m. CIRS and TIRS returned SPICE(UNKNOWNFRAME) and a state of |r| = 0; TEME the
  native frame, up to 23″ from `ToFrame`. A coordinate search in a managed frame with a kernel spacecraft taking part
  is the library's since the third review of #690.
- **A failed SGP4 returned a zero state; TEME outside the PCK a zero rotation** ([#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454), the TLE and TEME paths).
  `SpiceAPI.ConvertTleToStateVector`, under `TLE.ToOsculating` and `ToStateVector`, throws `InvalidOperationException`
  naming the TLE and the epoch when the native layer returns r = 0 (a decayed or suborbital orbit, an eccentricity out
  of range); no valid state has r = 0. The native `TLE::ToStateVector` left its state unwritten when `evsgp4_c`
  signalled, so the state returned was what the stack held: on Linux zero by chance (28872 of Vallado's set gave
  r = (0, 0, 0), v = (0, 6.95e-307, 6.95e-307)), on Windows x64 and macOS arm64 a non-zero state, possibly that of an
  earlier call. It now returns a zero state then, a change of the C++ layer (decision of S. Guillet, 2026-11-08; the
  other change of 11.0.0 is the ephemeris read of #454, above). TEME ↔ ICRF reads no kernel;
  `SpiceAPI.TransformFrame` between TEME and a SPICE frame throws when SPICE gives no rotation for that frame (ITRF93
  before 2000); every frame since (#454's ephemeris read and frame guard, above).
- **A TLE built its Earth in ITRF93 at its epoch** ([#597](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/597)). `new TLE(...)` (twice) and `Omm.ToTle` built
  `new CelestialBody(399, new Frame("ITRF93"), epoch)`: outside the Earth's binary PCK (every TLE before 2000 or after
  the PCK's end) printed two SPICE(FRAMEDATANOTFOUND) messages, without an exception, and left the observer's state
  read in a frame without data. The Earth is built in `Frame.ECLIPTIC_J2000` at the epoch, as `ConvertTleToStateVector`
  builds it; SGP4 does not read it, so no state moves.
- **SGP4 counted tsince in TDB seconds from an epoch the library converted itself** ([#599](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/599)). `TLE.ToOsculating(t)` (and
  `ToStateVector(t)`, `AtEpoch`, `TLEPropagator`, the TLE participants of scenarios and conjunctions) called
  `evsgp4_c` at the TDB of t, so that tsince was the TDB seconds between the library's TDB of t and CSPICE's TDB of
  the epoch: the drift of TDB − TT over the interval and the difference of the two TDB models (up to 1.07 µs at the
  epoch). tsince is now the TT seconds from the TLE epoch (UTC, as the line gives it) to t, the UTC days of Vallado's
  driver away from a leap second, whatever the time frame of t: the call is at the epoch `getelm_c` reads (read once
  per TLE from the native layer) plus those seconds. SGP4 itself is unchanged: CSPICE's `evsgp4_c`, Vallado's 2006
  code in the AFSPC mode with WGS-72 old (KE = 7.43669161e-2).
  - **What moves.** Every SGP4 state at a given instant, by up to 0.36 m after a day in LEO (0.11 m for 06251 of
    Vallado's set, 0.21 m for the ISS of 2026-11-07, 0.03 m in GEO), and by the epoch term at the epoch (up to 9.3 mm,
    1 µs); fits (`ToTLE`) by the same terms.
  - **Measured** against `tools/sgp4_reference` (Vallado's code, `sgp4` 2.27, WGS-72 old, AFSPC mode): the 29 element
    sets of Vallado's verification set that SGP4 evaluates, at the 577 tsince of `tcppver.out`, within 7.6e-5 m and
    1.3e-8 m/s per component, CSPICE's own difference from the reference; 06251 and the ISS hourly over a day at UTC
    instants within 0.36 mm, the double of the epoch passed to `evsgp4_c`. `SpiceAPI.ConvertTleToStateVector` counts
    tsince the same way.
- **The TLE epoch was read through a `double`, truncated to the tick** ([#603](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/603)). The day and its fraction are read
  in decimal and the fraction rounded to the 100 ns tick: the 8 decimals of a day are whole multiples of 864 µs, so the
  epoch is exact. 43 % of TLE epochs were read 100 ns early (CALSPHERE 1, `26311.28305435`: 06:47:35.8958399 instead
  of 06:47:35.8958400), and their OMM `EPOCH` text 1 µs early.
- **`TLE.Create` wrote fields its columns could not hold, or not the nearest value** ([#600](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/600), [#601](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/601),
  [#602](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/602), [#480](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/480)). Each field is now the nearest value of its columns, as the format and
  `tools/sgp4_reference` (exact decimal) give it. What moves, text only:
  - the epoch is rounded to the nearest 1e-8 day and carried into the next day and year: an epoch in the last 432 µs
    of a UTC day was written one day early (2026-11-07T23:59:59.9996 UTC as `26311.00000000`, now `26312.00000000`;
    2026-12-31T23:59:59.9996 as `27001.00000000`) (#600);
  - B* and n̈/6 below 1e-10 took a two-digit exponent, a 70-character line that threw `ArgumentException`: they are
    written as the nearest value of a five-digit mantissa and a one-digit exponent, ` 00000-0` below 5e-11 and
    ` 10000-9` from 5e-11 to 1e-10; from 0.999995e9 the field cannot hold the value and `ArgumentOutOfRangeException`
    is thrown (#601);
  - e was clamped to at least 1e-7, so e = 0 was written `0000001`: it is clamped to [0, 0.9999999] (#602);
  - an angle of −0.0 was written `-0.0000`, and ṅ/2 in (−5e-9, 0] `- .00000000`, a 70-character line that threw: a
    value that rounds to zero is written without a sign (#480).
- **A non-UTC epoch was written as UTC** ([#479](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/479)). `TLE.Create` (and `ToTLE`, `Omm.ToTle`),
  `OpmStateVector.FromStateVector` (under `Spacecraft.ToOpm` and `Opm.CreateFromStateVector`) and
  `ImpulseManeuver.ToOpmManeuverParameters` wrote the calendar of the epoch's own time scale under a UTC epoch field
  or `TIME_SYSTEM = UTC`: a TDB epoch 69.19 s late (530 km in LEO), TT 69.184 s, TAI 37 s, and `MAN_EPOCH_IGNITION`
  always, its thrust window being TDB. They write the UTC of the instant: 2021-03-20 12:00 TDB gives `21079.49919924`,
  CSPICE's UTC 11:58:50.8143932, where it gave `21079.50000000`. UTC epochs are written as before.
- **The OPM and OMM readers took `EPOCH` as UTC whatever `TIME_SYSTEM` said** ([#485](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/485)). `Opm.ToStateVector`
  (and `ToSpacecraft`) read the epoch in the time system of the metadata, UTC, TAI, TDB, TT or GPS (in any case), and
  `Omm.ToTle` writes the UTC of its epoch so read: a TDB file was read 69.19 s late. Another time system (UT1, TCB,
  MET, ...) throws `InvalidOperationException` naming it; loading and validation are unchanged.
  `OpmStateVector.ToStateVector`, which carries no time system, still reads UTC. UTC files are read as before, bit for
  bit.
- **`ToTLE` threw `InvalidCastException` for elements already in TEME** ([#464](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/464)). A `TLE`, or Keplerian or
  equinoctial elements in `Frame.TEME`, are fitted as their state in TEME; mean elements throw the
  `InvalidOperationException` of `ToStateVector` that sends to SGP4. A `StateVector` fits as before, bit for bit.
- **The TLE documentation named ṅ and n̈ and no SGP4 model** ([#605](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/605)). `FirstDerivationMeanMotion` and
  `SecondDerivativeMeanMotion`, and the `nDot` and `nDDot` of `TLE.Create`, hold ṅ/2 and n̈/6, the TLE fields and OMM's
  `MEAN_MOTION_DOT` and `MEAN_MOTION_DDOT`. The XML documentation of `TLE`, `ToOsculating` and
  `SpiceAPI.ConvertTleToStateVector`, [TLE](../reference/tle.md) and the [TLE & OMM tutorial](../tutorials/tle-and-omm.md)
  state the model: CSPICE `evsgp4_c`, Vallado's 2006 code, AFSPC mode, WGS-72 old (KE = 7.43669161e-2), no other mode
  or constants, tsince in TT seconds, errors thrown.

- **The managed window search returned its edges up to 1 s early, could start before the search window, and had no
  extremes and no equality** ([#590](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/590), [#461](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/461), [#486](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/486), [#487](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/487)). `GeometryFinder` is
  the search of `FindWindowsOnDistanceConstraint`, `FindWindowsOnCoordinateConstraint` and
  `FindWindowsOnIlluminationConstraint` of `CelestialItem` and `Site`, and of `Site.FindDayWindows`, whenever CSPICE
  cannot search (a propagated spacecraft, a site built from its coordinates, two natural bodies).
  - **The edges ([#590](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/590)).** Each transition was bisected to a bracket of 1 s or less (0.9375 s at a 60 s step)
    and its failing end returned: every interior edge came 0 to 0.94 s early, every window start and end alike (on a
    LEO pass from a station, the rise and the set 0.4 to 587 ms early; on a LEO day, all 89 apsides, nodes and altitude
    crossings early, by up to 0.898 s). Each transition is now located to 1 µs, CSPICE's convergence tolerance, by
    Brent's root finder on the quantity minus the value (`Greater`, `Lower`, `Equal`), by a bisection of the relation
    (`GreaterOrEqual`, `LowerOrEqual`, a boolean quantity), and the root is returned.
  - **The bounds ([#461](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/461)).** The first transition was searched over [start − step, start], so a window that held
    at the start began before it, one step early at most, and the quantity was evaluated before the search window (on a
    propagated spacecraft, outside its stored states, #527); the last edge stopped up to 1 s short of the end. Nothing
    is evaluated outside the search window now; a window that holds at its start starts there, one that holds at its end
    ends there. The windows are closed, as CSPICE's: two windows that one edge closes and opens are one.
  - **The extremes ([#486](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/486)).** `LocalMin`, `LocalMax`, `AbsoluteMin` and `AbsoluteMax` threw
    `ArgumentException("Invalid operator")`. The local extremes are now the sign changes of the rate of the quantity
    (the derivative of the quartics through five values a sixteenth and a thirty-second of the step apart, combined by
    Richardson's extrapolation), each located to 1 µs, inside the search window, as zero-length windows; the absolute
    extremes the lowest or highest of the local extremes and of the bounds, as CSPICE's `gfdist`, and, with a positive
    `adjustValue`, the windows within it of the extreme. `adjustValue`, which the managed path ignored, is now taken by
    the coordinate and illumination searches; the distance searches take none.
  - **Equality ([#487](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/487)).** `Equal` tested |x − v| < 1e-9 at the samples and returned no window (0 for the 30
    nodes of a LEO day). It now gives the crossings of the value as zero-length windows, a jump at a branch cut (a right
    ascension, a longitude, an azimuth) being no crossing, and `NotEqual` the rest of the search window, a window from
    each crossing to the next.
  - **Measured** against CSPICE's `gfdist` and `gfposc` converged to 1e-9 s (`tools/geometry_reference`, Linux x64,
    2026-11-06): 2.4e-7 s on the edges and crossings (the Earth-Moon distance, the Moon from DSS-13 and DSS-13 from the
    Moon, the Sun from a site), 4.8e-7 s on the Earth-Moon distance extremes at a 1-day step, 4.5e-9 s on the Moon's
    elevation extremes from DSS-13 at a 60 s step.
  - **Changes published results:** every managed window edge moves by up to 1 s toward its root (by up to a step for
    a window that held at the start of the search), and a window that ended up to 1 s before the end of the search now
    ends at it. The searches CSPICE runs (a kernel spacecraft with a kernel body or site) are unchanged.
- **The managed threshold searches lost the windows, gaps and crossings shorter than the step**
  ([#678](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/678)). `GeometryFinder` decided each
  transition from the sign of the relation at the samples alone: a window, a gap or a pair of crossings between two
  samples that left no trace on them was lost, and how many windows a search returned depended on where the samples
  fell, not decreasing monotonically as the step grew. A LEO (a = 6800 km, i = 51.6°) above 30° from DSS-13 gave 1, 0
  and 0 of its 2 passes (3 min each) at steps of 5, 10 and 20 min, and 2, 0 and 0 of its 3 windows below 1000 km, all of
  which CSPICE's `gfposc` and `gfdist` find at every one of these steps; sin(2π t / 5400 s) > 0.995 gave none of its 16
  windows a day at 3, 5, 10 and 20 min and 2 at 22 min, but 8 at 4 min and all 16 at 7.5 and 11.25 min; a site at
  DSS-13's coordinates gave 1 and 0 of the 3 windows of the Sun's incidence below 58.83° at 5.5 and 6 h. The search now
  looks for the extremes of the margin of the relation that the samples hide, by the rule of the occultation search (the
  internal `HiddenExtremes`, moved out of `OccultationFinder` and shared: Brent's `localmin` between the two neighbors
  of each three-sample extreme that does not reach zero, and over the first and last intervals), and adds each extreme
  on the other side of zero as a sample before the transitions are located to 1 µs; each sample is evaluated once. A
  zero margin fails for `Greater`, `Lower` and the `adjustValue` windows of `AbsoluteMin` and `AbsoluteMax`, and holds
  for `GreaterOrEqual`, `LowerOrEqual`, `Equal` and `NotEqual`; a bracket with an undefined (NaN) sample is not
  searched. Every window, gap and crossing of a `double` quantity is therefore found when the step is at most half the
  time between two extremes of the quantity, a jump of a branch cut counting as one, down to the resolution of
  `localmin`, about 2 √ε h + 2τ/3 for a step h and τ = 1 µs (2.5 µs at a 1 min step, 19 µs at 10 min, 41 µs at 22.5 min;
  "about 1 µs" until #668, which longer steps do not reach); a tangency between two samples (an extreme exactly at the
  value) is not found. The
  documentation asked for a step shorter than every window and gap, "as with CSPICE": CSPICE asks for a step shorter
  than the shortest interval where the quantity is monotone
  ([`gfdist_c`](https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/gfdist_c.html), Particulars, "The Search
  Process" and "Step Size"; the same in `gfposc_c` and `gfilum_c`), whatever the length of the windows.
  - Measured** (Linux x64, 2026-11-14): the 16 closed-form windows at 3, 10 and 22 min for `Greater`, `GreaterOrEqual`,
    `Lower` and `LowerOrEqual`, the 32 crossings of `Equal` and the 33 windows of `NotEqual` at 10 min, the 16 gaps of
    `Greater` and of `AbsoluteMin` with an adjustment, and the 16 windows of a sawtooth, each ending at its jump, at 7
    to 20 min, within 8.0e-7 s of the closed forms; the LEO's passes and range windows at 5, 10 and 20 min within 2.4e-7
    s of `gfposc` and `gfdist` converged to 1e-9 s (`tools/geometry_reference`), 3.6e-7 s on the same orbit propagated
    with RK7(8), and the count with Velocity-Verlet; the same passes and range windows of a site built from DSS-13's
    coordinates (`Site.FindWindowsOnCoordinateConstraint` and `FindWindowsOnDistanceConstraint`, 1, 0 and 0 of the 2
    passes before) within 3.6e-7 s of `gfposc` and `gfdist` on the fixed point of these coordinates; the 3 incidence
    windows within 2.4e-7 s of `gfilum`. Over 425 searches of a day (the elevation, range and azimuth of the LEO from
    DSS-13 and from a site from coordinates, a sinusoid, the Earth-Moon distance; six relations; steps of 30 s to 10
    min), each search returns the same windows at every step, and the 8618 edges found before are unchanged bit for bit;
    the conformance cases `ground_station`, `tracking_data` and `orbital_events` are unchanged bit for bit. Cost: 1735
    evaluations of the quantity on the LEO day at a 1 min step, 1463 before (1.19 times); the same day is complete at 10
    min with 481.
  - Changes published results:** windows are added where the samples hid them, and a window across a gap they hid is
    split there; no edge found before moves. The searches CSPICE runs, the occultation and field-of-view searches (bit
    for bit), `LocalMin`, `LocalMax`, the absolute extremes without adjustment and a boolean quantity are unchanged.
- **The site-visibility example of the tutorial found the times when the spacecraft is 10° below the horizon**
  ([#591](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/591)). `docs/tutorials/geometry-and-visibility.md` called `site.FindWindowsOnCoordinateConstraint` with the
  spacecraft as observer, which searches the site seen from the spacecraft (its declination is minus the elevation): 9
  windows, 78 284 s of a day, against 6 passes of 2 312 s. The example now searches the spacecraft from the site
  (`spacecraft.FindWindowsOnCoordinateConstraint(window, site, site.Frame, …)`), and `docs/reference/sites.md` lists
  the call. `docs/reference/event-detection.md` no longer says that the propagation locates "other state transitions":
  it detects the maneuvers' events only, and points to the window searches for the others.
- **On the reference-body path, the light-time and stellar-aberration corrections were not CSPICE's**
  ([#491](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/491)). The path computes the state
  when CSPICE cannot read the pair: a site as observer (from the kernels or from its coordinates), a spacecraft not
  from a kernel as observer or as target, a star; through it `Site.GetHorizontalCoordinates`, the angular separations and
  illumination angles seen from a site, and the managed window searches with a correction.
  - **The cause.** `CN`, `CN+S`, `XCN` and `XCN+S` were not applied (the geometric state was returned); the `LT` and
    `XLT` velocities missed the rate of the light time; `LT+S` and `XLT+S` returned the geometric position plus
    v_obs·|v_obs|/c, with the `LT` and `XLT` velocity.
  - **The fix.** CSPICE's `spkaps` (N0067, read from its source): the light time of `spkltc`, one iteration for `LT` and
    `XLT`, up to five until it changes by less than 1e-17 of the epoch for `CN` and `XCN`, the target read at t ∓ lt
    of the epoch as CSPICE forms it in double, the velocity (1 ∓ dlt/dt)·v_target − v_observer; for the `+S` flags the
    stellar aberration of `stelab` (`stlabx` for transmission) of the light-time position, and its rate (`zzstelab`)
    from the light-time velocity and the observer's acceleration, the central difference of its velocity over ±1 s
    (`spkacs`). `None` is unchanged, bit for bit.
  - **Measured** against `spkezr`, `spkcpo`, `spkcpt` and `azlcpo` (`tools/light_time_reference`, Linux x64,
    2026-11-06), the nine flags at three epochs: the Moon from DSS-13, the Moon and the Sun from a site given by its
    coordinates, within 2.2e-5 m and 1.9e-9 m/s; a LEO spacecraft given by one state, from that site and from the Earth,
    and the Moon, the Sun and the site from it, the correction within 3.9e-4 m and 8.3e-7 m/s (the 100 ns of the TT
    argument of its two-body state, #508), the Sun's `+S` within 2.9 cm and 6.4e-5 m/s (the stellar aberration of
    the 1.2e-4 m/s between its two-body trajectories); the spacecraft's azimuth and elevation from the site within
    5.4e-11 rad of the correction, its range within 0.16 mm and its range rate within 7.3e-7 m/s. On the four light-time
    settings of the ground-station cases of the conformance suite (a two-body LEO from a site near Toulouse), the rises,
    sets and culminations within 0.50 ms of GMAT's `ContactLocator` (its millisecond print), the azimuth and elevation
    within 1.7e-7°, the range within 1.3 mm and the range rate within 0.014 mm/s of `azlcpo`, `LT+S` included (the
    range rate was 0.728 m/s off with `LT` and `XLT`, `LT+S` 4.1e-3° in azimuth and 211 m in range).
  - **Not in this fix:** the output frame is still evaluated at the epoch of the observation; `spkezr` evaluates a
    non-inertial frame centred on another body at t − lt of its centre (the Moon from DSS-13 with `LT`, 531 to 620 m in
    `ITRF93`; [#593](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/593), split from #491).
    The topocentric frame of the observing site and the inertial frames are not affected.
  - **Changes published results:** on that path, the `LT` and `XLT` velocities by up to 0.77 m/s (a LEO spacecraft),
    the `CN` and `XCN` states by up to 42 km (the Moon from a site), the `+S` states by up to 14 775 km (the Sun from a
    site, 20.7″) and their velocities by up to 3.5 km/s (the Sun from a LEO spacecraft); the managed searches with a
    correction move with them (the sunrise of `SiteTests` with `LT+S`, 1.13 s, now within 2.4e-7 s of `gfposc`).

- **A geopotential of degree 0 read the orientation of its body, and failed where no orientation data covers the
  epoch** ([#594](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/594)). A field without harmonics (degree 0 or 1; the models start at degree 2) rotated the position to the
  body-fixed frame and the acceleration back, which a point mass does not need: beyond the coverage of the Earth's binary
  PCK, SPICE reported `FRAMEDATANOTFOUND` and the propagation failed with NaN steps ("100 consecutive rejections").
  `GeopotentialGravitationalField` now gives such a field the point mass of the GM of its central term in the frame of
  the state, its acceleration and its partials alike, without the orientation. The runner of the conformance suite
  takes a case's GM as the central term of a field of degree 0 (decision 19 of #457), and the two-body cases of lot 4
  after 2026-05-11 (`eclipse` 006, `attitude` 003) failed with the runner's Earth orientation file. A field of degree 0
  moves by the rounding of the rotation it no longer makes (8.2e-16 of the acceleration at most, measured on the
  twelve cases of `GeopotentialCentralTermTests`); every other field is unchanged, bit for bit.
- **Conformance runner: the geometry and event categories of lot 4** (design of lot 4 of
  [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457), approved by S. Guillet,
  2026-11-06). New solvers for `ground_station`, `orbital_events` and `attitude`, and the second branch of `eclipse`
  (every eclipse of a window by the Earth and the Moon, light time `none` or `cn`), on the library's window searches
  (#590); a shared two-body trajectory; the arrays of a golden compared element by element, their count exact, so that
  a pass or an event the library does not find FAILS; the direction angle of a pointing direction. The 22 cases of the
  suite's lot-4 branch (4cd11f9) pass with at most 0.137 of their tolerance, and the 151 cases of that branch pass. See
  [Validation](../guides/validation.md#lot-4-geometry-and-events).
- **Conformance runner: the TLE categories and TEME of lot 5** (design of lot 5 of
  [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457), approved by S. Guillet,
  2026-11-08). New solvers for `sgp4` (TEME states at tsince or at UTC instants, and their ICRF states, through
  `TLE.ToOsculating` and `Frame.TEME`) and `tle_conversion` (`TLE.ToOmm`, and `Omm.ToTle` from an OMM item in its
  `TIME_SYSTEM`), and TEME in the frames of `frame_transformation`. A case whose `gravity_model` or `opsmode` is not
  `wgs72old` and `afspc`, the only model of the library's SGP4, is an ERROR that names it. New kinds of `spec.md` §6.1:
  the scalar per component, the relative delta, the TLE epoch on the UTC calendar, and the TLE line, whose fields the
  runner decodes by the spec's column table in `decimal`, independently of the library's parser, so that a line of the
  wrong length or checksum FAILs. The 25 cases of the suite's lot-5 branch (b10b925) pass with at most 0.642 of their
  tolerance (the velocity of frame 035, ICRF → TEME in 1990), and the 176 cases of that branch pass. See [Validation](../guides/validation.md#lot-5-tles-sgp4-and-teme).
- **Conformance runner: the conjunction categories of lot 6** (design of lot 6 of
  [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457), approved by S. Guillet,
  2026-11-09). New solvers for `conjunction` (two objects propagated in RK7(8) with their covariances, or read from
  SPKs, `ConjunctionAssessment.Analyze` and `AnalyzeAll`; the covariance at TCA as the library propagates it),
  `collision_probability` (the public `Analyze` on straight-line trajectories at TCA, the Pc or Frisbee's maximum, the
  encounter-plane sigmas, the clip flag) and `cdm` (decode, to inertial axes, and export through `EncounterCase.ToCdm`
  read back); `stm` takes the `propagator` category's data files and integrator, its case and schemas now synced from
  the suite (`Fixture/Lot6`). New kind of `spec.md` §6.1: the normalized covariance; the relative kind and the scalar
  per component take arrays of any length; a cdm export compares its fields in a table of its own. A covariance the
  library did not propagate, a Pc it did not compute and a sigma it clipped are an ERROR that says so. The 25 cases of
  the suite's lot-6 branch (970a3bf) pass with at most 0.66 of their tolerance (the relative position of the written
  CDM, one 100 ns tick of the TCA; 0.30 since #624, a correlation of conjunction 006, cdm 007 at 0.019), and the 201
  cases of that branch pass. See [Validation](../guides/validation.md#lot-6-conjunctions-pc-cdm-and-the-state-transition-matrix).
- **An impulse gated by its minimum epoch fired there on a false crossing, or missed a crossing just after it**
  ([#569](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/569)).
  - **The cause.** Before `MinimumEpoch`, the g-function of a maneuver was replaced by +1, and the integrators compared
    the first step end after it with that value.
  - **Positive-to-negative maneuvers** (`PerigeeHeightManeuver`, `CombinedManeuver`) whose r·v was already negative at
    the minimum epoch fired there. On a 400 × 1 000 km orbit gated at S+1000: 120.6 m/s at 7 234 km instead of
    27.4 m/s at the apogee, and up to 608.5 m/s.
  - **Negative-to-positive maneuvers** (`ApogeeHeightManeuver`, `PhasingManeuver`, `ApsidalAlignmentManeuver`) missed a
    crossing between the minimum epoch and the end of the integration step that held it, and fired one orbit later.
  - **`PlaneAlignmentManeuver`** took the other node, half an orbit later.
  - **Chained impulses**, gated by the end of the previous window, were affected the same way: a 4 000 s hold fired the
    second burn 5 858 s early with 102.6 m/s instead of 26.4 m/s.
  - **The fix.** The integrators now evaluate the g-function at the minimum epoch, in the step that holds it, and search
    the first crossing from there, as GMAT's `Propagate` to the epoch then to the apsis does. A crossing before the
    minimum epoch never counts; a g exactly zero at it counts there when the sign that follows it is the target sign.
    RK7(8) fires within 1.5e-7 s of the Keplerian epoch in these cases, 9.9e-8 s at the apsides and nodes (worsts
    1.42e-7 s and 9.86e-8 s on Linux x64, Windows and macOS arm64, the 100 ns tick of the epochs included);
    Velocity-Verlet at its step end.
  - **Unchanged, bit for bit:**
    - every step that does not hold a minimum epoch;
    - every step that holds one where the former decision holds: the g-function is positive at the minimum epoch, or a
      negative-to-positive maneuver's g stays negative to the step end, or a positive-to-negative maneuver's g turns
      positive by the step end; except `PlaneAlignmentManeuver`, whose node is now chosen from the state at the minimum
      epoch instead of the step end, and moves where a node lies between the two;
    - a true positive-to-negative crossing in that step, except a root within 1e-10 s after the minimum epoch, which
      was labelled one tick before it and is now labelled at or after it.
  - **Also moves:**
    - with Velocity-Verlet, a minimum epoch within its own lag of a crossing: the decision follows Velocity-Verlet's
      trajectory, not the Keplerian epoch. At 10 s steps the lag is up to 0.096 s at a node, 0.18 s at an apsis of
      the orbit after a Velocity-Verlet burn and 1.5 s at the intersection of an apsidal alignment (worsts 0.0953 s,
      0.176 s and 1.44 s on Linux x64, Windows and macOS arm64);
    - an apsis at the start of the propagation given by Keplerian elements (r·v exactly 0) with the minimum epoch at
      that start now fires there with RK7(8), at the first step end with Velocity-Verlet, instead of one orbit later.
  - **User code:**
    - a user `ImpulseManeuver` whose g has no recurring crossing (a time-like g) and whose root precedes its minimum
      epoch no longer fires in the window: it used to fire at the minimum epoch;
    - a custom `Integrator` (the class is public) keeps the former behaviour, the defect included: the gate is evaluated
      by `VVIntegrator` and `RK78Integrator`, and `ManeuverEventDetector.Evaluate`, public, still returns +1 before the
      minimum epoch.
  - **The cost:** one shortened RK7(8) step per segment with a minimum epoch inside it, in whichever step it lies; none
    in Velocity-Verlet.
  - No API change. See [Impulse Maneuvers](../reference/impulse-maneuvers.md#minimum-epoch).
- **A spacecraft propagated again gated its chained maneuvers with the windows of an earlier propagation**
  ([#573](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/573)).
  - **The cause.** A propagation raises the minimum epoch of each chained maneuver to the end of the window of the
    maneuver before it. `Spacecraft.Propagate` starts the chain again, but kept those raises, so a later propagation
    from another state or over another window gated its maneuvers with the latest windows of every earlier run.
  - **The effect.** Chained maneuvers fired late, by whole orbits. On a 400 × 1 000 km orbit with two
    `ApogeeHeightManeuver`s (a hold of 5 000 s), propagated from the apogee and then again from the perigee, the second
    burn fired one orbit (6 565.30 s) after that of a spacecraft never propagated, 2.4e6 m away after 6 hours, with
    RK7(8) and Velocity-Verlet alike. An attitude between two impulses, `Scenario.SimulateAsync` run again,
    `BatchPropagator` and the covariance propagation were affected the same way, and so were `ExecutedManeuvers`,
    `ToOpm` and the scenario summary.
  - **The fix.** The gate a propagation raises belongs to that propagation: the next `Spacecraft.Propagate` (and
    `Scenario.SimulateAsync` and `BatchPropagator` through it) clears it before it runs. A spacecraft propagated again
    fires its maneuvers as a spacecraft never propagated, bit for bit.
  - **`MinimumEpoch`** reports the later of the minimum epoch given to the maneuver (its constructor, or
    `Spacecraft.SetStandbyManeuver(maneuver, minimumEpoch)`) and the gate of the last propagation. After one
    propagation it is the value it was; after a later one, that propagation's gate.
  - **`SetStandbyManeuver(maneuver, minimumEpoch)`:** a later minimum epoch given there is kept by every later
    propagation, as before.
  - **Unchanged, bit for bit:** the first propagation of a spacecraft, and a propagation repeated with the same
    state and window.
  - **Not covered:** a spacecraft propagated twice through `CentralBodyPropagatorBuilder` or a `CentralBodyPropagator`
    built directly, which does not start the chain again ([#583](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/583), under
    **Known limitations**).
  - No API change: the setter of `MinimumEpoch` was internal. See
    [Impulse Maneuvers](../reference/impulse-maneuvers.md#minimum-epoch).
- **The attitude of a propagation follows its maneuver chain: the attitude timeline**
  ([#539](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/539),
  [#570](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/570)).
  - **The cause.** The leading attitudes all executed on the initial state, the last one sampled at every output; an
    attitude chained after an impulse was executed by `ManeuverEventDetector.HandleEvent` on the state of the event,
    recorded once and held inertially, the leading attitude taking over again from the next output; a hold delayed
    nothing; the impulse's attitude applied from its event; a sentinel (`Quaternion.Zero`, the identity) dropped an
    attitude that was exactly the identity; an attitude chained past the end of the window was recorded.
  - **The effect**, measured before this change (240b000a) on #569's 400 × 1 000 km orbit (an apogee raise of
    233.8 m/s at the perigee, dt = 15.3 s, RK7(8), 10 s outputs) against the rules below: after impulse → LVLH the
    attitude up to 3.14 rad from LVLH (1.83 rad at the samples), the LVLH 5.10 s early; after nadir → impulse → LVLH
    (3 600 s), the nadir back from the next output, 3.14 rad from LVLH; the front 1.56 rad from ΔV over the thrust window
    before the burn; a nadir held 600 s before a prograde never in force (1.74 rad); a prograde whose minimum epoch is
    S + 300 in force from S, and one chained after an impulse with S + 4 000 activated 1 040 s early; a hold of 3 600 s
    before an impulse ignored, the burn 5 926 s before the next perigee after the hold; a leading nadir equal to the
    identity at the start not recorded (the CK unreadable by `pxform` before the first output); an attitude chained past
    the end of the window recorded; the first impulse after leading attitudes missing from `ExecutedManeuvers` (the first
    leading attitude recorded in its place, the further ones, and a leading attitude with nothing executed after it,
    lost; nadir → impulse → LVLH: indices {0, 2}), from `ToOpm` and from the scenario summary (#570).
  - **The fix.** A propagation lays its chain on an attitude timeline: an attitude activates at the later of its
    `MinimumEpoch` and the end of the previous maneuver's window, and its law is in force to the next activation,
    sampled on the solution at its activation, at each output of its interval and 1 µs before the change that ends it;
    the impulse's attitude is in force over [tc − 0.666 dt, end of its maneuver window], its start no earlier than the
    start of the propagation or the previous activation; after an impulse the law in force before it resumes, unless an
    attitude chained after it activates there; an activation after the end is not executed; an activation at a minimum
    epoch given in another frame is labelled TDB, as every epoch of the propagation; no sentinel. Every executed
    maneuver, attitudes included, is in `ExecutedManeuvers`, once, in the order of the chain. Against the rules written
    in the tests (the burn durations by the rocket equation, the TT seconds by CSPICE's `unitim_c`) the activations and
    windows within one tick (1e-7 s), and against the closed forms of the laws on the solution: 7.1e-16 rad at the
    samples, 4.1e-5 rad between them at 10 s outputs (1.5e-3 rad at 60 s; on the kernels of `AttitudeRecordTests` the
    TRIAD after an impulse, now a sampled law, 5.9e-5 rad, the worst at 10 s outputs, against 5.7e-5 rad for the
    instrument TRIAD in the entry of #537), 2.7e-16 rad for the discrete attitudes;
    `pxform` on the CK the library writes within 8.8e-10 rad of the managed attitude (the same on Linux x64, Windows and
    macOS arm64).
- **The impulse after an attitude chained after an impulse fired inside the first impulse's window**
  ([#554](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/554)). The attitude, executed at
  the first impulse's event, set the gate of the next impulse from that event, inside the first impulse's window (590 s inside its hold for a
  hold of 600 s): on 240b000a RK7(8) fired the second apogee raise at the first one's event, S + 2 963.1895589 s to the
  printed digits, 7 230 s before the perigee it is now gated to. It now fires at
  the first perigee after the end of the first impulse's maneuver window plus the attitude's hold: S + 9 528.4908671 s,
  within 1.6e-8 s of the Keplerian closed form, with a hold of 0 s or 600 s on the first; Velocity-Verlet at its step end
  after it (the test of #577 that gives the same second burn for every hold of the first impulse, 60 to 600 s in
  RK7(8), now holds for a hold of 0 s too, as in Velocity-Verlet).
- **About the Earth the holds counted TDB seconds** ([#547](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/547)).
  The hold of an attitude and the windows of an impulse were TDB arithmetic on the labels, while the integration and
  the outputs about the Earth count TT seconds (#501): an attitude after a held one fell 1e-7 to 2e-7 s from an output
  sample, a hold of 1 000 s was 999.99999982 TT seconds, and the CK of such a chain could not be written
  (`SPICE(TIMESOUTOFORDER)` before #537). About the Earth the windows of an impulse now count TT seconds from the time
  argument of its event, and the hold of an attitude TT seconds from its activation; an activation at the end of a
  hold that ends on an output is that output's epoch. About any other body, SSB mode included, TDB arithmetic, bit for
  bit.
- **The native errors of the proxies that return `bool` are raised on Windows** ([#564](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/564)).
  `LoadKernelsProxy`, `UnloadKernelsProxy`, `ReadOrientationProxy`, `ReadEphemerisProxy`, `WriteEphemerisProxy` and
  `WriteOrientationProxy` return a one-byte C++ `bool`, which .NET read as a four-byte Win32 `BOOL`. On Windows x64 a
  native `false` could come back `true`, so a failed read, write or kernel load returned as a success, without its
  SPICE error: a CK read outside its coverage gave an empty sequence instead of "No orientation found". The returns
  are now marshalled as one byte. Linux and macOS behaved as intended and are unchanged.
- **A propagation solution is read at an epoch in any time frame.** A UTC, TAI, TT or GPS epoch read on a solution
  about any central body was taken as the TAI difference from its TDB base, another instant by the change of TDB − TT
  (0.22 m after a day and 6.4 m after 30 days in LEO), and the UTC start or end of a UTC window could compare a tick
  outside the solution through TAI, where `GetStateTransitionMatrix`, `GetStateVector` and the other getters threw
  `ArgumentOutOfRangeException` (at the start, at 2021-06-02T14:51:26.1940007 UTC for instance). About the Earth the TT
  time argument now reads the TT seconds of the epoch; about any other body the epoch is converted to TDB, the frame of
  the labels, first (2.1 cm about the Moon in the tests). `InterpolateAt`, `GetStateVector`,
  `GetStateTransitionMatrix`, `GetCovariance`, the other getters, the replay of the variational equations and the
  coverage checks of the conjunction analysis read both bounds of the solution in the seconds of its time argument
  ([#507](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/507)).
- **The difference of two UTC times counts the leap seconds, and two-body time arguments no longer depend on the
  tags.** `Time - Time` subtracted the calendars of two UTC times: `new Window(utc1, utc2).Length` was 120 s for the two
  minutes around the leap second of 2016-12-31, and is now 121 s; it goes through TAI for UTC and local times, so a
  difference of local times now also counts the hour of a daylight-saving change. `Time + TimeSpan` is unchanged,
  calendar arithmetic: across a leap second `(t + d) - t` is now `d + 1 s` for a UTC time `t`, and
  `new Window(utcStart, length)` keeps `Length` equal to `length` while `EndDate - StartDate`, and a propagation over
  the window about the Earth, count the leap second. `AtEpoch` counted TDB seconds
  between two TDB epochs, UTC calendar seconds between two UTC epochs (7.7 km off in LEO across a leap second) and TT
  seconds otherwise; it now counts the dynamical time of the central body, TT about the Earth and TDB about any other
  body, whatever the tags, as do the time of flight of the Lambert solver and the intervals of the initial orbit
  determination ([#508](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/508)).
- **The command line reads a calendar epoch tagged UTC as UTC.** "2025-01-01T00:00:00 UTC" was read as a TDB calendar,
  69.18 s early; only the form ending in Z was UTC. Any case, with or without a space
  ([#509](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/509)).
- **A conversion to the frame of a time returns it unchanged.** `ToTDB()` of a TDB time went through TAI and back,
  one tick off at 2 of 935 041 instants of a grid (2024-05-09T12:00 TDB and 2078-02-02T18:00 TDB): the start of a
  propagation moved by a tick, and a covariance propagation from such an epoch was rejected with a message that printed
  two equal epochs. A UTC (or TAI, TT, GPS) initial epoch is now compared with the start of its window as TDB labels:
  through TAI it could be a tick off its own label (2021-03-20T00:05:05.0969684 UTC), where its covariance was rejected
  the same way and its start state went through the Keplerian round trip
  ([#510](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/510)).
- **The documentation gave 12:00 UTC for the epoch of the reference cases**, which is 12:00 TDB, 69.19 s earlier
  ([#511](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/511)).
- **RK7(8) with `IncludeInStepControl` no longer stops at random on the first steps of a segment in dense air.** At the
  start of a segment, the elements of Φ that start at 0 carry an error estimate that is the rounding noise of the drag
  Jacobian across the 13 stages, which no smaller step lowers. In dense air at a tight tolerance it could exceed the
  tolerance at every attempt down to the minimum step: at 1e-13, 22 km above the Earth with A/m = 1e-3 m²/kg (a drag of
  400 g), 1 to 13 times the tolerance. RK7(8) then stopped with "100 consecutive rejections" at its minimum step,
  depending on the last bits of the start and of the step sizes: 3 of 201 initial steps 60 s ± 100 ulp of that case on
  Linux, and on macOS arm64 at the start of #467, moved by 1e-5 m, so the same input could pass on one platform and
  stop on another; each segment after a maneuver was a new draw. At the minimum step size, a step that only the error
  of Φ and Ψ rejects, the state meeting its tolerance, is now accepted on the error of the state (decided by
  S. Guillet, 2026-10-26), the step that Hipparchus, Orekit and GMAT take, which keep Φ out of the step control. Φ of
  such a start stays within 1.3e-7 of central differences of whole propagations at 1e-14, the resolution of that
  reference, and within 4.6e-11 of Φ at 1e-14 on the steps of the state. Only a step that its retry would repeat is
  accepted so, and only with a minimum step size below 10 ms, where the guard of 10 000 steps within 100 s can stop a Φ
  that stays noisy (its message naming Y). A propagation that completed before takes the same steps, bit for bit, and
  without `IncludeInStepControl` nothing changes. In SSB mode at 1e-11 that guard now stops the propagation, where the
  100 rejections stopped it at the start. A state that misses its tolerance at the minimum step still stops on the 100
  rejections, whose message now names Y only when the state met its tolerance. No public API change.
  [#468](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/468)

- **Kepler's equation from the mean anomaly.** `EccentricAnomaly()`, `TrueAnomaly()`, `TrueLongitude()` and
  `ToStateVector()` of orbital parameters given by their mean anomaly, `StateVector.EccentricAnomaly()`, the state and
  anomalies read at another epoch (`AtEpoch`, `ToStateVector(Time)`, `TrueAnomaly(Time)`, `TrueLongitude(Time)`; a TLE's
  go through SGP4), and the start of a propagation from anything other than a state vector at its start solved Kepler's
  equation by Newton's method from E = M and stopped at a step of 1e-6, without a bound. That left about 1000 times the
  rounding error: for a periapsis at 7000 km, 7.1e-6 m at e = 0.9, 3.9e-4 m at e = 0.999 and 1.5e-5 m on a hyperbola at
  e = 1.01. The loop took up to 71447 iterations at e = 0.999999 and returned NaN for some mean anomalies at e ≥
  0.99999. The ellipse is now solved for M reduced to [−π, π] with 2π in two parts (reduced once more, exactly, beyond
  2^52 rad), and both conics by Newton's method from an upper bound of the root where the equation is convex. The loop
  stops at the rounding floor and takes at most 50 iterations (11 measured on the issue's grids). On those grids the
  eccentric anomaly is within 0.60 of the rounding floor on ellipses and 2.2 on hyperbolas (1124 before), and the same
  states are within 7.7e-8 m, 1.4e-7 m and 2.5e-8 m. A non-finite mean anomaly or eccentricity of orbital parameters
  given by M now throws `InvalidOperationException`, naming e and M, where NaN was returned: from these paths, from the
  constructors of `CentralBodyPropagator` (Velocity-Verlet propagated NaN, RK7(8) failed after 100 rejected steps), and
  from `Equals`, `==` and `!=` of two such parameters typed `OrbitalParameters`, which compare their states (they
  compared equal). With a NaN eccentricity only `EccentricAnomaly()` changes: `TrueAnomaly()` and `ToStateVector()`
  already threw `InvalidOperationException`. A hyperbola with e < 2 and |M| above max(3.0e307, 1.8e308 (e − 1)) rad
  throws too, its start overflowing (a finite H was returned; no orbit reaches such a mean anomaly). M = 0 and e = 0 are
  unchanged, bit for bit; other states move by up to the former error, so a propagation started from elements takes
  slightly different steps. The round trip of a near-circular state of #398 now returns it to 1.9e-15 of its radius
  (1.4e-15 in 10.2.0-preview-1).
  [#453](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/453)

- **Equinoctial elements of a circular or an equatorial orbit.** `EquinoctialElements` with f = g = 0 or h = k = 0
  exactly (either sign of zero) moved the satellite along its orbit:
  - by Ω for a circular orbit: 4 788 km for i = 30°, Ω = 40° at 7 000 km;
  - by ϖ for an equatorial orbit: 13 248 km for e = 0.05, ϖ = 120°;
  - by 2r for some circular equatorial orbits, such as a `KeplerianElements` with e = 0, i = 0, Ω = 0° and ω = 180°
    through `ToEquinoctial()`.

  `ToStateVector()` now computes the state directly from the elements, without angles (GMAT Mathematical
  Specification R2026a, §3.1.4). It is within 2.1e-16 of |r| of a 60-digit reference on the cases of the tests; GMAT
  R2026a agrees with that reference to 1.7e-9 m, the resolution of its output. The Keplerian angles take Ω = 0 for an
  equatorial orbit, and ω = 0 for a circular one, with ν the argument of latitude, as `StateVector` does; they are
  then reduced to [0, 2π] from the true longitude itself, so that they keep their precision for any L.

  This fixes every path through these elements:
  - `ToKeplerianElements()`, `AtEpoch` and `ToStateVector(Time)`;
  - the start of a propagation;
  - `ToTLE` of a state within 1e-10 rad of the TEME equator, which threw after 15 iterations and now fits as at
    1e-9 rad, with the same two lines (1.6 m in the case of the tests);
  - `ToTLE` of such elements given in a frame other than TEME, which fitted the wrong state without an error;
  - `TLE.Create` with mean equinoctial elements;
  - the state of a spacecraft whose initial orbit is such elements before it is propagated (`GetEphemeris`, which the
    conjunction analysis reads), and its OPM export;
  - `TrueLongitude()`, `TrueLongitude(Time)`, `MeanLongitude()` and `ToStateVector(ν)`, which maneuvers take from
    their target orbit;
  - the CLI with `--from-equinoctial` (`orbital-parameters-converter` and `propagate`).

  The same paths are fixed for elements whose argument of periapsis lost its precision, or was 0 or π, because its
  products underflow (e·tan(i/2) below the smallest normal double, 2.2e-308).

  Within the parabolic band, |e − 1| < 1e-6, the state took p = 2q, which put its periapsis up to 5e-7 of q away from
  the perigee radius (3.2 m at e = 1 − 9e-7 for q = 7 000 km). It now takes P when P is finite, and otherwise
  q (1 + e), the conic of the eccentricity with its periapsis at q, as SPICE CONICS. A near-parabolic `StateVector`
  comes back from `ToEquinoctial().ToStateVector()` within 5.6e-16 of |r| (1.75 m off at e = 1 − 5e-7 before).

  For all other equinoctial elements, the state moves by rounding only: the largest error against the 60-digit
  reference goes from 3.5e-15 to 2.6e-15 of |r| on 20 000 elements. Their angles, Keplerian elements, `AtEpoch`, the
  TLE fit of a state vector and `TLE.Create` are unchanged, bit for bit.

  A parabolic `EquinoctialElements` with no perigee radius overflowed the stack. With a finite `P` it now returns its
  state; without one, it throws `InvalidOperationException`.
  [#452](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/452)

- **The managed field-of-view search takes the target's shape and tests each field as CSPICE's `gftfov` does.**
  `Instrument.FindWindowsInFieldOfViewConstraint`, when the instrument's spacecraft or the target is not in the kernels
  (a propagated spacecraft or a spacecraft from a TLE, a kernel-backed spacecraft with a propagated target, a site given
  by its coordinates, a star), sampled `IsInFOV`, which tested the direction of the target's center: `targetShape` and
  `targetFrame` were ignored, so the Earth as an ellipsoid was a point, its edges 1 080 to 1 480 s from `gftfov`'s and,
  for a field grazing the limb, no window where `gftfov` finds one; and a circle was tested as the square of its
  half-angle, +188 s and −83 s on DSS-13 ([#515](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/515)). The other shapes carried their own defects, which this
  change replaces with the same margin:
  - an ellipse took `FieldOfView`, the IK's `FOV_REF_ANGLE`, across the reference plane and the cross angle in it, and
    tested angles instead of the plane of the field: about ±400 s on every edge
    ([#518](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/518));
  - a rectangle was tested on the ICRF components of the target's direction, never on the axes of the instrument: no
    window where `gftfov` finds two, 401 of 1 290 epochs wrong against `fovtrg` for the Earth
    ([#519](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/519));
  - the reference vector was not made perpendicular to the boresight, as `getfov` does: 193 s and 198 s on an ellipse
    whose reference vector is 63° from its boresight, against the Earth and a spacecraft target, measured with the
    half-angles of #518 corrected (−756 s and one window instead of two with them)
    ([#520](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/520));
  - the windows were located by a 1 s bisection, up to 1 s before every edge (0.2 to 0.9 s measured), and the first one was searched a step
    before the search window, starting 60 s early at a 60 s step ([#521](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/521); [#461](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/461) for this path,
    `GeometryFinder` and its other constraints unchanged).

  The managed search now computes what `gftfov` computes, without writing a kernel: a continuous margin of the target in
  the field (for a circle or an ellipse, `gftfov`'s field ellipsoid and the overlap margin of the occultation search;
  for a rectangle against an ellipsoid, the largest discriminant of the ray over the rectangle, in closed form), located
  by the root finder of the occultations to 1 µs. `targetShape` and `targetFrame` mean what `gftfov`'s `tshape` and
  `tframe` mean. Against `gftfov` converged to 1e-9 s on its own kernels (`tools/fov_reference`): 8.3e-7 s geometric,
  2.1e-6 s with the light time and `+S`, every field shape against the Earth, the Moon, the Sun, DSS-13, a spacecraft,
  the Earth-Moon barycenter and L1; a propagated spacecraft against `gftfov` on the kernels the library writes, 1.3e-6 s
  on 221 searches off the grazing and slow edges, every target kind, `None`, `LT` and `LT+S` on every field and the nine
  aberrations on a circle and a narrow field; 2.5e-4 s where the field grazes the Earth's limb (the same on Linux x64,
  Windows x64 and macOS arm64). The kernels
  the library writes for an attitude other than the identity, or for an initial state in ECLIPJ2000, do not describe
  what it reads (an inverted CK, interpolated across a discrete attitude change; ECLIPJ2000 states read as ICRF):
  `gftfov` on them differs from both paths by up to 972 s, three defects outside this change, each its own issue
  ([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529), [#534](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/534), [#535](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/535)),
  fixed below.
  The CSPICE path is unchanged. See
  [Spacecraft](../reference/spacecraft.md#field-of-view-search).

- **The attitude and the states of a propagated spacecraft, as the library reads them and as it writes them in its
  kernels.** Five defects made them disagree with each other and with what SPICE defines:
  - the CK held the inverse of the attitude, the rotation from the spacecraft frame where `ckw03_c` takes the C-matrix
    to it: 3.13 rad from nadir for a nadir attitude, which Cosmographia showed inverted, and its angular velocity was
    always 0 ([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529));
  - a spacecraft propagated from ECLIPJ2000, B1950, FK4, ECLIPB1950 or GALACTIC stored its states and its attitude in
    that frame and read them as ICRF: positions 2.76e6 m, 8.3e4 m and 1.18e7 m off, 1.86e11 m in SSB mode, the attitude
    0.409, 0.0214 and 2.12 rad off; the managed occultation of the Sun by the Earth ended at 922.37 s from GALACTIC
    instead of 2071.45 s ([#535](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/535), [#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529));
  - between two output epochs the latest attitude held, ω·Δt off: 6.42e-2 rad at a 60 s step, field-of-view edges up to
    53.8 s late ([#529](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/529));
  - the CK interpolated across a discrete attitude change, turning over the whole gap before it: 107.5° before a TRIAD
    attitude after an impulse ([#534](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/534));
  - every read of the attitude was stored and became part of it: a read before the first entry made the CK unwritable
    (`SPICE(TIMESOUTOFORDER)`), one from ECLIPJ2000 aborted the process when the CK was written, a read before the
    propagation became the attitude (1.806 rad off), and reads after it changed the CK by up to 6.53e-3 rad
    ([#523](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/523)). A propagation alone could also make its CK unwritable: an attitude chained after a held one, its hold
    counted in TDB seconds, fell 1e-7 to 2e-7 s from an output on the TT grid of #501 (since #547 the holds count TT
    seconds about the Earth, above).

  A managed spacecraft now keeps an attitude record that only its propagation fills and that a read never changes; its
  states and attitudes are stored in ICRF (the integration and `PropagationSolution` unchanged, bit for bit); the
  attitude between two samples of a law is their exact spherical linear interpolation, the law is sampled once more
  1 µs before a discrete change, and two entries within 2 µs are one epoch. `WriteOrientation` writes the C-matrix in
  J2000 with the angular velocity of the CK, the earlier attitude repeated 1 µs before each discrete change, and
  `WriteEphemeris` writes J2000. Against CSPICE on the kernels the library writes and the closed forms of the laws on
  the solution of the propagation (the worst over Linux x64, Windows x64 and macOS arm64, 2026-11-02): the attitude
  6.1e-10 rad from `pxform` on its CK outside those microseconds (1.3e-9 rad from 2025-01-19, where the spacing of the
  encoded SCLK doubles), 1.3e-15 rad from the law at its samples and 5.7e-5 rad between them at 10 s outputs (5.9e-5 rad
  since #539, the TRIAD after an impulse, above), its angular velocity 2.3e-5 rad/s from the rate of the
  law (the CK's model, which the library and the CK share to 9.9e-13 rad/s); the stored states equal to the solution
  rotated to ICRF by `pxform` at the outputs, the positions between them 1.3e-8 m from the RK7(8) solution (7.1e-4 m in
  SSB mode), in every inertial initial frame; the managed occultation of the Sun by the Earth from GALACTIC within one
  tick of its ICRF twin and 4.0e-7 s of `gfoclt` on the SPK; the 20 field-of-view dynamics variants on those kernels
  within the 2.4e-6 s of #515 (971.8 s and 232.6 s before); the partial step before an RK7(8) event 1.2e-4 rad from the
  law (7.1e-2 rad before, where the last output sample held; since #539 the change is the opening of the impulse's
  thrust window, 9.3e-5 rad over a partial step of 34.3 s); reads before, during and after a propagation leave the record and the CK identical. See
  [Spacecraft](../reference/spacecraft.md#attitude-and-kernels); the changes of behaviour are under **Changed**.
- **The states of a propagated spacecraft across an impulse** ([#540](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/540)).
  The stored states were one set of output states, read by a Lagrange polynomial of the 8 nearest and written as one
  SPK segment of degree 15: both interpolated through the jump of the velocity at each impulse. Near #540's 234 m/s
  burn the managed reads were 192 m off and the SPK 208 m (Velocity-Verlet at 10 s); RK7(8) 340 m and 326 m; up to
  16 km and 150 km on other burns (a combined maneuver within the first outputs), and the velocity 1 µs before an
  impulse 40 to 890 m/s off, nearly the whole ΔV. Every reader was affected: `GetEphemeris`,
  `GetGeometricStateRelativeTo`, the managed occultation and field-of-view searches, the geometry finders, the
  conjunctions with an `ILocalizable` participant and the avoidance burns read from the ephemeris, the SPK and every
  reader of it (a kernel-backed spacecraft, the Cosmographia export, `gfoclt`, `gftfov`). The stored states are now
  split into arcs at each impulse, the states of the integrator before and after it at its epoch, each read within its
  arc (the later one at the impulse: the state after it, as `InterpolateAt`), and written as one type-9 segment per arc.
  Against the RK7(8) solution within ±150 s of each impulse (the worst over Linux x64, Windows and macOS arm64, CI run
  41651463194): the managed reads 6e-9 to 1.7e-8 m at 1 and 10 s outputs (2.3e-8 m on a chain of four
  impulses), their error far from the impulses, 3.3e-4 m at 60 s; `spkezr` on the SPK within 1.2e-3 m of them (1.1e-3 m
  on the chain, 1.7e-3 m in SSB mode), the resolution of the ET doubles of its nodes; the velocity 1 µs before an impulse
  1.3e-11 m/s managed in RK7(8), 8.4e-7 m/s with Velocity-Verlet, and 8.0e-7 m/s by `spkezr`; in SSB mode, where the
  solution carries the noise of the third body near the Earth (#576), 4.9e-12 m/s managed and 6.3e-7 m/s by `spkezr`
  against the state before the impulse moved back by the closed-form acceleration of the Sun and the Earth, the worst over
  Linux, Windows and macOS arm64.
  An output closer to an impulse than a tenth of the output step is not stored, the impulse's state is (kept, an output
  3 µs from an impulse put the SPK 301 m off); the first and the last output always are. Velocity-Verlet keeps its own error far from the impulses (0.16 m at 10 s and 33 m at
  60 s outputs). Far from the impulses, and without an impulse, the reads are those of before, bit for bit; the
  trajectory, `PropagationSolution` and the attitude are unchanged. See
  [The stored states across an impulse](../reference/spacecraft.md#the-stored-states-across-an-impulse); the changes of
  behaviour are under **Changed**.
- **A read of the stored states sorted every state** ([#522](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/522)).
  `GetEphemeris` and `GetGeometricStateRelativeTo` of a propagated spacecraft sorted all its stored states at each call:
  1.6 to 2.0 ms a read at 7 000 to 9 000 states. The arcs above are sorted arrays read by a binary search, without a
  lock: 0.001 to 0.003 ms, against 1.6 to 2.2 ms for the sort in the same process (2 000 reads at 7 000 and 9 000
  states, Linux x64, 2026-11-03, not a benchmark), the result bit for bit that of the sort.
- **`Instrument.Orientation` has one meaning, the Euler angles of the frame kernel.** The managed path read it as a
  rotation vector and conjugated it, the spacecraft-frame getters as the same rotation vector, and the frame kernel as
  the Euler angles 1-2-3, as its documentation says: the managed boresight 4.634° from the frame kernel's for
  (0.3, 0.5, 0.2) and 53.9° for (−0.7, 1.1, 2.3), the getters 180° off for (0, π/2, 0), and a TRIAD attitude built from
  an instrument aimed another vector than the one the field-of-view search sees (66.3° for (0.3, 0.5, 0.2)). Every path
  now takes `RotationToSpacecraft`, `eul2m(x, y, z, 1, 2, 3)` as Frames Required Reading defines a TK frame
  ("Defining a TK Frame Using Euler Angles"): 2.2e-16 rad from the closed form, and 4.7e-16 rad from
  `pxform(instrument, spacecraft)` on the frame kernels the library writes (Windows x64; 3.0e-16 rad on Linux x64,
  2.9e-16 rad on macOS arm64) ([#524](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/524)).
- **`InstrumentPointingToAttitude` returned the inverse rotation** ([#538](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/538)), the one that takes the target's direction to the
  boresight: at zero orientation the boresight landed 137.519° from the target, twice the 68.759° between them, and its
  test asserted a snapshot 95.8° from the Moon. It now takes the boresight to the target: 4.4e-16 rad from the minimal
  rotation of the frame kernel's boresight onto the Moon's direction from `spkezr`, for five orientations. The light
  time it requests is not applied to a celestial target, whose direction stays geometric (4.3e-5 to 4.5e-5 rad from the
  light-time-corrected Moon of `spkpos` from a LEO), a defect outside this change with its own issue,
  [#549](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/549) ([#537](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/537)).
- **A state in GCRF propagates.** `PropagatorBase` rejected `Frame.GCRF` as an initial frame ("must be defined in
  inertial frame"), although GCRF is inertial with the axes of ICRF. It is accepted, and `SpiceAPI` hands a frame named
  GCRF (`Frame.GCRF` or `new Frame("GCRF")`, in any case) to SPICE as `J2000` in `ReadEphemeris`, `ReadOrientation`,
  `TransformFrame` and `FindWindowsOnCoordinateConstraint`, and labels the result with the frame given (SPICE has no
  GCRF frame: the forces read uninitialised states, NaN, once the frame was accepted, and these methods and the CLI's
  `ephemeris` and `frame` commands in GCRF returned uninitialised or zero states). A propagation from a GCRF state is
  that of its ICRF twin, bit for bit (0 m), with Velocity-Verlet, RK7(8) with and without a covariance, an impulse then
  a nadir attitude, in SSB mode, and over the day of EGM2008 10×10, drag, SRP, the Moon and the Sun (Linux x64); the
  reads in GCRF equal those in J2000, bit for bit ([#543](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/543)).
- **`Spacecraft.ToOpm` exports the covariance of the initial state.** `StateVector.ToStateVector(epoch)` went through
  `AtEpoch`, which builds Keplerian elements without the covariance, even at the state's own epoch: the OPM never carried
  it. A `StateVector` read at the instant of its own epoch is now the state itself, with its covariance, and the OPM
  carries its 21 terms in km², km²/s and km²/s², the m² terms times 1e-6 within two ulp, in ICRF and ECLIPJ2000, the
  epoch omitted or tagged UTC or TDB. At another epoch the OPM carries no covariance, as before (Changed below)
  ([#551](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/551)).
- **`MemoryDataProvider` reads depend on the entries only.** An interpolated read was stored as an entry, so the reads
  after it depended on it: a state 7.70 m off its circular orbit after 40 reads, orientations 1.8e-7 rad off after reads
  in sequence, one entry more per read. A read is no longer stored, and an orientation is the exact spherical linear
  interpolation of its two entries (Changed below): 2.39e-16 rad from the closed form over 999 fractions of rotations of
  0.0113, 0.025 and 0.05 rad (6e-8 rad before), and the state at 605 s 8.8e-7 m from the orbit, whatever the reads
  before, the same on Linux, Windows and macOS arm64 ([#548](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/548)).
- **The command line reads the kernels of a spacecraft given by its NAIF identifier.** `orientation` built a managed
  spacecraft with an empty attitude record and printed the identity whatever the CK held (0.7 rad off in the issue's
  case); `ephemeris`, `sub-point` and `angular-separation` threw `NullReferenceException`. `orientation` now reads the CK
  by the identifier (`SpiceAPI.ReadOrientation`, `ckgpav_c` through the clock kernel), relative to the requested frame,
  and the other commands take the kernel-backed spacecraft at the start of the window or at the epoch. On the kernels the
  library writes for 600 s of a nadir LEO, against CSPICE: the orientation 0 rad and 2.2e-18 rad/s from `sxform` in
  J2000, ECLIPJ2000, ITRF93 and over a UTC window (4.5e-16 rad from spiceypy's `pxform`, CSPICE N0067), the ephemeris
  equal to `spkezr`, the sub-point equal to its closed form on `spkezr`, the same on Linux, Windows and macOS arm64
  ([#542](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/542)).
- **A null target orbit is an argument error.** The target-orbit constructor of `ImpulseManeuver`, under
  `ApsidalAlignmentManeuver`, `PlaneAlignmentManeuver` and `PhasingManeuver`, dereferenced the target before its null
  check: `NullReferenceException`, the `ArgumentException` unreachable. It throws `ArgumentNullException` (an
  `ArgumentException`) naming `targetOrbit`, before the check of the engine, whether the fuel tank is attached to a
  spacecraft or not ([#552](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/552)).
- **The XML documentation of `OpmManeuverParameters`.** A bare `<= 0` in three comments (`ManDeltaMass`, the
  constructor's `manDeltaMass`, `CreateImpulsive`'s `deltaMass`) was badly formed XML (CS1570 with
  `GenerateDocumentationFile`), and the whole documentation of the three members was dropped from the generated file.
  It is escaped; the library and the command line build without CS1570 ([#550](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/550)).
- **`PhasingManeuver`'s true-longitude constructor threw at its burn**
  ([#588](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/588)).
  - **The cause.** `PhasingManeuver(maneuverCenter, minimumEpoch, hold, trueLongitude, revolutionNumber, engine)` set
    no target orbit, and the burn read only the target orbit: `NullReferenceException` at the first perigee after the
    minimum epoch, in `TryExecute`, `Spacecraft.Propagate` and `Scenario.SimulateAsync` alike. `TargetTrueLongitude`
    was never read.
  - **The fix** (option (a), decided by S. Guillet, 2026-11-05). The true longitude is that of a virtual target on the
    spacecraft's current orbit at the epoch of the burn, Ω + ω + ν about the maneuver center in the frame of the
    spacecraft's state: Δθ = λ − λ_sc(burn), the formula of the target-orbit constructor with the target's angular
    momentum replaced by the current orbit's. The burn is at the perigee, so λ − (Ω + ω) is the phase of the target
    ahead of the spacecraft, modulo 2π, behind beyond π.
  - **Measured** (Linux x64, Windows x64 and macOS arm64, .NET 10, Release, 2026-11-05) against the two-body closed form (T₂ = T₁ − t(Δθ) / N, a₂
    from T₂, ΔV by vis-viva at the perigee) on a circular and an eccentric orbit, the target 30° ahead and behind, after
    1 and 3 revolutions: the ΔV of `TryExecute` within 9.2e-9 m/s, the hold 0.9 N T₂ within 2.2e-7 s, and within
    1.8e-12 m/s of the target-orbit constructor with the target on the same orbit at that phase (4.6e-13 m/s on Linux
    and macOS arm64). In a propagation
    about a point-mass Earth, RK7(8) at 1e-12 burns within 1.9e-8 s of the closed-form perigee, its ΔV within
    1.3e-8 m/s, and after N revolutions of the phasing orbit the spacecraft is within 1.4e-3 m of the virtual target;
    Velocity-Verlet at 1 s burns at the end of the step holding the perigee, 2.0e-4 m/s and 49 m.
  - **Unchanged, bit for bit:** the target-orbit constructor. See
    [Impulse Maneuvers](../reference/impulse-maneuvers.md#phasingmaneuver).
- **`ImpulseManeuver.TryExecute` changed the state it was given**, found with
  [#588](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/588).
  - **The cause.** It added the ΔV to the velocity of its argument and returned that same state. A spacecraft whose
    initial orbital parameters were that state, or Keplerian elements whose cached state it was (`ToStateVector()`),
    then held the state after the burn: its next `Propagate` started from it, while the elements still described the
    orbit before the burn.
  - **The fix.** It returns the state after the impulse as a new state (the position, epoch, frame, observer and
    covariance of the given state, its velocity plus the ΔV) and leaves the given state unchanged, for every impulse
    maneuver; so does `ManeuverEventDetector.HandleEvent`, whose state after the impulse is `EventResult.ModifiedState`.
  - **Unchanged, bit for bit:** the state it returns, the ΔV, the fuel and the windows, and every propagation, which
    executes impulses on a state of its own and continues from the returned state.
- **The distance search of a kernel spacecraft ran with a threshold 1000 times too small** ([#466](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/466)).
  When both objects come from the kernels and one is a spacecraft, `CelestialItem.FindWindowsOnDistanceConstraint`
  and `Site.FindWindowsOnDistanceConstraint` (a site from the kernels, a kernel spacecraft as observer) hand the search to
  CSPICE's `gfdist_c` through `SpiceAPI.FindWindowsOnDistanceConstraint`, which takes metres (the native layer converts
  them to km); both divided the metres by 1000 first, so "farther than 1815.148 km" ran as "farther than 1.815148 km":
  `Greater` returned the whole search window, `Lower` and `Equal` nothing, with no error, and the same geometry gave
  another answer through the managed search (a `Site` observer). They now pass the metres. Only these two calls change,
  on that branch; the coordinate search, which converts its distances to km for `gfposc_c`, was right and is unchanged.
  - **Measured** (Linux x64, 2026-11-11) against `gfdist_c` and `gfposc_c` converged to 1e-9 s
    (`tools/geometry_reference/kernel_search_reference.py`, `KernelWindowSearchTests`), LRO from 2023-12-15 00:05 to
    12:05 TDB at 60 s: LRO and the Moon beyond, within and at 1815.148 km (7, 6 and 12 windows; 1, 0 and 0 before),
    LRO and the Earth-Moon barycenter beyond 364 500 km (7; 1 before), LRO and a second kernel spacecraft (a two-body
    lunar orbiter written by the generator) within 3000 km (8; 0 before), each with either object as the receiver, and
    DSS-13 from the kernels and LRO beyond 372 000 km (2; 1 before): every edge within 4.8e-7 s, CSPICE's default
    tolerance of 1e-6 s; the managed search of LRO from DSS-13 within 1.2e-7 s of the same windows, and the coordinate
    searches (Z > 500 km, range > 1815.148 km and > 372 000 km) within 4.8e-7 s, before and after.
- **The illumination windows of a site from the kernels were seconds off** ([#488](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/488)).
  `Site.FindWindowsOnIlluminationConstraint` and `Site.FindDayWindows` of a site from the kernels (`new Site(id, name,
  body)`) search with CSPICE's `gfilum_c` through `SpiceAPI.FindWindowsOnIlluminationConstraint`, whose native layer
  read the altitude of the `Planetodetic` in km beside a radius in km: DSS-13, 1070.85 m up, became a point 1070.85 km
  up, whose normal is tilted by 94″, and its sunrise and sunset were 4.4 s off (3.1 s to 29.6 s in the other geometries
  measured). `SpiceAPI` now converts the altitude to km before the call. So are fixed the launch windows by day of a
  recovery site from the kernels (`Launch.FindLaunchWindows` through `FindDayWindows`) and the CLI command
  `find-windows-from-illumination-constraint` (under Changed). The sites built from their coordinates, which the library
  searches, are unchanged.
  - **Measured** (Linux x64, 2026-11-11) against `gfilum_c` converged to 1e-9 s
    (`tools/geometry_reference/kernel_search_reference.py`, `KernelWindowSearchTests`) over 24 h from 2023-12-15 00:05
    TDB: DSS-13 from the kernels, at its position at J2000 TDB, incidence above 90° without correction, below 90.8° and
    96° with LT+S (`FindDayWindows` at 0.8° and 6°), above 60° with CN+S, within 3.6e-7 s (4.4, 4.3, 3.6 and 29.6 s
    before); `SpiceAPI` at the longitude of DSS-13 at 0, 1071 and 5000 m, at latitudes 0 and 60° at 1071 m, and on the
    Moon at 1071 m over 30 days, within 4.8e-7 s (0.11 s to 15.2 s before; 0 m unchanged). The site from the kernels and
    the site built from its coordinates now agree within 4.96e-3 s (4.1 s and 5.2 s before): `gfilum_c` takes the
    normal of a point above the ellipsoid from `surfnm_c`, 0.11″ from the geodetic normal of the library at DSS-13
    ([Sites](../reference/sites.md#window-searches)).
- **`EpochAtMeanAnomaly` came one period late from a `StateVector`, and could be in the past at e = 0.999999** ([#474](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/474)).
  It added 2π to the difference of the anomalies at most once, so it was right only when both lay in [0, 2π): from a
  `StateVector`, whose mean anomaly is in (−π, π), a target between the position and the periapsis came one period late
  (12 % of the pairs of a grid of the issue), and so did the epoch of `ToStateVector(trueAnomaly)`; a target outside
  [0, 2π) gave a past epoch or one a period late; at e = 0.999999, where `KeplerianElements` keeps its mean anomaly
  unreduced, the epoch could be in the past or throw `OverflowException` (`TimeToRadius` and `TimeToSphereOfInfluence`
  with it). On an ellipse the difference is now reduced modulo 2π: the next passage at or after the epoch, whatever the
  turn of either anomaly; an open orbit still passes once. A passage beyond the epochs a `Time` holds throws
  `ArgumentOutOfRangeException`. `KeplerianElements` still keeps the mean anomaly at e = 0.999999 (`MeanAnomaly()`,
  `AtEpoch`, the OPM export), the open choice of the issue.
  - **Measured** (Linux x64, 2026-11-11) against ((M_t − M_0) mod 2π)/n at 60 digits
    (`tools/epoch_at_mean_anomaly/epoch_at_mean_anomaly_reference.py`, `EpochAtMeanAnomalyTests`), about the Earth in
    TT seconds: from the `StateVector` of a = 1e7 m, e = 0.3, M = 5 rad, the passage at 5.5 rad 791.956 s and at
    ν = 300° 1104.351 s later (10 743.970 s and 11 056.365 s before); from `KeplerianElements` at M = 1 rad the
    targets −7 and 8 rad, and from `EquinoctialElements` the targets 5.5, −7 and 8 rad, all within 4.5e-8 s, half a
    tick; a grid of `StateVector`s after the apoapsis (e from 0.001 to 0.999) within 8.4e-12 of the period; about the
    Sun, a comet at e = 0.999999 (a = 1e4 au) from M = −0.02 rad within 9.2e-4 s of 5.02e10 s (`OverflowException`
    before) and 300 years later within 3.4e-3 s of 5.60e9 s, the rounding of M (one ulp is 4.5e-3 s); at e = 0.999999
    and M = 20 rad, `TimeToRadius(7e6 m)` on the issue's a = 7e6 m within 3.9e-8 s of 5290.815 s (−12 194.735 s before)
    and `TimeToSphereOfInfluence` about the Moon (a = 5e7 m) within 1.35e-5 s of 973 267.782 s, where one ulp of cos ν
    moves the time by 2.2e-5 s. Pairs with both anomalies in [0, 2π) keep their epoch bit for bit.

- **`Time(string)` threw on the `UTC` of its documented example** ([#455](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/455), items 1 and 2).
  - **The cause.** After a calendar that `DateTime` does not read on its own, the constructor took the last three
    characters as the frame and knew only `TDB`, `TDT`, `GPS` and `TAI`: `new Time("2024-06-21T12:00:00.000 UTC")`, the
    example of `docs/reference/time-and-windows.md` and of `DEVELOPER_GUIDE.md`, threw `ArgumentException`, and so did
    ` TT`, the IAU name of the scale the library calls TDT.
  - **The fix.** The frame is the word after the last space, in any case: `UTC`, `TT` or `TDT`, `TDB`, `TAI`, `GPS`;
    `2024-06-21T12:00:00.000 UTC` is the instant of `2024-06-21T12:00:00.000Z` and of `Time.CreateUTC(772243200)`, its
    TAI 37 s later. A second 60 throws an `ArgumentException` that says a `Time` holds no leap second (it threw "Value is
    not a valid time frame", or a `FormatException` with a frame name). A frame name right after the calendar, with no
    space (`…12:00:00.000TDB`, `…12:05TDB`), is read with the calendar as written: the constructor cut the calendar one
    character short, which read the right instant when that character was a trailing zero (`.000TDB`, `12:00:00TDB`),
    and otherwise threw a `FormatException` (`12:00:05TDB`) or read another instant (`2024-06-21 12:05TDB` read 12:00).
    A `Z` or an offset before a frame name (`…12:00:00Z UTC`, `…+02:00 TDB`) throws an `ArgumentException` (Changed
    below): `DateTime` converted the calendar to the machine's local time, which the frame then labelled.
  - **Unchanged:** the `Z`, offset and untagged forms, and the time of every string with a space before its frame name
    and no zone designator.
  - **The documentation of the time scales.** `DEVELOPER_GUIDE.md` ("IERS Conventions, leap seconds from NAIF LSK
    kernels") and `docs/standards-and-units.md` ("Time: IERS conventions") now state what the managed conversions do: the
    leap second table of `TimeFrame` (a fixed 9 s before 1972, as the LSK), TT = TAI + 32.184 s, GPS = TAI − 19 s, and
    the one-term TDB − TT series, within 1.06 µs of the LSK formula SPICE `unitim` evaluates and 37.34 µs of ERFA `dtdb`
    from 1972 to 2100; CSPICE is given the managed TDB as its ephemeris time. See
    [Time Scales](../reference/time-and-windows.md#time-scales).
    `Period()` for e ≥ 1 (item 3) is [#663](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/663); the ephemeris time given to CSPICE is still the managed TDB,
    not the LSK formula (item 4, [#665](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/665)). Both are under **Known limitations**.

- **The documentation named `RelationnalOperator.Less`, `AbsMin` and `AbsMax`, which do not exist** ([#483](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/483)). The
  `Site` and TLE tracking examples of `DEVELOPER_GUIDE.md` did not compile (CS0117), and the operator tables of
  `DEVELOPER_GUIDE.md` and [Enumerations](../reference/enumerations.md#relationnaloperator) left out `NotEqual`,
  `GreaterOrEqual` and `LowerOrEqual`. The examples pass `Lower`; the `Site` example names the members of `Planetodetic`
  (`Longitude:`, `Latitude:`, `Altitude:`, CS1739 before), and the TLE example gives its 420 t spacecraft a maximum
  operating mass of 450 t (1 t threw `ArgumentOutOfRangeException`). Both compile and run: the Moon at 399 039 km from
  the site at 2024-01-01T12:00 TDB, as SPICE `spkcpo` gives it (399 039.486 km), and 7 windows of the ISS within
  2000 km of DSS-13 on 2024-06-03. The tables list the ten members as declared, each with its symbol, and the geometry
  tutorial names `Lower` and the negations. `DeveloperGuideExamplesSmokeTests` runs both examples on the packed package:
  the Moon's range within 1e-3 m of `spkcpo` (3.1e-5 m on Linux), and the TLE example's windows found. The other C#
  blocks of the guide and of `docs/` that do not compile (100 lines in 62 blocks: the `Frames.Frame` qualifier, members
  that do not exist) were [#662](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/662), fixed by #668 for the guide and by #705 for `docs/` (below).

- **The equinoctial snippet of the orbit-creation tutorial did not compile, and two pages called the elements free of
  singularity for every equatorial orbit** ([#465](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/465)). The
  snippet called `H()`, `K()`, `P()` and `Q()`: `P`, `H` and `K` are properties and nothing is named `Q` (CS1955, CS1061),
  and its labels h, k, p were the inclination components and the semi-latus rectum. It now prints `P` (m), `F`, `G`, `H`,
  `K` and `L0` under their definitions, and says that the classical set of GMAT (EquinoctialH, K, P, Q) is G, F, K, H in
  these names; for the tutorial's state it prints p = 7424377.237 m and f = e = 0.091820, the closed form of a periapsis
  (p = (r v)²/μ, e = p/r − 1). [Keplerian Elements](../reference/keplerian-elements.md#equinoctialelements) and
  `DEVELOPER_GUIDE.md` list the six properties and the accessor of each. Both now say what orbit-creation already said:
  no singularity at e = 0 or i = 0, where the Keplerian ω and Ω are undefined, and the retrograde equatorial orbit
  (i = 180°) singular, h and k growing as tan(i/2). `EquinoctialElementsTests` checks the tutorial's state against the closed form and state S1 of
  the issue against GMAT R2026a (components within 1.9e-16, P within 9.3e-10 m, Linux), the accessor methods
  against the properties, and the orbit of the issue at i = π − δ (δ = 1e-3, 1e-6, 1e-9) against tan(i/2) cos Ω and
  tan(i/2) sin Ω at 50 digits (H and K within 1.4e-16 relative, the state within 1.9e-9 m of the Keplerian one, Linux).

- **The export example of the OPM page did not compile** ([#557](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/557)). It called
  `spacecraft.ToOpm(epoch, Frame.ICRF)`, whose first parameter is the originator and which has no frame parameter
  (CS1503 twice). It now calls `spacecraft.ToOpm(epoch: epoch)`, and [OPM](../reference/opm.md#spacecraft-export) gives
  the signature, says that the state is that of the spacecraft's initial orbital parameters at the epoch (two-body, or
  SGP4 for a TLE; not a propagated state), written in their frame, and that a `CosparId` is required. The example
  compiles and runs: the state read back from the file is the state written.
- **A geopotential model file that ended with a line break threw, and an LF file read on Windows was one line**
  ([#502](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/502)). `GeopotentialModelReader` split the whole file on `Environment.NewLine` and parsed six fields
  from every piece: a final line break left an empty last piece, which threw `ArgumentOutOfRangeException`, and on
  Windows an LF file was one line, of which only the first coefficient was read. It now reads line by line, a line
  ending on LF, CRLF or CR on every platform, and skips blank lines; the parsing of a row is unchanged.
  `GeopotentialModelReaderTests` reads the 2553 rows of `EGM2008_to70_TideFree` joined by LF, CRLF and CR, with and
  without a final line break and with blank lines, each coefficient checked against the text of its row. The model
  files of the library and of the conformance suite have no final line break: no result moves.
- **The command line threw `FormatException` for a ground site given by its NAIF identifier**
  ([#514](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/514)). `Helpers.CreateLocalizable` took an identifier from 100 000 to 999 999 as a site, and read the
  site's number by parsing the name of an iterator type instead of its digits, so every command given a site as object
  or observer threw. The site is now `objectId % 1000` on the body `objectId / 1000`, the rule of `Site.NaifId`.
  `EphemerisTests.CallFromASite` reads the Moon from DSS-13 (399013) through `ephemeris`, against spiceypy's `spkezr`
  to 1e-9 relative.
- **A field-of-view search on a managed spacecraft without attitude searched the identity of ICRF**
  ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). A spacecraft never propagated, with no orientation added, built from a TLE or propagated
  by `TLEPropagator` has an empty attitude record, which `WriteOrientation` refuses to write; its frame reads the
  identity there, and `Instrument.FindWindowsInFieldOfViewConstraint` and `Instrument.IsInFOV` searched with it (a camera
  of 30° along +Y in LEO: 5 windows of the Earth over 6 h, its boresight ICRF +Y). Both now throw
  `InvalidOperationException` naming the spacecraft, after the argument and field checks, whose errors keep their
  precedence (internal `FieldOfViewSearch.CheckAttitude`). `FieldOfViewSearchTests.ManagedSpacecraftWithoutAttitude_Throws`
  checks the search (the Earth as a point and as its ellipsoid, the Moon) and `IsInFOV` on a spacecraft from a state,
  from a TLE and propagated by `TLEPropagator`, and that an attitude added lifts the refusal;
  `AttitudeAddedByHand_IsTheOneSearched` holds `IsInFOV` to the closed form of a circular field for an
  attitude given by hand, at the edges of the search within 9.7e-11 rad of the half-angle. The getters
  (`GetBoresightInICRFFrame`, …) keep the identity before any attitude.
- **The emission and phase angles of a site's illumination search were measured from the site itself**
  ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). `Site.FindWindowsOnIlluminationConstraint` takes no observer: the managed path measured the
  angles towards the site (0, one window of the whole search) and `gfilum_c` was given the site as its observer (0 and
  2 windows over 2024-03-01 for DSS-13, where `gfilum_c` from the Moon gives one emission window, 06:52:07.61 to
  17:06:28.25 TDB, and no phase window). The angles are at the point towards an observer (`gfilum_c` Particulars),
  undefined at the point itself: the search now throws `ArgumentException` for them and keeps the incidence, unchanged.
  `SiteTests.IlluminationEmission_FromTheMoon_EqualsGfilum` holds the workaround its message names to `gfilum_c`, 4.4e-3 s
  (the normal of [#654](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/654)).
- **An illumination method other than ELLIPSOID gave no window, or was ignored** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). `gfilum_c`
  supports ELLIPSOID only (Detailed_Input `method`); for `"Dsk"`, `"Elipsoid"`, `"ELLIP SOID"`, `""` or the DSK forms
  it signalled SPICE(INVALIDMETHOD) or SPICE(EMPTYSTRING), which the native proxy dropped: no window on the CSPICE path,
  while the managed path never read the method (3 windows of the ellipsoid for each string). One check
  (internal `SpiceAPI.CheckIlluminationMethod`), used by `Site.FindWindowsOnIlluminationConstraint` and
  `SpiceAPI.FindWindowsOnIlluminationConstraint`, accepts ELLIPSOID whatever its case and its leading or trailing
  whitespace and throws `ArgumentException` for anything else (a null method `ArgumentNullException`, on the managed path
  too); `"ellipsoid"` and `" Ellipsoid "` keep their windows to the tick.
- **The managed planetographic longitude was the planetocentric one** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). The managed
  `CoordinateSystem.Planetographic` search (a body, a propagated spacecraft or a site from coordinates; the CSPICE path is
  taken with a kernel spacecraft) compared the east longitude in (−π, π] where `gfposc_c` compares the planetographic
  one, in [0, 2π), positive west for a prograde body other than the Earth, the Moon and the Sun (`recpgr_c`
  Detailed_Output `lon`, Particulars): the Moon from the Earth above 1 rad over 10 days gave 10 windows where `gfposc_c`
  gives 11, above 4 rad none where it gives 10, and the Sun from Mercury 0.188 rad where `recpgr_c` gives 6.095. One
  internal evaluation shared by `CelestialItem` and `Site` (`GeodeticCoordinates.PlanetographicCoordinate`) takes the
  sense from the spin of the observer's body at the start of the search. `KernelWindowSearchTests` holds it to
  `gfposc_c` (the Moon from the Earth, the Earth and the Sun from Mercury, the Earth from Venus, DSS-13 from Mercury):
  the same window counts, the edges on the threshold within 3e-7 s (DSS-13 from Mercury; 2e-7 s for the others), those
  at the 0/2π cut within 6.6e-11 rad of it;
  `PlanetographicTests` holds the evaluation to `recpgr_c` for eight bodies within 1.7e-16 rad.
- **The managed planetographic latitude was the planetocentric one, and its altitude was a radius**
  ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). PLANETOGRAPHIC is (LONGITUDE, LATITUDE, ALTITUDE) in `gfposc_c` (Detailed_Input `coord`), its
  latitude the geodetic one on the spheroid (`recpgr_c` Particulars); the managed search took the planetocentric
  latitude (−0.297009940 rad for the Moon from the Earth on 2024-03-01, `recpgr_c` −0.297039902), whose edges above
  −0.4 rad were 51.5 s and 47.5 s off, accepted `Coordinate.Radius` and threw for `Coordinate.Altitude`. The latitude
  and the altitude are now the geodetic ones on the observer body's spheroid, their edges within 4e-7 s of `gfposc_c`
  (DSS-13 from the Moon, both; the Moon from the Earth 3e-7 s and 1e-7 s); the altitude is searched and the radius
  throws `ArgumentOutOfRangeException`.
- **A site built from coordinates, or a managed spacecraft, read in its own frame threw SPICE(UNKNOWNFRAME)**
  ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). A read in a frame that only the library holds, the `SiteFrame` of a site built from coordinates
  seen from an observer other than its body (the Moon, the Sun, a kernel spacecraft) or the `SpacecraftFrame` of a
  managed spacecraft, reached `spkezr_c` by name: `site.GetEphemeris(t, moon, site.Frame, …)`,
  `moon.GetEphemeris(t, earth, site.Frame, …)`, `SpiceAPI.ReadEphemeris` and the managed searches on them threw.
  `SpiceAPI.ManagedReadFrame` now routes these frames as [#558](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/558) routes TEME, CIRS and TIRS: read in J2000 and
  rotated by the managed frame at the epoch of the read. Against `spkezr_c` and `pxform_c` · `georec_c` on the frame
  kernel the library writes for the site, 4.0e-7 m and 6.4e-11 m/s over 5 epochs from the Moon; against `spkezr_c` in
  the frame of a managed spacecraft on the frame kernel and the CK the library writes from its record, 6.8e-8 m and
  1.6e-13 m/s. A managed spacecraft whose attitude record is empty (never propagated, no orientation added, from a TLE
  or `TLEPropagator`) has no attitude in the library: its frame is still read by name, as before, so the frame kernel
  and the CK of its id, if loaded, give its attitude (within 1.7e-16 rad of the attitude written), and SPICE rejects the
  frame otherwise; it is not read with the identity of the empty record.
- **`CelestialItem.GetEphemeris(window, …)` read past its window across a leap second** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)). It
  counted floor(`Length` / step) + 1 samples, `Length` counting the leap second ([#508](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/508)), and placed them in
  the calendar, which does not: over 2016-12-31T23:59:00Z to 2017-01-01T00:01:00Z it returned 122 states at 1 s and 2
  at 121 s, the last at 00:01:01Z, a second past the end. The samples now stop at the end, as `Site.GetEphemeris(window)`
  and `Star.Propagate` do: 121 and 1 states (the CLI's `ephemeris` with them). Every other window keeps its states bit
  for bit.
- **`LoadKernels` and `UnloadKernels` matched kernel paths by substring** ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)). A path whose text contained a
  loaded one was taken for a path inside it: after `k/SolarSystem`, the load of its sibling `k/SolarSystem2` reloaded
  `k/SolarSystem` and loaded nothing (L1 then threw SPKINSUFFDATA), in the other order the sibling was unloaded, and
  `UnloadKernels(k/SolarSystem)`, never loaded, unloaded `k/SolarSystem2`; a folder elsewhere whose path ends with a
  loaded one was never loaded. Paths are now compared by their components, on the full paths without their trailing
  separator, ordinal as before: the same path, or a path inside a loaded folder, reloads that entry, and a folder load
  first unloads the entries inside it, as before; a sibling is another path. `KernelRegistryTests` reads TK frame
  kernels of 90° and 45° about z back through SPICE (`pxform`) after each order, and compares on Linux the files SPICE
  holds (KEEPER, `ktotal_`/`kdata_`) with those the scenario loaded. No result moves on `Data/SolarSystem`.
- **`UnloadKernels` left a deleted or moved path loaded, and a folder listed after one of its files was unloaded**
  ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)). The unload ran only for a path that existed, and walked it as it was on disk, so a folder of kernels loaded,
  deleted, then unloaded stayed listed and SPICE kept its files (the Moon still read at 4.046675e8 m); through the
  `DirectoryInfo` that was loaded, whose cached `Exists` stayed true, the entry was dropped while SPICE kept the files.
  After `UnloadKernels` of a file of a loaded folder, the folder stayed listed while SPICE no longer held the file. Each
  entry now records the files its load handed SPICE, and the unload hands SPICE those names, whether the path still
  exists or not; an entry that holds the path is replaced, at its place, by its remaining files. A path that matches no
  entry is handed to SPICE as before, without the existence test: a file loaded by a meta-kernel is unloaded by its
  name. C++: `KernelsLoader::Unload` hands any path that is not an existing directory to `unload_c`, which unloads a
  file by the name it was loaded under and does nothing for a name it does not hold (its unreachable throws, which
  would have crossed the `extern "C"` boundary, are gone); the P/Invoke is unchanged. `ClearKernels` calls `kclear_c`
  without unloading each entry first, which would read again a text kernel deleted since its load. The C++ tests
  `API.UnloadKernelsProxyOfADeletedFile` and `API.UnloadKernelsProxyOfANameNotLoaded` check the frame lookup of
  `TransformFrameProxy`; `KernelRegistryTests` the frames and the listing after a deleted folder (both instances) and a
  file of a folder. SPICE unloads a text kernel or a meta-kernel by reading the pool again, which fails while another
  text kernel deleted since its load is still loaded, and leaves out the text kernels loaded after it. The unload of
  an SPK, a CK, a binary PCK, a DSK or an EK opens the other loaded kernels of its kind again (`unload_c` → `UNLOAD` →
  `SPKLEF`, `CKLPF`, `PCKLOF`, `ZZDSKLSF` or `EKLEF`), reads no text kernel, and fails on a kernel of its kind deleted
  since its load; after such a failure the SPKs, CKs or PCKs loaded before the deleted one take precedence over those
  loaded after it (each reload of a loaded file closes it and loads it again at the highest priority, and the files
  after the deleted one are not reloaded; measured with CSPICE N0067 for SPKs, CKs and PCKs, and by `KernelRegistryTests`
  for PCKs: a PCK frame read from the PCK loaded before the deleted one until the deleted one is unloaded). A
  meta-kernel's unload also unloads the kernels it names and opens again the loaded kernels of each of their kinds
  (measured for SPKs, CKs, PCKs, DSKs and EKs). SPICE loads a text kernel as a meta-kernel when its load assigns
  `KERNELS_TO_LOAD`, `PATH_SYMBOLS` or `PATH_VALUES` (FURNSH watches them), and an old DAF (`NAIF/DAF`, as
  `earthstns_itrf93_201023.bsp`) as an SPK or a CK from its segments (`GETFAT` → `ZZCKSPK`). The unload of the recorded
  files throws for such a failure unless a later unload of the same call, of the same entry or of another one, read the
  same kernels again: a text kernel's or a meta-kernel's for a text kernel, one of the same kind for an SPK, a CK, a PCK,
  a DSK or an EK, none for a meta-kernel, whose files the library does not read, nor for an old DAF, whose kind it does
  not tell (internal `SpiceAPI.UnloadRereads`, from the ID word of the file and, for a text kernel, the variables its
  data assigns, outside its comments and quoted strings, read at its load; the type KEEPER gives each text kernel of the
  tests is checked by `KernelRegistryTests` through `kdata`). Every entry the call matched is unloaded and removed
  before it throws. A binary kernel SPICE failed to open again stayed open in SPICE for the life of the process (fixed by
  the second round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), below): KEEPER
  records the handle 0 for it, its unload and `kclear_c` close nothing (measured for SPKs, CKs and PCKs; the EK not
  measured), and once its logical unit is released (more than the 23 units of CSPICE's handle manager in use) the
  searches that reach it fail to reconnect it: for a PCK every IAU frame, for a CK the reads at epochs not already read,
  while for an SPK the reads of the Moon, L1, L2 and Mars after 25 more DAFs did not fail in the measured layout
  (measured with CSPICE N0067; the CI of the first version of the PCK tests, run in the test process, failed
  that way on Linux and macOS arm64, `TISBOD` → `PCKSFS` → `DAFBBS` → `ZZDDHHLU` on the deleted PCK, in other test
  classes). The tests that delete a loaded binary kernel therefore run in the child process
  `IO.Astrodynamics.Tests.KernelUnload`, which the test project builds and copies to `KernelUnload/`, and are skipped
  on Windows, where a loaded binary kernel cannot be deleted. `KernelRegistryTests` unloads a folder of a text kernel listed before an SPK while a deleted text
  kernel is loaded, which throws and leaves the frame of the text kernel loaded after the deleted one unknown until
  the deleted one is unloaded; a meta-kernel of one SPK, whose unload SPICE fails the same way; a folder of two binary
  PCKs whose second is deleted (in the child process), between a PCK loaded before it and one loaded after it of the same frame class, whose
  unload no longer throws and leaves the later PCK in precedence; the unload of the first of those two PCKs alone, which
  threw and left the earlier PCK in precedence until the deleted one was unloaded; a folder of a meta-kernel of a PCK
  listed before a text kernel while a deleted PCK is loaded (in the child process), which threw, the text kernel's
  unload not taken for a repair (neither throws since the second round of #668, below); a folder of two loaded folders whose second's kernel is deleted, whose unload no longer
  stops at the first folder's failure; and a folder of a text kernel whose comment names `KERNELS_TO_LOAD`, listed
  before a deleted one, whose unload no longer throws. A folder is loaded in the order of its listing, alphabetical on
  NTFS: the tests name the file to list first from a to z in turn and check the order through `SpiceAPI.KernelFiles`.
- **The command line ignored a kernel file or a meta-kernel given as its kernels path** ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)). The 13 commands that
  load kernels wrapped the argument in a `DirectoryInfo`, from which a file loaded nothing without an error:
  `astro ephemeris mk.tm …` failed with "Celestial celestialItem frame can't be defined". A file, a kernel or a
  meta-kernel, is now loaded as a file (`Helpers.KernelsPath`), a folder as before, bit for bit; a path that does not
  exist is an argument error that names it (Changed, `LoadKernels` of a missing path). `KernelsPathTests` gives `frame-converter` a TK frame kernel, then a meta-kernel that names
  it, and reads the quaternion of the 90° rotation about z (|W| = |Z| = cos 45°).
- **The Cosmographia export wrote kernels that SPICE cannot load, and the native writers a file under a truncated
  name** ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)). SPICE takes a kernel path of at most 255 characters (NAIF, SPICE Kernel Required Reading,
  "Recommendations on Kernel File Naming"), and `LoadKernels` and the command line hand it the full path. The export,
  and `astro propagate` through it, deleted the previous export and wrote kernels at longer paths (`MySat.ck` at 302
  characters for a 120-character directory), which then failed with `SPICE(FILENAMETOOLONG)`. `SpiceAPI.WriteEphemeris`
  (under `Star` and `Site`) and `SpiceAPI.WriteOrientation` (under `Spacecraft.WriteOrientation`) handed such a path to
  CSPICE, which wrote a file under the name truncated to 255 and then failed. The export now checks every kernel path
  of its layout, under the output directory and under the new directory of the system temporary directory where it
  writes the kernels and loads the clock and frame kernels first, before it touches the output directory, and the
  native writers check their target, in the bytes the marshaller hands CSPICE (UTF-8 on Linux and macOS). `KernelPathLengthTests` writes each native writer's kernel at 255
  characters, which SPICE loads, while a copy at 256 fails in FURNSH; at 256 the writer throws and leaves no file, and a
  255-character path holding one é (256 bytes) is rejected on Linux; `Spacecraft.WriteEphemeris` and the clock writer
  still write at 300 characters, byte for byte. `ExporterTests` exports one character too long (nothing deleted) and
  at 255 (every kernel of `spice.json` loads), and to a short directory with a scratch directory whose kernels would be
  longer than 255 (nothing deleted, no scratch left); `PropagateTests` the command.
- **A body with a GM and no radii read GM 0 and no frame, one with radii and no GM a garbage frame** ([#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672)).
  The native body information read `BODYnnn_RADII` and `BODYnnn_GM` without checking that the kernel pool has them; the
  failed read left SPICE failed, so the reads after it did nothing until the error was reset at the end: the GM was the
  stack's (0 in practice) and the frame name was left undefined. The 8 bodies SPICE names that `gm_de440.tpc` gives a GM
  and `pck00011.tpc` no radii (Nix, Hydra, Kerberos, Styx, Pallas, Didymos, its barycentre, Dimorphos) read GM 0 and an
  empty or garbage frame, so `new CelestialBody(2000002)` threw; the 39 bodies with radii and no GM (Elara, Nereid...)
  read a garbage frame name, and `new CelestialBody(507)` was built with it. `GetCelestialBodyInfoProxy` now reads the
  radii and the GM only where `bodfnd_c` finds them: radii 0 and GM 0 where the pool has none, the frame read in every
  case, no SPICE error raised. `CelestialBody::ReadGM`, the mass of the native constructors of `WriteEphemerisProxy` and
  of the element conversions, was left unchanged
  ([#655](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/655)); its failure is reported by the
  conversions since #711 (below). Native change, no signature
  change.
  - **Test:** C++ `API.GetBodyInformationGmWithoutRadii` (Pallas, Nix: the GM against the text of `gm_de431.tpc`, the
    frame of `cnmfrm_c` called by the test, radii 0), `API.GetBodyInformationRadiiWithoutGm` (Elara: GM 0, the 40 km
    radii of `pck00011.tpc`, IAU_ELARA), `API.GetBodyInformationSolarSystemBarycenterGm` (the sum of the text over 1 to
    10), the GMs within 10 ulp of the text (`SpiceParseUlps` of `APITests.cpp`: at worst 4 ulp on Linux, 4 on Windows and
    5 on macOS arm64, the sum over 1 to 10, CI run 43602458842); `NativeAbortGuardTests.GetCelestialBodyInfo_GmWithoutRadii_ReadsTheKernelGm` (the 8 bodies against the GM
    parsed from the text of `gm_de440.tpc`, within 10 ulp: CSPICE's own parse is up to 5 ulp off the correctly rounded
    value, Jupiter barycentre, on Linux, Windows and macOS arm64 (CI run 43600640037), and 1 ulp for Nix, Kerberos and
    Pallas; the frames of `cnmfrm_c`, three reads equal),
    `GetCelestialBodyInfo_RadiiWithoutGm_ReadsTheFrame` (Elara, Nereid against the text of `pck00011.tpc`),
    `CelestialBodyTests.Pallas_BuildsWithTheKernelGm` and `Kerberos_WithoutFrame_Throws`. On `main` 2893404 the GMs
    read 0, Pallas throws, Kerberos is built with a garbage frame.
- **An unnamed identifier returned a stale or garbage name, a barycentre a garbage frame name** ([#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672)). The
  native record's name and frame name had no initialiser and were filled only when SPICE named the identifier and found
  a frame; `SpiceAPI.GetCelestialBodyInfo` returned the record without checking that SPICE named the identifier. After
  Pallas, 2000003 (a GM in `gm_de440.tpc`, no name) read the name `PALLAS` or stack bytes with identifier -1, the
  barycentres a garbage frame name, and `new CelestialBody(12345678)` was built from a fragment of earlier console
  output, or threw, in turn. The record is now zero-initialised, and `SpiceAPI.GetCelestialBodyInfo` and the two
  ellipsoid frames of `FindWindowsOnOccultationConstraint` throw `ArgumentException` naming the identifier when SPICE
  does not name it (the record's identifier stays -1; Changed below).
  - **Test:** C++ `API.GetBodyInformationInvalidId` (398 and 12345678 after the Earth: identifier -1, empty names, GM
    and radii 0) and `API.GetBodyInformationBarycenter`; `NativeAbortGuardTests.GetCelestialBodyInfo_UnnamedIdentifier_Throws`
    (398, 12345678, 2000003 and 99999999 after Pallas, three times, with `new CelestialBody` and `CelestialItem.Create`),
    `LagrangePoint_TheKernelsDoNotName_IsBuiltByCreateOnly` (391, 392, 394, 395, which `bodc2n_c` does not name: the
    record and `new CelestialBody` throw, `CelestialItem.Create` builds the `LagrangePoint`),
    `ReadEphemeris_FromAnUnnamedLagrangePoint_IsALagrangePoint` (the read from L1 the exact negation of the read to it,
    its observer the `LagrangePoint`, both overloads), `GetCelestialBodyInfo_Barycenter_HasNoFrame` (0 to 9 after the
    Earth, the names of `bodc2n_c`, no frame, the GM of the kernel text) and `Occultation_UnnamedEllipsoid_Throws`; CLI
    `BodyInformationsTests.UnnamedIdentifier_IsAnArgumentError` (398 and 12345678 after the Earth). On `main` 2893404
    the record is returned, the barycentres read `ITRF93` from the previous call, the occultation returns no window.
- **A deleted binary kernel stayed open in SPICE for the life of the process** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), second round). KEEPER's
  `UNLOAD` opens again the other loaded kernels of the kind it unloads (`unload_c` → `UNLOAD` → `SPKLEF`, `CKLPF`,
  `PCKLOF`, `EKLEF`); for a file deleted since its load `DAFOPR` fails before the subsystem touches its table, so the
  file stayed open in the DAF and subsystem tables while KEEPER stored the handle 0 for it: neither its unload nor
  `kclear_c` closed it (measured for SPKs, CKs and PCKs; the EK not measured), and once CSPICE's handle manager released
  its logical unit (more than 23 in use) the searches that reached it failed to reconnect it (`SPICE(FILEOPENFAIL)`): for
  a PCK every IAU frame, a deleted PCK never unloaded included, and for a CK the reads at epochs not already read (205
  intervals of the CLPS CK, read at 51, 102, 153 and 204); for an SPK the orphan handles stayed open, but the reads of the
  Moon (301), L1 (391), L2 (392) and Mars (4) from the Earth at ET 0, 1e6, 5e7 and -3e8 after 25 more DAFs did not fail
  in the measured layout (CSPICE N0067, fourth round of #668). With two deleted kernels of a kind no order of unloads avoided it, and no proxy gave the .NET
  layer the handle, so the fix is native (a technical reason, CLAUDE.md): `KernelsLoader::Load` and `Unload` first close
  every SPK, CK, binary PCK and EK whose file no longer exists by the handle KEEPER still holds (`spkuef_c`, `ckupf_c`,
  `pckuof_c`, `ekuef_c`), then unload it, the expected `SPICE(FILENOTFOUND)` of an intermediate reopening reset, the last
  unload reopening the remaining kernels of each kind in their load order; the P/Invoke declarations are unchanged. A DSK
  is left to SPICE: its subsystem has no public routine that closes it by handle. The native writers unload kernels too:
  the kernel object of `WriteEphemerisProxy` and `WriteOrientationProxy` unloads the file it replaces before writing and
  the file it wrote when it is destroyed (`EphemerisKernel::WriteData`, `OrientationKernel::WriteData`,
  `Kernel::~Kernel`), each an SPK or CK unload that opens the others again; they now unload through
  `KernelsLoader::UnloadFile`, which runs the same cleanup first (before, the reopening failed on the deleted kernel, which
  stayed open with the handle 0, and the destructor's failure, after the proxy's check, was left pending, so that the
  next kernel call failed with it). A kernel SPICE holds under a relative name, which a meta-kernel's `KERNELS_TO_LOAD`
  gives relative to the working directory of its load, is left to SPICE: once the working directory changed, the name no
  longer tells whether the file SPICE holds open was deleted, and the cleanup would close such an SPK while its file
  still exists. Measured with CSPICE N0067 and checked
  bit for bit against SPICE itself: the IAU_EARTH rotation and the Moon's state read before the deletion, read again once
  25 more DAFs are loaded.
  - **Test:** C++ `API.UnloadKernelsProxyWhenAnotherPckIsDeleted` (two copies of the moon PCK, the second deleted: the
    unload of the first returned false on `main` 9015ea2 and IAU_EARTH was the zero quaternion after 25 SPKs) and
    `API.UnloadKernelsProxyOfTwoDeletedSpks` (two deleted SPKs among four: the unload of the first returned false);
    `KernelRegistryTests.DeletedBinaryKernel_InAChildProcess`, in the child process `IO.Astrodynamics.Tests.KernelUnload`
    (not on Windows): a deleted PCK and the unload of another PCK, a folder of two PCKs both deleted, a deleted SPK and the
    unload of another SPK, a deleted PCK never unloaded and the load of a text kernel, a deleted CK and the unload of another
    copy of `Data/CLPS`'s CK; each checks that KEEPER no longer holds the deleted file and that its DAF handle is closed
    (`kdata_`, `dafhfn_` through `CspiceDirect`, Linux), then the IAU_EARTH rotation and the Moon's state after 25 more
    DAFs, bit for bit those read before the scenario. Six of them fail on `main` 9015ea2 (the unload throws, or the deleted
    file is still held). The writers: C++ `API.WriteEphemerisProxyWhenAnotherSpkIsDeleted` (two copies of the L2 SPK, the
    second deleted, an SPK written twice) and `API.WriteOrientationProxyWhenAnotherCkIsDeleted` (two CKs of the spacecraft
    -175 written by the proxy, the second deleted, a third written): before the writers' fix `failed_c()` was left true
    after the proxy returned true, KEEPER still held the deleted file with the handle 0, and the next unload returned false;
    the child scenarios `WriteEphemeris_WhenAnSpkWasDeleted_ClosesIt` and `WriteOrientation_WhenACkWasDeleted_ClosesIt`
    (the next kernel call threw the pending `SPICE(FILENOTFOUND)` before). The relative name: C++
    `API.LoadKernelsProxyAfterTheWorkingDirectoryChangedKeepsAnSpkAMetaKernelNamesRelatively` (KEEPER's handle of the SPK
    unchanged and open after the load of a text kernel elsewhere; before, it was closed) and the child scenario
    `KernelRegistryTests.LoadKernels_AfterTheWorkingDirectoryChanged_KeepsAnSpkAMetaKernelNamesRelatively_InAChildProcess`,
    on every platform (an object of an SPK the library wrote, read bit for bit after the load as before it; before, the
    read threw: no ephemeris data).
- **The native writers left the failure of their last unload pending** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth round). The kernel object of
  `WriteEphemerisProxy` and `WriteOrientationProxy` unloads the file it wrote when it is destroyed (`Kernel::~Kernel`),
  and that `unload_c` opens the other loaded SPKs or CKs again (`unload_c` → `UNLOAD` → `SPKLEF` or `CKLPF` →
  `DAFOPR`). The native drop of the deleted kernels leaves a name SPICE holds relative to the working directory to SPICE,
  so for such a kernel whose file was deleted, or after a change of working directory, the reopening fails with
  `SPICE(FILENOTFOUND)`. The object was a local destroyed after `return`, after the proxy's last `failed_c()` check, and
  `ActivateErrorManagement` does not reset: the proxy returned true, the error stayed pending, and the next proxy, an
  unrelated read, failed with it (`ReadEphemerisAtGivenEpochProxy` of the Moon a NaN state, `SpiceAPI.ReadEphemeris`
  "Celestial celestialItem name can't be defined", a wrong message that consumed the error); the failure path reset
  before the destructor, whose own error then leaked the same way, and neither proxy copied a message into
  `GetLastErrorProxy`. The kernel object now lives in an inner scope: the writing's error is taken and SPICE reset
  before it is destroyed, so that it unloads on a clean state as before, then the destruction's error is taken, and the
  proxy returns false with both messages in `GetLastErrorProxy` (a native change for a technical reason, CLAUDE.md: an
  error a proxy leaks; the P/Invoke declarations are unchanged). `SpiceAPI.WriteEphemeris` and `WriteOrientation` throw
  `InvalidOperationException` with that message, where they named the standard output. The success path is unchanged.
  - **Test:** C++ `API.WriteEphemerisProxyWhenAnSpkHeldUnderARelativeNameIsDeleted` and
    `API.WriteOrientationProxyWhenACkHeldUnderARelativeNameIsDeleted` (non-Windows: a meta-kernel naming `x.bsp` or
    `x.bc` loaded with the working directory set to its folder, restored at once, the file deleted, then a file written):
    the proxy returns false, `GetLastErrorProxy` names the file, `failed_c()` is false and the next read of the Moon from
    the Earth is bit for bit the one SPICE gave before the scenario; on 64a3026 the proxy returned true with the error
    pending. The child scenarios `WriteEphemeris_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending`
    and `WriteOrientation_WhenACkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending` of
    `KernelUnloadChildTests` (not on Windows): the writer throws naming the file, the next two reads of the Moon are bit
    for bit the read before; on 64a3026 the writer returned.
  - **The file a writer replaces** (review of the fourth round). The kernel object loads an existing file, and
    `EphemerisKernel::WriteData` and `OrientationKernel::WriteOrientations` unload it before removing it, an unload that
    fails the same way on such a kernel; they then removed the file anyway, and `spkopn_c` or `ckopn_c` returned at once
    on the pending error: the call threw with the old file gone and nothing written. They now stop before the removal
    when the unload failed, the error left signalled for the proxy, which reports it: the old file is on disk as it was
    (KEEPER's `UNLOAD` closed and dropped it before reopening the others), nothing is written. **Test:** C++
    `API.WriteEphemerisProxyReplacingAFileWhenAnSpkHeldUnderARelativeNameIsDeleted` and
    `API.WriteOrientationProxyReplacingAFileWhenACkHeldUnderARelativeNameIsDeleted` (non-Windows): the proxy returns
    false naming the file, nothing pending, the file's bytes those before the call, KEEPER (`kdata_c`) no longer holding
    it, the Moon bit for bit the state SPICE gave before; on the round's first fix the file no longer existed. The child
    scenarios `WriteEphemeris_ReplacingAFile_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndKeepsTheFile` and
    `WriteOrientation_ReplacingAFile_WhenACkHeldUnderARelativeNameWasDeleted_ThrowsAndKeepsTheFile` check the same
    through `SpiceAPI`.
- **`GetLoadedKernels` and the kernels the native layer drops** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth round and its review). The registry
  of `SpiceAPI` kept a deleted SPK, CK, binary PCK or EK listed after the native layer had dropped it, until it was
  unloaded. It now follows the native rule after each native load, unload or write (internal `ForgetDroppedKernels`),
  and only then: a `LoadKernels` or `UnloadKernels` that makes no native call (a path that does not exist, a directory
  without kernel files) leaves the registry as it is, since SPICE still holds the file (the first version forgot it after
  such a call too), and a symbolic link whose target was deleted counts as deleted, as `std::filesystem::exists`, which
  follows links, tells the native layer (the first version tested the link itself and kept it listed). **Test:** the child
  scenarios `LoadKernels_WithoutANativeCall_KeepsListingADeletedSpkSpiceStillHolds` and
  `LoadKernels_WhenTheTargetOfALinkedSpkWasDeleted_NoLongerListsTheLink` of `KernelUnloadChildTests` (not on Windows),
  against KEEPER's list (`CspiceDirect`, Linux) and the objects of the SPKs (read bit for bit while SPICE holds them, no
  longer known once it dropped them); both failed on the round's first fix.
- **The C++ tests linked a second CSPICE** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), second round). The native library linked CSPICE and SOFA
  without a keyword, which put the static archives in its link interface: the C++ test executable linked them again, so
  on Windows, where the DLL exports none of `cspice.lib`, a direct CSPICE call of a test bound to the executable's own
  copy, with an empty kernel pool and the default error action (why #672's first C++ tests failed on Windows only), and
  on Linux and macOS a routine the library does not contain (`rpd_c`) came from that copy. They are now linked
  `PRIVATE`; the headers stay public. The tests use `Constants::DEG_RAD`, bit for bit `rpd_c()`, and compile their
  cross-checks through `cnmfrm_c` and `bodc2n_c` on Linux and macOS only, where they bind to the library's CSPICE, the
  NAIF frames and names they expect asserted as literals on every platform. The shipped library is unchanged (on Linux
  the `.so` is byte for byte the one of the former link, sha256 `2b987ecb…`); the test executable holds no CSPICE
  routine (`nm`: no `T *_c`, one, `rpd_c`, before), and the C++ tests pass (368 at the end of the second round).
- **A ground station could not be the observer of `SpiceAPI.ReadEphemeris`** ([#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672)). Both overloads built their
  observer with `CelestialItem.Create`, which has no site branch: DSS-13 (399013) became a celestial body whose record
  has no radii, so the read threw `InvalidOperationException` ("frame can't be defined"), or labelled its state with a
  body built from undefined bytes. A site identifier (its body's identifier times 1000 plus its own, the body a planet
  or a moon, the rule of `Site.NaifId`) is now the observer `new Site(id % 1000, its SPICE name, new CelestialBody(id /
  1000))`, built before the SPICE lock as before (#623); the state is `spkezr_c`'s, unchanged.
  - **Test:** `SpiceApiTest.ReadEphemeris_FromASite_IsTheNegatedReadToTheSite` (ICRF, ECLIPJ2000 and the managed
    TEME, both overloads: the exact negation of the read from the Moon to DSS-13, an observer `Site` 399013 named
    DSS-13), `ReadEphemeris_FromASite_ObserverMatchesTheSiteObject` (the Moon read from a `Site` object, within 6e-8 m;
    2.98e-8 m measured on Linux, Windows and macOS arm64, CI run 43600640037), `ReadEphemeris_FromAnUnnamedSite_Throws`
    and `IsSiteId_IsTheRuleOfSiteNaifId`. On `main` 2893404 the `Time` overload throws in ICRF and the observer is a
    `CelestialBody`.

- **`CelestialItem.Satellites` kept every spacecraft alive, lost entries across threads, matched by NAIF id and listed
  items whose constructor threw** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)). It was a `HashSet` compared by NAIF id, without a lock, on process-wide
  instances such as `PlanetsAndMoons.EARTH_BODY`, filled by the base constructor before the checks of the derived one:
  2000 spacecraft built about the Earth and dropped stayed alive after a collection (20 of 20 sampled), a second
  spacecraft of an id was not listed and moving it about the Moon removed the first, a constructor that threw on its
  masses left its spacecraft listed, and 4000 constructions on 4 threads gave a wrong count in 20 runs of 20, 11 of them
  with an `InvalidOperationException` of the set. The library's own transient spacecraft (avoidance trials, the
  non-linearity index) leaked into the Earth at each analysis. The satellites are now held weakly and by instance
  (`ConditionalWeakTable`, internal `WeakSatellites`), and each constructor lists its item as its last statement, after
  every check; natural bodies and barycentres are listed by their observer as before, a spacecraft by a `CelestialBody`
  observer only, as before. `SatellitesTests` checks the collection, the 20 runs of concurrent constructions, the
  registration on 16 threads released together (on main the set threw or lost entries in each of 5 runs) and the
  throwing constructors by reference.
- **A maneuver whose engine's tank was not the propagated spacecraft's threw mid-run, burnt another spacecraft's fuel or
  pointed the wrong axes** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), [#586](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/586)). Nothing checked the chain before a run: an impulse on a tank added to no
  spacecraft threw `NullReferenceException` at its first burn (`Engine.Ignite`), under RK7(8) and Velocity-Verlet and
  through every path; one on another spacecraft's engine burnt that spacecraft's fuel; an attitude on such an engine took
  the static body axes (90° off with `BodyFront` +X) or the other spacecraft's. The construction of the propagator, and
  `PropagatorBase.Propagate` again, now throw `InvalidOperationException` naming the maneuver, the engine, its tank and
  the spacecraft when a maneuver of the chain draws on a tank that is not one of the spacecraft's tank instances held by
  it, before anything changes (`Spacecraft.Propagate`, `PropagateAsync`, the builder, the constructors,
  `Scenario.SimulateAsync`; `BatchPropagator` reports it in `PropagationResult.Error`). `AddFuelTank` rejects a tank held
  by another spacecraft, which it moved before, and attaches it atomically (8 spacecraft adding one tank at once, 500
  rounds: with a check made before the attachment, a round left the tank to two of them in each of 5 runs). `ManeuverEngineBindingTests` runs the 84 combinations of path,
  integrator, chain and engine, with the controls: the burn against Tsiolkovsky within 1e-12 relative (bit for bit
  those of main) and the prograde front of `BodyFront` +X against the velocity.
- **An instrument of id NaifId × 1000 broke the spacecraft frame** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)). `Add*Instrument` accepted the id of the
  spacecraft frame, and the instrument FK then defined that frame relative to itself: SPICE(BADFRAMESPEC2) at any read
  of the kernel-backed spacecraft. The `Instrument` constructor rejects it with `ArgumentOutOfRangeException`; the ids
  NaifId × 1000 − n of NAIF's convention (NAIF IDs Required Reading, "Instruments") are unchanged.
- **`AddEngine` accepted an engine on a tank equal by value to one of the spacecraft's** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), [#586](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/586)). The check used
  `FuelTank`'s equality on name, model and serial number, so an engine on a tank never added passed, entered
  `GetTotalISP`, and threw `NullReferenceException` at its first burn. It now takes the tank instance added
  (`ArgumentException` otherwise, as for an unknown tank, Changed below).
- **A clock shared by two spacecraft wrote the SCLK of the second** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)). Each constructor attached the clock without a
  check, so a clock given to spacecraft -1101 then -1102 wrote `SCLK_DATA_TYPE_1102` for both, and the CK of -1101 could
  not be written against it (SPICE(KERNELVARNOTFOUND) in `sce2c_c`); a constructor that threw on its body axes kept the
  clock. The constructors and `Opm.ToSpacecraft` reject a clock attached to a spacecraft of another NAIF id with
  `ArgumentException` (another instance of the same id, a kernel-backed twin, writes the same SCLK and is accepted), and
  attach it after every check, atomically (8 spacecraft of different ids built at once with one clock, 50 rounds: with a
  check made before the attachment, a round gave the clock to two of them in each of 5 runs, the SCLK declaring the
  first id).
- **A NAIF id below -2147483 gave a wrong frame id** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)). `SpacecraftFrame` computed NaifId × 1000 in unchecked
  `int` arithmetic: -2147484 gave the frame id 2147483296 and -3000000 1294967296, written in the FK; the library's own
  avoidance trials (from -9000001) and non-linearity index (from -9500001) had such ids. Both constructors reject an id
  below -2147483 with `ArgumentOutOfRangeException`, the frame id is computed checked, and the transient spacecraft
  draw their ids from cycling blocks inside the range ([-2099999, -2000000] and [-2147483, -2100000]); nothing keys on
  them, and the conjunction and non-linearity results are unchanged.
- **A kernel-backed spacecraft read its frame by its own name** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)). Its frame was handed to SPICE as
  `Name + "_FRAME"`, so a twin of another name than the propagated spacecraft (`SatK` for `KSAT_FRAME`), or any twin
  of a mission FK naming the frame of id NaifId × 1000 otherwise (`KSAT_BUS`), threw SPICE(UNKNOWNFRAME). The frame is
  now handed by the name the loaded kernels give to its id (`frmnam_c`, through the new native `GetFrameNameProxy`),
  read at each call so that an FK loaded after the twin is read, and by its own name when no kernel defines the id;
  `Frame.Name` is unchanged. A twin whose own name matched gets the same frame, bit for bit; a twin whose
  `Name + "_FRAME"` was the name of the frame of another id read that other frame without an error, and now reads the
  frame of its own id.
- **Frame kernels with names SPICE cannot read were written** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)). `SpacecraftFrame.WriteAsync`,
  `Instrument.WriteFrameAsync` and `Site.WriteFrameAsync` put the upper-cased name in a kernel variable name
  (`FRAME_<NAME>_FRAME`, `FRAME_<SPACECRAFT>_<INSTRUMENT>`, `FRAME_<SITE>_TOPO`), so 'Demo Sat', 'ISS (ZARYA)', 'Sat,1',
  "Sat'1" and a tab gave SPICE(BADVARASSIGN), 'Sat=1' SPICE(NUMBEREXPECTED), and a variable over 32 bytes (a
  21-character spacecraft name, a 13-character name of 'É') SPICE(BADVARNAME) at `LoadKernels`. The writers now check
  the variable before the file is opened (Kernel Required Reading, "Variable Name Rules": no blank, comma, parenthesis,
  '=' or TAB, 32 characters at most, counted by SPICE in the file's UTF-8 bytes; and no quote, which ends the string
  values), and throw `InvalidOperationException` naming the object, the variable and the rule, writing nothing. The
  instrument kernel writers (`Instrument.WriteKernelAsync`), whose `INS<id>_FOV_FRAME` string names the frame of the
  instrument FK, check its variable by the same rule: a quote ended that string, and `LoadKernels` stopped with
  SPICE(TYPEMISMATCH) (measured on CSPICE N0067, the process ended); a blank or a longer name named a frame no FK can
  define.

- **A spacecraft whose initial orbit is a TLE or a TEME state could not be propagated numerically** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)).
  Every path, `Spacecraft.Propagate` and `PropagateAsync`, `Scenario.SimulateAsync`, `BatchPropagator`,
  `MonteCarloPropagator`, the `CentralBodyPropagator` constructors and the builder, threw `ArgumentException`
  "Spacecraft initial orbital parameters must be defined in inertial frame": TEME, the frame of SGP4, was not in the
  list of inertial frames, and integrating in it, a frame of date, would add fictitious accelerations. A TLE now starts
  from its SGP4 state at the start of the window, a TEME state from itself at its epoch; either is rotated once to ICRF
  (the covariance through the Jacobian, diag(R, R)), on the observer of the initial orbit, and the propagation, its
  `PropagationSolution` and the stored states are in ICRF. The run is bit for bit that of its ICRF twin
  (`TlePropagationTests`: the eight activation paths, Velocity-Verlet and RK7(8), the Moon, the Sun and SRP, a
  covariance with Φ and P, SSB mode, drag on an Earth with NRLMSISE-00, which moves the end by 27.2 m in an hour;
  `MonteCarloPropagatorTests.TemeTemplate_RunsStartFromTheirRotatedSamples`). The central body of a TLE is its Earth, a
  point mass: for the ISS (`21020.53488036`), RK7(8) at 1e-9, the run is 53.663, 275.692 and 1118.386 km from SGP4
  after 1 h, 6 h and 24 h (on the closed form of Kepler within 9.3e-7 m over the hour at 1e-12), and B* is not used;
  from a TEME state on an Earth with EGM2008 at degree 10, 0.555, 0.788 and 7.217 km
  ([Numerical propagation of a TLE](../reference/tle.md#numerical-propagation-of-a-tle)). The conjunction analysis
  reads the TEME P0 rotated: TCA, covariance at TCA, Pc and non-linearity index bit for bit those of the ICRF twin,
  and within 3.1e-16 relative for the covariance held in RTN (`ConjunctionCovarianceEdgeTests`). The target orbit of
  an impulse of such a spacecraft is taken in ICRF, which its constructor now accepts; a target in TEME still
  constructs, and the propagation rejects it when it is built, before it changes anything, naming
  `targetOrbit.ToFrame(Frame.ICRF)` ([The frame of a target orbit](../reference/impulse-maneuvers.md#the-frame-of-a-target-orbit)).
  A set of mean elements in TEME (`tle.ToMeanKeplerianElements()`) is still rejected, as are CIRS and TIRS.
- **A propagated TLE spacecraft read SGP4** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)). `Spacecraft.GetGeometricStateRelativeTo` and the window of the
  stored states tested `InitialOrbitalParameters is TLE` before the stored states. A TLE spacecraft now reads the states
  of a numerical propagation by any path, the builder and a propagator built directly included, which leave
  `IsPropagated` false: `GetEphemeris`, `WriteEphemeris` (read back by `spkezr` within 2.8e-9 m), the window searches
  (the managed field of view against `gftfov` on the kernels it writes within 5e-7 s), the conjunction analysis with
  the check of #527 and the avoidance options on its force model, bit for bit those of its ICRF twin. Unpropagated, or
  sampled by `TLEPropagator`, it reads as before: its geometric state, and every read through it (from another body,
  the window searches), is SGP4 exactly; `GetEphemeris` from its Earth without aberration interpolates the samples of
  `TLEPropagator` (SGP4 at the samples, 7.6e-4 m from it between them for the ISS at 60 s). `Spacecraft.ToOpm` still writes SGP4
  ([#658](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/658)).
- **`astro propagate -t` ended in an unhandled exception** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)). The command built the spacecraft on the TLE, about
  the TLE's point-mass Earth, and the propagation rejected it ("must be defined in inertial frame"); once accepted, it
  would have run without the geopotential and the atmosphere of the CLI (12.3 km from them after 30 min, drag and SRP
  on). It now starts from the SGP4 state at the start of the window, in TEME on the Earth of the CLI (EGM2008 at degree
  10, the degree of `-n` since [#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681), the U.S. Standard Atmosphere 1976 with `-a`, `Helpers.CreateCenterOfMotion`), rotated once to ICRF by the
  library: its SPK is the library run from that state within 6.2e-4 m (`PropagateTests.PropagateFromTle_RunsOnTheCliEarth`).
  `-t` with a centre of motion other than the Earth throws `ArgumentException` naming 399, before anything is written.
- **A TLE read the kernels before it checked its text** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)). `new TLE(...)` built its Earth (radii, GM, frame), the
  first argument of its base constructor, before the null checks and the format checks of its lines. Without kernels a
  malformed TLE threw the kernel error ("Celestial celestialItem frame can't be defined") and CSPICE printed the banners
  of its failed reads on the standard output: its checksum error was never reached. The text is now checked first
  (null or empty lines and name, lengths, line numbers, catalog numbers, checksums), and the Earth is built once
  (`MeanSemiMajorAxis` takes its GM; the second Earth is gone, bit for bit). `ColdStartTests.TleText_IsCheckedBeforeAnyKernelRead`
  checks four malformed TLEs in a process without kernels, with nothing on the standard output. A valid TLE still
  needs the kernels that define the Earth.
- **The velocity of a `TLEPropagator` solution between its samples was up to 49.9 m/s off SGP4** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)). Each step of
  the solution carried zero accelerations, which the cubic Hermite of `PropagationSegment` interpolates the velocity
  with: 4.39 m/s off at mid-step, 48.7 m/s at a quarter step, 49.93 m/s at worst (ISS, 1 h at 60 s). The accelerations
  at the samples are now the central difference of SGP4's velocity at ±1 s: the velocity of `InterpolateAt` and
  `GetStateVector` is 4.28e-4 m/s off SGP4 at worst, the positions (0.376 m, the Hermite's own error) and the states
  at the samples unchanged (`TLEPropagatorTests.InterpolatedVelocity_FollowsSgp4`, against SGP4 through `evsgp4_c`).
- **`astro propagate` ignored `--earth-geopotential-degree` (`-n`): every run used degree 10** ([#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681)). The option was
  declared and read by no code: `PropagateCommand` built the initial orbit through the overload of
  `Helpers.ConvertToOrbitalParameters` that forwards no degree, so the default 10 reached `GeopotentialModelParameters`
  on the `-s`, `-k` and `-q` paths alike, and the SPKs of `-n 2`, `-n 10` and `-n 50` were byte-identical. The degree
  now reaches the Earth of the run, which is the library run of the same scenario with `GeopotentialModelParameters` of
  that degree. A degree above 100, the last degree of the embedded EGM2008 model, is an argument error whatever the centre, checked
  before anything is loaded (the field then padded the degrees beyond its file with zeros, so a wired `-n 150` would have
  run at degree 100; since #689 it throws too); 0 and 1 remain the point mass. The help text, whose parenthesis was not closed, reads "Degree of the Earth geopotential (EGM2008), at most
  100". `PropagateTests.EarthGeopotentialDegree_IsTheDegreeOfTheRun` reads the exported SPK at the end of a 600 s window
  (n = 2 and 50 on `-s`, n = 2 on `-k` from ECLIPJ2000 and on `-q`) against the library run of that degree, within
  3.8e-9 m (twice the worst over Linux, Windows and macOS arm64, 1.04e-9, 1.86e-9 and 1.04e-9 m, CI run 43595592229),
  and more than 1 m from the run of degree 10 (32.660 m for n = 2 and 4.514 m for n = 50 from 6800 km equatorial,
  49.2 m and 60.7 m on the `-k` and `-q` rows); no `-n` and `-n 10` stay
  byte-identical (`DefaultDegree_IsDegree10`). On a 30 min LEO from 6700 km, at 28 min, `-n 2` moves 168.5 m and `-n 50`
  5.80 m from what the CLI wrote before; no other result moves. Since `-t` runs on the Earth of the CLI
  ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)), the degree reaches it too (`Helpers.TleStartState`):
  `EarthGeopotentialDegree_IsTheDegreeOfTheTleRun` reads the SPK of the ISS TLE 600 s from 2021-01-20 13:00 UTC against
  the library run from the SGP4 state in TEME on an Earth of that degree, within the same 3.8e-9 m (1.04e-9 m on
  Linux), and 17.277 m (n = 2) and 3.850 m (n = 50) from the run of degree 10; no `-n` and `-n 10` are byte-identical
  there as well (`DefaultDegree_IsDegree10_FromTle`).
- **`astro sub-point` threw `NullReferenceException` for a site identifier** ([#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681)). It cast the object of
  `Helpers.CreateLocalizable` to `CelestialItem`, which a `Site` is not, and dereferenced the null. A site identifier
  (its body's identifier times 1000 plus its own, 399013 for DSS-13) is now an `ArgumentException` naming the kinds the
  command accepts, a celestial body or a spacecraft; any other identifier goes through `Helpers.CreateCelestialItem`,
  as before for bodies and kernel-backed spacecraft (`EphemerisTests.SubPoint_OfASite_IsAnArgumentError`; the sub-points
  of the Moon and of a kernel spacecraft unchanged).
- **`CelestialItem.ToString` printed GM under a wrong unit, and the mass, GM and radii to 7 significant digits**
  ([#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681)). The lines `Mass (kg)`, `GM (m^3.s^2)` and, in `CelestialBody.ToString`, `Equatorial radius (m)` and
  `Polar radius (m)` used the format `E` (6 decimals): with gm_de440 and pck00011 the Earth's GM was printed 8.9e-8
  relative off, its mass 6.7e-8 off, its equatorial radius 0.400 m and its polar radius 0.100 m off, and the GM label
  had no space before its colon. They are now printed as the flattening and J2 to J4 lines already were, in the
  shortest form that parses back to the property in the invariant culture, under `GM (m^3/s^2) :`; a spacecraft prints
  through the same lines, and `astro celestial-body-info` prints them. `CelestialBodyTests.CelestialBodyToString` and
  `BodyInformationsTests.CelestialBodyInformation`, snapshots before, now parse every value back to its property and
  check GM within 1 ulp of `BODY399_GM` of gm_de440.tpc (gm_de431.tpc for the CLI) times 1e9 and the radii equal to
  `BODY399_RADII` of pck00011.tpc times 1000, read from the kernel text; `SpacecraftTests.SpacecraftToString_RoundTrips`
  does the same for a spacecraft.
- **The release notes and the README named CLI spellings that do not exist** ([#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681)). The TEME entry below cited "the
  CLI `--frame TEME`", an option no command has: the frame is a positional argument of `ephemeris`, `orientation`,
  `frame-converter` and `orbital-parameters-converter`, which all accept `TEME`. The README listed
  `find-windows-from-FOV-constraint`, which `astro --help` lists as `find-windows-from-fov-constraint` (both spellings
  run). Both lines now give the spellings of the tool.

- **The conjunction analysis ignored the secondary's own hard-body radius** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)). The combined radius was the
  protected `HardBodyRadius` plus `SecondaryHardBodyRadiusMeters` of the options (5 m by default), whatever the radius of
  the secondary: a protected spacecraft of 8 m gave 13 m with secondaries of 0, 3 and 8 m alike, and the CDM case of the
  study of #668 (protected 10 m, secondary 3 m) 15 m and a Pc of 3.6145e-4 for 2.7147e-4 with its 13 m, 33 % high, on
  every path (`Screen`, `Analyze`, `AnalyzeAll`, both avoidance paths). `ToCdm` wrote object 2's `AREA_PC` as π·5².
  An option of −5 m gave 3 m, NaN or ∞ a null Pc without a flag, and both `Spacecraft` constructors accepted an infinite
  radius (the kernel-backed one any). The radius of the secondary is now its own when it gives one, the option otherwise,
  5 m when neither does (Changed below); the option of `Screen` is a fallback over its catalogue, never in conflict with
  a candidate's own radius. `Analyze_SecondaryHardBodyRadius_DesignedEncounters_MatchExactPc` splits the
  radius of the seven designed encounters of `tools/collision_probability` between the two spacecraft (0.6 R and 0.4 R,
  no option): the combined radius is their sum and the Pc the exact one of mpmath within 3.15e-10 on the three platforms (CI run 43601908000; bound 6.4e-10 since #668, from the three platforms' three-digit prints, 3.1549e-10 in CI run 43745883709, 6.3e-10 before); a
  theory checks the five rows of the rule on `Analyze` and `AnalyzeAll` (sampled and dense) and both avoidance paths,
  and the fallback rule on `Screen` (the candidate's own radius in the row where the others throw), with a spacecraft
  and a body as secondary; `Screen_MixedCatalogue_EachCandidateTakesItsOwnRadiusOrTheFallback` screens spacecraft of 2, 6 and 0 m and
  the Moon with a fallback of 3 m and with none (two-body and RK7(8)): each combined radius is 8 m plus the candidate's
  own or the fallback, each Pc that of `Analyze` on the pair, and `EvaluateAvoidance` runs on each screened encounter
  with the same radius; and `AREA_PC` is π·3² for a secondary of 3 m.
- **`EvaluateAvoidance` reset the settings of the encounter** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)). Both avoidance paths re-analyzed each option with
  new `ConjunctionAnalysisOptions` built from the four values `AvoidanceSearchOptions` mirrors, every other setting at
  its default: an encounter analyzed with a stale threshold of a day, a secondary radius of 0 m and no low-speed
  threshold had a combined radius of 8 m and no flag, each of its 10 options 13 m and `StaleCovarianceUsed`. The options
  are now re-analyzed with the settings of the encounter, but for the values the avoidance options give (Changed
  below); `AvoidanceFallbackTests` and `AvoidanceCovarianceTests` check the radius and the flags of every option on the
  two-body, Velocity-Verlet, RK7(8) and propagated-covariance paths, for encounters of `Analyze` (both paths),
  `AnalyzeAll` and `Screen` (a low-speed threshold of 100 km/s raising the flag on each option, the stale threshold
  raising it or not by the age of each covariance), that on the two-body path each option's TCA lies on the grid the
  encounter's sample step, maximum search step and bisection tolerance of 10 s give (the closed form of the sampled
  search: (j + 1/2) w after the burn, w = 7.5 s and 6.25 s on the two rows, 5 s with the avoidance defaults), and that
  null mirrors give, bit for bit, the options of the encounter's values given explicitly.
- **`Analyze` and `AnalyzeAll` accepted the protected spacecraft as its own secondary** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)): the midpoint (or on two
  propagations the window start) with a miss of 0 m and a Pc of 0.815, an empty list on two propagations for
  `AnalyzeAll`. A secondary with the NAIF identifier of the protected spacecraft, the rule by which `Screen` skips it,
  now throws `ArgumentException` naming `secondaryObject`, before any read of the states.
- **`Analyze` and `AnalyzeAll` accepted a trajectory propagated for another spacecraft** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)): another spacecraft's
  RK7(8) trajectory gave a miss of 0 m and a Pc of 0.676 for 176.70 m and 6.7e-78, a swapped pair the right miss with
  the covariances of the other objects; Velocity-Verlet and `TLEPropagator` solutions alike. A solution now records the
  spacecraft it propagated (every integrator and activation path of `PropagatorBase`, and `TLEPropagator`), and the two
  `PropagationSolution` overloads throw `ArgumentException` naming `protectedTrajectory` or `secondaryTrajectory` for
  one of another instance (checked for `Spacecraft.Propagate` in RK7(8) and Velocity-Verlet, `CentralBodyPropagator`,
  its builder, `BatchPropagator` and `TLEPropagator`); a solution built by hand is accepted, bit for bit.
- **The label of a TCA depended on the search path** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)). On a UTC window the sampled search labelled its TCA in UTC
  and the search on two propagations in the TDB of the step epoch that opened its bracket (455 of 540 UTC windows around
  27 minima, the other 85 in UTC): the same instant, a tick apart at most, yet unequal by `==`. A dense TCA is now
  labelled in the time frame of the window start, never a window end: with a `BisectionToleranceSeconds` below a tick
  (the default) the tick of that frame nearer the zero of the range rate; with a tolerance of a tick or more, the middle
  of its last bracket converted to that frame, or the neighbouring tick toward the zero where the range rate is smaller
  in magnitude (one tick at most from that middle); `DenseTca_IsLabelledInTheWindowScale` and `DenseTca_InTheWindowScale_IsTheTickNearerTheZero` check UTC,
  TT and TAI windows on RK7(8) and Velocity-Verlet pairs (the TCA 1.38e-7 s from the Keplerian zero on Linux x64, Windows x64 and macOS arm64, CI run 43601908000, bound 2.8e-7 s), a TDB
  window bit for bit. The conjunction cases of the conformance suite pass, their `tca_s` moved by 8.7e-8 s at most and
  their relative position by 0.30 mm at most (see [Validation](../guides/validation.md)).
- **The CDM declared any secondary spacecraft maneuverable** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)): `MANEUVERABLE` was `YES` for object 2 whenever it
  was a `Spacecraft`, an engineless one or a debris TLE wrapped in one included. It is now `YES` for a spacecraft with an
  engine and `N/A` otherwise ("not available" or "not applicable", CCSDS 508.0-B-1 Cor. 2, 1.4.3.1), the value given by
  `SecondaryParticipantMetadata` still winning; each written CDM is valid against the bundled NDM/XML 4.0.0 schema and
  reads back the same value.

- **`MemoryDataProvider` threw exception types SPICE does not throw, so a body that builds on SPICE did not build
  from memory** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). A target without stored states or read outside them, and a frame read outside its stored
  orientations, threw `ArgumentException`; an identifier without body information threw `KeyNotFoundException`. The
  library's handlers of missing data catch the `InvalidOperationException` of SPICE only, so `new CelestialBody(399)`
  on a provider holding the Earth's information alone threw `KeyNotFoundException` ('3'), and the orientation cache of
  a propagation over a memory frame threw from its constructor as soon as a buffer point lay before the first stored
  orientation. The state and orientation reads now throw `InvalidOperationException` naming the item and the epoch, the
  contract of the SPICE provider; a frame never added keeps `ArgumentException`. An identifier without information is a
  bad argument (decided by S. Guillet, 2026-11-14): `GetCelestialBodyInfo` throws `ArgumentException` naming `naifId`, the
  type and the parameter of `SpiceDataProvider` for an identifier SPICE does not name or that is not a celestial body's
  ([#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672)), and so do `new CelestialBody(id)` and `CelestialItem.Create(id)` of that identifier on both providers
(`CelestialItem.Create` of a Lagrange point, 391 to 395, reads its state and not its information: its missing state
throws `InvalidOperationException` on both, as SPICE for L3, whose state `Data/SolarSystem` lacks). A planet,
  moon, barycentre or the Sun whose barycentre's data, information included, the provider lacks builds with
  `InitialOrbitalParameters` null and an infinite sphere of influence, as the Earth built on SPICE outside de440s (SPICE
  names every barycentre), and the cache keeps the points the entries do not reach unavailable. `MemoryDataProviderTests`
  checks the types of the state and orientation reads against those of `SpiceDataProvider` for the same reads, and the
  exception type and parameter name of seven unknown identifiers (four celestial ones SPICE does not name, a spacecraft's,
  50 and 1000) against `SpiceDataProvider`'s; `MemoryDataProviderBodyTests` builds
  the Earth from one, two or three infos, a day before its stored states and within them, and the Moon, in a child
  process (the test process never swaps the global provider, #631), checks the same identifiers through
  `GetCelestialBodyInfo`, `new CelestialBody(id)` and `CelestialItem.Create(id)` against SPICE, the Lagrange points L1
  to L5 among them, and reads the cache over a
  memory frame between two grid points against the closed-form rotation, within 1.15e-16 rad (Changed, Breaking, below).
- **`ToEquatorial()` and `new Equatorial(StateVector)` ignored the state's frame** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). They took atan2 and asin
  of the position in whatever frame it was expressed in, so a state in ITRF93 gave an Earth-fixed longitude, one in
  ECLIPJ2000 an ecliptic longitude and latitude, a TLE (TEME) a right ascension up to 0.32° off; the Moon from the Earth
  over four epochs from 2000 to 2024 was up to 166.4° off in right ascension in IAU_MOON and MOON_ME, 24.7° to 123.1° in
  ITRF93. The position is now rotated to ICRF at the state's epoch, the rotation of `ToFrame`, before the angles; a state
  in ICRF is taken as it is, bit for bit, and the distance is that of the position as given. The coordinate searches of
  `CoordinateSystem.RaDec` keep the angles in their search frame, as CSPICE `gfposc`, through an internal helper.
  `EquatorialTests` reads the Moon from the Earth in ICRF, ECLIPJ2000, ITRF93, IAU_MOON, MOON_ME and TEME at four
  epochs against the closed form of RA and Dec on SPICE's J2000 state (`spkezr_c`): within one ulp of the angle in RA
  (4.4e-16 rad) and 1.1e-16 rad in Dec; Keplerian elements in ECLIPJ2000 against the closed-form obliquity rotation of
  their state, within one ulp; a TLE (TEME) against the ICRF state of `tools/teme_reference`, within 1.1e-13 rad in RA
  and 5.9e-11 rad in Dec, the residual of the SGP4 state and of the TEME rotation; the ICRF pole and equator expressed
  in ECLIPJ2000 give Dec π/2 exactly and 0 within 6.7e-17 rad (Changed, Breaking, below).
- **`Time.AddDays`, `AddHours` and `AddMinutes` of NaN returned the epoch, `CreateFromJD(NaN)` J2000** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). They
  forwarded to `DateTime`, whose range check is false for NaN, which it converts to 0 ticks; the infinities threw with
  `DateTime`'s parameter name, `value`. A non-finite argument now throws `ArgumentOutOfRangeException` naming `days`,
  `hours`, `minutes` or `julianDate`, as `AddSeconds` already did; a finite argument gives the result of `DateTime`, bit
  for bit, still truncated to the tick ([#637](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/637)). The command line's epoch `NaN JD` ran at J2000 and is now
  rejected. `DateTimeTests` checks the four methods in UTC and TDB, and 1e5 finite arguments against `DateTime`'s
  arithmetic (Changed, Breaking, below).
- **`Time.Create(year, doy, sec)` checked nothing** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). Year 0 was read as 2000, and a day or seconds outside
  their range rolled into the neighbouring day or year: `Create(2023, 366, 0)` was 2024-01-01, `Create(2024, 1, -1)`
  2023-12-31T23:59:59Z. It now throws `ArgumentOutOfRangeException` naming `year` outside [1, 9999], `doy` outside the
  365 or 366 days of its Gregorian year, or `sec` not finite or outside [0, 86400), or, on 9999-12-31, above
  86399.99999995 s, whose nearest tick, 86400 s, no `DateTime` holds (it threw with `DateTime`'s parameter name,
  `value`); a valid input builds as before.
  `DateTimeTests` checks the bounds against the Gregorian rule (day 60 is 29 February in 2000, 1 March in 1900) (Changed,
  Breaking, below).
- **`Matrix.Equals` took NaN as equal to any value, and equal matrices hashed differently** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). An element pair
  differed only when |a − b| > `Double.Epsilon`, which is false for NaN and for two values one smallest subnormal apart:
  a zero matrix equalled the same matrix with NaN at [0, 0], which equalled the matrix with 1 there, while the zero
  matrix did not, and the hash codes of the first two differed. The elements now compare by `double.Equals`, the
  semantics of `Vector3` and of the record structs: NaN equals NaN only, +0 equals −0. `MatirxTests` checks the
  reflexivity, symmetry and transitivity of `Equals` and the hash contract over the matrices of {0, −0, 4.9e-324, 1, NaN,
  ±∞}, against `double.Equals` pair by pair. The records that hold a `Matrix` (`EncounterState`, `CollisionRisk`, the
  CDM models) follow (Changed, below).
- **`Normalize()` of a zero `Vector3` or `Quaternion` returned zero silently, and a spacecraft accepted zero body axes**
  ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). `Vector3.Zero.Normalize()` was the zero vector, a vector rotated by a normalized zero quaternion was zero,
  and a `Spacecraft` with `bodyFront` +Y and the two other axes zero was accepted with zero axes. The public
  `Normalize()` of both types now throws `InvalidOperationException` for a magnitude of 0; the library's own
  normalizations keep their behaviour, bit for bit, through an internal helper (a zero angular velocity rotates nothing,
  a coincident target is at angle 0, collinear Lambert positions throw `ArgumentException`), and the `Spacecraft`
  constructor throws `ArgumentException` naming a zero `bodyFront`, `bodyRight` or `bodyUp`. `VectorTests` and
  `QuaternionTests` check the throw and, for 1e4 vectors of magnitude 1e-150 to 1e150 and 1000 quaternions, a unit
  result parallel to the input, the quaternion's rotation against its matrix; `StateOrientationTests`,
  `LambertSolverTests` and `CelestialBodyTests` the kept conventions; `SpacecraftTests` each zero axis (Changed,
  Breaking, below).
- **A spacecraft accepted body axes that are not finite** (fourth round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The constructor rejected only an axis of
  magnitude 0 (#680, above): an axis with a NaN or infinite component has a magnitude of NaN or +∞, so it was
  normalized to NaN components (`bodyFront` (0, NaN, 0) gave (NaN, NaN, NaN), (0, +∞, 0) gave (0, NaN, 0)), and every
  orthogonality and handedness comparison of a NaN is false, so the spacecraft was built with NaN axes. Its attitudes
  were then NaN (`ProgradeAttitude` and `CreateLVLH` with a NaN front, `CreateLVLH` with a −∞ up) or silently wrong where
  the law reads another axis (the identity for `ProgradeAttitude` with a NaN right). The `Spacecraft` constructor now
  throws `ArgumentException` naming the first of `bodyFront`, `bodyRight` and `bodyUp` with a component that is not
  finite, before the clock is attached, so a throw leaves the clock free (#676). Every finite axis is unchanged, bit
  for bit. `SpacecraftTests` checks NaN, +∞ and −∞ in each axis, given alone or with the others, the parameter named,
  and that the clock is free after the throw.
- **`TriadAttitude.CreateLVLH` and `CreateProgradeWithSunTracking` pointed the static body axes** ([#677](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/677)). A
  spacecraft built with `bodyFront`, `bodyRight` and `bodyUp` got `Spacecraft.Down` and `Front` (LVLH), or `Front` and
  `Up` (prograde with Sun tracking), whatever its own axes: with the front +X, the right −Y and the up +Z its front was
  90° from the velocity under both factories; with the front +Z, the right +X and the up −Y both axes were 90° off.
  The single-vector attitudes and the attitude of an impulse read `BodyFront` of the spacecraft that carries the
  engine's tank. The factories now read its `BodyDown` and `BodyFront`, or `BodyFront` and `BodyUp`, at execution, the
  static axes while the tank is on no spacecraft (a read outside a propagation only: a propagation of an attitude whose
  tank is not the propagated spacecraft's throws, [#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)), and `PrimaryBodyVector` and `SecondaryBodyVector` return them; no
  public signature changes. `TriadAttitudeTests` compares the attitude of three right-handed triads (one turned 30°
  about (1, 1, 1)) with the closed form of each law built from the state, the spacecraft's axes and the Sun of `spkezr`
  (within 3.6e-16 rad on Linux, Windows and macOS arm64), and `AttitudeRecordTests` the record and `pxform` on the written CK with the same closed
  forms over both integrators, ICRF and ECLIPJ2000 starts, `Spacecraft.Propagate`, SSB mode and `Scenario`, leading,
  chained after an impulse, and an LVLH that an impulse interrupts and the timeline resumes (60 cases within the limits
  of the attitude grid); a factory attitude on a tank of no spacecraft, or of another spacecraft, is rejected by
  `Spacecraft.Propagate` before it runs (`TriadAttitudeTests`, #676). A spacecraft with the default axes is bit for bit.
  See Changed.
- **`CombinedManeuver`'s target orbit carried the current orbit's perigee radius** ([#677](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/677)). The target orbit was built
  with `perigeeRadius:` the perigee of the orbit before the burn (6 678 000 m for the orbit of `CombinedManeuverTest`,
  where the target's own perigee is 42 163 999.919 m or 6 600 000 m), so its `PerigeeRadius()` returned another orbit's.
  Only its velocity was read, which ignores that value on an ellipse, so no result moved. The argument is dropped; the
  ΔV, the windows and the fuel are bit for bit, the conformance case `maneuver/010` included. `CombinedManeuverTest`
  checks the target's perigee against a(1 − e) and min(target, apogee), and |ΔV| against vis-viva and the law of cosines
  of a combined burn at the apogee on the line of nodes (within 5.0e-16 relative on Linux, Windows and macOS arm64).
- **The documentation called the targets of `ApogeeHeightManeuver`, `PerigeeHeightManeuver` and `CombinedManeuver`
  heights or altitudes** ([#677](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/677)). The maneuvers tutorial said that `ApogeeHeightManeuver` raises the apogee "to a target
  altitude", and the impulse-maneuver reference that the apogee and perigee maneuvers raise the heights; the targets are
  radii in m from the centre of `ManeuverCenter`, and the burn moves the apsis up or down. From a 400 km circular
  orbit, `1000e3` read as an altitude is a radius 5 378 km under the surface and ends the propagation in
  `InsufficientFuelException`. The tutorial, the reference and the XML documentation of the three constructors now say
  "radius from the centre, in m" (R + h for an altitude h). `ApogeeHeightManeuverTests.TargetIsARadiusFromTheCentre`
  checks R + 1 000 km against the Hohmann first burn and the apogee radius after it by vis-viva (within 1.33e-14 and
  1.01e-15 relative on Linux, Windows and macOS arm64). No code changes.
- **Atmospheric drag asked about an Earth without an atmospheric model was dropped** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). A propagation built
  drag only for listed bodies with `HasAtmosphericModel`, which no predefined Earth has, so `includeAtmosphericDrag: true`
  about `PlanetsAndMoons.EARTH_BODY` changed nothing (0.000 m against the same run without drag, on every path and both
  integrators), a Moon-centred run with drag and no atmosphere ran without an error, and the CDM of such a run wrote
  `ATMOSPHERIC_MODEL = NONE`. The drag of a central body is now that of its atmosphere: its model, or for an Earth
  without one NRLMSISE-00 at nominal space weather, the model `GetAtmosphere` already gave it; another central body
  without a model throws. An Earth always has at least that atmosphere (decided by S. Guillet, 2026-11-14): a listed
  Earth without a model, in SSB mode or beside another central body, adds its drag, where it was dropped too (Changed
  below). `ForceModelDefectTests` checks on every entry point (the constructors, the builder, `Spacecraft.Propagate` and
  `PropagateAsync`, the three `Scenario.SimulateAsync`, the batch) that the run about `EARTH_BODY` equals bit for bit the
  run about an Earth built with `new Nrlmsise00Model(SpaceWeather.Nominal)`, the force and its partials too, and that a
  parallel batch equals its serial runs; and on nine paths, both integrators, that a listed `EARTH_BODY` in SSB mode
  and beside a Moon central body with a model equals bit for bit the run listing that explicit Earth.
- **A `GeopotentialModelParameters` built one body** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). The parameters kept the caller's `StreamReader`, which
  the first body read to its end and closed, so a second `new CelestialBody(..., parameters)` threw
  `ObjectDisposedException`. The coefficients are now read once, behind a thread-safe `Lazy`, and shared, read only, by
  the fields of every body built from the parameters, each with its own buffers. `GeopotentialModelParametersReuseTests`
  checks the acceleration and its partials of three bodies of one instance at 20 positions (LEO to GEO, on and near the
  polar axis), path and stream, degree 10 and 70, both central terms, bit for bit against a fresh instance, and eight
  threads sharing one instance. No result moves.
- **The central body left out by the constructors, and a listed twin ignored** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). The constructors of
  `CentralBodyPropagator` built the forces from the listed bodies only: an unlisted observer gave no drag and no shadow
  (4.76 km of drag over a day with US 1976 in LEO, 1.87 m of SRP shadow), and a listed body of the observer's NAIF
  identifier was dropped as a third body whatever its models, so a listed EGM2008 Earth moved an orbit on `EARTH_BODY`
  by 0.000 m instead of 4.9e5 m in 6 h, and a listed atmospheric twin gave its drag under the other Earth's gravity.
  `Scenario.AddCelestialItem` dropped a second body of one identifier silently. The central body now takes part on every
  path with the models of the observer, inserted first when it is not listed (the builder's rule), and bodies of one
  identifier with different models throw (Changed below). `ForceModelDefectTests` checks that an unlisted central body
  equals bit for bit the run that lists it first, on six paths, both integrators, from ICRF, ECLIPJ2000 and SSB states
  and from empty and null lists.
- **`CelestialBody.GetAtmosphere` ignored the body's model** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). With a full context it returned NRLMSISE-00 at
  nominal space weather for any Earth: 5.24e-12 kg/m³ at 400 km for an Earth with `EarthStandardAtmosphere` (2.80e-12,
  the 2.803e-12 of the U.S. Standard Atmosphere 1976 table) or with NRLMSISE-00 at F10.7 250 and Ap 100 (1.82e-11), while
  `GetAirDensity` returned 0 on a model-less Earth. The configured model now answers every getter, and a model-less Earth
  with a full context the NRLMSISE-00 of its drag; with a context that holds the altitude without the geodetic position
  or the epoch (the altitude overloads included), where it returned NaN, 0 and 0, it answers with the U.S. Standard
  Atmosphere 1976, its base atmosphere (an Earth always has at least the base atmosphere, decided by S. Guillet,
  2026-11-14). `CelestialBodyAtmosphereTests` checks the US 1976 Earth against table I from 86 to 1000 km (within the
  5e-4 of `EarthStandardAtmosphereReferenceTests`), a configured NRLMSISE-00 against its own values, details included,
  the one atmosphere of a model-less Earth across its getters and its drag, and the altitude-only and partial contexts
  of a model-less Earth: `EarthStandardAtmosphere`'s own values bit for bit, and table I within the 4.2e-4 the model
  states at 0, 11, 86, 400 and 1000 km.
- **A reused integrator kept the forces of every run** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). The force-building paths added their forces to the
  given integrator, which nothing cleared: a second run with one `RK78Integrator` held 2 forces and ended 8.37e6 m from
  the first (8.23e6 m with a `VVIntegrator`), a second `CentralBodyPropagatorBuilder.Build()` doubled them, and a batch
  whose factory returned one instance raced on its list. These paths now take the integrator atomically and throw
  `ArgumentException` (`integrator`) when it holds forces or was taken before (Changed below).
  `ForceModelDefectTests` checks every path, and a parallel batch of 8 tasks sharing one instance, 20 times: one task
  runs, bit for bit as with a fresh instance, the others fail.
- **A propagation step of zero or less was not checked** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). RK7(8) returned 0 states for a 0 s step and 1 state for
  −60 s without an error, Velocity-Verlet threw List's `capacity` message from inside the integration, and a Monte Carlo
  campaign returned every run failed. Every entry now throws `ArgumentOutOfRangeException` naming the step as the caller
  passed it, before anything changes (Changed below).
- **An OPM read without an observer was about the Earth whatever its `CENTER_NAME`** ([#679](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/679)). `Opm.ToStateVector()` and
  `ToSpacecraft()` built the Earth and never read `CENTER_NAME`, so a lunar state that `Opm.CreateFromStateVector` wrote
  (`CENTER_NAME = MOON`) read back as an Earth orbit: a 956 km and e 0.988 instead of 1885.08 km and 0.0079, and an SSB
  state a = −443 km. The position and velocity were exact; only the centre was wrong. The observer is now the centre
  `CENTER_NAME` names, looked up, trimmed and in any letter case, in an internal table (`CCSDS/Common/CcsdsCenterNames`)
  of the SPICE names of the barycenters 0 to 9, the Sun and the 161 planets and moons of `PlanetsAndMoons`, the names
  the writers emit, and of NAIF's built-in synonyms of the barycenters (`SSB`, `SOLAR_SYSTEM_BARYCENTER`, `EMB`,
  `EARTH MOON BARYCENTER`, `EARTH-MOON BARYCENTER`, `<PLANET>_BARYCENTER`); a `Barycenter` for 0 to 9, otherwise a
  `CelestialBody` in ICRF at the epoch, as before for the Earth. The ids are literals: no kernel read, no type
  initializer (#623). `OpmTests` reads the lunar OPM about the Moon, its semi-major axis the vis-viva of the Moon's
  kernel GM within 1e-12 relative, and maps each name of a theory to the id CSPICE's `bodn2c_` gives it;
  `CcsdsCenterNamesTests` checks every entry against the name SPICE gives its id (`GetCelestialBodyInfo`), and all 186
  names agree with `bodn2c_` of CSPICE N0067. An EARTH OPM reads as before, bit for bit.
- **`Omm.ToTle` turned any OMM with TLE parameters into an Earth TEME TLE** ([#679](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/679)). `IsTleCompatible` checked only the
  `tleParameters` block, and `ToTle` built the Earth and TEME whatever `CENTER_NAME`, `REF_FRAME` and
  `MEAN_ELEMENT_THEORY` said, so a MOON, ICRF, DSST or SGP4-XP OMM gave a TLE describing another orbit (the same line 2
  for every one of them), `Omm.LoadFromString` loaded them without a word (the validator's warnings for ICRF and an
  unknown theory are not seen by a `Load` caller, and it says nothing of the centre), and `ToString` marked them
  ` (TLE)`. A TLE holds SGP4 mean elements of an Earth orbit in TEME, and the library runs the classic SGP4 (CSPICE
  `evsgp4_c`): `IsTleCompatible` now also requires `MEAN_ELEMENT_THEORY` SGP4, `REF_FRAME` TEME and a `CENTER_NAME`
  that names the Earth (in the table of the OPM centre above, so `earth` and ` EARTH ` are the Earth, `EARTH
  BARYCENTER` is not); each keyword is read trimmed and in any letter case, so a padded ` TEME ` or `SGP4 ` converts as
  before. `OmmTests` checks seven rows of centre, frame and theory, built and loaded, and that the lower-case and
  padded twins of an EARTH/TEME/SGP4 OMM, built and loaded from `iss.omm`, give its TLE lines.
- **A document holding several messages loaded its first one silently** ([#679](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/679)). The OMM, OPM and CDM readers took the
  first message element (`FirstOrDefault`) and dropped the others: an `<ndm>` of two OMMs in the form of a Space-Track
  catalog (ISS and CSS) passed schema validation (NDM/XML allows several messages in an `<ndm>`) and loaded as the ISS
  through `Omm.LoadFromFile`, `LoadFromString`, `LoadFromStream` and `OmmReader`, `Omm.TryLoadFromFile` returned true
  with no error, and `OmmValidator.ValidateFile` reported it valid (0 errors, 119 warnings), from the content of the
  first OMM; the same for two OPMs and two CDMs, and for two messages nested under another root. Each reader now counts
  the messages of its own type below the root, at any depth (internal `CCSDS/Common/CcsdsMessageLocator`; a second
  message below a child of an `<ndm>`, which the schema rejects, counts too), and throws its parse exception
  (`OmmParseException`, `OpmParseException`, `CdmParseException`) naming their number when there are two or more; the
  message read is, as before, the root message, the child of an `<ndm>` or the descendant of another root. A mixed NDM
  of one OMM and one OPM still gives each reader its message. The tests build the documents and count their messages
  with System.Xml.Linq, independently of the readers, and reach every path: `Load*` with and without schema
  validation, `Load*` and `ReadFrom*` from a file, a string and a stream, `TryLoadFromFile`, `ValidateFile` and
  `Cdm.ReadFromFile`/`ReadFromString`/`ReadFromStream`.
- **`new Quaternion(axis, angle)` took its axis as it was** ([#688](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/688)). The constructor returned
  (cos(a/2), sin(a/2) axis) without checking or normalising the axis: Euler-Rodrigues parameters are a unit quaternion
  only for a unit axis, and a vector rotated by q p q* is scaled by |q|², so `new Quaternion((0, 0, 2), π/2)` had a
  magnitude of 1.58 and turned X to (−1.5, 2, 0), 126.87° instead of 90°, and a zero axis gave a quaternion of
  magnitude 0.707 that turned X to (0.5, 0, 0), whose `Normalize()` was the identity; a NaN component gave a NaN
  quaternion. The constructor now divides the axis by its magnitude (scaled by its largest component outside about
  1e-150 to 1e150) and throws `ArgumentException` (parameter `axis`) for a zero axis or a component that is not finite.
  The library's own axis-angle quaternions (`Instrument`'s Euler angles, `PlaneAlignmentManeuver`, the SLERP of the
  attitude record, `StateOrientation.AtDate`, whose zero angular velocity is a zero axis at a zero angle) go through an
  internal factory, bit for bit as before. `QuaternionTests` checks 10 000 random axes of 1e-10 to 1e10 and angles in
  [−2π, 2π) against Rodrigues' formula (worst 6.4e-16 rad, |q| within 3.3e-16 of 1) and against CSPICE `vrotv` and
  `axisar` (rotation matrix within 8.9e-16), the table of the issue, and the zero and non-finite axes.
- **Public math methods that normalized leniently returned silently for a zero or non-finite input** (the known
  limitation of [#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)). #680 made `Vector3.Normalize()` and `Quaternion.Normalize()` throw for a magnitude
  of 0 and moved the library's calls to the internal lenient helper, which four public methods also used on their own
  argument or result: `Matrix.FromQuaternion` of the zero quaternion was the identity (so `Matrix.TransformCovariance`
  returned the covariance unrotated) and of a NaN quaternion all NaN; `Quaternion.Lerp` from q to −q at t = 0.5, or
  to the zero quaternion at t = 1, was the zero quaternion; `Quaternion.SLERP`, which never normalized its inputs, gave
  a quaternion of magnitude 0.707 toward the zero quaternion and the SLERP of the quaternions, not of their rotations,
  for non-unit inputs (`SLERP(2 I, 3 Rx(90°), 0.5)` 0.075 off Rx(45°)); `Vector3.Angle(vector, plane)` and
  `Angle(vector, Plane)` with a zero normal gave the unsigned 0 or π, the sign lost (π from X to (−1, 1e-300, 0)
  where the normal −z gives −π/2 to Y); `Vector3.To` returned the identity for a zero vector, NaN for vectors whose
  squared lengths overflow (1e200) and the zero quaternion or a non-unit one for lengths below about 1e-150, and two
  vectors collinear and opposite in doubles whose `Angle` is not exactly π (a × b = 0 exactly) missed its half-turn
  branch: w = a · b + |a| |b| was 0, giving the zero quaternion, or a rounding residue, giving the identity (7309 of the
  29 857 such pairs of 100 000 b = −k a, 1102 of them zero quaternions; review of this change). Under the same
  signatures, `FromQuaternion` and `TransformCovariance` throw
  `ArgumentException` (`q`, `rotation`) for a zero or non-finite quaternion; `Lerp` throws `InvalidOperationException`
  for a zero or non-finite instance and `ArgumentException` (`q2`) for a zero or non-finite `q2` or a chord through
  zero; `SLERP` normalizes both inputs, with the same exceptions (`q`); `Angle(vector, plane)` throws
  `ArgumentException` (`plane`) for a zero or non-finite normal; `To` throws `InvalidOperationException` for a zero or
  non-finite instance and `ArgumentException` (`vector`) for a zero or non-finite target, divides vectors outside about
  1e-150 to 1e150 by their largest component, and gives a half turn for vectors collinear and opposite in doubles.
  Every non-degenerate result of `FromQuaternion`, `TransformCovariance`, `Lerp`, `Angle` and `To` is unchanged bit for
  bit. The library's own interpolation of the orientation cache of a propagation, the signed angle of
  `ApsidalAlignmentManeuver`, the single-vector attitudes and the attitude of an impulse (`To` of the body front onto a
  position, velocity, angular momentum or delta-V, which keeps the identity for a zero target, a zero delta-V included)
  go through internal unchecked paths, bit for bit but
  for the half turn of collinear opposite vectors, where they recorded the zero quaternion or the identity. `MatrixTests` checks `FromQuaternion` of 10 000 random quaternions of 1e-10 to 1e10 against the
  closed form of q / |q| (exact) and CSPICE `q2m` (1.1e-15), `QuaternionTests` the closed forms of Lerp along q to −q
  and of `SLERP(2 I, 3 Rx(90°), 0.5)` = Rx(45°) (exact), `VectorTests` the signed angle ±π/2 about normals of 1e-100 to
  1e100, `To` from x̂ to ŷ (Rz(90°)) and to −x̂ (a half turn) at lengths of 4.9e-324 to 1e200 and the 29 857 collinear
  opposite pairs against the closed form of a half turn (4.0e-16 rad), and each throw; `ImpulseManeuverTests` an
  impulse of zero delta-V, whose attitude stays the identity.
- **`PerigeeVector()` and `ApogeeVector()` of a near-circular orbit were zero, or off the orbit plane** ([#686](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/686)). Both
  normalized, leniently, the eccentricity vector recomputed from the state, v × h / GM − r̂, and put the perigee on x̂
  only for an eccentricity of exactly 0. Near the rounding level of the eccentricity the two unit terms cancel: the
  state of the issue, a circular orbit of 7178 km inclined by 30° (e = 1.1e-16), had a zero perigee and apogee vector,
  infinite `PerigeeVelocity()` and `ApogeeVelocity()`, and gave `PerigeeHeightManeuver` and `ApogeeHeightManeuver` a
  target of 0 (and `Launch.GetInertialInsertionVelocity` an infinite velocity); 13 to 37 of 2000 random elements at
  e = 1e-18 to 2e-16 had a zero vector, and the direction was off the SPICE conic by up to 3.12 rad at e ≤ 2e-16 and
  9.6e-7 rad at 1e-9; a circular state, whose eccentricity vector is rounding noise with a part out of the plane, had
  its perigee off the plane (|p̂ · ĥ| up to 1.0), and the x̂ of e = 0 was out of the plane of an inclined orbit.
  Under the same signatures, the apsides are a(1 − e) P̂ and −a(1 + e) P̂ with P̂ the perifocal axis of the elements,
  (cos Ω cos ω − sin Ω sin ω cos i, sin Ω cos ω + cos Ω sin ω cos i, sin ω sin i), for `KeplerianElements`,
  `EquinoctialElements` and `TLE`, and for a `StateVector` the part of its eccentricity vector in its orbit plane,
  normalized, or its unit node vector when that part is zero (x̂ for an equatorial orbit); the apsis velocities stay
  vis-viva at the vector's radius, never 0 now. Mean `KeplerianElements` and `EquinoctialElements` keep throwing, as
  `ToStateVector()` and `EccentricityVector()` do, now at e = 0 too; a `TLE` takes the axis of its osculating elements
  at its epoch, the elements its other members return. `KeplerianElementsTests` and `EquinoctialElementsTests` check 13
  eccentricities from 0 to 0.95, 2000 sets of angles each, against CSPICE `conics` at M = 0 and π (perigee within 3.6e-16 rad and 4.4e-16 of its radius, apogee its opposite at a(1 + e); the equinoctial elements within 1.4e-15 rad and 4.1e-15 with the rounding of their conversion);
  `StateVectorTests` 10 000 near-circular states (|p̂ · ĥ| 2.8e-16, against 1.0 before), states with a zero eccentricity vector (the node, exactly), and
  elliptic states against `oscelt` then `conics` (7.1e-10 rad at e = 1e-6 to 1.7e-15 at 0.95, the conditioning of the eccentricity vector); the maneuver and launch tests the state of the issue
  (apsis radii and velocities within an ulp of 7178 km and √(GM / r)); `KeplerianElementsTests` the throws of mean
  elements at e = 0, 0.01 and 0.7, and the apsides of the ISS TLE and of a TLE of e = 0.72 against the periapsis of their
  SGP4 state by `oscelt` then `conics` (1.8e-13 rad); each figure the worst of Linux x64, Windows x64 and macOS arm64
  (CI run 43745883709).
- **A rectilinear state had an RTN frame with zero rows and the elements of an equatorial orbit** ([#687](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/687)).
  `StateVector.CreateRtnRotation` built its rows with the lenient normalization, so for r × v = 0 (a radial motion, a
  zero velocity, the centre) the T and N rows were zero (and the R row at the centre): `FromRtn((0, 1, 0))` was the zero
  vector and `RotateCovarianceToRtn` of the identity diag(1, 0, 0, 1, 0, 0), through `ToRtn`, `FromRtn` and both
  covariance rotations, so the conjunction analysis and the CDM export of such a participant used a singular rotation;
  `Inclination()` was atan2(0, 0) = 0, so the node was x̂ (Ω = 0) and ω = π, and `ToKeplerianElements` and
  `ToEquinoctial` gave an equatorial orbit, the node vectors ±x̂, the longitudes 0 or a number, and the two-body motion
  to another epoch (`AtEpoch`, `ToStateVector(Time)`) a mean anomaly or a state of NaN; nothing threw. Under the same
  signatures, every member that reads the orbit plane or the RTN frame throws `InvalidOperationException` for a
  rectilinear or non-finite state (the condition of `RtnProcessNoise`, r × v = 0 exactly), the list of the Breaking
  entry below; the guard is in `Inclination()`, which the node, the argument of periapsis, the node vectors, the
  longitudes and the elements read, and in the RTN rotation; `Eccentricity()` = 1 and `TrueAnomaly()` = π of a moving
  state off the centre, the limits of every nearby orbit, are kept. `StateVectorTests` checks every listed member, each on a fresh state, on the four
  states of the issue, a state moving through the centre and a non-finite one, CSPICE `oscltx` signalling `SPICE(DEGENERATECASE)` on the same states, and a state of
  |h| = 1e-3 m²/s whose rotation is bit for bit its closed form; `ConjunctionAssessmentTests` an analysis with a
  protected spacecraft at rest at the centre. The 32 dense cases of `ConjunctionAssessmentTests` that put the protected
  spacecraft there now move both participants by the same uniform motion, (1, 0, 0) m + (0, 0, 1e-3) m/s t, which
  leaves their relative state bit for bit and their closed-form TCAs and misses unchanged.

- **A target orbit in another frame than the propagation's burned silently** ([#685](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/685)). The
  constructor of `ImpulseManeuver` checks the frame of its target only when the tank of its engine is on a spacecraft,
  against the initial orbit the spacecraft has then, and the check of the propagation (#675) covered only a TLE or TEME
  start: a `PlaneAlignmentManeuver`, `ApsidalAlignmentManeuver` or target-orbit `PhasingManeuver` built before its tank
  was added, or whose spacecraft was then given another initial orbit (`SetInitialOrbitalParameters`), read the
  components of its target as if they were in the integration frame. A plane change of 2° between circular orbits of
  7000 km at 30° and 32° in ICRF, the target given in ECLIPJ2000, burned 2807.185 m/s and left the orbit 23.44° from the
  target's plane, the closed form 2 v sin(Δi/2) of the plane misread (Δi = 21.44°), for 263.394 m/s. The propagator now
  checks every target of the chain, leading and chained, against the frame of the propagation when it is built, on
  every spacecraft (`PropagatorBase.TargetOrbitFrameError`, the check of #675 without its early return), before
  anything changes, and again when it propagates (`PropagatorBase.Propagate` reads the chain of the spacecraft again),
  against the frame of its integration: a maneuver attached to the spacecraft after a `CentralBodyPropagator` was built
  (constructor or builder) and then `Propagate()` burned the same 2807.185 m/s (second review). `ImpulseManeuverTests` covers the three maneuvers, both orders, leading and chained, both
  integrators, and a fresh spacecraft as one already propagated (its states and maneuver windows left as they were);
  the target rotated to ICRF, and a spacecraft and a target both in ECLIPJ2000, burn the closed form to 7.0e-9 m/s
  (RK7(8), threshold 1.4e-8 m/s, twice the worst of the three platforms) and end in the target's plane to 4.0e-11°; a maneuver attached after the
  propagator was built, by its constructor or its builder, with both integrators, is rejected by `Propagate()` before
  its burn (no window, the tank full); `ScenarioTests` covers `Scenario.SimulateAsync`. Both checks compared the frames by
  name, so that a target in GCRF on a spacecraft propagated in ICRF, and the reverse, was rejected by the constructor
  ("must be defined in the same frame"), when the propagator was built and when it propagated, although the library
  gives GCRF the axes of ICRF (`GcrfFrame`, the identity rotation) and the relabel the message asked for,
  `targetOrbit.ToFrame(Frame.ICRF)`, changed no number (third review). They now take GCRF and ICRF as one frame
  (internal `PropagatorBase.SameAxes`, by name, any case), and no other pair; `Frame.Equals` is unchanged (#562).
  `ImpulseManeuverTests` runs a plane change of 2° with its target in GCRF on an ICRF spacecraft, every order of
  attachment, leading and chained, both integrators, bit for bit the run with the target in ICRF, and leading in RK7(8)
  the closed form to 6.9e-9 m/s (threshold 1.4e-8 m/s); the reverse pair, an ICRF target on a GCRF spacecraft, bit for
  bit, an ECLIPJ2000 target still rejected on it; and on a TLE spacecraft a GCRF target accepted (rejected on a mean TEME
  set, as an ICRF one) and burning bit for bit the ICRF target.
- **The CLI took identifiers for sites that the library does not** (cli-site-rule of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)'s second round). The commands built a site of every
  identifier from 100001 to 999998 (`Helpers.IsSiteIdentifier`), named `site<id>`, where the library's rule
  (`SpiceAPI.IsSiteId`, the rule of `Site.NaifId`) excludes x000, a body of at most 100 and the Lagrange points 391 to
  395, and names the site by its SPICE name: from the observer 399000 `ephemeris` threw `ArgumentOutOfRangeException`
  from `Site(0, ...)` where `SpiceAPI.ReadEphemeris` throws `ArgumentException` ("399000 has no name"), 391013 failed on
  the body 391, 100500 as "100 is not a celestial body", and `sub-point` rejected 391013, 399000 and 100500 as sites. The
  library gives its internals to the CLI (`InternalsVisibleTo`, as to the conformance runner; the CLI is packed with the
  library of its build), and `Helpers.CreateLocalizable` and `sub-point` take the library's rule and its site factory
  (internal `SpiceAPI.CreateKernelSite`, which `SpiceAPI.CreateObserver` uses too); `Helpers.IsSiteIdentifier` is gone.
  `EphemerisTests` compares the command from 391013, 399000, 100500, 301001 and 395013 with `SpiceAPI.ReadEphemeris`
  (same exception and message), the site of 399013 with the observer of `SpiceAPI.ReadEphemeris` (DSS-13), and
  `sub-point` of 391013, 399000 and 100500 with `CelestialItem.Create`, and the command from a site whose SPK is
  loaded without a kernel that names it with `SpiceAPI.ReadEphemeris` (the same "has no name in SPICE"), then, its
  frame kernel loaded, with the states of `SpiceAPI.ReadEphemeris` under its name.
- **The frame of a site from the kernels had two orientations** ([#690](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/690)). SPICE read the
  frame of a kernel site by its name (`DSS-13_TOPO` of `earth_topo_201023.tf`, built on the coordinates of the station
  at 2020-10-23 on the WGS84 spheroid) in `spkezr_c` and `gfposc_c`, while the library rotated by a frame it built from
  `Site.Planetodetic`, the position of the station at J2000 TDB on the `pck00011` ellipsoid (`ToFrame(site.Frame)`,
  `GetHorizontalCoordinates`, the managed coordinate searches): 7.76e-8 rad apart for DSS-13 at 2000-01-02 and at
  2024-03-01, 7.72e-8 rad of it the 0.41 m of plate motion between J2000 and 2020-10-23 and 2.1e-9 rad the spheroid;
  the Moon by name and rotated 7.4e-8 to 7.8e-8 rad apart, the elevations 6.1e-8 rad. A kernel site named otherwise
  than its kernel frame (`new Site(13, "GOLDSTONE", earth)`) threw SPICE(UNKNOWNFRAME) (`'GOLDSTONE_TOPO'` not
  recognized) on a read by name. `SpiceAPI.SpiceFrameName` now hands the frame of a kernel site by the name the loaded
  kernels give its id, 1000000 + NaifId (`frmnam_c`, as for a kernel spacecraft since #676), and its own name when
  none does; `SiteFrame.GetStateOrientationToICRF` of a kernel site whose frame id the loaded kernels define is
  SPICE's transformation of that frame, read at each call and not stored (internal `SpiceAPI.KernelDefinesFrame`); the
  illumination angles (`IlluminationIncidence`, `IlluminationEmission`) take the normal of the ellipsoid at
  `Planetodetic`, the normal `gfilum_c` takes for the site, so they keep agreeing with
  `FindWindowsOnIlluminationConstraint`. No C++ change. `FrameTests` checks the frame of DSS-13 against SPICE's
  `DSS-13_TOPO` (0 rad, angular velocity 0) and against the closed form of `TKFRAME_1399013_ANGLES` (3.13e-16 rad,
  threshold 6.3e-16 rad) and the Moon from GOLDSTONE against `spkezr_c` in `DSS-13_TOPO` (2.1e-16 rad);
  `SiteTests` the horizontal coordinates against `spkezr_c` in `DSS-13_TOPO` by name (2.1e-16 rad, threshold 4.2e-16 rad)
  and the literals of `tools/geodetic_reference/dss13_topo.c` in `DSS-13_TOPO`; `SiteTests`,
  `GeometryFinderTests` and `KernelWindowSearchTests` the managed searches of the kernel site against `gfposc_c` in
  `DSS-13_TOPO` (`tools/geometry_reference`, regenerated: the Moon's Z and elevation extremes, the LEO elevation, 1.2e-7
  s two-body and 2.4e-7 s RK7(8)), where they compared with a reference in the frame the library built. The frame kernel
  of such a site (`Site.WriteFrameAsync`, which `CosmographiaExporter` writes for every site of a scenario) still held
  the frame of `Planetodetic` under the kernel's id 1399013 and name `DSS-13_TOPO`, 7.76e-8 rad from it, and for
  GOLDSTONE renamed that id `GOLDSTONE_TOPO` (second review): it now writes the kernel frame, its kernel name and id and
  its rotation to the frame of the body read from SPICE as `TKFRAME_<id>_MATRIX`, one column per line (CSPICE reads 132
  characters of a line: the nine components on one line gave 6), and rejects a kernel frame not fixed in the frame of
  the body (`InvalidOperationException`, internal `SpiceAPI.KernelFrameNameOf`). `SiteTests` loads the file written for
  DSS-13 and GOLDSTONE, renamed so that it redefines nothing of `Data/SolarSystem`, and reads its rotation to
  `DSS-13_TOPO` by `pxform` (2.1e-16 rad, threshold 4.3e-16 rad; 7.76e-8 rad before), and rejects a site
  frame defined relative to J2000. A kernel site whose frame id no loaded kernel defines kept the frame of its
  `Planetodetic` for `ToFrame(site.Frame)`, `GetHorizontalCoordinates` and the managed searches, while an ephemeris read
  in it reached `spkezr_c` by its own name, `Name + "_TOPO"`, which SPICE does not know: the site, the Moon and
  `SpiceAPI.ReadEphemeris` (at an epoch and over a window) in its frame threw SPICE(UNKNOWNFRAME) (third review). Such a
  read is now read in J2000 and rotated by that frame, as for a site built from coordinates (#682,
  `SpiceAPI.ManagedReadFrame`), decided at each read, so that a frame kernel of its id loaded later is read by its
  kernel name. `SiteTests` compares the reads of the Moon and of the site from the Earth, by `CelestialItem.GetEphemeris`
  and `SpiceAPI.ReadEphemeris` at an epoch and over a window, with `spkezr_c` in a frame kernel the test writes in
  NAIF's topocentric convention (the angles of `earth_topo_201023.tf`) on `Planetodetic`, under another id: the position
  within 7.3e-16 (angle and relative magnitude), the velocity within 4.9e-11 m/s and the frame within 5.8e-16 rad
  (thresholds 1.5e-15, 9.9e-11 m/s and 1.2e-15 rad); then loads a frame kernel of the site's id, under
  another name, and reads that kernel frame by name, bit for bit, and the managed frame again once it is unloaded; and
  checks that the frame kernel written for such a site is the frame it uses (5.8e-16 rad, 0 from NAIF's, threshold
  1.2e-15 rad); each figure the worst of Linux x64, Windows x64 and macOS arm64 and each threshold twice it (CI run
  43745883709). Calls that threw now return; nothing else moves.
- **A coordinate search in a frame whose reads are the library's returned no window** (third review of
  [#690](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/690)). When CSPICE searched
  (`Site.FindWindowsOnCoordinateConstraint` of a kernel site with a kernel spacecraft as observer,
  `CelestialItem.FindWindowsOnCoordinateConstraint` with a kernel spacecraft taking part), `gfposc_c` got the frame by
  name: the frame of a kernel site whose frame id no loaded kernel defines (`Name + "_TOPO"`), of a site built from
  coordinates, of a managed spacecraft with an attitude record, `Frame.TEME`, `Frame.CIRS` and `Frame.TIRS`, names SPICE
  does not know, so that its proxy dropped the error and the search returned no window, while a read in those frames is
  the library's (#558, #682). These searches now take the managed search in every frame `SpiceAPI.ManagedReadFrame`
  handles, decided at each search (internal `SpiceAPI.IsManagedFrame`), so that the search agrees with the reads; and
  `SpiceAPI.FindWindowsOnCoordinateConstraint`, which can only hand the frame to CSPICE, throws `ArgumentException`
  (`frame`) for a frame of the library that SPICE does not know by the name handed (internal `SpiceAPI.SpiceKnowsFrame`:
  TEME, CIRS, TIRS, the frame of a site or a spacecraft whose id no loaded kernel defines under that name).
  `KernelWindowSearchTests` searches, at a 5 min step, the declination of a kernel site at DSS-13's coordinates without
  a frame kernel seen from the kernel LEO (`kernel_search_leo.bsp`) in the site's frame (2 and 12 windows, 0 before),
  and of the LEO from the Earth in the frames of that site and of a site built from the same coordinates (9 windows),
  against `gfposc_c` in a frame kernel the test writes in NAIF's topocentric convention on the same coordinates: within
  5.0e-7 s on the three platforms (threshold 1.0e-6 s, the managed search at 1 µs against `gfposc_c` at its default 1e-6 s); a
  frame kernel of the site's id loaded, the site's search is `gfposc_c` in that kernel frame, bit for bit.

- **A geopotential degree above its model file ran at the file's degree, and a missing harmonic was read as zero**
  ([#689](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/689)). `GeopotentialGravitationalField`
  set its degree to the one asked whatever the file held and caught the missing coefficients of every degree as zeros:
  `GeopotentialModelParameters(path, 80)` on `EGM2008_to70_TideFree` built a field of degree 80 whose acceleration at
  6678.137 km was that of degree 70, bit for bit, and a file without its (2, 0) row built a field without J2, 2.648e-2
  m/s² from the full file at the pole at 6678.137 km (the closed form 3 J2 μ Re²/r⁴). The reader now records the
  highest degree of its file (internal `GeopotentialModelReader.MaximumDegree`), and the field, which both public paths
  reach, rejects a degree above it and a missing (n, m) with 2 ≤ n ≤ degree, when the body (or the field) is built, so
  that the parameters still read their file once (#674); degrees 0 and 1 stay the point mass. A complete file gives the
  same table, bit for bit, for every degree it covers. `GeopotentialModelDegreeTests` builds degrees 71, 80 and 100 from
  `EGM2008_to70_TideFree` through the field, a path and a stream; the file without its (2, 0) row at degree 10 (and its
  (10, 10) row) throws naming the pair, and at degrees 0 and 1 builds and gives GM/r² along −r to 1e-15, bit for bit
  the full file's; the file followed by explicit zero rows to degree 80 gives the degree-70 field bit for bit over 2000
  directions at 300 km. The fields of degrees 120 and 130 of `GeopotentialPartialsTests` are built from such a file
  (results unchanged, bit for bit). The model files the library, the command line and the conformance runner use are
  complete: `EGM2008_to70_TideFree` (2553 rows, Σ (n + 1) for n = 2 to 70), the CLI's `EGM2008_to100_TideFree` (5148)
  and the suite's `GMM2B_to20` and `LP165P_to20` (228 each).

- **`Star.Propagate` and `Star.PropagateAsync` never returned for a step of zero or less** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The loop `epoch += stepSize` never
  passed the end of the window, and a negative step stored a state at every turn, without bound; #674 had guarded
  `Scenario.SimulateAsync` before its stars, not the star's own methods. Both now apply the step rule of the library
  (`PropagatorBase.RequirePositiveStep`, #674) first, `PropagateAsync` before it creates its task, and store nothing.
  `StarTests` checks a step of 0, −1 tick and −1 s on both methods (the base needed a watchdog: a step of 0 had not
  ended after 5 s).

- **`Star.IsPropagated` was always false** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). It read `PropagationOutput`, the
  output directory of an older design, which no code sets, while `Propagate` keeps its states in memory. It now reads
  the stored states, as `Spacecraft.IsPropagated` does: true after `Propagate`, `PropagateAsync` or
  `Scenario.SimulateAsync`. `PropagationOutput` stays, documented as always null, marked `[Obsolete]` (see
  **Deprecated**). `StarTests` and `ScenarioTests` check each path; the SPK of the star propagated by `SimulateAsync`, read back by SPICE (`spkezr` through `SpiceAPI`), is the
  catalog position moved by the proper motion, written in the test (right ascension and declination plus the proper
  motion times the Julian years from the catalog epoch, at the star's distance, by CSPICE's `latrec_c`), to 1.98e-16
  relative over four years (threshold 4.0e-16, twice the worst; Validation category, Linux only, CI run 43773937161).

- **A star propagated twice kept the states of both propagations** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). `Star.Propagate` added each state
  to a dictionary with `TryAdd`, which kept the states of every earlier propagation, the first one of an epoch winning,
  and `Scenario.SimulateAsync` propagated its stars through it without clearing them, where `Spacecraft.Propagate`
  replaces the stored states: `WriteEphemeris` wrote one segment from the first window ever propagated to the last, the
  gaps between windows interpolated, so that SPICE read the star at epochs no propagation asked for. Measured: after a
  propagation over four days of January 2001 (step one day) and one over the first six hours of 1 March 2001 (step 1 h), the one segment of
  the SPK started on 1 January (ET 31579200 s), and SPICE read the star there. Each propagation now computes all its states, then replaces those the
  star held (one array published at once); `Scenario.SimulateAsync` goes through it. `StarTests` and `ScenarioTests`
  propagate twice over different windows and steps (`Propagate` then `Propagate`, `Propagate` then `PropagateAsync`, two
  scenarios over different windows): SPICE (`spkezr` through `SpiceAPI`) reads the SPK at each epoch of the second grid
  at the catalog position moved by the proper motion, written in the tests (the epochs whole half days from the catalog
  epoch), to 1.39e-16 relative on Linux, closer than the star moves in 1 µs (3.6e-15 relative, the bound of the tests
  that run on every platform), and throws at the epochs of the first window and between the two; the Validation test,
  which runs on Linux alone, holds the noise-level record (threshold 2.8e-16, twice the Linux worst) and reads the
  segment with CSPICE (`dafus`, `dafgda`): one segment
  of type 8 over the second window, its first epoch, step and number of states those of the second grid. A single
  propagation writes the same SPK as before, bit for bit.

- **A star off whole and half days from J2000 was that of an epoch up to about 20 µs away** (fourth round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). `Star.GetEquatorialCoordinates`,
  `GetRightAscensionSigma` and `GetDeclinationSigma` took dt, the Julian years from the catalog epoch, from the
  difference of the two Julian dates, each rounded to a double near 2.45e6 (4.66e-10 day, about 40 µs) before the
  subtraction, against the remark of `Time.ToJulianDate`. dt now comes from the exact difference of the ticks, rounded
  once to a double, then divided by the days of a Julian year, in each time's own calendar as before (a UTC catalog epoch
  and a UTC epoch still differenced their UTC calendars, until [#709](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/709), below). Measured on the star of `StarTests` (1 rad, 0.1 rad/yr, catalog
  epoch 2001-01-01 TDB) against the closed form, dt from the tick difference in `decimal` (28 digits) rounded once: at
  the 744 hourly epochs of March 2001, at 199 epochs 1234567 ticks apart and at 99 epochs before the catalog epoch, the
  right ascension and the declination were 6.328e-14 rad away, the position 7.287e-14 relative from CSPICE's `latrec_c`
  of the closed form (20 times the 3.6e-15 the star moves in 1 µs); now 2.220e-16 rad and 3.711e-16 (4.441e-16 rad and
  4.657e-16 for a star of 0.01 rad/yr 40 years after its catalog epoch, 6.217e-15 rad and 6.343e-15 before), the
  sigmas 3.469e-16 relative (3.930e-12 before). The error is proportional to the proper motion: about 1e-16 relative
  and below for the proper motions of real stars. At epochs whole or half days from J2000, whose Julian dates are exact,
  nothing moves, bit for bit, nor any other test or measurement of the suite. `StarTests` checks the angles and the
  sigmas against the closed form on every platform (thresholds 8.9e-16 rad and 7.0e-16 relative, twice the worst, the
  same on Linux x64, Windows x64 and macOS arm64, CI run 43773937161; the sigmas square through `Math.Pow`, the
  platform's libm, which could have differed) and the position against `latrec_c` in the Validation category (9.4e-16,
  twice the worst, Linux only).

- **A `CentralBodyPropagator` given an integrator with its forces threw `NullReferenceException` unless its caller
  initialized the integrator** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The constructors
  `(window, spacecraft, IIntegrator, deltaT)` and `(…, covariancePropagation)` ended without
  `integrator.Initialize(InitialState)`, which the force-building constructors and the internal one of the avoidance
  options call: the integrator had no observer, frame, evaluation state nor RK7(8) stages (RK7(8) failed in
  `RK78Stepper.Step`, Velocity-Verlet in `Integrator.UpdateEvalState`), and no document told the caller to initialize
  it. Both now initialize it with the initial state of the propagation (about the central body, the Sun in SSB mode, in
  the inertial frame of the propagation, at the TDB start of the window), before the covariance propagation is enabled,
  the order of the force-building path; the default Velocity-Verlet constructor, whose integrator its own constructor
  initializes, chains past that call. `IntegratorInitializationTests` runs both constructors, RK7(8) (with and without
  the state transition matrix) and Velocity-Verlet, without any `Initialize` by the caller, against the two-body closed
  form over the TT seconds of the hour about the Earth (9.95e-5 m in RK7(8) at 1e-10, 15.9 m by Velocity-Verlet at 1 s,
  its truncation), about the Sun in SSB mode over the TDB seconds of a day (1.22e-4 m), from an ECLIPJ2000 state
  integrated in ECLIPJ2000 (9.95e-5 m) (the same on the three platforms; thresholds twice the worst); bit for bit the run the caller
  initializes after construction, with and without an impulse, and the force-building constructor with the same single
  force; a recording `IIntegrator` sees one `Initialize`, by the constructor, with the central body, the frame and the
  TDB start of a UTC window.

- **A static solar system object read on a `MemoryDataProvider` without its data left its class unusable for the life
  of the process** (second round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)).
  The type initializers of `Barycenters`, `PlanetsAndMoons` (`EARTH_BODY`, `MOON_BODY`) and `Stars` (`SUN_BODY`) built
  their objects with the natural-body constructors, which read the provider: with a provider holding only the Earth's
  information, `CelestialItem.Create(391)` threw a `TypeInitializationException` for `Barycenters` (the information of
  the barycentre 0), and still did once the data were added, the runtime keeping the exception; the library's own paths
  that read those classes (the observer resolution, `LagrangePoint`, `Star`, the Sun of SSB mode, the radiation forces)
  failed with it. The objects now read no provider data when their class is initialized: their identifier, name and
  predicates need none, and their other members are read at the first read of one of them, exactly as the natural-body
  constructors read them, through one internal object published with a compare-exchange, never under a lock held
  across the provider call (#623); a read that throws caches nothing and the next read tries again, on the provider set
  at its time, so that a read that failed on one provider succeeds once the data exist on that provider or on another
  set since (a `SpiceDataProvider` with its kernels after a `MemoryDataProvider`); once read, an object keeps the provider
  it read its data from, for its data and its ephemeris. The objects users build are unchanged, bit for bit. In fresh
  processes (`MemoryDataProviderBodyTests`, modes `static-bodies-memory`, `static-bodies-spice` and
  `static-bodies-switch` of the cold-start child): on a memory provider holding the Earth's information only,
  `CelestialItem.Create(391)` throws the provider's `InvalidOperationException` naming 391 and
  `SOLAR_SYSTEM_BARYCENTER.GM` its `ArgumentException` naming `naifId`, twice; once the information and the states are
  added, the GM are those given and the orbits of L1, the Earth barycentre, `EARTH_BODY` and `SUN_BODY` the exact
  differences of the stored states; on SPICE, the kernels loaded first, the names and GM of the ten barycentres, the GM,
  equatorial radius, J2, name and frame of `EARTH_BODY` and the GM of `SUN_BODY` and `MOON_BODY` equal SPICE's body
  information, and the orbit of `EARTH_BODY` `spkezr`'s state of 399 about 3 in ECLIPJ2000 at J2000 TDB, exactly; read
  first on an empty memory provider (the `ArgumentException` of each), then after `SetDataProvider(new
  SpiceDataProvider())` and `LoadKernels`, the GM of the solar system barycentre, the Earth barycentre, `EARTH_BODY` and
  `SUN_BODY`, the frame and orbit of `EARTH_BODY`, the orbit of the Earth barycentre and its ephemeris about the solar
  system barycentre are SPICE's, exactly, and stay SPICE's once another memory provider is set (before this, the retry
  went back to the provider of the type initializer: the objects threw the memory provider's `ArgumentException` for the
  process while `SpiceAPI.GetCelestialBodyInfo(0)` answered). On SPICE, reading those data before `LoadKernels` kept
  the zeros SPICE answers, fixed by the third round (the next entry).
- **A static solar system object read on SPICE before `LoadKernels` kept the zeros SPICE answered for the life of the
  process** (third round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). SPICE
  names the bodies without kernels (its built-in names), and since #672 a body information without `BODYnnn_GM` or
  `BODYnnn_RADII` has a GM or radii of 0: the first read of a static object of `Barycenters`, `PlanetsAndMoons` or `Stars`
  succeeded with those zeros and the deferred object kept them. Measured in a fresh process: `EARTH_BODY` had a GM of 0,
  radii of 0, a J2 of NaN, the built-in frame `IAU_EARTH`, no orbit and an infinite sphere of influence, the GM of the
  Earth barycentre, the solar system barycentre, `SUN_BODY` and `MOON_BODY` 0, all unchanged after `LoadKernels`
  (`SpiceAPI.GetCelestialBodyInfo(399)` then gave 3.986e14 m³/s²). A static object now keeps its data only when the
  provider gives the data the kernels give each of them: a GM (on SPICE `BODYnnn_GM`, for the solar system barycentre the
  sum of `BODY1_GM` to `BODY10_GM`) and, for `EARTH_BODY`, `MOON_BODY` and `SUN_BODY`, radii (`BODYnnn_RADII`), checked
  before its orbit is read; otherwise the read throws `InvalidOperationException` naming the missing variable, keeps
  nothing, and the next read tries again. The objects users build are unchanged, bit for bit. In a fresh process
  (`MemoryDataProviderBodyTests`, mode `static-bodies-before-kernels` of the cold-start child): before any kernel, the GM
  of `EARTH_BODY` (twice, naming `BODY399_GM`), its frame, radius and orbit, the GM of the Earth barycentre (naming
  `BODY3_GM`) and of the solar system barycentre (naming `BODY1_GM` to `BODY10_GM`), the radius and frame of `SUN_BODY`
  and the GM of `MOON_BODY` throw; with `gm_de440.tpc` alone, the GM of `EARTH_BODY` throws naming `BODY399_RADII`
  (twice) and the frame of `MOON_BODY` throws; after `LoadKernels` the GM, radii, J2 (finite) and frame (`ITRF93`) of
  `EARTH_BODY`, its orbit (`spkezr`'s state of 399 about 3 in ECLIPJ2000 at J2000 TDB), the GM of the two barycentres
  and of `MOON_BODY` and the radius and GM of `SUN_BODY` are SPICE's (`SpiceAPI.GetCelestialBodyInfo`), exactly.
  A first read after a partial load kept its data for the process too: with `latest_leapseconds.tls`, `gm_de440.tpc` and
  `pck00011.tpc` loaded (GM and radii defined), `EARTH_BODY` had a J2 of NaN (`BODY399_J2` is in `geophysical.ker`), the
  frame `IAU_EARTH` (`OBJECT_EARTH_FRAME` is in `earth_assoc_itrf93.tf`; `IAU_EARTH` is 8.49e-4 rad from `ITRF93` on
  2001-06-01 and 1.46e-3 rad on 2025-06-01, 5.4 to 9.3 km at the equator, which geodetic coordinates, sites and drag
  read), no orbit and an infinite sphere of influence, `MOON_BODY` the frame `IAU_MOON`, all unchanged after
  `LoadKernels` of the whole set. On the `SpiceDataProvider` the data of a static object now hold for the generation of
  the kernel pool they were read at: each `LoadKernels`, `UnloadKernels` and `ClearKernels` raises it (internal
  `SpiceAPI.KernelPoolGeneration`), and the first read after reads the data again; data equal to those held keep the
  objects held (the frame and orbit instances, which the orientation cache of a propagation is keyed by), other data
  replace them, and a read that throws keeps nothing new. Another provider is read once, as before. The same child: after
  the partial load the J2 (NaN), the frames (`IAU_EARTH`, `IAU_MOON`) and the absent orbit are SPICE's, the reload of the
  PCK keeps the frame instance; after `LoadKernels` of the directory the J2, frames (`ITRF93`, `MOON_ME`) and orbit are
  SPICE's, exactly (red before: J2 NaN); the reload of a kernel keeps the frame and orbit instances; after
  `ClearKernels` the GM throws naming `BODY399_GM` (twice), and after `LoadKernels` again the GM, J2 and frame are
  SPICE's.
- **The CDM wrote `INTRACK_THRUST` from the engines of a spacecraft** ([#684](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/684)). `EncounterCase.ToCdm` set
  `INTRACK_THRUST` of either object to `YES` for a spacecraft with an engine and `NO` for one without, its capability,
  where CCSDS 508.0-B-1 Cor. 2 (Table 3-3) defines it as whether in-track thrust modeling was used for the OD of the
  object (the examples of §3.6.3 and §4.4 have `MANEUVERABLE = YES` with `INTRACK_THRUST = NO`), and a `null` in
  `ProtectedParticipantMetadata`/`SecondaryParticipantMetadata` could not clear it. Under the same signatures (decided
  by S. Guillet, 2026-11-13), the keyword is `NO` for a covariance the library propagated to TCA, whose force model
  holds no thrust, with the other force-model keywords, and omitted otherwise, as `EARTH_TIDES` is; a value given in
  the options is written, `null` keeping the inferred one. `MANEUVERABLE` is unchanged. `CdmTests` checks the case of
  the issue (an engine on both spacecraft: no `INTRACK_THRUST` element, `MANEUVERABLE` still `YES`, schema-valid, read
  back null) and each override of either object; `CdmPropagatedCovarianceTests` checks `NO` for two propagated
  covariances, a secondary with an engine included, and the keyword omitted for the covariance held in RTN.
- **The CDM wrote `SOLAR_RAD_PRESSURE = YES` for a covariance it did not propagate** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). Outside a covariance the
  library propagated to TCA, `EncounterCase.ToCdm` wrote `SOLAR_RAD_PRESSURE` from the reflectivity coefficient of a
  spacecraft, `YES` for a `SolarRadiationCoeff` above 0 (1.0 by default), whether or not any solar radiation pressure
  was modeled: for a manual encounter never propagated, a covariance given at TCA or held in RTN, and a participant
  propagated without solar radiation pressure, where CCSDS 508.0-B-1 Cor. 2 (Table 3-3; wording confirmed by
  S. Guillet, 2026-11-15) defines it, as `INTRACK_THRUST` (#684), as whether that perturbation was used for the OD of the object.
  Under the same signatures, the keyword is `YES` or `NO` from the forces of the propagation of a covariance the library
  propagated, as before, and omitted otherwise, as `GRAVITY_MODEL`, `ATMOSPHERIC_MODEL`, `N_BODY_PERTURBATIONS`,
  `INTRACK_THRUST` and `EARTH_TIDES` are; a value given in `ProtectedParticipantMetadata`/`SecondaryParticipantMetadata`
  is written, `null` keeping the inferred one. `CdmTests` checks the manual encounter (no element, read back null) and
  each override of either object; `CdmPropagatedCovarianceTests` checks `NO` for two covariances propagated in RK7(8)
  without solar radiation pressure and a Cr of 1.5, and the keyword omitted for Velocity-Verlet and for the covariance
  held in RTN.
- **A message with two segments loaded its first one silently** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The OMM and OPM readers found
  the header, body, segment, metadata and data of a message by their first element (`FirstOrDefault`), so an OMM whose
  body held a second `<segment>`, and equally a second header, body, metadata, data section or data block, loaded as
  its first one: iss.omm with its segment duplicated loaded as the ISS through `OmmReader`, `Omm.LoadFromString`,
  `LoadFromFile` and `LoadFromStream`, with or without schema validation, and the same for the OPM; a duplicated
  `<metadata>` was read as its first. The NDM/XML 4.0.0 schemas bound each to one (`ommType`, `ommBody`, `ommSegment`,
  `ommData`; the same for the OPM, its maneuvers excepted), but schema validation checked nothing of an unqualified
  document, the form the writers and Space-Track write (only the qualified copy was rejected, "invalid child element
  segment"; it checks the standard forms since the third round, below, which rejects such a document first under
  default validation). The CDM reader read a second header, body, `relativeMetadataData` (its miss distance dropped), metadata,
  data section or block as its first, and threw `ArgumentException` from the `Cdm` constructor for a body of one or
  three segments. Under the same signatures, the three readers take each of these elements through one internal helper
  (`CCSDS/Common/CcsdsElements`), which throws the reader's parse exception (`OmmParseException`, `OpmParseException`,
  `CdmParseException`) naming the element and its count when there are several, and the CDM reader throws
  `CdmParseException` for a body that does not hold two segments. Keyword duplicates (two `OBJECT_NAME`) are out of
  scope. The tests build the documents with System.Xml.Linq and count the elements independently of the readers, show
  with the embedded SANA schemas that the qualified copy is rejected and its one-segment original accepted, and reach
  every path: `Load*` with and without schema validation, `Load*` and `ReadFrom*` from a file, a string and a stream,
  `TryLoadFromFile`, `ValidateFile` and `Cdm.ReadFrom*`; every block of a data section the reader reads is duplicated
  once (`OmmReaderTests`, `OpmReaderTests`, `CdmTests`, whose CDM gives each segment the `odParameters` block `ToCdm`
  does not write). The single-segment documents of the tests load as before.
- **The OMM, OPM and CDM schema validation checked nothing of a standard document** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
  [#614](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/614)). `OmmValidator`, `OpmValidator`
  and `CdmValidator` loaded the common and message modules of the embedded NDM/XML 4.0.0 schemas, whose elements are
  qualified (`elementFormDefault="qualified"`), with a synthetic root declared in the namespace `urn:ccsds:schema:ndmxml`.
  A document whose root has no namespace, the two forms the SANA master declares (CCSDS 505.0-B-3), matched no
  declaration: .NET reported "Could not find schema information" as a warning per element, `IsValid` counts errors only,
  and nothing below the root was checked. So `Omm`/`Opm.LoadFrom*`, whose `validateSchema` is true by default, loaded
  silently what the schema rejects: an OPM whose `X` is in `units="m"` loaded with 6778.137 read as km, an OMM with
  `INCLINATION` in `rad` loaded, undeclared elements were ignored, a root `<foo>` loaded as an OMM, a malformed `EPOCH`
  or a missing `OBJECT_NAME` reached the reader's parse exception and `ECCENTRICITY` -0.5 an
  `ArgumentOutOfRangeException` of `MeanElements`; the CDM reported a `COLLISION_PROBABILITY` of 1.5 valid (#614).
  Space-Track's iss.omm and H2ADEB.omm were valid with 28 to 133 warnings. Under the same signatures, a root without a namespace is
  now validated against the schemas of its form through one internal helper (`CCSDS/Common/NdmXmlStandardForms`), from
  the embedded master (`ndmxml-4.0.0-master-4.0.xsd`, whose `ndm`, `omm`, `opm` and `cdm` roots have no namespace): the
  unqualified set when no element is in the NDM/XML namespace (the form of Space-Track and of the OMM and OPM writers),
  the embedded files with their single `elementFormDefault` set to `unqualified`, which is SANA's `ndmxml_unqualified`
  set (measured: the 14 files differ by that attribute alone); the qualified set otherwise (every element below the root
  in the namespace). A root other than `ndm` or the message's, or one in another namespace, is an error (`OMM007`,
  `OPM006`, `CDM001`), and so is an `ndm` that holds no child of the validator's message, which the NDM master accepts
  (lxml 6.1.3 on SANA's unqualified set accepts an empty `ndm` and one of an OPM only) but in which the reader finds no
  message: `OmmValidator` reported `<ndm/>` or an NDM of one OPM valid with no issue, `Omm.LoadFromString` then throwing
  `OmmParseException`, `OpmValidator` an NDM of one OMM, and the CDM validator the same, its `ndm` root in the namespace
  included (`NdmXmlStandardForms.MessageError`; `ValidateSchema_NdmWithout{Omm,Opm,Cdm}_IsInvalid`). A root in the
  NDM/XML namespace, IO's earlier form of a bare message and the form `CdmWriter` writes (#615), is validated as before,
  bit for bit, but for that CDM `ndm`. The verdicts were measured against lxml 6.1.3 (libxml2) on SANA's
  unqualified set (sanaregistry.org/files/ndmxml_unqualified/, read 2026-11-15, SHA-256 of omm-3.0 e54622ea…2a1a,
  opm-3.0 193e2eff…9519, common-4.0 3ae25a91…dcde, master-4.0 2abc69ce…df36) and on the qualified set: a prototype of
  this rule agreed with lxml on the 67 documents of the measurement (iss.omm, H2ADEB.omm, iss_minimal.xml and
  satellite_complete.xml, each with one mutation in each form), whose mutations the tests take. The tests reject each mutation in each form (`OmmValidatorTests`, `OpmValidatorTests`,
  `CdmTests`), accept Space-Track's files and the writers' outputs without a warning (`OmmWriterTests`,
  `OpmValidatorTests`), and check that an OPM in metres throws `OpmValidationException` from `Opm.LoadFrom*`.
- **The C# examples of the developer guide did not compile** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). 22 of the 60 blocks of
  `DEVELOPER_GUIDE.md` failed against the library: the spacecraft example built an `Engine` without its tank, added it
  before the tank (`AddEngine` throws for a tank the spacecraft does not hold, #676), called `SetFuelTank` and read
  `StateVectorsRelativeToICRF`, neither of which exists, and its Engine table listed a constructor that does not exist;
  elsewhere, parameters of other names (`a:` on `KeplerianElements`, `longitude:` on `Planetodetic`, `maxRevolutions:`,
  `occultingBody:`), `TLE.Configuration`, `Omm.CreateForSgp4`, `Omm.SaveToString`, `LambertSolution.DepartureVelocity`,
  `PlanetsAndMoons.SUN`, unary minus on `Vector3`, `Frames.Frame` (which does not resolve in an application's namespace),
  a TRIAD attitude on two opposite body vectors (which throws), a local named `explicit` and locals declared twice. Every
  block now compiles: each is the region of a method of `IO.Astrodynamics.Tests/Documentation/DeveloperGuideExamples.cs`,
  in a namespace outside `IO.Astrodynamics` as an application's, and `DeveloperGuideExamplesTests` checks that each
  block equals its region and runs the 40 that need only `Data/SolarSystem`; the spacecraft example against the two-body
  state of its elements after 12 h (124.5 m, the Velocity-Verlet error at the 1 s step it now uses: 447 km at the 60 s
  of before; bounds 2.5e2 m and 2.8e-1 m/s, twice the worst, the same on the three platforms). A
  block that continues an earlier one names its inputs in its first comment. The KeplerianElements table listed
  `ApogeeRadius()`, which never existed (the apogee radius is `ApogeeVector().Magnitude()`, as
  `docs/reference/keplerian-elements.md` now says too); `GuideMethodTables_NameMembersOfTheLibrary` checks that every
  method of the guide's member tables is a public method or constructor of the type of its heading, its bases or the
  library's extension methods, with an overload of the written argument count. No library result moves.
- **`Normalize`, `Magnitude`, `FromQuaternion`, `TransformCovariance`, `Lerp`, `SLERP` and `Angle` gave zero, NaN,
  +Infinity or threw for finite vectors and quaternions outside about 1e-154 to 1e154** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The magnitudes of `Vector3` and
  `Quaternion` were √(Σ c²) unscaled: a component above about 1.34e154 squared to +Infinity, so `Normalize()` returned the
  zero vector or quaternion and `Magnitude()` +Infinity for (1e200, 0, 0); components below about 1.5e-162 squared to 0,
  so `Normalize()` threw "magnitude of 0" and `Magnitude()` returned 0 for (1e-170, 0, 0); between about 1e-162 and
  1e-154 the subnormal sum of squares lost digits (`Normalize()` of (1e-160, 1e-160, 0) 5.5e-6 off 1/√2, silently).
  `FromQuaternion`, `TransformCovariance`, `Lerp` and `SLERP` threw for a finite non-zero quaternion out of that range,
  `Lerp`'s chord overflowed between quaternions of opposite components near `double.MaxValue`, `Angle(Vector3)` gave 0
  between tiny vectors and NaN between huge ones, and `Angle(vector, plane)` threw for a tiny or huge normal and took
  atan2((a × b) · n̂, a · b) on unscaled vectors (π/4 instead of atan 2 from (1e200, 0, 0) to (1e200, 2e200, 0), NaN for
  most huge pairs, 0 below about 1e-154). A vector or
  quaternion whose sum of squares lies outside [1e-300, `double.MaxValue`] is now scaled first by the power of two of its
  largest component (`Math.ILogB`, `Math.ScaleB`, exact): every finite non-zero one has a unit vector and its
  representable magnitude (+Infinity only above `double.MaxValue`); `Lerp` recomputes on both quaternions scaled by one
  power of two a chord that overflows, or whose quaternions have their largest component below 1e-150 (its products
  rounded into the subnormal range: from 4.9e-324 (1, 0, 0, 0) to 4.9e-324 (0, 1, 0, 0) any t gave (1, 0, 0, 0), at 1e-320
  a component 4e-4 off); the signed `Angle` scales both vectors by the powers of two of their largest components (exact,
  the angle unchanged) unless both sums of squares lie in [1e-300, 1e300]. Every input whose sum of squares lies in
  [1e-300, `double.MaxValue`] (for `Lerp`, whose largest component is at least 1e-150; for `Angle(Vector3)` and the signed
  `Angle`, both sums in [1e-300, 1e300]) gives the same result bit for bit, so no result of the library on physical
  quantities moves. The axis-angle constructor of #688 takes the same scaling (bit for bit in its former range). A
  spacecraft body axis that is tiny or huge but finite is now its direction (`new Spacecraft(..., bodyFront: (0, 1e-170,
  0))` threw naming `bodyFront`), and the library's own rotation between two vectors (the lenient form of `Vector3.To`)
  is the rotation of tiny non-zero vectors, where it was the identity. The tests take closed forms exact in doubles: (2, −3, 6) 2^k
  normalizes to (2/7, −3/7, 6/7) for k from −1070 to 1021 and (1, −2, 2, 4) 2^k to (0.2, −0.4, 0.4, 0.8) from −1070 to
  1000;
  |(3, 4, 0) 2^1000| = 5 · 2^1000 and |(3, 4, 0) ε| = 5ε exactly; π/4 between (1, 0, 0) and (1, 1, 0) at 2^±1000 within
  one ulp; `FromQuaternion((1e200, 1e200, 0, 0))` against Rx(90°) and `SLERP(2^1000 I, 2^-1000 Rx(90°), 0.5)` against
  Rx(45°) (MEASURE 2.220e-16 and 0); `TransformCovariance` by (1e200, 1e200, 0, 0) and (1e-170, 1e-170, 0, 0) against
  Rx(90°) P Rx(90°)ᵀ (3.553e-15, two ulps of 9); the chord from (1e308, 0, 0, 0) to (−1e308, 1e308, 0, 0) at 0.5,
  (0, 1, 0, 0) exactly; `Lerp` from s (1, 0, 0, 0) to s (0, 1, 0, 0) for s from 4.9e-324 to 1e-160 against
  (1 − t, t, 0, 0) / √((1 − t)² + t²) (3.331e-16); the signed `Angle` from (1, 0, 0) 2^ka to (1, 2, 0) 2^kb within one
  ulp of atan 2 (ka, kb from −1074 to 1022), and 1e4 directions in a plane at scales 2^-960 to 2^1022 against β − α
  (8.882e-16 rad); and 1e4 vectors of magnitude 2^-1075 to 2^1023 whose exact direction is the vector scaled back by
  an exact power of two (| |u| − 1 | 2.220e-16, |u × d| / |d| 1.221e-16, the magnitude 1.940e-16 relative against a
  double-double computation; 0.939 ulp per component and 2.125e-16 relative magnitude against an 80-digit decimal
  computation during the triage); bounds twice the worst over the three platforms (the same figures on the three, but
  the SLERP at 2^±1000, 5.6e-17 on Windows x64). `VectorTests`, `QuaternionTests`, `MatirxTests`, `SpacecraftTests`.
- **`Plane.GetAngle` returned NaN for parallel normals, for normals outside about 1e-154 to 1e154, and for a zero or
  non-finite normal or vector** (third round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)).
  The quotient (n1 · n2) / (|n1| |n2|) rounded past ±1 for parallel and antiparallel normals, where `Acos` and `Asin`
  return NaN: NaN in 478 904 of 2e6 pairs of planes (a, a s) and (a, −a s), s in [0.1, 10.1], and in 239 452 of 1e6
  angles of a vector a s to the plane of normal a. The magnitudes were unscaled (NaN for (1e200, 0, 0), π/4 1.3e-4 rad
  off for normals near 1e-160), and a zero or non-finite normal or vector (`default(Plane)` included) gave NaN silently.
  The quotient is now clamped to [-1, 1] (it changes only results that were NaN), computed through the scaled unit
  vectors of `Normalize` when a sum of squares lies outside [1e-300, 1e300], and a zero or non-finite normal or vector
  throws (Changed below). Normals whose sums of squares both lie in [1e-300, 1e300] give the former result bit for bit;
  a finite result of before with a sum of squares in (1e300, `double.MaxValue`] can move by one ulp (the cosine through
  the unit vectors). `CelestialBody.HelioSynchronousOrbit`, the library's one call (a normal of about 1e11 m and a unit
  vector), does not move. `PlaneTests` checks 1e5 parallel and antiparallel pairs against the closed forms
  0, π and ±π/2 (MEASURE 2.980e-8 rad on the three platforms, the √(2ε) of acos and asin one ulp from ±1; bound twice
  the worst), π/4 between normals at 2^±1000 and 2^-1070 within one ulp, and the throws.
- **The C# examples of the two `CLAUDE.md` files and of several pages of `docs/` did not compile or threw** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), third round).
  12 of the 27 blocks of `IO.Astrodynamics.Net/CLAUDE.md` failed against the library: the TLE example read `newTle`,
  which it never declared; the OMM example saved with `wrapInNdm` and `indent`, parameters of `Opm.SaveToFile` that
  `Omm.SaveToFile` does not take; the OMM ↔ TLE, OPM ↔ Spacecraft, OPM ↔ StateVector and atmosphere examples declared
  the same locals twice; the maneuver export called a `CreateSpacecraftWithManeuvers()` that does not exist and an
  undeclared `maneuver`; the user-defined parameters and covariance examples built an `OpmData` from a `StateVector`
  (it takes an `OpmStateVector`, from `OpmStateVector.FromStateVector`); the geopotential and drag examples wrote
  `Frames.Frame`, which does not resolve in an application's namespace; the name-shadowing example was three bare
  member accesses, not statements. Two more threw: the Earth observation TRIAD pointed `Spacecraft.Down` at the Earth
  and `Spacecraft.Up`, opposite to it, at the Sun, which `TriadAttitude` rejects (its body vectors at least 5 degrees
  apart), now `Spacecraft.Right` (+X), which sets the roll about nadir (the solar panels stay on `Up`, as in the
  prograde examples and `CreateProgradeWithSunTracking`); and the geopotential example, read with an orbit about
  `EARTH_BODY`, listed an Earth of another gravitational field, which a propagation rejects (#674), now with its orbit
  built on the degree-10 Earth. The root `CLAUDE.md`'s in-memory provider example now says it runs in a child process
  in `IO.Astrodynamics.Tests`. Every block now compiles: each is the region of a method of
  `IO.Astrodynamics.Tests/Documentation/ClaudeMdExamples.cs` (the root `CLAUDE.md`'s blocks too), and
  `ClaudeMdExamplesTests` checks that each block equals its region, compares the declared `IIntegrator` with the
  library's by reflection, runs the 20 blocks that need only `Data/SolarSystem` (the maneuver export on a spacecraft
  propagated through an apogee raise, so that its loop exports the impulse), and checks what they state: the catalog
  number of the TLE text, the mean motion of the OMM in rad/s, the OPM state and the exported impulse in the units of
  CCSDS 502.0-B-3, the 1976 standard atmosphere at 10 km against its table I, and the Earth observation attitude, Down
  on the nadir and Right on the Sun's direction perpendicular to it (2.5e-16 and 6.5e-16 rad on the three platforms;
  bounds 5.0e-16 and 1.3e-15 rad, twice the worst). The same TRIAD of
  opposite vectors, passed a minimum separation of 0 so as not to throw, stood in `docs/tutorials/maneuvers.md`, where
  its roll was not toward the Sun, and a second TRIAD of that page passed 0 too, which turned off the guard on
  collinear reference vectors; both keep the default 5 degrees. `docs/` wrote `Frames.Frame` 14 times in 8 pages
  (`get-started`, the orbit-creation and ephemeris-queries tutorials, the force-models, celestial-bodies,
  state-vector, occultations and spacecraft references), now `Frame` with `using IO.Astrodynamics.Frames;`; and,
  compiled whole with their own using directives, `get-started` and the orbit-creation and ephemeris-queries tutorials
  failed on named arguments the constructors do not have (`KeplerianElements(a:, e:, ...)`, `Spacecraft(id:, ...,
  fuelTank:, engine:)`, `HelioSynchronousOrbit(..., epoch:)`) and on `Position` read from the `OrbitalParameters` of a
  window's ephemeris, now corrected. `DocumentationPagesTests` checks the csharp blocks of `docs/` and `README.md` for
  a type named through a namespace relative to `IO.Astrodynamics` and for a `TriadAttitude` of minimum separation 0.
  The block and region reading of `DeveloperGuideExamplesTests` is shared (`DocumentationCodeBlocks`). Documentation
  only: no library result moves.
- **`TriadAttitude` let collinear vectors through, and accepted a separation that switched its checks off** (fourth
  round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The constructors stored `minimumVectorSeparation` unchecked, and the body vectors (at construction)
  and the reference directions (in `ComputeOrientation`) were rejected when acos of their clamped cosine was below the
  separation or above π − separation. A separation of 0, a negative one or NaN made both tests false for every pair:
  antiparallel or parallel body vectors, and nadir with zenith or with the Earth's direction, gave the same arbitrary
  roll (a half turn about (−1, 0, 1)/√2 on a LEO, the primary on its target), without error, as the TRIAD of
  `docs/tutorials/maneuvers.md` met (above). A tiny positive separation did not close it: π − 1e-300 rounds to π, so
  exactly antiparallel vectors passed, and acos cannot resolve angles near 0 or π, so collinear vectors that round apart
  (v and fl(k v)) were measured up to 3.3e-8 rad from 0 or π (23.8 % of 2e5 random pairs) and passed any separation
  below that. A separation above π/2 rejected every pair, perpendicular ones included, as "too close to collinear". A
  NaN body vector passed at the default separation and gave a NaN quaternion. Two vectors now pass when the sine of
  their angle, |û × ŵ| of their unit vectors, is at least sin(separation): the same angles away from the boundary, but
  without the rounding of π − separation and the resolution of acos. Vectors collinear to the rounding of their
  components have a sine of at most about (3 + √2)·2^-53 = 4.9e-16 (measured up to 2.4e-16 on the same pairs, 1.8e-16
  on 2e4 others), which a separation below it still let through (v and fl(2.9 v) at 1e-17 gave one roll, fl(−2.9 v)
  another), so the separation is now in [1e-14, π/2] rad, 20 times that bound, and a collinear, zero or non-finite pair
  is rejected at every separation allowed (Known limitations: subnormal components). Every attitude accepted is
  unchanged, bit for bit: the triads, the matrices and the quaternion are computed as before. `TriadAttitudeTests`
  checks each separation out of range (0, −0, −0.1, NaN, ±∞, 2, the double above π/2, 1e-300, 1e-17 and the double
  below 1e-14) on the four constructors, 1e-14 accepted with the attitude of the default separation, π/2 on
  perpendicular vectors and targets against the closed form, antiparallel body vectors and targets and parallel targets
  at 1e-14, 2e4 random pairs v and fl(k v) at 1e-14, v and fl(±2.9 v), fl(±1.1 v) (acos 2.1e-8 and 1.5e-8 rad from
  collinear) at 1e-12, and NaN and infinite body vectors (Changed, Breaking, below).
- **A keyword out of its range escaped the OMM and OPM readers as an `ArgumentException`, and an enumerated CDM keyword
  as a `FormatException`** (fourth round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The readers passed the parsed
  values straight to the public constructors of the data model (`MeanElements`, `SpacecraftParameters`,
  `TleParameters`, `OmmMetadata`, `CcsdsHeader`, `UserDefinedParameter`, `OpmKeplerianElements`, `OpmData`,
  `OpmManeuverParameters`, `OpmMetadata`), whose argument checks throw `ArgumentOutOfRangeException` or
  `ArgumentException`, and wrapped only `XmlException` and their own parse failures, so the constructor's exception
  crossed `Omm`/`Opm.LoadFrom*` and `OmmReader`/`OpmReader.ReadFrom*`, whose documented exception is the reader's parse
  exception, naming a constructor parameter (`Eccentricity must be non-negative. (Parameter 'eccentricity')`). Without
  `validateSchema` it escaped for every range check (an OMM `ECCENTRICITY` of -0.5, `INCLINATION` of 190,
  `RA_OF_ASC_NODE` of 360, `MEAN_MOTION` of 0, `ELEMENT_SET_NO` of 10000, a negative `MASS`; an OPM `SEMI_MAJOR_AXIS` of
  the wrong sign for its `ECCENTRICITY`, `MAN_DELTA_MASS` of 1; an empty `OBJECT_NAME`, `ORIGINATOR` or
  `MAN_REF_FRAME`), and with the default validation for the values the NDM/XML 4.0.0 schema accepts: an OMM
  `MEAN_MOTION` or `SEMI_MAJOR_AXIS` not above 0 (`revType` and `distanceType` are `xsd:double`), the OPM
  `SEMI_MAJOR_AXIS` sign and `MASS` 0 (`massType` is a `nonNegativeDouble`, `OpmData` required a mass above 0, read since #708), and the
  empty strings (`xsd:string`). The CDM reader, which has no schema step, let the `FormatException` of
  `CdmEnumExtensions` cross every read for an `OBJECT` of `OBJECT3`, a `REF_FRAME` of `TEME`, a `COVARIANCE_METHOD`,
  `MANEUVERABLE`, `SCREEN_VOLUME_FRAME`, `SCREEN_VOLUME_SHAPE`, `SOLAR_RAD_PRESSURE`, `EARTH_TIDES` or `INTRACK_THRUST`
  that is not one of its values, where `OBJECT_TYPE` already threw `CdmParseException`. Under the same signatures, the
  readers build each block through one internal helper (`CCSDS/Common/CcsdsValues`), which maps the parameter the
  `ArgumentException` names to its keyword and throws `OmmParseException` or `OpmParseException` naming the keyword, its
  text, its block and the data model's rule, with the `ArgumentException` as `InnerException`
  (`ECCENTRICITY '-0.5' in meanElements is invalid: Eccentricity must be non-negative.`; for the OPM
  `SEMI_MAJOR_AXIS` the `ECCENTRICITY` it was checked with too); the CDM reader catches the `FormatException` of the
  enumerated keywords, `OBJECT` and the YES/NO keywords included, and throws `CdmParseException` naming the keyword and
  its value. `TryLoadFromFile` and `ValidateFile`, which caught every exception, now report the keyword. The rules stay
  those of the data model, whose public constructors keep their `ArgumentException` for a programmatic caller, and
  those the schema allows (an OMM `MEAN_MOTION` or `SEMI_MAJOR_AXIS` not above 0, the OPM `SEMI_MAJOR_AXIS` sign, the
  empty strings) are the library's rules beyond the schema ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), decided by S. Guillet,
  2026-11-16: no orbit has those elements, and CCSDS 502.0-B-3 makes the metadata mandatory with a non-empty value); a
  document that loaded loads as before, bit for bit. The tests change one keyword of iss.omm (with a GM and a
  spacecraftParameters block) or of satellite_complete.xml, both unqualified and schema-valid, or of a CDM of the
  library: every keyword with a range or a required string of the OMM and the OPM and every enumerated keyword of the
  CDM, through every entry point, with the NDM/XML 4.0.0 schema as the oracle of the value's validity where it has a
  facet (`OmmValidator`, `OpmValidator`, `Cdm.ValidateSchemaFromXml`), and the bounds of the facets (`ECCENTRICITY` 0,
  `INCLINATION` 0 and 180, `RA_OF_ASC_NODE` -360 and 359.999, `MASS` 0) read exactly
  (`ReadFromString_KeywordOutOfRange_*`, `ReadFromString_KeywordNotOneOfItsValues_ThrowsCdmParseException`,
  `ReadFromString_KeywordAtRangeBoundary_Parses`).
- **A CSPICE window search that SPICE could not run returned no window** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth round;
  [#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454),
  [#496](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/496)). The five native proxies of the
  window searches (`gfdist_c`, `gfoclt_c`, `gfposc_c`, `gfilum_c`, `gftfov_c`) read SPICE's error, reset it and returned
  no window, its message on standard output only: a frame name SPICE does not know (`new Frame("NOT_A_FRAME_668")` in a
  coordinate or illumination search), a body without ephemeris data (399999), a window past `de440s.bsp` or the Earth's
  PCK (2160), a kernel spacecraft searched past its SPK, an instrument whose kernel is not loaded (`INS-999123_FOV_FRAME`
  not found), a step of 0, a result cell that overflows: an empty result a caller could not tell from a condition that
  never holds. The proxies now return `bool` and keep SPICE's message for `GetLastErrorProxy()`, as `ReadEphemerisProxy`
  does (a native change for a technical reason: an error a proxy drops), and the five `SpiceAPI.FindWindows*` throw
  `InvalidOperationException` with it, under every public route that reaches them: the CSPICE paths of
  `CelestialItem.FindWindowsOnDistanceConstraint`, `FindWindowsOnOccultationConstraint` and
  `FindWindowsOnCoordinateConstraint`, of `Site.FindWindowsOnDistanceConstraint`, `FindWindowsOnOccultationConstraint`,
  `FindWindowsOnCoordinateConstraint`, `FindWindowsOnIlluminationConstraint` and `FindDayWindows` (so
  `Launch.FindLaunchWindows` by day or by night), of `Instrument.FindWindowsInFieldOfViewConstraint`, and the five CLI
  commands `find-windows-from-*-constraint`. The reference is SPICE's own error signal, read through the native
  library on every platform (`SpiceApiTest`, `KernelWindowSearchTests`, `FieldOfViewReferenceTests`, the CLI's
  `GeometryFinderTests` and the C++ `API` tests). A search SPICE runs
  is unchanged, bit for bit: the same native call, copy and labelling (the CSPICE goldens of `SpiceApiTest`,
  `KernelWindowSearchTests`, `OccultationSearchValidationTests` and `FieldOfViewSearchTests`). The 1000-window cap of
  the copy stays ([#653](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/653)).
- **`SpiceAPI.TransformFrame` threw for the frame of a site only the library holds** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth round). The frame of
  a site built from coordinates, and of a site from the kernels whose frame id (1000000 + NaifId) no loaded kernel
  defines, is the library's: the reads in it are rotated (#682, third review of #690), and `ToFrame` and
  `GetHorizontalCoordinates` use its orientation, but `SpiceAPI.TransformFrame` handed its name, `Name + "_TOPO"`, to
  SPICE, which does not know it, and threw `InvalidOperationException`. It now composes the transform through ICRF, as
  for TEME, decided at each call (a frame kernel of its id loaded later is read by name): the orientation of
  `SiteFrame.GetStateOrientationToICRF`, bit for bit `ToFrame`'s, within 9.3e-16 rad and 5.7e-20 rad/s of SPICE's
  `sxform_c` in the frame kernel `WriteFrameAsync` writes for the site, to and from ICRF and ITRF93 (the same on Linux
  x64, Windows x64 and macOS arm64, CI run 43773937161). The frame of a site whose frame the kernels define, the frames of spacecraft (SPICE's by name, which reads the
  frame kernel and the CK written for them) and CIRS and TIRS ([#656](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/656)) keep their path; TEME's composition is the same code, bit for bit.
- **The documentation of a site observer of `SpiceAPI.ReadEphemeris` by identifiers stated no condition** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth
  round). The observer of a site identifier is a `Site` built from its SPICE name and read at J2000 TDB (#672), so a
  site no loaded kernel names throws `ArgumentException` (`naifId`, "has no name in SPICE") and one whose SPK does not
  cover J2000 TDB `InvalidOperationException` (SPICE's message at 2000 JAN 01 12:00:00), which the XML documentation of
  both overloads and the root `CLAUDE.md` did not say. They now do, with the workaround for `Aberration.None`: read with
  the site as target and negate the state. Documentation only: no behaviour changes
  (`SpiceApiTest.ReadEphemeris_FromAnUnnamedWrittenSite_ThrowsAndTheTargetReadWorks` pins it).
- **A kernel writer or loader given a bad argument or an odd path ended the process** ([#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706)). The native
  writers and loaders let a C++ exception cross P/Invoke (`std::terminate`, exit code 134, measured on c533a6e):
  `SpiceAPI.WriteEphemeris` with one or two states (the native rule is three or more, its message said "2 items or
  more"), or states in two frames, `Site.WriteEphemeris` over a window its 1-minute step samples twice,
  `WriteEphemeris`, `WriteOrientation` and `LoadKernels` of a loop of symbolic links (`filesystem_error`, `ELOOP`), and,
  on Windows, from the code (not measured), a native writer replacing a file SPICE still held open; a CK of no record read an empty array (a
  segmentation fault, at the native level only, the .NET layer rejecting it since #560). The four proxies now catch
  every C++ exception and return false with its message (a native change for a technical reason, no signature changed),
  and `SpiceAPI.WriteEphemeris` rejects the states the native writer cannot write before the call (Changed, Breaking,
  below). The C++ tests `API.WriteEphemerisProxyOf*`, `API.WriteOrientationProxyOf*` and
  `API.KernelProxiesOfASymbolicLinkLoopReturnFalse`, and the child scenarios of `KernelUnloadChildTests`, exit 134 or
  fail on c533a6e. Writes of three states or more in one frame about a celestial body are unchanged, bit for bit.
- **A native writer that replaced a loaded file left SPICE reading the replaced data** ([#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706)). The writer's kernel
  object loaded the file once more and unloaded it once; `furnsh_c` of a loaded file adds a KEEPER entry of the same
  handle, so the caller's load stayed, holding the replaced file, deleted but open: `GetLoadedKernels` listed it and every
  read returned the replaced states (measured on c533a6e: x = 6800.001 km at +60 s where the new file holds 6805 km, and
  no data in the new coverage), until the file was unloaded. `WriteEphemeris`, `WriteOrientation`, `Star.WriteEphemeris`,
  `Site.WriteEphemeris` and `Spacecraft.WriteOrientation` now unload a target `LoadKernels` loaded, as `UnloadKernels`
  does, and the native layer every other load of its name (`KernelsLoader::UnloadEveryLoad`) (Changed, Breaking, below).
  `Spacecraft.WriteEphemeris` unloaded the file in SPICE but left it listed; `GetLoadedKernels` no longer lists it
  (`KernelRegistryTests.*_ReplacingALoadedFile_*`, red on c533a6e).
- **A writer left a second load, or a load through a symbolic link, of the file it replaced** ([#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706),
  review). `Spacecraft.WriteEphemeris` made one native unload of its target, so a file two meta-kernels held kept a KEEPER
  entry, and every writer compared names as strings, so a load under a symbolic link to the target (or in a directory
  loaded through a link) stayed in SPICE and listed: SPICE read the replaced data (measured on 9338a40: x = 6800 km at
  +60 s, no state at +180 s that only the new file holds). Every writer now unloads each name KEEPER holds and each file
  of the registry whose name resolves through the target, the same string or an absolute name whose symbolic links,
  followed one at a time, reach the target's entry (`KernelsLoader::ResolvesThrough`, through the new proxies
  `UnloadEveryLoadProxy` and `KernelNameResolvesThroughProxy`, a native change for a technical reason: KEEPER's list is
  read by native code only, and the .NET registry follows the native rule). A hard link and the file a link given to the
  writer names keep what they name and stay loaded; a relative name other than the path (a meta-kernel's) is not found
  (C++ tests `API.*ThroughSymbolicLinks*`, `API.*ThroughASymbolicLink*`, `API.UnloadEveryLoadProxy*`,
  `API.KernelNameResolvesThroughProxy`; `KernelRegistryTests.Writers_ReplacingAFileTwoMetaKernelsHold_UnloadEveryLoad`
  and `Writers_ReplacingAFileLoadedThroughASymbolicLink_UnloadIt`, red on 9338a40).
- **`Spacecraft.WriteEphemeris` ignored a failed unload of the file it replaced** ([#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706)). When the unload of the
  target failed (KEEPER's reopening of the other SPKs, on a deleted one SPICE holds under a relative name), it replaced
  the file all the same and reported nothing, SPICE printing `SPICE(FILENOTFOUND)` on the standard output. It now
  throws `InvalidOperationException` with SPICE's message and keeps the file as it was, as the native writers do
  (child scenarios `SpacecraftWriteEphemeris_Replacing*_WhenAnSpkHeldUnderARelativeNameWasDeleted_*`). `LoadKernelsProxy`
  and `UnloadKernelsProxy` reset SPICE without keeping its message; they keep it for `GetLastErrorProxy`, and the
  `InvalidOperationException` of `LoadKernels` and `UnloadKernels` carries it ("Kernel ... can't be unloaded:
  SPICE(FILENOTFOUND) ...") where it said "You can have more details on standard output". The exception types are
  unchanged.
- **The CCSDS readers loaded NaN and infinite values** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). The `ParseDouble` of `OmmReader`, `OpmReader` and
  `CdmReader` used `double.TryParse` with the invariant culture, which reads `NaN`, `Infinity` and `-Infinity` in any
  case and, since .NET Core 3.0, rounds an overflow (`1e400`) to ±Infinity; nothing checked the result, and every range
  check of the data model and of the validators (`x < 0`) is false for NaN. With the default `validateSchema` and
  `validateContent`, an OMM with `ECCENTRICITY`, `MEAN_MOTION`, `INCLINATION`, `RA_OF_ASC_NODE`, `BSTAR` or
  `MEAN_MOTION_DOT` of `NaN` loaded, so did an OPM with `ECCENTRICITY`, `MASS`, `SEMI_MAJOR_AXIS`, `X`, `GM`, `CX_X`,
  `MAN_DURATION` or `DRAG_COEFF` of `NaN` or `Infinity`, and a CDM with a `MISS_DISTANCE`, `X` or `CR_R` of `NaN`,
  `Infinity` or `1e400`, which `Cdm.Validate` reported valid: .NET's schema validator accepts NaN against the bound
  facets of NDM/XML 4.0.0 (`nonNegativeDouble`, `inclinationRange`, `angleRange`) and `Infinity` or an overflow as an
  `xsd:double` (libxml2 rejects `Infinity`). Each `ParseDouble` now throws the reader's parse exception for a value that
  is not finite, naming the keyword and the text (`Failed to parse numeric value 'NaN' for field 'ECCENTRICITY': not a
  finite number.`; for the CDM `Invalid numeric value for MISS_DISTANCE: 'NaN' is not a finite number.`), for every
  numeric keyword of the three messages, and so `Load*`, `TryLoadFromFile` (a `PARSE_ERROR`) and `ValidateFile`. A
  finite document reads bit for bit as before, and `INF` and `-INF` throw the parse exception as before
  (`OmmReaderTests`, `OpmReaderTests`, `OmmTests`, `OpmTests` and `CdmTests`
  `*_NonFiniteValue_*`, through every entry point, red on 46e6975). The CDM reader checks the value it holds after its
  conversion to m or m/s as well: a finite text in km or km/s above about 1.8e305 (`1e306` for `X`, `Z`, `X_DOT`,
  `MISS_DISTANCE`, `RELATIVE_POSITION_R`, `RELATIVE_VELOCITY_T`) became ±Infinity by its product by 1000 (IEEE 754), which
  `Cdm.Validate` reported valid; it throws `CdmParseException` naming the keyword, its text and its unit
  (`Invalid numeric value for X: '1e306' km is not a finite number in SI units.`), and 1e305 km reads as before, bit for
  bit (`CdmTests.ReadFromString_FiniteValueOverflowingInSiUnits_ThrowsCdmParseException`, red on the first fix of #708;
  `ReadFromString_LargestValueFiniteInSiUnits_ReadsTheProduct`).
- **An OPM `MASS` of 0 threw** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). `OpmData` required a mass above 0 and `OpmValidator` reported "Mass must be
  positive.", where NDM/XML 4.0.0 types `MASS` as `massType`, a `nonNegativeDouble` (`minInclusive` 0.0), the type of
  the OMM's `MASS` too, which `SpacecraftParameters` accepts at 0: a schema-valid OPM with a `MASS` of 0 threw
  `OpmParseException` (`MASS '0' in spacecraftParameters is invalid: Mass must be positive.`) with or without schema
  validation. `OpmData` and `OpmValidator` now take a mass of 0 or more, so the OPM loads, `Data.Mass` 0 exactly, and
  `Opm.ToSpacecraft` builds a spacecraft of mass 0, as `CelestialItem` allows (`OpmReaderTests.ReadFromString_MassZero_ReadsZero`,
  `OpmValidatorTests.Validate_MassZero_NoError`, red on 46e6975, the schema their oracle).
- **The messages of the `MeanElements` angles misstated their range** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). The checks of `RA_OF_ASC_NODE`,
  `ARG_OF_PERICENTER` and `MEAN_ANOMALY` take [-360, 360), the `angleRange` of NDM/XML 4.0.0 (`minInclusive` -360.0,
  `maxExclusive` 360.0), but the messages said "between -360 and 360 degrees", which 360, refused, is; the reader
  carried that text into its `OmmParseException`. They now say "RAAN must be in [-360, 360) degrees." (and likewise for
  the argument of pericenter and the mean anomaly), and the XML documentation of the three properties, which said
  "0 to 360", states [-360, 360). No value is accepted or refused differently
  (`MeanElementsTests.Constructor_AngleAt360_MessageStatesHalfOpenRange`,
  `OmmReaderTests.ReadFromString_AngleAt360_MessageStatesHalfOpenRange`, red on 46e6975).
- **The constructors of the CCSDS data model accepted NaN and infinite values** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). Each range check was the
  complement of the range (`x < 0`, `x < -360 || x >= 360`), false for NaN, and the half-open ranges took +Infinity:
  `MeanElements.CreateWithMeanMotion` accepted an eccentricity or a mean motion of NaN and an eccentricity of +∞,
  `new SpacecraftParameters(mass: NaN)`, `OpmKeplerianElements.CreateWithTrueAnomaly(semiMajorAxis: NaN, ...)` and
  `new OpmManeuverParameters(manDuration: NaN, ...)` built, against their documented ranges. Each check is now the
  negation of the admitted range, with a finite value where the range is half-open: `MeanElements` (a and n finite and
  above 0, e finite and 0 or more, i in [0, 180], the three angles in [-360, 360), GM finite and above 0),
  `SpacecraftParameters` and `OpmData` (the five spacecraft parameters finite and 0 or more, through the internal
  `CcsdsValues.IsNullOrFiniteNonNegative`), `OpmKeplerianElements` (e finite and 0 or more, a finite, i in [0, 180], GM
  finite and above 0) and `OpmManeuverParameters` (`manDuration` finite and 0 or more, `manDeltaMass` finite and 0 or
  less), the same `ArgumentOutOfRangeException` naming the parameter. The parameters without a range (the state
  vector, the covariance, the ΔV components, the TLE parameters, the CDM records) were left unchanged here; [#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717)
  extends the rule to them (below). Finite values build bit
  for bit as before (`MeanElementsTests`, `SpacecraftParametersTests`, `OpmDataTests`, `OpmKeplerianElementsTests` and
  `OpmManeuverParametersTests` `*_NonFinite*`, NaN, +∞ and −∞ in each ranged parameter, red on 46e6975; the bounds
  e 0 and −0, i 0 and 180, the angles −360, a mass and a delta mass of 0 stored as given).
- **The CCSDS content validators reported NaN valid** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). The error checks of `OmmValidator`, `OpmValidator` and
  `CdmValidator` were the complement of their range (`CxX < 0`, `MissDistanceMeters < 0.0`, ...), false for NaN: a
  covariance built with `new CovarianceMatrix(...)` or `CovarianceMatrix.FromMatrix` with a NaN variance, which the
  covariance constructors do not range-check, and a `CdmRelativeMetadataData` with a NaN miss distance were reported
  valid, against the validators' own rules ("must be non-negative"). Each error check is now the negation of its
  admitted range, with a finite value where the range is half-open, so NaN is an `InvalidRange` error (`OMM002`,
  `OPM002`) or a CDM content error naming its value (`Variance CX_X must be finite and non-negative, got NaN.`); the
  warnings and the information levels are unchanged (`OmmValidatorTests` and `OpmValidatorTests`
  `Validate_NaNVariance_ReportsError`, each variance; `CdmTests.Validate_NaNMissDistance_IsInvalid`; red on 46e6975).
  Since [#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717) a NaN or infinite variance or miss distance no longer builds, so these errors are reached through a
  negative value (`Validate_NegativeVariance_ReportsError`, `Validate_NegativeMissDistance_IsInvalid`, which replace them).
- **The OMM content validation refused a negative angle the schema accepts** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). `OmmValidator` checked
  `RA_OF_ASC_NODE`, `ARG_OF_PERICENTER` and `MEAN_ANOMALY` against [0, 360) ("must be 0-360 degrees"), where the
  `angleRange` of NDM/XML 4.0.0 and `MeanElements` take [-360, 360): `Omm.LoadFrom*` with the default `validateContent`
  threw `OmmValidationException` for a schema-valid angle of -360 or -10, which `validateContent: false` loaded. The
  three checks now take [-360, 360) ("must be in [-360, 360) degrees"), NaN refused. `Omm.ToTle` writes such an angle
  as its value in [0, 360) (-10 as 350.0000, measured) (`OmmValidatorTests.Validate_NegativeAngleInSchemaRange_NoError`,
  -360, -10 and -0 for each angle, the schema its oracle, red on 46e6975 for -360 and -10).
- **The OMM content validation refused a `MASS` of 0** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). `OmmValidator` reported "MASS must be positive",
  where `massType` of NDM/XML 4.0.0 is a `nonNegativeDouble` and `SpacecraftParameters` accepts 0: the default
  `Omm.LoadFrom*` threw `OmmValidationException` for a schema-valid OMM that `validateContent: false` loaded. The check
  now reports a mass that is not a finite value of 0 or more ("MASS must be non-negative"), as for the other spacecraft
  parameters and the OPM (`OmmValidatorTests.Validate_MassZero_NoError`, the schema its oracle, red on 46e6975;
  `OmmReaderTests.ReadFromString_KeywordAtRangeBoundary_Parses` now loads its bounds, `RA_OF_ASC_NODE` -360 and `MASS`
  0 included, with the default validation).
- **The angles of an OPM had no range** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). `RA_OF_ASC_NODE`, `ARG_OF_PERICENTER`, `TRUE_ANOMALY` and
  `MEAN_ANOMALY` are `ndm:angleType` in ndmxml-4.0.0-opm-3.0.xsd, `angleRange` [-360, 360) (`minInclusive` -360.0,
  `maxExclusive` 360.0), the range `MeanElements` checks for the OMM, and the XML documentation of
  `OpmKeplerianElements` said "-360 to 360", but nothing checked them: `CreateWithTrueAnomaly(..., raan: NaN, aop: +∞,
  trueAnomaly: NaN, ...)` built and `OpmValidator` reported it valid, and `Opm.LoadFrom*` with `validateSchema: false`
  loaded an angle of 1000, -720 or 360, which the schema rejects. `OpmKeplerianElements.CreateWithTrueAnomaly` and
  `CreateWithMeanAnomaly` now throw `ArgumentOutOfRangeException` naming `raan`, `aop`, `trueAnomaly` or `meanAnomaly`
  for an angle outside [-360, 360), NaN and ±Infinity included ("RAAN must be in [-360, 360) degrees."), and the reader
  `OpmParseException` naming the keyword and its text; -360, -0, -10 and the largest double below 360 are stored as
  given. `FromKeplerianElements` writes each angle as its value in [0, 360): for an angle of 2π, which the [0, 2π)
  normalization of `KeplerianElements` gives for -1e-17 rad, it wrote 360, now 0 (an angle in [0, 360) bit for bit as
  before); `Spacecraft.ToOpm` builds its elements through it, its output unchanged
  (`OpmKeplerianElementsTests.Create_AngleOutOfSchemaRange_ThrowsArgumentOutOfRangeException`, 28 rows,
  `FromKeplerianElements_AngleAtTwoPi_WritesItsValueInZeroTo360`, `OpmReaderTests` rows `RA_OF_ASC_NODE` 360,
  `ARG_OF_PERICENTER` -360.5, `TRUE_ANOMALY` 1000 and `ReadFromString_MeanAnomalyOutOfSchemaRange_*`, `OpmTests` rows
  without the schema, red on the first fix of #708, the schema their oracle; `Create_AngleAtSchemaBounds_StoresTheValueGiven`,
  `OpmTests.ToOpm_RetrogradeEquatorialOrbit_WritesAnglesInSchemaRange`, guards). `OpmValidator` checks no angle: the
  constructor refuses one out of range, so no `Opm` holds it.

- **An element conversion about a centre the native layer cannot read ended the process or returned a zero state** ([#711](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/711),
  [#655](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/655)). The native conversions build the centre of the elements as a celestial body, which threw a C++
  exception across P/Invoke for a centre SPICE does not name: `SpiceAPI.ConvertConicElementsToStateVector` (an ellipse)
  and `ConvertEquinoctialElementsToStateVector` about a spacecraft whose GM a kernel gives ended the process (exit code
  134, measured on 46e6975). For a centre the kernel pool gives no GM, the failed GM read left SPICE failed, so that the
  name read after it wrote nothing and the native code read an indeterminate flag and name: about Elara (507, named,
  no `BODY507_GM`) both returned the state (0, 0, 0) silently, about a spacecraft or L1 without a GM the result was
  undefined. The three native conversions (`ConvertStateVectorToEquatorialCoordinatesProxy` included, which .NET does
  not call) now catch the exception and return a NaN result with the exception's message then SPICE's in
  `GetLastErrorProxy`, SPICE reset, and `CelestialBody(int)` initialises the flag, the name and the GM it reads, a
  missing GM reading 0 (a native change for a technical reason, no signature changed); `SpiceAPI` rejects the centres
  the native layer cannot read before the call and throws on a NaN state (Changed, Breaking, below). C++ tests
  `API.Convert*ProxyOfAnUnnamedCentreWithAGmReturnsNaN` and `API.Convert*ProxyOfACentreWithoutGmReturnsNaN`, child
  scenarios `ConvertConicElementsToStateVector_AboutASpacecraftWithAGm_ThrowsArgumentException` (and its equinoctial
  analog, exit 134 on 46e6975) and `ConvertConicElementsToStateVector_AboutABodyNoLongerNamed_ThrowsArgumentException`,
  `SpiceApiTest.Convert*ElementsToStateVector_AboutABodyWithoutGm_ThrowsInvalidOperationException`.
  `ConvertConicElementsToStateVectorAtEpochProxy`, under `ConvertConicElementsToStateVector(KeplerianElements, Time[, gm])`
  and the open orbits of the one-argument overload, neither activated the error management nor checked SPICE: a failed
  `conics_c` (a GM of 0, about Elara for instance) returned the state it never wrote and left SPICE failed, so that the
  next native call read the Earth as an unnamed body (`GetCelestialBodyInfoProxy(399)` gave `Id` -1, measured on a77531f)
  and the conversion threw "Celestial celestialItem name can't be defined"; in a process that had made no other call,
  SPICE's default action aborted it. It now returns the NaN state with SPICE's message, SPICE reset (C++
  `API.ConvertConicElementsToStateVectorAtEpochProxyOfANonPositiveGmReturnsNaN`, the message of CONICS's
  `SPICE(BADGM)`; `SpiceApiTest.ConvertConicElementsToStateVector_OfAnOpenOrbitAboutABodyWithoutGm_ThrowsInvalidOperationException`).
  Elements outside the domains of `EQNCPV` and `CONICS` returned the zero state silently (measured on 46e6975: equinoctial
  e = 0.95 and a < 0, an ellipse of a < 0); they throw `ArgumentException` (Changed, Breaking, below;
  `SpiceApiTest.ConvertConicElementsToStateVector_OfAPeriapsisNotAboveZero_ThrowsArgumentException`,
  `ConvertEquinoctialElementsToStateVector_OutsideTheDomainOfEqncpv_ThrowsArgumentException`,
  `ConvertConicElementsToStateVector_WithAGmNotFiniteAndAboveZero_ThrowsArgumentOutOfRangeException`, the domains read
  from `eqncpv.c` and `conics.c` of CSPICE N0067). Conversions inside those domains about a celestial body SPICE names
  with a GM are unchanged, bit for bit.
- **A TLE read before its kernels threw a misleading message and stayed unusable** ([#711](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/711)). Without the
  kernels, the native Earth's GM read fails (`BODY399_GM`); `TLE.ToStateVector` threw "the native layer returned a zero
  state" while the cause was the missing kernels, and the epoch read returned the epoch `getelm_c` never wrote, which the
  TLE cached for its life: once the kernels were loaded, the same instance threw again or returned a state 1.6e9 m off
  (measured on 46e6975). The two TLE proxies now return NaN (every element, the epoch included) with SPICE's message on
  any failure, an SGP4 error included, and catch their C++ exceptions; `SpiceAPI.ConvertTleToStateVector` and the epoch
  read throw `InvalidOperationException` with SPICE's message ("The variable BODY399_GM could not be found in the kernel
  pool.", or SGP4's), and the TLE caches its epoch only once read, so that the same instance gives the SGP4 state once
  the kernels are loaded (child mode `tle-before-kernels` of `ColdStartTests`: 3.25e-4 m from `tools/sgp4_reference`,
  within the 7.2e-4 m of `Sgp4ReferenceTests`, bit for bit a fresh instance; C++ `API.ConvertTLEToStateVectorProxyOfAFailedSgp4ReturnsNaN`
  and `API.GetTLEElementsProxyOfAFailedSgp4ReturnsNaN`, the 33334 set of `Data/Sgp4Reference`). The exception type is
  unchanged; TLEs read with their kernels are unchanged, bit for bit.
- **`GetCelestialBodyInfoProxy` threw for a named spacecraft** ([#711](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/711)). The search of the centre of motion
  threw `InvalidArgumentException` out of the proxy for an identifier SPICE names outside the celestial categories
  (GALILEO ORBITER, -77); the .NET layer rejects such identifiers first (#560), so only a native caller met it. The proxy
  now returns the record of an identifier SPICE does not name (`Id` -1) with the message in `GetLastErrorProxy`
  (C++ `API.GetCelestialBodyInfoProxyOfANamedSpacecraftReturnsNoRecord`).
- **A failed `SpiceAPI.ReadOrientation` hid SPICE's error and left it pending for the next call** ([#712](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/712)).
  With no CK loaded, a frame SPICE does not know or no clock for the spacecraft, the read threw "No orientation found"
  and left SPICE failed, so that the next native call, an unrelated `LoadKernels` for instance, threw the stale message
  ("At least one CK file must be loaded by CKLPF before beginning a search.", measured on 46e6975); a missing clock's
  message was reset and lost. `ReadOrientationProxy` now checks SPICE after each clock conversion and after the fallback
  of `ckgpav_c` to `ckgp_c`, and returns false with SPICE's message, SPICE reset; its catch branch, and those of
  `LaunchProxy` and `ReadEphemerisProxy`, take SPICE's pending error with the exception's (a native change for a
  technical reason, no signature changed). The exception type is unchanged; a read outside the CK's coverage still
  throws "No orientation found", and ordinary reads are unchanged, bit for bit (child scenarios
  `ReadOrientation_WithNoCkLoaded_ThrowsSpicesMessage_AndLeavesNothingPending` and
  `ReadOrientation_WithoutAClock_ThrowsSpicesMessage`, C++ `API.ReadOrientationProxyOfAnUnknownFrameReturnsSpicesMessageAndLeavesNothingPending`,
  `API.ReadOrientationProxyWithoutAClockReturnsSpicesMessage` and `API.LaunchProxyOfASiteOnABodyWithoutGmLeavesNothingPending`).
  The tests of #706 that kept a second CK loaded so that this error did not reach the next call no longer do.
- **The two-body proxies aborted the process or returned the stack's contents** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714)).
  `Propagate2BodiesProxy` and `ConvertStateVectorToConicOrbitalElementProxy` activated no error management, reset SPICE
  without its message and returned the arrays `prop2b_c` and `oscltx_c` never wrote: as the first native call of a
  process, `SpiceAPI.Propagate2Bodies` and `ConvertStateVectorToConicOrbitalElement` with a GM of 0 or below ended it
  through SPICE's default action, ABORT (exit code 1, no managed exception); after any other call they returned about
  7e-310 for a GM below 0, a zero position or velocity, or a position parallel to the velocity, with no error, and so did
  the overloads that take the GM of an observer without one (Elara, 507), all measured on a80f6a5. Both proxies now
  return a NaN result (the epoch plus dt, the centre and the frame as given; NaN in every element) with SPICE's message,
  SPICE reset (a native change for a technical reason, no signature changed), and `SpiceAPI` throws (Changed, Breaking,
  below). C++ `API.Propagate2BodiesProxyOfAStateOutsideTheDomainOfProp2bReturnsNaN`,
  `API.ConvertStateVectorToConicOrbitalElementProxyOfAStateOutsideTheDomainOfOscltxReturnsNaN` and the death tests
  `API.*AsTheFirstCallReturnsNaN` (exit code 1 on the base), child modes `two-body-first-propagate` and
  `two-body-first-conic` of `ColdStartTests`, `SpiceApiTest.TwoBodyCalls_*` (the messages those of direct `prop2b_c` and
  `oscltx_c` calls, an ordinary state their results bit for bit; the domains read from `prop2b.c` and `oscelt.c` of
  CSPICE N0067).
- **A SPICE error with no long message was reported as a success** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714)). `ReportNativeErrors`, through which most
  proxies report, built its message from SPICE's long message alone, and returned true when it was empty: `prop2b.c`
  signals `SPICE(NONPOSITIVEMASS)`, `SPICE(ZEROPOSITION)`, `SPICE(ZEROVELOCITY)` and `SPICE(NONCONICMOTION)` with no
  `setmsg_`. Such an error is now reported by its short message (`SPICE(ZEROVELOCITY)`), the proxy returning false or
  its NaN result. Only a native caller met it before the two-body proxies reported through it.
- **The message of a failed frame transformation named a wrong cause** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714)). `TransformFrameProxy` checked SPICE only
  after `xf2rav_c`, the unwritten matrix of a failed `sxform_c` passed to the quaternion, then reset SPICE without its
  message and returned the zero quaternion, so that `SpiceAPI.TransformFrame` (and `Frame.ToFrame` through the
  `SpiceDataProvider`) threw "the frame data (a PCK, CK or frame kernel) does not cover this epoch or is not loaded" for an
  unknown frame too. The proxy reports right after `sxform_c` and catches C++ exceptions, returning a NaN rotation and
  angular velocity with SPICE's message; `TransformFrame` keeps the frames and the epoch and appends it
  (`SPICE(UNKNOWNFRAME)`'s "The frame NOT_A_FRAME was not recognized as a known reference frame.",
  `SPICE(FRAMEDATANOTFOUND)`'s "PCK data required to compute the orientation of the body-fixed frame ITRF93 for epoch
  2199 AUG 22 04:00:00.000 TDB were not found. ..."). The exception type is unchanged (C++
  `API.TransformFrameProxyOfAnUnknownFrameReturnsNaNWithSpicesMessage`,
  `API.TransformFrameProxyOutsideTheCoverageOfAPckReturnsNaNWithSpicesMessage`; `SpiceApiTest.TransformFrame_ThatSpiceCannotGive_*`,
  the text that of a direct `sxform_c` call).
- **The message of a centre without GM changed from run to run** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714), question 4 of #713). After a failed GM read
  SPICE is failed, `BODC2N` returns at once and `bodc2n_c` copies its own unwritten local into the caller's flag
  (`bodc2n_c.c` of N0067, `*found = fnd;`), so that initialising the flag (#711) did not make it determinate: about Elara,
  six runs of nine gave "CelestialItem id507 can't be found" before SPICE's message, three SPICE's alone, and the body
  was built with an empty name when the flag was not zero. Both `CelestialBody` constructors now throw `SDKException`
  `CelestialItem id<id> can't be read:` before `bodc2n_c`, SPICE's error left pending for the proxy, which appends it:
  "CelestialItem id507 can't be read: The variable BODY507_GM could not be found in the kernel pool." on every run (C++
  `CelestialBody.ConstructorsOfABodyWithoutGmThrowBeforeReadingItsName`, the whole message in
  `API.Convert*ProxyOfACentreWithoutGmReturnsNaN` and `API.LaunchProxyOfASiteOnABodyWithoutGmLeavesNothingPending`;
  `SpiceApiTest.Convert*ElementsToStateVector_AboutABodyWithoutGm_ThrowsInvalidOperationException`). The exception type
  is unchanged.
- **The last native error outlived its call** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714)). `lastError` was written only on failure, so that
  `GetLastErrorProxy()` returned the message of an earlier call after a proxy that succeeded or failed without a message
  of its own: a conversion of a NaN inclination threw "Native proxy call failed", or, after a failed conversion about
  Elara, Elara's message about the Earth's elements (measured on a80f6a5). Every proxy that reports through `lastError`
  now clears it when it starts (C++ `API.EveryReportingProxyThatSucceedsClearsTheLastError`, the 25 proxies).
  `ReadEphemerisProxy` clears it once, before its epochs, and keeps the message of the first epoch that fails, where
  `SpiceAPI.ReadEphemeris` stops (decided by S. Guillet, 2026-11-17; before, each failed epoch replaced the message, so
  the last one was named: C++ `API.ReadEphemerisProxyPastTheCoverageNamesTheFirstFailedEpoch`, red on 507f963).
  `SpiceAPI.ReadEphemeris` over a window checks every state before it rotates any into a managed frame, whose rotation
  calls `TransformFrameProxy` and so clears the message
  (`SpiceApiTest.ReadEphemeris_OverAWindowPastTheCoverageInAManagedFrame_ThrowsSpicesMessage`: the frame of a site on
  Mars past the end of de440s gives SPICE's message of the first failed epoch, 2150 JAN 22 12:00 TDB, as J2000 does; it
  named 2150 JAN 25 on 507f963).
- **A CK without angular velocity could not be read** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714)). `ckgpav_c` skips a segment without rates and returns
  found = false with no error ("Segments containing only pointing data will be skipped over", its Restrictions 1), and
  `ReadOrientationProxy` fell back to `ckgp_c` only on an error, so that `SpiceAPI.ReadOrientation` of a type-1 CK, or of
  a type-2 or type-3 CK written without rates, threw "No orientation found" (a type-3 CK written by `ckw03_c` without
  rates, measured on a80f6a5). The proxy falls back to `ckgp_c` when `ckgpav_c` finds nothing too; such a record has the
  pointing of `ckgp_c` and a zero angular velocity (decided by S. Guillet, 2026-11-17; C++
  `API.ReadOrientationProxyOfACkWithoutAngularVelocityReadsThePointing`, within 2.3e-16 rad of `ckgp_c` and of the
  closed form R_z(1e-3 t), twice the worst on Linux x64 and macOS arm64, where it runs;
  `SpiceApiTest.ReadOrientation_OfACkWithOrWithoutAngularVelocity_IsTheCkgpPointing`, within 3.2e-16 rad, the CK with
  rates (0, 0, 1e-3) rad/s, twice the Linux worst, Validation; CI run 43974478027). The CKs the library writes carry
  their rates and read as before, bit for bit.
- **The position of a star depended on the time frame of its epoch** ([#709](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/709)). `Star.GetEquatorialCoordinates`,
  `GetRightAscensionSigma` and `GetDeclinationSigma` took dt from the calendars of the catalog epoch and of the epoch,
  each in its own time frame, so that the same instant gave two positions (relative to the epoch in TDB: 2.299e-7 in UTC,
  1.153e-7 in TAI, 4.977e-12 in TT, for the star of 1 rad and 0.1 rad/yr of `StarTests`, measured on 46e6975), and two
  UTC times skipped the leap seconds between them (86 400 s from 2016-12-31T12:00Z to 2017-01-01T12:00Z, where 86 401 s
  elapse). dt now counts the Julian years of TDB: both times are converted to TDB (the catalog epoch once, when the star
  is built; `Epoch` is kept as given), as SOFA's `iauStarpm` and `iauPmsafe` take TDB dates and proper motions in
  radians per TDB Julian year (`external-lib/sofa-src/starpm.c` and `pmsafe.c`, Notes 1 and 2). The catalog epoch is
  read in its own time frame and converted (decided by S. Guillet, 2026-11-16; #709 left its scale open): `Epoch` stays
  an instant like every other `Time` of the library, where a catalog epoch always read as TDB whatever its tag would put
  a catalog epoch tagged UTC 64 to 69 s from the instant its tag names. The position of a time is that of its TDB time, bit for bit, and agrees within the motion of 1 µs
  between frames (`StarTests.GetEquatorialCoordinates_TheSameInstantInEveryFrame_IsOnePosition`); across the leap second
  it counts 86 401 s plus the change of TDB − TT, 0.000e+00 rad from the closed form of the leapseconds kernel
  (`AcrossALeapSecond_CountsIt`, 3.169e-9 rad before); against dt from CSPICE's ET (`unitim_c` of the TT of each UTC
  time), 3.553e-15 rad over 2001 and 2016-07 to 2017-06 (Validation, Linux, threshold 7.2e-15; 1.0e-14 for the straight line of #718). For a
  catalog epoch or an epoch in UTC, TAI or GPS a star moves by μ × (TDB − UTC or TAI), about 32 to 69 s of its motion:
  about 1e-11 rad for a real star of 1″/yr; in TT, under 1.7 ms of it. `Star.Propagate`, `GetEphemeris` and the star of
  an occultation or field-of-view search over a UTC window move the same way. Two times in TDB, every star of the
  suite, are unchanged, bit for bit.
- **A star whose declination passed a pole jumped toward the equator** ([#709](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/709)). `Star.GetEquatorialCoordinates` reduced
  δ0 + μδ dt modulo π/2: past a pole the declination folded toward the equator and the right ascension did not turn, a
  jump of up to π/2 in direction (1.5 rad + 0.1 rad/yr read 0.0092 rad after 0.8 yr), and a star at a pole read the
  equator ((π/2) mod (π/2) = 0). A declination of magnitude above π/2 is now reduced to (−π, π], then reflected through
  the pole it passed, π − d (−π − d past the south pole), and the right ascension turned by π: the pair points where the
  unreduced angles of the linear model point, (cos d cos a, cos d sin a, sin d). Against the closed form of the spherical
  identity, 9.992e-15 rad and 1.539e-15 in direction over two years every 0.01 yr past the north pole, the south pole,
  and both for a star of 1 rad/yr, each step no longer than the arc of the angles (3.142 rad, a direction 2.0 away and
  a jump of 1.569 rad before; `StarTests.GetEquatorialCoordinates_AcrossAPole_CrossesIt`, thresholds 2.0e-14 rad and
  3.2e-15 over Linux, Windows and macOS arm64); against CSPICE's `latrec_c` then `reclat_c` of the unreduced angles, 1.533e-15 rad
  (`AcrossAPole_IsReclatOfLatrec`, Validation, threshold 3.1e-15). Since #718 a star moves along `iauStarpm`'s
  straight line instead (below), which crosses a pole without this reduction. Declinations within [−π/2, π/2] are
  unchanged, bit for bit; the snapshot of `StarTests.GetEquatorialCoordinates2`, past the south pole, which encoded the
  jump (−0.128953 rad), is replaced by the closed form (−1.441843 rad, the right ascension turned by π). The right
  ascension is in [0, 2π) as documented: a value just below 0 whose sum with 2π rounds to 2π (above −ulp(2π)/2,
  4.44e-16 rad) was returned as 2π (a right ascension of 0 moving −1e-30 rad/yr read 6.283185307179586 rad after a
  Julian year); it is now 0, the double of [0, 2π) nearest to it on the circle, and −1e-15 rad still 6.283185307179585
  (`StarTests.GetEquatorialCoordinates_RightAscensionJustBelowZero_IsInZeroTwoPi`, red on c54a0fc for −1e-30, −1e-17 and
  −4e-16).
- **The sigmas of a star were reduced like angles** ([#709](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/709)). `Star.GetRightAscensionSigma` and `GetDeclinationSigma`
  reduced √(σ² + (dt σμ)²) modulo 2π and π/2: a declination sigma of 2 rad read 0.429 rad, one of 2 rad grown by
  1e-3 rad/yr over 2000 years 1.258 rad (2.828 rad). A standard deviation is now returned as it is
  (`StarTests.Sigmas_AboveAnAngleRange_AreNotReduced`; the snapshots of `GetDeclinationSigma` and
  `GetRightAscensionSigma`, 0.588 and 5.133 rad, are replaced by their closed forms, 10.013 and 11.416 rad). Sigmas
  below π/2 and 2π are unchanged, bit for bit.
- **The deep space example of the developer guide measured nothing** ([#704](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/704)). "Deep Space Propagation
  with Multiple Perturbations" compared a massless probe started at the Moon's state with the Moon after 25 days, which
  the probe cannot follow (the Moon's own mass, about 1.2 % of the attraction of their relative motion), so its 54 310 km
  said nothing of the propagation, and its test only ran the block. The example now propagates the Moon: its Earth's
  central term is GM_Earth + GM_Moon (a field of degree 0 with the model's GM, `GeopotentialCentralTerm.ModelGravitationalParameter`),
  and it prints its difference to the Moon of DE440, about 2.0 km. `DeveloperGuideExamplesTests` checks it against an
  independent integration of the same forces (point masses with the kernel GMs, the Sun and the barycentres of Jupiter
  and Saturn read from SPICE at the TDB of the TT argument, RK4 at 300 s, converged to about 1e-4 m): 468.4 m and
  1.197e-3 m/s, the error of Velocity-Verlet at the example's 5 min, that integration against the Moon of de440s,
  1.514 km (the forces it leaves out), and the example against it, 1.972 km (bounds 9.4e2 m, 2.4e-3 m/s, 3.1e3 m and
  4.0e3 m, twice the worst over Linux, Windows and macOS arm64); with the Earth's GM alone the integration ends 54 310 km from the Moon.
  The comment of the 5 min step states what the step costs where it said "for accuracy". The library is unchanged
  (`DeepSpaceExample_FollowsTheMoonOfTheEphemeris`, red on 46e6975: the block returned nothing to compare; with the
  Earth's GM alone, 5.431e7 m from the reference).
- **The C# blocks of `docs/` and `README.md` were not compiled, and 34 of their lines did not compile** ([#705](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/705)).
  Compiled each in its own method with its inputs declared, the blocks of 12 pages failed against the library: the TLE
  tutorial read `tle.NoradId` (now `NoradCatalogId`) and multiplied the method `Inclination` (now the field
  `MeanInclination`); the mission design tutorial passed `shortWay:` and `nRevolutions:` to `LambertSolver.Solve`, whose
  first argument is the direction of motion (`isRetrograde`, the prose reworded: prograde or retrograde, 0 to N
  revolutions, two branches each), and read `DepartureVelocity`, `ArrivalVelocity` and `TotalDeltaV` (now `V1`, `V2`,
  `DeltaV`); the coordinate conversions tutorial read `Position` on the `OrbitalParameters` of `ToFrame` (now
  `.ToStateVector()`), used `positionCirs` before declaring it (here and in `standards-and-units.md`: the example now
  starts from an ICRF position) and named `Planetodetic`'s arguments in camelCase; the geometry tutorial called
  `FindWindowsOnOccultationConstraint` on the observer with `occultingBody:` and `occultedBody:`, which it does not have
  (now on the target, the observer and the front body as arguments, as the occultations reference) and built a TLE
  spacecraft with the kernel-backed constructor; the batch tutorial passed a lambda to `BatchOptions.Progress`, an
  `IProgress<BatchProgress>`, and read a per-task `ElapsedTime` (the batch's `BatchResult.ElapsedTime`); the
  thread-safety guide called an `AtEpoch` of three arguments, which does not exist, for the state on each task's Earth
  (now rebuilt on it: the pattern it stood for, a state about a shared Earth without the geopotential, fails every task
  with `ArgumentException`, #674) and elided the required members of `MonteCarloConfiguration`; the Monte Carlo tutorial
  read `MeanRssPosition` and `StdRssPosition`, which `EpochStatistics` does not have; the numerical propagation tutorial
  gave `Clock` its period instead of its ticks per second, read `Position` on the tuple of `InterpolateAt` and listed
  `PlanetsAndMoons.JUPITER_BODY` (now `Barycenters.JUPITER_BARYCENTER`); the Lambert reference named the third argument
  of `Solve` `targetObjectOrbitalParameters`, which the library spells `targetOjectOrbitalParameters` (the example passes
  its arguments by position, the listing shows the library's name); the SPICE API reference listed
  `AddCelestialBodyInfo(params CelestialBody[])`, whose type is `IO.Astrodynamics.DTO.CelestialBody`; the covariance
  tutorial called a helper `WithRtnCovariance` defined nowhere (now in the block). Every csharp block is now the region
  of a method of the page's file under `IO.Astrodynamics.Tests/Documentation/Pages/`, its inputs the method's parameters
  named by its first comment (`// Inputs: ...`), and `DocumentationPagesExamplesTests` checks that each block equals its
  region, that every page with csharp blocks has its file, that each block declaring using directives compiles with
  those of its page alone and a console application's implicit ones, as a reader who copies it compiles it (the first
  blocks of the TLE and coordinate conversion tutorials lacked `IO.Astrodynamics.OrbitalParameters.TLE`, and
  `IO.Astrodynamics.OrbitalParameters` and `IO.Astrodynamics.SolarSystemObjects`), and runs the 18 blocks that need only
  `Data/SolarSystem`, the first TLE block so compiled (its printed catalog number and inclination against the columns of
  its lines), and the Lambert, frame, lunar eclipse, batch and thread-safety blocks with their inputs: the Lambert velocities
  against the two-body closed form (3.1e-7 m at arrival), the ecliptic state against the rotation by the obliquity of
  ECLIPJ2000 (9.3e-10 m), the CIRS round trip (1.4e-16), the eclipse of 2024-03-25 bracketed within 60 s by the discs of
  the Earth and the Moon as the Sun sees them (bounds twice the worst over Linux, Windows and macOS arm64). The 12 signature listings and
  fragments that are not statements are fenced `csharp-signature` (highlighted as C# through `mkdocs.yml`, and checked
  against the library's constructors and methods by reflection), or written out in full; the excerpt of `Constants` that
  opens `docs/standards-and-units.md`, declarations fenced `csharp` with no region (region 1 of that page is its second
  csharp block), is checked by value. A block's leading using alias may name a public type of the library. This lifts the known
  limitation of #662. Documentation and tests only: no library result moves.
- **The managed field-of-view search of a field with tiny half-angles threw an unrelated `ArgumentException`** ([#715](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/715)).
  The managed search and `IsInFOV` of a circle or an ellipse build an ellipsoid whose limb is the cone of the field, of
  semi-axes D sin REF and D sin REF · tan CROSS / tan REF at the target's distance D. Where the square of one of them
  underflows to 0 (IEEE 754: a half-angle below about 6e-171 rad at the Moon's distance, 1e-200 and 5e-324 measured),
  the ellipsoid of the occultation search threw `ArgumentException`, "The semi-axes of an ellipsoid must have positive
  lengths", instead of the `InvalidOperationException` of a field that cannot be searched; it now throws that, naming
  the half-angles and the underflow (`InstrumentTests.TinyHalfAngle_ManagedSearchThrowsInvalidOperation`, red on
  a80f6a5 for the six rows of a circle and an ellipse, a point and an ellipsoid target). The rectangle, whose margin
  takes the half-angles as angles, was not affected. A target at the observer, distance 0 (two spacecraft of the same
  state), threw the same `ArgumentException` for a circle or an ellipse of any field: a point target now throws
  `InvalidOperationException` naming the target at the observer, which has no direction, and an ellipsoid target the
  `InvalidOperationException` of an observer inside the target, never a message about the half-angles
  (`InstrumentTests.PointTargetAtTheObserver_ThrowsInvalidOperationNamingTheDistance`, red on c3a4cf8, whose underflow
  guard blamed the half-angles 0.1 and 0.1 rad; the rectangle still reports such a point in view, its margin taking
  atan2(0, 0) = 0, [#721](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/721)). Every other
  field and target is searched as before, bit for bit (the same vectors). Migration: a caller that caught
  `ArgumentException` for these cases catches `InvalidOperationException`. As the issue recommends (decided by
  S. Guillet, 2026-11-17), the constructors keep the rule "finite and positive" for both half-angles, and the
  documentation states that half-angles below about 1e-16 rad can be degenerate for `gftfov` (SPICE(DEGENERATECASE),
  measured with `fovtrg_c` up to 1.53e-17 rad with the boresight at the Moon), the two paths then possibly disagreeing,
  and that the managed construction of a circle or an ellipse loses its precision far below (the Moon's ellipsoid at the
  boresight not found for a half-angle of 1e-155 rad in the reference plane, measured).
- **Six csharp blocks of three tutorials did not compile with the directives of their page** ([#716](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/716)).
  `DocumentationPagesExamplesTests.PageBlocks_CompileWithTheUsingsOfTheirPage` compiled, as a reader who copies them,
  only the blocks that declare using directives (15 of 174), so a block without a list of its own that used a namespace
  its page never imports passed: `Planetodetic` in the coordinate conversions tutorial (`IO.Astrodynamics.Coordinates`),
  `CentralBodyPropagator` and `CentralBodyPropagatorBuilder` in three blocks of the numerical propagation tutorial
  (`IO.Astrodynamics.Propagator`), `Omm` and `Opm` in the TLE and OMM tutorial (`IO.Astrodynamics.CCSDS.OMM` and
  `IO.Astrodynamics.CCSDS.OPM`), CS0246 and CS0103 for a reader. The test now compiles every block of a page that
  declares directives with the block's own, the earlier blocks' and a console application's implicit ones (red on
  a80f6a5 on exactly these six blocks of three of the eight pages); the first blocks of the coordinate conversions and
  numerical propagation tutorials import the two namespaces, and the OMM and OPM blocks of the TLE tutorial import their
  own. A page that declares no directive gives the reader none and is not compiled this way. Documentation and tests
  only: no library result moves.

- **The CCSDS data model built values without a range that its readers refuse** ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717)). The
  constructors of `OpmStateVector` (X ... Z_DOT), `CovarianceMatrix` (its 21 terms), `OpmManeuverParameters` (MAN_DV_1
  to MAN_DV_3) and `TleParameters` (BSTAR, BTERM, MEAN_MOTION_DOT, MEAN_MOTION_DDOT, AGOM), and the init accessors of the
  CDM records (`CdmRelativeMetadataData`, `CdmRelativeStateVector`, `CdmOdParameters`, `CdmAdditionalParameters`,
  `CdmStateVector`, `CdmCovarianceMatrix`: 47 real values), assigned NaN and ±Infinity unchecked, where #708 had
  checked the ranged parameters only: `Validate()` and `validateBeforeSave` passed them (an infinite BSTAR with an
  information message, a covariance term other than a variance, every CDM value but the miss distance), the writers
  wrote `NaN`, `Infinity` or `-Infinity` (`Infinity` is not an `xsd:double` literal, XML Schema Part 2, 3.2.5.1), and
  the readers refused the document (measured on a80f6a5: `new OpmStateVector(epoch, NaN, ...)` written as
  `<X units="km">NaN</X>`, then `OpmParseException`; `CY_X = NaN` in an OMM, `BSTAR = NaN`, `with { CollisionProbability
  = NaN }` on a CDM read back, each written and refused). Each value now goes through the internal `CcsdsValues`
  (`RequireFinite`, `RequireNullOrFinite`, a vector by component, a matrix by term), which throws
  `ArgumentOutOfRangeException` naming the parameter or the property; the factories (`FromStateVector`,
  `FromFullMatrix`, `FromMatrix`, `FromMatrixWithUnitConversion`, `CreateImpulsive`, the four `CreateWith*`,
  `FromRtnCovariance`) inherit the check under the name of the term at fault. Finite values build bit for bit as
  before, and the written documents are byte for byte unchanged (`OpmStateVectorTests`, `CovarianceMatrixTests`,
  `OpmManeuverParametersTests`, `TleParametersTests` and `CdmTests` `*_NonFinite*`, NaN, +∞ and −∞ in each value, 622
  cases red on a80f6a5; `CdmTests.RealProperties_ListEveryRealPropertyOfTheRecords` guards that every real property of
  the six records is checked; ±`double.MaxValue`, ±`double.Epsilon` and −0 stored as given, a Pc of `double.Epsilon`
  written and read back bit for bit). `CdmCovarianceMatrix.StateCovarianceRtn` held the caller's `Matrix`, whose `Set`
  writes the array it wraps, and returned it: a NaN or ±Infinity set on the matrix given, or on the one the getter
  returned (a CDM read by `Cdm.ReadFromString` included), reached the record after the check, `Validate()` passed it,
  `WriteToString` wrote it and the reader refused the document (measured on the first fix of #717). The record now holds
  a copy of the matrix it is given and its getter returns a copy (internal `CcsdsValues.Copy`), so a term set on either
  matrix does not reach it (`CdmTests.Init_MatrixChangedAfterwards_TheRecordKeepsTheTermsItChecked` and
  `StateCovarianceRtn_ReturnedMatrixChanged_TheCdmStillWritesAndReadsBack`, CR_R, CT_R and CNDOT_NDOT with NaN, +∞ and
  −∞, 18 cases red on a80f6a5).
- **The CDM reader read a decimal comma as another number** ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717), the number half of [#619](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/619)). `CdmReader`
  parsed with `NumberStyles.AllowThousands`, which drops every comma wherever it stands: `MISS_DISTANCE` `118,1` read
  as 1181 m, `0,715` as 715 m, `COLLISION_PROBABILITY` `4,835E-05` as 0.04835 (1000 times the value), `CR_R` `4,1` as
  41 m², `X` `2,570.097065` km as 2 570 097.065 m (measured on a80f6a5). `xsd:double` has no comma (XML Schema Part 2,
  3.2.5.1), nor a value of CCSDS 508.0-B-1 (6.3.2.2 to 6.3.2.4, confirmed by S. Guillet, 2026-11-17). The reader now
  parses with `NumberStyles.Float` only, and a comma throws the existing `CdmParseException`
  (`Invalid numeric value for MISS_DISTANCE: '118,1'.`). Every `xsd:double` numeral (a sign, no integer digit, an
  exponent of either case, surrounding whitespace) reads bit for bit as before (`CdmTests`
  `ReadFromString_CommaInValue_ThrowsCdmParseException`, 10 values, red on a80f6a5;
  `ReadFromString_XsdDoubleLexicalForms_AreRead`). #619 is partly fixed (decided by S. Guillet, 2026-11-17): its date
  half stays open.

- **`EvaluateAvoidance` did not check the steps of its re-analysis** ([#640](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/640)).
  `Analyze`, `AnalyzeAll` and `Screen` refuse a `SampleStep` or a `MaximumEventSearchStep` of zero or less;
  `EvaluateAvoidance` passed `AvoidanceSearchOptions.AnalysisSampleStep` and `MaximumEventSearchStep` to the re-analysis
  of each option unchecked. Measured on a85098d with the heap limited to 2 GB: on the two-body path each of the four
  values (either step, 0 s or −5 s) ended in `OutOfMemoryException` (about 15 s without the limit; a
  `MaximumEventSearchStep` of −5 s gave a search step of four bisection tolerances, an `AnalysisSampleStep` of −5 s
  stepped backwards); on the paths of a propagated participant (its force model, or its propagated covariance) a
  `MaximumEventSearchStep` of 0 s ended the same way, −5 s and a sample step of 0 s or −5 s were accepted silently. Both now throw `ArgumentOutOfRangeException` naming the option of the avoidance, before any
  option is analyzed (`ConjunctionAssessmentTests.EvaluateAvoidance_StepNotPositive_Throws`, the three paths, 0 s and
  −5 s, 12 rows, red on a85098d).

- **A window without a close approach gave its midpoint on the sampled path** ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621)).
  `Screen` and the `ILocalizable` overloads of `Analyze` and `AnalyzeAll` returned the window midpoint when the range
  rate had no zero from negative to positive in the window, and the search on two propagations returned the end where
  the distance grows into the window: the same window, two answers, neither a close approach. On the reproduction of
  #621 (two LEO crossing at 60°, 250 m at t0 + 1800 s), the sampled `Analyze` returned t0 + 1789.5 s and 79 930.977 m
  on [1780, 1799] s, where the end is 7 616.710 m away and closing at 7.6 km/s, and `Screen` dropped the object at its
  default 25 km; on [1200, 1500] s the midpoint, 3 285 799 m, against 2 242 051 m at the end (measured on a85098d).
  `Analyze` and `Screen` now return the closest window end on every path, the absolute minimum of the distance over the
  window (CSPICE's `ABSMIN`), flagged by the new `EncounterQualityFlags.TcaAtWindowEnd`, and `AnalyzeAll` an empty
  list, as on two propagations (decided by S. Guillet, 2026-11-17). Against CSPICE's `gfdist_c` on the SPKs of the two
  propagations, over windows of a monotonic approach, a recession and a maximum only, through both paths and `Screen`:
  the end of `ABSMIN` itself, the miss within 2.3e-4 m of CSPICE's distance there
  (`ConjunctionKernelAndWindowTests.NoCloseApproach_AnalyzeReturnsTheClosestEnd_Flagged_AnalyzeAllNothing`, 10 rows, red
  on a85098d: the midpoints, `Screen` empty, no flag on the dense path).

- **The search on two propagations read the trajectory without the burn after an impulse** ([#574](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/574)).
  RK7(8) adds the accepted step that holds an event before it detects the event, and the segment that ends at an
  impulse keeps that step past it: between the impulse and the end of that step the polynomial search of `Analyze` and
  `AnalyzeAll` on two `PropagationSolution`s read the cubic Hermite of the trajectory without the burn, so a close
  approach there had no minimum on it and was missed. On #574's LEO (a 233.8 m/s apogee raise at S + 2963.2 s, the step
  ending 12.4 s after it about a point-mass Earth), secondaries passing 30 m from the spacecraft 1, 3, 4.5 and 6 s after
  the burn were listed only by the minimum before the burn, 2k s away (658.5 m off for a crossing), and `Analyze` returned
  a minimum 4 s off for one passing 2 s before it (measured on a85098d). The step that holds the event of a segment now
  ends at the event, the epoch the propagation labelled it (`TimeArgument.EpochAt(BaseEpoch, EventTime)`, the base of
  the next segment), in the steps and the knots of the search, an `EventAction.Continue` included; the position is
  continuous there. Against a brute force of the states of the two solutions every 10 ms, refined by golden section
  (`ConjunctionPostBurnTests`, 8 rows red on a85098d, both sides of the burn, a crossing, a `Continue` event and
  Velocity-Verlet, which locates its events at a step end and is unchanged): every minimum listed within 7e-7 s and
  1.7e-9 m. Bit for bit without an event: the long-validation conjunction tests and the `conjunction` conformance cases,
  without impulses, do not move.

**Changed**

- **Breaking: a star moves along a straight line in space** ([#718](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/718)).
  `Star.GetEquatorialCoordinates` moved the catalog angles linearly, α0 + μα dt and δ0 + μδ dt, at the catalog distance,
  a model whose departure from the reference its remark cited, SOFA's `iauStarpm`, nothing gave. A star now moves along
  a straight line at constant speed in ICRF, the closed form of `iauStarpm`'s space motion with a radial velocity of 0
  (`external-lib/sofa-src/starpm.c`, Note 5; decided by S. Guillet, 2026-11-17): with x = μα* dt and y = μδ dt, the
  position r0 (u0 + x e_α + y e_δ), so α = α0 + atan2(x, cos δ0 − y sin δ0), δ = atan2(sin δ0 + y cos δ0, √(x² + (cos δ0
  − y sin δ0)²)), and the distance `Distance` · √(1 + x² + y²), as `iauStarpm`'s parallax decreases, no longer the
  catalog distance (Barnard's star: 1.26e-5 relative after 100 years, 7.1e11 m). The form divides by no cos δ0 and
  crosses a pole by itself. The change is of the second order in μ dt for a star given dα/dt as before: Barnard's star
  0.33 mas after 10 years, 34 mas after 100 and 9.4″ after 1000, Polaris 4.2 mas after 100 years, a star near a pole
  1.26° after 1000. Against `iauStarpm` (`tools/sofa_reference/starpm_ref.c`, SOFA 2023-10-11; Barnard's star from
  Hipparcos I/311 and Gaia DR3, Polaris, and Barnard's motion at ±89.5°, which crosses a pole): 1.8e-17 rad after 1 and
  10 years, 2.4e-14 rad after 100 and 2.4e-10 rad after 1000 (its light-time and relativistic terms of the observed
  place, as dt⁴), the distance 7.6e-8 relative per decade (`iauStarpv`'s inertial radial velocity −v_t²/(2c) for an
  observed rv of 0) (`StarTests.GetEquatorialCoordinates_CatalogStars_AreIauStarpm`, red on a80f6a5: 1.3e-7 rad after 10
  years for Barnard's star); against the closed form in long double, the stars at ±π/2 and the synthetic stars of the
  suite included, 7.8e-16 rad and 2.2e-16 relative (`IsTheStraightLine_OfALongDoubleClosedForm`); three positions dt
  apart lie on one line at constant speed within 1.2e-15 of their size (`PositionsLieOnOneLineAtConstantSpeed`, 3.7
  before); thresholds twice the worst over Linux x64, Windows x64 and macOS arm64 (the same on the three, CI run
  43974478027; the Validation tests twice the Linux worst), but after 1 and 10 years against `iauStarpm`, where the
  worst lies below one ulp of the right ascension (8.9e-16 rad near 4.7 rad), twice the resolution of the returned
  angles, 1.9e-15 rad, so that a one-ulp difference of another platform passes (decided by S. Guillet, 2026-11-17). A
  star without proper motion, and every star at its catalog epoch, is unchanged, bit for bit
  (`WithoutProperMotion_IsTheCatalog_BitForBit`), and so are the stars of the field-of-view and kernel tests.
  `GetGeometricStateRelativeTo`, `GetEphemeris`, the states of `Propagate` and `Scenario.SimulateAsync`, the SPK of
  `WriteEphemeris` and the occultation and field-of-view searches of a star with proper motion move with it. The tests
  that encoded the linear angles are measured against the straight line written in the test
  (`StarTests.GetEquatorialCoordinates`, `GetEquatorialCoordinates2`, `Propagate`, `ScenarioTests.PropagateStar`, whose
  snapshots 1.1 and 1.2 rad and catalog distances are replaced; the dt tests of #709 keep their dt references, the ticks
  or CSPICE's ET); the pole tests of #709 take straight lines that pass near and over a pole (a straight line never
  passes both poles: #709's case of 1 rad/yr through both was an artefact of the linear angles). The sentences of the
  #709 entries under **Fixed** about the linear model are history, superseded by this entry. Migration: none for a
  catalog star given its catalog values (see the next entry); code that expects the angles to grow by μ dt computes the
  straight line ([Stars](../reference/stars.md#the-model)). `Distance` stays the catalog distance. No radial velocity is
  taken (rv = 0, its size on the [Stars](../reference/stars.md#limits) page).
- **Breaking: the proper motion in right ascension of a star is a true angle, μα*** ([#718](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/718)).
  The constructor's `rightAscensionProperMotion`, `rightAscensionSigma` and `rightAscensionSigmaProperMotion` and their
  properties gave no unit and no convention, and the model added the proper motion to the right ascension, so it was
  dα/dt (SOFA's convention, `starpm.c` Note 4), while the catalogs a caller reads, Hipparcos (I/239 `pmRA`,
  "mu_alpha.cos(delta)") and Gaia DR3 (I/355 `pmRA`, "pmRA*cosDE"; both cited from #718, confirmed by S. Guillet,
  2026-11-17), give μα* = (dα/dt) cos δ: a catalog star given its `pmra` moved cos δ0 times too fast in right ascension
  (Barnard's star 26.8 mas away from `iauStarpm` after 10 years, Polaris 439 mas, a rate 77.9 times too large). They are
  now μα*, σα* and σμα* (decided by S. Guillet, 2026-11-17), true angles in radians and radians per Julian year of TDB,
  as Hipparcos and Gaia give them, documented on the constructor, the properties and the new
  [Stars](../reference/stars.md) page with the units of every parameter and the conversion of a Gaia DR3 or Hipparcos
  record; `GetRightAscensionSigma` keeps its formula and returns σα*. Barnard's star built from its Gaia DR3 record by
  its units alone is `iauStarpm` fed `pmra`/cos δ to the figures above
  (`StarTests.GetEquatorialCoordinates_FromAGaiaDr3Record_IsIauStarpm`, 27.7 mas after 10 years before); the same μα*
  moves a star on the equator, at 1 rad and at a pole by the same angle along e_α
  (`RightAscensionProperMotion_IsTheTrueAngleRate`; before, cos δ0 times that, and nothing at a pole). For the same
  number a star now moves cos δ0 times as fast in right ascension as before: 0.54 times for the stars of the tests at 1
  rad, 1/77.9 for Polaris. The declination motion and sigmas are unchanged. The reference of `StarOccultationTests`
  (`tools/occultation_reference/star_occultation_reference.py`) reads its 0.1″/yr as μα* and moves its star along the
  straight line; its literals of 0.1″/yr are regenerated (spiceypy 8.4.0, CSPICE N0067; the cases without proper motion
  reproduce their literals): 1.91e-6 s at worst at the edges and 1.30e-16 rad in direction on Linux x64, within the
  thresholds of 4.8e-6 s and 4.4e-16 rad. Migration: a caller that passed dα/dt passes `RightAscensionProperMotion` ×
  cos δ0, and multiplies its right ascension sigma and sigma rate by cos δ0; a caller that passed a catalog's `pmra`
  needs no change and now gets the star of the catalog.
- **Breaking: the element conversions of `SpiceAPI` reject a centre the native layer cannot read** ([#711](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/711),
  Fixed above). `ConvertConicElementsToStateVector(KeplerianElements)` (an ellipse) and
  `ConvertEquinoctialElementsToStateVector` throw `ArgumentException` (parameter `keplerianElements`, `equinoctialElements`)
  before the native call for elements about an observer that is not a celestial body (not a `CelestialItem` of
  `IsCelestialBodyId`: a spacecraft, a site) or whose identifier SPICE does not name, where the process ended for a
  centre with a GM and the conversion of the result threw `ArgumentException` (`naifId`) otherwise; and
  `InvalidOperationException` with SPICE's message when the kernel pool has no GM for the centre ("The variable
  BODY507_GM could not be found in the kernel pool."), where a zero or undefined state was returned. A null argument
  throws `ArgumentNullException`. Native callers: `ConvertConicElementsToStateVectorProxy`,
  `ConvertEquinoctialElementsToStateVectorProxy` and `ConvertStateVectorToEquatorialCoordinatesProxy` return a NaN
  result with `GetLastErrorProxy()` instead of throwing or returning zeros; `ConvertTLEToStateVectorProxy` and
  `GetTLEElementsProxy` return NaN instead of a zero state or unwritten elements; `GetCelestialBodyInfoProxy` returns
  `Id` -1 instead of throwing; `ReadOrientationProxy`, `LaunchProxy` and `ReadEphemerisProxy` leave SPICE reset when
  they return false; `ConvertConicElementsToStateVectorAtEpochProxy` returns a NaN state with `GetLastErrorProxy()` and
  SPICE reset when `conics_c` fails, instead of the unwritten state with SPICE left failed. Migration: convert elements
  about a celestial body SPICE names, with its GM kernel loaded (`gm_de440.tpc`); catch `InvalidOperationException`.
  Ordinary inputs are unchanged, bit for bit.
- **Breaking: the element conversions of `SpiceAPI` throw for elements outside the domain of their SPICE routine** ([#711](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/711),
  Fixed above). `ConvertEquinoctialElementsToStateVector` throws `ArgumentException` (`equinoctialElements`) for a
  semi-major axis that is not finite and above 0 or an eccentricity sqrt(F² + G²) above 0.9 (`EQNCPV`'s
  `SPICE(BADSEMIAXIS)` and `SPICE(ECCOUTOFRANGE)`); the three `ConvertConicElementsToStateVector` overloads throw
  `ArgumentException` (`keplerianElements`) for a periapsis radius that is not finite and above 0, an ellipse of negative
  semi-major axis or a hyperbola of positive one (`CONICS`'s `SPICE(BADPERIAPSEVALUE)`); the overload with a GM throws
  `ArgumentOutOfRangeException` (`gm`) for a GM that is not finite and above 0 (`SPICE(BADGM)`), and the overload with an
  epoch `InvalidOperationException` when the GM of the observer is not (a body without `BODYnnn_GM`, as Elara), as the
  one-argument overload does for an open orbit. Each is checked before the native call. Before, the equinoctial elements
  and an ellipse returned the zero state without an exception (46e6975), a failed `conics_c` of the overloads with an
  epoch (a GM of 0 or below, a periapsis radius not above 0) threw an unrelated `InvalidOperationException` ("Celestial
  celestialItem name can't be defined") after leaving SPICE failed, and a GM not finite returned a state that is not
  finite. A null argument throws `ArgumentNullException`. Migration: convert elliptic equinoctial elements of
  eccentricity above 0.9 with `EquinoctialElements.ToStateVector()`, pass a positive finite GM, and catch
  `ArgumentException` where elements may be inconsistent.
- **Breaking: the two-body calls of `SpiceAPI` check their arguments and throw SPICE's errors** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714), Fixed above).
  `Propagate2Bodies(StateVector, double, TimeSpan)` and `ConvertStateVectorToConicOrbitalElement(StateVector, double)`
  throw `ArgumentOutOfRangeException` (`gm`) for a GM that is not finite and above 0 (`SPICE(NONPOSITIVEMASS)` of
  `prop2b.c` and `oscelt.c`); the overloads that take the GM of the observer (`Propagate2Bodies(StateVector, TimeSpan)`,
  `(StateVector, Time)`, `ConvertStateVectorToConicOrbitalElement(StateVector)`) throw `InvalidOperationException` naming
  `BODYnnn_GM` when that GM is not, as `ConvertConicElementsToStateVector(KeplerianElements, Time)` does; every overload
  throws `ArgumentNullException` for a null state and `ArgumentException` (`stateVector`) for a position or a velocity
  that is not finite, which SPICE turns into NaN with no error; and `InvalidOperationException` with SPICE's message for a
  state SPICE rejects (decided by S. Guillet, 2026-11-17: SPICE's own tolerances cannot be reproduced in .NET): a zero
  position, a zero velocity, a position parallel to the velocity, a time out of `prop2b_c`'s range
  (`SPICE(ZEROPOSITION)`, `SPICE(ZEROVELOCITY)`, `SPICE(NONCONICMOTION)`, `SPICE(DTOUTOFRANGE)`; `SPICE(DEGENERATECASE)`
  for the elements). Before, the process ended when the call was its first native call, and otherwise the stack's
  contents (about 7e-310) were returned. Native callers: both proxies return a NaN result with `GetLastErrorProxy()`.
  Migration: pass a finite state and a finite GM above 0, load the GM of the observer, and catch
  `InvalidOperationException` for a degenerate state. Results on ordinary inputs are unchanged, bit for bit.
- **Breaking: the element conversions of `SpiceAPI` reject elements that are not finite** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714)). The three
  `ConvertConicElementsToStateVector` overloads throw `ArgumentException` (`keplerianElements`) for an eccentricity,
  inclination, ascending node, argument of periapsis or mean anomaly that is not finite, and
  `ConvertEquinoctialElementsToStateVector` (`equinoctialElements`) for a p, f, g, h, k or L that is not, before the native
  call. Before, a NaN mean anomaly gave a finite state silently (CONICS checks its domain with comparisons NaN passes:
  `conics_c` gives a finite state for M0 = NaN with no error, `SpiceApiTest.Conics_OfElementsNotFinite_SignalsNoError`),
  and a NaN inclination a NaN state that threw `InvalidOperationException` "Native proxy call failed" or the message of
  an earlier call. The semi-major axis of a parabola stays infinite: its periapsis radius is checked. Migration: pass
  finite elements.
- **Breaking: `SpiceAPI.WriteEphemeris` rejects a centre SPICE does not name** ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714), the last row of
  [#655](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/655)). It throws `ArgumentException`
  (`stateVectors`, "The NAIF identifier 2999991 has no name in SPICE: ...") before anything is unloaded or written, an
  existing file kept byte for byte, for a centre no loaded kernel names (a body built from custom parameters), with or
  without a GM in the kernel pool, where the native writer threw `InvalidOperationException` ("CelestialItem id2999991
  can't be found"); the check is that of the element conversions (#711, #680's rule). A centre SPICE names whose GM the
  kernel pool lacks still reaches the native writer, `InvalidOperationException` with the message of the Fixed entry
  above, nothing written. `Star.WriteEphemeris` and `Site.WriteEphemeris` write about named bodies and are unchanged.
  Migration: catch `ArgumentException`, or load a kernel that names the body (`NAIF_BODY_NAME`, `NAIF_BODY_CODE`)
  (`SpiceApiTest.WriteEphemeris_AboutACentreSpiceDoesNotName_ThrowsArgumentExceptionAndKeepsTheFile`).
- **Breaking: `TransformFrameProxy` returns a NaN rotation when it fails**
  ([#714](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/714), Fixed above). A native caller
  gets a NaN rotation and angular velocity (decided by S. Guillet, 2026-11-17) with SPICE's message in
  `GetLastErrorProxy()`, where it got the zero quaternion with no message (#454); `GetLastErrorProxy()` is empty after a
  reporting proxy that succeeds. `SpiceAPI.TransformFrame` throws the same `InvalidOperationException` on both, its
  message now SPICE's. Migration (native callers): test the rotation for NaN, or read `GetLastErrorProxy()`, where the
  zero quaternion was tested.
- **Breaking: an instrument checks its field of view when it is built** ([#707](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/707)). The constructor of `Instrument`, so
  `AddCircularInstrument`, `AddEllipticalInstrument`, `AddRectangularInstrument` and any subclass, throws
  `ArgumentException` naming the parameter for a boresight or a reference vector with a component that is not finite or
  a magnitude of 0 (`boresight`, checked first, then `refVector`), a reference vector parallel to the boresight,
  |b × r| = 0 on the given vectors, the test of CSPICE's `getfov` (`refVector`), and a field of view that is NaN or
  +∞ (`fieldOfView`; 0 and negative values threw before, now with the parameter's name), and adds nothing;
  `AddEllipticalInstrument` and `AddRectangularInstrument` throw `ArgumentOutOfRangeException` naming `crossAngle` for
  a cross angle of +∞ or a NaN of positive sign (0x7FF8000000000000), which `ThrowIfNegative` let through, as for a
  negative one and for `double.NaN` before (a cross angle of 0 too since [#715](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/715), the entry below). These are
  the fields `getfov` rejects on the kernel the library writes (SPICE(ZEROBORESIGHT), SPICE(BADREFVECTORSPEC)) or
  cannot read (NaN, Infinity: an IK holding `FOV_CROSS_ANGLE = Infinity` fails to load, SPICE(NUMBEREXPECTED)), and the rule of the body axes of a spacecraft and of the body vectors of a TRIAD (#703).
  Before, the instrument was built: `GetBoresightInICRFFrame` and the other getters returned the zero or NaN vector,
  `InstrumentPointingToAttitude` the identity or a NaN attitude, `WriteKernelAsync` a kernel SPICE rejects
  (`BORESIGHT = ( 0, 0, 0 )`, `FOV_REF_ANGLE = NaN`), and the field-of-view search and `IsInFOV` threw only when
  called, `InvalidOperationException` for the zero and parallel vectors and an unrelated `ArgumentException` ("The
  semi-axes of an ellipsoid must have positive lengths") for NaN and infinite ones; the search no longer checks the
  vectors. A reference vector off the boresight by any non-zero cross product is accepted, as `getfov` accepts it
  (`InstrumentTests.RejectedFields_AreThoseGetfovRejects`, `getfov_c` on the kernels, and the cross-platform
  `AddInstrument_*_Throws`, red on 46e6975, `AddInstrument_CrossAngleNotFinite_Throws` red on c54a0fc for +∞ and the
  positive NaN). Results for the instruments accepted are unchanged, bit for bit.
  Migration: give a finite, non-zero boresight, a finite reference vector not parallel to it, a finite positive
  half-angle and a finite cross angle.
- **Breaking: an instrument's cross angle is a finite positive number** ([#715](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/715)). `AddEllipticalInstrument` and
  `AddRectangularInstrument` (the constructors of `EllipticalInstrument` and `RectangularInstrument`) throw
  `ArgumentOutOfRangeException` naming `crossAngle` for a cross angle of +0, which they accepted, writing an IK with
  `FOV_CROSS_ANGLE = 0` while every search of the library and `IsInFOV` threw `InvalidOperationException` ("its
  half-angles must be positive"), and add nothing; −0, which `ThrowIfNegative` rejected with its own message ("must be a
  non-negative value"), negative values and the values that are not finite now carry one message, "The cross angle of
  an instrument must be a finite positive number". A cross angle of 0 is a field of no width, as the field of view of 0
  the constructor already rejected: CSPICE's `gftfov` (N0067, through the library's proxy) rejects it with
  SPICE(DEGENERATECASE), for the rectangle whatever the boresight (consecutive boundary vectors with a zero cross
  product, `zzfovaxi`) and for the ellipse when its computed semi-axis is exactly of zero length (boresight +Z,
  `zzgffvu`); with the boresight at the Moon, a unit vector rounded from its ephemeris, the semi-axis is not exactly
  zero and `gftfov` searches the segment, one window of 1 µs for the Moon's centre and of 59.8 min for its ellipsoid
  (measured on Linux, `InstrumentTests.ZeroHalfAngle_IsDegenerateForGftfov`). The search no longer checks the cross
  angle: its `InvalidOperationException` "its half-angles must be positive" is gone, since no instrument can reach it.
  Results for the instruments accepted are unchanged, bit for bit (`InstrumentTests.AddInstrument_CrossAngleNotPositive_Throws`,
  red on a80f6a5 for the 8 rows of +0, −0 and the negatives of both shapes). Migration: give a positive cross angle; an
  instrument with no extent across the reference plane can be searched neither by the library nor reliably by
  `gftfov`.
- **Breaking: an instrument's orientation is finite** ([#715](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/715)). `AddCircularInstrument`, `AddRectangularInstrument` and
  `AddEllipticalInstrument` (the constructor of `Instrument`) throw `ArgumentException` naming `orientation` for Euler
  angles with a component that is NaN or infinite, and add nothing, the rule of the boresight and the reference vector
  (#707) and of the body axes of a spacecraft (#703). Before, the instrument was built with a NaN
  `RotationToSpacecraft` (IEEE 754: the sine and cosine of a NaN or an infinity are NaN), its spacecraft-frame and ICRF
  getters returned NaN vectors, and `WriteFrameAsync` wrote `TKFRAME_<id>_ANGLES = ( NaN,0,0 )` (or `Infinity`), which
  SPICE fails to load (SPICE(NUMBEREXPECTED), the variables before that line left in the kernel pool), measured on
  a80f6a5 (`InstrumentTests.AddInstrument_OrientationNotFinite_Throws`, red there for the 33 rows of the three
  shapes). Every finite angle stays accepted (decided by S. Guillet, 2026-11-17), results unchanged bit for bit
  (`AddInstrument_OrientationFinite_IsAccepted`); SPICE's frame agrees with `RotationToSpacecraft` to a few ulps of
  each angle, 1.8e-15 rad within [−2π, 2π] and more for large angles (0.268 rad at 1e300), as documented on
  `Instrument.Orientation`. Migration: give finite Euler angles, preferably within a few turns.
- **Breaking: `SpiceAPI.WriteEphemeris` throws `ArgumentException` for states the native writer cannot write** ([#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706),
  Fixed above). Fewer than three states, states not all in the frame of the first one (compared by name, ordinal), or a
  centre of motion that is not a celestial body (not a `CelestialItem` of `IsCelestialBodyId`: a spacecraft, a site)
  throw `ArgumentException` (parameter `stateVectors`) before anything is written, where the process ended, or, for a
  spacecraft centre, `InvalidOperationException` came through the native read of an uninitialised GM.
  `Star.WriteEphemeris` and `Site.WriteEphemeris` inherit it: a site's window shorter than two minutes throws. A path the
  native file system calls reject (a loop of symbolic links) makes `WriteEphemeris`, `WriteOrientation` and
  `LoadKernels` throw `InvalidOperationException` with the file system's message, where the process ended. Migration:
  pass three states or more, in one frame, relative to a celestial body; catch the exceptions.
- **Breaking: a writer that replaces a file `LoadKernels` loaded unloads it first** ([#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706), Fixed above).
  `WriteEphemeris`, `WriteOrientation`, `Star.WriteEphemeris`, `Site.WriteEphemeris`, `Spacecraft.WriteOrientation` and
  `Spacecraft.WriteEphemeris` unload such a target as `UnloadKernels` does, and every other load of it, under its path or
  an absolute name whose symbolic links resolve through it (a meta-kernel's load, a link to it): `GetLoadedKernels` no
  longer lists them (a loaded directory is replaced by its remaining files), and the new file is left unloaded, as the writers leave every
  file they write, so a read of its object fails until it is loaded again, where the native writers went on reading the
  replaced data. A failed unload throws the `InvalidOperationException` of `UnloadKernels` before anything is written,
  the file kept as it was. Files that were not loaded are written as before, bit for bit. Migration: `LoadKernels` the
  file again after writing it.
- **Breaking: kernel paths longer than SPICE takes throw `ArgumentException` before anything is written** ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673), Fixed
  above). `CosmographiaExporter.ExportAsync` and `astro propagate` throw for an output directory (parameter
  `outputDirectory`), or a system temporary directory (no parameter), whose kernels would have a full path longer than
  255 characters (UTF-8 bytes), and delete nothing; they succeeded with kernels `LoadKernels`
  could not load, after deleting the previous export. `SpiceAPI.WriteEphemeris` (so `Star.WriteEphemeris` and
  `Site.WriteEphemeris`) and `SpiceAPI.WriteOrientation` (so `Spacecraft.WriteOrientation`) throw `ArgumentException`
  (parameter `filePath`) where they threw `InvalidOperationException` after leaving a file under the truncated name.
  Migration: choose a shorter output directory (the CK of a spacecraft,
  `<output>/<Mission>_<Scenario>/Spacecrafts/<name>/Orientation/<name>.ck`, is usually the longest kernel) or a shorter
  temporary directory (`TMPDIR`, `TEMP`), and catch `ArgumentException`. `Spacecraft.WriteEphemeris` and the text kernel writers are unchanged.
- **Breaking: `LoadKernels` of a path that does not exist throws `ArgumentException`** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), second round).
  `SpiceAPI.LoadKernels` threw nothing and loaded nothing for a path that is neither a file nor a directory on disk, and
  for a `FileInfo` that names a directory or a `DirectoryInfo` that names a file (it tested the path by its .NET type):
  the first read that needed the kernels failed later, with a SPICE error that did not name the path. It now throws
  `ArgumentException` (parameter `path`, the full path in the message) before anything is unloaded or reloaded, so a
  missing path inside a loaded folder no longer reloads the folder; and it takes the path as what it is on disk, a
  `FileInfo` naming a folder loading the folder, a `DirectoryInfo` naming a file loading the file (`SpiceApiTest`, a TK
  frame of 90° about z read back through `pxform`, and KEEPER's count unchanged by a failed call). Every `astro`
  command given a kernels path that does not exist fails with that `ArgumentException` (`KernelsPathTests`), where it
  failed later on a frame or a body. `UnloadKernels` of a missing path is unchanged. No numerical result moves.
  Migration: give an existing path, or test `File.Exists`/`Directory.Exists` first and catch `ArgumentException`.
- **Breaking: each native load and unload first unloads the SPKs, CKs, binary PCKs and EKs whose file was deleted** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
  second round, Fixed above). `LoadKernels` (each file it hands SPICE), `UnloadKernels`, `WriteEphemeris` and
  `WriteOrientation` (and so `Star.WriteEphemeris`, `Site.WriteEphemeris`, `Spacecraft.WriteOrientation` and the
  Cosmographia export) close and unload every such kernel first, so its data are no longer readable after the next
  kernel call; before, they stayed readable until CSPICE
  released its logical unit, after which, for the life of the process, every IAU frame failed for a PCK and the reads of
  a CK at epochs not already read failed (the SPK reads measured did not fail). `UnloadKernels` no
  longer throws for a deleted SPK, CK, binary PCK or EK (it still throws for a deleted text kernel, meta-kernel or DSK),
  and the kernels of the kind keep their precedence: the unload of a PCK while another PCK of the same frame class loaded
  before a deleted one returns and leaves the PCK loaded last in precedence, where it threw and gave the earlier one
  precedence until the deleted one was unloaded. `GetLoadedKernels` no longer lists a deleted SPK, CK, binary PCK or EK
  after the next native load, unload or write, which drops it in SPICE (fourth round: it listed it until it was
  unloaded, a file SPICE no longer held; a directory whose files were all dropped leaves the listing, one with
  remaining files stays listed, and a later unload of the dropped path does nothing; a call that hands SPICE nothing, a
  load of a path that does not exist or of a directory without kernel files, leaves the listing as it is, and a symbolic
  link counts as deleted once its target is, as the native layer tests it, review of the fourth round). Only a kernel SPICE holds under an absolute name is concerned: one a meta-kernel names relative to
  the working directory of its load is left to SPICE as before, deleted or not, and a deleted one can still stay open.
  Migration: keep a kernel file on disk until it is unloaded; restarting the process after deleting one is no longer
  needed; name the files of a meta-kernel by absolute paths (or an absolute `PATH_VALUES`) to have their deletion
  covered; a deleted SPK, CK, binary PCK or EK leaves `GetLoadedKernels` at the next native load, unload or write, so do
  not expect it there. No numerical result moves for kernels that exist, whose listing is unchanged.
- **Breaking: `WriteEphemeris` and `WriteOrientation` throw when the last unload of their native writer fails** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
  fourth round, Fixed above). `SpiceAPI.WriteEphemeris` and `WriteOrientation`, and so `Star.WriteEphemeris`,
  `Site.WriteEphemeris`, `Spacecraft.WriteOrientation` and the Cosmographia export, throw `InvalidOperationException`
  with SPICE's message when the unload of the written file, which opens the other loaded SPKs or CKs again, fails: on a
  kernel SPICE holds under a relative name whose file was deleted, or after a change of working directory. Before, they
  returned true and the next kernel call failed with a wrong error. A new file is written in that case. A file the call
  replaces is unloaded before its removal, an unload that fails the same way: the call then throws with the file left on
  disk as it was, no longer loaded, and nothing written (review of the fourth round; before, the file was removed, nothing
  was written in its place, and the call threw). Their other failures carry SPICE's message too, where they named the
  standard output. The success path is bit for bit unchanged. Migration: name a meta-kernel's files by absolute paths, or
  keep them on disk and keep the working directory until they are unloaded; catch `InvalidOperationException` where such a
  kernel may be loaded, and after it do not assume that a file being replaced holds the new data.
- **`GetLoadedKernels` after a partial unload** ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673), Fixed above): after `UnloadKernels` of a file or a subfolder of a
  loaded folder, the listing names the folder's remaining kernel files, in its place, instead of the folder; after
  `UnloadKernels` of a deleted or moved path, its data is no longer readable. Migration: look for the folder's files,
  or unload the whole folder.
- **Breaking: `SpiceAPI.FindWindowsOnIlluminationConstraint` and the CLI take the altitude in metres** ([#488](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/488), Fixed above):
  the altitude of the `Planetodetic`, which `Planetodetic` documents in m, reached `gfilum_c` as km, so a caller
  passing metres got edges seconds off. A caller who passed km gets a point 1000 times lower: at DSS-13 the sunrise moves
  by 5 ms between 0 and 1071 m. The CLI help of `find-windows-from-illumination-constraint` now names the units of its
  coordinates (rad, rad, m). An altitude of 0 gives the same call, bit for bit. Migration: pass the altitude in m (a km
  altitude times 1000).
- **Breaking: a kernel-backed spacecraft's initial orbital parameters are about the Earth** ([#620](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/620), Fixed above): its
  `InitialOrbitalParameters.Observer` is the Earth unless `centerOfMotion` says otherwise (the SSB before), so the
  observer of a conjunction analysis it protects, and of anything else that reads that centre, is the Earth. Pass
  `centerOfMotion: new Barycenter(0)` for the former state; the kernels must chain the spacecraft to the centre. The
  constructor `Spacecraft(int, string, Time?, ...)` gained that optional parameter, so code compiled against 10.2.0-preview-1
  must be recompiled.
- **Breaking: reads outside the coverage throw** ([#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454), [#527](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/527), Fixed above): an ephemeris read or a frame
  transformation that SPICE cannot give, and a conjunction window past a propagated spacecraft's states, throw instead
  of returning an undefined state, the zero rotation or an extrapolation. A kernel-backed spacecraft built for its CK
  alone, without an SPK that covers its epoch, now throws at construction: its initial state cannot be read. A planet or
  a moon the loaded kernels do not chain to its system barycenter has `InitialOrbitalParameters` null, and its
  `SphereOfInfluence` is then +Infinity, as for a body built from custom parameters. Migration: load an SPK that covers
  the epoch before building a kernel-backed spacecraft, test the `InitialOrbitalParameters` of a planet or a moon for
  null where its kernels may not chain it to its barycenter, and keep a conjunction window inside the stored states of
  a propagated participant.
- **Breaking: `Time(string)` rejects a `Z` or an offset before a frame name** ([#455](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/455), Fixed above): `"2024-06-21T12:00:00Z TDB"`
  and `"…+02:00 UTC"` throw `ArgumentException`. The calendar was converted to the machine's local time and then labelled
  with the frame named, so the instant depended on the machine's time zone; on a machine in UTC a `Z` read as if absent.
  Write the calendar without its zone designator, or the `Z` form alone for UTC.
- **Breaking: `AnalyzeAll` on two propagations no longer lists the window ends** ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621), Fixed above), and returns an
  empty list for a window without an interior minimum. Migration: call `Analyze` where a window end must be
  considered; without an interior minimum it returns the end where the distance grows into the window.
- **Every TEME ↔ ICRF result moves** with the managed TEME ([#451](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/451), Fixed above), by 0.3″ to 23.3″ over 2000–2026
  (the angle between the former TEME and the definition; 2.0″ to 23.2″ at the 13 epochs of `tools/teme_reference`):
  the ICRF states of a TLE (`TLE.ToOsculating(...).ToFrame(Frame.ICRF)`, 326 to 428 m on the three TLEs of the TEME
  study, 82 to 851 m on the verification TLEs), a TLE fitted from ICRF (`TLE.Create`, `ToTLE`, about 168 m), OPM and
  OMM in TEME, the CLI given the frame `TEME` (the frame arguments of `ephemeris`, `orientation`, `frame-converter`
  and `orbital-parameters-converter`), `TLEPropagator` once converted, and the TLE participants of scenarios and
  conjunctions. TEME ↔ ICRF before 2000 and after the end of the PCK, a zero rotation before, is now defined. The SGP4
  states in TEME do not move with it; tsince in TT moves them (#599, Fixed above).

- **The maneuver chain on the attitude timeline** (#539, #547, #570; Fixed above). Breaking:
  - **An attitude delays the next maneuver.** It activates at the later of its `MinimumEpoch` and the end of the
    previous maneuver's window, and its hold gates the next maneuver: the last leading hold delays the first impulse (a
    nadir held 3 600 s before an apogee raise due at S + 2 963 s: the burn one orbit later, S + 8 889.569 s), and an
    attitude chained after an impulse starts at the end of the impulse's maneuver window, not at its event (the gate of
    the next impulse 5.1 s later on #573's case, plus the hold). `Spacecraft.SetStandbyManeuver(attitude, epoch)` now
    delays an attitude.
  - **The windows and the holds count TT seconds about the Earth** (#547): `ThrustWindow`, `ManeuverWindow` and the
    gate of the next maneuver move by the change of TDB − TT from the start of the window to them, which grows with
    that span: at most about 3.4e-10 s per second of it (the TDB − TT of the leapseconds kernel, 1.657e-3 s
    · sin E), so up to 2.0e-7 s over 10 min, 1.3e-6 s over 4 000 s, 4.5e-6 s over 3.7 h and 2.9e-5 s over a day, less
    away from the worst season (8.0e-7 s and 9.0e-7 s measured on the gates of #573's cases).
  - **After an impulse** the law in force before it resumes at the end of its maneuver window; an attitude whose
    activation falls after the end of the propagation is not executed (null windows, not in `ExecutedManeuvers`, the
    standby maneuver).
  - **`ManeuverEventDetector.HandleEvent` executes the impulse only** and returns no next detector: it no longer
    executes the attitudes chained after the impulse; the propagation advances the chain.
  - **`ExecutedManeuvers`, `ToOpm` and the summaries** hold the leading attitudes and the first impulse after them; the
    summary window of a scenario ends at the end of the last hold (`ScenarioTests`: 3 min after the last impulse's window
    end, 08:47:34.12 instead of 08:47:31.02).
  - **A gate at the start where g is exactly 0:** a leading attitude held 0 s gates the first impulse at the start of the
    propagation; an apsis given there by Keplerian elements (r·v = 0.0) then fires at the start with RK7(8), at the first
    step end with Velocity-Verlet (#577's rule of a zero at the gate), instead of one orbit later (#580).
  - **Unchanged:** the integration, `PropagationSolution`, the positions and the SPK bit for bit, except where a crossing
    lies within the TT shift of a gate (the change of TDB − TT above, about the Earth) or g is exactly 0 there, and
    except the burns the gates above move (the first impulse after a held leading attitude, by an orbit; the impulse
    after an attitude chained after an impulse; the gate at the start); a chain without attitudes in its
    integration and burns but for that shift, its impulses' attitudes now from the opening of their thrust windows;
    every propagation about another body, and SSB mode, but for the attitudes, the windows and the gates the rules above
    move.
  - See [Attitudes](../reference/attitudes.md#the-attitude-timeline) and
    [Impulse Maneuvers](../reference/impulse-maneuvers.md#maneuver-chain).
  - **Migration:** let `Spacecraft.Propagate`, `Scenario.SimulateAsync` or a propagator advance the chain; code that
    took the next detector from `HandleEvent` takes the next maneuver from `Maneuver.NextManeuver`. Give
    `SetStandbyManeuver(attitude, epoch)` a minimum epoch only when the attitude must wait for it, and shorten a hold
    that was not meant to delay the next maneuver.

- **The occultation searches of a propagated spacecraft, a site or two natural bodies run in managed code, with the
  ellipsoids and the types of `gfoclt`.** `FindWindowsOnOccultationConstraint` of a `CelestialItem` or a `Site`, when
  CSPICE cannot search (the observer or the target not in the kernels, or no spacecraft among them), modeled the bodies
  as spheres of their equatorial radius in front of each other by the distance of their centers, sampled a boolean
  condition and refined it to about 1 s: the solar eclipse of 2001-12-14 seen from the Earth's center started 0.16 s
  before `gfoclt`'s, that of 2005-10-03 from DSS-13 0.12 s, and a window holding at the start of the search started
  up to one step before it ([#461](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/461)).
  The library now models each body as the triaxial ellipsoid of its radii (`CelestialBody.Radii`) oriented by a
  body-fixed frame, the body's own or the one given in `OccultationSearchOptions` (`FrontFrame`, `TargetFrame`), and
  locates each contact of the apparent discs (the method of CSPICE's `zzocced`) by Brent's method to
  `OccultationSearchOptions.ConvergenceTolerance`, 1 µs by default. The types are `gfoclt`'s (`Full`, `Annular`,
  `Partial`, `Any`, and `None` for the rest of the window), with `Aberration.None` or a light time. A window is
  clamped to the search window, and an event shorter than the step is found when the step is at most half the time
  between the extremes of the margins (a quarter of the period for the eclipses of a satellite). Against `gfoclt`
  converged to 1e-9 s: 1.2e-7 s geometric, 1.3e-6 s with the light time; eclipse 001 of a propagated spacecraft within
  4.8e-4 s of GMAT R2026a. The search reads the states and orientations, never writes a kernel and keeps no state:
  searches on separate propagations may run concurrently. `CelestialItem.IsOcculted` takes the same geometry. See
  [Occultations](../reference/occultations.md).
  [#277](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/277)
- **Breaking: propagations about the Earth integrate over TT seconds.** Every numerical propagation integrated over TDB seconds,
  whatever its central body. About the Earth (the resolved central body NAIF 399) it now integrates over the TT seconds
  between the instants of its window, as GMAT (elapsed A1 seconds) and the conformance suite (spec.md §12.1) do, the
  forces at the instant TT₀ + t: the bodies and the orientation of the Earth at its TDB, the drag at its UTC. The state
  transition matrix, the covariance, the replay of the conjunction analysis, the avoidance options, the non-linearity
  index and Monte Carlo follow, with both integrators, through every activation path. A final state moves by |v|·δ, δ
  the change of TDB − TT over the window: at most 0.22 m a day in LEO, 0.67 m over the 3-day Molniya of the suite.
  Against GMAT R2026a, the largest component of the final state of the conformance propagators 001 and 003 goes from
  0.093 m to 0.014 m and from 0.163 m to 0.011 m, and that of 013 from 0.518 m to 0.35 mm; with the model's GM in the
  central term (`GmatConformancePropagatorTests`, RK7(8) at 1e-11, largest components) 8.8 cm, 6.0 cm and 15.6 cm
  become 1.9 cm, 0.83 cm and 1.1 cm, and Velocity-Verlet at 1 s on 002 goes from 33.7 cm to 39.7 cm (probably, the δ
  term had cancelled part of its step error: an inference from the magnitudes alone, not traced). The three RK7(8) goldens are recaptured: 20.8 mm
  (GEO, a day), 8.3 mm (the maneuver case) and 31.1 mm (drag and SRP, 103 steps instead of 99, of which 6.6 mm the
  dynamics and 24.4 mm the integration error of the former steps). About the Moon, Mars, the Sun and in SSB mode the
  propagations are bit for bit. The conventions:
  - the labels stay TDB: segment bases, stage, event and output epochs are TDB `Time`s; `PropagationSegment.Duration`,
    `AcceptedStep.CumulativeTime` and `StepSize`, the event time and the duration of `IIntegrator.IntegrateSegment`
    count the seconds of the time argument. A custom integrator that builds its own epochs keeps TDB seconds (3.4e-10
    relative);
  - a window has the TT seconds between its instants, whatever their tags: a UTC day is 86 400 s, 86 401 s across a leap
    second, a TDB-tagged day from 2021-03-20 12:00 86 399.99999324768 s. The outputs are uniform in TT: those of a UTC
    window fall on its UTC grid, those of a TDB-tagged window leave its TDB grid by δ (29 µs a day), so an exact lookup
    of a round TDB epoch misses them. The TDB gaps of the labels of every propagation about the Earth, UTC or TDB window,
    vary by a tick, so `Spacecraft.WriteEphemeris` writes an SPK segment of type 9 where it wrote type 8. A window whose
    output grid misses its length by less than 1e-9 of it ends at its end, its last output labelled with the end as
    given;
  - a label holds its instant to the tick: chaining propagations or reading a solution through the labels a user
    receives keeps up to 1e-7 s, about 1e-10 relative on Φ and 0.75 mm in LEO;
  - the velocities of the input and output states are per TT second: a state from a TDB-rate source differs by up to
    3.4e-10 relative, about 0.65 m a day along-track in LEO as an initial condition (computed);
  - two-body motion about the Earth counts TT seconds too: `AtEpoch`, `ToKeplerianElements(epoch)`,
    `ToStateVector(epoch)`, `EpochAtMeanAnomaly` and `ToStateVector(trueAnomaly)` (with the Fixed entry of #508, above).
    A `TimeSpan` added to a TDB epoch is a span of TDB: `elements.ToStateVector(elements.Epoch + elements.Period())`
    from a TDB epoch is off the start by n·δ (1.2e-6 s of motion over an hour at J2000), not from a TT epoch, nor from
    a UTC epoch away from a leap second (across one, `Epoch + Period()` adds calendar seconds: see #508 under Fixed, above);
  - the default central term keeps the GM of the kernels, TDB-compatible, against the TT time argument: 1.5e-8 from the
    TT-compatible GM of EGM2008, which `GeopotentialCentralTerm.ModelGravitationalParameter` gives.

  The conformance runner reads the final state at the end of the window as the case tags it, and the `stm/001` fixture
  is a UTC window. See [Time & Windows](../reference/time-and-windows.md#the-time-argument-of-a-propagation)
  ([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)). Migration: a custom `IIntegrator`, and code that reads `Duration`, `CumulativeTime` or
  `StepSize`, takes TT seconds about the Earth; read a solution at a TDB epoch through `InterpolateAt`, not by an exact
  lookup of its outputs.
- **Breaking: the field-of-view search: edges, time frame, short events, corrections and errors.** With the Fixed entry of
  #515 above:
  - the managed path locates the edges to 1 µs instead of 1 s; a window that holds at the start of the search window
    starts there, not a step before it, and one that holds at its end ends there;
  - it returns TDB windows whatever the frame of the search window, as the CSPICE path does (it returned the frame of
    the search window), and counts the step in TDB seconds;
  - it finds a window or a gap shorter than the step when the step is at most half the time between the extremes of
    the margin; `gftfov` needs a step shorter than every window and gap, so with a longer step the managed path may find
    an event the CSPICE path misses;
  - `+S` applies the stellar aberration of `stelab` (`stlabx` for transmission) in the search and in `IsInFOV`, as
    `gftfov` does: in `gftfov`, `LT+S` moves the edges from `LT` by up to 0.11 s for the Earth, 14.1 s for the Moon and
    549 s for the Sun. `CelestialItem.GetEphemeris` applies the same correction, on the reference-body path too since
    [#491](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/491) (Fixed above);
  - a `Star` is a direction, as `gfrfov` reads a ray: no light time (`LT` and `CN` read as `None`), the stellar
    aberration alone for their `+S` forms;
  - `IsInFOV` is the `POINT` condition of `fovtrg` with the same field and corrections, and stays virtual; the search no
    longer calls it, so an override no longer changes the search. The overrides of `EllipticalInstrument` and
    `RectangularInstrument` are removed. Where it returned a value, it now throws `InvalidOperationException` for a field
    the search rejects (below: a boresight zero or parallel to the reference vector, a half-angle not positive, an
    angular radius above π/2 − 1e-6, a circle of half-angle π/2 included, a non-circular instrument of another
    subclass), and `ArgumentException` when the target is the instrument's own spacecraft; since [#707](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/707) the
    constructor of the instrument rejects the zero, non-finite and parallel vectors and a field of view that is not a
    finite positive number (`ArgumentException`, Changed above), and since [#715](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/715) the constructors of the ellipse and the
    rectangle a cross angle that is not one, so the search's check is left with the angular radius and the shape;
  - both paths check the same rules before the search and throw where they returned no window, or one computed on an
    extrapolation: a step that is not positive (`ArgumentOutOfRangeException`); a search window that ends before it
    starts, a target that is the instrument's spacecraft, an ellipsoid target without radii (a site, a spacecraft, a
    barycenter, a Lagrange point, a star) (`ArgumentException`); a boresight zero or parallel to the reference vector
    (rejected by the instrument's constructor since [#707](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/707)), a half-angle that is not positive (rejected by the
    constructors since [#707](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/707) and [#715](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/715)), a field whose angular
    radius exceeds π/2 − 1e-6, a non-circular instrument of another subclass (`InvalidOperationException`); on the managed path, a search window that leaves the propagated
    states of the instrument's spacecraft or of a spacecraft target (`ArgumentException`), and an observer inside the
    target's ellipsoid (`InvalidOperationException`). A spacecraft target is still read up to its light time before the
    start (reception) or after the end (transmission, `XLT` and `XCN`), milliseconds about the Earth, extrapolated.

  A search on a propagated spacecraft takes 0.1 to 1 s (0.1 to 0.7 s before). The occultation searches share the
  light time and the root finder, bit for bit. No other public signature changes; `Instrument.PositionInFOV` is
  removed (below) ([#515](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/515)). Migration: a subclass of `Instrument` that changed the search through
  `IsInFOV` gets the field of its shape, as `gftfov` does; test any other condition on the windows returned, and catch
  or avoid the inputs above that now throw.
- **Breaking: the kernels of a propagated spacecraft.** With the Fixed entry above, `Spacecraft.WriteOrientation`
  writes the C-matrix in J2000 with the angular velocity of the CK: every CK of an attitude other than the identity of
  ICRF changes. CKs written before hold the inverse attitude, in the initial frame, interpolated across the discrete
  changes; every reader (SPICE, Cosmographia, `SpiceAPI.ReadOrientation`, a kernel-backed spacecraft) reads them as
  before, and they cannot be repaired from their content: regenerate them by propagating again. A discrete change is
  held up to 1 µs before it, then the CK turns to the new attitude over that microsecond (up to 0.79 rad in the tests).
  The SPK is written in J2000, no longer in the initial frame (both read the same through SPICE). `WriteOrientation` of
  a spacecraft never propagated throws `InvalidOperationException`, where it wrote what its reads had stored or threw
  `ArgumentException`. `TLEPropagator` records no attitude: its SPK can now be written, its CK still cannot.
- **Breaking: the SPK of a propagated spacecraft is written per arc.** With the Fixed entry of #540 above,
  `Spacecraft.WriteEphemeris` writes one segment of type 9 (Lagrange, unequal steps) per arc between impulses, in time
  order, of degree min(7, N − 1) for an arc of N states, through a managed DAF writer: at an impulse an SPK reader gets
  the state after it, 1 µs before it the state before. The native writer wrote one segment of degree 15 across every
  impulse, of type 8 for equal steps (SSB mode, other central bodies; far from the impulses, 2.2e-2 m off in SSB mode
  where type 9 is 4.7e-4 m); a file of type 9 with several segments reads the same through SPICE and every SPK reader.
  An arc bounded by an impulse with fewer than 4 states takes 16 states of the solution. As before, a file that ends at
  an impulse located at the end of the propagation ends with the state before it, as the solution does. A file of 25
  segments or more holds a full summary record followed by an empty one, as CSPICE writes it. A write the writer refuses
  (no state, an arc of one state) leaves an existing file as it was, as the native writer did. The file is
  replaced and left unloaded, as before; `Star` and `Site` keep the native writer. SPKs written before read as before:
  regenerate those that cross an impulse.
- **Breaking: `Spacecraft.AddPropagatedStates`.** A propagation by `Spacecraft.Propagate` or `Scenario.SimulateAsync`
  replaces every stored state, those added before a first propagation included: they were merged with the propagated
  ones (only a propagated spacecraft was cleared). `TLEPropagator.Propagate` still adds its states, as before. A state
  goes to the arc of its epoch (the later one at an impulse), replacing a stored state of that arc at the same epoch;
  without stored states the states make one arc, read and written as before. Migration: add states after the
  propagation, not before it.
- **Breaking: states and attitudes in ICRF.** A spacecraft propagated from ECLIPJ2000, B1950, FK4, ECLIPB1950 or
  GALACTIC stores its states (with their covariance) and its attitude in ICRF; `PropagationSolution` keeps the initial
  frame, bit for bit, and the states read back in that frame are a rotation round trip, no longer bit for bit those of
  the solution. `Spacecraft.AddPropagatedStates` converts a state to ICRF, and a spacecraft not propagated returns its
  two-body state in ICRF. Without an attitude maneuver the attitude is the identity of ICRF, where the CK held the
  identity of the initial frame (0.409, 0.011 or 2.12 rad from it). Every attitude is computed on the state in ICRF: the
  roll that a single-vector or an impulse attitude leaves free no longer depends on the initial frame (0.48 to 0.87 rad
  between ICRF and ECLIPJ2000 before), and `Attitude.TryExecute` and `ImpulseManeuver.TryExecute` return the rotation
  from the spacecraft frame to ICRF, labelled ICRF, for a state in any frame. Migration: read the stored states in the
  initial frame with `GetEphemeris` in that frame or `ToFrame`, and compare them with the solution to rounding, not
  bit for bit.
- **Breaking: the attitude between outputs.** A continuous attitude between two output epochs is the spherical linear
  interpolation of its samples, no longer the earlier one held, up to a discrete change; its residual against the law
  grows as the square of the output step (5.7e-7, 5.7e-5 and 2.1e-3 rad at 1, 10 and 60 s on the inclined LEO of the
  tests; 5.9e-5 rad at 10 s for the TRIAD after an impulse since #539). Its angular velocity is that of the CK, no longer 0, in the convention of the frames (ICRF relative to the
  spacecraft, in the spacecraft frame), 2.3e-5 rad/s from the rate of the law at 10 s outputs; every read of the record
  returns it, the entries of `GetStateOrientationsToICRF` and `GetLatestStateOrientationToICRF` included, and the rate
  given to `AddStateOrientationToICRF` is not kept. `StateOrientation.AtDate` is unchanged, and its documentation states
  its convention, the opposite of the frames'. No migration: the attitude held between outputs was wrong; shorten the
  output step where the residual matters (#592).
- **Breaking: velocities in the spacecraft frame.** The spacecraft frame now turns with the attitude, where its angular
  velocity was 0, so a velocity expressed in it (`StateVector.ToFrame(spacecraft.Frame)`, `GetEphemeris` with the
  spacecraft frame, the angular velocity of `Spacecraft.GetOrientation`) takes the rotation of the frame,
  v' = R v − (R ω) × r': for the Earth's centre seen from a LEO, 7.7e3 m/s with a nadir attitude and 2.0e4 m/s with the
  prograde attitude of the tests, 4.2e8 m/s for the Sun. Against `spkezr` in the spacecraft frame on the kernels the
  library writes, the state relative to the Earth is 3.9e-3 m and 1.0e-5 m/s off (Linux x64, Windows x64 and macOS arm64).
  No migration: the former velocities left out the rotation of the frame.
- **Breaking: `SpacecraftFrame`.** A read stores nothing; the frame of a kernel-backed spacecraft reads SPICE at each
  call and no longer caches, so it no longer answers from its cache after its CK is unloaded. The entries and the reads are labelled
  with the spacecraft frame, the source of their rotation (they were labelled with the initial frame);
  `AddStateOrientationToICRF` takes the rotation from the spacecraft frame to ICRF so labelled, throws
  `ArgumentException` for another label, and replaces the entries within 2 µs, where it was dropped at an existing
  epoch. `AddStateOrientationToICRF`, `GetStateOrientationsToICRF`, `GetLatestStateOrientationToICRF` and
  `ClearStateOrientations` are virtual on `Frame`. Migration: keep the CK loaded while the frame of a kernel-backed
  spacecraft is read, and label a rotation given to `AddStateOrientationToICRF` with the spacecraft frame.
- **Breaking: `Instrument.Orientation` is the Euler angles 1-2-3 of the frame kernel.** With the Fixed entry above. A
  zero orientation is unchanged everywhere. About one axis, the managed field-of-view path is unchanged bit for bit,
  while `GetBoresightInSpacecraftFrame`, `GetRefVectorInSpacecraftFrame` and the attitudes built from them turn every
  vector by twice the angle about that axis, a vector along the axis excepted (34.38° at 0.3 rad, 180° at π/2). About
  more than one axis the managed path moves too (4.634° for (0.3, 0.5, 0.2)). A `TriadAttitude` or an
  `InstrumentPointingToAttitude` built from an instrument with a non-zero orientation changes, and now points the
  boresight the field-of-view search and SPICE see. The frame kernel and the CSPICE path are unchanged. Migration: give
  the Euler angles (x, y, z) about the axes 1, 2, 3 that the frame kernel takes; an orientation tuned to the former
  managed path or getters is computed again in that convention (`Instrument.RotationToSpacecraft` gives the rotation
  it means).
- **Breaking: `InstrumentPointingToAttitude`** returns the rotation that takes the boresight to the target (Fixed
  above): every attitude it computes changes, at zero orientation too. Migration: remove any inversion that made up
  for the former result.
- **Unchanged: `SpiceAPI.ReadOrientation` and `SpiceAPI.WriteOrientation`.** They stay the C-matrix pair of `ckgpav_c`
  and `ckw03_c`, the rotation from the reference frame to the spacecraft frame with the angular velocity of the
  spacecraft relative to the reference frame, in it; their documentation now says so. On a CK written by 11.0.0,
  `ReadOrientation` returns the C-matrix of the attitude, with a non-zero angular velocity.
- **A `StateVector` read at its own epoch is the state itself.** `ToStateVector(epoch)` (and `ToStateVector(trueAnomaly)`
  when it lands on the epoch) at the instant of the state's epoch, in any time frame, returns the state, position,
  velocity, frame, observer and covariance unchanged, tagged as `epoch` is: the same object for the same tag. It was the
  round trip through Keplerian elements, which moved it by rounding: 1.7e-9 m and 1.9e-12 m/s for a LEO, 5.3e-8 m and
  3.3e-12 m/s for a GEO. This reaches `Spacecraft.ToOpm`, the state of a spacecraft read before two states are
  propagated, and the directions of `ApsidalAlignmentManeuver`. At any other instant the result is the two-body motion,
  without a covariance, as before ([#551](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/551)).
- **`StateOrientation.Interpolate` is the exact spherical linear interpolation.** It took `Quaternion.SLERP`, which
  switches to a normalised linear interpolation below about 0.028 rad between the rotations (6e-8 rad off at
  0.025 rad); it now takes the axis and the angle of the rotation between them, the interpolation the attitude record of
  a spacecraft already uses (bit for bit the same). `Quaternion.SLERP` itself is unchanged. `MemoryDataProvider` no
  longer grows with its reads ([#548](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/548)).
- **Breaking: the `orientation` command of a spacecraft.** With the Fixed entry above. For a negative identifier it prints
  the attitude of the CK, relative to the requested frame, where it printed the identity whatever the CK held: its
  output changes for every spacecraft whose CK is not the identity. An epoch outside the CK throws
  `InvalidOperationException` with the SPICE error, where it printed the identity
  ([#542](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/542)). No migration: the former output ignored the CK.
- **Breaking: a number of seconds becomes the nearest tick of a `Time`** ([#625](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/625), Fixed above):
  `Time.Create`, `CreateTDB`, `CreateUTC`, `AddSeconds`, `AddMilliseconds`, `AddMicroseconds` and the TDB conversions
  give the nearest tick, ties to even, where they truncated, so an epoch built from seconds can be a tick later after
  J2000 and earlier before it; a span that is not finite, or whose ticks do not fit in a `long`, throws
  `ArgumentOutOfRangeException` (`AddSeconds(double.NaN)` returned the time unchanged). A coverage bound from 1983 to 2017 given
  as seconds can now be a tick outside the coverage, where the truncation kept a start from 1983 to J2000 or an end from 2000
  to 2017 inside, and a read there throws: read from the first tick inside. `SpiceAPI.ReadEphemeris(Time …)` and a
  kernel-backed spacecraft label their state with the epoch asked, and an end of a CSPICE search at an end of its search
  window is that end. `AddDays`, `AddHours`, `AddMinutes`, `CreateFromJD` and the `TimeSpan.FromSeconds` of the library
  outside `Time` (`Period()`, `SideralRotationPeriod`, `TrueSolarDay`, `Launch`, the windows of an impulse, the hold of
  `PhasingManeuver`, the epochs of the conjunction search) still truncate. Migration: pass a finite span, and expect an
  epoch built from seconds to differ from its former value by a tick.
- **Breaking: `TimeToRadius` and `TimeToSphereOfInfluence` throw where the orbit never reaches the radius** ([#627](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/627), Fixed above):
  a radius below the periapsis or above the apoapsis of an ellipse, an infinite one, or one reached outside the epochs
  of `Time`, throws `ArgumentOutOfRangeException` where it returned an apsis or failed with NaN; an infinite sphere of
  influence throws `InvalidOperationException`; a circular orbit returns its epoch. Migration: check that the radius
  lies between the periapsis and, on an ellipse, the apoapsis, or catch `ArgumentOutOfRangeException`; check the
  sphere of influence for +Infinity before `TimeToSphereOfInfluence`.
- **Breaking: `Quaternion.Identity` replaces `Quaternion.Zero`, and every public static field of the library is
  `readonly`** ([#571](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/571)). `Quaternion.Zero`
  was the identity (1, 0, 0, 0) under the name of the zero quaternion, and an assignable field: after
  `Quaternion.Zero = new Quaternion(0, 1, 0, 0)` anywhere in a process, `Frame.ICRF.ToFrame(Frame.ICRF, t)` was a 180°
  rotation about x, and so was every identity of the library (frames, `Vector3.To`, the attitudes, the initial attitude
  of a propagation). It is now `public static readonly Quaternion Identity`, the same value; there is no zero-quaternion
  constant. The 411 `NaifObject`, `CelestialBody` and `Barycenter` fields of `SolarSystemObjects` (`PlanetsAndMoons`,
  `Stars`, `Barycenters`, `Asteroids`, `Comets`, `GroundStations`, `LagrangePoints`) are `static readonly`, their
  values unchanged. Migration: replace `Quaternion.Zero` with `Quaternion.Identity` (or `new Quaternion()`); code that
  assigned one of these fields no longer compiles, so build the value it held in a variable of its own.
- **Breaking: a frame is its name, for every relation**
  ([#562](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/562)). `Frame`'s `==`, `!=` and
  `Equals(object)` also compared the type, while `Equals(Frame)` and `GetHashCode` compared the name only: a frame built
  by name and the managed frame of the same name (`GcrfFrame`, `CirsFrame`, `TirsFrame`, `TemeFrame`) or a
  `SpacecraftFrame` or `SiteFrame` were equal for `HashSet`, `Dictionary` and `Assert.Equal` and different for the
  operators, so a state in `new Frame("GCRF")` (a CCSDS file, the CLI) was rejected by the propagator as not inertial,
  two `StateVector`s that differed only by `Frame.GCRF` and `new Frame("GCRF")` were not equal, and a state in
  `Frame.CIRS` converted to `new Frame("CIRS")` missed the identity and went to SPICE. The five members are now one
  relation: the name compared ignoring case, as SPICE compares frame names (`namfrm_c`), whatever the type;
  `Equals(object)` and `GetHashCode` are `sealed`. `new Frame("TEME") == Frame.TEME`, `new Frame("gcrf") == Frame.GCRF`,
  `new Frame("CIRS") == Frame.CIRS` and `new Frame("j2000") == Frame.ICRF` are true; a propagation from
  `new Frame("gcrf")` is that of `Frame.GCRF`, bit for bit, in Velocity-Verlet and RK7(8), with an impulse and in SSB
  mode. Migration: code that told a managed frame from a frame of the same name with `==` tests the type instead
  (`frame is CirsFrame`); a subclass of `Frame` can no longer override `Equals(object)` or `GetHashCode`. Equality does
  not route: a frame named CIRS or TIRS that is not a `CirsFrame` or a `TirsFrame` still goes to SPICE, which does not
  know it ([Equality](../reference/frames.md#equality)). Nor does the name carry a definition: a `SiteFrame` or a
  `SpacecraftFrame` equals any frame of its name, a kernel frame included (a site named DSS-13 and the kernel frame
  `DSS-13_TOPO`), so `ToFrame` between them is the identity whatever their axes; give a site or a spacecraft a name no
  loaded kernel uses for another frame. A state converted to a frame equal to its own keeps its frame
  (the identity of `ToFrame`), so the CLI's `ephemeris` from a site observer, computed in ICRF, prints `Frame : J2000`
  for the default `ICRF` (handed to the library as `j2000`) where it printed `Frame : j2000`, the numbers unchanged.
- **Breaking: `SpiceAPI.GetCelestialBodyInfo` throws for an identifier that is not a celestial body** ([#560](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/560), Fixed above):
  an identifier without a name, such as -999, returned an empty record (identifier -1) and now throws
  `ArgumentException`, as `new CelestialBody(id)` and the command `celestial-body-info` do for it.
  `SpiceAPI.WriteOrientation` throws `ArgumentException` for records in more than one frame. Migration: pass the
  identifier of a celestial body (a barycenter 0 to 9, the Sun 10, a planet, a moon or a Lagrange point 101 to 999, or
  above 1000), or catch `ArgumentException`; write one frame per call to `SpiceAPI.WriteOrientation`.
- **Breaking: an identifier SPICE does not name throws `ArgumentException`** ([#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672), Fixed above), extending #560's
  entry above to the identifiers of the celestial classes (398, 12345678, 2000003):
  `SpiceAPI.GetCelestialBodyInfo` (`ParamName` `naifId`) returned a record with identifier -1 and a stale or garbage
  name; `new CelestialBody(id)`, `CelestialItem.Create(id)`, the command `celestial-body-info` and the observer of
  `SpiceAPI.ReadEphemeris` by identifiers threw `InvalidOperationException` or built a body from undefined bytes;
  a Lagrange point 391 to 395 the kernels do not name (`Data/SolarSystem` names none) throws from
  `SpiceAPI.GetCelestialBodyInfo` and `new CelestialBody(id)` only: `CelestialItem.Create(id)`, and so the observer of
  `SpiceAPI.ReadEphemeris`, still builds a `LagrangePoint` without reading the body information, named or not (its
  constructor reads its SPK at J2000 TDB, which `Data/SolarSystem` does not hold for L3);
  `SpiceAPI.FindWindowsOnOccultationConstraint` with such an identifier as an ellipsoid front or target without a frame
  (`ParamName` `frontBodyId` or `targetId`) returned no window. Migration: load the kernel that names the body
  (`NAIF_BODY_NAME`, `NAIF_BODY_CODE`), or catch `ArgumentException`.
- **Breaking: the GM and the frame of a body without radii or GM, and the observer of a read from a site** ([#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672),
  Fixed above). Under the same signatures: `SpiceAPI.GetCelestialBodyInfo(id).GM`, `CelestialBody.GM` and `Mass` move
  from 0 to the kernel's value for 7 bodies (Nix 3048175.64816976, Hydra 3211039.206155255, Kerberos
  1110040.850536676, Pallas 13665878145.96742, the Didymos barycentre 37.0362727488241, Dimorphos 0.339845587494,
  Didymos 36.696427161329 m³/s², as SPICE parses `gm_de440.tpc`; Styx's is 0), so a propagation with one of them as a
  third body now attracts; their frames (IAU_NIX, IAU_HYDRA, IAU_PALLAS, IAU_DIMORPHOS, IAU_DIDYMOS) and those of the
  bodies with radii and no GM are read, so `new CelestialBody` builds them, and Kerberos, Styx and the Didymos
  barycentre, which have none, throw `InvalidOperationException` every time; a barycentre's `FrameName` is empty. The
  record of a site identifier reads frame DSS-13_TOPO and GM 0, so `new CelestialBody(399013)` builds a body classed
  as an asteroid ([#651](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/651)) where it
  threw or was built with a garbage frame name. The observer of a state `SpiceAPI.ReadEphemeris` reads from a site
  identifier is a `Site` (NaifId 399013, Name DSS-13), not a `CelestialBody`, and a site whose SPK does not cover J2000
  TDB throws `InvalidOperationException` from the `Site` constructor. Every body with radii and a GM, and every
  barycentre's GM, is read bit for bit as before. Migration: none for a body the kernels describe; a caller that cast
  the observer of a read from a site to `CelestialBody` casts it to `Site`.
- **`Star.GetEphemeris` applies no light time** ([#533](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/533), Fixed above): with `LT`, `CN`, `XLT` and `XCN` it
  returns the geometric direction at the epoch, as with `None`, and with their `+S` forms that direction with the
  stellar aberration alone; the position moved by the proper motion over the star's light time before (6.2″ for
  0.1″/yr at 20 pc). The occultation search reads a star the same way.
- **Breaking: the managed `CoordinateSystem.Planetographic` is CSPICE's PLANETOGRAPHIC** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682), Fixed
  above): its longitude is in [0, 2π), positive west for a prograde body other than the Earth, the Moon and the Sun and
  positive east otherwise (the Moon from the Earth above 1 rad: 11 windows where there were 10, the second starting at
  04:00:35.63 instead of 16:23:27.62 TDB), its latitude the planetographic (geodetic) one (edges 51.5 s and 47.5 s from
  where they were for the Moon above −0.4 rad; unchanged on a sphere, such as the Moon or Mercury), and its third
  coordinate the altitude above the spheroid: `Coordinate.Altitude` is searched and `Coordinate.Radius` throws
  `ArgumentOutOfRangeException`. The managed path does not read `BODY<id>_PGR_POSITIVE_LON`, by which a kernel can
  override the sense for `gfposc_c` (GF Required Reading, "Meridian crossing"); no kernel of the library defines it.
  It works in the body-fixed frame of the observer's body, as `Geodetic` does, where `gfposc_c` takes the body of the
  frame's centre: the two agree for a frame centred on the observer. Migration: for the former east longitude in (−π, π],
  search `CoordinateSystem.Geodetic`, whose longitude it was; for the planetocentric latitude and radius,
  `CoordinateSystem.RaDec` (`Declination`, `Range`) in the body-fixed frame.
- **Breaking: `Site.FindWindowsOnIlluminationConstraint` throws for the emission and phase angles** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682),
  Fixed above): `ArgumentException` (`illuminationType`), the search having no observer. Migration: with the
  observer's NAIF id, `SpiceAPI.FindWindowsOnIlluminationConstraint(window, observerId, site.CelestialBody.NaifId,
  site.CelestialBody.Frame, site.Planetodetic, angle, …)` for objects of the kernels; otherwise
  `new GeometryFinder().FindWindowsWithCondition(window, t => site.IlluminationEmission(t, observer, aberration), relation,
  value, step)`, or `IlluminationPhase(t, source, observer, aberration)`.
- **Breaking: a field-of-view search or `IsInFOV` on a managed spacecraft without attitude throws** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682),
  Fixed above): `InvalidOperationException` for a spacecraft never propagated and with no orientation added, a spacecraft
  built from a TLE (and not propagated by `Spacecraft.Propagate`, which records its attitude since
  [#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)), and one propagated by `TLEPropagator`, which records no attitude
  ([#546](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/546)). Migration: propagate the spacecraft, or give it its attitude,
  `spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(q, Vector3.Zero, epoch, spacecraft.Frame))`;
  `Quaternion.Identity` reproduces the former result.
- **Breaking: an illumination method other than ELLIPSOID throws** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682), Fixed above):
  `Site.FindWindowsOnIlluminationConstraint` and `SpiceAPI.FindWindowsOnIlluminationConstraint` throw
  `ArgumentException` (`method`) for anything but ELLIPSOID, case and leading or trailing whitespace aside, the DSK forms
  included, which `gfilum_c` never supported (they returned no window). Migration: pass `"ELLIPSOID"`, the default.
- **Breaking: a site built from coordinates under the name of a station of a loaded frame kernel is read in its own
  frame** ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682), Fixed above): `new Site(13, "DSS-13", earth, coordinates)` has the frame name `DSS-13_TOPO`,
  which `earth_topo_201023.tf` defines; its reads in its frame from an observer other than the Earth went through the
  kernel's station frame and are now rotated by its own coordinates (27.0 to 30.3 m and 2.0e-3 m/s from the Moon for
  DSS-13's own coordinates, 2024-03-01). A caller who loaded the frame kernel the library writes for a site (or the
  frame kernel and the CK of a managed spacecraft, read through an object whose attitude record holds an entry) gets the
  library's rotation, the source of those kernels: 4.0e-7 m from them for a site, 6.8e-8 m for a spacecraft; a managed
  spacecraft with an empty record (a new object of the id, never propagated) is read through the kernels by name, as
  before. Migration: give a site built from coordinates a name no loaded kernel uses, or build the
  station from the kernels, `new Site(13, "DSS-13", earth)`.

- **Breaking: a maneuver draws on a tank of the spacecraft it is propagated with** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), [#586](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/586), Fixed above): a
  propagation whose chain holds a maneuver on an engine whose tank is not one of the spacecraft's throws
  `InvalidOperationException` before it runs, from `Spacecraft.Propagate`, `PropagateAsync`, the builder, the
  `CentralBodyPropagator` constructors and `Scenario.SimulateAsync` (in `PropagationResult.Error` for `BatchPropagator`).
  An impulse on such a tank threw `NullReferenceException` mid-run or burnt another spacecraft's fuel; an attitude on it
  ran with the static body axes or another spacecraft's. A chain built on the spacecraft's own tanks is unchanged, bit
  for bit. Migration: build each spacecraft's maneuvers on an engine whose tank was added to that spacecraft; distinct
  spacecraft imply distinct maneuvers. The exception types of the component checks follow one rule (decided by S.
  Guillet, 2026-11-14, after the .NET Framework Design Guidelines): `ArgumentException` where the call receives the
  faulty object (`AddFuelTank`, `AddEngine`, a constructor's clock), `InvalidOperationException` for this chain check,
  which reads the state of the spacecraft, as #585's check on the same paths. #586 proposed `ArgumentException` for the
  chain check; it keeps `InvalidOperationException` by that rule.
- **Breaking: `Spacecraft.AddFuelTank` rejects a tank held by another spacecraft** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), [#586](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/586), Fixed above):
  `ArgumentException`, nothing changed; it moved the tank before. Migration: one tank per spacecraft.
- **Breaking: `Spacecraft.AddEngine` takes the tank instance added, and throws `ArgumentException`** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), [#586](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/586), Fixed above): an engine on a tank equal by value to one of the spacecraft's, but another
  instance, throws `ArgumentException` (`ParamName` "engine"), and so does an engine on an unknown tank, which threw
  `InvalidOperationException` before (the rule of the Breaking entry on the maneuver tanks above). Migration: build the
  engine on the tank instance given to `AddFuelTank`, and catch `ArgumentException` where `InvalidOperationException`
  was caught.
- **Breaking: a clock belongs to the spacecraft of one NAIF id** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), Fixed above): both `Spacecraft` constructors and
  `Opm.ToSpacecraft` throw `ArgumentException` (`ParamName` "clock") for a clock attached to a spacecraft of another
  id. Migration: one `new Clock(name, resolution)` per spacecraft.
- **Breaking: a spacecraft NAIF id is in [-2147483, -1]** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), Fixed above): both constructors, `Opm.ToSpacecraft`
  and the runs of a Monte Carlo campaign whose `BaseNaifId - i` leaves the range (at the construction of their tasks)
  throw `ArgumentOutOfRangeException`. Such an id gave a wrong frame id before. Migration: an id in the range.
- **Breaking: `Add*Instrument` rejects the id of the spacecraft frame, NaifId × 1000** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), Fixed above):
  `ArgumentOutOfRangeException`. Migration: an id in [NaifId × 1000 − 999, NaifId × 1000 − 1].
- **Breaking: the frame kernel writers reject names SPICE cannot hold in a kernel variable** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), Fixed above):
  `SpacecraftFrame.WriteAsync`, `Instrument.WriteFrameAsync`, `Instrument.WriteKernelAsync`, `Site.WriteFrameAsync`,
  and `CosmographiaExporter` and the CLI command `propagate` through them, throw `InvalidOperationException` for a name
  with a blank, a tab or another whitespace or control character, a quote, a parenthesis, a comma or '=', or of more
  than 32 bytes in its variable: 20 ASCII characters for a spacecraft, 25 for a spacecraft and an instrument together,
  21 for a site. Every name refused by the frame kernel writers gave an FK that `LoadKernels` rejected, and the
  instrument kernel names the frame of that FK; the constructors are unchanged, so an OPM object named 'ISS (ZARYA)' still
  builds and propagates. Migration: name the spacecraft, the instrument or the site within the rule.
- **`CelestialItem.Satellites` holds its items weakly and by instance, safe across threads** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), Fixed above): a
  spacecraft referenced nowhere else leaves the collection after a collection of the garbage collector, two items of one
  NAIF id are both listed, `SetInitialOrbitalParameters` removes only the instance it moves, and the order of the
  enumeration is not specified. A class derived directly from `CelestialItem` outside the library is no longer listed
  by its observer: the listing is the last statement of the constructors of `CelestialBody`, `Barycenter` and
  `Spacecraft`. Migration: keep a reference to a spacecraft that is reached through `Satellites`.
- **The frame of a kernel-backed spacecraft is the frame the kernels define for its id** ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676), Fixed above): SPICE is
  asked for the name of the frame of id NaifId × 1000 at each read (`frmnam_c`), one more native call per read of that
  frame (0.3 to 1.3 µs on reads of 3.4 to 4.2 µs). A twin whose `Name + "_FRAME"` named the frame of another id, which
  it read silently, reads the frame of its own id. The native layer gains `GetFrameNameProxy`, the bool and
  `GetLastErrorProxy` pattern; its P/Invoke and the
  native library of this release are in step.

- **Breaking: the velocity of a `TLEPropagator` solution between its samples moves** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675), Fixed above): from up to
  49.9 m/s off SGP4 to 4.3e-4 m/s (ISS, 60 s); the positions and the states at the samples are bit for bit. The
  `PropagationSolution` overloads of the conjunction analysis fed with two `TLEPropagator` solutions move with it (the
  ISS and its elements at 61.64°, 6 h, 7 encounters: the TCA from up to 4.44 ms to 5.2 µs from SGP4's, the relative
  speed from 0.75 m/s to 7.3e-5 m/s off, the miss by at most 0.27 mm). `TLEPropagator` reads SGP4 twice more per
  sample (1 day at 60 s from 87-133 ms to 262-305 ms), 1 s before the start and 1 s after the last sample included, so a
  TLE whose SGP4 fails within 1 s of an end of the window throws `InvalidOperationException`, before the spacecraft
  receives any sample (its stored states left as they were). Migration: none for the
  velocities; for a TLE that decays at an end of the window, shorten the window by a second. The exact SGP4 state at any
  instant is `tle.ToStateVector(t)`.
- **Breaking: a malformed TLE reports its own error first** ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675), Fixed above): a null or empty line 1 gives "Value
  cannot be null or empty." and a line 1 shorter than 32 characters "TLE line 1 must be exactly 69 characters long"
  where both gave "Invalid TLE line 1 format." (`ArgumentException`, parameter `line1`, in both); a line 1 whose epoch
  does not parse and whose checksum is wrong throws the checksum's `InvalidOperationException` where it threw
  `FormatException` or `ArgumentOutOfRangeException`. A valid TLE is unchanged. Migration: catch `ArgumentException`
  and `InvalidOperationException`, as the constructor documents.
- **Breaking: `astro propagate -n` above 100 throws `ArgumentException`** ([#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681), Fixed above): such a degree ran at
  degree 10 about the Earth, as every `-n` did, and was ignored about any other centre, which has no geopotential; it
  now throws whatever the centre, before anything is loaded or written. Migration: pass a degree of at most 100, the
  degree of the embedded EGM2008 model (about another centre, omit `-n`); any `-n` other than 10 about the Earth now
  gives the run of that degree, from a TLE (`-t`) too.
- **Breaking: the text of `CelestialItem.ToString`, `CelestialBody.ToString` and `Spacecraft.ToString`** ([#681](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/681), Fixed
  above), and so of `astro celestial-body-info`: the mass, GM and radii are printed in round-trip form (Earth with
  gm_de440: `5.972168399787583E+24`, `398600435507022.6`, `6378136.6`, `6356751.9`), the GM line under
  `GM (m^3/s^2) :`. Migration: a caller that parsed the text reads the properties `Mass`, `GM`, `EquatorialRadius` and
  `PolarRadius` instead.

- **Breaking: the hard-body radius of the secondary is its own** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683), Fixed above).
  `ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters`, `ScreeningOptions.DefaultSecondaryHardBodyRadiusMeters`
  and `AvoidanceSearchOptions.SecondaryHardBodyRadiusMeters` are `double?`, null by default (5.0 before). The radius of
  the secondary is the `HardBodyRadius` of a secondary spacecraft when it is above 0, the option when the secondary
  gives none (a radius of 0, or not a spacecraft), 5 m when neither does; an option of `Analyze`, `AnalyzeAll` or
  `EvaluateAvoidance` that differs from the radius of the secondary throws `ArgumentException` naming the secondary and
  the option, while `ScreeningOptions.DefaultSecondaryHardBodyRadiusMeters` is a fallback: a candidate that gives a
  radius keeps it whatever the option, so `Screen` never throws for a candidate's radius and screens a catalogue of
  spacecraft with different radii (decided by S. Guillet, 2026-11-14), each analysis and the re-analysis of its avoidance
  options carrying the candidate's resolved radius; an option that is NaN, infinite or negative throws
  `ArgumentOutOfRangeException`.
  Both `Spacecraft` constructors throw `ArgumentOutOfRangeException` for a `hardBodyRadius` that is not finite or is
  negative, the kernel-backed one included. The combined radius and the Pc move for every secondary spacecraft with a
  radius under the default options (p + 5 m to p + s): `Screen`'s ranking, `EvaluateAvoidance`'s Pc and ranking, and
  the CDM's `COLLISION_PROBABILITY` and `AREA_PC` with them; secondaries of 0 m and other objects are bit for bit.
  Migration: read the properties as `double?`; drop an option of `Analyze`, `AnalyzeAll` or `EvaluateAvoidance` that
  restated or overrode the radius of the secondary spacecraft (or give the same value), and set `HardBodyRadius` on the
  secondary instead (a screening fallback that overrode a candidate's radius now leaves it its own); code compiled against
  10.2.0-preview-1 must be recompiled.
- **Breaking: the avoidance options inherit the settings of the encounter** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683), Fixed above).
  `AvoidanceSearchOptions.AnalysisSampleStep` and `MaximumEventSearchStep` are `TimeSpan?`,
  `LowRelativeSpeedThresholdMetersPerSecond` and `SecondaryHardBodyRadiusMeters` `double?`, null by default for the
  value the encounter was analyzed with (1 min, 60 s, 1.0 and 5.0 before). The stale threshold, the bisection tolerance
  and the non-linearity threshold are those of the encounter (their defaults before). An encounter analyzed with the
  default options gives the same options as before, bit for bit, apart from the radius; one of `Screen` carries its 5
  min sample step, whose search step on the two-body orbit is then 18.75 s instead of 3.75 s (no option of the two
  measured pairs moved). Migration: read the four properties as nullable; give a value to override the encounter's;
  recompile.
- **Breaking: `Analyze` and `AnalyzeAll` refuse the protected spacecraft as secondary and a trajectory of another
  spacecraft** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683), Fixed above): `ArgumentException` naming `secondaryObject` for a secondary with the protected NAIF
  identifier, and naming `protectedTrajectory` or `secondaryTrajectory` for a `PropagationSolution` propagated for
  another instance, even one with the same NAIF identifier. Only calls whose result was meaningless throw. Migration:
  leave the protected spacecraft out of the secondaries, as `Screen` does; pass each spacecraft with its own solution,
  the instance that was propagated.
- **The TCA of the search on two propagations is labelled in the time frame of the window start** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683), Fixed above):
  on a non-TDB window it was mostly TDB; it moves by a tick at most, toward the zero of the range rate, and the burn
  epochs of its avoidance options take that frame too. A TDB window, the sampled search and `ToCdm` (UTC already) are
  unchanged. `Time` equality compares frames, so a comparison of such a TCA with `==` or `Equals` against a TDB `Time`
  changes outcome; compare with `<`, `>` or `CompareTo`, which compare instants, or convert first.
- **The CDM writes `MANEUVERABLE` `N/A` for a secondary spacecraft without an engine** ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683), Fixed above): `YES`
  before. A secondary with an engine, and a value given by `SecondaryParticipantMetadata`, are unchanged. Migration: set
  `SecondaryParticipantMetadata.Maneuverable` when the capacity of the object is known.

- **Breaking: `MemoryDataProvider` throws `InvalidOperationException` for missing data and `ArgumentException` for an
  unknown identifier** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680), Fixed above):
  `GetEphemeris` of a target without stored states or outside them and `FrameTransformationToICRF` outside a frame's
  stored orientations threw `ArgumentException` and now throw `InvalidOperationException`. `GetCelestialBodyInfo` of an
  identifier without information threw `KeyNotFoundException`, and so did `new CelestialBody(id)` and
  `CelestialItem.Create(id)` of an identifier the provider lacks; they now throw `ArgumentException` whose `ParamName` is
  `naifId`, as `SpiceDataProvider` for an identifier SPICE does not name (`CelestialItem.Create` of a Lagrange point,
  which reads its state only, throws `InvalidOperationException` for a missing state, as on SPICE). A frame never added still throws
  `ArgumentException`. A body built on the provider without its barycentre's data or information now builds without an
  orbit instead of throwing. A variational replay of the conjunction analysis over a provider short of data surfaces the
  provider's `InvalidOperationException` unwrapped. Migration: catch `InvalidOperationException` for missing states and
  orientations, and `ArgumentException` (the same handler on both providers) for an identifier without information, or
  add its information with `AddCelestialBodyInfo` first.
- **Breaking: `ToEquatorial()` and `new Equatorial(StateVector)` give the right ascension and declination in ICRF**
  ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680), Fixed above): for a state in another frame the angles change (up to 166.4° in RA for the Moon in MOON_ME,
  24.7° to 123.1° in ITRF93, 0.87° to 4.13° in ECLIPJ2000, up to 0.32° for a TLE in TEME, the Dec by up to 3.65°); the
  distance and the epoch do not, and a state in ICRF gives the same numbers, bit for bit. `ToCartesian()` and
  `ToDirection()` of such an `Equatorial`, built from its angles, now give ICRF vectors, no longer vectors in the state's
  frame. The frame's orientation is now
  read, so a state in a frame the data do not cover at its epoch (ITRF93 outside the PCK) throws
  `InvalidOperationException`. Migration: for the angles in the state's own frame, take atan2 and asin of
  `stateVector.Position`, or search with `CoordinateSystem.RaDec` in that frame.
- **Breaking: `Time.AddDays`, `AddHours`, `AddMinutes` and `CreateFromJD` throw for a non-finite argument** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680),
  Fixed above): NaN returned the epoch or J2000 and now throws `ArgumentOutOfRangeException`; the infinities, which threw
  with the parameter name `value`, name the caller's parameter. The command line rejects an epoch `NaN JD`. Migration:
  check `double.IsFinite` before the call.
- **Breaking: `Time.Create(year, doy, sec)` checks its ranges** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680), Fixed above): year 0 (read as 2000), a year
  above 9999, a day outside its year and seconds outside [0, 86400) throw `ArgumentOutOfRangeException`; a non-finite
  `sec` names `sec` (it named `seconds`), as do seconds whose nearest tick is past 9999-12-31T23:59:59.9999999 (they
  named `value`). Migration: normalise the day and the seconds before the call, or add the
  excess with `AddSeconds`.
- **`Matrix` equality follows `double.Equals`** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680), Fixed above): two matrices, or two records holding one, are no
  longer equal when one has NaN where the other has a number, or when two elements differ by 4.9e-324; NaN against NaN
  stays equal. No call throws and no numeric result moves.
- **Breaking: `Vector3.Normalize()` and `Quaternion.Normalize()` throw for a magnitude of 0** ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680), Fixed above):
  they returned the value itself. Only the zero vector and the zero quaternion throw for their magnitude: a finite
  non-zero one whose components are all tiny normalizes since the third round of #668 (Changed below), which also made
  a component that is not finite throw. `new Spacecraft(...)` throws `ArgumentException` naming `bodyFront`,
  `bodyRight` or `bodyUp` for an axis of magnitude 0 (accepted when the other given axes were zero too, thrown without a
  parameter name otherwise). Migration: test `Magnitude() > 0` before normalizing; give non-zero axes.
- **Breaking: the TRIAD factories point the spacecraft's instance axes** ([#677](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/677), Fixed above): for a spacecraft built
  with `bodyFront`, `bodyRight` or `bodyUp` other than the defaults, the attitude of `TriadAttitude.CreateLVLH` and
  `CreateProgradeWithSunTracking` changes (90° for the axes of the issue, up to 180° in general), and with it its
  attitude record, `GetStateOrientationToICRF`, the CK `WriteOrientation` writes, its instruments' pointing and the
  managed field-of-view searches; `PrimaryBodyVector` and `SecondaryBodyVector` of a factory attitude follow the
  spacecraft of the engine's tank. A spacecraft with the default axes is bit for bit, and so are the `PrimaryBodyVector`
  and `SecondaryBodyVector` of an engine whose tank is on no spacecraft, which no propagation points ([#676](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/676)); the forces read no attitude, so the trajectory does not move. Migration, for the former attitude: the
  constructor with the static axes, `new TriadAttitude(center, epoch, hold, Spacecraft.Down, OrbitalDirectionTarget.Nadir,
  Spacecraft.Front, OrbitalDirectionTarget.Prograde, engine)`, or `Spacecraft.Front`, `OrbitalDirectionTarget.Prograde`,
  `Spacecraft.Up`, `new CelestialAttitudeTarget(sun)` for prograde with Sun tracking.
- **Breaking: drag about an Earth without an atmospheric model is NRLMSISE-00 at nominal space weather** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674),
  Fixed above). A propagation about `PlanetsAndMoons.EARTH_BODY`, or any Earth built without a model, with
  `includeAtmosphericDrag: true` now has drag: over one day on a 6800 km orbit with A = 1 m², m = 100 kg and Cd 2.2,
  4.627 km in RK7(8) at 1e-11 and 4.265 km in Velocity-Verlet at 60 s; Ψ gains its Cd column, which moves the
  covariance, and the CDM names `NRLMSISE-00`. The get-started RK7(8) example, about `EARTH_BODY` with drag, now has
  drag. So does a listed Earth without a model where the central body is not that Earth (decided by S. Guillet,
  2026-11-14): in SSB mode (a star or barycentre observer), where the run ran without drag, and beside another central
  body with a model, whose drag it ran alone. On a 6700 km orbit with A = 10 m², m = 100 kg and Cd 2.2, 51.4 m over
  20 min in Velocity-Verlet at 10 s and 51.25 m in RK7(8) at 1e-10, in SSB mode as about a Moon given a model; 0.64 m
  over 600 s for the VV-SSB variant of `GcrfPropagationTests` (6778 km, 1000 kg). A run with a configured model, or
  without drag, is unchanged, bit for bit. Migration: pass `includeAtmosphericDrag: false` for the former result, or
  give the Earth the model you want.
- **Breaking: drag about another central body without an atmospheric model throws** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674), Fixed above): a central body
  other than the Earth and the Sun without a model throws `InvalidOperationException` naming it, before anything changes,
  whatever the listed bodies (a Moon-centred run with an atmospheric Earth listed applied that Earth's drag alone; Mars
  without a model; `astro propagate` about the Moon with `-a`). About the Sun (a star or barycentre observer, SSB mode),
  drag comes from the listed bodies with an atmosphere, an Earth always having one (above), and the run throws when none
  has one, the Moon alone without a model for instance (it ran without drag). Migration: give the body an
  `IAtmosphericModel`, or pass `includeAtmosphericDrag: false`.
- **Breaking: the central body takes part on every path** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674), Fixed above): the constructors of
  `CentralBodyPropagator`, `Spacecraft.Propagate` and `PropagateAsync`, `Scenario.SimulateAsync` and the batch insert the
  central body first when no listed body has its NAIF identifier, as the builder did: its atmosphere gives drag (4.760 km
  in RK7(8) and 4.500 km in Velocity-Verlet over a day with US 1976 on the orbit above) and its shadow scales SRP
  (1.871 m and 1.241 m). A run without drag and SRP is unchanged (0.000 m, from an empty list too), and so is a run that
  lists its central body. Migration: none for the corrected result; the former one has no equivalent.
- **Breaking: bodies of one NAIF identifier must have the same models** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674), Fixed above): a listed body of the
  observer's identifier with another GM, radii, gravitational field or atmospheric model, or two listed bodies of one
  identifier that differ, throw `ArgumentException` naming them, on every path, the builder included; so does
  `Scenario.AddCelestialItem` of a body whose identifier the scenario holds with other models (it was dropped). Examples:
  an EGM2008 or atmospheric Earth listed while the orbit is on `EARTH_BODY`; `astro propagate` given its centre again in
  its celestial bodies. Models are compared by value (NRLMSISE-00 by its switches and space weather, a custom
  `IAtmosphericModel` by `Equals`); a value-equal twin of the observer runs with the observer's instance, which moves a
  run that listed one with a configured NRLMSISE-00 by 6e-5 m over an hour in RK7(8). Migration: list the observer's
  instance, or build the orbit on the listed body.
- **Breaking: `CelestialBody.GetAtmosphere` and the context getters read the body's model** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674), Fixed above): with a
  full context an Earth with a configured model returns that model (US 1976: 2.80e-12 kg/m³ at 400 km instead of
  5.24e-12), and on a model-less Earth `GetAirDensity`, `GetAirPressure` and `GetAirTemperature` return NRLMSISE-00 at
  nominal space weather where they returned 0, 0 and NaN. On a model-less Earth, a context that holds the altitude
  without the geodetic position or the epoch, and the altitude overloads (`GetAirDensity(double)`,
  `GetAirPressure(double)`, `GetAirTemperature(double)`, `GetAtmosphere(double)`), return the U.S. Standard Atmosphere
  1976 (`EarthStandardAtmosphere`, −5 to 1000 km and continued above) where they returned 0, 0 and NaN: 1.225 kg/m³,
  101.325 kPa and 15 °C at sea level, 2.80e-12 kg/m³ at 400 km (decided by S. Guillet, 2026-11-14). It is not the
  NRLMSISE-00 of the drag, which always has the full context, so no propagation moves. A null context, which holds no
  altitude, and another body without a model, still give NaN, 0 and 0 (`GetAtmosphere` of a model-less Earth threw
  `NullReferenceException` on a null context). `HasAtmosphericModel` still says whether a model was given. Migration:
  to keep "no atmosphere" for an altitude-only read, test `HasAtmosphericModel` before reading; for the drag's
  atmosphere, pass the geodetic position and the epoch (`AtmosphericContext.FromPlanetodetic`).
- **Breaking: a force-building path rejects an integrator that holds forces or was already used** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674), Fixed above):
  `Spacecraft.Propagate` with an integrator, the constructors of `CentralBodyPropagator` with celestial bodies, the
  builder (a second `Build()` included) and the batch, Monte Carlo and scenario factories throw `ArgumentException`
  (`integrator`) for such an instance. In `BatchPropagator` and `MonteCarloPropagator` the exception is the failure of
  its task: with a factory returning one instance, one run succeeds and the others fail. A construction rejected after
  the integrator was taken (an invalid covariance) leaves it taken. The constructor taking an `IIntegrator` with the
  forces the caller added is unchanged. Migration: pass a new integrator to each propagation; for forces of your own,
  add them all and use that constructor.
- **Breaking: a propagation step of zero or less throws** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674), Fixed above): `ArgumentOutOfRangeException`, named
  `propagatorStepSize` by `Spacecraft.Propagate`, `PropagateAsync` (before its task starts) and
  `Scenario.SimulateAsync` (before any star is propagated), `deltaT` by the constructors of `CentralBodyPropagator`,
  `PropagatorBase` and `VVIntegrator` and by `CentralBodyPropagatorBuilder`, and `DeltaT` by
  `MonteCarloPropagator.Propagate` (which returned every run failed). Migration: pass a positive step.
- **`new AtmosphericDrag(spacecraft, earth)` accepts an Earth without an atmospheric model** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)): it threw
  `ArgumentException` and now applies NRLMSISE-00 at nominal space weather; another body without a model still throws.
- **Breaking: the centre of an OPM is its `CENTER_NAME`** ([#679](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/679), Fixed above). Without an observer, `Opm.ToStateVector()`
  and `ToSpacecraft()` on an OPM whose `CENTER_NAME` is not a barycenter, the Sun, a planet or a moon the library
  identifies (a ground station, a site or a spacecraft, a body that only a kernel names, a typo) throw
  `ArgumentException` (`ParamName` `observer`) where they used the Earth; an observer whose NAIF id contradicts a known
  `CENTER_NAME` (the Earth passed for a MOON OPM, a site passed for an EARTH OPM) throws `ArgumentException` naming both;
  a non-Earth OPM read without an observer is about its centre, so every quantity derived from the observer moves.
  Migration: pass the observer for a site, a spacecraft or a kernel-defined centre; pass the centre the message names,
  or correct `CENTER_NAME`.
- **Breaking: `Omm.ToTle` throws for an OMM that is not SGP4, TEME and Earth** ([#679](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/679), Fixed above):
  `InvalidOperationException` naming each keyword at fault and its value (`CENTER_NAME 'MOON' is not EARTH; REF_FRAME
  'ICRF' is not TEME; ...`) where it returned a TLE of another orbit; `IsTleCompatible` is false and `ToString` has no
  ` (TLE)` for those OMMs. An OMM without TLE parameters throws its former message. The keywords are read trimmed and
  in any letter case (` TEME `, `sgp4`), as before. EARTH/TEME/SGP4 OMMs (`iss.omm`, `Omm.CreateForTle`, `TLE.ToOmm`,
  the conformance runner's) give the same TLE, bit for bit. Migration: correct the
  metadata if the elements really are SGP4 elements of an Earth orbit in TEME; otherwise keep the object in OMM form.
- **Breaking: a document holding several messages of a type throws** ([#679](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/679), Fixed above): `Omm`/`Opm.LoadFromFile`,
  `LoadFromString` and `LoadFromStream`, `Cdm.ReadFromFile`, `ReadFromStream` and `ReadFromString`, and
  `OmmReader`/`OpmReader`/`CdmReader.ReadFrom*` throw `OmmParseException`, `OpmParseException` or `CdmParseException`
  where they returned the first message, a second message at any depth below the root counted;
  `Omm`/`Opm.TryLoadFromFile` return false with a `PARSE_ERROR`; `OmmValidator`/`OpmValidator.ValidateFile` report the
  file invalid. A document with one message of the type, alone or beside messages of other types, loads as before. Migration: split the NDM into one `<omm>`, `<opm>` or `<cdm>` per
  document.
- **Breaking: the CDM writes `INTRACK_THRUST` only for a covariance the library propagated** ([#684](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/684), Fixed above):
  the text of an exported CDM changes. For a participant whose covariance the library did not propagate the keyword is
  omitted, and `Metadata.IntrackThrust` of the result of `ToCdm` is null, where a spacecraft with an engine got `YES`
  and one without `NO`; for a propagated covariance it is `NO`, even with engines. Migration: set
  `CdmExportOptions.ProtectedParticipantMetadata.IntrackThrust` or `SecondaryParticipantMetadata.IntrackThrust` to
  state the value of an OD.
- **Breaking: the CDM writes `SOLAR_RAD_PRESSURE` only for a covariance the library propagated** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above): the text of an
  exported CDM changes. For a participant whose covariance the library did not propagate the keyword is omitted, and
  `Metadata.SolarRadPressure` of the result of `ToCdm` is null, where a spacecraft got `YES` (`NO` for a
  `SolarRadiationCoeff` of 0); an export with a propagated covariance is unchanged, bit for bit. Migration: set
  `CdmExportOptions.ProtectedParticipantMetadata.SolarRadPressure` or `SecondaryParticipantMetadata.SolarRadPressure` to
  state the value of an OD.
- **Breaking: a message with several segments, or a repeated structural element, throws** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above): an OMM or OPM
  with more than one header, body, segment, metadata or data section, or block of a data section (the OPM maneuvers
  excepted), makes `OmmReader`/`OpmReader.ReadFrom*` and `Omm`/`Opm.LoadFrom*` throw `OmmParseException` or
  `OpmParseException` without schema validation (with it, since the third round, the schema rejects the document first,
  `OmmValidationException` or `OpmValidationException`); `TryLoadFromFile` returns false with a `PARSE_ERROR` (the
  schema error with schema validation) and `ValidateFile` reports the file invalid, where the first element was read. A CDM with such a repeated element, or a
  body without two segments, makes `CdmReader.ReadFrom*` and `Cdm.ReadFrom*` throw `CdmParseException`, where the first
  element was read and a segment count other than two threw `ArgumentException`. No schema-valid document moves.
  Migration: split such a document into one message per segment, and catch `CdmParseException` for a CDM of the wrong
  segment count.
- **Breaking: the OMM, OPM and CDM schema validation checks the standard forms** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
  [#614](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/614), Fixed above): verdicts only, no
  value read changes. A document whose root has no namespace that the NDM/XML 4.0.0 schema rejects, unqualified or
  qualified below its root, or whose root is neither `ndm` nor the message's, is now invalid, where it was reported
  valid with a warning per element: `OmmValidator`/`OpmValidator.ValidateSchema` and `Cdm.ValidateSchema*` report the
  error, `ValidateFile` reports the file invalid, `Omm`/`Opm.LoadFrom*` with the default `validateSchema: true` throw
  `OmmValidationException` or `OpmValidationException` (where they loaded, or threw a parse exception or an
  `ArgumentOutOfRangeException`), and `TryLoadFromFile` returns false with the schema error. An `ndm` that holds no
  message of the validator's type (empty, or of other messages only) is invalid too, without a namespace or, for the CDM,
  in it: `Omm`/`Opm.LoadFrom*` throw the validation exception where they threw `OmmParseException` or `OpmParseException`
  ("Could not find ... element in document"). A valid document,
  Space-Track's and the writers' output included, stays valid, now without warnings. A root in the NDM/XML namespace
  but a CDM `ndm` without a CDM, and every read with `validateSchema: false`, are unchanged, bit for bit by this entry
  (a keyword out of its range now throws the reader's parse exception on every read, the fourth round's entry below).
  Migration: fix the document, or pass `validateSchema: false` to read it without the schema step; catch the validation
  exception for an NDM without the message.
- **Breaking: the managed threshold searches return the windows, gaps and crossings shorter than the step**
  ([#678](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/678), Fixed above). Under the same signatures, the searches the library
  runs (`FindWindowsOnDistanceConstraint`, `FindWindowsOnCoordinateConstraint` and `FindWindowsOnIlluminationConstraint`
  of `CelestialItem` and `Site` when CSPICE cannot search, `Site.FindDayWindows` of a site built from its coordinates and
  `Launch.FindLaunchWindows` through it, `GeometryFinder.FindWindowsWithCondition` of a `double` quantity) return the
  windows, gaps and crossings that fell between two samples: windows are added, a window across a hidden gap is split,
  and `NotEqual` is split at the crossings added. No call throws and no edge found before moves; the quantity is
  evaluated about 1.2 times as often at a step where nothing is hidden. Migration: none to find every window; a caller
  who relied on a coarse step to pass over short windows filters the result by length.
- **Breaking: `new Quaternion(axis, angle)` normalises its axis and throws for a zero or non-finite one** ([#688](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/688), Fixed
  above): a finite non-zero axis of any length gives the unit quaternion of the rotation by `angle` about its direction,
  where a non-unit axis gave a non-unit quaternion turning by another angle; a zero axis, or one with a NaN or infinite
  component, throws `ArgumentException` (parameter `axis`) where it returned a quaternion that was no rotation. A unit
  axis (|axis| = 1 exactly) gives the same quaternion bit for bit; an axis that is unit only to rounding (`v.Normalize()`)
  can move it by an ulp. Nothing the library computes moves. Migration: pass any non-zero direction; check the magnitude
  of a vector that can be zero (a zero angular velocity) before passing it, and use `Quaternion.Identity` for no
  rotation.
- **Breaking: `Matrix.FromQuaternion`, `Matrix.TransformCovariance`, `Quaternion.Lerp`, `Quaternion.SLERP`,
  `Vector3.Angle(vector, plane)` and `Vector3.To` throw for a zero or non-finite input** (the known limitation of [#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680), Fixed above).
  `FromQuaternion` and `TransformCovariance` throw `ArgumentException` (`q`, `rotation`) where they used the identity
  (or NaN); `Lerp` throws `InvalidOperationException` for a zero or non-finite instance and `ArgumentException` (`q2`)
  for a zero or non-finite `q2` and when the chord passes through zero (`Lerp(q, −q, 0.5)`), where it returned the zero
  quaternion; `SLERP` normalizes its inputs and throws the same way (`q`), so non-unit inputs give the SLERP of their
  rotations, and inputs unit only to rounding can move its result by about 1e-16; `Angle(vector, plane)` and
  `Angle(vector, Plane)` throw `ArgumentException` (`plane`) for a zero or non-finite normal, where they returned 0 or
  π; `To` throws `InvalidOperationException` for a zero or non-finite instance and `ArgumentException` (`vector`) for a
  zero or non-finite target, where it returned the identity or NaN, and gives the rotation of the directions at any
  finite length and a half turn for vectors collinear and opposite in doubles, where it returned NaN, the zero quaternion
  or the identity. Nothing else the library computes moves: a single-vector attitude or the attitude of an impulse whose
  body front is collinear and opposite to its target (or delta-V) in doubles is now that half turn, where it recorded the
  zero quaternion or the identity; a zero target or delta-V keeps the identity.
  Migration: pass non-zero quaternions, normals and vectors; interpolate with `Lerp` between quaternions of the same
  hemisphere (flip `q2` when q · q2 < 0), or use `SLERP`, which takes the shorter path.
- **Breaking: the direction of `PerigeeVector()` and `ApogeeVector()` at and near e = 0** ([#686](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/686), Fixed above). For
  `KeplerianElements`, `EquinoctialElements` and `TLE` of eccentricity exactly 0, they move from ±x̂ a to the perifocal
  axis in the plane of the orbit, at ω from the node (Ω, ω and i of the elements; an `EquinoctialElements` has ω = 0);
  for a `StateVector` whose eccentricity vector is exactly zero, from x̂ a to its unit node vector times a (x̂ for an
  equatorial orbit, unchanged). Near-circular elements and states no longer give a zero vector, an infinite apsis
  velocity or a target of 0 for `PerigeeHeightManeuver` and `ApogeeHeightManeuver`. Elsewhere the direction is the
  elements' periapsis rather than the recomputed eccentricity vector, which moves the vector by 8.1e-16 at e = 0.95, 1.0e-14 at 0.1, 8.5e-13 at 1e-3, 9.6e-10 at 1e-6 and 8.3e-7 at 1e-9 relative for
  elements and 3.4e-16, 2.0e-15, 2.3e-13, 2.7e-10 and 2.4e-7 (the error of the former direction, ε / e) for states; the magnitudes move by an ulp at most, and so do the targets of the apsis
  maneuvers, their impulses, `PhasingManeuver` and `CombinedManeuver`. Mean elements have no position: mean
  `KeplerianElements` and `EquinoctialElements` throw `InvalidOperationException` from `PerigeeVector`, `ApogeeVector`,
  `PerigeeVelocity` and `ApogeeVelocity` at every eccentricity, as they did at e ≠ 0 through `ToStateVector()` and as
  `EccentricityVector()` does, and now also at e = 0, where they returned ±x̂ a; so `PerigeeHeightManeuver`,
  `ApogeeHeightManeuver` and `Launch` keep rejecting a mean target orbit. A `TLE`, whose semi-major axis, eccentricity
  and angles are those of its osculating elements at its epoch (SGP4/SDP4), takes the perifocal axis of those elements.
  Migration: read `PerigeeRadius()` for the magnitude alone; the direction is now the periapsis of the elements;
  convert mean elements with their propagator first (`TLE.ToOsculating()`).
- **Breaking: a rectilinear state throws where it has no RTN frame or orbit plane** ([#687](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/687), Fixed above): on a
  state with r × v = 0 exactly (a radial motion, a zero velocity, the centre) or a non-finite state, every member that
  reads the orbit plane or the RTN frame throws `InvalidOperationException`: `CreateRtnRotation`, `ToRtn`, `FromRtn`,
  `RotateCovarianceToRtn` and `RotateCovarianceFromRtn` (a matrix with zero rows before); `Inclination`,
  `AscendingNode`, `ArgumentOfPeriapsis`, `AscendingNodeVector`, `DescendingNodeVector`, `TrueLongitude()`,
  `TrueLongitude(Time)` and `MeanLongitude` (0, 0, π, x̂, −x̂, 0 and a number, or NaN); `ToKeplerianElements()`,
  `ToKeplerianElements(Time)`, `ToEquinoctial`, `AtEpoch`, `TrueAnomaly(Time)` and `ToStateVector(Time)` at another
  instant, the two-body ephemeris of a spacecraft that is not propagated (equatorial elements, NaN or a NaN state); at the
  centre, whose eccentricity vector is zero, also `TrueAnomaly()`, `MeanAnomaly()`, `EccentricAnomaly()`,
  `EpochAtMeanAnomaly`, `ToStateVector(double)`, `PerigeeVector`, `ApogeeVector`, `PerigeeVelocity` and
  `ApogeeVelocity` (0, the epoch, the state itself, the zero vector or NaN); and `MeanAnomaly()` of a non-finite state,
  which threw the message of an empty `Nullable`. A moving state off the centre keeps `Eccentricity()` = 1 and
  `TrueAnomaly()` = π, and every member that reads no plane is unchanged. `ConjunctionAssessment.Analyze`, `AnalyzeAll`,
  `EvaluateAvoidance`, `EncounterCovariance` and the CDM export throw through them for such a participant state at TCA
  or at its burn. Every state with r × v ≠ 0 is unchanged, bit for bit. Migration: give the state a non-zero angular
  momentum, or handle the radial case separately (its direction is r̂; it has no plane).

- **Breaking: a target orbit in another frame than the propagation's throws on every spacecraft**
  ([#685](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/685), Fixed above). A propagation whose impulse target orbit is not in the frame the spacecraft is
  propagated in throws `ArgumentException` (parameter `spacecraft`) when the propagator is built, on every path
  (`Spacecraft.Propagate` and `PropagateAsync`, `Scenario.SimulateAsync`, `BatchPropagator`, `MonteCarloPropagator`,
  the constructors, the builder and the avoidance propagations), and `InvalidOperationException` from
  `CentralBodyPropagator.Propagate()` for one attached to the spacecraft after the propagator was built, where only a
  TLE or TEME start was checked when the propagator was built and nothing when it propagated; other
  spacecraft burned with the target's components read in the integration frame (10.7 times the closed form in the case
  measured). The results of valid inputs are bit for bit. A target in GCRF on a spacecraft propagated in ICRF (a TLE or
  TEME start included), or in ICRF on one propagated in GCRF, is accepted as in the frame of the propagation, whose axes
  it shares (third review), where 10.x rejected it in the constructor. Migration: give the target in the spacecraft's
  propagation frame, `targetOrbit.ToFrame(spacecraft.InitialOrbitalParameters.Frame)`, or ICRF (or GCRF) for a TLE or
  TEME start.
- **Breaking: the CLI follows the library's rule of a site identifier** (cli-site-rule of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)'s second round, Fixed above). CLI only. An identifier from
  100001 to 999998 that is not a site by the library's rule (x000, a body of at most 100, a Lagrange point) is read as a
  celestial item: the error of `SpiceAPI.ReadEphemeris` instead of `ArgumentOutOfRangeException` or another message,
  and `sub-point` no longer calls it a site. A site is named by its SPICE name (DSS-13), not `site<id>`, and a site
  that no loaded kernel names fails as `SpiceAPI.ReadEphemeris` fails, `ArgumentException` "has no name in SPICE", where
  it was read as `site<id>`: the SPK of a site the library writes (`Site.WriteEphemeris`) names nothing, its name being
  in the frame kernel of `Site.WriteFrameAsync`. The printed states of a named site do not move. Migration: load a
  kernel that names the site (`NAIF_BODY_NAME`/`NAIF_BODY_CODE`, as the frame kernel of `Site.WriteFrameAsync` does)
  with its SPK; a script that matched the name `site<id>` or the former exceptions takes the SPICE name and the
  library's exceptions.
- **Breaking: a site from the kernels takes the frame of its kernels** ([#690](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/690), Fixed above). A
  site from the kernels whose frame id (1000000 + NaifId) the loaded kernels define uses that kernel frame on every
  path: its managed azimuths and elevations (`GetHorizontalCoordinates`), `ToFrame(site.Frame)` and the managed
  coordinate searches move by up to 7.8e-8 rad for DSS-13 (azimuth 117.89632135112616° to 117.89631808999324° and
  elevation 16.790619489720385° to 16.790616801126596° for the Moon at 2000-01-01 12:00 TDB; 1.1e-7 rad in azimuth at an
  elevation of -63°), 1.07e-5 s on a LEO elevation edge, 1.15e-3 s on a rise or set of the Moon and 1.07e-3 s on its
  elevation extremes. The reads by name and the CSPICE searches do not move. A kernel site named otherwise than its
  kernel frame now reads that frame where it threw SPICE(UNKNOWNFRAME). `Site.Planetodetic` and the illumination angles
  do not move, nor does a site built from coordinates, nor a kernel site whose frame no loaded kernel defines (the frame
  of its J2000 coordinates, in which an ephemeris is now read in J2000 and rotated where it threw SPICE(UNKNOWNFRAME),
  third review). `Site.WriteFrameAsync` (and so `CosmographiaExporter`)
  writes for such a site the kernel frame, under its kernel name and id, as `TKFRAME_<id>_MATRIX` relative to the frame
  of the body, where it wrote the frame of the J2000 coordinates as angles under the kernel's id (7.76e-8 rad from
  DSS-13_TOPO) and, for a site named otherwise, renamed the frame of that id; a kernel frame not fixed in the frame of
  the body throws `InvalidOperationException`, nothing written. Migration: to keep the topocentric frame of the J2000
  coordinates, build the site from them, `new Site(id, name, body, kernelSite.Planetodetic)`; its frame kernel is that
  of the former writer, under the name `<NAME>_TOPO`.
- **Breaking: a coordinate search in a frame whose reads are the library's is the library's** (third review of
  [#690](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/690), Fixed above).
  `CelestialItem.FindWindowsOnCoordinateConstraint` and `Site.FindWindowsOnCoordinateConstraint` search with the library
  in a frame whose reads are the library's (`Frame.TEME` or a frame named TEME, `Frame.CIRS`, `Frame.TIRS`, the frame of
  a site built from coordinates, of a kernel site whose frame id no loaded kernel defines, of a managed spacecraft with
  an attitude record), also when a kernel spacecraft takes part: where these searches returned no window they return
  the windows of the reads in that frame, and the frame of a site built from coordinates under a name a loaded kernel
  defines (`new Site(13, "DSS-13", earth, coordinates)` and `DSS-13_TOPO`), which `gfposc_c` searched in that kernel
  frame, is now searched in the frame of the coordinates, as it is read since #682 (7.8e-8 rad apart for DSS-13).
  `SpiceAPI.FindWindowsOnCoordinateConstraint` throws `ArgumentException` (`frame`) for a frame of the library SPICE does
  not know by name, where it returned no window. Migration: to search in a kernel frame, pass that frame
  (`new Frame("DSS-13_TOPO")`, or the frame of the kernel site, `new Site(13, "DSS-13", earth).Frame`); to search in a
  library frame with CSPICE, there is no way: use the `CelestialItem` or `Site` searches.

- **Breaking: a geopotential degree above its model file, or a file missing a harmonic, throws**
  ([#689](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/689), Fixed above). A degree of 2 or more
  above the highest degree of the model file throws `ArgumentOutOfRangeException` (parameter `maxDegrees`, the message
  naming the degree asked and the file's), from `new GeopotentialGravitationalField(file, degree)` and from the
  constructor of a `CelestialBody` given `GeopotentialModelParameters`; it ran silently at the file's degree. A file
  without a coefficient (n, m) with 2 ≤ n ≤ degree throws `ArgumentException` naming n and m; the term was zero. The
  check runs when the body or the field is built, not in the constructor of `GeopotentialModelParameters`, which reads
  nothing. Migration: pass a degree of at most 70 with `EGM2008_to70_TideFree` (`astro propagate -n`: at most 100, as
  before), or give a complete model file. No result moves for a degree the file covers.

- **Breaking: a step of zero or less of `Star.Propagate` and `Star.PropagateAsync` throws** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above):
  `ArgumentOutOfRangeException`, parameter `stepSize`, from the call itself for `PropagateAsync` (no task is created),
  where the call never returned and a negative step exhausted the memory. Migration: pass a positive step. No result
  moves for a positive step.

- **Breaking: `Star.IsPropagated` becomes true once the star is propagated** (second round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above), after `Propagate`,
  `PropagateAsync` or `Scenario.SimulateAsync`; it was always false. `PropagationOutput` stays null, marked `[Obsolete]`
  (see **Deprecated**). Migration: code that relied on false, or checked `PropagationOutput`, reads `IsPropagated`.

- **Breaking: the constructors of `CentralBodyPropagator` that take an integrator with its forces initialize it**
  (second round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above) with
  the initial state of the propagation. A run that threw `NullReferenceException` now propagates. An `Initialize` the
  caller made before construction with another observer or frame (the spacecraft's state about the solar system
  barycentre in SSB mode, for instance) is replaced by the propagator's; one made after construction still wins.
  Migration: drop the explicit `Initialize`, or keep it after construction with the propagator's initial state. No
  result moves for a caller who initialized with the central body and the frame of the propagation.

- **Breaking: the static objects of `Barycenters`, `PlanetsAndMoons` and `Stars` read their provider data at first use**
  (second round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above). With
  a provider lacking a static object's data, the first read of a member that needs them (GM, mass, orbit, radii, J2 to
  J4, frame, sphere of influence) throws the provider's own exception (`ArgumentException` naming `naifId` for missing
  information, `InvalidOperationException` for a missing state), at each read until the data exist, where the first read
  of the class threw a `TypeInitializationException` that never cleared. The provider is reached at that first read,
  not when the class is initialized: each read goes to the provider set at its time, and an object that has read its
  data keeps that provider for its data and its ephemeris, where before it kept the provider of the type initializer
  for both; the identifiers, names and predicates need no data, and `ExtendedInformation` is
  null until the data are read. On SPICE with the kernels loaded nothing moves. The `#623` deadlock needed a type
  initializer that read SPICE: none does now. Migration: catch `ArgumentException` or `InvalidOperationException`
  instead of `TypeInitializationException`; set the provider a static object should read before its first read of
  data, since a provider set afterwards does not reach it.
- **Breaking: `Vector3.Normalize()` and `Quaternion.Normalize()` throw for a component that is not finite** (third
  round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above):
  `InvalidOperationException`, where they returned NaN components ((NaN, 0, 1) gave (NaN, NaN, NaN), (+∞, 0, 0) gave
  (NaN, 0, 0)). A finite non-zero vector or quaternion of any size now normalizes, where a tiny one threw and a huge one
  gave zero, and `Magnitude()` gives its representable magnitude, where it gave 0 or +Infinity. Migration: check
  `double.IsFinite` on the components first, or catch `InvalidOperationException`.
- **Breaking: `Plane.GetAngle(Plane)` and `Plane.GetAngle(Vector3)` throw for a zero or non-finite normal or vector**
  (third round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above):
  `InvalidOperationException` when the normal of the plane itself is zero or has a component that is not finite
  (`default(Plane)` and `new Plane()` included), `ArgumentException` (`plane`, `vector`) for such an argument, where they
  returned NaN. The constructors still take any normal. Migration: give planes a finite non-zero normal, or catch.

- **Breaking: a static solar system object whose provider gives no GM or radii throws** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above). The first read of a
  member that needs the data of a static object of `Barycenters`, `PlanetsAndMoons` or `Stars` throws
  `InvalidOperationException`, naming the missing variable (`BODYnnn_GM`, `BODY1_GM` to `BODY10_GM` for the solar system
  barycentre, `BODYnnn_RADII` for `EARTH_BODY`, `MOON_BODY` and `SUN_BODY`), when its provider gives a GM or radii of 0
  (SPICE without the kernels that define them, a `MemoryDataProvider` record holding zeros), and keeps nothing, where it
  returned the zeros, J2 NaN and no orbit and kept them for the process. On SPICE with the kernels loaded nothing moves.
  Migration: load the kernels before the first read of a static object's data, or give a `MemoryDataProvider` record its
  GM and radii; `new CelestialBody(id)` still reads whatever the kernels give.

- **Breaking: on SPICE the static solar system objects follow the kernel pool** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above). After a
  `LoadKernels`, `UnloadKernels` or `ClearKernels`, the next read of a member of a static object of `Barycenters`,
  `PlanetsAndMoons` or `Stars` read on the `SpiceDataProvider` reads its data again: a kernel loaded later that defines
  its J2 (`geophysical.ker`), its frame (`earth_assoc_itrf93.tf`, `moon_assoc_me.tf`), its orbit (an SPK) or another GM
  changes it, where the data of the first read were kept for the process, and after the kernels that define its GM or
  radii are unloaded (`ClearKernels`) a read throws `InvalidOperationException` naming the variable until they are loaded
  again, where the data of the first read were returned. Data equal to those held keep the instances held, so that with
  kernels that redefine nothing of `Data/SolarSystem` nothing moves, bit for bit. Migration: to keep a body's data across
  changes of the pool, build it (`new CelestialBody(id)`), which reads the kernels once.

- **Breaking: `TriadAttitude` throws for a separation outside [1e-14, π/2] rad and for collinear or non-finite vectors
  at every separation allowed** (fourth round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above). The four public constructors throw
  `ArgumentOutOfRangeException` (`minimumVectorSeparation`) for a separation below 1e-14 rad, NaN, infinite or above
  π/2, where 0, a negative separation and NaN were accepted and turned the collinearity checks off, a positive one below
  1e-14 let collinear vectors that round apart through, and a separation above π/2 threw `ArgumentException` for every
  pair of body vectors. The test of the separation is now the sine of the
  angle, so exactly antiparallel vectors at a separation below about 2.2e-16 rad, collinear vectors that round apart at
  a separation below about 3.4e-8 rad, and body vectors with a component that is not finite throw `ArgumentException` at
  construction (where an attitude with an arbitrary roll, or a NaN quaternion, came out), and such reference directions
  `InvalidOperationException` in `ComputeOrientation`; at exactly the boundary angle the decision can differ by the
  rounding of sin against acos. Every accepted attitude is unchanged, bit for bit; the factories and the default 5
  degrees are unchanged. Migration: pass a separation in [1e-14, π/2] rad, the default 5 degrees where 0 was passed to
  turn the check off, and body vectors and targets that are not collinear.
- **Breaking: a propagation of a star replaces its states** (third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above): `Star.Propagate`,
  `Star.PropagateAsync` and `Scenario.SimulateAsync` keep the states of the last propagation only, as
  `Spacecraft.Propagate` does, where each added its states to those of the earlier ones. Migration: to write several
  windows, propagate over one window that covers them. No result moves for a star propagated once.

- **Breaking: a keyword out of its range throws the reader's parse exception** (fourth round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), Fixed above):
  `Omm`/`Opm.LoadFromFile`, `LoadFromString` and `LoadFromStream` and `OmmReader`/`OpmReader.ReadFrom*` throw
  `OmmParseException` or `OpmParseException` for a keyword whose value the data model refuses, where they threw the
  `ArgumentOutOfRangeException` or `ArgumentException` of its constructor, and `Cdm.ReadFrom*` and `CdmReader.ReadFrom*`
  throw `CdmParseException` for an enumerated keyword that is not one of its values, where they threw `FormatException`;
  the data model's exception is the `InnerException`. The `PARSE_ERROR` of `Omm`/`Opm.TryLoadFromFile` and the error of
  `ValidateFile` name the keyword and its value. The constructors of the data model keep their exceptions, and a
  document that loaded loads as before. Migration: catch the reader's parse exception (`OmmParseException`,
  `OpmParseException`, `CdmParseException`) instead of `ArgumentException` or `FormatException` around a load.

- **Breaking: a CSPICE window search that SPICE cannot run throws `InvalidOperationException`** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth round,
  Fixed above). The five `SpiceAPI.FindWindows*`, and the CSPICE paths of the distance, occultation, coordinate,
  illumination and field-of-view searches of `CelestialItem`, `Site` and `Instrument` (`Site.FindDayWindows` and
  `Launch.FindLaunchWindows` of a kernel site included), throw `InvalidOperationException` with SPICE's message where
  they returned no window: an unknown frame, a body or an epoch without ephemeris data, an epoch outside the coverage of
  the frame data or the C-kernel, a missing instrument kernel, a step SPICE rejects, a result cell that overflows. The
  CLI commands `find-windows-from-coordinate-constraint`, `find-windows-from-distance-constraint`,
  `find-windows-from-occultation-constraint`, `find-windows-from-fov-constraint` and
  `find-windows-from-illumination-constraint`, which call `SpiceAPI` directly, then end with that exception (its
  message and stack on the error output, exit code 1, as for the CLI's other errors) where they printed nothing and
  exited with 0. The `ArgumentException` checks made before the native call (the frames of the library SPICE does not
  know, the occultation bodies, the illumination method) are unchanged. Migration: load the kernel SPICE's message
  names, or catch `InvalidOperationException` (test the exit code of a CLI command) where an empty result was taken to
  mean "never". A search SPICE runs is unchanged, bit for bit.
- **`SpiceAPI.TransformFrame` composes the frame of a site only the library holds through ICRF** ([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), fourth round,
  Fixed above), where it threw `InvalidOperationException`. Not breaking for a valid use: every other pair of frames is
  unchanged, bit for bit. Migration: none; code that caught the exception gets the transform.
- **Breaking: a non-finite numeric value throws the reader's parse exception** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above).
  `Omm`/`Opm.LoadFromFile`, `LoadFromString` and `LoadFromStream`, `OmmReader`/`OpmReader`/`CdmReader.ReadFrom*` and
  `Cdm.ReadFrom*` throw `OmmParseException`, `OpmParseException` or `CdmParseException` for a numeric keyword of
  `NaN`, `Infinity`, `-Infinity` (any case) or an overflowing value such as `1e400`, and the CDM readers for a value in
  km or km/s whose value in m or m/s overflows (`1e306` km), where they loaded the document with a NaN or infinite value; `TryLoadFromFile` returns false with a `PARSE_ERROR`, and `ValidateFile` reports the
  document invalid. Migration: correct the producer of the document; a NaN or infinite value carried no orbit. Finite
  documents load bit for bit as before.
- **An OPM `MASS` of 0 is accepted** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above): `Opm.LoadFrom*`, `OpmReader.ReadFrom*` and
  `TryLoadFromFile` read it, where they threw `OpmParseException`; `new OpmData(stateVector, mass: 0)` builds, where it
  threw `ArgumentOutOfRangeException`; and `OpmValidator` reports no error for it, where it reported "Mass must be
  positive.". Migration: a caller that relied on the refusal checks `Data.Mass > 0` itself. No result moves for a
  positive mass.
- **The message of an angle of `MeanElements` out of its range states [-360, 360)** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above): the
  `ArgumentOutOfRangeException` of `MeanElements` for `raan`, `argOfPericenter` or `meanAnomaly`, and so the
  `OmmParseException` of the reader for `RA_OF_ASC_NODE`, `ARG_OF_PERICENTER` or `MEAN_ANOMALY`, say "must be in
  [-360, 360) degrees" where they said "must be between -360 and 360 degrees". Migration: none, unless a caller parses
  the message.
- **Breaking: the constructors of the CCSDS data model refuse non-finite values** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above).
  `MeanElements` (both constructors and both factories), `SpacecraftParameters`, `OpmData`,
  `OpmKeplerianElements.CreateWith*` and `OpmManeuverParameters` throw `ArgumentOutOfRangeException` naming the
  parameter for NaN, and for +Infinity or −Infinity, in a range-checked parameter, where they accepted it; so do
  `TLE.ToOmm` and `Spacecraft.ToOpm`, which build them, for such a value, where they wrote `NaN`. Migration: pass finite values; a
  non-finite element or spacecraft parameter carried no orbit. Finite values build bit for bit as before.
- **The CCSDS content validators report a NaN variance or miss distance** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above): `OmmValidator`,
  `OpmValidator` and `CdmValidator.Validate` (and `Cdm.Validate`) report an error for a NaN or infinite covariance
  variance or CDM miss distance, where they reported the object valid, and their range messages now name the finite
  rule (`must be finite and non-negative`, `must be finite and positive`). Only an object built programmatically with
  such a value is affected: the readers refuse it. Migration: none for finite data. Since [#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717) such an object no
  longer builds, so these errors are reached through a negative variance or miss distance only.
- **The OMM content validation accepts an angle in [-360, 0)** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above): `OmmValidator.Validate`,
  `ValidateFile` and `Omm.LoadFrom*` with the default `validateContent` accept `RA_OF_ASC_NODE`, `ARG_OF_PERICENTER`
  and `MEAN_ANOMALY` in [-360, 0), where they reported an `InvalidRange` error (`OMM002`) and threw
  `OmmValidationException`. Migration: none; a caller that wants [0, 360) normalizes the angle. No result moves for
  angles in [0, 360).
- **Breaking: the angles of `OpmKeplerianElements` are in [-360, 360)** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above).
  `OpmKeplerianElements.CreateWithTrueAnomaly` and `CreateWithMeanAnomaly` throw `ArgumentOutOfRangeException` naming
  `raan`, `aop`, `trueAnomaly` or `meanAnomaly` for an angle outside [-360, 360) (NaN and ±Infinity included), where
  they built; `Opm.LoadFrom*`, `OpmReader.ReadFrom*` and `TryLoadFromFile` with `validateSchema: false` throw
  `OpmParseException` (a `PARSE_ERROR`) for such an angle, where they loaded it (with the default validation the schema
  rejected it already, `OpmValidationException`, unchanged); `FromKeplerianElements` writes an angle of 2π as 0, where
  it wrote 360. Migration: pass an angle in [-360, 360), for example its value in [0, 360). An angle inside the range
  is stored as before, bit for bit, and `Spacecraft.ToOpm` writes the same document.
- **The OMM content validation accepts a `MASS` of 0** ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), Fixed above): `OmmValidator.Validate`,
  `ValidateFile` and `Omm.LoadFrom*` with the default `validateContent` accept it, where they reported an
  `InvalidRange` error (`OMM002`) and threw `OmmValidationException`. Migration: none; a caller that needs a positive
  mass checks it.
- **Breaking: the CCSDS data model refuses a non-finite value without a range** ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717), Fixed above).
  `new OpmStateVector(...)`, `OpmStateVector.FromStateVector`, `Opm.CreateFromStateVector` and `Spacecraft.ToOpm` throw
  `ArgumentOutOfRangeException` naming `x`, `y`, `z`, `xDot`, `yDot` or `zDot`; `new CovarianceMatrix(...)`,
  `FromFullMatrix`, `FromMatrix` and `FromMatrixWithUnitConversion` (so `Opm.CreateFromStateVector` for a state's covariance)
  the term (`cxX` ... `czDotZDot`); `new OpmManeuverParameters(...)`, `CreateImpulsive` and
  `ImpulseManeuver.ToOpmManeuverParameters` `manDv1`, `manDv2` or `manDv3`; `new TleParameters(...)`, the four
  `CreateWith*` factories and `Omm.CreateForTle` `bstar`, `bterm`, `meanMotionDot`, `meanMotionDDot` or `agom`; setting a
  real property of `CdmRelativeMetadataData`, `CdmRelativeStateVector`, `CdmOdParameters`, `CdmAdditionalParameters`,
  `CdmStateVector` or `CdmCovarianceMatrix` (by an object initializer, a `with` expression or `FromRtnCovariance`, a
  vector component or a covariance term included) the property, and so does `EncounterCase.ToCdm` for such a value in
  an encounter built by hand: each for NaN or ±Infinity, where they built it and wrote `NaN`, `Infinity` or `-Infinity`,
  which the readers refuse. A negative variance still builds and is reported by the validators. Migration: pass finite
  values, or leave an optional CDM value null; a non-finite state carried no orbit. Finite values build bit for bit as
  before, `TLE.ToOmm` is unchanged, and the documents written are byte for byte unchanged. `CdmCovarianceMatrix`
  copies the matrix it is given and the one its `StateCovarianceRtn` returns: a term set afterwards on the caller's matrix
  or on the matrix returned no longer changes the record. Migration: build the record again, or use
  `with { StateCovarianceRtn = matrix }` with the changed matrix.
- **Breaking: the CDM reader refuses a comma in a numeric value** ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717), Fixed above). `Cdm.ReadFromString`,
  `ReadFromStream`, `ReadFromFile` and `CdmReader.ReadFrom*` throw `CdmParseException` for a numeric value with a
  comma, where they dropped the comma and read another number. Migration: correct the producer, which must write a
  period. Documents written to the standard are read bit for bit as before.

- **Breaking: `EvaluateAvoidance` refuses a step of zero or less** ([#640](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/640), Fixed above).
  `AvoidanceSearchOptions.AnalysisSampleStep` and `MaximumEventSearchStep`, when given, must be positive, the rule of
  `ConjunctionAnalysisOptions`: `ArgumentOutOfRangeException` (its `ParamName` the option), where a negative step was
  accepted or the call ran out of memory. Migration: give a positive step, or null for the encounter's.

- **Breaking: a window without a close approach gives its closest end, flagged, and `AnalyzeAll` nothing** ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621), Fixed above).
  `Analyze`, `Screen` and the re-analysis of each avoidance option return the closest window end where the sampled path
  returned the midpoint (and where the search on two propagations returned the end the distance grows into, the same end
  but for a minimum its seeds miss), with `EncounterQualityFlags.TcaAtWindowEnd` (new, `1 << 8`) raised; the
  `ILocalizable` `AnalyzeAll` returns an empty list where it returned one midpoint encounter. Every encounter of a
  window without a close approach carries the flag, the CDM export's comment of the flags included; a close approach the
  sampled search finds on a window end (its range-rate zero in the half-tick next to the end, the tick nearer it #624
  returns) does not, and `AnalyzeAll` lists it
  (`ConjunctionKernelAndWindowTests.CloseApproachInTheLastHalfTick_IsListedAtTheEnd_NotFlagged`, red on 415442e, where
  the flag was raised for any TCA on a window end). Migration: test
  `QualityFlags.HasFlag(EncounterQualityFlags.TcaAtWindowEnd)` for an encounter whose closest approach lies outside the
  window, and widen the window to find it; expect an empty list from `AnalyzeAll` where one midpoint encounter came.

- **Breaking: the remediation of an initial covariance is bounded** ([#443](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/443), decided by S. Guillet, 2026-11-17).
  `StateVector.WithRemediatedCovariance()` and the conjunction analysis remediated any indefinite covariance by eigenvalue
  clipping, however deep its defect: a T–Ṙ correlation of −10 by a change of 5.5 times the values, flagged
  `CovarianceRemediated` only. Both now refuse a defect deeper than the rounding of the elements to 4 significant digits
  (the form of the CDM's covariances, CCSDS 508.0-B-1, to be verified by S. Guillet) explains: the smallest eigenvalue
  of the correlation matrix below −1e-3 of its trace (`CovarianceChecks.RemediationDepthBound`) in the frame of the state
  and in its RTN frame (the frame a CDM rounds in; a rotation deepens a rounding defect, up to −0.11 of the trace measured
  for covariances rounded in RTN). The bound is above the rigorous worst of that rounding, 8.34e-4 of the trace by Weyl's
  inequality (measured: 1.5e-4 over 6000 LEO and GEO covariances of ranks 1 to 6, 2.1e-4 by an adversarial search). A
  correlation of −1.01 in RTN (1.67e-3 of the trace) is refused; −1.0059 (9.8e-4) is remediated. The clipping is made in
  the frame of the state when the depth passes there, bit for bit as before; otherwise in the RTN frame of the state,
  rotated back: clipped in the frame of the state, a covariance rounded in RTN whose depth there reached −0.75 of the
  trace changed by up to 2.9 √(M_ii M_jj) (adversarial review of #443); clipped in RTN, by the size of its rounding
  (4.4e-4 at worst over 83 such covariances, `WithRemediatedCovariance_DeepOnlyInTheFrameOfTheState_IsClippedInRtn`, red
  on b8e2e42).
  `WithRemediatedCovariance` throws `InvalidOperationException` with the depth in both frames in its message; the
  analysis holds such a covariance in RTN, as its other refusals, without `CovarianceRemediated`
  (`CovarianceRemediationBoundTests`, `ConjunctionCovarianceEdgeTests.Analyze_InitialCovarianceBeyondTheDepthBound_IsHeldInRtn`,
  red on a85098d for every case beyond the bound). Results change only for such a covariance. Migration: a covariance
  refused by the bound is not one that rounding explains: correct its source, or give it at its full precision (one
  rounded to 3 digits or fewer may be refused).

**Deprecated**

- **`Star.PropagationOutput`** is marked `[Obsolete]` (a warning, CS0618; fourth round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), decided by S. Guillet,
  2026-11-15). It is always null: the output directory of an older design that wrote the propagation to files, which no
  code sets, while a star keeps its propagated states in memory. Nothing moves at run time. It may be removed in a later
  major version. Migration: read `IsPropagated` to know whether the star holds propagated states, and write them with
  `WriteEphemeris`, to a file of the caller's choice.

**Removed**

- **`Instrument.PositionInFOV`** (protected), on which `IsInFOV` was built before #515: its reference vector was not made
  perpendicular to the boresight and its corrections were those of `CelestialItem.GetEphemeris`. A subclass of
  `Instrument` that called it no longer compiles. Use `IsInFOV`, the `POINT` condition of CSPICE's `fovtrg` with the
  field and the corrections of the search; the azimuth and the elevation it returned have no replacement
  ([#515](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/515)).
- **`LowThrustManeuver`.** It was never implemented: every member threw `NotImplementedException`. It was marked
  `[Obsolete]` in 10.1.0 (see its Deprecated entry). Finite thrust is planned as a separate feature
  ([#334](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/334), [#536](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/536)).
- **`Quaternion.Zero`**, the identity under another name and an assignable field: use `Quaternion.Identity` (Changed
  above, [#571](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/571)).

**Known limitations**

The open defects a caller can meet at this release, grouped by cause; each line gives the workaround when there is
one. The other open issues (tests, the conformance runner, benchmarks, documentation warnings and feature requests) are
not listed.

- **A star takes no radial velocity** ([#718](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/718)). `Star` moves along `iauStarpm`'s straight line with rv = 0, its distance
  from the catalog parallax: Barnard's star's −110.47 km/s (Gaia DR3) would move it by 64 mas after 10 years, 6.5″ after
  100 and 11.4′ after 1000, more than the straight line corrects (34 mas after 100 years). The radial velocity input
  that #718 recommends would be a new constructor parameter, and the defect fixes of 11.0.0 keep the existing signatures
  (#668, decided by S. Guillet, 2026-11-14): it is left to an additive constructor overload after 11.0.0, in
  [#723](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/723) (decided by S. Guillet,
  2026-11-17; [Stars](../reference/stars.md#limits)).
- **A point target at the observer is in view of a rectangular field**
  ([#721](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/721)). The managed field-of-view
  search of a rectangular instrument reports a point target at the observer, distance 0, in view, its margin taking
  atan2(0, 0) = 0, where a circle and an ellipse throw `InvalidOperationException` since #715 (Fixed above). It is to
  throw as they do (decision 9 of #719, decided by S. Guillet, 2026-11-17). No workaround is needed for a target away
  from the observer.
- **A front body without radii on the CSPICE path of the occultation search**
  ([#496](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/496)). On the CSPICE path of
  `FindWindowsOnOccultationConstraint` the front body is always passed as an ellipsoid, so a front body without radii
  cannot be searched there: SPICE's error, `InvalidOperationException` since the fourth round of #668 (before, no
  window). The other SPICE errors of the CSPICE window searches throw since that round (Changed above).
- **A boolean quantity in the managed window search**
  ([#678](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/678)). `GeometryFinder.FindWindowsWithCondition` of a `Func<Time, bool>`
  has no margin to search between its samples: a window or a gap shorter than the step can be lost. Use a step shorter
  than every window and gap, or a `double` quantity whose sign is the condition. A `double` quantity's search finds a
  window or a gap between two samples down to the resolution of `localmin`, about 2 √ε h + 2τ/3 for a step h and
  τ = 1 µs (2.5 µs at a 1 min step, 19 µs at 10 min, 41 µs at 22.5 min; #668), and no tangency there (an extreme
  exactly at the value). The occultation and field-of-view searches have the same limit, the bound scaling with
  `OccultationSearchOptions.ConvergenceTolerance` (#668: their documentation said "however short", and the `stepSize`
  of the distance, coordinate and illumination searches of `CelestialItem` and `Site` "every window and gap").
- **At most 1000 windows from a CSPICE search**
  ([#653](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/653)). A search that CSPICE runs
  returns its first 1000 windows and drops the rest without an error; `NotEqual` then ends with one window across every
  dropped crossing. Split the search window so that each part holds fewer than 1000 windows.
- **The element conversions of `SpiceAPI`**
  ([#450](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/450),
  [#664](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/664)).
  `SpiceAPI.ConvertEquinoctialElementsToStateVector` passes the true longitude where `EQNCPV` takes the mean longitude
  (1029 km off for an ordinary LEO, 27 900 km at e = 0.89) and throws `ArgumentException` for a ≤ 0 or e > 0.9, the
  domain of `EQNCPV` (#711);
  the conversions hold the SPICE lock while they build their result (about 80 µs for a native call of 1.8 µs). Use
  `EquinoctialElements.ToStateVector()` and `KeplerianElements.ToStateVector()`.
- **Kernels the library writes** ([#545](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/545),
  [#546](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/546),
  [#655](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/655),
  [#661](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/661),
  [#706](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/706)). `Spacecraft.WriteOrientation`,
  `SpiceAPI.WriteOrientation` and `SpiceAPI.WriteEphemeris` (under `Star.WriteEphemeris` and `Site.WriteEphemeris`)
  leave the kernel they write unloaded: load it after writing. A file they replace is unloaded first under its path and
  the absolute names whose symbolic links resolve through it (#706), but not under a relative name other than its path
  (a meta-kernel's) nor through a hard link, through which SPICE goes on reading the replaced data: unload such a load
  before rewriting the file. Both writers enumerate the caller's
  states once, under the SPICE lock: pass an array. A spacecraft propagated by `TLEPropagator` has no attitude record, so its CK cannot be written and its
  field-of-view searches throw ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)); give it its attitude with `AddStateOrientationToICRF`.
- **Kernel paths of more than 255 characters** ([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)). `LoadKernels` and the command line hand SPICE the full path of
  each kernel, so a kernel whose full path is longer than 255 characters cannot be loaded through them
  (`SPICE(FILENAMETOOLONG)`, reported as "more details on standard output"), while `Spacecraft.WriteEphemeris` and the
  text kernel writers still write it: load it through a meta-kernel that names it by a shorter relative path, or write
  it under a shorter directory. Paths are compared ordinally, so on Windows and macOS two spellings that differ in case
  are two entries.
- **CIRS and TIRS by name, and searches in a managed frame**
  ([#558](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/558),
  [#656](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/656)). A frame built by name as CIRS
  or TIRS (`new Frame("CIRS")`, a CCSDS file, the CLI) equals `Frame.CIRS` or `Frame.TIRS` but goes to SPICE, which does
  not know it: `new Frame("CIRS").ToFrame(Frame.ICRF, t)` throws, and `SpiceAPI.TransformFrame` in CIRS or TIRS throws
  even for the instances. `FindWindowsOnCoordinateConstraint` with a kernel-backed spacecraft in a frame built by the
  name CIRS or TIRS throws SPICE's error (no window before the fourth round of #668), and `SpiceAPI.ReadOrientation` in those frames throws. Use the instances
  `Frame.CIRS` and `Frame.TIRS`, which the coordinate searches of `CelestialItem` and `Site` search in with the library
  (third review of #690). The frame of a site built from coordinates and that of a managed spacecraft with an attitude
  record are read in J2000 and rotated ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)), and searched in by the library (third review of #690), but a frame built by
  such a name (`new Frame("MYSITE_TOPO")`) still goes to SPICE, as does any frame built by a name SPICE does not know,
  whose CSPICE search throws SPICE's error (no window before the fourth round of #668).
- **Identifiers that are not celestial bodies**
  ([#650](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/650),
  [#651](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/651)). `SpiceAPI.ReadEphemeris` by
  identifiers throws `ArgumentException` for a spacecraft observer: with `Aberration.None`, read with the spacecraft as
  target and negate the state (with LT, CN or +S the negation is not the state `spkezr` gives); so does a site observer
  that no loaded kernel names, and one whose SPK does not cover J2000 TDB, which its `Site` constructor reads, throws
  `InvalidOperationException`, with the same workaround (documented in the fourth round of #668). `SpiceAPI.GetCelestialBodyInfo` of a site identifier (DSS-13, 399013) gives the Sun as centre and the
  barycentre 0, every identifier above 1000 being classed as an asteroid, so `new CelestialBody(399013)` and
  `CelestialItem.Create(399013)` build a body DSS-13 of GM 0 and frame DSS-13_TOPO about the Sun (they threw or were
  built with a garbage frame name before
  [#672](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/672)): build a `Site`.
  `SpiceAPI.ReadEphemeris` from a site identifier labels its state with a `Site` (#672). The commands of the CLI follow
  the same rule and build the same site (its SPICE name), under Changed.
  `MemoryDataProvider` throws `ArgumentException` naming `naifId` for a body it holds no information on, as
  `SpiceDataProvider` for an identifier SPICE does not name
  ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680); `KeyNotFoundException` before).
- **Reads past the stored states of a propagated spacecraft**
  ([#527](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/527),
  [#626](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/626)).
  `Spacecraft.GetGeometricStateRelativeTo`, `GetEphemeris` and the managed distance and coordinate searches extrapolate
  the 8-point polynomial past the stored states without an error (567 km off 6 min before the first state, 8 282 km 6
  min after the last). A propagation whose output step does not divide its window runs up to half a step past its end,
  where its orientation cache, which ends 120 s after the window, holds the Earth's orientation at its last grid point,
  and its ephemeris cache, which ends 240 s after it, extrapolates the third bodies; a geopotential or drag read there
  past the coverage throws. Keep reads
  inside the propagated window, and choose an output step that divides it, or one of 240 s or less.
- **Impulses at the edges of the stored states**
  ([#572](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/572),
  [#575](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/575),
  [#579](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/579),
  [#638](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/638)). With Velocity-Verlet the
  segment after an impulse starts at the event's label rounded to the 100 ns tick (up to 5e-8 s per event, accumulating
  over a chain), and an impulse whose crossing falls in the last step is executed (fuel, `ExecutedManeuvers`, `ToOpm`)
  while the states end before it: extend the window past the last maneuver. Two impulses less than 2 µs apart store no
  arc between them: a read at the first returns the state before both, and an SPK read between them throws
  SPKINSUFFDATA. The state that closes an arc carries its impulse's tick, up to half a tick from its instant (about 0.4
  mm at 8.6 km/s).
- **State kept by the maneuver chain**
  ([#583](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/583),
  [#584](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/584)). A spacecraft propagated a
  second time through `CentralBodyPropagatorBuilder.Build().Propagate()` or a `CentralBodyPropagator` built directly is
  not reset: the second run fires no burn and starts with the fuel the first left. A second
  `Spacecraft.SetStandbyManeuver` before a propagation does not replace the first, which fires while the new one is
  dropped silently. Use `Spacecraft.Propagate` or a fresh spacecraft, and set the standby maneuver once (a maneuver on
  another spacecraft's tank is rejected since #676).
- **Event detection** ([#578](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/578),
  [#580](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/580),
  [#581](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/581)). The public
  `IIntegrator.IntegrateSegment`, given several detectors, reports the first in list order whose sign changed in a step,
  not the earliest crossing (`Propagate` and `Scenario.SimulateAsync` pass one). A g-function exactly 0 at a step end
  past the gate is never a crossing, so a time-like user `ImpulseManeuver` or `IEventDetector` landing on a
  Velocity-Verlet or fixed-step RK7(8) step end never fires. A `PlaneAlignmentManeuver` whose propagation starts less
  than 1e-6 |r| past a node fires one orbit late.
- **A null target orbit** ([#555](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/555)). The
  target-orbit constructors of `ApogeeHeightManeuver` and `PerigeeHeightManeuver` throw `NullReferenceException` for a
  null `targetOrbit`, not an `ArgumentNullException` that names it.
- **Attitudes** ([#549](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/549),
  [#592](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/592)). Between output epochs the
  attitude record is the spherical linear interpolation of its samples, not the law: a `NadirAttitude` 1.47e-3 rad off
  nadir at 60 s outputs in LEO and up to 1.04° near the roll singularity, a TRIAD law up to 3.8e-4 rad on a Molniya;
  shorten the output step where it matters. `InstrumentPointingToAttitude` points at a celestial target geometrically
  although it requests LT (the Moon 4.2e-5 to 4.5e-5 rad, about 40 km, from its light-time direction from LEO); a `Site`
  target is correct. `TriadAttitude` rejects vectors collinear to the rounding of normal components at every separation
  (fourth round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)), but a
  subnormal component (below about 2.2e-308) rounds by an absolute 4.9e-324: v = (1, 2, 3)·1e-320 and fl(0.7 v) have a
  sine of 1.1e-4 and pass a separation below that, with a roll set by rounding; give body vectors of a normal magnitude.
- **OPM export** ([#339](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/339),
  [#658](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/658)). `Spacecraft.ToOpm` writes the
  two-body (or SGP4) motion of the initial orbit, not the propagated state, with the mass and the maneuvers after the
  propagation (1 212 km and 1 137 m/s off one burn later): for a propagated state, write `Opm.CreateFromStateVector`
  from `GetEphemeris` at the epoch. `Opm.CreateFromStateVector` and `ToOpm` write `REF_FRAME = J2000` for `Frame.ICRF`,
  which the library's own `OpmValidator` reports as non-standard: build the OPM with `new Opm(header, metadata, data)`
  and an `OpmMetadata` whose reference frame is `ICRF`, or edit `REF_FRAME` in the written file.
- **TLEs** ([#598](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/598),
  [#604](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/604),
  [#607](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/607)). `TLEPropagator` counts its
  samples in whole seconds: a step below 1 s throws `DivideByZeroException`, and a window that is not a multiple of the
  step loses its end; use a whole-second step that divides the window. A TLE with a blank international designator can
  be read, but neither re-created by `TLE.Create` nor round-tripped through an OMM. `TLE.Create` checks only that its
  elements are mean: pass mean elements about the Earth in `Frame.TEME`. `Omm.ToTle` writes element set 999 for an OMM
  without `ELEMENT_SET_NO`, `TLE.Create` 9999 by default ([#606](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/606)): pass the element set number.
  A spacecraft propagated numerically from a TLE ([#675](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/675)) integrates about the TLE's Earth, a point mass whose drag, without an atmospheric model,
  is NRLMSISE-00 at nominal space weather ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)), and an Earth with a geopotential or an atmosphere in the bodies throws
  `ArgumentException` (bodies of one identifier must have the same models, [#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)); give a TEME state on an Earth that
  carries the models
  ([Numerical propagation of a TLE](../reference/tle.md#numerical-propagation-of-a-tle)). CIRS and TIRS, quasi-inertial
  as TEME, are still rejected as initial frames. A TLE cannot be built without the kernels that define the Earth. After
  a numerical propagation, `TLEPropagator` adds its SGP4 states to the stored ones, and the spacecraft reads both.
- **CDM writing and validation** ([#614](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/614),
  [#615](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/615),
  [#618](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/618)). `CdmWriter` puts the `cdm` and
  `ndm` roots in the NDM/XML namespace, so the SANA schema rejects every CDM the library writes, which
  `Cdm.ValidateSchema` accepts in that form (it checks the standard forms since the third round of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), which closes
  [#614](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/614)).
  `CdmExporter` writes `COLLISION_PROBABILITY_METHOD = FOSTER-2D`, which SANA does not register (Foster's method is
  FOSTER-1992). `CdmExporter` refuses an encounter with one covariance, so its `FOSTER-2D-MAX-PC` is never written
  ([#342](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/342)). Remove the namespace of the
  written CDM before exchanging it (the result validates with `Cdm.ValidateSchemaFromXml`), and set
  `CdmExportOptions.CollisionProbabilityMethod`.
- **CDM dates and numbers** ([#616](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/616),
  [#617](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/617),
  [#619](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/619)). A date in a leap second throws
  `CdmParseException`. `CdmWriter` reads a `DateTime` of kind `Unspecified` as local time: build its dates with
  `DateTimeKind.Utc`. The reader takes a decimal comma for a thousands separator (`4,835E-05` read as 0.04835) and loads
  free-text or time-only dates (a time gets today's date) instead of rejecting them.
- **Orbits within 1e-6 of e = 1**
  ([#459](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/459),
  [#462](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/462),
  [#477](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/477),
  [#478](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/478)). An orbit with |e − 1| < 1e-6
  is taken as a parabola whatever its energy: `StateVector.SemiMajorAxis()` returns +Infinity, `MeanMotion()` and
  `MeanAnomaly()` take Barker's forms, `ToStateVector()` takes p = 2q (metres off on a round trip), and
  `ToEquinoctial()` gives P = NaN or ±Infinity. In 0 < |e − 1| < 1e-6 Kepler's equation is applied to Barker's anomaly,
  so `ToStateVector(ν)`, `TimeToRadius` and the equinoctial round trips can be thousands of km or hundreds of seconds
  off without an error. Parabolic `EquinoctialElements` with a finite P and no perigee radius throw from
  `ToKeplerianElements()`, `AtEpoch` and a propagation: pass `perigeeRadius: P / (1 + e)`.
- **Precision near e = 1** ([#472](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/472),
  [#484](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/484)).
  `KeplerianElements.ToStateVector()` loses digits through p = a(1 − e²), 1 + e cos ν and e + cos ν: 1.1e-11 relative at
  e = 0.999999 (155 m at the apoapsis of a 7000 km periapsis). Near the periapsis Kepler's equation stops at its
  cancellation floor: a state from a mean anomaly up to 1.1e-10 of |r| off at e = 0.999999, and
  `StateVector.MeanAnomaly()` up to 1.9e-10 of M.
- **No period for e ≥ 1** ([#663](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/663)).
  `Period()` throws the NaN `ArgumentException` of `TimeSpan` for a hyperbola and an `OverflowException` for a parabola,
  and for an ellipse in the parabolic band, with no message that says an open orbit has no period.
- **Elements accepted without a check**
  ([#476](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/476),
  [#481](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/481)). `KeplerianElements` accepts e
  = NaN or +Infinity: `TrueAnomaly()`, `ToStateVector()` and `ToEquinoctial()` then throw `InvalidOperationException`
  "Nullable object must have a value", which names neither e nor the call (+Infinity gives NaN states).
  `EquinoctialElements` accepts P ≤ 0 or a non-finite P. For a finite P ≤ 0, `ToStateVector()` gives a NaN velocity
  and `ToKeplerianElements()` a negative semi-major axis without an error, while `AtEpoch` and `ToStateVector(Time)`
  throw the Kepler solver's error on M = NaN, which does not name P; for P = ±∞ on an ellipse they give a non-finite
  state without an error. Check the elements before building them.
- **Mean elements and equality of orbital parameters**
  ([#463](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/463),
  [#475](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/475)). `AtEpoch(t)` and
  `ToKeplerianElements(t)` of mean elements (a TLE, `KeplerianElements.FromOMM`) return the same numbers labelled
  osculating, which bypasses the guard of `ToStateVector()`: use `TLE.ToOsculating()` or an SGP4 state. Typed
  `OrbitalParameters`, `==`, `!=` and `Equals` compare `ToStateVector()`, so they throw for mean elements (even against
  null) or e = NaN, and `GetHashCode` includes the reference, so equal copies hash differently and hash sets keep
  duplicates: compare the element values.
- **Angles brought into [0, 2π)**
  ([#471](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/471)). Angle normalization takes
  fl(2π) as 2π: each turn added to Ω, ω, ν or M leaves it 2.45e-16 rad low, each turn removed that much high, and
  `SpecialFunctions.NormalizeAngle(-1e-17)` returns 2π (360°, which can appear in an OPM or OMM).
- **Time equality and truncated spans**
  ([#512](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/512),
  [#637](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/637)). The same instant in two time
  frames has `CompareTo` 0, but `==` and `Equals` are false and the hashes differ, so `Dictionary`, `HashSet`,
  `Distinct` and `IndexOf` keyed by `Time` miss it: convert keys to one frame (`ToTDB()`). `AddDays`, `AddHours`,
  `AddMinutes`, `CreateFromJD` and the spans computed through `TimeSpan.FromSeconds` (`Period()`,
  `SideralRotationPeriod`, `TrueSolarDay`, the windows of an impulse, the hold of `PhasingManeuver`, `Launch`, the
  epochs of the conjunction search) truncate to the tick below, so each can be a tick early.
- **The ephemeris time given to CSPICE**
  ([#665](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/665), item 4 of
  [#455](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/455)). CSPICE is
  given the managed one-term TDB as its ephemeris time, within 1.06 µs of the LSK formula SPICE evaluates (1.055 µs at
  the tick, [Time Scales](../reference/time-and-windows.md#time-scales)): a UTC epoch at a coverage bound of a kernel,
  as `str2et` computes it, can be read up to about 1.1 µs outside it and throw SPKINSUFFDATA. Read 2 µs inside the
  bound.
- **Light time on the reference-body path**
  ([#489](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/489),
  [#593](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/593)). A `Site` built from its
  coordinates and read as a target from a kernel-backed observer takes the light time of the observer, not of the site:
  82.7 km off `spkcpt` from the Moon with LT, about 15 000 km from the Sun. A light-time-corrected state in a
  non-inertial frame centred on the target (ITRF93, MOON_ME) takes the frame at the observation epoch, not at t − lt as
  `spkezr` does: the Moon from DSS-13 with LT 531 m to 1.48 km off. `ToEquatorial` of such a state rotates it to ICRF at its epoch
  ([#680](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/680)): 3.05e-6 rad in RA and 1.15e-6 rad in Dec for the Moon from the Earth with LT in MOON_ME; read it in ICRF.
- **Illumination of a site above the ellipsoid**
  ([#654](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/654)). A site from the kernels,
  searched by `gfilum_c` with the normal of `surfnm_c` at the point, and the same site built from its coordinates,
  searched with the geodetic normal, give illumination windows up to 34.0 ms apart at DSS-13, 1070.85 m up (0.110″
  between the normals); `FindDayWindows` and `Launch` follow.
- **In-memory data provider** ([#559](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/559)).
  `MemoryDataProvider.GetEphemeris` returns its ICRF numbers labelled with the frame asked and ignores the aberration:
  read in ICRF without aberration and convert with `ToFrame`.
- **Memory and cost of the reads**
  ([#541](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/541),
  [#563](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/563)).
  `Frame.GetStateOrientationToICRF` (and `CirsFrame`, `GcrfFrame`, `TirsFrame`, `SiteFrame`) keeps every orientation it
  reads for the life of the frame, without bound (16.5 MB after 100 000 reads of ITRF93), and the static frames live for
  the whole process. `SpiceAPI.ReadOrientation` costs about 1.4 ms a call whatever the window, and the window reads of
  `ReadOrientation` and `ReadEphemeris` step by epoch += step, so their samples drift (4.3e-4 s observed) and the last
  epoch of the window can be dropped.
- **SSB mode about the Earth** ([#389](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/389),
  [#544](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/544),
  [#576](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/576)). `ThirdBodyPerturbation`
  computes the Earth's attraction with Battin's 1 + q, which loses about 8 digits near it: in SSB mode an RK7(8) LEO
  propagation takes about 5 500 times more steps than relative to the Earth (125 641 against 23 for one hour at 1e-10)
  and fails at 1e-12, its steps depend on the platform, and a start from ECLIPJ2000 or GALACTIC ends 0.07 to 0.56 m from
  its ICRF twin after 1 h. Propagate relative to the Earth, or keep the tolerance at 1e-9 or looser.
- **Integration accuracy** ([#439](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/439),
  [#445](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/445),
  [#561](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/561)). Velocity-Verlet is of first
  order with drag: on a dense perigee pass its error against RK7(8) is 267 m, 27.2 m and 2.72 m at steps of 1, 0.1 and
  0.01 s; use `RK78Integrator` when drag matters. At 1e-11 the 3 h error of RK7(8) in LEO with drag and SRP spans 0.94
  to 36.2 mm with the placement of the steps around the eclipse. The orientation cache of a propagation interpolates the
  Earth's 60 s nodes with the normalised-Lerp fallback of `Quaternion.SLERP`, up to 3.4e-10 rad from the exact
  interpolation. It is built for every central body, whether or not a force rotates, so each of its 60 s points past the
  coverage of the orientation kernel goes through the native read, which prints its SPICE message on standard output
  (123 lines for 2 h, measured before the fix of [#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454); the native read is unchanged): a point-mass
  propagation there still runs ([#595](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/595)).
- **Force model inputs** ([#674](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/674)). A configured `Nrlmsise00Model` shared by parallel propagations serializes their
  density calls on its lock (4 threads × 200 000 densities: 5.8 s on one instance, 0.9 s on one per thread); the default
  atmosphere of a model-less Earth is one instance per thread, so give each parallel propagation its own configured
  model. A custom `IAtmosphericModel` compares by `Equals`, so two separately built instances on bodies of one NAIF
  identifier throw unless it overrides `Equals`. `CentralBodyPropagator(window, spacecraft, IIntegrator, deltaT)` uses the
  integrator as given: call its `Initialize` with the initial state, or the first step throws `NullReferenceException`.
- **Mars atmosphere** ([#431](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/431)). Above 100 km
  `MarsStandardAtmosphere` extends an isothermal layer at 139 K: at 400 km its density is 4.6e7 times below Mars-GRAM,
  so drag in Mars orbit above about 120 km is underestimated by orders of magnitude.
- **Kelvin offset of the standard atmospheres**
  ([#438](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/438)). `EarthStandardAtmosphere` and
  `MarsStandardAtmosphere` take 273.15 where the NASA Glenn curve fits take 273.1: the Earth's density departs from the
  published fit by up to 0.285 % (at 25 km), the Mars density by about 0.02 %.
- **The non-linearity index**
  ([#403](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/403)). The threshold 1.0 of the
  non-linearity index is measured on two-body crossing encounters only (11 cases up to ν = 1.42), so the index stays off
  by default.
- **Close-approach search** ([#639](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/639),
  [#641](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/641)). In cases of measure zero the dense search can list a maximum (a range rate exactly 0.0 at a
  seed), or, with a `BisectionToleranceSeconds` coarser than a tick, a zero at the window end as the middle of its last
  bracket. The sampled search subdivides a step one tick longer than `MaximumEventSearchStep`, one sample more for a read
  100 ns closer ([#642](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/642)); results are unaffected.
  The `ILocalizable` overloads of `Analyze` and `AnalyzeAll` run the sampled search even for two participants propagated
  in RK7(8): the search on the polynomials of their steps is not inferred in 11.0.0 (D4 of the API review of
  [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)); pass their `PropagationSolution` for it.
- **Cost of the conjunction analysis**
  ([#447](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/447)).
  `ConjunctionAssessment.Analyze` allocates about 403 MB per pair over a 2 h window (0.83 s with the covariance held in
  RTN, 1.71 s propagated); results are unaffected.
- **Command line** ([#652](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/652),
  [#657](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/657)). The help of
  `find-windows-from-coordinate-constraint` lists `LATITUDINAL`, `SPHERICAL`, `CYLINDRICAL` and `COLATITUDE`, which do
  not exist, and its `RA/DEC` and `RIGHT ASCENSION` are rejected: pass the member names (`RaDec`, `RightAscension`).
  `ephemeris` labels ICRF states `j2000` on the SPICE path and `J2000` from a site observer, the numbers the same.

### 10.2.0-preview-1

A preview of 10.2.0, published on NuGet as a prerelease (`dotnet add package IO.Astrodynamics --prerelease`,
`dotnet tool install -g IO.Astrodynamics.CLI --prerelease`); the documentation site is not updated by a preview.

Propagation of the state transition matrix, of the sensitivity to the drag and reflectivity coefficients and of the
covariance in RK7(8), and their use by the conjunction analysis, the avoidance options and the CDM, with the defects
its validation found in older code. No breaking change to the public API. Several results change: see **Changed**
and **Fixed**. The open issues known at this preview are listed under **Known limitations**.

**Added**

- **State transition matrix and covariance propagation.** The RK7(8) integrator propagates, with the state, the state
  transition matrix Φ, the sensitivity Ψ to the drag and reflectivity coefficients and, on request, the covariance
  `P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q` of the initial orbital parameters, with state noise compensation in RTN
  (`RtnProcessNoise`, m²/s³), consider parameters (`ConsiderParameters`) and the execution error of impulsive maneuvers
  by the model of Gates (`ImpulseManeuver.ExecutionError`, `ManeuverExecutionError`). Activation through
  `CovariancePropagationOptions` (`CovariancePropagationMode.StateTransitionOnly` or `Covariance`):
  `CentralBodyPropagatorBuilder.WithCovariancePropagation`, two new `CentralBodyPropagator` constructors,
  `PropagationTask.CovariancePropagation`, a `Spacecraft.Propagate` overload and a `Scenario.SimulateAsync` overload.
  Results on `PropagationSolution`: `GetStateTransitionMatrix`, `GetParameterSensitivity` (columns in
  `SensitivityParameters`), `GetCovariance`, `GetExtendedCovariance` (rows in `ExtendedCovarianceParameters`),
  `GetStateVector`; in covariance mode the output states carry their covariance. Off by default: without it nothing
  changes, an initial covariance is not propagated and no partial derivative is evaluated. Velocity-Verlet stays the
  default integrator and does not support it (`NotSupportedException`). The SPK written from the propagated states does
  not carry the covariance. See [Covariance Propagation](../reference/covariance-propagation.md).
- **Integrator choice in `Scenario`.** New overload `Scenario.SimulateAsync(includeAtmosphericDrag,
  includeSolarRadiationPressure, propagatorStepSize, integratorFactory)`: the factory creates the integrator of each
  spacecraft, for example `() => new RK78Integrator()`. The existing overload and the default integrator,
  Velocity-Verlet, are unchanged.
- **Propagated covariance in the conjunction analysis.** `ConjunctionAnalysisOptions.CovariancePropagation`
  (`CovariancePropagationOptions`, covariance mode without process noise by default), `ComputeNonlinearityIndex` (off by
  default) and `NonlinearityIndexThreshold` (1.0); `CovarianceSource` and `ParticipantCovariance` (source, age, `ProcessNoiseApplied`,
  `NonlinearityIndex`) on `EncounterState.ProtectedCovariance` and `SecondaryCovariance`, with a new constructor
  overload; `EncounterQualityFlags.NonlinearCovariancePropagation`; `AvoidanceSearchOptions.ExecutionError`. The CDM
  export of a propagated covariance carries the rows `CDRG_*` and `CSRP_*` of the consider parameters, the force model
  used (`GRAVITY_MODEL`, `ATMOSPHERIC_MODEL`, `N_BODY_PERTURBATIONS`, `SOLAR_RAD_PRESSURE`) and the process noise in a
  comment. See [Conjunction Assessment](../reference/conjunction-assessment.md#covariance-at-tca).
- **The constants of a geopotential model.** `GeopotentialModelParameters(path, degree, gravitationalParameter,
  referenceRadius)` and the same with a `Stream`, for a model whose coefficients are normalized to other constants than
  EGM2008's (finite and positive, `ArgumentOutOfRangeException` otherwise); `GeopotentialModelParameters.GravitationalParameter`
  and `ReferenceRadius` (null with the constructors without constants), and the same on `GeopotentialGravitationalField`,
  the constants its harmonics take. See [Force Models](../reference/force-models.md#geopotentialgravitationalfield)
  ([#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410)).
- **Remediation of an initial covariance made indefinite by rounding.** `StateVector.WithRemediatedCovariance()` returns
  a `CovarianceRemediation` (`State`, `Remediated`, `SmallestCorrelationEigenvalue`): the state with its covariance
  remediated by the eigenvalue clipping of the conjunction analysis, the same code and tolerances, so that an explicit
  propagation of the covariance accepts it; a covariance that the propagation accepts is returned as it is, the state
  itself. It refuses, with `InvalidOperationException`, a covariance that rounding does not explain (not 6×6, not finite,
  a negative variance, an asymmetry beyond the tolerance). The propagation itself stays strict and still rejects such a
  covariance. See [Covariance Propagation](../reference/covariance-propagation.md#an-initial-covariance-indefinite-from-rounding).
  
**Changed**

- **The conjunction analysis propagates the covariance to TCA.** For a participant propagated in RK7(8) (a
  `PropagationSolution` passed to the analysis, or the last propagation of a `Spacecraft`) whose initial state vector
  carries a covariance at the start of that propagation, `Analyze` and `AnalyzeAll` use the covariance propagated to TCA,
  by a replay of the variational equations along the steps of that propagation, instead of the initial covariance held
  fixed in RTN: its Pc changes, and becomes the Pc of the propagated covariance (on the two-body conjunction of the tests,
  24 h after the initial states, the in-track σ at TCA is 21.7 km against 232 m, the Pc 2.0e-6 against 1.9e-4), and
  `StaleCovarianceUsed` is no longer raised for it. `Screen` and the other participants (Velocity-Verlet, not
  propagated, TLE, SPICE) give the same results as before, bit for bit. An initial covariance that rounding made
  indefinite is remediated by eigenvalue clipping before its propagation, and `CovarianceRemediated` is raised; one that
  rounding does not explain (a negative variance, not finite) is held in RTN as before, without an exception. To
  restore the former behavior, set
  `ConjunctionAnalysisOptions.CovariancePropagation = null`. To replay it, a `Spacecraft` propagated in RK7(8) keeps its
  last propagation until it is propagated again (about 0.55 MB for one day in LEO with drag); a propagation by
  Velocity-Verlet keeps nothing more than before.
- **Avoidance options propagated numerically.** When the covariance of the protected participant is propagated,
  `EvaluateAvoidance` propagates each option in RK7(8) from the state and the covariance at its burn, with the force
  model of the trajectory, instead of taking the two-body orbit of the state after the burn without covariance: the miss
  distance and the Pc of every option change. The state of an option and its covariance are integrated with the mass of
  the trajectory at the burn less the fuel of the option, and that fuel is estimated from the mass at the burn
  ([#415](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/415)).
- **Avoidance options without a propagated covariance follow the force model of the participant.** When the covariance
  of the protected participant is not propagated (held in RTN, or none: Velocity-Verlet, no initial covariance,
  `CovariancePropagation = null`), `EvaluateAvoidance` propagates each option from the state after its burn with the
  force model of the participant's own propagation, in RK7(8) or by Velocity-Verlet, instead of taking the two-body orbit
  of that state, and the protected participant of each option carries its initial covariance held in RTN instead of none
  ([#402](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/402)): the miss distance, the TCA
  and the Pc of every such option change. On a LEO at 400 km with drag and geopotential, the two-body option was 4.0 to
  58.5 km off at its TCA, and an option ranked safe (miss 23.2 km, Pc 4.1e-6) passed at 89 m, Pc 3.0e-4. A participant
  not propagated, by a TLE or a SPICE kernel keeps the two-body orbit, now stated by the new
  `AvoidanceOption.Trajectory` (`AvoidanceTrajectory`). The secondary participant of the options keeps the resolution of
  its covariance of the encounter. A spacecraft keeps the force model of its last propagation by RK7(8) or
  Velocity-Verlet, without its steps (about 0.2 MB for one day in LEO), until it is propagated again. Cost: one
  propagation per option from its burn (the 36 options of the default grid on R2 over 8 h: 2.2 s in RK7(8), 5.5 s by
  Velocity-Verlet at 1 s, against about 0.4 s).

- **The propagator builder adds the central body.** `CentralBodyPropagatorBuilder.Build` adds the central body of the
  spacecraft (the observer of its initial orbital parameters, or the Sun for a star or a barycenter) to the perturbing
  bodies when they do not hold it. Before, a builder given the Sun and the Moon only, with `IncludeAtmosphericDrag()`,
  added no drag, and its solar radiation pressure ignored the shadow of the central body; both now apply. A builder that
  already listed the central body gives the same results, bit for bit. To keep the former behavior, build the
  propagator with the `CentralBodyPropagator` constructor and the same bodies, which is unchanged.
- **NRLMSISE-00 without allocation.** `Nrlmsise00Model.GetDensity` no longer allocates (4.2 to 5.4 kB per call
  before): the model keeps its work arrays and builds its working copies of the inputs as values. Its results are
  unchanged bit for bit, checked against the previous implementation on 3,240 inputs through every branch of the
  model and on the three public methods of `NRLMSISE00` (`Calculate`, `CalculateThermosphere`,
  `CalculateWithDrag`), which still return a new output. Atmospheric drag reads the density and the angular velocity
  of the body without allocating during a propagation: one day of LEO propagation with NRLMSISE-00 drag allocates
  7.8 MB instead of 45 MB, and 8.3 MB instead of 279 MB with the state transition matrix, in about the same time.
  `NRLMSISE00` instances, which already kept state between calls, are not thread-safe; `Nrlmsise00Model` serializes
  its calls, as before.
- **Covariance towards a rotating frame.** `ToFrame` now transforms a state covariance with the Jacobian of the
  state transformation, `J = [[R, 0], [-[(R ω)×] R, R]]`, instead of the block-diagonal rotation `diag(R, R)`.
  Towards ITRF93, TIRS or any body-fixed frame, the velocity covariance now includes the position uncertainty
  carried by the rotation of the frame (about 7 mm/s for 100 m of position uncertainty in an Earth-fixed frame); it
  used to be wrong by that amount. Between inertial frames the result is unchanged, apart from the symmetrization of
  the output. `Matrix.TransformCovariance` is documented as valid between inertial frames only, and the RTN rotation
  of the conjunction analysis and of the CDM export keeps its pure-rotation convention.

**Fixed**

- **The Mars standard atmosphere above 50 km.** `MarsStandardAtmosphere` took the temperature of the NASA Glenn curve
  fit, −23.4 − 0.00222 h °C, at every altitude: it reached 0 K at 112.5 km, so the density was infinite there and
  negative above, and it rose with the altitude from 101.4 km. Drag pushed every Mars orbit above 112.5 km of geodetic
  altitude along its velocity: one day in Velocity-Verlet raised a 150 km orbit by 312 km and a 200 km orbit by 17 km,
  through every propagation path and `astro propagate -a`. Above 49.887 km, where the curve fit reaches 139 K, the model
  is now the isothermal layer of Seiff (1982): the temperature held at 139 K and the hydrostatic pressure with the
  inverse-square gravity, reached over a transition of 5 km that keeps the density and its first two derivatives
  continuous, so RK7(8) with `IncludeInStepControl` crosses it. The density is positive and decreasing at every altitude,
  within a factor 2.23 to 2.81 of Seiff's nominal model from 50 to 100 km where the curve fit departed by up to 101, and
  drag opposes the relative velocity everywhere; `GetTemperature` and `GetPressure` follow. Unchanged bit for bit below
  49.887 km. Above about 100 km the layer extrapolates a 139 K mesosphere: the thermosphere is not modelled, so drag in
  orbit above about 120 km is underestimated (#431). No public API change.
- **The Earth standard atmosphere is the U.S. Standard Atmosphere 1976.** `EarthStandardAtmosphere`, documented and
  exported to the CDM (`ATMOSPHERIC_MODEL = US STANDARD ATMOSPHERE 1976`) as the U.S. Standard Atmosphere 1976, was the
  curve fit of the NASA Glenn Research Center, whose upper-stratosphere line ran from 25 km to every altitude: against
  the tables of the standard its density was 2.97 times theirs at 86 km, 80.1 at 140 km, 38.7 at 200 km, 2.28 at 400 km
  and 0.28 at 750 km, and its temperature 399 K at 86 km against 187 K. Every propagation with drag on this model,
  `astro propagate` included, used those densities (one day at 300 km, A = 10 m², m = 100 kg: re-entry within the day
  where the standard gives −18.3 km of semi-major axis). It is now the standard itself, NASA-TM-X-74335, from −5 to
  1000 km: below 86 km its seven layers in geopotential altitude, from 86 to 1000 km the number densities of N₂, O, O₂,
  Ar, He and H of its diffusion equations, integrated once when the type loads; the density within 4.2e-4 of its table I
  (the rounding of the table), the temperature and the pressure to its last digit. The slope of its density jumps at the
  base of each layer (+23.5 % at 11 km), where RK7(8) with `IncludeInStepControl` stopped: transitions of ±100 m keep
  the density and its first two derivatives continuous, within 2.1e-4 of the equations; the junction at 86 km, the
  switch of the mean molecular weight at 100 km and the entry of hydrogen at 150 km are smoothed the same way. Above
  1000 km, where the standard stops, ln ρ is continued with its slope (a scale height of 237 km); the density no longer
  stays constant above 779.67 km. `GetTemperature` gives the kinetic temperature and `GetPressure` the pressure of the
  standard. No public API change (#437).
- **The branches of the Mars standard atmosphere meet.** `MarsStandardAtmosphere` switched from one branch of the NASA
  Glenn curve fit to the next at once: its density rose by 0.41 % at 7 km, and RK7(8) stopped there (100 consecutive
  rejections) at every tolerance with `IncludeInStepControl`, and at 1e-12 and 1e-13 without it when the drag is strong.
  Over 500 m centred on 7 km, the temperature now goes from one branch to the next by a quintic smootherstep: the density
  and its first two derivatives are continuous, dρ/dh is in closed form, and RK7(8) completes, with and without the
  option, through every propagation path. Outside that interval the model is unchanged bit for bit; inside, the density
  stays within 0.20 % of the curve fit (#433). The same transitions joined the branches of the Earth curve fit at 11 and
  25 km, before that model became the U.S. Standard Atmosphere 1976 (#437). No public API change.
- **The step control of the variational equations through the SRP penumbra.** With `IncludeInStepControl`, RK7(8) took
  thousands of steps in the penumbra of the Earth: a Sun-synchronous LEO with SRP over one day (R3) took 23 967 steps
  at 1e-13, 21 596 of them in its 265 s of penumbra, and stopped at 1e-14 on the guard below. The SRP partials in the
  penumbra were the central differences of the acceleration, which divided the rounding of the shadow fraction (a few
  1e-10 at half shadow, a few 1e-8 at the contacts) by their step, a noise of 5.0e-7 to 1.6e-3 of the block that the
  step of Φ followed. They now take the analytic derivative of the shadow function of the GMAT Mathematical
  Specification, R2026a, §4.2.5, in a form without cancellation, and the shadow regime from the angles of the discs:
  R3 takes 1 692, 2 247, 3 017 and 4 757 steps at 1e-11 to 1e-14 with the option (748 to 1 821 without it), R4 (GEO
  in eclipse season) 458 at 1e-14 instead of 2 402. The partials agree with Ridders' derivative of the acceleration
  within its resolution across the penumbra (2.0e-8 at half shadow in LEO, against 9.4e-7), and a call costs 11 µs
  instead of 73 µs (Linux, without the ephemeris cache). Without the option the trajectories do not change, bit for bit; Φ and Ψ move only through the
  steps that touch the penumbra (R3 at 1e-13 after one day: Φ by 2.6e-9, Ψ_Cr by 8.7e-9). The step counts after #434 and
  #410 are in [Covariance Propagation](../reference/covariance-propagation.md#options). See
  [Covariance Propagation](../reference/covariance-propagation.md)
  ([#423](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/423)).
- **The shadow fraction near the contacts of the penumbra.** `CelestialItem.ShadowFraction` computed the two-circle
  area in a closed form that cancels near the contacts: against the exact value, its error grew from up to 4e-9 across
  the penumbra in LEO to 1.7e-4 within 1e-12 of its width of a contact, and it returned values below 0 or above 1 on
  states inside the penumbra (about 350 of 16 000 within 15 m of the contacts of a Sun-synchronous LEO), or NaN where
  an acos argument passed 1 by rounding, which SRP took as sunlight. It now sums the two circular segments cut by the
  common chord, with Heron's half chord in the form of Kahan and the half angles from atan2: within 2.2e-16 of the exact
  value of its inputs everywhere, and always in [0, 1] for finite inputs, as documented; the SRP partials take ν and its
  gradient from the same geometry. Outside the penumbra the results do not change, bit for bit, and neither do the
  Velocity-Verlet trajectories measured (the change of the acceleration is below the precision of the velocity). The SRP
  acceleration in the penumbra changes by the former rounding, a few 1e-9 of the unshadowed acceleration away from the
  contacts and up to 1.9e-5 within a millimetre of them; RK7(8) trajectories that cross a penumbra move by the
  integration noise of the steps this changes: 2.8 mm over a day of a Sun-synchronous LEO at 1e-13, 0.41 mm over the
  3 h of the LEO golden with drag and SRP, recaptured (the other goldens bit for bit). A call in the penumbra costs
  82 ns instead of 37 ns. No public API change
  ([#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434)).
- **The geopotential of EGM2008 with its own constants.** The coefficients of a model are normalized to its GM and
  reference radius, which the coefficient file does not carry; the harmonics were evaluated with the GM and the
  equatorial radius of the SPICE kernels (3.98600435507e14 m³/s² of `gm_de440.tpc`, 6378136.6 m of `pck00011.tpc`)
  instead of those of EGM2008 (3.986004415e14 m³/s², 6378136.3 m; Pavlis et al., 2012): 7.9e-8 of the harmonics at
  degree 2 (9.5e-10 m/s² in LEO at 400 km), about 4.7e-8 more per degree, in the acceleration and in its partials alike.
  They now take the constants of EGM2008 for the Earth, and the central term keeps the GM of the kernels, TDB-compatible
  like the propagation (EGM2008's is TT-compatible, and within 2.3e-10 of DE440's once scaled; with it, a LEO would move
  by 20 m a day). Against the closed form of the potential of degree 2 with EGM2008's constants, the field agrees to
  2.8e-14 to 1.0e-12 of the harmonics (7.9e-8 before). Every propagation around the Earth with a geopotential moves, with
  either integrator: over one day, 7.8 cm in LEO at 400 km (10×10, drag), 16.6 cm on a Sun-synchronous orbit at 700 km,
  1.6 mm in GEO (70×70), 43 cm over two revolutions of a 300 × 36 000 km orbit; the three RK7(8) goldens by 13.4 mm,
  1.6 mm and 24.6 mm, recaptured; the conformance cases 001 to 003 stay within their tolerances (their residuals against
  the GMAT goldens change by 0.15 m at most). Bodies without a geopotential, and another body's geopotential (its kernel
  constants, as before), do not change, bit for bit. The CDM names the field `EGM2008: nD nO`. The comparison with
  GMAT gives GMAT the same field, through a `.cof` whose coefficients are scaled to the kernel GM of its header
  ([#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410)).
- **The step control of the variational equations in dense air.** With `IncludeInStepControl`, RK7(8) took ever
  smaller steps as the density grew and never failed: the US Standard Atmosphere 1976 at 200 km took 107 290 steps over
  2 min at 1e-11, and at 1e-12 and 1e-13 the propagation did not finish (memory grew to an `OutOfMemoryException` over
  1 h); NRLMSISE-00 at 400 km, 6 362 steps at 1e-13, against 2 without the option. The cause was the rounding noise of
  the density gradient of the drag partials (about 4e-10 of ∇ρ, from the geodetic conversion of each perturbed position
  of the Cartesian differences), which the embedded error estimate of Φ cannot cancel. The gradient is now taken in
  geodetic coordinates: dρ/dh in closed form for the US 1976 and Mars atmospheres, and for NRLMSISE-00 the derivatives
  of the model in its own inputs (fourth order in altitude), within 1.5e-12 to 8.2e-12 of Ridders' reference instead of
  3.3e-10 to 9.7e-10. The same cases take 6 to 10 steps (US 1976, 200 km, 1e-11 to 1e-13, with the U.S. Standard Atmosphere 1976 of #437) and 6 to 321 steps
  (NRLMSISE-00, 200 to 400 km). A day of LEO with NRLMSISE-00 drag and Ψ_Cd takes 15 % to 22 % more time without the
  option (1e-12 to 1e-13, the same steps), and with it 3.3 s instead of 61 s at 1e-13. Φ, Ψ and P with drag change by the change of the gradient; trajectories without the
  option are unchanged. A collapse of the step from another cause now stops with an `InvalidOperationException` that
  names the option and the remedies, after 10 000 consecutive steps within 100 s of flight, instead of exhausting the
  memory; it applies with `IncludeInStepControl` only. See [Covariance Propagation](../reference/covariance-propagation.md).
- **Avoidance with the protected participant propagated with Φ in the step control.** `EvaluateAvoidance` threw an
  `ArgumentException` when the protected participant was propagated with the covariance and `IncludeInStepControl`
  and the secondary participant had an initial covariance but no propagated one: the replay of its variational
  equations took the options of the protected solution, the option included, which the replay rejects. It now
  takes them without it, as the analysis of each option already did; the covariance of the secondary participant at
  the TCA of each option is that of its propagation with the covariance, bit for bit. See
  [Avoidance](../reference/avoidance.md).
- **Geodetic coordinates above and below the surface.** `Planetocentric.ToPlanetodetic` took the geodetic latitude
  from the relation of the surface, tan φ = tan φc / (1 − f)², at any altitude, and the altitude from that latitude:
  above the surface the latitude came out too large by about f sin 2φ · h / (a + h). On the Earth the altitude was up
  to 5.2 km off between 100 and 800 km (1.7 km at 400 km and 51.5°), 234 km at GEO, and 33 m within 5 km of the
  surface; drag read its density at that altitude, up to 2.8 % too low in LEO at mid-latitudes. The conversion is now
  the exact closed form of Vermeille (2002) with the changes of Karney (2011), for oblate, spherical and prolate bodies,
  and agrees with CSPICE `recgeo_c` to 1.0e-15 rad and 5.7e-16 of the altitude scale; drag takes it directly from the
  body-fixed position. Every propagation with drag changes: 118 m after 3 h on the LEO case with drag and SRP of the
  golden tests. The geodetic coordinates of a site read from a kernel, and of a state (the coordinate constraints of the
  geometry finder, the `ephemeris` command of the CLI) change with it: DSS-13 moves from 1073.24 m and 35.24719501° to
  1070.85 m and 35.24716449°, its topocentric frame by 3.05e-5° in latitude, its azimuths and elevations by up to
  4e-5°. [#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411)
- **Custom sites above the surface and on other bodies than the Earth.** `Planetodetic.ToPlanetocentric`, which places
  every site given by geodetic coordinates, returned the planetocentric latitude of the surface point below the site
  with the radius of the site itself, and took N with the WGS84 e² (0.00669437999014) whatever the body. A site at 1 km
  of altitude on the Earth was 1.1 to 3.4 m away from its place (16.8 m at 5 km, 1.6 cm at most at the surface); on
  Mars and the Moon every site was kilometres away (8.3 km and 5.7 km). The conversion is now the exact closed form
  with the e² of the body, and agrees with CSPICE `georec_c` to 4.4e-16 of the distance; the ephemeris, horizontal
  coordinates, illumination angles and tracking data of custom sites change with it.
  [#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411)
- **NRLMSISE-00 in universal time.** `Nrlmsise00Model` read the day of year and the seconds of the day from the calendar
  of the epoch in its own time scale and gave them to the model as UT: a propagation, whose epochs are TDB, ran the
  model 69.2 s late (in 2021), up to 2.8e-3 on the density at 400 km. The epoch is now converted to UTC, and the seconds
  of the day are taken to the 100 ns tick instead of the millisecond. An epoch given in UTC gives the same density, to
  the rounding of the seconds of the day; the LEO case with drag and SRP of the golden tests moves by 1.06 m after 3 h on
  top of the geodetic fix. UT1 is not used: UTC stands for it, and the 0.9 s that UT1 − UTC can reach move the density
  by 3.6e-5 at most at 400 km. [#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409)
- **Drag partials across the altitude cutoffs of NRLMSISE-00.** NRLMSISE-00 drops the mixing contribution of a species
  above 160, 200, 240, 250, 300, 320 and 450 km (flag 15, as in its reference implementation), so its density jumps
  there, by up to 1.8e-4. The density gradient of the drag partials, a central difference over about 4 m, added that
  jump divided by its step whenever its stencil straddled one of these altitudes: the gradient was off by up to 230 %,
  and RK7(8) with the state transition matrix in the step control (`IncludeInStepControl`) could stop after 100
  rejections at its minimum step. There the gradient is now the one-sided difference on the side of the state, the
  gradient of the model on that side (within 3.2e-8 of Ridders' derivative); elsewhere it is unchanged, bit for bit.
  The trajectories do not depend on the partials and are unchanged.
  [#413](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/413)
- **Drag with the U.S. Standard Atmosphere 1976 or the Mars standard atmosphere without allocation.** Atmospheric drag
  built an atmospheric context (80 bytes) for each density with `EarthStandardAtmosphere` and
  `MarsStandardAtmosphere`, once for the acceleration and eight times for its partial derivatives: 720 bytes per
  integration stage with the state transition matrix. It now reads their density from the altitude, as for
  NRLMSISE-00, and allocates nothing per stage. One day of LEO propagation with US 1976 drag in RK7(8) allocates
  0.87 MB instead of 7.3 MB with the state transition matrix, and 0.37 MB instead of 1.2 MB without it, in about the
  same time; Mars the same. Results unchanged, bit for bit. A class derived from either model that re-implements
  `IAtmosphericModel` is not used by drag, as for `Nrlmsise00Model`.
  [#404](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/404)
- **True anomaly of a near-circular, slightly inclined state vector, and the start of a propagation.** A
  `StateVector` on a circular orbit (e < 1e-3) inclined by less than 1e-3 rad counted its true anomaly from the x
  axis, which lies in the orbit plane only if the orbit is equatorial or its node is on it. Its mean anomaly, its
  Keplerian elements and every state derived from them (`AtEpoch`, `ToStateVector(Time)`, `TrueAnomaly(Time)`) were
  moved along the orbit: a GEO state inclined by 0.05° with z moved by 1 km came back 0.38 m away at its own epoch, and
  the map was 2.6 away from the identity in its derivative. The true anomaly of every circular orbit is now counted
  from the node, as it already was above 1e-3 rad, and agrees with SPICE OSCELT to 8.9e-16 rad on the mean longitude;
  the round trip returns the state to 1.4e-15 of its radius. Orbits with e ≥ 1e-3 or i ≥ 1e-3 rad are unchanged, bit
  for bit. `CentralBodyPropagator` now takes a state vector given at the start of its window as it is, instead of
  through the Keplerian elements, so a propagation starts from exactly that state: the start used to move by about
  1e-9 m (the rounding of the round trip), which changes the steps of an adaptive propagation, by 6.0 mm after 3 h on
  the LEO case with drag and SRP of the golden tests. Other orbital parameters, and a state vector at another epoch,
  still go through `AtEpoch`. [#398](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/398)
- **Loaded kernels while another thread loads.** `SpiceAPI.GetLoadedKernels` returned its internal list, outside the
  lock of `SpiceAPI`: enumerating it while another thread loaded or unloaded kernels could throw
  `InvalidOperationException` ("Collection was modified"). It now returns a copy taken under the lock, the kernels
  loaded at the call.
- **Kernels of a directory.** `SpiceAPI.LoadKernels` on a directory now hands SPICE its kernel files only: binary
  kernels (ID word `DAF/` or `DAS/`, or `NAIF/DAF` and `NAIF/DAS`) and text kernels (ID word `KPL/`, or a
  `\begindata` block for an old kernel without ID word), in the order of the directory listing, as before. The other
  files, from which SPICE loaded nothing, are left out: the native loader used to open the EGM2008 geopotential model
  of the solar system directory, which on Windows could make the library fail to open it ("being used by another
  process"). What SPICE loads is unchanged. A file path is still handed to SPICE as it is.
  Symbolic links are followed; an entry without content (a dangling link, a named pipe, an empty file) is skipped,
  as the native loader skipped what was not a regular file.

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
  that shares a `CelestialBody` with a geopotential. Solutions that share a central body without a geopotential may be
  queried concurrently (see the entry on the orientation cache below).
- **TCA of the conjunction analysis of two propagated trajectories (#363).** The polynomial search of the
  `PropagationSolution` overloads located the TCA on the cubic Hermite interpolation of the positions. Each TCA is now
  refined by bisection of the range rate on the states of the trajectories. On RK7(8) trajectories at the default
  tolerance, the TCA moves from 2.6e-6 s to 1.3e-4 s away from the Keplerian reference to less than 1e-7 s; the
  refinement itself changes the miss distance at second order only (by up to 2.8 mm in the test cases). Between
  versions, however, the miss distance, and therefore the collision probability, on RK7(8) trajectories moves by up to
  the error of the former cubic Hermite interpolation, since the states it is computed on now come from the shortened
  RK7(8) step: metres to tens of metres in LEO (2.76 m and 63.6 m in two LEO cases with EGM2008 10×10, the Moon and the
  Sun over one day), tens of metres in GEO. The local minima are selected on the polynomials and only refined on the
  states, with secants checked by a bracket before the bisection: on two 400 km LEO trajectories over one day with
  EGM2008 10×10, the Moon and the Sun, `Analyze` takes 15 to 20 ms, against 8 to 13 ms before and 190 to 200 ms with a
  first version of the refinement that selected the minima on the states and bisected over whole steps (Release,
  median of 21 calls). The bisection now stops once its bracket is one tick (100 ns) wide, the resolution of the
  epochs: with the default `BisectionToleranceSeconds` of 1e-9 s it used to repeat the same evaluation up to its 80
  iterations; the sampled search of the other overloads gains the same, with unchanged results.
- **Orientation cache of the central body frame shared between propagations.** A propagation put its orientation
  cache on the `Frame` of the central body when the propagator was built and removed it at the end of the
  propagation, and each RK7(8) evaluation of a solution put it back for its duration. Every user of that body saw it:
  two propagations or two RK7(8) solutions sharing an Earth, run or queried from two threads (`BatchPropagator`,
  `Scenario.SimulateAsync`, `Parallel.ForEach`), read each other's cache, so their states were off by tens of metres
  (21.7 m and 74.9 m in a reproduction with drag), and the frame could keep a cache whose window had ended, with every
  later Earth-fixed orientation frozen at its edge (61° off a month later). Without any thread, a propagator built and
  propagated after another one was built on the same body ran with the cache of the other. The cache is now kept by the
  propagator and active only on the thread that propagates or evaluates, for that propagation's frame; the shared
  `Frame` is never changed. Results are those of a propagation alone, bit for bit, and a `CelestialBody` without a
  geopotential can be shared between concurrent propagations and evaluations; one with a geopotential still cannot.
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
- **Orbital elements of a state vector** ([#368](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/368)).
  `StateVector` computed its inclination, node, argument of periapsis and true anomaly with `acos`, which loses about
  half the digits near 0 and π, and reflected the argument of periapsis on the sign of `e_z`, which is zero for an
  equatorial orbit. Every conversion of a state to elements went through it, and so did every state at another epoch
  (`AtEpoch`, `ToStateVector(epoch)`), including the initial state of a numerical propagation and the states of an
  unpropagated spacecraft. An equatorial orbit with its periapsis in the lower half-plane had its argument of periapsis
  mirrored: in a conjunction test, a spacecraft was put 3 609 km away from its given state. Near an apsis of an
  eccentric orbit the round trip moved the state by up to 1 cm (0.1 m after a burn at perigee), and a state converted
  to itself by 0.72 m at the scale of the Earth orbit. The angles now come from `atan2` and agree with SPICE OSCELT to a
  few 1e-15 rad; the round trip is at rounding. An orbit within 1e-10 rad of the equator is equatorial, as in SPICE:
  its inclination is 0 or π, its node 0, and its argument of periapsis is measured from the x axis. Numerical
  propagations that start from a state vector change at the rounding level (1.4 µm at most in the golden of
  `PropagationGoldenTests`, recaptured), and by much more for an equatorial orbit or a start near an apsis.
- **Parabolic and hyperbolic orbits** ([#368](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/368)).
  The parabolic anomaly from the true anomaly was `ν/2 - sinh(ν/2)` instead of Barker's `D = tan(ν/2)`, its inverse
  solved `M = D/2 + D³/6` instead of Barker's equation `M = D + D³/3`, and the parabolic mean motion was
  `2 √(μ/q³)` instead of `√(μ/(2q³))`: a parabolic state and its elements did not correspond, except at the periapsis.
  They now follow Barker's equation, solved in closed form, and agree with its trigonometric solution to a few 1e-15.
  On parabolic and hyperbolic orbits the mean anomaly is no longer reduced modulo 2π (`KeplerianElements`, `AtEpoch`,
  `ToKeplerianElements`, `EpochAtMeanAnomaly`): it is negative before the periapsis and grows without bound after
  it, as in SPICE OSCELT, where it used to put a state before the periapsis after it. The hyperbolic Kepler solver
  starts from `asinh(M/e)` and converges for a negative mean anomaly, where its former guess was NaN. Hyperbolic
  states and elements agree with SPICE OSCELT to 2e-13 relative.
- **SPICE elements of a parabolic state** ([#373](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/373)).
  `SpiceAPI.ConvertStateVectorToConicOrbitalElement` threw `ArgumentException` on a parabolic state: SPICE gives a
  parabola its perifocal distance and no semi-major axis, which `KeplerianElements` requires to be infinite. The
  elements now have an infinite semi-major axis and the perifocal distance of SPICE, and agree with the given
  parabolic elements to 1.5e-15 (the mean anomaly relative to 1 + |M|: SPICE and the library follow the same
  Barker's equation); back to a state, they give the state again within 8e-16 relative.
- **SPICE state of an open orbit at the epoch of its elements** ([#372](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/372)).
  `SpiceAPI.ConvertConicElementsToStateVector(KeplerianElements)`, the overload without an epoch, never returned on
  some hyperbolas (before the periapsis, for instance M = -2 at e = 1.5): the native conic elements solved the
  elliptic Kepler equation when they were built, by an iteration that does not converge for e > 1, and the call held
  the SPICE lock, so every other SPICE call of the process waited. Parabolic and hyperbolic elements now go to SPICE
  CONICS at the epoch of the elements, as the overloads with an epoch already did; SPICE OSCELT finds the given
  elements in the state to 2.3e-14. Elliptic elements are unchanged, and no code of the library or the CLI calls this
  overload.
- **A maneuver whose precondition fails no longer hangs the propagation** ([#355](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/355)).
  When an event handler returned `EventAction.Continue`, which `ManeuverEventDetector` does when the precondition of
  its maneuver fails (`CombinedManeuver`, whose apogee must lie near the line of nodes), the propagator ignored it and
  integrated the same segment again, without end, adding a segment to the solution each time: with RK7(8) the
  propagation was still running after 20 s with 431 MB allocated, and with Velocity-Verlet it ran out of memory in
  about 1 s. `Continue` now does what its documentation says: the propagation goes on from the event with the state
  unchanged and the detector of `NextDetector`, and the occurrence just handled is not detected again. The maneuver
  fires at the first later occurrence whose precondition holds (for instance once the perturbations have turned the
  line of apsides towards the nodes); if none does within the window, the propagation ends normally and the maneuver is
  not executed: its `ThrustWindow` stays null and it remains the standby maneuver. The skipped occurrences split the
  solution into segments, with the state transition matrix continuous across them. With RK7(8) the event time can
  fall just before the root, on the 100 ns grid of the epochs, so the next segment starts the detector from its value
  past that root, the rule Hipparchus applies to a zero at the start of a step; Velocity-Verlet locates the event at
  the end of a step, already past the root. Propagations without `Continue` are unchanged bit for bit.
- **SRP, albedo and thermal pressure independent of the observer of the state** ([#360](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/360)).
  The three forces read the positions of the Sun and of the occulting, reflecting or emitting bodies with the light
  time from the observer of the state, not from the spacecraft. Relative to the Sun, the central body of a
  heliocentric propagation, the Earth was moved by its velocity times about 499 s, some 15,000 km: for the same
  physical state 7000 km from the Earth, SRP in the umbra was 6.9e-7 m/s² instead of zero, and the albedo and thermal
  accelerations were off by 90 % to 96 %. The forces now read geometric positions (`Aberration.None`), as the
  cannonball model of Montenbruck and Gill does, and give the same acceleration relative to the Earth and relative to
  the Sun, to 3.8e-12 relative (1.75e-10 in the penumbra, where the shadow varies over about 65 km). The light time and
  the aberration from the spacecraft, of order v/c, are neglected: the shadow axis of the Earth is off by the annual
  aberration, about 20", some 680 m at 7000 km, or about 0.1 s on an eclipse entry in LEO. Planet-centred propagations
  with these forces change slightly: the Sun seen from the Earth moves by its own motion over the light time, about
  6 km; the golden SRP case of `PropagationGoldenTests` moved by 1.7 mm and 1.8e-6 m/s over 3 h, with the same steps
  (recaptured). The ephemeris cache of `CentralBodyPropagator` no longer holds light-time positions, which no force
  reads any more: it is built in about 40 % of the time (11 ms instead of 27 ms per day for the Moon and the Sun).
- **Conformance runner: the end of the window only, and LFS pointers as errors** ([#426](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/426),
  [#425](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/425)). The `propagator` and `stm`
  solvers asked for an output state every `step_size_s` (1 s in the suite) and read only the end of the window; since
  #363 each output inside an RK7(8) step costs a shortened step, so the GEO 70x70 case took 58 s instead of 1.1 s and
  the suite 107 s instead of 45 s. They now ask for the end of the window only, with the same results, bit for bit. A
  data file of the suite left as a Git LFS pointer (a clone without `git lfs pull`) made its case a FAIL, as if the
  library were wrong; it is now an ERROR that names the file. The library is unchanged.

**Known limitations**

Open issues, to be handled after the 11.0.0 release.

- **SSB mode in low orbit** ([#389](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/389)). An RK7(8) propagation of a LEO state in SSB mode (central body
  the Sun) takes about 5 500 times more steps than relative to the Earth (125 641 steps against 23 for one hour at
  1e-10), and fails at 1e-12, with or without the covariance propagation.
- **Threshold of the non-linearity index** ([#403](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/403)). The threshold of 1.0 is measured on two-body
  crossing encounters only, where Foster agreed with the Monte Carlo in all 11 cases up to ν = 1.42. Slow encounters,
  drag and radial miss offsets are not measured, so the index stays off by default.
- **No Martian thermosphere** ([#431](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/431)). Above Seiff's 100 km, `MarsStandardAtmosphere` extends its
  isothermal layer at 139 K: at 400 km its density is 4.6e7 times below the Mars-GRAM sample (2.6e-15 kg/m³), so drag
  in Mars orbit above about 120 km is underestimated by orders of magnitude.
- **Kelvin offset of the Mars curve fit** ([#438](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/438)). `MarsStandardAtmosphere` takes 273.15 where the NASA
  Glenn curve fit takes 273.1, which moves its density by about 0.02 %. The Earth model, the U.S. Standard Atmosphere
  1976 since [#437](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/437), is not concerned.
- **Velocity-Verlet with drag** ([#439](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/439)). The default integrator is of first order with drag: on a dense
  pass its error against RK7(8) at 1e-13 is 267 m, 27.2 m and 2.72 m at steps of 1, 0.1 and 0.01 s (1.10 m, 1.10e-2 m
  and 1.12e-4 m without drag). Use RK7(8) when drag matters.
- **No bound on the remediation of a covariance** ([#443](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/443)). The eigenvalue clipping of the conjunction
  analysis and of `StateVector.WithRemediatedCovariance()` remediates any indefinite covariance that passes the other
  checks, however deep the defect: a correlation of −10 is accepted, with a largest relative change of 5.5.
- **RK7(8) across shadow passages** ([#445](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/445)). At a tolerance of 1e-11, the global error over 3 h in LEO
  with drag and SRP spans 0.94 to 36.2 mm with the placement of the steps around the eclipse: the tolerance bounds the
  global error only to within a factor of about 40.
- **Memory of the conjunction analysis** ([#447](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/447)). `ConjunctionAssessment.Analyze` allocates about
  403 MB per pair over a 2 h window (0.83 s with the covariance held in RTN, 1.71 s propagated), from the sampling of
  the trajectories during the search of TCA. Results are unaffected.

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
