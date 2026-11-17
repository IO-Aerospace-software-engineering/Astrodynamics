// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The reads of a propagated spacecraft stay within its propagated states (#527, decided by S. Guillet, 2026-11-17):
/// <c>GetGeometricStateRelativeTo</c>, the short-circuit of <c>GetEphemeris</c> from the centre of the states and every
/// read through them throw <see cref="InvalidOperationException"/> before the first state and after the last, where the
/// 8-point polynomial was extrapolated without an error before (567 km 6 min before the first state, 8 282 km 6 min after
/// the last on the issue's LEO); the epoch a light-time correction reads may lie up to its light time outside them.
/// </summary>
public class PropagatedStatesSpanTests
{
    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(10.0);
    private const double OneTick = 1.0e-7;

    /// <summary>The light-time reads at the ends of the states against the same correction written in the test on the
    /// two-body motion of the end state, m (#527): the Lagrange read 1.3 s outside the states. Worst 3.27e-5 m in RK7(8) and
    /// 2.20e-3 m in Velocity-Verlet at 1 s (whose stored states depart from the two-body motion of the end state) on Linux
    /// x64. Provisional: twice the Linux worst, rounded up at the second significant digit, to be set again from the Windows
    /// x64 and macOS arm64 CI logs.</summary>
    internal const double LightTimeAtTheEnds = 4.5e-3;

    private readonly ITestOutputHelper _output;

