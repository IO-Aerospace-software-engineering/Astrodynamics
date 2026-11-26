# Avoidance Studies

!!! note
    Avoidance studies live in the `IO.Astrodynamics.SSA` namespace.

`ConjunctionAssessment.EvaluateAvoidance(...)` generates and ranks simple impulsive avoidance options for a conjunction encounter, and returns them in an `AvoidanceStudy` with the lead times that gave none.

## EvaluateAvoidance

| Parameter | Type | Description |
|-----------|------|-------------|
| `encounter` | `EncounterCase` | The conjunction to mitigate |
| `options` | `AvoidanceSearchOptions` | Search grid parameters |

The method applies candidate burns in the protected spacecraft's RTN frame at each combination of lead time and delta-V magnitude, then reanalyzes the encounter after each candidate. The grid is the constraint: every lead time and magnitude is evaluated (R17 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), decided by S. Guillet, 2026-11-19; before 11.0.0 the `ManeuverConstraints` of the protected profile, 10 min to 24 h and 5 m/s by default, dropped the others, a magnitude of 0 included). The burn epoch must lie in the screening window of the encounter: a lead time whose burn falls before it gives no option and is listed in `AvoidanceStudy.Skipped` with the reason `AvoidanceSkipReason.BeforeWindow` (dropped without a word before 11.0.0), and so is one whose burn the propagation the options are propagated on does not hold (`OutsideTrajectory`, which the library's own analyses do not reach: their propagations cover the window).

The method returns an `AvoidanceStudy` (before 11.0.0 the list of options; `EvaluateAvoidance(...).Options` is that list):

| Property | Type | Description |
|----------|------|-------------|
| `Options` | `IReadOnlyList<AvoidanceOption>` | The options, by ranking score, lower first, at most `MaxReturnedOptions` |
| `Skipped` | `IReadOnlyList<SkippedLeadTime>` | The lead times that gave no option, in the order of the grid, each with its `LeadTime` and `Reason` (`AvoidanceSkipReason`); its `ToString` the lead time and the reason |

### Trajectory and covariance of each option

