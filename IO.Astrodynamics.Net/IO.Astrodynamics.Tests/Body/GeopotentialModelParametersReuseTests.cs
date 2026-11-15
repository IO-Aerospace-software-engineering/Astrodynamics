// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// One <see cref="GeopotentialModelParameters"/> builds any number of bodies, on any thread, each with the field of a
/// fresh instance (#674, defect 14). The fresh field is checked independently against GMAT R2026a
/// (<c>GmatConformancePropagatorTests</c>) and the constants of the model (<c>GeopotentialConstantsTests</c>).
/// </summary>
public class GeopotentialModelParametersReuseTests
{
    public GeopotentialModelParametersReuseTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0);

    private static string ModelPath => Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree");

    private static GeopotentialModelParameters Parameters(string source, ushort degree, bool constants,
        GeopotentialCentralTerm centralTerm)
    {
        if (constants)
        {
            return source == "path"
                ? new GeopotentialModelParameters(ModelPath, degree, 3.986004418e14, 6378137.0) { CentralTerm = centralTerm }
                : new GeopotentialModelParameters(File.OpenRead(ModelPath), degree, 3.986004418e14, 6378137.0)
                    { CentralTerm = centralTerm };
        }

        return source == "path"
            ? new GeopotentialModelParameters(ModelPath, degree) { CentralTerm = centralTerm }
            : new GeopotentialModelParameters(File.OpenRead(ModelPath), degree) { CentralTerm = centralTerm };
    }

    private static CelestialBody Earth(GeopotentialModelParameters parameters) =>
        new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, parameters);

    public static TheoryData<string, ushort, bool, GeopotentialCentralTerm> Configurations() => new()
    {
        { "path", 10, false, GeopotentialCentralTerm.BodyGravitationalParameter },
        { "stream", 70, false, GeopotentialCentralTerm.BodyGravitationalParameter },
        { "path", 70, true, GeopotentialCentralTerm.ModelGravitationalParameter },
        { "stream", 10, true, GeopotentialCentralTerm.BodyGravitationalParameter },
        { "path", 10, false, GeopotentialCentralTerm.ModelGravitationalParameter }
    };

    // LEO to GEO, the poles on the polar axis, a point near the axis and the equator
    private static readonly Vector3[] Positions =
    {
        new(6700000.0, 0.0, 0.0), new(0.0, 6700000.0, 0.0), new(-4737564.0, 4737564.0, 0.0),
        new(3868247.0, 3868247.0, 3868247.0), new(-3868247.0, 3868247.0, -3868247.0), new(0.0, 0.0, 6700000.0),
        new(0.0, 0.0, -6700000.0), new(1e-3, 0.0, 6700000.0), new(0.0, 1.0, -7000000.0), new(7000000.0, 0.0, 1000.0),
        new(5000000.0, -4000000.0, 2500000.0), new(-6000000.0, -2000000.0, -2500000.0),
        new(12000000.0, 5000000.0, 3000000.0), new(0.0, 0.0, 20000000.0), new(26560000.0, 0.0, 0.0),
        new(-15000000.0, 15000000.0, 10000000.0), new(42164000.0, 0.0, 0.0), new(0.0, -42164000.0, 0.0),
        new(29814000.0, 29814000.0, 0.0), new(0.0, 0.0, -42164000.0)
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public void GeopotentialModelParameters_BuildsSeveralBodies_EachWithTheSameField(string source, ushort degree,
        bool constants, GeopotentialCentralTerm centralTerm)
    {
        // Arrange: three bodies from one instance, and one from a fresh instance of the same file
        var shared = Parameters(source, degree, constants, centralTerm);
        var bodies = Enumerable.Range(0, 3).Select(_ => Earth(shared)).ToArray();
        var fresh = Earth(Parameters(source, degree, constants, centralTerm));

        // Act, assert: the acceleration and its partials bit for bit
        foreach (var position in Positions)
        {
            var velocity = new Vector3(-1000.0, 3000.0, 2000.0);
            var reference = new StateVector(position, velocity, fresh, Epoch, Frames.Frame.ICRF);
            var expected = fresh.Field.ComputeGravitationalAcceleration(reference);
            var expectedPartials = new double[9];
            Assert.True(fresh.Field.TryAccumulatePositionPartials(reference, expectedPartials));
            foreach (var body in bodies)
            {
                var state = new StateVector(position, velocity, body, Epoch, Frames.Frame.ICRF);
                var actual = body.Field.ComputeGravitationalAcceleration(state);
                Assert.True(expected.X == actual.X && expected.Y == actual.Y && expected.Z == actual.Z,
                    $"{position}: {actual} != {expected}");
                var partials = new double[9];
                Assert.True(body.Field.TryAccumulatePositionPartials(state, partials));
                Assert.Equal(expectedPartials, partials);
            }
        }
    }

    [Fact]
    public void GeopotentialModelParameters_SharedAcrossThreads()
    {
        // Arrange: eight tasks build their body from one instance, started together
        var shared = new GeopotentialModelParameters(ModelPath, 10);
        var window = new TimeSystem.Window(Epoch, Epoch.AddHours(1.0));
        const int count = 8;
        using var start = new Barrier(count);

        Spacecraft NewSpacecraft(int id, CelestialBody earth) => new(id, $"GPR{-id}", 100.0, 10000.0,
            new Clock($"gprclk{-id}", 65536),
            new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0), earth, Epoch,
                Frames.Frame.ICRF));

        // Act
        var tasks = Enumerable.Range(0, count).Select(i => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            var earth = Earth(shared);
            var spacecraft = NewSpacecraft(-7301 - i, earth);
            spacecraft.Propagate(window, new CelestialItem[] { earth }, new RK78Integrator(1e-10, 1e-10), false, false,
                TimeSpan.FromSeconds(60.0));
            return spacecraft.PropagatedStates[^1];
        }, TaskCreationOptions.LongRunning)).ToArray();
        Task.WaitAll(tasks);

        var freshEarth = Earth(new GeopotentialModelParameters(ModelPath, 10));
        var reference = NewSpacecraft(-7310, freshEarth);
        reference.Propagate(window, new CelestialItem[] { freshEarth }, new RK78Integrator(1e-10, 1e-10), false, false,
            TimeSpan.FromSeconds(60.0));
        var expected = reference.PropagatedStates[^1];

        // Assert
        foreach (var task in tasks)
        {
            var actual = task.Result;
            Assert.True(expected.Position.X == actual.Position.X && expected.Position.Y == actual.Position.Y &&
                        expected.Position.Z == actual.Position.Z && expected.Velocity.X == actual.Velocity.X &&
                        expected.Velocity.Y == actual.Velocity.Y && expected.Velocity.Z == actual.Velocity.Z,
                $"{actual.Position} != {expected.Position}");
        }
    }
}
