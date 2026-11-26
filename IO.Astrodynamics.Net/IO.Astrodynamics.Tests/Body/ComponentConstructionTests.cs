// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The components of a spacecraft, lot L5b of #668 (R10 and decision 76, decided by S. Guillet, 2026-11-23):
/// <see cref="FuelTank"/>, <see cref="Engine"/> and <see cref="Payload"/> take their model and serial number last and
/// optional, a tank may start empty, the three compare by reference, and <c>AddFuelTank</c>, <c>AddEngine</c> and
/// <c>AddPayload</c> return their argument and refuse a second component of the same name on a spacecraft (ordinal).
/// </summary>
/// <remarks>
/// The references are the decided rules and the explicit forms: a component built without a model or a serial number
/// propagates as one built with them, bit for bit (IC-20 of the lot's gap analysis).
/// </remarks>
public class ComponentConstructionTests
{
    public ComponentConstructionTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static int _nextId = -183000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static Spacecraft NewSpacecraft(CelestialBody earth) => new(NextId(), "Sat", 1000.0,
        new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF));

    private static Spacecraft NewSpacecraft() => NewSpacecraft(new CelestialBody(PlanetsAndMoons.Earth));

    // ---------------------------------------------------------------------------------------------------------------------
    // The constructors (R10, decision 76)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Components_TakeTheirModelAndSerialNumberLastAndOptional()
    {
        var tank = new FuelTank("Tank", 3000.0, 2000.0);
        var engine = new Engine("Main", 320.0, 5.0, tank);
        var payload = new Payload("Battery", 10.0);

        Assert.Equal(("Tank", 3000.0, 2000.0, 2000.0), (tank.Name, tank.Capacity, tank.InitialQuantity, tank.Quantity));
        Assert.Null(tank.Model);
        Assert.Null(tank.SerialNumber);
        Assert.Equal(("Main", 320.0, 5.0), (engine.Name, engine.Isp, engine.FuelFlow));
        Assert.Same(tank, engine.FuelTank);
        Assert.Null(engine.Model);
        Assert.Null(engine.SerialNumber);
        Assert.Equal(("Battery", 10.0), (payload.Name, payload.Mass));
        Assert.Null(payload.SerialNumber);

        var described = new FuelTank("Tank", 3000.0, 2000.0, "Model-T", "SN-T");
        Assert.Equal(("Model-T", "SN-T"), (described.Model, described.SerialNumber));
        var describedEngine = new Engine("Main", 320.0, 5.0, described, "Model-E", "SN-E");
        Assert.Equal(("Model-E", "SN-E"), (describedEngine.Model, describedEngine.SerialNumber));
        Assert.Equal("SN-P", new Payload("Battery", 10.0, "SN-P").SerialNumber);
    }

    [Fact]
    public void Components_RequireAName()
    {
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new FuelTank("", 1.0, 1.0)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new Engine(null, 300.0, 1.0, new FuelTank("T", 1.0, 1.0))).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new Payload("", 1.0)).ParamName);
    }

    /// <summary>
    /// A tank may start empty (R10): its first burn throws <see cref="InsufficientFuelException"/>, as a tank holding less
    /// than the burn.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void EmptyTank_IsAcceptedAndItsFirstBurnThrows(string integrator)
    {
        var tank = new FuelTank("Empty", 1000.0, 0.0);
        Assert.Equal(0.0, tank.Quantity);
        Assert.Equal("initialQuantity", Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("T", 1000.0, -1e-300)).ParamName);

        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = NewSpacecraft(earth);
        spacecraft.AddFuelTank(tank);
        var engine = spacecraft.AddEngine(new Engine("Main", 300.0, 10.0, tank));
        spacecraft.SetManeuvers(new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth));

        var exception = Assert.Throws<InsufficientFuelException>(() => spacecraft.Propagate(new Window(S, TimeSpan.FromHours(2.0)), Options(earth, integrator)));
        Assert.Equal(0.0, exception.AvailableMass);
        Assert.True(exception.RequiredMass > 0.0);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Reference equality (decision 76, R10 open choice 3)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Components_CompareByReference()
    {
        var tank = new FuelTank("Tank", 3000.0, 2000.0, "M", "SN");
        var twinTank = new FuelTank("Tank", 3000.0, 2000.0, "M", "SN");
        var engine = new Engine("Main", 320.0, 5.0, tank, "M", "SN");
        var twinEngine = new Engine("Main", 320.0, 5.0, tank, "M", "SN");
        var payload = new Payload("Battery", 10.0, "SN");
        var twinPayload = new Payload("Battery", 10.0, "SN");

        Assert.False(tank.Equals(twinTank));
        Assert.False(engine.Equals(twinEngine));
        Assert.False(payload.Equals(twinPayload));
        Assert.True(tank.Equals(tank));

        foreach (var type in new[] { typeof(FuelTank), typeof(Engine), typeof(Payload) })
        {
            Assert.DoesNotContain(type.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));
            Assert.Null(type.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static));
            Assert.Null(type.GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static));
            Assert.Equal(typeof(object), type.GetMethod(nameof(Equals), [typeof(object)])!.DeclaringType);
            Assert.Equal(typeof(object), type.GetMethod(nameof(GetHashCode), Type.EmptyTypes)!.DeclaringType);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The Add methods: their argument back, one name per spacecraft (decision 76, R10)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AddMethods_ReturnTheirArgument()
    {
        var spacecraft = NewSpacecraft();
        var tank = new FuelTank("Tank", 3000.0, 2000.0);
        var engine = new Engine("Main", 320.0, 5.0, tank);
        var payload = new Payload("Battery", 10.0);

        Assert.Same(tank, spacecraft.AddFuelTank(tank));
        Assert.Same(engine, spacecraft.AddEngine(engine));
        Assert.Same(payload, spacecraft.AddPayload(payload));
        Assert.Equal(3010.0, spacecraft.GetTotalMass());
    }

    [Fact]
    public void SecondTankOfAName_IsRefused_AndCanGoToAnotherSpacecraft()
    {
        var spacecraft = NewSpacecraft();
        spacecraft.AddFuelTank(new FuelTank("Tank", 3000.0, 2000.0, "M", "SN-1"));
        var second = new FuelTank("Tank", 1000.0, 1000.0, "M", "SN-2");

        var exception = Assert.Throws<ArgumentException>(() => spacecraft.AddFuelTank(second));

        Assert.Equal("fuelTank", exception.ParamName);
        Assert.Single(spacecraft.FuelTanks);
        // The name check runs before the tank is attached: the refused tank is free (IC-16)
        Assert.Null(second.Spacecraft);
        var other = NewSpacecraft();
        Assert.Same(second, other.AddFuelTank(second));
        Assert.Same(other, second.Spacecraft);
        // Names compare ordinally
        Assert.NotNull(spacecraft.AddFuelTank(new FuelTank("TANK", 1000.0, 1000.0)));
    }

    [Fact]
    public void SecondEngineOfAName_IsRefused()
    {
        var spacecraft = NewSpacecraft();
        var tank = spacecraft.AddFuelTank(new FuelTank("Tank", 3000.0, 2000.0));
        spacecraft.AddEngine(new Engine("Main", 320.0, 5.0, tank, "M", "SN-1"));

        var exception = Assert.Throws<ArgumentException>(() => spacecraft.AddEngine(new Engine("Main", 300.0, 4.0, tank, "M", "SN-2")));

        Assert.Equal("engine", exception.ParamName);
        Assert.Single(spacecraft.Engines);
        Assert.NotNull(spacecraft.AddEngine(new Engine("Main2", 300.0, 4.0, tank)));
    }

    /// <summary>
    /// The one-name rule of the payloads has no file behind it, unlike the instruments': a payload name reaches no kernel
    /// and no message beyond the Add call. It is the rule of every component, for uniformity (decision 76).
    /// </summary>
    [Fact]
    public void SecondPayloadOfAName_IsRefused()
    {
        var spacecraft = NewSpacecraft();
        spacecraft.AddPayload(new Payload("Battery", 10.0));

        var exception = Assert.Throws<ArgumentException>(() => spacecraft.AddPayload(new Payload("Battery", 12.0)));

        Assert.Equal("payload", exception.ParamName);
        Assert.Single(spacecraft.Payloads);
    }

    [Fact]
    public void SameInstanceTwice_IsRefused()
    {
        var spacecraft = NewSpacecraft();
        var tank = spacecraft.AddFuelTank(new FuelTank("Tank", 3000.0, 2000.0));
        var engine = spacecraft.AddEngine(new Engine("Main", 320.0, 5.0, tank));
        var payload = spacecraft.AddPayload(new Payload("Battery", 10.0));

        Assert.Throws<ArgumentException>(() => spacecraft.AddFuelTank(tank));
        Assert.Throws<ArgumentException>(() => spacecraft.AddEngine(engine));
        Assert.Throws<ArgumentException>(() => spacecraft.AddPayload(payload));
        Assert.Single(spacecraft.FuelTanks);
        Assert.Single(spacecraft.Engines);
        Assert.Single(spacecraft.Payloads);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Results unchanged (IC-20)
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A spacecraft whose components have no model and no serial number propagates an impulse as one whose components
    /// have them, bit for bit, under both integrators.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void ComponentsWithoutModelOrSerial_PropagateAsDescribedOnes(string integrator)
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);

        (Spacecraft, ImpulseManeuver) Build(bool described)
        {
            var spacecraft = NewSpacecraft(earth);
            var tank = spacecraft.AddFuelTank(described ? new FuelTank("Tank", 1000.0, 1000.0, "M", "SN-T") : new FuelTank("Tank", 1000.0, 1000.0));
            var engine = spacecraft.AddEngine(described ? new Engine("Main", 300.0, 10.0, tank, "M", "SN-E") : new Engine("Main", 300.0, 10.0, tank));
            spacecraft.AddPayload(described ? new Payload("Battery", 10.0, "SN-P") : new Payload("Battery", 10.0));
            var maneuver = new ApogeeHeightManeuver(8000e3, engine, S, TimeSpan.Zero, earth);
            spacecraft.SetManeuvers(maneuver);
            return (spacecraft, maneuver);
        }

        var (bare, bareManeuver) = Build(false);
        var (described, describedManeuver) = Build(true);
        var window = new Window(S, TimeSpan.FromHours(3.0));

        var a = bare.Propagate(window, Options(earth, integrator)).StateVectors;
        var b = described.Propagate(window, Options(earth, integrator)).StateVectors;

        Assert.True(bareManeuver.FuelBurned > 0.0);
        Assert.Equal(describedManeuver.FuelBurned, bareManeuver.FuelBurned);
        Assert.Equal(b.Count, a.Count);
        Assert.True(a.Zip(b).All(p => p.First.Position == p.Second.Position && p.First.Velocity == p.Second.Velocity));
    }

    private static PropagationOptions Options(CelestialBody earth, string integrator) => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
        OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None
    };
}
