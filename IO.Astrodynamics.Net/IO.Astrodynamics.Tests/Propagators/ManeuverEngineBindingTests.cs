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
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
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
/// Tsiolkovsky's closed form and the prograde law (the unit velocity).
/// </remarks>
public class ManeuverEngineBindingTests
{
    private static readonly Time Epoch = new(2024, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
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

    private static Spacecraft NewSpacecraft(CelestialBody earth, out Engine engine, Vector3? front = null, Vector3? right = null)
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "BIND" + -id, DryMass, 10000.0, new Clock("BIND" + -id, 65536), Orbit(earth),
            bodyFront: front, bodyRight: right, bodyUp: front.HasValue ? Vector3.VectorZ : null);
        var tank = new FuelTank("TANK" + -id, "MODEL", "SN" + -id, Fuel, Fuel);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("ENGINE" + -id, "MODEL", "SN" + -id, Isp, 5.0, tank);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    private static Func<Integrator> IntegratorOf(string name) => name switch
    {
        "RK78" => () => new RK78Integrator(),
        "VV" => () => new VVIntegrator(TimeSpan.FromSeconds(10.0)),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    // The activation paths: the call that must throw, or, for the batch, the error of its task
    private static Exception Activate(string path, Spacecraft spacecraft, CelestialBody earth, Func<Integrator> integrator)
    {
        CelestialItem[] bodies = [earth];
        switch (path)
        {
            case "Propagate":
                return Record.Exception(() => spacecraft.Propagate(Window, bodies, integrator(), false, false, Output));
            case "PropagateDefault":
                return Record.Exception(() => spacecraft.Propagate(Window, bodies, false, false, Output));
            case "PropagateAsync":
                return Record.Exception(() => spacecraft.PropagateAsync(Window, bodies, false, false, Output).GetAwaiter().GetResult());
            case "Builder":
                return Record.Exception(() => new CentralBodyPropagatorBuilder(Window, spacecraft, integrator(), Output).Build().Propagate());
            case "Constructor":
                return Record.Exception(() =>
                {
                    using var propagator = new CentralBodyPropagator(Window, spacecraft, integrator(), bodies, false, false, Output);
                    propagator.Propagate();
                });
            case "Scenario":
            case "ScenarioDefault":
            {
                var scenario = new Scenario("BIND", new IO.Astrodynamics.Mission.Mission("BIND"), Window);
                scenario.AddCelestialItem(earth);
                scenario.AddSpacecraft(spacecraft);
                return Record.Exception(() => (path == "Scenario"
                    ? scenario.SimulateAsync(false, false, Output, integrator)
                    : scenario.SimulateAsync(false, false, Output)).GetAwaiter().GetResult());
            }
            case "Batch":
            case "BatchDefault":
            {
                var task = new PropagationTask(spacecraft, Window, bodies, false, false, Output, path == "Batch" ? integrator : null);
                var result = BatchPropagator.Propagate([task]).Results.Single();
                Assert.False(result.Succeeded);
                Assert.Null(result.Solution);
                return result.Error;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(path));
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        (string Path, string[] Integrators)[] paths =
        [
            ("Propagate", ["RK78", "VV"]), ("Builder", ["RK78", "VV"]), ("Constructor", ["RK78", "VV"]),
            ("Scenario", ["RK78", "VV"]), ("Batch", ["RK78", "VV"]),
            // The paths of the default integrator, Velocity-Verlet
            ("PropagateDefault", ["VV"]), ("PropagateAsync", ["VV"]), ("ScenarioDefault", ["VV"]), ("BatchDefault", ["VV"])
        ];
        foreach (var (path, integrators) in paths)
        foreach (var integrator in integrators)
        foreach (var chain in new[] { "Impulse", "Attitude", "ImpulseSecond" })
        foreach (var engine in new[] { "UnattachedTank", "OtherSpacecraft" })
        {
            yield return [path, integrator, chain, engine];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EngineNotOnSpacecraft_Throws(string path, string integratorName, string chain, string engineKind)
    {
        // Arrange: a valid run on the spacecraft's own engine, whose state the rejected call must leave as it was
        var integrator = IntegratorOf(integratorName);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var ownEngine);
        spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(earth, Epoch, TimeSpan.Zero, 7300e3, ownEngine));
        spacecraft.Propagate(Window, [earth], integrator(), false, false, Output);
        Assert.Single(spacecraft.ExecutedManeuvers);

        var other = NewSpacecraft(earth, out var otherEngine);
        var foreignEngine = engineKind == "OtherSpacecraft"
            ? otherEngine
            : new Engine("LOOSE", "MODEL", "SN-LOOSE", Isp, 5.0, new FuelTank("LOOSE", "MODEL", "SN-LOOSE", Fuel, Fuel));
        Maneuver.Maneuver head = chain switch
        {
            "Impulse" => new ApogeeHeightManeuver(earth, Epoch, TimeSpan.Zero, 7300e3, foreignEngine),
            "Attitude" => new ProgradeAttitude(earth, Epoch, TimeSpan.FromMinutes(10.0), foreignEngine),
            _ => new ProgradeAttitude(earth, Epoch, TimeSpan.FromMinutes(10.0), ownEngine)
        };
        if (chain == "ImpulseSecond")
        {
            head.SetNextManeuver(new ApogeeHeightManeuver(earth, Epoch, TimeSpan.Zero, 7300e3, foreignEngine));
        }

        spacecraft.SetStandbyManeuver(head);

        var ownTank = spacecraft.FuelTanks.Single();
        var otherTank = other.FuelTanks.Single();
        var states = spacecraft.InterpolatedStatesWindow;
        var midState = spacecraft.GetGeometricStateRelativeTo(Epoch.AddSeconds(3600.0), earth);
        var lastPropagation = spacecraft.LastPropagation;
        var record = spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
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
        Assert.Equal(states, spacecraft.InterpolatedStatesWindow);
        var midAfter = spacecraft.GetGeometricStateRelativeTo(Epoch.AddSeconds(3600.0), earth);
        Assert.Equal(midState.Position, midAfter.Position);
        Assert.Equal(midState.Velocity, midAfter.Velocity);
        Assert.Same(lastPropagation, spacecraft.LastPropagation);
        Assert.Equal(record, spacecraft.Frame.GetStateOrientationsToICRF().ToArray());
        Assert.Equal(executed, spacecraft.ExecutedManeuvers.ToArray());
        Assert.Equal(ownQuantity, ownTank.Quantity);
        Assert.Equal(foreignQuantity, foreignEngine.FuelTank.Quantity);
        Assert.Equal(Fuel, otherTank.Quantity);
        Assert.Empty(other.ExecutedManeuvers);
        if (engineKind == "OtherSpacecraft")
        {
            Assert.Same(other, foreignEngine.FuelTank.Spacecraft);
        }
        else
        {
            Assert.Null(foreignEngine.FuelTank.Spacecraft);
        }
    }

    [Fact]
    public void SharedTank_Rejected()
    {
        // Arrange: #586's second setup, B given A's tank
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
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
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
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
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var maneuver = new ApogeeHeightManeuver(earth, Epoch, TimeSpan.Zero, 7300e3, engine);
        spacecraft.SetStandbyManeuver(maneuver);
        double m0 = spacecraft.DryOperatingMass + Fuel;

        // Act
        spacecraft.Propagate(Window, [earth], IntegratorOf(integratorName)(), false, false, Output);

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
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var engine, Vector3.VectorX, Vector3.VectorY.Inverse());
        spacecraft.SetStandbyManeuver(new ProgradeAttitude(earth, Epoch, TimeSpan.FromMinutes(30.0), engine));

        // Act
        spacecraft.Propagate(Window, [earth], IntegratorOf(integratorName)(), false, false, Output);

        // Assert: the front of the record along the unit velocity at the outputs
        double worst = 0.0;
        for (var t = Epoch; t <= Window.EndDate; t = t.AddSeconds(Output.TotalSeconds))
        {
            var velocity = spacecraft.GetGeometricStateRelativeTo(t, earth).Velocity;
            var front = spacecraft.BodyFront.Rotate(spacecraft.Frame.GetStateOrientationToICRF(t).Rotation);
            worst = System.Math.Max(worst, AttitudeRecordTests.Angle(front, velocity));
        }

        _output.WriteLine($"MEASURE #676 prograde front against the velocity, BodyFront +X, {integratorName}: {worst:E3} rad");
        Assert.True(worst <= ProgradeFrontTolerance, $"{worst:E3} rad");
    }
}
