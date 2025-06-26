using IO.Astrodynamics.Math;
using IO.Astrodynamics.PDS.V4.MissionInformation;
using System.Collections.Generic;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// Represents a solver for the Lambert problem, which calculates the velocity vectors required to transfer between two points in space
/// </summary>
public class LambertSolver
{
    public Vector3 R1 { get; }
    public Vector3 R2 { get; }
    public double TimeOfFlight { get; }
    public double Mu { get; }
    public int Nmax { get; private set; }
    public List<Vector3> V1 { get; }
    public List<Vector3> V2 { get; }
    public List<double> X { get; }
    public List<int> Iters { get; }

    // Autres membres privés nécessaires
    private double m_c, m_s, m_lambda;
    private int m_multiRevs;

    public LambertSolver(Vector3 r1, Vector3 r2, double tof, double mu, bool isCounterClockwise, int maxRevolution = 0)
    {
        R1 = r1;
        R2 = r2;
        TimeOfFlight = tof;
        Mu = mu;
        m_multiRevs = maxRevolution;

        V1 = new List<Vector3>();
        V2 = new List<Vector3>();
        X = new List<double>();
        Iters = new List<int>();
    }

    public void Solve(bool isCounterClockwise)
    {
        
    }

    /// <summary>
    /// Computes the time of flight for a given x parameter and number of revolutions in the Lambert problem.
    /// </summary>
    /// <param name="x">The x parameter (related to the geometry of the transfer).</param>
    /// <param name="N">The number of full revolutions.</param>
    /// <returns>The computed time of flight.</returns>
    private double XToTimeOfFlight(double x, int N)
    {
        double a = 1.0 / (1.0 - x * x);
        double tof;

        if (a > 0) // Elliptic case
        {
            double alpha = 2.0 * System.Math.Acos(x);
            double beta = 2.0 * System.Math.Asin(System.Math.Sqrt(m_lambda * m_lambda / a));
            if (m_lambda < 0.0) beta = -beta;
            tof = (a * System.Math.Sqrt(a) * ((alpha - System.Math.Sin(alpha)) - (beta - System.Math.Sin(beta)) + 2.0 * System.Math.PI * N)) / 2.0;
        }
        else // Hyperbolic case
        {
            double alpha = 2.0 * System.Math.Acosh(x);
            double beta = 2.0 * System.Math.Asinh(System.Math.Sqrt(-m_lambda * m_lambda / a));
            if (m_lambda < 0.0) beta = -beta;
            tof = (-a * System.Math.Sqrt(-a) * ((beta - System.Math.Sinh(beta)) - (alpha - System.Math.Sinh(alpha)))) / 2.0;
        }

        return tof;
    }

    /// <summary>
    /// Computes the time of flight for a given x parameter and number of revolutions using the appropriate method (Battin, Lagrange, or Lancaster).
    /// </summary>
    /// <param name="x">The x parameter (related to the geometry of the transfer).</param>
    /// <param name="N">The number of full revolutions.</param>
    /// <returns>The computed time of flight.</returns>
    private double XToTimeOfFlightGeneral(double x, int N)
    {
        const double battinThreshold = 0.01;
        const double lagrangeThreshold = 0.2;
        double dist = System.Math.Abs(x - 1);

        if (dist < lagrangeThreshold && dist > battinThreshold)
        {
            // Use Lagrange time of flight expression
            return XToTimeOfFlight(x, N);
        }

        double K = m_lambda * m_lambda;
        double E = x * x - 1.0;
        double rho = System.Math.Abs(E);
        double z = System.Math.Sqrt(1 + K * E);

        if (dist < battinThreshold)
        {
            // Use Battin series time of flight expression
            double eta = z - m_lambda * x;
            double S1 = 0.5 * (1.0 - m_lambda - x * eta);
            double Q = SpecialFunctions.Hypergeometric(S1, 1e-11);
            Q = 4.0 / 3.0 * Q;
            double tof = (eta * eta * eta * Q + 4.0 * m_lambda * eta) / 2.0 + N * System.Math.PI / System.Math.Pow(rho, 1.5);
            return tof;
        }
        else
        {
            // Use Lancaster time of flight expression
            double y = System.Math.Sqrt(rho);
            double g = x * z - m_lambda * E;
            double d;
            if (E < 0)
            {
                double l = System.Math.Acos(g);
                d = N * System.Math.PI + l;
            }
            else
            {
                double f = y * (z - m_lambda * x);
                d = System.Math.Log(f + g);
            }

            double tof = (x - m_lambda * z - d / y) / E;
            return tof;
        }
    }

