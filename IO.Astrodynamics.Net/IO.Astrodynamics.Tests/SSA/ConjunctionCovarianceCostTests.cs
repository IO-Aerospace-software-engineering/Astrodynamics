// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The cost of the propagated covariance in the conjunction analysis (step 10, E2): the replay of the variational
/// equations, the non-linearity index and the avoidance options, measured and published, not asserted beyond their
/// results being there (informative timings, Release).
/// </summary>
/// <remarks>
/// In the <c>LongValidation</c> category since 2026-10-22 (decision of S. Guillet): timings, about 3 min.
/// </remarks>
[Trait("Category", "LongValidation")]
public class ConjunctionCovarianceCostTests : IClassFixture<ReferenceCases>
{
    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public ConjunctionCovarianceCostTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [LongValidationFact]
    public void Cost_OfTheReplay_OfTheIndexAndOfTheAvoidance_OnR2()
    {
        // Arrange: two participants of R2 (LEO 400 km, EGM2008 10×10, Sun, Moon, drag), crossing a day after the start,
        // propagated in RK7(8) at 1e-11
        var r2 = _cases.R2;
        var start = r2.InitialState.Epoch;
        var tca = start.AddHours(23.0);
        var (protectedOrbit, secondaryOrbit) = ConjunctionCovarianceCase.Pair((IO.Astrodynamics.Body.CelestialBody)r2.InitialState.Observer, start, tca);
        var sigma = new[] { 100.0, 100.0, 100.0, 0.1, 0.1, 0.1 };
        var protectedCraft = ReferenceCase.NewSpacecraft(-6801, ConjunctionCovarianceCase.WithRtnCovariance(protectedOrbit, sigma));
        var secondaryCraft = ReferenceCase.NewSpacecraft(-6802, ConjunctionCovarianceCase.WithRtnCovariance(secondaryOrbit, sigma));
        var watch = Stopwatch.StartNew();
        foreach (var craft in new[] { protectedCraft, secondaryCraft })
        {
            craft.Propagate(r2.Window, r2.Bodies, new RK78Integrator(1e-11, 1e-11), true, false, TimeSpan.FromSeconds(60.0), null);
        }

        double propagation = watch.Elapsed.TotalMilliseconds / 2.0;
        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var window = new TimeSystem.Window(start.AddHours(22.0), start.AddHours(24.0));

        // Act: the analysis without the propagated covariance, with the replay only, and with the index
        double held = Median(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { CovariancePropagation = null }));
        double replay = Median(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window));
        double withIndex = Median(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { ComputeNonlinearityIndex = true }), 3);
        // The avoidance burns must lie in the screening window: three lead times from the TCA found in it, up to 6 h, with
        // the default magnitudes and directions (up to 54 options; those below the minimum lead time of 10 min are
        // skipped). The pair meets more than once in the window: the TCA found is the best one, 12.3 h after the start,
        // not the crossing near 23 h, so the lead times reach 0.32 h only
        var screening = new TimeSystem.Window(start.AddHours(12.0), start.AddHours(24.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondaryCraft, screening,
            new ConjunctionAnalysisOptions { ComputeNonlinearityIndex = false });
        double lead = System.Math.Min(6.0 * 3600.0, (encounter.EncounterState.Epoch - screening.StartDate).TotalSeconds - 60.0);
        var leads = new[] { lead / 3.0, 2.0 * lead / 3.0, lead }.Select(TimeSpan.FromSeconds).ToArray();
        watch.Restart();
        var options = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions { LeadTimes = leads, MaxReturnedOptions = 54 });
        double avoidance = watch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"R2-like participants over one day: propagation {propagation:F0} ms each (RK7(8) at 1e-11, without the covariance)");
        _output.WriteLine($"Analyze, covariance held in RTN: {held:F0} ms; replay of both participants: {replay:F0} ms; with the index: {withIndex:F0} ms");
        _output.WriteLine($"EvaluateAvoidance, default magnitudes and directions, lead times up to {lead / 3600.0:F2} h: " +
                          $"{options.Count} options in {avoidance:F0} ms, each propagated with the covariance from its burn " +
                          $"to the end of the window ({(screening.EndDate - encounter.EncounterState.Epoch).TotalHours:F1} h after the TCA)");
        Assert.Equal(CovarianceSource.Propagated, encounter.EncounterState.ProtectedCovariance.Source);
        Assert.NotEmpty(options);
    }

    [LongValidationTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cost_OfTheAvoidanceWithoutAPropagatedCovariance_OnR2(bool rk78)
    {
        // Arrange: the protected participant of R2 (LEO 400 km, EGM2008 10×10, Sun, Moon, drag) with its initial covariance,
        // over 8 h in RK7(8) at 1e-11 or by Velocity-Verlet at 1 s; the secondary a two-body orbit given at the TCA, 6 h
        // after the start, 200 m away; the covariance held in RTN (#402)
        var r2 = _cases.R2;
        var start = r2.InitialState.Epoch;
        var tca = start.AddHours(6.0);
        var propagation = new TimeSystem.Window(start, start.AddHours(8.0));
        var sigma = new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };
        var protectedCraft = ReferenceCase.NewSpacecraft(rk78 ? -6811 : -6812, ConjunctionCovarianceCase.WithRtnCovariance(r2.InitialState, sigma));
        if (rk78)
            protectedCraft.Propagate(propagation, r2.Bodies, new RK78Integrator(1e-11, 1e-11), true, false, TimeSpan.FromSeconds(60.0), null);
        else
            protectedCraft.Propagate(propagation, r2.Bodies, true, false, TimeSpan.FromSeconds(1.0));
        var secondaryCraft = ReferenceCase.NewSpacecraft(rk78 ? -6813 : -6814, AvoidanceFallbackCase.SecondaryNear(
            protectedCraft.GetEphemeris(tca, r2.InitialState.Observer, r2.InitialState.Frame, Aberration.None).ToStateVector()));
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            new TimeSystem.Window(start, start.AddHours(7.0)), new ConjunctionAnalysisOptions { CovariancePropagation = null });

        // Act: the default grid (lead times 1 h, 3 h and 6 h; 0.05, 0.10 and 0.25 m/s; six directions)
        var watch = Stopwatch.StartNew();
        var options = ConjunctionAssessment.EvaluateAvoidance(encounter, new AvoidanceSearchOptions { MaxReturnedOptions = 54 });
        double avoidance = watch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"EvaluateAvoidance without a propagated covariance, {(rk78 ? "RK7(8) at 1e-11" : "Velocity-Verlet at 1 s")}: " +
                          $"{options.Count} options in {avoidance:F0} ms, each propagated from its burn to the end of the window");
        Assert.NotEmpty(options);
        Assert.All(options, o => Assert.Equal(AvoidanceTrajectory.Propagated, o.Trajectory));
    }

    // The median of the elapsed time of several calls after one warm-up call, in ms
    private static double Median(Func<EncounterCase> analysis, int calls = 5)
    {
        analysis();
        var times = new double[calls];
        for (int i = 0; i < calls; i++)
        {
            var watch = Stopwatch.StartNew();
            analysis();
            times[i] = watch.Elapsed.TotalMilliseconds;
        }

        return times.OrderBy(t => t).ElementAt(calls / 2);
    }
}
