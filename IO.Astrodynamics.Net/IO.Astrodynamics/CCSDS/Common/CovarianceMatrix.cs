// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Text;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// Represents a 6x6 position-velocity covariance matrix used in CCSDS Navigation Data Messages.
/// </summary>
/// <remarks>
/// <para>
/// As defined in CCSDS 502.0-B-3 (ODM Blue Book).
/// The covariance matrix is stored in lower-triangular form with 21 unique elements.
/// </para>
/// <para>
/// Matrix layout (lower triangular):
/// <code>
///     X       Y       Z       X_DOT   Y_DOT   Z_DOT
/// X   CX_X
/// Y   CY_X    CY_Y
/// Z   CZ_X    CZ_Y    CZ_Z
/// X'  CXD_X   CXD_Y   CXD_Z   CXD_XD
/// Y'  CYD_X   CYD_Y   CYD_Z   CYD_XD  CYD_YD
/// Z'  CZD_X   CZD_Y   CZD_Z   CZD_XD  CZD_YD  CZD_ZD
/// </code>
/// </para>
/// <para>
/// Units:
/// - Position-position covariance: km²
/// - Position-velocity covariance: km²/s
/// - Velocity-velocity covariance: km²/s²
/// </para>
/// </remarks>
public class CovarianceMatrix
{
    /// <summary>
    /// Gets the comments associated with the covariance matrix.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Gets the reference frame for the covariance matrix.
    /// </summary>
    /// <remarks>
    /// Optional. Common values: "RTN" (Radial-Transverse-Normal), "TNW", "ICRF".
    /// If not specified, the covariance is in the same frame as the state.
    /// </remarks>
    public string ReferenceFrame { get; }

    // Position covariance elements (km²)
    /// <summary>X-X position covariance in km².</summary>
    public double CxX { get; }
    /// <summary>Y-X position covariance in km².</summary>
    public double CyX { get; }
    /// <summary>Y-Y position covariance in km².</summary>
    public double CyY { get; }
    /// <summary>Z-X position covariance in km².</summary>
    public double CzX { get; }
    /// <summary>Z-Y position covariance in km².</summary>
    public double CzY { get; }
    /// <summary>Z-Z position covariance in km².</summary>
    public double CzZ { get; }

    // Position-velocity cross-covariance elements (km²/s)
    /// <summary>X_DOT-X position-velocity covariance in km²/s.</summary>
    public double CxDotX { get; }
    /// <summary>X_DOT-Y position-velocity covariance in km²/s.</summary>
    public double CxDotY { get; }
    /// <summary>X_DOT-Z position-velocity covariance in km²/s.</summary>
    public double CxDotZ { get; }
    /// <summary>Y_DOT-X position-velocity covariance in km²/s.</summary>
    public double CyDotX { get; }
    /// <summary>Y_DOT-Y position-velocity covariance in km²/s.</summary>
    public double CyDotY { get; }
    /// <summary>Y_DOT-Z position-velocity covariance in km²/s.</summary>
    public double CyDotZ { get; }
    /// <summary>Z_DOT-X position-velocity covariance in km²/s.</summary>
    public double CzDotX { get; }
    /// <summary>Z_DOT-Y position-velocity covariance in km²/s.</summary>
    public double CzDotY { get; }
    /// <summary>Z_DOT-Z position-velocity covariance in km²/s.</summary>
    public double CzDotZ { get; }

