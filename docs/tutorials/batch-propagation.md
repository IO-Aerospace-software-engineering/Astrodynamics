# Batch Propagation

This tutorial covers propagating multiple spacecraft in parallel using
`BatchPropagator`: task setup, fault isolation, progress reporting,
cancellation, and thread safety requirements.

## Overview

`BatchPropagator` propagates a list of spacecraft simultaneously, distributing
work across CPU cores. Each spacecraft runs in its own thread with independent
fault isolation --- if one propagation fails, the others continue.

This is useful for catalog maintenance, constellation analysis, and any
workflow that processes many objects over the same time window.

## Setting up propagation tasks

Each spacecraft is wrapped in a `PropagationTask` that specifies the time
window, celestial environment, force model options, output step size, and
integrator factory.

```csharp
var tasks = new List<PropagationTask>
{
    new PropagationTask(
        spacecraft1, window,
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        IncludeAtmosphericDrag: false,
        IncludeSolarRadiationPressure: false,
        DeltaT: TimeSpan.FromSeconds(60),
        IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)),
    new PropagationTask(
        spacecraft2, window,
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        IncludeAtmosphericDrag: true,
        IncludeSolarRadiationPressure: true,
        DeltaT: TimeSpan.FromSeconds(60),
        IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)),
};
```

Each task can have different force model settings. For example, a high-drag
LEO satellite might include atmospheric drag while a GEO satellite does not.

## Running the batch

```csharp
var result = BatchPropagator.Propagate(tasks, new BatchOptions
{
    MaxDegreeOfParallelism = 4
});
```

`MaxDegreeOfParallelism` controls the maximum number of concurrent
propagations. Set this to the number of available CPU cores, or lower if
you want to leave headroom for other processes.

## Processing results

Each result entry indicates whether the propagation succeeded or failed:

```csharp
foreach (var r in result.Results)
{
    if (r.Succeeded)
    {
        Console.WriteLine($"{r.Spacecraft.Name}: " +
                          $"{r.Solution.StateVectors.Count} states, " +
                          $"duration {r.ElapsedTime.TotalSeconds:F1}s");
    }
    else
    {
        Console.WriteLine($"{r.Spacecraft.Name}: FAILED - {r.Error.Message}");
    }
}
```

### Fault isolation

A failed propagation does not affect other tasks. Common failure causes
include:

- Divergent trajectory (e.g., reentry or escape)
- Invalid initial conditions
- Force model configuration errors

Check `r.Error` for the exception details and `r.Spacecraft` to identify
which object failed.

## Progress reporting and cancellation

### Progress reporting

Pass a progress callback to monitor completion:

```csharp
var result = BatchPropagator.Propagate(tasks, new BatchOptions
{
    MaxDegreeOfParallelism = 4,
    Progress = (completed, total) =>
    {
        Console.WriteLine($"Progress: {completed}/{total}");
    }
});
```

The callback fires each time a task completes (successfully or not).

### Cancellation

Use a `CancellationToken` to abort the batch early:

```csharp
var cts = new CancellationTokenSource();

// Cancel after 30 seconds
cts.CancelAfter(TimeSpan.FromSeconds(30));

var result = BatchPropagator.Propagate(tasks, new BatchOptions
{
    MaxDegreeOfParallelism = 4,
    CancellationToken = cts.Token
});
```

Cancellation stops new tasks from starting. Tasks already in progress
run to completion.

## Thread safety requirements

!!! warning "One CelestialBody per task for geopotential"
    The `GeopotentialGravitationalField` force model is **not** thread-safe.
    Each `PropagationTask` must use its own `CelestialBody` instance when
    geopotential harmonics are enabled. Sharing a single `CelestialBody`
    across tasks causes data races and incorrect results.

Create separate instances for each task:

```csharp
var tasks = Enumerable.Range(0, spacecraftList.Count).Select(i =>
{
    // Fresh CelestialBody per task
    var taskEarth = new CelestialBody(PlanetsAndMoons.EARTH);

    return new PropagationTask(
        spacecraftList[i], window,
        new CelestialItem[] { taskEarth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        IncludeAtmosphericDrag: false,
        IncludeSolarRadiationPressure: false,
        DeltaT: TimeSpan.FromSeconds(60),
        IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0));
}).ToList();
```

The `IntegratorFactory` delegate already creates a fresh integrator per task,
so no additional care is needed there.

## Summary

- `BatchPropagator.Propagate` runs multiple spacecraft propagations in
  parallel with fault isolation.
- Each `PropagationTask` specifies its own force model, output step, and
  integrator factory.
- Use `MaxDegreeOfParallelism` to control concurrency.
- Progress callbacks and `CancellationToken` support operational monitoring.
- Create a separate `CelestialBody` instance per task when using geopotential
  gravity to avoid thread-safety issues.
