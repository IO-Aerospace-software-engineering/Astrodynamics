// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.Linq;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// F1 for the reduced degree of the geopotential partials (option <c>GeopotentialPartialsDegree</c> of B3): Φ of R2 and
/// R4 against central differences of whole propagations, with the partials at degree 2, 8, 20 and the full degree, and
/// the cost of the propagation, for the B3 decision.
/// </summary>
/// <remarks>
/// <para>
/// The reference does not depend on the partials: one set of 12 propagations per case, as in <c>StmValidationTests</c>.
/// The error is the worst 3×3 block of Φ over the arc (every 10 min and at the end). R2 has a 10×10 field: the option
/// takes the smaller of the requested degree and the degree of the field, so 20 is the full degree there. The cost is
/// the median of three propagations with Φ at 1e-13, against the median of three without, Release; informative (the
/// benchmarks come with G1). The figures below were measured with this test alone: run with other test classes
/// in parallel, the same propagations come out up to twice as slow and the ratios move with the load.
/// </para>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-16, this test alone), worst block over the arc, and the cost with Φ against without: R2 (10×10)
/// 2.0e-3 at degree 2, 8.5e-4 at 8, 4.6e-8 at 10 (full), about 1 s against 0.17 s at every degree (5.5× to 6.0×: the drag
/// partials dominate); R4 (70×70) 1.2e-6 at degree 2, 2.8e-8 at 8, 20 and 70, 0.17 to 0.19 s at degree 2 to 20 and
/// 0.25 s at 70, against 0.17 s (1.0× to 1.5×). The accuracies are asserted as records within a factor of 2, except,
/// since 2026-11-11, at the degrees whose partials are those of the full degree, held below an upper record (below). The
/// geopotential partials of a LEO keep a contribution of every degree (#369), so in R2 a reduced degree leaves Φ above
/// the 1e-4 of F1 after one day and saves nothing; at GEO the high degrees vanish, so from degree 8 Φ is that of the
/// full degree, a third cheaper.
/// </para>
/// <para>
/// With the constants of the geopotential of #410 (run 39977618859 of <c>long-validation.yml</c>, 2026-10-23), R4 at degree
/// 8 is 6.09e-8 (2.76e-8 before), the F1 noise of R4, which moves the same way on the Φ of the full degree
/// (<c>StmValidationTests</c>, 9.3e-9 to 6.1e-8); from degree 8 the partials are those of the full degree, so the
/// records of degrees 8, 20 and 70 are set at 6.1e-8 (decision of S. Guillet, 2026-10-23). Degree 2 1.18e-6; R2 at
/// degrees 20 and 10 2.38e-8, within its record.
/// </para>
/// <para>
/// With the TT time argument of #501 (run 41268727370 of <c>long-validation.yml</c>, 2026-11-01, and the same figures
/// measured in a session the same day), R4 at degrees 8, 20 and 70 is 9.0e-9, the F1 noise of R4 that moves the same way
/// on the Φ of the full degree (<c>StmValidationTests</c>, Φ_rr 6.1e-8 to 9.0e-9): the records of degrees 8, 20 and 70
/// are set at 9.0e-9, as on 2026-10-23. Degree 2 1.17e-6; R2 1.98e-3, 8.50e-4 and 2.98e-8 at degrees 2, 8 and 20 and at
/// the full degree, within its records.
/// </para>
/// <para>
/// With the nearest tick of #625 (run 42945251492 of <c>long-validation.yml</c>, 2026-11-11, and the same figures measured
/// in a session the same day), R4 at degrees 8, 20 and 70 is 1.83e-8 (9.0e-9 before) and R2 at degrees 20 and 10 1.61e-8
/// (2.98e-8 before). Both are the F1 noise of the reference, which moves the same way on the Φ of the full degree
/// (<c>StmValidationTests</c>, R4 9.0e-9 to 1.8e-8, R2 3.0e-8 to 1.6e-8). The conversion that moves them is the nearest
/// tick of <c>AddSeconds</c>, through which <c>TerrestrialTime.EpochAt</c> labels every stage of the integration, and for
/// R2 the rounded TDB − TT, through which the drag reads its UTC: about half of the TDB labels at which the forces read
/// the bodies, the orientation and the UTC move by one tick. The tested Φ moves by less than 3.5e-9 (R4) and 6.9e-11
/// (R2), the reference by the whole figure. Over eleven starts moved by −5 to +5 ticks, R4 at degrees 8, 20 and 70 spans
/// 1.0e-8 to 3.3e-8 (3.31e-8 one tick later, 3.30e-8 at 20 and 70) and R2 at degrees 20 and 10 1.6e-8 to 1.24e-7 (three
/// ticks later; 4.15e-8 at worst in the ten other starts); under the truncation before #625 the starts moved by one tick
/// already gave R4 2.1e-8 and 5.3e-8, outside the band of the record 9.0e-9, which a single draw had set. Degree 2 of R4
/// 1.16e-6 (1.14e-6 to 1.21e-6 over the starts); R2 1.98e-3 and 8.50e-4 at degrees 2 and 8, unchanged over every start:
/// the truncation of the partials, which a tick does not move. The degrees whose partials are those of the full degree
/// (R4 at 8, 20 and 70, R2 at 20 and 10) are therefore held below an upper record, twice the worst of the eleven starts
/// rounded up at the second significant digit: 6.7e-8 for R4 (twice 3.31e-8) and 2.5e-7 for R2 (twice 1.24e-7)
/// (decision 53 of #457, S. Guillet, 2026-11-11). What this gives up: the lower bound, which flagged a stale record, and
/// the upper bounds move from 1.8e-8 to 6.7e-8 (R4) and from 9.1e-8 to 2.5e-7 (R2), so a departure of the degree-8 Φ of
/// R4 from that of the full degree by a few 1e-8 would pass. The truncation figures keep their band of a factor of 2. The
/// starts were measured on Linux only, as <c>long-validation.yml</c> runs on <c>ubuntu-latest</c> only.
/// </para>
/// <para>
/// In the <c>LongValidation</c> category since 2026-10-22 (decision of S. Guillet): a measurement for B3, decided on
/// 2026-10-16, about 2 min.
/// </para>
/// </remarks>
[Trait("Category", "LongValidation")]
public class GeopotentialPartialsDegreeTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1976;
    private static readonly TimeSpan Spacing = TimeSpan.FromMinutes(10.0);

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public GeopotentialPartialsDegreeTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    // Case, requested degree, record of the worst block over the arc, and whether that record is an upper bound only:
    // the partials of the full degree, whose figure is the F1 noise of the reference (decision 53 of #457, 2026-11-11),
    // where the others are held within a factor of 2 of their record
    public static TheoryData<string, int[], double[], bool[]> Cases => new()
    {
        { "R2", new[] { 2, 8, 20, 10 }, new[] { 1.98e-3, 8.49e-4, 2.5e-7, 2.5e-7 }, new[] { false, false, true, true } },
        { "R4", new[] { 2, 8, 20, 70 }, new[] { 1.16e-6, 6.7e-8, 6.7e-8, 6.7e-8 }, new[] { false, true, true, true } }
    };

    [LongValidationTheory]
    [MemberData(nameof(Cases))]
    public void ReducedDegree_AccuracyAndCost(string name, int[] degrees, double[] records, bool[] upperOnly)
    {
        // Arrange
        var referenceCase = _cases[name];
        var epochs = referenceCase.ArcEpochs(Spacing);
        var expected = WholePropagationDifferences.Phi(
            start => referenceCase.PropagateReference(NaifId - 1, start, ReferenceCases.DragCoefficient,
                ReferenceCases.ReflectivityCoefficient, ReferenceCases.ReferenceTolerance),
            referenceCase.InitialState, referenceCase.PositionStep, referenceCase.VelocityStep, epochs);
        double plain = Median(() => referenceCase.Propagate(NaifId, ReferenceCases.Tolerance, null));

        // Act
        for (int d = 0; d < degrees.Length; d++)
        {
            var options = new VariationalOptions(referenceCase.Parameters, GeopotentialPartialsDegree: degrees[d]);
            PropagationSolution solution = null;
            double seconds = Median(() => solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance, options));
            double worst = epochs.Select((t, k) =>
                StmMeasures.WorstBlockError(StmMeasures.Phi(StmMeasures.Y(solution, t)), expected[k])).Max();

            // Assert
            _output.WriteLine($"{name}, partials at degree {degrees[d]}: worst block over the arc {worst:E2}; " +
                              $"{seconds:F2} s with Φ, {plain:F2} s without ({seconds / plain:F1}×)");
            if (upperOnly[d])
            {
                Assert.True(worst < records[d], $"{name}, degree {degrees[d]}: {worst:E2} against the record {records[d]:E2}");
            }
            else
            {
                Assert.InRange(worst, records[d] / 2.0, 2.0 * records[d]);
            }
        }
    }

    private static double Median(Func<PropagationSolution> propagate)
    {
        var times = new double[3];
        for (int i = 0; i < 3; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            propagate();
            times[i] = stopwatch.Elapsed.TotalSeconds;
        }

        Array.Sort(times);
        return times[1];
    }
}
