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
        var maneuver = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8378137.0, engine);
        spacecraft.SetStandbyManeuver(maneuver);
        var window = new Window(S, MinimumEpochGateTests.GateAtTt(length));
        double massBefore = spacecraft.GetTotalMass();
        var solution = spacecraft.Propagate(window, [earth], NewIntegrator(integratorName), false, false, TimeSpan.FromSeconds(outputStep));

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
        spacecraft.SetStandbyManeuver(impulse);
        var window = new Window(S, end);
        double massBefore = spacecraft.GetTotalMass();
        var solution = spacecraft.Propagate(window, [earth], NewIntegrator(integratorName), false, false, TimeSpan.FromSeconds(10.0));

        AssertEndsAfterTheBurn(spacecraft, impulse, solution, window, massBefore, earth);
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
        var ephemeris = spacecraft.GetEphemeris(end, earth, Frame.ICRF, Aberration.None).ToStateVector();
        double lastOutput = (last.Velocity - expectedVelocity).Magnitude();
        double atEnd = (interpolated - expectedVelocity).Magnitude();
        double stored = (ephemeris.Velocity - expectedVelocity).Magnitude();
        _output.WriteLine($"MEASURE #575 impulse at the end: {solution.Segments.Count} segments, burn {(end - burnEpoch).TotalSeconds:E3} s before the end, ΔV {maneuver.DeltaV.Magnitude():F3} m/s; " +
                          $"velocity less the state after: last output {lastOutput:E3}, InterpolateAt {atEnd:E3}, GetEphemeris {stored:E3} m/s");
        Assert.Equal(burnEpoch, end);
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
    }
}
