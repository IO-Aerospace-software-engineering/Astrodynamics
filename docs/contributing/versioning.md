---
title: Versioning
---

# Versioning

Current version information and release notes since 8.0. Breaking changes are called out in each release.

## Current Versions

| Component | Version |
|-----------|---------|
| NuGet (`IO.Astrodynamics`) | `10.2.0-preview-1` |
| CLI tool (`IO.Astrodynamics.CLI`) | `10.2.0-preview-1` |
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

### 11.0.0 (in development)

Changes since 10.2.0-preview-1. Entries are added as they merge.

A major version, because it breaks the API: members deprecated for removal in 11.0 are removed, each listed under
**Removed** as it merges.

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
  state agrees to 3.3e-8 m and 2.3e-12 m/s. Decision 18 of #457.
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
  the 200 000 evaluations and returned a Pc 1.3e-6 off, with no flag (PC-1 of the lot's review). The public
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
  included, only seeding the search (S1 of the lot's review): the Hermite misses the zero by its error (#363, 12 to
  212 µs at the default steps of `new RK78Integrator()` on this crossing), within which a window ending before a minimum
  listed its unrefined Hermite epoch, a point still closing (as before 11.0.0), and a window starting before one listed
  nothing (an intermediate state of this change). A window end is now bracketed when the range rate says the minimum
  lies inside, and a minimum whose range-rate zero lies outside the window is dropped. A window end is never listed,
  not even a zero of the range rate exactly there, which `Analyze` returns as that end. A window whose candidates all lie within the flatness tolerance of the seeds (a slow pass, a
  window of a few ticks about a minimum) listed nothing and `Analyze` returned its midpoint, although the range rate
  went from negative to positive inside (F1 of the lot's second review: 1 ms off on a pass at 0.113 m/s and 50 m); the
  zero is now bisected over the window, and `Analyze` on this path never returns the midpoint. Against `gfdist_c` on
  windows ending or starting 1 µs to 1 ms before or after each of the four minima, at 60 s and at the default steps,
  and on windows of 2 and 3 ticks on each side of each minimum at 60 s steps, the same minima within the bounds above
  (9.0e-8 s and 2.7e-7 m at the default steps, their states against their SPK of 60 s outputs). A seed that the refinement could not
  bracket on the range rate within two moves of its bracket was kept unrefined: on a slow pass at the default steps
  (50 m along-track, a drift of 100 m across the plane), the Hermite seeded minima near the maxima of the distance, which
  `AnalyzeAll` listed (t0 + 4246.66 s, 111.79 m, a range rate of +0.0012 m/s, where `gfdist_c` lists no local minimum)
  and the dense `Analyze` returned instead of the closer end, 53.06 m against 111.79 m (main returned that end); on a
  pass with two minima per orbit at the default steps, a seed 1.6 s past its minimum was listed unrefined, 1.35e-4 m off. A seed the secants
  cannot bracket is now searched on its own range rate, from the seed toward the side it points to (1 ms, then distances
  doubling up to 1.024 s and 1 s apart after that, up to the neighbouring candidate: an interval of the other sign 1 s
  wide or more holds a read, where distances doubling all the way stepped over a shallow minimum next to a fold of the
  range rate, 2.9e-7 m deep and positive over 1.6 s, lost in 204 to 229 windows of each propagation of the review's fifth
  probe while main listed its seed unrefined, 4.85 s off at 60 s steps), then between the candidates, moved twice at
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
  a value of the undefined state: 0 for Mars and Jupiter in the review's probe); a read of its position throws. The orientation cache of a propagation's central
  body (`PropagationFrameOrientationCache`) keeps a grid point SPICE cannot give as unavailable, so a point-mass
  propagation about the Earth still runs past the Earth's PCK, and a geopotential or drag there throws (they used the
  zero rotation before). A window that ends (or starts) inside the coverage, its next grid point outside, no longer
  throws for that point: the point after the window end is replaced by the orientation read there, and a read the grid
  cannot interpolate reads the frame at its epoch, so only an epoch SPICE does not cover throws, with SPICE's message (a
  propagation up to the last instant of `earth_latest_high_prec.bpc`, or 30 s before it, threw at its last minute; KR-1
  of the lot's review). A propagation still reads past its window end when its output step does not divide the window:
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
    before, 003 and 005 returned one tick early), asserted at 6.3e-10 (1.9e-8 before). Against `gfdist_c` on the
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
  CSPICE searches of `SpiceAPI` at an end of the search window is that end (the second review:
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
    ticks of its SPK carry the epoch asked (10 036 of 20 000 and 1 441 of 3 000 did not on main, 3 267 and 474 at the
    first round of the fix), and 201 `gfdist` searches of 2024 give back their search window tick for tick (28 of 300
    ends past it and 22 starts before it at the first round, 146 starts before it on main).
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
    Three bounds of the tests measured again (Linux x64, to be confirmed on the three platforms): the grazing fields of
    the managed field-of-view search (801 ELLIPSOID LT on its kernels 1.20e-3 s, an ill-conditioned edge that the same
    search moves by 9.6e-4 s between the propagated states and their kernels; 802 LT 6.7e-6 s, LT+S 8.84e-5 s), the miss
    of a conjunction read through stored states (3.12e-9 m, a few ulps of a LEO position), and a Velocity-Verlet output a
    tick before an impulse, which reads the earlier arc, whose closing state carries the label of the impulse, the
    nearest tick of its instant: half a tick of motion at most (3.96e-4 m at 8.6 km/s). The noise of the central
    differences of the F1 validation is drawn again (run 42945251492 of `long-validation.yml`), so the full degree of
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
  a radius not yet reached (EpochAtMeanAnomaly adds a period only for e ≤ 1 − 1e-6); pre-existing, for its own issue.
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
  before, bit for bit, on the 149 scenarios of the probe of #628 and of its review (141 837 windows: every list of
  `AnalyzeAll`, every result of `Analyze` and the SPKs), at its cost (16.5 to 21.5 ms against 16.8 to 18.9 ms on the day
  below, the same 31 minima), and every TCA it lists is a minimum of the states (inside the window, in order, none twice;
  new tests `Dense_ByDefault_*` against CSPICE), but a minimum no seed reaches may be lost: `Analyze` then returns another
  minimum, 1.7 mm to 9.6 cm farther (median 3.5 cm, 4039 of the 141 837 windows), or a window end (1275 windows: farther
  in 845, by up to 1.46 m, median 4.5 cm; closer in 430, by up to 1.36 m, an end being no close approach). With
  `ConjunctionAnalysisOptions.ReadRangeRateOverWindow = true` (under Added), the results of the review's last commit,
  bit for bit on the same windows: the range rate is also read at every candidate and, between two candidates more
  than `MaximumEventSearchStep` (60 s by default) apart, at the fewest epochs evenly spaced that leave no gap wider by
  more than one tick (about the Earth a step of 60 TT seconds may span 60 s and one tick of its TDB labels), as the
  sampled search does, and each change from negative to positive between two consecutive reads that holds no TCA
  yet is bisected to the tick nearer its zero, one TCA per pair of reads. The search from a seed still reads 1 s apart
  up to its neighbouring candidate: capped at `MaximumEventSearchStep`, it read 29 to 33 % fewer states at fixed 600 s
  steps for one trajectory, but the reads up to 60 s apart then stepped over a positive interval of 2.3 to 43 s lying
  more than 60 s past its seed, lost in 21 to 135 of 843 windows (review of #628). Each epoch is read once, with the
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
    y0 = 1496 and 1497 m, from 309 and 341 to 1 and 152 of 948 (not changed, below). Over the 81 scenarios of the
    probe of #628 and of its review, no TCA listed by default moves but at the level of noise below, and no entry is
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
    which is not read at its middle), and those at the candidates take the states read per window of the probe of #628
    from 54 to 78 on average; at fixed 600 s steps for one trajectory and the default for the other, 469 to 554
    against 481 to 564.
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
  native frame, up to 23″ from `ToFrame`. A coordinate search in a managed frame is not mapped.
