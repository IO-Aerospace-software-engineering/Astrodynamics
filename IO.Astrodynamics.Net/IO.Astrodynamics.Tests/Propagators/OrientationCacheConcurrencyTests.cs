// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The orientation cache of the central body frame is active only on the thread that propagates or evaluates a
/// propagation: propagations and RK7(8) evaluations that share a central body without a geopotential give the same
/// bits concurrently as sequentially, and leave no cache on the shared frame (H3 review of #366, finding 1).
/// </summary>
/// <remarks>
/// Each test builds its own Earth, so that the test classes xUnit runs in parallel do not share it.
/// </remarks>
public class OrientationCacheConcurrencyTests
{
    private const int Queries = 4000;
    private static readonly TimeSystem.Time EpochA = new(2021, 3, 20, 12, 0, 0);
    private static readonly TimeSystem.Time EpochB = EpochA.AddDays(10.0);
    private static readonly TimeSpan Duration = TimeSpan.FromHours(6.0);

    public OrientationCacheConcurrencyTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>Point-mass Earth with NRLMSISE-00: no geopotential, so it may be shared between threads.</summary>
    private static CelestialBody PointMassEarth() =>
        new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, EpochA, null, new Nrlmsise00Model(SpaceWeather.Nominal));

    private static Spacecraft LeoSpacecraft(CelestialBody earth, TimeSystem.Time epoch, int naifId)
    {
        const double deg = IO.Astrodynamics.Constants.Deg2Rad;
        var orbit = new KeplerianElements(6778137.0, 0.0005, 51.6 * deg, 20.0 * deg, 15.0 * deg, 0.0, earth, epoch,
            Frames.Frame.ICRF);
        return new Spacecraft(naifId, $"CACHE{-naifId}", 100.0, 1000.0, new Clock($"cache{-naifId}", 65536), orbit,
            10.0, 2.2, null, 1.5);
    }

    private static PropagationSolution PropagateLeo(CelestialBody earth, TimeSystem.Time epoch, int naifId,
        VariationalOptions options = null)
    {
        return LeoSpacecraft(earth, epoch, naifId).Propagate(new TimeSystem.Window(epoch, epoch + Duration),
            new CelestialItem[] { earth }, new RK78Integrator(), true, false, TimeSpan.FromSeconds(60.0), options);
    }

    // Epochs inside the steps, where InterpolateAt computes a shortened step with the forces
    private static TimeSystem.Time QueryEpoch(TimeSystem.Time start, int index) =>
        start.AddSeconds(Duration.TotalSeconds * (index + 0.37) / Queries);

    private static (Vector3 Position, Vector3 Velocity)[] Interpolate(PropagationSolution solution,
        TimeSystem.Time start)
    {
        var states = new (Vector3, Vector3)[Queries];
        for (int i = 0; i < Queries; i++)
        {
            states[i] = solution.InterpolateAt(QueryEpoch(start, i));
        }

        return states;
    }

    private static double[][] EvaluateVariational(PropagationSolution solution, TimeSystem.Time start)
    {
        var values = new double[Queries / 10][];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new double[42];
            solution.EvaluateVariational(QueryEpoch(start, 10 * i), values[i], Array.Empty<double>());
        }

        return values;
    }

    // Runs both functions at the same time, each on its own thread
    private static (T A, T B) RunConcurrently<T>(Func<T> a, Func<T> b)
    {
        using var barrier = new Barrier(2);
        T resultA = default, resultB = default;
        Exception errorA = null, errorB = null;
        var threadA = new Thread(() =>
        {
            barrier.SignalAndWait();
            try { resultA = a(); }
            catch (Exception e) { errorA = e; }
        });
        var threadB = new Thread(() =>
        {
            barrier.SignalAndWait();
            try { resultB = b(); }
            catch (Exception e) { errorB = e; }
        });
        threadA.Start();
        threadB.Start();
        threadA.Join();
        threadB.Join();
        Assert.Null(errorA);
        Assert.Null(errorB);
        return (resultA, resultB);
    }

    private static void AssertSameBits(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.X), BitConverter.DoubleToInt64Bits(actual.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Y), BitConverter.DoubleToInt64Bits(actual.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Z), BitConverter.DoubleToInt64Bits(actual.Z));
    }

    private static void AssertSameStates((Vector3 Position, Vector3 Velocity)[] expected,
        (Vector3 Position, Vector3 Velocity)[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            AssertSameBits(expected[i].Position, actual[i].Position);
            AssertSameBits(expected[i].Velocity, actual[i].Velocity);
        }
    }

    private static void AssertSameOutputs(PropagationSolution expected, PropagationSolution actual)
    {
        Assert.Equal(expected.StateVectors.Count, actual.StateVectors.Count);
        for (int i = 0; i < expected.StateVectors.Count; i++)
        {
            AssertSameBits(expected.StateVectors[i].Position, actual.StateVectors[i].Position);
            AssertSameBits(expected.StateVectors[i].Velocity, actual.StateVectors[i].Velocity);
        }
    }

    // The Earth frame of the propagations gives the orientation of an Earth that no propagation used, a month later:
    // no cache was left on it, on this thread or any other
    private static void AssertNoCacheLeftOn(CelestialBody earth)
    {
        Assert.Null(earth.Frame.OrientationCache);
        var later = EpochA.AddDays(30.0);
        var expected = PointMassEarth().Frame.GetStateOrientationToICRF(later);
        var actual = RunConcurrently(() => earth.Frame.GetStateOrientationToICRF(later),
            () => earth.Frame.GetStateOrientationToICRF(later));
        foreach (var orientation in new[] { actual.A, actual.B })
        {
            Assert.Equal(expected.Rotation, orientation.Rotation);
            AssertSameBits(expected.AngularVelocity, orientation.AngularVelocity);
        }
    }

    [Fact]
    public void InterpolateAt_OnTwoSolutionsSharingACentralBody_FromTwoThreads_MatchesTheSequentialResults()
    {
        // Arrange: the reproduction of the review, two RK7(8) propagations of 6 h, 10 days apart, with drag
        var earth = PointMassEarth();
        var solutionA = PropagateLeo(earth, EpochA, -1971);
        var solutionB = PropagateLeo(earth, EpochB, -1972);
        var sequentialA = Interpolate(solutionA, EpochA);
        var sequentialB = Interpolate(solutionB, EpochB);

        // Act
        var (concurrentA, concurrentB) = RunConcurrently(() => Interpolate(solutionA, EpochA),
            () => Interpolate(solutionB, EpochB));

        // Assert
        AssertSameStates(sequentialA, concurrentA);
        AssertSameStates(sequentialB, concurrentB);
        AssertNoCacheLeftOn(earth);
    }

    [Fact]
    public void EvaluateVariational_OnTwoSolutionsSharingACentralBody_FromTwoThreads_MatchesTheSequentialResults()
    {
        // Arrange
        var earth = PointMassEarth();
        var options = new VariationalOptions(ForceParameters.DragCoefficient);
        var solutionA = PropagateLeo(earth, EpochA, -1973, options);
        var solutionB = PropagateLeo(earth, EpochB, -1974, options);
        var sequentialA = EvaluateVariational(solutionA, EpochA);
        var sequentialB = EvaluateVariational(solutionB, EpochB);

        // Act
        var (concurrentA, concurrentB) = RunConcurrently(() => EvaluateVariational(solutionA, EpochA),
            () => EvaluateVariational(solutionB, EpochB));

        // Assert
        for (int i = 0; i < sequentialA.Length; i++)
        {
            Assert.Equal(sequentialA[i], concurrentA[i]);
            Assert.Equal(sequentialB[i], concurrentB[i]);
        }

        AssertNoCacheLeftOn(earth);
    }

    [Fact]
    public void TwoPropagationsSharingACentralBody_OnTwoThreads_MatchTheSequentialPropagations()
    {
        // Arrange
        var earth = PointMassEarth();
        var sequentialA = PropagateLeo(earth, EpochA, -1975);
        var sequentialB = PropagateLeo(earth, EpochB, -1976);

        // Act
        var (concurrentA, concurrentB) = RunConcurrently(() => PropagateLeo(earth, EpochA, -1977),
            () => PropagateLeo(earth, EpochB, -1978));

        // Assert
        AssertSameOutputs(sequentialA, concurrentA);
        AssertSameOutputs(sequentialB, concurrentB);
        AssertNoCacheLeftOn(earth);
    }

    [Fact]
    public void TwoPropagatorsBuiltBeforeEitherPropagates_EachUseTheirOwnOrientationCache()
    {
        // Arrange: each propagation alone
        var earth = PointMassEarth();
        var bodies = new CelestialItem[] { earth };
        var windowA = new TimeSystem.Window(EpochA, EpochA + Duration);
        var windowB = new TimeSystem.Window(EpochB, EpochB + Duration);
        var aloneA = new CentralBodyPropagator(windowA, LeoSpacecraft(earth, EpochA, -1979), bodies, true, false,
            TimeSpan.FromSeconds(10.0)).Propagate();
        var aloneB = new CentralBodyPropagator(windowB, LeoSpacecraft(earth, EpochB, -1980), bodies, true, false,
            TimeSpan.FromSeconds(10.0)).Propagate();

        // Act: both built, then both propagated (Velocity-Verlet, the default)
        var propagatorA = new CentralBodyPropagator(windowA, LeoSpacecraft(earth, EpochA, -1981), bodies, true, false,
            TimeSpan.FromSeconds(10.0));
        var propagatorB = new CentralBodyPropagator(windowB, LeoSpacecraft(earth, EpochB, -1982), bodies, true, false,
            TimeSpan.FromSeconds(10.0));
        var builtFirstA = propagatorA.Propagate();
        var builtFirstB = propagatorB.Propagate();

        // Assert
        AssertSameOutputs(aloneA, builtFirstA);
        AssertSameOutputs(aloneB, builtFirstB);
        AssertNoCacheLeftOn(earth);
    }

    [Fact]
    public void APropagatorBuiltButNeverRun_DoesNotChangeTheNextPropagationOfTheBody()
    {
        // Arrange: the case found in #369, with EGM2008 10×10; the propagator that never runs ends 10 days earlier,
        // so a cache left by it would freeze the Earth rotation at the edge of its window
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, EpochA,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var bodies = new CelestialItem[] { earth };
        var window = new TimeSystem.Window(EpochB, EpochB.AddHours(3.0));
        var alone = LeoSpacecraft(earth, EpochB, -1983).Propagate(window, bodies, new RK78Integrator(), false, false,
            TimeSpan.FromSeconds(60.0));

        // Act
        using (new CentralBodyPropagator(new TimeSystem.Window(EpochA, EpochA.AddHours(1.0)),
                   LeoSpacecraft(earth, EpochA, -1984), new RK78Integrator(), bodies, false, false,
                   TimeSpan.FromSeconds(60.0)))
        {
        }

        var afterUnused = LeoSpacecraft(earth, EpochB, -1985).Propagate(window, bodies, new RK78Integrator(), false,
            false, TimeSpan.FromSeconds(60.0));

        // Assert
        AssertSameOutputs(alone, afterUnused);
        Assert.Null(earth.Frame.OrientationCache);
    }
}
