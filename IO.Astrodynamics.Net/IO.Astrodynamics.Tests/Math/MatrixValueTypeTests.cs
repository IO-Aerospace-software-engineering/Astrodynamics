// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

/// <summary>
/// The immutable <see cref="Matrix"/> and the operators of <see cref="Vector3"/> (#668, R26): the indexer, <c>With</c>,
/// <c>Create</c>, <c>Diagonal</c>, <c>Identity</c>, the index and dimension checks, <c>default(Matrix)</c> as the 0 × 0
/// matrix, <c>ToString</c>, and the vector's <c>Dot</c>, unary minus, scalar product on the left and product by a 3 × 3
/// matrix. Expected values are closed forms, or the existing members the new ones must equal bit for bit.
/// </summary>
public class MatrixValueTypeTests
{
    private static readonly double[,] Terms = { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } };

    [Fact]
    public void Indexer_ReadsTheTermOfGet()
    {
        var m = new Matrix(Terms);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(Terms[i, j], m[i, j]);
                Assert.Equal(Terms[i, j], m.Get(i, j));
            }
        }
    }

    [Theory]
    [InlineData(-1, 0, "row")]
    [InlineData(2, 0, "row")]
    [InlineData(0, -1, "column")]
    [InlineData(0, 3, "column")]
    [InlineData(int.MinValue, 0, "row")]
    public void IndexOutside_ThrowsArgumentOutOfRange(int row, int column, string parameter)
    {
        var m = new Matrix(Terms);
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => m[row, column]).ParamName);
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => m.Get(row, column)).ParamName);
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => m.With(row, column, 0.0)).ParamName);
    }

    [Fact]
    public void With_ReturnsACopyWithOneTermChanged()
    {
        var m = new Matrix(Terms);
        var changed = m.With(1, 2, -7.5);

        Assert.Equal(-7.5, changed[1, 2]);
        Assert.Equal(6.0, m[1, 2]);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (i != 1 || j != 2)
                {
                    Assert.Equal(Terms[i, j], changed[i, j]);
                }
            }
        }

        Assert.Equal(new Matrix(Terms), m);
        Assert.NotEqual(m, changed);
    }

    [Fact]
    public void Create_CallsTheFunctionOncePerTermRowByRow()
    {
        var calls = new List<(int, int)>();
        var m = Matrix.Create(2, 3, (i, j) =>
        {
            calls.Add((i, j));
            return 10.0 * i + j;
        });

        Assert.Equal(new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2) }, calls);
        Assert.Equal(2, m.Rows);
        Assert.Equal(3, m.Columns);
        Assert.Equal(new Matrix(new[,] { { 0.0, 1.0, 2.0 }, { 10.0, 11.0, 12.0 } }), m);
        Assert.Throws<ArgumentNullException>(() => Matrix.Create(1, 1, null));
    }

    [Fact]
    public void DiagonalAndIdentity_AreTheClosedForms()
    {
        var d = Matrix.Diagonal(2.0, -3.0, 0.5);
        Assert.Equal(new Matrix(new[,] { { 2.0, 0.0, 0.0 }, { 0.0, -3.0, 0.0 }, { 0.0, 0.0, 0.5 } }), d);
        Assert.Equal(new Matrix(0, 0), Matrix.Diagonal());
        Assert.Throws<ArgumentNullException>(() => Matrix.Diagonal(null));

        var i4 = Matrix.Identity(4);
        Assert.Equal(4, i4.Rows);
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Assert.Equal(i == j ? 1.0 : 0.0, i4[i, j]);
            }
        }

        Assert.Equal(Matrix.Identity6x6(), Matrix.Identity(6));
        Assert.Equal(new Matrix(0, 0), Matrix.Identity(0));
    }

    [Fact]
    public void NegativeDimension_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(-1, 1, Array.Empty<double>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix.Create(-1, 1, (_, _) => 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix.Identity(-1));
    }

    /// <summary>
    /// <c>default(Matrix)</c>, whose array is null, behaves as <c>new Matrix(0, 0)</c> for every member (a
    /// <see cref="NullReferenceException"/> before 11.0.0 for Get, ToArray, Inverse and Decompose).
    /// </summary>
    [Fact]
    public void Default_IsTheZeroByZeroMatrixForEveryMember()
    {
        var d = default(Matrix);
        var empty = new Matrix(0, 0);

        Assert.Equal(0, d.Rows);
        Assert.Equal(0, d.Columns);
        Assert.Equal(empty, d);
        Assert.Equal(empty.GetHashCode(), d.GetHashCode());
        Assert.Equal("[]", d.ToString());
        Assert.Equal(empty.ToString(), d.ToString());
        Assert.Equal(new double[0, 0], d.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => d[0, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Get(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => d.With(0, 0, 1.0));
        Assert.Equal(empty, d.Multiply(d));
        Assert.Equal(empty, d * empty);
        Assert.Empty(d.Multiply(Array.Empty<double>()));
        Assert.Equal(empty, d.Transpose());
        Assert.Equal(empty, d.Inverse());
        Assert.Equal(1, d.Decompose(out var lum, out var perm));
        Assert.Equal(new double[0, 0], lum);
        Assert.Empty(perm);
        Assert.Equal(empty, d.Add(d));
        Assert.Equal(empty, d + empty);
        Assert.True(d.IsSymmetric());
        Assert.Equal(empty, d.Cholesky());
        Assert.Equal(empty, d.SubMatrix(0, 0, 0, 0));
        Assert.Throws<IndexOutOfRangeException>(() => empty.ToQuaternion());
        Assert.Throws<IndexOutOfRangeException>(() => d.ToQuaternion());
        Assert.Throws<ArgumentException>(() => Matrix.CreateBlockDiagonal(d, Matrix.Identity(3)));
        Assert.Throws<ArgumentException>(() => Matrix.TransformCovariance(d, Quaternion.Identity));
    }

    [Fact]
    public void ToString_PrintsTheRowsInvariantAndRoundTrip()
    {
        var m = new Matrix(new[,] { { 1.0, 0.1 }, { -1.0 / 3.0, 1e-300 } });
        Assert.Equal("[[1, 0.1], [-0.3333333333333333, 1E-300]]", m.ToString());
        Assert.Equal("[[1, 2, 3], [4, 5, 6]]", new Matrix(Terms).ToString());
        Assert.Equal("[[], []]", new Matrix(2, 0).ToString());

        // Round trip: every printed term parses back to the same double, whatever the culture of the thread
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // A culture with a decimal comma and a list separator ';' (InvariantGlobalization holds no named culture)
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            comma.NumberFormat.NegativeSign = "~";
            comma.TextInfo.ListSeparator = ";";
            CultureInfo.CurrentCulture = comma;
            Assert.Equal("0,1", 0.1.ToString(CultureInfo.CurrentCulture));
            Assert.Equal("[[1, 0.1], [-0.3333333333333333, 1E-300]]", m.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal(-1.0 / 3.0, double.Parse("-0.3333333333333333", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Dot_IsTheSumOfTheProductsInOrder()
    {
        var a = new Vector3(1.0, -2.0, 3.0);
        var b = new Vector3(4.0, 5.0, -6.0);
        Assert.Equal(1.0 * 4.0 + -2.0 * 5.0 + 3.0 * -6.0, a.Dot(b));
        Assert.Equal(-24.0, a.Dot(b));

        // The expression x·x' + y·y' + z·z', bit for bit, on terms where the order of the sum matters
        var c = new Vector3(0.1, 1e16, -1e16);
        var e = new Vector3(0.3, 1.0, 1.0);
        Assert.Equal(c.X * e.X + c.Y * e.Y + c.Z * e.Z, c.Dot(e));
        Assert.Equal(0.0, Vector3.VectorX.Dot(Vector3.VectorY));
    }

    [Fact]
    public void UnaryMinus_IsTheOppositeAndInverse()
    {
        var v = new Vector3(1.5, -2.0, 0.0);
        var minus = -v;
        Assert.Equal(-1.5, minus.X);
        Assert.Equal(2.0, minus.Y);
        Assert.Equal(0.0, minus.Z);
        Assert.Equal(v.Inverse(), -v);
    }

    [Fact]
    public void ScalarOnTheLeft_IsTheSameVectorAsOnTheRight()
    {
        var v = new Vector3(0.1, -3.7, 1e300);
        foreach (double k in new[] { 2.0, -0.3, 1.0 / 3.0, 1e10 })
        {
            Assert.Equal(v * k, k * v);
        }

        Assert.Equal(new Vector3(0.2, -7.4, 2e300), 2.0 * v);
    }

    [Fact]
    public void MatrixTimesVector_IsTheRotationAndMultiplyBitForBit()
    {
        // Closed form: Rz(π/2) turns X onto Y (to rounding of cos π/2)
        var turned = Matrix.CreateRotationMatrixZ(IO.Astrodynamics.Constants.HalfPi) * Vector3.VectorX;
        Assert.Equal(0.0, turned.X, 15);
        Assert.Equal(1.0, turned.Y);
        Assert.Equal(0.0, turned.Z);

        // The arithmetic of Multiply(double[]), bit for bit
        var m = new Matrix(new[,] { { 0.1, -0.7, 3.3 }, { 1e-9, 2.0 / 3.0, -5.0 }, { 7.0, 0.25, -1.0 / 7.0 } });
        var v = new Vector3(1.0 / 3.0, -1e8, 0.3);
        double[] expected = m.Multiply(new[] { v.X, v.Y, v.Z });
        var product = m * v;
        Assert.Equal(expected[0], product.X);
        Assert.Equal(expected[1], product.Y);
        Assert.Equal(expected[2], product.Z);
    }

    [Fact]
    public void MatrixTimesVector_NotThreeByThree_Throws()
    {
        var v = new Vector3(1.0, 2.0, 3.0);
        Assert.Equal("m", Assert.Throws<ArgumentException>(() => new Matrix(Terms) * v).ParamName);
        Assert.Equal("m", Assert.Throws<ArgumentException>(() => Matrix.Identity(4) * v).ParamName);
        Assert.Equal("m", Assert.Throws<ArgumentException>(() => default(Matrix) * v).ParamName);
    }
}
