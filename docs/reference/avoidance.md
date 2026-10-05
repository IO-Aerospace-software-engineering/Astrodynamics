# Avoidance Studies

!!! note
    Avoidance studies live in the `IO.Astrodynamics.SSA` namespace.

`ConjunctionAssessment.EvaluateAvoidance(...)` generates and ranks simple impulsive avoidance options for a conjunction encounter.

## EvaluateAvoidance

| Parameter | Type | Description |
|-----------|------|-------------|
| `encounter` | `EncounterCase` | The conjunction to mitigate |
| `options` | `AvoidanceSearchOptions` | Search grid parameters |

The method applies candidate burns in the protected spacecraft's RTN frame at each combination of lead time and delta-V magnitude, then reanalyzes the encounter after each candidate.

## AvoidanceSearchOptions

| Property | Type | Description |
|----------|------|-------------|
| `LeadTimes` | `TimeSpan[]` | Burn lead times before TCA |
| `DeltaVMagnitudesMetersPerSecond` | `double[]` | Candidate delta-V magnitudes (m/s) |

## AvoidanceOption

Each result describes one candidate impulsive burn.

| Property | Type | Description |
|----------|------|-------------|
| `BurnEpoch` | `Time` | Epoch of the avoidance burn |
| `DeltaVInertial` | `Vector3` | Delta-V in inertial frame (m/s) |
| `FuelCostEstimateKilograms` | `double` | Estimated fuel consumption (kg) |
| `PostManeuverEncounter` | `EncounterCase` | Reanalyzed encounter after the burn |
| `RankingScore` | `double` | Internal heuristic for ordering candidates |

The ranking score is useful for sorting candidates but should not replace mission-specific operational rules.

## Example

```csharp
var options = ConjunctionAssessment.EvaluateAvoidance(
    encounter,
    new AvoidanceSearchOptions
    {
        LeadTimes = new[]
        {
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(3),
            TimeSpan.FromHours(6)
        },
        DeltaVMagnitudesMetersPerSecond = new[] { 0.05, 0.10, 0.25 }
    });

foreach (var opt in options.OrderBy(o => o.RankingScore))
{
    Console.WriteLine($"Burn at {opt.BurnEpoch}: dV={opt.DeltaVInertial.Magnitude():F3} m/s, "
        + $"post-miss={opt.PostManeuverEncounter.EncounterState.MissDistanceMeters:F0} m");
}
```

## See Also

- [Conjunction Assessment](conjunction-assessment.md)
- [CDM](cdm.md)
- [Impulse Maneuvers](impulse-maneuvers.md)
