using System;

namespace IO.Astrodynamics.Math;

/// <summary>
/// Newton-Raphson method for finding the root of a function. 
/// </summary>
public class NewtonRaphson
{
    /// <summary>
    /// Solve the equation f(x) = 0 using the Newton-Raphson method. 
    /// </summary>
    /// <param name="function"></param>
    /// <param name="derivative"></param>
    /// <param name="initialGuess"></param>
    /// <param name="tolerance"></param>
    /// <param name="maxIterations"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static double Solve(
        Func<double, double> function,
        Func<double, double> derivative,
        double initialGuess,
        double tolerance = 1e-6,
        int maxIterations = 100)
    {
        double x = initialGuess;

        for (int i = 0; i < maxIterations; i++)
        {
            double fValue = function(x);
            double fDerivative = derivative(x);

            if (System.Math.Abs(fDerivative) < 1e-12) // Avoid division by zero or near-zero derivatives
                throw new Exception("Derivative too small; method fails.");

            // Update the root estimate using Newton-Raphson formula
            double xNew = x - fValue / fDerivative;

            // Check for convergence
            if (System.Math.Abs(xNew - x) < tolerance)
                return xNew;

            x = xNew;
        }

        throw new Exception("Newton-Raphson method did not converge within the maximum number of iterations.");
    }
}