    /// <summary>
    /// Calculates the first, second, and third derivatives of the time of flight function with respect to the input
    /// parameter <paramref name="x"/>.
    /// </summary>
    /// <remarks>The method assumes that <paramref name="x"/> is within the range [-1, 1]. Values outside this
    /// range may result in undefined behavior due to the mathematical constraints of the calculation.</remarks>
    /// <param name="x">The input parameter for which the derivatives are calculated. Must be in the range [-1, 1].</param>
    /// <param name="timeOfFlight">The time of flight value used in the derivative calculations.</param>
    /// <returns>A tuple containing the first, second, and third derivatives of the time of flight function: <list type="bullet">
    /// <item><description><c>firstDerivative</c>: The first derivative.</description></item>
    /// <item><description><c>secondDerivative</c>: The second derivative.</description></item>
    /// <item><description><c>thirdDerivative</c>: The third derivative.</description></item> </list></returns>
    private (double firstDerivative, double secondDerivative, double thirdDerivative) TimeOfFlightDerivatives(double x, double timeOfFlight)
    {
        double lambdaSquared = m_lambda * m_lambda;
        double lambdaCubed = lambdaSquared * m_lambda;
        double oneMinusXSquared = 1.0 - x * x;
        double y = System.Math.Sqrt(1.0 - lambdaSquared * oneMinusXSquared);
        double ySquared = y * y;
        double yCubed = ySquared * y;

        double firstDerivative = 1.0 / oneMinusXSquared * (3.0 * timeOfFlight * x - 2.0 + 2.0 * lambdaCubed * x / y);
        double secondDerivative = 1.0 / oneMinusXSquared * (3.0 * timeOfFlight + 5.0 * x * firstDerivative + 2.0 * (1.0 - lambdaSquared) * lambdaCubed / yCubed);
        double thirdDerivative = 1.0 / oneMinusXSquared *
                                 (7.0 * x * secondDerivative + 8.0 * firstDerivative - 6.0 * (1.0 - lambdaSquared) * lambdaSquared * lambdaCubed * x / yCubed / ySquared);

        return (firstDerivative, secondDerivative, thirdDerivative);
    }

    
    /// <summary>
    /// Solves the Lambert problem using Halley's method for root-finding.
    /// This method iteratively refines the value of the parameter `x` to match the target time of flight `T`.
    /// </summary>
    /// <param name="T">The target time of flight.</param>
    /// <param name="x0">The initial guess for the parameter `x`.</param>
    /// <param name="N">The number of full revolutions in the transfer orbit.</param>
    /// <param name="eps">The convergence tolerance for the solution.</param>
    /// <param name="iterMax">The maximum number of iterations allowed.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description><c>x</c>: The converged value of the parameter `x`.</description></item>
    /// <item><description><c>iterations</c>: The number of iterations performed.</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// Halley's method is a third-order iterative method for solving nonlinear equations.
    /// It uses the first, second, and third derivatives of the function to achieve faster convergence.
    /// </remarks>
    private (double x, int iterations) HalleySolver(double T, double x0, int N, double eps, int iterMax)
    {
        // Initialize the parameter `x` with the initial guess `x0`.
        double x = x0;
        int it = 0; // Iteration counter.
        double err = 1.0; // Error metric for convergence.
        double xnew = 0.0; // Variable to store the updated value of `x`.
        double tof = 0.0, delta = 0.0; // Variables for time of flight and difference calculation.
    
        // Iterative loop to refine the value of `x` until convergence or maximum iterations are reached.
        while ((err > eps) && (it < iterMax))
        {
            // Compute the time of flight for the current value of `x`.
            tof = XToTimeOfFlightGeneral(x, N);
    
            // Compute the first, second, and third derivatives of the time of flight function.
            var (firstDv, secondDv, thirdDv) = TimeOfFlightDerivatives(x, tof);
    
            // Calculate the difference between the computed and target time of flight.
            delta = tof - T;
    
            // Compute the square of the first derivative for use in the update formula.
            double DT2 = firstDv * firstDv;
    
            // Update the value of `x` using Halley's method formula.
            xnew = x - delta * (DT2 - delta * secondDv / 2.0) /
                (firstDv * (DT2 - delta * secondDv) + thirdDv * delta * delta / 6.0);
    
            // Calculate the error as the absolute difference between the old and new values of `x`.
            err = System.Math.Abs(x - xnew);
    
            // Update `x` with the new value.
            x = xnew;
    
            // Increment the iteration counter.
            it++;
        }
    
        // Return the converged value of `x` and the number of iterations performed.
        return (x, it);
    }
}