// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The start of a <see cref="CentralBodyPropagator"/>: a state vector given at the start of the window is taken as it is,
/// not through the Keplerian elements (#398).
/// </summary>
public class PropagatorStartStateTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0, frame: TimeFrame.UTCFrame);

    private readonly CelestialBody _earth;

    public PropagatorStartStateTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
    }

    public static TheoryData<string, bool> Starts => new()
    {
        { "RK7(8), window in UTC", false }, { "RK7(8), window in TDB", true },
        { "Velocity-Verlet, window in UTC", false }, { "Velocity-Verlet, window in TDB", true }
    };

    [Theory]
    [MemberData(nameof(Starts))]
    public void StateVectorAtTheStartOfTheWindow_IsTheFirstStateBitForBit(string name, bool tdbWindow)
    {
        // Arrange: the R4 GEO state with z moved by 1 km, which the Keplerian round trip moved by 0.38 m before #398,
        // and still returns only to its rounding
        var orbit = new KeplerianElements(42164137.0, 0.0002, 0.05 * Deg, 0.0, 30.0 * Deg, 0.0, _earth, Epoch,
            Frames.Frame.ICRF).ToStateVector();
        var start = new StateVector(orbit.Position + new Vector3(0.0, 0.0, 1000.0), orbit.Velocity, _earth, Epoch,
            Frames.Frame.ICRF);
        var spacecraft = ReferenceCase.NewSpacecraft(-1398, start);
        var startDate = tdbWindow ? Epoch.ToTDB() : Epoch;
        var window = new Window(startDate, startDate.AddSeconds(600.0));
        var bodies = new CelestialItem[] { _earth };

        // Act
        using var propagator = name.StartsWith("RK")
            ? new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-12, 1e-12), bodies, false, false,
                TimeSpan.FromSeconds(60.0))
            : new CentralBodyPropagator(window, spacecraft, bodies, false, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        // Assert
        var first = solution.Segments[0].Steps[0];
        Assert.Equal(start.Position, first.StartPosition);
        Assert.Equal(start.Velocity, first.StartVelocity);
    }
}
