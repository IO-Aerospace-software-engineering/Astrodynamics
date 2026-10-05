# Enumerations

Reference for enumeration types used across the framework.

## Aberration

Controls light-time and stellar-aberration correction in ephemeris queries.

| Value | Description |
|-------|-------------|
| `None` | No correction |
| `LT` | Light-time correction |
| `LTS` | Light-time plus stellar aberration |
| `CN` | Converged Newtonian light-time |
| `CNS` | Converged Newtonian light-time plus stellar aberration |
| `XLT` | Transmission light-time |
| `XCN` | Transmission converged Newtonian |

## RelationnalOperator

Used in geometry-finder constraint searches.

| Value | Description |
|-------|-------------|
| `Greater` | Greater than threshold |
| `Less` | Less than threshold |
| `Equal` | Equal to threshold |
| `AbsMin` | Absolute minimum |
| `AbsMax` | Absolute maximum |
| `LocalMin` | Local minimum |
| `LocalMax` | Local maximum |

## OccultationType

| Value | Description |
|-------|-------------|
| `Full` | Full occultation |
| `Partial` | Partial occultation |
| `Annular` | Annular occultation |
| `Any` | Any type of occultation |

## ShapeType

| Value | Description |
|-------|-------------|
| `Ellipsoid` | Ellipsoidal body shape |
| `Point` | Point body shape |

## OrbitalElementsType

| Value | Description |
|-------|-------------|
| `Osculating` | Osculating (instantaneous) elements |
| `Mean` | Mean elements (e.g., from TLE/OMM) |

!!! warning
    Mean elements cannot call `ToStateVector()` directly. Use `TLE.ToOsculating()` instead.

## EncounterQualityFlags

!!! note
    These flags are produced by the SSA workflow in `IO.Astrodynamics.SSA`.

| Value | Description |
|-------|-------------|
| `None` | Clean conjunction solution |
| `MissingProtectedCovariance` | Protected object has no covariance |
| `MissingSecondaryCovariance` | Secondary object has no covariance |
| `CovarianceUnavailable` | No covariance for either participant |
| `CovarianceRemediated` | Covariance was remediated |
| `StaleCovarianceUsed` | An initial-state covariance older than `ConjunctionAnalysisOptions.StaleCovarianceThreshold` (60 s by default) was used at TCA, held fixed in RTN |
| `LowRelativeVelocityEncounter` | 2D assumption may be degraded |
| `SingleCovarianceMaximumPcUsed` | Single-covariance maximum Pc was used |

## CDM Enumerations

!!! note
    These enumerations are in the `IO.Astrodynamics.CCSDS.CDM` namespace.

| Enumeration | Values |
|-------------|--------|
| `CdmObjectRole` | Object 1 / Object 2 roles |
| `CdmScreenVolumeFrame` | Screening volume reference frame |
| `CdmScreenVolumeShape` | Screening volume geometry |
| `CdmReferenceFrame` | CDM state vector reference frame |
| `CdmCovarianceMethod` | Covariance computation method |
| `CdmManeuverable` | Maneuverability status |

## CrossingDirection

Used in the [event detection](event-detection.md) system for g-function zero-crossing triggers.

| Value | Description |
|-------|-------------|
| `NegativeToPositive` | g-function crosses zero from negative to positive |
| `PositiveToNegative` | g-function crosses zero from positive to negative |
| `Any` | g-function crosses zero in either direction |

## OrbitalDirection

Used with [`OrbitalDirectionTarget`](attitudes.md#orbitaldirectiontarget) for TRIAD attitude targets.

| Value | Description |
|-------|-------------|
| `Prograde` | Velocity direction (+v) |
| `Retrograde` | Anti-velocity direction (-v) |
| `Nadir` | Toward central body center (-r) |
| `Zenith` | Away from central body center (+r) |
| `Normal` | Orbital angular momentum (h = r x v) |
| `AntiNormal` | Anti-orbital angular momentum |

## InstrumentShape

| Value | Description |
|-------|-------------|
| `Circular` | Circular field of view |
| `Elliptical` | Elliptical field of view |
| `Rectangular` | Rectangular field of view |

## IlluminationAngle

| Value | Description |
|-------|-------------|
| `Incidence` | Angle between surface normal and illumination direction |
| `Emission` | Angle between surface normal and observer direction |
| `Phase` | Angle between illumination and observer directions |

## Classification (TLE)

| Value | Description |
|-------|-------------|
| `Unclassified` | `'U'` — unclassified |
| `Classified` | `'C'` — classified |
| `Secret` | `'S'` — secret |

## See Also

- [Predefined Objects](predefined-objects.md)
- [Conjunction Assessment](conjunction-assessment.md)
- [CDM](cdm.md)
