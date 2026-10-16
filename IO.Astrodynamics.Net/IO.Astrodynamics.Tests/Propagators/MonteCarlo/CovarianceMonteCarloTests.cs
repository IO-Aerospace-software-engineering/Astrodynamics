// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

/// <summary>
/// F3 on R1: the covariance propagated by the state transition matrix against a Monte Carlo of 5000 runs
/// (<see cref="CovarianceMonteCarlo"/>), every 10 min up to 6 h, at one revolution and at 24 h, and the public product of
/// the campaign against its runs.
/// </summary>
/// <remarks>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-16; seed 20261016, RK7(8) at 1e-11). The element-wise test holds at every epoch up
/// to 24 h: worst |z| 1.38 at 10 min, below 0.8 from 40 min, 0.29 at 24 h. In inertial axes, though, the along-track
/// variance (38 km RMS at 24 h) sets the scale of every element, so this test cannot see the thin directions of P. The
/// ratios of S to P along every direction of the state (<see cref="CovarianceMonteCarlo.EpochResult"/>), which do not
/// depend on the axes, stay within the sampling band, 0.93 to 1.07, up to 1 h (0.949 to 1.050), and leave it from
/// 1.17 h: 1.16 at one revolution (1.646 h), 1.26 at 2 h, 350 at 6 h, 1.26e6 at 24 h, along one direction of the
/// state; the five others stay between 0.96 and 1.03. The Kolmogorov-Smirnov test of the squared Mahalanobis distances
/// holds up to 1.83 h (p = 0.011; 0.058 at one revolution) and fails from 2.0 h (p = 1.9e-4), 1.2 revolutions: the
/// domain of validity of the linear propagation for this P0 (σ = 100 m and 0.1 m/s in RTN) in LEO 700 km. Beyond about
/// one revolution P is the linear covariance, not the covariance of the deviations.
/// </para>
/// <para>
/// The position alone, what a collision probability projects on the encounter plane, holds longer: its ratios stay
/// within their sampling band (0.95 to 1.05 for three components) up to 6 h (0.96 to 1.03), and at 24 h one direction of
/// the position has 3.29 times the variance of P, while the radial variance alone is 1.08 times that of P.
/// </para>
/// <para>
/// The shape of the departure is the curvature of the orbit: the along-track spread (RMS 2.8 km at 2 h, 38 km at 24 h,
/// from the offset below) lies on the curved orbit, so the samples sit below the straight line of the linear model, the
/// "banana" of J. L. Junkins, M. R. Akella, K. T. Alfriend, <i>Non-Gaussian error propagation in orbital mechanics</i>,
/// Journal of the Astronautical Sciences 44(4), 1996 (confirmed by S. Guillet, 2026-10-16). Their mean radial offset
/// follows −E[δT²] / (2 r) once it exceeds the sampling noise of about 1.4 m: −8.9 m against −6.8 m at 6 h, −104 m
/// against −102 m at 24 h. At the boundary of the domain it is still below that noise (+1.46 m against −0.54 m at
/// 2.0 h), and the evidence there is the variance along the thin direction of P, which the curvature fills. Since P is
/// very thin across that curve, a few metres move the Mahalanobis distances by far more than their spread. With the
/// deviations from the sample mean the test fails from 2.17 h, and with the sample covariance instead of P from 2.33 h
/// (p = 5.8e-5), both upper bounds (their centre and S come from the runs): the distribution itself is no longer
/// Gaussian in Cartesian coordinates.
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class CovarianceMonteCarloTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1980;
    private const int Seed = 20261016;

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public CovarianceMonteCarloTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [Fact]
    public void R1_PropagatedCovariance_MatchesMonteCarlo()
    {
        // Arrange
        var r1 = _cases.R1;
        var start = r1.InitialState.Epoch;
        var epochs = new[] { start.Add(r1.InitialState.Period()), start.AddHours(6.0), start.AddHours(24.0) }
            .Concat(Enumerable.Range(1, 36).Select(k => start.AddMinutes(10.0 * k))).Distinct().OrderBy(t => t).ToArray();

        // Act
        var results = CovarianceMonteCarlo.Run(r1, NaifId, r1.Window, epochs, 5000, Seed);

        // Assert
        var revolution = results.Single(r => r.Epoch == epochs[0]);
        foreach (var r in results)
        {
            _output.WriteLine(CovarianceMonteCarlo.Describe("R1", start, r));
            Assert.True(r.WorstZ < 4.0, $"element-wise at {r.Epoch}: |z| = {r.WorstZ:F2}");
        }

        // Both tests pass on the short arc, at one revolution (specification, F3)
        Assert.True(revolution.Passes, $"one revolution: |z| {revolution.WorstZ:F2}, p {revolution.KsPValue:E2}");

        // Up to 1 h the sample covariance is P along every direction of the state, within the sampling band
        CovarianceMonteCarlo.AssertWithinTheSamplingBand(results.Where(r => r.Epoch <= start.AddHours(1.0)), 5000);

        // The boundary of the domain of validity, on the 10 min grid: measured at 2.0 h (the tests pass up to 1.83 h),
        // asserted within half an hour, since the KS significance crosses α there by a few samples
        var firstFailure = results.First(r => !r.Passes).Epoch;
        Assert.InRange((firstFailure - start).TotalHours, 1.5, 2.5);
        Assert.All(results.Where(r => r.Epoch < firstFailure), r => Assert.True(r.Passes));

        // Beyond it the sample variance departs from P along one direction of the state: records within a factor of 2
        // of the measurement at 6 h and 24 h (the largest ratio of S to P)
        CovarianceMonteCarlo.AssertLargestRatio(results, start.AddHours(6.0), LargestRatioAt6Hours);
        CovarianceMonteCarlo.AssertLargestRatio(results, start.AddHours(24.0), LargestRatioAt24Hours);

        // The position alone, what a collision probability uses, stays within its sampling band up to 6 h; at 24 h one
        // direction of the position departs too (a record)
        CovarianceMonteCarlo.AssertWithinTheSamplingBand(results.Where(r => r.Epoch <= start.AddHours(6.0)), 5000, true);
        CovarianceMonteCarlo.AssertLargestRatio(results, start.AddHours(24.0), LargestPositionRatioAt24Hours, true);
    }

    // The largest ratio of the sample variance to the propagated variance, over the state and over the position (Linux,
    // .NET 10, 2026-10-16)
    private const double LargestRatioAt6Hours = 350.5;

    private const double LargestRatioAt24Hours = 1.26e6;

    private const double LargestPositionRatioAt24Hours = 3.29;
}
