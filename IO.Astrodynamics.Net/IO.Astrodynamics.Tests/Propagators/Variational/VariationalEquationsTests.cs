// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The variational equations over RK7(8) steps, outside any propagator: the discretization, closed forms, Ridders'
/// derivative of the step map, and the contract of <see cref="VariationalEquations"/>.
/// </summary>
public class VariationalEquationsTests : IClassFixture<PartialsTestCases>
{
    // Oscillator: ω h = 0.03 per step, 200 steps of 30 s, a little less than one period (2π/ω = 6283 s).
    private const double Omega = 1e-3;
    private const double Damping = 0.1;
    private const double StepSize = 30.0;
    private const int Steps = 200;

    // Against a closed form. The local error of an 8th-order step is of order (ω h)^9 = 2e-14 relative, times a constant
    // below 1, so the 200 steps add up to well below 1e-12; rounding adds about 200 ε = 4e-14.
    private const double ClosedFormTolerance = 1e-12;

    // Two computations of the same discrete values, in different operation orders (13 stages, a few hundred
    // operations): rounding only.
    private const double RoundingTolerance = 1e-13;

    // Against Ridders' derivative of the step map, whose own error estimate must stay two orders below (the rule of the
    // force partials, step 2).
    private const double RiddersTolerance = 1e-8;
    private const double RiddersEstimateBound = 1e-10;

