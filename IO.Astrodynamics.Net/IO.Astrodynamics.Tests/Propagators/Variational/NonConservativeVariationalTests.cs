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
/// against central differences of whole propagations over one day: the partials of step 5b (B5, B6) end to end.
/// </summary>
/// <remarks>
/// <para>
/// RK7(8) at 1e-13, the tolerance of the reference propagations of F1. Steps of 1 m and 1 mm/s on the state, as in the
/// conservative cases, and 1 % of the coefficient on Cd and Cr. The perturbed propagations choose their own steps, so the
/// differences carry the integration error, which bounds what they resolve: R2 without its drag gives 1.1e-6 to 2.4e-6
/// (measured on the LEO 400 km state, 2026-10-12).
/// </para>
/// <para>
/// The full study of F1, with every case and block and the plateau of the steps, comes with step 9; this test checks
/// the partials of drag and SRP against the threshold of the specification for R2 to R5 (1e-4). R3 crosses the
/// penumbra twice per revolution: with the derivative of the shadow fraction neglected, Φ of a LEO case with SRP was off
/// by 1.05e-4, which is why the SRP partials take the default central differences in the penumbra.
/// </para>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-12): R2, Φ 2.4e-6 and Ψ_Cd 1.2e-7; R3, Φ 6.9e-6 and Ψ_Cr 5.3e-5. The values are the
/// same, to the digits shown, with the central-difference partials that preceded step 5b. Ψ_Cr of R3, 44 m per unit
/// Cr after one day, is the smallest sensitivity here, so the error of the integration weighs most on it.
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class NonConservativeVariationalTests : IClassFixture<ReferenceCases>
{
    private const double Tolerance = 1e-13;

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
    public void PhiAndPsi_MatchCentralDifferencesOfWholePropagations(string name)
    {
        // Arrange
        var referenceCase = _cases[name];
        var start = referenceCase.InitialState;
        var end = start.Epoch.Add(referenceCase.Duration);
        var parameter = referenceCase.AtmosphericDrag ? ForceParameters.DragCoefficient : ForceParameters.ReflectivityCoefficient;
        var solution = Propagate(referenceCase, start, 2.2, 1.5, new VariationalOptions(parameter));
        var y = new double[42];
        solution.EvaluateVariational(end, y, Array.Empty<double>());
        var phi = new double[36];
        var psi = new double[6];
        for (int i = 0; i < 6; i++)
        {
            Array.Copy(y, 7 * i, phi, 6 * i, 6);
            psi[i] = y[7 * i + 6];
        }

        // Act
        var expectedPhi = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1.0 : 1e-3;
            var plus = FinalState(Propagate(referenceCase, Perturbed(start, column, delta), 2.2, 1.5, null));
            var minus = FinalState(Propagate(referenceCase, Perturbed(start, column, -delta), 2.2, 1.5, null));
            for (int row = 0; row < 6; row++)
            {
                expectedPhi[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        bool drag = referenceCase.AtmosphericDrag;
        double coefficient = drag ? 2.2 : 1.5;
        double step = 0.01 * coefficient;
        var psiPlus = FinalState(Propagate(referenceCase, start, drag ? 2.2 + step : 2.2, drag ? 1.5 : 1.5 + step, null));
        var psiMinus = FinalState(Propagate(referenceCase, start, drag ? 2.2 - step : 2.2, drag ? 1.5 : 1.5 - step, null));
        var expectedPsi = psiPlus.Zip(psiMinus, (p, m) => (p - m) / (2.0 * step)).ToArray();

        // Assert: the threshold of the specification for R2 to R5 (F1)
        double phiError = StmMeasures.WorstBlockError(phi, expectedPhi);
        double psiPositionError = RiddersDerivative.RelativeFrobeniusError(psi[..3], expectedPsi[..3]);
        double psiVelocityError = RiddersDerivative.RelativeFrobeniusError(psi[3..], expectedPsi[3..]);
        _output.WriteLine($"{name}: Φ {phiError:E2} (worst 3×3 block), Ψ_{(drag ? "Cd" : "Cr")} {psiPositionError:E2} " +
                          $"(position), {psiVelocityError:E2} (velocity), |Ψ| = {Norm(expectedPsi[..3]):E2} m, " +
                          $"{solution.Segments.Sum(s => s.Steps.Count)} steps");
        Assert.True(phiError < 1e-4, $"Φ: {phiError:E2}");
        Assert.True(psiPositionError < 1e-4, $"Ψ, position: {psiPositionError:E2}");
        Assert.True(psiVelocityError < 1e-4, $"Ψ, velocity: {psiVelocityError:E2}");
    }

    private static PropagationSolution Propagate(ReferenceCase referenceCase, StateVector start, double dragCoefficient,
        double reflectivityCoefficient, VariationalOptions options)
    {
        var spacecraft = new Spacecraft(-1965, "NONCONSERVATIVE", 100.0, 1000.0, new Clock("nonconservative", 65536),
            start, 10.0, dragCoefficient, null, reflectivityCoefficient);
        return spacecraft.Propagate(new TimeSystem.Window(start.Epoch, start.Epoch.Add(referenceCase.Duration)),
            referenceCase.Bodies, new RK78Integrator(Tolerance, Tolerance), referenceCase.AtmosphericDrag,
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
}
