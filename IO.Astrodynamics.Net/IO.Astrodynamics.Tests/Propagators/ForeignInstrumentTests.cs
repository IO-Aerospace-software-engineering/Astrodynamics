// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// An attitude points the instruments of the spacecraft propagated (#797, decision 66 of #668, decided by S. Guillet,
/// 2026-11-23): an <see cref="InstrumentPointingToAttitude"/> or a <see cref="TriadAttitude"/> built on another
/// spacecraft's instrument ran, pointing that instrument's boresight (0.577 rad of wrong pointing in the probe of L5-26),
/// on its own engine. <see cref="Spacecraft.SetManeuvers"/> refuses it (<see cref="ArgumentException"/>, nothing changed),
/// and the propagator constructor and <c>Propagate</c> refuse a chain that holds one (<see cref="InvalidOperationException"/>),
/// beside the engine check, for each form: the pointing, the TRIAD of one instrument, and the TRIAD of two, the foreign
/// instrument primary or secondary.
/// </summary>
/// <remarks>
/// The case: a 7 000 km orbit, e = 0.01, i = 0.9 rad, about a point-mass Earth from 2024-01-01 00:00 TDB, a window of 2 h
/// with outputs every 60 s. Each spacecraft holds a tank, an engine and two circular instruments, boresights +Z and +X.
/// </remarks>
public class ForeignInstrumentTests
{
    private static readonly Time Epoch = new(2024, 1, 1, 0, 0, 0, TimeScale.TDB);
    private static readonly Window Window = new(Epoch, Epoch.AddSeconds(7200.0));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static readonly TimeSpan Hold = TimeSpan.FromMinutes(10.0);
    private static int _nextId = -797000;

