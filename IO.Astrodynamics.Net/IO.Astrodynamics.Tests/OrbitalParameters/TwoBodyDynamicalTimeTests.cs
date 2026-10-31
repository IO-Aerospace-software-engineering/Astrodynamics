// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Tests.Time;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Two-body motion counts the dynamical time of the central body, as the numerical propagation does (#501, #508): the
/// TT seconds between the two epochs about the Earth, their TDB seconds about any other body, whatever their tags, the
/// leap seconds between two UTC epochs included, and none at the same instant. Before, <c>AtEpoch</c> counted TDB
/// seconds between two TDB epochs, UTC calendar seconds between two UTC epochs and TT seconds otherwise (0.22 m a day
/// in LEO between the TDB and the TT seconds, 7.7 km across a leap second).
/// </summary>
public class TwoBodyDynamicalTimeTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    // Ten days across the leap second of 2016-12-31, near the perihelion, where TDB − TT changes fastest: 2.8e-4 s
    private static readonly TimeSystem.Time StartUtc = new(2016, 12, 25, 0, 0, 0, frame: TimeFrame.UTCFrame);
    private static readonly TimeSystem.Time EndUtc = new(2017, 1, 4, 0, 0, 0, frame: TimeFrame.UTCFrame);

    private readonly ITestOutputHelper _output;

    public TwoBodyDynamicalTimeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static readonly string[] Tags = { "UTC", "TAI", "TT", "TDB" };

    public static TheoryData<string, string, string> TagPairs
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var body in new[] { "Earth", "Moon" })
            foreach (var from in Tags)
            foreach (var to in Tags)
                data.Add(body, from, to);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TagPairs))]
    public void AtEpoch_CountsTheDynamicalTimeOfTheCentralBody(string body, string fromTag, string toTag)
    {
        // Arrange
        var elements = Orbit(body, Tagged(StartUtc, fromTag));
        var target = Tagged(EndUtc, toTag);
        double expected = ReferenceSeconds(body, elements.Epoch, target);

        // Act
        double elapsed = Elapsed(elements, elements.AtEpoch(target).MeanAnomaly(), expected);
        double elapsedOfElements = Elapsed(elements, elements.ToKeplerianElements(target).MeanAnomaly(), expected);

        // Assert
        double bound = Bound(body, fromTag, toTag);
        _output.WriteLine($"MEASURE {body} {fromTag} -> {toTag}: elapsed - reference {elapsed - expected:E3} s (bound {bound:E2})");
        Assert.True(System.Math.Abs(elapsed - expected) <= bound, $"{elapsed - expected} s from the reference.");
        Assert.True(System.Math.Abs(elapsedOfElements - expected) <= bound,
            $"ToKeplerianElements: {elapsedOfElements - expected} s from the reference.");
        Assert.Equal(target, elements.AtEpoch(target).Epoch);
    }

    [Theory]
    [MemberData(nameof(TagPairs))]
    public void ToStateVector_IsTheKeplerStateAtTheDynamicalTime(string body, string fromTag, string toTag)
    {
        // Arrange: SPICE prop2b of the state at the start over the reference seconds
        var elements = Orbit(body, Tagged(StartUtc, fromTag));
        var target = Tagged(EndUtc, toTag);
        var expected = TwoBody(elements.ToStateVector(), ReferenceSeconds(body, elements.Epoch, target));

        // Act
        var state = elements.ToStateVector(target);

        // Assert: the elapsed-time bound of the pair as a distance along the orbit, plus 1 mm for the two
        // conversions and the Kepler equation over 10 days (both solve it to the rounding of 1e3 rad of mean anomaly)
        double error = (state.Position - expected.Position).Magnitude();
        double bound = Bound(body, fromTag, toTag) * expected.Velocity.Magnitude() + 1e-3;
        _output.WriteLine($"MEASURE {body} {fromTag} -> {toTag}: |r - prop2b| {error:E3} m (bound {bound:E2})");
        Assert.True(error <= bound, $"{error} m from the Kepler state.");
        Assert.Equal(target, state.Epoch);
    }

    [Theory]
    [InlineData("Earth")]
    [InlineData("Moon")]
    public void TheSameInstantInAnotherFrame_IsTheOrbitItself(string body)
    {
        // Arrange: a UTC orbit, read at its own instant in each frame (the TDB label is up to a tick before it)
        var elements = Orbit(body, StartUtc);
        var state = elements.ToStateVector();

        foreach (var epoch in new[] { StartUtc.ToTAI(), StartUtc.ToTDT(), StartUtc.ToGPS(), StartUtc.ToTDB() })
        {
            // Act
            var at = elements.AtEpoch(epoch);

            // Assert: no elapsed time, so the mean anomaly and the state of the orbit, bit for bit
            Assert.Equal(elements.MeanAnomaly(), at.MeanAnomaly());
            Assert.Equal(state.Position, at.ToStateVector().Position);
            Assert.Equal(state.Velocity, at.ToStateVector().Velocity);
            Assert.Equal(epoch, at.Epoch);
        }
    }

    [Fact]
    public void AcrossTheLeapSecond_TwoUtcEpochs_CountIt()
    {
        // Arrange: a minute before and a minute after the leap second, 121 s of TT and of TDB (to the series)
        var before = new TimeSystem.Time(2016, 12, 31, 23, 59, 0, frame: TimeFrame.UTCFrame);
        var after = new TimeSystem.Time(2017, 1, 1, 0, 1, 0, frame: TimeFrame.UTCFrame);

        foreach (var body in new[] { "Earth", "Moon" })
        {
            var elements = Orbit(body, before);

            // Act
            double elapsed = Elapsed(elements, elements.AtEpoch(after).MeanAnomaly(), 120.5);

            // Assert: 121 s, not the 120 s of the calendars; TDB − TT changes by 4e-8 s in these two minutes
            Assert.Equal(121.0, elapsed, 6);
        }
    }

    private static KeplerianElements Orbit(string body, TimeSystem.Time epoch)
    {
        // A LEO about the Earth, a low lunar orbit about the Moon, both inclined
        return body == "Earth"
            ? new KeplerianElements(6778137.0, 0.001, 51.6 * Deg, 30.0 * Deg, 40.0 * Deg, 10.0 * Deg,
                new CelestialBody(PlanetsAndMoons.EARTH), epoch, Frames.Frame.ICRF)
            : new KeplerianElements(1837400.0, 0.001, 60.0 * Deg, 30.0 * Deg, 40.0 * Deg, 10.0 * Deg,
                new CelestialBody(PlanetsAndMoons.MOON), epoch, Frames.Frame.ICRF);
    }

    private static TimeSystem.Time Tagged(TimeSystem.Time utc, string tag) => tag switch
    {
        "UTC" => utc,
        "TAI" => utc.ToTAI(),
        "TT" => utc.ToTDT(),
        "TDB" => utc.ToTDB(),
        _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, null)
    };

    // The dynamical time of the central body between the two tagged epochs, as the epochs are (a TDB label is up to a
    // tick before the UTC instant it was converted from)
    private static double ReferenceSeconds(string body, TimeSystem.Time from, TimeSystem.Time to) => body == "Earth"
        ? DynamicalTimeReference.TtSecondsBetween(from, to)
        : DynamicalTimeReference.TdbSecondsBetween(from, to);

    // The elapsed time between the reference series and the framework's: exact between UTC, TAI and TT tags about the
    // Earth and between TDB tags about the Moon; through the series otherwise (DynamicalTimeReference.SeriesBound). The
    // mean anomaly after 10 days, 950 rad in LEO, resolves 1.1e-13 rad, 1e-10 s of motion: 1e-9 s for the exact pairs
    private static double Bound(string body, string fromTag, string toTag)
    {
        bool throughTheSeries = body == "Earth" ? fromTag == "TDB" || toTag == "TDB" : fromTag != "TDB" || toTag != "TDB";
        return throughTheSeries ? DynamicalTimeReference.SeriesBound : 1e-9;
    }

    // The elapsed time that took the mean anomaly of the elements to the given one, the whole turns taken from the
    // expected elapsed time
    private static double Elapsed(KeplerianElements elements, double meanAnomaly, double expected)
    {
        double n = elements.MeanMotion();
        double difference = meanAnomaly - (elements.MeanAnomaly() + n * expected);
        difference -= IO.Astrodynamics.Constants._2PI * System.Math.Round(difference / IO.Astrodynamics.Constants._2PI);
        return expected + difference / n;
    }

    // prop2b over the whole ticks of the elapsed time, then the sub-tick remainder along the velocity
    private static StateVector TwoBody(StateVector initial, double elapsed)
    {
        long ticks = (long)System.Math.Round(elapsed * TimeSpan.TicksPerSecond);
        double remainder = elapsed - ticks / (double)TimeSpan.TicksPerSecond;
        var state = SpiceAPI.Instance.Propagate2Bodies(initial, initial.Observer.GM, TimeSpan.FromTicks(ticks));
        return new StateVector(state.Position + state.Velocity * remainder, state.Velocity, state.Observer,
            state.Epoch, state.Frame);
    }
}
