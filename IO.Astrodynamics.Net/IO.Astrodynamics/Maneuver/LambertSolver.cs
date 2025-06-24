using IO.Astrodynamics.Math;
using System.Collections.Generic;

namespace IO.Astrodynamics.Maneuver;

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
        // Conversion de la logique du constructeur C++ ici
        // Utiliser Vector3.Normalize, Vector3.Cross, Vector3.Magnitude, etc.
        // Remplir V1, V2, X, Iters, Nmax
    }

    /// <summary>
    /// Computes the time of flight for a given x parameter and number of revolutions in the Lambert problem.
    /// </summary>
    /// <param name="x">The x parameter (related to the geometry of the transfer).</param>
    /// <param name="N">The number of full revolutions.</param>
    /// <returns>The computed time of flight.</returns>
    public double XToTimeOfFlight(double x, int N)
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
    public double XToTimeOfFlightGeneral(double x, int N)
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
}