using System;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

public class JacobianTests
{
    [Fact]
    public void Evaluate_ShouldReturnCorrectJacobianForSimpleFunction()
    {
        // Arrange
        var jacobian = new Jacobian();
        Func<double[], double>[] functions =
        {
            x => x[0] * x[0], // f1(x) = x1^2
            x => x[1] * x[1] // f2(x) = x2^2
        };
        double[] x = [2.0, 3.0];

        // Act
        Matrix result = jacobian.Evaluate(functions, x);

        // Assert
        Assert.Equal(2, result.Rows); // Supposons que le résultat soit une matrice 2x2
        Assert.Equal(2, result.Columns);
        // Check : df1/dx1 = 4, df2/dx2 = 6
        Assert.Equal(4.0, result.Get(0, 0), 5); // Tolérance de 10^-5
        Assert.Equal(0.0, result.Get(0, 1), 5);
        Assert.Equal(0.0, result.Get(1, 0), 5);
        Assert.Equal(6.0, result.Get(1, 1), 5);
    }

    [Fact]
    public void Evaluate_ShouldReturnZeroJacobianForZeroFunction()
    {
        // Arrange
        var jacobian = new Jacobian();
        Func<double[], double>[] functions =
        {
            x => 0.0, // f1(x) = 0
            x => 0.0 // f2(x) = 0
        };
        double[] x = [2.0, 3.0];

        // Act
        Matrix result = jacobian.Evaluate(functions, x);

        // Assert
        Assert.Equal(2, result.Rows);
        Assert.Equal(2, result.Columns);
        // Check is null matrix
        for (int i = 0; i < result.Rows; i++)
        {
            for (int j = 0; j < result.Columns; j++)
            {
                Assert.Equal(0.0, result.Get(i, j), 5);
            }
        }
    }

    [Fact]
    public void Evaluate_ShouldThrowArgumentExceptionWhenDimensionsMismatch()
    {
        // Arrange
        var jacobian = new Jacobian(3, 1);
        Func<double[], double>[] functions =
        {
            x => x[0] + x[1],
            x => x[0] * x[1]
        };
        double[] x = [2.0];

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => jacobian.Evaluate(functions, x));
    }

    [Fact]
    public void Evaluate_WithValidInputs_ReturnsExpectedMatrix()
    {
        // Arrange
        var jacobian = new Jacobian();
        Func<double[], double>[] functions =
        {
            x => x[0] * x[0],
            x => x[1] * x[1]
        };
        double[] x = { 2.0, 3.0 };
        double[] currentValues = { 4.0, 9.0 };

        // Act
        var result = jacobian.Evaluate(functions, x, currentValues);

        // Assert
        Assert.Equal(2, result.Rows);
        Assert.Equal(2, result.Columns);
    }


    [Fact]
    public void Evaluate_WithNullFunctionsArray_ThrowsArgumentNullException()
    {
        // Arrange
        var jacobian = new Jacobian();
        Func<double[], double>[] functions = null;
        double[] x = { 2.0, 3.0 };
        double[] currentValues = { 4.0, 9.0 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => jacobian.Evaluate(functions, x, currentValues));
    }

    [Fact]
    public void Evaluate_WithNullXArray_ThrowsArgumentNullException()
    {
        // Arrange
        var jacobian = new Jacobian();
        Func<double[], double>[] functions =
        {
            x => x[0] * x[0],
            x => x[1] * x[1]
        };
        double[] x = null;
        double[] currentValues = { 4.0, 9.0 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => jacobian.Evaluate(functions, x, currentValues));
    }

    [Fact]
    public void Evaluate_WithNullCurrentValuesArray_ThrowsArgumentNullException()
    {
        // Arrange
        var jacobian = new Jacobian();
        Func<double[], double>[] functions =
        {
            x => x[0] * x[0],
            x => x[1] * x[1]
        };
        double[] x = { 2.0, 3.0 };
        double[] currentValues = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => jacobian.Evaluate(functions, x, currentValues));
    }
}