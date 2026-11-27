// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// A maneuver draws on a tank of the propagated spacecraft (#676, #586): a chain whose engine's tank is another
/// spacecraft's, or no spacecraft's, is rejected by every activation path before anything changes, and a tank belongs to
/// one spacecraft.
/// </summary>
/// <remarks>
/// The case: a 7 000 km orbit, e = 0.01, i = 0.9 rad, from its apoapsis at 2024-01-01 00:00 TDB about a point-mass Earth,
/// a window of 2 h with outputs every 60 s; 1 000 kg dry, 3 000 kg of fuel, Isp 320 s, 5 kg/s. The rejected calls are
/// checked against the state the previous valid run left, read before the call (structural); the controls against
/// Tsiolkovsky's closed form and the prograde law (the unit velocity). A spacecraft on a TLE or an osculating TEME state,
/// propagated numerically since #675, is rejected on every path as well: the ISS TLE of 2021-01-20, the TEME state its SGP4
/// gives at the TLE epoch, a window of 2 h from that epoch.
/// </remarks>
public class ManeuverEngineBindingTests
{
    private static readonly Time Epoch = new(2024, 1, 1, 0, 0, 0, TimeScale.TDB);
    private static readonly Window Window = new(Epoch, Epoch.AddSeconds(7200.0));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private const double Isp = 320.0;
    private const double DryMass = 1000.0;
    private const double Fuel = 3000.0;

    // The prograde front of the attitude record against the unit velocity of the stored states at the outputs, with
    // BodyFront +X, worst over the outputs (the static axes left it at π/2 on main); CI run 43591645844 (2026-11-14):
    // RK7(8) 8.141e-13 rad on Linux, Windows and macOS arm64, Velocity-Verlet 8.117e-13 rad on the three. Twice the
    // worst of the three platforms, 8.141e-13 rad
    private const double ProgradeFrontTolerance = 1.7e-12;

    private readonly ITestOutputHelper _output;
    private static int _nextId = -1700;

    public ManeuverEngineBindingTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    private static KeplerianElements Orbit(CelestialBody earth) =>
        new(7000e3, 0.01, 0.9, 0.0, 0.0, System.Math.PI, earth, Epoch, Frame.ICRF);