    public ForeignInstrumentTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    private static Spacecraft NewSpacecraft(CelestialBody earth, out Engine engine)
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "FI" + -id, 1000.0, new KeplerianElements(7000e3, 0.01, 0.9, 0.0, 0.0, System.Math.PI, earth, Epoch, Frame.ICRF), 10000.0,
            new Clock("FI" + -id, 65536));
        var tank = new FuelTank("TANK" + -id, "MODEL", "SN" + -id, 3000.0, 3000.0);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("ENGINE" + -id, "MODEL", "SN" + -id, 320.0, 5.0, tank);
        spacecraft.AddEngine(engine);
        spacecraft.AddCircularInstrument("CAMZ" + -id, 0.1, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: id * 1000 - 1, model: "MODEL");
        spacecraft.AddCircularInstrument("CAMX" + -id, 0.1, Vector3.VectorX, Vector3.VectorY, Vector3.Zero, naifId: id * 1000 - 2, model: "MODEL");
        return spacecraft;
    }

    private static Instrument Camera(Spacecraft spacecraft, string axis) =>
        spacecraft.Instruments.Single(x => x.Name.StartsWith("CAM" + axis, StringComparison.Ordinal));

    // The attitude of the form on the instruments of instrumentsOf, on the engine of the spacecraft propagated
    private static Attitude AttitudeOf(string form, Spacecraft own, Spacecraft other, Engine engine)
    {
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);
        return form switch
        {
            "Pointing" => new InstrumentPointingToAttitude(Epoch, Hold, Camera(other, "Z"), moon, engine, Aberration.None),
            "Triad" => new TriadAttitude(Epoch, Hold, Camera(other, "Z"), moon, sun, engine),
            "TriadPrimary" => new TriadAttitude(Epoch, Hold, Camera(other, "Z"), moon, Camera(own, "X"), sun, engine),
            "TriadSecondary" => new TriadAttitude(Epoch, Hold, Camera(own, "Z"), moon, Camera(other, "X"), sun, engine),
            _ => throw new ArgumentOutOfRangeException(nameof(form))
        };
    }

    private static PropagationOptions Options(CelestialBody earth, string integrator) => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : RK78IntegratorOptions.Default,
        OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None
    };

    [Theory]
    [InlineData("Pointing")]
    [InlineData("Triad")]
    [InlineData("TriadPrimary")]
    [InlineData("TriadSecondary")]
    public void SetManeuvers_RefusesAnAttitudeOnAnotherSpacecraftsInstrument(string form)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var other = NewSpacecraft(earth, out _);
        var own = new ProgradeAttitude(earth, Epoch, TimeSpan.Zero, engine);
        spacecraft.SetManeuvers(own);
        var foreign = AttitudeOf(form, spacecraft, other, engine);

        var exception = Record.Exception(() => spacecraft.SetManeuvers(own, foreign));

        var rejected = Assert.IsType<ArgumentException>(exception);
        Assert.Equal("maneuvers", rejected.ParamName);
        Assert.Contains(other.Name, rejected.Message);
        Assert.Contains(spacecraft.Name, rejected.Message);
        Assert.Equal([own], spacecraft.Maneuvers);
        Assert.Same(spacecraft, own.PlanOwner);
        Assert.Null(own.NextManeuver);
        Assert.Null(foreign.PlanOwner);
    }

    // The chain the run meets, past the claim: the foreign attitude linked after the own one through the internal setter of
    // NextManeuver, which a test reaches, so that the run's own re-check is what throws
    private static void PlanPastTheClaim(string form, Spacecraft spacecraft, Spacecraft other, Engine engine, CelestialBody earth)
    {
        var own = new ProgradeAttitude(earth, Epoch, TimeSpan.Zero, engine);
        var foreign = AttitudeOf(form, spacecraft, other, engine);
        Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(own, foreign));
        spacecraft.SetManeuvers(own);
        own.NextManeuver = foreign;
    }

    [Theory]
    [InlineData("Pointing", "RK78")]
    [InlineData("Pointing", "VV")]
    [InlineData("Triad", "RK78")]
    [InlineData("Triad", "VV")]
    [InlineData("TriadPrimary", "RK78")]
    [InlineData("TriadPrimary", "VV")]
    [InlineData("TriadSecondary", "RK78")]
    [InlineData("TriadSecondary", "VV")]
    public void ThePropagatorConstructor_RefusesAChainWithAnotherSpacecraftsInstrument(string form, string integrator)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var other = NewSpacecraft(earth, out _);
        PlanPastTheClaim(form, spacecraft, other, engine, earth);

        var exception = Record.Exception(() => new CentralBodyPropagator(Window, spacecraft, Options(earth, integrator)));
        var throughSpacecraft = Record.Exception(() => spacecraft.Propagate(Window, Options(earth, integrator)));

        foreach (var e in new[] { exception, throughSpacecraft })
        {
            var rejected = Assert.IsType<InvalidOperationException>(e);
            Assert.Contains(other.Name, rejected.Message);
            Assert.Contains(spacecraft.Name, rejected.Message);
        }

        Assert.False(spacecraft.IsPropagated);
        Assert.Empty(spacecraft.ExecutedManeuvers);
    }

    [Theory]
    [InlineData("Pointing", "RK78")]
    [InlineData("Pointing", "VV")]
    [InlineData("Triad", "RK78")]
    [InlineData("Triad", "VV")]
    [InlineData("TriadPrimary", "RK78")]
    [InlineData("TriadPrimary", "VV")]
    [InlineData("TriadSecondary", "RK78")]
    [InlineData("TriadSecondary", "VV")]
    public void Propagate_RefusesAChainWithAnotherSpacecraftsInstrument(string form, string integrator)
    {
        // Arrange: a propagator built on a valid plan, the foreign attitude linked after it
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var other = NewSpacecraft(earth, out _);
        var own = new ProgradeAttitude(earth, Epoch, TimeSpan.Zero, engine);
        spacecraft.SetManeuvers(own);
        var propagator = new CentralBodyPropagator(Window, spacecraft, Options(earth, integrator));
        var foreign = AttitudeOf(form, spacecraft, other, engine);
        own.NextManeuver = foreign;

        // Act
        var exception = Record.Exception(() => propagator.Propagate());

        // Assert: refused before anything changes
        var rejected = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(other.Name, rejected.Message);
        Assert.Contains(spacecraft.Name, rejected.Message);
        Assert.False(spacecraft.IsPropagated);
        Assert.Empty(spacecraft.ExecutedManeuvers);
        Assert.Empty(spacecraft.Frame.GetStateOrientationsToICRF());
    }

    /// <summary>
    /// The control: the same forms on the spacecraft's own instruments are set and run, each attitude executed.
    /// </summary>
    [Theory]
    [InlineData("Pointing")]
    [InlineData("Triad")]
    [InlineData("TriadPrimary")]
    [InlineData("TriadSecondary")]
    public void AnAttitudeOnItsOwnInstruments_Runs(string form)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var attitude = AttitudeOf(form, spacecraft, spacecraft, engine);

        spacecraft.SetManeuvers(attitude);
        spacecraft.Propagate(Window, Options(earth, "VV"));

        Assert.Equal([attitude], spacecraft.ExecutedManeuvers);
    }
}
