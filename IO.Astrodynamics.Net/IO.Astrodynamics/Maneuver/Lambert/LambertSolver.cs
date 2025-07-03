using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.PDS.V4.MissionInformation;
using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.Marshalling;

namespace IO.Astrodynamics.Maneuver.Lambert;

/// <summary>
/// Represents a solver for the Lambert problem, which calculates the velocity vectors required to transfer between two points in space
/// </summary>
public class LambertSolver
{
    // Autres membres privés nécessaires
    private double m_lambda;

    public LambertSolver()
    {

    }

    public LambertResult Solve(bool isRetrograde, OrbitalParameters.OrbitalParameters startPosition, OrbitalParameters.OrbitalParameters finalPosition, CelestialItem centerOfMotion, ushort maxRevolution)
    {
        // 1. Vérification des paramètres d'entrée
        ArgumentNullException.ThrowIfNull(startPosition);
        ArgumentNullException.ThrowIfNull(finalPosition);
        ArgumentNullException.ThrowIfNull(centerOfMotion);

        // 2. Conversion des positions dans le référentiel ICRF et relatives au centre de mouvement
        var startICRF = startPosition.ToFrame(Frames.Frame.ICRF).RelativeTo(centerOfMotion, Aberration.None).ToStateVector();
        var finalICRF = finalPosition.ToFrame(Frames.Frame.ICRF).RelativeTo(centerOfMotion, Aberration.None).ToStateVector();

        // 3. Extraction des vecteurs de position et des époques
        Vector3 r1 = startICRF.Position;
        Vector3 r2 = finalICRF.Position;
        double tof = (finalICRF.Epoch - startICRF.Epoch).TotalSeconds;
        if (tof <= 0)
            throw new ArgumentException("Time of flight must be positive (final epoch after start epoch).");

        // 4. Paramètre gravitationnel
        double mu = centerOfMotion.GM;
        if (mu <= 0)
            throw new ArgumentException("Central body GM (mu) must be positive.");

        // 5. Calculs géométriques Lambert
        double c = (r2 - r1).Magnitude();
        double R1 = r1.Magnitude();
        double R2 = r2.Magnitude();
        double s = (c + R1 + R2) / 2.0;

        Vector3 ir1 = r1.Normalize();
        Vector3 ir2 = r2.Normalize();
        Vector3 ih = ir1.Cross(ir2).Normalize();

        if (System.Math.Abs(ih.Z) < 1e-12)
            throw new ArgumentException("The angular momentum vector has no z component, cannot define transfer direction.");

        double lambda2 = 1.0 - c / s;
        m_lambda = System.Math.Sqrt(lambda2);

        Vector3 it1, it2;
        if (ih.Z < 0.0)
        {
            m_lambda = -m_lambda;
            it1 = ir1.Cross(ih);
            it2 = ir2.Cross(ih);
        }
        else
        {
            it1 = ih.Cross(ir1);
            it2 = ih.Cross(ir2);
        }
        it1 = it1.Normalize();
        it2 = it2.Normalize();

        if (isRetrograde)
        {
            m_lambda = -m_lambda;
            it1 = it1.Inverse();
            it2 = it2.Inverse();
        }

        double lambda3 = m_lambda * lambda2;
        double T = System.Math.Sqrt(2.0 * mu / (s * s * s)) * tof;

        // 6. Détermination du nombre maximal de révolutions
        ushort Nmax = (ushort)(T / System.Math.PI);
        double T00 = System.Math.Acos(m_lambda) + m_lambda * System.Math.Sqrt(1.0 - lambda2);
        double T0 = T00 + Nmax * System.Math.PI;
        double T1 = 2.0 / 3.0 * (1.0 - lambda3);

        if (Nmax > 0 && T < T0)
        {
            int it = 0;
            double err = 1.0;
            double Tmin = T0;
            double x_old = 0.0, x_new = 0.0;
            while (true)
            {
                var (DT, DDT, DDDT) = TimeOfFlightDerivatives(x_old, Tmin);
                if (DT != 0.0)
                    x_new = x_old - DT * DDT / (DDT * DDT - DT * DDDT / 2.0);
                err = System.Math.Abs(x_old - x_new);
                if (err < 1e-13 || it > 12)
                    break;
                Tmin = XToTimeOfFlightGeneral(x_new, Nmax);
                x_old = x_new;
                it++;
            }
            if (Tmin > T)
                Nmax -= 1;
        }
        Nmax = System.Math.Min(maxRevolution, System.Math.Max((ushort)0, Nmax));

        // 7. Recherche des solutions pour chaque nombre de révolutions
        var result = new LambertResult(Nmax);

        // 7.1 - Solution 0 révolution (directe)
        double x0;
        if (T >= T00)
            x0 = -(T - T00) / (T - T00 + 4);
        else if (T <= T1)
            x0 = T1 * (T1 - T) / (2.0 / 5.0 * (1 - lambda2 * lambda3) * T) + 1;
        else
            x0 = System.Math.Pow((T / T00), 0.69314718055994529 / System.Math.Log(T1 / T00)) - 1.0;

        var (xZero, iterZero) = HalleySolver(T, x0, 0, 1e-5, 15);

        // 8. Compute velocity vectors and other parameters for each solution
        double gamma = System.Math.Sqrt(mu * s / 2.0);
        double rho = (R1 - R2) / c;
        double sigma = System.Math.Sqrt(1 - rho * rho);

        // 8.1 - Direct transfer solution (0 revolutions)
        result.AddSolution(ComputeSolution((uint)iterZero, lambda2, sigma, xZero, gamma, rho, R1, R2, ir1, ir2, it1, it2));

        // 8.2 - Multi-revolution solutions (left/right)
        for (int i = 1; i <= Nmax; ++i)
        {
            // Left branch
            double tmp = System.Math.Pow((i * Constants._2PI) / (8.0 * T), 2.0 / 3.0);
            double xLeft = (tmp - 1) / (tmp + 1);
            var (xL, itL) = HalleySolver(T, xLeft, i, 1e-8, 15);

            result.AddSolution(ComputeSolution((uint)itL, lambda2, sigma, xL, gamma, rho, R1, R2, ir1, ir2, it1, it2, (uint)i, LambertBranch.Left));

            // Right branch
            tmp = System.Math.Pow((8.0 * T) / (i * System.Math.PI), 2.0 / 3.0);
            double xRight = (tmp - 1) / (tmp + 1);
            var (xR, itR) = HalleySolver(T, xRight, i, 1e-8, 15);

            result.AddSolution(ComputeSolution((uint)itR, lambda2, sigma, xR, gamma, rho, R1, R2, ir1, ir2, it1, it2, (uint)i, LambertBranch.Right));
        }

        return result;
    }

    private LambertSolution ComputeSolution(uint it, double lambda2, double sigma, double x, double gamma, double rho, double r1, double r2, in Vector3 ir1, in Vector3 ir2, Vector3 it1, Vector3 it2, uint i = 0, LambertBranch? branch = null)
    {
        double y = System.Math.Sqrt(1.0 - lambda2 + lambda2 * x * x);
        double vr1 = gamma * ((m_lambda * y - x) - rho * (m_lambda * y + x)) / r1;
        double vr2 = -gamma * ((m_lambda * y - x) + rho * (m_lambda * y + x)) / r2;
        double vt = gamma * sigma * (y + m_lambda * x);
        double vt1 = vt / r1;
        double vt2 = vt / r2;

        Vector3 v1 = ir1 * vr1 + it1 * vt1;
        Vector3 v2 = ir2 * vr2 + it2 * vt2;

        return new LambertSolution(i, v1, v2, x, it, branch);
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