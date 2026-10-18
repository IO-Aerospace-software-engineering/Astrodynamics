// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Propagator.Variational;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The probability of collision of Foster with the propagated covariance against a Monte Carlo of the probability, the
/// measurement that sets the threshold of the non-linearity index (step 10, the guard decided on 2026-10-16).
/// </summary>
/// <remarks>
/// <para>
/// Two-body encounters of the case of <see cref="ConjunctionCovarianceCase"/>, the TCA 6 h, 24 h and 72 h after the
/// initial states, with a realistic initial covariance (σ 100 m and 0.1 m/s on each RTN axis) and a large one (1 km and
/// 1 m/s). Foster: the analysis with the covariance propagated by the variational equations of RK7(8), combined
/// hard-body radius 500 m. Monte Carlo: N pairs of initial states drawn from the two initial covariances (fixed seed),
/// each propagated exactly in the two-body problem to the TCA (<see cref="OrbitalParameters.OrbitalParameters.AtEpoch"/>,
/// independent of the RK7(8)), to its own closest approach near the TCA (the epoch moved twice by the time to the closest
/// approach of the rectilinear relative motion, each state propagated again from its initial state), a hit below the
/// combined radius.
/// </para>
/// <para>
/// Agreement: Foster within the 99 % Clopper–Pearson interval of the Monte Carlo (C. J. Clopper, E. S. Pearson, <i>The
/// use of confidence or fiducial limits illustrated in the case of the binomial</i>, Biometrika 26, 1934; the interval
/// from the quantiles of the beta distribution; confirmed by S. Guillet, 2026-10-18).
/// </para>
/// </remarks>
[Trait("Category", "LongValidation")]
public class FosterMonteCarloValidationTests : IClassFixture<ConjunctionCovarianceCase>
{
    private const int Seed = 20261017;
    private const double CombinedRadius = 500.0;

    private readonly ConjunctionCovarianceCase _case;
    private readonly ITestOutputHelper _output;

    public FosterMonteCarloValidationTests(ConjunctionCovarianceCase @case, ITestOutputHelper output)
    {
        _case = @case;
        _output = output;
    }

    [Theory]
    [InlineData(6.0, 1.0, 10_000_000)]
    [InlineData(24.0, 1.0, 10_000_000)]
    [InlineData(72.0, 1.0, 10_000_000)]
    [InlineData(6.0, 10.0, 10_000_000)]
    [InlineData(24.0, 10.0, 10_000_000)]
    [InlineData(72.0, 10.0, 10_000_000)]
    [InlineData(168.0, 10.0, 100_000_000)]
    [InlineData(72.0, 30.0, 100_000_000)]
    [InlineData(168.0, 30.0, 100_000_000)]
    [InlineData(72.0, 100.0, 100_000_000)]
    [InlineData(168.0, 100.0, 100_000_000)]
    public void Foster_AgainstMonteCarlo(double hours, double scale, int samples)
    {
        // Arrange
        var start = ConjunctionCovarianceCase.Start;
        var tca = start.AddHours(hours);
        var (protectedOrbit, secondaryOrbit) = ConjunctionCovarianceCase.Pair(_case.Earth, start, tca);
        var sigma = new[] { 100.0, 100.0, 100.0, 0.1, 0.1, 0.1 }.Select(s => s * scale).ToArray();
        var protectedStart = ConjunctionCovarianceCase.WithRtnCovariance(protectedOrbit, sigma);
        var secondaryStart = ConjunctionCovarianceCase.WithRtnCovariance(secondaryOrbit, sigma);
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6601 - (int)hours - 1000 * (int)scale, protectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6701 - (int)hours - 1000 * (int)scale, secondaryStart);
        var window = new TimeSystem.Window(start, tca.AddHours(1.0));
        foreach (var craft in new[] { protectedCraft, secondaryCraft })
        {
            craft.Propagate(window, new CelestialItem[] { _case.Earth },
                new IO.Astrodynamics.Propagator.Integrators.RK78Integrator(1e-12, 1e-12), false, false,
                TimeSpan.FromSeconds(600.0), null);
        }

