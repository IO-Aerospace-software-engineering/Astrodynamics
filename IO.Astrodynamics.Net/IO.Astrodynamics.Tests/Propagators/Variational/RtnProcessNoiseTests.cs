// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The RTN process noise of C3: Qc = R_rtnᵀ diag(q_R, q_T, q_N) R_rtn at each stage, and the noise covariance Q it
/// integrates against the short-arc discrete form of Tapley, Schutz and Born (section 4.9).
/// </summary>
public class RtnProcessNoiseTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public RtnProcessNoiseTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Theory]
    [MemberData(nameof(States))]
    public void Accumulate_IsTheRtnDensityRotatedByTheRtnFrameOfTheState(string name)
    {
        // Arrange: three different densities, so that every axis is checked
        var state = PartialsTestCases.State(name, _cases.Earth);
        var noise = new RtnProcessNoise(3e-12, 5e-11, 7e-13);
        var qc = new double[9];

        // Act
        noise.Accumulate(state, qc);

        // Assert: R_rtnᵀ diag(q) R_rtn with the RTN rotation of StateVector, through MathNet
        var rotation = CovarianceAssert.ToMathNet(Rows(state.CreateRtnRotation()), 3);
        var diagonal = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfDiagonalArray(
            new[] { 3e-12, 5e-11, 7e-13 });
        var expected = CovarianceAssert.ToArray(rotation.Transpose() * diagonal * rotation);
        double error = CovarianceAssert.RelativeError(qc, expected);
        _output.WriteLine($"{name}: Qc against R_rtnᵀ diag(q) R_rtn {error:E2}");
        Assert.True(error < 1e-14, $"{name}: {error:E2}");
        CovarianceAssert.SymmetricPositiveSemiDefinite(qc, 3, name);
    }

    [Fact]
    public void Accumulate_AddsToTheBuffer_WithoutAllocating()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var noise = new RtnProcessNoise(1.0, 2.0, 3.0);
        var once = new double[9];
        noise.Accumulate(state, once);
        var twice = new double[9];
        noise.Accumulate(state, twice);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        noise.Accumulate(state, twice);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: the trace of Qc is q_R + q_T + q_N, whatever the axes
        Assert.Equal(0, allocated);
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(2.0 * once[i], twice[i], 14);
        }

        Assert.Equal(6.0, once[0] + once[4] + once[8], 14);
    }

    [Fact]
    public void Constructor_WithANegativeOrNonFiniteDensity_Throws()
    {
        Assert.Equal("radial",
            Assert.Throws<ArgumentOutOfRangeException>(() => new RtnProcessNoise(-1e-12, 0.0, 0.0)).ParamName);
        Assert.Equal("transverse",
            Assert.Throws<ArgumentOutOfRangeException>(() => new RtnProcessNoise(0.0, double.NaN, 0.0)).ParamName);
        Assert.Equal("normal",
            Assert.Throws<ArgumentOutOfRangeException>(() => new RtnProcessNoise(0.0, 0.0, double.PositiveInfinity))
                .ParamName);
        var noise = new RtnProcessNoise(1e-12, 0.0, 2e-12);
        Assert.Equal(1e-12, noise.Radial);
        Assert.Equal(0.0, noise.Transverse);
        Assert.Equal(2e-12, noise.Normal);
    }

    [Fact]
    public void Accumulate_OnARectilinearState_Throws()
    {
        // Arrange: the velocity along the position
        var state = new StateVector(new Vector3(7e6, 0.0, 0.0), new Vector3(10.0, 0.0, 0.0), _cases.Earth,
            PartialsTestCases.Epoch, Frames.Frame.ICRF);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new RtnProcessNoise(1e-12, 1e-12, 1e-12).Accumulate(state, new double[9]));

        // Assert
        Assert.Contains("rectilinear", exception.Message);
    }

    [Fact]
    public void TwoBody_TransverseNoise_MatchesTheDiscreteFormOfTapley_AndTheGapDecreasesWithTheArc()
    {
        // Arrange: LEO 400 km, point-mass Earth, q_T alone. The discrete form of Tapley, Schutz and Born (section 4.9)
        // takes Φ(t, τ) ≈ [[I, (t − τ) I], [0, I]] and Qc constant over the arc, in the RTN axes of its start:
        // Q = [[Δt³/3 Qc, Δt²/2 Qc], [Δt²/2 Qc, Δt Qc]]. Q is integrated over the arc in one RK7(8) step, with the RTN
        // axes of each stage. The gap is first order in the arc: the RTN axes turn at the orbital rate n, 1.13e-3 rad/s,
        // which the discrete form holds fixed. Measured 9.5e-2, 4.8e-2, 2.4e-2, 1.2e-2 at 120, 60, 30, 15 s; below 1 % from
        // 10 s, the arcs asserted (Linux, .NET 10, 2026-10-14).
        const double transverse = 1e-10;
        var start = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var qc = new double[9];
        new RtnProcessNoise(0.0, transverse, 0.0).Accumulate(start, qc);

        double previous = double.PositiveInfinity;
        foreach (double arc in new[] { 10.0, 5.0, 2.5, 1.25 })
        {
            // Act
            var q = IntegratedQ(new RtnProcessNoise(0.0, transverse, 0.0), start, arc);

            // Assert: below 1 %, and halving with the arc
            double error = StmMeasures.WorstBlockError(q, Discrete(qc, arc));
            _output.WriteLine($"arc {arc} s: Q against the discrete form {error:E2} (worst 3×3 block)");
            Assert.True(error < 0.01, $"arc {arc} s: {error:E2}");
            Assert.True(error < 0.51 * previous, $"arc {arc} s: {error:E2}, not half of {previous:E2}");
            previous = error;
        }
    }

    [Fact]
    public void TwoBody_NoiseFixedInInertialAxes_MatchesTheDiscreteFormToSecondOrder()
    {
        // Arrange: the same Qc, held in the inertial axes of the start (ConstantProcessNoise), so that the only departure
        // from the discrete form is Φ against [[I, Δt I], [0, I]], the gravity gradient: second order in the arc. This
        // isolates the first-order gap of the RTN noise above as the rotation of the RTN axes. Measured 1.5e-3, 3.8e-4 and
        // 9.6e-5 at 60, 30 and 15 s (Linux, .NET 10, 2026-10-14).
        var start = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var qc = new double[9];
        new RtnProcessNoise(0.0, 1e-10, 0.0).Accumulate(start, qc);

        double previous = double.PositiveInfinity;
        foreach (double arc in new[] { 60.0, 30.0, 15.0 })
        {
            // Act
            var q = IntegratedQ(new ConstantProcessNoise(qc), start, arc);

            // Assert: a quarter with each halving of the arc
            double error = StmMeasures.WorstBlockError(q, Discrete(qc, arc));
            _output.WriteLine($"arc {arc} s, Qc fixed: Q against the discrete form {error:E2} (worst 3×3 block)");
            Assert.True(error < 0.26 * previous, $"arc {arc} s: {error:E2}, not a quarter of {previous:E2}");
            previous = error;
        }
    }

    // Q integrated over one RK7(8) step of the arc around a point-mass Earth, as a full 6×6 matrix
    private double[] IntegratedQ(IProcessNoiseModel noise, StateVector start, double arc)
    {
        var forces = new List<ForceBase> { new GravitationalAcceleration(_cases.Earth) };
        var equations = new VariationalEquations(new VariationalOptions(ProcessNoise: noise));
        var y = VariationalTestDriver.Identity(equations);
        var q = VariationalTestDriver.ZeroCovariance(equations);
        VariationalTestDriver.Integrate(forces, equations, new ForceEvaluationContext(100.0, 2.2, 1.5), start, arc, 1, y,
            q);
        return CovarianceAssert.Full(q);
    }

    // The discrete short-arc form of Tapley, Schutz and Born, section 4.9
    private static double[] Discrete(double[] qc, double dt)
    {
        var result = new double[36];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[6 * i + j] = dt * dt * dt / 3.0 * qc[3 * i + j];
                result[6 * i + j + 3] = dt * dt / 2.0 * qc[3 * i + j];
                result[6 * (i + 3) + j] = dt * dt / 2.0 * qc[3 * i + j];
                result[6 * (i + 3) + j + 3] = dt * qc[3 * i + j];
            }
        }

        return result;
    }

    private static double[] Rows(IO.Astrodynamics.Math.Matrix matrix)
    {
        var result = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[3 * i + j] = matrix.Get(i, j);
            }
        }

        return result;
    }
}
