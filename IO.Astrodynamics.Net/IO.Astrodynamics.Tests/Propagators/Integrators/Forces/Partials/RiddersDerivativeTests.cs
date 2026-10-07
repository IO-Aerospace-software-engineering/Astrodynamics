// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The reference derivative of the partials tests, checked against closed-form derivatives.
/// </summary>
public class RiddersDerivativeTests
{
    [Fact]
    public void Derivative_Sine_MatchesCosine()
    {
        // Act
        var (derivative, error) = RiddersDerivative.Derivative(System.Math.Sin, 0.7, 0.1);

        // Assert
        Assert.Equal(System.Math.Cos(0.7), derivative, 1e-13);
        Assert.True(error < 1e-12, $"error estimate {error:E2}");
    }

    [Fact]
    public void Derivative_Exponential_MatchesExponential()
    {
        // Act
        var (derivative, error) = RiddersDerivative.Derivative(System.Math.Exp, 1.3, 0.5);

        // Assert
        Assert.Equal(System.Math.Exp(1.3), derivative, 1e-12);
        Assert.True(error < 1e-11, $"error estimate {error:E2}");
    }

    [Fact]
    public void Derivative_InverseSquare_MatchesClosedForm()
    {
        // Arrange: the shape of a point-mass attraction, at the scale of a LEO radius
        const double r = 6778137.0;

        // Act
        var (derivative, error) = RiddersDerivative.Derivative(x => 1.0 / (x * x), r, 1e-3 * r);

        // Assert
        double expected = -2.0 / (r * r * r);
        Assert.Equal(1.0, derivative / expected, 1e-12);
        Assert.True(error < 1e-12 * System.Math.Abs(expected), $"error estimate {error:E2}");
    }

    [Fact]
    public void RelativeFrobeniusError_IsTheRatioOfNorms()
    {
        // Arrange
        double[] expected = { 3.0, 0.0, 4.0 };
        double[] actual = { 3.0, 0.5, 4.0 };

        // Act
        double error = RiddersDerivative.RelativeFrobeniusError(actual, expected);

        // Assert
        Assert.Equal(0.1, error, 1e-15);
    }
}
