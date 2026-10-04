# Conjunction Assessment

!!! note
    `ConjunctionAssessment` and its companion types live in the `IO.Astrodynamics.SSA` namespace.

`ConjunctionAssessment` is the main SSA facade for screening, analyzing, and evaluating conjunction events between a protected spacecraft and candidate secondary objects.

!!! warning "Covariance is not propagated to TCA"
    The covariance used at the time of closest approach (TCA) is resolved in this order, for each
    participant:

    1. the covariance carried by the participant's state at TCA, when there is one (age zero). The
       `ILocalizable` and `PropagationSolution` entry points sample states without covariance, so in
       practice this applies to hand-built states only;
    2. otherwise the covariance of the participant's initial `StateVector`, **held fixed in the RTN
       frame**: rotated into RTN with the initial state, then back into the inertial frame with the
       state at TCA. It is not propagated. Its age, the time between its epoch and TCA, is reported in
       `EncounterState.ProtectedCovarianceAge` / `SecondaryCovarianceAge`, and
       `EncounterQualityFlags.StaleCovarianceUsed` is raised when it exceeds
       `ConjunctionAnalysisOptions.StaleCovarianceThreshold` (60 s by default);
    3. otherwise no covariance: `MissingProtectedCovariance` or `MissingSecondaryCovariance`, and
       `CovarianceUnavailable` when both are missing.

    Holding the covariance in RTN keeps its shape relative to the orbit (large along-track, small
    radial), but its size does not grow with time as a propagated covariance would. Keep the initial
    epoch close to the TCA, or supply covariances from a recent orbit determination.

## ConjunctionAssessment Methods

| Method | Description |
|--------|-------------|
| `Screen(profile, secondaries, window, options)` | Rank many candidates against one protected spacecraft |
| `Analyze(profile, secondary, window)` | Return the single best conjunction in a window |
| `AnalyzeAll(profile, secondary, window)` | Return every detected conjunction in a window |
| `EvaluateAvoidance(encounter, options)` | Generate impulsive avoidance options for an encounter |

Each method has two families of overloads:

- **`ILocalizable`** overloads for generic participants. States are sampled from each participant's ephemeris: SPICE data for kernel-backed objects, the stored states of a spacecraft that was already propagated, otherwise the initial orbit (Keplerian, or SGP4 for a TLE).
- **`PropagationSolution`** overloads when both trajectories were already propagated, reusing dense output.

## ProtectedSpacecraftProfile

Packages the protected `Spacecraft` with maneuver constraints used by avoidance searches.

| Property | Description |
|----------|-------------|
| `Spacecraft` | The protected spacecraft instance |
| `ManeuverConstraints` | Delta-V and lead-time limits for avoidance searches |

## ScreeningOptions

| Property | Description |
|----------|-------------|
| `MaxMissDistanceMeters` | Filter threshold for miss distance |
| `MaxResults` | Maximum number of ranked results to return |

Results are ordered by descending collision probability, then by miss distance.

## EncounterCase

Top-level conjunction result packaging all assessment products.

| Property | Type | Description |
|----------|------|-------------|
| `ProtectedAsset` | `ProtectedSpacecraftProfile` | The protected spacecraft profile |
| `SecondaryObject` | `ILocalizable` | The secondary object |
| `ScreeningWindow` | `Window` | The searched window |
| `EncounterState` | `EncounterState` | Geometry and covariance at TCA |
| `CollisionRisk` | `CollisionRisk` | Collision probability products |
| `ProtectedState` / `SecondaryState` | `StateVector` | Participant states at TCA |
| `ToCdm(CdmExportOptions)` | `Cdm` | Export to CCSDS CDM |

## EncounterState

| Property | Type | Description |
|----------|------|-------------|
| `Epoch` | `Time` | Time of closest approach |
| `MissDistanceMeters` | `double` | Miss distance (m) |
| `RelativeState` | `RelativeState` | Relative position and velocity |
| `CombinedCovarianceRtn` | `Matrix` | Combined 6x6 covariance in the protected object's RTN frame |
| `QualityFlags` | `EncounterQualityFlags` | Covariance and applicability warnings |
| `ProtectedCovarianceAge` / `SecondaryCovarianceAge` | `TimeSpan?` | Time between the epoch of the covariance used and TCA: zero when it was available at TCA, null when there is none |

## RelativeState

| Property | Description |
|----------|-------------|
| `RelativePositionInertial` | Relative position in ICRF (m) |
| `RelativeVelocityInertial` | Relative velocity in ICRF (m/s) |
| `RelativePositionRtn` | Relative position in protected object's RTN frame (m) |
| `RelativeVelocityRtn` | Relative velocity in RTN frame (m/s) |

## CollisionRisk

| Property | Description |
|----------|-------------|
| `CombinedHardBodyRadiusMeters` | Sum of hard-body radii (m) |
| `ProbabilityOfCollision` | Pc, null when no covariance is available |
| `ProjectedCovariance` | 2D encounter-plane covariance |
| `RadialSigmaMeters` / `InTrackSigmaMeters` / `CrossTrackSigmaMeters` | RTN sigmas (m) |

## ConjunctionAnalysisOptions

| Property | Default | Description |
|----------|---------|-------------|
| `SampleStep` | 1 min | Coarse search cadence |
| `MaximumEventSearchStep` | 60 s | Largest interval subdivided while refining the TCA |
| `SecondaryHardBodyRadiusMeters` | 5 m | Secondary hard-body radius when the object does not expose one |
| `BisectionToleranceSeconds` | 1e-9 s | TCA refinement tolerance |
| `LowRelativeSpeedThresholdMetersPerSecond` | 1 m/s | Below this relative speed, `LowRelativeVelocityEncounter` is raised |
| `StaleCovarianceThreshold` | 60 s | Covariance age beyond which `StaleCovarianceUsed` is raised |

## Practical Limits

- Collision probability uses a 2D Foster-style encounter-plane method.
- `EncounterQualityFlags.LowRelativeVelocityEncounter` is an applicability warning that the 2D assumption may be degraded.
- Covariance is not propagated to TCA; see the warning at the top of this page.
- With both covariances, Pc is the Foster 2D probability of the combined covariance.
- With only one covariance, Pc is the maximum probability over the unknown covariance (Frisbee's
  method), an upper bound, and `SingleCovarianceMaximumPcUsed` is raised.
- With no covariance, no Pc is computed (`ProbabilityOfCollision` is null) and
  `CovarianceUnavailable` is raised.

## Example

```csharp
var encounter = ConjunctionAssessment.Analyze(
    new ProtectedSpacecraftProfile(protectedSpacecraft),
    secondarySpacecraft,
    new Window(epoch, epoch.AddMinutes(10.0)));

Console.WriteLine($"Miss distance: {encounter.EncounterState.MissDistanceMeters:F1} m");
Console.WriteLine($"Pc: {encounter.CollisionRisk.ProbabilityOfCollision:E3}");
```

## See Also

- [Avoidance Studies](avoidance.md)
- [CDM](cdm.md)
- [Propagators](propagators.md)
