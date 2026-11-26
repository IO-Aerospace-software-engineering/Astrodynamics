// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// A propagation over a window of zero length throws <see cref="ArgumentException"/> naming <c>window</c> at the start of
/// the call, before anything changes, on every path (decision 7 of lot L4 of #668, decided by S. Guillet, 2026-11-22;
/// <see cref="TlePropagator"/> by decision 15, the same day). Before, a numerical propagation ran, re-armed the spacecraft
/// and threw <see cref="InvalidOperationException"/> "Solution contains no segments", and <see cref="TlePropagator"/>
/// returned one sample at the start.
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
            PlanetsAndMoons.EarthBody, Start, Frames.Frame.ICRF);
        return new Spacecraft(id, "ZERO" + -id, 1000.0, orbit, 3000.0, new Clock("zero" + -id, 65536));
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
                integrator.AddForce(new GravitationalAcceleration(PlanetsAndMoons.EarthBody));
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
    /// <see cref="TlePropagator"/> throws in its constructor, in TDB and in UTC alike, and adds no state to the spacecraft
    /// (decision 15).
    /// </summary>
    [Theory]
    [InlineData(TimeScale.TDB)]
    [InlineData(TimeScale.UTC)]
    public void TLEPropagator_AZeroLengthWindow_Throws_NamingTheWindow(TimeScale scale)
    {
        // Arrange
        var tle = new IO.Astrodynamics.OrbitalParameters.TLE.Tle("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        int id = System.Threading.Interlocked.Decrement(ref _nextId);
        var spacecraft = new Spacecraft(id, "ZERO" + -id, 100.0, tle, 10000.0, new Clock("zero" + -id, 256));
        var epoch = scale == TimeScale.UTC ? tle.Epoch.ToUtc() : tle.Epoch.ToTdb();
        var empty = new Window(epoch, epoch);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new TlePropagator(empty, spacecraft, TimeSpan.FromSeconds(60.0)));

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

    /// <summary>
    /// <see cref="Scenario.Simulate"/> checks the window whatever the scenario holds (decision 18 of lot L4 of #668,
    /// decided by S. Guillet, 2026-11-22): a scenario of stars alone over a zero-length window throws before anything
    /// runs, its star not propagated and <see cref="Scenario.IsSimulated"/> unchanged. Before, the stars were propagated
    /// and the scenario reported simulated. <see cref="Star.Propagate"/> throws itself since lot L6 (<c>StarTests</c>).
    /// </summary>
    [Fact]
    public void Scenario_OfStarsAlone_AZeroLengthWindow_Throws_NamingTheWindow()
    {
        // Arrange
        var scenario = new Scenario("zerostars", new IO.Astrodynamics.Mission.Mission("zerostars"), Empty);
        var star = new Star(4, "ZEROSTARALONE", 1e30, "spec", 2, 0.3792,
            IO.Astrodynamics.Coordinates.Equatorial.FromRightAscensionDeclination(1.0, 1.0, Start), 0.1, 0.1, 0, 0, 0, 0,
            Start);
        scenario.AddStar(star);
        bool simulatedBefore = scenario.IsSimulated;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => scenario.Simulate());

        // Assert
        Assert.Equal("window", exception.ParamName);
        Assert.False(star.IsPropagated);
        Assert.Equal(simulatedBefore, scenario.IsSimulated);
    }

    /// <summary>
    /// <see cref="MonteCarloPropagator.Propagate(MonteCarloConfiguration, System.IProgress{BatchProgress}, System.Threading.CancellationToken)"/>
    /// throws at the start of the call, before anything is sampled, as every propagation path (decisions 7 and 15); before,
    /// each run failed inside the batch and the campaign returned no statistics and no reason.
    /// </summary>
    [Fact]
    public void MonteCarlo_AZeroLengthWindow_Throws_NamingTheWindow()
    {
        // Arrange
        int id = System.Threading.Interlocked.Decrement(ref _nextId);
        var orbit = new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 6200.0, 4500.0),
            PlanetsAndMoons.EarthBody, Start, Frames.Frame.ICRF,
            IO.Astrodynamics.Math.Matrix.Diagonal(100.0, 100.0, 100.0, 1e-4, 1e-4, 1e-4));
        var template = new Spacecraft(id, "ZERO" + -id, 1000.0, orbit, 3000.0, new Clock("zero" + -id, 65536));
        var config = new MonteCarloConfiguration
        {
            RunCount = 3, Seed = 1, TemplateSpacecraft = template, Window = Empty, BaseNaifId = id - 100000
        };
        var reports = new System.Collections.Generic.List<BatchProgress>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MonteCarloPropagator.Propagate(config, null));
        var withProgress = Assert.Throws<ArgumentException>(() =>
            MonteCarloPropagator.Propagate(config, new System.Progress<BatchProgress>(reports.Add)));

        // Assert
        Assert.Equal("window", exception.ParamName);
        Assert.Equal("window", withProgress.ParamName);
        Assert.Empty(reports);
    }
}
