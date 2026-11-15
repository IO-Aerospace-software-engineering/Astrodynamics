// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

/// <summary>
/// The runs of a Monte Carlo campaign (<see cref="MonteCarloPropagator.PropagateRuns"/>), which F3 reads at any epoch:
/// the coefficients of each run, and the public product against its runs.
/// </summary>
public class MonteCarloRunsTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1990;
    private const int Seed = 20261016;

    private readonly ReferenceCases _cases;

    public MonteCarloRunsTests(ReferenceCases cases)
    {
        _cases = cases;
    }

    [Fact]
    public void PropagateRuns_GivesEachRunItsCoefficients()
    {
        // Arrange: two runs of R1 over 10 min, with their own Cd and Cr
        var r1 = _cases.R1;
        var start = CovarianceMonteCarlo.WithInitialCovariance(r1.InitialState);
        var window = new TimeSystem.Window(r1.InitialState.Epoch, r1.InitialState.Epoch.AddMinutes(10.0));
        var config = CovarianceMonteCarlo.Configuration(r1, ReferenceCase.NewSpacecraft(NaifId - 1, start), window, 2,
            Seed);
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

    /// <summary>
    /// A campaign whose ids <c>BaseNaifId − i</c> leave [-2147483, -1] (#676): run 4 of BaseNaifId -2147480 would be
    /// -2147484, whose frame id NaifId × 1000 is not an int; the construction of its task throws, before any run is
    /// propagated. On main the frame id wrapped around to 2147483296 and the campaign ran.
    /// </summary>
    [Fact]
    public void BaseNaifIdLeavingTheRange_Throws()
    {
        // Arrange
        var r1 = _cases.R1;
        var start = CovarianceMonteCarlo.WithInitialCovariance(r1.InitialState);
        var window = new TimeSystem.Window(r1.InitialState.Epoch, r1.InitialState.Epoch.AddMinutes(10.0));
        var config = CovarianceMonteCarlo.Configuration(r1, ReferenceCase.NewSpacecraft(NaifId - 2, start), window, 5, Seed) with
        {
            BaseNaifId = -2147480
        };

        // Act
        var exception = Record.Exception(() => MonteCarloPropagator.Propagate(config));

        // Assert
        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("naifId", rejected.ParamName);
        Assert.Equal(-2147484, rejected.ActualValue);
    }

    /// <summary>
    /// Control (#676): the campaign whose last id is -2147483, the bound, runs its 5 runs.
    /// </summary>
    [Fact]
    public void BaseNaifIdAtTheBound_Runs()
    {
        // Arrange
        var r1 = _cases.R1;
        var start = CovarianceMonteCarlo.WithInitialCovariance(r1.InitialState);
        var window = new TimeSystem.Window(r1.InitialState.Epoch, r1.InitialState.Epoch.AddMinutes(10.0));
        var config = CovarianceMonteCarlo.Configuration(r1, ReferenceCase.NewSpacecraft(NaifId - 3, start), window, 5, Seed) with
        {
            BaseNaifId = -2147479
        };

        // Act
        var campaign = MonteCarloPropagator.Propagate(config);

        // Assert
        Assert.Equal(5, campaign.SuccessCount);
        Assert.Equal(0, campaign.FailureCount);
    }

    [Fact]
    public void PublicCampaign_IsTheAggregateOfItsRuns()
    {
        // Arrange: 200 runs, the first 200 of the campaign of 5000 (the sampler draws them in order)
        var r1 = _cases.R1;
        var start = CovarianceMonteCarlo.WithInitialCovariance(r1.InitialState);
        var config = CovarianceMonteCarlo.Configuration(r1, ReferenceCase.NewSpacecraft(NaifId, start), r1.Window, 200,
            Seed);

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
