# Monte Carlo Propagation

This tutorial covers running Monte Carlo dispersion campaigns to
characterize orbit uncertainty: configuring the campaign, ensuring thread
safety, running the propagation, and interpreting statistical results.

## Overview

The `MonteCarloPropagator` samples the initial state covariance, propagates
each realization independently, and aggregates per-epoch statistics across
all runs. This gives you dispersion ellipses, percentile bounds, and
confidence intervals for position and velocity at every output epoch.

## Configuration

### Initial state with covariance

Attach a 6x6 covariance matrix to the initial `StateVector`. The Monte Carlo
sampler draws initial conditions from the multivariate normal distribution
defined by this covariance.

```csharp
var cov = new Matrix(6, 6);
cov.Set(0, 0, 100.0);   // sigma_x^2  (m^2)
cov.Set(1, 1, 100.0);   // sigma_y^2
cov.Set(2, 2, 100.0);   // sigma_z^2
cov.Set(3, 3, 0.01);    // sigma_vx^2 (m^2/s^2)
cov.Set(4, 4, 0.01);    // sigma_vy^2
cov.Set(5, 5, 0.01);    // sigma_vz^2

var orbit = new StateVector(
    new Vector3(r0, 0, 0),
    new Vector3(0, v0, 0),
    earth, Time.J2000TDB, Frame.ICRF, cov);
```

Use a full (non-diagonal) covariance when available from orbit determination.
Diagonal-only covariance underestimates cross-axis correlations.

### MonteCarloConfiguration

The configuration object controls every aspect of the campaign:

```csharp
var config = new MonteCarloConfiguration
{
    RunCount = 100,
    Seed = 42,
    TemplateSpacecraft = spacecraft,
    Window = new Window(Time.J2000TDB, Time.J2000TDB.AddHours(2)),
    CelestialBodiesFactory = () => [
        new CelestialBody(PlanetsAndMoons.EARTH),
        Stars.SUN_BODY,
        PlanetsAndMoons.MOON_BODY
    ],
    DeltaT = TimeSpan.FromSeconds(60),
    IntegratorFactory = () => new RK78Integrator(1e-10, 1e-10, 30.0),
};
```

| Property | Purpose |
|----------|---------|
| `RunCount` | Number of Monte Carlo samples |
| `Seed` | RNG seed for reproducibility (same seed = same results) |
| `TemplateSpacecraft` | Spacecraft whose initial state covariance is sampled |
| `Window` | Propagation time window |
| `CelestialBodiesFactory` | Factory that creates a fresh set of celestial bodies per thread |
| `DeltaT` | Output step size for statistics |
| `IntegratorFactory` | Factory that creates a fresh integrator per thread |

### Thread safety with CelestialBodiesFactory

The `GeopotentialGravitationalField` force model is **not** thread-safe ---
each propagation thread needs its own `CelestialBody` instance. The
`CelestialBodiesFactory` delegate is called once per thread to create
isolated instances:

```csharp
CelestialBodiesFactory = () =>
[
    new CelestialBody(PlanetsAndMoons.EARTH),   // fresh Earth per thread
    Stars.SUN_BODY,
    PlanetsAndMoons.MOON_BODY
]
```

If you omit the geopotential model (degree 0), you can share instances,
but using the factory pattern is always safe and recommended.

## Running the campaign

```csharp
var result = MonteCarloPropagator.Propagate(config);

Console.WriteLine($"Completed {result.SuccessCount} / {config.RunCount} runs");
Console.WriteLine($"Failed:   {result.FailureCount}");
```

The propagator runs samples in parallel, using all available CPU cores.
Failed runs (e.g., due to divergent trajectories) are counted but do not
stop the campaign.

## Interpreting results

### Per-epoch statistics

`result.EpochStatistics` contains one entry per output epoch. Each entry
provides mean, standard deviation, and percentiles for position and velocity
dispersion.

```csharp
// Statistics at the final epoch
var last = result.EpochStatistics[^1];

Console.WriteLine($"Epoch:           {last.Epoch}");
Console.WriteLine($"Mean RSS pos:    {last.MeanRssPosition:F1} m");
Console.WriteLine($"Std RSS pos:     {last.StdRssPosition:F1} m");
Console.WriteLine($"RSS pos P50:     {last.RssPositionPercentiles.P50:F1} m");
Console.WriteLine($"RSS pos P95:     {last.RssPositionPercentiles.P95:F1} m");
Console.WriteLine($"RSS pos P99:     {last.RssPositionPercentiles.P99:F1} m");
```

### RSS percentiles

The RSS (root-sum-square) position error combines all three axes into a single
scalar. Percentiles tell you the dispersion bound that contains a given
fraction of samples:

| Percentile | Meaning |
|------------|---------|
| P50 | Median --- half of samples are below this value |
| P95 | 95th percentile --- 95% of samples are within this bound |
| P99 | 99th percentile --- conservative bound for safety analysis |

### Dispersion growth over time

Plot RSS percentiles versus epoch to visualize how uncertainty grows:

```csharp
foreach (var stat in result.EpochStatistics)
{
    Console.WriteLine($"{stat.Epoch}, " +
                      $"{stat.RssPositionPercentiles.P50:F1}, " +
                      $"{stat.RssPositionPercentiles.P95:F1}, " +
                      $"{stat.RssPositionPercentiles.P99:F1}");
}
```

This data can be exported to CSV for plotting in external tools.

## Best practices

**Run count**: Start with 100 runs for quick checks. Use 500--1000 for
production analysis. Beyond 1000, percentile estimates stabilize and
additional runs add diminishing value.

**Seed selection**: Always set an explicit seed for reproducibility. Change the
seed and re-run to verify that results are not sensitive to a particular
random draw.

**Covariance realism**: The Monte Carlo results are only as good as the input
covariance. Use calibrated covariance from orbit determination rather than
assumed diagonal values.

**Force model consistency**: Use the same force model configuration
(geopotential degree, third bodies, drag, SRP) as your operational
propagator. Mismatched models invalidate the dispersion analysis.

**Output step size**: Choose `DeltaT` based on your analysis needs. Smaller
steps give smoother statistics curves but increase memory usage. For LEO,
60 seconds is typical; for GEO, 300 seconds is sufficient.

**Failure handling**: A small number of failed runs (< 5%) is normal for
highly dispersed initial conditions. Investigate if the failure rate exceeds
this --- it may indicate unrealistic covariance or force model instability.

## Summary

- `MonteCarloPropagator.Propagate` runs a dispersion campaign by sampling
  initial state covariance.
- Use `CelestialBodiesFactory` and `IntegratorFactory` to ensure thread safety.
- `EpochStatistics` provides per-epoch mean, standard deviation, and RSS
  percentiles (P50, P95, P99).
- Set an explicit `Seed` for reproducible results.
- Start with 100 runs; increase to 500--1000 for production analysis.
