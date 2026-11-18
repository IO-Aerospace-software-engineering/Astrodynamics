// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The bound on the depth of the defect that the remediation of an initial covariance repairs (#443, decided by
/// S. Guillet, 2026-11-17): the smallest eigenvalue of the correlation matrix at least −1e-3 of its trace, in the frame of
/// the state or in its RTN frame, the frame of a CDM. The references are computed here, independently of the library: the
/// correlation matrix and its eigenvalues by MathNet, the RTN rotation from r and v, and the rounding of a covariance to 4
/// significant digits element by element.
/// </summary>
public class CovarianceRemediationBoundTests : IClassFixture<PartialsTestCases>
{
    private const double Bound = 1e-3;

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public CovarianceRemediationBoundTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private StateVector Leo => PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);

    // #443's case: σ 50, 200, 30 m and 0.2, 0.05, 0.03 m/s in RTN, a T–Ṙ correlation ρ, rotated to the frame of the state
    private static (Matrix Rtn, Matrix Inertial) WithTrCorrelation(StateVector state, double rho)
    {
        var sigma = new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(1, 3, rho * sigma[1] * sigma[3]);
        rtn.Set(3, 1, rho * sigma[1] * sigma[3]);
        return (rtn, Rotate(rtn, state, toRtn: false));
    }

    // The block-diagonal rotation between the inertial axes and the RTN axes of the state: R = [r̂, t̂, n̂] by rows
    private static Matrix Rotate(Matrix p, StateVector state, bool toRtn)
    {
        var r = state.Position / state.Position.Magnitude();
        var n = state.Position.Cross(state.Velocity);
        n /= n.Magnitude();
        var t = n.Cross(r);
        var axes = new[] { r, t, n };
        var rotation = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, 6);
        for (int k = 0; k < 3; k++)
        {
            var row = new[] { axes[k].X, axes[k].Y, axes[k].Z };
            for (int j = 0; j < 3; j++)
            {
                rotation[k, j] = row[j];
                rotation[k + 3, j + 3] = row[j];
            }
        }

        var m = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, 6, (i, j) => p.Get(i, j));
        var result = toRtn ? rotation * m * rotation.Transpose() : rotation.Transpose() * m * rotation;
        var matrix = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                matrix.Set(i, j, 0.5 * (result[i, j] + result[j, i]));
            }
        }

        return matrix;
    }

    // The smallest eigenvalue of the correlation matrix over its trace (6), independently of the library code
    private static double Depth(Matrix p)
    {
        var c = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, 6,
            (i, j) => 0.5 * (p.Get(i, j) + p.Get(j, i)) / System.Math.Sqrt(p.Get(i, i) * p.Get(j, j)));
        return c.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric).EigenValues.Min(e => e.Real) / 6.0;
    }

    private static StateVector WithCovariance(StateVector state, Matrix covariance) =>
        new(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, covariance);

    [Theory]
    [InlineData(-1.0061)] // just beyond the bound: λ = 1 − |ρ| = −6.1e-3, −1.02e-3 of the trace in RTN
    [InlineData(-1.01)]
    [InlineData(-1.1)]
    [InlineData(-1.5)]
    [InlineData(-2.0)]
    [InlineData(-10.0)] // #443: remediated by a change of 5.5 times the values before 11.0.0
    public void WithRemediatedCovariance_DefectBeyondTheBoundInBothFrames_IsRefused(double rho)
    {
        // Arrange: the depth in RTN and in the frame of the state, computed here
        var (rtn, inertial) = WithTrCorrelation(Leo, rho);
        double rtnDepth = Depth(rtn), inertialDepth = Depth(inertial);
        _output.WriteLine($"ρ = {rho}: depth {rtnDepth:E3} in RTN, {inertialDepth:E3} in the frame of the state");
        Assert.True(rtnDepth < -Bound && inertialDepth < -Bound, "the case is beyond the bound in both frames");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => WithCovariance(Leo, inertial).WithRemediatedCovariance());

        // Assert: the depth in the message
        Assert.Contains("#443", exception.Message);
        Assert.Contains($"{inertialDepth:E3}", exception.Message);
        Assert.Contains($"{rtnDepth:E3} in the RTN frame", exception.Message);
    }

    [Theory]
    [InlineData(-1.0059, true)] // just inside the bound: −9.83e-4 of the trace in RTN, beyond it in the frame of the state
    [InlineData(-1.0001, false)]
    [InlineData(-1.000000001, false)]
    public void WithRemediatedCovariance_DefectWithinTheBound_IsRemediated(double rho, bool deepInTheFrameOfTheState)
    {
        // Arrange
        var (rtn, inertial) = WithTrCorrelation(Leo, rho);
        Assert.True(Depth(rtn) >= -Bound && Depth(rtn) < 0.0);
        Assert.Equal(deepInTheFrameOfTheState, Depth(inertial) < -Bound);

        // Act
        var remediation = WithCovariance(Leo, inertial).WithRemediatedCovariance();

        // Assert: remediated against the independent clipping, in the frame of the state where the depth passes the bound
        // there, in RTN and rotated back otherwise
        Assert.True(remediation.Remediated);
        var expected = deepInTheFrameOfTheState
            ? Rotate(SSA.ConjunctionCovarianceEdgeTests.ClipCorrelation(Rotate(inertial, Leo, toRtn: true)), Leo, toRtn: false)
            : SSA.ConjunctionCovarianceEdgeTests.ClipCorrelation(inertial);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double scale = System.Math.Sqrt(expected.Get(i, i) * expected.Get(j, j));
                Assert.True(System.Math.Abs(remediation.State.Covariance.Value.Get(i, j) - expected.Get(i, j)) <= 1e-12 * scale);
            }
        }
    }

    [Fact]
    public void WithRemediatedCovariance_RectilinearState_TakesTheFrameOfTheStateOnly()
    {
        // Arrange: a radial state has no RTN frame; the same covariance in the frame of the state
        var leo = Leo;
        var radial = new StateVector(leo.Position, leo.Position / leo.Position.Magnitude() * 7000.0, leo.Observer, leo.Epoch,
            leo.Frame);
        var (_, beyond) = WithTrCorrelation(leo, -1.1);
        var (_, within) = WithTrCorrelation(leo, -1.0001);
        Assert.True(Depth(beyond) < -Bound && Depth(within) >= -Bound);

        // Act, assert
        Assert.Throws<InvalidOperationException>(() => WithCovariance(radial, beyond).WithRemediatedCovariance());
        Assert.True(WithCovariance(radial, within).WithRemediatedCovariance().Remediated);
    }

    // A covariance of LEO or GEO: the Clohessy-Wiltshire propagation, over up to 7 days, of a covariance of the given rank
    // (σ 1 to 100 m and 1 to 100 mm/s along random directions), in RTN
    private static Matrix CwCovariance(Random random, int rank)
    {
        double n = 2.0 * System.Math.PI / ((random.Next(2) == 0 ? 5400.0 : 86164.0) * (0.9 + 0.4 * random.NextDouble()));
        double t = 7.0 * 86400.0 * random.NextDouble();
        double s = System.Math.Sin(n * t), c = System.Math.Cos(n * t);
        var phi = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfArray(new[,]
        {
            { 4 - 3 * c, 0, 0, s / n, 2 * (1 - c) / n, 0 },
            { 6 * (s - n * t), 1, 0, -2 * (1 - c) / n, (4 * s - 3 * n * t) / n, 0 },
            { 0, 0, c, 0, 0, s / n },
            { 3 * n * s, 0, 0, c, 2 * s, 0 },
            { -6 * n * (1 - c), 0, 0, -2 * s, 4 * c - 3, 0 },
            { 0, 0, -n * s, 0, 0, c }
        });
        var a = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, rank, (i, j) =>
        {
            double scale = i < 3 ? System.Math.Pow(10.0, 2.0 * random.NextDouble()) : System.Math.Pow(10.0, -3.0 + 2.0 * random.NextDouble());
            // A normal deviate by Box-Muller
            double g = System.Math.Sqrt(-2.0 * System.Math.Log(1.0 - random.NextDouble())) *
                       System.Math.Cos(2.0 * System.Math.PI * random.NextDouble());
            return scale * g;
        });
        var p = phi * a * a.Transpose() * phi.Transpose();
        var matrix = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                matrix.Set(i, j, 0.5 * (p[i, j] + p[j, i]));
            }
        }

        return matrix;
    }

    // Each element rounded to 4 significant digits, as a CDM writes it (one value for the two elements of a pair)
    private static Matrix RoundTo4Digits(Matrix p)
    {
        var rounded = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double value = p.Get(i, j);
                double r = value == 0.0 ? 0.0 : double.Parse(value.ToString("E3", System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.CultureInfo.InvariantCulture);
                rounded.Set(i, j, r);
                rounded.Set(j, i, r);
            }
        }

        return rounded;
    }

    [Fact]
    public void WithRemediatedCovariance_DeepOnlyInTheFrameOfTheState_IsClippedInRtn()
    {
        // Arrange: covariances rounded to 4 significant digits in RTN (a CDM) and rotated to the frame of the state, whose
        // defect passes the bound in RTN only (adversarial review of #443: clipped in the frame of the state, such a
        // covariance changed by up to 2.9 √(M_ii M_jj))
        var random = new Random(4430);
        var leo = Leo;
        int cases = 0;
        double worstChange = 0.0, worstStateDepth = 0.0, worstReference = 0.0;
        for (int k = 0; k < 6000; k++)
        {
            var rounded = RoundTo4Digits(CwCovariance(random, 1 + k % 6));
            var given = Rotate(rounded, leo, toRtn: false);
            if (Enumerable.Range(0, 6).Any(i => !(given.Get(i, i) > 0.0)) || Depth(rounded) < -Bound ||
                Depth(given) >= -Bound)
            {
                continue;
            }

            cases++;
            worstStateDepth = System.Math.Min(worstStateDepth, Depth(given));

            // Act
            var remediation = WithCovariance(leo, given).WithRemediatedCovariance();

            // Assert: remediated, the clipping of the RTN covariance (computed here) rotated back to the frame of the
            // state, so that the change in RTN is that of the clipping of its rounding
            Assert.True(remediation.Remediated);
            var actual = remediation.State.Covariance.Value;
            Assert.Null(Propagator.Variational.CovarianceChecks.Validate(Values(actual), 6, "remediated", out _));
            var expected = Rotate(SSA.ConjunctionCovarianceEdgeTests.ClipCorrelation(Rotate(given, leo, toRtn: true)), leo,
                toRtn: false);
            var actualRtn = Rotate(actual, leo, toRtn: true);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    double scale = System.Math.Sqrt(expected.Get(i, i) * expected.Get(j, j));
                    worstReference = System.Math.Max(worstReference, System.Math.Abs(actual.Get(i, j) - expected.Get(i, j)) / scale);
                    double rtnScale = System.Math.Sqrt(rounded.Get(i, i) * rounded.Get(j, j));
                    worstChange = System.Math.Max(worstChange,
                        System.Math.Abs(actualRtn.Get(i, j) - rounded.Get(i, j)) / rtnScale);
                }
            }
        }

        // The reference: the clipping of the same matrix in RTN, the two rotations and decompositions differing by rounding,
        // which a variance ratio of up to 1e15 amplifies in the correlations (1.640e-9 on Linux x64, Windows x64 and macOS
        // arm64 alike, CI run 44225490669; twice it, rounded up). The change in RTN of the order of the
        // rounding: each correlation moves by at most the largest clipped eigenvalue, 6 × 1e-3 within the bound (a
        // rigorous bound: |ΔC_ij| ≤ ‖ΔC‖₂)
        _output.WriteLine($"MEASURE #443 deep only in the frame of the state: {cases} cases, depth there down to " +
                          $"{worstStateDepth:E3} of the trace, largest change in RTN {worstChange:E3} of √(M_ii M_jj), " +
                          $"largest difference from the clipping in RTN {worstReference:E3} of √(M_ii M_jj)");
        Assert.True(cases > 0, "no covariance is deep only in the frame of the state");
        Assert.True(worstReference <= 3.3e-9, $"{worstReference:E3}");
        Assert.True(worstChange <= 6.0 * Bound, $"{worstChange:E3}");
    }

    private static double[] Values(Matrix p) =>
        Enumerable.Range(0, 36).Select(k => p.Get(k / 6, k % 6)).ToArray();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithRemediatedCovariance_CovarianceRoundedTo4Digits_IsNeverRefused(bool roundedInRtn)
    {
        // Arrange: 3000 covariances of ranks 1 to 6, rounded to 4 significant digits in RTN then rotated to the frame of
        // the state (a CDM converted to the frame of the state), or rounded in the frame of the state (an OPM)
        var random = new Random(443);
        var leo = Leo;
        double worst = 0.0, worstOther = 0.0;
        int remediated = 0;
        for (int k = 0; k < 3000; k++)
        {
            var rtn = CwCovariance(random, 1 + k % 6);
            Matrix given;
            double depth;
            if (roundedInRtn)
            {
                var rounded = RoundTo4Digits(rtn);
                depth = Depth(rounded);
                given = Rotate(rounded, leo, toRtn: false);
                worstOther = System.Math.Min(worstOther, Enumerable.Range(0, 6).All(i => given.Get(i, i) > 0.0) ? Depth(given) : 0.0);
            }
            else
            {
                given = RoundTo4Digits(Rotate(rtn, leo, toRtn: false));
                depth = Depth(given);
            }

            worst = System.Math.Min(worst, depth);
            if (Enumerable.Range(0, 6).Any(i => !(given.Get(i, i) > 0.0)))
            {
                // A rotated degenerate covariance may have a negative variance, refused for its own reason
                continue;
            }

            // Act: never refused for its depth
            var remediation = WithCovariance(leo, given).WithRemediatedCovariance();
            remediated += remediation.Remediated ? 1 : 0;
        }

        // Assert: the depth of the rounding within the rigorous bound of 4 digits, (n − 1) 2u / (1 − u) / n = 8.34e-4 of
        // the trace, u = 5e-4 (Weyl)
        _output.WriteLine($"MEASURE #443 rounded in {(roundedInRtn ? "RTN" : "the frame of the state")}: worst depth " +
                          $"{worst:E3} of the trace in that frame" +
                          (roundedInRtn ? $" ({worstOther:E3} in the frame of the state after the rotation)" : "") +
                          $", {remediated} of 3000 remediated");
        Assert.True(worst >= -8.34e-4, $"{worst:E3}");
        Assert.True(remediated > 0, "no covariance needed the remediation");
    }
}
