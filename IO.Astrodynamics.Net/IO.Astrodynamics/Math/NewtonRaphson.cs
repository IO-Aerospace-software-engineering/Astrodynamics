using System;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;

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
                throw new Exception($"Derivative too small (f'(x) = {fDerivative}) at iteration {i}. Adjust initial guess or problem scaling.");

            // Update the root estimate using Newton-Raphson formula
            double xNew = x - fValue / fDerivative;

            // Check for convergence (absolute and relative tolerance)
            if (System.Math.Abs(xNew - x) < tolerance || System.Math.Abs(xNew - x) < tolerance * System.Math.Abs(xNew))
                return xNew;

            if (double.IsNaN(xNew) || double.IsInfinity(xNew))
                throw new Exception("Newton-Raphson method diverged.");

            x = xNew;
        }


        throw new Exception("Newton-Raphson method did not converge within the maximum number of iterations.");
    }
    
    public static double[] SolveVector(
        Func<double[], double[]> costFunc,
        Func<double[], double[,]> jacobianFunc,
        double[] initialGuess,
        double tolerance = 1e-6,
        int maxIterations = 100)
    {
        var x = (double[])initialGuess.Clone();
        for (int iter = 0; iter < maxIterations; iter++)
        {
            double[] fx = costFunc(x);
            double norm = System.Math.Sqrt(fx.Sum(v => v * v));
            if (norm < tolerance)
                return x;

            double[,] J = jacobianFunc(x);

            // Solve J * dx = -f(x)
            double[] minusFx = fx.Select(v => -v).ToArray();
            double[] dx = SolveLinearSystem(J, minusFx);

            for (int k = 0; k < x.Length; k++)
                x[k] += dx[k];
        }
        throw new Exception("Vector Newton-Raphson did not converge.");
    }
    
    public static double BoundedNewtonRaphson(Func<double, double> f, Func<double, double> df, 
        double x0, double min, double max, double tolerance, int maxIterations)
    {
        double x = x0;
        for (int i = 0; i < maxIterations; i++)
        {
            double fx = f(x);
            if (System.Math.Abs(fx) < tolerance)
                return x;

            double dfx = df(x);
            if (System.Math.Abs(dfx) < 1e-10)
                break;

            double step = fx / dfx;
            double newX = x - step;
        
            // Bound the solution
            if (newX < min)
                newX = min;
            else if (newX > max)
                newX = max;

            if (System.Math.Abs(newX - x) < tolerance)
                return newX;

            x = newX;
        }
    
        throw new Exception("Failed to converge within maximum iterations");
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private static double[] SolveLinearSystem(double[,] a, double[] b)
    {
        var matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfArray(a);
        var vector = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray(b);
        var solution = matrix.Svd(true).Solve(vector);
        return solution.ToArray();
    }
}