        // Act: Foster with the propagated covariance; the Monte Carlo at its TCA
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            new TimeSystem.Window(tca.AddMinutes(-20.0), tca.AddMinutes(20.0)),
            new ConjunctionAnalysisOptions { SecondaryHardBodyRadiusMeters = CombinedRadius, ComputeNonlinearityIndex = true });
        double foster = encounter.CollisionRisk.ProbabilityOfCollision!.Value;
        double nu = System.Math.Max(encounter.EncounterState.ProtectedCovariance.NonlinearityIndex!.Value,
            encounter.EncounterState.SecondaryCovariance.NonlinearityIndex!.Value);
        long hits = MonteCarloHits(protectedStart, secondaryStart, encounter.EncounterState.Epoch, samples);
        var (low, high) = ClopperPearson(hits, samples, 0.01);
        double monteCarlo = (double)hits / samples;
        bool agrees = foster >= low && foster <= high;

        // Assert: the measurement, published (the threshold of the index is set from it)
        _output.WriteLine($"{hours} h, P0 × {scale}²: ν {nu:E3}; Foster {foster:E4}; Monte Carlo {monteCarlo:E4} " +
                          $"({hits} hits of {samples}, 99 % [{low:E4}, {high:E4}]); {(agrees ? "agrees" : "departs")}; " +
                          $"miss {encounter.EncounterState.MissDistanceMeters:F1} m, σ in-track {encounter.CollisionRisk.InTrackSigmaMeters:F0} m");
        Assert.True(hits > 0);
        if (nu <= ConjunctionAnalysisOptions.DefaultNonlinearityIndexThreshold)
        {
            Assert.True(agrees, $"ν {nu:E3} below the threshold, Foster {foster:E4} outside [{low:E4}, {high:E4}]");
        }
    }

    // Pairs of initial states drawn from the two initial covariances, propagated exactly in the two-body problem to the
    // TCA; the number whose rectilinear closest approach there is below the combined radius
    private static long MonteCarloHits(StateVector protectedStart, StateVector secondaryStart, TimeSystem.Time tca,
        int samples)
    {
        var protectedFactor = Cholesky(protectedStart);
        var secondaryFactor = Cholesky(secondaryStart);
        const int chunks = 100;
        long total = 0;
        Parallel.For(0, chunks, chunk =>
        {
            var random = new Random(Seed + chunk);
            long hits = 0;
            for (int i = 0; i < samples / chunks; i++)
            {
                var protectedSample = Sample(protectedStart, protectedFactor, random);
                var secondarySample = Sample(secondaryStart, secondaryFactor, random);
                double d2 = ClosestApproachSquared(protectedSample, secondarySample, tca);
                if (d2 < CombinedRadius * CombinedRadius)
                    hits++;
            }

            System.Threading.Interlocked.Add(ref total, hits);
        });
        return total;
    }

    // The squared distance of the closest approach of one pair near the nominal TCA: the epoch moved twice by the time
    // to the closest approach of the rectilinear relative motion, each state propagated again from its initial state,
    // exactly in the two-body problem; the residual time is then below a millisecond, where the motion is rectilinear
    private static double ClosestApproachSquared(StateVector protectedSample, StateVector secondarySample,
        TimeSystem.Time tca)
    {
        var epoch = tca;
        double d2 = double.NaN;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            var p = protectedSample.ToStateVector(epoch);
            var s = secondarySample.ToStateVector(epoch);
            var r = s.Position - p.Position;
            var v = s.Velocity - p.Velocity;
            double rv = r * v;
            double vv = v * v;
            d2 = r * r - rv * rv / vv;
            epoch = epoch.AddSeconds(-rv / vv);
        }

        return d2;
    }

    private static Matrix<double> Cholesky(StateVector state)
    {
        var p0 = state.Covariance.Value;
        return Matrix<double>.Build.Dense(6, 6, (i, j) => 0.5 * (p0.Get(i, j) + p0.Get(j, i))).Cholesky().Factor;
    }

    private static StateVector Sample(StateVector mean, Matrix<double> factor, Random random)
    {
        var z = new double[6];
        for (int k = 0; k < 6; k++)
            z[k] = Normal.Sample(random, 0.0, 1.0);
        var d = new double[6];
        for (int i = 0; i < 6; i++)
        {
            for (int k = 0; k <= i; k++)
                d[i] += factor[i, k] * z[k];
        }

        return new StateVector(mean.Position + new Vector3(d[0], d[1], d[2]), mean.Velocity + new Vector3(d[3], d[4], d[5]),
            mean.Observer, mean.Epoch, mean.Frame);
    }

    // The exact (Clopper–Pearson) interval of a binomial proportion at confidence 1 − α
    internal static (double Low, double High) ClopperPearson(long hits, long trials, double alpha)
    {
        double low = hits == 0 ? 0.0 : Beta.InvCDF(hits, trials - hits + 1, alpha / 2.0);
        double high = hits == trials ? 1.0 : Beta.InvCDF(hits + 1, trials - hits, 1.0 - alpha / 2.0);
        return (low, high);
    }
}
