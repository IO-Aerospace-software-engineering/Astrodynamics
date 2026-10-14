// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using MathNet.Numerics.LinearAlgebra;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The covariance from the variational values (C2, C4), against the covariance of the linear map computed by MathNet:
/// P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q and M diag(P0, Pp) Mᵀ + diag(Q, 0), M = [[Φ, Ψc], [0, I]].
/// </summary>
public class CovarianceModelTests
{
    private static readonly ConsiderParameters BothSigmas = new(0.1, 0.05);

    [Fact]
    public void Covariance_IsTheCovarianceOfTheLinearMap_Symmetrized()
    {
        // Arrange: Cd and Cr in Ψ, both consider parameters
        var (y, q, p0) = Values(8, 1);
        var options = new VariationalOptions(Parameters(8), PropagateCovariance: true, ConsiderParameters: BothSigmas);
        var model = new CovarianceModel(p0, options);
        var covariance = new double[36];

        // Act
        model.Covariance(y, q, covariance);

        // Assert
        var expected = Extended(y, q, p0, 8, new[] { 6, 7 }, new[] { 0.01, 0.0025 }).SubMatrix(0, 6, 0, 6);
        double error = CovarianceAssert.RelativeError(covariance, CovarianceAssert.ToArray(expected));
        Assert.True(error < 1e-14, $"{error:E2}");
        CovarianceAssert.SymmetricPositiveSemiDefinite(covariance, 6, "P");
    }

