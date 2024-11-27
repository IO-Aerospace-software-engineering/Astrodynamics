using System;

namespace IO.Astrodynamics.Math;

public class Solver
{
    /// <summary>
    /// Solve the quadratic equation ax^2 + bx + c = 0.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static double SolveQuadratic(double a, double b, double c)
    {
        double discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
            throw new InvalidOperationException("No real roots for the polynomial.");

        double root1 = (-b + System.Math.Sqrt(discriminant)) / (2 * a);
        double root2 = (-b - System.Math.Sqrt(discriminant)) / (2 * a);

        // Select the positive root (physically meaningful solution)
        return System.Math.Max(root1, root2);
    }
}