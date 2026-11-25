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
using IO.Astrodynamics.Propagator.Variational;
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
    // #575: an impulse at the end of the propagation
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// An <see cref="ApogeeHeightManeuver"/> at the perigee (TT 2 963.19 s) whose crossing falls in the last step: the
    /// impulse executes (ΔV, fuel, <c>ExecutedManeuvers</c>) and the solution ends with the state after it, a last segment of
    /// zero length at the burn (#575: it ended with the state before, 7 829.380 m/s where the record held a burn). The last
    /// output, <c>InterpolateAt</c> and <c>GetEphemeris</c> at the end are the state before plus the ΔV, the closed form of the
    /// burn. Velocity-Verlet at 1 s over 2 963.5 s and 2 964 s, outputs of 0.5, 4 and 10 s; at 10 s over 2 970 s.
    /// </summary>
    [Theory]
    [InlineData("VV1", 2964.0, 0.5)]
    [InlineData("VV1", 2964.0, 4.0)]
    [InlineData("VV1", 2964.0, 10.0)]
    [InlineData("VV1", 2963.5, 0.5)]
    [InlineData("VV10", 2970.0, 10.0)]
    public void ImpulseInTheLastStep_TheSolutionEndsWithTheStateAfterIt(string integratorName, double length, double outputStep)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(-7931, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var maneuver = new ApogeeHeightManeuver(8378137.0, engine, S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(maneuver);
        var window = new Window(S, MinimumEpochGateTests.GateAtTt(length));
        double massBefore = spacecraft.GetTotalMass();
        var solution = spacecraft.Propagate(window, new PropagationOptions { Integrator = NewIntegrator(integratorName), OutputStep = TimeSpan.FromSeconds(outputStep), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });

        AssertEndsAfterTheBurn(spacecraft, maneuver, solution, window, massBefore, earth);
    }

    /// <summary>
    /// A time-like user impulse whose root is the end of the window, TT 3 000 s, through RK7(8) adaptive and fixed-step and
    /// Velocity-Verlet: it fires at the end and the solution ends with the state after it (#575).
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("RKf10")]
    [InlineData("VV10")]
    public void ImpulseAtTheWindowEnd_TheSolutionEndsWithTheStateAfterIt(string integratorName)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(-7932, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var end = MinimumEpochGateTests.GateAtTt(3000.0);
        var impulse = new TimeLikeImpulse(earth, S, engine, end, 1.0);
        spacecraft.SetManeuvers(impulse);
        var window = new Window(S, end);
        double massBefore = spacecraft.GetTotalMass();
        var solution = spacecraft.Propagate(window, new PropagationOptions { Integrator = NewIntegrator(integratorName), OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });

        AssertEndsAfterTheBurn(spacecraft, impulse, solution, window, massBefore, earth);
    }

    /// <summary>
    /// The same burn in the last step through the public constructor of <see cref="CentralBodyPropagator"/> that takes a
    /// bare <see cref="IIntegrator"/>, one that returns a segment of no step for a duration of zero (as the
    /// <c>while (t &lt; duration)</c> loop of the Velocity-Verlet integrator did before 11.0.0): the propagator completes that
    /// segment with one step of zero length, which holds the state after the burn, so that the solution still ends with it
    /// and <c>InterpolateAt</c> at the end does not throw (#575: it threw "Segment contains no steps.", and the last output
    /// held the state before the burn).
    /// </summary>
    [Fact]
    public void ImpulseInTheLastStep_ACustomIntegratorWithoutAStepAtZeroDuration_TheSolutionEndsWithTheStateAfterIt()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(-7933, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var maneuver = new ApogeeHeightManeuver(8378137.0, engine, S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(maneuver);
        var window = new Window(S, MinimumEpochGateTests.GateAtTt(2964.0));
        double massBefore = spacecraft.GetTotalMass();
        var integrator = new NoStepAtZeroDuration(new VVIntegrator([new GravitationalAcceleration(earth)], TimeSpan.FromSeconds(1.0),
            spacecraft.InitialOrbitalParameters.ToStateVector()));
        var propagator = new CentralBodyPropagator(window, spacecraft, integrator, TimeSpan.FromSeconds(4.0));
        var solution = propagator.Propagate();

        Assert.Equal(1, integrator.ZeroDurationCalls);
        AssertEndsAfterTheBurn(spacecraft, maneuver, solution, window, massBefore, earth);
    }

    /// <summary>
    /// A light-time read up to 20 ms past the end of a propagation that ends with an impulse (the margin of #527, the
    /// internal reader of a corrected read) reads the arc of one state that the impulse opens (#575), touching the arc before
    /// it at the burn: the state after the impulse, as at 8436db52 (decision 90 of #668, decided by S. Guillet,
    /// 2026-11-23). The probe of the first review of lot L5a read the arc before the impulse extrapolated past it: 233.8 m/s
    /// and 7.829 m off the state after at 1 ms, 78.29 m at 10 ms (Velocity-Verlet at 1 s). The reference is the state
    /// after the burn, the state before plus the ΔV of the record (the closed form of an impulse); the read at the end itself
    /// is that state too (#575). An <see cref="ApogeeHeightManeuver"/> at the perigee in the last step of Velocity-Verlet at
    /// 1 s and 10 s, and RK7(8) over a window that ends at the burn it finds in a longer run.
    /// </summary>
    [Theory]
    [InlineData("VV1", 2964.0)]
    [InlineData("VV10", 2970.0)]
    [InlineData("RK", 0.0)]
    public void ALightTimeReadPastAnImpulseAtTheEnd_ReadsTheStateAfterIt(string integratorName, double length)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        Window window;
        if (length > 0.0)
        {
            window = new Window(S, MinimumEpochGateTests.GateAtTt(length));
        }
        else
        {
            // RK7(8) puts the burn at the root of its g-function: a first run finds it, the second ends there
            var (probe, probeEngine) = NewSpacecraft(-7935, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
            probe.SetManeuvers(new ApogeeHeightManeuver(8378137.0, probeEngine, S, TimeSpan.Zero, earth));
            var probeSolution = probe.Propagate(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), new PropagationOptions { Integrator = NewIntegrator(integratorName), OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });
            window = new Window(S, probeSolution.Segments[1].BaseEpoch);
        }

        var (spacecraft, engine) = NewSpacecraft(-7934, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var maneuver = new ApogeeHeightManeuver(8378137.0, engine, S, TimeSpan.Zero, earth);
        spacecraft.SetManeuvers(maneuver);
        var solution = spacecraft.Propagate(window, new PropagationOptions { Integrator = NewIntegrator(integratorName), OutputStep = TimeSpan.FromSeconds(4.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });

        // The burn at the end: the last arc is its state after alone, touching the arc before at the burn
        var end = window.EndDate;
        LabelAssert.Equal(end, solution.Segments[^1].BaseEpoch);
        var arcs = spacecraft.PropagatedArcs;
        var lastArc = Assert.Single(arcs[^1]);
        LabelAssert.Equal(end, lastArc.Epoch);
        LabelAssert.Equal(end, arcs[^2][^1].Epoch);
        var before = solution.Segments[^2].Steps[^1];
        var expectedVelocity = before.EndVelocity + maneuver.DeltaV;
        var atEnd = spacecraft.GetGeometricStateRelativeTo(end, earth);
        Assert.Equal(expectedVelocity, atEnd.Velocity);

        foreach (double past in new[] { 1.0e-3, 1.0e-2, 2.0e-2 })
        {
            var read = spacecraft.GetGeometricStateRelativeTo(end.AddSeconds(past), earth, 0.02);
            _output.WriteLine($"MEASURE decision 90 {integratorName} +{past * 1e3:F0} ms: |r - r_after| {(read.Position - atEnd.Position).Magnitude():E3} m, |v - v_after| {(read.Velocity - expectedVelocity).Magnitude():E3} m/s");
            Assert.Equal(atEnd.Position, read.Position);
            Assert.Equal(expectedVelocity, read.Velocity);
        }

        // Past the margin: outside the stored states
        Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(end.AddSeconds(0.03), earth, 0.02));
    }

    /// <summary>The OPM ΔV in m/s less the jump of the final velocity at the burn, m/s: the rounding of the km/s of the OPM and
    /// of the difference of two velocities. Worst 3.458e-13 m/s over the eight propagations of #575 on Linux x64, Windows x64
    /// and macOS arm64 alike (CI run 44225490669); twice it, rounded up.</summary>
    internal const double OpmDeltaVAgainstFinalJump = 7.0e-13;

    /// <summary>The fuel of the OPM maneuver less the drop of the spacecraft's mass, kg: the rounding of a difference of
    /// masses near 2 000 kg. Worst 1.421e-13 kg on Linux x64, Windows x64 and macOS arm64 alike (CI run 44225490669); twice
    /// it, rounded up.</summary>
    internal const double OpmFuelAgainstMassDrop = 2.9e-13;

    /// <summary>A bare <see cref="IIntegrator"/> that returns a segment of no step for a duration of zero or less.</summary>
    private sealed class NoStepAtZeroDuration(IIntegrator inner) : IIntegrator
    {
        internal int ZeroDurationCalls { get; private set; }

        public void Initialize(StateVector initialState) => inner.Initialize(initialState);

        public IntegrationResult IntegrateSegment(Vector3 startPosition, Vector3 startVelocity, Time baseEpoch, double duration,
            IReadOnlyList<IEventDetector> eventDetectors = null)
        {
            if (duration > 0.0)
            {
                return inner.IntegrateSegment(startPosition, startVelocity, baseEpoch, duration, eventDetectors);
            }

            ZeroDurationCalls++;
            return new IntegrationResult(new PropagationSegment(baseEpoch), null);
        }
    }

    private void AssertEndsAfterTheBurn(Spacecraft spacecraft, ImpulseManeuver maneuver, PropagationSolution solution, Window window, double massBefore, CelestialBody earth)
    {
        var end = window.EndDate;
        Assert.Contains(maneuver, spacecraft.ExecutedManeuvers);
        Assert.True(spacecraft.GetTotalMass() < massBefore);
        Assert.True(solution.Segments.Count >= 2, $"{solution.Segments.Count} segment: no segment after the burn");
        var burnEpoch = solution.Segments[^1].BaseEpoch;
        // The state before the burn: the end of the segment before the last one
        var previous = solution.Segments[^2];
        var before = previous.Steps[^1];
        var expectedVelocity = before.EndVelocity + maneuver.DeltaV;
        var last = solution.StateVectors[^1];
        var (_, interpolated) = solution.InterpolateAt(end);
        var ephemeris = spacecraft.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);
        double lastOutput = (last.Velocity - expectedVelocity).Magnitude();
        double atEnd = (interpolated - expectedVelocity).Magnitude();
        double stored = (ephemeris.Velocity - expectedVelocity).Magnitude();
        _output.WriteLine($"MEASURE #575 impulse at the end: {solution.Segments.Count} segments, burn {(end - burnEpoch).TotalSeconds:E3} s before the end, ΔV {maneuver.DeltaV.Magnitude():F3} m/s; " +
                          $"velocity less the state after: last output {lastOutput:E3}, InterpolateAt {atEnd:E3}, GetEphemeris {stored:E3} m/s");
        LabelAssert.Equal(burnEpoch, end);
        // The last output is at the end when the output step divides the window, otherwise on its grid before the burn
        if (last.Epoch == end)
        {
            Assert.Equal(0.0, lastOutput);
        }
        else
        {
            Assert.True(last.Epoch < burnEpoch);
        }

        Assert.Equal(0.0, atEnd);
        Assert.Equal(0.0, stored);

        // The OPM of the spacecraft at the end agrees with the final state and the fuel: its mass is the mass after the burn,
        // its one maneuver the ΔV between the state before and the state after, and the fuel burned (at the end: since #658
        // the OPM carries the state, the mass and the maneuvers of its epoch, the initial one by default)
        var opm = spacecraft.ToOpm(epoch: end);
        var entry = Assert.Single(opm.Data.Maneuvers);
        double opmDeltaV = (entry.DeltaVKmPerSec * 1000.0 - (interpolated - before.EndVelocity)).Magnitude();
        double opmFuel = System.Math.Abs(-entry.ManDeltaMass - (massBefore - spacecraft.GetTotalMass()));
        _output.WriteLine($"MEASURE #575 ToOpm: ΔV less the jump of the final state {opmDeltaV:E3} m/s (limit {OpmDeltaVAgainstFinalJump:E3}), fuel less the mass drop {opmFuel:E3} kg (limit {OpmFuelAgainstMassDrop:E3})");
        Assert.Equal(spacecraft.GetTotalMass(), opm.Data.Mass);
        Assert.True(opmDeltaV <= OpmDeltaVAgainstFinalJump, $"OPM ΔV off the jump of the final state by {opmDeltaV:E3} m/s");
        Assert.True(opmFuel <= OpmFuelAgainstMassDrop, $"OPM fuel off the mass drop by {opmFuel:E3} kg");
    }
}
