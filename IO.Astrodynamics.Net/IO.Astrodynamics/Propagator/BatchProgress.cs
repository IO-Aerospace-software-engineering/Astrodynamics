// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Reports the progress of a batch propagation run.
/// </summary>
/// <param name="Completed">Number of tasks that have finished (succeeded or failed).</param>
/// <param name="Total">Total number of tasks in the batch.</param>
public sealed record BatchProgress(int Completed, int Total);
