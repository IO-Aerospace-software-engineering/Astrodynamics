// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
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

using Time = IO.Astrodynamics.TimeSystem.Time;
using OrbitalParams = IO.Astrodynamics.OrbitalParameters.OrbitalParameters;

/// <summary>
/// The maneuver plan, lot L5a of #668 (R08, decisions 45 to 48, decided by S. Guillet, 2026-11-23):
/// <see cref="Spacecraft.SetManeuvers"/> links the plan in order and is the one way to link it, read back by
/// <see cref="Spacecraft.Maneuvers"/>; it claims each maneuver for its spacecraft and throws
/// <see cref="ArgumentException"/> for a maneuver of another spacecraft's plan, a duplicate, an engine whose tank is not
/// the spacecraft's or a target frame that differs, the claim released when the owner sets another plan; the run re-checks
/// with <see cref="InvalidOperationException"/>; the members of the former two-step linking are gone and the hierarchy is
/// closed.
/// </summary>
/// <remarks>
/// The references are the decided rules (links, exceptions, what changes) and, for the activation paths, the run of a
/// fresh spacecraft by <c>Spacecraft.Propagate</c> (every run a fresh run, decision 22 of lot L4), compared bit for bit.
/// The case: a 7 000 km orbit, e = 0.01, i = 0.5 rad, about a point-mass Earth from 2024-03-01 12:00 TDB; 1 000 kg dry,
/// 1 000 kg of fuel, Isp 300 s, 10 kg/s.
/// </remarks>
public class ManeuverPlanTests
{
    public ManeuverPlanTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static int _nextId = -845000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, S);

    private static Spacecraft NewSpacecraft(CelestialBody earth, out Engine engine, OrbitalParams orbit = null)
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "PLAN" + -id, 1000.0, orbit ?? new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF), 3000.0,
            new Clock("PLAN" + -id, 65536));
        var tank = new FuelTank("ft" + -id, 1000.0, 1000.0, "m", "sn" + -id);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("eng" + -id, 300.0, 10.0, tank, "m", "sn" + -id);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    private static PropagationOptions Options(CelestialBody earth, string integrator = "RK") => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
        OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None
    };

    // ---------------------------------------------------------------------------------------------------------------------
    // Links and read-back
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SetManeuvers_LinksThePlanInOrder_AndReadsItBack()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var nadir = new NadirAttitude(S, TimeSpan.FromSeconds(600.0), earth);
        var apogee = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        var prograde = new ProgradeAttitude(S, TimeSpan.Zero, earth);
        Assert.Empty(spacecraft.Maneuvers);

        spacecraft.SetManeuvers(nadir, apogee, prograde);

        Assert.Equal(new Maneuver.Maneuver[] { nadir, apogee, prograde }, spacecraft.Maneuvers);
        Assert.Same(apogee, nadir.NextManeuver);
        Assert.Same(prograde, apogee.NextManeuver);
        Assert.Null(prograde.NextManeuver);
        Assert.Same(nadir, spacecraft.StandbyManeuver);
        Assert.Empty(spacecraft.ExecutedManeuvers);
        // The plan cannot be changed through the list it returns
        Assert.False(spacecraft.Maneuvers is Maneuver.Maneuver[]);
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<Maneuver.Maneuver>)spacecraft.Maneuvers)[0] = prograde);
    }

    [Fact]
    public void SetManeuvers_TheCallersArray_IsCopied()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var a = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        var b = new PerigeeHeightManeuver(7100e3, engine, S, TimeSpan.Zero, earth);
        var array = new Maneuver.Maneuver[] { a, b };
        spacecraft.SetManeuvers(array);
        array[0] = b;
        Assert.Equal(new Maneuver.Maneuver[] { a, b }, spacecraft.Maneuvers);
    }

    [Fact]
    public void SetManeuvers_Nothing_ClearsThePlan_AndReleasesItsManeuvers()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var a = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        var b = new NadirAttitude(S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(a, b);

        spacecraft.SetManeuvers();

        Assert.Empty(spacecraft.Maneuvers);
        Assert.Null(spacecraft.StandbyManeuver);
        Assert.Null(a.NextManeuver);
        Assert.Null(a.PlanOwner);
        Assert.Null(b.PlanOwner);
        // The next propagation fires nothing
        spacecraft.Propagate(new Window(S, S.AddHours(3.0)), Options(earth));
        Assert.Empty(spacecraft.ExecutedManeuvers);
        Assert.Null(a.ThrustWindow);
        Assert.Equal(1000.0, spacecraft.FuelTanks.Single().Quantity);
    }

    [Fact]
    public void SetManeuvers_AnotherPlan_RelinksAndReleasesWhatItDropped()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var a = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        var b = new PerigeeHeightManeuver(7100e3, engine, S, TimeSpan.Zero, earth);
        var c = new NadirAttitude(S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(a, b, c);

        spacecraft.SetManeuvers(c, a);

        Assert.Equal(new Maneuver.Maneuver[] { c, a }, spacecraft.Maneuvers);
        Assert.Same(a, c.NextManeuver);
        Assert.Null(a.NextManeuver);
        Assert.Null(b.NextManeuver);
        Assert.Null(b.PlanOwner);
        Assert.Same(spacecraft, a.PlanOwner);
        Assert.Same(spacecraft, c.PlanOwner);
    }

    [Fact]
    public void SetManeuvers_Null_Throws()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var a = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(a);

        var e = Assert.Throws<ArgumentNullException>(() => spacecraft.SetManeuvers(null));
        Assert.Equal("maneuvers", e.ParamName);
        var n = Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(a, null));
        Assert.Equal("maneuvers", n.ParamName);
        Assert.Equal(new Maneuver.Maneuver[] { a }, spacecraft.Maneuvers);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The claim and its ArgumentException cases (decision 46)
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("a,a")]
    [InlineData("a,b,a")]
    [InlineData("n,a,n")]
    public void SetManeuvers_ADuplicate_Throws_AndChangesNothing(string list)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var a = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        var b = new PerigeeHeightManeuver(7100e3, engine, S, TimeSpan.Zero, earth);
        var n = new NadirAttitude(S, TimeSpan.Zero, earth);
        var former = new ProgradeAttitude(S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(former);
        var plan = list.Split(',').Select(x => x switch { "a" => (Maneuver.Maneuver)a, "b" => b, _ => n }).ToArray();

        var e = Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(plan));

        Assert.Equal("maneuvers", e.ParamName);
        Assert.Contains("twice", e.Message);
        Assert.Equal(new Maneuver.Maneuver[] { former }, spacecraft.Maneuvers);
        Assert.Same(former, spacecraft.StandbyManeuver);
        Assert.All(new Maneuver.Maneuver[] { a, b, n }, m =>
        {
            Assert.Null(m.PlanOwner);
            Assert.Null(m.NextManeuver);
        });
    }

    [Fact]
    public void SetManeuvers_AManeuverOfAnotherPlan_Throws_UntilItsOwnerReleasesIt()
    {
        var earth = Earth();
        var owner = NewSpacecraft(earth, out var engine);
        var other = NewSpacecraft(earth, out _);
        var a = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        owner.SetManeuvers(a);
        var otherPlan = new NadirAttitude(S, TimeSpan.Zero, earth);
        other.SetManeuvers(otherPlan);

        var e = Assert.Throws<ArgumentException>(() => other.SetManeuvers(a));
        Assert.Equal("maneuvers", e.ParamName);
        Assert.Contains(owner.Name, e.Message);
        Assert.Equal(new Maneuver.Maneuver[] { otherPlan }, other.Maneuvers);
        Assert.Same(owner, a.PlanOwner);
        Assert.Equal(new Maneuver.Maneuver[] { a }, owner.Maneuvers);

        // Released by its owner, it can join another plan of the same spacecraft
        owner.SetManeuvers();
        Assert.Null(a.PlanOwner);
        owner.SetManeuvers(a);
        Assert.Same(owner, a.PlanOwner);
    }

    [Theory]
    [InlineData("Impulse", "OtherSpacecraft")]
    [InlineData("Impulse", "UnattachedTank")]
    public void SetManeuvers_AnEngineNotOnTheSpacecraft_Throws_AndChangesNothing(string kind, string engineKind)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out _);
        var other = NewSpacecraft(earth, out var otherEngine);
        var foreign = engineKind == "OtherSpacecraft"
            ? otherEngine
            : new Engine("LOOSE", 300.0, 10.0, new FuelTank("LOOSE", 1000.0, 1000.0, "m", "SN-LOOSE"), "m", "SN-LOOSE");
        // An impulse only: an attitude has no engine since lot L5b (decisions 48 and 93 of #668)
        Assert.Equal("Impulse", kind);
        Maneuver.Maneuver m = new ApogeeHeightManeuver(8000e3, foreign, S, TimeSpan.Zero, earth);
        var ownManeuver = new NadirAttitude(S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(ownManeuver);

        var e = Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(ownManeuver, m));

        Assert.Equal("maneuvers", e.ParamName);
        Assert.Contains(foreign.Name, e.Message);
        Assert.Contains(spacecraft.Name, e.Message);
        Assert.Equal(new Maneuver.Maneuver[] { ownManeuver }, spacecraft.Maneuvers);
        Assert.Null(ownManeuver.NextManeuver);
        Assert.Null(m.PlanOwner);
        Assert.Empty(other.Maneuvers);
    }

    /// <summary>
    /// A target in another frame than the one the spacecraft is propagated in: a TEME target on a TLE spacecraft, which
    /// propagates in ICRF (#675); and a maneuver built before its tank was on the spacecraft (#685). The constructor of a
    /// maneuver checks no frame since lot L5b (decision 65 of #668): the plan and the run do.
    /// </summary>
    [Theory]
    [InlineData("TemeTargetOnATle")]
    [InlineData("BuiltBeforeTheTank")]
    public void SetManeuvers_ATargetFrameThatDiffers_Throws(string kind)
    {
        Spacecraft spacecraft;
        ImpulseManeuver maneuver;
        if (kind == "TemeTargetOnATle")
        {
            var tle = new Tle("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
            var tleEarth = (CelestialBody)tle.Observer;
            spacecraft = NewSpacecraft(tleEarth, out var engine, tle);
            var target = tle.ToStateVector(tle.Epoch);
            Assert.Equal(Frames.Frame.TEME, target.Frame);
            maneuver = new ApsidalAlignmentManeuver(target, engine, tle.Epoch, TimeSpan.Zero);
        }
        else
        {
            var earth = Earth();
            int id = NextId();
            spacecraft = new Spacecraft(id, "PLAN" + -id, 1000.0, new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF), 3000.0,
                new Clock("PLAN" + -id, 65536));
            var tank = new FuelTank("ft", 1000.0, 1000.0, "m", "sn");
            var engine = new Engine("eng", 300.0, 10.0, tank, "m", "sn");
            var target = new KeplerianElements(7100e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ECLIPTIC_J2000);
            maneuver = new ApsidalAlignmentManeuver(target, engine, S, TimeSpan.Zero);
            spacecraft.AddFuelTank(tank);
            spacecraft.AddEngine(engine);
        }

        var e = Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(maneuver));

        Assert.Equal("maneuvers", e.ParamName);
        Assert.Contains("target orbit", e.Message);
        Assert.Empty(spacecraft.Maneuvers);
        Assert.Null(maneuver.PlanOwner);
    }

    [Fact]
    public void SetManeuvers_ATargetInGcrfOnAnIcrfSpacecraft_IsAccepted()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var target = new KeplerianElements(7100e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.GCRF);
        var maneuver = new ApsidalAlignmentManeuver(target, engine, S, TimeSpan.Zero);
        spacecraft.SetManeuvers(maneuver);
        Assert.Equal(new Maneuver.Maneuver[] { maneuver }, spacecraft.Maneuvers);
    }

    [Fact]
    public void SetManeuvers_ConcurrentClaims_OnePlanHoldsTheManeuver()
    {
        // Two spacecraft sharing a tank cannot exist (#676), so the race is between two plans of the owner's engine: the
        // owner and a spacecraft that the engine check refuses; whatever the order, the maneuver ends in one plan
        var earth = Earth();
        for (int round = 0; round < 20; round++)
        {
            var owner = NewSpacecraft(earth, out var engine);
            var maneuvers = Enumerable.Range(0, 8).Select(_ => (Maneuver.Maneuver)new NadirAttitude(S, TimeSpan.Zero, earth)).ToArray();
            System.Threading.Tasks.Parallel.For(0, 8, i => owner.SetManeuvers(maneuvers[i]));
            Assert.Single(owner.Maneuvers);
            Assert.Single(maneuvers, m => m.PlanOwner != null);
            Assert.Same(owner.Maneuvers[0], maneuvers.Single(m => m.PlanOwner != null));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The run re-checks (InvalidOperationException)
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A maneuver linked into the chain past <see cref="Spacecraft.SetManeuvers"/> (the internal setter of
    /// <see cref="Maneuver.Maneuver.NextManeuver"/>, which only the library and its tests reach): every activation path
    /// rejects the run with <see cref="InvalidOperationException"/> before anything changes.
    /// </summary>
    [Theory]
    [InlineData("Propagate", "RK")]
    [InlineData("Propagate", "VV")]
    [InlineData("Constructor", "RK")]
    [InlineData("Reused", "RK")]
    [InlineData("Scenario", "VV")]
    [InlineData("Batch", "RK")]
    public void Run_AManeuverNotInThePlan_Throws(string path, string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var head = new NadirAttitude(S, TimeSpan.FromSeconds(60.0), earth);
        spacecraft.SetManeuvers(head);
        var window = new Window(S, S.AddHours(1.0));
        CentralBodyPropagator reused = path == "Reused" ? new CentralBodyPropagator(window, spacecraft, Options(earth, integrator)) : null;
        var stray = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
        head.NextManeuver = stray;

        Exception exception = path switch
        {
            "Propagate" => Record.Exception(() => spacecraft.Propagate(window, Options(earth, integrator))),
            "Constructor" => Record.Exception(() => new CentralBodyPropagator(window, spacecraft, Options(earth, integrator))),
            "Reused" => Record.Exception(() => reused.Propagate()),
            "Scenario" => Record.Exception(() =>
            {
                var scenario = new Scenario("PLAN", new IO.Astrodynamics.Mission.Mission("PLAN"), window);
                scenario.AddCelestialItem(earth);
                scenario.AddSpacecraft(spacecraft);
                scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }, OutputStep = Output });
            }),
            _ => BatchPropagator.Propagate([new PropagationTask(spacecraft, window, Options(earth, integrator))]).Results.Single().Error
        };

        var rejected = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plan", rejected.Message);
        Assert.False(spacecraft.IsPropagated);
        Assert.Null(head.ThrustWindow);
        Assert.Equal(1000.0, spacecraft.FuelTanks.Single().Quantity);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // One plan on every activation path
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The same plan (a leading nadir held 600 s, an apogee raise, a prograde attitude) on every activation path: each
    /// run is that of a fresh spacecraft by <c>Spacecraft.Propagate</c>, bit for bit, and runs the plan again from its
    /// first maneuver.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void ThePlan_RunsAlike_OnEveryPath(string integrator)
    {
        var earth = Earth();
        var window = new Window(S, S.AddHours(3.0));

        (Spacecraft, Maneuver.Maneuver[]) Planned()
        {
            var spacecraft = NewSpacecraft(earth, out var engine);
            Maneuver.Maneuver[] plan =
            [
                new NadirAttitude(S, TimeSpan.FromSeconds(600.0), earth), new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth),
                new ProgradeAttitude(S, TimeSpan.Zero, earth)
            ];
            spacecraft.SetManeuvers(plan);
            return (spacecraft, plan);
        }

        var (reference, referencePlan) = Planned();
        var referenceSolution = reference.Propagate(window, Options(earth, integrator));
        Assert.Equal(3, reference.ExecutedManeuvers.Count);

        foreach (var path in new[] { "Again", "Constructor", "Scenario", "Batch" })
        {
            var (spacecraft, plan) = path == "Again" ? (reference, referencePlan) : Planned();
            switch (path)
            {
                case "Again":
                    reference.Propagate(window, Options(earth, integrator));
                    break;
                case "Constructor":
                    new CentralBodyPropagator(window, spacecraft, Options(earth, integrator)).Propagate();
                    break;
                case "Scenario":
                    var scenario = new Scenario("PLAN", new IO.Astrodynamics.Mission.Mission("PLAN"), window);
                    scenario.AddCelestialItem(earth);
                    scenario.AddSpacecraft(spacecraft);
                    var options = Options(earth, integrator);
                    scenario.Simulate(options with { ThirdBodies = null });
                    break;
                default:
                    Assert.True(BatchPropagator.Propagate([new PropagationTask(spacecraft, window, Options(earth, integrator))]).Results.Single().Succeeded);
                    break;
            }

            Assert.Equal(3, spacecraft.ExecutedManeuvers.Count);
            var burn = (ImpulseManeuver)plan[1];
            var referenceBurn = (ImpulseManeuver)referencePlan[1];
            Assert.Equal(referenceBurn.DeltaV, burn.DeltaV);
            LabelAssert.Equal(referenceBurn.ThrustWindow, burn.ThrustWindow);
            Assert.Equal(referenceSolution.StateVectors[^1].Position, spacecraft.GetGeometricStateRelativeTo(window.EndDate, earth).Position);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The surface: the former linking members are gone, the hierarchy is closed (decisions 45, 48, 49)
    // ---------------------------------------------------------------------------------------------------------------------

    private const BindingFlags Everything = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Fact]
    public void TheFormerLinkingMembers_AreGone()
    {
        var spacecraft = typeof(Spacecraft);
        Assert.Null(spacecraft.GetMethod("SetStandbyManeuver", Everything));
        Assert.Null(spacecraft.GetProperty("InitialManeuver", Everything));
        Assert.Null(typeof(Maneuver.Maneuver).GetMethod("SetNextManeuver", Everything));
        Assert.Null(typeof(Maneuver.Maneuver).GetMethod("RaiseGivenMinimumEpoch", Everything));
        Assert.False(spacecraft.GetProperty("StandbyManeuver", Everything)!.GetMethod!.IsPublic);
        var setManeuvers = spacecraft.GetMethod("SetManeuvers");
        Assert.NotNull(setManeuvers);
        Assert.NotNull(setManeuvers.GetParameters().Single().GetCustomAttribute<ParamArrayAttribute>());
        Assert.Equal(typeof(System.Collections.Generic.IReadOnlyList<Maneuver.Maneuver>), spacecraft.GetProperty("Maneuvers")!.PropertyType);

        var next = typeof(Maneuver.Maneuver).GetProperty("NextManeuver")!;
        Assert.True(next.GetMethod!.IsPublic);
        Assert.True(next.SetMethod!.IsAssembly);
    }

    [Fact]
    public void TheHierarchy_IsClosed()
    {
        var maneuver = typeof(Maneuver.Maneuver);
        var constructor = Assert.Single(maneuver.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.True(constructor.IsFamilyAndAssembly);
        Assert.Empty(maneuver.GetConstructors());

        foreach (var type in new[] { maneuver, typeof(ImpulseManeuver), typeof(Attitude), typeof(NadirAttitude), typeof(ApogeeHeightManeuver) })
        {
            var tryExecute = type.GetMethod("TryExecute", Everything);
            Assert.True(tryExecute == null || tryExecute.IsAssembly, type.Name);
        }

        Assert.Null(maneuver.GetMethod("Execute", Everything | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Attitude).GetMethod("Execute", Everything | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Attitude).GetProperty("StateOrientation", Everything));
        var execute = typeof(ImpulseManeuver).GetMethod("Execute", Everything | BindingFlags.DeclaredOnly)!;
        Assert.True(execute.IsFamily && execute.IsAbstract);
        Assert.True(typeof(ImpulseManeuver).GetMethod("ComputeDeltaV")!.IsPublic);
        Assert.True(typeof(Attitude).GetMethod("ComputeStateOrientation")!.IsPublic);

        Assert.False(typeof(Engine).GetMethod("Ignite", Everything)!.IsPublic);
        Assert.False(typeof(PlaneAlignmentManeuver).GetProperty("RelativeInclination", Everything)!.GetMethod!.IsPublic);
        Assert.False(typeof(ApsidalAlignmentManeuver).GetProperty("IntersectsP", Everything)!.GetMethod!.IsPublic);
        Assert.False(typeof(ApsidalAlignmentManeuver).GetProperty("IntersectsQ", Everything)!.GetMethod!.IsPublic);
        Assert.All(typeof(PhasingManeuver).GetConstructors(), c => Assert.DoesNotContain(c.GetParameters(), p => p.ParameterType == typeof(TimeSpan)));
    }

    /// <summary>
    /// <c>ManeuverEventDetector</c> is internal (decision 88 of #668, decided by S. Guillet, 2026-11-23): the propagation
    /// takes no user detector, and its public <c>HandleEvent</c> executed a maneuver outside a run (the probe of the review:
    /// 105.5 kg of fuel burned, <c>DeltaV</c> and the windows rewritten after the run). The event types it implements stay
    /// public, as they are.
    /// </summary>
    [Fact]
    public void TheManeuverEventDetector_IsInternal()
    {
        var detector = typeof(IO.Astrodynamics.Propagator.Events.IEventDetector).Assembly
            .GetType("IO.Astrodynamics.Propagator.Events.ManeuverEventDetector", throwOnError: true)!;
        Assert.False(detector.IsVisible);
        Assert.True(detector.IsNotPublic);
        Assert.True(typeof(IO.Astrodynamics.Propagator.Events.IEventDetector).IsPublic);
        Assert.True(typeof(IO.Astrodynamics.Propagator.Events.EventResult).IsPublic);
        Assert.True(typeof(IO.Astrodynamics.Propagator.Events.EventAction).IsPublic);
        Assert.True(typeof(IO.Astrodynamics.Propagator.Events.CrossingDirection).IsPublic);
    }
}
