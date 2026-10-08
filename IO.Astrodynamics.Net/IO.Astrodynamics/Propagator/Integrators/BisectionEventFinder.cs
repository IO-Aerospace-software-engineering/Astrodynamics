// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Root-finding within an accepted integration step by bisection on the cumulative time t, to locate the zero-crossing
/// of an event g-function to high precision (~1e-10 seconds). The public overload evaluates the g-function on the cubic
/// Hermite interpolation of the step; <see cref="RK78Integrator"/> evaluates it on states computed by shortened RK7(8)
/// steps, at the accuracy of the integrator.
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
        var hermiteStep = step;
        return FindRoot(step.CumulativeTime, step.CumulativeTime + step.StepSize, t =>
        {
            // Interpolate state at t using Hermite dense output
            var (pos, vel) = PropagationSegment.HermiteInterpolate(hermiteStep, t);
            return eventFunc(pos, vel, stepStartEpoch.AddSeconds(t));
        }, gStart, gEnd, tolerance);
    }

    /// <summary>
    /// Find the time at which the event function <paramref name="g"/> crosses zero between <paramref name="tStart"/>
    /// and <paramref name="tEnd"/>, by bisection.
    /// </summary>
    /// <param name="tStart">Start of the interval, in seconds from the segment base epoch.</param>
    /// <param name="tEnd">End of the interval, in seconds from the segment base epoch.</param>
    /// <param name="g">Event g-function of the time, in seconds from the segment base epoch.</param>
    /// <param name="gStart">g-function value at <paramref name="tStart"/>.</param>
    /// <param name="gEnd">g-function value at <paramref name="tEnd"/>.</param>
    /// <param name="tolerance">Convergence tolerance in seconds.</param>
    /// <returns>The time of the zero-crossing, in seconds from the segment base epoch.</returns>
    internal static double FindRoot(double tStart, double tEnd, Func<double, double> g, double gStart, double gEnd,
        double tolerance = DefaultTolerance)
    {
        if (gStart * gEnd > 0.0)
            throw new ArgumentException("g-function must have opposite signs at step boundaries for bisection.");

        double tLo = tStart;
        double tHi = tEnd;
        double gLo = gStart;

        for (int i = 0; i < MaxIterations; i++)
        {
            if (tHi - tLo <= tolerance)
                break;

            double tMid = 0.5 * (tLo + tHi);
            double gMid = g(tMid);

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
