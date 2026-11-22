// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;
using OrbitalParams = IO.Astrodynamics.OrbitalParameters.OrbitalParameters;

public partial class IntegratorEventTests
{
    // ------------------------------------------------------------------------------------------------------------------
    // #572: the label of a Velocity-Verlet event
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>A perigee raise whose precondition never holds: every apogee is an event that continues the propagation,
    /// with no ΔV.</summary>
    private sealed class NeverPerigeeRaise : PerigeeHeightManeuver
    {
        internal NeverPerigeeRaise(CelestialItem center, Time minimumEpoch, Engine engine) : base(center, minimumEpoch, TimeSpan.Zero, 6878137.0, engine)
        {
        }

        internal int Checks { get; private set; }

        public override bool CheckPreconditions(StateVector state)
        {
            Checks++;
            return false;
        }
    }

    /// <summary>
    /// Velocity-Verlet with events that change nothing (a perigee raise whose precondition fails at each apogee,
    /// <see cref="EventAction.Continue"/>), about the Earth (TT) and the Moon (TDB), at whole and fractional steps: the
    /// segment after each event starts at the label of the state it carries, so the trajectory is the one without events
    /// (#572: each restart labelled the state up to half a tick from its instant, 3.4e-3 m along track after 8 apogees
    /// before the nearest-tick rounding of #625). Each event's base and the end of its segment are the same instant. The
    /// reference is the same propagation without the maneuver.
    /// </summary>
    [Theory]
    [InlineData("Earth", 1.0)]
    [InlineData("Earth", 10.0)]
    [InlineData("Earth", 0.7)]
    [InlineData("Moon", 1.0)]
    [InlineData("Moon", 0.7)]
    public void VelocityVerletEvent_TheNextSegmentStartsAtTheInstantOfItsState(string centerName, double step)
    {
        bool moon = centerName == "Moon";
        var center = new CelestialBody(moon ? PlanetsAndMoons.MOON : PlanetsAndMoons.EARTH, Frame.ICRF, S);
        KeplerianElements Orbit() => moon
            ? new KeplerianElements(MinimumEpochGateTests.MoonRadius + 600000.0, 0.1, 0.9, 0.3, 1.1, 0.5, center, S, Frame.ICRF)
            : new KeplerianElements(MinimumEpochGateTests.A0, MinimumEpochGateTests.E0, 0.9, 0.3, 1.1, 0.5, center, S, Frame.ICRF);
        var window = new Window(S, S.AddHours(12.0));
        var (withEvents, engine) = NewSpacecraft(moon ? -7921 : -7922, Orbit());
        var never = new NeverPerigeeRaise(center, S, engine);
        withEvents.SetStandbyManeuver(never);
        var (without, _) = NewSpacecraft(moon ? -7923 : -7924, Orbit());
        var integratorStep = TimeSpan.FromSeconds(step);
        var output = TimeSpan.FromSeconds(60.0);
        var solution = withEvents.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = integratorStep }, OutputStep = output, ThirdBodies = [center] });
        var free = without.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = integratorStep }, OutputStep = output, ThirdBodies = [center] });

        Assert.True(never.Checks >= 4, $"{never.Checks} events");
        Assert.Equal(never.Checks + 1, solution.Segments.Count);
        var argument = solution.TimeArgument;
        double worstLabel = 0.0;
        for (int i = 1; i < solution.Segments.Count; i++)
        {
            var previous = solution.Segments[i - 1];
            // The base of the next segment is the instant at which the previous one's state was taken
            double offset = argument.SecondsBetween(previous.BaseEpoch, solution.Segments[i].BaseEpoch) - previous.Duration;
            worstLabel = System.Math.Max(worstLabel, System.Math.Abs(offset));
        }

        double position = (solution.StateVectors[^1].Position - free.StateVectors[^1].Position).Magnitude();
        _output.WriteLine($"MEASURE #572 VV events {centerName} {step} s: {never.Checks} events, worst |base − end of the previous segment| {worstLabel:E3} s, final position less the propagation without events {position:E3} m (limit {ContinueChainAgainstFree:E3})");
        // The event time is the seconds of the label, the base of the next segment: the same double, measured 0 in every case
        Assert.Equal(0.0, worstLabel);
        Assert.True(position <= ContinueChainAgainstFree, $"{position:E3} m");
    }

    /// <summary>The final position of a Velocity-Verlet propagation with continued events less the same propagation without
    /// them, m: Velocity-Verlet's own restart noise, its global error moved by the grid of its steps shifted by up to half a
    /// tick at each event (the trajectory of Velocity-Verlet depends on the phase of its grid). Worst 1.077e-4 m on Linux
    /// x64, Windows x64 and macOS arm64 alike (CI run 44225490669; Earth, 0.7 s steps, 7 events; 1.60e-5 m at 1 s, 1.21e-6 m
    /// at 10 s, Moon 2.90e-5 m at 0.7 s and 0 at 1 s, where the step ends are ticks); before #572, 1.32e-3 to 2.03e-3 m
    /// about the Earth. Twice the worst, rounded up.</summary>
    internal const double ContinueChainAgainstFree = 2.2e-4;
}