- **A failed SGP4 returned a zero state; TEME outside the PCK a zero rotation** ([#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454), the TLE and TEME paths).
  `SpiceAPI.ConvertTleToStateVector`, under `TLE.ToOsculating` and `ToStateVector`, throws `InvalidOperationException`
  naming the TLE and the epoch when the native layer returns r = 0 (a decayed or suborbital orbit, an eccentricity out
  of range); no valid state has r = 0. The native `TLE::ToStateVector` left its state unwritten when `evsgp4_c`
  signalled, so the state returned was what the stack held: on Linux zero by chance (28872 of Vallado's set gave
  r = (0, 0, 0), v = (0, 6.95e-307, 6.95e-307)), on Windows x64 and macOS arm64 a non-zero state, possibly that of an
  earlier call. It now returns a zero state then, a change of the C++ layer (decision of S. Guillet, 2026-11-08; the
  other change of 11.0.0 is the ephemeris read of #454, below). TEME ↔ ICRF reads no kernel;
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
  epoch.** A field without harmonics (degree 0 or 1; the models start at degree 2) rotated the position to the
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
    built directly, which does not start the chain again (its own issue; under **Known limitations** if still open
    at the release).
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
  ([#554](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/554), case M). The attitude, executed at
  the first impulse's event, set the gate of the next impulse from that event, inside the first impulse's window (590 s inside its hold for a
  hold of 600 s): on 240b000a RK7(8) fired the second apogee raise at the first one's event, S + 2 963.1895589 s to the
  printed digits, 7 230 s before the perigee it is now gated to. It now fires at
  the first perigee after the end of the first impulse's maneuver window plus the attitude's hold: S + 9 528.4908671 s,
  within 1.6e-8 s of the Keplerian closed form, with a hold of 0 s or 600 s on the first; Velocity-Verlet at its step end
  after it (#577's F5 now holds for a hold of 0 s in RK7(8) too).
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
    seconds about the Earth, below).

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
  thrust window, 9.3e-5 rad over a partial step of 34.3 s, below); reads before, during and after a propagation leave the record and the CK identical. See
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

**Changed**

- **A kernel-backed spacecraft's initial orbital parameters are about the Earth** ([#620](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/620), Fixed above): its
  `InitialOrbitalParameters.Observer` is the Earth unless `centerOfMotion` says otherwise (the SSB before), so the
  observer of a conjunction analysis it protects, and of anything else that reads that centre, is the Earth. Pass
  `centerOfMotion: new Barycenter(0)` for the former state; the kernels must chain the spacecraft to the centre.
- **Reads outside the coverage throw** ([#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454), [#527](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/527), Fixed above): an ephemeris read or a frame
  transformation that SPICE cannot give, and a conjunction window past a propagated spacecraft's states, throw instead
  of returning an undefined state, the zero rotation or an extrapolation. A kernel-backed spacecraft built for its CK
  alone, without an SPK that covers its epoch, now throws at construction: its initial state cannot be read. A planet or
  a moon the loaded kernels do not chain to its system barycenter has `InitialOrbitalParameters` null, and its
  `SphereOfInfluence` is then +Infinity, as for a body built from custom parameters.
- **`AnalyzeAll` on two propagations no longer lists the window ends** ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621), Fixed above), and returns an
  empty list for a window without an interior minimum.
- **Every TEME ↔ ICRF result moves** with the managed TEME ([#451](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/451), Fixed above), by 0.3″ to 23.3″ over 2000–2026
  (the angle between the former TEME and the definition; 2.0″ to 23.2″ at the 13 epochs of `tools/teme_reference`):
  the ICRF states of a TLE (`TLE.ToOsculating(...).ToFrame(Frame.ICRF)`, 326 to 428 m on the three TLEs of the TEME
  study, 82 to 851 m on the verification TLEs), a TLE fitted from ICRF (`TLE.Create`, `ToTLE`, about 168 m), OPM and
  OMM in TEME, the CLI `--frame TEME`, `TLEPropagator` once converted, and the TLE participants of scenarios and
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
    except the burns the gates above move (F3 by an orbit, M, the gate at the start); a chain without attitudes in its
    integration and burns but for that shift, its impulses' attitudes now from the opening of their thrust windows;
    every propagation about another body, and SSB mode, but for the attitudes, the windows and the gates the rules above
    move.
  - See [Attitudes](../reference/attitudes.md#the-attitude-timeline) and
    [Impulse Maneuvers](../reference/impulse-maneuvers.md#maneuver-chain).

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
- **Propagations about the Earth integrate over TT seconds.** Every numerical propagation integrated over TDB seconds,
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
    `ToStateVector(epoch)`, `EpochAtMeanAnomaly` and `ToStateVector(trueAnomaly)` (with the Fixed entry of #508 below).
    A `TimeSpan` added to a TDB epoch is a span of TDB: `elements.ToStateVector(elements.Epoch + elements.Period())`
    from a TDB epoch is off the start by n·δ (1.2e-6 s of motion over an hour at J2000), not from a TT epoch, nor from
    a UTC epoch away from a leap second (across one, `Epoch + Period()` adds calendar seconds: see #508 below);
  - the default central term keeps the GM of the kernels, TDB-compatible, against the TT time argument: 1.5e-8 from the
    TT-compatible GM of EGM2008, which `GeopotentialCentralTerm.ModelGravitationalParameter` gives.

  The conformance runner reads the final state at the end of the window as the case tags it, and the `stm/001` fixture
  is a UTC window. See [Time & Windows](../reference/time-and-windows.md#the-time-argument-of-a-propagation)
  ([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)).
- **The field-of-view search: edges, time frame, short events, corrections and errors.** With the Fixed entry of
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
    549 s for the Sun. `CelestialItem.GetEphemeris` keeps its correction until
    [#491](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/491), so the two can differ for the same `Aberration`;
  - a `Star` is a direction, as `gfrfov` reads a ray: no light time (`LT` and `CN` read as `None`), the stellar
    aberration alone for their `+S` forms;
  - `IsInFOV` is the `POINT` condition of `fovtrg` with the same field and corrections, and stays virtual; the search no
    longer calls it, so an override no longer changes the search. The overrides of `EllipticalInstrument` and
    `RectangularInstrument` are removed. Where it returned a value, it now throws `InvalidOperationException` for a field
    the search rejects (below: a boresight zero or parallel to the reference vector, a half-angle not positive, an
    angular radius above π/2 − 1e-6, a circle of half-angle π/2 included, a non-circular instrument of another
    subclass), and `ArgumentException` when the target is the instrument's own spacecraft;
  - both paths check the same rules before the search and throw where they returned no window, or one computed on an
    extrapolation: a step that is not positive (`ArgumentOutOfRangeException`); a search window that ends before it
    starts, a target that is the instrument's spacecraft, an ellipsoid target without radii (a site, a spacecraft, a
    barycenter, a Lagrange point, a star) (`ArgumentException`); a boresight zero or parallel to the reference vector, a
    half-angle that is not positive, a field whose angular radius exceeds π/2 − 1e-6, a non-circular instrument of
    another subclass (`InvalidOperationException`); on the managed path, a search window that leaves the propagated
    states of the instrument's spacecraft or of a spacecraft target (`ArgumentException`), and an observer inside the
    target's ellipsoid (`InvalidOperationException`). A spacecraft target is still read up to its light time before the
    start (reception) or after the end (transmission, `XLT` and `XCN`), milliseconds about the Earth, extrapolated.

  A search on a propagated spacecraft takes 0.1 to 1 s (0.1 to 0.7 s before). The occultation searches share the
  light time and the root finder, bit for bit. No other public signature changes; `Instrument.PositionInFOV` is
  removed (below) ([#515](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/515)).
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
  without stored states the states make one arc, read and written as before.
- **Breaking: states and attitudes in ICRF.** A spacecraft propagated from ECLIPJ2000, B1950, FK4, ECLIPB1950 or
  GALACTIC stores its states (with their covariance) and its attitude in ICRF; `PropagationSolution` keeps the initial
  frame, bit for bit, and the states read back in that frame are a rotation round trip, no longer bit for bit those of
  the solution. `Spacecraft.AddPropagatedStates` converts a state to ICRF, and a spacecraft not propagated returns its
  two-body state in ICRF. Without an attitude maneuver the attitude is the identity of ICRF, where the CK held the
  identity of the initial frame (0.409, 0.011 or 2.12 rad from it). Every attitude is computed on the state in ICRF: the
  roll that a single-vector or an impulse attitude leaves free no longer depends on the initial frame (0.48 to 0.87 rad
  between ICRF and ECLIPJ2000 before), and `Attitude.TryExecute` and `ImpulseManeuver.TryExecute` return the rotation
  from the spacecraft frame to ICRF, labelled ICRF, for a state in any frame.
- **Breaking: the attitude between outputs.** A continuous attitude between two output epochs is the spherical linear
  interpolation of its samples, no longer the earlier one held, up to a discrete change; its residual against the law
  grows as the square of the output step (5.7e-7, 5.7e-5 and 2.1e-3 rad at 1, 10 and 60 s on the inclined LEO of the
  tests; 5.9e-5 rad at 10 s for the TRIAD after an impulse since #539). Its angular velocity is that of the CK, no longer 0, in the convention of the frames (ICRF relative to the
  spacecraft, in the spacecraft frame), 2.3e-5 rad/s from the rate of the law at 10 s outputs; every read of the record
  returns it, the entries of `GetStateOrientationsToICRF` and `GetLatestStateOrientationToICRF` included, and the rate
  given to `AddStateOrientationToICRF` is not kept. `StateOrientation.AtDate` is unchanged, and its documentation states
  its convention, the opposite of the frames'.
- **Breaking: velocities in the spacecraft frame.** The spacecraft frame now turns with the attitude, where its angular
  velocity was 0, so a velocity expressed in it (`StateVector.ToFrame(spacecraft.Frame)`, `GetEphemeris` with the
  spacecraft frame, the angular velocity of `Spacecraft.GetOrientation`) takes the rotation of the frame,
  v' = R v − (R ω) × r': for the Earth's centre seen from a LEO, 7.7e3 m/s with a nadir attitude and 2.0e4 m/s with the
  prograde attitude of the tests, 4.2e8 m/s for the Sun. Against `spkezr` in the spacecraft frame on the kernels the
  library writes, the state relative to the Earth is 3.9e-3 m and 1.0e-5 m/s off (Linux x64, Windows x64 and macOS arm64).
- **Breaking: `SpacecraftFrame`.** A read stores nothing; the frame of a kernel-backed spacecraft reads SPICE at each
  call and no longer caches, so it no longer answers from its cache after its CK is unloaded. The entries and the reads are labelled
  with the spacecraft frame, the source of their rotation (they were labelled with the initial frame);
  `AddStateOrientationToICRF` takes the rotation from the spacecraft frame to ICRF so labelled, throws
  `ArgumentException` for another label, and replaces the entries within 2 µs, where it was dropped at an existing
  epoch. `AddStateOrientationToICRF`, `GetStateOrientationsToICRF`, `GetLatestStateOrientationToICRF` and
  `ClearStateOrientations` are virtual on `Frame`.
- **Breaking: `Instrument.Orientation` is the Euler angles 1-2-3 of the frame kernel.** With the Fixed entry above. A
  zero orientation is unchanged everywhere. About one axis, the managed field-of-view path is unchanged bit for bit,
  while `GetBoresightInSpacecraftFrame`, `GetRefVectorInSpacecraftFrame` and the attitudes built from them turn every
  vector by twice the angle about that axis, a vector along the axis excepted (34.38° at 0.3 rad, 180° at π/2). About
  more than one axis the managed path moves too (4.634° for (0.3, 0.5, 0.2)). A `TriadAttitude` or an
  `InstrumentPointingToAttitude` built from an instrument with a non-zero orientation changes, and now points the
  boresight the field-of-view search and SPICE see. The frame kernel and the CSPICE path are unchanged.
- **Breaking: `InstrumentPointingToAttitude`** returns the rotation that takes the boresight to the target (Fixed
  above): every attitude it computes changes, at zero orientation too.
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
  ([#542](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/542)).
- **A number of seconds becomes the nearest tick of a `Time`** ([#625](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/625), Fixed above):
  `Time.Create`, `CreateTDB`, `CreateUTC`, `AddSeconds`, `AddMilliseconds`, `AddMicroseconds` and the TDB conversions
  give the nearest tick, ties to even, where they truncated, so an epoch built from seconds can be a tick later after
  J2000 and earlier before it; a span that is not finite, or whose ticks do not fit in a `long`, throws
  `ArgumentOutOfRangeException` (`AddSeconds(double.NaN)` returned the time unchanged). A coverage bound from 1983 to 2017 given
  as seconds can now be a tick outside the coverage, where the truncation kept a start from 1983 to J2000 or an end from 2000
  to 2017 inside, and a read there throws: read from the first tick inside. `SpiceAPI.ReadEphemeris(Time …)` and a
  kernel-backed spacecraft label their state with the epoch asked, and an end of a CSPICE search at an end of its search
  window is that end. `AddDays`, `AddHours`, `AddMinutes`, `CreateFromJD` and the `TimeSpan.FromSeconds` of the library
  outside `Time` (`Period()`, `SideralRotationPeriod`, `TrueSolarDay`, `Launch`, the windows of an impulse, the hold of
  `PhasingManeuver`, the epochs of the conjunction search) still truncate.
- **`TimeToRadius` and `TimeToSphereOfInfluence` throw where the orbit never reaches the radius** ([#627](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/627), Fixed above):
  a radius below the periapsis or above the apoapsis of an ellipse, an infinite one, or one reached outside the epochs
  of `Time`, throws `ArgumentOutOfRangeException` where it returned an apsis or failed with NaN; an infinite sphere of
  influence throws `InvalidOperationException`; a circular orbit returns its epoch.
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

**Removed**

- **`Instrument.PositionInFOV`** (protected), on which `IsInFOV` was built before #515: its reference vector was not made
  perpendicular to the boresight and its corrections were those of `CelestialItem.GetEphemeris`. A subclass of
  `Instrument` that called it no longer compiles. Use `IsInFOV`, the `POINT` condition of CSPICE's `fovtrg` with the
  field and the corrections of the search; the azimuth and the elevation it returned have no replacement
  ([#515](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/515)).
- **`LowThrustManeuver`.** It was never implemented: every member threw `NotImplementedException`. It was marked
  `[Obsolete]` in 10.1.0 (see its Deprecated entry). Finite thrust is planned as a separate feature.
- **`Quaternion.Zero`**, the identity under another name and an assignable field: use `Quaternion.Identity` (Changed
  above, [#571](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/571)).

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
