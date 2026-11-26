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
// Inputs: r0 and v0 (double), earth (CelestialBody)
var cov = Matrix.Diagonal(
    100.0, 100.0, 100.0,   // sigma_x^2, sigma_y^2, sigma_z^2 (m^2)
    0.01, 0.01, 0.01);     // sigma_vx^2, sigma_vy^2, sigma_vz^2 (m^2/s^2)

var orbit = new StateVector(
    new Vector3(r0, 0, 0),
    new Vector3(0, v0, 0),
    earth, Time.J2000Tdb, Frame.ICRF, cov);
```

Use a full (non-diagonal) covariance when available from orbit determination.
A diagonal covariance ignores the correlations between axes, which changes the
shape of the sampled dispersion in either direction.

### MonteCarloConfiguration

The configuration object controls every aspect of the campaign:

```csharp
// Inputs: spacecraft (Spacecraft)
var config = new MonteCarloConfiguration
{
    RunCount = 100,
    Seed = 42,
    TemplateSpacecraft = spacecraft,
    Window = new Window(Time.J2000Tdb, Time.J2000Tdb.AddHours(2)),
    Propagation = new PropagationOptions
    {
        Integrator = new RK78IntegratorOptions
        {
            AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
        },
        OutputStep = TimeSpan.FromSeconds(60),
        ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
    }
};
```

| Property | Purpose |
|----------|---------|
| `RunCount` | Number of Monte Carlo samples |
| `Seed` | RNG seed for reproducibility (same seed = same results) |
| `TemplateSpacecraft` | Spacecraft whose initial state covariance is sampled; without a maneuver plan, which the runs do not execute (`ArgumentException`, decision 41 of #668), and the runs take its dry mass alone ([#795](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/795)) |
| `Window` | Propagation time window |
| `Propagation` | The settings of every run, a `PropagationOptions` value ([Propagation Options](../reference/propagators.md#propagation-options)); null for its defaults, RK7(8) at 1e-9 every 60 s about the central body alone. `OutputStep` is the step of the statistics |

### Thread safety

Each run builds its own integrator, forces and geopotential field from `Propagation` (11.0.0, R06 of #668), so the runs
share their bodies, the template's central body with a geopotential included. The `CelestialBodiesFactory`, `DeltaT`
and `IntegratorFactory` of 10.x, which gave each run its own bodies and integrator, were removed in 11.0.0 (R07 of
#668).

## Running the campaign

```csharp
// Inputs: config (MonteCarloConfiguration)
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
provides the mean position and velocity, the sample covariance, the standard
deviation, minimum and maximum of each component, and the percentiles of the RSS
position and velocity dispersion.

```csharp
// Inputs: result (MonteCarloResult)
// Statistics at the final epoch
var last = result.EpochStatistics[^1];

Console.WriteLine($"Epoch:           {last.Epoch}");
Console.WriteLine($"Sigma position:  {last.StandardDeviation.Position} m");  // per ICRF axis
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
// Inputs: result (MonteCarloResult)
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

## Monte Carlo and the propagated covariance

The covariance propagated with the state by the state transition matrix
([Covariance Propagation](../reference/covariance-propagation.md)) is validated against this propagator: on 5000
runs in LEO, the sample covariance is the propagated one along every direction of the state for about one revolution,
and the distribution is Gaussian. Beyond it the curvature of the orbit bends the along-track spread: the sample
variance leaves the propagated one along one direction of the state, while the position alone holds up to about 6 h.
In inertial axes every element still agrees within its sampling noise over a whole day, because the along-track
variance sets that noise. See [Validation](../guides/validation.md#state-transition-matrix-and-covariance).

## Summary

- `MonteCarloPropagator.Propagate` runs a dispersion campaign by sampling
  initial state covariance.
- `MonteCarloConfiguration.Propagation` holds the settings of every run; the runs share their bodies.
- `EpochStatistics` provides per-epoch mean, standard deviation, and RSS
  percentiles (P50, P95, P99).
- Set an explicit `Seed` for reproducible results.
- Start with 100 runs; increase to 500--1000 for production analysis.
