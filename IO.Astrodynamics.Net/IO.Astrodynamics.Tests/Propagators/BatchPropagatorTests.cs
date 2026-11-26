// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators;

public class BatchPropagatorTests
{
    public BatchPropagatorTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    #region Helpers

    private static Spacecraft CreateSpacecraft(int naifId, string name)
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        double r0 = 6800000.0;
        double v0 = System.Math.Sqrt(earth.GM / r0);
        var orbit = new StateVector(
            new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
            earth, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF);
        return new Spacecraft(naifId, name, 100.0, orbit, 10000.0, new Clock("clk", 256));
    }

    private static Window DefaultWindow =>
        new(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddHours(1));

    private static PropagationTask CreateTask(Spacecraft sc)
    {
        return new PropagationTask(sc, DefaultWindow, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60) }, OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody } });
    }

    #endregion

    [Fact]
    public void SingleTaskMatchesDirectPropagate()
    {
        var sc1 = CreateSpacecraft(-2001, "Direct");
        var sc2 = CreateSpacecraft(-2002, "Batch");

        var window = DefaultWindow;
        var bodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody };

        // Direct propagation
        var directSolution = sc1.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60) }, OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = bodies });

        // Batch propagation
        var batchResult = BatchPropagator.Propagate(new[] { CreateTask(sc2) });

        Assert.True(batchResult.AllSucceeded);
        Assert.Single(batchResult.Results);

        var batchSolution = batchResult.Results[0].Solution;

        Assert.Equal(directSolution.StateVectors.Count, batchSolution.StateVectors.Count);

        // Compare first and last states
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var directLast = directSolution.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;
        var batchLast = batchSolution.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        Assert.True((directLast!.Position - batchLast!.Position).Magnitude() < 1.0,
            "Batch result should match direct propagation within 1 m");
    }

    [Fact]
    public void MultipleSpacecraftBatchReturnsAllResults()
    {
        var tasks = new List<PropagationTask>
        {
            CreateTask(CreateSpacecraft(-3001, "Sat1")),
            CreateTask(CreateSpacecraft(-3002, "Sat2")),
            CreateTask(CreateSpacecraft(-3003, "Sat3"))
        };

        var result = BatchPropagator.Propagate(tasks);

        Assert.Equal(3, result.Results.Count);
        Assert.True(result.AllSucceeded);
        Assert.Empty(result.Failed);
        Assert.True(result.ElapsedTime > TimeSpan.Zero);

        foreach (var r in result.Results)
        {
            Assert.True(r.Succeeded);
            Assert.NotNull(r.Solution);
            Assert.Null(r.Error);
            Assert.True(r.Solution.StateVectors.Count > 0);
        }
    }

    [Fact]
    public void BatchResultsMatchSequentialExecution()
    {
        var sc1 = CreateSpacecraft(-4001, "Seq1");
        var sc2 = CreateSpacecraft(-4002, "Seq2");
        var sc3 = CreateSpacecraft(-4003, "Par1");
        var sc4 = CreateSpacecraft(-4004, "Par2");

        var window = DefaultWindow;
        var bodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody };

        // Sequential
        var sol1 = sc1.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60) }, OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = bodies });
        var sol2 = sc2.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60) }, OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = bodies });

        // Parallel batch
        var batchResult = BatchPropagator.Propagate(new[]
        {
            CreateTask(sc3),
            CreateTask(sc4)
        });

        Assert.True(batchResult.AllSucceeded);

        var earth = new CelestialBody(PlanetsAndMoons.Earth);

        // Compare last state of each pair
        var seqLast1 = sol1.StateVectors.Last().RelativeTo(earth, Aberration.None) as StateVector;
        var parLast1 = batchResult.Results[0].Solution.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        var seqLast2 = sol2.StateVectors.Last().RelativeTo(earth, Aberration.None) as StateVector;
        var parLast2 = batchResult.Results[1].Solution.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;

        Assert.True((seqLast1!.Position - parLast1!.Position).Magnitude() < 1.0,
            "Parallel result 1 should match sequential within 1 m");
        Assert.True((seqLast2!.Position - parLast2!.Position).Magnitude() < 1.0,
            "Parallel result 2 should match sequential within 1 m");
    }

    [Fact]
    public void FailedTaskDoesNotAffectOthers()
    {
        var goodSc = CreateSpacecraft(-5001, "Good");
        // Drag about the Moon, which has no atmosphere: the task fails with InvalidOperationException (#674)
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var badSc = new Spacecraft(-5002, "Bad", 100.0, new StateVector(new Vector3(2.0e6, 0, 0), new Vector3(0, System.Math.Sqrt(moon.GM / 2.0e6), 0), moon,
                TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF), 10000.0,
            new Clock("clk", 256));

        var tasks = new List<PropagationTask>
        {
            CreateTask(goodSc),
            new PropagationTask(badSc, DefaultWindow, new PropagationOptions { AtmosphericDrag = true })
        };

        var result = BatchPropagator.Propagate(tasks);

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Failed);

        // Good task succeeded
        Assert.True(result.Results[0].Succeeded);
        Assert.NotNull(result.Results[0].Solution);

        // Bad task failed with the right exception
        Assert.False(result.Results[1].Succeeded);
        Assert.Null(result.Results[1].Solution);
        Assert.IsType<InvalidOperationException>(result.Results[1].Error);
        Assert.Contains("MOON", result.Results[1].Error.Message);
    }

    [Fact]
    public void CancellationStopsBatch()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel

        var tasks = new List<PropagationTask>
        {
            CreateTask(CreateSpacecraft(-6001, "Cancelled"))
        };

        // The token is a parameter since 11.0.0 (decision 4 of lot L4 of #668), carried by the exception
        var exception = Assert.Throws<OperationCanceledException>(() =>
            BatchPropagator.Propagate(tasks, cancellationToken: cts.Token));
        Assert.Equal(cts.Token, exception.CancellationToken);
    }

    [Fact]
    public void ProgressCallbackReportsCorrectCounts()
    {
        var reports = new List<BatchProgress>();
        var progress = new Progress<BatchProgress>(p => reports.Add(p));

        var tasks = new List<PropagationTask>
        {
            CreateTask(CreateSpacecraft(-7001, "Prog1")),
            CreateTask(CreateSpacecraft(-7002, "Prog2")),
            CreateTask(CreateSpacecraft(-7003, "Prog3"))
        };

        // Force sequential to get deterministic progress ordering
        var options = new BatchOptions
        {
            MaxDegreeOfParallelism = 1
        };

        // The progress is a parameter since 11.0.0 (decision 4 of lot L4 of #668)
        var result = BatchPropagator.Propagate(tasks, progress, options);

        Assert.True(result.AllSucceeded);

        // IProgress<T>.Report may be asynchronous with Progress<T>, so
        // spin briefly to let the SynchronizationContext flush reports.
        int retries = 0;
        while (reports.Count < 3 && retries < 50)
        {
            Thread.Sleep(10);
            retries++;
        }

        Assert.Equal(3, reports.Count);

        // All reports should have Total = 3
        Assert.All(reports, r => Assert.Equal(3, r.Total));

        // Completed should reach 3
        Assert.Contains(reports, r => r.Completed == 3);
    }

    [Fact]
    public void CustomRK78IntegratorOptionsWork()
    {
        var sc = CreateSpacecraft(-8001, "RK78Sat");

        var task = new PropagationTask(sc, DefaultWindow, new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions
            {
                AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
            },
            OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody },
            CovariancePropagation = CovariancePropagationOptions.None
        });

        var result = BatchPropagator.Propagate(new[] { task });

        Assert.True(result.AllSucceeded);
        Assert.Single(result.Results);

        var solution = result.Results[0].Solution;
        Assert.True(solution.StateVectors.Count > 0);

        // Verify radius is reasonable for a 6800 km circular orbit
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var lastState = solution.StateVectors.Last()
            .RelativeTo(earth, Aberration.None) as StateVector;
        double radius = lastState!.Position.Magnitude();
        Assert.True(radius > 6799000.0 && radius < 6801000.0,
            $"Radius {radius:F0} m should be near 6800 km");
    }

    [Fact]
    public void EmptyTaskListReturnsEmptyResult()
    {
        var result = BatchPropagator.Propagate(Array.Empty<PropagationTask>());

        Assert.Empty(result.Results);
        Assert.True(result.AllSucceeded);
        Assert.Empty(result.Failed);
        Assert.Equal(TimeSpan.Zero, result.ElapsedTime);
    }

    [Fact]
    public void NullTaskListThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BatchPropagator.Propagate(null));
    }

    [Fact]
    public void DuplicateSpacecraftThrowsArgumentException()
    {
        var sc = CreateSpacecraft(-10001, "Dup");

        var tasks = new List<PropagationTask>
        {
            CreateTask(sc),
            CreateTask(sc) // same instance
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            BatchPropagator.Propagate(tasks));

        Assert.Contains("Duplicate Spacecraft", ex.Message);
    }

    [Fact]
    public void SeparateCelestialBodyInstancesWithGeopotentialWork()
    {
        // Demonstrates the correct pattern: each task creates its own CelestialBody
        // with geopotential so that mutable Legendre/trig buffers are not shared.
        const int taskCount = 4;
        var tasks = new List<PropagationTask>(taskCount);

        for (int i = 0; i < taskCount; i++)
        {
            // Each task gets its own CelestialBody with geopotential — thread-safe pattern
            var earth = new CelestialBody(PlanetsAndMoons.Earth,
                new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

            double r0 = 6800000.0;
            double v0 = System.Math.Sqrt(earth.GM / r0);
            var orbit = new StateVector(
                new Vector3(r0, 0, 0), new Vector3(0, v0, 0),
                earth, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF);

            var sc = new Spacecraft(-(11001 + i), $"GeoSat{i}", 100.0, orbit,
                10000.0, new Clock($"clk{i}", 256));

            tasks.Add(new PropagationTask(sc, DefaultWindow, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0) }, OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = new CelestialItem[] { earth, Stars.SunBody, PlanetsAndMoons.MoonBody }, CovariancePropagation = CovariancePropagationOptions.None }));
        }

        var result = BatchPropagator.Propagate(tasks);

        Assert.True(result.AllSucceeded, "All tasks with separate CelestialBody instances should succeed");
        Assert.Equal(taskCount, result.Results.Count);

        // All results should have identical final radii (same initial orbit, same forces)
        var radii = result.Results.Select(r =>
        {
            var lastState = r.Solution.StateVectors.Last()
                .RelativeTo(r.Spacecraft.InitialOrbitalParameters.Observer, Aberration.None) as StateVector;
            return lastState!.Position.Magnitude();
        }).ToList();

        for (int i = 1; i < radii.Count; i++)
        {
            Assert.True(System.Math.Abs(radii[i] - radii[0]) < 1.0,
                $"Task {i} final radius {radii[i]:F0} m differs from task 0 ({radii[0]:F0} m) by more than 1 m");
        }
    }
}
