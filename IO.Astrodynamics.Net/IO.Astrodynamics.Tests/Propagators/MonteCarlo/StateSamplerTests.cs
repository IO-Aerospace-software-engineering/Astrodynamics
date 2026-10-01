using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

public class StateSamplerTests
{
    public StateSamplerTests()
    {
        SpiceAPI.Instance.LoadKernels(new System.IO.DirectoryInfo("Data/SolarSystem"));
    }

    private static StateVector CreateNominalWithCovariance(Matrix covariance)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double r0 = 6800000.0;
        double v0 = System.Math.Sqrt(earth.GM / r0);
        return new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, covariance);
    }

    private static Matrix CreateDiagonalCovariance()
    {
        var cov = new Matrix(6, 6);
        cov.Set(0, 0, 100.0);   // 10m position sigma
        cov.Set(1, 1, 100.0);
        cov.Set(2, 2, 100.0);
        cov.Set(3, 3, 0.01);    // 0.1 m/s velocity sigma
        cov.Set(4, 4, 0.01);
        cov.Set(5, 5, 0.01);
        return cov;
    }

    [Fact]
    public void SampleCount_ReturnsRequestedNumber()
    {
        var nominal = CreateNominalWithCovariance(CreateDiagonalCovariance());
        var samples = StateSampler.Sample(nominal, 50, seed: 42);

        Assert.Equal(50, samples.Length);
    }

    [Fact]
    public void SampleMean_ConvergesToNominal()
    {
        var nominal = CreateNominalWithCovariance(CreateDiagonalCovariance());
        int n = 100_000;
        var samples = StateSampler.Sample(nominal, n, seed: 123);

        double meanPx = samples.Average(s => s.Position.X);
        double meanPy = samples.Average(s => s.Position.Y);
        double meanPz = samples.Average(s => s.Position.Z);
        double meanVx = samples.Average(s => s.Velocity.X);
        double meanVy = samples.Average(s => s.Velocity.Y);
        double meanVz = samples.Average(s => s.Velocity.Z);

        // With sigma=10m, mean of 100k samples should converge to ~0.1m of nominal
        Assert.Equal(nominal.Position.X, meanPx, 0.5);
        Assert.Equal(nominal.Position.Y, meanPy, 0.5);
        Assert.Equal(nominal.Position.Z, meanPz, 0.5);
        Assert.Equal(nominal.Velocity.X, meanVx, 0.005);
        Assert.Equal(nominal.Velocity.Y, meanVy, 0.005);
        Assert.Equal(nominal.Velocity.Z, meanVz, 0.005);
    }

    [Fact]
    public void SampleCovariance_ConvergesToInput()
    {
        var inputCov = CreateDiagonalCovariance();
        var nominal = CreateNominalWithCovariance(inputCov);
        int n = 100_000;
        var samples = StateSampler.Sample(nominal, n, seed: 456);

        // Compute sample variance for each component
        double[] means = new double[6];
        for (int i = 0; i < n; i++)
        {
            var arr = samples[i].ToArray();
            var nomArr = nominal.ToArray();
            for (int j = 0; j < 6; j++)
                means[j] += (arr[j] - nomArr[j]);
        }
        for (int j = 0; j < 6; j++) means[j] /= n;

        double[] variances = new double[6];
        for (int i = 0; i < n; i++)
        {
            var arr = samples[i].ToArray();
            var nomArr = nominal.ToArray();
            for (int j = 0; j < 6; j++)
            {
                double diff = (arr[j] - nomArr[j]) - means[j];
                variances[j] += diff * diff;
            }
        }
        for (int j = 0; j < 6; j++) variances[j] /= (n - 1);

        // Position variances should be ~100 m²
        Assert.Equal(100.0, variances[0], 3.0);
        Assert.Equal(100.0, variances[1], 3.0);
        Assert.Equal(100.0, variances[2], 3.0);
        // Velocity variances should be ~0.01 m²/s²
        Assert.Equal(0.01, variances[3], 0.001);
        Assert.Equal(0.01, variances[4], 0.001);
        Assert.Equal(0.01, variances[5], 0.001);
    }

    [Fact]
    public void SameSeed_ProducesSameSamples()
    {
        var nominal = CreateNominalWithCovariance(CreateDiagonalCovariance());
        var samples1 = StateSampler.Sample(nominal, 10, seed: 42);
        var samples2 = StateSampler.Sample(nominal, 10, seed: 42);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(samples1[i].Position.X, samples2[i].Position.X);
            Assert.Equal(samples1[i].Position.Y, samples2[i].Position.Y);
            Assert.Equal(samples1[i].Position.Z, samples2[i].Position.Z);
            Assert.Equal(samples1[i].Velocity.X, samples2[i].Velocity.X);
            Assert.Equal(samples1[i].Velocity.Y, samples2[i].Velocity.Y);
            Assert.Equal(samples1[i].Velocity.Z, samples2[i].Velocity.Z);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSamples()
    {
        var nominal = CreateNominalWithCovariance(CreateDiagonalCovariance());
        var samples1 = StateSampler.Sample(nominal, 10, seed: 42);
        var samples2 = StateSampler.Sample(nominal, 10, seed: 99);

        bool anyDifferent = false;
        for (int i = 0; i < 10; i++)
        {
            if (samples1[i].Position.X != samples2[i].Position.X)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent);
    }

    [Fact]
    public void SamplesPreserveObserverEpochFrame()
    {
        var nominal = CreateNominalWithCovariance(CreateDiagonalCovariance());
        var samples = StateSampler.Sample(nominal, 5, seed: 42);

        foreach (var sample in samples)
        {
            Assert.Equal(nominal.Observer.NaifId, sample.Observer.NaifId);
            Assert.Equal(nominal.Epoch, sample.Epoch);
            Assert.Equal(nominal.Frame, sample.Frame);
            Assert.Null(sample.Covariance);
        }
    }

    [Fact]
    public void DiagonalCovariance_IndependentComponents()
    {
        var inputCov = CreateDiagonalCovariance();
        var nominal = CreateNominalWithCovariance(inputCov);
        int n = 100_000;
        var samples = StateSampler.Sample(nominal, n, seed: 789);

        // Compute cross-correlation between X position and Y position
        var nomArr = nominal.ToArray();
        double meanDx = 0, meanDy = 0;
        for (int i = 0; i < n; i++)
        {
            meanDx += samples[i].Position.X - nomArr[0];
            meanDy += samples[i].Position.Y - nomArr[1];
        }
        meanDx /= n;
        meanDy /= n;

        double crossCorr = 0;
        double varX = 0, varY = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = (samples[i].Position.X - nomArr[0]) - meanDx;
            double dy = (samples[i].Position.Y - nomArr[1]) - meanDy;
            crossCorr += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        double correlation = crossCorr / System.Math.Sqrt(varX * varY);

        // Correlation should be near 0 for diagonal covariance
        Assert.True(System.Math.Abs(correlation) < 0.0015,
            $"Cross-correlation {correlation} too high for diagonal covariance");
    }

    [Fact]
    public void BoxMuller_SkewnessAndKurtosis_MatchStandardNormal()
    {
        // Standard normal: skewness = 0, excess kurtosis = 0 (kurtosis = 3)
        var inputCov = CreateDiagonalCovariance(); // sigma_x = 10 m
        var nominal = CreateNominalWithCovariance(inputCov);
        int n = 200_000;
        var samples = StateSampler.Sample(nominal, n, seed: 314);

        // Normalize deviations to standard normal
        double sigma = System.Math.Sqrt(100.0); // 10 m
        double mean = 0;
        double m2 = 0, m3 = 0, m4 = 0;
        for (int i = 0; i < n; i++)
        {
            double z = (samples[i].Position.X - nominal.Position.X) / sigma;
            mean += z;
        }
        mean /= n;

        for (int i = 0; i < n; i++)
        {
            double z = (samples[i].Position.X - nominal.Position.X) / sigma - mean;
            double z2 = z * z;
            m2 += z2;
            m3 += z2 * z;
            m4 += z2 * z2;
        }
        m2 /= n;
        m3 /= n;
        m4 /= n;

        double skewness = m3 / System.Math.Pow(m2, 1.5);
        double kurtosis = m4 / (m2 * m2); // Should be 3.0 for normal

        // With 200k samples, sampling error is small
        Assert.True(System.Math.Abs(skewness) < 0.02,
            $"Skewness {skewness:F4} deviates from 0 (expected for normal distribution)");
        Assert.True(System.Math.Abs(kurtosis - 3.0) < 0.05,
            $"Kurtosis {kurtosis:F4} deviates from 3.0 (expected for normal distribution)");
    }

    [Fact]
    public void CorrelatedCovariance_ProducesCorrectCorrelation()
    {
        // Build covariance with known correlation ρ = 0.8 between X and Y
        // σ_x = 10 m, σ_y = 20 m → Cov(X,Y) = ρ * σ_x * σ_y = 0.8 * 10 * 20 = 160
        double sigmaX = 10.0, sigmaY = 20.0, rho = 0.8;
        var cov = new Matrix(6, 6);
        cov.Set(0, 0, sigmaX * sigmaX);       // 100
        cov.Set(1, 1, sigmaY * sigmaY);       // 400
        cov.Set(0, 1, rho * sigmaX * sigmaY); // 160
        cov.Set(1, 0, rho * sigmaX * sigmaY); // 160
        cov.Set(2, 2, 1.0);
        cov.Set(3, 3, 0.01);
        cov.Set(4, 4, 0.01);
        cov.Set(5, 5, 0.01);

        var nominal = CreateNominalWithCovariance(cov);
        int n = 100_000;
        var samples = StateSampler.Sample(nominal, n, seed: 271);

        // Compute sample Pearson correlation
        double sumDxDy = 0, sumDx2 = 0, sumDy2 = 0;
        double meanDx = samples.Average(s => s.Position.X) - nominal.Position.X;
        double meanDy = samples.Average(s => s.Position.Y) - nominal.Position.Y;
        for (int i = 0; i < n; i++)
        {
            double dx = (samples[i].Position.X - nominal.Position.X) - meanDx;
            double dy = (samples[i].Position.Y - nominal.Position.Y) - meanDy;
            sumDxDy += dx * dy;
            sumDx2 += dx * dx;
            sumDy2 += dy * dy;
        }

        double sampleCorrelation = sumDxDy / System.Math.Sqrt(sumDx2 * sumDy2);

        // With 100k samples, sample correlation should be within ~0.01 of true ρ
        Assert.True(System.Math.Abs(sampleCorrelation - rho) < 0.015,
            $"Sample correlation {sampleCorrelation:F4} deviates from expected ρ={rho}");
    }

    [Fact]
    public void MahalanobisDistance_FollowsChiSquared6()
    {
        // For multivariate normal N(μ, Σ), the squared Mahalanobis distance
        // D² = (x-μ)ᵀ Σ⁻¹ (x-μ) follows χ²(6).
        // P(D² ≤ 12.592) = 0.95 for χ²(6) — the 95th percentile critical value.
        var inputCov = CreateDiagonalCovariance();
        var nominal = CreateNominalWithCovariance(inputCov);
        int n = 50_000;
        var samples = StateSampler.Sample(nominal, n, seed: 161);

        var covInverse = inputCov.Inverse();
        int withinCritical = 0;
        double chi2_95 = 12.592; // χ²(6) 95th percentile

        for (int i = 0; i < n; i++)
        {
            double[] delta =
            [
                samples[i].Position.X - nominal.Position.X,
                samples[i].Position.Y - nominal.Position.Y,
                samples[i].Position.Z - nominal.Position.Z,
                samples[i].Velocity.X - nominal.Velocity.X,
                samples[i].Velocity.Y - nominal.Velocity.Y,
                samples[i].Velocity.Z - nominal.Velocity.Z
            ];

            var tmp = covInverse.Multiply(delta);
            double d2 = 0;
            for (int j = 0; j < 6; j++)
                d2 += delta[j] * tmp[j];

            if (d2 <= chi2_95) withinCritical++;
        }

        double fraction = (double)withinCritical / n;

        // Should be ~0.95; allow ±0.01 for sampling noise
        Assert.True(System.Math.Abs(fraction - 0.95) < 0.01,
            $"Fraction within χ²(6) 95% critical value: {fraction:F4} (expected ~0.95)");
    }

    [Fact]
    public void IdentityCovariance_SigmaEqualsOne()
    {
        // With identity covariance, each component should have σ = 1
        var cov = new Matrix(6, 6);
        for (int i = 0; i < 6; i++) cov.Set(i, i, 1.0);

        var nominal = CreateNominalWithCovariance(cov);
        int n = 100_000;
        var samples = StateSampler.Sample(nominal, n, seed: 555);

        for (int comp = 0; comp < 6; comp++)
        {
            double mean = 0;
            for (int i = 0; i < n; i++)
            {
                var arr = samples[i].ToArray();
                var nomArr = nominal.ToArray();
                mean += arr[comp] - nomArr[comp];
            }
            mean /= n;

            double variance = 0;
            for (int i = 0; i < n; i++)
            {
                var arr = samples[i].ToArray();
                var nomArr = nominal.ToArray();
                double d = (arr[comp] - nomArr[comp]) - mean;
                variance += d * d;
            }
            variance /= (n - 1);

            Assert.True(System.Math.Abs(variance - 1.0) < 0.02,
                $"Component {comp}: variance {variance:F4} deviates from 1.0");
        }
    }
}