    // Velocity covariance elements (km²/s²)
    /// <summary>X_DOT-X_DOT velocity covariance in km²/s².</summary>
    public double CxDotXDot { get; }
    /// <summary>Y_DOT-X_DOT velocity covariance in km²/s².</summary>
    public double CyDotXDot { get; }
    /// <summary>Y_DOT-Y_DOT velocity covariance in km²/s².</summary>
    public double CyDotYDot { get; }
    /// <summary>Z_DOT-X_DOT velocity covariance in km²/s².</summary>
    public double CzDotXDot { get; }
    /// <summary>Z_DOT-Y_DOT velocity covariance in km²/s².</summary>
    public double CzDotYDot { get; }
    /// <summary>Z_DOT-Z_DOT velocity covariance in km²/s².</summary>
    public double CzDotZDot { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CovarianceMatrix"/> class.
    /// </summary>
    /// <param name="cxX">X-X position covariance in km².</param>
    /// <param name="cyX">Y-X position covariance in km².</param>
    /// <param name="cyY">Y-Y position covariance in km².</param>
    /// <param name="czX">Z-X position covariance in km².</param>
    /// <param name="czY">Z-Y position covariance in km².</param>
    /// <param name="czZ">Z-Z position covariance in km².</param>
    /// <param name="cxDotX">X_DOT-X position-velocity covariance in km²/s.</param>
    /// <param name="cxDotY">X_DOT-Y position-velocity covariance in km²/s.</param>
    /// <param name="cxDotZ">X_DOT-Z position-velocity covariance in km²/s.</param>
    /// <param name="cxDotXDot">X_DOT-X_DOT velocity covariance in km²/s².</param>
    /// <param name="cyDotX">Y_DOT-X position-velocity covariance in km²/s.</param>
    /// <param name="cyDotY">Y_DOT-Y position-velocity covariance in km²/s.</param>
    /// <param name="cyDotZ">Y_DOT-Z position-velocity covariance in km²/s.</param>
    /// <param name="cyDotXDot">Y_DOT-X_DOT velocity covariance in km²/s².</param>
    /// <param name="cyDotYDot">Y_DOT-Y_DOT velocity covariance in km²/s².</param>
    /// <param name="czDotX">Z_DOT-X position-velocity covariance in km²/s.</param>
    /// <param name="czDotY">Z_DOT-Y position-velocity covariance in km²/s.</param>
    /// <param name="czDotZ">Z_DOT-Z position-velocity covariance in km²/s.</param>
    /// <param name="czDotXDot">Z_DOT-X_DOT velocity covariance in km²/s².</param>
    /// <param name="czDotYDot">Z_DOT-Y_DOT velocity covariance in km²/s².</param>
    /// <param name="czDotZDot">Z_DOT-Z_DOT velocity covariance in km²/s².</param>
    /// <param name="referenceFrame">Reference frame for the covariance (optional).</param>
    /// <param name="comments">Comments associated with the covariance (optional).</param>
    public CovarianceMatrix(
        double cxX, double cyX, double cyY,
        double czX, double czY, double czZ,
        double cxDotX, double cxDotY, double cxDotZ, double cxDotXDot,
        double cyDotX, double cyDotY, double cyDotZ, double cyDotXDot, double cyDotYDot,
        double czDotX, double czDotY, double czDotZ, double czDotXDot, double czDotYDot, double czDotZDot,
        string referenceFrame = null,
        IReadOnlyList<string> comments = null)
    {
        // Position covariance
        CxX = cxX;
        CyX = cyX;
        CyY = cyY;
        CzX = czX;
        CzY = czY;
        CzZ = czZ;

        // Position-velocity cross-covariance
        CxDotX = cxDotX;
        CxDotY = cxDotY;
        CxDotZ = cxDotZ;
        CyDotX = cyDotX;
        CyDotY = cyDotY;
        CyDotZ = cyDotZ;
        CzDotX = czDotX;
        CzDotY = czDotY;
        CzDotZ = czDotZ;

        // Velocity covariance
        CxDotXDot = cxDotXDot;
        CyDotXDot = cyDotXDot;
        CyDotYDot = cyDotYDot;
        CzDotXDot = czDotXDot;
        CzDotYDot = czDotYDot;
        CzDotZDot = czDotZDot;

        ReferenceFrame = referenceFrame;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a covariance matrix from a full 6x6 array.
    /// </summary>
    /// <param name="matrix">A 6x6 covariance matrix (must be symmetric).</param>
    /// <param name="referenceFrame">Reference frame for the covariance (optional).</param>
    /// <param name="comments">Comments associated with the covariance (optional).</param>
    /// <returns>A new CovarianceMatrix instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when matrix is null.</exception>
    /// <exception cref="ArgumentException">Thrown when matrix is not 6x6.</exception>
    public static CovarianceMatrix FromFullMatrix(double[,] matrix, string referenceFrame = null, IReadOnlyList<string> comments = null)
    {
        if (matrix == null)
            throw new ArgumentNullException(nameof(matrix));
        if (matrix.GetLength(0) != 6 || matrix.GetLength(1) != 6)
            throw new ArgumentException("Matrix must be 6x6.", nameof(matrix));

        return new CovarianceMatrix(
            cxX: matrix[0, 0],
            cyX: matrix[1, 0], cyY: matrix[1, 1],
            czX: matrix[2, 0], czY: matrix[2, 1], czZ: matrix[2, 2],
            cxDotX: matrix[3, 0], cxDotY: matrix[3, 1], cxDotZ: matrix[3, 2], cxDotXDot: matrix[3, 3],
            cyDotX: matrix[4, 0], cyDotY: matrix[4, 1], cyDotZ: matrix[4, 2], cyDotXDot: matrix[4, 3], cyDotYDot: matrix[4, 4],
            czDotX: matrix[5, 0], czDotY: matrix[5, 1], czDotZ: matrix[5, 2], czDotXDot: matrix[5, 3], czDotYDot: matrix[5, 4], czDotZDot: matrix[5, 5],
            referenceFrame: referenceFrame,
            comments: comments);
    }

    /// <summary>
    /// Converts the covariance to a full symmetric 6x6 matrix.
    /// </summary>
    /// <returns>A 6x6 covariance matrix.</returns>
    public double[,] ToFullMatrix()
    {
        var matrix = new double[6, 6];

        // Row 0: X
        matrix[0, 0] = CxX;

        // Row 1: Y
        matrix[1, 0] = CyX; matrix[1, 1] = CyY;
        matrix[0, 1] = CyX; // Symmetric

        // Row 2: Z
        matrix[2, 0] = CzX; matrix[2, 1] = CzY; matrix[2, 2] = CzZ;
        matrix[0, 2] = CzX; matrix[1, 2] = CzY; // Symmetric

        // Row 3: X_DOT
        matrix[3, 0] = CxDotX; matrix[3, 1] = CxDotY; matrix[3, 2] = CxDotZ; matrix[3, 3] = CxDotXDot;
        matrix[0, 3] = CxDotX; matrix[1, 3] = CxDotY; matrix[2, 3] = CxDotZ; // Symmetric

        // Row 4: Y_DOT
        matrix[4, 0] = CyDotX; matrix[4, 1] = CyDotY; matrix[4, 2] = CyDotZ; matrix[4, 3] = CyDotXDot; matrix[4, 4] = CyDotYDot;
        matrix[0, 4] = CyDotX; matrix[1, 4] = CyDotY; matrix[2, 4] = CyDotZ; matrix[3, 4] = CyDotXDot; // Symmetric

        // Row 5: Z_DOT
        matrix[5, 0] = CzDotX; matrix[5, 1] = CzDotY; matrix[5, 2] = CzDotZ; matrix[5, 3] = CzDotXDot; matrix[5, 4] = CzDotYDot; matrix[5, 5] = CzDotZDot;
        matrix[0, 5] = CzDotX; matrix[1, 5] = CzDotY; matrix[2, 5] = CzDotZ; matrix[3, 5] = CzDotXDot; matrix[4, 5] = CzDotYDot; // Symmetric

        return matrix;
    }

    /// <summary>
    /// Returns the lower-triangular elements as an array in CCSDS order.
    /// </summary>
    /// <returns>Array of 21 elements in lower-triangular order.</returns>
    public double[] ToLowerTriangularArray()
    {
        return new[]
        {
            CxX,
            CyX, CyY,
            CzX, CzY, CzZ,
            CxDotX, CxDotY, CxDotZ, CxDotXDot,
            CyDotX, CyDotY, CyDotZ, CyDotXDot, CyDotYDot,
            CzDotX, CzDotY, CzDotZ, CzDotXDot, CzDotYDot, CzDotZDot
        };
    }

    /// <summary>
    /// Gets the position sigma (1-sigma standard deviation) in kilometers.
    /// </summary>
    public (double SigmaX, double SigmaY, double SigmaZ) PositionSigma =>
        (System.Math.Sqrt(CxX), System.Math.Sqrt(CyY), System.Math.Sqrt(CzZ));

    /// <summary>
    /// Gets the velocity sigma (1-sigma standard deviation) in kilometers per second.
    /// </summary>
    public (double SigmaXDot, double SigmaYDot, double SigmaZDot) VelocitySigma =>
        (System.Math.Sqrt(CxDotXDot), System.Math.Sqrt(CyDotYDot), System.Math.Sqrt(CzDotZDot));

    public override string ToString()
    {
        var pos = PositionSigma;
        var vel = VelocitySigma;
        return $"Covariance[PosσX={pos.SigmaX:E2} km, VelσX={vel.SigmaXDot:E2} km/s, Frame={ReferenceFrame ?? "N/A"}]";
    }
}
