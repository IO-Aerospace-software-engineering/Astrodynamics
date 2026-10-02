// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Propagates multiple spacecraft in parallel with fault isolation, cancellation, and progress reporting.
/// Each task runs an independent <see cref="Body.Spacecraft.Spacecraft.Propagate"/> call.
/// </summary>
/// <remarks>
/// <h3>Thread Safety</h3>
/// <para>
/// <b>SPICE API:</b> All SPICE calls are serialized by a global lock — safe but may limit scaling
/// when many tasks need ephemeris data concurrently.
/// </para>
/// <para>
/// <b>Integrators:</b> Integrator instances must not be shared across tasks. Use
/// <see cref="PropagationTask.IntegratorFactory"/> to create a fresh instance per task.
/// </para>
/// <para>
/// <b>CelestialBody without geopotential:</b> Safe to share across tasks. Two-body gravity is
/// stateless, and the <c>Frame.GetStateOrientationToICRF</c> fallback uses a
/// <c>ConcurrentDictionary</c> cache that is inherently thread-safe.
/// </para>
/// <para>
/// <b>CelestialBody with <c>GeopotentialModelParameters</c>:</b> <b>NOT</b> safe to share.
/// Each <see cref="IO.Astrodynamics.Body.CelestialBody"/> constructed with geopotential parameters
/// owns a <see cref="IO.Astrodynamics.Body.GeopotentialGravitationalField"/> that uses pre-allocated
/// mutable buffers (<c>_P</c>, <c>_dP</c> Legendre tables and <c>_cosMLambda</c>, <c>_sinMLambda</c>
/// trig arrays). These buffers are overwritten on every acceleration evaluation, so concurrent
/// access from multiple threads causes data races.
/// </para>
/// <para>
/// <b>Frame orientation cache:</b> During propagation, the propagator sets an
/// <c>OrientationCache</c> on the central body's <c>Frame</c> and clears it on completion.
/// This cache is a plain property (not synchronized), so sharing the same <c>Frame</c> instance
/// across concurrent propagations can cause one task to read stale or partially written cache data
/// from another. When each task uses its own <c>CelestialBody</c> (and therefore its own <c>Frame</c>),
/// this is not an issue.
/// </para>
/// <para>
/// <b>Recommendation:</b> When using geopotential gravity, create a separate
/// <see cref="IO.Astrodynamics.Body.CelestialBody"/> instance per task for the central body.
/// This ensures each task has its own <c>GeopotentialGravitationalField</c> buffers and its own
/// <c>Frame</c> orientation cache. Third-body perturbation bodies (Sun, Moon, etc.) that do not
/// use geopotential can safely be shared.
/// </para>
/// </remarks>
public static class BatchPropagator
{
    /// <summary>
    /// Propagate all tasks in parallel (blocking).
    /// </summary>
    /// <param name="tasks">Propagation tasks to execute. Must not contain duplicate Spacecraft instances.</param>
    /// <param name="options">Parallelism, cancellation, and progress options. Null uses defaults.</param>
    /// <returns>Aggregated batch result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tasks"/> contains duplicate Spacecraft instances.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was triggered.</exception>
    public static BatchResult Propagate(IReadOnlyList<PropagationTask> tasks, BatchOptions options = null)
    {
        if (tasks == null) throw new ArgumentNullException(nameof(tasks));

        if (tasks.Count == 0)
            return new BatchResult(Array.Empty<PropagationResult>(), TimeSpan.Zero);

        ValidateNoDuplicateSpacecraft(tasks);

        options ??= new BatchOptions();

        var results = new PropagationResult[tasks.Count];
        int completed = 0;
        var stopwatch = Stopwatch.StartNew();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken = options.CancellationToken
        };

        Parallel.ForEach(Enumerable.Range(0, tasks.Count), parallelOptions, index =>
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            var task = tasks[index];
            try
            {
                PropagationSolution solution;
                if (task.IntegratorFactory != null)
                {
                    var integrator = task.IntegratorFactory();
                    solution = task.Spacecraft.Propagate(
                        task.Window, task.CelestialBodies, integrator,
                        task.IncludeAtmosphericDrag, task.IncludeSolarRadiationPressure,
                        task.DeltaT);
                }
                else
                {
                    solution = task.Spacecraft.Propagate(
                        task.Window, task.CelestialBodies,
                        task.IncludeAtmosphericDrag, task.IncludeSolarRadiationPressure,
                        task.DeltaT);
                }

                results[index] = new PropagationResult(task.Spacecraft, solution, null);
            }
            catch (OperationCanceledException)
            {
                throw; // let Parallel.ForEach propagate cancellation
            }
            catch (Exception ex)
            {
                results[index] = new PropagationResult(task.Spacecraft, null, ex);
            }
            finally
            {
                int done = Interlocked.Increment(ref completed);
                options.Progress?.Report(new BatchProgress(done, tasks.Count));
            }
        });

        stopwatch.Stop();
        return new BatchResult(results, stopwatch.Elapsed);
    }

    /// <summary>
    /// Propagate all tasks in parallel (async wrapper).
    /// </summary>
    /// <param name="tasks">Propagation tasks to execute. Must not contain duplicate Spacecraft instances.</param>
    /// <param name="options">Parallelism, cancellation, and progress options. Null uses defaults.</param>
    /// <returns>Aggregated batch result.</returns>
    public static Task<BatchResult> PropagateAsync(IReadOnlyList<PropagationTask> tasks, BatchOptions options = null)
    {
        return Task.Run(() => Propagate(tasks, options), options?.CancellationToken ?? default);
    }

    private static void ValidateNoDuplicateSpacecraft(IReadOnlyList<PropagationTask> tasks)
    {
        var seen = new HashSet<Body.Spacecraft.Spacecraft>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (!seen.Add(tasks[i].Spacecraft))
            {
                throw new ArgumentException(
                    $"Duplicate Spacecraft instance detected at index {i} " +
                    $"(name: '{tasks[i].Spacecraft.Name}'). " +
                    "Each task must use a distinct Spacecraft instance because Propagate() " +
                    "calls ResetPropagation() which is not thread-safe.",
                    nameof(tasks));
            }
        }
    }
}
