# Covariance Propagation Provenance

This page traces every equation implemented for the state transition matrix and covariance work (phase 2,
feature 1). For each one it gives the quantity, where it is implemented, the published reference it comes from,
the tests that check it, and the status of the citation:

- **Verified on the text**: the cited passage was read and matches the implementation.
- **To be verified by S. Guillet**: the reference could not be read from the development environment. The
  implementation is checked by the listed tests, and the citation awaits confirmation on the text.

Rows are added by each pull request that implements an equation. A formula of the new code that is missing from
this page is a defect.

## Equations

| Quantity | Implementation | Reference | Tests | Citation status |
|----------|----------------|-----------|-------|-----------------|
| Default state partials of a force (B1): central differences of the acceleration at the fixed epoch of the state, column `j` = `(a(x + h e_j) - a(x - h e_j)) / ((x_j + h) - (x_j - h))`, with `h_r = δ_r max(|r|, 1 m)`, `h_v = δ_v max(|v|, 1 m/s)`, `δ_r = δ_v = 3e-6`; the velocity columns are skipped for a force that does not depend on the velocity | `Propagator/Forces/ForceBase.cs`, `ForceBase.AccumulateStatePartials` (default of every force without analytic partials, including forces written outside the library) | Central difference, and division by the difference of the perturbed components as stored to remove the representation error of the step: W. H. Press, S. A. Teukolsky, W. T. Vetterling, B. P. Flannery, *Numerical Recipes*, 3rd edition, Cambridge University Press, 2007, section 5.7. Steps from the [step study](#finite-difference-steps) below. | `ForceBasePartialsTests` (force of known Jacobian, non-linear in `r` and linear in `v`, at the four reference states: below 1e-8), `FiniteDifferenceStepStudyTests` | Steps measured by the study. *Numerical Recipes*: to be verified by S. Guillet. |
| Default parameter partials: central difference on the drag coefficient `Cd` or the reflectivity coefficient `Cr` of the evaluation context, `h_p = 1e-3 max(|C|, 1)`; `Cr` is shared by SRP, albedo and thermal pressure, so `∂a/∂Cr` sums their three contributions | `Propagator/Forces/ForceBase.cs`, `ForceBase.AccumulateParameterPartials`; `ForceEvaluationContext` | The built-in forces are linear in their coefficient, so the central difference has no truncation error, and its rounding error is about `ε / δ_p`. | `ForceParameterPartialsTests` (`∂a/∂Cd = a / Cd` for drag, `∂a/∂Cr = a / Cr` for SRP, albedo and thermal, and their sum: below 1e-10), `ForceEvaluationContextTests` (the acceleration scales exactly with the mass and the coefficient) | Linearity checked bit for bit by the listed tests. |
| Point-mass partials of the central body (B2), `∂a/∂r = μ (3 r rᵀ / |r|⁵ - I / |r|³)`, with `μ` and `r` relative to the attracting body, as in the acceleration | `Body/PointMassPartials.cs`, `GravitationalField.TryAccumulatePositionPartials`, `CelestialItem.TryAccumulateGravitationalPositionPartials`, `GravitationalAcceleration` | O. Montenbruck, E. Gill, *Satellite Orbits: Models, Methods and Applications*, Springer, 2000, chapter 7 (variational equations). | `GravitationalAccelerationPartialsTests` against Ridders' reference: geocentric states 1e-13 to 2e-12; states relative to the Sun, with and without the ephemeris cache, 2e-10 to 1e-8 (limited by the resolution of heliocentric positions); tolerance 1e-6 | To be verified by S. Guillet (section and equation numbers). |
| Geopotential partials: none analytic until the Cunningham recursion (step 5a); the default central differences | `GeopotentialGravitationalField.TryAccumulatePositionPartials` returns false, and `GravitationalAcceleration` falls back to the default path | See the first row. | `GravitationalAccelerationPartialsTests.Geopotential_*`; step study, EGM2008 10×10: below 5e-11 at the default step | Not applicable. |
| Third-body partials (B4), `∂a/∂r = μ_j (3 ρ ρᵀ / |ρ|⁵ - I / |ρ|³)`, `ρ = r - d_j`; the indirect term does not depend on `r` | `Propagator/Forces/ThirdBodyPerturbation.cs`, `ThirdBodyPerturbation`; `d_j` comes from the same method as the acceleration (ephemeris cache, or SPICE in ICRF) | Montenbruck and Gill (2000), op. cit., chapter 7. | `ThirdBodyPerturbationPartialsTests`: four reference states, Moon and Sun, with and without the ephemeris cache, against Ridders' reference: 1e-13 to 2e-12; tolerance 1e-6 | To be verified by S. Guillet (section and equation numbers). |
| Reference derivative of the tests: Ridders' extrapolation of central differences over a decreasing sequence of steps (contraction 1.4, table of 10, stop when the error grows by a factor of 2), with its error estimate | Test project, `RiddersDerivative` | C. J. F. Ridders, "Accurate computation of F'(x) and F'(x)F''(x)", *Advances in Engineering Software* 4(2), 75-76, 1982; *Numerical Recipes*, op. cit., section 5.7, routine `dfridr`. | `RiddersDerivativeTests` (sine, exponential, inverse square: 1e-12 to 1e-13) | To be verified by S. Guillet. |

## Finite-Difference Steps

The relative steps of the default central differences come from a plateau study: the error of the default path
against a reference, for relative steps from 1e-12 to 1e-2 (half decades), for every built-in force at the four
reference states (LEO 400 km, LEO 800 km, GEO, perigee of a 500 × 40 000 km HEO; 2021-03-20 12:00 UTC, all in
sunlight). The reference is analytic for the point mass and the third bodies, and Ridders' extrapolation for the
others. The test `FiniteDifferenceStepStudyTests` reruns it and writes the curves when the environment variable
`IO_ASTRODYNAMICS_STUDY_OUTPUT` names a directory.

Two errors are measured: relative to the block of the force itself, and, for `∂a/∂r`, scaled by the point-mass
block of the central body, which is the error the state transition matrix sees once the blocks of all the forces are
summed. They differ for SRP: its position derivative is about `|a| / d_sun`, so a step scaled by the geocentric `|r|`
leaves a rounding error near 1e-6 relative, but near 1e-17 scaled. Analytic SRP partials come with step 5b.

![Error of the default central differences against the relative step](../assets/covariance/fd-step-study.png)

Data: [fd-step-study.csv](../assets/covariance/fd-step-study.csv) (Linux, .NET 10, 2026-10-07).

Worst case over the four states at the default step `δ = 3e-6`:

| Force | Reference | `∂a/∂r`, relative | `∂a/∂r`, scaled | `∂a/∂v`, relative |
|-------|-----------|-------------------|-----------------|-------------------|
| Point mass | Analytic (B2) | 4.6e-11 | 4.6e-11 | Not applicable |
| Third body, Moon | Analytic (B4) | 8.8e-11 | 4.3e-16 | Not applicable |
| Third body, Sun | Analytic (B4) | 1.2e-10 | 8.9e-16 | Not applicable |
| EGM2008 10×10 | Ridders | 4.4e-11 | 4.4e-11 | Not applicable |
| Drag, NRLMSISE-00 (not at GEO) | Ridders | 1.6e-8 | 2.7e-12 | 1.4e-11 |
| SRP (Earth and Moon occulting) | Ridders | 8.0e-7 | 9.1e-17 | Not applicable |
| Albedo | Ridders | 5.4e-11 | 5.6e-19 | Not applicable |
| Thermal | Ridders | 4.1e-11 | 4.7e-19 | Not applicable |

The worst scaled error of `∂a/∂r` stays below 1e-9 from `δ = 3e-7` to `1e-5` and is smallest at `3e-6`; the error
of `∂a/∂v` stays below 1e-9 from `1e-7` to `1e-4` and is smallest at `3e-6`, in line with the `ε^(1/3)` of a central
difference. The test asserts, at the default steps, a relative error below 1e-4 (the threshold of the specification
for the default path), a scaled error of `∂a/∂r` and a relative error of `∂a/∂v` below 1e-9, and a Ridders error
estimate below 1e-7.

## Modeling Assumptions

The assumptions of the feature are listed here with their reference and measured effect as the lots implementing
them are merged.

| Assumption | Lot | Reference | Measured effect |
|------------|-----|-----------|-----------------|
| Linear propagation of the covariance | A, C | Pending | Validity domain to be measured against Monte Carlo (F3) |
| Open-loop impulsive maneuvers: nominal delta-V, no derivative of the firing time | A | Pending | To be measured |
| Derivative of the shadow function neglected in SRP, albedo and thermal partials | B | Pending | Penumbra effect to be measured (F1) |
| Atmospheric density gradient by finite differences, constant space weather | B | Pending | To be measured (B7) |
| Cartesian covariance in the inertial frame of propagation; RTN by pure rotation | C | Pending | Not applicable |

## See Also

- [Force Models](force-models.md)
- [Integrators](integrators.md)
- [Validation](../guides/validation.md)
