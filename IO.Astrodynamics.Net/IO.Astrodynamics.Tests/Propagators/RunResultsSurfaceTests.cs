// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The results of a run, lot L5a of #668 (R09, decisions 54 to 60, decided by S. Guillet, 2026-11-23): what a caller can
/// no longer change on a published solution (<see cref="PropagationSegment"/> immutable from outside, read-only
/// <c>Steps</c>, <c>Segments</c> and <c>StateVectors</c>, the solution's constructor and mutators internal,
/// <c>Integrator.UpdateStateVector</c> internal, #796), <see cref="PropagationSolution.CovariancePropagation"/>
/// <see cref="CovariancePropagationOptions.None"/> rather than null, <see cref="Spacecraft.ExecutedManeuvers"/> a new
/// read-only list per run, the summary of a scenario built from the records, and the fuel accessors (#801).
/// </summary>
/// <remarks>
/// The references are the decided rules. The case of the runs: a 7 000 km orbit, e = 0.01, i = 0.5 rad, about a
/// point-mass Earth from 2024-03-01 12:00 TDB; 1 000 kg dry, 1 000 kg of fuel, Isp 300 s, 10 kg/s; an apogee raise to
/// 8 000 km at the first periapsis after the start.
/// </remarks>
public class RunResultsSurfaceTests
{
    public RunResultsSurfaceTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static int _nextId = -868000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private const BindingFlags Everything = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static CelestialBody Earth() => new(PlanetsAndMoons.Earth, Frames.Frame.ICRF, S);

