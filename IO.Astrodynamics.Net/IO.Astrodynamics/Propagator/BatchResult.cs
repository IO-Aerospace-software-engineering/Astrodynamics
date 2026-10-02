// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Aggregated results from a batch propagation run.
/// </summary>
public sealed class BatchResult
{
    /// <summary>
    /// Per-task results in the same order as the input task list.
    /// </summary>
    public IReadOnlyList<PropagationResult> Results { get; }

    /// <summary>
    /// True when every task in the batch succeeded.
    /// An empty batch is considered successful.
    /// </summary>
    public bool AllSucceeded { get; }

    /// <summary>
    /// Subset of <see cref="Results"/> that failed.
    /// </summary>
    public IReadOnlyList<PropagationResult> Failed { get; }

    /// <summary>
    /// Wall-clock time for the entire batch run.
    /// </summary>
    public TimeSpan ElapsedTime { get; }

    internal BatchResult(IReadOnlyList<PropagationResult> results, TimeSpan elapsedTime)
    {
        Results = results ?? throw new ArgumentNullException(nameof(results));
        ElapsedTime = elapsedTime;
        Failed = results.Where(r => !r.Succeeded).ToList();
        AllSucceeded = Failed.Count == 0;
    }
}
