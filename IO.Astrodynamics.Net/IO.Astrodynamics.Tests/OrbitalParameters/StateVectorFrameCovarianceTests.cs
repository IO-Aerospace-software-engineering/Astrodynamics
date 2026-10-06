// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using MathNetMatrix = MathNet.Numerics.LinearAlgebra.Matrix<double>;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Covariance carried through <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToFrame"/>: the Jacobian
/// of the state transformation, including the angular velocity term of a rotating frame (phase 2, C1).
/// </summary>
public class StateVectorFrameCovarianceTests
{
    // Inside the coverage of the Earth orientation kernel of the test data.
    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0);

    // The state transformation is linear in (r, v) at a fixed epoch, so the central differences have no truncation
    // error: large steps keep the rounding error far below the tolerances.
    private static readonly double[] FiniteDifferenceSteps = { 1000.0, 1000.0, 1000.0, 1.0, 1.0, 1.0 };

    public StateVectorFrameCovarianceTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ToFrame_RotatingFrame_JacobianMatchesFiniteDifferences()
    {
        // Arrange
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var state = CreateLeoState(null);
        var orientation = Frames.Frame.ICRF.ToFrame(itrf93, Epoch);

        // Act: Frame.ToFrame gives the angular velocity in the source frame, the Jacobian takes it in the target frame.
        var jacobian = Matrix.CreateStateTransformationJacobian(orientation.Rotation,
            orientation.AngularVelocity.Rotate(orientation.Rotation));
        var reference = FiniteDifferenceJacobian(state, itrf93);

        // Assert
        Assert.True(RelativeError(jacobian, reference) < 1e-9, $"Jacobian error {RelativeError(jacobian, reference):E3}");
        // The angular velocity block is four orders of magnitude below the rotation blocks: check it on its own.
        Assert.True(BlockRelativeError(jacobian, reference, 3, 0) < 1e-9,
            $"-[w x] R block error {BlockRelativeError(jacobian, reference, 3, 0):E3}");
        Assert.True(FrobeniusNorm(Block(reference, 3, 0), null) > 1e-5, "The reference must have a non-zero angular velocity block.");
    }

    [Fact]
    public void ToFrame_RotatingFrame_CovarianceIsTransformedWithTheFiniteDifferenceJacobian()
    {
        // Arrange
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var covariance = CreateCovariance();
        var state = CreateLeoState(covariance);
        var jacobian = FiniteDifferenceJacobian(state, itrf93);
        var expected = jacobian.Multiply(covariance).Multiply(jacobian.Transpose());

        // Act
        var transformed = state.ToFrame(itrf93).ToStateVector().Covariance!.Value;

        // Assert
        for (int row = 0; row < 6; row += 3)
        {
            for (int column = 0; column < 6; column += 3)
            {
                double error = BlockRelativeError(transformed, expected, row, column);
                Assert.True(error < 1e-9, $"Block ({row},{column}) error {error:E3}");
            }
        }
    }

    [Fact]
    public void ToFrame_RoundTripThroughRotatingFrame_RestoresStateAndCovariance()
    {
        // Arrange
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var covariance = CreateCovariance();
        var state = CreateLeoState(covariance);

        // Act
        var roundTrip = state.ToFrame(itrf93).ToFrame(Frames.Frame.ICRF).ToStateVector();

        // Assert
        Assert.True((roundTrip.Position - state.Position).Magnitude() < 1e-12 * state.Position.Magnitude());
        Assert.True((roundTrip.Velocity - state.Velocity).Magnitude() < 1e-12 * state.Velocity.Magnitude());
        for (int row = 0; row < 6; row += 3)
        {
            for (int column = 0; column < 6; column += 3)
            {
                double error = BlockRelativeError(roundTrip.Covariance!.Value, covariance, row, column);
                Assert.True(error < 1e-12, $"Block ({row},{column}) round-trip error {error:E3}");
            }
        }
    }

    [Fact]
    public void ToFrame_PositionUncertaintyInducesVelocityUncertaintyInEarthFixedFrame()
    {
        // Arrange: 100 m isotropic position uncertainty, no velocity uncertainty.
        const double sigmaPosition = 100.0;
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var covariance = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            covariance.Set(i, i, sigmaPosition * sigmaPosition);
        }

        var state = CreateLeoState(covariance);
        double omega = Frames.Frame.ICRF.ToFrame(itrf93, Epoch).AngularVelocity.Magnitude();
        Assert.InRange(omega, 7.29e-5, 7.30e-5);

        // Act
        var transformed = state.ToFrame(itrf93).ToStateVector().Covariance!.Value;

        // Assert: v' = R v - w x r' with w along the z-axis of ITRF93, so sigma(vx) = sigma(vy) = w sigma(r) and vz is
        // not affected. A block-diagonal rotation would leave the velocity covariance at zero.
        double expected = omega * sigmaPosition;
        Assert.Equal(expected, System.Math.Sqrt(transformed.Get(3, 3)), expected * 1e-6);
        Assert.Equal(expected, System.Math.Sqrt(transformed.Get(4, 4)), expected * 1e-6);
        Assert.True(System.Math.Sqrt(System.Math.Abs(transformed.Get(5, 5))) < 1e-3 * expected);
    }

    [Fact]
    public void ToFrame_InertialFrames_CovarianceIsThePureRotation()
    {
        // Arrange
        var covariance = CreateCovariance();
        var state = CreateLeoState(covariance);
        var orientation = Frames.Frame.ICRF.ToFrame(Frames.Frame.ECLIPTIC_J2000, Epoch);
        var expected = Matrix.TransformCovariance(covariance, orientation.Rotation);

        // Act
        var transformed = state.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector().Covariance!.Value;

        // Assert: between inertial frames the Jacobian is diag(R, R); only the symmetrization rounding differs.
        for (int row = 0; row < 6; row += 3)
        {
            for (int column = 0; column < 6; column += 3)
            {
                double error = BlockRelativeError(transformed, expected, row, column);
                Assert.True(error < 1e-14, $"Block ({row},{column}) error {error:E3}");
            }
        }
    }

    [Fact]
    public void ToFrame_RotatingFrame_CovarianceIsSymmetricPositiveSemiDefinite()
    {
        // Arrange
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var state = CreateLeoState(CreateCovariance());

        // Act
        var transformed = state.ToFrame(itrf93).ToStateVector().Covariance!.Value;

        // Assert
        Assert.True(transformed.IsSymmetric(0.0));
        var eigenValues = MathNetMatrix.Build.DenseOfArray(transformed.ToArray()).Evd().EigenValues;
        double trace = 0.0;
        double minimum = double.MaxValue;
        foreach (var value in eigenValues)
        {
            trace += value.Real;
            minimum = System.Math.Min(minimum, value.Real);
        }

        Assert.True(minimum >= -1e-12 * trace, $"Smallest eigenvalue {minimum:E3}, trace {trace:E3}");
    }

    [Fact]
    public void ToFrame_WithoutCovariance_ReturnsStateWithoutCovariance()
    {
        // Arrange
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var state = CreateLeoState(null);

        // Act
        var transformed = state.ToFrame(itrf93).ToStateVector();

        // Assert
        Assert.Null(transformed.Covariance);
        Assert.Same(state, state.ToFrame(Frames.Frame.ICRF));
    }

    private static StateVector CreateLeoState(Matrix? covariance)
    {
        return new StateVector(
            new Vector3(4211623.0, -3218447.0, 4402165.0),
            new Vector3(4523.1, 5601.7, 1188.4),
            TestHelpers.EarthAtJ2000, Epoch, Frames.Frame.ICRF, covariance);
    }

    /// <summary>
    /// A full, correlated covariance: P = L L^T, with position sigmas near 100 m and velocity sigmas near 0.1 m/s.
    /// </summary>
    private static Matrix CreateCovariance()
    {
        double[,] lower =
        {
            { 100.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
            { 35.0, 90.0, 0.0, 0.0, 0.0, 0.0 },
            { -20.0, 15.0, 80.0, 0.0, 0.0, 0.0 },
            { 0.02, -0.01, 0.03, 0.1, 0.0, 0.0 },
            { -0.03, 0.04, 0.01, 0.02, 0.09, 0.0 },
            { 0.01, 0.02, -0.02, -0.01, 0.03, 0.08 }
        };
        var l = new Matrix(lower);
        return l.Multiply(l.Transpose());
    }

    private static Matrix FiniteDifferenceJacobian(StateVector state, Frames.Frame target)
    {
        var jacobian = new Matrix(6, 6);
        var nominal = state.ToArray();
        for (int k = 0; k < 6; k++)
        {
            var plus = Perturb(state, nominal, k, FiniteDifferenceSteps[k]).ToFrame(target).ToStateVector().ToArray();
            var minus = Perturb(state, nominal, k, -FiniteDifferenceSteps[k]).ToFrame(target).ToStateVector().ToArray();
            for (int i = 0; i < 6; i++)
            {
                jacobian.Set(i, k, (plus[i] - minus[i]) / (2.0 * FiniteDifferenceSteps[k]));
            }
        }

        return jacobian;
    }

    private static StateVector Perturb(StateVector state, double[] nominal, int component, double step)
    {
        var x = (double[])nominal.Clone();
        x[component] += step;
        return new StateVector(new Vector3(x[0], x[1], x[2]), new Vector3(x[3], x[4], x[5]),
            state.Observer, state.Epoch, state.Frame);
    }

    private static Matrix Block(Matrix matrix, int row, int column)
    {
        return matrix.SubMatrix(row, column, 3, 3);
    }

    private static double RelativeError(Matrix actual, Matrix expected)
    {
        return FrobeniusNorm(actual, expected) / FrobeniusNorm(expected, null);
    }

    private static double BlockRelativeError(Matrix actual, Matrix expected, int row, int column)
    {
        return RelativeError(Block(actual, row, column), Block(expected, row, column));
    }

    private static double FrobeniusNorm(Matrix matrix, Matrix? subtract)
    {
        double sum = 0.0;
        for (int i = 0; i < matrix.Rows; i++)
        {
            for (int j = 0; j < matrix.Columns; j++)
            {
                double value = matrix.Get(i, j) - (subtract?.Get(i, j) ?? 0.0);
                sum += value * value;
            }
        }

        return System.Math.Sqrt(sum);
    }
}
