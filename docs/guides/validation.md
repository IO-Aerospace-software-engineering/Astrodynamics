# Validation

## Disclaimer

!!! warning "Not certified for critical applications"
    This software is provided as-is and is **not certified** for flight-critical, safety-critical, or life-critical applications. Independent verification and validation is required before operational use.

## General Validation Guidance

When integrating the library into an analysis pipeline:

1. **Cross-reference results** against trusted external tools such as JPL Horizons before relying on them for operational decisions.
2. **Verify kernel coverage** — confirm that the loaded SPICE kernels are correct and complete for the analysis interval.
3. **Check time-system assumptions** — be explicit about TDB vs. UTC and verify conversions at domain boundaries.
4. **Account for numerical precision** — long-duration or high-sensitivity propagations accumulate integration error. Compare against reference solutions at intermediate epochs, not only at the final epoch.
5. **Perform independent V&V** — production workflows should include an independent verification step before operational use.

## SSA-Specific Validation

For conjunction assessment and CDM workflows:

1. **Validate collision-probability behavior** against trusted SSA references for the probability method family being used.
2. **Inspect `EncounterQualityFlags`** before using Pc values in operational decision support. Flags such as low relative velocity or covariance remediation are applicability warnings.
3. **Confirm covariance realism** — verify that participant covariances and hard-body radii are realistic and current. The library propagates to TCA the covariance of the participants it propagates in RK7(8), linearly and without process noise by default, which is often optimistic: check `EncounterState.ProtectedCovariance` and `SecondaryCovariance` for the source of each covariance (`Propagated`, or the initial covariance held in RTN for the others), whether a process noise was applied, and the non-linearity index; see [Conjunction Assessment](../reference/conjunction-assessment.md#covariance-at-tca).
4. **Schema-validate CDM products** using `Cdm.ValidateSchema()` or `Cdm.ValidateSchemaFromXml()` before exchanging them with external systems.
5. **Treat edge cases carefully** — low-relative-velocity encounters may require analysis beyond the standard 2D Pc workflow.

### Foster against a Monte Carlo of the probability

With the covariance propagated to TCA, the Foster probability of collision agreed with a Monte Carlo of the probability
(pairs of initial states propagated exactly to their own closest approach, 99 % Clopper–Pearson interval) on two-body
crossing encounters 6 h to 7 days after the initial states, initial covariances of 100 m and 0.1 m/s up to 10 km and
10 m/s, non-linearity indices from 1.3e-3 to 1.42 (`FosterMonteCarloValidationTests`, `LongValidation`, 2026-10-17, ν measured again on 2026-10-18); the
table and the threshold it sets are in [Conjunction Assessment](../reference/conjunction-assessment.md#non-linearity-index).

## Conformance Tests

The conformance cases come from the
[conformance-tests](https://github.com/IO-Aerospace-software-engineering/conformance-tests)
suite: inputs, golden outputs produced by an external reference tool, and tolerances. It holds
98 cases: ten propagation, eclipse, geosynchronous, tracking and pointing cases, and the 88 cases
of lot 1 ([#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457)):
48 orbital-element conversions, 12 time-scale conversions and 28 frame transformations, whose
goldens come from GMAT R2026a, each checked against an independent reference and against CSPICE,
the second reference. Lot 1 is independent of any framework (decision 16 of #457,
conformance-tests#19): no framework's delta enters its tolerances. On the suite's main (d4e9905),
eight of the ten other cases keep overrides set from the deltas of IO.Astrodynamics (runner
reports of 2026-10-04 and 2026-10-21); conformance-tests#25 reworks them in the same way (see
[The earlier categories](#the-earlier-categories)). A tolerance of lot 1 is the
operational need of its key: 1 mm for lengths and positions, 1 µm/s for speeds and velocities,
1e-9 deg for angles, 1e-11 for the eccentricity and the equinoctial f, g, h, k, 1 µs for the
period and the time scales, 1e-12 relative for the mean motion, the energy and the angular
momentum. It is raised only where the references cannot do better, to twice the largest of
CSPICE's delta on Linux, Windows and macOS arm64, the golden's distance to the independent
reference and the conditioning floor of the output, rounded up at the second significant digit
(the suite's `spec.md` §6.4, measured 2026-10-27). Three keys are raised: the geodetic latitude
(5.2e-8 deg) and the geodetic altitude (2.8e-6 km), both limited by GMAT's own golden, and the
time outputs that cross TDB and TT (7.6e-5 s), whose key also covers the spread of the TDB − TT
series between GMAT and ERFA (37.57 µs) plus GMAT's printing of TDB. The runner passes the 88
cases of the suite at d4e9905 with at most 0.495 of the tolerance, on these geodetic outputs
(cases 029 and 030), 0.36 on the time scales and below 0.10 elsewhere (Linux, 2026-10-27). These
worst ratios are the same on Windows and macOS arm64: the `MEASURE` lines of the CI of 2026-10-26
(the tree of 1f226ea, the suite at 371c515) give them on the three platforms, from cases whose
goldens conformance-tests#24 left unchanged. That PR regenerated the ITRF93 cases 002 to
008 with a new Earth orientation file; they pass at 2.9e-5 of the tolerance at most, measured on
Linux only. Lot 2 adds 13 propagator cases (see [Lot 2](#lot-2-drag-srp-heo-and-other-central-bodies)), lot 3
12 maneuver and 7 lambert cases (see [Lot 3](#lot-3-impulsive-maneuvers-and-lamberts-problem)), lot 4 21 geometry and
event cases (see [Lot 4](#lot-4-geometry-and-events)), lot 5 20 TLE cases and 5 TEME frame cases (see
[Lot 5](#lot-5-tles-sgp4-and-teme)), lot 6 8 conjunction, 7 collision-probability, 7 CDM and 3 state transition matrix
cases (see [Lot 6](#lot-6-conjunctions-pc-cdm-and-the-state-transition-matrix)).
Eight harnesses run the cases:

- the conformance runner `IO.Astrodynamics.ConformanceRunner` runs every case and writes a report,
  `IO.Astrodynamics.Net/conformance-report.json`; it propagates with the adaptive RK7(8) integrator
  (absolute and relative tolerances 1e-13, steps from 1 ms to 300 s; 1e-11 before lot 2);
- unit tests run the three 24-hour propagation cases with RK7(8) (`RK78IntegratorTests.Conformance00x_*`)
  and with Velocity-Verlet at a fixed 1 s step (`PropagatorTests.Conformance00x_*`), against the
  GMAT goldens of conformance-tests#25 with the model's GM in the central term, and print the
  errors they measure;
- `Lot1CaseTests` runs the runner on the cases of lot 1, copied with the schemas and the tolerances
  of the suite into `IO.Astrodynamics.ConformanceRunner.Tests/Fixture/Lot1` by
  `tools/conformance/sync_fixture.py --lot 1`, with the suite's Earth orientation file, which differs
  from the runner's (NAIF's `earth_000101_270122_261026.bpc` since conformance-tests#24); its
  `SOURCE` file records the suite commit, d4e9905 (2026-10-27). The test prints one `MEASURE` line
  per output: the delta, the tolerance and their ratio;
- `Lot2CaseTests` runs the runner on the 13 propagator cases of lot 2, copied into `Fixture/Lot2` by
  `tools/conformance/sync_fixture.py --lot 2` with the data files that differ from the runner's and
  from `Fixture/Lot1` (the lunar and Martian fields, the Mars cut, `moon_assoc_pa.tf`); its `SOURCE`
  records the suite commit, 8b5a347 of the suite's lot-2 branch, [conformance-tests#28](https://github.com/IO-Aerospace-software-engineering/conformance-tests/pull/28) (2026-10-28). It prints the same
  `MEASURE` lines, which the CI logs carry on the three platforms;
- `Lot3CaseTests` runs the runner on the 19 maneuver and lambert cases of lot 3, copied into `Fixture/Lot3` by
  `tools/conformance/sync_fixture.py --lot 3`, whose data files are all the runner's or `Fixture/Lot1`'s; its `SOURCE`
  records the suite commit, 9dca9c7 of the suite's lot-3 branch,
  [conformance-tests#31](https://github.com/IO-Aerospace-software-engineering/conformance-tests/pull/31) (2026-11-05).
  It prints the same `MEASURE` lines, one per field of each burn;
- `Lot4CaseTests` runs the runner on the 22 ground_station, eclipse, orbital_events and attitude cases of lot 4 (eclipse
  001, regenerated by lot 4, included), copied into `Fixture/Lot4` by `tools/conformance/sync_fixture.py --lot 4`, whose
  data files are all the runner's, `Fixture/Lot1`'s or the library tests' SPK of LRO (`IO.Astrodynamics.Tests/Data/LRO`,
  linked into the test output); its `SOURCE` records the suite commit, 4cd11f9 of the suite's lot-4 branch (2026-11-06).
  It prints the same `MEASURE` lines, one per field of each pass, sample, event and attitude;
- `Lot5CaseTests` runs the runner on the 14 sgp4 and 6 tle_conversion cases and the TEME frame cases 032 to 036 of lot
  5, copied into `Fixture/Lot5` by `tools/conformance/sync_fixture.py --lot 5` (lot 1's fixture keeps the frame cases
  001 to 031), whose only data file, the suite's Earth orientation file of 036, is `Fixture/Lot1`'s; its `SOURCE` records
  the suite commit, b10b925 of the suite's lot-5 branch (2026-11-08). It prints the same `MEASURE` lines, one per
  component state, field and line;
- `Lot6CaseTests` runs the runner on the 8 conjunction, 7 collision_probability, 7 cdm and 3 stm cases of lot 6,
  copied into `Fixture/Lot6` by `tools/conformance/sync_fixture.py --lot 6` with the CDM each cdm case reads next to its
  inputs and the two SPKs of conjunction 007 (the other data files are the runner's or `Fixture/Lot1`'s); its `SOURCE`
  records the suite commit, 970a3bf of the suite's lot-6 branch (2026-11-09). It prints the same `MEASURE` lines and the
  worst ratio of each case (`WORST`). `StmCaseTests` runs the variants of stm 001 (a perturbed, a null or a malformed
  golden) on the same fixture, which replaces the stm case and the schemas the runner's tests extended before the suite
  held the category.

The first two evaluate the final state of a propagation at the end of the window as it is tagged, the end of the TT
seconds the library integrates about the Earth since #501, like the reference tool's elapsed time, and report the
magnitude of the position and velocity errors.

### Running the Suite

The suite keeps its kernels and the data of its goldens in Git LFS. A clone without LFS holds
pointer files of about 130 bytes in their place. Clone it next to the repository (`../conformance-tests`
from its root) and fetch the files:

```bash
git clone https://github.com/IO-Aerospace-software-engineering/conformance-tests
cd conformance-tests
git lfs install && git lfs pull
```

Then, from `IO.Astrodynamics.Net/`:

```bash
dotnet run --project IO.Astrodynamics.ConformanceRunner -c Release -- \
  ../../conformance-tests IO.Astrodynamics.Tests/Data/SolarSystem conformance-report.json
```

The exit code is the number of failed cases plus the number of errors. A data file left as an LFS
pointer makes its case an ERROR that names the file. The suite (98 cases) takes about 5 s in
Release on Linux (4 cores, 2026-10-27: 4.3 s for d4e9905, 4.8 s for conformance-tests#25, one
run each, the start of the process included); with the integrator at 1e-13, 6.1 s for the suite's
main (4a7c20f) and 12.2 s for the 111 cases of the lot-2 branch (e6fafe4, the same cases as 8b5a347), one run each
(2026-10-28), 11.7 s for the 130 cases of the lot-3 branch (9dca9c7, 2026-11-05, one run), 25 s for the 176 cases of the lot-5 branch (b10b925, 2026-11-08, one run) and 21 s for the 201 cases of the lot-6 branch (970a3bf, 2026-11-09, one run); the eclipse case took 48 s alone before the managed occultation search of #495. The runner asks the propagator for the end of the window only: an
output every `step_size_s` (1 s) cost a shortened RK7(8) step each and took the suite to 107 s
(#426).

### The Earlier Categories

conformance-tests#25 (draft, c1af386) gives the five earlier categories a model each (its `spec.md`
§12), goldens from GMAT R2026a or CSPICE in ICRF, the keys of §12 with their operational
tolerances and no override. The runner computes them as §12 says, from the optional inputs that
the PR adds, and keeps what the cases of the suite's main need where they lack them, so that it
passes both:

| Category | The runner's model | Inputs of conformance-tests#25 | Without them (main) |
|----------|--------------------|--------------------------------|---------------------|
| `propagator` | EGM2008 of the suite's `data/` with its GM and radius applied to the whole field (`GeopotentialCentralTerm.ModelGravitationalParameter`), third bodies as point masses, RK7(8) at 1e-13 (1e-11 before lot 2), ICRF | `kernels`: the Earth orientation file and `gm_de440.tpc` of `data/`, loaded for the case | the runner's kernels |
| `eclipse` | two-body trajectory, the managed occultation search of #495 (the pck00011 ellipsoid oriented by `IAU_EARTH`, the Sun's sphere), geometric, transitions to 1 µs | `gm_km3_s2`: the GM of the trajectory, applied as a field of degree 0 whose central term takes it, the state of the elements with it (velocity scaled by √GM); `kernels` | the kernel GM |
| `tracking_data` | the station on the Earth's kernel ellipsoid in ITRF93, the UTC grid of the window, the Moon's sphere, geometric | `location.ellipsoid`: checked against the kernel radii (1 µm), ERROR otherwise, since a `Site` takes no other ellipsoid; `kernels` | the kernel ellipsoid |
| `pointing_triad` | TRIAD of the instrument's boresight and reference vector towards `CelestialAttitudeTarget`s with `Aberration.LT` (LT-corrected from the Earth's centre), compared by the rotation angle 4·atan2(‖q − s·g‖, ‖q + s·g‖) | — | the same |
| `geosynchronous` | `CelestialBody.GeosynchronousOrbit` with a declared period (#494), the lot-1 keys of its quantities | `sidereal_period_s`, `gm_km3_s2` (checked against the kernel GM, ERROR otherwise), `kernels` | the period of ITRF93 at the epoch |

Every output of these categories takes its key from the table of §12 (the eclipse instants by
their difference in seconds, the arcs and the field-of-view flag exact), and a missing key is an
ERROR; an override of the suite's main that names a former key (`position_km`, `velocity_km_s`,
`attitude_quaternion`, `penumbra_duration_s`) applies after the overrides of the output and of
its key. Release, Linux, 2026-10-27, the largest ratio of a delta to its tolerance:

| Category | conformance-tests#25 (c1af386) | main (d4e9905) |
|----------|--------------------------------|----------------|
| `propagator` | 0.173 (003, vz: 1.73e-7 km/s; 001 to 003 within 15.6 cm and 0.173 mm/s per component); with the integrator at 1e-13 of lot 2, 0.180 (003, vz: 1.80e-7 km/s; 001 to 003 within 16.3 cm and 0.180 mm/s per component; measured again on 2026-10-28 on the suite's main, 4a7c20f, which holds these cases); with the TT time argument of [#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501), at 1e-13 on the suite at 09d10cd (2026-11-01), 0.0186 (001, the largest velocity component: 1.86e-8 km/s; 001 to 003 within 1.41 cm and 0.0186 mm/s per component) | 0.815 (004, y: 8.15e-5 km against the override of 1e-4 km), the same at 1e-13 |
| `eclipse` | 0.046 (umbra exit, 4.6e-4 s) | 0.0019 (penumbra duration against the override of 1 s) |
| `tracking_data` | 0.0033 (median range rate, 3.3e-10 km/s) | 0.78 (minimum range, 2.3e-3 km against the override of 3e-3 km) |
| `pointing_triad` | 3.8e-11 (3.8e-16 rad) | 3.8e-12 |
| `geosynchronous` | 0.0011 (argp of 001, 1.1e-12 deg) | 0.0013 (inclination of 001) |

98 cases pass on each suite. On main, the propagators 001 to 003 keep their JGM3 goldens: with
the model's GM in the central term, 003 is 15.2 m from its golden (y), beyond its `abs_tol` of
4 m but within its `rel_tol` of 1e-4 (OR rule, ratio 0.092); 001 and 002 pass on `abs_tol`.

The `conformance` job of the CI runs the latest version of the suite on Linux, Windows and macOS
arm64 and keeps each report as an artifact. The suite is private: the job reads it with the
secret `CONFORMANCE_TESTS_TOKEN` and is skipped without it.

### Lot 2: Drag, SRP, HEO and Other Central Bodies

Lot 2 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) (design in
[#500](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/500), decisions 21 to 23, S. Guillet,
2026-10-28) adds 13 `propagator` cases, in [conformance-tests#28](https://github.com/IO-Aerospace-software-engineering/conformance-tests/pull/28) (8b5a347) until it merges: NRLMSISE-00 drag
(006 to 009), solar radiation pressure with the shadows of the Earth and the Moon (010 to 012), high eccentric orbits
over 3 days (013 and 014, and 015 with drag and SRP), and the Moon (016, LP165P 20×20 in MOON_PA), Mars (017, GMM-2B
20×20 in IAU_MARS) and the Sun (018, 30 days, the Sun and the planets) as central bodies. Their goldens are GMAT R2026a
runs with the Earth, the Moon and Mars oriented through SPICE by the kernels of the suite's `data/`, read back in ICRF;
018 is also checked against an independent n-body integration. The runner computes them as the suite's `spec.md`
§12.1 specifies, and reports an ERROR for what the library cannot compute that way:

| Item | The runner | ERROR |
|------|------------|-------|
| Field | the table of §12.1 by `geopotential_model` (`GeopotentialFieldCatalog`): EGM2008 (3.986004415e14 m³/s², 6378136.3 m), LP165P (4.902801056e12 m³/s², 1738 km), GMM-2B (4.28283719012840e13 m³/s², 3397 km), read from `data/`, the model's GM and radius on the whole field (`GeopotentialCentralTerm.ModelGravitationalParameter`), at degree 0 the point mass of the model's GM; `"none"`, the point mass of the kernel GM | an unknown model, the field of another body than the central body, a degree beyond the file's, `"none"` with a degree other than 0 |
| Drag | `Nrlmsise00Model` at the case's constant F10.7, F10.7A and Ap, every switch on (switch 9 = 1: the daily Ap alone), in place of the US 1976 atmosphere, which put 006 to 009 1.9 to 67 km off | drag about another body than the Earth, another atmosphere model, `drag: true` |
| SRP | the library's cannonball with its occulters, every celestial body of the propagation but the Sun | SRP about another body than the Earth, `shadow_bodies` other than those occulters, `srp: true` |
| Spacecraft | the case's mass, area and coefficients; without drag and SRP a case may omit them | drag or SRP without the spacecraft block, which the runner defaulted before |
| Epochs | `EpochParser`: the tags of §3 (UTC, TAI, TT, TDB), a window end with up to 7 decimals (004, 005) | another form |
| Kernels | the case's `kernels`, loaded after the runner's (`CaseKernels`): the Mars cut that gives the centre of Mars (499), `moon_assoc_pa.tf`, which associates the Moon with MOON_PA in place of MOON_ME | a file missing or left as an LFS pointer |
| Integrator | RK7(8) at 1e-13, steps from 1 ms to 300 s (open choice 18 of the design): at 1e-11 the error estimate misses the jumps of the shadow function's derivative at the contacts, 0.21 m on 015 | — |

The tolerance of a case is the operational one per day of its window (decision 23): T_op = ceil2(1 m × Δt / 86400 s)
for the positions and ceil2(1 mm/s × Δt / 86400 s) for the velocities, Δt the length of the window in TT seconds about
the Earth and in TDB seconds otherwise, raised to twice the largest reference term where that is larger (it is not, on
any case). `tolerances.yaml` keeps the one-day values, 1e-3 km and 1e-6 km/s; the suite writes the others as overrides
of the case: 0.25 m and 0.25 mm/s for 007 (6 h), 2 m and 2 mm/s for 012 (2 days), 3 m and 3 mm/s for 010 and 013 to
015 (3 days), 30 m and 30 mm/s for 018 (30 days). Release, Linux, 2026-11-01, the largest component of the error of the
final state and the largest ratio to its tolerance (the runner on the suite at 09d10cd, the same cases as the branch of
lot 2; `conformance-report.json`):

| Case | Position | Velocity | Tolerance | Largest ratio |
|------|----------|----------|-----------|---------------|
| `propagator_24h_leo400_grav10_drag_006` | 4.1 mm | 0.0067 mm/s | 1 m, 1 mm/s | 0.0067 |
| `propagator_6h_leo250_grav10_drag_007` | 20.2 mm | 0.023 mm/s | 0.25 m, 0.25 mm/s | 0.092 |
| `propagator_24h_leo500_grav10_drag_008` | 8.3 mm | 0.0077 mm/s | 1 m, 1 mm/s | 0.0083 |
| `propagator_24h_leo200x800_grav10_drag_009` | 32.0 mm | 0.036 mm/s | 1 m, 1 mm/s | 0.036 |
| `propagator_72h_geo_grav10_srp_010` | 0.13 mm | 1.0e-5 mm/s | 3 m, 3 mm/s | 4.5e-5 |
| `propagator_24h_sso700_grav10_srp_011` | 2.2 mm | 0.0024 mm/s | 1 m, 1 mm/s | 0.0024 |
| `propagator_48h_meo_grav10_srp_012` | 0.62 mm | 1.1e-4 mm/s | 2 m, 2 mm/s | 3.1e-4 |
| `propagator_72h_molniya_grav10_013` | 0.35 mm | 1.9e-4 mm/s | 3 m, 3 mm/s | 1.2e-4 |
| `propagator_72h_gto_grav10_014` | 0.72 mm | 1.3e-4 mm/s | 3 m, 3 mm/s | 2.4e-4 |
| `propagator_72h_gto_grav10_drag_srp_015` | 12.2 mm | 0.0017 mm/s | 3 m, 3 mm/s | 0.0041 |
| `propagator_24h_llo_grav20_016` | 0.44 mm | 4.8e-4 mm/s | 1 m, 1 mm/s | 4.8e-4 |
| `propagator_24h_lmo_grav20_017` | 3.6 mm | 0.0030 mm/s | 1 m, 1 mm/s | 0.0036 |
| `propagator_30d_helio_nbody_018` | 4.6 mm | 8.8e-6 mm/s | 30 m, 30 mm/s | 1.5e-4 |

About the Earth the library integrates over the TT seconds of the window since #501, as the suite does (its `spec.md`
§12.1) and GMAT (its native A1 rate), the forces at TT₀ + t
([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)): over the TDB seconds, the
errors of 006 to 012 were 2.2 cm to 10.8 cm and those of 013 to 015 0.30 m to 0.52 m (2026-10-28), the largest ratio
0.34 (007); the move of each final state is |v|·δ, δ the change of TDB − TT over the window, within 0.13 % for the
gravity and two-body cases and within −1.4 % to +2.0 % for the drag and SRP cases (measured on the prototype of #501,
2026-10-31; the implementation moves the deltas of the Earth cases against the prototype by 0.59 mm at most, on 007,
whose move is about 5 cm, so these ratios hold to about 1 %). About the Moon, Mars and the Sun
both integrate over TDB seconds. The field files of `data/` end without a final newline, which the library's reader needs
([#502](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/502)). The CI logs of `Lot2CaseTests`
give the ratios on Windows and macOS arm64.

### Lot 3: Impulsive Maneuvers and Lambert's Problem

Lot 3 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) (design in
[#587](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/587) and
[conformance-tests#31](https://github.com/IO-Aerospace-software-engineering/conformance-tests/pull/31), approved by
S. Guillet, 2026-11-05) adds two categories, specified by the suite's `spec.md` §13. `maneuver` (001 to 012) is a chain
of one to three impulsive maneuvers about the Earth during a one-day propagation, point mass or EGM2008 10×10 with the
Moon and the Sun: each burn's epoch, position, Δv and mass after it, then the final state. `lambert` (001 to 007) is
Lambert's problem with 0, 1 or 2 revolutions, both branches and a retrograde transfer: v1 and v2. Every Δv of a golden
is found by GMAT R2026a's differential corrector, and every target uses the osculating two-body elements with the kernel
GM. The runner (`ManeuverSolver`, `LambertCaseSolver`) maps each step to the library's maneuver, with no library change:

| Step `type` | The library | From the case |
|-------------|-------------|---------------|
| `apoapsis_radius` | `ApogeeHeightManeuver` | the radius (the library's "height" is a radius) |
| `periapsis_radius` | `PerigeeHeightManeuver` | the radius |
| `plane_change` | `PlaneAlignmentManeuver` | a circular orbit of the target inclination and node, in ICRF |
| `apsidal_alignment` | `ApsidalAlignmentManeuver` | a, e and ω of the case, i and Ω of the initial state (the current plane: a chain that changes the plane before this step is an ERROR) |
| `phasing` | `PhasingManeuver` (target orbit) | the target's state at the start and N |
| `combined` | `CombinedManeuver` | the periapsis radius and inclination |
| Lambert | `LambertSolver` (Izzo) | r1 at the departure epoch, r2 `time_of_flight_s` TT seconds later; for N ≥ 1 the branch by the semi-major axis of each solution's v1, whatever the solver calls it (Left or Right) |

- **Propulsion.** The spacecraft has the case's dry mass, a tank with its fuel and an engine with its Isp at a fuel flow
  of 1e12 kg/s, so that the thrust and maneuver windows of an impulse vanish (open choice 17).
- **Earliest epochs.** The library's hold belongs to the maneuver that sets it: the hold of step k is the
  `earliest_after_previous_s` of step k + 1, the minimum epoch of the first step the start plus its own, in TT seconds.
  `PhasingManeuver` sets its own hold, 0.9 N periods of the phasing orbit (16264 s on 009, whose restore declares
  15000 s); both fire the restore at the third periapsis.
- **Outputs.** The epoch of a burn is the base of the segment its impulse opens, in TT seconds from the start (open
  choice 16: the library has no public burn epoch); its position the start of that segment; its Δv the library's
  `DeltaV`, the velocity jump in ICRF; its mass the total mass at the start minus the fuel burned up to it. A step not
  executed in the window makes its outputs, and those of the steps after it, an ERROR that names it; an exhausted tank
  is an ERROR. The report names a burn output `burns[<label>].<field>`, and `accuracy_by_metric` gathers the burns
  of every case under `maneuver/burns[].<field>`.

Every key takes its operational tolerance (no case has an override): 1 ms for a burn epoch, 1 m for a burn position,
1 mm/s for a Δv, 1 g for a mass, 1 mm/s for a Lambert velocity, and 1 m and 1 mm/s per component of the final state.
Release, Linux, 2026-11-05, the runner on the suite at 9dca9c7: the worst ratio of each key over the cases, and of each
case over its keys.

| Key | Worst delta | Ratio | Case and output |
|-----|-------------|-------|-----------------|
| `maneuver_epoch_s` | 8.4e-7 s | 8.4e-4 | `maneuver_24h_leo_pm_apsidal_008`, `burns[align].elapsed_s` |
| `maneuver_position_km` | 6.0 mm | 0.0060 | 008, `burns[align].position_km` |
| `maneuver_delta_v_km_s` | 1.1e-3 mm/s | 0.0011 | `maneuver_24h_leo_pm_gto_012`, `burns[raise].delta_v_km_s` |
| `maneuver_mass_kg` | 3.2e-7 kg | 3.2e-4 | 008, `burns[align].mass_after_kg` |
| `propagator_position_km` | 31.4 mm | 0.031 | 008, `final_x_km` |
| `propagator_velocity_km_s` | 0.034 mm/s | 0.034 | 008, `final_vx_km_s` |
| `lambert_velocity_km_s` | 4.2e-3 mm/s | 0.0042 | `lambert_10800s_0rev_002`, `v2_km_s` |

| Case | Largest ratio | Output |
|------|---------------|--------|
| `maneuver_24h_leo_pm_apoapsis_001` | 0.0038 | `burns[raise].position_km` |
| `maneuver_24h_leo_grav10_apoapsis_002` | 0.0018 | `burns[raise].position_km` |
| `maneuver_24h_leo_pm_hohmann_003` | 0.0040 | `burns[raise_periapsis].position_km` |
| `maneuver_24h_leo_grav10_hohmann_004` | 0.0022 | `burns[raise_periapsis].position_km` |
| `maneuver_24h_leo_pm_lower_005` | 0.0016 | `burns[lower].position_km` |
| `maneuver_24h_leo_pm_plane_006` | 0.0027 | `burns[plane].position_km` |
| `maneuver_24h_leo_grav10_plane_007` | 0.0027 | `burns[plane].position_km` |
| `maneuver_24h_leo_pm_apsidal_008` | 0.034 | `final_vx_km_s` |
| `maneuver_24h_leo_pm_phasing_009` | 0.0034 | `burns[restore].position_km` |
| `maneuver_24h_leo_pm_combined_010` | 0.0024 | `burns[combined].position_km` |
| `maneuver_24h_leo_grav10_chain_011` | 0.0026 | `burns[raise_periapsis].position_km` |
| `maneuver_24h_leo_pm_gto_012` | 0.011 | `final_x_km` |
| `lambert_3000s_0rev_001` | 3.3e-4 | `v2_km_s` |
| `lambert_10800s_0rev_002` | 0.0042 | `v2_km_s` |
| `lambert_10800s_1rev_lower_003` | 4.6e-6 | `v2_km_s` |
| `lambert_10800s_1rev_higher_004` | 4.0e-4 | `v2_km_s` |
| `lambert_3000s_0rev_retro_005` | 0.0011 | `v2_km_s` |
| `lambert_20000s_2rev_lower_006` | 3.6e-4 | `v2_km_s` |
| `lambert_20000s_2rev_higher_007` | 0.0025 | `v2_km_s` |

The burn epochs agree within 8.4e-7 s, GMAT's epoch quantum (an A1 MJD double, 3.1e-7 s apart), and the burn
positions within 6 mm, the distance travelled in that time. 008's final state, the worst of the lot, is of the size of
the golden's own spread (0.046 of the tolerance, its corrector variants, conformance-tests#31). The field cases (002,
004, 007, 011) agree as closely as the point-mass ones: the goldens orient the Earth by the suite's ITRF93 (GMAT's
`EarthS`), as the library does. The CI logs of `Lot3CaseTests` give the ratios on Windows and macOS arm64.

### Lot 4: Geometry and Events

Lot 4 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) (design approved by
S. Guillet, 2026-11-06) adds three categories and a second branch of `eclipse`, specified by the suite's `spec.md` §14
and §12.2. `ground_station` (001 to 006) gives the passes of a two-body LEO, or of LRO from its SPK with the Moon
occulting, above a station's mask (rise, set, duration, culmination, maximum elevation) and the azimuth, elevation,
range and range rate at sample epochs, with the light time `none`, `lt`, `lt+s` or `xlt`. `eclipse` 002 to 006 give every
eclipse of a window by the Earth and the Moon (umbra, antumbra, each partial interval), geometric or `cn`.
`orbital_events` (001 to 005) gives the apsides with their radii, the ICRF nodes and the crossings of a geodetic
altitude of a propagation, point mass or EGM2008 10×10 with the Moon and the Sun. `attitude` (001 to 005) gives TRIAD
attitudes from nadir with the velocity or the orbit normal, and a single pointing axis, along a two-body orbit. The
goldens are GMAT R2026a's (`ContactLocator`, `EclipseLocator`, stopping conditions, `NadirPointing` and
`CoordinateSystemFixed`), CSPICE `azlcpo`'s for the station samples.

The runner reads them through the library's window searches, whose edges are the roots to 1 µs since #590:

| Category | The runner (solver) | The library |
|----------|---------------------|-------------|
| `ground_station` | `GroundStationSolver` | the windows of the declination in the station's frame (`Site.Frame`) above the mask, `FindWindowsOnCoordinateConstraint`, minus the windows of `FindWindowsOnOccultationConstraint` (the target a point, the Moon's sphere); the culmination the `AbsoluteMax` of the declination over the pass, an edge where the elevation is monotonic (006); the samples `Site.GetHorizontalCoordinates` and r·v/\|r\| of `GetEphemeris(t, site, ICRF, …)`; the flags `none`, `lt`, `lt+s`, `xlt` as `Aberration.None`, `LT`, `LTS`, `XLT` |
| `eclipse` (002 to 006) | `EclipseSolver` | the `Full`, `Annular` and `Partial` windows of `FindWindowsOnOccultationConstraint` of each body, the Sun as target, the Earth's ellipsoid in IAU_EARTH, in time order; the trajectory about the Earth built apart from the occulting bodies; `cn` as `Aberration.CN` |
| `orbital_events` | `OrbitalEventsSolver` | the propagation of the `propagator` category (`PropagationCase`) with 10 s stored states; the apsides the `LocalMin` and `LocalMax` of `FindWindowsOnDistanceConstraint`, the nodes the edges of the windows of Z > 0 in ICRF, the altitude crossings the edges of the windows of the geodetic altitude above the value in the Earth's frame, the edges at the bounds of the window left out; the epochs TT seconds from the start |
| `attitude` | `AttitudeSolver` | `TriadAttitude` toward `OrbitalDirectionTarget.Nadir`, `Prograde` or `Normal`, or `NadirAttitude` with the case's axis as `BodyFront`, set at the start with no hold; the output step the sampling step, so that every sample is a recorded attitude, read at t0 + k·step TT seconds by `SpacecraftFrame.GetStateOrientationToICRF` |

- **Two-body trajectories** (`TwoBodyTrajectory`, shared by the station, eclipse and attitude solvers): RK7(8) about the
  Earth over the TT seconds of the window, the case's GM as the GM of a geopotential of degree 0 with the model's central
  term (decision 19 of #457). A field of degree 0 is the point mass of its central term computed in the frame of the
  state since lot 4: it no longer reads the Earth's orientation, which the runner's Earth orientation file does not
  cover after 2026-05-11 (`eclipse` 006 and `attitude` 003 lie after it).
- **Indexed outputs.** The arrays of a golden are compared element by element: `passes[k].<field>`,
  `samples[k].<field>`, `events[k].<field>`, `events.<list>[k].<field>`, `attitude_quaternions[k]`,
  `pointing_directions[k]`, k from 0; the array itself is its count, an exact output, so that a pass, an event or an
  attitude the framework does not find FAILS, as does each of its fields, rather than making the case an ERROR. The body
  and the type of an eclipse event and the epoch of a sample are exact. A pointing direction is compared by the
  direction angle atan2(‖c × g‖, c·g) (`MetricKind.DirectionAngle`).

Every key takes its operational tolerance (no case has an override): 10 ms for a pass or eclipse edge, 100 ms for a
duration and a culmination, 1e-4 deg for an angle, 1 m for a range or an apsis radius, 0.1 mm/s for a range rate, 1 ms
for an orbital event, 1e-5 rad for an attitude. Release, Linux, 2026-11-06, the runner on the suite at 4cd11f9: the
worst ratio of each key over the cases, and of each case over its keys.

| Key | Worst delta | Ratio | Case and output |
|-----|-------------|-------|-----------------|
| `station_event_time_s` | 0.55 ms | 0.055 | `ground_station_12h_lro_mask0_moon_lt_006`, `passes[0].set` |
| `station_duration_s` | 0.21 ms | 0.0021 | 006, `passes[0].duration_s` |
| `station_culmination_time_s` | 0.55 ms | 0.0055 | 006, `passes[0].culmination` |
| `station_angle_deg` | 4.4e-7 deg | 0.0044 | 006, `passes[4].max_elevation_deg` |
| `tracking_range_km` | 1.2 mm | 0.0012 | `ground_station_24h_leo_mask10_lt_002`, `samples[8].range_km` |
| `tracking_range_rate_km_s` | 0.014 mm/s | 0.137 | `ground_station_24h_leo_mask10_lts_003`, `samples[10].range_rate_km_s` |
| `eclipse_time_s` | 1.07 ms | 0.107 | `eclipse_8d_geo_season_earth_004`, `events[1].stop` |
| `eclipse_duration_s` | 1.49 ms | 0.015 | 004, `events[2].duration_s` |
| `event_epoch_s` | 1.2e-5 s | 0.012 | `orbital_events_24h_leo_grav10_altitude_003`, `events.altitude_up[12].elapsed_s` |
| `event_radius_km` | 0.021 mm | 2.1e-5 | `orbital_events_48h_heo_grav10_apsides_nodes_004`, `events.apoapsis[3].radius_km` |
| `attitude_angle_rad` | 4.2e-10 rad | 4.2e-5 | `attitude_12h_molniya_lvlh_004`, `attitude_quaternions[715]` |

| Case | Largest ratio | Output |
|------|---------------|--------|
| `ground_station_24h_leo_mask10_geo_001` | 0.072 | `samples[3].range_rate_km_s` |
| `ground_station_24h_leo_mask10_lt_002` | 0.137 | `samples[10].range_rate_km_s` |
| `ground_station_24h_leo_mask10_lts_003` | 0.137 | `samples[10].range_rate_km_s` |
| `ground_station_24h_leo_mask10_xlt_004` | 0.101 | `samples[10].range_rate_km_s` |
| `ground_station_24h_leo_mask0_geo_005` | 0.072 | `samples[3].range_rate_km_s` |
| `ground_station_12h_lro_mask0_moon_lt_006` | 0.055 | `passes[0].set` |
| `eclipse_simple_001` | 0.046 | `umbra_exit` |
| `eclipse_72h_geo_earth_moon_002` | 0.048 | `events[8].stop` |
| `eclipse_72h_geo_earth_moon_cn_003` | 0.049 | `events[3].start` |
| `eclipse_8d_geo_season_earth_004` | 0.107 | `events[1].stop` |
| `eclipse_1h_leo_annular_moon_005` | 0.036 | `events[1].stop` |
| `eclipse_1h_leo_total_moon_006` | 0.048 | `events[0].stop` |
| `orbital_events_24h_leo_pm_apsides_nodes_001` | 0.0015 | `events.descending_node[12].elapsed_s` |
| `orbital_events_24h_leo_grav10_apsides_nodes_002` | 0.0012 | `events.periapsis[8].elapsed_s` |
| `orbital_events_24h_leo_grav10_altitude_003` | 0.012 | `events.altitude_up[12].elapsed_s` |
| `orbital_events_48h_heo_grav10_apsides_nodes_004` | 8.5e-4 | `events.descending_node[2].elapsed_s` |
| `orbital_events_48h_heo_grav10_altitude_005` | 7.8e-4 | `events.altitude_up[1].elapsed_s` |
| `attitude_2h_leo_nadir_normal_001` | 1.5e-5 | `attitude_quaternions[118]` |
| `attitude_2h_leo_nadir_velocity_002` | 1.5e-5 | `attitude_quaternions[118]` |
| `attitude_2h_leo_lvlh_003` | 8.2e-6 | `attitude_quaternions[113]` |
| `attitude_12h_molniya_lvlh_004` | 4.2e-5 | `attitude_quaternions[715]` |
| `attitude_2h_leo_nadir_axis_005` | 1.5e-5 | `pointing_directions[118]` |

The pass and eclipse edges are within GMAT's millisecond print (±0.5 ms) but on two cases: the passes of LRO (006,
0.55 ms), where GMAT converges the light time that the model iterates once (0.88 ms of the suite's E_2nd there), and the
grazing umbra of the GEO season (004, 1.07 ms), whose chord moves by about 12 ms per metre out of the shadow plane,
GMAT's own out-of-plane drift of 0.15 m (the suite's `spec.md` §12.2, G2). The orbital events agree to 1.5e-6 s on the apsides and nodes, GMAT's epoch
quantum, and to 1.2e-5 s on the LEO altitude crossings, GMAT's own altitude (1 mm from CSPICE `recgeo`). The range rate
with light time, the largest ratio of the lot (0.014 mm/s), is the library's light-time rate since #491 against
`azlcpo`'s. The CI logs of `Lot4CaseTests` give the ratios on Windows and macOS arm64.

### Lot 5: TLEs, SGP4 and TEME

Lot 5 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) (design approved by
S. Guillet, 2026-11-08) adds two categories, specified by the suite's `spec.md` §15, and TEME to `frame_transformation`
(§2.1, §11.3). `sgp4` 001 to 008 give the TEME states of Vallado's verification TLEs at minutes since the epoch (an
Alpha-5 and 1957 edit in 008), 009 to 014 the TEME and ICRF states hourly over a day of six TLEs (the ISS and TDRS 6 of
CelesTrak's 2026-11-07 data, 014 the day before its epoch), with the TLE epoch; `tle_conversion` 001 to 003 read TLEs
into their OMM fields, 004 to 006 write TLEs from OMM mean elements (the day and year carry, zeros, exponents below
1e-10, e = 0, a TT epoch); `frame_transformation` 032 to 036 rotate LEO and GEO states between TEME and ICRF from 1990
to 2021, and Vallado's appendix C state to ITRF93. The goldens are Vallado's code (`sgp4` 2.27, WGS-72 old, mode `a`)
for SGP4, the exact decimal evaluation of the format for the text, GMAT R2026a's TEME axes for TEME → ICRF, CSPICE
second reference of the first two, ERFA's CIP route of the third.

| Category | The runner (solver) | The library |
|----------|---------------------|-------------|
| `sgp4` | `Sgp4Solver` | `new TLE(name, line1, line2)`; a tsince sample is the TT instant `tle.Epoch` + 60·tsince, a UTC instant is read as it is, both through `TLE.ToOsculating`, whose tsince is the TT seconds from the TLE epoch (#599); ICRF by `ToFrame(Frame.ICRF)`, the managed TEME (#451); the epoch the UTC calendar date of `tle.Epoch` (#603). `gravity_model` and `opsmode` other than `wgs72old` and `afspc` are an ERROR: the library's SGP4 is CSPICE `evsgp4_c`, WGS-72 old in the AFSPC mode only |
| `tle_conversion` | `TleConversionSolver` | `tle_to_omm`: `TLE.ToOmm()`, its mean elements and TLE parameters; `omm_to_tle`: an `Omm` of the item (Earth, TEME, SGP4, its `TIME_SYSTEM`), then `Omm.ToTle()`, which reads the epoch in that time system and writes its UTC (#485, #479), each field the nearest value of its columns (#600 to #602, #480) |
| `frame_transformation` | `FrameTransformationSolver` | `TEME` resolved to `Frame.TEME` (`FrameCatalog`), quasi-inertial, v′ = R v; TEME → ITRF93 composed through ICRF |

- **New kinds** (`spec.md` §6.1): the SGP4 states are compared per component (`MetricKind.ScalarPerComponent`, the
  largest component delta); the decoded TLE values by their relative delta, a zero golden passing only on an exact zero
  (`Relative`); the TLE epochs on the UTC calendar, 86 400 s a day, defined in 1957 and 2056 (`CalendarInstant`). A
  computed TLE (`TleLine`, `TleLineComparer`) is exact in its length, checksums, line numbers, catalog number and
  identifiers, and each other field is decoded from the spec's column table (`TleColumns`, in `decimal`, independently
  of the library's parser), differenced exactly and compared by its key: lines of the wrong length or checksum FAIL,
  the fields with them.
- **Indexed outputs.** `states[k].teme.position_km`, `states[k].icrf.velocity_km_s`, `items[k].<KEYWORD>` and
  `items[k].tle.<FIELD>`, k from 0; `states` and `items` are counts, exact.

The TEME outputs take their operational tolerance; the ICRF outputs of `sgp4` 010, 011, 013 and 014 and the five frame
cases take the overrides of the suite, set by GMAT's kinematic terms of the 1994 equation of the equinoxes (§15.3, T1)
and the 2000A/2000B spread: a runner exact to Vallado's definition sits at about half of them. Release, Linux,
2026-11-08, the runner on the suite at b10b925: the worst ratio of each key over the cases, and of each case over its keys.

| Key | Worst delta | Ratio | Case and output |
|-----|-------------|-------|-----------------|
| `sgp4_position_km` | 0.36 mm | 0.036 | `sgp4_24h_near_earth_iss_icrf_010`, `states[8].teme.position_km` |
| `sgp4_velocity_km_s` | 4.1e-7 m/s | 0.041 | 010, `states[20].teme.velocity_km_s` |
| `sgp4_icrf_position_km` | 0.125 m | 0.50 | `sgp4_24h_near_earth_2000_icrf_013`, `states[10].icrf.position_km` |
| `sgp4_icrf_velocity_km_s` | 9.8e-5 m/s | 0.49 | 013, `states[15].icrf.velocity_km_s` |
| `tle_epoch_s` | 0 | 0 | every epoch |
| `tle_value_rel` | 5.3e-15 | 3.4e-4 | `tle_conversion_tle_to_omm_leo_001`, `items[1].MEAN_MOTION` |
| `tle_field_*` (6 keys) | 0 | 0 | every field of every written TLE |
| `position_km` (overrides of 032 to 036) | 7.4 mm | 0.62 | `frame_transformation_icrf_teme_leo_1990_035` |
| `velocity_km_s` (overrides of 032 to 036) | 7.7e-6 m/s | 0.64 | 035 |

| Case | Largest ratio | Output |
|------|---------------|--------|
| `sgp4_72h_near_earth_teme_example_001` | 2.9e-5 | `states[10].teme.position_km` |
| `sgp4_24h_near_earth_low_perigee_002` | 5.1e-6 | `states[8].teme.velocity_km_s` |
| `sgp4_7h_near_earth_decay_003` | 1.8e-7 | `states[12].teme.position_km` |
| `sgp4_48h_deep_12h_resonant_neg_bstar_004` | 7.1e-5 | `states[18].teme.position_km` |
| `sgp4_24h_deep_24h_resonant_geo_005` | 6.9e-6 | `states[10].teme.position_km` |
| `sgp4_12h_deep_lyddane_006` | 2.9e-6 | `states[34].teme.position_km` |
| `sgp4_27h_deep_high_e_neg_ndot_1994_007` | 0.0076 | `states[3].teme.position_km` |
| `sgp4_24h_near_earth_alpha5_1957_008` | 1.4e-5 | `states[4].teme.position_km` |
| `sgp4_24h_near_earth_drag_icrf_009` | 0.059 | `states[22].icrf.velocity_km_s` |
| `sgp4_24h_near_earth_iss_icrf_010` | 0.48 | `states[15].icrf.velocity_km_s` |
| `sgp4_24h_deep_geo_icrf_011` | 0.50 | `states[23].icrf.position_km` |
| `sgp4_24h_deep_molniya_icrf_012` | 0.46 | `states[5].icrf.position_km` |
| `sgp4_24h_near_earth_2000_icrf_013` | 0.50 | `states[10].icrf.position_km` |
| `sgp4_24h_deep_backward_icrf_014` | 0.50 | `states[24].icrf.position_km` |
| `tle_conversion_tle_to_omm_leo_001` | 3.4e-4 | `items[1].MEAN_MOTION` |
| `tle_conversion_tle_to_omm_deep_002` | 2.5e-4 | `items[1].MEAN_MOTION` |
| `tle_conversion_tle_to_omm_alpha5_pivot_003` | 1.6e-4 | `items[1].MEAN_MOTION` |
| `tle_conversion_omm_to_tle_synthetic_004` | 0 | every field |
| `tle_conversion_omm_to_tle_edges_005` | 0 | every field |
| `tle_conversion_omm_to_tle_celestrak_006` | 0 | every field |
| `frame_transformation_teme_icrf_leo_2021_032` | 0.50 | `position_km` |
| `frame_transformation_teme_icrf_geo_2012_033` | 0.50 | `velocity_km_s` |
| `frame_transformation_teme_icrf_geo_2016_034` | 0.48 | `velocity_km_s` |
| `frame_transformation_icrf_teme_leo_1990_035` | 0.64 | `velocity_km_s` |
| `frame_transformation_teme_itrf93_vallado_2004_036` | 0.52 | `velocity_km_s` |

The TEME states are CSPICE's SGP4 at the exact tsince: the residual is the double ET₀ + Δ the library passes to
`evsgp4_c` (0.36 mm on the 2026 ISS, CSPICE's own time resolution there); the decoded mean motion goes through the mean
semi-major axis and back (5.3e-15 relative). The CI logs of `Lot5CaseTests` give the ratios on Windows and macOS arm64.

### Lot 6: Conjunctions, Pc, CDM and the State Transition Matrix

Lot 6 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) (design approved by
S. Guillet, 2026-11-09) adds four categories, specified by the suite's `spec.md` §16. `conjunction` 001 to 006 give the
TCA, the miss distance, the relative speed and state in the RTN of object 1, each object's covariance at TCA (σ and
correlations in its own RTN, propagated linearly from the epoch) and the 2D Pc of two objects propagated 6 hours about
the Earth (head-on, crossing, point mass, GEO at 1 m/s, a flat minimum, drag with two ballistic coefficients); 007 the
same of two objects read from SPKs, 008 the 15 minima of 12 hours. `collision_probability` 001 to 007 give the 2D Pc of
encounters at TCA designed in the suite's catalog (Pc 2.1e-2 to 5.1e-12, a hard-body radius much larger than the
covariance, a covariance not positive definite, Frisbee's maximum). `cdm` 001 to 006 read CDMs the suite writes
(obligatory keywords, relative metadata, every optional keyword with the consider terms, CARA's form turned to inertial
axes, day-of-year dates, lengths without units), 007 writes one. `stm` 001 to 003 are the cases of
conformance-tests#10 regenerated on the suite's Earth. The goldens are GMAT R2026a (TCA, relative state, Φ, the
covariance by `Sat.OrbitSTM`), CSPICE (the kernel objects), CARA's `Pc2D` definition at 50 digits and the decimal text
of the CDMs.

| Category | The runner (solver) | The library |
|----------|---------------------|-------------|
| `conjunction` | `ConjunctionSolver` | each object the spacecraft of a propagator case (`PropagationCase`, the case's force model), its initial state carrying its covariance rotated from its RTN (`RotateCovarianceFromRtn`), propagated in RK7(8) from the epoch to the end of the window; `ConjunctionAssessment.Analyze` (`closest`) or `AnalyzeAll` (`all_minima`) on the two `PropagationSolution`s, which propagates each covariance to TCA by the replay of the variational equations; the combined hard-body radius on object 1. Kernel objects: the kernel-backed `Spacecraft`, its orbit about the Earth (#620), and the `ILocalizable` overloads. `tca_s` in TT seconds from the epoch, TDB seconds for kernel objects |
| `collision_probability` | `CollisionProbabilitySolver` | the public `Analyze` on straight-line trajectories over TCA ± 10 s (`StraightLineEncounter`), each object a spacecraft at its state at TCA with its covariance; `CollisionRisk.ProbabilityOfCollision`, the maximum Pc when `SingleCovarianceMaximumPcUsed` is raised, `remediated` from `CovarianceRemediated`; the sigmas from the eigenvalues of `ProjectedCovariance` of the given covariances, an absent one given as zero (the projection of a maximum Pc holds Frisbee's addition) |
| `cdm` | `CdmSolver` | `Cdm.ReadFromFile` (decode), `StateVector.RotateCovarianceFromRtn` (to_inertial), and `Analyze` + `EncounterCase.ToCdm` + `WriteToString` + `Cdm.ReadFromString` (export); the fields in the suite's units, a value in m^p times 10^(−3p) |
| `stm` | `StmSolver` | as before; it now takes the `propagator` category's data files of `data/` and its RK7(8) settings (1 ms to 300 s) |

- **New kind** (`spec.md` §6.1): the normalized covariance (`MetricKind.NormalizedCovariance`), max |ΔC_ij|/√(C_ii C_jj)
  over the lower triangle; the relative kind and the scalar per component take arrays of any length (6 sigmas, 15
  correlations, 21 covariance elements, 24 consider terms). The matrix block of `final_stm` stays `StmComparer`'s.
- **Indexed outputs.** `objects[k].<field>` and `minima[k].<field>`, k from 0, `objects` and `minima` counts, exact; a
  minimum the runner does not find FAILs the count and the minimum.
- **A cdm export has its own table** (`MetricTable.CdmExportTable`): its outputs carry the names of a decode, compared
  at the operational thresholds of the fields a framework writes.
- **What the runner cannot give is an ERROR that says so:** a covariance the library did not propagate to TCA (held in
  RTN), a Pc it did not compute, and an encounter-plane sigma whose eigenvalue the library raised to the clip
  (`ProjectedCovariance` is after the clip; no case compares one: 006's minor sigma is left out by the suite).

Release, Linux, 2026-11-09, the runner on the suite at 970a3bf (the native library of the lot's #454 change built
locally): every case passes; the worst ratio of each key over the cases, and of each case over its keys.

| Key | Worst delta | Ratio | Case and output |
|-----|-------------|-------|-----------------|
| `conjunction_tca_s` | 1.3e-7 s | 0.13 | `conjunction_12h_leo500_spk_crossing_007`, `tca_s` |
| `conjunction_position_km` | 0.79 mm | 0.50 | 007, `relative_position_rtn_km` (T 1.6 mm) |
| `conjunction_velocity_km_s` | 5.0e-7 m/s | 0.50 | 007, `relative_velocity_rtn_km_s` |
| `covariance_sigma_rel` | 1.1e-6 | 0.11 | `conjunction_6h_leo400_grav10_drag_crossing_006`, `objects[1].sigma_rtn` |
| `covariance_correlation` | 3.0e-6 | 0.30 | 006, `objects[0].correlation_rtn` |
| `collision_probability_rel` | 1.1e-6 | 0.0011 | 006, `collision_probability` |
| `encounter_plane_sigma_rel` | 7.5e-12 | 7.5e-6 | `collision_probability_synthetic_mid_002`, minor sigma |
| `cdm_value_rel` | 2.1e-16 | 2.1e-4 | `cdm_decode_synthetic_optional_003`, `objects[1].covariance_consider` |
| `cdm_covariance_corr` | 1.7e-14 | 0.017 | `cdm_export_synthetic_leo_007`, `objects[0].covariance_rtn` |
| `cdm_epoch_s` | 1e-7 s | 0.10 | cdm 007, `tca` |
| `cdm_position_km` | 0.66 mm | 0.66 | cdm 007, `relative_position_rtn_km` |
| `cdm_velocity_km_s` | 4.3e-7 m/s | 0.43 | cdm 007, `relative_velocity_rtn_km_s` |
| `stm_block` | 1.9e-10 | 0.0019 | `stm_24h_leo_grav10_002`, every block |
| `propagator_position_km` | 1.0 mm | 0.0010 | stm 002, `final_x_km` |

| Case | Largest ratio | Output |
|------|---------------|--------|
| `conjunction_6h_leo700_grav10_headon_001` | 0.0058 | `tca_s` |
| `conjunction_6h_leo500_grav10_crossing_002` | 0.0025 | `relative_position_rtn_km` |
| `conjunction_6h_leo500_pm_crossing_003` | 0.0024 | `relative_position_rtn_km` |
| `conjunction_6h_geo_grav10_slow_004` | 1.9e-5 | `objects[1].correlation_rtn` |
| `conjunction_6h_leo700_grav10_flat_005` | 6.4e-5 | `relative_velocity_rtn_km_s` |
| `conjunction_6h_leo400_grav10_drag_crossing_006` | 0.30 | `objects[0].correlation_rtn` |
| `conjunction_12h_leo500_spk_crossing_007` | 0.50 | `relative_velocity_rtn_km_s` |
| `conjunction_12h_leo500_grav10_allminima_008` | 0.0063 | `minima[14].tca_s` |
| `collision_probability_synthetic_high_001` | 1.6e-8 | `collision_probability` |
| `collision_probability_synthetic_mid_002` | 7.5e-6 | `encounter_plane_sigma_minor_km` |
| `collision_probability_synthetic_low_003` | 7.4e-8 | `collision_probability` |
| `collision_probability_synthetic_floor_004` | 1.5e-6 | `encounter_plane_sigma_minor_km` |
| `collision_probability_synthetic_large_hbr_005` | 1.8e-6 | `encounter_plane_sigma_major_km` |
| `collision_probability_synthetic_npd_006` | 2.3e-10 | `encounter_plane_sigma_major_km` |
| `collision_probability_synthetic_frisbee_max_007` | 7.3e-10 | `encounter_plane_sigma_minor_km` |
| `cdm_decode_synthetic_obligatory_001` | 2.1e-4 | `objects[1].covariance_rtn` |
| `cdm_decode_synthetic_relative_002` | 2.1e-4 | `objects[1].covariance_rtn` |
| `cdm_decode_synthetic_optional_003` | 2.1e-4 | `objects[1].covariance_consider` |
| `cdm_to_inertial_synthetic_cara_form_004` | 3.7e-4 | `objects[1].covariance_inertial` |
| `cdm_decode_synthetic_day_of_year_005` | 2.1e-4 | `objects[1].covariance_rtn` |
| `cdm_decode_synthetic_default_units_006` | 2.1e-4 | `objects[1].covariance_rtn` |
| `cdm_export_synthetic_leo_007` | 0.66 | `relative_position_rtn_km` |
| `stm_24h_leo_twobody_001` | 8.0e-5 | Φ, every block (8.0e-12) |
| `stm_24h_leo_grav10_002` | 0.0019 | Φ, every block |
| `stm_24h_geo_grav70_003` | 6.0e-4 | `final_y_km` (Φ 1.7e-11, 1.7e-4) |

The kernel objects of 007 sit at half their tolerance: the delta is the TCA's, 1.3e-7 s, about one ulp of CSPICE's ET
there (1.19e-7 s), which moves the RTN state by ν_N × Δt; one ulp is the floor the suite added to 007's position
tolerance (7.9e-7 km of 1.6e-6 km). The written CDM of cdm 007 sits at 0.66 of 1 mm: the library's TCA is one 100 ns
tick of `Time` before the exact one (12:01:09.1853859 TDB for 12:01:09.1853860), and the written states, at that TCA,
move by v × 1e-7 s (0.61 mm for object 1, 6.1 km/s along y); the CDM is consistent with itself. 006 carries the
drag's partials at 0.30 of the correlation tolerance, inside GMAT's own spread (lot 6 design, §7.2). The CI logs of `Lot6CaseTests` give the ratios on Windows and macOS arm64.

### Measured Errors

Against the goldens of conformance-tests#25 (GMAT R2026a, the state entered and read in EarthICRF, EGM2008 from a
`.cof` whose header carries the model's GM and radius; two-body cases 004 and 005 with the GM of `gm_de440.tpc`), the
magnitude of the error of the final state, with the model's GM in the central term
(`GeopotentialCentralTerm.ModelGravitationalParameter`, as the runner computes them), over the TT seconds of the day
(#501). Measured in Release, 2026-11-01: RK7(8), the `RK78IntegratorTests.Conformance00x_*` tests (the runner gives the same within its kernels:
its per-component deltas are in [The earlier categories](#the-earlier-categories)), and the runner for 004 and 005 (Linux);
Velocity-Verlet, the `PropagatorTests.Conformance00x_*` tests. The figures of the tests are those of the CI of
[#505](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/505) (run 41268715428), the same on
Linux, Windows and macOS arm64 to the digits of the table.

| Case | Orbit and force model (inputs) | Golden | RK7(8), 1e-11 | Velocity-Verlet, 1 s |
|------|--------------------------------|--------|---------------|----------------------|
| `propagator_24h_leo_grav10_001` | LEO about 6,800 km, 24 h; EGM2008 10x10, Moon, Sun | GMAT R2026a, EGM2008 10x10 | 0.027 m, 0.031 mm/s | 281.1 m, 317.2 mm/s |
| `propagator_24h_geo_grav70_002` | GEO (INTELSAT 901), 24 h; EGM2008 70x70, Moon, Sun, planets | GMAT R2026a, EGM2008 70x70 | 0.0083 m, 0.00069 mm/s | 0.464 m, 0.033 mm/s |
| `propagator_24h_sso_grav10_003` | SSO 700 km, 24 h; EGM2008 10x10, Moon, Sun | GMAT R2026a, EGM2008 10x10 | 0.015 m, 0.0098 mm/s | 242.8 m, 254.6 mm/s |
| `propagator_1sd_geo_twobody_004` | GEO equatorial, one sidereal day; two-body | GMAT R2026a, two-body | 0.0043 m, 0.00027 mm/s (1e-13) | not run |
| `propagator_1sd_geo_i30_twobody_005` | GEO inclined 30 deg, one sidereal day; two-body | GMAT R2026a, two-body | 0.0041 m, 0.00029 mm/s (1e-13) | not run |

Since lot 2 the runner integrates at 1e-13 (see [Lot 2](#lot-2-drag-srp-heo-and-other-central-bodies)): 0.020 m and
0.024 mm/s on 001, 0.0083 m and 0.00069 mm/s on 002, 0.012 m and 0.0031 mm/s on 003, 0.0043 m and 0.00027 mm/s on 004,
0.0041 m and 0.00029 mm/s on 005 (Linux, Release, 2026-11-01, the suite at 09d10cd). Over the TDB seconds of the day,
before #501: 0.118 m, 0.071 m, 0.177 m, 0.081 m and 0.083 m (2026-10-28). The library's tests keep 1e-11; their
figures over the TDB seconds were 0.111 m, 0.071 m and 0.169 m for RK7(8), 0.394 m on 002 for Velocity-Verlet.

The test limits are non-regression guards, twice the worst over Linux, Windows and macOS arm64 of the `MEASURE` lines of
the CI of #505 (run 41268715428, 2026-11-01), rounded up at the second significant digit; in parentheses, the limits
set before #501, twice the figures over the TDB seconds above, on Linux. They are not accuracy specifications:

| Case | RK7(8) limits | Velocity-Verlet limits |
|------|---------------|------------------------|
| `propagator_24h_leo_grav10_001` | 0.054 m, 0.062 mm/s (0.23 m, 0.26 mm/s) | 570 m, 640 mm/s (the same) |
| `propagator_24h_geo_grav70_002` | 0.017 m, 0.0014 mm/s (0.15 m, 0.011 mm/s) | 0.93 m, 0.067 mm/s (0.79 m, 0.057 mm/s) |
| `propagator_24h_sso_grav10_003` | 0.030 m, 0.020 mm/s (0.34 m, 0.36 mm/s) | 490 m, 510 mm/s (the same) |

The Velocity-Verlet limits of 002 rise with #501: its error grew from 0.394 m to 0.464 m when the TT time argument took
the δ term out of the RK7(8) error of the same case (0.071 m to 0.0083 m); probably, the δ term had cancelled part of
the error of the 1 s step (an inference from the magnitudes alone, not traced).

Before conformance-tests#25 the goldens of 001 to 003 were GMAT R2025a runs with JGM3 in EarthMJ2000Eq, 3.4 m to 13.2 m
from the library. With RK7(8), the remaining centimeters come from the differences between the models of the two
tools (see [Error Budget](#error-budget)); most of the decimeters measured before #501 were the change of TDB − TT
over the day, |v|·δ, from the TDB seconds the library integrated. With Velocity-Verlet at 1 s, the LEO and SSO errors are
almost entirely the truncation error of the integrator: 279.5 m over 24 h on the two-body LEO orbit
alone (see [Integrators](../reference/integrators.md#accuracy-and-cost)). At GEO the orbital motion
is slow enough for a 1 s step to be nearly exact.

### Against GMAT With EGM2008's GM In The Central Term

The goldens of cases 001 to 003 of conformance-tests#25 are GMAT R2026a runs with EGM2008, from a `.cof` whose header
carries the GM and the radius of the model, which GMAT applies to the central term as well as to the harmonics (decision
17 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457)); the runner computes them with
the option below. The library evaluates the
central term with the GM of its kernels by default; with
`GeopotentialCentralTerm.ModelGravitationalParameter` it takes the model's GM there too
([Force Models](../reference/force-models.md#the-central-term-with-the-gm-of-the-model)). Against GMAT's runs of the
study of those cases (copied with their scripts and logs into `Data/GmatConformancePropagators`), the largest component
of the final state (`GmatConformancePropagatorTests`, over the TT seconds of the day; CI run 41268715428 of #505,
2026-11-01, the same on Linux, Windows and macOS arm64 to the digits of the table):

| Case | With the model's GM, RK7(8) 1e-11 | With the kernel GM (default), RK7(8) 1e-11 | With the model's GM, Velocity-Verlet 1 s |
|------|-----------------------------------|--------------------------------------------|------------------------------------------|
| 001, LEO, 10×10, Moon, Sun | 1.91 cm, 0.023 mm/s | 15.52 m, 17.3 mm/s | not run (truncation about 300 m) |
| 002, GEO, 70×70, Moon, Sun, planets | 0.83 cm, 0.0007 mm/s | 6.89 m, 0.49 mm/s | 39.7 cm, 0.028 mm/s (7.28 m with the kernel GM) |
| 003, SSO, 10×10, Moon, Sun | 1.14 cm, 0.0087 mm/s | 18.36 m, 20.0 mm/s | not run (truncation about 250 m) |

Over the TDB seconds of the day, before #501, the RK7(8) figures were 8.83 cm, 5.95 cm and 15.6 cm and Velocity-Verlet
33.7 cm (2026-10-27): the change of TDB − TT, |v|·δ, was most of them; probably, the δ term had cancelled part of the
step error of Velocity-Verlet (an inference from the magnitudes alone, not traced). What remains is the models of the two tools, chiefly the orientation of the Earth (GMAT's FK5 with its EOP file, the library's `ITRF93`;
the study measured GMAT's own spread over the Earth-orientation data at up to 4.4 cm) and the GMs of the third bodies.
The bounds of the test are twice the worst over Linux, Windows and macOS arm64 (run 41268715428), rounded up at the
second significant digit: 3.9 cm, 1.7 cm and 2.3 cm (0.047 mm/s, 0.0015 mm/s and 0.018 mm/s) for RK7(8), 0.80 m
(0.057 mm/s) for Velocity-Verlet. Before #501, over the TDB seconds, they were 0.18 m, 0.12 m and 0.32 m, and 0.68 m
(0.048 mm/s; CI of 77b509c, 2026-10-27). The bound of Velocity-Verlet rises because its error grew from 33.7 cm to
39.7 cm, by 6.0 cm, as the TT time argument took the δ term out of the RK7(8) error of the same case, from 5.95 cm to
0.83 cm; probably, the δ term had offset part of the error of the 1 s step, an inference from these two magnitudes
alone, not traced.

Run the conformance runner, next to a clone of conformance-tests:

```bash
cd IO.Astrodynamics.Net
dotnet run -c Release --project IO.Astrodynamics.ConformanceRunner -- \
    ../../conformance-tests IO.Astrodynamics.Tests/Data/SolarSystem conformance-report.json
```

Run the unit tests and see the measured errors:

```bash
cd IO.Astrodynamics.Net
dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj \
    --filter "FullyQualifiedName~Conformance00" --logger "console;verbosity=detailed"
```

## Error Budget

Known sources of the centimeter residual between IO.Astrodynamics (RK7(8)) and the GMAT goldens of conformance-tests#25
(the decimeters over the TDB seconds before #501, the meters of the JGM3 goldens before conformance-tests#25). Each open point is tracked.

| Source | IO.Astrodynamics | Golden (GMAT) | Effect | Status |
|--------|------------------|---------------|--------|--------|
| Gravity field | EGM2008, tide-free, truncated to the case degree | The same file, written as GMAT's `.cof` with the model's GM and radius in its header (conformance-tests#25); JGM3 before | None since conformance-tests#25 | Resolved ([conformance-tests#5](https://github.com/IO-Aerospace-software-engineering/conformance-tests/issues/5), conformance-tests#25) |
| Earth orientation of the gravity field | SPICE `ITRF93` with `earth_latest_high_prec.bpc`, EOP included | GMAT's own Earth-fixed reduction and EOP file | Measured with GMAT R2026a (2026-10-19): from `EarthICRF`, GMAT's Earth-fixed frame is within about 2.6e-8 rad of `ITRF93`; from `EarthMJ2000Eq` coordinates taken as ICRF, as in the scripts of cases 001 to 003, 1.8e-7 rad ([Against GMAT](#against-gmat-h2)) | Measured; the effect on the goldens of cases 001 to 003 not isolated |
| Inertial frame | `Frame.ICRF` (SPICE `J2000`, ICRF-aligned) | The state entered and read in `EarthICRF` since conformance-tests#25, integrated in GMAT's `EarthMJ2000Eq`, whose rotation to `EarthICRF` changes by 1e-10 to 2e-10 rad in a day (8.3e-6 km on 002, counted in the golden's spread); `EarthMJ2000Eq` (EME2000) before | Measured with GMAT R2026a (2026-10-19): GMAT turns `EarthICRF`, whose axes are those of `J2000`, to `EarthMJ2000Eq` by 2.1e-7 rad (43.7 mas) at 2021-03-20 12:00 TDB, mostly about y, drifting by 9.3e-10 rad over the day; about 1.5 m at 7000 km and 8.9 m in GEO ([Against GMAT](#against-gmat-h2)) | Measured at the epoch of the H2 cases; the effect on the goldens of cases 001 to 003, at their own epochs, not isolated |
| Ephemerides | `de440s.bsp` | `de440s.bsp`, the same file (identical MD5) | None | Verified |
| Constants (Earth GM, reference radius of the field) | The central term with the GM of `gm_de440.tpc` (3.98600435507e14 m³/s², TDB-compatible); the harmonics of EGM2008 with its own GM and reference radius (3.986004415e14 m³/s², 6378136.3 m) since #410, before which they took the GM of the kernels and the equatorial radius of `pck00011.tpc` (6378136.6 m) | The GM and the radius of EGM2008 in the header of the `.cof`, applied to the whole field (conformance-tests#25); GMAT defaults and the JGM3 file before | Measured (2026-10-23): the old constants put the harmonics 7.9e-8 away from EGM2008 at degree 2 (9.5e-10 m/s² in LEO 400 km): over one day 7.8 cm on R2 and 1.6 mm on R4; the residuals of cases 001 to 003 against their goldens changed by 0.15 m at most. Remaining: the GM and the radius of EGM2008 are TT-compatible values, used with the TDB-compatible coordinates of the propagation, as GMAT and the ICGEM convention do; scaled to TDB-compatible values (Klioner et al., 2010), the harmonics would change by 4.5e-8, about 4.5 cm a day on R2. The central term takes the model's GM on request, GMAT's convention for a `.cof` header (decision 18 of #457; [above](#against-gmat-with-egm2008s-gm-in-the-central-term)), which the runner applies to the propagator cases | Fixed ([#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410)); the TDB scaling a known approximation |
| Time scales | The TT seconds between the instants of the window about the Earth, the forces at TT₀ + t (#501); the end read as tagged | Elapsed time `ElapsedSecs = 86400`, A1 (TT) seconds | Over the TDB seconds the library integrated before #501, the final state moved by |v|·δ, δ the change of TDB − TT over the day: 5.1 cm in LEO from 2021-03-20, most of the decimeter residuals of 001 to 003; sampling the end of the window in the wrong scale shifted it by about 18 µs, 0.14 m in LEO (before #410) | Resolved ([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)) |
| Integration error | RK7(8), tolerances 1e-11 (1e-13 in the runner) | PrinceDormand78, accuracy 1e-13 | The library's integration error on the GEO of the two-body cases is 8.1e-6 m over the TT length against Kepler (design of #501, §9), while 004 and 005 are 4.3 mm and 4.1 mm from GMAT over one sidereal day (the runner at 1e-13): the residual is not the library's integration, but GMAT's integration, its handling of the epochs or its constants. The 0.08 m of 004 and 005 over the TDB seconds before #501 was mostly |v|·δ | Not attributed |

## State Transition Matrix and Covariance

The state transition matrix Φ, the sensitivity Ψ to Cd and Cr and the propagated covariance P
([Covariance Propagation](../reference/covariance-propagation.md)) are validated end to end, against references
independent of the variational equations: central differences of whole propagations (F1) and a Monte Carlo of the
initial state (F3). The cases are those of the specification of phase 2, feature 1, all on 2021-03-20 at 12:00 TDB
with a spacecraft of 100 kg, 10 m², Cd 2.2 and Cr 1.5:

| Case | Orbit | Force model | Arc |
|------|-------|-------------|-----|
| R1 | LEO 700 km | two-body | 1 day |
| R2 | LEO 400 km | EGM2008 10×10, Sun, Moon, drag (NRLMSISE-00) | 1 day |
| R3 | Sun-synchronous 700 km, an eclipse on every revolution | EGM2008 10×10, Sun, Moon, SRP | 1 day |
| R4 | GEO, at the equinox (eclipse season) | EGM2008 70×70, Sun, Moon, planets, SRP | 1 day |
| R5 | HEO 300 × 36 000 km | EGM2008 10×10, Sun, Moon, drag | 2 revolutions |
| Eclipsing | LEO 400 × 1000 km, in the shadow at the start, apogee raised to 2000 km at the first perigee (233.8 m/s) | point-mass Earth, Sun, Moon, drag, SRP, albedo, thermal | 6 h |

Unless stated otherwise, every figure below was measured on Linux, .NET 10, on 2026-10-16, and is written to the
output of the test named with it.

### Φ and Ψ against central differences of whole propagations (F1)

Φ and Ψ come from RK7(8) at 1e-13 with the default step control, except Ψ_Cr, held with `IncludeInStepControl`. The
reference is made of 12 propagations perturbed by ±δ on each component of the initial state, and of 2 per
coefficient. They run at 1e-14, start from the exact perturbed state, and each chooses its own steps. The errors are
relative Frobenius norms against the local reference, for each 3×3 block of Φ and for the position and velocity rows of
Ψ. They are taken every 10 min and at the end of the arc, and at the middle of every penumbra crossing of the cases
with SRP. Thresholds of the specification: R1 below 1e-7, R2 to R5 below 1e-4 outside the penumbra.

| Case | Φ_rr | Φ_rv | Φ_vr | Φ_vv | Ψ, position | Ψ, velocity | In the penumbra |
|------|------|------|------|------|-------------|-------------|-----------------|
| R1 | 1.3e-8 | 1.6e-8 | 1.4e-8 | 1.7e-8 | | | |
| R2 | 3.0e-8 | 1.3e-8 | 3.0e-8 | 1.2e-8 | Cd 1.2e-7 | Cd 1.2e-7 | |
| R3 | 2.3e-7 | 2.5e-7 | 2.3e-7 | 2.5e-7 | Cr 1.6e-5 | Cr 2.3e-4 (local, a record) | 30 crossings: Φ 2.3e-7 to 2.5e-7, Ψ_Cr 7.9e-6 |
| R4 | 9.0e-9 | 2.4e-9 | 8.9e-9 | 2.2e-9 | Cr 1.4e-6 | Cr 1.6e-8 | 2 crossings: Φ 5.1e-10 to 5.7e-9, Ψ_Cr 5.1e-9 |
| R5 | 2.4e-7 | 2.4e-7 | 2.4e-7 | 2.4e-7 | Cd 1.4e-7 | Cd 1.4e-7 | |

Worst over the arc, outside the penumbra, each relative to the local reference (`StmValidationTests`). R1 to R5
measured again on 2026-11-01 with the TT time argument of
[#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501) (Linux), another draw of the noise
of the central differences (R4 Φ_rr 6.1e-8 to 9.0e-9; R3 Φ 1.9e-7 to 2.1e-7 before, 2.3e-7 to 2.5e-7 after). Before,
R1 as measured on 2026-10-16 and R2 to R5 measured again on 2026-10-23 with the constants of the geopotential of
[#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410) (Linux): they move within what
the central differences of whole propagations resolve, by up to a factor of 6 either way (R4 Φ_rr 9.3e-9 before, 6.1e-8
after; R3 Φ 4.2e-7 to 5.3e-7 before, 1.9e-7 to 2.1e-7 after), all below the thresholds. The errors in the
penumbra are no larger than outside: the SRP partials differentiate the shadow function, by central differences in
and near the penumbra when these figures were measured, analytically since
[#423](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/423), which moves Φ of R3 in the third
digit and Ψ_Cr not to three digits (Linux, 2026-10-22).

The shadow fraction without cancellation of
[#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434) changes the acceleration in the
penumbra by the rounding of the former closed form, and so the steps of every propagation that crosses it, the
references included. Measured again on R3 (Linux, 2026-10-22): Φ 4.2e-7, 5.2e-7, 4.2e-7, 5.3e-7 over the arc and 4.0e-7
to 5.2e-7 in the penumbra; Ψ_Cr 2.3e-5 (position) and 4.0e-4 (velocity) locally outside the penumbra, 1.4e-5 and 1.8e-5
in it, 3.8e-6 and 5.5e-6 over the arc. The local velocity figure is the record of the reference described below, set
again at twice its measurement, 8.1e-4, and the penumbra record at 3.6e-5 (decision of S. Guillet); Ψ_Cr of the library
itself does not move against GMAT (G5 below).

The constants of the geopotential of
[#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410) change the field by 7.9e-8 of its
harmonics, and so the steps of every propagation with a geopotential, the references included. Measured again (Linux,
2026-10-23), within the thresholds and records above: R3 Φ 1.9e-7, 2.1e-7, 1.9e-7, 2.1e-7 over the arc and 1.9e-7 to
2.1e-7 in the penumbra; Ψ_Cr 2.9e-5 (position) and 2.6e-4 (velocity) locally outside the penumbra, 1.1e-5 and 1.3e-5 in
it, 6.0e-6 and 8.8e-6 over the arc; over the arc relative to the largest value, R2 Ψ_Cd 1.2e-7, R4 Ψ_Cr 3.2e-9 and
3.9e-9, R5 Ψ_Cd 5.8e-8 and 8.3e-8; the local figure of Ψ_Cr of R3 in velocity 5.5e-4 at 5 % of Cr and 1.6e-4 at 20 %
(over the arc 1.6e-5 and 6.2e-6); without `IncludeInStepControl`, Ψ_Cr off by 2.5e-5 and 1.4e-4 (R3), 1.9e-6 and
2.4e-8 (R4). The figures of the next paragraph are those of 2026-10-16.

Ψ is also held over the arc relative to its largest value, a measure that does not depend on what the reference
resolves where Ψ is small: R2 Ψ_Cd 1.2e-7 (position and velocity), R3 Ψ_Cr 1.5e-6 and 2.1e-6, R4 Ψ_Cr 2.0e-9 and
1.9e-9, R5 Ψ_Cd 4.6e-8 and 5.3e-8. The local 9.2e-5 of Ψ_Cr of R3 in velocity, at 9.5 h, measures the reference, not
Ψ, and is published as a record rather than held to the threshold. The velocity part of Ψ_Cr passes there through a
local minimum, 7.9e-4 m/s per unit Cr, and the local figure follows the step of the reference: 3.3e-4 at 5 % of Cr,
9.2e-5 at 10 %, 9.9e-5 at 20 %. Over the arc the same comparisons give 9.5e-6, 2.1e-6 and 9.4e-6. Without
`IncludeInStepControl`, Ψ_Cr is off by 2.2e-5 (position) and 2.2e-4 (velocity) over the arc of R3, and by 4.9e-7 and
3.1e-8 for R4, relative to the local reference.

**Steps of the reference** (`WholePropagationStepStudyTests`, `LongValidation`): position steps from 1 cm to 1 km (the velocity step 1e-3
s⁻¹ times the position step), and the coefficients from 0.1 % to 30 %. On the small steps the integration noise of the
perturbed propagations dominates, on the large ones the δ² truncation. The chosen steps are within a factor of 2 of the
smallest error of each sweep, at the end of the arc: 10 m and 1 cm/s (R1 to R4, Eclipsing), 1 m and 1 mm/s (R5); Cd 1 %
(R2), 5 % (R5), 30 % (Eclipsing); Cr 10 % (R3, R4), 30 % (Eclipsing). For Φ each sweep has its minimum between the two
sides. For Ψ two sweeps show the noise floor of the reference rather than a plateau: Ψ_Cr of R3, which is not
monotone, and Ψ_Cr of the eclipsing case. Since
[#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434) the sweep of Ψ_Cr of R3 is
published and not asserted (decision of S. Guillet, 2026-10-23): the shadow fraction without cancellation changed the
steps of the reference propagations through the penumbra and reshuffled their noise, and the sweep became 1.43e-4,
2.42e-4, 2.61e-5, 9.47e-6, 4.44e-6 and 4.23e-6 from 0.1 % to 30 % (1.0e-5, 3.8e-6, 1.7e-5 and 4.3e-6 from 5 % to 30 %
before), the chosen 10 % at 2.2 times the smallest; Ψ_Cr of the library does not move against GMAT (G5). Since
[#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410) the sweeps of Ψ_Cr of R4 and of
Ψ_Cd of R2 are published and not asserted as well (decision of S. Guillet, 2026-10-23): the constants of the
geopotential changed the steps of the reference propagations, and Ψ_Cr of R4 became 3.1e-7, 1.5e-8, 5.7e-8, 4.1e-9,
1.7e-9 and 1.8e-9 from 0.1 % to 30 % (2.8e-7, 3.6e-8, 1.1e-8, 1.6e-9, 1.3e-9, 9.2e-10 before), not monotone, the chosen
10 % at 2.5 times the smallest; Ψ_Cd of R2 1.9e-8, 1.2e-7, 3.0e-6, 1.2e-5, 4.9e-5 and 1.1e-4 (7.1e-8 at 0.1 % before),
the chosen 1 % unchanged and 6.5 times the 0.1 %, whose noise moved. The chosen steps stay. The factor of 2 holds for
Φ of R3 and R4 and every other sweep. Since #398 the latter is published and not asserted: 1.4e-3, 7.2e-4, 2.4e-4,
1.1e-5, 1.3e-4 and 4.7e-5 from 0.1 % to 30 %, so no step lies on a plateau there. On the code before #398, the start of
the reference moved by 2e-9 m along x alone takes that sweep from 3.7e-3, 1.8e-4, 2.3e-4, 1.3e-4, 3.4e-5, 1.8e-5 to
2.1e-2, 7.0e-5, 2.4e-5, 8.1e-5, 7.2e-6, 1.5e-5. Ψ_Cd of the eclipsing case is held at its chosen step, 30 %, to a
record since [#413](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/413) (decision of
S. Guillet, 2026-10-20), rather than within a factor of 2 of the smallest error of its sweep. Its references fire the burn
of the tested propagation in open loop, and that burn moves at the level of the noise with the steps of the variational
equations: moved by one tick of 100 ns, it takes the sweep from 5 % to 30 % from 3.4e-7, 2.5e-8, 3.7e-7, 1.3e-8 to
3.0e-7, 7.9e-8, 2.6e-7, 1.0e-7 (one tick earlier) or 6.7e-7, 1.6e-7, 2.6e-7, 2.1e-7 (one tick later), while the tested
Ψ_Cd moves by 3e-11 (measured 2026-10-20, with
[#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and
[#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409)). The record is twice the worst at
30 % of seven burns from −20 to +20 ticks: 2.05e-7, a record of 4.1e-7 (3.5e-7 and 7.0e-7 before #411 and #409). The reference runs at
1e-14 rather than the 1e-13 of the specification. Its smallest error on Φ is lower in R1 to R4: R1 1.7e-8 against
2.3e-8, R2 2.4e-8 against 1.0e-7, R3 1.4e-7 against 1.8e-6, R4 4.6e-8 against 5.6e-8 (run 39977618859 of
`long-validation.yml`, with the constants of #410; R2 4.6e-8 against 1.4e-7, R3 1.9e-7 against 1.0e-6, R4 2.8e-8
against 5.6e-8 on 2026-10-16). Both resolve R5 alike: over five
starts moved by −2e-9 to 2e-9 m along x, 4.1e-7 to 8.0e-7 at 1e-14 and 1.1e-7 to 7.0e-7 at 1e-13 (6.7e-7 and 7.8e-7
before [#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and
[#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409)), so R5 is held at 1e-14 to a
record of 1.7e-6, twice the worst (decision of S. Guillet, 2026-10-20).

With the TT time argument of [#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)
(run 41268727370 of `long-validation.yml`, 2026-11-01), which moves the steps of the reference propagations about the
Earth, the smallest error on Φ at 1e-14 and at 1e-13 is R1 1.7e-8 and 2.3e-8 (unchanged), R2 3.0e-8 and 2.2e-7, R3
9.5e-8 and 7.2e-7, R4 8.9e-9 and 5.7e-8, R5 2.4e-7 and 8.3e-7, the eclipsing case 5.3e-8 and 1.4e-7. At 1e-14 every
chosen step of Φ is the smallest of its sweep; at 1e-13 the 100 m step is smaller for R4 (5.65e-8, against 1.27e-7 at
10 m) and the eclipsing case (1.41e-7, against 5.09e-7), and the 1 m step for R1 (2.30e-8, against 2.31e-8), so the
figures at 1e-13 are the smallest of each sweep, those of 100 m for R4 and the eclipsing case. Ψ_Cd of R5 at 5 % is
7.7e-8, the smallest of its sweep; Ψ_Cd of the eclipsing case at 30 % 9.5e-8, against its record of 4.1e-7, while its
sweep is smallest at 20 %, 1.7e-8, 5.7 times below (published, not asserted: the case is held to its record). The sweeps published and not asserted, from 0.1 % to 30 %: Ψ_Cr of
R3 1.8e-3, 3.7e-4, 3.1e-5, 9.2e-7, 1.3e-5 and 8.8e-6, the chosen 10 % now the smallest; Ψ_Cr of R4 1.6e-7, 1.1e-8,
2.2e-9, 3.7e-9, 9.4e-9 and 1.3e-9, the chosen 10 % 3.0 times the smallest; Ψ_Cd of R2 5.4e-8, 1.2e-7, 3.0e-6, 1.2e-5,
4.9e-5 and 1.1e-4, the chosen 1 % 2.3 times the 0.1 %; Ψ_Cr of the eclipsing case 2.5e-3, 1.2e-3, 4.4e-4, 2.3e-4,
9.3e-5 and 1.2e-5. The chosen steps stay.

**Start of the reference.** A propagation takes a state vector given at the start of its window as it is, so each
reference propagation starts from exactly its perturbed state. Until
[#398](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/398), the initial state went through
the Keplerian elements (`OrbitalParameters.AtEpoch`). For a near-circular orbit inclined by less than 1e-3 rad (R4),
the true anomaly was wrong, which moved a perturbed state by millimetres to decimetres. Its derivative was not the
identity either, so central differences through it left a floor of 2.0e-5 on Φ of R4 (2.2e-4 with the references of
step 9). The true anomaly fixed alone, the round trip would return the state to its rounding, about 1e-8 m. That is
enough for R4 (Φ 3.3e-8), but not for two references: Ψ_Cr of R3, which is resolved only to the noise of its reference,
and the test after a maneuver, which compares the states bit for bit. Hence the state taken as it is: R4 Φ 2.8e-8.

**Across a maneuver** (`ManeuverStmValidationTests`, Eclipsing case; measured again on 2026-10-24, after
[#423](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/423),
[#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434) and the density of
[#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and
[#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409); the figures of 2026-10-17 in
parentheses). Φ and Ψ model the burn in open loop: the epoch
and the ΔV are those of the nominal trajectory. Against a reference that fires that nominal burn (fixed in epoch and
ΔV), the errors are as follows:

- Φ: 3.2e-8 at most per block, 3.0e-8 in the 7 penumbra crossings (5.5e-8 and 5.2e-8; published as a record, 1.1e-7;
  2.9e-8 before #398);
- Ψ_Cd: 1.1e-7 over the arc relative to its largest value (7.5e-8), and 1.4e-6 relative to the local value after the
  burn (3.7e-6);
- Ψ_Cr: 1.6e-6 in position and 2.3e-6 in velocity over the arc (5.2e-5 and 9.4e-5; 4.4e-6 before #398), and 1.5e-5
  relative to the local value after the burn (3.7e-4; 1.3e-5 before #398), records rather than F1 figures.

Since [#398](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/398) the propagations start
from exactly the state given, about 1e-9 m from where they started before. That shift alone moves the reference of
Ψ_Cr, whose propagations choose their own steps, by a factor of 2 to 30. On the code before the fix, the start of the
reference moved by 2e-9 m along x takes Ψ_Cr over the arc from 4.4e-6 to 9.3e-6, locally after the burn from 1.3e-5 to
5.4e-5, and Φ from 2.9e-8 to 4.3e-8. The Ψ_Cr under test does not move: with Φ and Ψ in the step control it is the same
before and after the fix to 2.5e-10 of its largest value, while its reference moves by up to 6.7e-5. Ψ_Cr is therefore
held to F1 against the same propagation at 1e-14 with Φ and Ψ in the step control, without central differences: 9.8e-11
in position and 1.8e-10 in velocity over the arc, 5.2e-7 at most relative to the local value wherever it is not zero
(2.1e-12, 3.0e-12 and 1.2e-8 on 2026-10-17).
Against the central differences it is published as a record, over the arc and locally.

Ψ starts from zero (in the shadow for Cr, at 1000 km for Cd), where the reference does not resolve it relative to its
local value. Against a reference that retargets the apogee raise at its own perigee (closed loop), Φ is the same
before the burn (below 8.9e-9; 6.8e-9 on 2026-10-17) and off by 0.48, 0.33, 0.42 and 0.23 per block after it. Ψ_Cd is off by 0.37 and Ψ_Cr
by 1.2 of their largest values. This is the measured effect of the open-loop assumption: the propagated covariance is
that of a burn executed as commanded, with its execution error, not that of a maneuver that retargets its ΔV.

**Reduced degree of the geopotential partials** (`GeopotentialPartialsDegreeTests`, `LongValidation`). The internal option
`GeopotentialPartialsDegree` keeps the full degree in the acceleration but reduces it in the partials. Worst block of Φ
over the arc, and the cost of the propagation with Φ against the propagation without it (median of 3, Release,
informative, measured with the test alone: run in parallel with other test classes, the times move with the
load):

| Case | Degree 2 | Degree 8 | Degree 20 | Full degree | Cost |
|------|----------|----------|-----------|-------------|------|
| R2 (10×10) | 2.0e-3 | 8.5e-4 | 4.6e-8 (the full degree) | 4.6e-8 | 5.5× to 6.0× at every degree (0.93 to 1.03 s, against 0.17 s): the drag partials dominate |
| R4 (70×70) | 1.2e-6 | 2.8e-8 | 2.8e-8 | 2.8e-8 | 1.0× to 1.1× up to degree 20, 1.5× at 70 (0.25 s, against 0.17 s) |

Measured on 2026-10-16. With the constants of the geopotential of
[#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410) (run 39977618859 of
`long-validation.yml`): R2 2.0e-3, 8.5e-4 and 2.4e-8 at degrees 2, 8 and 20 and at the full degree; R4 1.2e-6 at
degree 2 and 6.1e-8 at degree 8, the F1 noise of R4, which moves the same way on the full degree (9.3e-9 to 6.1e-8
in `StmValidationTests`): its records at degrees 8, 20 and 70 are set at 6.1e-8 (decision of S. Guillet,
2026-10-23). With the TT time argument of #501 (run 41268727370 of `long-validation.yml`, 2026-11-01), R4 is 1.2e-6 at
degree 2 and 9.0e-9 at degrees 8, 20 and 70, the F1 noise of R4 again (Φ_rr 6.1e-8 to 9.0e-9 in `StmValidationTests`),
and its records are set at 9.0e-9; R2 2.0e-3, 8.5e-4 and 3.0e-8, within its records.

In LEO the partials keep a contribution of every degree, so a reduced degree leaves Φ above the threshold after one day
and saves nothing. At GEO the high degrees vanish, and from degree 8 Φ is that of the full degree, a third cheaper.

### Propagated covariance against Monte Carlo (F3)

P0 is σ = 100 m and 0.1 m/s on each RTN axis, uncorrelated, turned to the inertial frame. A campaign of
`MonteCarloPropagator` draws 5000 runs (seed 20261016, RK7(8) at 1e-11). P comes from `GetCovariance` in covariance
mode, with the same integrator. Two tests are applied every 10 min up to 6 h, at one revolution and at 24 h:

- **element-wise**: each of the 21 elements of the sample covariance S within 4 standard deviations of P, with the
  variance of the Wishart estimator `(P_ij² + P_ii P_jj) / (N − 1)`;
- **Kolmogorov-Smirnov**: the squared Mahalanobis distances of the runs from the nominal state, computed with P, tested
  against χ²(6) at α = 0.01.

| Case | Element-wise, worst \|z\| | KS at one revolution | Domain of validity (both tests pass) | Test |
|------|---------------------------|----------------------|--------------------------------------|------|
| R1 | 1.38 at 10 min, 0.29 at 24 h: passes up to 24 h | p = 0.058 (1.646 h) | up to 1.83 h; fails from 2.0 h (p = 1.9e-4) | `CovarianceMonteCarloTests` |
| R2 | 1.89: passes up to 24 h | p = 0.032 (1.543 h) | up to 1.67 h; fails from 1.83 h (p = 5.3e-4) | `CovarianceMonteCarloLongTests` |
| R2, Cd sampled (σ = 0.22) | 2.26: passes up to 24 h | p = 0.37 | up to 2.33 h; fails from 2.5 h | `CovarianceMonteCarloLongTests` |
| R3, Cr sampled (σ = 0.15) | 2.14: passes up to 24 h | p = 0.011 (1.646 h) | up to 1.67 h; fails from 1.83 h (p = 3.3e-3) | `CovarianceMonteCarloLongTests` |

With Cd or Cr sampled, P holds the coefficient as a consider parameter, `Ψc Pp Ψcᵀ`, so the element-wise test also
validates Ψ.

The element-wise test passing up to 24 h does not mean that the second moments of P hold for a day. In inertial axes
the along-track variance (38 km RMS at 24 h for R1) sets the scale of every element, so the test cannot see the thin
directions of P. The ratios of S to P along every direction of the state do not depend on the axes: the eigenvalues
of L⁻¹ S L⁻ᵀ, with P = L Lᵀ, all 1 under linearity up to the sampling band, (1 ± √(6/N))², 0.93 to 1.07 for N = 5000
(Marchenko and Pastur). The same ratios over the position alone, what a collision probability projects on the
encounter plane, have the band 0.95 to 1.05.

| Case | Ratios of S to P, state, up to 1 h | Largest at one revolution | 6 h | 24 h | Position, up to 6 h | Position, 24 h (radial alone) |
|------|------------------------------------|---------------------------|-----|------|---------------------|-------------------------------|
| R1 | 0.947 to 1.050 | 1.16 | 350 | 1.3e6 | 0.96 to 1.03 | 3.29 (1.08) |
| R2 | 0.947 to 1.040 | 1.15 | 609 | 2.4e6 | 0.95 to 1.04 | 5.00 (1.17) |
| R2, Cd sampled | 0.954 to 1.056 | 1.05 | 2.16 | 87 | 0.96 to 1.05 | 3.96 (1.11) |
| R3, Cr sampled | 0.954 to 1.056 | 1.13 | 362 | 1.3e6 | 0.96 to 1.05 | 4.32 (1.11) |

The figures of R2 were re-measured on 2026-10-20 after
[#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and
[#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409), which move the density of the
drag cases: with Cd sampled, p at one revolution from 0.34 to 0.37, the largest ratio at 6 h from 2.17 to 2.16, the
position at 24 h from 3.93 to 3.96, the mean radial offset at 24 h from −198 m against −186 m to −200 m against
−188 m; without, the position at 24 h from 4.99 to 5.00. The other figures are unchanged. The figures of R3 with Cr sampled were
re-measured on 2026-10-23 after
[#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434), which changes the SRP
acceleration in the penumbra by the rounding of the former shadow fraction and so the steps through it (run
39907481180 of `long-validation.yml`): unchanged at the precision of the tables, worst |z| 2.14, p = 0.011 at one
revolution (1.646 h), the domain of validity up to 1.67 h (p = 3.3e-3 at 1.83 h), the ratios 0.954 to 1.056 up to 1 h,
1.13 at one revolution, 362 at 6 h and 1.3e6 at 24 h, the position at 24 h 4.32 (radial 1.11). These figures were
published before [#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410), the
constants of the geopotential, which moves R2 by 7.85 cm and R3 by 16.6 cm over one day. Measured again with those
constants and the TT time argument of [#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)
(run 41268727370 of `long-validation.yml`, 2026-11-01): unchanged at the precision of the tables, the domains of
validity and the mean radial offsets at 24 h included (87.5 with Cd sampled and 4.325, radial 1.105, for R3 at 24 h,
printed to four digits, at the edge of the rounding of 87, 4.32 and 1.11); the significance of R3 with Cr sampled at
1.67 h is 0.01004 (0.01003 before).

- Up to about one revolution S is P along every direction of the state, within the sampling noise. From about one
  revolution, the sample variance along one direction of the state leaves its band and grows by orders of magnitude
  (for R1 the five others stay between 0.96 and 1.03). Beyond about one revolution P is the linear covariance of the
  deviations, not their covariance.
- The position holds longer: within its band, or at its edge (1.051 for R3 with Cr at 2 h), up to 6 h in every case.
  At 24 h one direction of the position has 3.3 to 5.0 times the variance of P, while the radial variance alone is
  1.08 to 1.17 times that of P.

The shape of the distribution departs at the same time: beyond about one revolution the Kolmogorov-Smirnov test fails,
and then fails massively (D = 0.62 at 6 h and 0.94 at 24 h for R1). The sampled Cd widens the along-track spread, which
the thin direction of P tolerates longer: the test of R2 with Cd sampled even passes again at 3.17 h and 3.33 h before
failing for good from 3.5 h. The departure is the curvature of the orbit. The along-track spread (RMS 2.8 km at 2 h,
38 km at 24 h for R1) lies on the curved orbit, so the runs sit below the straight line of the linear model, and fill
the thin direction of P. Their mean radial offset follows −E[δT²] / (2 r) once it exceeds the sampling noise, about
1.4 m: for R1, −8.9 m against −6.8 m at 6 h and −104 m against −102 m at 24 h; at 24 h, −129 m against −124 m for R2,
−200 m against −188 m for R2 with Cd sampled and −104 m against −102 m for R3. At the boundary of the domain the offset is still within that noise (+1.46 m against
−0.54 m at 2.0 h for R1): there the evidence is the variance along the thin direction. P is very thin across the
curve, so a few metres move the Mahalanobis distances far beyond their spread. Taken about the sample mean, or with S
itself in place of P, the test fails from 2.2 h and 2.3 h for R1: the distribution itself is no longer Gaussian in
Cartesian coordinates. These two epochs are upper bounds, since both variants estimate their centre, and S, from the
runs while their significance is that of a fully specified distribution.

The boundary epochs come from a significance that crosses α = 0.01 by a few samples (R1 at 1.83 h, p = 0.011; R3 with
Cr at 1.67 h, p = 0.0100), so the tests assert them within half an hour. Up to 1 h every case has margin (p = 0.88,
0.92, 0.12 and 0.53), and the ratios of S to P stay within their band (asserted). The domain of validity is quoted in
the [reference page](../reference/covariance-propagation.md#modeling-assumptions).

The Monte Carlo of R1 runs in the Linux CI (about 2.5 min). Those of R2 and R3 run in the manual workflow
`long-validation.yml` (category `LongValidation`, 5 to 6.5 min each). The tests of that category are skipped unless
`IO_ASTRODYNAMICS_LONG_VALIDATION` is 1, which the workflow sets; it runs before each release, and on a pull request
that changes the dynamics they measure.

### Against GMAT (H2)

Φ, Ψ, P, Q and the partials of the forces are compared with GMAT R2026a, a third-party implementation: the official
Ubuntu build of the GMAT project, run from the command line (`GmatConsole`), and its own Python API for the A matrix
(version and checksums in `tools/gmat_reference/README.md`). The GMAT reports and console logs are committed with the
scripts that wrote them, so the tests (`GmatReferenceTests`, `GmatPartialsTests`, category `Validation`) do not need
GMAT; the manual workflow `gmat-reference.yml` runs GMAT again and checks that it writes the same reports byte for byte,
and the same console logs but for the date of the run and its duration. Every figure below was measured on 2026-10-20;
those published on 2026-10-19 came out with the same digits. The figures of P, Q and the partials were measured again on
2026-10-24, after [#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410), with the same
digits.

**Cases.** The reference cases where GMAT can be given the models of the library:

| Case | From | Orbit and force model | GMAT step |
|------|------|-----------------------|-----------|
| G1 | R1 | LEO 700 km, two-body, 1 day | 10 s |
| G2 | R2 without drag | LEO 400 km, EGM2008 10×10, Sun, Moon, 1 day | 10 s |
| G3 | R4 without SRP | GEO, EGM2008 70×70, Sun, Moon, the barycentres of the planets, 1 day | 60 s |
| G4 | R5 without drag | HEO 300 × 36 000 km, EGM2008 10×10, Sun, Moon, 2 revolutions | 10 s |
| G5 | R3 | SSO 700 km, EGM2008 10×10, Sun, Moon, SRP, an eclipse every revolution, 1 day | 2.5 s |
| G6 | R2 | LEO 400 km, EGM2008 10×10, Sun, Moon, drag (NRLMSISE-00), 1 day | 10 s |

The library runs RK7(8) at 1e-13; GMAT runs PrinceDormand78 at a fixed step, at which it propagates the covariance and
resolves Φ best. At each case's step, GMAT's Φ agrees with that at half the step to 3.5e-13 (G3) to 5.6e-11 (G5), 2.6e-5
for G6, and its state to 0.39 mm at most outside G6; at 10 s, G5 moved by 2 cm across the eclipses, as much as its
departure from the library, hence 2.5 s. At a fixed step, that agreement is set by the last digits of the field GMAT
is given: on G3, seven fields within 0.4 m of radius and 1.5e-8 of GM gave 3.4e-7 to 1.4e-5 m on the state, against the
5.8e-5 m between the two tools (#410).

**Models.** What GMAT is given, so that the comparison measures the computation:

| Model | Library | GMAT | Residual |
|-------|---------|------|----------|
| Frame | SPICE `J2000` | the state in `EarthICRF`, whose axes are those of `J2000` (the Sun of `de440s.bsp` equal to 2e-16); integration in `EarthMJ2000Eq`, the reports turned to ICRF by the constant rotation of the start (2.1e-7 rad) | none measurable |
| Two-body, geopotential | the central term with the GM of `gm_de440.tpc`; EGM2008 to degree 70, its harmonics with its own GM and reference radius (#410) | the same field converted to GMAT's `.cof`: the kernel GM in the header, which GMAT applies to both terms, the radius of EGM2008, every coefficient times GM_EGM2008 / GM_kernel | identical field, to the 15 digits of the `.cof` |
| Earth orientation | `ITRF93`, `earth_latest_high_prec.bpc` | IAU-1976/FK5 with GMAT's EOP file | about 2.6e-8 rad at the epoch of the cases |
| Third bodies | `de440s.bsp` | the same SPK through SPICE, the same GM | none measurable |
| SRP | cannonball, two-circle shadow | spherical, the same flux at 1 AU, AU, Earth and Sun radii | Φ: GMAT leaves out the derivative of the shadow function (its own warning, quoted under the Φ table) |
| Drag | NRLMSISE-00, F10.7 = F10.7A = 150, Ap = 4 | NRLMSISE-00, the same F10.7, Kp = 1 (ap = 4) | density within 5.0e-6 along G6 (mean −4.8e-7). Before [#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and [#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409) were fixed, the library's was 2.8 % lower to 0.13 % higher: its geodetic altitude, up to 1.7 km too high above the surface, then its TDB taken as UT (2.2e-3) |

**Φ.** Worst block over the arc (every hour and at the end), relative Frobenius norms. Total: the library against GMAT.
Models: the central differences of whole propagations of the library against GMAT's own, with the same steps (no
variational equation enters). Computation: each tool's Φ against its own central differences.

| Case | Total | Models | GMAT's computation | Library's computation | State departure |
|------|-------|--------|--------------------|-----------------------|-----------------|
| G1 | 7.6e-12 | 6.7e-9 | 1.7e-8 | 1.7e-8 | 53 µm, 5.6e-8 m/s |
| G2 | 8.4e-9 | 1.4e-8 | 1.7e-8 | 1.9e-8 | 5.7 cm, 6.4e-5 m/s |
| G3 | 4.8e-12 | 2.2e-8 | 1.7e-8 | 7.7e-9 | 58 µm, 2.8e-9 m/s |
| G4 | 3.8e-8 | 7.9e-8 | 4.6e-8 | 1.5e-8 | 25 cm, 1.8e-4 m/s |
| G5 | 1.0e-4 | 2.5e-7 | 9.9e-5 | 2.5e-7 | 8.0 mm, 9.1e-6 m/s |
| G6 | 3.7e-5 | 1.1e-6 | 3.6e-5 | 3.0e-8 | 11 cm, 1.3e-4 m/s |

Measured on 2026-11-01 with the TT time argument of
[#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501): GMAT's rows are at elapsed A1, so
TT, seconds; the library integrates over TT seconds about the Earth and is read at the same elapsed times, through its
dense output and the variational equations at an exact t (`InterpolateAtElapsed`), not at `Time` labels, which hold
TT₀ + t to 1e-7 s only (7.5e-4 m in LEO, about 1e-10 on Φ). The window of the cases is the duration in TT seconds, its
end one tick after the label of its end. Read at TDB offsets, G1's total would be 7.2e-9 and G5's state 5.5 cm. Only
the columns that depend on the library moved; GMAT's computation is GMAT's Φ against GMAT's own central differences,
which #501 does not touch. The totals are those of 2026-10-23 at the digits of the table. The cells that moved, the
value of 2026-10-23 in parentheses: G1's state 53 µm (5.25e-5 m; 52 µm); G5's state 8.0 mm and 9.1e-6 m/s (8.2 mm and
9.2e-6 m/s); and, within what central differences of whole propagations resolve, the models of G3 2.2e-8 (1.2e-8), G4
7.9e-8 (7.2e-8) and G5 2.5e-7 (1.5e-7), and the library's computation of G3 7.7e-9 (7.8e-9), G4 1.5e-8 (1.4e-8), G5
2.5e-7 (2.1e-7) and G6 3.0e-8 (2.4e-8).

Measured on 2026-10-23 with the field of #410 (the harmonics with the constants of EGM2008), GMAT run again with it:
the totals and the states of G1 to G4 moved by one unit of the third digit at most; G5's state moved from 7.5 mm, G6's total from 5.4e-5
(GMAT's step error, below); the models and computation columns, central differences of whole propagations, moved
within what they resolve.

- On G1 to G4 the two tools agree within what central differences of whole propagations resolve, 1e-8 to 7e-8, which
  bounds the model column; on G1 and G3 the total, 5e-12 to 8e-12, is the integration error of the two tools.
- G5: the total departure is GMAT's computation. Its Φ leaves out the derivative of the shadow function in the
  penumbra, which the library differentiates (analytically since #423, by central differences before; GMAT's
  Mathematical Specification, §4.2.5, has the derivative that its build leaves out); left out, the library
  departed by 1.05e-4 after one day in step 5b. The models agree to 2.5e-7 (1.5e-7 before #501, 2.8e-7 before #410 and 1.9e-7 before #434,
  each of which reshuffled the steps of the central differences of the library through the penumbra). GMAT prints, 23 times in the console log of
  each G5 run (`G5/nominal.log`, committed with the reports): *"Warning: The orbit state transition matrix does not
  currently contain SRP contributions from shadow partial derivatives when using Spherical SRP."* On this point the
  R2026a build departs from its Mathematical Specification, whose SRP partials include the derivative of the shadow
  function (§4.2.4 and §4.2.5, eqs. 4.79 and 4.83).
- G6: the total departure is GMAT's computation, 3.6e-5, mostly GMAT's step error at 10 s: its Φ moves by 2.6e-5
  between 10 s and 5 s (5.3e-5 and 4.2e-5 before #410). The models agree to 1.1e-6, the library's Φ holds its own
  central differences to 2.4e-8. Before
  #411 and #409 were fixed, the densities differed and the models departed by 7.1e-4 (total 6.9e-4, state 3.9 km).

**Ψ**, over the arc relative to its largest value, against GMAT's central differences on the coefficient (GMAT has no
Ψ): Ψ_Cr of G5 5.5e-6 (position) and 7.9e-6 (velocity), the central differences of the two tools against each other
3.1e-6 and 4.4e-6 (9.6e-6 and 1.4e-5 before #410, 4.1e-6 and 5.7e-6 before #434: the library's central differences
moved, its Ψ against GMAT did not, to two digits); Ψ_Cd of G6 7.8e-7 and 7.6e-7, the central differences of the two
tools against each other 8.2e-7 and 8.1e-7 (1.3e-2 before #411 and #409, the density).

**P**, from the correlated P0 of `CovariancePropagationTests` (σ 50, 200 and 30 m, 0.2, 0.05 and 0.03 m/s in RTN),
over the arc: G1 4.4e-9 relative Frobenius, 4.5e-9 on the correlation matrix; G2 1.2e-8 and 1.2e-6. The departure on
the correlation matrix splits into three parts, which the test checks to close:

| Part | G1 | G2 |
|------|----|----|
| GMAT's computation: its P against its own Φ P0 Φᵀ, which it propagates step by step | 4.4e-9 | 5.7e-9 |
| The library's computation: its P against its own Φ P0 Φᵀ | 9.0e-16 | 1.6e-15 |
| Φ alone: Φ P0 Φᵀ of the library against that of GMAT | 1.3e-10 | 1.3e-6 |

On G1 the departure is GMAT's, so P on G1 is held to 1e-8 rather than the 1e-10 of the design (decision of S. Guillet,
2026-10-19). On G2 it is Φ's, carried into a direction of small variance. The worst element is (y, vx) at 21 h, in
ICRF: there σ_y is 52 m against 11.6 km for the largest σ of the position block. An element of Φ P0 Φᵀ is the product
of two rows of Φ whitened by P0, so its departure on the correlation matrix is bounded by the departures of these two
rows in the metric of P0: 1.25e-6 for y, 2.9e-8 for vx, while Φ departs by 8.2e-9 per block at that epoch. That
departure of Φ, 8.4e-9 per block over the arc, is within what the model column resolves (1.4e-8). GMAT has no consider
parameter, so Ψ Pp Ψᵀ has no reference here.

**Q**, the state noise compensation of q_T = 1e-11 m²/s³ alone, at the end of the day. GMAT adds the discrete form of
Tapley, Schutz and Born §4.9 at every integration step, its RTN axes held over the step; the library integrates the
Lyapunov equation (Q to 6.3e-9 and 8.8e-9 between 1e-13 and 1e-14). GMAT's Q departs from the library's at first order
in its step:

| GMAT step | 20 s | 10 s | 5 s | 2.5 s |
|-----------|------|------|-----|-------|
| G1 | 2.35e-4 | 1.17e-4 | 5.87e-5 | 2.93e-5 |
| G2 | 2.34e-4 | 1.17e-4 | 5.84e-5 | 2.92e-5 |

Each halving halves the departure (ratios 2.000, 1.999 once), and the first-order limit 2 Q(Δt/2) − Q(Δt) is the
library's Q to 3.1e-9 to 1.0e-8 (G1) and 1.5e-8 to 2.7e-8 (G2). That agreement is bounded by the library's side: its
own Q moves by 6.3e-9 (G1) and 8.8e-9 (G2) between 1e-13 and 1e-14, not by GMAT's. To resolve Q, GMAT starts these runs from a P0 of 1 mm and 1 µm/s, and
Q is its P less its propagated P0: the difference of two covariances of the size of the P0 above resolved Q to 6e-5 at
best.

**Partials**, at the four states of B7, GMAT's A matrix against the library, relative Frobenius norms:

| Force | ∂a/∂r | ∂a/∂v |
|-------|-------|-------|
| Point-mass Earth | 2.8e-16 to 5.0e-16 | zero in both |
| EGM2008 70×70 | 2.2e-12 (GEO) to 2.2e-10 (HEO perigee) | zero in both |
| Moon, Sun | 3.0e-16 to 9.6e-16 | zero in both |
| SRP, in sunlight | 3.0e-16 to 6.7e-16 | zero in both |
| Drag | 3.3e-3 to 3.6e-3 | 6.9e-8 to 3.6e-7 |

The geopotential departure is the Earth orientation of the two tools. The drag acceleration agrees to 6.7e-8 to 4.0e-7
and its velocity partials as well; in position the drag partials keep the departure of GMAT's density gradient, since
the library's agrees with Ridders' reference to 5e-10 (B5). Before #411 and #409 were fixed, the drag partials were
compared with GMAT's scaled by the ratio of the accelerations (4.3 % at the HEO perigee). The analytic threshold of B
(1e-6) is asserted wherever the models agree, drag included; the other figures as records at twice the measurement,
rounded up at the second significant digit.

## Reference Validations

Beyond the conformance cases, the test suite compares the library with published references:

| Reference | What is compared | Tests | Tolerance |
|-----------|------------------|-------|-----------|
| NASA CARA `Pc2D_Foster_UnitTest`, Omitron case 01 | Foster 2D collision probability, against the exact Pc of its inputs (`tools/collision_probability`, mpmath at 50 digits; the published value is 2.9e-6 from it) | `CollisionProbability_NasaOmitronReferenceCase_MatchesPublishedValue`; through the entry points, `Analyze_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint` and `AnalyzeAll_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint` | Measured 2026-11-09 (Linux x64; provisional until the three platforms): 2.6e-15 relative, asserted at 5.3e-15; through the entry points 2.2e-7 (the covariance held in RTN over the refined TCA's offset), asserted at 4.4e-7 |
| NASA CARA `Pc2D_Foster_UnitTest`, Alfano case 03 | Foster 2D collision probability, against the exact Pc (the published value is 2.3e-6 from it) | `CollisionProbability_NasaAlfanoCase03_MatchesPublishedValue`, `Analyze_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint`, `AnalyzeAll_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint` | 0 and 4.8e-11 relative, asserted at 5.3e-15 and 4.4e-7 |
| Frisbee, maximum Pc with a single covariance | Critical covariance matrix; the Table 1 outcomes against the exact maximum Pc (published to two or three digits) | `SingleCovarianceMaximumPc_FrisbeeCriticalCovarianceMatchesPublishedMatrix`, `SingleCovarianceMaximumPc_FrisbeeTable1_MatchesPublishedApproximateOutcomes` | 1e-6 absolute on the matrix; 3.2e-16 relative on the four Table 1 cases, asserted at 5.3e-15 |
| The exact 2D Pc of CARA's definition (`tools/collision_probability`, mpmath at 50 digits, tanh-sinh and Gauss-Legendre agreeing to 1e-30) | The seven designed `collision_probability` encounters of lot 6 of #457 (Pc 2.1e-2 to 5.1e-12, a hard-body radius much larger than the covariance, a covariance not positive definite in the encounter plane, Frisbee's maximum), internally and through `Analyze`; the 25 reproductions of #610, #611 and #612; four covariances far wider than the disc with an off-axis miss (σ 1e5 to 1e6 m, R 1 m, at 70 digits; PC-1 of the lot's review); the private erf and erfc | `ConjunctionAssessmentTests.CollisionProbability_DesignedEncounters_MatchExactPc`, `Analyze_DesignedEncounters_MatchExactPcAndFlags`, `CollisionProbability_IssueReproductions_MatchExactPc`, `CollisionProbability_CovarianceFarWiderThanTheDisc_MatchExactPc`, `CollisionProbability_PrivateErrorFunctions_MatchMpmath` | Measured 2026-11-09 (Linux x64; provisional): 2.0e-14, 9.0e-9 through `Analyze`, 7.6e-15, 5.0e-11 (the erfc difference's own noise, about ε σ/R), 1.9e-16; each asserted at twice |
| Central differences of whole propagations (F1), *Numerical Recipes* §5.7 | Φ per 3×3 block and Ψ over the arc on R1 to R5, across a maneuver, and with a reduced degree of the geopotential partials; the plateau of the steps | `StmValidationTests`, `ManeuverStmValidationTests`, `GeopotentialPartialsDegreeTests`, `WholePropagationStepStudyTests` | R1 1e-7, R2 to R5 1e-4 outside the penumbra (specification); see [State Transition Matrix and Covariance](#state-transition-matrix-and-covariance) |
| Monte Carlo of the initial state (F3): Wishart variance (Anderson, 2003, §7.3) and Kolmogorov-Smirnov against χ²(6) (*Numerical Recipes* §14.3.3) | Propagated covariance against 5000 runs, R1 to R3 | `CovarianceMonteCarloTests`, `CovarianceMonteCarloLongTests`, `KolmogorovSmirnovTests` | 4 standard deviations element-wise; α = 0.01 |
| GMAT R2026a (H2), `GmatConsole` and GMAT's Python API | Φ per block over the arc on G1 to G6, Ψ_Cr and Ψ_Cd, P, the limit of GMAT's discrete process noise, and the partials of each force at the B7 states | `GmatReferenceTests`, `GmatPartialsTests`; the `stm` conformance case, `StmCaseTests` | G1 1e-10 on Φ, 1e-8 on P; the library's own computation to F1; the analytic partials to 1e-6 (B); the rest records at twice the measurement; see [Against GMAT](#against-gmat-h2) |
| CSPICE N0067 `georec_c`/`recgeo_c` and IAU SOFA `iauGd2gce`/`iauGc2gde` (`tools/geodetic_reference`) | Geodetic to body-fixed Cartesian coordinates and back on the Earth, Mars, the Moon and the prolate comet 103P/Hartley 2, from −5 km to 384 400 km of altitude (3744 points), and inside the evolute near the centre (292 points, with the nearest foot point in binary128); the topocentric coordinates of DSS-13 and the states of a custom site against SPICE; the method of Gauss on observations built by SPICE from a reference orbit seen from the exact site (`iod_observations.c`) | `GeodeticCoordinatesTests`, `PlanetocentricTests`, `PlanetodeticTests`, `SiteTests`, `ScenarioTests.PropagateSite`, `InitialOrbitDeterminationTests.FromTheExactSite*` | Measured 2026-10-19: position 4.4e-16 of the distance, latitude 1.0e-15 rad, altitude 5.7e-16 of max(\|h\|, a); asserted at 1e-15 (latitude 2e-15); inside the evolute, against the nearest foot point computed in binary128 (`ExactGeodetic` of `geodetic_ref.c`, measured 2026-10-20), latitude 2.8e-16 rad and altitude 1.8e-15 of a, asserted at 4e-15; SPICE departs from that foot point by up to 2.4e-10 rad, its own closure there being 1.9e-12 of a against the library's 1.8e-15; the site of the Gauss observations within 2.8e-4 m of SPICE's, the determined state 9.58e3 m and 77.5 m/s from the reference orbit at 3 min of spacing, the error of the method, of second order in the spacing (ratio 4.00 per halving), held to twice that |
| IAU SOFA, release 2023-10-11 | Frame bias, precession, nutation, CIP, CIO locator, Earth rotation angle, GCRS to CIRS and to TIRS | `Iau2006ModelTests`, `Iau2006FundamentalArgumentsTests`, `FrameChainIntegrationTests`, `Iau2006SofaSweepTests` (300 epochs, 1990 to 2040), with reference values from the harnesses in `tools/sofa_reference` | From 1e-14 rad (precession angles) to 5e-9 rad (IAU 2000B against SOFA's IAU 2000A); see [Standards & Units](../standards-and-units.md#measured-agreement-with-sofa) |

## See Also

- [High-Fidelity Propagation](high-fidelity-propagation.md)
- [Conjunction Assessment](../reference/conjunction-assessment.md)
