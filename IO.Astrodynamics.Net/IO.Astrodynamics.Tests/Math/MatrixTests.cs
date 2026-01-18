using System;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

public class MatirxTests
{
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

    #endregion
}