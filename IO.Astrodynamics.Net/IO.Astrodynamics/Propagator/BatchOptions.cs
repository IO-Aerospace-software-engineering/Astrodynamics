// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Configuration for parallel batch propagation: parallelism limits, cancellation, and progress reporting.
/// </summary>
public sealed record BatchOptions
{
    /// <summary>
    /// Maximum number of tasks to propagate concurrently.
    /// -1 (default) lets the runtime decide.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = -1;

    /// <summary>
    /// Token to cancel the batch. A cancelled token causes <see cref="OperationCanceledException"/>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = default;

    /// <summary>
    /// Optional callback to receive progress reports after each task completes.
    /// </summary>
    public IProgress<BatchProgress> Progress { get; init; }
}
