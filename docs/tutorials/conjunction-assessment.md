# Conjunction Assessment

This tutorial walks through the full space situational awareness (SSA)
workflow: setting up spacecraft with covariance, analyzing conjunctions,
screening catalogs, evaluating avoidance maneuvers, and exporting
CCSDS-compliant Conjunction Data Messages.

## Setting up spacecraft with covariance

Collision probability needs a position and velocity covariance on the
protected (primary) and secondary objects. Covariance is attached to the
initial `StateVector` as a 6x6 matrix.

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

```csharp
// Build a 6x6 covariance matrix (diagonal for simplicity)
var protectedCov = new Matrix(6, 6);
protectedCov.Set(0, 0, 25.0);   // sigma_x^2  (m^2)
protectedCov.Set(1, 1, 25.0);   // sigma_y^2
protectedCov.Set(2, 2, 25.0);   // sigma_z^2
protectedCov.Set(3, 3, 0.01);   // sigma_vx^2 (m^2/s^2)
protectedCov.Set(4, 4, 0.01);   // sigma_vy^2
protectedCov.Set(5, 5, 0.01);   // sigma_vz^2

var protectedState = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7656.2204182967143, 0.0),
    earth, epoch, Frame.ICRF, protectedCov);

var protectedSpacecraft = new Spacecraft(
    -1001, "Protected", 120.0, 150.0,
    new Clock("Protected_CLK", 256), protectedState,
    hardBodyRadius: 8.0);
```

The `hardBodyRadius` parameter sets the physical keep-out sphere around the
object and is used in miss-distance calculations and collision probability.

Set up the secondary object the same way:

```csharp
var secondaryCov = new Matrix(6, 6);
secondaryCov.Set(0, 0, 50.0);
secondaryCov.Set(1, 1, 50.0);
secondaryCov.Set(2, 2, 50.0);
secondaryCov.Set(3, 3, 0.02);
secondaryCov.Set(4, 4, 0.02);
secondaryCov.Set(5, 5, 0.02);

var secondaryState = new StateVector(
    new Vector3(6800500.0, 100.0, 50.0),
    new Vector3(-50.0, 7656.0, 10.0),
    earth, epoch, Frame.ICRF, secondaryCov);

var secondarySpacecraft = new Spacecraft(
    -2001, "Secondary", 80.0, 100.0,
    new Clock("Secondary_CLK", 256), secondaryState,
    hardBodyRadius: 3.0);
```

## Basic conjunction analysis

The simplest analysis evaluates a single protected-vs-secondary pair over a
time window. `ConjunctionAssessment.Analyze` samples both objects'
ephemerides and returns the closest-approach encounter. For a spacecraft that
has not been propagated, its ephemeris is the initial orbit extrapolated on a
Keplerian orbit (SGP4 for a TLE); propagate it first, or use the
`PropagationSolution` overloads, for a high-fidelity trajectory.

```csharp
var encounter = ConjunctionAssessment.Analyze(
    new ProtectedSpacecraftProfile(protectedSpacecraft),
    secondarySpacecraft,
    new Window(epoch, epoch.AddMinutes(10.0)));

Console.WriteLine($"TCA:            {encounter.EncounterState.Epoch}");
Console.WriteLine($"Miss distance:  {encounter.EncounterState.MissDistanceMeters:F1} m");
Console.WriteLine($"Probability:    {encounter.CollisionRisk.ProbabilityOfCollision:E3}");
Console.WriteLine($"Relative speed: {encounter.EncounterState.RelativeState.RelativeVelocityInertial.Magnitude():F1} m/s");
```

The `ProtectedSpacecraftProfile` wraps the protected spacecraft with the
maneuver constraints used by avoidance studies (`ManeuverConstraints`).

## Analyzing all encounters

When objects have multiple close approaches in the window, use `AnalyzeAll`
to return every encounter rather than just the closest:

```csharp
var allEncounters = ConjunctionAssessment.AnalyzeAll(
    new ProtectedSpacecraftProfile(protectedSpacecraft),
    secondarySpacecraft,
    new Window(epoch, epoch.AddHours(24.0)));

foreach (var enc in allEncounters)
{
    Console.WriteLine($"TCA: {enc.EncounterState.Epoch}, Miss: {enc.EncounterState.MissDistanceMeters:F1} m, " +
                      $"Pc: {enc.CollisionRisk.ProbabilityOfCollision:E3}");
}
```

## Ranked screening

For operational SSA, you typically screen one protected asset against many
secondary objects. `Screen` evaluates all pairs, filters by distance or
probability thresholds, and returns a ranked list.

```csharp
var encounters = ConjunctionAssessment.Screen(
    new ProtectedSpacecraftProfile(protectedSpacecraft),
    new ILocalizable[] { secondary1, secondary2, secondary3 },
    window,
    new ScreeningOptions
    {
        MaxMissDistanceMeters = 10000.0,
        MaxResults = 20
    });

foreach (var enc in encounters)
{
    Console.WriteLine($"{enc.SecondaryObject.Name}: " +
                      $"Miss={enc.EncounterState.MissDistanceMeters:F1} m, " +
                      $"Pc={enc.CollisionRisk.ProbabilityOfCollision:E3}");
}
```

`ScreeningOptions` supports filtering by:

| Property | Description |
|----------|-------------|
| `MaxMissDistanceMeters` | Upper bound on miss distance |
| `MaxResults` | Maximum number of encounters to return |

Results are ranked by collision probability (highest first), then by miss
distance.

## Reusing propagated trajectories

If you have already propagated both trajectories, pass the
`PropagationSolution` objects to reuse their dense output instead of
sampling the participants' ephemerides:

