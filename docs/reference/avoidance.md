# Avoidance Studies

!!! note
    Avoidance studies live in the `IO.Astrodynamics.SSA` namespace.

`ConjunctionAssessment.EvaluateAvoidance(...)` generates and ranks simple impulsive avoidance options for a conjunction encounter.

## EvaluateAvoidance

| Parameter | Type | Description |
|-----------|------|-------------|
| `encounter` | `EncounterCase` | The conjunction to mitigate |
| `options` | `AvoidanceSearchOptions` | Search grid parameters |

The method applies candidate burns in the protected spacecraft's RTN frame at each combination of lead time and delta-V magnitude, then reanalyzes the encounter after each candidate. The burn epoch must lie in the screening window of the encounter.

### Trajectory and covariance of each option

When the covariance of the protected participant was propagated to the encounter (`CovarianceSource.Propagated`, see
[Conjunction Assessment](conjunction-assessment.md#covariance-at-tca)):

- each option is **propagated numerically in RK7(8)** from the state after its burn to the end of the screening window
  (or of the participant's propagation), with the forces, the integrator settings and the orientation cache of the
  trajectory it leaves, its mass after the burn (the fuel of the estimate below) and its coefficients;
- its initial covariance is the covariance at the burn plus the execution error of the burn, P(t_b) + B L_e Bᵀ,
  B = [0 ; I], L_e the covariance of the model of Gates for the ΔV of the option (`AvoidanceSearchOptions.ExecutionError`,
  `ManeuverExecutionError`; none by default);
- with consider parameters, it starts from the extended covariance at the burn, so that the correlation of the state
  and of Cd and Cr carries over: P = Φ P0 Φᵀ + Φ C0 Ψcᵀ + Ψc C0ᵀ Φᵀ + Ψc Pp Ψcᵀ + Q, C0 = cov(δx(t_b), δp);
- the secondary participant keeps one resolution of its covariance for all the options.

Each option is a ΔV fixed in epoch and in direction: the open-loop model of the propagated covariance. Against the
propagation of the same spacecraft from the start with that burn fixed and its execution error, the covariance of an
option at its TCA agrees to 1.9e-9 and its state to 0.6 mm (LEO with drag, SRP, Moon and Sun, Cd and Cr as consider
parameters and process noise, `AvoidanceCovarianceTests`); the execution error adds Φ B L_e Bᵀ Φᵀ at the TCA to 1.3e-11.
A burn retargeted in operations (a miss distance aimed at rather than a fixed ΔV) does not have this covariance after it:
against a retargeted burn, Φ after the burn was off by 0.23 to 0.48 in the validation of the state transition matrix
([#397](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/397)).

Otherwise, as before 10.2.0, the trial spacecraft after the burn is not propagated: its trajectory is the two-body orbit
of the state after the burn, and its covariance is that of the encounter resolution (held in RTN), the execution error
not used.

!!! note "Change of results in 10.2.0"
    With a propagated covariance, the miss distance and the Pc of every option change: the trajectory after the burn
    follows the full force model instead of the two-body orbit, and the protected participant has a covariance at the
    TCA of the option, where it had none before (the single-covariance maximum Pc then applied). Cost: one RK7(8)
    propagation with the covariance per option, from its burn to the end of the window (54 options with the default
    grid).

## AvoidanceSearchOptions

| Property | Type | Description |
|----------|------|-------------|
| `LeadTimes` | `TimeSpan[]` | Burn lead times before TCA |
| `DeltaVMagnitudesMetersPerSecond` | `double[]` | Candidate delta-V magnitudes (m/s) |
| `ExecutionError` | `ManeuverExecutionError` | Execution error of the burns (model of Gates), added to the covariance at the burn when the covariance is propagated; null by default |

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
