using System;
using IO.Astrodynamics.Math;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Math;

public class MatirxTests
{
    private readonly ITestOutputHelper _output;

    public MatirxTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CreateMatrix()
    {
        Matrix m = new Matrix(2, 3);
        Assert.Equal(2, m.Rows);
        Assert.Equal(3, m.Columns);
    }

    [Fact]
    public void CreateMatrixFromData()
    {
        double[,] data = new double[2, 3];
        Matrix m = new Matrix(data);
        Assert.Equal(2, m.Rows);
        Assert.Equal(3, m.Columns);
    }

    [Fact]
    public void CreateMatrixFromArray()
    {
        double[] data = new double[6] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m = new Matrix(2, 3, data);
        Assert.Equal(1, m.Get(0, 0));
        Assert.Equal(2, m.Get(0, 1));
        Assert.Equal(3, m.Get(0, 2));
        Assert.Equal(4, m.Get(1, 0));
        Assert.Equal(5, m.Get(1, 1));
        Assert.Equal(6, m.Get(1, 2));
    }

    [Fact]
    public void Equality()
    {
        double[] data = new double[6] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m = new Matrix(2, 3, data);

        Matrix m2 = new Matrix(2, 3, data);

        double[] data3 = new double[6] { 2.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m3 = new Matrix(2, 3, data3);

        double[] data4 = new double[6] { 2.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m4 = new Matrix(3, 2, data3);
        Assert.Equal(m, m2);
        Assert.NotEqual(m2, m3);
        Assert.True(m == m2);
        Assert.True(m != m3);
        Assert.True(m.Equals(m2));
        Assert.True(m.Equals((object)m2));
        Assert.False(m.Equals(null));
        Assert.False(m.Equals((object)null));
        Assert.NotEqual(m3, m4);
    }


    [Fact]
    public void MultiplyMatrix()
    {
        Matrix m1 = new Matrix(2, 3, new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 });
        Matrix m2 = new Matrix(3, 2, new[] { 7.0, 8.0, 9.0, 10.0, 11.0, 12.0 });
        Matrix res = m1.Multiply(m2);
        Assert.Equal(2, res.Rows);
        Assert.Equal(2, res.Columns);
        Assert.Equal(58, res.Get(0, 0));
        Assert.Equal(64, res.Get(0, 1));
        Assert.Equal(139, res.Get(1, 0));
        Assert.Equal(154, res.Get(1, 1));

        Assert.Equal(res, m1 * m2);
    }

    [Fact]
    public void MultiplyVector()
    {
        //J2000->Ecliptic
        double[] v = new double[36];
        v[0] = 1.0;
        v[7] = 0.9174820620691818;
        v[8] = 0.3977771559319137;
        v[13] = -0.3977771559319137;
        v[14] = 0.9174820620691818;
        v[21] = 1.0;
        v[28] = 0.9174820620691818;
        v[29] = 0.3977771559319137;
        v[34] = -0.3977771559319137;
        v[35] = 0.9174820620691818;
        Matrix m = new Matrix(6, 6, v);

        //Earth from sun at 0 TDB
        double[] earthSv = { -26499033.67742509, 132757417.33833946, 57556718.47053819, -29.79426007, -5.01805231, -2.17539380 };
        double[] res = m.Multiply(earthSv);
        Assert.Equal(-26499033.67742509, res[0]);
        Assert.Equal(144697296.79254317, res[1]);
        Assert.Equal(-611.1494260579348, res[2]);
        Assert.Equal(-29.79426007, res[3]);
        Assert.Equal(-5.469294939745739, res[4]);
        Assert.Equal(0.0001817867528561834, res[5]);
        Assert.Equal(res, m * earthSv);
        Assert.Equal(res, earthSv * m);
    }

    [Fact]
    public void Inverse()
    {
        //J2000->Ecliptic
        double[] v = new double[36];
        v[0] = 1.0;
        v[7] = 0.9174820620691818;
        v[8] = 0.3977771559319137;
        v[13] = -0.3977771559319137;
        v[14] = 0.9174820620691818;
        v[21] = 1.0;
        v[28] = 0.9174820620691818;
        v[29] = 0.3977771559319137;
        v[34] = -0.3977771559319137;
        v[35] = 0.9174820620691818;
        Matrix m = new Matrix(6, 6, v);

        var res = m.Inverse();
        //Eclip=>J2000
        Assert.Equal(1.0, res.Get(0, 0));
        Assert.Equal(0.0, res.Get(0, 1));
        Assert.Equal(0.9174820620691818, res.Get(1, 1));
        Assert.Equal(-0.3977771559319137, res.Get(1, 2));
        Assert.Equal(0.3977771559319137, res.Get(2, 1));
        Assert.Equal(0.9174820620691818, res.Get(2, 2));
        Assert.Equal(1.0, res.Get(3, 3));
        Assert.Equal(0.9174820620691818, res.Get(4, 4));
        Assert.Equal(-0.3977771559319137, res.Get(4, 5));
        Assert.Equal(0.3977771559319137, res.Get(5, 4));
        Assert.Equal(0.9174820620691818, res.Get(5, 5));
    }

    [Fact]
    public void CreateRotationMatrixX()
    {
        var res = Matrix.CreateRotationMatrixX(Astrodynamics.Constants.PI2);
        Assert.Equal(1.0, res.Get(0, 0));
        Assert.Equal(-1.0, res.Get(1, 2));
        Assert.Equal(1.0, res.Get(2, 1));
    }

    [Fact]
    public void CreateRotationMatrixY()
    {
        var res = Matrix.CreateRotationMatrixY(Astrodynamics.Constants.PI2);
        Assert.Equal(1.0, res.Get(0, 2));
        Assert.Equal(1.0, res.Get(1, 1));
        Assert.Equal(-1.0, res.Get(2, 0));

        var rot = res.Multiply(new double[] { 1.0, 0.0, 0.0 });
    }

    [Fact]
    public void CreateRotationMatrixZ()
    {
        var res = Matrix.CreateRotationMatrixZ(Astrodynamics.Constants.PI2);
        Assert.Equal(-1.0, res.Get(0, 1));
        Assert.Equal(1.0, res.Get(1, 0));
        Assert.Equal(1.0, res.Get(2, 2));
    }

    [Fact]
    public void ToQuaternion_ShouldReturnCorrectQuaternion_WhenTraceIsPositive()
    {
        // Arrange
        var matrix = new Matrix(new double[,]
        {
            { 2, -1, 0 },
            { -1, 2, 0 },
            { 0, 0, 2 }
        });

        // Act
        var quaternion = matrix.ToQuaternion();

        // Assert
        Assert.Equal(1, quaternion.W, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.X, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.Y, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.Z, precision: 5);
    }

    [Fact]
    public void ToQuaternionZRotationAxis()
    {
        // Arrange
        var matrix = Matrix.CreateRotationMatrixZ(0.5);

        // Act
        var quaternion = matrix.ToQuaternion();

        // Assert
        Assert.Equal(0.96891242171064484, quaternion.W, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.X, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.Y, precision: 5);
        Assert.Equal(0.24740395925452291, quaternion.VectorPart.Z, precision: 5);
    }

    [Fact]
    public void ToQuaternionXRotationAxis()
    {
        // Arrange
        var matrix = Matrix.CreateRotationMatrixX(0.5);

        // Act
        var quaternion = matrix.ToQuaternion();

        // Assert
        Assert.Equal(0.96891242171064484, quaternion.W, precision: 5);
        Assert.Equal(0.24740395925452291, quaternion.VectorPart.X, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.Y, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.Z, precision: 5);
    }

    [Fact]
    public void ToQuaternionYRotationAxis()
    {
        // Arrange
        var matrix = Matrix.CreateRotationMatrixY(0.5);

        // Act
        var quaternion = matrix.ToQuaternion();

        // Assert
        Assert.Equal(0.96891242171064484, quaternion.W, precision: 5);
        Assert.Equal(0, quaternion.VectorPart.X, precision: 5);
        Assert.Equal(0.24740395925452291, quaternion.VectorPart.Y, precision: 5);
        Assert.Equal(0.0, quaternion.VectorPart.Z, precision: 5);
    }

    [Fact]
    public void ToQuaternionXYZRotationAxis()
    {
        // Arrange
        var matrix = new Matrix(3,3);
        matrix.Set(0, 0, 0.7652395);
        matrix.Set(0, 1, -0.3224221);
        matrix.Set(0, 2, 0.5571826);
        matrix.Set(1, 0, 0.5571826);
        matrix.Set(1, 1, 0.7652395);
        matrix.Set(1, 2, -0.3224221);
        matrix.Set(2, 0, -0.3224221);
        matrix.Set(2, 1, 0.5571826);
        matrix.Set(2, 2, 0.7652395);

        // Act
        var quaternion = matrix.ToQuaternion();

        // Assert
        Assert.Equal(0.9077057, quaternion.W, precision: 5);
        Assert.Equal(0.2422604, quaternion.VectorPart.X, precision: 5);
        Assert.Equal(0.2422604, quaternion.VectorPart.Y, precision: 5);
        Assert.Equal(0.2422604, quaternion.VectorPart.Z, precision: 5);
    }
    
    [Fact]
    public void ToQuaternionMinusXYZRotationAxis()
    {
        // Arrange
        var matrix = new Matrix(3,3);
        matrix.Set(0, 0, 0.7652395);
        matrix.Set(0, 1, 0.5571826);
        matrix.Set(0, 2, -0.3224221);
        matrix.Set(1, 0, -0.3224221);
        matrix.Set(1, 1, 0.7652395);
        matrix.Set(1, 2, 0.5571826);
        matrix.Set(2, 0, 0.5571826);
        matrix.Set(2, 1, -0.3224221);
        matrix.Set(2, 2, 0.7652395);

        // Act
        var quaternion = matrix.ToQuaternion();

        // Assert
        Assert.Equal(0.9077057, quaternion.W, precision: 5);
        Assert.Equal(-0.2422604, quaternion.VectorPart.X, precision: 5);
        Assert.Equal(-0.2422604, quaternion.VectorPart.Y, precision: 5);
        Assert.Equal(-0.2422604, quaternion.VectorPart.Z, precision: 5);
    }


    [Fact]
    public void GetHashCode_ReturnsSameHashCode_ForIdenticalMatrices()
    {
        var data1 = new double[,] { { 1, 2 }, { 3, 4 } };
        var data2 = new double[,] { { 1, 2 }, { 3, 4 } };
        var matrix1 = new Matrix(data1);
        var matrix2 = new Matrix(data2);

        Assert.Equal(matrix1.GetHashCode(), matrix2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ReturnsDifferentHashCode_ForDifferentMatrices()
    {
        var data1 = new double[,] { { 1, 2 }, { 3, 4 } };
        var data2 = new double[,] { { 5, 6 }, { 7, 8 } };
        var matrix1 = new Matrix(data1);
        var matrix2 = new Matrix(data2);

        Assert.NotEqual(matrix1.GetHashCode(), matrix2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ReturnsDifferentHashCode_ForDifferentDimensions()
    {
        var data1 = new double[,] { { 1, 2 }, { 3, 4 } };
        var data2 = new double[,] { { 1, 2, 3 }, { 4, 5, 6 } };
        var matrix1 = new Matrix(data1);
        var matrix2 = new Matrix(data2);

        Assert.NotEqual(matrix1.GetHashCode(), matrix2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ReturnsSameHashCode_ForEmptyMatrices()
    {
        var data1 = new double[,] { };
        var data2 = new double[,] { };
        var matrix1 = new Matrix(data1);
        var matrix2 = new Matrix(data2);

        Assert.Equal(matrix1.GetHashCode(), matrix2.GetHashCode());
    }

    #region Transpose Tests

    [Fact]
    public void Transpose_SquareMatrix_ReturnsCorrectTranspose()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        });

        var transposed = matrix.Transpose();

        Assert.Equal(3, transposed.Rows);
        Assert.Equal(3, transposed.Columns);
        Assert.Equal(1, transposed.Get(0, 0));
        Assert.Equal(4, transposed.Get(0, 1));
        Assert.Equal(7, transposed.Get(0, 2));
        Assert.Equal(2, transposed.Get(1, 0));
        Assert.Equal(5, transposed.Get(1, 1));
        Assert.Equal(8, transposed.Get(1, 2));
        Assert.Equal(3, transposed.Get(2, 0));
        Assert.Equal(6, transposed.Get(2, 1));
        Assert.Equal(9, transposed.Get(2, 2));
    }

    [Fact]
    public void Transpose_RectangularMatrix_SwapsDimensions()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 }
        });

        var transposed = matrix.Transpose();

        Assert.Equal(3, transposed.Rows);
        Assert.Equal(2, transposed.Columns);
        Assert.Equal(1, transposed.Get(0, 0));
        Assert.Equal(4, transposed.Get(0, 1));
        Assert.Equal(2, transposed.Get(1, 0));
        Assert.Equal(5, transposed.Get(1, 1));
    }

