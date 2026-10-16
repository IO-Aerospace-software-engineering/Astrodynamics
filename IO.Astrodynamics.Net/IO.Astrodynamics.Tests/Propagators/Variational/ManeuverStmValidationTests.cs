// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// F1 across an impulsive maneuver, on the eclipsing case of <see cref="ReferenceCases"/>, and the measured effect of
/// the open-loop assumption.
/// </summary>
/// <remarks>
/// <para>
/// Φ and Ψ treat the maneuver in open loop: its epoch and its ΔV are those of the nominal trajectory, so
/// <c>∂x⁺/∂x⁻ = I</c> at the burn (specification, modeling assumptions; the chaining of A5). Two references by central
/// differences of whole propagations, from the exact perturbed state (#398), at 1e-14:
/// </para>
/// <list type="bullet">
/// <item><description>Open loop: each perturbed propagation fires <see cref="FixedImpulse"/>, the nominal ΔV at the
/// nominal epoch, the model behind Φ. Φ and Ψ are held to F1 against it over the arc: 1e-4 outside the penumbra, the
/// penumbra published apart.</description></item>
/// <item><description>Closed loop: each perturbed propagation fires the apogee raise itself, at its own perigee and with
/// its own ΔV, as a real spacecraft would. The difference between Φ and this reference after the burn is the effect of
/// the open-loop assumption, published and asserted as a record.</description></item>
/// </list>
/// <para>
/// Ψ starts from zero (in the shadow for Cr, at 1000 km for Cd), where the reference does not resolve it relative to
/// its local value: Ψ is held to F1 over the arc relative to its largest value, the measure of #394, and relative to the
/// local value after the burn, once it is resolved. The steps come from <c>WholePropagationStepStudyTests</c>.
/// </para>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-16), burn at 0.823 h, |ΔV| = 233.8 m/s, 7 penumbra crossings. Open loop: Φ per
/// block 2.6e-8, 2.9e-8, 2.5e-8, 2.8e-8 outside the penumbra, 2.8e-8 at most in it; Ψ_Cd 6.4e-7 over the arc,
/// 3.7e-6 locally after the burn (8.1e-7 in the penumbra); Ψ_Cr 4.4e-6 and 1.3e-5 (4.1e-6). Closed loop: before the burn
/// the same (Φ below 6.8e-9); after it Φ is off by 0.48, 0.33, 0.42, 0.23 per block, Ψ_Cd by 0.37 and Ψ_Cr by 1.2 of
/// their largest values. The apogee raise retargets its ΔV from the state it finds at its own perigee, which removes
/// most of the deviation of the apogee: the covariance of a targeted maneuver is not the open-loop one. The open-loop
/// model is that of a burn executed as commanded (epoch and ΔV fixed), with its execution error (Gates).
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class ManeuverStmValidationTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1974;
    private static readonly TimeSpan Spacing = TimeSpan.FromMinutes(10.0);

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public ManeuverStmValidationTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [Fact]
    public void EclipsingCaseWithAManeuver_PhiAndPsiMatchTheOpenLoopReference_AndTheClosedLoopEffectIsMeasured()
    {
        // Arrange: Φ and Ψ_Cd with the default step control, Ψ_Cr with IncludeInStepControl (decision of 2026-10-15)
        var eclipsing = _cases.Eclipsing;
        var parameters = ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient;
        var (solution, maneuver) = _cases.PropagateEclipsing(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(parameters));
        var (controlled, _) = _cases.PropagateEclipsing(NaifId - 1, ReferenceCases.Tolerance,
            new VariationalOptions(parameters, IncludeInStepControl: true));
        Assert.Equal(2, solution.Segments.Count);
        var burnEpoch = solution.Segments[1].BaseEpoch;
        var deltaV = maneuver.DeltaV;
        var penumbra = eclipsing.PenumbraEpochs(solution);
        var epochs = eclipsing.ArcEpochs(Spacing).Where(t => t != burnEpoch).Concat(penumbra).OrderBy(t => t).ToArray();
        bool[] inPenumbra = epochs.Select(t => penumbra.Contains(t)).ToArray();
        bool[] afterBurn = epochs.Select(t => t > burnEpoch).ToArray();

        // Act: the two references
        var openLoop = _cases.OpenLoop(burnEpoch, deltaV);
        var openPhi = WholePropagationDifferences.Phi(start => Reference(start, openLoop),
            eclipsing.InitialState, eclipsing.PositionStep, eclipsing.VelocityStep, epochs);
        var closedPhi = WholePropagationDifferences.Phi(start => Reference(start, null),
            eclipsing.InitialState, eclipsing.PositionStep, eclipsing.VelocityStep, epochs);
        var phi = epochs.Select(t => StmMeasures.Phi(StmMeasures.Y(solution, t))).ToArray();
        var openErrors = epochs.Select((_, k) => StmMeasures.BlockErrors(phi[k], openPhi[k])).ToArray();
        var closedErrors = epochs.Select((_, k) => StmMeasures.BlockErrors(phi[k], closedPhi[k])).ToArray();

        // Assert: open loop, F1 outside the penumbra
        _output.WriteLine($"burn at {(burnEpoch - eclipsing.InitialState.Epoch).TotalHours:F3} h, |ΔV| = " +
                          $"{deltaV.Magnitude():F3} m/s; {epochs.Length} epochs, {penumbra.Count} in the penumbra");
        string[] blocks = { "Φ_rr", "Φ_rv", "Φ_vr", "Φ_vv" };
        for (int b = 0; b < 4; b++)
        {
            double outside = StmValidationTests.Worst(openErrors, inPenumbra, false, e => e[b]);
            double inside = StmValidationTests.Worst(openErrors, inPenumbra, true, e => e[b]);
            _output.WriteLine($"open loop {blocks[b]}: worst outside the penumbra {outside:E2}, in the penumbra {inside:E2}");
            Assert.True(outside < 1e-4, $"open loop {blocks[b]}: {outside:E2}");
            Assert.True(inside < PenumbraRecord, $"open loop {blocks[b]} in the penumbra: {inside:E2}");
        }

        // Ψ_Cd and Ψ_Cr against the open loop, over the arc relative to their largest value: they start from zero (in the
        // shadow for Cr, at 1000 km for Cd), where the reference does not resolve them relative to the local value. The
        // local measure after the burn is published with it
        var psiSolutions = new[] { solution, controlled };
        var closedLoop = new List<double>();
        var afterTheBurn = epochs.Select(t => t > burnEpoch).ToArray();
        for (int p = 0; p < 2; p++)
        {
            bool drag = p == 0;
            double coefficient = drag ? ReferenceCases.DragCoefficient : ReferenceCases.ReflectivityCoefficient;
            double step = coefficient * (drag ? eclipsing.DragCoefficientStep : eclipsing.ReflectivityCoefficientStep);
            var expected = WholePropagationDifferences.Psi(
                c => Reference(eclipsing.InitialState, openLoop, drag ? c : ReferenceCases.DragCoefficient,
                    drag ? ReferenceCases.ReflectivityCoefficient : c), coefficient, step, epochs);
            var actual = epochs.Select(t => StmMeasures.Column(StmMeasures.Y(psiSolutions[p], t), 6 + p)).ToArray();
            var (position, velocity) = WholePropagationDifferences.PsiArcError(actual, expected);
            var local = StmValidationTests.PsiErrors(psiSolutions[p], 6 + p, epochs, expected);
            double localOutside = 0.0, localInside = 0.0;
            for (int k = 0; k < epochs.Length; k++)
            {
                if (!afterTheBurn[k])
                {
                    continue;
                }

                double e = System.Math.Max(local[k][0], local[k][1]);
                if (inPenumbra[k])
                {
                    localInside = System.Math.Max(localInside, e);
                }
                else
                {
                    localOutside = System.Math.Max(localOutside, e);
                }
            }

            string what = drag ? "Ψ_Cd" : "Ψ_Cr";
            _output.WriteLine($"open loop {what}: worst over the arc relative to its largest value, position " +
                              $"{position:E2}, velocity {velocity:E2}; relative to the local value after the burn, " +
                              $"outside the penumbra {localOutside:E2}, in the penumbra {localInside:E2}");
            Assert.True(position < 1e-4, $"open loop {what} position: {position:E2}");
            Assert.True(velocity < 1e-4, $"open loop {what} velocity: {velocity:E2}");
            Assert.True(localOutside < 1e-4, $"open loop {what} after the burn: {localOutside:E2}");
            Assert.True(localInside < PsiPenumbraRecord, $"open loop {what} after the burn, in the penumbra: {localInside:E2}");

            // The closed loop for Ψ: the coefficient also moves the perigee and so the burn
            var closed = WholePropagationDifferences.Psi(
                c => Reference(eclipsing.InitialState, null, drag ? c : ReferenceCases.DragCoefficient,
                    drag ? ReferenceCases.ReflectivityCoefficient : c), coefficient, step, epochs);
            var closedArc = WholePropagationDifferences.PsiArcError(actual, closed);
            _output.WriteLine($"closed loop {what}: worst over the arc relative to its largest value, position " +
                              $"{closedArc.Position:E2}, velocity {closedArc.Velocity:E2}");
            closedLoop.Add(System.Math.Max(closedArc.Position, closedArc.Velocity));
        }

        // Closed loop: before the burn the same as the open loop; after it, the effect of the open-loop assumption
        for (int b = 0; b < 4; b++)
        {
            double before = 0.0, after = 0.0;
            for (int k = 0; k < epochs.Length; k++)
            {
                if (afterBurn[k])
                {
                    after = System.Math.Max(after, closedErrors[k][b]);
                }
                else
                {
                    before = System.Math.Max(before, closedErrors[k][b]);
                }
            }

            _output.WriteLine($"closed loop {blocks[b]}: worst before the burn {before:E2}, after it {after:E2}, " +
                              $"at the end {closedErrors[^1][b]:E2}");
            Assert.True(before < 1e-4, $"closed loop {blocks[b]} before the burn: {before:E2}");
            closedLoop.Insert(b, after);
        }

        // The effect of the open-loop assumption, recorded within a factor of 2 of the measurement
        for (int i = 0; i < closedLoop.Count; i++)
        {
            Assert.InRange(closedLoop[i], ClosedLoopRecords[i] / 2.0, 2.0 * ClosedLoopRecords[i]);
        }
    }

    // Records at about twice the measurement (Linux, .NET 10, 2026-10-16)
    private const double PenumbraRecord = 6e-8;

    private const double PsiPenumbraRecord = 1e-5;

    // The measurements: Φ_rr, Φ_rv, Φ_vr, Φ_vv after the burn, then Ψ_Cd and Ψ_Cr over the arc
    private static readonly double[] ClosedLoopRecords = { 0.477, 0.332, 0.419, 0.230, 0.368, 1.17 };

    private PropagationSolution Reference(StateVector start, Func<Engine, ImpulseManeuver> maneuver,
        double dragCoefficient = ReferenceCases.DragCoefficient,
        double reflectivityCoefficient = ReferenceCases.ReflectivityCoefficient)
    {
        var solution = _cases.PropagateEclipsingReference(NaifId - 2, start, maneuver, dragCoefficient,
            reflectivityCoefficient);
        Assert.Equal(2, solution.Segments.Count);
        return solution;
    }
}
