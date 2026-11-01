// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.Tests.Time;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The readers of a <see cref="PropagationSolution"/> read the instant an epoch names, whatever its time frame (#507).
/// Before #507 an epoch in UTC, TAI, TT or GPS was read on a TDB solution as a difference of TAI, so as TT seconds
/// from a TDB label: another instant, by the change of TDB − TT since the start (0.22 m after a day in LEO, 6.4 m after
/// 30 days), and a spurious <see cref="ArgumentOutOfRangeException"/> at the UTC end of a UTC window. About the Earth
/// the solution counts TT seconds (#501), about any other body TDB seconds.
/// </summary>
public class PropagationSolutionTimeFrameTests
{
    private static readonly TimeSystem.Time Start = new(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame);

    // 13 h after the start, in UTC: TDB − TT changes there by 2.0e-5 s from the start
    private static readonly TimeSystem.Time ReadEpoch = Start.AddHours(13.0);

    private readonly ITestOutputHelper _output;

    public PropagationSolutionTimeFrameTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<string, bool> Cases => new()
    {
        { "Earth", true }, { "Earth", false }, { "Moon", true }, { "Moon", false }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnInstantReadInEveryFrame_IsTheSameState(string body, bool rk78)
    {
        // Arrange
        var solution = Propagate(body, rk78, out _);
        var utc = ReadEpoch;

        // Act
        var states = new Dictionary<string, (Vector3 Position, Vector3 Velocity)>
        {
            ["UTC"] = solution.InterpolateAt(utc),
            ["TAI"] = solution.InterpolateAt(utc.ToTAI()),
            ["TT"] = solution.InterpolateAt(utc.ToTDT()),
            ["GPS"] = solution.InterpolateAt(utc.ToGPS()),
            ["TDB"] = solution.InterpolateAt(utc.ToTDB())
        };

        // Assert: UTC, TAI, TT and GPS differ by whole ticks, so they name the same t in both time arguments
        foreach (var frame in new[] { "TAI", "TT", "GPS" })
        {
            Assert.Equal(states["UTC"].Position, states[frame].Position);
            Assert.Equal(states["UTC"].Velocity, states[frame].Velocity);
        }

        double tdbDeparture = (states["TDB"].Position - states["UTC"].Position).Magnitude();
        _output.WriteLine($"MEASURE {body} {(rk78 ? "RK7(8)" : "Velocity-Verlet")}: TDB label vs UTC tag {tdbDeparture:E3} m");
        if (body == "Moon")
        {
            // About the Moon the epoch is converted to TDB, the frame of the labels: its label itself
            Assert.Equal(states["UTC"].Position, states["TDB"].Position);
            Assert.Equal(states["UTC"].Velocity, states["TDB"].Velocity);
        }
        else
        {
            // About the Earth the TDB label of the instant is up to a tick before it (ToTDB truncates)
            Assert.True(tdbDeparture <= states["UTC"].Velocity.Magnitude() * 1e-7,
                $"The TDB label reads {tdbDeparture} m from the UTC tag.");
        }
    }

    [Theory]
    [InlineData("Earth")]
    [InlineData("Moon")]
    public void AnInstantReadInUtc_IsTheTwoBodyStateAtItsDynamicalTime(string body)
    {
        // Arrange: RK7(8) at 1e-13 over the point mass, against SPICE prop2b over the seconds of the dynamical time
        // of the central body from the start, TT about the Earth, TDB about the Moon (reference of DynamicalTimeReference)
        var solution = Propagate(body, true, out var initial);
        double elapsed = body == "Earth"
            ? DynamicalTimeReference.TtSecondsBetween(Start, ReadEpoch)
            : DynamicalTimeReference.TdbSecondsBetween(Start, ReadEpoch);
        var expected = TwoBody(initial, elapsed);

        // Act
        var (position, velocity) = solution.InterpolateAt(ReadEpoch);

        // Assert: the propagation starts at the TDB label of the start, up to a tick before it (choice 7 of #501), and
        // about the Moon the read epoch is its TDB label too, so up to one tick of motion each, above the integration
        // floor of RK7(8) at 1e-13 (6.4e-5 m over a day in LEO, design of #501, §9); the reference's elapsed time is
        // within 0.01 µs of the framework's series over 13 h. The other time argument would be off by the change of
        // TDB − TT, 2.0e-5 s: 3.2 cm about the Moon, 15 cm about the Earth
        double error = (position - expected.Position).Magnitude();
        double bound = (body == "Earth" ? 1 : 2) * 1e-7 * initial.Velocity.Magnitude() + 1e-4;
        _output.WriteLine($"MEASURE {body}: |r - prop2b| {error:E3} m (bound {bound:E3}), |v - prop2b| {(velocity - expected.Velocity).Magnitude():E3} m/s");
        Assert.True(error <= bound, $"{body}: {error} m from the two-body state at the dynamical time.");
    }

    [Fact]
    public void TheCovarianceAndTheTransitionMatrix_ReadInUtcAboutTheMoon_AreThoseOfTheirTdbLabel()
    {
        // Arrange
        var solution = Propagate("Moon", true, out _, withCovariance: true);
        var tdb = ReadEpoch.ToTDB();

        // Act
        var phiUtc = solution.GetStateTransitionMatrix(ReadEpoch);
        var phiTdb = solution.GetStateTransitionMatrix(tdb);
        var covarianceUtc = solution.GetCovariance(ReadEpoch);
        var covarianceTdb = solution.GetCovariance(tdb);

        // Assert
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(phiTdb.Get(i, j), phiUtc.Get(i, j));
                Assert.Equal(covarianceTdb.Get(i, j), covarianceUtc.Get(i, j));
            }
        }
    }

    [Theory]
    [InlineData("Earth")]
    [InlineData("Moon")]
    public void TheUtcEndOfAUtcWindow_IsReadAtItsInstant(string body)
    {
        // Arrange
        var solution = Propagate(body, true, out _, withCovariance: true);
        var end = Start.AddDays(1.0);

        // Act
        var phi = solution.GetStateTransitionMatrix(end);
        var (position, velocity) = solution.InterpolateAt(end);
        var (labelPosition, _) = solution.InterpolateAt(end.ToTDB());

        // Assert: inside the solution, at the instant of the end: about the Moon its TDB label itself, about the Earth
        // within a tick of motion of it (the propagation about the Moon runs on to its output grid, 2.4e-5 s later, as
        // before #501)
        double departure = (position - labelPosition).Magnitude();
        _output.WriteLine($"MEASURE {body}: end read at its UTC tag vs its TDB label {departure:E3} m");
        Assert.Equal(6, phi.Rows);
        if (body == "Moon")
            Assert.Equal(labelPosition, position);
        else
            Assert.True(departure <= velocity.Magnitude() * 1e-7, $"{departure} m from the TDB label of the end.");
    }

    // UTC instants whose TDB label, converted to TAI, is not their own TAI: the conversion of the label truncates
    // TDB − TT to the tick a second time. Found by the review of #505 over 8e6 random UTC instants of 2021 (the first
    // one whose label is a tick later in TAI) and over a scan of 2021-03-20 by steps of 4999 ticks (one a tick off)
    private static readonly TimeSystem.Time LabelLaterInTai =
        new(new DateTime(2021, 6, 2, 14, 51, 26).AddTicks(1940007), TimeFrame.UTCFrame);

    private static readonly TimeSystem.Time LabelOffInTai =
        new(new DateTime(2021, 3, 20, 0, 5, 5).AddTicks(969684), TimeFrame.UTCFrame);

    [Theory]
    [InlineData("Earth")]
    [InlineData("Moon")]
    public void TheUtcStartOfAUtcWindow_IsInsideTheSolution(string body)
    {
        // Arrange: a UTC window starting at an instant whose TDB label, the start of the solution, compares a tick after
        // it through TAI; before the start bound was read in the time argument, the readers below threw
        // ArgumentOutOfRangeException there, as on main (#507)
        var start = LabelLaterInTai;
        Assert.True(start.ToTDB().ToTAI() > start.ToTAI());
        var centralBody = new CelestialBody(body == "Earth" ? PlanetsAndMoons.EARTH : PlanetsAndMoons.MOON,
            Frames.Frame.ICRF, start);
        var orbit = new KeplerianElements(body == "Earth" ? 6778137.0 : 1837400.0, 0.001, 0.9, 0.2, 0.3, 0.0,
            centralBody, start.ToTDB(), Frames.Frame.ICRF).ToStateVector();
        var initial = new StateVector(orbit.Position, orbit.Velocity, centralBody, orbit.Epoch, Frames.Frame.ICRF,
            Diagonal(100.0, 1e-4));
        var spacecraft = ReferenceCase.NewSpacecraft(body == "Earth" ? -1509 : -1510, initial);
        var window = new Window(start, start.AddHours(1.0));
        using var propagator = new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-12, 1e-12),
            new CelestialItem[] { centralBody }, false, false, TimeSpan.FromMinutes(10.0),
            new CovariancePropagationOptions());
        var solution = propagator.Propagate();

        // Act
        var phi = solution.GetStateTransitionMatrix(start);
        var state = solution.GetStateVector(start);
        var (position, _) = solution.InterpolateAt(start);
        var replayed = VariationalReplay.Replay(solution, spacecraft,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }, start);

        // Assert: the start of the propagation, less than a tick after its label (identity of Φ to its first-order term,
        // |v| / |r| × 1e-7 s); the screening window of a conjunction analysis covers it at both ends
        Assert.Equal(position, state.Position);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.True(System.Math.Abs(phi.Get(i, j) - (i == j ? 1.0 : 0.0)) < 1e-6, $"Φ[{i},{j}] = {phi.Get(i, j)}");
            }
        }

        Assert.Single(replayed.Segments);
        Assert.True(solution.Covers(window.StartDate, 0.0));
        Assert.True(solution.Covers(window.EndDate, PropagationSolution.EpochTick));
        Assert.False(solution.Covers(start.AddSeconds(-1e-6), 0.0));
    }

    [Theory]
    [InlineData("Earth")]
    [InlineData("Moon")]
    public void ACovarianceGivenAtTheUtcStartOfItsWindow_IsPropagatedFromTheStateAsGiven(string body)
    {
        // Arrange: a UTC initial state with its covariance, at an instant whose TDB label is a tick off it in TAI;
        // before the epochs were compared as TDB labels the propagation of the covariance was rejected there, with a
        // message printing two equal epochs, and the start state went through the Keplerian round trip (#510)
        var start = LabelOffInTai;
        Assert.NotEqual(start.ToTAI(), start.ToTDB().ToTAI());
        var centralBody = new CelestialBody(body == "Earth" ? PlanetsAndMoons.EARTH : PlanetsAndMoons.MOON,
            Frames.Frame.ICRF, start);
        var orbit = new KeplerianElements(body == "Earth" ? 6778137.0 : 1837400.0, 0.001, 0.9, 0.2, 0.3, 0.0,
            centralBody, start, Frames.Frame.ICRF);
        var orbitState = orbit.ToStateVector();
        var initial = new StateVector(orbitState.Position, orbitState.Velocity, centralBody, start, Frames.Frame.ICRF,
            Diagonal(100.0, 1e-4));
        var window = new Window(start, start.AddHours(1.0));
        var bodies = new CelestialItem[] { centralBody };
        var spacecraft = ReferenceCase.NewSpacecraft(body == "Earth" ? -1511 : -1512, initial);
        using var covariancePropagator = new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-12, 1e-12),
            bodies, false, false, TimeSpan.FromMinutes(10.0),
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        var replayedCraft = ReferenceCase.NewSpacecraft(body == "Earth" ? -1513 : -1514, initial);
        using var transitionPropagator = new CentralBodyPropagator(window, replayedCraft,
            new RK78Integrator(1e-12, 1e-12), bodies, false, false, TimeSpan.FromMinutes(10.0),
            new CovariancePropagationOptions());

        // Act
        var solution = covariancePropagator.Propagate();
        var transitionOnly = transitionPropagator.Propagate();
        var resolver = new EncounterCovarianceResolver(replayedCraft, transitionOnly,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }, start.AddMinutes(30.0));

        // Assert: the propagation starts at the state as given (no Keplerian round trip), the initial covariance of
        // the replay is accepted at the same epoch, and orbital parameters taken at the TDB label of their UTC epoch
        // are the parameters themselves (no two-body motion over the sub-tick residual of the label)
        Assert.Equal(initial.Position, solution.StateVectors[0].Position);
        Assert.Equal(initial.Velocity, solution.StateVectors[0].Velocity);
        Assert.NotNull(resolver.PropagatedSolution(start.AddMinutes(30.0)));
        Assert.NotNull(resolver.Replayed);
        Assert.Equal(orbitState.Position, orbit.AtEpoch(start.ToTDB()).ToStateVector().Position);
    }

    private static PropagationSolution Propagate(string body, bool rk78, out StateVector initial,
        bool withCovariance = false)
    {
        var centralBody = new CelestialBody(body == "Earth" ? PlanetsAndMoons.EARTH : PlanetsAndMoons.MOON,
            Frames.Frame.ICRF, Start);
        initial = body == "Earth"
            ? new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5400.0, 5400.0), centralBody, Start,
                Frames.Frame.ICRF)
            : new StateVector(new Vector3(1837400.0, 0.0, 0.0), new Vector3(0.0, 1155.0, 1155.0), centralBody, Start,
                Frames.Frame.ICRF);
        if (withCovariance)
            initial = new StateVector(initial.Position, initial.Velocity, centralBody, Start, Frames.Frame.ICRF,
                Diagonal(100.0, 1e-4));

        var spacecraft = ReferenceCase.NewSpacecraft(body == "Earth" ? -1507 : -1508, initial);
        var window = new Window(Start, Start.AddDays(1.0));
        var bodies = new CelestialItem[] { centralBody };
        using var propagator = rk78
            ? withCovariance
                ? new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-13, 1e-13), bodies, false,
                    false, TimeSpan.FromHours(1.0), new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance })
                : new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-13, 1e-13), bodies, false,
                    false, TimeSpan.FromHours(1.0))
            : new CentralBodyPropagator(window, spacecraft, bodies, false, false, TimeSpan.FromSeconds(10.0));
        return propagator.Propagate();
    }

    // prop2b over the whole ticks of the elapsed time, then the sub-tick remainder along the velocity: the remainder,
    // below 5e-8 s, leaves a·f²/2 < 1e-14 m
    private static StateVector TwoBody(StateVector initial, double elapsed)
    {
        long ticks = (long)System.Math.Round(elapsed * TimeSpan.TicksPerSecond);
        double remainder = elapsed - ticks / (double)TimeSpan.TicksPerSecond;
        var state = SpiceAPI.Instance.Propagate2Bodies(initial, initial.Observer.GM, TimeSpan.FromTicks(ticks));
        return new StateVector(state.Position + state.Velocity * remainder, state.Velocity, state.Observer,
            state.Epoch, state.Frame);
    }

    private static IO.Astrodynamics.Math.Matrix Diagonal(double position, double velocity)
    {
        var matrix = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            matrix.Set(i, i, position);
            matrix.Set(i + 3, i + 3, velocity);
        }

        return matrix;
    }
}
