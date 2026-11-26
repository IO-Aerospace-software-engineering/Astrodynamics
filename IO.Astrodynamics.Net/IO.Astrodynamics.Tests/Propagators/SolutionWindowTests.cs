// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The reader of a solution, lot L5a of #668 (R09, decision 53, decided by S. Guillet, 2026-11-23): the public get-only
/// <see cref="PropagationSolution.Observer"/> and <see cref="PropagationSolution.Frame"/> of its states, those of the
/// samples on a <see cref="TlePropagator"/> solution (the TLE's Earth and TEME), so that
/// <see cref="PropagationSolution.GetStateVector"/> reads it (it threw before); and the public
/// <see cref="PropagationSolution.Window"/>, [first segment base, end] in TDB labels, the span the reads accept (the
/// check of <c>Covers</c>, unchanged), a read outside it throwing <see cref="ArgumentOutOfRangeException"/> naming it.
/// </summary>
public class SolutionWindowTests
{
    public SolutionWindowTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);

    private static int _nextId = -785000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static CelestialBody Earth => PlanetsAndMoons.EARTH_BODY;

    private static Tle Iss() => new("ISS",
        "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

    private static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
    {
        int id = NextId();
        return new Spacecraft(id, $"SW{-id}", 100.0, orbit, 10000.0, new Clock($"swclk{-id}", 65536), 10.0, 2.2);
    }

    [Fact]
    public void ObserverAndFrame_ArePublicAndGetOnly()
    {
        foreach (var name in new[] { nameof(PropagationSolution.Observer), nameof(PropagationSolution.Frame) })
        {
            var property = typeof(PropagationSolution).GetProperty(name);
            Assert.NotNull(property);
            Assert.True(property.GetMethod!.IsPublic, name);
            Assert.False(property.SetMethod?.IsPublic ?? false, name);
        }

        var window = typeof(PropagationSolution).GetProperty(nameof(PropagationSolution.Window));
        Assert.NotNull(window);
        Assert.Equal(typeof(Window), window.PropertyType);
        Assert.Null(window.SetMethod);
    }

    [Fact]
    public void ATLEPropagatorSolution_HasTheObserverAndFrameOfItsSamples_AndGetStateVectorReadsSgp4()
    {
        // Arrange
        var tle = Iss();
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        var solution = new TlePropagator(window, NewSpacecraft(tle), Step).Propagate();

        // Assert: the TLE's Earth and TEME, those of every sample
        Assert.Same(solution.StateVectors[0].Observer, solution.Observer);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, ((CelestialItem)solution.Observer).NaifId);
        Assert.Equal(Frames.Frame.TEME, solution.Frame);

        // At each sample, SGP4 itself, bit for bit, in TEME about the Earth
        foreach (var sample in solution.StateVectors)
        {
            var read = solution.GetStateVector(sample.Epoch);
            var sgp4 = tle.ToStateVector(sample.Epoch);
            Assert.Equal(sgp4.Position, read.Position);
            Assert.Equal(sgp4.Velocity, read.Velocity);
            Assert.Equal(Frames.Frame.TEME, read.Frame);
            Assert.Same(solution.Observer, read.Observer);
            LabelAssert.Equal(sample.Epoch, read.Epoch);
        }
    }

    [Theory]
    [InlineData(TimeScale.TDB)]
    [InlineData(TimeScale.UTC)]
    public void TheWindowOfATLEPropagatorSolution_IsTheWindowInTdb(TimeScale scale)
    {
        // Arrange
        var tle = Iss();
        var start = scale == TimeScale.UTC ? tle.Epoch.ToUTC() : tle.Epoch.ToTDB();
        var window = new Window(start, start.AddHours(1.0));

        // Act
        var solution = new TlePropagator(window, NewSpacecraft(tle), Step).Propagate();

        // Assert: the TDB labels of the window, exactly
        LabelAssert.Equal(window.StartDate.ToTDB(), solution.Window.StartDate);
        LabelAssert.Equal(window.EndDate.ToTDB(), solution.Window.EndDate);
        Assert.Equal(TimeScale.TDB, solution.Window.StartDate.Scale);
        Assert.Equal(TimeScale.TDB, solution.Window.EndDate.Scale);
    }

    private static PropagationSolution Numerical(string kind, string orbit, in Window window)
    {
        var options = new PropagationOptions
        {
            Integrator = kind == "VV"
                ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }
                : new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 },
            OutputStep = Step,
            ThirdBodies = orbit == "SSB" ? [Stars.SUN_BODY, Earth] : [Earth],
            CovariancePropagation = CovariancePropagationOptions.None
        };
        var epoch = window.StartDate.ToTDB();
        Spacecraft spacecraft;
        switch (orbit)
        {
            case "SSB":
                var heliocentric = new StateVector(new Vector3(1.5e11, 0.0, 0.0), new Vector3(0.0, 29780.0, 1000.0),
                    Stars.SUN_BODY, epoch, Frames.Frame.ICRF);
                spacecraft = NewSpacecraft(heliocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None)
                    .ToStateVector());
                break;
            case "ECLIPJ2000":
                spacecraft = NewSpacecraft(new StateVector(new Vector3(6700000.0, 0.0, 0.0),
                    new Vector3(0.0, 8000.0, 0.0), Earth, epoch, Frames.Frame.ECLIPTIC_J2000));
                break;
            default:
                spacecraft = NewSpacecraft(new StateVector(new Vector3(6700000.0, 0.0, 0.0),
                    new Vector3(0.0, 8000.0, 0.0), Earth, epoch, Frames.Frame.ICRF));
                var tank = new FuelTank($"ft{-spacecraft.NaifId}", 1000.0, 1000.0, "model", "sn");
                spacecraft.AddFuelTank(tank);
                var engine = new Engine($"eng{-spacecraft.NaifId}", 300.0, 10.0, tank, "model", "sn");
                spacecraft.AddEngine(engine);
                spacecraft.SetManeuvers(new ApogeeHeightManeuver(9000000.0,
                    engine,
                    epoch.AddSeconds(600.0),
                    TimeSpan.Zero,
                    Earth));
                break;
        }

        return spacecraft.Propagate(window, options);
    }

    [Theory]
    [InlineData("RK", "Impulse", TimeScale.TDB)]
    [InlineData("VV", "Impulse", TimeScale.TDB)]
    [InlineData("RK", "Impulse", TimeScale.UTC)]
    [InlineData("VV", "Impulse", TimeScale.UTC)]
    [InlineData("RK", "ECLIPJ2000", TimeScale.UTC)]
    [InlineData("RK", "SSB", TimeScale.TDB)]
    public void TheWindowOfANumericalSolution_IsTheWindowInTdb_ToOneTick(string kind, string orbit, TimeScale scale)
    {
        // Arrange: a window the output step does not divide, so that the propagation ends at the window end
        var start = new TimeSystem.Time(2021, 1, 1, 12, 0, 0, scale);
        var window = new Window(start, start.AddSeconds(orbit == "SSB" ? 86430.0 : 7230.0));

        // Act
        var solution = Numerical(kind, orbit, window);

        // Assert: the first segment base and the end, TDB labels, the window to one tick
        LabelAssert.Equal(solution.Segments[0].BaseEpoch, solution.Window.StartDate);
        LabelAssert.Equal(window.StartDate.ToTDB(), solution.Window.StartDate);
        Assert.Equal(TimeScale.TDB, solution.Window.EndDate.Scale);
        Assert.InRange(System.Math.Abs((solution.Window.EndDate - window.EndDate.ToTDB()).Ticks), 0L, 1L);
        if (orbit == "Impulse")
        {
            Assert.True(solution.Segments.Count > 1);
        }

        // The reads accept both ends as the caller gave them
        solution.GetStateVector(window.StartDate);
        solution.GetStateVector(window.EndDate);
        solution.GetStateVector(solution.Window.StartDate);
        solution.GetStateVector(solution.Window.EndDate);
    }

    [Theory]
    [InlineData("Numerical")]
    [InlineData("TLE")]
    public void AReadOutsideTheWindow_ThrowsArgumentOutOfRangeException_NamingTheWindow(string path)
    {
        // Arrange
        PropagationSolution solution;
        if (path == "TLE")
        {
            var tle = Iss();
            solution = new TlePropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), NewSpacecraft(tle), Step)
                .Propagate();
        }
        else
        {
            var start = new TimeSystem.Time(2021, 1, 1, 12, 0, 0, TimeScale.TDB);
            solution = Numerical("RK", "ECLIPJ2000", new Window(start, start.AddHours(1.0)));
        }

        var w = solution.Window;

        // Act and assert: after the end and before the start
        foreach (var epoch in new[] { w.EndDate.AddSeconds(1.0), w.StartDate.AddSeconds(-1.0) })
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => solution.GetStateVector(epoch));
            Assert.Equal("epoch", exception.ParamName);
            Assert.Contains(w.StartDate.ToString(), exception.Message);
            Assert.Contains(w.EndDate.ToString(), exception.Message);
        }
    }
}