    [Fact]
    public void ExtendedCovariance_IsTheCovarianceOfTheStateAndTheConsiderParameters()
    {
        // Arrange: Cd and Cr in Ψ, Cr alone a consider parameter
        var (y, q, p0) = Values(8, 2);
        var options = new VariationalOptions(Parameters(8), PropagateCovariance: true,
            ConsiderParameters: new ConsiderParameters(reflectivityCoefficientSigma: 0.2));
        var model = new CovarianceModel(p0, options);
        var extended = new double[49];

        // Act
        model.ExtendedCovariance(y, q, extended);

        // Assert
        Assert.Equal(7, model.ExtendedDimension);
        Assert.Equal(1, model.ConsiderCount);
        var expected = Extended(y, q, p0, 8, new[] { 7 }, new[] { 0.04 });
        double error = CovarianceAssert.RelativeError(extended, CovarianceAssert.ToArray(expected));
        Assert.True(error < 1e-14, $"{error:E2}");
        CovarianceAssert.SymmetricPositiveSemiDefinite(extended, 7, "extended covariance");
        var p = new double[36];
        model.Covariance(y, q, p);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(p[6 * i + j], extended[7 * i + j]);
            }
        }
    }

    [Fact]
    public void WithoutConsiderParameters_PIsPhiP0PhiTPlusQ_AndTheExtendedCovarianceIsP()
    {
        // Arrange: Cd in Ψ, but no consider parameter, so Ψ does not enter P
        var (y, q, p0) = Values(7, 3);
        var model = new CovarianceModel(p0, new VariationalOptions(ForceParameters.DragCoefficient,
            PropagateCovariance: true));
        var covariance = new double[36];
        var extended = new double[36];

        // Act
        model.Covariance(y, q, covariance);
        model.ExtendedCovariance(y, q, extended);

        // Assert
        var expected = Extended(y, q, p0, 7, Array.Empty<int>(), Array.Empty<double>());
        Assert.True(CovarianceAssert.RelativeError(covariance, CovarianceAssert.ToArray(expected)) < 1e-14);
        Assert.Equal(covariance, extended);
        Assert.Equal(6, model.ExtendedDimension);
    }

    [Fact]
    public void ConsiderParametersAndBuffers_AreChecked()
    {
        // Arrange
        var p0 = Values(6, 4).P0;

        // Act and assert: a consider parameter needs its column of Ψ
        var exception = Assert.Throws<ArgumentException>(() => new CovarianceModel(p0,
            new VariationalOptions(ForceParameters.DragCoefficient, PropagateCovariance: true,
                ConsiderParameters: new ConsiderParameters(reflectivityCoefficientSigma: 0.1))));
        Assert.Equal("options", exception.ParamName);
        Assert.Contains("column of Ψ", exception.Message);
        Assert.Throws<ArgumentException>(() => new CovarianceModel(new double[35], new VariationalOptions()));
        Assert.Throws<ArgumentNullException>(() => new CovarianceModel(p0, null));

        var model = new CovarianceModel(p0, new VariationalOptions(PropagateCovariance: true));
        Assert.Equal("y", Assert.Throws<ArgumentException>(() =>
            model.Covariance(new double[42], new double[21], new double[36])).ParamName);
        Assert.Equal("q", Assert.Throws<ArgumentException>(() =>
            model.Covariance(new double[36], Array.Empty<double>(), new double[36])).ParamName);
        Assert.Equal("covariance", Assert.Throws<ArgumentException>(() =>
            model.Covariance(new double[36], new double[21], new double[35])).ParamName);
        Assert.Equal("extended", Assert.Throws<ArgumentException>(() =>
            model.ExtendedCovariance(new double[36], new double[21], new double[49])).ParamName);
    }

    [Fact]
    public void ConsiderParameters_RejectNegativeOrNonFiniteSigmas()
    {
        Assert.Equal("dragCoefficientSigma",
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConsiderParameters(-0.1)).ParamName);
        Assert.Equal("reflectivityCoefficientSigma",
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConsiderParameters(null, double.NaN)).ParamName);
        Assert.Equal(ForceParameters.None, new ConsiderParameters().Parameters);
        Assert.Equal(ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient, BothSigmas.Parameters);
    }

    [Fact]
    public void CovarianceChecks_RejectAndNameEachDefect()
    {
        Assert.Contains("36 values", CovarianceChecks.Validate(new double[35], 6, "P0", out _));
        var nan = new double[9];
        nan[4] = double.NaN;
        Assert.Contains("finite", CovarianceChecks.Validate(nan, 3, "P0", out var none));
        Assert.Null(none);
        Assert.Contains("symmetric", CovarianceChecks.Validate(new[] { 1.0, 0.1, 0, 0.2, 1, 0, 0, 0, 1 }, 3, "P0",
            out _));
        Assert.Contains("positive semi-definite",
            CovarianceChecks.Validate(new[] { 1.0, 2, 0, 2, 1, 0, 0, 0, 1 }, 3, "P0", out _));
        Assert.Null(CovarianceChecks.Validate(Values(6, 5).P0, 6, "P0", out var symmetrized));
        Assert.Equal(36, symmetrized.Length);
    }

    private static ForceParameters Parameters(int columns) => columns switch
    {
        6 => ForceParameters.None,
        7 => ForceParameters.DragCoefficient,
        _ => ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient
    };

    // A Y of n columns, a Q and a P0 (both positive definite), seeded, with the magnitudes of a LEO case: Φ blocks of
    // 1, 1e3 s, 1e-3 /s and 1; P0 of 100 m and 0.1 m/s; Q of 1 m and 1 mm/s
    private static (double[] Y, double[] Q, double[] P0) Values(int columns, int seed)
    {
        var random = new Random(seed);
        var y = new double[6 * columns];
        for (int i = 0; i < 6; i++)
        {
            for (int c = 0; c < columns; c++)
            {
                double scale = c >= 6 ? 10.0 : i < 3 == c < 3 ? 1.0 : i < 3 ? 1e3 : 1e-3;
                y[i * columns + c] = scale * (2.0 * random.NextDouble() - 1.0) + (i == c ? 1.0 : 0.0);
            }
        }

        var p0 = PositiveDefinite(random, 100.0, 0.1);
        var full = PositiveDefinite(random, 1.0, 1e-3);
        var q = new double[21];
        for (int i = 0; i < 6; i++)
        {
            for (int j = i; j < 6; j++)
            {
                q[VariationalEquations.CovarianceIndex(i, j)] = full[6 * i + j];
            }
        }

        return (y, q, p0);
    }

    // S A Aᵀ S + diag(S²) / 10, S = diag(σr, σr, σr, σv, σv, σv)
    private static double[] PositiveDefinite(Random random, double sigmaPosition, double sigmaVelocity)
    {
        var a = Matrix<double>.Build.Dense(6, 6, (_, _) => random.NextDouble() - 0.5);
        var s = Matrix<double>.Build.DenseOfDiagonalArray(Enumerable.Range(0, 6)
            .Select(i => i < 3 ? sigmaPosition : sigmaVelocity).ToArray());
        var m = s * a * a.Transpose() * s + s * s / 10.0;
        return CovarianceAssert.ToArray((m + m.Transpose()) / 2.0);
    }

    // M diag(P0, Pp) Mᵀ + diag(Q, 0), M = [[Φ, Ψc], [0, I]], by MathNet
    private static Matrix<double> Extended(double[] y, double[] q, double[] p0, int columns, int[] considerColumns,
        double[] variances)
    {
        int c = considerColumns.Length;
        var m = Matrix<double>.Build.Dense(6 + c, 6 + c);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                m[i, j] = y[i * columns + j];
            }

            for (int k = 0; k < c; k++)
            {
                m[i, 6 + k] = y[i * columns + considerColumns[k]];
            }
        }

        for (int k = 0; k < c; k++)
        {
            m[6 + k, 6 + k] = 1.0;
        }

        var initial = Matrix<double>.Build.Dense(6 + c, 6 + c);
        initial.SetSubMatrix(0, 0, CovarianceAssert.ToMathNet(p0, 6));
        for (int k = 0; k < c; k++)
        {
            initial[6 + k, 6 + k] = variances[k];
        }

        var noise = Matrix<double>.Build.Dense(6 + c, 6 + c);
        noise.SetSubMatrix(0, 0, CovarianceAssert.ToMathNet(CovarianceAssert.Full(q), 6));
        return m * initial * m.Transpose() + noise;
    }
}
