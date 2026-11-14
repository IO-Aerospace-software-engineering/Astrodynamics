// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The TCA and miss distance of the conjunction analysis of two RK7(8) trajectories (#363), against the two-body
/// solution in universal variables.
/// </summary>
public class ConjunctionTcaAccuracyTests
{
    private static readonly TimeSystem.Time Start = PartialsTestCases.Epoch;

    // The secondary is 5 % faster than circular at the meeting, on a 400 × 1900 km orbit: its steps differ from those of
    // the protected spacecraft, so the errors of their interpolations do not cancel in the relative state
    private const double SecondarySpeedFactor = 1.05;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public ConjunctionTcaAccuracyTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
    }

    /// <summary>
    /// A 400 km LEO and a 400 × 1900 km orbit, point-mass Earth, the default tolerance of RK7(8), crossing at 40° with a
    /// radial offset of 100 m, at several epochs within the steps of the two trajectories. The search on the
    /// polynomials of the trajectories locates the TCA on their cubic Hermite interpolation, off by 2.6e-6 s to 1.3e-4 s
    /// in these cases; the TCA is then refined on the states of the trajectories, which have the accuracy of the
    /// integrator: 8.8e-9 s to 5.1e-8 s. The miss distance is computed on those states in both cases, so the error of
    /// the TCA reaches it at second order only (0.2 mm to 2.8 mm before the refinement, 0.2 mm to 0.26 mm after, the
    /// error of the integration over 2000 s).
    /// </summary>
    [Theory]
    [InlineData(1900.0)]
    [InlineData(1950.0)]
    [InlineData(2000.0)]
    [InlineData(2050.0)]
    [InlineData(2100.0)]
    [InlineData(2150.0)]
    [InlineData(2200.0)]
    [InlineData(2250.0)]
    public void Analyze_OfTwoRk78Trajectories_FindsTheKeplerianTcaAndMissDistance(double meeting)
    {
        // Arrange: the states at the meeting, then back to the start of the window along the Keplerian orbits
        double mu = _earth.GM;
        var protectedInitial = PartialsTestCases.State(PartialsTestCases.Leo400, _earth);
        var (protectedAtMeeting, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity,
            meeting);
        var radial = Position(protectedAtMeeting).Normalize();
        var secondaryPosition = Position(protectedAtMeeting) + radial * 100.0;
        var velocity = Velocity(protectedAtMeeting);
        double angle = 40.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var secondaryVelocity = (velocity * System.Math.Cos(angle) + radial.Cross(velocity) * System.Math.Sin(angle)) *
                                (SecondarySpeedFactor * System.Math.Sqrt(mu / secondaryPosition.Magnitude()) /
                                 velocity.Magnitude());
        var (secondaryStart, _) = KeplerianStm.Propagate(mu, secondaryPosition, secondaryVelocity, -meeting);
        var secondaryInitial = new StateVector(Position(secondaryStart), Velocity(secondaryStart), _earth, Start,
            Frames.Frame.ICRF);

        var window = new TimeSystem.Window(Start, Start.AddHours(1.0));
        var protectedSpacecraft = Spacecraft(-1981, "TCAP", protectedInitial);
        var secondarySpacecraft = Spacecraft(-1982, "TCAS", secondaryInitial);
        var protectedTrajectory = protectedSpacecraft.Propagate(window, new CelestialItem[] { _earth },
            new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0));
        var secondaryTrajectory = secondarySpacecraft.Propagate(window, new CelestialItem[] { _earth },
            new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0));

        // The reference: the zero of the range rate of the Keplerian states
        double referenceTca = KeplerianTca(mu, protectedInitial, secondaryInitial, meeting - 100.0, meeting + 100.0);
        var (p, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity, referenceTca);
        var (s, _) = KeplerianStm.Propagate(mu, secondaryInitial.Position, secondaryInitial.Velocity, referenceTca);
        double referenceMiss = (Position(s) - Position(p)).Magnitude();

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedSpacecraft),
            protectedTrajectory, secondarySpacecraft, secondaryTrajectory, window);

        // Assert
        // The TCA in TT seconds from the start, the time argument of the trajectories and of the Keplerian reference (#501)
        double tcaError = PropagationTimeArgument.Tt.SecondsBetween(Start, encounter.EncounterState.Epoch) - referenceTca;
        double missError = encounter.EncounterState.MissDistanceMeters - referenceMiss;
        _output.WriteLine($"Keplerian TCA {referenceTca:F6} s, miss {referenceMiss:F6} m; analysis: TCA {tcaError:E3} s, " +
                          $"miss {missError:E3} m from the reference");
        Assert.True(System.Math.Abs(tcaError) < 1e-6, $"TCA {tcaError:E3} s");
        Assert.True(System.Math.Abs(missError) < 1e-3, $"miss distance {missError:E3} m");
    }

    // The pair of the theory above meeting 2000 s after the start, propagated over an hour in RK7(8) or by Velocity-Verlet
    // (10 s); the Keplerian TCA in TT seconds from the start
    private (Spacecraft Protected, PropagationSolution ProtectedTrajectory, Spacecraft Secondary,
        PropagationSolution SecondaryTrajectory, double KeplerianTca) LabelPair(bool rk78, int naifId)
    {
        const double meeting = 2000.0;
        double mu = _earth.GM;
        var protectedInitial = PartialsTestCases.State(PartialsTestCases.Leo400, _earth);
        var (protectedAtMeeting, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity, meeting);
        var radial = Position(protectedAtMeeting).Normalize();
        var secondaryPosition = Position(protectedAtMeeting) + radial * 100.0;
        var velocity = Velocity(protectedAtMeeting);
        double angle = 40.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var secondaryVelocity = (velocity * System.Math.Cos(angle) + radial.Cross(velocity) * System.Math.Sin(angle)) *
                                (SecondarySpeedFactor * System.Math.Sqrt(mu / secondaryPosition.Magnitude()) /
                                 velocity.Magnitude());
        var (secondaryStart, _) = KeplerianStm.Propagate(mu, secondaryPosition, secondaryVelocity, -meeting);
        var secondaryInitial = new StateVector(Position(secondaryStart), Velocity(secondaryStart), _earth, Start,
            Frames.Frame.ICRF);

        var window = new TimeSystem.Window(Start, Start.AddHours(1.0));
        var protectedSpacecraft = Spacecraft(naifId, "TCAP" + -naifId, protectedInitial);
        var secondarySpacecraft = Spacecraft(naifId - 1, "TCAS" + -naifId, secondaryInitial);
        PropagationSolution Propagate(Spacecraft spacecraft) => rk78
            ? spacecraft.Propagate(window, new CelestialItem[] { _earth }, new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0))
            : spacecraft.Propagate(window, new CelestialItem[] { _earth }, false, false, TimeSpan.FromSeconds(10.0));
        var protectedTrajectory = Propagate(protectedSpacecraft);
        var secondaryTrajectory = Propagate(secondarySpacecraft);
        return (protectedSpacecraft, protectedTrajectory, secondarySpacecraft, secondaryTrajectory,
            KeplerianTca(mu, protectedInitial, secondaryInitial, meeting - 100.0, meeting + 100.0));
    }

    private static TimeSystem.ITimeFrame LabelFrame(string frame) => frame switch
    {
        "UTC" => TimeSystem.TimeFrame.UTCFrame,
        "TT" => TimeSystem.TimeFrame.TDTFrame,
        "TAI" => TimeSystem.TimeFrame.TAIFrame,
        _ => TimeSystem.TimeFrame.TDBFrame
    };

    // The analysis window, a minute inside the propagations at each end, in the given frame: the TCA lies between two
    // steps of the trajectories, never in the bracket that starts at the window start
    private static TimeSystem.Window LabelWindow(string frame) =>
        new(Start.AddMinutes(1.0).ConvertTo(LabelFrame(frame)), Start.AddMinutes(59.0).ConvertTo(LabelFrame(frame)));

    [Theory]
    [InlineData("UTC", true, false)]
    [InlineData("UTC", true, true)]
    [InlineData("UTC", false, false)]
    [InlineData("UTC", false, true)]
    [InlineData("TT", true, false)]
    [InlineData("TT", false, true)]
    [InlineData("TAI", true, true)]
    [InlineData("TAI", false, false)]
    [InlineData("TDB", true, false)]
    [InlineData("TDB", false, true)]
    public void DenseTca_IsLabelledInTheWindowScale(string frame, bool rk78, bool all)
    {
        // Arrange
        int naifId = -2101 - 2 * (Array.IndexOf(new[] { "UTC", "TT", "TAI", "TDB" }, frame) * 4 + (rk78 ? 2 : 0) + (all ? 1 : 0));
        var pair = LabelPair(rk78, naifId);
        var window = LabelWindow(frame);
        var profile = new ProtectedSpacecraftProfile(pair.Protected);

        // Act: the polynomial search of the two trajectories, and the sampled search of the same spacecraft (their last
        // propagations) as the second reader of the scale
        var dense = all
            ? ConjunctionAssessment.AnalyzeAll(profile, pair.ProtectedTrajectory, pair.Secondary, pair.SecondaryTrajectory, window)
            : new[] { ConjunctionAssessment.Analyze(profile, pair.ProtectedTrajectory, pair.Secondary, pair.SecondaryTrajectory, window) };
        var sampled = ConjunctionAssessment.Analyze(profile, pair.Secondary, window);

        // Assert: one label, the scale of the window start, on both paths (#683: the dense TCA took the TDB scale of the
        // step that opens its bracket), the states read at it, never a window end
        Assert.NotEmpty(dense);
        Assert.Equal(window.StartDate.Frame, sampled.EncounterState.Epoch.Frame);
        foreach (var encounter in dense)
        {
            var epoch = encounter.EncounterState.Epoch;
            _output.WriteLine($"LABEL {frame} rk78={rk78} all={all}: {epoch} ({epoch.DateTime.Ticks}) miss " +
                              $"{encounter.EncounterState.MissDistanceMeters:R} m; sampled {sampled.EncounterState.Epoch}");
            Assert.Equal(window.StartDate.Frame, epoch.Frame);
            Assert.Equal(epoch, encounter.ProtectedState.Epoch);
            Assert.Equal(epoch, encounter.SecondaryState.Epoch);
            Assert.NotEqual(window.StartDate, epoch);
            Assert.NotEqual(window.EndDate, epoch);
            Assert.True(epoch > window.StartDate && epoch < window.EndDate);
        }
    }

    [Theory]
    [InlineData("UTC", true)]
    [InlineData("UTC", false)]
    [InlineData("TT", true)]
    [InlineData("TT", false)]
    [InlineData("TAI", true)]
    [InlineData("TAI", false)]
    public void DenseTca_InTheWindowScale_IsTheTickNearerTheZero(string frame, bool rk78)
    {
        // Arrange
        int naifId = -2201 - 2 * (Array.IndexOf(new[] { "UTC", "TT", "TAI" }, frame) * 2 + (rk78 ? 1 : 0));
        var pair = LabelPair(rk78, naifId);
        var window = LabelWindow(frame);

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(pair.Protected), pair.ProtectedTrajectory,
            pair.Secondary, pair.SecondaryTrajectory, window);

        // Assert: the label in the window scale is the tick of that scale nearer the zero of the range rate of the states
        // (#624's rule, recomputed here from the public interpolation of the two trajectories)
        double RangeRate(TimeSystem.Time epoch)
        {
            var (p, v) = pair.ProtectedTrajectory.InterpolateAt(epoch);
            var (s, w) = pair.SecondaryTrajectory.InterpolateAt(epoch);
            return (s - p) * (w - v);
        }

        var label = encounter.EncounterState.Epoch;
        Assert.Equal(LabelFrame(frame), label.Frame);
        double atLabel = System.Math.Abs(RangeRate(label));
        double before = System.Math.Abs(RangeRate(label.AddTicks(-1)));
        double after = System.Math.Abs(RangeRate(label.AddTicks(1)));
        _output.WriteLine($"MEASURE #683 label {frame} rk78={rk78}: |rr| {atLabel:E3} at the label, {before:E3} a tick before, " +
                          $"{after:E3} a tick after (m²/s)");
        Assert.True(atLabel <= before && atLabel <= after, $"|rr| {atLabel:E3}, {before:E3}, {after:E3}");

        // The instant within the bound of the theory above of the zero of the Keplerian range rate (RK7(8) only: the
        // Velocity-Verlet steps of 10 s carry their own error)
        if (rk78)
        {
            double tcaError = PropagationTimeArgument.Tt.SecondsBetween(Start, label) - pair.KeplerianTca;
            _output.WriteLine($"MEASURE #683 label {frame}: TCA {tcaError:E3} s from the Keplerian zero");
            Assert.True(System.Math.Abs(tcaError) < 1e-6, $"TCA {tcaError:E3} s");
        }
    }

    [Theory]
    [InlineData("inFrame")]
    [InlineData("startInstant")]
    [InlineData("endInstant")]
    [InlineData("zeroOnTheTick")]
    [InlineData("nearerBelow")]
    [InlineData("nearerAbove")]
    [InlineData("tie")]
    [InlineData("firstTick")]
    [InlineData("lastTick")]
    public void InWindowScale_TheTickOfTheWindowFrameNearerTheZero(string row)
    {
        // Arrange: a UTC window of an hour and a range rate linear in time, g = t − t0 (s), whose zero t0 is given in UTC
        // ticks: the closed form of the tick nearer the zero
        var start = TimeSystem.Time.J2000UTC.AddHours(1.0);
        var window = new TimeSystem.Window(start, start.AddHours(1.0));
        var middle = start.AddMinutes(30.0);
        TimeSystem.Time zero = row switch
        {
            "nearerBelow" => middle.AddTicks(3),
            "nearerAbove" => middle.AddTicks(7),
            "tie" => middle,
            "firstTick" => start.AddTicks(1),
            "lastTick" => window.EndDate.AddTicks(-1),
            _ => middle
        };

        // The zero of g at zero + offset ticks: 0.2 tick inside the window for the first and last rows
        double offsetTicks = row switch { "nearerBelow" => 0.3, "nearerAbove" => -0.3, "tie" => 0.5, "firstTick" => -0.8, "lastTick" => 0.8, _ => 0.0 };
        double RangeRate(TimeSystem.Time t) => (t - zero).Ticks * 1e-7 - offsetTicks * 1e-7;
        var epoch = row switch
        {
            "inFrame" => middle.AddTicks(12),
            "startInstant" => start.ToTDB(),
            "endInstant" => window.EndDate.ToTDB(),
            _ => zero.ToTDB()
        };

        // Act
        var label = ConjunctionAssessment.InWindowScale(epoch, window, RangeRate);

        // Assert: the frame of the window start; an epoch already in it unchanged, a window end that end; otherwise the
        // tick where |g| is smaller (the lower one on a tie), never a window end (#683)
        Assert.Equal(window.StartDate.Frame, label.Frame);
        var expected = row switch
        {
            "inFrame" => middle.AddTicks(12),
            "startInstant" => start,
            "endInstant" => window.EndDate,
            "zeroOnTheTick" => middle,
            "nearerBelow" => middle.AddTicks(3),
            "nearerAbove" => middle.AddTicks(7),
            "tie" => middle,
            "firstTick" => start.AddTicks(1),
            _ => window.EndDate.AddTicks(-1)
        };
        Assert.Equal(expected, label);
    }

    private static double KeplerianTca(double mu, StateVector first, StateVector second, double low, double high)
    {
        double RangeRate(double t)
        {
            var (a, _) = KeplerianStm.Propagate(mu, first.Position, first.Velocity, t);
            var (b, _) = KeplerianStm.Propagate(mu, second.Position, second.Velocity, t);
            return (Position(b) - Position(a)) * (Velocity(b) - Velocity(a));
        }

        Assert.True(RangeRate(low) < 0.0 && RangeRate(high) > 0.0);
        while (high - low > 1e-10)
        {
            double middle = 0.5 * (low + high);
            if (RangeRate(middle) < 0.0)
                low = middle;
            else
                high = middle;
        }

        return 0.5 * (low + high);
    }

    private static Spacecraft Spacecraft(int naifId, string name, StateVector state)
    {
        return new Spacecraft(naifId, name, 100.0, 1000.0, new Clock(name, 65536), state, 1.0);
    }

    private static Vector3 Position(double[] state) => new(state[0], state[1], state[2]);

    private static Vector3 Velocity(double[] state) => new(state[3], state[4], state[5]);
}
