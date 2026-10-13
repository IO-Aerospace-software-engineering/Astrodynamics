// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The plateau study that sets the relative step of the density gradient of the drag partials
/// (<see cref="AtmosphericDrag.DensityGradientRelativeStep"/>): the error of ∇ρ, and of the ∂a/∂r block it feeds,
/// against Ridders' reference, as a function of the relative step, at the reference states with an atmosphere.
/// </summary>
/// <remarks>
/// <para>
/// The reference of ∇ρ is Ridders' derivative of the density chain itself; the reference of ∂a/∂r is Ridders'
/// derivative of the whole acceleration. GEO is left out, as for the other drag tests.
/// </para>
/// <para>
/// Bounds at the default step: relative error of ∇ρ and of ∂a/∂r below 1e-8, two orders of magnitude inside the 1e-6
/// of the specification for the analytic partials (B7), and Ridders' error estimate below 1e-8. In-sample, as the
/// study of the default steps: it guards the plateau against a change of the atmospheric model.
/// </para>
/// <para>
/// To write the curves, set the environment variable <c>IO_ASTRODYNAMICS_STUDY_OUTPUT</c> to a directory: the test then
/// writes <c>density_step_study.csv</c> there (state, quantity, relative step, relative error, Ridders' error estimate).
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class DensityGradientStepStudyTests : IClassFixture<PartialsTestCases>
{
    private const string OutputVariable = "IO_ASTRODYNAMICS_STUDY_OUTPUT";

    // Half-decade grid from 1e-12 to 1e-2, plus the default step.
    private static readonly double[] RelativeSteps = Enumerable.Range(0, 21)
        .Select(k => System.Math.Pow(10.0, -12.0 + 0.5 * k))
        .Append(AtmosphericDrag.DensityGradientRelativeStep)
        .Distinct()
        .Order()
        .ToArray();

    private readonly PartialsTestCases _cases;

    public DensityGradientStepStudyTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    [Fact]
    public void DefaultStep_LiesOnThePlateauOfTheDensityGradient()
    {
        // Arrange
        var rows = new List<StudyRow>();
        foreach (var name in PartialsTestCases.StateNames.Where(s => s != PartialsTestCases.Geo))
        {
            var state = PartialsTestCases.State(name, _cases.Earth);
            var spacecraft = PartialsTestCases.Spacecraft(state);
            var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
            var force = new AtmosphericDrag(spacecraft, _cases.Earth);
            var rotation = force.BodyFixedRotation(state);
            var relative = force.GetRelativeState(state);
            double speed = relative.AtmosphereRelativeVelocity.Magnitude();
            double k = -0.5 * spacecraft.SectionalArea / context.TotalMass * context.DragCoefficient;
            var accelerationPerDensity = relative.AtmosphereRelativeVelocity * (k * speed);

            var gradientReference = new double[3];
            var gradientReferenceErrors = new double[3];
            for (int j = 0; j < 3; j++)
            {
                int column = j;
                (gradientReference[j], gradientReferenceErrors[j]) = RiddersDerivative.Derivative(
                    t => force.Density(With(state.Position, column, t), rotation, state.Epoch),
                    RiddersDerivative.Component(state.Position, j), 1e-3 * state.Position.Magnitude());
            }

            double gradientReferenceError = System.Math.Sqrt(gradientReferenceErrors.Sum(e => e * e) /
                                                             gradientReference.Sum(e => e * e));
            var dadrReference = RiddersDerivative.StatePartials(force, state, context,
                1e-3 * state.Position.Magnitude(), 1e-3 * state.Velocity.Magnitude());

            // The velocity chain of ∂a/∂r, which does not depend on the step: the partials minus their gradient term
            var dadr = new double[9];
            force.AccumulateStatePartials(state, context, dadr, new double[9]);
            var gradient = new double[3];
            force.DensityGradient(state.Position, rotation, state.Epoch, gradient);
            var velocityChain = new double[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    velocityChain[3 * i + j] = dadr[3 * i + j] -
                                               RiddersDerivative.Component(accelerationPerDensity, i) * gradient[j];
                }
            }

            // Act
            foreach (var step in RelativeSteps)
            {
                force.DensityGradient(state.Position, rotation, state.Epoch, gradient, step);
                rows.Add(new StudyRow(name, "gradient", step,
                    RiddersDerivative.RelativeFrobeniusError(gradient, gradientReference), gradientReferenceError));

                var block = new double[9];
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        block[3 * i + j] = velocityChain[3 * i + j] +
                                           RiddersDerivative.Component(accelerationPerDensity, i) * gradient[j];
                    }
                }

                rows.Add(new StudyRow(name, "dadr", step,
                    RiddersDerivative.RelativeFrobeniusError(block, dadrReference.Dadr), dadrReference.DadrError));
            }
        }

        // Assert
        WriteCsvIfRequested(rows);
        foreach (var row in rows.Where(r => r.RelativeStep == AtmosphericDrag.DensityGradientRelativeStep))
        {
            string what = $"{row.State}, {row.Quantity}";
            Assert.True(row.Error < 1e-8, $"{what}: relative error {row.Error:E2} at the default step");
            Assert.True(row.ReferenceError < 1e-8, $"{what}: reference error estimate {row.ReferenceError:E2}");
        }
    }

    private static Vector3 With(in Vector3 vector, int index, double value)
    {
        return index switch
        {
            0 => new Vector3(value, vector.Y, vector.Z),
            1 => new Vector3(vector.X, value, vector.Z),
            _ => new Vector3(vector.X, vector.Y, value)
        };
    }

    private static void WriteCsvIfRequested(IReadOnlyList<StudyRow> rows)
    {
        var directory = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var lines = new List<string> { "state,quantity,relative_step,relative_error,reference_error" };
        lines.AddRange(rows.Select(r => string.Join(",", r.State, r.Quantity,
            r.RelativeStep.ToString("R", CultureInfo.InvariantCulture),
            r.Error.ToString("R", CultureInfo.InvariantCulture),
            r.ReferenceError.ToString("R", CultureInfo.InvariantCulture))));
        File.WriteAllLines(Path.Combine(directory, "density_step_study.csv"), lines);
    }

    private sealed record StudyRow(string State, string Quantity, double RelativeStep, double Error,
        double ReferenceError);
}