    public PropagatedStatesSpanTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static Spacecraft Propagated(int naifId, string integrator, string frame, out CelestialBody earth)
    {
        earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var icrf = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5400.0, 5400.0), earth, S, Frame.ICRF);
        var initial = frame == "ICRF" ? icrf : icrf.ToFrame(Frame.ECLIPTIC_J2000).ToStateVector();
        var spacecraft = new Spacecraft(naifId, $"SPAN{-naifId}", 1000.0, 3000.0, new Clock($"SPAN{-naifId}_CLK", 65536), initial);
        Integrator integratorInstance = integrator == "RK" ? new RK78Integrator(1e-12, 1e-12) : new VVIntegrator(TimeSpan.FromSeconds(1.0));
        spacecraft.Propagate(new Window(S, S.AddHours(2.0)), [earth], integratorInstance, false, false, Output);
        return spacecraft;
    }

    /// <summary>
    /// At the first and the last state the reads are the stored states, bit for bit; one tick and 6 min outside them,
    /// <c>GetGeometricStateRelativeTo</c>, <c>GetEphemeris</c> from the Earth (the short-circuit) and from the Moon (the
    /// reference-body path) and a light-time read whose observation epoch is outside throw. Both integrators, from ICRF and
    /// from ECLIPJ2000. Before the fix none threw.
    /// </summary>
    [Theory]
    [InlineData("RK", "ICRF", -5271)]
    [InlineData("VV", "ICRF", -5272)]
    [InlineData("RK", "ECLIPJ2000", -5273)]
    [InlineData("VV", "ECLIPJ2000", -5274)]
    public void ReadsOutsideThePropagatedStates_Throw(string integrator, string frame, int naifId)
    {
        // Arrange
        var spacecraft = Propagated(naifId, integrator, frame, out var earth);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, S);
        var states = spacecraft.PropagatedStates;
        var (first, last) = (states[0], states[^1]);

        // Act, assert: the ends are the stored states
        foreach (var end in new[] { first, last })
        {
            var geometric = spacecraft.GetGeometricStateRelativeTo(end.Epoch, earth);
            Assert.Equal(end.Position, geometric.Position);
            Assert.Equal(end.Velocity, geometric.Velocity);
            var read = spacecraft.GetEphemeris(end.Epoch, earth, Frame.ICRF, Aberration.None).ToStateVector();
            Assert.Equal(end.Position, read.Position);
            Assert.Equal(end.Velocity, read.Velocity);
            Assert.NotNull(spacecraft.GetEphemeris(end.Epoch, moon, Frame.ICRF, Aberration.None));
        }

        // Outside them every read throws
        var outside = new[] { first.Epoch.AddSeconds(-OneTick), first.Epoch.AddSeconds(-360.0), last.Epoch.AddSeconds(OneTick), last.Epoch.AddSeconds(360.0) };
        foreach (var t in outside)
        {
            var e = Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(t, earth));
            _output.WriteLine($"MEASURE #527 {integrator} {frame} at {(t - S).TotalSeconds:R} s: {e.Message}");
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, moon, Frame.ICRF, Aberration.None));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, moon, Frame.ICRF, Aberration.LT));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, earth, Frame.ECLIPTIC_J2000, Aberration.XCN));
        }

        // The managed distance search over a window past the states reads them outside: it throws
        Assert.Throws<InvalidOperationException>(() => spacecraft.FindWindowsOnDistanceConstraint(new Window(last.Epoch.AddSeconds(-600.0), last.Epoch.AddSeconds(600.0)),
            moon, RelationnalOperator.Greater, 1.0, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray());
    }

    /// <summary>
    /// A light-time read from the Moon at the first state (LT, CN, the retarded epoch 1.3 s before it) and a transmission
    /// read at the last (XLT, XCN, 1.3 s after it) are allowed: the epoch a correction reads may lie up to its light time
    /// outside the states. The reference: the same correction written in the test, the spacecraft carried from the end state
    /// by its two-body motion (the propagation's own, a point-mass Earth), the Earth and the Moon from SPICE, from the solar
    /// system barycentre.
    /// </summary>
    [Theory]
    [InlineData("RK", -5275)]
    [InlineData("VV", -5276)]
    public void LightTimeReadsAtTheEnds_AreAllowedWithinTheLightTime(string integrator, int naifId)
    {
        var spacecraft = Propagated(naifId, integrator, "ICRF", out var earth);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, S);
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var states = spacecraft.PropagatedStates;
        double worst = 0.0;
        foreach (var (end, aberration, sign) in new[] { (states[0], Aberration.LT, -1.0), (states[0], Aberration.CN, -1.0), (states[^1], Aberration.XLT, 1.0), (states[^1], Aberration.XCN, 1.0) })
        {
            var t = end.Epoch;
            var observer = moon.GetGeometricStateRelativeTo(t, ssb).Position;
            Vector3 Target(double dt)
            {
                var at = t.AddSeconds(dt);
                return earth.GetGeometricStateRelativeTo(at, ssb).Position + end.AtEpoch(at).ToStateVector().Position;
            }

            // spkltc: one light time from the geometric distance (LT, XLT), or iterated to convergence (CN, XCN)
            double lt = (Target(0.0) - observer).Magnitude() / IO.Astrodynamics.Constants.C;
            int iterations = aberration is Aberration.LT or Aberration.XLT ? 1 : 5;
            var expected = Vector3.Zero;
            for (int i = 0; i < iterations; i++)
            {
                expected = Target(sign * lt) - observer;
                lt = expected.Magnitude() / IO.Astrodynamics.Constants.C;
            }

            var read = spacecraft.GetEphemeris(t, moon, Frame.ICRF, aberration).ToStateVector();
            double delta = (read.Position - expected).Magnitude();
            worst = System.Math.Max(worst, delta);
            _output.WriteLine($"MEASURE #527 {integrator} {aberration} at {(t - S).TotalSeconds:R} s, light time {lt:R} s: |Δr| {delta:E3} m against the correction on the two-body motion");
        }

        Assert.True(worst <= LightTimeAtTheEnds, $"{worst:E3} m");
    }

    /// <summary>
    /// A TLE spacecraft sampled by <see cref="TLEPropagator"/> reads SGP4 outside its samples, from the Earth without
    /// aberration as from any other body: before the fix the short-circuit of <c>GetEphemeris</c> extrapolated the samples.
    /// </summary>
    [Fact]
    public void SampledTleSpacecraft_ReadsSgp4OutsideItsSamples()
    {
        var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var spacecraft = new Spacecraft(-5277, "SPAN5277", 1000.0, 3000.0, new Clock("SPAN5277_CLK", 65536), tle);
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        new TLEPropagator(window, spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();
        var earth = (CelestialItem)tle.Observer;
        double worst = 0.0;
        foreach (var t in new[] { window.StartDate.AddSeconds(-360.0), window.EndDate.AddSeconds(360.0) })
        {
            var sgp4 = tle.ToStateVector(t).ToFrame(Frame.ICRF).ToStateVector();
            var read = spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None).ToStateVector();
            double delta = (read.Position - sgp4.Position).Magnitude();
            worst = System.Math.Max(worst, delta);
            _output.WriteLine($"MEASURE #527 sampled TLE at {(t - window.StartDate).TotalSeconds:R} s: |Δr| {delta:E3} m against SGP4");
        }

        // The read is SGP4 itself, rotated as the test rotates it: 0 m measured on Linux x64
        Assert.Equal(0.0, worst);
    }

    /// <summary>A spacecraft never propagated reads the two-body motion of its initial orbit at any epoch (the control).</summary>
    [Fact]
    public void Unpropagated_ReadsAnywhere()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var initial = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5400.0, 5400.0), earth, S, Frame.ICRF);
        var spacecraft = new Spacecraft(-5278, "SPAN5278", 1000.0, 3000.0, new Clock("SPAN5278_CLK", 65536), initial);
        var t = S.AddHours(-5.0);
        Assert.Equal(initial.AtEpoch(t).ToStateVector().Position, spacecraft.GetGeometricStateRelativeTo(t, earth).Position);
    }
}
