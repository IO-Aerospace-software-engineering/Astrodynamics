// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

using Time = IO.Astrodynamics.TimeSystem.Time;
using OrbitalParams = IO.Astrodynamics.OrbitalParameters.OrbitalParameters;

/// <summary>
/// The records of the maneuvers a run executed, lot L5a of #668 (R09, decisions 57 and 59, decided by S. Guillet,
/// 2026-11-23): one sealed <see cref="ExecutedManeuver"/> per executed maneuver on <see cref="PropagationSolution.Maneuvers"/>,
/// in execution order (the definition, the epoch of the impulse or the activation of the attitude, the unclipped thrust
/// window, the maneuver window, the delta-V in the frame of the solution, zero for an attitude, the fuel burned and the
/// masses before and after), values a later run does not change; the summary of a scenario built from them; the
/// segment of an integrator outside the library built from its steps (decision 55).
/// </summary>
/// <remarks>
/// The references are the decided rules and independent ones: the definitions of the last run (the values the run writes on
/// them, decision 52), the segments of the solution (an impulse opens the next one, at its epoch, with its delta-V), the
/// rocket equation (<see cref="Tsiolkovski.DeltaM"/> of the mass before the burn), and the hull of the maneuver windows
/// by <see cref="Window.Merge"/>, the rule of the summary before 11.0.0. The case: a 7 000 km orbit, e = 0.01, i = 0.5
/// rad, about a point-mass Earth from 2024-03-01 12:00 TDB; 1 000 kg dry, 1 000 kg of fuel, Isp 300 s, 10 kg/s; a nadir
/// attitude held 600 s, an apogee raise to 8 000 km, a prograde attitude, a perigee raise to 7 100 km, over 3 h.
/// </remarks>
public class ExecutedManeuverRecordTests
{
    public ExecutedManeuverRecordTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static readonly Window Window3H = new(S, S.AddHours(3.0));
    private static int _nextId = -869000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, S);

    private static OrbitalParams Orbit(CelestialBody earth, string frame)
    {
        var icrf = new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF);
        switch (frame)
        {
            case "ECLIPJ2000":
                return icrf.ToStateVector().ToFrame(Frames.Frame.ECLIPTIC_J2000);
            case "SSB":
                var sv = icrf.ToStateVector();
                var fromSsb = earth.GetGeometricStateRelativeTo(S, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                return new StateVector(fromSsb.Position + sv.Position, fromSsb.Velocity + sv.Velocity,
                    Barycenters.SOLAR_SYSTEM_BARYCENTER, S, Frames.Frame.ICRF);
            default:
                return icrf;
        }
    }

    private sealed record Case(Spacecraft Spacecraft, Engine Engine, Maneuver.Maneuver[] Plan, CelestialBody Earth, string Frame);

    private static Case Planned(string frame = "ICRF", CelestialBody earth = null)
    {
        earth ??= Earth();
        int id = NextId();
        var spacecraft = new Spacecraft(id, "EM" + -id, 1000.0, Orbit(earth, frame), 3000.0, new Clock("EM" + -id, 65536));
        var tank = new FuelTank("ft" + -id, 1000.0, 1000.0, "m", "sn" + -id);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng" + -id, 300.0, 10.0, tank, "m", "sn" + -id);
        spacecraft.AddEngine(engine);
        Maneuver.Maneuver[] plan =
        [
            new NadirAttitude(earth, S, TimeSpan.FromSeconds(600.0)),
            new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine),
            new ProgradeAttitude(earth, S, TimeSpan.Zero),
            new PerigeeHeightManeuver(earth, S, TimeSpan.Zero, 7100e3, engine)
        ];
        spacecraft.SetManeuvers(plan);
        return new Case(spacecraft, engine, plan, earth, frame);
    }

    private static PropagationOptions Options(Case c, string integrator = "RK") => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
        OutputStep = Output,
        ThirdBodies = c.Frame == "SSB" ? [c.Earth, new CelestialBody(Stars.Sun, Frames.Frame.ICRF, S)] : [c.Earth],
        CovariancePropagation = CovariancePropagationOptions.None
    };

    // The values of a record, for a comparison after a later run
    private static (Vector3, double, double, double, Window, Window, Time) Values(ExecutedManeuver r) =>
        (r.DeltaV, r.FuelBurned, r.MassBefore, r.MassAfter, r.ThrustWindow, r.ManeuverWindow, r.Epoch);

    private static void AssertSameValues(IReadOnlyList<(Vector3, double, double, double, Window, Window, Time)> expected,
        IReadOnlyList<ExecutedManeuver> records)
    {
        Assert.Equal(expected.Count, records.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            var (dv, fuel, before, after, thrust, window, epoch) = expected[i];
            Assert.Equal(dv, records[i].DeltaV);
            Assert.Equal(fuel, records[i].FuelBurned);
            Assert.Equal(before, records[i].MassBefore);
            Assert.Equal(after, records[i].MassAfter);
            LabelAssert.Equal(thrust, records[i].ThrustWindow);
            LabelAssert.Equal(window, records[i].ManeuverWindow);
            LabelAssert.Equal(epoch, records[i].Epoch);
        }
    }

    /// <summary>
    /// The records of a run against the definitions as the run left them, the segments of its solution and the rocket
    /// equation.
    /// </summary>
    private static void AssertRecords(Case c, PropagationSolution solution, IReadOnlyList<ExecutedManeuver> records,
        double initialMass)
    {
        // Every maneuver of the plan, in execution order, those the spacecraft lists
        Assert.Equal(c.Plan, records.Select(r => r.Definition));
        Assert.Equal(c.Spacecraft.ExecutedManeuvers, records.Select(r => r.Definition));

        double mass = initialMass;
        int impulses = 0;
        foreach (var record in records)
        {
            var definition = record.Definition;
            LabelAssert.Equal(definition.ThrustWindow!.Value, record.ThrustWindow);
            LabelAssert.Equal(definition.ManeuverWindow!.Value, record.ManeuverWindow);
            Assert.Equal(definition.FuelBurned, record.FuelBurned);
            Assert.Equal(TimeScale.TDB, record.Epoch.Scale);
            Assert.Equal(mass, record.MassBefore);
            if (definition is ImpulseManeuver impulse)
            {
                impulses++;
                Assert.Equal(impulse.DeltaV, record.DeltaV);
                Assert.Equal(Tsiolkovski.DeltaM(c.Engine.ISP, record.MassBefore, record.DeltaV.Magnitude()), record.FuelBurned);
                Assert.True(record.FuelBurned > 0.0);
                Assert.Equal(record.MassBefore - record.FuelBurned, record.MassAfter, 9);

                // The impulse opens the next segment, at its epoch, with its delta-V, in the frame of the solution
                var before = solution.Segments[impulses - 1];
                var after = solution.Segments[impulses];
                LabelAssert.Equal(after.BaseEpoch, record.Epoch);
                // The velocity before it: at the event time of RK7(8), whose last step may run past it (#574), at the end
                // of the last step of Velocity-Verlet, integrated again to the event (#572)
                var velocityBefore = solution.Dynamics != null && before.EventTime is { } eventTime
                    ? solution.Dynamics.StateAt(before, eventTime).Item2
                    : before.Steps[^1].EndVelocity;
                var jump = after.Steps[0].StartVelocity - velocityBefore;
                Assert.True((jump - record.DeltaV).Magnitude() < 1e-9, $"jump {jump}, delta-V {record.DeltaV}");
            }
            else
            {
                // An attitude: its activation, no delta-V, no fuel
                LabelAssert.Equal(definition.ManeuverWindow.Value.StartDate, record.Epoch);
                Assert.Equal(Vector3.Zero, record.DeltaV);
                Assert.Equal(0.0, record.FuelBurned);
                Assert.Equal(record.MassBefore, record.MassAfter);
            }

            Assert.True(record.Epoch >= solution.Window.StartDate && record.Epoch <= solution.Window.EndDate);
            mass = record.MassAfter;
        }

        Assert.Equal(2, impulses);
        Assert.Equal(c.Spacecraft.GetTotalMass(), mass);
        // The fuel accessors: the initial fuel of the spacecraft's tanks, and what the run left in them
        Assert.Equal(1000.0, c.Spacecraft.GetInitialFuel());
        Assert.Equal(c.Engine.FuelTank.Quantity, c.Spacecraft.GetRemainingFuel());
        Assert.Equal(1000.0 - records.Sum(r => r.FuelBurned), c.Spacecraft.GetRemainingFuel(), 9);
    }

    [Theory]
    [InlineData("RK", "ICRF")]
    [InlineData("VV", "ICRF")]
    [InlineData("RK", "ECLIPJ2000")]
    [InlineData("VV", "ECLIPJ2000")]
    [InlineData("RK", "SSB")]
    [InlineData("VV", "SSB")]
    public void SpacecraftPropagate_RecordsEveryExecutedManeuver(string integrator, string frame)
    {
        var c = Planned(frame);
        double initialMass = c.Spacecraft.GetTotalMass();

        var solution = c.Spacecraft.Propagate(Window3H, Options(c, integrator));

        AssertRecords(c, solution, solution.Maneuvers, initialMass);
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void TheRecordsOfASolution_DoNotChangeWithALaterRun_OfOnePropagator(string integrator)
    {
        var c = Planned();
        double initialMass = c.Spacecraft.GetTotalMass();
        var propagator = new CentralBodyPropagator(Window3H, c.Spacecraft, Options(c, integrator));

        var first = propagator.Propagate();
        AssertRecords(c, first, first.Maneuvers, initialMass);
        var values = first.Maneuvers.Select(Values).ToArray();

        // The same instance again: a fresh run, the same records in a list of its own
        var second = propagator.Propagate();
        AssertRecords(c, second, second.Maneuvers, initialMass);
        AssertSameValues(values, second.Maneuvers);
        Assert.NotSame(first.Maneuvers, second.Maneuvers);

        // A run that reaches no burn: the definitions are reset, the records of the first run keep their values
        c.Spacecraft.Propagate(new Window(S, S.AddSeconds(300.0)), Options(c, integrator));
        Assert.Null(c.Plan[1].ThrustWindow);
        Assert.Equal(Vector3.Zero, ((ImpulseManeuver)c.Plan[1]).DeltaV);
        AssertSameValues(values, first.Maneuvers);
        Assert.Same(c.Plan[1], first.Maneuvers[1].Definition);
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void ABatch_RecordsEveryExecutedManeuver(string integrator)
    {
        var c = Planned();
        double initialMass = c.Spacecraft.GetTotalMass();

        var result = BatchPropagator.Propagate([new PropagationTask(c.Spacecraft, Window3H, Options(c, integrator))]).Results.Single();

        Assert.True(result.Succeeded);
        AssertRecords(c, result.Solution, result.Solution.Maneuvers, initialMass);
    }

    /// <summary>
    /// Decision 59: the summary of a scenario is built from the records of the run: its maneuvers are the records, its
    /// fuel consumption their sum and its maneuver window their hull, as the definitions gave them before 11.0.0 (the
    /// hull of <see cref="Window.Merge"/>), and a later run leaves them as they were.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void TheSummaryOfAScenario_IsBuiltFromTheRecords(string integrator)
    {
        var c = Planned();
        double initialMass = c.Spacecraft.GetTotalMass();
        var scenario = new Scenario("EM", new IO.Astrodynamics.Mission.Mission("EM"), Window3H);
        scenario.AddCelestialItem(c.Earth);
        scenario.AddSpacecraft(c.Spacecraft);

        var summary = scenario.Simulate(Options(c, integrator) with { ThirdBodies = null }).SpacecraftSummaries.Single();

        Assert.Same(c.Spacecraft, summary.Spacecraft);
        AssertRecords(c, c.Spacecraft.LastPropagation ?? CentralBodyPropagatorRun(c, integrator), summary.Maneuvers, initialMass);
        if (c.Spacecraft.LastPropagation is { } kept)
        {
            Assert.Equal(kept.Maneuvers, summary.Maneuvers);
        }

        // The rule before 11.0.0, over the definitions the run left: the sum of the fuel, the hull of the windows
        Assert.Equal(c.Plan.Sum(m => m.FuelBurned), summary.FuelConsumption);
        var hull = c.Plan[0].ManeuverWindow!.Value;
        foreach (var maneuver in c.Plan.Skip(1))
        {
            hull = hull.Merge(maneuver.ManeuverWindow!.Value);
        }

        LabelAssert.Equal(hull, summary.ManeuverWindow!.Value);

        // A later run that reaches no burn: the summary keeps its values
        var values = summary.Maneuvers.Select(Values).ToArray();
        double consumption = summary.FuelConsumption;
        c.Spacecraft.Propagate(new Window(S, S.AddSeconds(300.0)), Options(c, integrator));
        AssertSameValues(values, summary.Maneuvers);
        Assert.Equal(consumption, summary.FuelConsumption);
        LabelAssert.Equal(hull, summary.ManeuverWindow!.Value);
    }

    // A Velocity-Verlet run keeps no solution on the spacecraft: the segments of the same run, by a fresh spacecraft
    private static PropagationSolution CentralBodyPropagatorRun(Case c, string integrator)
    {
        var twin = Planned(c.Frame, c.Earth);
        return twin.Spacecraft.Propagate(Window3H, Options(twin, integrator));
    }

    [Fact]
    public void AScenarioSpacecraftWithoutManeuvers_HasAnEmptySummary()
    {
        var earth = Earth();
        int id = NextId();
        var spacecraft = new Spacecraft(id, "EM" + -id, 1000.0, Orbit(earth, "ICRF"), 3000.0, new Clock("EM" + -id, 65536));
        var scenario = new Scenario("EM", new IO.Astrodynamics.Mission.Mission("EM"), new Window(S, S.AddHours(1.0)));
        scenario.AddCelestialItem(earth);
        scenario.AddSpacecraft(spacecraft);

        var summary = scenario.Simulate(new PropagationOptions { CovariancePropagation = CovariancePropagationOptions.None })
            .SpacecraftSummaries.Single();

        Assert.Empty(summary.Maneuvers);
        Assert.Null(summary.ManeuverWindow);
        Assert.Equal(0.0, summary.FuelConsumption);
    }

    [Fact]
    public void ATLEPropagatorSolutionAndOneBuiltByTheLibrary_HaveNoRecords()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        int id = NextId();
        var spacecraft = new Spacecraft(id, "EM" + -id, 100.0, tle, 10000.0, new Clock("EM" + -id, 65536));

        var solution = new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Output).Propagate();

        Assert.Empty(solution.Maneuvers);
        Assert.Empty(new PropagationSolution().Maneuvers);
    }

    [Fact]
    public void ExecutedManeuver_IsASealedRecordOfGetOnlyValues()
    {
        var type = typeof(ExecutedManeuver);
        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
        Assert.Equal(
            new[] { "Definition", "Epoch", "ThrustWindow", "ManeuverWindow", "DeltaV", "FuelBurned", "MassBefore", "MassAfter" }
                .OrderBy(n => n, StringComparer.Ordinal),
            type.GetProperties().Where(p => p.DeclaringType == type && p.Name != "EqualityContract").Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 55: an integrator outside the library builds its segment from its steps
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePublicSegmentConstructor_CopiesTheSteps()
    {
        var a = new AcceptedStep(0.0, 10.0, new Vector3(7000e3, 0.0, 0.0), new Vector3(0.0, 7500.0, 0.0),
            new Vector3(7000e3, 75000.0, 0.0), new Vector3(-80.0, 7500.0, 0.0), new Vector3(-8.0, 0.0, 0.0),
            new Vector3(-8.0, -0.1, 0.0));
        var b = a with { CumulativeTime = 10.0 };
        var steps = new List<AcceptedStep> { a, b };

        var segment = new PropagationSegment(S, steps);
        steps.Add(a);
        steps[0] = b;

        Assert.Equal(new[] { a, b }, segment.Steps);
        LabelAssert.Equal(S, segment.BaseEpoch);
        Assert.Equal(20.0, segment.Duration);
        Assert.Empty(new PropagationSegment(S).Steps);
        Assert.Throws<ArgumentNullException>(() => new PropagationSegment(S, null));
    }
}
