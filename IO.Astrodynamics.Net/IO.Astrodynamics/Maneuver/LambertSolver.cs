using System.Collections.Generic;
using IO.Astrodynamics.Math;

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
}