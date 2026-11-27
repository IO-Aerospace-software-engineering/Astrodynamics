using System;

namespace IO.Astrodynamics.Math;

/// <summary>
/// A bisection root finder, internal since 11.0.0 (D2 N4 of #668: the helpers and solvers no public member exposes).
/// </summary>
internal static class BisectionMethod
{
    internal static double Solve(
        Func<double, double> f,     // Your polynomial function
        double a,                   // Left endpoint
        double b,                   // Right endpoint
        double tolerance = 1E-12,   // Matching your tolerance
        int maxIterations = 1000)   // Matching your max iterations
    {
        if (f(a) * f(b) >= 0)
        {
            throw new ArgumentException("Function must have different signs at endpoints a and b");
        }

        double c = a;
        int iterations = 0;

        while ((b - a) > tolerance && iterations < maxIterations)
        {
            c = (a + b) / 2;
            double fc = f(c);

            if (System.Math.Abs(fc) < tolerance)
                break;

            if (f(a) * fc < 0)
            {
                b = c;
            }
            else
            {
                a = c;
            }

            iterations++;
        }

        if (iterations >= maxIterations)
        {
            throw new ArgumentException($"Failed to converge after {maxIterations} iterations");
        }

        return c;
    }
}