    private static Spacecraft NewSpacecraft(CelestialBody earth, out Engine engine, Vector3? front = null, Vector3? right = null,
        IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit = null)
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "BIND" + -id, DryMass, orbit ?? Orbit(earth), 10000.0, new Clock("BIND" + -id, 65536),
            bodyFront: front, bodyRight: right, bodyUp: front.HasValue ? Vector3.VectorZ : null);
        var tank = new FuelTank("TANK" + -id, Fuel, Fuel, "MODEL", "SN" + -id);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("ENGINE" + -id, Isp, 5.0, tank, "MODEL", "SN" + -id);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    private static Func<IntegratorOptions> IntegratorOf(string name) => name switch
    {
        "RK78" => () => RK78IntegratorOptions.Default,
        "VV" => () => new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) },
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    // The activation paths: the call that must throw, or, for the batch, the error of its task
    private static Exception Activate(string path, Spacecraft spacecraft, CelestialBody earth, Func<IntegratorOptions> integrator) =>
        Activate(path, spacecraft, earth, integrator, Window);

    private static Exception Activate(string path, Spacecraft spacecraft, CelestialBody earth, Func<IntegratorOptions> integrator, Window window)
    {
        CelestialItem[] bodies = [earth];
        switch (path)
        {
            case "Propagate":
                return Record.Exception(() => spacecraft.Propagate(window, new PropagationOptions { Integrator = integrator(), OutputStep = Output, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None }));
            case "PropagateDefault":
                return Record.Exception(() => spacecraft.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = Output }, OutputStep = Output, ThirdBodies = bodies }));
            case "Constructor":
                return Record.Exception(() =>
                {
                    var propagator = new CentralBodyPropagator(window, spacecraft, new PropagationOptions { Integrator = integrator(), OutputStep = Output, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None });
                    propagator.Propagate();
                });
            case "Scenario":
            case "ScenarioDefault":
            {
                var scenario = new Scenario("BIND", new IO.Astrodynamics.Mission.Mission("BIND"), window);
                scenario.AddCelestialItem(earth);
                scenario.AddSpacecraft(spacecraft);
                return Record.Exception(() => scenario.Simulate(path == "Scenario"
                    ? new PropagationOptions { Integrator = integrator(), OutputStep = Output, CovariancePropagation = CovariancePropagationOptions.None }
                    : new PropagationOptions { Integrator = new VVIntegratorOptions { Step = Output }, OutputStep = Output }));
            }
            case "Batch":
            case "BatchDefault":
            {
                var task = new PropagationTask(spacecraft, window, new PropagationOptions { Integrator = path == "Batch" ? integrator() : new VVIntegratorOptions { Step = Output }, OutputStep = Output, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None });
                var result = BatchPropagator.Propagate([task]).Results.Single();
                Assert.False(result.Succeeded);
                Assert.Null(result.Solution);
                return result.Error;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(path));
        }
    }

    /// <summary>
    /// Since decision 46 of #668 <see cref="Spacecraft.SetManeuvers"/> refuses a maneuver on an engine whose tank is not the
    /// spacecraft's (<see cref="ArgumentException"/>, nothing changed): checked here, then the chain the run meets is built
    /// past the claim, <paramref name="foreign"/> linked after <paramref name="own"/> through the internal setter of
    /// <see cref="Maneuver.Maneuver.NextManeuver"/>, which a test reaches, so that the run's own re-check
    /// (<see cref="InvalidOperationException"/>) is what the activation paths throw.
    /// </summary>
    private static void PlanPastTheClaim(Spacecraft spacecraft, Maneuver.Maneuver own, Maneuver.Maneuver foreign)
    {
        var before = spacecraft.Maneuvers.ToArray();
        var refused = Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(own, foreign));
        Assert.Equal("maneuvers", refused.ParamName);
        Assert.Contains(((ImpulseManeuver)foreign).Engine.Name, refused.Message);
        Assert.Equal(before, spacecraft.Maneuvers);
        Assert.Null(foreign.PlanOwner);
        Assert.Null(own.PlanOwner);

        spacecraft.SetManeuvers(own);
        own.NextManeuver = foreign;
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var (path, integrators) in Paths)
        foreach (var integrator in integrators)
        // An impulse only: an attitude has no engine since lot L5b (decisions 48 and 93 of #668)
        foreach (var chain in new[] { "Impulse", "ImpulseSecond" })
        foreach (var engine in new[] { "UnattachedTank", "OtherSpacecraft", "NotAdded", "Twin" })
        {
            yield return [path, integrator, chain, engine];
        }
    }

    // The activation paths and their integrators
    private static readonly (string Path, string[] Integrators)[] Paths =
    [
        ("Propagate", ["RK78", "VV"]), ("Constructor", ["RK78", "VV"]),
        ("Scenario", ["RK78", "VV"]), ("Batch", ["RK78", "VV"]),
        // Velocity-Verlet at the output step, the integrator of the members without one before 11.0.0
        ("PropagateDefault", ["VV"]), ("ScenarioDefault", ["VV"]), ("BatchDefault", ["VV"])
    ];

    public static IEnumerable<object[]> TleOrTemeCases()
    {
        foreach (var (path, integrators) in Paths)
        foreach (var integrator in integrators)
        foreach (var orbit in new[] { "Tle", "Teme" })
        foreach (var engine in new[] { "UnattachedTank", "OtherSpacecraft" })
        {
            yield return [path, integrator, orbit, engine];
        }
    }

    [Theory]
    [MemberData(nameof(TleOrTemeCases))]
    public void EngineNotOnSpacecraft_TleOrTemeOrbit_Throws(string path, string integratorName, string orbitKind, string engineKind)
    {
        // Arrange: a TLE spacecraft starts from SGP4 at the window start on the Earth of the TLE, a TEME state from itself,
        // both rotated once to ICRF (#675); a valid run on the spacecraft's own engine, whose state the rejected call must
        // leave as it was
        var integrator = IntegratorOf(integratorName);
        var tle = new Tle("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var epoch = tle.Epoch.ToTdb();
        var window = new Window(epoch, epoch.AddSeconds(7200.0));
        CelestialBody earth;
        IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit;
        if (orbitKind == "Tle")
        {
            earth = (CelestialBody)tle.Observer;
            orbit = tle;
        }
        else
        {
            earth = new CelestialBody(PlanetsAndMoons.Earth);
            var teme = tle.ToStateVector(epoch);
            Assert.Equal(Frame.TEME, teme.Frame);
            orbit = new StateVector(teme.Position, teme.Velocity, earth, epoch, Frame.TEME);
        }

        var spacecraft = NewSpacecraft(earth, out var ownEngine, orbit: orbit);
        spacecraft.SetManeuvers(new ApogeeHeightManeuver(7300e3, ownEngine, epoch, TimeSpan.Zero, earth));
        spacecraft.Propagate(window, new PropagationOptions { Integrator = integrator(), OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });
        Assert.Single(spacecraft.ExecutedManeuvers);

        var other = NewSpacecraft(earth, out var otherEngine);
        var foreignEngine = engineKind == "OtherSpacecraft"
            ? otherEngine
            : new Engine("LOOSE", Isp, 5.0, new FuelTank("LOOSE", Fuel, Fuel, "MODEL", "SN-LOOSE"), "MODEL", "SN-LOOSE");
        PlanPastTheClaim(spacecraft, new ProgradeAttitude(epoch, TimeSpan.Zero, earth),
            new ApogeeHeightManeuver(7300e3, foreignEngine, epoch, TimeSpan.Zero, earth));

        var ownTank = spacecraft.FuelTanks.Single();
        var states = spacecraft.InterpolatedStatesWindow;
        var mid = epoch.AddSeconds(3600.0);
        var midState = spacecraft.GetGeometricStateRelativeTo(mid, earth);
        var lastPropagation = spacecraft.LastPropagation;
        var record = spacecraft.Frame.GetStateOrientationsToIcrf().ToArray();
        var executed = spacecraft.ExecutedManeuvers.ToArray();
        double ownQuantity = ownTank.Quantity;
        double foreignQuantity = foreignEngine.FuelTank.Quantity;

        // Act
        var exception = Activate(path, spacecraft, earth, integrator, window);

        // Assert: rejected before anything changes, the engine and the spacecraft named
        var rejected = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(foreignEngine.Name, rejected.Message);
        Assert.Contains(spacecraft.Name, rejected.Message);
        Assert.True(spacecraft.IsPropagated);
        Assert.NotNull(states);
        LabelAssert.Equal(states, spacecraft.InterpolatedStatesWindow);
        var midAfter = spacecraft.GetGeometricStateRelativeTo(mid, earth);
        Assert.Equal(midState.Position, midAfter.Position);
        Assert.Equal(midState.Velocity, midAfter.Velocity);
        Assert.Same(lastPropagation, spacecraft.LastPropagation);
        Assert.Equal(record, spacecraft.Frame.GetStateOrientationsToIcrf().ToArray());
        Assert.Equal(executed, spacecraft.ExecutedManeuvers.ToArray());
        Assert.Equal(ownQuantity, ownTank.Quantity);
        Assert.Equal(foreignQuantity, foreignEngine.FuelTank.Quantity);
        Assert.Equal(Fuel, other.FuelTanks.Single().Quantity);
        Assert.Empty(other.ExecutedManeuvers);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EngineNotOnSpacecraft_Throws(string path, string integratorName, string chain, string engineKind)
    {
        // Arrange: a valid run on the spacecraft's own engine, whose state the rejected call must leave as it was
        var integrator = IntegratorOf(integratorName);
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = NewSpacecraft(earth, out var ownEngine);
        spacecraft.SetManeuvers(new ApogeeHeightManeuver(7300e3, ownEngine, Epoch, TimeSpan.Zero, earth));
        spacecraft.Propagate(Window, new PropagationOptions { Integrator = integrator(), OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });
        Assert.Single(spacecraft.ExecutedManeuvers);

        var other = NewSpacecraft(earth, out var otherEngine);
        var foreignEngine = ForeignEngine(engineKind, spacecraft, ownEngine, otherEngine);
        // The foreign impulse after an own attitude, held 0 s ("Impulse") or 10 min ("ImpulseSecond")
        Maneuver.Maneuver foreign = new ApogeeHeightManeuver(7300e3, foreignEngine, Epoch, TimeSpan.Zero, earth);
        var own = new ProgradeAttitude(Epoch, chain == "ImpulseSecond" ? TimeSpan.FromMinutes(10.0) : TimeSpan.Zero, earth);
        PlanPastTheClaim(spacecraft, own, foreign);

        var ownTank = spacecraft.FuelTanks.Single();
        var otherTank = other.FuelTanks.Single();
        var states = spacecraft.InterpolatedStatesWindow;
        var midState = spacecraft.GetGeometricStateRelativeTo(Epoch.AddSeconds(3600.0), earth);
        var lastPropagation = spacecraft.LastPropagation;
        var record = spacecraft.Frame.GetStateOrientationsToIcrf().ToArray();
        var executed = spacecraft.ExecutedManeuvers.ToArray();
        double ownQuantity = ownTank.Quantity;
        double foreignQuantity = foreignEngine.FuelTank.Quantity;

        // Act
        var exception = Activate(path, spacecraft, earth, integrator);

        // Assert: rejected before anything changes, the engine and the spacecraft named
        var rejected = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(foreignEngine.Name, rejected.Message);
        Assert.Contains(spacecraft.Name, rejected.Message);
        Assert.True(spacecraft.IsPropagated);
        LabelAssert.Equal(states, spacecraft.InterpolatedStatesWindow);
        var midAfter = spacecraft.GetGeometricStateRelativeTo(Epoch.AddSeconds(3600.0), earth);
        Assert.Equal(midState.Position, midAfter.Position);
        Assert.Equal(midState.Velocity, midAfter.Velocity);
        Assert.Same(lastPropagation, spacecraft.LastPropagation);
        Assert.Equal(record, spacecraft.Frame.GetStateOrientationsToIcrf().ToArray());
        Assert.Equal(executed, spacecraft.ExecutedManeuvers.ToArray());
        Assert.Equal(ownQuantity, ownTank.Quantity);
        Assert.Equal(foreignQuantity, foreignEngine.FuelTank.Quantity);
        Assert.Equal(Fuel, otherTank.Quantity);
        Assert.Empty(other.ExecutedManeuvers);
        switch (engineKind)
        {
            case "OtherSpacecraft":
                Assert.Same(other, foreignEngine.FuelTank.Spacecraft);
                break;
            case "UnattachedTank":
                Assert.Null(foreignEngine.FuelTank.Spacecraft);
                break;
            default:
                // On the spacecraft's own tank, not in its engines
                Assert.Same(ownTank, foreignEngine.FuelTank);
                Assert.DoesNotContain(spacecraft.Engines, x => ReferenceEquals(x, foreignEngine));
                break;
        }
    }

    // The engine of the foreign maneuver: another spacecraft's, one on a tank of no spacecraft, one built on the
    // spacecraft's own tank and never added with AddEngine, or a twin of its engine (another instance of its name, model and
    // serial number), which AddEngine refuses as a second engine of the name (decision 92 of #668, decided by S. Guillet,
    // 2026-11-23; decision 76 for the name: equal by value, it was refused as already added before)
    private static Engine ForeignEngine(string engineKind, Spacecraft spacecraft, Engine ownEngine, Engine otherEngine) => engineKind switch
    {
        "OtherSpacecraft" => otherEngine,
        "UnattachedTank" => new Engine("LOOSE", Isp, 5.0, new FuelTank("LOOSE", Fuel, Fuel, "MODEL", "SN-LOOSE"), "MODEL", "SN-LOOSE"),
        "NotAdded" => new Engine("NOTADDED", Isp, 5.0, spacecraft.FuelTanks.Single(), "MODEL", "SN-NOTADDED"),
        "Twin" => new Engine(ownEngine.Name, Isp, 5.0, spacecraft.FuelTanks.Single(), ownEngine.Model, ownEngine.SerialNumber),
        _ => throw new ArgumentOutOfRangeException(nameof(engineKind))
    };

    /// <summary>
    /// "An engine that is on this spacecraft" (decision 46) is an engine of <see cref="Spacecraft.Engines"/>, added with
    /// <see cref="Spacecraft.AddEngine"/>, compared by reference (decision 92 of #668, decided by S. Guillet, 2026-11-23):
    /// an engine built on the spacecraft's own tank and never added, or a twin of an added engine (another instance of its
    /// name, which <c>AddEngine</c> refuses as a second engine of the name, decision 76), is refused by <c>SetManeuvers</c> with
    /// <see cref="ArgumentException"/> (<c>maneuvers</c>), the plan unchanged; once added, the same engine is accepted and
    /// fires (before, the tank alone was checked and both passed).
    /// </summary>
    [Theory]
    [InlineData("NotAdded")]
    [InlineData("Twin")]
    public void AnEngineNotAddedWithAddEngine_IsRefusedBySetManeuvers(string engineKind)
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = NewSpacecraft(earth, out var ownEngine);
        var engine = ForeignEngine(engineKind, spacecraft, ownEngine, null);
        Assert.True(engineKind == "Twin" ? engine.Name == ownEngine.Name && !ReferenceEquals(engine, ownEngine) : !spacecraft.Engines.Contains(engine));
        Assert.DoesNotContain(engine, spacecraft.Engines);
        var maneuver = new ApogeeHeightManeuver(7300e3, engine, Epoch, TimeSpan.Zero, earth);
        // An impulse only: an attitude has no engine since lot L5b (decisions 48 and 93 of #668)
        foreach (var refusedManeuver in new Maneuver.Maneuver[] { maneuver })
        {
            var refused = Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(refusedManeuver));
            Assert.Equal("maneuvers", refused.ParamName);
            Assert.Contains(engine.Name, refused.Message);
            // The advice the caller can follow: for an engine of the name of an added one, which AddEngine refuses, the
            // engine instance of Engines; for an
            // engine not added, AddEngine (second review of lot L5a)
            if (engineKind == "Twin")
            {
                Assert.Contains("Engines", refused.Message);
                Assert.DoesNotContain("with AddEngine before the plan", refused.Message);
            }
            else
            {
                Assert.Contains("with AddEngine before the plan", refused.Message);
            }

            Assert.Empty(spacecraft.Maneuvers);
            Assert.Null(refusedManeuver.PlanOwner);
        }

        if (engineKind == "Twin")
        {
            Assert.Throws<ArgumentException>(() => spacecraft.AddEngine(engine));
            return;
        }

        // Added, the same engine is accepted and fires
        spacecraft.AddEngine(engine);
        spacecraft.SetManeuvers(maneuver);
        spacecraft.Propagate(Window, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });
        Assert.Same(maneuver, Assert.Single(spacecraft.ExecutedManeuvers));
        Assert.True(maneuver.FuelBurned > 0.0);
    }

    [Fact]
    public void SharedTank_Rejected()
    {
        // Arrange: #586's second setup, B given A's tank
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var a = NewSpacecraft(earth, out var engineOfA);
        var b = NewSpacecraft(earth, out _);
        var tankOfB = b.FuelTanks.Single();

        // Act
        var exception = Record.Exception(() => b.AddFuelTank(engineOfA.FuelTank));

        // Assert: nothing changed
        var rejected = Assert.IsType<ArgumentException>(exception);
        Assert.Equal("fuelTank", rejected.ParamName);
        Assert.Same(a, engineOfA.FuelTank.Spacecraft);
        Assert.Same(tankOfB, b.FuelTanks.Single());
        Assert.Same(engineOfA.FuelTank, a.FuelTanks.Single());
    }

    [Fact]
    public void SharedTank_SameSpacecraftAgain_StillRejectedAsDuplicate()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var a = NewSpacecraft(earth, out var engine);

        // Act and assert: the existing duplicate rule
        Assert.Throws<ArgumentException>(() => a.AddFuelTank(engine.FuelTank));
        Assert.Same(a, engine.FuelTank.Spacecraft);
    }

    [Theory]
    [InlineData("RK78")]
    [InlineData("VV")]
    public void OwnEngine_FiresAsTsiolkovsky(string integratorName)
    {
        // Arrange: the impulse of the rejected chains, on the spacecraft's own engine
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var maneuver = new ApogeeHeightManeuver(7300e3, engine, Epoch, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(maneuver);
        double m0 = spacecraft.DryOperatingMass + Fuel;

        // Act
        spacecraft.Propagate(Window, new PropagationOptions { Integrator = IntegratorOf(integratorName)(), OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });

        // Assert: Tsiolkovsky's closed form, m0 (1 − exp(−Δv / (Isp g0))), g0 = 9.80665 m/s²
        double dv = maneuver.DeltaV.Magnitude();
        double expected = m0 * (1.0 - System.Math.Exp(-dv / (Isp * 9.80665)));
        _output.WriteLine($"{integratorName}: dv {dv:R} m/s, fuel burned {maneuver.FuelBurned:R} kg, final {spacecraft.GetGeometricStateRelativeTo(Window.EndDate, earth).Position}");
        Assert.True(dv > 1.0);
        Assert.Single(spacecraft.ExecutedManeuvers);
        Assert.Equal(expected, maneuver.FuelBurned, expected * 1e-12);
        Assert.Equal(Fuel - maneuver.FuelBurned, engine.FuelTank.Quantity, 1e-9);
    }

    [Theory]
    [InlineData("RK78")]
    [InlineData("VV")]
    public void Attitude_OwnEngine_CustomAxes_FrontAlongVelocity(string integratorName)
    {
        // Arrange: BodyFront +X, which the static axes would leave 90° off
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = NewSpacecraft(earth, out var engine, Vector3.VectorX, Vector3.VectorY.Inverse());
        spacecraft.SetManeuvers(new ProgradeAttitude(Epoch, TimeSpan.FromMinutes(30.0), earth));

        // Act
        spacecraft.Propagate(Window, new PropagationOptions { Integrator = IntegratorOf(integratorName)(), OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });

        // Assert: the front of the record along the unit velocity at the outputs
        double worst = 0.0;
        for (var t = Epoch; t <= Window.EndDate; t = t.AddSeconds(Output.TotalSeconds))
        {
            var velocity = spacecraft.GetGeometricStateRelativeTo(t, earth).Velocity;
            var front = spacecraft.BodyFront.Rotate(spacecraft.Frame.GetStateOrientationToIcrf(t).Rotation);
            worst = System.Math.Max(worst, AttitudeRecordTests.Angle(front, velocity));
        }

        _output.WriteLine($"MEASURE #676 prograde front against the velocity, BodyFront +X, {integratorName}: {worst:E3} rad");
        Assert.True(worst <= ProgradeFrontTolerance, $"{worst:E3} rad");
    }
}
