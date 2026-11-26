# Enumerations

Reference for enumeration types used across the framework.

## Aberration

Controls light-time and stellar-aberration correction in ephemeris queries.

| Value | Description |
|-------|-------------|
| `None` | No correction |
| `LT` | Light-time correction |
| `LtS` | Light-time plus stellar aberration |
| `CN` | Converged Newtonian light-time |
| `CnS` | Converged Newtonian light-time plus stellar aberration |
| `XLT` | Transmission light-time |
| `XCN` | Transmission converged Newtonian |

## TimeScale

The scale of a `Time` (`Time.Scale`), a required argument of its constructors and factories since 11.0.0 (R24 of #668);
see [Time & Windows](time-and-windows.md#time-scales).

| Value | Description |
|-------|-------------|
| `TDB` | Barycentric Dynamical Time, SPICE's ET; 0, the scale of `default(Time)` |
| `TT` | Terrestrial Time, TAI + 32.184 s |
| `TAI` | International Atomic Time |
| `UTC` | Coordinated Universal Time |
| `GPS` | GPS time, TAI − 19 s |

## RelationalOperator

Used in geometry-finder constraint searches.

| Value | Description |
|-------|-------------|
| `GreaterThan` | Greater than threshold (`>`) |
| `LessThan` | Lower than threshold (`<`) |
| `Equal` | Equal to threshold (`=`) |
| `NotEqual` | Not equal to threshold (`!`) |
| `GreaterThanOrEqual` | Greater than or equal to threshold (`>=`) |
| `LessThanOrEqual` | Lower than or equal to threshold (`<=`) |
| `AbsoluteMin` | Absolute minimum (`ABSMIN`) |
| `AbsoluteMax` | Absolute maximum (`ABSMAX`) |
| `LocalMin` | Local minimum (`LOCMIN`) |
| `LocalMax` | Local maximum (`LOCMAX`) |

`NotEqual`, `GreaterThanOrEqual` and `LessThanOrEqual` are accepted by every search: on the CSPICE path, which has no such
relation, `GreaterThanOrEqual` and `LessThanOrEqual` are searched as `GreaterThan` and `LessThan` (closed windows), and `NotEqual` is
the search window less the crossings of `Equal` (#482; [Sites](sites.md#window-searches)).

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
| `Mean` | Mean elements (e.g., from TLE/OMM): no state vector without their theory; `AtEpoch` and `ToKeplerianElements(Time)` keep the type, and a numerical propagation refuses them as initial orbit (#463) |

!!! warning
    Mean elements cannot call `ToStateVector()` directly. Use `Tle.ToOsculating()` instead.

## EncounterQualityFlags

!!! note
    These flags are produced by the SSA workflow in `IO.Astrodynamics.Ssa`.

| Value | Description |
|-------|-------------|
| `None` | Clean conjunction solution |
| `MissingProtectedCovariance` | Protected object has no covariance |
| `MissingSecondaryCovariance` | Secondary object has no covariance |
| `CovarianceUnavailable` | No covariance for either participant |
| `CovarianceRemediated` | Covariance was remediated: the encounter-plane covariance before the probability, or the initial covariance of a participant, indefinite from rounding, by eigenvalue clipping before its propagation; an initial covariance whose defect is deeper than rounding explains (the smallest eigenvalue of its correlation matrix below −1e-3 of its trace in the frame of the state and in its RTN frame, [#443](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/443)) is not remediated but held in RTN, unflagged (see [Covariance Propagation](covariance-propagation.md#an-initial-covariance-indefinite-from-rounding)) |
| `StaleCovarianceUsed` | An initial-state covariance older than `ConjunctionAnalysisOptions.StaleCovarianceThreshold` (60 s by default) was used at TCA, held fixed in RTN |
| `LowRelativeVelocityEncounter` | 2D assumption may be degraded |
| `SingleCovarianceMaximumPcUsed` | Single-covariance maximum Pc was used |
| `NonlinearCovariancePropagation` | A propagated covariance has a non-linearity index above `ConjunctionAnalysisOptions.NonlinearityIndexThreshold`: beyond the domain where Foster was validated against a Monte Carlo, not a wrong Pc; confirm a Pc that matters by a Monte Carlo |
| `TcaAtWindowEnd` | The window holds no close approach (no range-rate zero from negative to positive): the TCA of `Analyze` and `Screen` is the closest window end, CSPICE's `ABSMIN`, where the objects are still closing or already receding; widen the window to find the closest approach. `AnalyzeAll` lists no such encounter; a close approach found on a window end (its range-rate zero in the half-tick next to it) is not flagged ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621), new in 11.0.0, `1 << 8`) |

## TrajectorySource

How the trajectory of a participant of an encounter is computed (`EncounterState.ProtectedTrajectory`,
`SecondaryTrajectory`), and that of an avoidance option after its burn (`AvoidanceOption.Trajectory`, `Propagated` or
`TwoBody`; [Avoidance Studies](avoidance.md)); in the `IO.Astrodynamics.Ssa` namespace (R17 of #668: `AvoidanceTrajectory`
before 11.0.0, of the avoidance options only).

| Value | Description |
|-------|-------------|
| `Propagated` | Propagated numerically: a `PropagationSolution` given to the analysis or the states a numerical propagation stored; for an option, from the state after the burn with the force model of the protected participant's propagation (RK7(8) or Velocity-Verlet) |
| `TwoBody` | The two-body orbit of the initial orbital parameters of a spacecraft without stored states; for an option, of the state after the burn, the participant having no numerical propagation (not propagated, TLE, SPICE kernel) |
| `Sgp4` | SGP4 from the TLE of a spacecraft that no numerical propagation stored states for |
| `Kernel` | Read through the data provider: a kernel-backed spacecraft, or an object that is not a spacecraft |

## CloseApproachSearch

The search that found the epoch of an encounter (`EncounterCase.Search`, [Conjunction
Assessment](conjunction-assessment.md#conjunctionassessment-methods)); in the `IO.Astrodynamics.Ssa` namespace (R18 of
#668).

| Value | Description |
|-------|-------------|
| `Sampled` | The range rate sampled over the window and bisected: the `ILocalizable` overloads, the screening, the avoidance options, two trajectories that do not share their observer and frame |
| `Polynomial` | The minima of the cubic Hermite of the stored steps of two propagations that share their observer and frame, refined on the range rate of the states |
| `None` | No search: the encounter given at its TCA by the states of its objects (`Cdm.ToEncounter`, decision 110 of #668) |

## AvoidanceSkipReason

Why a lead time of the avoidance grid gave no option (`SkippedLeadTime.Reason`, `AvoidanceStudy.Skipped`,
[Avoidance Studies](avoidance.md)); in the `IO.Astrodynamics.Ssa` namespace.

| Value | Description |
|-------|-------------|
| `BeforeWindow` | The burn falls before the start of the window of the encounter |
| `OutsideTrajectory` | The burn falls outside the propagation of the protected spacecraft the options are propagated on |

## CovarianceSource

Where the covariance of one participant at TCA comes from, in the order the conjunction analysis resolves it
([Conjunction Assessment](conjunction-assessment.md#covariance-at-tca)); in the `IO.Astrodynamics.Ssa` namespace.

| Value | Description |
|-------|-------------|
| `None` | No covariance |
| `ProvidedAtEncounter` | The state at TCA carried its covariance (age zero) |
| `InitialStateHeldInRtn` | The initial covariance held fixed in RTN, not propagated (the fallback) |
| `Propagated` | The initial covariance propagated to TCA by the variational equations of RK7(8) |

## CDM Enumerations

!!! note
    These enumerations are in the `IO.Astrodynamics.Ccsds` namespace.

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

## CovariancePropagationMode

What a propagation integrates besides the state ([Covariance Propagation](covariance-propagation.md)).

| Value | Description |
|-------|-------------|
| `StateTransitionOnly` | The state transition matrix Φ and the sensitivity Ψ to the force parameters (default) |
| `Covariance` | Φ, Ψ and the covariance P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q |

## ForceParameter

A force model parameter: a column of the sensitivity Ψ, or a consider parameter of the extended covariance.

| Value | Description |
|-------|-------------|
| `DragCoefficient` | Drag coefficient Cd, with atmospheric drag |
| `ReflectivityCoefficient` | Reflectivity coefficient Cr, with solar radiation, albedo or thermal radiation pressure |

## GeopotentialCentralTerm

The GM of the central term of a geopotential (`GeopotentialModelParameters.CentralTerm`); the harmonics always take
the constants of the model. See [Force Models](force-models.md#the-central-term-with-the-gm-of-the-model).

| Value | Description |
|-------|-------------|
| `BodyGravitationalParameter` | The GM of the body, from its kernels (the default) |
| `ModelGravitationalParameter` | The GM of the model: the whole field takes the constants of the model, as GMAT applies a `.cof` header |

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

## BodyAxis

An axis of the spacecraft body frame (`IO.Astrodynamics.Maneuver`, decision 69 of #668): the axis a single-vector
attitude points (`bodyAxis`, `Front` by default) and the axes of the `BodyAxis` constructor of
[`TriadAttitude`](attitudes.md#constructors), resolved on the instance axes of the spacecraft whose plan last held the
maneuver, the static axes before any plan ([Body Axes](attitudes.md#body-axes)). A value outside the enumeration throws
`ArgumentOutOfRangeException`.

| Value | Description |
|-------|-------------|
| `Front` | `Spacecraft.BodyFront` (+Y by default) |
| `Back` | `Spacecraft.BodyBack` (−Y by default) |
| `Right` | `Spacecraft.BodyRight` (+X by default) |
| `Left` | `Spacecraft.BodyLeft` (−X by default) |
| `Up` | `Spacecraft.BodyUp` (+Z by default) |
| `Down` | `Spacecraft.BodyDown` (−Z by default) |

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
| `Emission` | Angle between surface normal and observer direction; needs an observer: `Site.FindIlluminationWindows`, which takes none, throws for it (#682) |
| `Phase` | Angle between illumination and observer directions; needs an observer, as `Emission` |

## TleClassification

| Value | Description |
|-------|-------------|
| `Unclassified` | `'U'` — unclassified |
| `Classified` | `'C'` — classified |
| `Secret` | `'S'` — secret |

## See Also

- [Predefined Objects](predefined-objects.md)
- [Conjunction Assessment](conjunction-assessment.md)
- [CDM](cdm.md)
