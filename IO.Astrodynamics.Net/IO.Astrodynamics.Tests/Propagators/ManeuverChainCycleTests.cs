// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// A cyclic maneuver chain is rejected with an exception (#585). Before, <c>m.SetNextManeuver(m)</c> or a chain closed on
/// an earlier maneuver ended the process at the first <c>Spacecraft.Propagate</c> (a stack overflow in
/// <c>Maneuver.Reset</c>, exit code 134), and a propagation through the builder never returned (#585's measurements, Linux
/// x64, 7f1cc0b8).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Maneuver.Maneuver.SetNextManeuver"/> throws <see cref="ArgumentException"/> and leaves the chain as it was.
/// A cycle built through a subclass, which sets the protected <see cref="Maneuver.Maneuver.NextManeuver"/>, is rejected by
/// <see cref="Spacecraft.SetStandbyManeuver"/> (<see cref="ArgumentException"/>), by the construction of the propagator
/// (<c>Spacecraft.Propagate</c>, the builder, the batch: <see cref="InvalidOperationException"/>, before the states of a
/// previous propagation are cleared) and by <see cref="PropagatorBase.Propagate"/> for a cycle made after the construction.
/// </para>
/// <para>
/// The case is #585's: a 500 × 1 000 km orbit (6 878 137 × 7 378 137 m), i = 0.5 rad, from its perigee at
/// 2025-06-01 00:00 TDB about a point-mass Earth; 500 kg dry, 800 kg of fuel, Isp 320 s, 5 kg/s; a window of 6 h. These
/// tests check exceptions and the links of a chain: they compare no value with a reference. A valid chain's propagation is
/// checked against the Keplerian closed form by <see cref="RepropagationTests"/>, <see cref="MinimumEpochGateTests"/> and
/// <see cref="AttitudeTimelineTests"/>, which the change leaves to pass.
/// </para>
/// </remarks>
public class ManeuverChainCycleTests
{
    private static readonly Time S = new(2025, 6, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
    private static readonly Window SixHours = new(S, S.AddSeconds(6.0 * 3600.0));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static int _nextId = -585000;

    public ManeuverChainCycleTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    /// <summary>A nadir attitude whose next maneuver a test sets past <see cref="Maneuver.Maneuver.SetNextManeuver"/>.</summary>
    private sealed class LinkedNadir(CelestialItem center, Time minimumEpoch, TimeSpan hold, Engine engine)
        : NadirAttitude(center, minimumEpoch, hold, engine)
    {
        public void ForceNext(Maneuver.Maneuver next) => NextManeuver = next;
    }

    /// <summary>An apogee height maneuver whose next maneuver a test sets past <see cref="Maneuver.Maneuver.SetNextManeuver"/>.</summary>
    private sealed class LinkedApogee(CelestialItem center, Time minimumEpoch, TimeSpan hold, double apogee, Engine engine)
        : ApogeeHeightManeuver(center, minimumEpoch, hold, apogee, engine)
    {
        public void ForceNext(Maneuver.Maneuver next) => NextManeuver = next;
    }

    private sealed class Setup
    {
        public CelestialBody Earth { get; init; }
        public Spacecraft Spacecraft { get; init; }
        public Engine Engine { get; init; }

        public LinkedApogee Apogee() => new(Earth, S, TimeSpan.Zero, 8378137.0, Engine);
        public PerigeeHeightManeuver Perigee() => new(Earth, S, TimeSpan.Zero, 7378137.0, Engine);
        public LinkedNadir Nadir() => new(Earth, S, TimeSpan.FromSeconds(600.0), Engine);
    }

    private static Setup Create()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var orbit = new KeplerianElements(7128137.0, 500.0 / 7128.137, 0.5, 0.0, 0.0, 0.0, earth, S, Frame.ICRF);
        int id = System.Threading.Interlocked.Decrement(ref _nextId);
        var spacecraft = new Spacecraft(id, $"CYC{-id}", 500.0, 3000.0, new Clock($"CYC{-id}_CLK", 65536), orbit);
        var tank = new FuelTank("ft", "model", "sn", 800.0, 800.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 320.0, 5.0, tank);
        spacecraft.AddEngine(engine);
        return new Setup { Earth = earth, Spacecraft = spacecraft, Engine = engine };
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // SetNextManeuver
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("apo>apo")]
    [InlineData("nadir>nadir")]
    [InlineData("apo>peri>apo")]
    [InlineData("apo>nadir>apo")]
    [InlineData("nadir>apo>peri>nadir")]
    public void SetNextManeuver_ClosingACycle_Throws_AndLeavesTheChain(string chain)
    {
        var setup = Create();
        var names = chain.Split('>');
        var made = new Dictionary<string, Maneuver.Maneuver>();
        var maneuvers = names.Select(n => made.TryGetValue(n, out var m)
            ? m
            : made[n] = n switch { "apo" => setup.Apogee(), "peri" => setup.Perigee(), _ => setup.Nadir() }).ToList();

        // Every link but the last, which would close the cycle
        for (int i = 0; i < maneuvers.Count - 2; i++)
        {
            maneuvers[i].SetNextManeuver(maneuvers[i + 1]);
        }

        var last = maneuvers[^2];
        var before = last.NextManeuver;
        var e = Assert.Throws<ArgumentException>(() => last.SetNextManeuver(maneuvers[^1]));
        Assert.Equal("maneuver", e.ParamName);
        Assert.Contains("cycle", e.Message);
        Assert.Same(before, last.NextManeuver);
        // The chain from the head still ends
        Assert.Equal(maneuvers.Count - 1, Maneuver.Maneuver.Chain(maneuvers[0]).Count);
    }

