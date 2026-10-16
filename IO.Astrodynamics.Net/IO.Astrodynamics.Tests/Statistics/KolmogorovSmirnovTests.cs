// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using MathNet.Numerics.Distributions;
using Xunit;

namespace IO.Astrodynamics.Tests.Statistics;

public class KolmogorovSmirnovTests
{
    [Theory]
    [InlineData(1e-3)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    [InlineData(6.0)]
    [InlineData(12.6)]
    [InlineData(40.0)]
    public void ChiSquared6Cdf_MatchesMathNet(double x)
    {
        Assert.Equal(new ChiSquared(6).CumulativeDistribution(x), KolmogorovSmirnov.ChiSquared6Cdf(x), 1e-14);
    }

    [Fact]
    public void ChiSquared6Cdf_IsZeroBelowZero()
    {
        Assert.Equal(0.0, KolmogorovSmirnov.ChiSquared6Cdf(-1.0));
        Assert.Equal(0.0, KolmogorovSmirnov.ChiSquared6Cdf(0.0));
    }

    [Theory]
    [InlineData(1.358, 0.05)]
    [InlineData(1.628, 0.01)]
    public void Q_AtTheCriticalValuesOfStephens_IsTheSignificance(double lambda, double alpha)
    {
        // Stephens (1974), table 1A, case 0: the critical values are given to three decimals, so 2e-4 on α
        Assert.Equal(alpha, KolmogorovSmirnov.Q(lambda), 2e-4);
    }

    [Fact]
    public void Q_TendsToOneAtZeroAndToZeroAtInfinity()
    {
        Assert.Equal(1.0, KolmogorovSmirnov.Q(0.0));
        Assert.Equal(1.0, KolmogorovSmirnov.Q(0.1), 1e-12);
        Assert.Equal(0.0, KolmogorovSmirnov.Q(10.0), 1e-40);
    }

    [Theory]
    [InlineData(0.002)]
    [InlineData(0.005)]
    [InlineData(0.01)]
    [InlineData(0.05)]
    public void Q_ForASmallLambda_IsOne(double lambda)
    {
        // The alternating series, summed in 100 terms, gave 0.078, 0.40 and 0.87 at the first three
        Assert.Equal(1.0, KolmogorovSmirnov.Q(lambda));
    }

    [Fact]
    public void Q_IsContinuousAtTheCutoff()
    {
        Assert.Equal(1.0, KolmogorovSmirnov.Q(0.2), 1e-12);
    }

    [Fact]
    public void Statistic_OfOnePoint_IsTheLargerGapOfTheStep()
    {
        // F(0.3) = 0.3 for the uniform distribution: the empirical CDF jumps from 0 to 1 there
        Assert.Equal(0.7, KolmogorovSmirnov.Statistic(new[] { 0.3 }, x => x), 1e-15);
    }

    [Fact]
    public void Statistic_OfAnEmptySample_Throws()
    {
        Assert.Throws<ArgumentException>(() => KolmogorovSmirnov.Statistic(Array.Empty<double>(), x => x));
    }

    [Fact]
    public void ChiSquared6Sample_IsAccepted_AndScaledByOnePointOne_IsRejected()
    {
        // Arrange: 5000 sums of 6 squared standard normals (MathNet), the size of the Monte Carlo of F3
        var normal = new Normal(0.0, 1.0, new Random(20261016));
        var sample = Enumerable.Range(0, 5000).Select(_ => Enumerable.Range(0, 6).Sum(_ => System.Math.Pow(normal.Sample(), 2)))
            .ToArray();

        // Act
        double accepted = KolmogorovSmirnov.PValue(KolmogorovSmirnov.Statistic(sample, KolmogorovSmirnov.ChiSquared6Cdf),
            sample.Length);
        double rejected = KolmogorovSmirnov.PValue(
            KolmogorovSmirnov.Statistic(sample.Select(x => 1.1 * x), KolmogorovSmirnov.ChiSquared6Cdf), sample.Length);

        // Assert
        Assert.True(accepted > 0.01, $"χ²(6) sample: p = {accepted:E2}");
        Assert.True(rejected < 0.01, $"χ²(6) sample scaled by 1.1: p = {rejected:E2}");
    }
}
