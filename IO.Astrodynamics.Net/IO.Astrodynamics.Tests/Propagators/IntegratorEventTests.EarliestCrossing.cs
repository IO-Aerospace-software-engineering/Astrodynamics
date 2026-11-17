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
    // #578: the earliest crossing of several detectors
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Two detectors through <see cref="IIntegrator.IntegrateSegment"/>, from the apogee of the 400 × 1 000 km orbit: the
    /// perigee (g = r·v, − → +, at T/2 by the closed form) and a time detector whose root is T/2 + 2 s, in both list orders.
    /// The earliest, the perigee, is reported (#578: the first in list order was, the perigee then lost for one orbit):
    /// RK7(8) at its root, Velocity-Verlet at the end of the step that holds it.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    [InlineData("VV10")]
    public void TwoDetectors_TheEarliestCrossingIsReported(string integratorName)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var sv = MinimumEpochGateTests.Orbit(earth, System.Math.PI).ToStateVector();
        var k = new KeplerClosedForm(earth.GM, sv.Position, sv.Velocity, 0.0);
        double perigee = k.T / 2.0;
        var timeRoot = S.AddSeconds(perigee + 2.0);
        var apsis = new Detector { Direction = CrossingDirection.NegativeToPositive, G = s => s.Position * s.Velocity };
        var timed = new Detector { Direction = CrossingDirection.NegativeToPositive, G = s => (s.Epoch - timeRoot).TotalSeconds };
        foreach (bool timeFirst in new[] { true, false })
        {
            var integrator = NewIntegrator(integratorName);
            integrator.AddForce(new GravitationalAcceleration(earth));
            integrator.Initialize(sv);
            IEventDetector[] detectors = timeFirst ? [timed, apsis] : [apsis, timed];
            var result = integrator.IntegrateSegment(sv.Position, sv.Velocity, S, 4000.0, detectors);
            Assert.NotNull(result.Event);
            var evt = result.Event.Value;
            double error = evt.EventTime - perigee;
            _output.WriteLine($"MEASURE #578 two detectors {integratorName} time first {timeFirst}: detector {(ReferenceEquals(detectors[evt.DetectorIndex], apsis) ? "perigee" : "time")}, event - perigee {error:E3} s");
            Assert.Same(apsis, detectors[evt.DetectorIndex]);
            if (integratorName == "RK")
            {
                Assert.True(System.Math.Abs(error) <= MinimumEpochGateTests.Rk78Epoch["apsis"], $"{error:E3} s");
            }
            else
            {
                double h = Step(integratorName);
                Assert.Equal(System.Math.Ceiling(perigee / h) * h, evt.EventTime);
            }
        }
    }

    /// <summary>
    /// Two time detectors whose roots, 10 s and 30 s, are in the first RK7(8) step [0, 60]: the one at 10 s is reported at
    /// its root, in either list order (#578: the first listed was, the other lost). Velocity-Verlet at 60 s reports the one
    /// at 10 s too, at the end of that step.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("RKf60")]
    [InlineData("VV60")]
    public void TwoTimeDetectorsInOneStep_TheEarlierRootIsReported(string integratorName)
    {
        foreach (bool laterFirst in new[] { true, false })
        {
            var (integrator, start) = integratorName == "VV60"
                ? LineWith(new VVIntegrator(TimeSpan.FromSeconds(60.0)))
                : Line(integratorName);
            var early = new Detector { Direction = CrossingDirection.NegativeToPositive, G = s => (s.Epoch - S.AddSeconds(10.0)).TotalSeconds };
            var late = new Detector { Direction = CrossingDirection.NegativeToPositive, G = s => (s.Epoch - S.AddSeconds(30.0)).TotalSeconds };
            IEventDetector[] detectors = laterFirst ? [late, early] : [early, late];
            var result = integrator.IntegrateSegment(start.Position, start.Velocity, S, 400.0, detectors);
            Assert.NotNull(result.Event);
            var evt = result.Event.Value;
            _output.WriteLine($"MEASURE #578 two time detectors {integratorName} later first {laterFirst}: {(ReferenceEquals(detectors[evt.DetectorIndex], early) ? "10 s" : "30 s")} at {evt.EventTime:R} s");
            Assert.Same(early, detectors[evt.DetectorIndex]);
            Assert.Equal(integratorName == "VV60" ? 60.0 : 10.0, evt.EventTime);
        }
    }

    private static (Integrator Integrator, StateVector Start) LineWith(Integrator integrator)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var start = new StateVector(new Vector3(0.0, 7e6, 0.0), new Vector3(1.0, 0.0, 0.0), earth, S, Frame.ICRF);
        integrator.Initialize(start);
        return (integrator, start);
    }
}
