// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The steps of the central differences of whole propagations (F1), chosen on the plateau of their error.
/// </summary>
/// <remarks>
/// <para>
/// For each reference case, Φ and Ψ by central differences of whole propagations at the tolerance of the reference
/// (<see cref="ReferenceCases.ReferenceTolerance"/>), from the exact state (#398), against Φ and Ψ integrated at
/// <see cref="ReferenceCases.Tolerance"/>, with Φ and Ψ in the step control when the case has SRP (Ψ_Cr is held to F1 on
/// that option, decision of S. Guillet, 2026-10-15; without SRP the option changes nothing that matters here and costs
/// minutes for R2), at the end of the case: the worst 3×3 block
/// of Φ, and the worst of the position and velocity rows of each Ψ column. Position steps from 1 cm to 1 km, the
/// velocity step 1e-3 s⁻¹ times the position step, and the coefficients from 0.1 % to 30 %. On the left the
/// integration noise of the perturbed propagations, which choose their own steps, divided by the step; on the right the
/// truncation of the central difference, of order δ² (<i>Numerical Recipes</i>, 3rd edition, section 5.7). The
/// integrated Φ is checked independently (the Keplerian STM of F2, the partials against Ridders' derivative), so the
/// smallest error of the sweep is where the reference resolves best.
/// </para>
/// <para>
/// The test asserts that the steps of <see cref="ReferenceCases"/> are within a factor of 2 of the smallest error of
/// their sweep, and that the reference at 1e-14 resolves Φ at least as well as at the 1e-13 of the specification, which
/// justifies its tolerance (decision of S. Guillet, 2026-10-16). Measured (Linux, .NET 10, 2026-10-16), the smallest
/// error of Φ at 1e-14 and at 1e-13: R1 1.7e-8 and 2.3e-8, R2 4.6e-8 and 1.4e-7, R3 1.9e-7 and 1.0e-6, R4 2.8e-8 and
/// 5.6e-8, R5 6.7e-7 and 7.8e-7. Chosen steps: 10 m and 1 cm/s for R1 to R4, 1 m and 1 mm/s for R5 (6.7e-7, 8.7e-7 at
/// 10 m); Cd 1 % for R2 (1.2e-7) and 5 % for R5 (5.3e-8); Cr 10 % for R3 (3.8e-6) and R4 (9.7e-10).
/// </para>
/// <para>
/// For Φ every sweep has its minimum between the two sides. For Ψ two do not: the sweep of Ψ_Cr of R3 is not monotone
/// (1.0e-5, 3.8e-6, 1.7e-5, 4.3e-6 at 5 %, 10 %, 20 % and 30 %), and the Ψ errors of the eclipsing case still fall at
/// the edge of the sweep (Cd 1.1e-7, Cr 1.8e-5 at 30 %). There the sweep shows the noise floor of the reference rather
/// than a plateau, and the factor of 2 holds at the edge. Since #398 (2026-10-17) the sweep of Ψ_Cr of the eclipsing case
/// is published and not asserted (decision of S. Guillet, 2026-10-17): the propagations start from exactly the state
/// given, about 1e-9 m from the start through the Keplerian elements before, and the sweep became 1.4e-3, 7.2e-4,
/// 2.4e-4, 1.1e-5, 1.3e-4, 4.7e-5 from 0.1 % to 30 %; on <c>main</c> before the fix, the start of the reference moved by
/// 2e-9 m along x alone took it from 3.7e-3, 1.8e-4, 2.3e-4, 1.3e-4, 3.4e-5, 1.8e-5 to 2.1e-2, 7.0e-5, 2.4e-5, 8.1e-5,
/// 7.2e-6, 1.5e-5 (H3 review of #399), the noise of the reference, so no step is on a plateau there. The study evaluates the end of the arc, while F1 asserts
/// over the arc: <c>StmValidationTests</c> holds Ψ over the arc relative to its largest value, which does not depend on
/// the step of the reference. To write the curves, set <c>IO_ASTRODYNAMICS_STUDY_OUTPUT</c> to a directory: the test writes
/// <c>whole_propagation_step_study_{case}.csv</c> there (quantity, step, error).
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class WholePropagationStepStudyTests : IClassFixture<ReferenceCases>
{
    private const string OutputVariable = "IO_ASTRODYNAMICS_STUDY_OUTPUT";
    private const int NaifId = -1971;

    private static readonly double[] PositionSteps = { 0.01, 0.1, 1.0, 10.0, 100.0, 1000.0 };
    private static readonly double[] CoefficientSteps = { 0.001, 0.01, 0.05, 0.1, 0.2, 0.3 };

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public WholePropagationStepStudyTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [Theory]
    [InlineData("R1")]
    [InlineData("R2")]
    [InlineData("R3")]
    [InlineData("R4")]
    [InlineData("R5")]
    public void ChosenSteps_LieOnThePlateau(string name)
    {
        // Arrange
        var referenceCase = _cases[name];
        var solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(referenceCase.Parameters, IncludeInStepControl: referenceCase.SolarRadiationPressure));

        // Act and assert
        Study(referenceCase, solution, (start, cd, cr, tolerance) =>
            referenceCase.PropagateReference(NaifId - 1, start, cd, cr, tolerance));
    }

    [Fact]
    public void Eclipsing_ChosenSteps_LieOnThePlateau()
    {
        // Arrange: the reference fires the burn of the integrated trajectory in open loop, the model of Φ
        var eclipsing = _cases.Eclipsing;
        var (solution, maneuver) = _cases.PropagateEclipsing(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(eclipsing.Parameters, IncludeInStepControl: true));
        var openLoop = _cases.OpenLoop(solution.Segments[1].BaseEpoch, maneuver.DeltaV);

        // Act and assert
        Study(eclipsing, solution, (start, cd, cr, tolerance) =>
            _cases.PropagateEclipsingReference(NaifId - 1, start, openLoop, cd, cr, tolerance), assertPsiCr: false);
    }

    private void Study(ReferenceCase referenceCase, PropagationSolution solution,
        Func<StateVector, double, double, double, PropagationSolution> reference, bool assertPsiCr = true)
    {
        var y = StmMeasures.Y(solution, referenceCase.Window.EndDate);
        var rows = new List<(string Quantity, double Step, double Error)>();

        // Φ with the reference at its tolerance, and at the 1e-13 of the specification for the comparison
        foreach (var (quantity, tolerance) in new[] { ("Phi", ReferenceCases.ReferenceTolerance), ("Phi_1e-13", 1e-13) })
        {
            foreach (double step in PositionSteps)
            {
                var expected = WholePropagationDifferences.Phi(
                    start => reference(start, ReferenceCases.DragCoefficient, ReferenceCases.ReflectivityCoefficient,
                        tolerance),
                    referenceCase.InitialState, step, 1e-3 * step);
                rows.Add((quantity, step, StmMeasures.WorstBlockError(StmMeasures.Phi(y), expected)));
            }
        }

        int column = 6;
        foreach (var parameter in new[] { ForceParameters.DragCoefficient, ForceParameters.ReflectivityCoefficient })
        {
            if ((referenceCase.Parameters & parameter) == 0)
            {
                continue;
            }

            bool drag = parameter == ForceParameters.DragCoefficient;
            double coefficient = drag ? ReferenceCases.DragCoefficient : ReferenceCases.ReflectivityCoefficient;
            var actual = StmMeasures.Column(y, column++);
            foreach (double relativeStep in CoefficientSteps)
            {
                var expected = WholePropagationDifferences.Psi(
                    c => reference(referenceCase.InitialState, drag ? c : ReferenceCases.DragCoefficient,
                        drag ? ReferenceCases.ReflectivityCoefficient : c, ReferenceCases.ReferenceTolerance),
                    coefficient, relativeStep * coefficient);
                var (position, velocity) = WholePropagationDifferences.PsiError(actual, expected);
                rows.Add((drag ? "Psi_Cd" : "Psi_Cr", relativeStep, System.Math.Max(position, velocity)));
            }
        }

        foreach (var row in rows)
        {
            _output.WriteLine($"{referenceCase.Name} {row.Quantity} step {row.Step:G3}: {row.Error:E2}");
        }

        WriteCsvIfRequested(referenceCase.Name, rows);
        AssertOnThePlateau(rows, "Phi", referenceCase.PositionStep);
        double atReferenceTolerance = rows.Where(r => r.Quantity == "Phi").Min(r => r.Error);
        double atSpecificationTolerance = rows.Where(r => r.Quantity == "Phi_1e-13").Min(r => r.Error);
        Assert.True(atReferenceTolerance <= atSpecificationTolerance,
            $"the reference at 1e-14 resolves {atReferenceTolerance:E2}, at 1e-13 {atSpecificationTolerance:E2}");
        AssertOnThePlateau(rows, "Psi_Cd", referenceCase.DragCoefficientStep);
        if (assertPsiCr)
        {
            AssertOnThePlateau(rows, "Psi_Cr", referenceCase.ReflectivityCoefficientStep);
        }
    }

    private static void AssertOnThePlateau(List<(string Quantity, double Step, double Error)> rows, string quantity,
        double chosen)
    {
        var sweep = rows.Where(r => r.Quantity == quantity).ToArray();
        if (sweep.Length == 0)
        {
            return;
        }

        double smallest = sweep.Min(r => r.Error);
        double atChosen = sweep.Single(r => r.Step == chosen).Error;
        Assert.True(atChosen <= 2.0 * smallest,
            $"{quantity}: {atChosen:E2} at the chosen step {chosen:G3}, smallest of the sweep {smallest:E2}");
    }

    private static void WriteCsvIfRequested(string name, List<(string Quantity, double Step, double Error)> rows)
    {
        var directory = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var lines = new List<string> { "quantity,step,error" };
        lines.AddRange(rows.Select(r => string.Join(",", r.Quantity, r.Step.ToString("R", CultureInfo.InvariantCulture),
            r.Error.ToString("R", CultureInfo.InvariantCulture))));
        File.WriteAllLines(Path.Combine(directory, $"whole_propagation_step_study_{name}.csv"), lines);
    }
}
