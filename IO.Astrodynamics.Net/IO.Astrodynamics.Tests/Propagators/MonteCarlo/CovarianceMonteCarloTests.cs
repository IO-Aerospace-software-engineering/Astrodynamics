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
/// to 24 h: worst |z| 1.38 at 10 min, below 0.8 from 40 min, 0.29 at 24 h. The second moments of the propagated
/// covariance are those of the Monte Carlo. The Kolmogorov-Smirnov test of the squared Mahalanobis distances holds up
/// to 1.83 h (p = 0.011; 0.058 at one revolution, 1.646 h) and fails from 2.0 h (p = 1.9e-4), 1.2 revolutions: the
/// domain of validity of the linear propagation for this P0 (σ = 100 m and 0.1 m/s in RTN) in LEO 700 km.
/// </para>
/// <para>
/// The shape of the departure is the curvature of the orbit: the along-track spread (RMS 2.8 km at 2 h, 38 km at 24 h,
/// from the offset below) lies on the curved orbit, so the samples sit below the straight line of the linear model, by a
/// mean radial offset that follows −E[δT²] / (2 r) (−8.9 m against −6.8 m at 6 h, −104 m against −102 m at 24 h,
/// around which the sampling noise moves it by about 1.4 m), the "banana" of J. L. Junkins, M. R. Akella, K. T. Alfriend, <i>Non-Gaussian error
/// propagation in orbital mechanics</i>, Journal of the Astronautical Sciences 44(4), 1996 (to be verified by
/// S. Guillet). Since P is very thin across that curve, a few metres move the Mahalanobis distances by far more than
/// their spread. With the deviations from the sample mean the test fails from 2.17 h, and with the sample covariance
/// instead of P from 2.33 h (p = 5.8e-5): the distribution itself is no longer Gaussian in Cartesian coordinates.
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
            _output.WriteLine($"R1 at {(r.Epoch - start).TotalHours:F3} h: worst |z| {r.WorstZ:F2} ({r.WorstRow}, " +
                              $"{r.WorstColumn}); KS D = {r.KsStatistic:E3}, p = {r.KsPValue:E3} (about the mean " +
                              $"{r.KsPValueAboutTheMean:E3}, of the sample {r.KsPValueOfTheSample:E3}); mean radial offset " +
                              $"{r.MeanRadialOffset:F2} m, curvature −E[δT²]/(2r) {r.CurvatureOffset:F2} m");
            Assert.True(r.WorstZ < 4.0, $"element-wise at {r.Epoch}: |z| = {r.WorstZ:F2}");
        }

        // Both tests pass on the short arc, at one revolution (specification, F3)
        Assert.True(revolution.Passes, $"one revolution: |z| {revolution.WorstZ:F2}, p {revolution.KsPValue:E2}");

        // The boundary of the domain of validity: the tests pass up to 1.83 h and fail from 2.0 h, on the 10 min grid
        var firstFailure = results.First(r => !r.Passes).Epoch;
        Assert.Equal(start.AddHours(2.0), firstFailure);
        Assert.All(results.Where(r => r.Epoch < firstFailure), r => Assert.True(r.Passes));
    }

    [Fact]
    public void PropagateRuns_GivesEachRunItsCoefficients()
    {
        // Arrange: two runs of R1 over 10 min, with their own Cd and Cr
        var r1 = _cases.R1;
        var start = CovarianceMonteCarlo.WithInitialCovariance(r1.InitialState);
        var window = new TimeSystem.Window(r1.InitialState.Epoch, r1.InitialState.Epoch.AddMinutes(10.0));
        var config = CovarianceMonteCarlo.Configuration(r1, ReferenceCase.NewSpacecraft(NaifId - 3, start), window, 2,
            Seed, NaifId - 3);
        var (states, coefficients) = StateSampler.SampleWithCoefficients(start, 2.2, 0.22, 1.5, 0.15, 2, Seed);

        // Act
        var runs = MonteCarloPropagator.PropagateRuns(config, start, states, 7, coefficients);

        // Assert
        for (int k = 0; k < 2; k++)
        {
            Assert.True(runs.Results[k].Succeeded);
            Assert.Equal(coefficients[k].DragCoefficient, runs.Results[k].Spacecraft.DragCoefficient);
            Assert.Equal(coefficients[k].ReflectivityCoefficient, runs.Results[k].Spacecraft.SolarRadiationCoeff);
            Assert.Equal(config.BaseNaifId - 7 - k, runs.Results[k].Spacecraft.NaifId);
            Assert.Equal(states[k].Position, runs.Results[k].Spacecraft.InitialOrbitalParameters.ToStateVector().Position);
        }
    }

    [Fact]
    public void PublicCampaign_IsTheAggregateOfItsRuns()
    {
        // Arrange: 200 runs, the first 200 of the campaign of 5000 (the sampler draws them in order)
        var r1 = _cases.R1;
        var start = CovarianceMonteCarlo.WithInitialCovariance(r1.InitialState);
        var config = CovarianceMonteCarlo.Configuration(r1, ReferenceCase.NewSpacecraft(NaifId - 2, start), r1.Window, 200,
            Seed, NaifId - 2);

        // Act
        var campaign = MonteCarloPropagator.Propagate(config);
        var runs = MonteCarloPropagator.PropagateRuns(config, start, StateSampler.Sample(start, 5000, Seed)[..200], 0);

        // Assert: at each output epoch, the sample covariance of the public product is that of the runs
        Assert.Equal(200, campaign.SuccessCount);
        for (int k = 0; k < campaign.EpochStatistics.Count; k++)
        {
            var statistics = campaign.EpochStatistics[k];
            var states = runs.Results.Select(r => WholePropagationDifferences.State(r.Solution, statistics.Epoch)).ToArray();
            var expected = CovarianceMonteCarlo.SampleCovariance(states);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(expected[6 * i + j], statistics.SampleCovariance.Get(i, j),
                        1e-12 * System.Math.Sqrt(expected[7 * i] * expected[7 * j]));
                }
            }
        }
    }
}
