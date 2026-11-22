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
window and the settings of its propagation, a `PropagationOptions` value: the
integrator, the output step, the third bodies and the forces
([Propagation Options](../reference/propagators.md#propagation-options)).

```csharp
// Inputs: spacecraft1 and spacecraft2 (Spacecraft), window (Window)
var gravity = new PropagationOptions
{
    Integrator = new RK78IntegratorOptions
    {
        AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
    },
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
};
var tasks = new List<PropagationTask>
{
    new PropagationTask(spacecraft1, window, gravity),
    new PropagationTask(spacecraft2, window, gravity with { AtmosphericDrag = true, SolarRadiationPressure = true }),
};
```

Each task can have different force model settings. For example, a high-drag
LEO satellite might include atmospheric drag while a GEO satellite does not.

## Running the batch

```csharp
// Inputs: tasks (List<PropagationTask>)
var result = BatchPropagator.Propagate(tasks, new BatchOptions
{
    MaxDegreeOfParallelism = 4
});
```

`MaxDegreeOfParallelism` controls the maximum number of concurrent
propagations. Set this to the number of available CPU cores, or lower if
you want to leave headroom for other processes.

## Processing results

Each result entry indicates whether the propagation succeeded or failed; the elapsed time is that of the whole batch:

```csharp
// Inputs: result (BatchResult)
foreach (var r in result.Results)
{
    if (r.Succeeded)
    {
        Console.WriteLine($"{r.Spacecraft.Name}: " +
                          $"{r.Solution.StateVectors.Count} states");
    }
    else
    {
        Console.WriteLine($"{r.Spacecraft.Name}: FAILED - {r.Error.Message}");
    }
}

Console.WriteLine($"Batch: {result.ElapsedTime.TotalSeconds:F1} s, all succeeded: {result.AllSucceeded}");
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

Pass an `IProgress<BatchProgress>` to monitor completion:

```csharp
// Inputs: tasks (List<PropagationTask>)
var result = BatchPropagator.Propagate(tasks, new BatchOptions
{
    MaxDegreeOfParallelism = 4,
    Progress = new Progress<BatchProgress>(p =>
    {
        Console.WriteLine($"Progress: {p.Completed}/{p.Total}");
    })
});
```

It is reported each time a task completes (successfully or not); `Progress<T>` calls its handler on the thread pool,
or on the synchronization context it was created on.

### Cancellation

Use a `CancellationToken` to abort the batch early:

```csharp
// Inputs: tasks (List<PropagationTask>)
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

!!! note "Shared bodies"
    Since 11.0.0 each propagation builds its own geopotential field from the central body's model (R06 of #668), so the
    tasks may share one `CelestialBody` with geopotential harmonics, and one `PropagationOptions` value: each task builds
    its own integrator and forces from it. Before, each task needed its own `CelestialBody` and an integrator factory.

```csharp
// Inputs: spacecraftList (List<Spacecraft>), window (Window)
var options = new PropagationOptions
{
    Integrator = new RK78IntegratorOptions
    {
        AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
    },
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
};
var tasks = spacecraftList.Select(spacecraft => new PropagationTask(spacecraft, window, options)).ToList();
```

## Summary

- `BatchPropagator.Propagate` runs multiple spacecraft propagations in
  parallel with fault isolation.
- Each `PropagationTask` specifies its settings in a `PropagationOptions`
  value, which tasks may share.
- Use `MaxDegreeOfParallelism` to control concurrency.
- Progress callbacks and `CancellationToken` support operational monitoring.
- Tasks may share their bodies, a geopotential included: each propagation
  builds its own field.
