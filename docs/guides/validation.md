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
3. **Confirm covariance realism** — verify that participant covariances and hard-body radii are realistic and current.
4. **Schema-validate CDM products** using `Cdm.ValidateSchema()` or `Cdm.ValidateSchemaFromXml()` before exchanging them with external systems.
5. **Treat edge cases carefully** — low-relative-velocity encounters may require analysis beyond the standard 2D Pc workflow.

## Conformance Tests

The test suite includes conformance cases validated against reference trajectories covering LEO, GEO, and SSO orbits with various force-model configurations. These cases verify position agreement to within 300 m and velocity agreement to within 0.35 m/s over 24-hour propagation windows (differences are dominated by gravity-model variants, not integrator error).

Run the conformance suite with:

```bash
cd IO.Astrodynamics.Net
dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj --filter "DisplayName~Conformance"
```

## See Also

- [High-Fidelity Propagation](high-fidelity-propagation.md)
- [Conjunction Assessment](../reference/conjunction-assessment.md)
