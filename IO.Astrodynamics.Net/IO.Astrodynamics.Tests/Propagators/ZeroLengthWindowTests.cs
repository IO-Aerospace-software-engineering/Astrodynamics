// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// A numerical propagation over a window of zero length throws <see cref="ArgumentException"/> naming <c>window</c> at the
/// start of the call, before anything changes (decision 7 of lot L4 of #668, decided by S. Guillet, 2026-11-22). Before,
/// it ran, re-armed the spacecraft and threw <see cref="InvalidOperationException"/> "Solution contains no segments".
/// </summary>
public class ZeroLengthWindowTests
{
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly Window Empty = new(Start, Start);

    public ZeroLengthWindowTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static int _nextId = -1793000;

    private static Spacecraft NewSpacecraft()
    {
        int id = System.Threading.Interlocked.Decrement(ref _nextId);
        var orbit = new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 6200.0, 4500.0),
            PlanetsAndMoons.EARTH_BODY, Start, Frames.Frame.ICRF);
        return new Spacecraft(id, "ZERO" + -id, 1000.0, 3000.0, new Clock("zero" + -id, 65536), orbit);
    }

    [Theory]
    [InlineData("Spacecraft.Propagate")]
    [InlineData("CentralBodyPropagator")]
    [InlineData("IIntegrator")]
    public void AZeroLengthWindow_Throws_NamingTheWindow(string path)
    {
        // Arrange
        var spacecraft = NewSpacecraft();
        Action propagate = path switch
        {
            "Spacecraft.Propagate" => () => spacecraft.Propagate(Empty),
            "CentralBodyPropagator" => () => _ = new CentralBodyPropagator(Empty, spacecraft),
            _ => () =>
            {
                var integrator = new RK78Integrator();
                integrator.AddForce(new GravitationalAcceleration(PlanetsAndMoons.EARTH_BODY));
                _ = new CentralBodyPropagator(Empty, spacecraft, integrator, TimeSpan.FromSeconds(60.0));
            }
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(propagate);

        // Assert
        Assert.Equal("window", exception.ParamName);
        Assert.False(spacecraft.IsPropagated);
    }

    /// <summary>
    /// <see cref="Scenario.Simulate"/> throws before it propagates its stars; a batch task reports the exception as its
    /// error, its fault isolated.
    /// </summary>
    [Fact]
    public void ScenarioAndBatch_ThrowAtTheStart()
    {
        // Arrange
        var scenario = new Scenario("zero", new IO.Astrodynamics.Mission.Mission("zero"), Empty);
        var star = new Star(3, "ZEROSTAR", 1e30, "spec", 2, 0.3792,
            IO.Astrodynamics.Coordinates.Equatorial.FromRightAscensionDeclination(1.0, 1.0, Start), 0.1, 0.1, 0, 0, 0, 0,
            Start);
        scenario.AddStar(star);
        scenario.AddSpacecraft(NewSpacecraft());

        // Act
        var exception = Assert.Throws<ArgumentException>(() => scenario.Simulate());
        var batch = BatchPropagator.Propagate(new[] { new PropagationTask(NewSpacecraft(), Empty) });

        // Assert
        Assert.Equal("window", exception.ParamName);
        Assert.False(star.IsPropagated);
        Assert.False(scenario.IsSimulated);
        Assert.Equal("window", Assert.IsType<ArgumentException>(Assert.Single(batch.Results).Error).ParamName);
    }
}
