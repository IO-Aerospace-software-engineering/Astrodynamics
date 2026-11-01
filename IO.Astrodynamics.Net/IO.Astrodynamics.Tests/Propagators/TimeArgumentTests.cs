// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.Tests.Time;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The time argument of a numerical propagation (#501): TT seconds between the instants of the window about the Earth,
/// TDB seconds about any other central body, SSB mode included, through every public path that builds a propagation,
/// with both integrators, whatever the tags of the window.
/// </summary>
/// <remarks>
/// <para>
/// The references are independent of the conversions of the library: the TT and TDB seconds between two tagged epochs
/// of <see cref="DynamicalTimeReference"/> (the leap seconds of the IERS and the formula of the leap seconds kernel), and
/// the two-body solution in universal variables (<see cref="KeplerianStm"/>) over those seconds.
/// </para>
/// <para>
/// Over an hour from 2021-03-20 12:00 TDB, TDB − TT changes by 2.8e-7 s, 2.2 mm of a LEO: the Kepler bound below, ten
/// times smaller, tells the two time arguments apart.
/// </para>
/// </remarks>
public class TimeArgumentTests
{
    private static readonly TimeSystem.Time Start = new(2021, 3, 20, 12, 0, 0);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(600.0);

    // RK7(8) at 1e-13 against Kepler over the reference seconds, in LEO and LLO after an hour: a tenth of the 2.2 mm of a
    // TDB time argument about the Earth, far above the integration error (1.3e-7 m at most, MEASURE lines, Linux,
    // 2026-11-01)
    private const double KeplerBound = 2e-4;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;
    private readonly CelestialBody _moon;

