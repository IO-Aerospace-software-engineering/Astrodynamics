using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IO.Astrodynamics.Math;

/// <summary>
/// An immutable matrix of doubles, rows × columns (#668, R26): no member changes a matrix once built. A term is read with
/// the indexer or <see cref="Get"/>, a changed copy is made with <see cref="With"/>, and a matrix is built from an array
/// (copied), a row-major sequence, a function of the indexes (<see cref="Create"/>), a diagonal (<see cref="Diagonal"/>)
/// or as the identity (<see cref="Identity"/>). <c>default(Matrix)</c> is the 0 × 0 matrix for every member. Before
/// 11.0.0 a public <c>Set</c> wrote the terms in place, through every copy of the struct.
/// </summary>
public readonly record struct Matrix
{
    private static readonly double[,] NoTerms = new double[0, 0];

    readonly double[,] _data;

    /// <summary>
    /// The number of rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// The number of columns.
    /// </summary>
    public int Columns { get; }

    // The terms, those of the 0 × 0 matrix for default(Matrix), whose array is null
    private double[,] Data => _data ?? NoTerms;

    /// <summary>
    /// The matrix of <paramref name="rows"/> × <paramref name="columns"/> zeros.
    /// </summary>
    /// <param name="rows">The number of rows, 0 or more.</param>
    /// <param name="columns">The number of columns, 0 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative (#668, R26; an <see cref="OverflowException"/>
    /// before 11.0.0).</exception>
    public Matrix(int rows, int columns)
    {
        RequireDimensions(rows, columns);
        _data = new double[rows, columns];
        Rows = rows;
        Columns = columns;
    }

    /// <summary>
    /// A matrix of the terms of <paramref name="data"/>, copied: a write to the array afterwards does not change the
    /// matrix, its equality or its hash code (#745, decided by S. Guillet, 2026-11-18; before 11.0.0 the matrix wrapped
    /// the caller's array).
    /// </summary>
    /// <param name="data">The terms, row by row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    public Matrix(double[,] data) : this(CloneArray(data), true)
    {
    }

    // The matrix that wraps data, an array no one else holds: the library's own results, built in a fresh array (#745)
    private Matrix(double[,] data, bool _)
    {
        Rows = data.GetLength(0);
        Columns = data.GetLength(1);
        _data = data;
    }

    private static double[,] CloneArray(double[,] data) =>
        (double[,])(data ?? throw new ArgumentNullException(nameof(data))).Clone();

    /// <summary>
    /// The matrix that wraps <paramref name="data"/> without copying it, for an array that the caller built and holds
    /// nowhere else, so that the library's results cost no copy (#745). Never for an array another holder keeps.
    /// </summary>
    internal static Matrix Wrap(double[,] data) => new(data, true);

    /// <summary>
    /// The matrix of <paramref name="rows"/> × <paramref name="columns"/> terms read row by row from
    /// <paramref name="data"/>, copied.
    /// </summary>
    /// <param name="rows">The number of rows, 0 or more.</param>
    /// <param name="columns">The number of columns, 0 or more.</param>
    /// <param name="data">The terms, row-major: term (i, j) is <c>data[i * columns + j]</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> does not hold rows × columns terms.</exception>
    public Matrix(int rows, int columns, double[] data)
    {
        RequireDimensions(rows, columns);
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != rows * columns)
        {
            throw new ArgumentException($"data parameter must have a size of {rows * columns}", nameof(data));
        }

        Rows = rows;
        Columns = columns;
        _data = new double[rows, columns];
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                _data[i, j] = data[(i * Columns) + j];
            }
        }
    }

    /// <summary>
    /// The matrix of <paramref name="rows"/> × <paramref name="columns"/> terms <paramref name="term"/>(i, j), called
    /// once per term, row by row (#668, R26).
    /// </summary>
    /// <param name="rows">The number of rows, 0 or more.</param>
    /// <param name="columns">The number of columns, 0 or more.</param>
    /// <param name="term">The term of row i and column j.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="term"/> is null.</exception>
    public static Matrix Create(int rows, int columns, Func<int, int, double> term)
    {
        RequireDimensions(rows, columns);
        ArgumentNullException.ThrowIfNull(term);
        var data = new double[rows, columns];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                data[i, j] = term(i, j);
            }
        }

        return Wrap(data);
    }

    /// <summary>
    /// The square matrix of the terms of <paramref name="diagonal"/> on its diagonal and zeros elsewhere, n × n for n
    /// terms (#668, R26).
    /// </summary>
    /// <param name="diagonal">The diagonal, from (0, 0).</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagonal"/> is null.</exception>
    public static Matrix Diagonal(params double[] diagonal)
    {
        ArgumentNullException.ThrowIfNull(diagonal);
        int n = diagonal.Length;
        var data = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            data[i, i] = diagonal[i];
        }

        return Wrap(data);
    }

    /// <summary>
    /// The identity matrix of dimension <paramref name="dimension"/> (#668, R26).
    /// </summary>
    /// <param name="dimension">The number of rows and of columns, 0 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative.</exception>
    public static Matrix Identity(int dimension)
    {
        RequireDimensions(dimension, dimension);
        var data = new double[dimension, dimension];
        for (int i = 0; i < dimension; i++)
        {
            data[i, i] = 1.0;
        }

        return Wrap(data);
    }

    private static void RequireDimensions(int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
    }

    /// <summary>
    /// The term of row <paramref name="row"/> and column <paramref name="column"/>, both from 0 (#668, R26).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the matrix (an
    /// <see cref="IndexOutOfRangeException"/> before 11.0.0, a <see cref="NullReferenceException"/> on
    /// <c>default(Matrix)</c>).</exception>
    public double this[int row, int column] => Get(row, column);

    /// <summary>
    /// The term of row <paramref name="row"/> and column <paramref name="column"/>, both from 0: the indexer's.
    /// </summary>
    /// <param name="row">The row, from 0.</param>
    /// <param name="column">The column, from 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the matrix (an
    /// <see cref="IndexOutOfRangeException"/> before 11.0.0, a <see cref="NullReferenceException"/> on
    /// <c>default(Matrix)</c>).</exception>
    public double Get(int row, int column)
    {
        RequireIndexes(row, column);
        return _data[row, column];
    }

    /// <summary>
    /// A copy of this matrix whose term (<paramref name="row"/>, <paramref name="column"/>) is <paramref name="value"/>,
    /// this matrix unchanged (#668, R26).
    /// </summary>
    /// <param name="row">The row, from 0.</param>
    /// <param name="column">The column, from 0.</param>
    /// <param name="value">The new term.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the matrix.</exception>
    public Matrix With(int row, int column, double value)
    {
        RequireIndexes(row, column);
        var data = (double[,])_data.Clone();
        data[row, column] = value;
        return Wrap(data);
    }

    private void RequireIndexes(int row, int column)
    {
        if ((uint)row >= (uint)Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"The row must be in [0, {Rows}).");
        }

        if ((uint)column >= (uint)Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, $"The column must be in [0, {Columns}).");
        }
    }

    /// <summary>
    /// Writes a term of a matrix the library is building, before any caller holds it: the only write of the type, never
    /// on a matrix given or returned (#668, R26: it replaces the public <c>Set</c> of the library's own sites).
    /// </summary>
    internal void SetTerm(int row, int column, double value)
    {
        _data[row, column] = value;
    }

    // Σ_j v[j]·m[row, j] from 0 in column order, the arithmetic of Multiply(double[]) for a 3-column matrix
    internal double RowTimes(int row, in Vector3 v)
    {
        double sum = 0.0;
        sum += v.X * _data[row, 0];
        sum += v.Y * _data[row, 1];
        sum += v.Z * _data[row, 2];
        return sum;
    }

    /// <summary>
    /// Multiply two matrices
    /// </summary>
    /// <param name="matrix"></param>
    /// <returns></returns>
    public Matrix Multiply(Matrix matrix)
    {
        if (Columns != matrix.Rows)
        {
            throw new ArgumentException("Matrixes with incompatible size");
        }

        var res = new Matrix(Rows, matrix.Columns);
        var lhs = Data;
        var rhs = matrix.Data;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < matrix.Columns; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < Columns; k++)
                {
                    sum += lhs[i, k] * rhs[k, j];
                }

                res._data[i, j] = sum;
            }
        }

        return res;
    }

    /// <summary>
    /// Multiply a vector by this matrix
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public double[] Multiply(double[] vector)
    {
        if (Columns != vector.Length)
        {
            throw new ArgumentException("Matrixes with incompatible size");
        }

        double[] res = new double[vector.Length];
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                res[i] += vector[j] * Data[i, j];
            }
        }

        return res;
    }

    public Matrix Inverse()
    {
        // assumes determinant is not 0
        // that is, the matrix does have an inverse
        int n = Rows;
        double[,] result = new double[Rows, Columns];

        int[] perm; // out parameter
        Decompose(out var lum, out perm); // ignore return

        double[] b = new double[n];
        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
                if (i == perm[j])
                    b[j] = 1.0;
                else
                    b[j] = 0.0;

            double[] x = Reduce(lum, b); // 
            for (int j = 0; j < n; ++j)
                result[j, i] = x[j];
        }

        return Wrap(result);
    }


    public int Decompose(out double[,] lum, out int[] perm)
    {
        // Crout's LU decomposition for matrix determinant and inverse
        // stores combined lower & upper in lum[][]
        // stores row permuations into perm[]
        // returns +1 or -1 according to even or odd number of row permutations
        // lower gets dummy 1.0s on diagonal (0.0s above)
        // upper gets lum values on diagonal (0.0s below)

        int toggle = +1; // even (+1) or odd (-1) row permutatuions
        int n = Rows;

        // make a copy of m[][] into result lu[][]
        lum = (double[,])Data.Clone();

        // make perm[]
        perm = new int[n];
        for (int i = 0; i < n; ++i)
            perm[i] = i;

        for (int j = 0; j < n - 1; ++j) // process by column. note n-1 
        {
            double max = System.Math.Abs(lum[j, j]);
            int piv = j;

            for (int i = j + 1; i < n; ++i) // find pivot index
            {
                double xij = System.Math.Abs(lum[i, j]);
                if (!(xij > max)) continue;
                max = xij;
                piv = i;
            } // i

            if (piv != j)
            {
                double[] tmp = GetRow(lum, piv); // swap rows j, piv

                for (int idx = 0; idx < Columns; idx++)
                {
                    lum[piv, idx] = lum[j, idx];
                }

                for (int idx = 0; idx < Columns; idx++)
                {
                    lum[j, idx] = tmp[idx];
                }

                (perm[piv], perm[j]) = (perm[j], perm[piv]);

                toggle = -toggle;
            }

            double xjj = lum[j, j];
            if (xjj == 0.0) continue;

            for (int i = j + 1; i < n; ++i)
            {
                double xij = lum[i, j] / xjj;
                lum[i, j] = xij;
                for (int k = j + 1; k < n; ++k)
                    lum[i, k] -= xij * lum[j, k];
            }
        } // j

        return toggle; // for determinant
    }

    double[] Reduce(double[,] luMatrix, double[] b)
    {
        int n = luMatrix.GetLength(0);
        double[] x = new double[n];
        //b.CopyTo(x, 0);
        for (int i = 0; i < n; ++i)
            x[i] = b[i];

        for (int i = 1; i < n; ++i)
        {
            double sum = x[i];
            for (int j = 0; j < i; ++j)
                sum -= luMatrix[i, j] * x[j];
            x[i] = sum;
        }

        x[n - 1] /= luMatrix[n - 1, n - 1];
        for (int i = n - 2; i >= 0; --i)
        {
            double sum = x[i];
            for (int j = i + 1; j < n; ++j)
                sum -= luMatrix[i, j] * x[j];
            x[i] = sum / luMatrix[i, i];
        }

        return x;
    } // Reduce

    private double[] GetRow(double[,] data, int rowNumber)
    {
        return Enumerable.Range(0, data.GetLength(1))
            .Select(x => data[rowNumber, x])
            .ToArray();
    }

    public static Matrix CreateRotationMatrixX(double angle)
    {
        Matrix mtx = new Matrix(3, 3);
        mtx.SetTerm(0, 0, 1.0);
        mtx.SetTerm(1, 1, System.Math.Cos(angle));
        mtx.SetTerm(1, 2, -System.Math.Sin(angle));
        mtx.SetTerm(2, 1, System.Math.Sin(angle));
        mtx.SetTerm(2, 2, System.Math.Cos(angle));

        return mtx;
    }

    public static Matrix CreateRotationMatrixY(double angle)
    {
        Matrix mtx = new Matrix(3, 3);
        mtx.SetTerm(0, 0, System.Math.Cos(angle));
        mtx.SetTerm(0, 2, System.Math.Sin(angle));
        mtx.SetTerm(1, 1, 1.0);
        mtx.SetTerm(2, 0, -System.Math.Sin(angle));
        mtx.SetTerm(2, 2, System.Math.Cos(angle));
        return mtx;
    }

    public static Matrix CreateRotationMatrixZ(double angle)
    {
        Matrix mtx = new Matrix(3, 3);
        mtx.SetTerm(0, 0, System.Math.Cos(angle));
        mtx.SetTerm(0, 1, -System.Math.Sin(angle));
        mtx.SetTerm(1, 0, System.Math.Sin(angle));
        mtx.SetTerm(1, 1, System.Math.Cos(angle));
        mtx.SetTerm(2, 2, 1.0);
        return mtx;
    }


    public Quaternion ToQuaternion()
    {
        double q0, q1, q2, q3;
        double trace = Data[0, 0] + Data[1, 1] + Data[2, 2];  // Sum of diagonal elements
        double s;

        if (trace > 0)
        {
            s = System.Math.Sqrt(trace + 1.0) * 2; // 4 * q0
            q0 = 0.25 * s;
            q1 = (Data[2, 1] - Data[1, 2]) / s;
            q2 = (Data[0, 2] - Data[2, 0]) / s;
            q3 = (Data[1, 0] - Data[0, 1]) / s;
        }
        else if ((Data[0, 0] > Data[1, 1]) && (Data[0, 0] > Data[2, 2]))
        {
            s = System.Math.Sqrt(1.0 + Data[0, 0] - Data[1, 1] - Data[2, 2]) * 2; // 4 * q1
            q0 = (Data[2, 1] - Data[1, 2]) / s;
            q1 = 0.25 * s;
            q2 = (Data[0, 1] + Data[1, 0]) / s;
            q3 = (Data[0, 2] + Data[2, 0]) / s;
        }
        else if (Data[1, 1] > Data[2, 2])
        {
            s = System.Math.Sqrt(1.0 + Data[1, 1] - Data[0, 0] - Data[2, 2]) * 2; // 4 * q2
            q0 = (Data[0, 2] - Data[2, 0]) / s;
            q1 = (Data[0, 1] + Data[1, 0]) / s;
            q2 = 0.25 * s;
            q3 = (Data[1, 2] + Data[2, 1]) / s;
        }
        else
        {
            s = System.Math.Sqrt(1.0 + Data[2, 2] - Data[0, 0] - Data[1, 1]) * 2; // 4 * q3
            q0 = (Data[1, 0] - Data[0, 1]) / s;
            q1 = (Data[0, 2] + Data[2, 0]) / s;
            q2 = (Data[1, 2] + Data[2, 1]) / s;
            q3 = 0.25 * s;
        }

        return new Quaternion(q0, new Vector3(q1, q2, q3)).NormalizeOrZero();
    }

    /// <summary>
    /// Whether the two matrices have the same dimensions and, element by element, values equal by
    /// <see cref="double.Equals(double)"/>: NaN equals NaN only, +0 equals -0.
    /// </summary>
    public bool Equals(Matrix other)
    {
        if (Rows != other.Rows)
        {
            return false;
        }

        if (Columns != other.Columns)
        {
            return false;
        }

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                // double.Equals, the semantics of Vector3 and of the record structs: NaN equals NaN only, +0 equals -0 (#680:
                // |a - b| > Double.Epsilon is false for NaN and for two entries one smallest subnormal apart, which compared
                // equal, so equality was not transitive and equal matrices hashed differently)
                if (!Data[i, j].Equals(other.Data[i, j]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        const double prime = 31.0;
        double datahash = 1.0;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                datahash = datahash * prime + Data[i,j];
            }
        }
        return HashCode.Combine(datahash, Rows, Columns);
    }

    public static Matrix operator *(Matrix lhs, Matrix rhs)
    {
        return lhs.Multiply(rhs);
    }

    public static double[] operator *(Matrix lhs, double[] rhs)
    {
        return lhs.Multiply(rhs);
    }

    public static double[] operator *(double[] lhs, Matrix rhs)
    {
        return rhs.Multiply(lhs);
    }

    /// <summary>
    /// Returns the transpose of this matrix.
    /// </summary>
    /// <returns>A new matrix that is the transpose of this matrix.</returns>
    public Matrix Transpose()
    {
        var result = new Matrix(Columns, Rows);
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                result.SetTerm(j, i, Data[i, j]);
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a 3x3 rotation matrix from a quaternion: the matrix of the active rotation q p q* of its unit quaternion.
    /// </summary>
    /// <param name="q">The quaternion representing the rotation, of any finite non-zero magnitude.</param>
    /// <returns>A 3x3 rotation matrix.</returns>
    /// <exception cref="ArgumentException"><paramref name="q"/> is the zero quaternion or has a component that is not
    /// finite: it is no rotation (#680's known limitation; the identity was returned for the zero quaternion before
    /// 11.0.0). Every finite non-zero quaternion is a rotation, its magnitude scaled as <see cref="Quaternion.Normalize"/>
    /// does (third round of #668: outside about 1e-154 to 1e154 it threw before 11.0.0).</exception>
    public static Matrix FromQuaternion(Quaternion q)
    {
        return RotationMatrix(q, nameof(q));
    }

    // The rotation matrix of a quaternion, which must be normalizable, the parameter of the caller named in the exception
    private static Matrix RotationMatrix(in Quaternion q, string parameterName)
    {
        var qn = Quaternion.RequireUnit(q, parameterName);
        double w = qn.W;
        double x = qn.VectorPart.X;
        double y = qn.VectorPart.Y;
        double z = qn.VectorPart.Z;

        var m = new Matrix(3, 3);

        // Row 0
        m.SetTerm(0, 0, 1 - 2 * (y * y + z * z));
        m.SetTerm(0, 1, 2 * (x * y - w * z));
        m.SetTerm(0, 2, 2 * (x * z + w * y));

        // Row 1
        m.SetTerm(1, 0, 2 * (x * y + w * z));
        m.SetTerm(1, 1, 1 - 2 * (x * x + z * z));
        m.SetTerm(1, 2, 2 * (y * z - w * x));

        // Row 2
        m.SetTerm(2, 0, 2 * (x * z - w * y));
        m.SetTerm(2, 1, 2 * (y * z + w * x));
        m.SetTerm(2, 2, 1 - 2 * (x * x + y * y));

        return m;
    }

    /// <summary>
    /// Creates a 6x6 block diagonal matrix from two 3x3 matrices.
    /// </summary>
    /// <param name="upperLeft">The upper-left 3x3 block.</param>
    /// <param name="lowerRight">The lower-right 3x3 block.</param>
    /// <returns>A 6x6 block diagonal matrix.</returns>
    public static Matrix CreateBlockDiagonal(Matrix upperLeft, Matrix lowerRight)
    {
        if (upperLeft.Rows != 3 || upperLeft.Columns != 3 ||
            lowerRight.Rows != 3 || lowerRight.Columns != 3)
        {
            throw new ArgumentException("Both matrices must be 3x3.");
        }

        var result = new Matrix(6, 6);

        // Copy upper-left block
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result.SetTerm(i, j, upperLeft.Get(i, j));
            }
        }

        // Copy lower-right block
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result.SetTerm(i + 3, j + 3, lowerRight.Get(i, j));
            }
        }

        // Off-diagonal blocks are already 0

        return result;
    }

    /// <summary>
    /// A copy of this matrix that shares no array with it: the public types that hold a matrix copy it in and out with it
    /// (#722, when a public <c>Set</c> wrote the array every copy of the struct shares; kept since 11.0.0 so that no write
    /// of <see cref="SetTerm"/> on a matrix the library is building reaches a holder). A matrix without terms
    /// (<c>default(Matrix)</c> included, whose array is null) is returned as it is.
    /// </summary>
    internal Matrix Copy() => _data is null || Rows == 0 || Columns == 0 ? this : Wrap((double[,])Data.Clone());

    /// <summary>
    /// The copy of a nullable matrix (<see cref="Copy()"/>), null for null (#722).
    /// </summary>
    internal static Matrix? Copy(Matrix? matrix) => matrix?.Copy();

    /// <summary>
    /// Converts the matrix to a 2D double array.
    /// </summary>
    /// <returns>A 2D array containing the matrix data.</returns>
    public double[,] ToArray()
    {
        return (double[,])Data.Clone();
    }

    /// <summary>
    /// Rotates a 6x6 position-velocity covariance matrix: P' = T · P · T^T, with T = diag(R, R).
    /// Valid only between frames that do not rotate relative to each other, such as two inertial frames.
    /// </summary>
    /// <param name="covariance">The 6x6 covariance matrix to transform (m², m²/s, m²/s²).</param>
    /// <param name="rotation">The quaternion representing the rotation from current frame to target frame.</param>
    /// <returns>The transformed 6x6 covariance matrix.</returns>
    /// <remarks>
    /// <para>
    /// T is the 6×6 block-diagonal matrix with the 3×3 rotation matrix R in both diagonal blocks. It is the
    /// Jacobian of the state transformation only when the target frame has no angular velocity relative to the
    /// source frame.
    /// </para>
    /// <para>
    /// Do not use it towards a rotating frame (ITRF93, TIRS, a body-fixed frame): there the velocity transforms as
    /// v' = R v - ω × r', so the velocity covariance also depends on the position covariance through ω.
    /// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToFrame"/> applies the full Jacobian and is the method to
    /// use for any frame change of a state carrying a covariance.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when covariance is not 6x6, or when <paramref name="rotation"/> is the zero
    /// quaternion or has a component that is not finite (#680's known limitation; the covariance was returned unrotated for
    /// the zero quaternion before 11.0.0). Every finite non-zero quaternion is a rotation (third round of #668: outside
    /// about 1e-154 to 1e154 it threw before 11.0.0).</exception>
    public static Matrix TransformCovariance(Matrix covariance, Quaternion rotation)
    {
        if (covariance.Rows != 6 || covariance.Columns != 6)
        {
            throw new ArgumentException("Covariance matrix must be 6x6.", nameof(covariance));
        }

        // Get the 3×3 rotation matrix from the quaternion
        var R = RotationMatrix(rotation, nameof(rotation));

        // Build the 6×6 block-diagonal transformation matrix T = [R 0; 0 R]
        var T = CreateBlockDiagonal(R, R);

        // Compute P' = T * P * T^T
        return T.Multiply(covariance).Multiply(T.Transpose());
    }

    /// <summary>
    /// Builds the 6x6 Jacobian of the state transformation r' = R r, v' = R v - ω × r' applied by
    /// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToFrame"/>.
    /// </summary>
    /// <param name="rotation">Rotation from the source frame to the target frame: r' = r.Rotate(rotation).</param>
    /// <param name="angularVelocity">The angular velocity ω of the target frame relative to the source frame (rad/s),
    /// expressed in the target frame, as used with r' in v' = R v - ω × r'.</param>
    /// <returns>J = [[R, 0], [-[ω×] R, R]], where [ω×] is the cross-product matrix of ω.</returns>
    /// <remarks>
    /// The transformation is linear in (r, v) at a fixed epoch, so J is its exact derivative:
    /// ∂r'/∂r = R, ∂r'/∂v = 0, ∂v'/∂r = -[ω×] R, ∂v'/∂v = R. The lower-left block is the term that a block-diagonal
    /// rotation omits towards a rotating frame.
    /// </remarks>
    internal static Matrix CreateStateTransformationJacobian(Quaternion rotation, Vector3 angularVelocity)
    {
        var r = RotationMatrix(rotation, nameof(rotation));
        var jacobian = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                jacobian._data[i, j] = r._data[i, j];
                jacobian._data[i + 3, j + 3] = r._data[i, j];
            }
        }

        // Lower-left block -[ω×] R, with [ω×] = [[0, -ωz, ωy], [ωz, 0, -ωx], [-ωy, ωx, 0]].
        double wx = angularVelocity.X;
        double wy = angularVelocity.Y;
        double wz = angularVelocity.Z;
        for (int j = 0; j < 3; j++)
        {
            double r0 = r._data[0, j];
            double r1 = r._data[1, j];
            double r2 = r._data[2, j];
            jacobian._data[3, j] = wz * r1 - wy * r2;
            jacobian._data[4, j] = wx * r2 - wz * r0;
            jacobian._data[5, j] = wy * r0 - wx * r1;
        }

        return jacobian;
    }

    /// <summary>
    /// Transforms a covariance matrix through a linear map of Jacobian J: P' = J · P · J^T, symmetrized.
    /// </summary>
    /// <param name="covariance">The square covariance matrix to transform.</param>
    /// <param name="jacobian">The Jacobian of the map; its column count must equal the covariance dimension.</param>
    /// <returns>The transformed covariance, (P' + P'^T) / 2, exactly symmetric.</returns>
    /// <exception cref="ArgumentException">Thrown when the dimensions do not match.</exception>
    internal static Matrix TransformCovarianceWithJacobian(Matrix covariance, Matrix jacobian)
    {
        if (covariance.Rows != covariance.Columns)
        {
            throw new ArgumentException("Covariance matrix must be square.", nameof(covariance));
        }

        if (jacobian.Columns != covariance.Rows)
        {
            throw new ArgumentException("The Jacobian must have as many columns as the covariance has rows.", nameof(jacobian));
        }

        return Symmetrize(jacobian.Multiply(covariance).Multiply(jacobian.Transpose()));
    }

    /// <summary>
    /// Returns (M + M^T) / 2 for a square matrix M.
    /// </summary>
    /// <param name="matrix">The square matrix to symmetrize.</param>
    /// <returns>A new, exactly symmetric matrix.</returns>
    /// <exception cref="ArgumentException">Thrown when the matrix is not square.</exception>
    internal static Matrix Symmetrize(Matrix matrix)
    {
        if (matrix.Rows != matrix.Columns)
        {
            throw new ArgumentException("Only a square matrix can be symmetrized.", nameof(matrix));
        }

        var result = new Matrix(matrix.Rows, matrix.Columns);
        for (int i = 0; i < matrix.Rows; i++)
        {
            result._data[i, i] = matrix._data[i, i];
            for (int j = i + 1; j < matrix.Columns; j++)
            {
                double value = 0.5 * (matrix._data[i, j] + matrix._data[j, i]);
                result._data[i, j] = value;
                result._data[j, i] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a 6x6 identity matrix suitable for covariance initialization.
    /// </summary>
    /// <returns>A 6x6 identity matrix.</returns>
    public static Matrix Identity6x6() => Identity(6);

    /// <summary>
    /// Checks if this matrix is symmetric within the specified tolerance.
    /// </summary>
    /// <param name="tolerance">The tolerance for floating-point comparison.</param>
    /// <returns>True if the matrix is symmetric, false otherwise.</returns>
    public bool IsSymmetric(double tolerance = 1e-10)
    {
        if (Rows != Columns) return false;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = i + 1; j < Columns; j++)
            {
                if (System.Math.Abs(Data[i, j] - Data[j, i]) > tolerance)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Computes the Cholesky decomposition of this symmetric positive-definite matrix.
    /// Returns a lower-triangular matrix L such that L * L^T = this matrix.
    /// </summary>
    /// <param name="tolerance">Tolerance for symmetry check.</param>
    /// <returns>The lower-triangular Cholesky factor L.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the matrix is not square, not symmetric, or not positive-definite.
    /// </exception>
    public Matrix Cholesky(double tolerance = 1e-10)
    {
        if (Rows != Columns)
            throw new InvalidOperationException("Cholesky decomposition requires a square matrix.");

        if (!IsSymmetric(tolerance))
            throw new InvalidOperationException("Cholesky decomposition requires a symmetric matrix.");

        int n = Rows;
        var L = new Matrix(n, n);

        for (int j = 0; j < n; j++)
        {
            double sum = 0.0;
            for (int k = 0; k < j; k++)
                sum += L.Get(j, k) * L.Get(j, k);

            double diag = Data[j, j] - sum;
            if (diag <= 0.0)
                throw new InvalidOperationException("Matrix is not positive-definite.");

            L.SetTerm(j, j, System.Math.Sqrt(diag));

            for (int i = j + 1; i < n; i++)
            {
                double innerSum = 0.0;
                for (int k = 0; k < j; k++)
                    innerSum += L.Get(i, k) * L.Get(j, k);

                L.SetTerm(i, j, (Data[i, j] - innerSum) / L.Get(j, j));
            }
        }

        return L;
    }

    /// <summary>
    /// Adds two matrices element-wise.
    /// </summary>
    /// <param name="other">The matrix to add.</param>
    /// <returns>A new matrix that is the element-wise sum of this and <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public Matrix Add(Matrix other)
    {
        if (Rows != other.Rows || Columns != other.Columns)
        {
            throw new ArgumentException("Matrices must have the same dimensions for addition.");
        }

        var result = new Matrix(Rows, Columns);
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                result.SetTerm(i, j, Data[i, j] + other.Get(i, j));
            }
        }

        return result;
    }

    /// <summary>
    /// Adds two matrices element-wise.
    /// </summary>
    public static Matrix operator +(Matrix lhs, Matrix rhs)
    {
        return lhs.Add(rhs);
    }

    /// <summary>
    /// Extracts a sub-block from this matrix.
    /// </summary>
    /// <param name="startRow">Starting row index (inclusive).</param>
    /// <param name="startColumn">Starting column index (inclusive).</param>
    /// <param name="blockRows">Number of rows to extract.</param>
    /// <param name="blockColumns">Number of columns to extract.</param>
    /// <returns>A new matrix containing the extracted sub-block.</returns>
    public Matrix SubMatrix(int startRow, int startColumn, int blockRows, int blockColumns)
    {
        if (startRow + blockRows > Rows || startColumn + blockColumns > Columns)
        {
            throw new ArgumentOutOfRangeException("Sub-matrix exceeds matrix dimensions.");
        }

        var result = new Matrix(blockRows, blockColumns);
        for (int i = 0; i < blockRows; i++)
        {
            for (int j = 0; j < blockColumns; j++)
            {
                result.SetTerm(i, j, Data[startRow + i, startColumn + j]);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a 3x3 matrix from three row vectors.
    /// </summary>
    /// <param name="row0">The first row vector.</param>
    /// <param name="row1">The second row vector.</param>
    /// <param name="row2">The third row vector.</param>
    /// <returns>A 3x3 matrix with the specified row vectors.</returns>
    public static Matrix FromRowVectors(Vector3 row0, Vector3 row1, Vector3 row2)
    {
        var m = new Matrix(3, 3);
        m.SetTerm(0, 0, row0.X); m.SetTerm(0, 1, row0.Y); m.SetTerm(0, 2, row0.Z);
        m.SetTerm(1, 0, row1.X); m.SetTerm(1, 1, row1.Y); m.SetTerm(1, 2, row1.Z);
        m.SetTerm(2, 0, row2.X); m.SetTerm(2, 1, row2.Y); m.SetTerm(2, 2, row2.Z);
        return m;
    }

    public static Matrix FromColumnVectors(Vector3 col0, Vector3 col1, Vector3 col2)
    {
        var m = new Matrix(3, 3);
        m.SetTerm(0, 0, col0.X);
        m.SetTerm(1, 0, col0.Y);
        m.SetTerm(2, 0, col0.Z);
        m.SetTerm(0, 1, col1.X);
        m.SetTerm(1, 1, col1.Y);
        m.SetTerm(2, 1, col1.Z);
        m.SetTerm(0, 2, col2.X);
        m.SetTerm(1, 2, col2.Y);
        m.SetTerm(2, 2, col2.Z);
        return m;
    }

    /// <summary>
    /// The rows of the matrix, <c>[[a, b], [c, d]]</c>, each term in invariant culture and round trip ("R"), <c>[]</c>
    /// for a matrix without rows (#668, R26: the record's <c>Matrix { Rows = 2, Columns = 2 }</c> before 11.0.0).
    /// </summary>
    public override string ToString()
    {
        var data = Data;
        var text = new StringBuilder("[");
        for (int i = 0; i < Rows; i++)
        {
            text.Append(i == 0 ? "[" : ", [");
            for (int j = 0; j < Columns; j++)
            {
                if (j > 0)
                {
                    text.Append(", ");
                }

                text.Append(data[i, j].ToString("R", CultureInfo.InvariantCulture));
            }

            text.Append(']');
        }

        return text.Append(']').ToString();
    }
}
