using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

public class StatisticsAggregatorTests
{
    public StatisticsAggregatorTests()
    {
        SpiceAPI.Instance.LoadKernels(new System.IO.DirectoryInfo("Data/SolarSystem"));
    }

    private static PropagationSolution CreateSolution(double[][] states, CelestialBody observer)
    {
        var solution = new PropagationSolution();
        var svList = new StateVector[states.Length];
        for (int i = 0; i < states.Length; i++)
        {
            var s = states[i];
            svList[i] = new StateVector(
                new Vector3(s[0], s[1], s[2]),
                new Vector3(s[3], s[4], s[5]),
                observer, TimeSystem.Time.J2000TDB.AddSeconds(i * 60), Frames.Frame.ICRF);
        }

        solution.SetOutputStates(svList);
        return solution;
    }

    private static CelestialBody Earth => new CelestialBody(PlanetsAndMoons.EARTH);

    [Fact]
    public void MeanPosition_ComputedCorrectly()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 10, 20, 30, 1, 2, 3 } }, earth),
            CreateSolution(new[] { new double[] { 20, 40, 60, 4, 5, 6 } }, earth),
            CreateSolution(new[] { new double[] { 30, 60, 90, 7, 8, 9 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Single(stats);
        Assert.Equal(20.0, stats[0].MeanPosition.X, 10);
        Assert.Equal(40.0, stats[0].MeanPosition.Y, 10);
        Assert.Equal(60.0, stats[0].MeanPosition.Z, 10);
    }

    [Fact]
    public void MeanVelocity_ComputedCorrectly()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 10, 20, 30, 1, 2, 3 } }, earth),
            CreateSolution(new[] { new double[] { 20, 40, 60, 4, 5, 6 } }, earth),
            CreateSolution(new[] { new double[] { 30, 60, 90, 7, 8, 9 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(4.0, stats[0].MeanVelocity.X, 10);
        Assert.Equal(5.0, stats[0].MeanVelocity.Y, 10);
        Assert.Equal(6.0, stats[0].MeanVelocity.Z, 10);
    }

    [Fact]
    public void SampleCovariance_KnownData()
    {
        // 3 samples: [1,0,0,0,0,0], [0,1,0,0,0,0], [0,0,1,0,0,0]
        // Mean = [1/3, 1/3, 1/3, 0, 0, 0]
        // Bessel-corrected variance for X: sum of (xi-mean)^2 / (3-1)
        // deviations: [2/3, -1/3, -1/3] → sum of sq = 4/9+1/9+1/9 = 6/9 → / 2 = 3/9 = 1/3
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 1, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 0, 1, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 0, 0, 1, 0, 0, 0 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        // Variance for each position component: 1/3
        Assert.Equal(1.0 / 3.0, stats[0].SampleCovariance.Get(0, 0), 10);
        Assert.Equal(1.0 / 3.0, stats[0].SampleCovariance.Get(1, 1), 10);
        Assert.Equal(1.0 / 3.0, stats[0].SampleCovariance.Get(2, 2), 10);
        // Cross-covariance X-Y: sum of (xi-mx)(yi-my) / 2
        // = (2/3)(-1/3) + (-1/3)(2/3) + (-1/3)(-1/3) = -2/9 -2/9 +1/9 = -3/9 → / 2 = -1/6
        Assert.Equal(-1.0 / 6.0, stats[0].SampleCovariance.Get(0, 1), 10);
    }

    [Fact]
    public void StandardDeviation_MatchesCovarianceDiagonal()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 10, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 20, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 30, 0, 0, 0, 0, 0 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(System.Math.Sqrt(stats[0].SampleCovariance.Get(0, 0)), stats[0].StandardDeviation.Position.X, 10);
    }

    [Fact]
    public void MinMax_ComputedCorrectly()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 5, 20, 30, 1, 2, 3 } }, earth),
            CreateSolution(new[] { new double[] { 15, 10, 60, 4, 5, 6 } }, earth),
            CreateSolution(new[] { new double[] { 10, 30, 45, 7, 8, 9 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(5.0, stats[0].Min.Position.X);
        Assert.Equal(15.0, stats[0].Max.Position.X);
        Assert.Equal(10.0, stats[0].Min.Position.Y);
        Assert.Equal(30.0, stats[0].Max.Position.Y);
    }

    [Fact]
    public void RssPercentiles_MedianOfKnownData()
    {
        // 3 solutions with position deviations from mean
        // Mean: [10, 0, 0, ...]
        // deviations: [0, 0, 0], [10, 0, 0], [-10, 0, 0]
        // RSS pos: 0, 10, 10 → sorted: 0, 10, 10
        // Median (50th): ceil(0.5*3)-1 = 1 → sorted[1] = 10
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 10, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 20, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 0, 0, 0, 0, 0, 0 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        // 50th percentile
        Assert.Equal(10.0, stats[0].RssPositionPercentiles.P50, 5);
    }

    [Fact]
    public void RssPercentiles_99thPercentile()
    {
        // 100 solutions with predictable RSS values
        var earth = Earth;
        var solutions = new List<PropagationSolution>();
        for (int i = 0; i < 100; i++)
        {
            solutions.Add(CreateSolution(new[] { new double[] { i, 0, 0, 0, 0, 0 } }, earth));
        }

        var stats = StatisticsAggregator.Aggregate(solutions);

        // Mean X = 49.5, so deviations range from -49.5 to 49.5
        // RSS = |deviation| for 1D
        // 99th percentile: ceil(0.99*100)-1 = 98 → sorted[98]
        // The 99th percentile should be close to the maximum deviation
        Assert.True(stats[0].RssPositionPercentiles.P99 > stats[0].RssPositionPercentiles.P50,
            "99th percentile should exceed median");
    }

    [Fact]
    public void MultipleEpochs_ProducesCorrectCount()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[]
            {
                new double[] { 10, 0, 0, 0, 0, 0 },
                new double[] { 20, 0, 0, 0, 0, 0 },
                new double[] { 30, 0, 0, 0, 0, 0 }
            }, earth),
            CreateSolution(new[]
            {
                new double[] { 11, 0, 0, 0, 0, 0 },
                new double[] { 21, 0, 0, 0, 0, 0 },
                new double[] { 31, 0, 0, 0, 0, 0 }
            }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(3, stats.Count);
    }

    [Fact]
    public void CovarianceMatrix_IsSymmetric()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 1, 4, 7, 2, 5, 8 } }, earth),
            CreateSolution(new[] { new double[] { 3, 6, 9, 4, 7, 1 } }, earth),
            CreateSolution(new[] { new double[] { 5, 2, 3, 6, 1, 4 } }, earth),
            CreateSolution(new[] { new double[] { 7, 8, 1, 3, 9, 2 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        for (int i = 0; i < 6; i++)
            for (int j = i + 1; j < 6; j++)
                Assert.Equal(stats[0].SampleCovariance.Get(i, j),
                    stats[0].SampleCovariance.Get(j, i), 12);
    }

    [Fact]
    public void CovarianceDiagonal_IsNonNegative()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 1, 2, 3, 4, 5, 6 } }, earth),
            CreateSolution(new[] { new double[] { 1, 2, 3, 4, 5, 6 } }, earth), // identical → zero variance
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        for (int i = 0; i < 6; i++)
            Assert.True(stats[0].SampleCovariance.Get(i, i) >= 0,
                $"Covariance diagonal [{i},{i}] = {stats[0].SampleCovariance.Get(i, i)} must be >= 0");
    }

    [Fact]
    public void BesselCorrection_ExactValuesForTwoSamples()
    {
        // Two samples: X = {a, b}. Mean = (a+b)/2.
        // Bessel-corrected variance = (a-mean)² + (b-mean)² / (2-1) = (b-a)²/2
        double a = 3.0, b = 7.0;
        double expectedVariance = (b - a) * (b - a) / 2.0; // (4²)/2 = 8.0

        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { a, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { b, 0, 0, 0, 0, 0 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(expectedVariance, stats[0].SampleCovariance.Get(0, 0), 12);
        Assert.Equal(System.Math.Sqrt(expectedVariance), stats[0].StandardDeviation.Position.X, 12);
    }

    [Fact]
    public void StandardDeviation_AllSixComponents_ExactValues()
    {
        // 4 samples with known values for all 6 components
        // X: {2, 4, 6, 8} → mean=5, Bessel var = Σ(xi-5)²/3 = (9+1+1+9)/3 = 20/3
        // VX: {1, 3, 5, 7} → mean=4, Bessel var = (9+1+1+9)/3 = 20/3
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 2, 10, 100, 1, 10, 100 } }, earth),
            CreateSolution(new[] { new double[] { 4, 20, 200, 3, 20, 200 } }, earth),
            CreateSolution(new[] { new double[] { 6, 30, 300, 5, 30, 300 } }, earth),
            CreateSolution(new[] { new double[] { 8, 40, 400, 7, 40, 400 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        // X: {2,4,6,8} mean=5 → Σ(xi-5)²=9+1+1+9=20 → var=20/3
        Assert.Equal(System.Math.Sqrt(20.0 / 3.0), stats[0].StandardDeviation.Position.X, 10);
        // Y: {10,20,30,40} mean=25 → Σ(yi-25)²=225+25+25+225=500 → var=500/3
        Assert.Equal(System.Math.Sqrt(500.0 / 3.0), stats[0].StandardDeviation.Position.Y, 10);
        // Z: {100,200,300,400} mean=250 → Σ(zi-250)²=22500+2500+2500+22500=50000 → var=50000/3
        Assert.Equal(System.Math.Sqrt(50000.0 / 3.0), stats[0].StandardDeviation.Position.Z, 8);

        // VX: {1,3,5,7} mean=4 → var=20/3
        Assert.Equal(System.Math.Sqrt(20.0 / 3.0), stats[0].StandardDeviation.Velocity.X, 10);
        // VY: {10,20,30,40} mean=25 → var=500/3
        Assert.Equal(System.Math.Sqrt(500.0 / 3.0), stats[0].StandardDeviation.Velocity.Y, 10);
        // VZ: {100,200,300,400} mean=250 → var=50000/3
        Assert.Equal(System.Math.Sqrt(50000.0 / 3.0), stats[0].StandardDeviation.Velocity.Z, 8);
    }

    [Fact]
    public void MinMax_VelocityComponents_ComputedCorrectly()
    {
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 0, 0, 0, 10, 20, 30 } }, earth),
            CreateSolution(new[] { new double[] { 0, 0, 0, -5, 50, 15 } }, earth),
            CreateSolution(new[] { new double[] { 0, 0, 0, 25, -10, 45 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(-5.0, stats[0].Min.Velocity.X);
        Assert.Equal(25.0, stats[0].Max.Velocity.X);
        Assert.Equal(-10.0, stats[0].Min.Velocity.Y);
        Assert.Equal(50.0, stats[0].Max.Velocity.Y);
        Assert.Equal(15.0, stats[0].Min.Velocity.Z);
        Assert.Equal(45.0, stats[0].Max.Velocity.Z);
    }

    [Fact]
    public void RssPercentiles_ExactNearestRank_10Samples()
    {
        // 10 solutions along X-axis: positions 0..9, mean = 4.5
        // Deviations: -4.5, -3.5, -2.5, -1.5, -0.5, 0.5, 1.5, 2.5, 3.5, 4.5
        // RSS (1D = |dev|) sorted: 0.5, 0.5, 1.5, 1.5, 2.5, 2.5, 3.5, 3.5, 4.5, 4.5
        // P50: ceil(0.50*10)-1 = 4 → sorted[4] = 2.5
        // P95: ceil(0.95*10)-1 = 9 → sorted[9] = 4.5
        // P99: ceil(0.99*10)-1 = 9 → sorted[9] = 4.5
        var earth = Earth;
        var solutions = new List<PropagationSolution>();
        for (int i = 0; i < 10; i++)
            solutions.Add(CreateSolution(new[] { new double[] { i, 0, 0, 0, 0, 0 } }, earth));

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(2.5, stats[0].RssPositionPercentiles.P50, 10);
        Assert.Equal(4.5, stats[0].RssPositionPercentiles.P95, 10);
        Assert.Equal(4.5, stats[0].RssPositionPercentiles.P99, 10);
    }

    [Fact]
    public void RssPercentiles_Velocity_ExactValues()
    {
        // 5 solutions varying only VX: {0, 10, 20, 30, 40}, mean = 20
        // Deviations: -20, -10, 0, 10, 20. RSS sorted: 0, 10, 10, 20, 20
        // P50: ceil(0.50*5)-1 = 2 → sorted[2] = 10
        // P95: ceil(0.95*5)-1 = 4 → sorted[4] = 20
        // P99: ceil(0.99*5)-1 = 4 → sorted[4] = 20
        var earth = Earth;
        var solutions = new List<PropagationSolution>();
        for (int i = 0; i < 5; i++)
            solutions.Add(CreateSolution(new[] { new double[] { 0, 0, 0, i * 10, 0, 0 } }, earth));

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(10.0, stats[0].RssVelocityPercentiles.P50, 10);
        Assert.Equal(20.0, stats[0].RssVelocityPercentiles.P95, 10);
        Assert.Equal(20.0, stats[0].RssVelocityPercentiles.P99, 10);
    }

    [Fact]
    public void Rss3D_PositionPercentile_UsesEuclideanNorm()
    {
        // 3 solutions with 3D deviations from mean (1,1,1):
        //   (0,0,0) → dev=(-1,-1,-1) → RSS = √3
        //   (1,1,1) → dev=(0,0,0)    → RSS = 0
        //   (2,2,2) → dev=(1,1,1)    → RSS = √3
        // Sorted RSS: 0, √3, √3
        // P50: ceil(0.5*3)-1 = 1 → sorted[1] = √3
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 0, 0, 0, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 1, 1, 1, 0, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 2, 2, 2, 0, 0, 0 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(System.Math.Sqrt(3.0), stats[0].RssPositionPercentiles.P50, 10);
    }

    [Fact]
    public void CrossCovariance_ExactHandComputed()
    {
        // 4 samples: X and VX are linearly related (VX = 2*X)
        // X:  {1, 2, 3, 4}, mean=2.5
        // VX: {2, 4, 6, 8}, mean=5
        // Cov(X,VX) = Σ(Xi-2.5)(VXi-5)/(4-1) = ((-1.5)(-3)+(-0.5)(-1)+(0.5)(1)+(1.5)(3))/3
        //           = (4.5+0.5+0.5+4.5)/3 = 10/3
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 1, 0, 0, 2, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 2, 0, 0, 4, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 3, 0, 0, 6, 0, 0 } }, earth),
            CreateSolution(new[] { new double[] { 4, 0, 0, 8, 0, 0 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        // Cov[0,3] = Cov(X, VX) = 10/3
        Assert.Equal(10.0 / 3.0, stats[0].SampleCovariance.Get(0, 3), 10);
        // Symmetry
        Assert.Equal(10.0 / 3.0, stats[0].SampleCovariance.Get(3, 0), 10);
    }

    [Fact]
    public void IdenticalSolutions_ZeroDispersion()
    {
        // All solutions identical → zero variance, zero std dev, zero RSS
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 100, 200, 300, 10, 20, 30 } }, earth),
            CreateSolution(new[] { new double[] { 100, 200, 300, 10, 20, 30 } }, earth),
            CreateSolution(new[] { new double[] { 100, 200, 300, 10, 20, 30 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        Assert.Equal(0.0, stats[0].StandardDeviation.Position.X, 12);
        Assert.Equal(0.0, stats[0].StandardDeviation.Position.Y, 12);
        Assert.Equal(0.0, stats[0].StandardDeviation.Position.Z, 12);
        Assert.Equal(0.0, stats[0].StandardDeviation.Velocity.X, 12);
        Assert.Equal(0.0, stats[0].StandardDeviation.Velocity.Y, 12);
        Assert.Equal(0.0, stats[0].StandardDeviation.Velocity.Z, 12);

        Assert.Equal(0.0, stats[0].RssPositionPercentiles.P50, 12);
        Assert.Equal(0.0, stats[0].RssPositionPercentiles.P95, 12);
        Assert.Equal(0.0, stats[0].RssPositionPercentiles.P99, 12);
        Assert.Equal(0.0, stats[0].RssVelocityPercentiles.P50, 12);
    }

    [Fact]
    public void VarianceDecomposition_TraceEqualsSumOfComponentVariances()
    {
        // Trace of covariance = sum of variances = σ²_x + σ²_y + σ²_z + σ²_vx + σ²_vy + σ²_vz
        var earth = Earth;
        var solutions = new List<PropagationSolution>
        {
            CreateSolution(new[] { new double[] { 1, 5, 9, 2, 6, 3 } }, earth),
            CreateSolution(new[] { new double[] { 3, 7, 11, 4, 8, 1 } }, earth),
            CreateSolution(new[] { new double[] { 5, 3, 7, 6, 2, 5 } }, earth),
            CreateSolution(new[] { new double[] { 7, 1, 5, 8, 4, 7 } }, earth),
            CreateSolution(new[] { new double[] { 9, 9, 3, 10, 10, 9 } }, earth),
        };

        var stats = StatisticsAggregator.Aggregate(solutions);

        double trace = 0;
        double sumStdSq = 0;
        for (int i = 0; i < 6; i++)
        {
            trace += stats[0].SampleCovariance.Get(i, i);
        }

        var std = stats[0].StandardDeviation;
        sumStdSq += std.Position.X * std.Position.X;
        sumStdSq += std.Position.Y * std.Position.Y;
        sumStdSq += std.Position.Z * std.Position.Z;
        sumStdSq += std.Velocity.X * std.Velocity.X;
        sumStdSq += std.Velocity.Y * std.Velocity.Y;
        sumStdSq += std.Velocity.Z * std.Velocity.Z;

        Assert.Equal(trace, sumStdSq, 10);
    }
}
