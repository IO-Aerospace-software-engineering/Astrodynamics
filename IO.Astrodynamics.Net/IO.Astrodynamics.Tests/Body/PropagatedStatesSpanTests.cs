// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
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
    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(10.0);
    private const double OneTick = 1.0e-7;

    /// <summary>The light-time reads at the ends of the states against the same correction written in the test on the
    /// two-body motion of the end state, m (#527): the Lagrange read 1.3 s outside the states. Worst 3.27e-5 m in RK7(8) and
    /// 2.203e-3 m in Velocity-Verlet at 1 s (whose stored states depart from the two-body motion of the end state, XLT) on
    /// Linux x64, Windows x64 and macOS arm64 alike (CI run 44225490669). Twice the worst, rounded up at the second
    /// significant digit.</summary>
    internal const double LightTimeAtTheEnds = 4.5e-3;

    /// <summary>The light-time reads of <c>ApparentPositionReader</c> at the ends of the states of a spacecraft target
    /// against the same correction written in the test on the two-body motion of the end state, m (#527): the read 0.67 ms
    /// outside the states. Worst 2.772e-5 m (Velocity-Verlet at 1 s) on Linux x64 and macOS arm64, 2.773e-5 m on Windows
    /// x64 (CI run 44225490669), at the resolution of the barycentric positions in double (1.5e11 m × 1.1e-16). Twice the
    /// worst, rounded up at the second significant digit.</summary>
    internal const double ReaderLightTimeAtTheEnds = 5.6e-5;

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
        var spacecraft = new Spacecraft(naifId, $"SPAN{-naifId}", 1000.0, initial, 3000.0, new Clock($"SPAN{-naifId}_CLK", 65536));
        IntegratorOptions integratorInstance = integrator == "RK" ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 } : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) };
        spacecraft.Propagate(new Window(S, S.AddHours(2.0)), new PropagationOptions { Integrator = integratorInstance, OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });
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
            var read = spacecraft.GetEphemeris(end.Epoch, earth, Frame.ICRF, Aberration.None);
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
        Assert.Throws<InvalidOperationException>(() => moon.FindDistanceWindows(new Window(last.Epoch.AddSeconds(-600.0), last.Epoch.AddSeconds(600.0)), spacecraft, RelationnalOperator.Greater, 1.0, new WindowSearchOptions { Step = TimeSpan.FromSeconds(60.0) }).ToArray());
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

            var read = spacecraft.GetEphemeris(t, moon, Frame.ICRF, aberration);
            double delta = (read.Position - expected).Magnitude();
            worst = System.Math.Max(worst, delta);
            _output.WriteLine($"MEASURE #527 {integrator} {aberration} at {(t - S).TotalSeconds:R} s, light time {lt:R} s: |Δr| {delta:E3} m against the correction on the two-body motion");
        }

        Assert.True(worst <= LightTimeAtTheEnds, $"{worst:E3} m");
    }

    /// <summary>
    /// The light-time reads of the managed field-of-view and occultation searches (<c>ApparentPositionReader</c>) at the
    /// ends of the states of a spacecraft target, allowed as those of <c>GetEphemeris</c>: the retarded epoch of LT and CN
    /// at the first state, the advanced epoch of XLT and XCN at the last, 0.67 ms outside them for a target 200 km ahead of
    /// the observer. Before the fix the reader read the target with no margin and threw. The reference: the same
    /// correction written in the test (spkltc relative to the Earth, the observer's reference body, plus the Earth's
    /// motion during the light time), the target carried from its end state by its two-body motion, the Earth from SPICE.
    /// </summary>
    [Theory]
    [InlineData("RK", -5281)]
    [InlineData("VV", -5283)]
    public void ApparentPositionReader_AtTheEnds_IsAllowedWithinTheLightTime(string integrator, int naifId)
    {
        // Arrange
        var (observer, target, earth) = Pair(naifId, integrator);
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var states = target.PropagatedStates;
        double worst = 0.0;
        foreach (var (end, aberration, sign) in new[] { (states[0], Aberration.LT, -1.0), (states[0], Aberration.CN, -1.0), (states[^1], Aberration.XLT, 1.0), (states[^1], Aberration.XCN, 1.0) })
        {
            var t = end.Epoch;
            var reader = new IO.Astrodynamics.Occultation.ApparentPositionReader(observer, aberration, false, true);
            Assert.Equal(PlanetsAndMoons.EARTH.NaifId, reader.Reference.NaifId);
            var observerState = reader.Observer(t);
            var observerFromSsb = earth.GetGeometricStateRelativeTo(t, ssb).Position + observerState.Position;
            Vector3 Target(double dt)
            {
                // Read at the tick nearest t + dt and carried to it by the velocities: the 50 ns of a tick would move the
                // Earth by 1.5 mm, more than what the test measures
                var at = t.AddSeconds(dt);
                double residual = dt - (at - t).Ticks / (double)TimeSpan.TicksPerSecond;
                var earthState = earth.GetGeometricStateRelativeTo(at, ssb);
                var targetState = end.AtEpoch(at).ToStateVector();
                return earthState.Position + targetState.Position + (earthState.Velocity + targetState.Velocity) * residual;
            }

            double lt = (Target(0.0) - observerFromSsb).Magnitude() / IO.Astrodynamics.Constants.C;
            int iterations = aberration is Aberration.LT or Aberration.XLT ? 1 : 5;
            var expected = Vector3.Zero;
            for (int i = 0; i < iterations; i++)
            {
                expected = Target(sign * lt) - observerFromSsb;
                lt = expected.Magnitude() / IO.Astrodynamics.Constants.C;
            }

            // Act
            StateVector referenceAtEpoch = null;
            var read = reader.Position(target, t, observerState, ref referenceAtEpoch, out _);

            // Assert
            double delta = (read - expected).Magnitude();
            worst = System.Math.Max(worst, delta);
            _output.WriteLine($"MEASURE #527 reader {integrator} {aberration} at {(t - S).TotalSeconds:R} s, light time {lt:E3} s: |Δr| {delta:E3} m against the correction on the two-body motion");
        }

        Assert.True(worst <= ReaderLightTimeAtTheEnds, $"{worst:E3} m");
    }

    /// <summary>
    /// The managed field-of-view search of a propagated spacecraft target, and the managed occultation search of that
    /// target seen from the observer, over the whole span of their states, with each light-time correction: they read the
    /// target up to its light time outside its states (0.67 ms) and return as many windows as the geometric search. Before
    /// the fix every corrected search threw
    /// <see cref="InvalidOperationException"/> at its first or last read.
    /// </summary>
    [Fact]
    public void ManagedSearches_OverTheWholeSpan_ReadTheTargetWithinTheLightTime()
    {
        // Arrange
        var (observer, target, earth) = Pair(-5285, "RK");
        var window = new Window(S, S.AddHours(1.0));
        var instrument = observer.Instruments.Single();
        var step = TimeSpan.FromSeconds(60.0);
        var geometric = instrument.FindFieldOfViewWindows(window, target, new FieldOfViewSearchOptions { Step = step, TargetShape = ShapeType.Point, TargetFrame = Frame.ICRF }).ToArray();
        var hidden = observer.FindOccultationWindows(window, target, earth, new OccultationSearchOptions { Step = step, TargetShape = ShapeType.Point, FrontShape = ShapeType.Ellipsoid }).ToArray();
        Assert.NotEmpty(geometric);

        foreach (var aberration in new[] { Aberration.LT, Aberration.LTS, Aberration.CN, Aberration.CNS, Aberration.XLT, Aberration.XLTS, Aberration.XCN, Aberration.XCNS })
        {
            // Act
            var corrected = instrument.FindFieldOfViewWindows(window, target, new FieldOfViewSearchOptions { Step = step, Aberration = aberration, TargetShape = ShapeType.Point, TargetFrame = Frame.ICRF }).ToArray();
            var occulted = observer.FindOccultationWindows(window, target, earth, new OccultationSearchOptions { Step = step, Aberration = aberration, TargetShape = ShapeType.Point, FrontShape = ShapeType.Ellipsoid }).ToArray();

            // Assert: the windows of the geometric search, their ends moved by the aberration (the target's motion from
            // the solar system barycentre over the light time, 20 m across 200 km, and the stellar aberration, each about
            // 1e-4 rad, reached at the rate of the line of sight in the field)
            Assert.Equal(geometric.Length, corrected.Length);
            for (int i = 0; i < geometric.Length; i++)
            {
                double start = System.Math.Abs((corrected[i].StartDate - geometric[i].StartDate).TotalSeconds);
                double end = System.Math.Abs((corrected[i].EndDate - geometric[i].EndDate).TotalSeconds);
                _output.WriteLine($"MEASURE #527 field of view {aberration} window {i}: start {start:E3} s, end {end:E3} s from the geometric search");
            }

            Assert.Equal(hidden.Length, occulted.Length);
        }
    }

    // The observer, with a circular instrument of 1 rad along its velocity at the start, and a target 200 km ahead on the
    // same orbit, both propagated over [S, S + 1 h]; the attitude of the observer is the identity
    private static (Spacecraft Observer, Spacecraft Target, CelestialBody Earth) Pair(int naifId, string integrator)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var initial = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5400.0, 5400.0), earth, S, Frame.ICRF);
        var ahead = initial.AtEpoch(S.AddSeconds(200000.0 / initial.Velocity.Magnitude())).ToStateVector();
        var observer = new Spacecraft(naifId, $"SPAN{-naifId}", 1000.0, initial, 3000.0, new Clock($"SPAN{-naifId}_CLK", 65536));
        var target = new Spacecraft(naifId - 1, $"SPAN{1 - naifId}", 1000.0, new StateVector(ahead.Position, ahead.Velocity, earth, S, Frame.ICRF), 3000.0,
            new Clock($"SPAN{1 - naifId}_CLK", 65536));
        observer.AddCircularInstrument($"SPAN{-naifId}_CAM", 1.0, new Vector3(0.0, 1.0, 1.0).Normalize(), Vector3.VectorX, Vector3.Zero, naifId: naifId * 1000 - 1, model: "fov527");
        var window = new Window(S, S.AddHours(1.0));
        foreach (var spacecraft in new[] { observer, target })
        {
            IntegratorOptions integratorInstance = integrator == "RK" ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 } : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) };
            spacecraft.Propagate(window, new PropagationOptions { Integrator = integratorInstance, OutputStep = Output, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None });
        }

        return (observer, target, earth);
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
        var spacecraft = new Spacecraft(-5277, "SPAN5277", 1000.0, tle, 3000.0, new Clock("SPAN5277_CLK", 65536));
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        new TLEPropagator(window, spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();
        var earth = (CelestialItem)tle.Observer;
        double worst = 0.0;
        foreach (var t in new[] { window.StartDate.AddSeconds(-360.0), window.EndDate.AddSeconds(360.0) })
        {
            var sgp4 = tle.ToStateVector(t).ToFrame(Frame.ICRF).ToStateVector();
            var read = spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None);
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
        var spacecraft = new Spacecraft(-5278, "SPAN5278", 1000.0, initial, 3000.0, new Clock("SPAN5278_CLK", 65536));
        var t = S.AddHours(-5.0);
        Assert.Equal(initial.AtEpoch(t).ToStateVector().Position, spacecraft.GetGeometricStateRelativeTo(t, earth).Position);
    }
}
