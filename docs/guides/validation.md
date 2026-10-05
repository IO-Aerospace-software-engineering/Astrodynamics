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
3. **Confirm covariance realism** — verify that participant covariances and hard-body radii are realistic and current. The library does not propagate covariance to TCA; see [Conjunction Assessment](../reference/conjunction-assessment.md).
4. **Schema-validate CDM products** using `Cdm.ValidateSchema()` or `Cdm.ValidateSchemaFromXml()` before exchanging them with external systems.
5. **Treat edge cases carefully** — low-relative-velocity encounters may require analysis beyond the standard 2D Pc workflow.

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
| Earth orientation of the gravity field | SPICE `ITRF93` with `earth_latest_high_prec.bpc`, EOP included | GMAT's own Earth-fixed reduction and EOP file | Not measured | Open question |
| Inertial frame | `Frame.ICRF` (SPICE `J2000`, ICRF-aligned) | `EarthMJ2000Eq` (EME2000) | At most the ~23 mas frame bias, about 0.8 m in LEO and 4.7 m in GEO, depending on how GMAT ties its ephemerides to EarthMJ2000Eq | Open question |
| Ephemerides | `de440s.bsp` | `de440s.bsp`, the same file (identical MD5) | None | Verified |
| Constants (Earth GM, reference radius of the field) | From the EGM2008 field and the SPICE kernels | GMAT defaults and the JGM3 file; `Earth.Mu` is not overridden in the scripts of cases 001 to 003 | Not measured | Open question |
| Time scales | UTC window converted to TDB | Elapsed time `ElapsedSecs = 86400` | Sampling the end of the window in the wrong scale shifts it by about 18 µs, i.e. 0.14 m in LEO (measured: 12.99 m against 13.13 m) | Resolved: tests and runner both use the UTC end converted to TDB |
| Integration error | RK7(8), tolerances 1e-11 | PrinceDormand78, accuracy 1e-13 | 0.08 m over one sidereal day at GEO (two-body cases 004 and 005) | Measured |

## Reference Validations

Beyond the conformance cases, the test suite compares the library with published references:

| Reference | What is compared | Tests | Tolerance |
|-----------|------------------|-------|-----------|
| NASA CARA `Pc2D_Foster_UnitTest`, Omitron case 01 | Foster 2D collision probability | `CollisionProbability_NasaOmitronReferenceCase_MatchesPublishedValue`; through the entry points, `Analyze_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint` and `AnalyzeAll_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint` | 1e-5 relative to the published Pc |
| NASA CARA `Pc2D_Foster_UnitTest`, Alfano case 03 | Foster 2D collision probability | `CollisionProbability_NasaAlfanoCase03_MatchesPublishedValue`, `Analyze_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint`, `AnalyzeAll_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint` | 1e-5 relative to the published Pc |
| Frisbee, maximum Pc with a single covariance | Critical covariance matrix and Table 1 outcomes | `SingleCovarianceMaximumPc_FrisbeeCriticalCovarianceMatchesPublishedMatrix`, `SingleCovarianceMaximumPc_FrisbeeTable1_MatchesPublishedApproximateOutcomes` | 1e-6 absolute on the matrix; 3 % relative on the four Table 1 cases |
| IAU SOFA, release 2023-10-11 | Frame bias, precession, nutation, CIP, CIO locator, Earth rotation angle, GCRS to CIRS and to TIRS | `Iau2006ModelTests`, `Iau2006FundamentalArgumentsTests`, `FrameChainIntegrationTests`, `Iau2006SofaSweepTests` (300 epochs, 1990 to 2040), with reference values from the harnesses in `tools/sofa_reference` | From 1e-14 rad (precession angles) to 5e-9 rad (IAU 2000B against SOFA's IAU 2000A); see [Standards & Units](../standards-and-units.md#measured-agreement-with-sofa) |

## See Also

- [High-Fidelity Propagation](high-fidelity-propagation.md)
- [Conjunction Assessment](../reference/conjunction-assessment.md)
