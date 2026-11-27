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
/// Each task runs an independent <see cref="Body.Spacecraft.Spacecraft.Propagate(IO.Astrodynamics.TimeSystem.Window, PropagationOptions, System.Threading.CancellationToken)"/>
/// call with the settings of <see cref="PropagationTask.Options"/> (R06 of #668).
/// </summary>
/// <remarks>
/// <h3>Thread Safety</h3>
/// <para>
/// <b>SPICE API:</b> All SPICE calls are serialized by a global lock — safe but may limit scaling
/// when many tasks need ephemeris data concurrently.
/// </para>
/// <para>
/// <b>Integrators:</b> each task builds its own integrator from <see cref="PropagationTask.Options"/>.
/// </para>
/// <para>
/// <b>CelestialBody without geopotential:</b> Safe to share across tasks. Two-body gravity is
/// stateless, and the <c>Frame.GetStateOrientationToIcrf</c> fallback uses a
/// <c>ConcurrentDictionary</c> cache that is inherently thread-safe.
/// </para>
/// <para>
/// <b>CelestialBody with <c>GeopotentialModelParameters</c>:</b> safe to share across tasks since 11.0.0: each
/// propagation builds its own field from the body's model, over its shared coefficients (R06 of #668, one field per run).
/// Before, the propagation evaluated the field of the body, which is <b>NOT</b> thread-safe, as the body's own
/// <see cref="IO.Astrodynamics.Body.CelestialItem.EvaluateGravitationalAcceleration(IO.Astrodynamics.OrbitalParameters.OrbitalParameters)"/>
/// still is:
/// Each <see cref="IO.Astrodynamics.Body.CelestialBody"/> constructed with geopotential parameters
/// owns a <see cref="IO.Astrodynamics.Body.GeopotentialGravitationalField"/> that uses pre-allocated
/// mutable buffers (<c>_P</c>, <c>_dP</c> Legendre tables and <c>_cosMLambda</c>, <c>_sinMLambda</c>
/// trig arrays). These buffers are overwritten on every acceleration evaluation, so concurrent
/// access from multiple threads causes data races.
/// </para>
/// <para>
/// <b>Frame orientation cache:</b> The orientation cache of the central body frame is active only on
/// the thread that runs the propagation, for its duration; the shared <c>Frame</c> is never changed, so
/// concurrent propagations sharing a central body do not see each other's cache.
/// </para>
/// <para>
/// <b>Maneuvers:</b> a maneuver draws on a fuel tank of the spacecraft it is propagated with, a tank belonging to one
/// spacecraft (<see cref="IO.Astrodynamics.Body.Spacecraft.Spacecraft.AddFuelTank"/>): distinct spacecraft imply distinct
/// maneuvers and engines. A task whose maneuver draws on another spacecraft's tank, or on a tank of none, fails with an
/// <see cref="InvalidOperationException"/> in its <see cref="PropagationResult.Error"/> before anything runs (#676, #586;
/// before, it burnt the other spacecraft's fuel and wrote its records, or threw a <see cref="NullReferenceException"/>).
/// </para>
/// <para>
/// <b>Recommendation:</b> share the bodies across tasks, and give the settings once, in one
/// <see cref="PropagationOptions"/> value.
/// </para>
/// </remarks>
public static class BatchPropagator
{
    /// <summary>
    /// Propagate all tasks in parallel (blocking).
    /// </summary>
    /// <remarks>
    /// <c>Propagate(tasks, null)</c> is ambiguous between this overload and the one with a progress callback (CS0121),
    /// accepted by decision 5 of lot L4 of #668: name the argument, <c>Propagate(tasks, options: null)</c>, or leave it
    /// out.
    /// </remarks>
    /// <param name="tasks">Propagation tasks to execute. Must not contain duplicate Spacecraft instances.</param>
    /// <param name="options">The parallelism; null for <see cref="BatchOptions.Default"/>.</param>
    /// <param name="cancellationToken">Stops the batch, the running tasks included (see
    /// <see cref="Propagate(IReadOnlyList{PropagationTask}, IProgress{BatchProgress}, BatchOptions, CancellationToken)"/>).</param>
    /// <returns>Aggregated batch result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tasks"/> contains duplicate Spacecraft instances.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
    /// carries it.</exception>
    public static BatchResult Propagate(IReadOnlyList<PropagationTask> tasks, BatchOptions options = null,
        CancellationToken cancellationToken = default)
    {
        return Propagate(tasks, progress: null, options, cancellationToken);
    }

    /// <summary>
    /// Propagate all tasks in parallel (blocking), reporting the progress after each task that completes, propagated or
    /// failed.
    /// </summary>
    /// <remarks>
    /// Cancellation (R28 of #668): the token is read before anything runs, so that a token already cancelled changes
    /// nothing, then by each task before it starts and inside its propagation, at each accepted step of its integrator
    /// (see <see cref="Body.Spacecraft.Spacecraft.Propagate(IO.Astrodynamics.TimeSystem.Window, PropagationOptions, CancellationToken)"/>):
    /// the running tasks stop too, where before 11.0.0 the token of <c>BatchOptions</c> was read before each task only
    /// and the running ones ran to their end. A batch stopped throws one <see cref="OperationCanceledException"/>
    /// carrying the token, never an <see cref="AggregateException"/>, and returns no result: the tasks completed before
    /// the stop are complete, each spacecraft it stopped is left as a stopped propagation leaves it (C2 of lot L4), the
    /// tasks not started are untouched. A stopped task is not reported to <paramref name="progress"/>.
    /// </remarks>
    /// <param name="tasks">Propagation tasks to execute. Must not contain duplicate Spacecraft instances.</param>
    /// <param name="progress">Receives the number of completed tasks after each one; null for none.</param>
    /// <param name="options">The parallelism; null for <see cref="BatchOptions.Default"/>.</param>
    /// <param name="cancellationToken">Stops the batch, the running tasks included.</param>
    /// <returns>Aggregated batch result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tasks"/> contains duplicate Spacecraft instances.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
    /// carries it.</exception>
    public static BatchResult Propagate(IReadOnlyList<PropagationTask> tasks, IProgress<BatchProgress> progress,
        BatchOptions options = null, CancellationToken cancellationToken = default)
    {
        if (tasks == null) throw new ArgumentNullException(nameof(tasks));

        ValidateNoDuplicateSpacecraft(tasks);
        cancellationToken.ThrowIfCancellationRequested();

        if (tasks.Count == 0)
            return new BatchResult(Array.Empty<PropagationResult>(), TimeSpan.Zero);

        options ??= BatchOptions.Default;

        var results = new PropagationResult[tasks.Count];
        int completed = 0;
        var stopwatch = Stopwatch.StartNew();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        Parallel.ForEach(Enumerable.Range(0, tasks.Count), parallelOptions, index =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var task = tasks[index];
            try
            {
                var solution = task.Spacecraft.Propagate(task.Window, task.Options, cancellationToken);
                results[index] = new PropagationResult(task.Spacecraft, solution, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // the batch stops: Parallel.ForEach throws one OperationCanceledException of the token
            }
            catch (Exception ex)
            {
                results[index] = new PropagationResult(task.Spacecraft, null, ex);
            }

            int done = Interlocked.Increment(ref completed);
            progress?.Report(new BatchProgress(done, tasks.Count));
        });

        stopwatch.Stop();
        return new BatchResult(results, stopwatch.Elapsed);
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
                    "re-arms the spacecraft (ResetPropagation), which is not thread-safe.",
                    nameof(tasks));
            }
        }
    }
}
