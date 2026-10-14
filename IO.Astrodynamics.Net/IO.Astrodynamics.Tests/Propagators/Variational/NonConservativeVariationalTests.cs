// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// Φ and Ψ of the reference cases with non-conservative forces, R2 (drag) and R3 (SRP, an eclipse on every revolution),
/// against central differences of whole propagations over one day: the partials of step 5b (B5, B6) end to end; and the
/// covariance of C2 with the coefficient as a consider parameter, against the covariance through those differences.
/// </summary>
/// <remarks>
/// <para>
/// Φ and Ψ come from RK7(8) at 1e-13, the step controlled on the state only (A2). The reference propagations run at
/// 1e-14: they choose their own steps, so the differences carry the noise of the integration, which bounds what they
/// resolve. Steps of 1 m and 1 mm/s on the state, as in the conservative cases, and 1 % of the coefficient on Cd and Cr.
/// </para>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-13): R2, Φ 5.0e-7 and Ψ_Cd 1.1e-7; R3, Φ 6.2e-7 and Ψ_Cr 3.3e-5 (2.4e-6, 1.2e-7,
/// 6.9e-6 and 5.3e-5 with the reference propagations at 1e-13, before the H3 review of #378). The covariance through
/// the reference, Φ_ref P0 Φ_refᵀ + σ² ψ_ref ψ_refᵀ: R2 1.2e-7, R3 3.6e-7 (2026-10-14).
/// </para>
/// <para>
/// What each figure measures (H3 review of #378). The reference for Ψ_Cr of R3, 44 m per unit Cr after one day, is off by
/// 6.1e-5, 6.6e-5, 3.0e-4 and 4.9e-5 at 1e-13 for steps of 1 %, 5 %, 10 % and 20 % of Cr, and by 2.2e-5, 3.7e-5, 7.5e-5
/// and 7.2e-6 at 1e-14, against Ψ integrated with Φ and Ψ in the step control: noise, not the δ² truncation, which
/// Richardson's extrapolation does not reduce. Ψ_Cd of R2 is resolved better: the 1 % reference is off by its δ²
/// truncation only (1.2e-7, 1.2e-5 at 10 %). So the comparison with the reference checks the partials end to end only
/// down to that noise. The integration error of Ψ itself, which is not in the step control, is measured against the same
/// propagation with Φ and Ψ in the step control (<c>IncludeInStepControl</c>, consistent to 3e-9 at 1e-12 and 1e-13):
/// 1.3e-5 for R3, asserted below, and 1.9e-10 for R2 (not asserted: that propagation takes 7 minutes). The partials
/// themselves are checked against Ridders' derivative of the acceleration in <c>DragPartialsTests</c> and
/// <c>RadiationPressurePartialsTests</c>.
/// </para>
/// <para>
/// The full study of F1, with every case and block and the plateau of the steps, comes with step 9; this test checks
/// the partials of drag and SRP against the threshold of the specification for R2 to R5 (1e-4). R3 crosses the
/// penumbra twice per revolution: with the derivative of the shadow fraction neglected, Φ of a LEO case with SRP was off
/// by 1.05e-4, which is why the SRP partials take the default central differences in the penumbra.
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class NonConservativeVariationalTests : IClassFixture<ReferenceCases>
{
    private const double Tolerance = 1e-13;

    private const double ReferenceTolerance = 1e-14;

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public NonConservativeVariationalTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [Theory]
    [InlineData("R2")]
    [InlineData("R3")]
    public void PhiPsiAndCovariance_MatchCentralDifferencesOfWholePropagations(string name)
    {
        // Arrange: the covariance too (C2), from a P0 given in RTN and the coefficient as a consider parameter, σ = 10 %
        var referenceCase = _cases[name];
        var start = referenceCase.InitialState;
        var parameter = referenceCase.AtmosphericDrag ? ForceParameters.DragCoefficient : ForceParameters.ReflectivityCoefficient;
        double sigma = referenceCase.AtmosphericDrag ? 0.22 : 0.15;
        var consider = referenceCase.AtmosphericDrag ? new ConsiderParameters(sigma) : new ConsiderParameters(null, sigma);
        var withCovariance = CovariancePropagationTests.WithCovariance(start);
        var solution = Propagate(referenceCase, withCovariance, 2.2, 1.5, Tolerance,
            new VariationalOptions(parameter, PropagateCovariance: true, ConsiderParameters: consider));
        var (phi, psi) = PhiAndPsi(solution, referenceCase);
        var covariance = new double[36];
        solution.EvaluateCovariance(referenceCase.InitialState.Epoch.Add(referenceCase.Duration), covariance);

        // Act
        var expectedPhi = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1.0 : 1e-3;
            var plus = FinalState(Propagate(referenceCase, Perturbed(start, column, delta), 2.2, 1.5, ReferenceTolerance,
                null));
            var minus = FinalState(Propagate(referenceCase, Perturbed(start, column, -delta), 2.2, 1.5,
                ReferenceTolerance, null));
            for (int row = 0; row < 6; row++)
            {
                expectedPhi[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        bool drag = referenceCase.AtmosphericDrag;
        double coefficient = drag ? 2.2 : 1.5;
        double step = 0.01 * coefficient;
        var psiPlus = FinalState(Propagate(referenceCase, start, drag ? 2.2 + step : 2.2, drag ? 1.5 : 1.5 + step,
            ReferenceTolerance, null));
        var psiMinus = FinalState(Propagate(referenceCase, start, drag ? 2.2 - step : 2.2, drag ? 1.5 : 1.5 - step,
            ReferenceTolerance, null));
        var expectedPsi = psiPlus.Zip(psiMinus, (p, m) => (p - m) / (2.0 * step)).ToArray();

        // The covariance through the reference: Φ_ref P0 Φ_refᵀ + σ² ψ_ref ψ_refᵀ
        var phiReference = CovarianceAssert.ToMathNet(expectedPhi, 6);
        var p0 = CovarianceAssert.ToMathNet(Symmetrized(withCovariance.Covariance.Value), 6);
        var psiReference = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(expectedPsi);
        var expectedCovariance = CovarianceAssert.ToArray(phiReference * p0 * phiReference.Transpose() +
                                                          sigma * sigma * psiReference.OuterProduct(psiReference));

        // Assert: the threshold of the specification for R2 to R5 (F1)
        double phiError = StmMeasures.WorstBlockError(phi, expectedPhi);
        double covarianceError = StmMeasures.WorstBlockError(covariance, expectedCovariance);
        _output.WriteLine($"{name}: P {covarianceError:E2} (worst 3×3 block)");
        Assert.True(covarianceError < 1e-4, $"P: {covarianceError:E2}");
        double psiPositionError = RiddersDerivative.RelativeFrobeniusError(psi[..3], expectedPsi[..3]);
        double psiVelocityError = RiddersDerivative.RelativeFrobeniusError(psi[3..], expectedPsi[3..]);
        _output.WriteLine($"{name}: Φ {phiError:E2} (worst 3×3 block), Ψ_{(drag ? "Cd" : "Cr")} {psiPositionError:E2} " +
                          $"(position), {psiVelocityError:E2} (velocity), |Ψ| = {Norm(expectedPsi[..3]):E2} m");
        Assert.True(phiError < 1e-4, $"Φ: {phiError:E2}");
        Assert.True(psiPositionError < 1e-4, $"Ψ, position: {psiPositionError:E2}");
        Assert.True(psiVelocityError < 1e-4, $"Ψ, velocity: {psiVelocityError:E2}");
    }

    [Fact]
    public void R3_Psi_MatchesTheSamePropagationWithPsiInTheStepControl()
    {
        // Arrange: the integration error of Ψ, which the step control on the state does not see (A2), against the same
        // propagation with Φ and Ψ in the step control, free of the noise of the central differences of propagations.
        // It runs through the eclipses, where the SRP partials are continuous since the H3 review of #378: before, the
        // step control on Φ and Ψ rejected 100 steps down to 1e-6 s at the first umbra entry.
        const string name = "R3";
        var referenceCase = _cases[name];
        var start = referenceCase.InitialState;
        var parameter = referenceCase.AtmosphericDrag ? ForceParameters.DragCoefficient : ForceParameters.ReflectivityCoefficient;

        // Act
        var (_, psi) = PhiAndPsi(Propagate(referenceCase, start, 2.2, 1.5, Tolerance,
            new VariationalOptions(parameter)), referenceCase);
        var (_, expected) = PhiAndPsi(Propagate(referenceCase, start, 2.2, 1.5, Tolerance,
            new VariationalOptions(parameter, IncludeInStepControl: true)), referenceCase);

        // Assert: the threshold of the specification for R2 to R5 (F1); measured 1.3e-5 (Linux, .NET 10, 2026-10-13)
        double positionError = RiddersDerivative.RelativeFrobeniusError(psi[..3], expected[..3]);
        double velocityError = RiddersDerivative.RelativeFrobeniusError(psi[3..], expected[3..]);
        _output.WriteLine($"{name}: Ψ against Ψ in the step control {positionError:E2} (position), " +
                          $"{velocityError:E2} (velocity)");
        Assert.True(positionError < 1e-4, $"Ψ, position: {positionError:E2}");
        Assert.True(velocityError < 1e-4, $"Ψ, velocity: {velocityError:E2}");
    }

    private static (double[] Phi, double[] Psi) PhiAndPsi(PropagationSolution solution, ReferenceCase referenceCase)
    {
        var y = new double[42];
        solution.EvaluateVariational(referenceCase.InitialState.Epoch.Add(referenceCase.Duration), y,
            new double[solution.Dynamics.Options.PropagateCovariance ? VariationalEquations.CovarianceLength : 0]);
        var phi = new double[36];
        var psi = new double[6];
        for (int i = 0; i < 6; i++)
        {
            Array.Copy(y, 7 * i, phi, 6 * i, 6);
            psi[i] = y[7 * i + 6];
        }

        return (phi, psi);
    }

    private static PropagationSolution Propagate(ReferenceCase referenceCase, StateVector start, double dragCoefficient,
        double reflectivityCoefficient, double tolerance, VariationalOptions options)
    {
        var spacecraft = new Spacecraft(-1965, "NONCONSERVATIVE", 100.0, 1000.0, new Clock("nonconservative", 65536),
            start, 10.0, dragCoefficient, null, reflectivityCoefficient);
        return spacecraft.Propagate(new TimeSystem.Window(start.Epoch, start.Epoch.Add(referenceCase.Duration)),
            referenceCase.Bodies, new RK78Integrator(tolerance, tolerance), referenceCase.AtmosphericDrag,
            referenceCase.SolarRadiationPressure, TimeSpan.FromSeconds(3600.0), options);
    }

    private static double[] FinalState(PropagationSolution solution)
    {
        var step = solution.Segments[^1].Steps[^1];
        return new[]
        {
            step.EndPosition.X, step.EndPosition.Y, step.EndPosition.Z,
            step.EndVelocity.X, step.EndVelocity.Y, step.EndVelocity.Z
        };
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

    private static double Norm(double[] v) => System.Math.Sqrt(v.Sum(x => x * x));

    private static double[] Symmetrized(IO.Astrodynamics.Math.Matrix matrix)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                result[6 * i + j] = 0.5 * (matrix.Get(i, j) + matrix.Get(j, i));
            }
        }

        return result;
    }
}
