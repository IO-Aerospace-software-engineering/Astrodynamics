// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

public class ConstantProcessNoiseTests
{
    [Fact]
    public void Accumulate_AddsQcToTheBuffer()
    {
        // Arrange
        var qc = new[] { 4.0, 1.0, 0.0, 1.0, 2.0, -0.5, 0.0, -0.5, 1.0 };
        var noise = new ConstantProcessNoise(qc);
        var buffer = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 };

        // Act
        noise.Accumulate(null, buffer);

        // Assert
        Assert.Equal(new[] { 5.0, 2.0, 1.0, 2.0, 3.0, 0.5, 1.0, 0.5, 2.0 }, buffer);
    }

    [Fact]
    public void Constructor_StoresQcSymmetrized()
    {
        // Arrange: asymmetric by 1e-14 relative, inside the tolerance
        var qc = new[] { 1.0, 0.5 + 1e-14, 0.0, 0.5, 1.0, 0.0, 0.0, 0.0, 1.0 };
        var noise = new ConstantProcessNoise(qc);
        var buffer = new double[9];

        // Act
        noise.Accumulate(null, buffer);

        // Assert
        Assert.Equal(buffer[1], buffer[3]);
    }

    [Fact]
    public void Constructor_AcceptsAPositiveSemiDefiniteQc()
    {
        // Arrange: no noise along the third axis
        var qc = new[] { 1.0, 0.0, 0.0, 0.0, 2.0, 0.0, 0.0, 0.0, 0.0 };

        // Act
        var noise = new ConstantProcessNoise(qc);

        // Assert
        Assert.NotNull(noise);
    }

    [Fact]
    public void Constructor_WithWrongLength_Throws()
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => new ConstantProcessNoise(new double[8]));

        // Assert
        Assert.Equal("qc", exception.ParamName);
        Assert.Contains("9 values", exception.Message);
    }

    [Fact]
    public void Constructor_WithAnAsymmetricQc_Throws()
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new ConstantProcessNoise(new[] { 1.0, 0.5, 0.0, 0.4, 1.0, 0.0, 0.0, 0.0, 1.0 }));

        // Assert
        Assert.Contains("symmetric", exception.Message);
    }

    [Fact]
    public void Constructor_WithANegativeEigenvalue_Throws()
    {
        // Arrange: eigenvalues 3 and −1 in the first two axes
        var qc = new[] { 1.0, 2.0, 0.0, 2.0, 1.0, 0.0, 0.0, 0.0, 1.0 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new ConstantProcessNoise(qc));

        // Assert
        Assert.Contains("positive semi-definite", exception.Message);
    }
}