When the covariance of the protected participant was propagated to the encounter (`CovarianceSource.Propagated`, see
[Conjunction Assessment](conjunction-assessment.md#covariance-at-tca)):

- each option is **propagated numerically in RK7(8)** from the state after its burn to the end of the screening window
  (or of the participant's propagation), with the forces, the integrator settings and the orientation cache of the
  trajectory it leaves, its mass after the burn (the mass of the trajectory at the burn less the fuel of the estimate
  below) and its coefficients, for the state as for its covariance;
- its initial covariance is the covariance at the burn plus the execution error of the burn, P(t_b) + B L_e Bᵀ,
  B = [0 ; I], L_e the covariance of the model of Gates for the ΔV of the option (`AvoidanceSearchOptions.ExecutionError`,
  `ManeuverExecutionError`; none by default);
- with consider parameters, it starts from the extended covariance at the burn, so that the correlation of the state
  and of Cd and Cr carries over: P = Φ P0 Φᵀ + Φ C0 Ψcᵀ + Ψc C0ᵀ Φᵀ + Ψc Pp Ψcᵀ + Q, C0 = cov(δx(t_b), δp);
- the secondary participant keeps one resolution of its covariance for all the options; without a propagated
  covariance, its variational equations are replayed along the steps of its propagation with the options of the
  protected solution, without `IncludeInStepControl`, which the replay cannot apply (its steps are those of the
  propagation; [#424](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/424)).

Each option is a ΔV fixed in epoch and in direction: the open-loop model of the propagated covariance. Against the
propagation of the same spacecraft from the start with that burn fixed and its execution error, both at 1e-13, the
state of an option at its TCA is that of the propagation bit for bit and its covariance agrees to 5.2e-15 (LEO with
drag, SRP, Moon and Sun, Cd and Cr as consider parameters and process noise, `AvoidanceCovarianceTests`), also when the
protected spacecraft spends fuel after the burns of the options: the mass of an option is that of the trajectory at its
burn, not that of the spacecraft at the end of its propagation
([#415](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/415)). The execution error adds
Φ B L_e Bᵀ Φᵀ at the TCA to 1.3e-11.
A burn retargeted in operations (a miss distance aimed at rather than a fixed ΔV) does not have this covariance after it:
against a retargeted burn, Φ after the burn was off by 0.23 to 0.48 in the validation of the state transition matrix
([#397](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/397)).

Otherwise (the covariance of the protected participant held in RTN, given at TCA, or none: a participant propagated
by Velocity-Verlet or without an initial covariance, `ConjunctionAnalysisOptions.CovariancePropagation = CovariancePropagationOptions.None`, a
participant not propagated), each option is propagated from the state after its burn with the **force model of the
protected participant's own propagation**, when there is one
([#402](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/402)):

| Protected participant | Trajectory of each option (`AvoidanceOption.Trajectory`) |
|---|---|
| Propagated in RK7(8), with or without an initial covariance, the state transition matrix or the covariance not used by the analysis | `Propagated`: RK7(8) with the forces, the integrator settings and the orientation cache of that propagation, without the variational equations |
| Propagated by Velocity-Verlet | `Propagated`: Velocity-Verlet at the step of that propagation, with its forces |
| Not propagated (state vector, Keplerian elements) | `TwoBody`: the two-body orbit of the state after the burn, the participant's own model |
| A TLE (SGP4) or a SPICE kernel, not propagated | `TwoBody`: SGP4 cannot take the state after a burn and the force model of a kernel is unknown, so the option leaves out the perturbations of that model; no force model is assumed |

The propagation of the participant is the `PropagationSolution` passed to the analysis or, for a `Spacecraft` passed as
an `ILocalizable`, its last propagation: a spacecraft keeps the force model of its last propagation by RK7(8) or
Velocity-Verlet (its forces with their ephemeris cache, the integrator settings, the orientation cache, the mass and
coefficients of each segment, without the steps; about 0.34 MB for one day in LEO with EGM2008 10×10, NRLMSISE-00, the
Moon and the Sun, measured in lot L4 of #668, `DefaultIntegratorFiguresTests`, alone in the CI of #789 at aefd6c2b on
Linux x64, Windows x64 and macOS arm64) until it is propagated again. The burn state is the state of that trajectory, after the maneuvers
before the burn; the mass of the option is the mass of the trajectory at the burn less its fuel. A maneuver of the
participant after the burn is not part of the option: each option is one burn on the trajectory, the open-loop model.
The options end at the end of the screening window or of the propagation, whichever comes first; a burn outside the
propagation is skipped.

The covariance of the protected participant at the TCA of an option is the one the analysis resolves for it at the burn
(given there by its ephemeris, or its initial covariance held in RTN), **held in RTN** from the burn to the TCA of the
option: with the initial covariance, the initial covariance held in RTN as for the encounter (`InitialStateHeldInRtn`),
its age counted from the initial state, so that `StaleCovarianceUsed` is raised as for the encounter. The execution
error is not used: a velocity covariance at the burn held in RTN never reaches the position covariance at the TCA. The
secondary participant keeps the resolution of its covariance of the encounter (propagated with the same options when it
was) for all the options.

Against the same spacecraft propagated from the start with the burn fixed and analyzed with the covariance held in RTN,
each option has the same TCA, miss distance and Pc (`AvoidanceFallbackTests`; LEO 400 km, EGM2008 10×10, NRLMSISE-00,
Moon, Sun, SRP, in RK7(8) and by Velocity-Verlet).

!!! note "Change of results in 10.2.0-preview-1"
    With a propagated covariance, the miss distance and the Pc of every option change: the trajectory after the burn
    follows the full force model instead of the two-body orbit, and the protected participant has a covariance at the
    TCA of the option, where it had none before (the single-covariance maximum Pc then applied). Cost: one RK7(8)
    propagation with the covariance per option, from its burn to the end of the window (54 options with the default
    grid).

    Without a propagated covariance, the options of a participant propagated in RK7(8) or by Velocity-Verlet follow its
    force model instead of the two-body orbit, and the protected participant of every option carries its initial
    covariance held in RTN instead of none. On a LEO at 400 km with drag and geopotential (TCA 6 h after the start,
    encounter at 151 m, Pc 1.9e-4), the two-body option was 4.0 km (1 h lead) to 58.5 km (3 h lead) off at its TCA,
    with miss distances of 1.9 to 23 km and Pc of 4e-6 to 5e-5, where the propagated burn passes at 89 m to 2.9 km with
    Pc of 5e-33 to 3.4e-4: an option ranked safe (23.2 km, Pc 4.1e-6) passed at 89 m, Pc 3.0e-4. Cost: one propagation
    per option from its burn; the 36 options of the default grid on R2 over 8 h in 2.2 s in RK7(8) at 1e-11 and 5.5 s
    by Velocity-Verlet at 1 s (`ConjunctionCovarianceCostTests`, Release, informative), against about 0.4 s for the
    two-body trials.

## AvoidanceSearchOptions

| Property | Type | Description |
|----------|------|-------------|
| `LeadTimes` | `IReadOnlyList<TimeSpan>` | Burn lead times before TCA (1 h, 3 h and 6 h); empty or a lead time not positive throws `ArgumentOutOfRangeException` at init |
| `DeltaVMagnitudesMetersPerSecond` | `IReadOnlyList<double>` | Candidate delta-V magnitudes (m/s; 0.05, 0.10 and 0.25); empty or a magnitude not positive and finite throws `ArgumentOutOfRangeException` at init |
| `ExecutionError` | `ManeuverExecutionError` | Execution error of the burns (model of Gates), added to the covariance at the burn when the covariance is propagated, not used otherwise; null by default |
| `Analysis` | `ConjunctionAnalysisOptions` | The settings of the re-analysis of each option; null (default) for those the encounter was analyzed with; a record given replaces them whole, its `SecondaryHardBodyRadiusMeters` by the rule of [Conjunction Assessment](conjunction-assessment.md#conjunctionassessment-methods) (a value that differs from the radius a secondary spacecraft gives throws `ArgumentException` naming `options`); the options are analyzed by the sampled search, so a record given with `ReadRangeRateOverWindow = true` throws `ArgumentException` naming `options` (give `encounter.AnalysisOptions with { ReadRangeRateOverWindow = false }`), only a null `Analysis` having it cleared; `ComputeNonlinearityIndex` is cleared, given or inherited |
| `MaxReturnedOptions` | `int` | Number of options returned after ranking (10) |
| `PcThreshold` | `double` | Pc below which an option ranks first (1e-4) |

`AvoidanceSearchOptions.Default` is `new AvoidanceSearchOptions()`. Each option is re-analyzed with `Analysis`, or, when it is null, with the settings the encounter was analyzed with, on
both paths (the covariance propagated or not)
([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683); one record, structuring choice 2
of #668, decided by S. Guillet, 2026-11-19: before 11.0.0 the avoidance had an `AnalysisSampleStep`, a
`MaxEventSearchStep`, a `SecondaryHardBodyRadiusMeters` and a `LowRelativeSpeedThresholdMetersPerSecond` of its own,
each null for the encounter's). To change one value, start from the settings of the encounter
(`AvoidanceSearchOptions.Default with { Analysis = settings with { SampleStep = ... } }`, `settings` the options given to
`Analyze`). Before 11.0.0 every other setting went back to its default: an encounter analyzed with a stale
threshold of a day, a secondary radius of 0 m and no low-speed threshold had a combined radius of 8 m and no flag, and
each of its options 13 m and `StaleCovarianceUsed`. The reads of the range rate over the window and the non-linearity
index stay off for the options, and the covariance is propagated as the [paths above](#trajectory-and-covariance-of-each-option)
state. An encounter of `Screen` carries the settings of the screening, a sample step of 5 min by default: without
stored steps (the two-body orbit) the search step of the options is then 18.75 s instead of 3.75 s (none of the 90
options of a measured two-body pair moved, none of the 10 of a propagated pair; two minima within one search step would
be one).

## AvoidanceOption

Each result describes one candidate impulsive burn.

| Property | Type | Description |
|----------|------|-------------|
| `BurnEpoch` | `Time` | Epoch of the avoidance burn |
| `DeltaVInertial` | `Vector3` | Delta-V in inertial frame (m/s) |
| `FuelCostEstimateKilograms` | `double` | Estimated fuel consumption (kg) |
| `PostManeuverEncounter` | `EncounterCase` | Reanalyzed encounter after the burn |
| `RankingScore` | `double` | Internal heuristic for ordering candidates |
| `Trajectory` | `TrajectorySource` | `Propagated` with the force model of the protected participant, or `TwoBody` without one (`AvoidanceTrajectory` before 11.0.0) |

The ranking score is useful for sorting candidates but should not replace mission-specific operational rules.

## Example

```csharp
// Inputs: encounter (EncounterCase)
var study = ConjunctionAssessment.EvaluateAvoidance(
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

foreach (var opt in study.Options)
{
    Console.WriteLine($"Burn at {opt.BurnEpoch}: dV={opt.DeltaVInertial.Magnitude():F3} m/s, "
        + $"post-miss={opt.PostManeuverEncounter.EncounterState.MissDistanceMeters:F0} m");
}

foreach (var skipped in study.Skipped)
{
    Console.WriteLine(skipped);   // for instance "06:00:00: before the window"
}
```

## See Also

- [Conjunction Assessment](conjunction-assessment.md)
- [CDM](cdm.md)
- [Impulse Maneuvers](impulse-maneuvers.md)
