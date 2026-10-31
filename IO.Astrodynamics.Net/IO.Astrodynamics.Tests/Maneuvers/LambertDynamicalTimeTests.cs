// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Tests.Time;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers.Lambert;

/// <summary>
/// The time of flight of the Lambert solver is the dynamical time of the centre of motion between the two epochs, TT
/// about the Earth and TDB about any other body, whatever their tags, the leap seconds between two UTC epochs
/// included (#508). Before, two UTC epochs across the leap second of 2016-12-31 gave a time of flight 1 s short.
/// </summary>
public class LambertDynamicalTimeTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    // 2401 s of TT across the leap second, the calendars 2400 s apart
    private static readonly TimeSystem.Time DepartureUtc = new(2016, 12, 31, 23, 40, 0, frame: TimeFrame.UTCFrame);
    private static readonly TimeSystem.Time ArrivalUtc = new(2017, 1, 1, 0, 20, 0, frame: TimeFrame.UTCFrame);

    private readonly ITestOutputHelper _output;

    public LambertDynamicalTimeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<string, string, string> TagPairs
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var body in new[] { "Earth", "Moon" })
            foreach (var from in new[] { "UTC", "TAI", "TT", "TDB" })
            foreach (var to in new[] { "UTC", "TAI", "TT", "TDB" })
                data.Add(body, from, to);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TagPairs))]
    public void TheTransfer_IsTheOrbitThroughBothPoints_InTheDynamicalTimeOfTheCentre(string body, string fromTag,
        string toTag)
    {
        // Arrange: a point of an orbit and the point SPICE prop2b reaches after the dynamical time between the tags
        var centre = new CelestialBody(body == "Earth" ? PlanetsAndMoons.EARTH : PlanetsAndMoons.MOON);
        var departure = Tagged(DepartureUtc, fromTag);
        var arrival = Tagged(ArrivalUtc, toTag);
        var start = new KeplerianElements(body == "Earth" ? 6778137.0 : 1837400.0, 0.001, 51.6 * Deg, 30.0 * Deg,
            40.0 * Deg, 10.0 * Deg, centre, departure, Frames.Frame.ICRF).ToStateVector();
        double timeOfFlight = body == "Earth"
            ? DynamicalTimeReference.TtSecondsBetween(departure, arrival)
            : DynamicalTimeReference.TdbSecondsBetween(departure, arrival);
        var end = TwoBody(start, timeOfFlight);
        var target = new StateVector(end.Position, end.Velocity, centre, arrival, Frames.Frame.ICRF);

        // Act
        var solution = new LambertSolver().Solve(false, start, target, centre, 0).GetZeroRevolutionSolution();

        // Assert: the velocity of the orbit at departure, to the elapsed-time bound of the pair as a velocity, twice
        // |v| / tof per second of time of flight (a time of flight 1 s off, the leap second, moves it by about 3 m/s
        // in LEO; the TDB seconds for TT seconds between two TDB epochs about the Earth by 2.3e-6 m/s)
        double error = (solution.V1 - start.Velocity).Magnitude();
        double bound = 2.0 * start.Velocity.Magnitude() / timeOfFlight * ElapsedBound(fromTag, toTag);
        _output.WriteLine($"MEASURE {body} {fromTag} -> {toTag}: |v1 - v| {error:E3} m/s (bound {bound:E2})");
        Assert.True(error <= bound, $"{error} m/s from the velocity of the orbit.");
    }

    // The framework's time of flight against the reference's: when both tags are TDB, or neither is, the change of the
    // two series over the 40 min, below 1e-9 s, plus two ticks of the TDB conversions of the epochs about the Moon; a
    // TDB tag and another, the departure of the two series at one end, 1.2423 µs at most (DynamicalTimeReference),
    // plus a tick
    private static double ElapsedBound(string fromTag, string toTag) =>
        (fromTag == "TDB") == (toTag == "TDB") ? 2.1e-7 : 1.2423e-6 + 1e-7;

    private static TimeSystem.Time Tagged(TimeSystem.Time utc, string tag) => tag switch
    {
        "UTC" => utc,
        "TAI" => utc.ToTAI(),
        "TT" => utc.ToTDT(),
        "TDB" => utc.ToTDB(),
        _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, null)
    };

    // prop2b over the whole ticks of the time of flight, then the sub-tick remainder along the velocity
    private static StateVector TwoBody(StateVector initial, double elapsed)
    {
        long ticks = (long)System.Math.Round(elapsed * TimeSpan.TicksPerSecond);
        double remainder = elapsed - ticks / (double)TimeSpan.TicksPerSecond;
        var state = SpiceAPI.Instance.Propagate2Bodies(initial, initial.Observer.GM, TimeSpan.FromTicks(ticks));
        return new StateVector(state.Position + state.Velocity * remainder, state.Velocity, state.Observer,
            state.Epoch, state.Frame);
    }
}
