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
    // #581: a plane alignment starting just past a node
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A <see cref="PlaneAlignmentManeuver"/> whose propagation starts δ after a node of the 400 × 1 000 km orbit
    /// (i 0.5 rad, ω = π: the ascending node at the apogee, the descending node at the perigee) toward a plane turned by
    /// 0.05 rad about the line of nodes, within 1e-6 |r| of the node for |δ| ≤ 19 ms: it fires at the node ahead, the
    /// closed form's, with that node's ΔV, 2 |v_h| sin(Δi/2) (#581: δ = 1, 5 and 10 ms locked onto the node just passed and
    /// fired a period after it). Starts 1, 5 and 10 ms past each node and 10 ms before it, the minimum epoch at the start
    /// and 100 s before it. RK7(8) within the node family of <see cref="MinimumEpochGateTests"/>; Velocity-Verlet at the
    /// end of the step that holds the node ahead, within its lag limit.
    /// </summary>
    [Theory]
    [MemberData(nameof(PlaneCases))]
    public void PlaneAlignment_StartingJustPastANode_FiresAtTheNodeAhead(string integratorName, bool ascending, double delta, double gateBefore)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var reference = new KeplerianElements(MinimumEpochGateTests.A0, MinimumEpochGateTests.E0, 0.5, 0.3, System.Math.PI, System.Math.PI, earth, S, Frame.ICRF).ToStateVector();
        var k0 = new KeplerClosedForm(earth.GM, reference.Position, reference.Velocity, 0.0);
        // The node passed, then the start δ after it
        double node = ascending ? 0.0 : k0.T / 2.0;
        var (r0, v0) = k0.StateAt(node + delta);
        var start = new StateVector(r0, v0, earth, S, Frame.ICRF);
        var (spacecraft, engine) = NewSpacecraft(ascending ? -7911 : -7912, start);
        var target = new KeplerianElements(MinimumEpochGateTests.A0, MinimumEpochGateTests.E0, 0.55, 0.3, System.Math.PI, System.Math.PI, earth, S, Frame.ICRF);
        var maneuver = new PlaneAlignmentManeuver(target, engine, S.Add(TimeSpan.FromSeconds(-gateBefore)), TimeSpan.Zero);
        spacecraft.SetManeuvers(maneuver);
        var solution = spacecraft.Propagate(new Window(S, MinimumEpochGateTests.GateAtTt(4000.0)), new PropagationOptions { Integrator = NewIntegrator(integratorName), OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });

        // The closed form from the start: the first node at or after it, and its ΔV
        var k = new KeplerClosedForm(earth.GM, r0, v0, 0.0);
        var tsv = target.ToStateVector();
        var ht = tsv.Position.Cross(tsv.Velocity).Normalize();
        var d = k.Hh.Cross(ht).Normalize();
        double nu = k.TrueAnomalyOf(d);
        double tA = k.TimeOfTrue(nu, 0.0), tD = k.TimeOfTrue(nu + System.Math.PI, 0.0);
        double tc = System.Math.Min(tA, tD);
        var (rc, vc) = k.StateAt(tc);
        double theta = System.Math.Acos(System.Math.Clamp(k.Hh.Dot(ht), -1.0, 1.0));
        var horizontal = vc - rc.Normalize() * vc.Dot(rc.Normalize());
        double expectedDeltaV = 2.0 * horizontal.Magnitude() * System.Math.Sin(theta / 2.0);
        // The node ahead is ascending when g = r·ĥ_target rises through it
        var (rAfter, _) = k.StateAt(tc + 1.0);
        bool aheadAscending = rAfter.Dot(ht) > 0.0;

        Assert.True(solution.Segments.Count >= 2, "no burn");
        double burn = MinimumEpochGateTests.Tt(solution.Segments[1].BaseEpoch);
        double error = burn - tc;
        double dv = maneuver.DeltaV.Magnitude() - expectedDeltaV;
        _output.WriteLine($"MEASURE #581 plane {integratorName} {(ascending ? "ascending" : "descending")} δ {delta} s gate −{gateBefore} s: burn − node ahead {error:E3} s (node at {tc:F3} s), ΔV − closed form {dv:E3} m/s, ascending {maneuver.IsAscendingNode} (ahead {aheadAscending})");
        Assert.Equal(aheadAscending, maneuver.IsAscendingNode);
        if (IsVelocityVerlet(integratorName))
        {
            double limit = MinimumEpochGateTests.VelocityVerletLagLimit["node|" + integratorName];
            Assert.InRange(error, -limit, Step(integratorName) + limit);
        }
        else
        {
            Assert.True(System.Math.Abs(error) <= MinimumEpochGateTests.Rk78Epoch["node"], $"{error:E3} s");
            Assert.True(System.Math.Abs(dv) <= MinimumEpochGateTests.Rk78DeltaV["node"], $"{dv:E3} m/s");
        }
    }

    public static IEnumerable<object[]> PlaneCases()
    {
        foreach (var integrator in new[] { "VV1", "VV10", "RK" })
        foreach (var ascending in new[] { true, false })
        foreach (var delta in new[] { 0.001, 0.005, 0.01, -0.01 })
        foreach (var gateBefore in new[] { 0.0, 100.0 })
            yield return [integrator, ascending, delta, gateBefore];
    }
}
