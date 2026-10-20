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
suite: inputs, golden outputs produced by an external reference tool, and tolerances. Two
harnesses run them:

- the conformance runner `IO.Astrodynamics.ConformanceRunner` runs every case with the adaptive
  RK7(8) integrator (absolute and relative tolerances 1e-11) and writes a report,
  `IO.Astrodynamics.Net/conformance-report.json`;
- unit tests run the three 24-hour propagation cases with RK7(8) (`RK78IntegratorTests.Conformance00x_*`)
  and with Velocity-Verlet at a fixed 1 s step (`PropagatorTests.Conformance00x_*`), and print the
  errors they measure.

Both evaluate the final state at the exact end of the window, whose UTC epoch is converted to TDB,
like the reference tool's elapsed time, and report the magnitude of the position and velocity errors.

### Measured Errors

Measured on 2026-10-04. RK7(8): conformance runner report (IO.Astrodynamics 10.x, the framework
commit is recorded in the report). Velocity-Verlet: output of the `PropagatorTests.Conformance00x_*` tests.

| Case | Orbit and force model (inputs) | Golden | RK7(8), 1e-11 | Velocity-Verlet, 1 s |
|------|--------------------------------|--------|---------------|----------------------|
| `propagator_24h_leo_grav10_001` | LEO about 6,800 km, 24 h; EGM2008 10x10, Moon, Sun | GMAT R2025a, JGM3 10x10 | 13.13 m, 14.8 mm/s | 294.3 m, 332.0 mm/s |
| `propagator_24h_geo_grav70_002` | GEO (INTELSAT 901), 24 h; EGM2008 70x70, Moon, Sun, planets | GMAT R2025a, JGM3 70x70 | 8.02 m, 0.57 mm/s | 8.48 m, 0.61 mm/s |
| `propagator_24h_sso_grav10_003` | SSO 700 km, 24 h; EGM2008 10x10, Moon, Sun | GMAT R2025a, JGM3 10x10 | 3.56 m, 3.84 mm/s | 246.4 m, 258.4 mm/s |
| `propagator_1sd_geo_twobody_004` | GEO equatorial, one sidereal day; two-body | Kepler's third law | 0.08 m, 0.006 mm/s | not run |
| `propagator_1sd_geo_i30_twobody_005` | GEO inclined 30 deg, one sidereal day; two-body | Kepler's third law | 0.08 m, 0.006 mm/s | not run |

The test limits are non-regression guards set a few percent above these values, not accuracy
specifications:

| Case | RK7(8) limits | Velocity-Verlet limits |
|------|---------------|------------------------|
| `propagator_24h_leo_grav10_001` | 13.5 m, 15.5 mm/s | 300 m, 350 mm/s |
| `propagator_24h_geo_grav70_002` | 8.3 m, 0.6 mm/s | 9 m, 0.7 mm/s |
| `propagator_24h_sso_grav10_003` | 4 m, 4 mm/s | 250 m, 260 mm/s |

