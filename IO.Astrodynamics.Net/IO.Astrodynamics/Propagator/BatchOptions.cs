// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Configuration for parallel batch propagation: the parallelism limit. Since 11.0.0 the cancellation token and the
/// progress callback are parameters of <see cref="BatchPropagator.Propagate(System.Collections.Generic.IReadOnlyList{PropagationTask}, System.IProgress{BatchProgress}, BatchOptions, System.Threading.CancellationToken)"/>
/// (R28 of #668, decision 4 of lot L4), and the record has the <see cref="Default"/> of every options record (D1).
/// </summary>
public sealed record BatchOptions
{
    /// <summary>
    /// The default settings, equal to <c>new BatchOptions()</c>.
    /// </summary>
    public static BatchOptions Default { get; } = new();

    /// <summary>
    /// Maximum number of tasks to propagate concurrently.
    /// -1 (default) lets the runtime decide.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = -1;
}