    [Fact]
    public void Transpose_TwiceReturnsOriginal()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 }
        });

        var doubleTransposed = matrix.Transpose().Transpose();

        Assert.Equal(matrix, doubleTransposed);
    }

    #endregion

    #region ToArray Tests

    [Fact]
    public void ToArray_ReturnsCorrectCopy()
    {
        var original = new double[,]
        {
            { 1, 2 },
            { 3, 4 }
        };
        var matrix = new Matrix(original);

        var array = matrix.ToArray();

        Assert.Equal(1, array[0, 0]);
        Assert.Equal(2, array[0, 1]);
        Assert.Equal(3, array[1, 0]);
        Assert.Equal(4, array[1, 1]);
    }

    [Fact]
    public void ToArray_ReturnsCopyNotReference()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2 },
            { 3, 4 }
        });

        var array = matrix.ToArray();
        array[0, 0] = 999;

        // Original matrix should be unchanged
        Assert.Equal(1, matrix.Get(0, 0));
    }

    #endregion

    #region Identity6x6 Tests

    [Fact]
    public void Identity6x6_ReturnsCorrectDimensions()
    {
        var identity = Matrix.Identity6x6();

        Assert.Equal(6, identity.Rows);
        Assert.Equal(6, identity.Columns);
    }

    [Fact]
    public void Identity6x6_HasOnesOnDiagonal()
    {
        var identity = Matrix.Identity6x6();

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(1.0, identity.Get(i, i));
        }
    }

    [Fact]
    public void Identity6x6_HasZerosOffDiagonal()
    {
        var identity = Matrix.Identity6x6();

        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                if (i != j)
                {
                    Assert.Equal(0.0, identity.Get(i, j));
                }
            }
        }
    }

    [Fact]
    public void Identity6x6_MultiplyByVectorReturnsVector()
    {
        var identity = Matrix.Identity6x6();
        var vector = new double[] { 1, 2, 3, 4, 5, 6 };

        var result = identity.Multiply(vector);

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(vector[i], result[i]);
        }
    }

    #endregion

    #region IsSymmetric Tests

    [Fact]
    public void IsSymmetric_SymmetricMatrix_ReturnsTrue()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 2, 4, 5 },
            { 3, 5, 6 }
        });

        Assert.True(matrix.IsSymmetric());
    }

    [Fact]
    public void IsSymmetric_NonSymmetricMatrix_ReturnsFalse()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        });

        Assert.False(matrix.IsSymmetric());
    }

    [Fact]
    public void IsSymmetric_NonSquareMatrix_ReturnsFalse()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 }
        });

        Assert.False(matrix.IsSymmetric());
    }

    [Fact]
    public void IsSymmetric_IdentityMatrix_ReturnsTrue()
    {
        var identity = Matrix.Identity6x6();

        Assert.True(identity.IsSymmetric());
    }

    [Fact]
    public void IsSymmetric_WithTolerance_HandlesFloatingPointErrors()
    {
        var matrix = new Matrix(new double[,]
        {
            { 1, 2.0000000001 },
            { 2.0, 4 }
        });

        // Should be false with default tolerance
        Assert.False(matrix.IsSymmetric(1e-12));

        // Should be true with larger tolerance
        Assert.True(matrix.IsSymmetric(1e-8));
    }

    #endregion

    #region CreateBlockDiagonal Tests

    [Fact]
    public void CreateBlockDiagonal_ReturnsCorrectDimensions()
    {
        var upper = new Matrix(3, 3);
        var lower = new Matrix(3, 3);

        var result = Matrix.CreateBlockDiagonal(upper, lower);

        Assert.Equal(6, result.Rows);
        Assert.Equal(6, result.Columns);
    }

    [Fact]
    public void CreateBlockDiagonal_PlacesUpperLeftCorrectly()
    {
        var upper = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        });
        var lower = new Matrix(3, 3);

        var result = Matrix.CreateBlockDiagonal(upper, lower);

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(upper.Get(i, j), result.Get(i, j));
            }
        }
    }

    [Fact]
    public void CreateBlockDiagonal_PlacesLowerRightCorrectly()
    {
        var upper = new Matrix(3, 3);
        var lower = new Matrix(new double[,]
        {
            { 10, 11, 12 },
            { 13, 14, 15 },
            { 16, 17, 18 }
        });

        var result = Matrix.CreateBlockDiagonal(upper, lower);

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(lower.Get(i, j), result.Get(i + 3, j + 3));
            }
        }
    }

    [Fact]
    public void CreateBlockDiagonal_OffDiagonalBlocksAreZero()
    {
        var upper = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        });
        var lower = new Matrix(new double[,]
        {
            { 10, 11, 12 },
            { 13, 14, 15 },
            { 16, 17, 18 }
        });

        var result = Matrix.CreateBlockDiagonal(upper, lower);

        // Upper-right block should be zero
        for (int i = 0; i < 3; i++)
        {
            for (int j = 3; j < 6; j++)
            {
                Assert.Equal(0.0, result.Get(i, j));
            }
        }

        // Lower-left block should be zero
        for (int i = 3; i < 6; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(0.0, result.Get(i, j));
            }
        }
    }

    [Fact]
    public void CreateBlockDiagonal_ThrowsForNon3x3Matrices()
    {
        var upper = new Matrix(2, 2);
        var lower = new Matrix(3, 3);

        Assert.Throws<ArgumentException>(() => Matrix.CreateBlockDiagonal(upper, lower));
    }

    #endregion

    #region Cholesky Tests

    [Fact]
    public void Cholesky_3x3PositiveDefinite_ReconstructsOriginal()
    {
        var P = new Matrix(new double[,]
        {
            { 4, 12, -16 },
            { 12, 37, -43 },
            { -16, -43, 98 }
        });

        var L = P.Cholesky();
        var reconstructed = L * L.Transpose();

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.Equal(P.Get(i, j), reconstructed.Get(i, j), 10);
    }

    [Fact]
    public void Cholesky_6x6CovarianceMatrix_ReconstructsOriginal()
    {
        // Realistic LEO covariance (diagonal-dominant, units m² and m²/s²)
        var P = new Matrix(6, 6);
        P.Set(0, 0, 1e4); P.Set(0, 1, 1e2); P.Set(0, 2, 5e1);
        P.Set(1, 0, 1e2); P.Set(1, 1, 2e4); P.Set(1, 2, 3e1);
        P.Set(2, 0, 5e1); P.Set(2, 1, 3e1); P.Set(2, 2, 1.5e4);
        P.Set(3, 3, 1e-2); P.Set(3, 4, 1e-4);
        P.Set(4, 3, 1e-4); P.Set(4, 4, 2e-2); P.Set(4, 5, 5e-5);
        P.Set(5, 4, 5e-5); P.Set(5, 5, 1.5e-2);

        var L = P.Cholesky();
        var reconstructed = L * L.Transpose();

        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                Assert.Equal(P.Get(i, j), reconstructed.Get(i, j), 6);
    }

    [Fact]
    public void Cholesky_IdentityMatrix_ReturnsIdentity()
    {
        var I = new Matrix(new double[,]
        {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 }
        });

        var L = I.Cholesky();

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.Equal(I.Get(i, j), L.Get(i, j), 10);
    }

    [Fact]
    public void Cholesky_DiagonalMatrix_ReturnsSqrtDiagonal()
    {
        var D = new Matrix(new double[,]
        {
            { 4, 0, 0 },
            { 0, 9, 0 },
            { 0, 0, 16 }
        });

        var L = D.Cholesky();

        Assert.Equal(2.0, L.Get(0, 0), 10);
        Assert.Equal(3.0, L.Get(1, 1), 10);
        Assert.Equal(4.0, L.Get(2, 2), 10);
    }

    [Fact]
    public void Cholesky_LowerTriangular_ZerosAboveDiagonal()
    {
        var P = new Matrix(new double[,]
        {
            { 4, 12, -16 },
            { 12, 37, -43 },
            { -16, -43, 98 }
        });

        var L = P.Cholesky();

        for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++)
                Assert.Equal(0.0, L.Get(i, j), 10);
    }

    [Fact]
    public void Cholesky_NonSquareMatrix_Throws()
    {
        var m = new Matrix(2, 3);

        Assert.Throws<InvalidOperationException>(() => m.Cholesky());
    }

    [Fact]
    public void Cholesky_NonSymmetricMatrix_Throws()
    {
        var m = new Matrix(new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        });

        Assert.Throws<InvalidOperationException>(() => m.Cholesky());
    }

    [Fact]
    public void Cholesky_NonPositiveDefinite_Throws()
    {
        // Negative eigenvalue matrix
        var m = new Matrix(new double[,]
        {
            { 1, 0 },
            { 0, -1 }
        });

        Assert.Throws<InvalidOperationException>(() => m.Cholesky());
    }

    [Fact]
    public void Cholesky_SemiDefinite_Throws()
    {
        // Positive semi-definite (rank deficient): last diagonal becomes 0
        var m = new Matrix(new double[,]
        {
            { 1, 1 },
            { 1, 1 }
        });

        Assert.Throws<InvalidOperationException>(() => m.Cholesky());
    }

    [Fact]
    public void Cholesky_1x1Matrix_ReturnsScalar()
    {
        var m = new Matrix(new double[,] { { 4.0 } });

        var L = m.Cholesky();

        Assert.Equal(2.0, L.Get(0, 0), 10);
    }

    #endregion

    #region FromQuaternion Tests

    [Fact]
    public void FromQuaternion_IdentityQuaternion_ReturnsIdentityMatrix()
    {
        var identity = new Quaternion(1, new Vector3(0, 0, 0));

        var matrix = Matrix.FromQuaternion(identity);

        Assert.Equal(3, matrix.Rows);
        Assert.Equal(3, matrix.Columns);
        Assert.Equal(1.0, matrix.Get(0, 0), 5);
        Assert.Equal(0.0, matrix.Get(0, 1), 5);
        Assert.Equal(0.0, matrix.Get(0, 2), 5);
        Assert.Equal(0.0, matrix.Get(1, 0), 5);
        Assert.Equal(1.0, matrix.Get(1, 1), 5);
        Assert.Equal(0.0, matrix.Get(1, 2), 5);
        Assert.Equal(0.0, matrix.Get(2, 0), 5);
        Assert.Equal(0.0, matrix.Get(2, 1), 5);
        Assert.Equal(1.0, matrix.Get(2, 2), 5);
    }

    [Fact]
    public void FromQuaternion_90DegreeZRotation_ReturnsCorrectMatrix()
    {
        // 90 degree rotation around Z axis: q = cos(45°) + sin(45°)k
        var angle = System.Math.PI / 2;
        var q = new Quaternion(System.Math.Cos(angle / 2), new Vector3(0, 0, System.Math.Sin(angle / 2)));

        var matrix = Matrix.FromQuaternion(q);

        // Expected: rotation by 90° around Z
        Assert.Equal(0.0, matrix.Get(0, 0), 5);
        Assert.Equal(-1.0, matrix.Get(0, 1), 5);
        Assert.Equal(0.0, matrix.Get(0, 2), 5);
        Assert.Equal(1.0, matrix.Get(1, 0), 5);
        Assert.Equal(0.0, matrix.Get(1, 1), 5);
        Assert.Equal(0.0, matrix.Get(1, 2), 5);
        Assert.Equal(0.0, matrix.Get(2, 0), 5);
        Assert.Equal(0.0, matrix.Get(2, 1), 5);
        Assert.Equal(1.0, matrix.Get(2, 2), 5);
    }

    [Fact]
    public void FromQuaternion_RoundTripWithToQuaternion()
    {
        // Create a rotation matrix
        var originalMatrix = Matrix.CreateRotationMatrixZ(0.7);

        // Convert to quaternion and back
        var quaternion = originalMatrix.ToQuaternion();
        var reconstructedMatrix = Matrix.FromQuaternion(quaternion);

        // Should get the same matrix back
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(originalMatrix.Get(i, j), reconstructedMatrix.Get(i, j), 5);
            }
        }
    }

    #endregion

    #region State Transformation Jacobian Tests

    [Fact]
    public void CreateStateTransformationJacobian_ZeroAngularVelocity_IsBlockDiagonalRotation()
    {
        // Arrange
        var rotation = new Quaternion(System.Math.Cos(0.3), new Vector3(0.0, 0.0, System.Math.Sin(0.3)));
        var r = Matrix.FromQuaternion(rotation);

        // Act
        var jacobian = Matrix.CreateStateTransformationJacobian(rotation, Vector3.Zero);

        // Assert
        var expected = Matrix.CreateBlockDiagonal(r, r);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(expected.Get(i, j), jacobian.Get(i, j));
            }
        }
    }

    [Fact]
    public void CreateStateTransformationJacobian_IdentityRotation_LowerLeftBlockIsMinusCrossProductMatrix()
    {
        // Arrange
        var identity = new Quaternion(1.0, Vector3.Zero);
        var omega = new Vector3(1e-5, -2e-5, 7.292115e-5);

        // Act
        var jacobian = Matrix.CreateStateTransformationJacobian(identity, omega);

        // Assert: dv'/dr applied to any r must equal -(w x r).
        var r = new Vector3(7000000.0, -1200000.0, 300000.0);
        var expected = omega.Cross(r).Inverse();
        var actual = jacobian.SubMatrix(3, 0, 3, 3).Multiply(new[] { r.X, r.Y, r.Z });
        Assert.Equal(expected.X, actual[0], 9);
        Assert.Equal(expected.Y, actual[1], 9);
        Assert.Equal(expected.Z, actual[2], 9);
        Assert.Equal(0.0, jacobian.Get(0, 3));
        Assert.Equal(1.0, jacobian.Get(3, 3));
    }

    [Fact]
    public void TransformCovarianceWithJacobian_ReturnsSymmetrizedProduct()
    {
        // Arrange
        var covariance = new Matrix(new[,] { { 4.0, 1.0 }, { 1.0, 9.0 } });
        var jacobian = new Matrix(new[,] { { 1.0, 2.0 }, { 0.5, -1.0 }, { 3.0, 0.0 } });

        // Act
        var result = Matrix.TransformCovarianceWithJacobian(covariance, jacobian);

        // Assert
        var expected = jacobian.Multiply(covariance).Multiply(jacobian.Transpose());
        Assert.Equal(3, result.Rows);
        Assert.Equal(3, result.Columns);
        Assert.True(result.IsSymmetric(0.0));
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(expected.Get(i, j), result.Get(i, j), 12);
            }
        }
    }

    [Fact]
    public void TransformCovarianceWithJacobian_InvalidDimensions_Throws()
    {
        Assert.Throws<ArgumentException>(() => Matrix.TransformCovarianceWithJacobian(new Matrix(2, 3), new Matrix(2, 2)));
        Assert.Throws<ArgumentException>(() => Matrix.TransformCovarianceWithJacobian(new Matrix(3, 3), new Matrix(3, 2)));
    }

    [Fact]
    public void Symmetrize_AveragesOffDiagonalTerms()
    {
        // Arrange
        var matrix = new Matrix(new[,] { { 1.0, 2.0 }, { 4.0, 5.0 } });

        // Act
        var result = Matrix.Symmetrize(matrix);

        // Assert
        Assert.Equal(1.0, result.Get(0, 0));
        Assert.Equal(3.0, result.Get(0, 1));
        Assert.Equal(3.0, result.Get(1, 0));
        Assert.Equal(5.0, result.Get(1, 1));
        Assert.Throws<ArgumentException>(() => Matrix.Symmetrize(new Matrix(2, 3)));
    }

    #endregion

    #region TransformCovariance Tests

    [Fact]
    public void TransformCovariance_IdentityRotation_ReturnsSameCovariance()
    {
        // Create a diagonal covariance matrix
        var covariance = new Matrix(6, 6);
        covariance.Set(0, 0, 1.0);
        covariance.Set(1, 1, 2.0);
        covariance.Set(2, 2, 3.0);
        covariance.Set(3, 3, 0.1);
        covariance.Set(4, 4, 0.2);
        covariance.Set(5, 5, 0.3);

        var identityRotation = new Quaternion(1, new Vector3(0, 0, 0));

        var result = Matrix.TransformCovariance(covariance, identityRotation);

        // Result should be the same as input
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(covariance.Get(i, j), result.Get(i, j), 10);
            }
        }
    }

    [Fact]
    public void TransformCovariance_DiagonalCovariance_180DegreeRotation_PreservesDiagonal()
    {
        // Diagonal covariance
        var covariance = new Matrix(6, 6);
        covariance.Set(0, 0, 1.0);
        covariance.Set(1, 1, 1.0);
        covariance.Set(2, 2, 1.0);
        covariance.Set(3, 3, 0.01);
        covariance.Set(4, 4, 0.01);
        covariance.Set(5, 5, 0.01);

        // 180 degree rotation around Z
        var angle = System.Math.PI;
        var rotation = new Quaternion(System.Math.Cos(angle / 2), new Vector3(0, 0, System.Math.Sin(angle / 2)));

        var result = Matrix.TransformCovariance(covariance, rotation);

        // For isotropic covariance, diagonal should be preserved
        Assert.Equal(1.0, result.Get(0, 0), 10);
        Assert.Equal(1.0, result.Get(1, 1), 10);
        Assert.Equal(1.0, result.Get(2, 2), 10);
        Assert.Equal(0.01, result.Get(3, 3), 10);
        Assert.Equal(0.01, result.Get(4, 4), 10);
        Assert.Equal(0.01, result.Get(5, 5), 10);
    }

    [Fact]
    public void TransformCovariance_90DegreeZRotation_SwapsXYVariances()
    {
        // Covariance with different X and Y variances
        var covariance = new Matrix(6, 6);
        covariance.Set(0, 0, 4.0);  // σ²x = 4
        covariance.Set(1, 1, 1.0);  // σ²y = 1
        covariance.Set(2, 2, 9.0);  // σ²z = 9
        covariance.Set(3, 3, 0.04); // σ²vx
        covariance.Set(4, 4, 0.01); // σ²vy
        covariance.Set(5, 5, 0.09); // σ²vz

        // 90 degree rotation around Z
        var angle = System.Math.PI / 2;
        var rotation = new Quaternion(System.Math.Cos(angle / 2), new Vector3(0, 0, System.Math.Sin(angle / 2)));

        var result = Matrix.TransformCovariance(covariance, rotation);

        // After 90° Z rotation: X→Y, Y→-X
        // So σ²x' = σ²y and σ²y' = σ²x
        Assert.Equal(1.0, result.Get(0, 0), 5);  // New X variance was Y variance
        Assert.Equal(4.0, result.Get(1, 1), 5);  // New Y variance was X variance
        Assert.Equal(9.0, result.Get(2, 2), 5);  // Z variance unchanged
        Assert.Equal(0.01, result.Get(3, 3), 5); // New Vx variance was Vy variance
        Assert.Equal(0.04, result.Get(4, 4), 5); // New Vy variance was Vx variance
        Assert.Equal(0.09, result.Get(5, 5), 5); // Vz variance unchanged
    }

    [Fact]
    public void TransformCovariance_PreservesSymmetry()
    {
        // Create a symmetric covariance matrix with off-diagonal terms
        var covariance = new Matrix(6, 6);
        covariance.Set(0, 0, 1.0);
        covariance.Set(1, 1, 2.0);
        covariance.Set(2, 2, 3.0);
        covariance.Set(0, 1, 0.5); covariance.Set(1, 0, 0.5);
        covariance.Set(3, 3, 0.1);
        covariance.Set(4, 4, 0.2);
        covariance.Set(5, 5, 0.3);
        covariance.Set(3, 4, 0.05); covariance.Set(4, 3, 0.05);

        // Arbitrary rotation
        var rotation = new Quaternion(0.9, new Vector3(0.1, 0.2, 0.3)).Normalize();

        var result = Matrix.TransformCovariance(covariance, rotation);

        // Result should still be symmetric
        Assert.True(result.IsSymmetric(1e-10));
    }

    [Fact]
    public void TransformCovariance_PreservesPositiveDefiniteness()
    {
        // Create a positive definite covariance matrix (diagonal)
        var covariance = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            covariance.Set(i, i, 1.0 + i * 0.1);
        }

        var rotation = new Quaternion(0.7, new Vector3(0.3, 0.4, 0.5)).Normalize();

        var result = Matrix.TransformCovariance(covariance, rotation);

        // All diagonal elements should be positive
        for (int i = 0; i < 6; i++)
        {
            Assert.True(result.Get(i, i) > 0, $"Diagonal element [{i},{i}] should be positive");
        }
    }

    [Fact]
    public void TransformCovariance_ThrowsForNon6x6Matrix()
    {
        var covariance = new Matrix(3, 3);
        var rotation = new Quaternion(1, new Vector3(0, 0, 0));

        Assert.Throws<ArgumentException>(() => Matrix.TransformCovariance(covariance, rotation));
    }

    [Fact]
    public void TransformCovariance_MathematicalVerification_TPT()
    {
        // Manually verify P' = T * P * T^T
        // Create a simple covariance
        var P = new Matrix(6, 6);
        P.Set(0, 0, 4.0);
        P.Set(1, 1, 1.0);
        P.Set(2, 2, 1.0);
        P.Set(3, 3, 0.04);
        P.Set(4, 4, 0.01);
        P.Set(5, 5, 0.01);

        // 90 degree Z rotation
        var angle = System.Math.PI / 2;
        var q = new Quaternion(System.Math.Cos(angle / 2), new Vector3(0, 0, System.Math.Sin(angle / 2)));

        // Get rotation matrix
        var R = Matrix.FromQuaternion(q);

        // Build T manually
        var T = Matrix.CreateBlockDiagonal(R, R);

        // Compute P' = T * P * T^T manually
        var Pprime_manual = T.Multiply(P).Multiply(T.Transpose());

        // Compute using TransformCovariance
        var Pprime_method = Matrix.TransformCovariance(P, q);

        // They should be equal
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(Pprime_manual.Get(i, j), Pprime_method.Get(i, j), 10);
            }
        }
    }

    // Third round of #668: the rotation's magnitude was not scaled, so a finite quaternion with a component above about
    // 1.34e154 threw ArgumentException (rotation). (1e200, 1e200, 0, 0) is Rx(90°), which sends y to z and z to -y: the
    // closed form of R P Rᵀ for P = diag(4, 1, 9, 0.04, 0.01, 0.09) is diag(4, 9, 1, 0.04, 0.09, 0.01). The worst element,
    // MEASURE 3.553e-15 (two ulps of 9) at 1e200 and 1e-170 alike on each of Linux x64, Windows x64 and macOS arm64 (CI
    // run 43745883709, 2026-11-15); twice the worst rounded up at the second significant digit
    private const double ExtremeRotationCovarianceTolerance = 7.2e-15;

    [Theory]
    [InlineData(1e200)]
    [InlineData(1e-170)]
    public void TransformCovariance_ExtremeFiniteRotation_IsTheRotationOfItsUnitQuaternion(double c)
    {
        var P = new Matrix(6, 6);
        double[] diagonal = { 4.0, 1.0, 9.0, 0.04, 0.01, 0.09 };
        for (int i = 0; i < 6; i++) P.Set(i, i, diagonal[i]);
        double[] expected = { 4.0, 9.0, 1.0, 0.04, 0.09, 0.01 };

        var result = Matrix.TransformCovariance(P, new Quaternion(c, c, 0.0, 0.0));

        double worst = 0.0;
        for (int i = 0; i < 6; i++)
        for (int j = 0; j < 6; j++)
            worst = System.Math.Max(worst, System.Math.Abs(result.Get(i, j) - (i == j ? expected[i] : 0.0)));
        _output.WriteLine($"MEASURE TransformCovariance by ({c:E1}, {c:E1}, 0, 0) against Rx(90°) P Rx(90°)ᵀ: {worst:E3}");
        Assert.True(worst <= ExtremeRotationCovarianceTolerance, $"{worst:E3}");

        // The zero and non-finite rotations still throw
        Assert.Equal("rotation", Assert.Throws<ArgumentException>(() => Matrix.TransformCovariance(P, new Quaternion(0.0, 0.0, 0.0, 0.0))).ParamName);
        Assert.Equal("rotation", Assert.Throws<ArgumentException>(() => Matrix.TransformCovariance(P, new Quaternion(double.NaN, 0.0, 0.0, 0.0))).ParamName);
    }

    #endregion

    #region Add

    [Fact]
    public void Add_SameDimensions()
    {
        var a = new Matrix(new double[,] { { 1, 2 }, { 3, 4 } });
        var b = new Matrix(new double[,] { { 5, 6 }, { 7, 8 } });
        var result = a.Add(b);
        Assert.Equal(6.0, result.Get(0, 0));
        Assert.Equal(8.0, result.Get(0, 1));
        Assert.Equal(10.0, result.Get(1, 0));
        Assert.Equal(12.0, result.Get(1, 1));
    }

    [Fact]
    public void Add_OperatorPlus()
    {
        var a = new Matrix(new double[,] { { 1, 2 }, { 3, 4 } });
        var b = new Matrix(new double[,] { { 10, 20 }, { 30, 40 } });
        var result = a + b;
        Assert.Equal(11.0, result.Get(0, 0));
        Assert.Equal(44.0, result.Get(1, 1));
    }

    [Fact]
    public void Add_DimensionMismatch_Throws()
    {
        var a = new Matrix(2, 3);
        var b = new Matrix(3, 2);
        Assert.Throws<ArgumentException>(() => a.Add(b));
    }

    #endregion

    #region SubMatrix

    [Fact]
    public void SubMatrix_Extract2x2From3x3()
    {
        var m = new Matrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } });
        var sub = m.SubMatrix(0, 0, 2, 2);
        Assert.Equal(2, sub.Rows);
        Assert.Equal(2, sub.Columns);
        Assert.Equal(1.0, sub.Get(0, 0));
        Assert.Equal(2.0, sub.Get(0, 1));
        Assert.Equal(4.0, sub.Get(1, 0));
        Assert.Equal(5.0, sub.Get(1, 1));
    }

    [Fact]
    public void SubMatrix_OffsetExtraction()
    {
        var m = new Matrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } });
        var sub = m.SubMatrix(1, 1, 2, 2);
        Assert.Equal(5.0, sub.Get(0, 0));
        Assert.Equal(6.0, sub.Get(0, 1));
        Assert.Equal(8.0, sub.Get(1, 0));
        Assert.Equal(9.0, sub.Get(1, 1));
    }

    [Fact]
    public void SubMatrix_OutOfRange_Throws()
    {
        var m = new Matrix(3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.SubMatrix(2, 2, 2, 2));
    }

    #endregion

    #region FromRowVectors

    [Fact]
    public void FromRowVectors_CreatesCorrectMatrix()
    {
        var r0 = new Vector3(1, 0, 0);
        var r1 = new Vector3(0, 1, 0);
        var r2 = new Vector3(0, 0, 1);
        var m = Matrix.FromRowVectors(r0, r1, r2);
        Assert.Equal(3, m.Rows);
        Assert.Equal(3, m.Columns);
        Assert.Equal(1.0, m.Get(0, 0));
        Assert.Equal(0.0, m.Get(0, 1));
        Assert.Equal(1.0, m.Get(1, 1));
        Assert.Equal(1.0, m.Get(2, 2));
    }

    [Fact]
    public void FromRowVectors_ArbitraryValues()
    {
        var r0 = new Vector3(1, 2, 3);
        var r1 = new Vector3(4, 5, 6);
        var r2 = new Vector3(7, 8, 9);
        var m = Matrix.FromRowVectors(r0, r1, r2);
        Assert.Equal(1.0, m.Get(0, 0));
        Assert.Equal(3.0, m.Get(0, 2));
        Assert.Equal(5.0, m.Get(1, 1));
        Assert.Equal(7.0, m.Get(2, 0));
        Assert.Equal(9.0, m.Get(2, 2));
    }

    #endregion

    // #680: the elements compare as double.Equals, the semantics of Vector3 and of every record struct of the library: NaN
    // equals NaN only, +0 equals -0, and two entries one smallest subnormal apart differ. The expectation is computed pair by
    // pair from double.Equals, never from Matrix.Equals; equal matrices hash equal (the .NET contract)
    [Fact]
    public void Equals_FollowsDoubleEquals_AndTheHashContract()
    {
        double[] values = { 0.0, -0.0, double.Epsilon, 1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        var matrices = new System.Collections.Generic.List<(double A, double B, Matrix M)>();
        foreach (var a in values)
        {
            foreach (var b in values)
            {
                matrices.Add((a, b, new Matrix(new[,] { { a, b }, { 0.0, 0.0 } })));
            }
        }

        foreach (var (a1, b1, m1) in matrices)
        {
            // Reflexive
            Assert.True(m1.Equals(m1));
            Assert.True(m1 == new Matrix(new[,] { { a1, b1 }, { 0.0, 0.0 } }));
            foreach (var (a2, b2, m2) in matrices)
            {
                bool expected = a1.Equals(a2) && b1.Equals(b2);
                Assert.True(expected == m1.Equals(m2), $"({a1:R}, {b1:R}) against ({a2:R}, {b2:R}): Equals {m1.Equals(m2)}");
                Assert.Equal(expected, m1 == m2);
                // Symmetric
                Assert.Equal(m1.Equals(m2), m2.Equals(m1));
                if (expected)
                {
                    Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
                }
            }
        }

        // Transitive: a == b and b == c imply a == c
        foreach (var (_, _, m1) in matrices)
        {
            foreach (var (_, _, m2) in matrices)
            {
                if (!m1.Equals(m2)) continue;
                foreach (var (_, _, m3) in matrices)
                {
                    if (m2.Equals(m3))
                    {
                        Assert.True(m1.Equals(m3));
                    }
                }
            }
        }

        // The issue's cases: a NaN entry against a number, and 0 against the smallest subnormal
        var zero = new Matrix(2, 2);
        var withNaN = new Matrix(new[,] { { double.NaN, 0.0 }, { 0.0, 0.0 } });
        var withOne = new Matrix(new[,] { { 1.0, 0.0 }, { 0.0, 0.0 } });
        Assert.False(zero == withNaN);
        Assert.False(withNaN == withOne);
        Assert.False(zero == new Matrix(new[,] { { double.Epsilon, 0.0 }, { 0.0, 0.0 } }));
        Assert.True(withNaN == new Matrix(new[,] { { double.NaN, 0.0 }, { 0.0, 0.0 } }));
        Assert.True(zero == new Matrix(new[,] { { -0.0, 0.0 }, { 0.0, -0.0 } }));
    }

    // The known limitation of #680: FromQuaternion normalised its argument leniently, so the zero quaternion gave the
    // identity and TransformCovariance returned the covariance unrotated; a zero or non-finite quaternion is no rotation
    // (a tiny non-zero one, which threw until the third round of #668, is a rotation: see
    // TransformCovariance_ExtremeFiniteRotation_IsTheRotationOfItsUnitQuaternion)
    [Theory]
    [InlineData(0.0, 0.0, 0.0, 0.0)]
    [InlineData(-0.0, 0.0, -0.0, 0.0)]
    [InlineData(double.NaN, 0.0, 0.0, 0.0)]
    [InlineData(1.0, double.PositiveInfinity, 0.0, 0.0)]
    [InlineData(0.0, 0.0, double.NegativeInfinity, 1.0)]
    public void FromQuaternion_ZeroOrNonFiniteQuaternion_Throws(double w, double x, double y, double z)
    {
        var q = new Quaternion(w, x, y, z);
        Assert.Equal("q", Assert.Throws<ArgumentException>(() => Matrix.FromQuaternion(q)).ParamName);
        Assert.Equal("rotation", Assert.Throws<ArgumentException>(() => Matrix.TransformCovariance(Matrix.Identity6x6(), q)).ParamName);
    }

    // The rotation matrix of a non-zero quaternion is the closed form of its unit quaternion u = q / |q|:
    // [1 - 2(y² + z²), 2(xy - wz), 2(xz + wy); 2(xy + wz), 1 - 2(x² + z²), 2(yz - wx); 2(xz - wy), 2(yz + wx), 1 - 2(x² + y²)]
    // (M. D. Shuster, A survey of attitude representations, J. Astronaut. Sci. 41(4), 1993, the attitude matrix of a
    // quaternion, transposed for the active rotation q p q*; confirmed by S. Guillet, 2026-11-15), and SPICE q2m (Validation)
    private static Quaternion RandomQuaternion(Random random)
    {
        Quaternion q;
        do
        {
            q = new Quaternion(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0,
                random.NextDouble() * 2.0 - 1.0);
        } while (q.Magnitude() < 1e-3);

        return q * System.Math.Pow(10.0, random.NextDouble() * 20.0 - 10.0);
    }

    // The worst element of FromQuaternion against the closed form of q / |q| and against SPICE q2m over 10 000 random
    // quaternions of 1e-10 to 1e10, MEASURE 0 against the closed form on each of Linux x64, Windows x64 and macOS arm64
    // and 1.110e-15 against q2m (Validation category, Linux only; CI run 43745883709, 2026-11-15); twice the worst,
    // rounded up at the second significant digit
    private const double FromQuaternionTolerance = 2.3e-15;

    [Fact]
    public void FromQuaternion_NonZeroQuaternion_IsTheRotationMatrixOfItsUnitQuaternion()
    {
        var random = new Random(6801);
        double worst = 0.0;
        for (int i = 0; i < 10000; i++)
        {
            var q = RandomQuaternion(random);
            double m = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X + q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);
            double w = q.W / m, x = q.VectorPart.X / m, y = q.VectorPart.Y / m, z = q.VectorPart.Z / m;
            double[] expected =
            [
                1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y - w * z), 2.0 * (x * z + w * y),
                2.0 * (x * y + w * z), 1.0 - 2.0 * (x * x + z * z), 2.0 * (y * z - w * x),
                2.0 * (x * z - w * y), 2.0 * (y * z + w * x), 1.0 - 2.0 * (x * x + y * y)
            ];
            var matrix = Matrix.FromQuaternion(q);
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                worst = System.Math.Max(worst, System.Math.Abs(matrix.Get(r, c) - expected[3 * r + c]));
        }

        Console.WriteLine($"MEASURE FromQuaternion against the closed form of q / |q|: {worst:E3}");
        Assert.True(worst <= FromQuaternionTolerance, $"{worst:E3}");
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void FromQuaternion_NonZeroQuaternion_IsTheRotationMatrixOfSpiceQ2m()
    {
        TestKernels.EnsureSolarSystem();
        var random = new Random(6802);
        double worst = 0.0;
        for (int i = 0; i < 10000; i++)
        {
            var q = RandomQuaternion(random);
            double m = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X + q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);
            var expected = IO.Astrodynamics.Tests.Body.CspiceDirect.Q2m(q.W / m, q.VectorPart.X / m, q.VectorPart.Y / m, q.VectorPart.Z / m);
            var matrix = Matrix.FromQuaternion(q);
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                worst = System.Math.Max(worst, System.Math.Abs(matrix.Get(r, c) - expected[3 * r + c]));
        }

        Console.WriteLine($"MEASURE FromQuaternion against SPICE q2m of q / |q|: {worst:E3}");
        Assert.True(worst <= FromQuaternionTolerance, $"{worst:E3}");
    }

    /// <summary>
    /// <c>new Matrix(double[,])</c> copies the caller's array (#745, decided by S. Guillet, 2026-11-18): a write to that
    /// array afterwards leaves the matrix, its equality and its hash code unchanged, as <c>Matrix(rows, columns, double[])</c>
    /// and <see cref="Matrix.ToArray"/> already did. Before, the matrix wrapped the array: the probe of the issue,
    /// <c>a[0, 0] = 5</c> after <c>new Matrix(a)</c>, gave <c>Get(0, 0)</c> 5 and another hash code. The expected terms
    /// are those given; the reference hash code is that of a matrix built from a fresh copy of the same terms.
    /// </summary>
    [Fact]
    public void Constructor_FromArray_CopiesTheArray()
    {
        var array = new double[,] { { 1.0, 0.0 }, { 0.0, 1.0 } };
        var matrix = new Matrix(array);
        var reference = new Matrix(2, 2, new[] { 1.0, 0.0, 0.0, 1.0 });
        int hash = matrix.GetHashCode();

        array[0, 0] = 5.0;
        array[1, 0] = -3.0;

        Assert.Equal(1.0, matrix.Get(0, 0));
        Assert.Equal(0.0, matrix.Get(1, 0));
        Assert.Equal(hash, matrix.GetHashCode());
        Assert.Equal(reference.GetHashCode(), matrix.GetHashCode());
        Assert.Equal(reference, matrix);

        // The matrix still changes through its own Set, which the array does not see (#722 kept Matrix mutable by Set)
        matrix.Set(0, 1, 7.0);
        Assert.Equal(7.0, matrix.Get(0, 1));
        Assert.Equal(0.0, array[0, 1]);
    }

    /// <summary>
    /// The library's results built in an array of their own share it with nothing (#745): the product, the inverse and
    /// the copy of a matrix, both overloads of <see cref="Jacobian.Evaluate(Func{double[], double}[], double[])"/>, and
    /// <see cref="IO.Astrodynamics.CCSDS.Common.CovarianceMatrix.ToMatrix"/> and
    /// <see cref="IO.Astrodynamics.CCSDS.Common.CovarianceMatrix.ToMatrixWithUnitConversion"/>, which wrap their array
    /// (internal <c>Matrix.Wrap</c>), are not changed by a later write to the matrices or arrays they came from, and a
    /// write to one result reaches neither its source nor the next result of the same call. The rotation of
    /// <c>SiteFrame.FromPlanetodetic</c>, the last array wrapped, is local to that method and leaves it as a quaternion,
    /// so no matrix of it can be written. Expected values are closed forms.
    /// </summary>
    [Fact]
    public void Results_ShareNoArrayWithTheirInputs()
    {
        var array = new double[,] { { 2.0, 1.0 }, { 1.0, 3.0 } };
        var matrix = new Matrix(array);
        var inverse = matrix.Inverse();
        var product = matrix.Multiply(matrix);
        var copy = matrix.Copy();
        array[0, 0] = 100.0;
        matrix.Set(1, 1, 100.0);

        // Closed forms: [[2, 1], [1, 3]]^-1 = [[3, -1], [-1, 2]] / 5 and [[2, 1], [1, 3]]^2 = [[5, 5], [5, 10]]
        Assert.Equal(0.6, inverse.Get(0, 0), 15);
        Assert.Equal(0.4, inverse.Get(1, 1), 15);
        Assert.Equal(5.0, product.Get(0, 0));
        Assert.Equal(10.0, product.Get(1, 1));
        Assert.Equal(2.0, copy.Get(0, 0));
        Assert.Equal(3.0, copy.Get(1, 1));
        copy.Set(0, 0, -1.0);
        Assert.Equal(2.0, matrix.Get(0, 0));

        // The Jacobian of (x0 x1, x0 + x1) at (2, 3) is [[3, 2], [1, 1]] (central differences are exact for these terms but
        // for rounding); a write to the result or to x does not reach the next evaluation
        var f = new Func<double[], double>[] { v => v[0] * v[1], v => v[0] + v[1] };
        var x = new[] { 2.0, 3.0 };
        var jacobian = new Jacobian();
        foreach (Func<Matrix> evaluate in new Func<Matrix>[]
                 {
                     () => jacobian.Evaluate(f, (double[])x.Clone()),
                     () => jacobian.Evaluate(f, (double[])x.Clone(), new[] { 6.0, 5.0 })
                 })
        {
            var first = evaluate();
            first.Set(0, 0, 100.0);
            first.Set(1, 1, 100.0);
            var second = evaluate();
            Assert.Equal(3.0, second.Get(0, 0), 8);
            Assert.Equal(2.0, second.Get(0, 1), 8);
            Assert.Equal(1.0, second.Get(1, 0), 8);
            Assert.Equal(1.0, second.Get(1, 1), 8);
        }

        // A covariance's matrices: a write to one leaves the covariance and the next matrix as they were (the terms in km²,
        // km²/s, km²/s², times 1e6 in SI)
        var covariance = new IO.Astrodynamics.CCSDS.Common.CovarianceMatrix(
            1.0, 0.1, 2.0,
            0.2, 0.3, 3.0,
            0.01, 0.02, 0.03, 4.0e-4,
            0.04, 0.05, 0.06, 1.0e-5, 5.0e-4,
            0.07, 0.08, 0.09, 2.0e-5, 3.0e-5, 6.0e-4);
        foreach (Func<Matrix> read in new Func<Matrix>[] { covariance.ToMatrix, covariance.ToMatrixWithUnitConversion })
        {
            var first = read();
            double scale = first.Get(0, 0);
            first.Set(0, 0, -7.0);
            first.Set(2, 1, -7.0);
            var second = read();
            Assert.Equal(scale, second.Get(0, 0));
            Assert.Equal(0.3 * scale, second.Get(2, 1));
            Assert.Equal(0.3 * scale, second.Get(1, 2));
        }

        Assert.Equal(1.0, covariance.CxX);
        Assert.Equal(0.3, covariance.CzY);
        Assert.Equal(1.0e6, covariance.ToMatrixWithUnitConversion().Get(0, 0));
    }
}
