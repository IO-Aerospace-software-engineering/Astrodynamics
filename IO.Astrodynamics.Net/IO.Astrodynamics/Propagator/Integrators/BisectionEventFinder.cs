// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Root-finding within an accepted integration step using cubic Hermite dense output.
/// Bisects on the cumulative time t within the step to locate the zero-crossing
/// of an event g-function to high precision (~1e-10 seconds).
/// </summary>
public static class BisectionEventFinder
{
    private const int MaxIterations = 60; // ~60 iterations for 1e-10 precision in a 86400s step
    private const double DefaultTolerance = 1e-10;

    /// <summary>
    /// Find the cumulative time (seconds from segment base epoch) at which the event function
    /// crosses zero within the given accepted step.
    /// </summary>
    /// <param name="step">The accepted step containing endpoint states and accelerations.</param>
    /// <param name="eventFunc">
    /// Event g-function: (position, velocity, epoch) → double.
    /// Must have opposite signs at step start and step end.
    /// </param>
    /// <param name="gStart">g-function value at step start.</param>
    /// <param name="gEnd">g-function value at step end.</param>
    /// <param name="stepStartEpoch">Absolute epoch at the start of the segment (not the step).</param>
    /// <param name="tolerance">Convergence tolerance in seconds. Default: 1e-10.</param>
    /// <returns>Cumulative time (seconds from segment base epoch) at the zero-crossing.</returns>
    public static double FindRoot(
        in AcceptedStep step,
        Func<Vector3, Vector3, Time, double> eventFunc,
        double gStart, double gEnd,
        Time stepStartEpoch,
        double tolerance = DefaultTolerance)
    {
        if (gStart * gEnd > 0.0)
            throw new ArgumentException("g-function must have opposite signs at step boundaries for bisection.");

        double tLo = step.CumulativeTime;
        double tHi = step.CumulativeTime + step.StepSize;
        double gLo = gStart;

        for (int i = 0; i < MaxIterations; i++)
        {
            if (tHi - tLo <= tolerance)
                break;

            double tMid = 0.5 * (tLo + tHi);

            // Interpolate state at tMid using Hermite dense output
            var (pos, vel) = PropagationSegment.HermiteInterpolate(step, tMid);
            var epoch = stepStartEpoch.AddSeconds(tMid);
            double gMid = eventFunc(pos, vel, epoch);

            if (gMid == 0.0)
                return tMid;

            if (gLo * gMid < 0.0)
            {
                tHi = tMid;
            }
            else
            {
                tLo = tMid;
                gLo = gMid;
            }
        }

        return 0.5 * (tLo + tHi);
    }
}
