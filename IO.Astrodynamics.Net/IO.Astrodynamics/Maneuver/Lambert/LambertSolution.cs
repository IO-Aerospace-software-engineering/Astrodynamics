using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Maneuver.Lambert;

/// <summary>
/// Represents a solution to the Lambert problem, which includes the velocity vectors and other parameters related to the transfer between two points in space.
/// </summary>
public class LambertSolution
{
    /// <summary>
    /// The number of full revolutions made during the transfer. A value of 0 indicates a direct transfer without any revolutions.
    /// </summary>
    public uint Revolutions { get; }
    
    /// <summary>
    /// The branch of the Lambert solution, indicating whether it is a left or right branch.
    /// A null value indicates that the solution corresponds to a direct transfer (0 revolutions).
    /// </summary>
    public LambertBranch? Branch { get; } // null pour 0 révolution
    
    /// <summary>
    /// The initial velocity vector at the departure point.
    /// </summary>
    public Vector3 V1 { get; }
    
    /// <summary>
    /// The final velocity vector at the arrival point.
    /// </summary>
    public Vector3 V2 { get; }
    
    /// <summary>
    /// The solution parameter (typically related to the universal variable formulation).
    /// </summary>
    public double X { get; }
    
    /// <summary>
    /// The number of iterations performed to obtain the solution.
    /// </summary>
    public uint Iterations { get; }
    
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LambertSolution"/> class with the specified parameters.
    /// </summary>
    /// <param name="revolutions">
    /// The number of full revolutions made during the transfer. A value of 0 indicates a direct transfer without any revolutions.
    /// </param>
    /// <param name="v1">
    /// The initial velocity vector at the departure point.
    /// </param>
    /// <param name="v2">
    /// The final velocity vector at the arrival point.
    /// </param>
    /// <param name="x">
    /// The solution parameter (typically related to the universal variable formulation).
    /// </param>
    /// <param name="iterations">
    /// The number of iterations performed to obtain the solution.
    /// </param>
    /// <param name="branch">
    /// The branch of the Lambert solution, indicating whether it is a left or right branch. Null for direct transfer (0 revolutions).
    /// </param>
    public LambertSolution(uint revolutions, in Vector3 v1, in Vector3 v2, double x, uint iterations, LambertBranch? branch = null)
    {
        Revolutions = revolutions;
        Branch = branch;
        V1 = v1;
        V2 = v2;
        X = x;
        Iterations = iterations;
    }
    
    
    ///<summary>
    /// Initializes a new instance of the <see cref="LambertSolution"/> class for a direct transfer (0 revolutions).
    /// </summary>
    /// <param name="v1">
    /// The initial velocity vector at the departure point.
    /// </param>
    /// <param name="v2">
    /// The final velocity vector at the arrival point.
    /// </param>
    /// <param name="x">
    /// The solution parameter (typically related to the universal variable formulation).
    /// </param>
    /// <param name="iterations">
    /// The number of iterations performed to obtain the solution.
    /// </param>
    public LambertSolution(in Vector3 v1, in Vector3 v2, double x, uint iterations):this(0, v1, v2, x, iterations, null)
    {
        // This constructor is specifically for the case of a direct transfer (0 revolutions).
        // It initializes the Revolutions to 0 and Branch to null.
    }
    
}

/// <summary>
/// Represents the branch of the Lambert solution.
/// </summary>
public enum LambertBranch
{
    Left,
    Right
}