    private static Spacecraft NewSpacecraft(CelestialBody earth, out Engine engine)
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "RR" + -id, 1000.0, new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF), 3000.0,
            new Clock("RR" + -id, 65536));
        var tank = new FuelTank("ft" + -id, 1000.0, 1000.0, "m", "sn" + -id);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("eng" + -id, 300.0, 10.0, tank, "m", "sn" + -id);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    private static PropagationOptions Options(CelestialBody earth, string integrator = "RK",
        CovariancePropagationOptions covariance = null) => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
        OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = covariance ?? CovariancePropagationOptions.None
    };

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 55: a published solution cannot be changed from outside (#796)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void PropagationSegment_IsImmutableFromOutside()
    {
        var type = typeof(PropagationSegment);
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(c => string.Join(",", c.GetParameters().Select(p => p.ParameterType)))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            typeof(Time).ToString(),
            typeof(Time) + "," + typeof(IEnumerable<AcceptedStep>)
        }.OrderBy(s => s, StringComparer.Ordinal), constructors);

        foreach (var name in new[] { "AddStep", "InterpolateAt", "HermiteInterpolate" })
        {
            var method = type.GetMethod(name, Everything);
            Assert.NotNull(method);
            Assert.True(method.IsAssembly, name);
        }

        Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            m => m.DeclaringType == type && !m.IsSpecialName && m.ReturnType == typeof(void));
        Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.False(p.SetMethod?.IsPublic ?? false, p.Name));
    }

    [Fact]
    public void PropagationSolution_HasNoPublicConstructorOrMutator()
    {
        var type = typeof(PropagationSolution);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        foreach (var name in new[] { "SetOutputStates", "AddSegment", "InterpolateAt" })
        {
            var method = type.GetMethod(name, Everything);
            Assert.NotNull(method);
            Assert.True(method.IsAssembly, name);
        }

        Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.False(p.SetMethod?.IsPublic ?? false, p.Name));
    }

    [Fact]
    public void IntegratorUpdateStateVector_IsInternal()
    {
        var method = typeof(Integrator).GetMethod("UpdateStateVector", Everything);
        Assert.NotNull(method);
        Assert.True(method.IsAssembly);
        Assert.False(method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic);
    }

    // The collection a getter returns cannot be written through a cast: not the inner list or array, and its IList
    // indexer and ICollection.Add throw NotSupportedException
    private static void AssertReadOnly<T>(IReadOnlyList<T> collection)
    {
        Assert.IsNotType<List<T>>(collection);
        Assert.IsNotType<T[]>(collection);
        var list = Assert.IsAssignableFrom<IList<T>>(collection);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = list[0]);
        Assert.Throws<NotSupportedException>(() => list.Add(list[0]));
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void TheCollectionsOfASolution_AreReadOnly(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out _);
        var solution = spacecraft.Propagate(new Window(S, S.AddHours(1.0)), Options(earth, integrator));

        AssertReadOnly(solution.Segments);
        AssertReadOnly(solution.StateVectors);
        AssertReadOnly(solution.Segments[0].Steps);
        // Each getter returns the one wrapper of its collection, allocated once
        Assert.Same(solution.Segments, solution.Segments);
        Assert.Same(solution.StateVectors, solution.StateVectors);
        Assert.Same(solution.Segments[0].Steps, solution.Segments[0].Steps);
    }

    [Fact]
    public void TheCollectionsOfATLEPropagatorSolution_AreReadOnly()
    {
        var tle = new Tle("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        int id = NextId();
        var spacecraft = new Spacecraft(id, "RR" + -id, 100.0, tle, 10000.0, new Clock("RR" + -id, 65536));
        var solution = new TlePropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Output).Propagate();

        AssertReadOnly(solution.Segments);
        AssertReadOnly(solution.StateVectors);
        AssertReadOnly(solution.Segments[0].Steps);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 56: CovariancePropagation is None, never null
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void ASolutionWithoutVariationalEquations_HasCovariancePropagationNone(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out _);
        var solution = spacecraft.Propagate(new Window(S, S.AddHours(1.0)), Options(earth, integrator));

        Assert.Equal(CovariancePropagationOptions.None, solution.CovariancePropagation);
        Assert.Equal(CovariancePropagationMode.None, solution.CovariancePropagation.Mode);
    }

    [Fact]
    public void ASolutionWithTheStateTransitionMatrix_HasItsOptions()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out _);
        var stm = new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly };
        var solution = spacecraft.Propagate(new Window(S, S.AddHours(1.0)), Options(earth, "RK", stm));

        Assert.Same(stm, solution.CovariancePropagation);
    }

    [Fact]
    public void ATLEPropagatorSolutionAndOneBuiltByTheLibrary_HaveCovariancePropagationNone()
    {
        var tle = new Tle("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        int id = NextId();
        var spacecraft = new Spacecraft(id, "RR" + -id, 100.0, tle, 10000.0, new Clock("RR" + -id, 65536));
        var solution = new TlePropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Output).Propagate();

        Assert.Equal(CovariancePropagationOptions.None, solution.CovariancePropagation);
        // A solution built by the library outside a propagator (the conformance runner's straight lines)
        Assert.Equal(CovariancePropagationOptions.None, new PropagationSolution().CovariancePropagation);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 58: ExecutedManeuvers is a new read-only list per run
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void AHeldExecutedManeuversList_KeepsItsMembers_AfterTheNextRun(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var apogee = new ApogeeHeightManeuver(8000e3, engine, S.AddSeconds(600.0), TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(apogee);
        spacecraft.Propagate(new Window(S, S.AddHours(3.0)), Options(earth, integrator));
        var held = spacecraft.ExecutedManeuvers;
        Assert.Same(apogee, Assert.Single(held));

        // A run of 300 s that does not reach the burn: the spacecraft lists nothing, the held list its run's burn
        spacecraft.Propagate(new Window(S, S.AddSeconds(300.0)), Options(earth, integrator));

        Assert.Empty(spacecraft.ExecutedManeuvers);
        Assert.Same(apogee, Assert.Single(held));
        Assert.NotSame(held, spacecraft.ExecutedManeuvers);
        Assert.IsAssignableFrom<IReadOnlyList<Maneuver.Maneuver>>(held);
        var collection = Assert.IsAssignableFrom<ICollection<Maneuver.Maneuver>>(held);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(apogee));
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    [Fact]
    public void ExecutedManeuvers_IsAReadOnlyList()
    {
        Assert.Equal(typeof(IReadOnlyList<Maneuver.Maneuver>),
            typeof(Spacecraft).GetProperty(nameof(Spacecraft.ExecutedManeuvers))!.PropertyType);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decisions 57 and 59: the records on the solution, the summary built from them
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheRecordsAndTheSummary_HaveTheirDecidedShape()
    {
        var record = typeof(PropagationSolution).Assembly.GetType("IO.Astrodynamics.Propagator.ExecutedManeuver");
        Assert.NotNull(record);
        Assert.True(record.IsSealed);
        Assert.NotNull(record.GetMethod("<Clone>$", Everything));
        Assert.Equal(typeof(IReadOnlyList<>).MakeGenericType(record),
            typeof(PropagationSolution).GetProperty("Maneuvers")?.PropertyType);

        var summary = typeof(SpacecraftSummary);
        Assert.True(summary.IsSealed);
        Assert.Empty(summary.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(IReadOnlyList<>).MakeGenericType(record), summary.GetProperty("Maneuvers")!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<SpacecraftSummary>),
            typeof(ScenarioSummary).GetProperty(nameof(ScenarioSummary.SpacecraftSummaries))!.PropertyType);
    }

    [Fact]
    public void TheSummariesOfAScenario_AreInScenarioOrder()
    {
        var earth = Earth();
        var scenario = new Scenario("RR", new IO.Astrodynamics.Mission.Mission("RR"), new Window(S, S.AddHours(1.0)));
        scenario.AddCelestialItem(earth);
        for (int i = 0; i < 3; i++)
        {
            scenario.AddSpacecraft(NewSpacecraft(earth, out _));
        }

        var summary = scenario.Simulate(Options(earth) with { ThirdBodies = null });

        Assert.Equal(scenario.Spacecrafts, summary.SpacecraftSummaries.Select(s => s.Spacecraft));
    }

    /// <summary>
    /// The maneuver window of a summary is the hull that <see cref="Window.Hull"/> folded over the windows of the records,
    /// in execution order, gave before 11.0.0, to the label (decision 59): a later window that starts earlier moves the
    /// start, and on ends that are one instant tagged in different scales the later window's label is kept. Records out of
    /// order, the ends of the second and third one instant each in TAI and TT (TT = TAI + 32.184 s exactly), and the fuel
    /// the sum of the records'.
    /// </summary>
    [Fact]
    public void TheManeuverWindowOfASummary_IsTheHullOfWindowMerge_ToTheLabel()
    {
        var tt = TimeSpan.FromSeconds(32.184);
        var first = new Window(S.AddSeconds(600.0), S.AddSeconds(900.0));
        var earlierStart = new Window(S.AddSeconds(100.0).ToTai(), S.AddSeconds(1200.0).ToTai());
        var tiedEnds = new Window(new Time(earlierStart.StartDate.DateTime + tt, TimeScale.TT),
            new Time(earlierStart.EndDate.DateTime + tt, TimeScale.TT));
        Assert.True(tiedEnds.StartDate == earlierStart.StartDate && !tiedEnds.StartDate.EqualsExact(earlierStart.StartDate));
        Assert.True(tiedEnds.EndDate == earlierStart.EndDate && !tiedEnds.EndDate.EqualsExact(earlierStart.EndDate));
        var windows = new[] { first, earlierStart, tiedEnds };
        var fuels = new[] { 12.5, 0.25, 3.0 };
        var records = windows.Select((w, i) => new ExecutedManeuver(null!, w.StartDate, w, w, IO.Astrodynamics.Math.Vector3.Zero, fuels[i], 0.0, 0.0)).ToArray();

        var summary = new SpacecraftSummary(null!, records);

        var expected = windows.Skip(1).Aggregate(windows[0], (hull, w) => hull.Hull(w));
        Assert.True(expected.StartDate.EqualsExact(tiedEnds.StartDate) && expected.EndDate.EqualsExact(tiedEnds.EndDate));
        Assert.NotNull(summary.ManeuverWindow);
        Assert.True(summary.ManeuverWindow.Value.StartDate.EqualsExact(expected.StartDate), $"{summary.ManeuverWindow.Value.StartDate}");
        Assert.True(summary.ManeuverWindow.Value.EndDate.EqualsExact(expected.EndDate), $"{summary.ManeuverWindow.Value.EndDate}");
        Assert.Equal(12.5 + 0.25 + 3.0, summary.FuelConsumption);
        Assert.Same(records, summary.Maneuvers);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 60: the fuel accessors, and GetTotalIsp and GetTotalFuelFlow over every engine (lot L5b, after the tanks
    // refuse a non-finite quantity, #800: L5-20 as amended, #801 part 1)
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// GetTotalIsp and GetTotalFuelFlow count every engine, whatever its tank holds (decision 60 of #668, L5-20 as
    /// amended, #801 part 1): they counted only the engines whose tank held fuel at the start of a run, which made them
    /// 0 / 0 when none did. The empty tank is set by reflection, the constructor refusing an initial quantity of 0 in this
    /// lot: the analog of L5-20, engines of 300 s at 10 kg/s and 200 s at 30 kg/s, 300 s with the filter, 225 s without.
    /// </summary>
    [Fact]
    public void GetTotalISP_AndGetTotalFuelFlow_CountEveryEngine()
    {
        var spacecraft = NewSpacecraft(Earth(), out _);
        var empty = new FuelTank("ftE", 1000.0, 500.0, "m", "snE");
        spacecraft.AddFuelTank(empty);
        spacecraft.AddEngine(new Engine("engE", 200.0, 30.0, empty, "m", "snE"));
        typeof(FuelTank).GetField("<InitialQuantity>k__BackingField", Everything)!.SetValue(empty, 0.0);

        Assert.Equal(40.0, spacecraft.GetTotalFuelFlow());
        Assert.Equal(225.0, spacecraft.GetTotalIsp(), 1e-12);
    }

    /// <summary>
    /// Without an engine, GetTotalIsp is 0 / 0, NaN, as before (#801 part 1 changes the filter only; IC-12 of the gap
    /// analysis of L5b), and GetTotalFuelFlow 0.
    /// </summary>
    [Fact]
    public void GetTotalISP_WithoutAnEngine_IsNaN()
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "RR" + -id, 1000.0, new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, Earth(), S, Frames.Frame.ICRF), 3000.0,
            new Clock("RR" + -id, 65536));

        Assert.Equal(0.0, spacecraft.GetTotalFuelFlow());
        Assert.True(double.IsNaN(spacecraft.GetTotalIsp()));
    }

    [Fact]
    public void TheFuelAccessors_ReplaceGetTotalFuel()
    {
        var type = typeof(Spacecraft);
        Assert.Null(type.GetMethod("GetTotalFuel", Everything));
        Assert.Equal(typeof(double), type.GetMethod("GetInitialFuel", Type.EmptyTypes)?.ReturnType);
        Assert.Equal(typeof(double), type.GetMethod("GetRemainingFuel", Type.EmptyTypes)?.ReturnType);
    }
}
