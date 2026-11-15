// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// <see cref="CelestialItem.Satellites"/> (#676): the instances orbiting a body, held weakly, by reference, safe across
/// threads, and only those whose constructor completed.
/// </summary>
public class SatellitesTests
{
    private static readonly TimeSystem.Time Epoch = new(2024, 1, 1, 0, 0, 0);

    public SatellitesTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static StateVector Orbit(ILocalizable observer) =>
        new(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0), observer, Epoch, Frames.Frame.ICRF);

    private static bool Lists(CelestialItem body, CelestialItem item) => body.Satellites.Any(x => ReferenceEquals(x, item));

    // Built in a frame of its own, so that no local of the test keeps the spacecraft reachable
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference> BuildAndDrop(CelestialBody earth, int count)
    {
        var references = new List<WeakReference>(count);
        for (int i = 0; i < count; i++)
        {
            references.Add(new WeakReference(new Spacecraft(-30000 - i, "DROPPED" + i, 100.0, 1000.0,
                new Clock("DROPPED" + i, 65536), Orbit(earth))));
        }

        return references;
    }

    [Fact]
    public void DroppedSpacecraft_LeaveSatellites()
    {
        // Arrange: 1000 spacecraft about the process-wide Earth, no longer referenced once built
        var earth = PlanetsAndMoons.EARTH_BODY;
        var references = BuildAndDrop(earth, 1000);

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert: the collection is not a root; on main every one stays alive, listed by the Earth
        Assert.Equal(0, references.Count(r => r.IsAlive));
        Assert.DoesNotContain(earth.Satellites, x => x.Name.StartsWith("DROPPED", StringComparison.Ordinal));
    }

    [Fact]
    public void ConcurrentConstruction_ListsEachInstanceOnce()
    {
        // 20 runs of 4000 constructions on 4 threads, each about a body of its own: on main the HashSet lost entries or
        // threw in each of 20 runs (the triage's measurement)
        for (int run = 0; run < 20; run++)
        {
            // Arrange
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var built = new Spacecraft[4000];

            // Act
            var exception = Record.Exception(() => Parallel.For(0, built.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 },
                i => built[i] = new Spacecraft(-40000 - i, "P" + i, 100.0, 1000.0, new Clock("P" + i, 65536), Orbit(earth))));

            // Assert: the set of the instances built, by reference
            Assert.Null(exception);
            var listed = earth.Satellites.ToArray();
            Assert.Equal(built.Length, listed.Length);
            Assert.Equal(built.Length, earth.Satellites.Count);
            var expected = new HashSet<CelestialItem>(built, ReferenceEqualityComparer.Instance);
            Assert.True(expected.SetEquals(listed), $"run {run}");
        }
    }

    /// <summary>
    /// The registration under contention (#676): twice as many threads as cores, 16 at least, released together by a
    /// barrier, list interleaved instances about one body, in 5 runs. The construction test above depends on the load of
    /// the process (its adds rarely meet when the other tests run); here every thread adds at once, so that the HashSet of
    /// main loses entries or throws its InvalidOperationException in each run.
    /// </summary>
    [Fact]
    public void ConcurrentRegistration_ListsEachInstanceOnce()
    {
        // Arrange: 500 spacecraft per thread, built about a body of their own
        int threads = System.Math.Max(16, 2 * Environment.ProcessorCount);
        var source = new CelestialBody(PlanetsAndMoons.EARTH);
        var items = Enumerable.Range(0, threads * 500)
            .Select(i => new Spacecraft(-50000 - i, "R" + i, 100.0, 1000.0, new Clock("R" + i, 65536), Orbit(source))).ToArray();
        var expected = new HashSet<CelestialItem>(items, ReferenceEqualityComparer.Instance);

        for (int run = 0; run < 5; run++)
        {
            // Act: each thread adds the items t, t + threads, t + 2 threads, ... about a fresh body
            var target = new CelestialBody(PlanetsAndMoons.MOON);
            var errors = new ConcurrentQueue<Exception>();
            using var barrier = new Barrier(threads);
            var workers = Enumerable.Range(0, threads).Select(t => new Thread(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    for (int i = t; i < items.Length; i += threads)
                    {
                        target.AddSatellite(items[i]);
                    }
                }
                catch (Exception e)
                {
                    errors.Enqueue(e);
                }
            })).ToArray();
            foreach (var worker in workers) worker.Start();
            foreach (var worker in workers) worker.Join();

            // Assert: no error, every instance listed once
            Assert.True(errors.IsEmpty, $"run {run}: {errors.FirstOrDefault()}");
            var listed = target.Satellites.ToArray();
            Assert.Equal(items.Length, listed.Length);
            Assert.True(expected.SetEquals(listed), $"run {run}");
        }
    }

    [Fact]
    public void SameNaifId_BothListed_RemoveByInstance()
    {
        // Arrange: two spacecraft of one NAIF id
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var a = new Spacecraft(-1001, "A", 100.0, 1000.0, new Clock("CA", 65536), Orbit(earth));
        var b = new Spacecraft(-1001, "B", 100.0, 1000.0, new Clock("CB", 65536), Orbit(earth));
        Assert.True(Lists(earth, a));
        Assert.True(Lists(earth, b));

        // Act: B moves about the Moon
        b.SetInitialOrbitalParameters(Orbit(moon));

        // Assert: A stays, B is listed by the Moon only
        Assert.True(Lists(earth, a));
        Assert.False(Lists(earth, b));
        Assert.True(Lists(moon, b));
        Assert.Equal(1, earth.Satellites.Count);
        Assert.Equal(1, moon.Satellites.Count);
    }

    public static IEnumerable<object[]> ThrowingConstructions()
    {
        // maximumOperatingMass < mass
        yield return [new Func<CelestialBody, Clock, Spacecraft>((o, c) => new Spacecraft(-1003, "GHOST", 1000.0, 10.0, c, Orbit(o))), typeof(ArgumentOutOfRangeException)];
        // a NAIF id whose frame id NaifId * 1000 is not an int
        yield return [new Func<CelestialBody, Clock, Spacecraft>((o, c) => new Spacecraft(-2147484, "GHOST", 100.0, 1000.0, c, Orbit(o))), typeof(ArgumentOutOfRangeException)];
        // non-orthogonal body axes
        yield return [new Func<CelestialBody, Clock, Spacecraft>((o, c) => new Spacecraft(-1003, "GHOST", 100.0, 1000.0, c, Orbit(o),
            bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorX, bodyUp: Vector3.VectorZ)), typeof(ArgumentException)];
    }

    [Theory]
    [MemberData(nameof(ThrowingConstructions))]
    public void ThrowingConstructor_RegistersNothing(Func<CelestialBody, Clock, Spacecraft> construct, Type exceptionType)
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var clock = new Clock("GHOST_CLK", 65536);

        // Act
        var exception = Record.Exception(() => construct(earth, clock));

        // Assert: nothing listed, and the clock still free for a valid spacecraft
        Assert.IsType(exceptionType, exception);
        Assert.Empty(earth.Satellites);
        Assert.Null(clock.Spacecraft);
        var valid = new Spacecraft(-1003, "VALID", 100.0, 1000.0, clock, Orbit(earth));
        Assert.Same(valid, clock.Spacecraft);
        Assert.Same(valid, earth.Satellites.Single());
    }

    [Fact]
    public void ThrowingConstructor_ClockOfAnotherId_RegistersNothing()
    {
        // Arrange: a clock attached to a spacecraft of another NAIF id
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var clock = new Clock("SHARED_CLK", 65536);
        var holder = new Spacecraft(-1004, "HOLDER", 100.0, 1000.0, clock, Orbit(earth));

        // Act
        var exception = Record.Exception(() => new Spacecraft(-1005, "GHOST", 100.0, 1000.0, clock, Orbit(earth)));

        // Assert: only the holder is listed
        Assert.IsType<ArgumentException>(exception);
        Assert.Same(holder, earth.Satellites.Single());
    }

    [Fact]
    public void NaturalBodiesStayListed()
    {
        // Act
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var barycenter = new Barycenter(3);

        // Assert: listed by their observers, as on main (control)
        var earthObserver = (CelestialItem)earth.InitialOrbitalParameters.Observer;
        var barycenterObserver = (CelestialItem)barycenter.InitialOrbitalParameters.Observer;
        Assert.True(Lists(earthObserver, earth));
        Assert.True(Lists(barycenterObserver, barycenter));
        Assert.Equal(1, earthObserver.Satellites.Count);
        Assert.Equal(1, barycenterObserver.Satellites.Count);
    }

    [Fact]
    public void NonGenericEnumeration_ListsTheSameInstances()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var a = new Spacecraft(-1007, "A", 100.0, 1000.0, new Clock("A", 65536), Orbit(earth));

        // Act
        var listed = new List<object>();
        foreach (var item in (System.Collections.IEnumerable)earth.Satellites)
        {
            listed.Add(item);
        }

        // Assert
        Assert.Same(a, Assert.Single(listed));
    }

    [Fact]
    public void SpacecraftAboutABarycenter_NotListed()
    {
        // Arrange: the rule of main kept, a spacecraft is listed by a CelestialBody observer only
        var barycenter = new Barycenter(3, Epoch);

        // Act
        var spacecraft = new Spacecraft(-1006, "BARY", 100.0, 1000.0, new Clock("BARY", 65536), Orbit(barycenter));

        // Assert
        Assert.False(Lists(barycenter, spacecraft));
        Assert.Empty(barycenter.Satellites);
    }
}
