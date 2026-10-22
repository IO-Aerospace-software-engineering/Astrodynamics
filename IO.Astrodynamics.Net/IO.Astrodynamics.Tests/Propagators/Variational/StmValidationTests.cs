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
/// partials differentiate the shadow function in the penumbra: by central differences in and near it from #379, by its
/// analytic derivative since #423, which moved Φ of R3 in the third digit (3.31e-7 to 5.18e-7 per block over the arc
/// before, 3.34e-7 to 5.20e-7 after; Linux, .NET 10, 2026-10-22) and Ψ_Cr not to three digits.
/// </para>
/// <para>
/// Ψ is also held over the arc relative to its largest value, the measure of #394, which does not depend on what the
/// reference resolves where Ψ is small: R2 Ψ_Cd 1.2e-7 (position and velocity), R3 Ψ_Cr 1.5e-6 and 2.1e-6, R4 Ψ_Cr
/// 2.0e-9 and 1.9e-9, R5 Ψ_Cd 4.6e-8 and 5.3e-8.
/// </para>
/// <para>
/// The local measure of Ψ_Cr of R3 in velocity is a record (1.8e-4), not held to the threshold: its 9.2e-5, at 9.5 h,
/// measures the reference. The velocity part of Ψ_Cr passes there through a local minimum (7.9e-4 m/s per unit Cr),
/// and the figure follows the step of the reference: 3.3e-4 at 5 % of Cr, 9.2e-5 at 10 %, 9.9e-5 at 20 %, while over
/// the arc the same comparison gives 9.5e-6, 2.1e-6 and 9.4e-6 (asserted at each step). A fixed-step reference resolved
/// worse, 2.9e-4 at 5 s, through the shadow crossings (a design measurement, not a committed test). Ψ_Cr itself is
/// within 7.1e-8 of the same propagation at a tighter tolerance over the eclipsing arc (#396; 9.6e-8 before #420).
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

    // Threshold outside the penumbra, the record of the penumbra (0 when the case has no SRP), and the record of the local
    // measure of Ψ_Cr in velocity where the reference does not resolve it (0 when it is held to the threshold)
    public static TheoryData<string, double, double, double> Cases => new()
    {
        { "R1", 1e-7, 0.0, 0.0 },
        { "R2", 1e-4, 0.0, 0.0 },
        { "R3", 1e-4, 1.5e-5, 1.8e-4 },
        { "R4", 1e-4, 2.5e-8, 0.0 },
        { "R5", 1e-4, 0.0, 0.0 }
    };

    // Steps of the reference of Ψ_Cr, relative to Cr, around the chosen one, for the cases whose local Ψ_Cr is a record
    private static readonly double[] OtherReflectivitySteps = { 0.05, 0.2 };

    [Theory]
    [MemberData(nameof(Cases))]
    public void PhiAndPsi_MatchCentralDifferencesOfWholePropagations_OverTheArc(string name, double threshold,
        double penumbraRecord, double localPsiCrRecord)
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
        var worst = new List<(string What, double Outside, double Penumbra, double End, double Bound)>();
        for (int b = 0; b < 4; b++)
        {
            worst.Add((blocks[b], Worst(phiErrors, inPenumbra, false, e => e[b]),
                Worst(phiErrors, inPenumbra, true, e => e[b]), phiErrors[^1][b], threshold));
        }

        // Ψ, per column, position and velocity rows apart: over the arc relative to its largest value (the measure of
        // #394), held to the threshold in every case, and relative to the local value, held to the threshold except where
        // the reference does not resolve it (R3, Ψ_Cr in velocity: a record)
        int column = 6;
        var arcErrors = new List<(string What, double Position, double Velocity)>();
        foreach (var parameter in new[] { ForceParameters.DragCoefficient, ForceParameters.ReflectivityCoefficient })
        {
            if ((referenceCase.Parameters & parameter) == 0)
            {
                continue;
            }

            bool drag = parameter == ForceParameters.DragCoefficient;
            double coefficient = drag ? ReferenceCases.DragCoefficient : ReferenceCases.ReflectivityCoefficient;
            double relativeStep = drag ? referenceCase.DragCoefficientStep : referenceCase.ReflectivityCoefficientStep;
            string label = drag ? "Ψ_Cd" : "Ψ_Cr";
            int c = column++;
            var psiSolution = drag ? solution : controlled;
            var actual = epochs.Select(t => StmMeasures.Column(StmMeasures.Y(psiSolution, t), c)).ToArray();
            double[][] ExpectedPsi(double step) => WholePropagationDifferences.Psi(
                v => Reference(referenceCase, referenceCase.InitialState, drag ? v : ReferenceCases.DragCoefficient,
                    drag ? ReferenceCases.ReflectivityCoefficient : v), coefficient, coefficient * step, epochs);
            var expectedPsi = ExpectedPsi(relativeStep);
            var psiErrors = PsiErrors(psiSolution, c, epochs, expectedPsi);
            double velocityBound = !drag && localPsiCrRecord > 0.0 ? localPsiCrRecord : threshold;
            worst.Add((label + " position", Worst(psiErrors, inPenumbra, false, e => e[0]),
                Worst(psiErrors, inPenumbra, true, e => e[0]), psiErrors[^1][0], threshold));
            worst.Add((label + " velocity", Worst(psiErrors, inPenumbra, false, e => e[1]),
                Worst(psiErrors, inPenumbra, true, e => e[1]), psiErrors[^1][1], velocityBound));
            var arc = WholePropagationDifferences.PsiArcError(actual, expectedPsi);
            arcErrors.Add((label, arc.Position, arc.Velocity));
            if (!drag)
            {
                // Without the option, published only: the bounds of the eclipsing case record it (#394, #396)
                var free = PsiErrors(solution, c, epochs, expectedPsi);
                _output.WriteLine($"{name} Ψ_Cr without IncludeInStepControl, outside the penumbra: position " +
                                  $"{Worst(free, inPenumbra, false, e => e[0]):E2}, velocity " +
                                  $"{Worst(free, inPenumbra, false, e => e[1]):E2}");
            }

            if (!drag && localPsiCrRecord > 0.0)
            {
                // Where the local measure reaches its worst: the velocity part of Ψ_Cr passes through a local minimum
                int k = Enumerable.Range(0, epochs.Length).Where(i => !inPenumbra[i]).MaxBy(i => psiErrors[i][1]);
                double speed = System.Math.Sqrt(expectedPsi[k][3] * expectedPsi[k][3] +
                                                expectedPsi[k][4] * expectedPsi[k][4] +
                                                expectedPsi[k][5] * expectedPsi[k][5]);
                _output.WriteLine($"{name} Ψ_Cr velocity, worst local error at " +
                                  $"{(epochs[k] - referenceCase.InitialState.Epoch).TotalHours:F2} h, where |Ψ_Cr| in " +
                                  $"velocity is {speed:E2} m/s per unit Cr");

                // The local measure follows the step of the reference; the measure over the arc does not
                foreach (double other in OtherReflectivitySteps)
                {
                    var otherExpected = ExpectedPsi(other);
                    var otherLocal = PsiErrors(psiSolution, c, epochs, otherExpected);
                    var otherArc = WholePropagationDifferences.PsiArcError(actual, otherExpected);
                    _output.WriteLine($"{name} Ψ_Cr with a reference step of {100.0 * other:F0} % of Cr: local, " +
                                      $"outside the penumbra, position {Worst(otherLocal, inPenumbra, false, e => e[0]):E2}, " +
                                      $"velocity {Worst(otherLocal, inPenumbra, false, e => e[1]):E2}; over the arc " +
                                      $"relative to its largest value, position {otherArc.Position:E2}, velocity " +
                                      $"{otherArc.Velocity:E2}");
                    Assert.True(otherArc.Position < threshold && otherArc.Velocity < threshold,
                        $"{name} Ψ_Cr over the arc at a step of {other:P0}: {otherArc.Position:E2}, {otherArc.Velocity:E2}");
                }
            }
        }

        _output.WriteLine($"{name}: {epochs.Length} epochs, {penumbra.Count} in the penumbra");
        foreach (var (what, outside, inside, end, bound) in worst)
        {
            _output.WriteLine($"{name} {what}: worst over the arc outside the penumbra {outside:E2}, in the penumbra " +
                              $"{(penumbra.Count > 0 ? inside.ToString("E2") : "-")}, at the end {end:E2}" +
                              (bound != threshold ? $" (local measure, a record: {bound:E1})" : ""));
            Assert.True(outside < bound, $"{name} {what}: {outside:E2} outside the penumbra");
            if (penumbra.Count > 0 && penumbraRecord > 0.0)
            {
                Assert.True(inside < penumbraRecord, $"{name} {what}: {inside:E2} in the penumbra");
            }
        }

        foreach (var (what, position, velocity) in arcErrors)
        {
            _output.WriteLine($"{name} {what} over the arc relative to its largest value: position {position:E2}, " +
                              $"velocity {velocity:E2}");
            Assert.True(position < threshold, $"{name} {what} over the arc, position: {position:E2}");
            Assert.True(velocity < threshold, $"{name} {what} over the arc, velocity: {velocity:E2}");
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
