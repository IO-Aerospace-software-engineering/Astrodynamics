# Conjunction Assessment

!!! note
    `ConjunctionAssessment` and its companion types live in the `IO.Astrodynamics.SSA` namespace.

`ConjunctionAssessment` is the main SSA facade for screening, analyzing, and evaluating conjunction events between a protected spacecraft and candidate secondary objects.

## ConjunctionAssessment Methods

| Method | Description |
|--------|-------------|
| `Screen(profile, secondaries, window, options)` | Rank many candidates against one protected spacecraft |
| `Analyze(profile, secondary, window)` | Return the single best conjunction in a window |
| `AnalyzeAll(profile, secondary, window)` | Return every detected conjunction in a window |
| `EvaluateAvoidance(encounter, options)` | Generate impulsive avoidance options for an encounter |

Each method has two families of overloads:

- **`ILocalizable`** overloads for generic participants.
- **`PropagationSolution`** overloads when both trajectories were already propagated, reusing dense output.

## ProtectedSpacecraftProfile

Packages the protected `Spacecraft` with maneuver constraints used by avoidance searches.

| Property | Description |
|----------|-------------|
| `Spacecraft` | The protected spacecraft instance |

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
| `EncounterState` | `EncounterState` | Geometry and covariance at TCA |
| `CollisionRisk` | `CollisionRisk` | Collision probability products |
| `ToCdm(CdmExportOptions)` | `Cdm` | Export to CCSDS CDM |

## EncounterState

| Property | Type | Description |
|----------|------|-------------|
| `Epoch` | `Time` | Time of closest approach |
| `MissDistance` | `double` | Miss distance (m) |
| `RelativeState` | `RelativeState` | Relative position and velocity |
| `CombinedRtnCovariance` | `Matrix` | Combined 6x6 RTN covariance |
| `QualityFlags` | `EncounterQualityFlags` | Covariance and applicability warnings |

## RelativeState

| Property | Description |
|----------|-------------|
| `RelativePositionInertial` | Relative position in ICRF (m) |
| `RelativeVelocityInertial` | Relative velocity in ICRF (m/s) |
| `RelativePositionRTN` | Relative position in protected object's RTN frame (m) |
| `RelativeVelocityRTN` | Relative velocity in RTN frame (m/s) |

## CollisionRisk

| Property | Description |
|----------|-------------|
| `CombinedHardBodyRadius` | Sum of hard-body radii (m) |
| `ProbabilityOfCollision` | Pc when covariance is available |
| `ProjectedCovariance` | 2D encounter-plane covariance |
| `RadialSigma` / `InTrackSigma` / `CrossTrackSigma` | RTN sigmas (m) |

## Practical Limits

- Collision probability uses a 2D Foster-style encounter-plane method.
- `EncounterQualityFlags.LowRelativeVelocityEncounter` is an applicability warning that the 2D assumption may be degraded.
- Covariance is required for both participants to compute Pc.

## Example

```csharp
var encounter = ConjunctionAssessment.Analyze(
    new ProtectedSpacecraftProfile(protectedSpacecraft),
    secondarySpacecraft,
    new Window(epoch, epoch.AddMinutes(10.0)));

Console.WriteLine($"Miss distance: {encounter.EncounterState.MissDistance:F1} m");
Console.WriteLine($"Pc: {encounter.CollisionRisk.ProbabilityOfCollision:E3}");
```

## See Also

- [Avoidance Studies](avoidance.md)
- [CDM](cdm.md)
- [Propagators](propagators.md)
