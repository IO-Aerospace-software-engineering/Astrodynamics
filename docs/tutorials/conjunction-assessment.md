# Conjunction Assessment

This tutorial walks through the full space situational awareness (SSA)
workflow: setting up spacecraft with covariance, analyzing conjunctions,
screening catalogs, evaluating avoidance maneuvers, and exporting
CCSDS-compliant Conjunction Data Messages.

## Setting up spacecraft with covariance

Conjunction assessment requires position and velocity covariance on both the
protected (primary) and secondary objects. Covariance is attached to the
initial `StateVector` as a 6x6 matrix.

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
time window. `ConjunctionAssessment.Analyze` propagates both objects and
returns the closest-approach encounter.

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

The `ProtectedSpacecraftProfile` wraps the protected spacecraft with any
additional screening configuration (e.g., custom covariance scaling).

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
| `MinCollisionProbability` | Lower bound on collision probability |
| `MaxResults` | Maximum number of encounters to return |

Results are ranked by collision probability (highest first).

## Reusing propagated trajectories

If you have already propagated the protected spacecraft (e.g., from a
previous analysis), pass the pre-computed trajectory to avoid redundant
propagation:

```csharp
// Propagate once
var protectedProfile = new ProtectedSpacecraftProfile(protectedSpacecraft);

// Reuse for multiple secondaries
var enc1 = ConjunctionAssessment.Analyze(protectedProfile, secondary1, window);
var enc2 = ConjunctionAssessment.Analyze(protectedProfile, secondary2, window);
```

The profile caches the propagated trajectory after the first call.

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
    Console.WriteLine($"Lead={opt.LeadTime.TotalHours:F0}h, " +
                      $"DV={opt.DeltaV:F3} m/s => " +
                      $"Miss={opt.ResultingMissDistance:F0} m, " +
                      $"Pc={opt.ResultingProbability:E3}");
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
        Console.WriteLine($"Validation error: {error}");
```

### CDM fields

The exported CDM includes:

- **Header**: message ID, creation date, originator
- **Relative metadata**: TCA, miss distance, reference frame
- **Object 1 / Object 2**: state vectors, covariance (RTN frame), physical
  properties (mass, area, drag/SRP coefficients)
- **Collision probability**: computed using the single-covariance maximum
  probability method

## Best practices and quality flags

### Covariance quality

The accuracy of collision probability depends heavily on covariance quality.
Watch for these indicators:

- **Covariance realism**: Diagonal-only covariance underestimates probability.
  Use full 6x6 matrices from orbit determination when available.
- **Covariance age**: Covariance propagated far from the last observation
  epoch becomes unreliable. Prefer recent OD solutions.
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
