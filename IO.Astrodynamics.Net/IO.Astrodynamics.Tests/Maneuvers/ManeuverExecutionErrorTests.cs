// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Tests.Propagators.Variational;
using MathNet.Numerics.LinearAlgebra;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Maneuvers;

/// <summary>
/// The execution error of C5 against the model of Gates (JPL TR 32-504, 1963): the four terms of section III summed, the
/// same covariance in the frame of the thrust, and a Monte Carlo of the four error mechanisms of section II.
/// </summary>
public class ManeuverExecutionErrorTests
{
    private readonly ITestOutputHelper _output;

    public ManeuverExecutionErrorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // σ_r (m/s), σ_s, σ_a (m/s), σ_p (rad), and the commanded ΔV (m/s)
    public static TheoryData<double, double, double, double, double, double, double> Cases => new()
    {
        { 0.01, 0.01, 0.005, 0.005, 3.0, -4.0, 12.0 },
        { 0.0, 0.02, 0.0, 0.0, 100.0, 0.0, 0.0 },
        { 0.001, 0.0, 0.002, 0.0, 0.0, 0.05, -0.02 },
        { 0.002, 0.003, 0.001, 0.0175, -250.0, 40.0, 7.0 },
        { 0.0, 0.0, 0.0, 0.01, 0.6, 0.8, 0.0 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Covariance_IsTheSumOfTheFourTermsOfGates(double resolution, double shutoff, double autopilot,
        double pointing, double x, double y, double z)
    {
        // Arrange
        var deltaV = new Vector3(x, y, z);
        var model = new ManeuverExecutionError(resolution, shutoff, autopilot, pointing);
        var covariance = new double[9];

        // Act
        model.Covariance(deltaV, covariance);

        // Assert: section III, with the commanded ΔV known, L_v = v vᵀ, G = v vᵀ / V², V̄² = V²
        var v = Vector<double>.Build.Dense(new[] { x, y, z });
        double v2 = v.DotProduct(v);
        var lv = v.OuterProduct(v);
        var g = lv / v2;
        var identity = Matrix<double>.Build.DenseIdentity(3);
        var shutoffTerm = shutoff * shutoff * lv;
        var resolutionTerm = resolution * resolution * g;
        var pointingTerm = pointing * pointing * (v2 * identity - lv);
        var autopilotTerm = autopilot * autopilot * (identity - g);
        var expected = CovarianceAssert.ToArray(shutoffTerm + resolutionTerm + pointingTerm + autopilotTerm);
        double error = CovarianceAssert.RelativeError(covariance, expected);
        _output.WriteLine($"L_e against L_s + L_r + L_p + L_a: {error:E2}");
        Assert.True(error < 1e-14, $"{error:E2}");
        CovarianceAssert.SymmetricPositiveSemiDefinite(covariance, 3, "L_e");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Covariance_IsDiagonalInTheFrameOfTheThrust(double resolution, double shutoff, double autopilot,
        double pointing, double x, double y, double z)
    {
        // Arrange: an orthonormal frame whose first axis is along ΔV
        var deltaV = new Vector3(x, y, z);
        var model = new ManeuverExecutionError(resolution, shutoff, autopilot, pointing);
        var covariance = new double[9];
        var u = deltaV.Normalize();
        var helper = System.Math.Abs(u.X) < 0.9 ? new Vector3(1.0, 0.0, 0.0) : new Vector3(0.0, 1.0, 0.0);
        var e1 = u.Cross(helper).Normalize();
        var e2 = u.Cross(e1);
        var frame = Matrix<double>.Build.DenseOfRowArrays(new[] { u.X, u.Y, u.Z }, new[] { e1.X, e1.Y, e1.Z },
            new[] { e2.X, e2.Y, e2.Z });

        // Act
        model.Covariance(deltaV, covariance);

        // Assert: along ΔV σ_r² + σ_s² V², across it σ_a² + σ_p² V² on each axis, no correlation
        double v2 = deltaV.MagnitudeSquared();
        var expected = Matrix<double>.Build.DenseOfDiagonalArray(new[]
        {
            resolution * resolution + shutoff * shutoff * v2, autopilot * autopilot + pointing * pointing * v2,
            autopilot * autopilot + pointing * pointing * v2
        });
        var inThrustFrame = frame * CovarianceAssert.ToMathNet(covariance, 3) * frame.Transpose();
        double error = CovarianceAssert.RelativeError(CovarianceAssert.ToArray(inThrustFrame),
            CovarianceAssert.ToArray(expected));
        Assert.True(error < 1e-14, $"{error:E2}");
    }

    [Fact]
    public void Covariance_MatchesAMonteCarloOfTheFourErrorMechanisms()
    {
        // Arrange: section II and III of Gates, e = s v + r v / V + u × v + w × v / V, with s, r Gaussian of standard
        // deviations σ_s and σ_r, u and w spherical Gaussian of σ_p and σ_a on each axis; 200 000 samples, fixed seed
        const int samples = 200_000;
        var deltaV = new Vector3(-250.0, 40.0, 7.0);
        var model = new ManeuverExecutionError(0.2, 0.003, 0.1, 0.0175);
        var random = new Random(1963);
        double v = deltaV.Magnitude();
        var sum = new double[9];
        for (int n = 0; n < samples; n++)
        {
            double s = Gaussian(random) * model.ShutoffSigma;
            double r = Gaussian(random) * model.ResolutionSigma;
            var u = new Vector3(Gaussian(random), Gaussian(random), Gaussian(random)) * model.PointingSigma;
            var w = new Vector3(Gaussian(random), Gaussian(random), Gaussian(random)) * model.AutopilotSigma;
            var e = deltaV * s + deltaV * (r / v) + u.Cross(deltaV) + w.Cross(deltaV) / v;
            var components = new[] { e.X, e.Y, e.Z };
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    sum[3 * i + j] += components[i] * components[j];
                }
            }
        }

        // Act
        var covariance = new double[9];
        model.Covariance(deltaV, covariance);

        // Assert: each second moment within 4 standard deviations of its estimator, (L_ij² + L_ii L_jj) / N for a
        // zero-mean Gaussian sample (Wishart)
        double worst = 0.0;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double sampled = sum[3 * i + j] / samples;
                double expected = covariance[3 * i + j];
                double sigma = System.Math.Sqrt((expected * expected + covariance[4 * i] * covariance[4 * j]) / samples);
                double deviation = System.Math.Abs(sampled - expected) / sigma;
                worst = System.Math.Max(worst, deviation);
                Assert.True(deviation < 4.0, $"({i}, {j}): {sampled:E4} against {expected:E4}, {deviation:F2} σ");
            }
        }

