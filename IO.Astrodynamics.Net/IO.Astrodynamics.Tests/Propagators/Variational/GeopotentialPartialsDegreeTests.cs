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
/// 0.25 s at 70, against 0.17 s (1.0× to 1.5×). The accuracies are asserted as records within a factor of 2. The
/// geopotential partials of a LEO keep a contribution of every degree (#369), so in R2 a reduced degree leaves Φ above
/// the 1e-4 of F1 after one day and saves nothing; at GEO the high degrees vanish, so from degree 8 Φ is that of the
/// full degree, a third cheaper.
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

    // Case, requested degree, record of the worst block over the arc
    public static TheoryData<string, int[], double[]> Cases => new()
    {
        { "R2", new[] { 2, 8, 20, 10 }, new[] { 1.98e-3, 8.49e-4, 4.56e-8, 4.56e-8 } },
        { "R4", new[] { 2, 8, 20, 70 }, new[] { 1.16e-6, 2.76e-8, 2.76e-8, 2.76e-8 } }
    };

    [LongValidationTheory]
    [MemberData(nameof(Cases))]
    public void ReducedDegree_AccuracyAndCost(string name, int[] degrees, double[] records)
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
            Assert.InRange(worst, records[d] / 2.0, 2.0 * records[d]);
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