```csharp
var protectedTrajectory = protectedSpacecraft.Propagate(window, perturbingBodies, false, false, step);
var secondaryTrajectory = secondarySpacecraft.Propagate(window, perturbingBodies, false, false, step);

var encounter = ConjunctionAssessment.Analyze(
    new ProtectedSpacecraftProfile(protectedSpacecraft), protectedTrajectory,
    secondarySpacecraft, secondaryTrajectory,
    window);
```

The profile itself caches nothing: each call samples the trajectories it is
given.

## Avoidance trade study

Once a conjunction is identified, `EvaluateAvoidance` generates a matrix of
avoidance options by varying lead time and delta-v magnitude. Each option
shows the resulting miss distance and collision probability after the
maneuver.

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

foreach (var opt in options)
{
    var after = opt.PostManeuverEncounter;
    Console.WriteLine($"Burn at {opt.BurnEpoch}, " +
                      $"DV={opt.DeltaVInertial.Magnitude():F3} m/s => " +
                      $"Miss={after.EncounterState.MissDistanceMeters:F0} m, " +
                      $"Pc={after.CollisionRisk.ProbabilityOfCollision:E3}");
}
```

The trade study helps operators select the minimum delta-v that achieves an
acceptable miss distance within operational lead-time constraints.

## CDM export and validation

Export any encounter as a CCSDS Conjunction Data Message (CDM) for sharing
with conjunction assessment providers or archival.

### Exporting a CDM

```csharp
var cdm = encounter.ToCdm(new CdmExportOptions
{
    Originator = "MyOpsCenter",
    MessageId = "CDM-001"
});

// Write to XML file
cdm.WriteToFile("encounter.cdm.xml");
```

### Validating a CDM

Validate any CDM file against the CCSDS schema:

```csharp
var validation = Cdm.ValidateSchema("encounter.cdm.xml");

if (validation.IsValid)
    Console.WriteLine("CDM is schema-valid.");
else
    foreach (var error in validation.Errors)
        Console.WriteLine($"Validation error: {error.Message}");
```

### CDM fields

The exported CDM includes:

- **Header**: message ID, creation date, originator
- **Relative metadata**: TCA, miss distance, reference frame
- **Object 1 / Object 2**: state vectors, covariance (RTN frame), physical
  properties (mass, area, drag/SRP coefficients)
- **Collision probability**: the Pc of the encounter, with
  `COLLISION_PROBABILITY_METHOD` set to `FOSTER-2D`. Export requires a
  covariance for both participants, so the single-covariance maximum Pc is
  never exported; `CdmExportOptions.CollisionProbabilityMethod` overrides the
  method name
- **Covariance age**: when a participant's covariance comes from its initial
  state, a comment gives its age at TCA. An encounter flagged
  `StaleCovarianceUsed` is refused unless `CdmExportOptions.AllowStaleCovariance`
  is set

## Best practices and quality flags

### Covariance quality

The accuracy of collision probability depends heavily on covariance quality.
Watch for these indicators:

- **Covariance realism**: A diagonal covariance ignores the correlations
  between axes. Depending on the geometry, that can raise or lower the
  probability, so neither direction is a safe bound. Use full 6x6 matrices
  from orbit determination when available.
- **Covariance age**: The library does not propagate covariance: it holds the
  initial covariance fixed in RTN up to TCA, so its size does not grow with
  time. Check `EncounterState.ProtectedCovarianceAge` and
  `SecondaryCovarianceAge`, and prefer covariances from a recent orbit
  determination.
- **Hard-body radius**: Setting this too large inflates probability
  artificially. Use realistic physical dimensions.

### Screening thresholds

- Start with a generous miss-distance threshold (e.g., 10 km) and tighten
  based on operational experience.
- For LEO, relative speeds are typically 7--15 km/s; for GEO, under 1 km/s.
  Adjust screening windows accordingly.

### Avoidance decisions

- Prefer maneuvers with the longest feasible lead time --- they require less
  delta-v for the same miss-distance improvement.
- Always re-screen after a planned avoidance maneuver to verify the new
  trajectory does not introduce secondary conjunctions.

## Recommended workflow

A typical operational SSA workflow proceeds as follows:

1. **Ingest** updated orbital data (state vectors + covariance) for your
   protected asset and the catalog of tracked objects.

2. **Screen** the protected asset against the catalog:
    ```csharp
    var encounters = ConjunctionAssessment.Screen(
        profile, catalog, window, screeningOptions);
    ```

3. **Triage** the ranked results. Focus on encounters exceeding your
   probability threshold (e.g., Pc > 1e-5).

4. **Analyze** high-priority encounters in detail:
    ```csharp
    var details = ConjunctionAssessment.Analyze(
        profile, highPrioritySecondary, refinedWindow);
    ```

5. **Evaluate avoidance** if the risk is unacceptable:
    ```csharp
    var trades = ConjunctionAssessment.EvaluateAvoidance(
        details, avoidanceOptions);
    ```

6. **Export CDM** for coordination with partner agencies:
    ```csharp
    var cdm = details.ToCdm(exportOptions);
    cdm.WriteToFile("cdm_report.xml");
    ```

7. **Execute** the chosen avoidance maneuver and return to step 2 to verify
   the post-maneuver trajectory is clear.

## Summary

- Use `Analyze` for single-pair assessment and `Screen` for catalog-wide
  screening.
- `AnalyzeAll` returns every encounter in the window, not just the closest.
- `EvaluateAvoidance` produces a lead-time/delta-v trade matrix.
- Export encounters as CCSDS CDMs with `ToCdm` and validate with
  `Cdm.ValidateSchema`.
- Covariance quality is the single most important factor in collision
  probability accuracy.