        _output.WriteLine($"Monte Carlo of the four mechanisms: worst element {worst:F2} σ of the estimator");
    }

    [Fact]
    public void EqualProportionalAndFixedErrors_GiveASphericalError()
    {
        // Arrange: the remark that ends section III, σ_s = σ_p and σ_r = σ_a make the error spherical for any ΔV
        var model = new ManeuverExecutionError(0.003, 0.01, 0.003, 0.01);
        var deltaV = new Vector3(12.0, -5.0, 3.0);
        var covariance = new double[9];

        // Act
        model.Covariance(deltaV, covariance);

        // Assert
        double expected = 0.01 * 0.01 * deltaV.MagnitudeSquared() + 0.003 * 0.003;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(i == j ? expected : 0.0, covariance[3 * i + j], 1e-15);
            }
        }
    }

    [Fact]
    public void AZeroDeltaV_AddsNoError_AndATinyOneTakesTheFixedTerms()
    {
        // Arrange
        var model = new ManeuverExecutionError(0.01, 0.02, 0.005, 0.03);
        var zero = new double[9];
        var tiny = new double[9];
        var deltaV = new Vector3(0.0, 0.0, 1e-9);

        // Act
        model.Covariance(Vector3.Zero, zero);
        model.Covariance(deltaV, tiny);

        // Assert: no burn, no error (decision of S. Guillet, 2026-10-14); a 1 nm/s burn has the fixed errors, σ_r along ΔV
        // and σ_a across it
        Assert.All(zero, value => Assert.Equal(0.0, value));
        Assert.Equal(0.005 * 0.005, tiny[0], 1e-18);
        Assert.Equal(0.005 * 0.005, tiny[4], 1e-18);
        Assert.Equal(0.01 * 0.01, tiny[8], 1e-18);
        Assert.Equal(0.0, tiny[1], 1e-18);
    }

    [Theory]
    [InlineData(1e-160)]
    [InlineData(1e-300)]
    [InlineData(double.Epsilon)]
    public void AnyNonZeroDeltaV_TakesTheFixedTerms_WithoutOverflow(double magnitude)
    {
        // Arrange: ΔV so small that V² is subnormal or zero (H3 review of #384: (σ_r² − σ_a²) / V² overflowed below
        // about 1e-154 m/s); along x and along a diagonal
        var model = new ManeuverExecutionError(0.01, 0.02, 0.005, 0.03);
        var alongX = new double[9];
        var diagonal = new double[9];

        // Act
        model.Covariance(new Vector3(magnitude, 0.0, 0.0), alongX);
        model.Covariance(new Vector3(magnitude, -magnitude, magnitude), diagonal);

        // Assert: the fixed terms, σ_r² along ΔV and σ_a² across it, as for any non-zero ΔV (decision of S. Guillet,
        // 2026-10-14)
        double[] expectedAlongX = { 1e-4, 0.0, 0.0, 0.0, 2.5e-5, 0.0, 0.0, 0.0, 2.5e-5 };
        for (int k = 0; k < 9; k++)
        {
            Assert.Equal(expectedAlongX[k], alongX[k], 1e-18);
        }

        var u = new[] { 1.0, -1.0, 1.0 }.Select(x => x / System.Math.Sqrt(3.0)).ToArray();
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = (1e-4 - 2.5e-5) * u[i] * u[j] + (i == j ? 2.5e-5 : 0.0);
                Assert.Equal(expected, diagonal[3 * i + j], 1e-18);
            }
        }
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Equal("resolutionSigma", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManeuverExecutionError(-1e-3, 0.0, 0.0, 0.0)).ParamName);
        Assert.Equal("shutoffSigma", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManeuverExecutionError(0.0, double.NaN, 0.0, 0.0)).ParamName);
        Assert.Equal("autopilotSigma", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManeuverExecutionError(0.0, 0.0, double.PositiveInfinity, 0.0)).ParamName);
        Assert.Equal("pointingSigma", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManeuverExecutionError(0.0, 0.0, 0.0, -0.01)).ParamName);
        Assert.Equal("covariance", Assert.Throws<ArgumentException>(() =>
            new ManeuverExecutionError(0.0, 0.0, 0.0, 0.0).Covariance(new Vector3(1.0, 0.0, 0.0), new double[8]))
            .ParamName);
    }

    // A standard Gaussian deviate, by the Box-Muller transform
    private static double Gaussian(Random random)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = random.NextDouble();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
    }
}
