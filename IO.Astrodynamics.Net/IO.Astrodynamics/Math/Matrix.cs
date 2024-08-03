using System;
using System.Linq;

namespace IO.Astrodynamics.Math;

public readonly record struct Matrix
{
    readonly double[,] _data;
    public int Rows { get; }
    public int Columns { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    public Matrix(int rows, int columns)
    {
        _data = new double[rows, columns];
        Rows = rows;
        Columns = columns;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="data"></param>
    public Matrix(double[,] data)
    {
        Rows = data.GetLength(0);
        Columns = data.GetLength(1);
        _data = data;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    /// <param name="data"></param>
    public Matrix(int rows, int columns, double[] data)
    {
        if (data.Length != rows * columns)
        {
            throw new ArgumentException($"data parameter must have a size of {rows * columns}");
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
    /// Get value at index
    /// </summary>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public double Get(int row, int column)
    {
        return _data[row, column];
    }

    /// <summary>
    /// Set value at index
    /// </summary>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <param name="value"></param>
    public void Set(int row, int column, double value)
    {
        _data[row, column] = value;
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

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < matrix.Columns; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < Columns; k++)
                {
                    sum += _data[i, k] * matrix.Get(k, j);
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
                res[i] += vector[j] * _data[i, j];
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

        return new Matrix(result);
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
        lum = (double[,])_data.Clone();

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
        mtx.Set(0, 0, 1.0);
        mtx.Set(1, 1, System.Math.Cos(angle));
        mtx.Set(1, 2, -System.Math.Sin(angle));
        mtx.Set(2, 1, System.Math.Sin(angle));
        mtx.Set(2, 2, System.Math.Cos(angle));

        return mtx;
    }

    public static Matrix CreateRotationMatrixY(double angle)
    {
        Matrix mtx = new Matrix(3, 3);
        mtx.Set(0, 0, System.Math.Cos(angle));
        mtx.Set(0, 2, System.Math.Sin(angle));
        mtx.Set(1, 1, 1.0);
        mtx.Set(2, 0, -System.Math.Sin(angle));
        mtx.Set(2, 2, System.Math.Cos(angle));
        return mtx;
    }

    public static Matrix CreateRotationMatrixZ(double angle)
    {
        Matrix mtx = new Matrix(3, 3);
        mtx.Set(0, 0, System.Math.Cos(angle));
        mtx.Set(0, 1, -System.Math.Sin(angle));
        mtx.Set(1, 0, System.Math.Sin(angle));
        mtx.Set(1, 1, System.Math.Cos(angle));
        mtx.Set(2, 2, 1.0);
        return mtx;
    }

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
                if (System.Math.Abs(_data[i, j] - other.Get(i, j)) > Double.Epsilon)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_data, Rows, Columns);
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
}