    private static readonly Vector3 DragDirection = new(1e-3, -2e-3, 5e-4);
    private static readonly Vector3 ReflectivityDirection = new(-4e-4, 1e-4, 3e-3);

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public VariationalEquationsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Fact]
    public void Constructor_WithoutOptions_Throws()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new VariationalEquations(null));

        // Assert
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithAnUnsupportedParameter_Throws()
    {
        // Arrange
        var options = new VariationalOptions((ForceParameters)4);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new VariationalEquations(options));

        // Assert
        Assert.Equal("options", exception.ParamName);
    }

    // parameters: the ForceParameters flags (None 0, Cd 1, Cr 2), internal to the library
    [Theory]
    [InlineData(0, 0, 6, 36)]
    [InlineData(1, 1, 7, 42)]
    [InlineData(2, 1, 7, 42)]
    [InlineData(3, 2, 8, 48)]
    public void Dimensions_FollowTheParameters(int parameters, int parameterCount, int columns, int length)
    {
        // Act
        var equations = new VariationalEquations(new VariationalOptions((ForceParameters)parameters));

        // Assert
        Assert.Equal(parameterCount, equations.ParameterCount);
        Assert.Equal(columns, equations.ColumnCount);
        Assert.Equal(length, equations.YLength);
        Assert.False(equations.HasProcessNoise);
    }

    [Fact]
    public void SetIdentity_GivesTheIdentityAndAZeroSensitivity()
    {
        // Arrange
        var equations = new VariationalEquations(new VariationalOptions(ForceParameters.DragCoefficient));
        var y = Enumerable.Repeat(7.0, equations.YLength).ToArray();

        // Act
        equations.SetIdentity(y);

        // Assert
        for (int i = 0; i < 6; i++)
        {
            for (int c = 0; c < 7; c++)
            {
                Assert.Equal(i == c ? 1.0 : 0.0, y[i * 7 + c]);
            }
        }
    }

    [Fact]
    public void CovarianceIndex_EnumeratesTheUpperTriangleRowByRow()
    {
        // Act
        var indices = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            for (int j = i; j < 6; j++)
            {
                indices.Add(VariationalEquations.CovarianceIndex(i, j));
            }
        }

        // Assert
        Assert.Equal(Enumerable.Range(0, VariationalEquations.CovarianceLength), indices);
    }

    [Fact]
    public void LinearSystem_EachColumnOfY_IsTheStepOfTheUnitState()
    {
        // Arrange: for a linear force, column j of Φ is the RK step of the unit state e_j with Cd = Cr = 0, and the column
        // of a parameter is the RK step of the zero state with that parameter at 1 (the step is affine in both).
        var force = new LinearTestForce(Omega, Damping, DragDirection, ReflectivityDirection);
        var forces = new List<ForceBase> { force };
        var equations = new VariationalEquations(new VariationalOptions(ForceParameters.DragCoefficient |
                                                                         ForceParameters.ReflectivityCoefficient));
        var start = State(new Vector3(7e6, -1e5, 3e5), new Vector3(10.0, 7.5e3, -40.0));
        var context = new ForceEvaluationContext(100.0, 2.2, 1.5);
        var y = VariationalTestDriver.Identity(equations);

        // Act
        VariationalTestDriver.Integrate(forces, equations, context, start, StepSize, 1, y,
            VariationalTestDriver.ZeroCovariance(equations));

        // Assert
        for (int column = 0; column < 8; column++)
        {
            var unit = new double[6];
            double drag = 0.0;
            double reflectivity = 0.0;
            if (column < 6)
            {
                unit[column] = 1.0;
            }
            else if (column == 6)
            {
                drag = 1.0;
            }
            else
            {
                reflectivity = 1.0;
            }

            var unitState = State(new Vector3(unit[0], unit[1], unit[2]), new Vector3(unit[3], unit[4], unit[5]));
            var (position, velocity) = VariationalTestDriver.Integrate(forces,
                new VariationalEquations(new VariationalOptions()), new ForceEvaluationContext(1.0, drag, reflectivity),
                unitState, StepSize, 1, VariationalTestDriver.Identity(new VariationalEquations(new VariationalOptions())),
                Array.Empty<double>());
            var expected = new[] { position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z };
            var actual = Enumerable.Range(0, 6).Select(row => y[row * 8 + column]).ToArray();
            double columnError = RiddersDerivative.RelativeFrobeniusError(actual, expected);
            _output.WriteLine($"column {column}: relative error {columnError:E2}");
            Assert.True(columnError < RoundingTolerance, $"column {column}: relative error {columnError:E2}");
        }
    }

    [Fact]
    public void HarmonicOscillator_PhiMatchesTheClosedForm()
    {
        // Arrange: x'' = −ω² x on each axis, Φ_rr = Φ_vv = cos ωt, Φ_rv = sin ωt / ω, Φ_vr = −ω sin ωt.
        var equations = new VariationalEquations(new VariationalOptions());
        var y = VariationalTestDriver.Identity(equations);
        double t = Steps * StepSize;

        // Act
        VariationalTestDriver.Integrate(new List<ForceBase> { new LinearTestForce(Omega) }, equations,
            new ForceEvaluationContext(1.0, 0.0, 0.0), State(new Vector3(1e3, 2e3, -5e2), new Vector3(1.0, -2.0, 0.5)),
            StepSize, Steps, y, Array.Empty<double>());

        // Assert
        double c = System.Math.Cos(Omega * t);
        double s = System.Math.Sin(Omega * t);
        AssertPhiBlocks(y, 6, c, s / Omega, -Omega * s, c);
    }

    [Fact]
    public void DampedOscillator_PhiMatchesTheClosedForm()
    {
        // Arrange: x'' = −ω² x − 2ζω x'. With a = ζω and b = ω √(1 − ζ²):
        // Φ_rr = e^(−at) (cos bt + a/b sin bt), Φ_rv = e^(−at) sin bt / b,
        // Φ_vr = −e^(−at) ω²/b sin bt, Φ_vv = e^(−at) (cos bt − a/b sin bt).
        var equations = new VariationalEquations(new VariationalOptions());
        var y = VariationalTestDriver.Identity(equations);
        double t = Steps * StepSize;

        // Act
        VariationalTestDriver.Integrate(new List<ForceBase> { new LinearTestForce(Omega, Damping) }, equations,
            new ForceEvaluationContext(1.0, 0.0, 0.0), State(new Vector3(1e3, 2e3, -5e2), new Vector3(1.0, -2.0, 0.5)),
            StepSize, Steps, y, Array.Empty<double>());

        // Assert
        double a = Damping * Omega;
        double b = Omega * System.Math.Sqrt(1.0 - Damping * Damping);
        double decay = System.Math.Exp(-a * t);
        double c = System.Math.Cos(b * t);
        double s = System.Math.Sin(b * t);
        AssertPhiBlocks(y, 6, decay * (c + a / b * s), decay * s / b, -decay * Omega * Omega / b * s,
            decay * (c - a / b * s));
    }

    [Fact]
    public void ForcedOscillator_PsiMatchesTheClosedForm()
    {
        // Arrange: x'' = −ω² x + Cd u + Cr w. From rest, x = (Cd u + Cr w)(1 − cos ωt)/ω² and
        // x' = (Cd u + Cr w) sin ωt / ω, so Ψ_Cd = [u (1 − cos ωt)/ω² ; u sin ωt / ω], and the same with w for Cr.
        var equations = new VariationalEquations(new VariationalOptions(ForceParameters.DragCoefficient |
                                                                         ForceParameters.ReflectivityCoefficient));
        var y = VariationalTestDriver.Identity(equations);
        double t = Steps * StepSize;

        // Act
        VariationalTestDriver.Integrate(
            new List<ForceBase> { new LinearTestForce(Omega, 0.0, DragDirection, ReflectivityDirection) }, equations,
            new ForceEvaluationContext(1.0, 2.2, 1.5), State(new Vector3(1e3, 2e3, -5e2), new Vector3(1.0, -2.0, 0.5)),
            StepSize, Steps, y, Array.Empty<double>());

        // Assert
        double positionFactor = (1.0 - System.Math.Cos(Omega * t)) / (Omega * Omega);
        double velocityFactor = System.Math.Sin(Omega * t) / Omega;
        AssertColumn(y, 8, 6, DragDirection * positionFactor, DragDirection * velocityFactor);
        AssertColumn(y, 8, 7, ReflectivityDirection * positionFactor, ReflectivityDirection * velocityFactor);
    }

    [Fact]
    public void FreeParticle_QIsTheWhiteNoiseClosedForm()
    {
        // Arrange: no force, so Φ(τ) = [[I, τ I], [0, I]] and Q(t) = ∫ Φ(τ) B Qc Bᵀ Φ(τ)ᵀ dτ
        // = [[Qc t³/3, Qc t²/2], [Qc t²/2, Qc t]]. Q is a polynomial of degree 3 in t and A is nilpotent, so the
        // RK7(8) step is exact up to rounding.
        var qc = new[] { 4e-12, 1e-12, 0.0, 1e-12, 2e-12, -5e-13, 0.0, -5e-13, 1e-12 };
        var equations = new VariationalEquations(new VariationalOptions(ProcessNoise: new ConstantProcessNoise(qc)));
        var y = VariationalTestDriver.Identity(equations);
        var q = VariationalTestDriver.ZeroCovariance(equations);
        double t = Steps * StepSize;

        // Act
        VariationalTestDriver.Integrate(new List<ForceBase>(), equations, new ForceEvaluationContext(1.0, 0.0, 0.0),
            State(new Vector3(1e3, 2e3, -5e2), new Vector3(1.0, -2.0, 0.5)), StepSize, Steps, y, q);

        // Assert
        AssertQBlocks(q, qc, t * t * t / 3.0, t * t / 2.0, t, RoundingTolerance);
    }

    [Fact]
    public void HarmonicOscillator_QMatchesTheClosedForm()
    {
        // Arrange: Qc = q I and Φ(τ) B = [sin ωτ / ω ; cos ωτ], so Q_rr = q (t/2 − sin 2ωt / 4ω) / ω²,
        // Q_rv = q sin² ωt / 2ω², Q_vv = q (t/2 + sin 2ωt / 4ω), on each axis.
        const double noise = 3e-12;
        var qc = new[] { noise, 0.0, 0.0, 0.0, noise, 0.0, 0.0, 0.0, noise };
        var equations = new VariationalEquations(new VariationalOptions(ProcessNoise: new ConstantProcessNoise(qc)));
        var y = VariationalTestDriver.Identity(equations);
        var q = VariationalTestDriver.ZeroCovariance(equations);
        double t = Steps * StepSize;

        // Act
        VariationalTestDriver.Integrate(new List<ForceBase> { new LinearTestForce(Omega) }, equations,
            new ForceEvaluationContext(1.0, 0.0, 0.0), State(new Vector3(1e3, 2e3, -5e2), new Vector3(1.0, -2.0, 0.5)),
            StepSize, Steps, y, q);

        // Assert
        double s2 = System.Math.Sin(2.0 * Omega * t) / (4.0 * Omega);
        double s = System.Math.Sin(Omega * t);
        AssertQBlocks(q, qc, (t / 2.0 - s2) / (Omega * Omega), s * s / (2.0 * Omega * Omega), t / 2.0 + s2,
            ClosedFormTolerance);
    }

    [Fact]
    public void KeplerOrbit_QStaysPositiveSemiDefinite()
    {
        // Arrange: point-mass LEO over about one orbit, with an anisotropic Qc.
        var qc = new[] { 1e-12, 2e-13, 0.0, 2e-13, 5e-13, 1e-13, 0.0, 1e-13, 2e-12 };
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var equations = new VariationalEquations(new VariationalOptions(ProcessNoise: new ConstantProcessNoise(qc)));
        var y = VariationalTestDriver.Identity(equations);
        var q = VariationalTestDriver.ZeroCovariance(equations);

        // Act
        VariationalTestDriver.Integrate(new List<ForceBase> { new GravitationalAcceleration(_cases.Earth) }, equations,
            new ForceEvaluationContext(100.0, 2.2, 1.5), state, 60.0, 92, y, q);

        // Assert: the smallest eigenvalue is not below −1e-12 × trace (the rule of C6)
        var full = Matrix<double>.Build.Dense(6, 6, (i, j) =>
            q[VariationalEquations.CovarianceIndex(System.Math.Min(i, j), System.Math.Max(i, j))]);
        double minimum = full.Evd(Symmetricity.Symmetric).EigenValues.Min(e => e.Real);
        _output.WriteLine($"smallest eigenvalue {minimum:E3}, trace {full.Trace():E3}");
        Assert.True(minimum >= -1e-12 * full.Trace(), $"smallest eigenvalue {minimum:E3}, trace {full.Trace():E3}");
    }

    [Theory]
    [MemberData(nameof(States))]
    public void OneStep_YIsTheJacobianOfTheStepMap(string name)
    {
        // Arrange: point mass and third bodies, whose partials are analytic, so that Y is the Jacobian of the step map
        // up to rounding.
        var state = PartialsTestCases.State(name, _cases.Earth);
        var forces = ThirdBodyForces();
        var context = ForceEvaluationContext.FromSpacecraft(PartialsTestCases.Spacecraft(state));
        var equations = new VariationalEquations(new VariationalOptions());
        var y = VariationalTestDriver.Identity(equations);
        const double h = 60.0;

        // Act
        VariationalTestDriver.Integrate(forces, equations, context, state, h, 1, y, Array.Empty<double>());

        // Assert
        var reference = new RK78Stepper(forces) { Context = context };
        reference.Reset(state.Observer, state.Frame, state.Epoch);
        var expected = new double[36];
        double worstEstimate = 0.0;
        for (int column = 0; column < 6; column++)
        {
            double initialStep = 1e-3 * (column < 3 ? state.Position.Magnitude() : state.Velocity.Magnitude());
            for (int row = 0; row < 6; row++)
            {
                int c = column;
                int r = row;
                var (derivative, error) = RiddersDerivative.Derivative(delta =>
                {
                    var x0 = Perturbed(state, c, delta);
                    reference.Step(x0.Position, x0.Velocity, state.Epoch, 0.0, h, out var p, out var v, out _, out _);
                    return r < 3 ? RiddersDerivative.Component(p, r) : RiddersDerivative.Component(v, r - 3);
                }, 0.0, initialStep);
                expected[row * 6 + column] = derivative;
                worstEstimate = System.Math.Max(worstEstimate, System.Math.Abs(error));
            }
        }

        double relativeEstimate = worstEstimate / System.Math.Sqrt(expected.Sum(e => e * e));
        _output.WriteLine($"{name}: Ridders estimate {relativeEstimate:E2}, " +
                          $"relative error {RiddersDerivative.RelativeFrobeniusError(y, expected):E2}");
        Assert.True(relativeEstimate < RiddersEstimateBound, $"Ridders estimate {relativeEstimate:E2}");
        double relativeError = RiddersDerivative.RelativeFrobeniusError(y, expected);
        Assert.True(relativeError < RiddersTolerance, $"relative error {relativeError:E2}");
    }

    [Fact]
    public void Step_CountsThirteenStageJacobiansPerStep()
    {
        // Arrange
        var equations = new VariationalEquations(new VariationalOptions());
        var y = VariationalTestDriver.Identity(equations);

        // Act
        VariationalTestDriver.Integrate(new List<ForceBase> { new LinearTestForce(Omega) }, equations,
            new ForceEvaluationContext(1.0, 0.0, 0.0), State(Vector3.VectorX, Vector3.VectorY), StepSize, 3, y,
            Array.Empty<double>());

        // Assert
        Assert.Equal(39, equations.StageJacobianEvaluations);
    }

    [Fact]
    public void ScaledError_IsTheStateErrorOfTheScaledUnitStates()
    {
        // Arrange: for a linear force, the embedded error of column j of Y is the embedded error of the state step from
        // e_j (Cd = Cr = 0), and that of a parameter column is the one from the zero state with the parameter at 1.
        const double absoluteTolerance = 1e-9;
        const double relativeTolerance = 1e-10;
        var forces = new List<ForceBase> { new LinearTestForce(Omega, Damping, DragDirection, ReflectivityDirection) };
        var equations = new VariationalEquations(new VariationalOptions(ForceParameters.DragCoefficient |
                                                                         ForceParameters.ReflectivityCoefficient));
        var start = State(new Vector3(7e6, -1e5, 3e5), new Vector3(10.0, 7.5e3, -40.0));
        var context = new ForceEvaluationContext(100.0, 2.2, 1.5);
        var y0 = VariationalTestDriver.Identity(equations);
        var y1 = (double[])y0.Clone();
        VariationalTestDriver.Integrate(forces, equations, context, start, 600.0, 1, y1, Array.Empty<double>());
        var scales = new double[8];
        equations.ColumnScales(start.Position, start.Velocity, context, scales);

        // Act
        double error = equations.ScaledError(y0, y1, scales, absoluteTolerance, relativeTolerance);

        // Assert
        double expected = 0.0;
        var stepper = new RK78Stepper(forces);
        stepper.Reset(start.Observer, start.Frame, start.Epoch);
        for (int column = 0; column < 8; column++)
        {
            var unit = new double[6];
            if (column < 6)
            {
                unit[column] = 1.0;
            }

            stepper.Context = new ForceEvaluationContext(1.0, column == 6 ? 1.0 : 0.0, column == 7 ? 1.0 : 0.0);
            stepper.Step(new Vector3(unit[0], unit[1], unit[2]), new Vector3(unit[3], unit[4], unit[5]), start.Epoch,
                0.0, 600.0, out var p, out var v, out var errP, out var errV);
            var start6 = unit;
            var end6 = new[] { p.X, p.Y, p.Z, v.X, v.Y, v.Z };
            var error6 = new[] { errP.X, errP.Y, errP.Z, errV.X, errV.Y, errV.Z };
            for (int row = 0; row < 6; row++)
            {
                expected = System.Math.Max(expected, RK78Integrator.ScaledComponentError(start6[row] * scales[column],
                    end6[row] * scales[column], error6[row] * scales[column], absoluteTolerance, relativeTolerance));
            }
        }

        _output.WriteLine($"scaled error {error:E6}, from the unit states {expected:E6}");
        Assert.True(expected > 0.0);
        Assert.Equal(expected, error, expected * RoundingTolerance);
    }

    [Fact]
    public void ScaledError_BeforeAnyStep_Throws()
    {
        // Arrange
        var equations = new VariationalEquations(new VariationalOptions());
        var y = VariationalTestDriver.Identity(equations);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            equations.ScaledError(y, y, new double[6], 1e-9, 1e-9));

        // Assert
        Assert.Contains("step", exception.Message);
    }

    [Fact]
    public void ColumnScales_AreTheSizesOfTheInitialPerturbations()
    {
        // Arrange
        var equations = new VariationalEquations(new VariationalOptions(ForceParameters.DragCoefficient |
                                                                         ForceParameters.ReflectivityCoefficient));
        var scales = new double[8];

        // Act
        equations.ColumnScales(new Vector3(3.0, 4.0, 0.0), new Vector3(0.0, 6.0, 8.0),
            new ForceEvaluationContext(100.0, -2.2, 0.0), scales);

        // Assert: |r|, |v|, |Cd|, and 1 for a zero Cr
        Assert.Equal(new[] { 5.0, 5.0, 5.0, 10.0, 10.0, 10.0, 2.2, 1.0 }, scales);
    }

    [Fact]
    public void Step_AllocatesNothing()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var forces = ThirdBodyForces();
        var context = ForceEvaluationContext.FromSpacecraft(PartialsTestCases.Spacecraft(state));
        var qc = new[] { 1e-12, 0.0, 0.0, 0.0, 1e-12, 0.0, 0.0, 0.0, 1e-12 };
        var equations = new VariationalEquations(new VariationalOptions(ForceParameters.DragCoefficient,
            new ConstantProcessNoise(qc)));
        var stepper = new RK78Stepper(forces) { Context = context };
        stepper.Reset(state.Observer, state.Frame, state.Epoch);
        stepper.Step(state.Position, state.Velocity, state.Epoch, 0.0, 60.0, out _, out _, out _, out _);
        var y0 = VariationalTestDriver.Identity(equations);
        var y1 = new double[equations.YLength];
        var q0 = new double[VariationalEquations.CovarianceLength];
        var q1 = new double[VariationalEquations.CovarianceLength];
        equations.Step(stepper.StageStates, forces, context, 60.0, y0, q0, y1, q1);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 10; k++)
        {
            equations.Step(stepper.StageStates, forces, context, 60.0, y0, q0, y1, q1);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Step_WithWrongBuffers_Throws()
    {
        // Arrange
        var state = State(Vector3.VectorX, Vector3.VectorY);
        var forces = new List<ForceBase> { new LinearTestForce(Omega) };
        var context = new ForceEvaluationContext(1.0, 0.0, 0.0);
        var stepper = new RK78Stepper(forces);
        stepper.Reset(state.Observer, state.Frame, state.Epoch);
        stepper.Step(state.Position, state.Velocity, state.Epoch, 0.0, StepSize, out _, out _, out _, out _);
        var withNoise = new VariationalEquations(new VariationalOptions(
            ProcessNoise: new ConstantProcessNoise(new double[9])));
        var y = VariationalTestDriver.Identity(withNoise);
        var q = new double[VariationalEquations.CovarianceLength];

        // Act and assert
        Assert.Throws<ArgumentException>(() =>
            withNoise.Step(stepper.StageStates.Take(12).ToList(), forces, context, StepSize, y, q, y, q));
        Assert.Throws<ArgumentException>(() =>
            withNoise.Step(stepper.StageStates, forces, context, StepSize, new double[35], q, y, q));
        Assert.Throws<ArgumentException>(() =>
            withNoise.Step(stepper.StageStates, forces, context, StepSize, y, q, new double[37], q));
        Assert.Throws<ArgumentException>(() =>
            withNoise.Step(stepper.StageStates, forces, context, StepSize, y, Array.Empty<double>(), y, q));
        Assert.Throws<ArgumentException>(() =>
            withNoise.Step(stepper.StageStates, forces, context, StepSize, y, q, y, new double[20]));
        Assert.Throws<ArgumentNullException>(() =>
            withNoise.Step(null, forces, context, StepSize, y, q, y, q));
        Assert.Throws<ArgumentNullException>(() =>
            withNoise.Step(stepper.StageStates, null, context, StepSize, y, q, y, q));
        Assert.Throws<ArgumentException>(() => withNoise.SetIdentity(new double[35]));
        Assert.Throws<ArgumentException>(() =>
            withNoise.ColumnScales(Vector3.VectorX, Vector3.VectorY, context, new double[7]));
    }

    private List<ForceBase> ThirdBodyForces()
    {
        var forces = new List<ForceBase>
        {
            new GravitationalAcceleration(_cases.Earth),
            new ThirdBodyPerturbation(_cases.Moon, _cases.Earth),
            new ThirdBodyPerturbation(_cases.Sun, _cases.Earth)
        };
        var cache = PartialsTestCases.Cache(_cases.Earth, _cases.Earth, _cases.Moon, _cases.Sun);
        foreach (var force in forces)
        {
            force.EphemerisCache = cache;
        }

        return forces;
    }

    private StateVector State(Vector3 position, Vector3 velocity)
    {
        return new StateVector(position, velocity, _cases.Earth, PartialsTestCases.Epoch, Frames.Frame.ICRF);
    }

    private static StateVector Perturbed(StateVector state, int component, double delta)
    {
        var position = state.Position;
        var velocity = state.Velocity;
        switch (component)
        {
            case 0: position = new Vector3(position.X + delta, position.Y, position.Z); break;
            case 1: position = new Vector3(position.X, position.Y + delta, position.Z); break;
            case 2: position = new Vector3(position.X, position.Y, position.Z + delta); break;
            case 3: velocity = new Vector3(velocity.X + delta, velocity.Y, velocity.Z); break;
            case 4: velocity = new Vector3(velocity.X, velocity.Y + delta, velocity.Z); break;
            default: velocity = new Vector3(velocity.X, velocity.Y, velocity.Z + delta); break;
        }

        return new StateVector(position, velocity, state.Observer, state.Epoch, state.Frame);
    }

    // Each 3×3 block of Φ is a multiple of the identity: compare each block with its own norm.
    private void AssertPhiBlocks(double[] y, int columns, double rr, double rv, double vr, double vv)
    {
        var expected = new[] { rr, rv, vr, vv };
        for (int block = 0; block < 4; block++)
        {
            int rowOffset = block < 2 ? 0 : 3;
            int columnOffset = block % 2 == 0 ? 0 : 3;
            var actual = new double[9];
            var reference = new double[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    actual[3 * i + j] = y[(rowOffset + i) * columns + columnOffset + j];
                    reference[3 * i + j] = i == j ? expected[block] : 0.0;
                }
            }

            double error = RiddersDerivative.RelativeFrobeniusError(actual, reference);
            _output.WriteLine($"Φ block {block}: relative error {error:E2}");
            Assert.True(error < ClosedFormTolerance, $"block {block}: relative error {error:E2}");
        }
    }

    private void AssertColumn(double[] y, int columns, int column, Vector3 position, Vector3 velocity)
    {
        var actual = Enumerable.Range(0, 6).Select(row => y[row * columns + column]).ToArray();
        var expected = new[] { position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z };
        double error = RiddersDerivative.RelativeFrobeniusError(actual, expected);
        _output.WriteLine($"column {column}: relative error {error:E2}");
        Assert.True(error < ClosedFormTolerance, $"column {column}: relative error {error:E2}");
    }

    // Q = [[Qc rr, Qc rv], [Qc rv, Qc vv]]: compare each 3×3 block with its own norm.
    private void AssertQBlocks(double[] q, double[] qc, double rr, double rv, double vv, double tolerance)
    {
        var factors = new[] { rr, rv, vv };
        var offsets = new[] { (0, 0), (0, 3), (3, 3) };
        for (int block = 0; block < 3; block++)
        {
            var (rowOffset, columnOffset) = offsets[block];
            var actual = new double[9];
            var reference = new double[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int row = rowOffset + i;
                    int column = columnOffset + j;
                    actual[3 * i + j] = q[VariationalEquations.CovarianceIndex(System.Math.Min(row, column),
                        System.Math.Max(row, column))];
                    reference[3 * i + j] = qc[3 * i + j] * factors[block];
                }
            }

            double error = RiddersDerivative.RelativeFrobeniusError(actual, reference);
            _output.WriteLine($"Q block {block}: relative error {error:E2}");
            Assert.True(error < tolerance, $"block {block}: relative error {error:E2}");
        }
    }
}
