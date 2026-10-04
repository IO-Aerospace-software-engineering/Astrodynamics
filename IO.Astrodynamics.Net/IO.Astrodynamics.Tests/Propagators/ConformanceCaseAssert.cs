using IO.Astrodynamics.Math;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// Checks a propagation against the golden of a conformance case the way the conformance runner does
/// (IO.Astrodynamics.ConformanceRunner, PropagatorSolver): the final state is interpolated at the exact end of the
/// window, whose UTC epoch is converted to TDB, and the errors are vector magnitudes. The limits are non-regression
/// guards set from the measured errors, not accuracy specifications.
/// </summary>
internal static class ConformanceCaseAssert
{
    internal static void FinalStateWithin(
        ITestOutputHelper output,
        string caseName,
        PropagationSolution solution,
        Window window,
        Vector3 expectedPosition,
        Vector3 expectedVelocity,
        double positionLimitMeters,
        double velocityLimitMetersPerSecond)
    {
        var (position, velocity) = solution.InterpolateAt(window.EndDate.ToTDB());
        double positionError = (position - expectedPosition).Magnitude();
        double velocityError = (velocity - expectedVelocity).Magnitude();

        output.WriteLine(
            $"{caseName}: position error {positionError:F3} m (limit {positionLimitMeters} m), " +
            $"velocity error {velocityError * 1000.0:F3} mm/s (limit {velocityLimitMetersPerSecond * 1000.0} mm/s)");

        Assert.True(positionError < positionLimitMeters,
            $"{caseName}: position error {positionError:F3} m, limit {positionLimitMeters} m");
        Assert.True(velocityError < velocityLimitMetersPerSecond,
            $"{caseName}: velocity error {velocityError:F6} m/s, limit {velocityLimitMetersPerSecond} m/s");
    }
}
