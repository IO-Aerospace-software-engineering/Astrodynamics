// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// F1: Φ and Ψ of the reference cases R1 to R5 against central differences of whole propagations, per 3×3 block of Φ
/// and per Ψ column, over the arc.
/// </summary>
/// <remarks>
/// <para>
/// Φ and Ψ come from RK7(8) at <see cref="ReferenceCases.Tolerance"/> (1e-13), the step controlled on the state only
/// (A2), except Ψ_Cr, which F1 holds on <c>IncludeInStepControl</c> (decision of S. Guillet, 2026-10-15). The reference
/// propagations run at <see cref="ReferenceCases.ReferenceTolerance"/> (1e-14), from the exact perturbed state (#398),
/// with the steps of <c>WholePropagationStepStudyTests</c>. The errors are relative Frobenius norms against the local
/// reference: each 3×3 block of Φ, the position and velocity rows of each Ψ column, every 10 min and at the end.
/// </para>
/// <para>
/// Thresholds of the specification: R1 below 1e-7, R2 to R5 below 1e-4 outside the penumbra. The penumbra epochs of the
/// cases with SRP (R3, R4) are the middles of the Earth's penumbra crossings of the nominal trajectory, found by
/// bisection on the shadow fraction; their errors are measured and published apart, and asserted as records at about
/// twice the measurement. The epochs after a crossing are not excluded: what the penumbra leaves in Φ and Ψ is held to
/// the threshold.
/// </para>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-16), worst over the arc outside the penumbra, Φ per block (rr, rv, vr, vv):
/// R1 1.3e-8, 1.6e-8, 1.4e-8, 1.7e-8; R2 4.6e-8, 3.6e-8, 4.6e-8, 3.6e-8, Ψ_Cd 1.6e-7 (position), 1.3e-7 (velocity);
/// R3 7.3e-7, 9.1e-7, 7.3e-7, 9.1e-7, Ψ_Cr 9.1e-6, 9.2e-5; R4 1.8e-8, 2.7e-8, 2.8e-8, 2.2e-8, Ψ_Cr 4.9e-7, 3.1e-8;
/// R5 6.7e-7, 3.3e-7, 6.7e-7, 3.3e-7, Ψ_Cd 5.6e-8, 6.4e-8. In the penumbra (R3: 30 crossings; R4: 2), no larger than
/// outside: R3 Φ 7.1e-7 to 9.0e-7, Ψ_Cr 5.8e-6 and 7.2e-6; R4 Φ 3.4e-9 to 1.3e-8, Ψ_Cr 9.8e-9 and 7.0e-9. The SRP
/// partials take central differences in and near the penumbra (#379), so the shadow function is differentiated.
/// </para>
/// <para>
/// The 9.2e-5 of Ψ_Cr of R3 in velocity, at 9.5 h, measures the reference: the velocity part of Ψ_Cr passes there
/// through a local minimum (7.9e-4 m/s per unit Cr), and the figure follows the step of the reference (3.3e-4 at 5 % of
/// Cr, 9.9e-5 at 20 %; a fixed-step reference resolves worse, 2.9e-4 at 5 s, through the shadow crossings). Ψ_Cr itself
/// is within 9.6e-8 of the same propagation at a tighter tolerance over the eclipsing arc (#396).
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class StmValidationTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1972;
    private static readonly TimeSpan Spacing = TimeSpan.FromMinutes(10.0);

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public StmValidationTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    // Threshold outside the penumbra, and the record of the penumbra (0 when the case has no SRP)
    public static TheoryData<string, double, double> Cases => new()
    {
        { "R1", 1e-7, 0.0 },
        { "R2", 1e-4, 0.0 },
        { "R3", 1e-4, 1.5e-5 },
        { "R4", 1e-4, 2.5e-8 },
        { "R5", 1e-4, 0.0 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void PhiAndPsi_MatchCentralDifferencesOfWholePropagations_OverTheArc(string name, double threshold,
        double penumbraRecord)
    {
        // Arrange
        var referenceCase = _cases[name];
        var solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(referenceCase.Parameters));
        var controlled = referenceCase.SolarRadiationPressure
            ? referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
                new VariationalOptions(referenceCase.Parameters, IncludeInStepControl: true))
            : null;
        var penumbra = referenceCase.PenumbraEpochs(solution);
        var epochs = referenceCase.ArcEpochs(Spacing).Concat(penumbra).OrderBy(t => t).ToArray();
        bool[] inPenumbra = epochs.Select(t => penumbra.Contains(t)).ToArray();

        // Act
        var expectedPhi = WholePropagationDifferences.Phi(
            start => Reference(referenceCase, start, ReferenceCases.DragCoefficient,
                ReferenceCases.ReflectivityCoefficient),
            referenceCase.InitialState, referenceCase.PositionStep, referenceCase.VelocityStep, epochs);
        var phiErrors = epochs.Select((t, k) => StmMeasures.BlockErrors(StmMeasures.Phi(StmMeasures.Y(solution, t)), expectedPhi[k])).ToArray();

        // Assert: Φ, per block
        string[] blocks = { "Φ_rr", "Φ_rv", "Φ_vr", "Φ_vv" };
        var worst = new List<(string What, double Outside, double Penumbra, double End)>();
        for (int b = 0; b < 4; b++)
        {
            worst.Add((blocks[b], Worst(phiErrors, inPenumbra, false, e => e[b]),
                Worst(phiErrors, inPenumbra, true, e => e[b]), phiErrors[^1][b]));
        }

        // Ψ, per column, position and velocity rows apart
        int column = 6;
        foreach (var parameter in new[] { ForceParameters.DragCoefficient, ForceParameters.ReflectivityCoefficient })
        {
            if ((referenceCase.Parameters & parameter) == 0)
            {
                continue;
            }

            bool drag = parameter == ForceParameters.DragCoefficient;
            double coefficient = drag ? ReferenceCases.DragCoefficient : ReferenceCases.ReflectivityCoefficient;
            double step = coefficient * (drag ? referenceCase.DragCoefficientStep : referenceCase.ReflectivityCoefficientStep);
            var expectedPsi = WholePropagationDifferences.Psi(
                c => Reference(referenceCase, referenceCase.InitialState, drag ? c : ReferenceCases.DragCoefficient,
                    drag ? ReferenceCases.ReflectivityCoefficient : c), coefficient, step, epochs);
            string label = drag ? "Ψ_Cd" : "Ψ_Cr";
            int c = column++;
            var psiErrors = PsiErrors(drag ? solution : controlled, c, epochs, expectedPsi);
            worst.Add((label + " position", Worst(psiErrors, inPenumbra, false, e => e[0]),
                Worst(psiErrors, inPenumbra, true, e => e[0]), psiErrors[^1][0]));
            worst.Add((label + " velocity", Worst(psiErrors, inPenumbra, false, e => e[1]),
                Worst(psiErrors, inPenumbra, true, e => e[1]), psiErrors[^1][1]));
            if (!drag)
            {
                // Without the option, published only: the bounds of the eclipsing case record it (#394, #396)
                var free = PsiErrors(solution, c, epochs, expectedPsi);
                _output.WriteLine($"{name} Ψ_Cr without IncludeInStepControl, outside the penumbra: position " +
                                  $"{Worst(free, inPenumbra, false, e => e[0]):E2}, velocity " +
                                  $"{Worst(free, inPenumbra, false, e => e[1]):E2}");
            }
        }

        _output.WriteLine($"{name}: {epochs.Length} epochs, {penumbra.Count} in the penumbra");
        foreach (var (what, outside, inside, end) in worst)
        {
            _output.WriteLine($"{name} {what}: worst over the arc outside the penumbra {outside:E2}, in the penumbra " +
                              $"{(penumbra.Count > 0 ? inside.ToString("E2") : "-")}, at the end {end:E2}");
            Assert.True(outside < threshold, $"{name} {what}: {outside:E2} outside the penumbra");
            if (penumbra.Count > 0 && penumbraRecord > 0.0)
            {
                Assert.True(inside < penumbraRecord, $"{name} {what}: {inside:E2} in the penumbra");
            }
        }
    }

    private static PropagationSolution Reference(ReferenceCase referenceCase, StateVector start, double cd, double cr)
    {
        return referenceCase.PropagateReference(NaifId - 1, start, cd, cr, ReferenceCases.ReferenceTolerance);
    }

    internal static double[][] PsiErrors(PropagationSolution solution, int column, TimeSystem.Time[] epochs,
        double[][] expected)
    {
        var errors = new double[epochs.Length][];
        for (int k = 0; k < epochs.Length; k++)
        {
            var actual = StmMeasures.Column(StmMeasures.Y(solution, epochs[k]), column);
            var (position, velocity) = WholePropagationDifferences.PsiError(actual, expected[k]);
            errors[k] = new[] { position, velocity };
        }

        return errors;
    }

    internal static double Worst(double[][] errors, bool[] inPenumbra, bool penumbra, Func<double[], double> select)
    {
        double worst = 0.0;
        for (int k = 0; k < errors.Length; k++)
        {
            if (inPenumbra[k] == penumbra)
            {
                worst = System.Math.Max(worst, select(errors[k]));
            }
        }

        return worst;
    }
}