With RK7(8), the remaining few meters come from the differences between the models of the two
tools (see [Error Budget](#error-budget)). With Velocity-Verlet at 1 s, the LEO and SSO errors are
almost entirely the truncation error of the integrator: 279.5 m over 24 h on the two-body LEO orbit
alone (see [Integrators](../reference/integrators.md#accuracy-and-cost)). At GEO the orbital motion
is slow enough for a 1 s step to be nearly exact.

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

Known sources of the few-meter residual between IO.Astrodynamics (RK7(8)) and the GMAT goldens.
None of them has been isolated yet; each open point is tracked.

| Source | IO.Astrodynamics | Golden (GMAT) | Effect | Status |
|--------|------------------|---------------|--------|--------|
| Gravity field | EGM2008, tide-free, truncated to the case degree | JGM3 at the same degree | Not measured; expected to dominate | Open: goldens to regenerate with EGM2008 ([conformance-tests#5](https://github.com/IO-Aerospace-software-engineering/conformance-tests/issues/5)) |
| Earth orientation of the gravity field | SPICE `ITRF93` with `earth_latest_high_prec.bpc`, EOP included | GMAT's own Earth-fixed reduction and EOP file | Measured with GMAT R2026a (2026-10-19): from `EarthICRF`, GMAT's Earth-fixed frame is within about 2.6e-8 rad of `ITRF93`; from `EarthMJ2000Eq` coordinates taken as ICRF, as in the scripts of cases 001 to 003, 1.8e-7 rad ([Against GMAT](#against-gmat-h2)) | Measured; the effect on the goldens of cases 001 to 003 not isolated |
| Inertial frame | `Frame.ICRF` (SPICE `J2000`, ICRF-aligned) | `EarthMJ2000Eq` (EME2000) | Measured with GMAT R2026a (2026-10-19): GMAT turns `EarthICRF`, whose axes are those of `J2000`, to `EarthMJ2000Eq` by 2.1e-7 rad (43.7 mas) at 2021-03-20 12:00 TDB, mostly about y, drifting by 9.3e-10 rad over the day; about 1.5 m at 7000 km and 8.9 m in GEO ([Against GMAT](#against-gmat-h2)) | Measured at the epoch of the H2 cases; the effect on the goldens of cases 001 to 003, at their own epochs, not isolated |
| Ephemerides | `de440s.bsp` | `de440s.bsp`, the same file (identical MD5) | None | Verified |
| Constants (Earth GM, reference radius of the field) | From the EGM2008 field and the SPICE kernels | GMAT defaults and the JGM3 file; `Earth.Mu` is not overridden in the scripts of cases 001 to 003 | Not measured | Open question |
| Time scales | UTC window converted to TDB | Elapsed time `ElapsedSecs = 86400` | Sampling the end of the window in the wrong scale shifts it by about 18 µs, i.e. 0.14 m in LEO (measured: 12.99 m against 13.13 m) | Resolved: tests and runner both use the UTC end converted to TDB |
| Integration error | RK7(8), tolerances 1e-11 | PrinceDormand78, accuracy 1e-13 | 0.08 m over one sidereal day at GEO (two-body cases 004 and 005) | Measured |

## State Transition Matrix and Covariance

The state transition matrix Φ, the sensitivity Ψ to Cd and Cr and the propagated covariance P
([Covariance Propagation](../reference/covariance-propagation.md)) are validated end to end, against references
independent of the variational equations: central differences of whole propagations (F1) and a Monte Carlo of the
initial state (F3). The cases are those of the specification of phase 2, feature 1, all on 2021-03-20 at 12:00 UTC
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
| R2 | 4.6e-8 | 3.6e-8 | 4.6e-8 | 3.6e-8 | Cd 1.6e-7 | Cd 1.3e-7 | |
| R3 | 7.3e-7 | 9.1e-7 | 7.3e-7 | 9.1e-7 | Cr 9.1e-6 | Cr 9.2e-5 | 30 crossings: Φ 7.1e-7 to 9.0e-7, Ψ_Cr 7.2e-6 |
| R4 | 1.8e-8 | 2.7e-8 | 2.8e-8 | 2.2e-8 | Cr 4.9e-7 | Cr 3.1e-8 | 2 crossings: Φ 1.3e-8, Ψ_Cr 9.8e-9 |
| R5 | 6.7e-7 | 3.3e-7 | 6.7e-7 | 3.3e-7 | Cd 5.6e-8 | Cd 6.4e-8 | |

Worst over the arc, outside the penumbra, each relative to the local reference (`StmValidationTests`). The errors in the
penumbra are no larger than outside: the SRP partials take central differences in and near the penumbra, so the
shadow function is differentiated.

Ψ is also held over the arc relative to its largest value, a measure that does not depend on what the reference
resolves where Ψ is small: R2 Ψ_Cd 1.2e-7 (position and velocity), R3 Ψ_Cr 1.5e-6 and 2.1e-6, R4 Ψ_Cr 2.0e-9 and
1.9e-9, R5 Ψ_Cd 4.6e-8 and 5.3e-8. The local 9.2e-5 of Ψ_Cr of R3 in velocity, at 9.5 h, measures the reference, not
Ψ, and is published as a record rather than held to the threshold. The velocity part of Ψ_Cr passes there through a
local minimum, 7.9e-4 m/s per unit Cr, and the local figure follows the step of the reference: 3.3e-4 at 5 % of Cr,
9.2e-5 at 10 %, 9.9e-5 at 20 %. Over the arc the same comparisons give 9.5e-6, 2.1e-6 and 9.4e-6. Without
`IncludeInStepControl`, Ψ_Cr is off by 2.2e-5 (position) and 2.2e-4 (velocity) over the arc of R3, and by 4.9e-7 and
3.1e-8 for R4, relative to the local reference.

**Steps of the reference** (`WholePropagationStepStudyTests`): position steps from 1 cm to 1 km (the velocity step 1e-3
s⁻¹ times the position step), and the coefficients from 0.1 % to 30 %. On the small steps the integration noise of the
perturbed propagations dominates, on the large ones the δ² truncation. The chosen steps are within a factor of 2 of the
smallest error of each sweep, at the end of the arc: 10 m and 1 cm/s (R1 to R4, Eclipsing), 1 m and 1 mm/s (R5); Cd 1 %
(R2), 5 % (R5), 30 % (Eclipsing); Cr 10 % (R3, R4), 30 % (Eclipsing). For Φ each sweep has its minimum between the two
sides. For Ψ two sweeps show the noise floor of the reference rather than a plateau: Ψ_Cr of R3, which is not
monotone, and Ψ_Cr of the eclipsing case. Since #398 the latter is published and not asserted: 1.4e-3, 7.2e-4, 2.4e-4,
1.1e-5, 1.3e-4 and 4.7e-5 from 0.1 % to 30 %, so no step lies on a plateau there. On the code before #398, the start of
the reference moved by 2e-9 m along x alone takes that sweep from 3.7e-3, 1.8e-4, 2.3e-4, 1.3e-4, 3.4e-5, 1.8e-5 to
2.1e-2, 7.0e-5, 2.4e-5, 8.1e-5, 7.2e-6, 1.5e-5. Ψ_Cd of the eclipsing case is held at its chosen step, 30 %, to a
record of 7.0e-7 since [#413](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/413) (decision of
S. Guillet, 2026-10-20), rather than within a factor of 2 of the smallest error of its sweep. Its references fire the burn
of the tested propagation in open loop, and that burn moves at the level of the noise with the steps of the variational
equations: moved by one tick of 100 ns, it takes the sweep from 5 % to 30 % from 2.8e-7, 3.0e-7, 1.1e-8, 1.5e-7 to
2.7e-7, 3.7e-7, 2.2e-7, 3.5e-7 (one tick earlier) or 9.4e-7, 8.0e-7, 4.3e-7, 2.8e-7 (one tick later), while the tested
Ψ_Cd moves by 3e-11. The record is twice the worst at 30 % of seven burns from −20 to +20 ticks. The reference runs at
1e-14 rather than the 1e-13 of the specification. Its smallest error on Φ is lower in R1 to R4: R1 1.7e-8 against
2.3e-8, R2 4.6e-8 against 1.4e-7, R3 1.9e-7 against 1.0e-6, R4 2.8e-8 against 5.6e-8. Both resolve R5 alike: over five
starts moved by −2e-9 to 2e-9 m along x, 4.1e-7 to 8.0e-7 at 1e-14 and 1.1e-7 to 7.0e-7 at 1e-13 (6.7e-7 and 7.8e-7
before [#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and
[#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409)), so R5 is held at 1e-14 to a
record of 1.7e-6, twice the worst (decision of S. Guillet, 2026-10-20).

**Start of the reference.** A propagation takes a state vector given at the start of its window as it is, so each
reference propagation starts from exactly its perturbed state. Until
[#398](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/398), the initial state went through
the Keplerian elements (`OrbitalParameters.AtEpoch`). For a near-circular orbit inclined by less than 1e-3 rad (R4),
the true anomaly was wrong, which moved a perturbed state by millimetres to decimetres. Its derivative was not the
identity either, so central differences through it left a floor of 2.0e-5 on Φ of R4 (2.2e-4 with the references of
step 9). The true anomaly fixed alone, the round trip would return the state to its rounding, about 1e-8 m. That is
enough for R4 (Φ 3.3e-8), but not for two references: Ψ_Cr of R3, which is resolved only to the noise of its reference,
and the test after a maneuver, which compares the states bit for bit. Hence the state taken as it is: R4 Φ 2.8e-8.

**Across a maneuver** (`ManeuverStmValidationTests`, Eclipsing case). Φ and Ψ model the burn in open loop: the epoch
and the ΔV are those of the nominal trajectory. Against a reference that fires that nominal burn (fixed in epoch and
ΔV), the errors are as follows:

- Φ: 5.5e-8 at most per block, 5.2e-8 in the 7 penumbra crossings (published as a record, 1.1e-7; 2.9e-8 before
  #398);
- Ψ_Cd: 7.5e-8 over the arc relative to its largest value, and 3.7e-6 relative to the local value after the burn;
- Ψ_Cr: 5.2e-5 in position and 9.4e-5 in velocity over the arc (4.4e-6 before #398), and 3.7e-4 relative to the
  local value after the burn (1.3e-5 before #398), records rather than F1 figures.

Since [#398](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/398) the propagations start
from exactly the state given, about 1e-9 m from where they started before. That shift alone moves the reference of
Ψ_Cr, whose propagations choose their own steps, by a factor of 2 to 30. On the code before the fix, the start of the
reference moved by 2e-9 m along x takes Ψ_Cr over the arc from 4.4e-6 to 9.3e-6, locally after the burn from 1.3e-5 to
5.4e-5, and Φ from 2.9e-8 to 4.3e-8. The Ψ_Cr under test does not move: with Φ and Ψ in the step control it is the same
before and after the fix to 2.5e-10 of its largest value, while its reference moves by up to 6.7e-5. Ψ_Cr is therefore
held to F1 against the same propagation at 1e-14 with Φ and Ψ in the step control, without central differences: 2.1e-12
in position and 3.0e-12 in velocity over the arc, 1.2e-8 at most relative to the local value wherever it is not zero.
Against the central differences it is published as a record, over the arc and locally.

Ψ starts from zero (in the shadow for Cr, at 1000 km for Cd), where the reference does not resolve it relative to its
local value. Against a reference that retargets the apogee raise at its own perigee (closed loop), Φ is the same
before the burn (below 6.8e-9) and off by 0.48, 0.33, 0.42 and 0.23 per block after it. Ψ_Cd is off by 0.37 and Ψ_Cr
by 1.2 of their largest values. This is the measured effect of the open-loop assumption: the propagated covariance is
that of a burn executed as commanded, with its execution error, not that of a maneuver that retargets its ΔV.

**Reduced degree of the geopotential partials** (`GeopotentialPartialsDegreeTests`). The internal option
`GeopotentialPartialsDegree` keeps the full degree in the acceleration but reduces it in the partials. Worst block of Φ
over the arc, and the cost of the propagation with Φ against the propagation without it (median of 3, Release,
informative, measured with the test alone: run in parallel with the other Validation classes, the times move with the
load):

| Case | Degree 2 | Degree 8 | Degree 20 | Full degree | Cost |
|------|----------|----------|-----------|-------------|------|
| R2 (10×10) | 2.0e-3 | 8.5e-4 | 4.6e-8 (the full degree) | 4.6e-8 | 5.5× to 6.0× at every degree (0.93 to 1.03 s, against 0.17 s): the drag partials dominate |
| R4 (70×70) | 1.2e-6 | 2.8e-8 | 2.8e-8 | 2.8e-8 | 1.0× to 1.1× up to degree 20, 1.5× at 70 (0.25 s, against 0.17 s) |

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
| R3, Cr sampled (σ = 0.15) | 2.14: passes up to 24 h | p = 0.011 (1.646 h) | up to 1.67 h; fails from 1.83 h (p = 3.2e-3) | `CovarianceMonteCarloLongTests` |

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
−188 m; without, the position at 24 h from 4.99 to 5.00. The other figures are unchanged.

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
`long-validation.yml` (category `LongValidation`, 5 to 6.5 min each).

### Against GMAT (H2)

Φ, Ψ, P, Q and the partials of the forces are compared with GMAT R2026a, a third-party implementation: the official
Ubuntu build of the GMAT project, run from the command line (`GmatConsole`), and its own Python API for the A matrix
(version and checksums in `tools/gmat_reference/README.md`). The GMAT reports and console logs are committed with the
scripts that wrote them, so the tests (`GmatReferenceTests`, `GmatPartialsTests`, category `Validation`) do not need
GMAT; the manual workflow `gmat-reference.yml` runs GMAT again and checks that it writes the same reports byte for byte,
and the same console logs but for the date of the run and its duration. Every figure below was measured on 2026-10-20;
those published on 2026-10-19 came out with the same digits.

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
resolves Φ best. At each case's step, GMAT's Φ agrees with that at half the step to 4.1e-14 (G3) to 5.0e-11 (G5), 4.2e-5
for G6, and its state to 0.35 mm at most outside G6; at 10 s, G5 moved by 2 cm across the eclipses, as much as its
departure from the library, hence 2.5 s.

**Models.** What GMAT is given, so that the comparison measures the computation:

| Model | Library | GMAT | Residual |
|-------|---------|------|----------|
| Frame | SPICE `J2000` | the state in `EarthICRF`, whose axes are those of `J2000` (the Sun of `de440s.bsp` equal to 2e-16); integration in `EarthMJ2000Eq`, the reports turned to ICRF by the constant rotation of the start (2.1e-7 rad) | none measurable |
| Two-body, geopotential | GM of `gm_de440.tpc`, EGM2008 to degree 70, the radius of `pck00011.tpc` | the same field converted to GMAT's `.cof`, with the same GM and radius | identical field |
| Earth orientation | `ITRF93`, `earth_latest_high_prec.bpc` | IAU-1976/FK5 with GMAT's EOP file | about 2.6e-8 rad at the epoch of the cases |
| Third bodies | `de440s.bsp` | the same SPK through SPICE, the same GM | none measurable |
| SRP | cannonball, two-circle shadow | spherical, the same flux at 1 AU, AU, Earth and Sun radii | Φ: GMAT leaves out the derivative of the shadow function (its own warning, quoted under the Φ table) |
| Drag | NRLMSISE-00, F10.7 = F10.7A = 150, Ap = 4 | NRLMSISE-00, the same F10.7, Kp = 1 (ap = 4) | density within 5.0e-6 along G6 (mean −4.8e-7). Before [#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411) and [#409](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/409) were fixed, the library's was 2.8 % lower to 0.13 % higher: its geodetic altitude, up to 1.7 km too high above the surface, then its TDB taken as UT (2.2e-3) |

**Φ.** Worst block over the arc (every hour and at the end), relative Frobenius norms. Total: the library against GMAT.
Models: the central differences of whole propagations of the library against GMAT's own, with the same steps (no
variational equation enters). Computation: each tool's Φ against its own central differences.

| Case | Total | Models | GMAT's computation | Library's computation | State departure |
|------|-------|--------|--------------------|-----------------------|-----------------|
| G1 | 7.6e-12 | 6.7e-9 | 1.7e-8 | 1.7e-8 | 52 µm, 5.6e-8 m/s |
| G2 | 8.4e-9 | 1.3e-8 | 1.6e-8 | 2.0e-8 | 5.7 cm, 6.4e-5 m/s |
| G3 | 4.9e-12 | 1.8e-8 | 1.3e-8 | 4.6e-9 | 58 µm, 2.8e-9 m/s |
| G4 | 3.8e-8 | 6.0e-8 | 5.2e-8 | 2.1e-8 | 25 cm, 1.8e-4 m/s |
| G5 | 1.0e-4 | 1.9e-7 | 9.9e-5 | 5.2e-7 | 8.2 mm, 9.4e-6 m/s |
| G6 | 5.4e-5 | 9.0e-7 | 5.3e-5 | 4.9e-8 | 11 cm, 1.3e-4 m/s |

- On G1 to G4 the two tools agree within what central differences of whole propagations resolve, 1e-8 to 6e-8, which
  bounds the model column; on G1 and G3 the total, 5e-12 to 8e-12, is the integration error of the two tools.
- G5: the total departure is GMAT's computation. Its Φ leaves out the derivative of the shadow function in the
  penumbra, which the library differentiates (the SRP partials take central differences there); left out, the library
  departed by 1.05e-4 after one day in step 5b. The models agree to 1.9e-7. GMAT prints, 23 times in the console log of
  each G5 run (`G5/nominal.log`, committed with the reports): *"Warning: The orbit state transition matrix does not
  currently contain SRP contributions from shadow partial derivatives when using Spherical SRP."* On this point the
  R2026a build departs from its Mathematical Specification, whose SRP partials include the derivative of the shadow
  function (§4.2.4 and §4.2.5, eqs. 4.79 and 4.83).
- G6: the total departure is GMAT's computation, 5.3e-5, mostly GMAT's step error at 10 s: its Φ moves by 4.2e-5
  between 10 s and 5 s. The models agree to 9.0e-7, the library's Φ holds its own central differences to 4.9e-8. Before
  #411 and #409 were fixed, the densities differed and the models departed by 7.1e-4 (total 6.9e-4, state 3.9 km).

**Ψ**, over the arc relative to its largest value, against GMAT's central differences on the coefficient (GMAT has no
Ψ): Ψ_Cr of G5 5.5e-6 (position) and 7.9e-6 (velocity), the central differences of the two tools against each other
4.1e-6 and 5.7e-6; Ψ_Cd of G6 7.7e-7 and 7.6e-7, the central differences of the two tools against each
other 8.1e-7 and 8.0e-7 (1.3e-2 before #411 and #409, the density).

**P**, from the correlated P0 of `CovariancePropagationTests` (σ 50, 200 and 30 m, 0.2, 0.05 and 0.03 m/s in RTN),
over the arc: G1 4.4e-9 relative Frobenius, 4.5e-9 on the correlation matrix; G2 2.1e-8 and 1.3e-6. The departure on
the correlation matrix splits into three parts, which the test checks to close:

| Part | G1 | G2 |
|------|----|----|
| GMAT's computation: its P against its own Φ P0 Φᵀ, which it propagates step by step | 4.4e-9 | 2.3e-8 |
| The library's computation: its P against its own Φ P0 Φᵀ | 9.0e-16 | 1.5e-15 |
| Φ alone: Φ P0 Φᵀ of the library against that of GMAT | 1.3e-10 | 1.3e-6 |

On G1 the departure is GMAT's, so P on G1 is held to 1e-8 rather than the 1e-10 of the design (decision of S. Guillet,
2026-10-19). On G2 it is Φ's, carried into a direction of small variance. The worst element is (y, vx) at 21 h, in
ICRF: there σ_y is 52 m against 11.6 km for the largest σ of the position block. An element of Φ P0 Φᵀ is the product
of two rows of Φ whitened by P0, so its departure on the correlation matrix is bounded by the departures of these two
rows in the metric of P0: 1.24e-6 for y, 2.9e-8 for vx, while Φ departs by 8.2e-9 per block at that epoch. That
departure of Φ, 8.4e-9 per block over the arc, is within what the model column resolves (1.3e-8). GMAT has no consider
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
library's Q to 3.1e-9 to 1.0e-8 (G1) and 1.9e-8 to 2.1e-8 (G2). That agreement is bounded by the library's side: its
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
| NASA CARA `Pc2D_Foster_UnitTest`, Omitron case 01 | Foster 2D collision probability | `CollisionProbability_NasaOmitronReferenceCase_MatchesPublishedValue`; through the entry points, `Analyze_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint` and `AnalyzeAll_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint` | 1e-5 relative to the published Pc |
| NASA CARA `Pc2D_Foster_UnitTest`, Alfano case 03 | Foster 2D collision probability | `CollisionProbability_NasaAlfanoCase03_MatchesPublishedValue`, `Analyze_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint`, `AnalyzeAll_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint` | 1e-5 relative to the published Pc |
| Frisbee, maximum Pc with a single covariance | Critical covariance matrix and Table 1 outcomes | `SingleCovarianceMaximumPc_FrisbeeCriticalCovarianceMatchesPublishedMatrix`, `SingleCovarianceMaximumPc_FrisbeeTable1_MatchesPublishedApproximateOutcomes` | 1e-6 absolute on the matrix; 3 % relative on the four Table 1 cases |
| Central differences of whole propagations (F1), *Numerical Recipes* §5.7 | Φ per 3×3 block and Ψ over the arc on R1 to R5, across a maneuver, and with a reduced degree of the geopotential partials; the plateau of the steps | `StmValidationTests`, `ManeuverStmValidationTests`, `GeopotentialPartialsDegreeTests`, `WholePropagationStepStudyTests` | R1 1e-7, R2 to R5 1e-4 outside the penumbra (specification); see [State Transition Matrix and Covariance](#state-transition-matrix-and-covariance) |
| Monte Carlo of the initial state (F3): Wishart variance (Anderson, 2003, §7.3) and Kolmogorov-Smirnov against χ²(6) (*Numerical Recipes* §14.3.3) | Propagated covariance against 5000 runs, R1 to R3 | `CovarianceMonteCarloTests`, `CovarianceMonteCarloLongTests`, `KolmogorovSmirnovTests` | 4 standard deviations element-wise; α = 0.01 |
| GMAT R2026a (H2), `GmatConsole` and GMAT's Python API | Φ per block over the arc on G1 to G6, Ψ_Cr and Ψ_Cd, P, the limit of GMAT's discrete process noise, and the partials of each force at the B7 states | `GmatReferenceTests`, `GmatPartialsTests`; the `stm` conformance case, `StmCaseTests` | G1 1e-10 on Φ, 1e-8 on P; the library's own computation to F1; the analytic partials to 1e-6 (B); the rest records at twice the measurement; see [Against GMAT](#against-gmat-h2) |
| CSPICE N0067 `georec_c`/`recgeo_c` and IAU SOFA `iauGd2gce`/`iauGc2gde` (`tools/geodetic_reference`) | Geodetic to body-fixed Cartesian coordinates and back on the Earth, Mars, the Moon and the prolate comet 103P/Hartley 2, from −5 km to 384 400 km of altitude (3744 points), and inside the evolute near the centre (292 points, with the nearest foot point in binary128); the topocentric coordinates of DSS-13 and the states of a custom site against SPICE; the method of Gauss on observations built by SPICE from a reference orbit seen from the exact site (`iod_observations.c`) | `GeodeticCoordinatesTests`, `PlanetocentricTests`, `PlanetodeticTests`, `SiteTests`, `ScenarioTests.PropagateSite`, `InitialOrbitDeterminationTests.FromTheExactSite*` | Measured 2026-10-19: position 4.4e-16 of the distance, latitude 1.0e-15 rad, altitude 5.7e-16 of max(\|h\|, a); asserted at 1e-15 (latitude 2e-15); inside the evolute, against the nearest foot point computed in binary128 (`ExactGeodetic` of `geodetic_ref.c`, measured 2026-10-20), latitude 2.8e-16 rad and altitude 1.8e-15 of a, asserted at 4e-15; SPICE departs from that foot point by up to 2.4e-10 rad, its own closure there being 1.9e-12 of a against the library's 1.8e-15; the site of the Gauss observations within 2.8e-4 m of SPICE's, the determined state 9.58e3 m and 77.5 m/s from the reference orbit at 3 min of spacing, the error of the method, of second order in the spacing (ratio 4.00 per halving), held to twice that |
| IAU SOFA, release 2023-10-11 | Frame bias, precession, nutation, CIP, CIO locator, Earth rotation angle, GCRS to CIRS and to TIRS | `Iau2006ModelTests`, `Iau2006FundamentalArgumentsTests`, `FrameChainIntegrationTests`, `Iau2006SofaSweepTests` (300 epochs, 1990 to 2040), with reference values from the harnesses in `tools/sofa_reference` | From 1e-14 rad (precession angles) to 5e-9 rad (IAU 2000B against SOFA's IAU 2000A); see [Standards & Units](../standards-and-units.md#measured-agreement-with-sofa) |

## See Also

- [High-Fidelity Propagation](high-fidelity-propagation.md)
- [Conjunction Assessment](../reference/conjunction-assessment.md)
