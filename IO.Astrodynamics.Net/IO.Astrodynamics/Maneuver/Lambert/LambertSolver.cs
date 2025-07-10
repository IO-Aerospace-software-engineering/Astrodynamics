using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using System;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver.Lambert;

/// <summary>
/// Represents a solver for the Lambert problem, which calculates the velocity vectors required to transfer between two points in space
/// </summary>
public class LambertSolver
{
    const double BattinThreshold = 0.01;
    const double LagrangeThreshold = 0.2;
    /// <summary>
    /// Solves the Lambert problem to find the velocity vectors required to transfer between two points in space.
    /// </summary>
    /// <param name="isRetrograde">Indicates whether the transfer is retrograde (true) or prograde (false).</param>
    /// <param name="initialOrbitalParameters">The initial position and velocity of the spacecraft at the start of the transfer.</param>
    /// <param name="targetOjectOrbitalParameters">The orbital parameters of the target object at the end of the transfer.</param>
    /// <param name="centerOfMotion">The celestial body around which the transfer is being performed.</param>
    /// <param name="maxRevolution">The maximum number of revolutions allowed in the transfer.</param>
    /// <returns>A <see cref="LambertResult"/> containing the computed velocity vectors and other parameters for the transfer.</returns>
    public LambertResult Solve(bool isRetrograde, OrbitalParameters.OrbitalParameters initialOrbitalParameters, OrbitalParameters.OrbitalParameters targetOjectOrbitalParameters,
        CelestialItem centerOfMotion, ushort maxRevolution)
    {
        // Validate input parameters
        ArgumentNullException.ThrowIfNull(initialOrbitalParameters);
        ArgumentNullException.ThrowIfNull(targetOjectOrbitalParameters);
        ArgumentNullException.ThrowIfNull(centerOfMotion);

        // Convert the start and final positions to ICRF frame relative to the center of motion
        var startICRF = initialOrbitalParameters.ToFrame(Frames.Frame.ICRF).RelativeTo(centerOfMotion, Aberration.None).ToStateVector();
        var finalICRF = targetOjectOrbitalParameters.ToFrame(Frames.Frame.ICRF).RelativeTo(centerOfMotion, Aberration.None).ToStateVector();

        // Compute the position vectors in ICRF frame and the time of flight
        Vector3 r1 = startICRF.Position;
        Vector3 r2 = finalICRF.Position;
        double tof = (finalICRF.Epoch - startICRF.Epoch).TotalSeconds;
        if (tof <= 0)
            throw new ArgumentException("Time of flight must be positive (final epoch after start epoch).");

        // 4. Retrieve the gravitational parameter (GM) of the central body
        double mu = centerOfMotion.GM;
        if (mu <= 0)
            throw new ArgumentException("Central body GM (mu) must be positive.");

        // 5. Calculate the geometric parameters for the Lambert problem
        double c = (r2 - r1).Magnitude();
        double r1Norm = r1.Magnitude();
        double r2Norm = r2.Magnitude();
        double s = (c + r1Norm + r2Norm) / 2.0;

        // Check if the transfer is possible
        Vector3 ir1 = r1.Normalize();
        Vector3 ir2 = r2.Normalize();
        Vector3 ih = ir1.Cross(ir2).Normalize();

        if (System.Math.Abs(ih.Z) < 1e-12)
            throw new ArgumentException("The angular momentum vector has no z component, cannot define transfer direction.");

        // compute the Lambert parameter lambda
        double lambda2 = 1.0 - c / s;
        double lambda = System.Math.Sqrt(lambda2);

        Vector3 it1, it2;
        if (ih.Z < 0.0)
        {
            lambda = -lambda;
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

        // If the transfer is retrograde, invert the lambda and the in-plane vectors
        if (isRetrograde)
        {
            lambda = -lambda;
            it1 = it1.Inverse();
            it2 = it2.Inverse();
        }

        double lambda3 = lambda * lambda2;
        double T = System.Math.Sqrt(2.0 * mu / (s * s * s)) * tof;

        // 6. Determine the number of revolutions
        ushort nmax = (ushort)(T / System.Math.PI);
        double t00 = System.Math.Acos(lambda) + lambda * System.Math.Sqrt(1.0 - lambda2);
        double t0 = t00 + nmax * System.Math.PI;
        double t1 = 2.0 / 3.0 * (1.0 - lambda3);

        if (nmax > 0 && T < t0)
        {
            int it = 0;
            double tMin = t0;
            double xOld = 0.0, xNew = 0.0;
            while (true)
            {
                var (firstDerivative, secondDerivative, thirdDerivative) = TimeOfFlightDerivatives(xOld, tMin, lambda);
                if (firstDerivative != 0.0)
                    xNew = xOld - firstDerivative * secondDerivative / (secondDerivative * secondDerivative - firstDerivative * thirdDerivative / 2.0);
                var err = System.Math.Abs(xOld - xNew);
                if (err < 1e-13 || it > 12)
                    break;
                tMin = XToTimeOfFlightGeneral(xNew, nmax, lambda);
                xOld = xNew;
                it++;
            }

            if (tMin > T)
                nmax -= 1;
        }

        nmax = System.Math.Min(maxRevolution, System.Math.Max((ushort)0, nmax));

        // 7. Initialize the LambertResult object to store solutions
        var result = new LambertResult(nmax);

        // 7.1 - Solution 0 révolution (directe)
        double x0;
        if (T >= t00)
            x0 = -(T - t00) / (T - t00 + 4);
        else if (T <= t1)
            x0 = t1 * (t1 - T) / (2.0 / 5.0 * (1 - lambda2 * lambda3) * T) + 1;
        else
            x0 = System.Math.Pow((T / t00), 0.69314718055994529 / System.Math.Log(t1 / t00)) - 1.0;

        var (xZero, iterZero) = HalleySolver(T, x0, 0, 1e-5, 15, lambda);

        // 8. Compute the parameters for the Lambert transfer solutions
        double gamma = System.Math.Sqrt(mu * s / 2.0);
        double rho = (r1Norm - r2Norm) / c;
        double sigma = System.Math.Sqrt(1 - rho * rho);

        // 8.1 - Compute the velocity vectors for the zero-revolution solution
        result.AddSolution(ComputeSolution(startICRF, finalICRF, (uint)iterZero, lambda, lambda2, sigma, xZero, gamma, rho, r1Norm, r2Norm, ir1, ir2, it1, it2));

        // 8.2 - Multi-revolution solutions (left/right)
        for (int i = 1; i <= nmax; ++i)
        {
            // Left branch
            double tmp = System.Math.Pow((i * Constants._2PI) / (8.0 * T), 2.0 / 3.0);
            double xLeft = (tmp - 1) / (tmp + 1);
            var (xL, itL) = HalleySolver(T, xLeft, i, 1e-8, 15, lambda);

            result.AddSolution(ComputeSolution(startICRF, finalICRF, (uint)itL, lambda, lambda2, sigma, xL, gamma, rho, r1Norm, r2Norm, ir1, ir2, it1, it2, (uint)i,
                LambertBranch.Left));

            // Right branch
            tmp = System.Math.Pow((8.0 * T) / (i * System.Math.PI), 2.0 / 3.0);
            double xRight = (tmp - 1) / (tmp + 1);
            var (xR, itR) = HalleySolver(T, xRight, i, 1e-8, 15, lambda);

            result.AddSolution(ComputeSolution(startICRF, finalICRF, (uint)itR, lambda, lambda2, sigma, xR, gamma, rho, r1Norm, r2Norm, ir1, ir2, it1, it2, (uint)i,
                LambertBranch.Right));
        }

        return result;
    }

    /// <summary>
    /// Computes the velocity vectors for a given Lambert solution based on the input parameters.
    /// </summary>
    /// <param name="initialOrbitalParams">The initial orbital parameters of the spacecraft at the start of the transfer.</param>
    /// <param name="targetObjectOrbitalParameters">The orbital parameters of the target object at the end of the transfer.</param>
    /// <param name="it">The number of iterations performed to compute the solution.</param>
    /// <param name="lambda">The Lambert parameter (related to the geometry of the transfer).</param>
    /// <param name="lambda2">The square of the Lambert parameter.</param>
    /// <param name="sigma">The sine of the angle between the position vectors at the start and end of the transfer.</param>
    /// <param name="x">The x parameter (related to the geometry of the transfer).</param>
    /// <param name="gamma">The gamma parameter (related to the gravitational parameter and semi-major axis).</param>
    /// <param name="rho">The ratio of the difference in distances to the total distance between the start and end positions.</param>
    /// <param name="r1">The magnitude of the position vector at the start of the transfer.</param>
    /// <param name="r2">The magnitude of the position vector at the end of the transfer.</param>
    /// <param name="ir1">The unit vector in the direction of the position vector at the start of the transfer.</param>
    /// <param name="ir2">The unit vector in the direction of the position vector at the end of the transfer.</param>
    /// <param name="it1">The unit vector in the direction of the in-plane vector at the start of the transfer.</param>
    /// <param name="it2">The unit vector in the direction of the in-plane vector at the end of the transfer.</param>
    /// <param name="i">The number of revolutions (default is 0).</param>
    /// <param name="branch">The branch of the Lambert solution (left or right, default is null for direct transfer).</param>
    private LambertSolution ComputeSolution(StateVector initialOrbitalParams, StateVector targetObjectOrbitalParameters, uint it, double lambda, double lambda2, double sigma,
        double x, double gamma, double rho, double r1, double r2, in Vector3 ir1,
        in Vector3 ir2, Vector3 it1, Vector3 it2, uint i = 0, LambertBranch? branch = null)
    {
        double y = System.Math.Sqrt(1.0 - lambda2 + lambda2 * x * x);
        double vr1 = gamma * ((lambda * y - x) - rho * (lambda * y + x)) / r1;
        double vr2 = -gamma * ((lambda * y - x) + rho * (lambda * y + x)) / r2;
        double vt = gamma * sigma * (y + lambda * x);
        double vt1 = vt / r1;
        double vt2 = vt / r2;

        Vector3 v1 = ir1 * vr1 + it1 * vt1;
        Vector3 v2 = ir2 * vr2 + it2 * vt2;

        Vector3 deltaV1 = v1 - initialOrbitalParams.Velocity;
        Vector3 deltaV2 = targetObjectOrbitalParameters.Velocity - v2;

        return new LambertSolution(i, v1, v2, x, it, deltaV1, deltaV2, branch);
    }

    /// <summary>
    /// Computes the time of flight for a given x parameter and number of revolutions in the Lambert problem.
    /// </summary>
    /// <param name="x">The x parameter (related to the geometry of the transfer).</param>
    /// <param name="n">The number of full revolutions.</param>
    /// <param name="lambda">The Lambert parameter (related to the geometry of the transfer).</param>
    /// <returns>The computed time of flight.</returns>
    private double XToTimeOfFlight(double x, int n, double lambda)
    {
        double a = 1.0 / (1.0 - x * x);
        double tof;

        if (a > 0) // Elliptic case
        {
            double alpha = 2.0 * System.Math.Acos(x);
            double beta = 2.0 * System.Math.Asin(System.Math.Sqrt(lambda * lambda / a));
            if (lambda < 0.0) beta = -beta;
            tof = (a * System.Math.Sqrt(a) * ((alpha - System.Math.Sin(alpha)) - (beta - System.Math.Sin(beta)) + 2.0 * System.Math.PI * n)) / 2.0;
        }
        else // Hyperbolic case
        {
            double alpha = 2.0 * System.Math.Acosh(x);
            double beta = 2.0 * System.Math.Asinh(System.Math.Sqrt(-lambda * lambda / a));
            if (lambda < 0.0) beta = -beta;
            tof = (-a * System.Math.Sqrt(-a) * ((beta - System.Math.Sinh(beta)) - (alpha - System.Math.Sinh(alpha)))) / 2.0;
        }

        return tof;
    }

    /// <summary>
    /// Computes the time of flight for a given x parameter and number of revolutions using the appropriate method (Battin, Lagrange, or Lancaster).
    /// </summary>
    /// <param name="x">The x parameter (related to the geometry of the transfer).</param>
    /// <param name="n">The number of full revolutions.</param>
    /// <param name="lambda">lambda parameter</param>
    /// <returns>The computed time of flight.</returns>
    private double XToTimeOfFlightGeneral(double x, int n, double lambda)
    {
        
        double dist = System.Math.Abs(x - 1);

        if (dist < LagrangeThreshold && dist > BattinThreshold)
        {
            // Use Lagrange time of flight expression
            return XToTimeOfFlight(x, n, lambda);
        }

        double k = lambda * lambda;
        double e = x * x - 1.0;
        double rho = System.Math.Abs(e);
        double z = System.Math.Sqrt(1 + k * e);

        if (dist < BattinThreshold)
        {
            // Use Battin series time of flight expression
            double eta = z - lambda * x;
            double s1 = 0.5 * (1.0 - lambda - x * eta);
            double q = SpecialFunctions.Hypergeometric(s1, 1e-11);
            q = 4.0 / 3.0 * q;
            double tof = (eta * eta * eta * q + 4.0 * lambda * eta) / 2.0 + n * System.Math.PI / System.Math.Pow(rho, 1.5);
            return tof;
        }
        else
        {
            // Use Lancaster time of flight expression
            double y = System.Math.Sqrt(rho);
            double g = x * z - lambda * e;
            double d;
            if (e < 0)
            {
                double l = System.Math.Acos(g);
                d = n * System.Math.PI + l;
            }
            else
            {
                double f = y * (z - lambda * x);
                d = System.Math.Log(f + g);
            }

            double tof = (x - lambda * z - d / y) / e;
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
    /// <param name="lambda">The Lambert parameter, which is used in the calculation of the derivatives.</param>
    /// <returns>A tuple containing the first, second, and third derivatives of the time of flight function: <list type="bullet">
    /// <item><description><c>firstDerivative</c>: The first derivative.</description></item>
    /// <item><description><c>secondDerivative</c>: The second derivative.</description></item>
    /// <item><description><c>thirdDerivative</c>: The third derivative.</description></item> </list></returns>
    private (double firstDerivative, double secondDerivative, double thirdDerivative) TimeOfFlightDerivatives(double x, double timeOfFlight, double lambda)
    {
        double lambdaSquared = lambda * lambda;
        double lambdaCubed = lambdaSquared * lambda;
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
    /// <param name="n">The number of full revolutions in the transfer orbit.</param>
    /// <param name="eps">The convergence tolerance for the solution.</param>
    /// <param name="iterMax">The maximum number of iterations allowed.</param>
    /// <param name="lambda">The Lambert parameter, which is used in the calculation of the time of flight.</param>
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
    private (double x, int iterations) HalleySolver(double T, double x0, int n, double eps, int iterMax, double lambda)
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
            tof = XToTimeOfFlightGeneral(x, n, lambda);

            // Compute the first, second, and third derivatives of the time of flight function.
            var (firstDv, secondDv, thirdDv) = TimeOfFlightDerivatives(x, tof, lambda);

            // Calculate the difference between the computed and target time of flight.
            delta = tof - T;

            // Compute the square of the first derivative for use in the update formula.
            double dt2 = firstDv * firstDv;

            // Update the value of `x` using Halley's method formula.
            xnew = x - delta * (dt2 - delta * secondDv / 2.0) /
                (firstDv * (dt2 - delta * secondDv) + thirdDv * delta * delta / 6.0);

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