# Covariance Propagation Provenance

This page traces every equation implemented for the state transition matrix and covariance work (phase 2,
feature 1), including the frame-change velocities that the covariance work relies on. For each one it gives the quantity, where it is implemented, the published reference it comes from,
the tests that check it, and the status of the citation:

- **Verified on the text**: the cited passage was read and matches the implementation.
- **To be verified by S. Guillet**: the reference could not be read from the development environment. The
  implementation is checked by the listed tests, and the citation awaits confirmation on the text.

Rows are added by each pull request that implements an equation. A formula of the new code that is missing from
this page is a defect.

## Equations

| Quantity | Implementation | Reference | Tests | Citation status |
|----------|----------------|-----------|-------|-----------------|
| Angular velocity of a frame transform: ICRF relative to the frame, in the frame axes, so that `R(t + dt) = R(t) exp(-[ω×] dt)` | Native `TransformFrameProxy` (SPICE `sxform_c`, then `xf2rav_c`), composed by `Frames/Frame.cs`, `Frame.ToFrame` | NAIF, `xf2rav_c` documentation: angular velocity of the second frame relative to the first, expressed in the first. | `FrameChainIntegrationTests.SpiceFrameRotationFollowsItsAngularVelocity` (ITRF93, IAU_MOON, MOON_ME, DSS-13_TOPO; below 2e-13 rad over 1 s) | Convention measured on the SPICE frames by the listed test. NAIF text: to be verified by S. Guillet. |
| Velocity of a frame change, `r' = R r`, `v' = R v - (R ω) × r'` | `OrbitalParameters/OrbitalParameters.cs`, `OrbitalParameters.ToFrame` | Transport theorem, with `ω` of `Frame.ToFrame` brought into the target frame by `R`. Same map as the SPICE state transformation matrix `[[R, 0], [dR/dt, R]]` (NAIF, `sxform_c` documentation), with `dR/dt = -[(R ω)×] R`. | `StateVectorFrameVelocityTests` (geocentric Moon against SPICE in ITRF93, both directions, 2000 and 2021; DSS-13 against SPICE; 1e-12), `FrameTests.StateInSiteFrameMatchesSpiceTopocentricFrame` | Checked against SPICE states (a few 1e-16). NAIF text: to be verified by S. Guillet. |
| Angular velocity of TIRS, `(0, 0, -Ω)` in TIRS axes, `Ω = 2π × 1.00273781191135448 / 86400` rad/s | `Frames/TirsFrame.cs`, `TirsFrame.EarthRotationRate` | IAU SOFA, `iauPvtob`, release 2023-10-11: constant `OM` and velocity `Ω ẑ × r` of an Earth-fixed point in the CIRS. The precession-nutation rate of the CIP is left out, as in `iauPvtob`. | `TirsFrameTests.TirsAngularVelocityDirectionIsNearZAxis`, `TirsFrameTests.TirsAngularVelocityMatchesFreshRecomputationOverOneSecond`, `FrameChainIntegrationTests.TirsAngularVelocityMatchesItrf93` (5e-11 rad/s), `FrameChainIntegrationTests.PointFixedInTirsMovesAtTheDerivativeOfItsIcrfPosition` (1e-6) | Verified on the text (`external-lib/sofa-src/pvtob.c`). |
| Angular velocity of CIRS, `-2 q⁻¹ dq/dt` for `q` the CIRS to ICRF rotation, by central difference over ±100 s (rounding and truncation both near 1e-7 of the rate) | `Frames/CirsFrame.cs`, `CirsFrame.ComputeAngularVelocity` | Quaternion kinematics in the Hamilton convention, `dq/dt = ½ q ⊗ ω_B` for the angular velocity `ω_B` of the rotating frame in its own axes: J. Solà, *Quaternion kinematics for the error-state Kalman filter*, arXiv:1711.02508, 2017. The sign gives the angular velocity of ICRF relative to CIRS, the convention of the first row. | `CirsFrameTests.CirsAngularVelocityMatchesFreshRecomputationOverOneSecond` (1e-12 rad), `FrameChainIntegrationTests.PointFixedInCirsMovesAtTheDerivativeOfItsIcrfPosition` (1e-5) | Checked by the listed tests. Solà (2017): to be verified by S. Guillet (section and equation numbers). |
| Angular velocity of a site frame, the body-fixed one rotated into topocentric axes | `Surface/SiteFrame.cs`, `SiteFrame.GetStateOrientationToICRF` | A frame fixed in the body frame shares its angular velocity; topocentric axes north, west, zenith, as the SPICE topocentric frames (`earth_topo_201023.tf`). | `FrameTests.SiteFrameMatchesSpiceTopocentricFrame` (1e-16 rad/s against DSS-13_TOPO), `FrameTests.SiteFrameDoesNotRotateRelativeToItrf93` | Checked against the SPICE frame DSS-13_TOPO. |
| Jacobian of a frame change of a state, `J = [[R, 0], [-[(R ω)×] R, R]]` | `Math/Matrix.cs`, `Matrix.CreateStateTransformationJacobian`; used by `OrbitalParameters.ToFrame` | Exact derivative of the velocity row above, a map linear in `(r, v)` at a fixed epoch. Same block structure as the SPICE state transformation matrix (NAIF, `sxform_c` documentation) and as the ECI to ECEF covariance transformation of D. A. Vallado, *Covariance Transformations for Satellite Flight Dynamics Operations*, AAS 03-526, 2003. | `StateVectorFrameCovarianceTests.ToFrame_RotatingFrame_JacobianMatchesFiniteDifferences`, `MatrixTests.CreateStateTransformationJacobian_*` | Derivation checked by central differences (1e-9). SPICE `sxform_c`, Vallado (2003): to be verified by S. Guillet. |
| Covariance through a linear map, `P' = J P Jᵀ`, then `(P' + P'ᵀ) / 2` | `Math/Matrix.cs`, `Matrix.TransformCovarianceWithJacobian`, `Matrix.Symmetrize` | Covariance of a linear transformation of a random vector, `Cov(J x) = J Cov(x) Jᵀ`: B. D. Tapley, B. E. Schutz, G. H. Born, *Statistical Orbit Determination*, Elsevier, 2004, chapter 4; Vallado (2003), op. cit. The symmetrization follows the rule of the feature specification (C2): every published covariance is symmetrized. | `StateVectorFrameCovarianceTests.ToFrame_RotatingFrame_CovarianceIsTransformedWithTheFiniteDifferenceJacobian`, `..._RoundTripThroughRotatingFrame_RestoresStateAndCovariance`, `..._CovarianceIsSymmetricPositiveSemiDefinite`, `MatrixTests.TransformCovarianceWithJacobian_*`, `MatrixTests.Symmetrize_*` | To be verified by S. Guillet (section and equation numbers). |
| Covariance in RTN, pure rotation `diag(R, R)` (unchanged, now documented) | `OrbitalParameters/StateVector.cs`, `StateVector.RotateCovarianceToRtn`, `StateVector.RotateCovarianceFromRtn` | Convention of the RTN covariance at TCA in the CCSDS Conjunction Data Message (CCSDS 508.0-B-1), used by the conjunction analysis and the CDM export. The rotation rate of the RTN frame is not applied. | `StateVectorTests.RotateCovarianceToRtn_DiagonalCovariance`, `ConjunctionAssessmentTests` | To be verified by S. Guillet. |

## Modeling Assumptions

The assumptions of the feature are listed here with their reference and measured effect as the lots implementing
them are merged.

| Assumption | Lot | Reference | Measured effect |
|------------|-----|-----------|-----------------|
| Linear propagation of the covariance | A, C | Pending | Validity domain to be measured against Monte Carlo (F3) |
| Open-loop impulsive maneuvers: nominal delta-V, no derivative of the firing time | A | Pending | To be measured |
| Derivative of the shadow function neglected in SRP, albedo and thermal partials | B | Pending | Penumbra effect to be measured (F1) |
| Atmospheric density gradient by finite differences, constant space weather | B | Pending | To be measured (B7) |
| Cartesian covariance in the inertial frame of propagation; RTN by pure rotation | C | See the RTN row above | Not applicable |

## See Also

- [StateVector](state-vector.md)
- [Frames & Orientation](frames.md#states-and-covariance)
- [Validation](../guides/validation.md)
