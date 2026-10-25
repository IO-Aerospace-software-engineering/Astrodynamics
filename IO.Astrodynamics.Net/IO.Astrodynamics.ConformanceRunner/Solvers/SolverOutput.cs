using System;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// An output of a conversion solver: its value, or the exception that kept the solver from computing it.
/// </summary>
/// <remarks>
/// A case checks the outputs present in its golden only (spec.md section 7). An output that cannot be computed is
/// therefore an ERROR only when the golden holds it, which <see cref="Comparison.OutputComparer"/> reports with the
/// message of the exception.
/// </remarks>
internal static class SolverOutput
{
    /// <summary>The value <paramref name="compute"/> gives, or the exception it throws.</summary>
    public static object Of(Func<object> compute)
    {
        try
        {
            return compute();
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