    [Fact]
    public void SetNextManeuver_OntoAChainThatHoldsACycle_Throws()
    {
        var setup = Create();
        var nadir = setup.Nadir();
        nadir.ForceNext(nadir);
        var apo = setup.Apogee();
        Assert.Throws<ArgumentException>(() => apo.SetNextManeuver(nadir));
        Assert.Null(apo.NextManeuver);
    }

    [Fact]
    public void SetNextManeuver_ValidLinks_AreKept()
    {
        var setup = Create();
        var apo = setup.Apogee();
        var nadir = setup.Nadir();
        var peri = setup.Perigee();
        Assert.Same(nadir, apo.SetNextManeuver(nadir));
        Assert.Same(peri, nadir.SetNextManeuver(peri));
        Assert.Equal(new Maneuver.Maneuver[] { apo, nadir, peri }, Maneuver.Maneuver.Chain(apo));

        // Two chains may share their tail: no cycle
        var other = setup.Apogee();
        other.SetNextManeuver(peri);
        Assert.Equal(new Maneuver.Maneuver[] { other, peri }, Maneuver.Maneuver.Chain(other));

        // Relinking and ending a chain
        Assert.Same(peri, apo.SetNextManeuver(peri));
        Assert.Null(apo.SetNextManeuver(null));
        Assert.Null(apo.NextManeuver);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // A cycle built through a subclass
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The cycles a subclass can build: a leading attitude, an impulse, an attitude after an impulse.</summary>
    private static Maneuver.Maneuver Cycle(Setup setup, string kind)
    {
        switch (kind)
        {
            case "nadir>nadir":
            {
                var nadir = setup.Nadir();
                nadir.ForceNext(nadir);
                return nadir;
            }
            case "apo>apo":
            {
                var apo = setup.Apogee();
                apo.ForceNext(apo);
                return apo;
            }
            case "apo>nadir>nadir":
            {
                var apo = setup.Apogee();
                var nadir = setup.Nadir();
                apo.SetNextManeuver(nadir);
                nadir.ForceNext(nadir);
                return apo;
            }
            case "apo>nadir>apo":
            {
                var apo = setup.Apogee();
                var nadir = setup.Nadir();
                apo.SetNextManeuver(nadir);
                nadir.ForceNext(apo);
                return apo;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    [Theory]
    [InlineData("nadir>nadir")]
    [InlineData("apo>apo")]
    [InlineData("apo>nadir>nadir")]
    [InlineData("apo>nadir>apo")]
    public void SetStandbyManeuver_ACycle_Throws(string kind)
    {
        var setup = Create();
        var head = Cycle(setup, kind);
        var e = Assert.Throws<ArgumentException>(() => setup.Spacecraft.SetStandbyManeuver(head));
        Assert.Equal("maneuver", e.ParamName);
        Assert.Null(setup.Spacecraft.StandbyManeuver);
        Assert.Null(setup.Spacecraft.InitialManeuver);
    }

    private static Integrator Integrator(string integrator) =>
        integrator == "VV" ? new VVIntegrator(TimeSpan.FromSeconds(1.0)) : new RK78Integrator(1e-12, 1e-12);

    /// <summary>
    /// The head of the chain set in standby, then the chain closed on it: the cycle the propagation meets. The head is an
    /// apogee maneuver or a nadir attitude, the cycle after it one of <paramref name="kind"/>.
    /// </summary>
    private static void CloseAfterStandby(Setup setup, string kind)
    {
        if (kind.StartsWith("nadir"))
        {
            var nadir = setup.Nadir();
            setup.Spacecraft.SetStandbyManeuver(nadir);
            nadir.ForceNext(nadir);
            return;
        }

        var apo = setup.Apogee();
        setup.Spacecraft.SetStandbyManeuver(apo);
        switch (kind)
        {
            case "apo>apo":
                apo.ForceNext(apo);
                break;
            case "apo>nadir>nadir":
                var nadirAfter = setup.Nadir();
                apo.SetNextManeuver(nadirAfter);
                nadirAfter.ForceNext(nadirAfter);
                break;
            case "apo>nadir>apo":
                var nadirBack = setup.Nadir();
                apo.SetNextManeuver(nadirBack);
                nadirBack.ForceNext(apo);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    [Theory]
    [InlineData("nadir>nadir", "RK")]
    [InlineData("nadir>nadir", "VV")]
    [InlineData("apo>apo", "RK")]
    [InlineData("apo>apo", "VV")]
    [InlineData("apo>nadir>nadir", "RK")]
    [InlineData("apo>nadir>nadir", "VV")]
    [InlineData("apo>nadir>apo", "RK")]
    [InlineData("apo>nadir>apo", "VV")]
    public void SpacecraftPropagate_ACycle_Throws_AndKeepsThePreviousPropagation(string kind, string integrator)
    {
        var setup = Create();
        var bodies = new CelestialItem[] { setup.Earth };
        // A propagation without maneuvers first: the rejected one must leave its states
        setup.Spacecraft.Propagate(SixHours, bodies, Integrator(integrator), false, false, Output);
        Assert.True(setup.Spacecraft.IsPropagated);
        var states = setup.Spacecraft.PropagatedStates.Length;

        CloseAfterStandby(setup, kind);
        var e = Assert.Throws<InvalidOperationException>(() =>
            setup.Spacecraft.Propagate(SixHours, bodies, Integrator(integrator), false, false, Output));
        Assert.Contains("cycle", e.Message);
        Assert.True(setup.Spacecraft.IsPropagated);
        Assert.Equal(states, setup.Spacecraft.PropagatedStates.Length);

        // The overload without an integrator and the one with the covariance options
        Assert.Throws<InvalidOperationException>(() => setup.Spacecraft.Propagate(SixHours, bodies, false, false, Output));
        Assert.Throws<InvalidOperationException>(() =>
            setup.Spacecraft.Propagate(SixHours, bodies, Integrator(integrator), false, false, Output, null));
    }

    [Theory]
    [InlineData("nadir>nadir", "RK")]
    [InlineData("nadir>nadir", "VV")]
    [InlineData("apo>apo", "RK")]
    [InlineData("apo>nadir>nadir", "RK")]
    [InlineData("apo>nadir>nadir", "VV")]
    [InlineData("apo>nadir>apo", "RK")]
    public void Builder_ACycle_ThrowsAtBuild(string kind, string integrator)
    {
        var setup = Create();
        CloseAfterStandby(setup, kind);
        var builder = new CentralBodyPropagatorBuilder(SixHours, setup.Spacecraft, Integrator(integrator), Output);
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Theory]
    [InlineData("nadir>nadir", "RK")]
    [InlineData("nadir>nadir", "VV")]
    [InlineData("apo>apo", "RK")]
    [InlineData("apo>apo", "VV")]
    [InlineData("apo>nadir>nadir", "RK")]
    [InlineData("apo>nadir>apo", "VV")]
    public void Builder_ACycleMadeAfterTheBuild_ThrowsAtPropagate(string kind, string integrator)
    {
        var setup = Create();
        var apo = setup.Apogee();
        var nadir = setup.Nadir();
        // The chain is valid when the propagator is built
        setup.Spacecraft.SetStandbyManeuver(kind.StartsWith("nadir") ? nadir : apo);
        if (kind != "nadir>nadir" && kind != "apo>apo") apo.SetNextManeuver(nadir);
        using var propagator = new CentralBodyPropagatorBuilder(SixHours, setup.Spacecraft, Integrator(integrator), Output).Build();

        switch (kind)
        {
            case "nadir>nadir":
            case "apo>nadir>nadir":
                nadir.ForceNext(nadir);
                break;
            case "apo>apo":
                apo.ForceNext(apo);
                break;
            default:
                nadir.ForceNext(apo);
                break;
        }

        var e = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        Assert.Contains("cycle", e.Message);
    }

    [Fact]
    public void BatchPropagator_ACycle_IsAnError()
    {
        var setup = Create();
        CloseAfterStandby(setup, "apo>nadir>apo");
        var task = new PropagationTask(setup.Spacecraft, SixHours, [setup.Earth], false, false, Output,
            () => new RK78Integrator(1e-12, 1e-12));
        var result = BatchPropagator.Propagate([task]).Results[0];
        Assert.False(result.Succeeded);
        Assert.IsType<InvalidOperationException>(result.Error);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Reset in a loop
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A chain of 200 000 attitudes, longer than the 37 373 frames of the recursion that overflowed on #585's cycle:
    /// <c>Spacecraft.ResetPropagation</c> resets it in a loop, so a long chain uses no stack.
    /// </summary>
    [Fact]
    public void ResetChain_ALongChain_UsesNoStack()
    {
        var setup = Create();
        var head = setup.Nadir();
        var current = (Maneuver.Maneuver)head;
        for (int i = 0; i < 200_000; i++)
        {
            current = current.SetNextManeuver(new NadirAttitude(setup.Earth, S, TimeSpan.Zero, setup.Engine));
            current.FuelBurned = 1.0;
        }

        Maneuver.Maneuver.ResetChain(head);
        Assert.Equal(0.0, current.FuelBurned);
    }
}
