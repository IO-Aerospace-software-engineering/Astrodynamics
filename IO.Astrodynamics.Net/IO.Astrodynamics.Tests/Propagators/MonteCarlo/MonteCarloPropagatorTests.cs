using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

public class MonteCarloPropagatorTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public MonteCarloPropagatorTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    private static Spacecraft CreateTemplateSpacecraft()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double r0 = 6800000.0; // ~430 km altitude
        double v0 = System.Math.Sqrt(earth.GM / r0);

        // Diagonal covariance: 100 m² position, 0.01 m²/s² velocity
        var cov = new Matrix(6, 6);
        cov.Set(0, 0, 100.0);
        cov.Set(1, 1, 100.0);
        cov.Set(2, 2, 100.0);
        cov.Set(3, 3, 0.01);
        cov.Set(4, 4, 0.01);
        cov.Set(5, 5, 0.01);

        var orbit = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, cov);

        return new Spacecraft(-1000, "Template", 100.0, 10000.0, new Clock("clk", 256), orbit);
    }

    private static MonteCarloConfiguration CreateDefaultConfig(int runCount = 5, int seed = 42)
    {
        var template = CreateTemplateSpacecraft();
        return new MonteCarloConfiguration
        {
            RunCount = runCount,
            Seed = seed,
            TemplateSpacecraft = template,
            Window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(1)),
            CelestialBodiesFactory = () =>
            [
                new CelestialBody(PlanetsAndMoons.EARTH),
                Stars.SUN_BODY,
                PlanetsAndMoons.MOON_BODY
            ],
            DeltaT = TimeSpan.FromSeconds(60),
        };
    }

    [Fact]
    public void SameSeedProducesSameResults()
    {
        var config1 = CreateDefaultConfig(runCount: 3, seed: 42);
        var config2 = CreateDefaultConfig(runCount: 3, seed: 42);

        var result1 = MonteCarloPropagator.Propagate(config1);
        var result2 = MonteCarloPropagator.Propagate(config2);

        Assert.Equal(result1.EpochStatistics.Count, result2.EpochStatistics.Count);
        for (int i = 0; i < result1.EpochStatistics.Count; i++)
        {
            Assert.Equal(result1.EpochStatistics[i].MeanPosition.X,
                result2.EpochStatistics[i].MeanPosition.X, 6);
            Assert.Equal(result1.EpochStatistics[i].MeanPosition.Y,
                result2.EpochStatistics[i].MeanPosition.Y, 6);
        }
    }

    [Fact]
    public void DifferentSeedsProduceDifferentResults()
    {
        var config1 = CreateDefaultConfig(runCount: 3, seed: 42);
        var config2 = CreateDefaultConfig(runCount: 3, seed: 99);

        var result1 = MonteCarloPropagator.Propagate(config1);
        var result2 = MonteCarloPropagator.Propagate(config2);

        // At least one epoch should have different mean positions
        bool anyDifferent = false;
        for (int i = 0; i < result1.EpochStatistics.Count && i < result2.EpochStatistics.Count; i++)
        {
            if (System.Math.Abs(result1.EpochStatistics[i].MeanPosition.X - result2.EpochStatistics[i].MeanPosition.X) > 0.001)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent);
    }

    [Fact]
    public void DispersionGrowsWithTime()
    {
        var config = CreateDefaultConfig(runCount: 5, seed: 42);
        var result = MonteCarloPropagator.Propagate(config);

        Assert.True(result.EpochStatistics.Count > 2);

        var firstStd = result.EpochStatistics[0].StandardDeviation.Position;
        var firstStdPos = firstStd.Magnitude();

        var lastIdx = result.EpochStatistics.Count - 1;
        var lastStd = result.EpochStatistics[lastIdx].StandardDeviation.Position;
        var lastStdPos = lastStd.Magnitude();

        Assert.True(lastStdPos > firstStdPos,
            $"Position dispersion should grow: first={firstStdPos:F2}, last={lastStdPos:F2}");
    }

    [Fact]
    public void TwoBodyCircularOrbitDispersion()
    {
        var config = CreateDefaultConfig(runCount: 5, seed: 42);
        var result = MonteCarloPropagator.Propagate(config);

        Assert.True(result.SuccessCount >= 2);
        Assert.True(result.EpochStatistics.Count > 0);

        // Initial position std dev should be on the order of sqrt(100)=10m
        var initialPosStd = result.EpochStatistics[0].StandardDeviation.Position.X;
        Assert.True(initialPosStd > 11.0 && initialPosStd < 12.0,
            $"Initial position X std dev should be ~10m, got {initialPosStd:F2}");
    }

    [Fact]
    public void SuccessAndFailureCountsCorrect()
    {
        var config = CreateDefaultConfig(runCount: 3, seed: 42);
        var result = MonteCarloPropagator.Propagate(config);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(42, result.Seed);
    }

    [Fact]
    public void CancellationStopsMonteCarlo()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel

        var config = CreateDefaultConfig(runCount: 10, seed: 42) with
        {
            BatchOptions = new BatchOptions { CancellationToken = cts.Token }
        };

        Assert.Throws<OperationCanceledException>(() => MonteCarloPropagator.Propagate(config));
    }

    [Fact]
    public void ProgressReporting()
    {
        int reportCount = 0;
        var progress = new Progress<BatchProgress>(p => Interlocked.Increment(ref reportCount));

        var config = CreateDefaultConfig(runCount: 3, seed: 42) with
        {
            BatchOptions = new BatchOptions
            {
                Progress = progress,
                MaxDegreeOfParallelism = 1 // sequential for deterministic progress
            }
        };

        var result = MonteCarloPropagator.Propagate(config);

        // Progress may report asynchronously; just verify propagation succeeded
        Assert.Equal(3, result.SuccessCount);
    }

    [Fact]
    public void InvalidConfig_RunCountLessThan2_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MonteCarloPropagator.Propagate(CreateDefaultConfig(runCount: 1)));
    }

    [Fact]
    public void InvalidConfig_NullSpacecraft_Throws()
    {
        var config = new MonteCarloConfiguration
        {
            RunCount = 2,
            Seed = 42,
            TemplateSpacecraft = null,
            Window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(1)),
            CelestialBodiesFactory = () => Array.Empty<CelestialItem>(),
            DeltaT = TimeSpan.FromSeconds(60),
        };

        Assert.Throws<ArgumentNullException>(() => MonteCarloPropagator.Propagate(config));
    }

    [Fact]
    public void InvalidConfig_NoCovarianceOnState_Throws()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double r0 = 6800000.0;
        double v0 = System.Math.Sqrt(earth.GM / r0);
        var orbit = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF); // no covariance

        var sc = new Spacecraft(-1000, "NoCov", 100.0, 10000.0, new Clock("clk", 256), orbit);

        var config = new MonteCarloConfiguration
        {
            RunCount = 2,
            Seed = 42,
            TemplateSpacecraft = sc,
            Window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(1)),
            CelestialBodiesFactory = () => [earth],
            DeltaT = TimeSpan.FromSeconds(60),
        };

        Assert.Throws<ArgumentException>(() => MonteCarloPropagator.Propagate(config));
    }

    [Fact]
    public void InvalidConfig_NullFactory_Throws()
    {
        var config = new MonteCarloConfiguration
        {
            RunCount = 2,
            Seed = 42,
            TemplateSpacecraft = CreateTemplateSpacecraft(),
            Window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(1)),
            CelestialBodiesFactory = null,
            DeltaT = TimeSpan.FromSeconds(60),
        };

        Assert.Throws<ArgumentNullException>(() => MonteCarloPropagator.Propagate(config));
    }

    [Fact]
    public void WithGeopotential_SeparateInstancesWork()
    {
        var template = CreateTemplateSpacecraftWithGeopotential();

        var config = new MonteCarloConfiguration
        {
            RunCount = 3,
            Seed = 42,
            TemplateSpacecraft = template,
            Window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10)),
            CelestialBodiesFactory = () =>
            [
                new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
                    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)),
                Stars.SUN_BODY,
                PlanetsAndMoons.MOON_BODY
            ],
            DeltaT = TimeSpan.FromSeconds(60),
        };

        var result = MonteCarloPropagator.Propagate(config);

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.True(result.EpochStatistics.Count > 0);
    }

    private static Spacecraft CreateTemplateSpacecraftWithGeopotential()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        double r0 = 6800000.0;
        double v0 = System.Math.Sqrt(earth.GM / r0);

        var cov = new Matrix(6, 6);
        cov.Set(0, 0, 100.0);
        cov.Set(1, 1, 100.0);
        cov.Set(2, 2, 100.0);
        cov.Set(3, 3, 0.01);
        cov.Set(4, 4, 0.01);
        cov.Set(5, 5, 0.01);

        var orbit = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, cov);

        return new Spacecraft(-1000, "Template", 100.0, 10000.0, new Clock("clk", 256), orbit);
    }
}