    public TimeArgumentTests(ITestOutputHelper output)
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _output = output;
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        _moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
    }

    // The windows of an hour from 2021-03-20 12:00 TDB, by their tags, and the UTC hour across the leap second of
    // 2016-12-31, 3 601 s of TT
    public static TheoryData<string, bool> TaggedWindows()
    {
        var data = new TheoryData<string, bool>();
        foreach (string tags in new[] { "TDB", "UTC", "TT", "TAI", "GPS", "Local", "TDB to UTC", "UTC leap second" })
        foreach (bool rk78 in new[] { true, false })
            data.Add(tags, rk78);
        return data;
    }

    [Theory]
    [MemberData(nameof(TaggedWindows))]
    public void AboutTheEarth_TheWindow_IsItsTtSecondsBetweenItsInstants(string tags, bool rk78)
    {
        // Arrange
        var window = WindowOf(tags);
        var initial = Leo(window.StartDate.ToTDB());

        // Act
        var solution = Spacecraft(-5011, initial).Propagate(window, new CelestialItem[] { _earth },
            rk78 ? new RK78Integrator(1e-13, 1e-13) : new VVIntegrator(TimeSpan.FromSeconds(10.0)), false, false, Step);

        // Assert: the TT seconds between the instants, exact in ticks without a TDB tag, within the departure of the two
        // series with one (2.49e-6 s); the propagation of the leap second runs to its 3 601 s past its grid of 3 600 s
        double reference = DynamicalTimeReference.TtSecondsBetween(window.StartDate, window.EndDate);
        double integrated = WindowEndAssert.IntegratedSeconds(solution);
        double bound = tags.Contains("TDB") ? DynamicalTimeReference.SeriesBound : 1e-9;
        _output.WriteLine($"{tags}, {(rk78 ? "RK7(8)" : "Velocity-Verlet")}: {integrated:R} s integrated, " +
                          $"{integrated - reference:E3} s from the reference");
        Assert.Same(PropagationTimeArgument.Tt, solution.TimeArgument);
        Assert.InRange(integrated - reference, -bound, bound);
        if (tags == "UTC leap second")
        {
            Assert.Equal(3601.0, reference);
            Assert.Null(solution.WindowEnd);
        }
        else
        {
            WindowEndAssert.ReachesTheEndOf(window, solution);
            Assert.Equal(window.EndDate.ToTDB(), solution.StateVectors[^1].Epoch);
        }

        if (rk78)
        {
            double error = KeplerError(initial, solution, integrated);
            _output.WriteLine($"MEASURE {tags}: RK7(8) at 1e-13 against Kepler over the TT length {error:E3} m");
            Assert.True(error < KeplerBound, $"{error:E3} m from Kepler");
        }
    }

    [Theory]
    [MemberData(nameof(TaggedWindows))]
    public void AboutTheMoon_TheWindow_IsItsTdbSeconds_AsBefore(string tags, bool rk78)
    {
        // Arrange
        var window = WindowOf(tags);
        var initial = Llo(window.StartDate.ToTDB());

        // Act
        var solution = Spacecraft(-5012, initial).Propagate(window, new CelestialItem[] { _moon },
            rk78 ? new RK78Integrator(1e-13, 1e-13) : new VVIntegrator(TimeSpan.FromSeconds(10.0)), false, false, Step);

        // Assert: the TDB seconds between the TDB labels of the ends, the expression of main, the longer of them and of
        // the output grid; within two ticks and the departure of the series of the TDB seconds between the instants
        var labels = new TimeSystem.Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        double expected = System.Math.Max(labels.Length.TotalSeconds, 6.0 * Step.TotalSeconds);
        double integrated = WindowEndAssert.IntegratedSeconds(solution);
        double reference = DynamicalTimeReference.TdbSecondsBetween(window.StartDate, window.EndDate);
        Assert.Same(PropagationTimeArgument.Tdb, solution.TimeArgument);
        Assert.Null(solution.WindowEnd);
        Assert.Equal(expected, integrated, 9);
        Assert.InRange(labels.Length.TotalSeconds - reference, -DynamicalTimeReference.SeriesBound - 2e-7,
            DynamicalTimeReference.SeriesBound + 2e-7);
        if (rk78)
        {
            double error = KeplerError(initial, solution, integrated);
            _output.WriteLine($"MEASURE {tags}: RK7(8) at 1e-13 about the Moon against Kepler {error:E3} m");
            Assert.True(error < KeplerBound, $"{error:E3} m from Kepler");
        }
    }

    // Every public path that builds a CentralBodyPropagator, by its name
    public static TheoryData<string, bool> Paths()
    {
        var data = new TheoryData<string, bool>();
        foreach (string path in new[]
                 {
                     "builder", "constructor", "constructor with options", "constructor with an integrator and its forces",
                     "Velocity-Verlet constructor", "Spacecraft.Propagate", "Spacecraft.Propagate with an integrator",
                     "Spacecraft.Propagate with options", "Spacecraft.PropagateAsync", "PropagationTask",
                     "PropagationTask with options", "builder with options"
                 })
        foreach (bool aboutTheEarth in new[] { true, false })
            data.Add(path, aboutTheEarth);
        return data;
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task EveryPath_TakesTheTimeArgumentOfItsCentralBody(string path, bool aboutTheEarth)
    {
        // Arrange: a UTC hour, the window of the suite about the Earth
        var window = WindowOf("UTC");
        var body = aboutTheEarth ? _earth : _moon;
        var initial = aboutTheEarth ? Leo(window.StartDate.ToTDB()) : Llo(window.StartDate.ToTDB());
        var bodies = new CelestialItem[] { body };
        var options = new CovariancePropagationOptions();
        bool velocityVerlet = path is "Velocity-Verlet constructor" or "Spacecraft.Propagate" or "Spacecraft.PropagateAsync" or
            "PropagationTask";
        var spacecraft = Spacecraft(-5013, initial);

        // Act
        var solution = path switch
        {
            "builder" => new CentralBodyPropagatorBuilder(window, spacecraft, new RK78Integrator(1e-13, 1e-13), Step)
                .Build().Propagate(),
            "builder with options" => new CentralBodyPropagatorBuilder(window, spacecraft,
                new RK78Integrator(1e-13, 1e-13), Step).WithCovariancePropagation(options).Build().Propagate(),
            "constructor" => new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-13, 1e-13), bodies,
                false, false, Step).Propagate(),
            "constructor with options" => new CentralBodyPropagator(window, spacecraft,
                new RK78Integrator(1e-13, 1e-13), bodies, false, false, Step, options).Propagate(),
            "constructor with an integrator and its forces" => WithItsForces(window, spacecraft, body),
            "Velocity-Verlet constructor" => new CentralBodyPropagator(window, spacecraft, bodies, false, false, Step)
                .Propagate(),
            "Spacecraft.Propagate" => spacecraft.Propagate(window, bodies, false, false, Step),
            "Spacecraft.Propagate with an integrator" => spacecraft.Propagate(window, bodies,
                new RK78Integrator(1e-13, 1e-13), false, false, Step),
            "Spacecraft.Propagate with options" => spacecraft.Propagate(window, bodies,
                new RK78Integrator(1e-13, 1e-13), false, false, Step, options),
            "Spacecraft.PropagateAsync" => await spacecraft.PropagateAsync(window, bodies, false, false, Step),
            "PropagationTask" => BatchPropagator.Propagate(new[]
                { new PropagationTask(spacecraft, window, bodies, false, false, Step) }).Results.Single().Solution,
            "PropagationTask with options" => BatchPropagator.Propagate(new[]
            {
                new PropagationTask(spacecraft, window, bodies, false, false, Step,
                    () => new RK78Integrator(1e-13, 1e-13)) { CovariancePropagation = options }
            }).Results.Single().Solution,
            _ => throw new ArgumentOutOfRangeException(nameof(path))
        };

        // Assert: the time argument of the central body, the window integrated in it, and the propagation of the same
        // integrator through the constructor, bit for bit
        var reference = velocityVerlet
            ? new CentralBodyPropagator(window, Spacecraft(-5014, initial), bodies, false, false, Step).Propagate()
            : new CentralBodyPropagator(window, Spacecraft(-5014, initial), new RK78Integrator(1e-13, 1e-13), bodies,
                false, false, Step).Propagate();
        Assert.Same(aboutTheEarth ? PropagationTimeArgument.Tt : PropagationTimeArgument.Tdb, solution.TimeArgument);
        if (aboutTheEarth)
        {
            WindowEndAssert.ReachesTheEndOf(window, solution);
        }

        AssertSameStates(reference, solution, path);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Scenario_EveryOverload_PropagatesItsSpacecraftOverTheTimeArgumentOfItsCentralBody(bool aboutTheEarth)
    {
        // Arrange
        var window = WindowOf("UTC");
        var body = aboutTheEarth ? _earth : _moon;
        var initial = aboutTheEarth ? Leo(window.StartDate.ToTDB()) : Llo(window.StartDate.ToTDB());
        var bodies = new CelestialItem[] { body };
        var velocityVerlet = Spacecraft(-5015, initial).Propagate(window, bodies, false, false, Step);
        var rk78 = Spacecraft(-5016, initial).Propagate(window, bodies, new RK78Integrator(1e-13, 1e-13), false, false,
            Step);

        // Act and assert: the three overloads, each the propagation of its integrator, its states labelled as the
        // propagation labels them (the last one the end of the window)
        for (int overload = 0; overload < 3; overload++)
        {
            var scenario = new Scenario("time" + overload, new Astrodynamics.Mission.Mission("time501" + overload),
                window);
            scenario.AddCelestialItem(body);
            var spacecraft = Spacecraft(-5017 - overload, initial);
            scenario.AddSpacecraft(spacecraft);
            _ = overload switch
            {
                0 => await scenario.SimulateAsync(false, false, Step),
                1 => await scenario.SimulateAsync(false, false, Step, () => new RK78Integrator(1e-13, 1e-13)),
                _ => await scenario.SimulateAsync(false, false, Step, () => new RK78Integrator(1e-13, 1e-13),
                    new CovariancePropagationOptions())
            };

            var expected = (overload == 0 ? velocityVerlet : rk78).StateVectors;
            var actual = spacecraft.PropagatedStates.ToArray();
            Assert.Equal(expected.Count, actual.Length);
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i].Epoch, actual[i].Epoch);
                Assert.Equal(expected[i].Position, actual[i].Position);
            }

            if (aboutTheEarth)
            {
                Assert.Equal(window.EndDate.ToTDB(), actual[^1].Epoch);
            }
        }
    }

    [Fact]
    public void MonteCarlo_TheStatisticsAreAtTheLabelsOfThePropagation()
    {
        // Arrange: two runs from a LEO with a covariance, over a UTC hour
        var window = WindowOf("UTC");
        var initial = PropagatorsTestCovariance(Leo(window.StartDate.ToTDB()));
        var reference = Spacecraft(-5021, initial).Propagate(window, new CelestialItem[] { _earth },
            new RK78Integrator(1e-11, 1e-11), false, false, Step);
        var config = new MonteCarloConfiguration
        {
            RunCount = 2, Seed = 501, TemplateSpacecraft = Spacecraft(-5022, initial), Window = window,
            CelestialBodiesFactory = () => new CelestialItem[] { new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start) },
            DeltaT = Step, IntegratorFactory = () => new RK78Integrator(1e-11, 1e-11)
        };

        // Act
        var result = MonteCarloPropagator.Propagate(config);

        // Assert: the epochs of the statistics are the output labels, uniform in TT, the last the end of the window
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(reference.StateVectors.Select(s => s.Epoch), result.EpochStatistics.Select(s => s.Epoch));
        Assert.Equal(window.EndDate.ToTDB(), result.EpochStatistics[^1].Epoch);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ssb_ATdbTimeArgument(bool rk78)
    {
        // Arrange: a heliocentric orbit of 1 AU given relative to the solar system barycentre (SSB mode, the Sun as the
        // central body), over a UTC hour
        var window = WindowOf("UTC");
        var ssb = new Barycenter(0);
        var initial = new StateVector(new Vector3(1.496e11, 0.0, 0.0), new Vector3(0.0, 29780.0, 0.0), ssb,
            window.StartDate.ToTDB(), Frames.Frame.ICRF);
        var bodies = new CelestialItem[] { new CelestialBody(Stars.Sun), _earth };

        // Act
        var solution = Spacecraft(-5031, initial).Propagate(window, bodies,
            rk78 ? new RK78Integrator(10.0) : new VVIntegrator(TimeSpan.FromSeconds(10.0)), false, false, Step);

        // Assert
        Assert.Same(PropagationTimeArgument.Tdb, solution.TimeArgument);
        Assert.Equal(System.Math.Max(window.EndDate.ToTDB().TimeSpanFromJ2000().TotalSeconds -
                                     window.StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds, 3600.0),
            WindowEndAssert.IntegratedSeconds(solution), 9);
    }

    [Theory]
    [InlineData("ECLIPJ2000")]
    [InlineData("B1950")]
    public void InANonIcrfFrame_TheTimeArgumentIsTt(string frameName)
    {
        // Arrange: the LEO in the frame, over a UTC hour
        var window = WindowOf("UTC");
        var frame = new Frames.Frame(frameName);
        var icrf = Leo(window.StartDate.ToTDB());
        var initial = icrf.ToFrame(frame).ToStateVector();

        // Act
        var solution = Spacecraft(-5041, initial).Propagate(window, new CelestialItem[] { _earth },
            new RK78Integrator(1e-13, 1e-13), false, false, Step);

        // Assert: TT seconds, and Kepler over them in the inertial frame of the propagation
        double error = KeplerError(initial, solution, DynamicalTimeReference.TtSecondsBetween(window.StartDate,
            window.EndDate));
        _output.WriteLine($"MEASURE {frameName}: RK7(8) at 1e-13 against Kepler over the TT length {error:E3} m");
        Assert.Same(PropagationTimeArgument.Tt, solution.TimeArgument);
        Assert.Equal(frame, solution.StateVectors[^1].Frame);
        WindowEndAssert.ReachesTheEndOf(window, solution);
        Assert.True(error < KeplerBound, $"{error:E3} m from Kepler");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AManeuver_TheNextSegmentStartsAtTheEventTime(bool rk78)
    {
        // Arrange: 400 × 1000 km LEO from apogee, the apogee raised to 2000 km at the first perigee, over a UTC window of
        // 2 h
        var window = new TimeSystem.Window(Start.ToUTC(), Start.ToUTC().AddHours(2.0));
        var orbit = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), _earth,
            window.StartDate.ToTDB(), Frames.Frame.ICRF);
        var spacecraft = Spacecraft(-5051, orbit);
        var tank = new FuelTank("ta5051", "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("en5051", "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(_earth, window.StartDate, TimeSpan.Zero, 8378137.0,
            engine));

        // Act
        var solution = spacecraft.Propagate(window, new CelestialItem[] { _earth },
            rk78 ? new RK78Integrator(1e-11, 1e-11) : new VVIntegrator(TimeSpan.FromSeconds(10.0)), false, false,
            TimeSpan.FromSeconds(60.0));

        // Assert: the event time of the first segment, in TT seconds, carried to its label (OnEpochGrid), is the TT
        // seconds between the two bases, exactly; the segments together span the TT length of the window
        // seconds between the two bases to the rounding of the seconds (one ulp, 4.5e-13 s); Velocity-Verlet ends its
        // segment at the end of the step that holds the root, which the next base labels to the tick, as every label
        Assert.Equal(2, solution.Segments.Count);
        var before = solution.Segments[0];
        var after = solution.Segments[1];
        double eventTime = rk78 ? before.EventTime!.Value : before.Duration;
        double between = PropagationTimeArgument.Tt.SecondsBetween(before.BaseEpoch, after.BaseEpoch);
        _output.WriteLine($"{(rk78 ? "RK7(8)" : "Velocity-Verlet")}: event at {eventTime:R} s, bases {between:R} s " +
                          $"apart, {between - eventTime:E3} s");
        Assert.Equal(after.BaseEpoch, PropagationTimeArgument.Tt.EpochAt(before.BaseEpoch, eventTime));
        Assert.InRange(between - eventTime, rk78 ? -1e-12 : -1e-7, rk78 ? 1e-12 : 0.0);
        WindowEndAssert.ReachesTheEndOf(window, solution);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WindowsOfArbitraryTicks_EndOnTheirEndLabel_AndReadThereInsideATick(bool utc)
    {
        // Arrange: 20 windows of 10 min to 6 h from random ticks, one output step of their length, the shape of the
        // windows of the avoidance options and of the non-linearity index
        var random = new Random(501);
        var failures = new List<string>();
        for (int k = 0; k < 20; k++)
        {
            var start = Start.Add(TimeSpan.FromTicks(random.NextInt64(0, TimeSpan.TicksPerDay)));
            var end = start.Add(TimeSpan.FromTicks(random.NextInt64(6000L * TimeSpan.TicksPerSecond / 10,
                6L * TimeSpan.TicksPerHour)));
            var window = utc ? new TimeSystem.Window(start.ToUTC(), end.ToUTC()) : new TimeSystem.Window(start, end);
            var initial = Leo(window.StartDate.ToTDB());

            // Act
            var solution = Spacecraft(-5061 - k, initial).Propagate(window, new CelestialItem[] { _earth },
                new RK78Integrator(1e-11, 1e-11), false, false, window.Length);

            // Assert
            if (!WindowEndAssert.Reaches(window, solution) ||
                solution.StateVectors[^1].Epoch != window.EndDate.ToTDB())
            {
                failures.Add($"{window.StartDate} to {window.EndDate}: ends at {solution.End}");
                continue;
            }

            var (atEnd, _) = solution.InterpolateAt(window.EndDate);
            var (atLength, _) = solution.InterpolateAtElapsed(WindowEndAssert.LengthOf(window, solution));
            double apart = (atEnd - atLength).Magnitude();
            if (apart > initial.Velocity.Magnitude() * 1e-7)
            {
                failures.Add($"{window.StartDate} to {window.EndDate}: read at the end {apart:E3} m from the end");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void AConjunction_OfATwoBodyAndANumericalParticipant_AgreesWithKeplerOverTt()
    {
        // Arrange: the protected spacecraft propagated by RK7(8) about a point-mass Earth, the secondary not propagated,
        // two-body (AtEpoch), crossing it 2 000 s after the start of a UTC window; both given at the UTC start, so that
        // the reference counts the TT seconds between UTC epochs, exact in ticks (through a TDB epoch the series of the
        // reference and of the library depart by 1e-6 s here)
        double mu = _earth.GM;
        var window = WindowOf("UTC");
        var protectedInitial = Leo(window.StartDate);
        var (protectedAtMeeting, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity,
            2000.0);
        var radial = new Vector3(protectedAtMeeting[0], protectedAtMeeting[1], protectedAtMeeting[2]);
        var velocity = new Vector3(protectedAtMeeting[3], protectedAtMeeting[4], protectedAtMeeting[5]);
        var secondaryVelocity = velocity.Rotate(new IO.Astrodynamics.Math.Quaternion(radial.Normalize(), 0.7));
        var (secondaryStart, _) = KeplerianStm.Propagate(mu, radial + radial.Normalize() * 100.0, secondaryVelocity,
            -2000.0);
        var secondaryInitial = new StateVector(new Vector3(secondaryStart[0], secondaryStart[1], secondaryStart[2]),
            new Vector3(secondaryStart[3], secondaryStart[4], secondaryStart[5]), _earth, window.StartDate,
            Frames.Frame.ICRF);
        var protectedSpacecraft = Spacecraft(-5081, protectedInitial);
        var secondary = Spacecraft(-5082, secondaryInitial);
        protectedSpacecraft.Propagate(window, new CelestialItem[] { _earth }, new RK78Integrator(1e-13, 1e-13), false,
            false, TimeSpan.FromSeconds(10.0));

        // Act
        var encounter = IO.Astrodynamics.SSA.ConjunctionAssessment.Analyze(new IO.Astrodynamics.SSA.ProtectedSpacecraftProfile(protectedSpacecraft),
            secondary, window);

        // Assert: both states at the TCA are Kepler over the TT seconds from the start to it: the two-body one between
        // the UTC epochs, to the rounding; the numerical one between the TDB labels of the start, which holds the given
        // state, and of the TCA, at which the stored states of the spacecraft are read (choice 7 of #501: their sub-tick
        // residuals, here 6.2e-8 s apart, 0.47 mm, labels as on main), to the integration. In TDB seconds both would be
        // 1.2 mm off along their orbits
        double elapsed = DynamicalTimeReference.TtSecondsBetween(window.StartDate, encounter.EncounterState.Epoch);
        double labels = PropagationTimeArgument.Tt.SecondsBetween(window.StartDate.ToTDB(),
            encounter.EncounterState.Epoch.ToTDB());
        var (p, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity, labels);
        var (s, _) = KeplerianStm.Propagate(mu, secondaryInitial.Position, secondaryInitial.Velocity, elapsed);
        double protectedError = (encounter.ProtectedState.Position - new Vector3(p[0], p[1], p[2])).Magnitude();
        double secondaryError = (encounter.SecondaryState.Position - new Vector3(s[0], s[1], s[2])).Magnitude();
        _output.WriteLine($"MEASURE TCA {elapsed:F6} s: protected {protectedError:E3} m (labels {labels - elapsed:E3} s), " +
                          $"secondary {secondaryError:E3} m from Kepler over the TT seconds");
        Assert.True(protectedError < KeplerBound, $"protected {protectedError:E3} m");
        Assert.True(secondaryError < KeplerBound, $"secondary {secondaryError:E3} m");
    }

    [Fact]
    public void ANonPropagatedSpacecraft_ItsEphemerisIsTwoBodyOverTt()
    {
        // Arrange: a LEO given in UTC, read an hour later in UTC (between UTC epochs the reference is exact in ticks)
        var epoch = Start.ToUTC();
        var initial = Leo(Start);
        var utcInitial = new StateVector(initial.Position, initial.Velocity, _earth, epoch, Frames.Frame.ICRF);
        var spacecraft = Spacecraft(-5091, utcInitial);
        var later = epoch.AddHours(1.0);

        // Act
        var state = spacecraft.GetEphemeris(later, _earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        // Assert: the two-body motion (AtEpoch) over the TT seconds between the two epochs
        double elapsed = DynamicalTimeReference.TtSecondsBetween(epoch, later);
        var (k, _) = KeplerianStm.Propagate(_earth.GM, initial.Position, initial.Velocity, elapsed);
        double error = (state.Position - new Vector3(k[0], k[1], k[2])).Magnitude();
        _output.WriteLine($"MEASURE not propagated: {error:E3} m from Kepler over {elapsed:R} s");
        Assert.True(error < KeplerBound, $"{error:E3} m");
    }

    // An hour from 2021-03-20 12:00 TDB, by the tags of its ends; the UTC hour across the leap second of 2016-12-31
    private static TimeSystem.Window WindowOf(string tags)
    {
        var end = Start.AddHours(1.0);
        return tags switch
        {
            "TDB" => new TimeSystem.Window(Start, end),
            "UTC" => new TimeSystem.Window(Start.ToUTC(), end.ToUTC()),
            "TT" => new TimeSystem.Window(Start.ToTDT(), end.ToTDT()),
            "TAI" => new TimeSystem.Window(Start.ToTAI(), end.ToTAI()),
            "GPS" => new TimeSystem.Window(Start.ConvertTo(TimeFrame.GPSFrame), end.ConvertTo(TimeFrame.GPSFrame)),
            "Local" => new TimeSystem.Window(Start.ConvertTo(TimeFrame.LocalFrame), end.ConvertTo(TimeFrame.LocalFrame)),
            "TDB to UTC" => new TimeSystem.Window(Start, end.ToUTC()),
            "UTC leap second" => new TimeSystem.Window(new TimeSystem.Time(2016, 12, 31, 23, 30, 0, frame: TimeFrame.UTCFrame),
                new TimeSystem.Time(2017, 1, 1, 0, 30, 0, frame: TimeFrame.UTCFrame)),
            _ => throw new ArgumentOutOfRangeException(nameof(tags))
        };
    }

    private StateVector Leo(TimeSystem.Time epoch) =>
        new KeplerianElements(6778137.0, 0.0005, 51.6 * IO.Astrodynamics.Constants.Deg2Rad,
            20.0 * IO.Astrodynamics.Constants.Deg2Rad, 15.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, _earth, epoch,
            Frames.Frame.ICRF).ToStateVector();

    private StateVector Llo(TimeSystem.Time epoch) =>
        new KeplerianElements(1837400.0, 0.001, 0.5, 0.2, 0.3, 0.0, _moon, epoch, Frames.Frame.ICRF).ToStateVector();

    private static StateVector PropagatorsTestCovariance(StateVector state) =>
        CovariancePropagationTests.WithCovariance(state);

    private static Spacecraft Spacecraft(int naifId, StateVector state) =>
        new(naifId, "TIME" + -naifId, 100.0, 1000.0, new Clock("time" + -naifId, 65536), state, 1.0);

    // The integrator with its own forces, the point mass of the central body
    private static PropagationSolution WithItsForces(TimeSystem.Window window, Spacecraft spacecraft, CelestialBody body)
    {
        var integrator = new RK78Integrator(1e-13, 1e-13);
        integrator.AddForce(new GravitationalAcceleration(body));
        var propagator = new CentralBodyPropagator(window, spacecraft, integrator, Step);
        integrator.Initialize(spacecraft.InitialOrbitalParameters.ToStateVector());
        return propagator.Propagate();
    }

    // The distance of the last accepted state from Kepler after elapsed seconds
    private static double KeplerError(StateVector initial, PropagationSolution solution, double elapsed)
    {
        var (state, _) = KeplerianStm.Propagate(((CelestialItem)initial.Observer).GM, initial.Position, initial.Velocity,
            elapsed);
        return (solution.Segments[^1].Steps[^1].EndPosition - new Vector3(state[0], state[1], state[2])).Magnitude();
    }

    private static void AssertSameStates(PropagationSolution expected, PropagationSolution actual, string path)
    {
        Assert.Equal(expected.StateVectors.Count, actual.StateVectors.Count);
        for (int i = 0; i < expected.StateVectors.Count; i++)
        {
            Assert.True(expected.StateVectors[i].Epoch == actual.StateVectors[i].Epoch &&
                        expected.StateVectors[i].Position == actual.StateVectors[i].Position &&
                        expected.StateVectors[i].Velocity == actual.StateVectors[i].Velocity,
                $"{path}: state {i} {actual.StateVectors[i].Position} against {expected.StateVectors[i].Position}");
        }
    }
}
