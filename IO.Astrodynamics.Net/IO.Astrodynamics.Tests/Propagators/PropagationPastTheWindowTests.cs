// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// A propagation whose output step does not divide its window runs to its last output, round(L/ΔT)·ΔT, up to ΔT/2 past
/// the window (#626). Its caches ended with the window (the orientation of the central body 120 s after it, the bodies
/// 240 s after it), and a read past their last grid point was held there (the Earth stopped turning) or extrapolated (the
/// bodies). The case of #626: a LEO, r = 6878.137 km, v = (0, 5.3829, 5.3829) km/s in ICRF, EGM2008 10×10, 7.0e8 s TT
/// after J2000.
/// </summary>
public class PropagationPastTheWindowTests
{
    private static readonly TimeSystem.Time Start =
        new TimeSystem.Time(new DateTime(2000, 1, 1, 12, 0, 0), TimeScale.TT).AddSeconds(7.0e8);

    private readonly ITestOutputHelper _output;

    public PropagationPastTheWindowTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
        new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

    private static Spacecraft NewSpacecraft(CelestialBody earth, int naifId) =>
        new(naifId, "PAST" + -naifId, 1000.0, 3000.0, new Clock("past" + -naifId, 65536),
            new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 5382.9, 5382.9), earth, Start,
                Frames.Frame.ICRF));

    private static double RotationAngle(Quaternion expected, Quaternion actual)
    {
        var difference = expected.Conjugate() * actual;
        return 2.0 * System.Math.Asin(System.Math.Min(1.0, difference.VectorPart.Magnitude() / difference.Magnitude()));
    }

    /// <summary>
    /// A propagation over a window of 3 900 s with outputs every 600 s (A) integrates the 4 200 s of a propagation over
    /// a window of 4 200 s (B): the same accepted steps, bit for bit, both paths that build the forces (RK7(8) and the
    /// default Velocity-Verlet), with the Moon and the Sun. On a85098d A's grid ended 180 s before its end, where the Earth
    /// was held 8.75e-3 rad off: its final state was 6.9 mm from B's (#626, row 1).
    /// </summary>
    [Theory]
    [InlineData("RK", 3900.0, 600.0)]
    [InlineData("VV", 3900.0, 600.0)]
    [InlineData("RK", 88200.0, 3600.0)]
    public void ToItsLastOutput_IsThePropagationOfThatSpan(string integrator, double length, double step)
    {
        // Arrange
        double span = System.Math.Round(length / step, MidpointRounding.AwayFromZero) * step;
        PropagationSolution Propagate(double windowLength, int naifId)
        {
            var earth = Earth();
            var bodies = new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY };
            var spacecraft = NewSpacecraft(earth, naifId);
            var window = new Window(Start, Start.AddSeconds(windowLength));
            return integrator == "RK"
                ? spacecraft.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), false, false,
                    TimeSpan.FromSeconds(step))
                : spacecraft.Propagate(window, bodies, false, false, TimeSpan.FromSeconds(step));
        }

        int id = integrator == "RK" ? (length < 10000.0 ? -6261 : -6263) : -6265;

        // Act
        var a = Propagate(length, id);
        var b = Propagate(span, id - 1);

        // Assert: the same accepted steps, the last of each ending at the span's end
        var stepsA = a.Segments.SelectMany(s => s.Steps).ToArray();
        var stepsB = b.Segments.SelectMany(s => s.Steps).ToArray();
        var lastA = stepsA[^1];
        var lastB = stepsB[^1];
        double apart = (lastA.EndPosition - lastB.EndPosition).Magnitude();
        double apartVelocity = (lastA.EndVelocity - lastB.EndVelocity).Magnitude();
        _output.WriteLine($"MEASURE #626 {integrator} L {length} s, step {step} s: {stepsA.Length} and {stepsB.Length} steps, " +
                          $"final state of A - B {apart:E3} m, {apartVelocity:E3} m/s");
        Assert.Equal(span, stepsA.Sum(s => s.StepSize), 9);
        Assert.Equal(stepsB.Length, stepsA.Length);
        for (int k = 0; k < stepsA.Length; k++)
        {
            Assert.Equal(stepsB[k], stepsA[k]);
        }
    }

    /// <summary>
    /// A read of the orientation cache after its last grid point reads the frame at the epoch: 60, 180 and 600 s after it
    /// the Earth's orientation against CSPICE <c>sxform</c>. On a85098d it was held at the last grid point: 4.4e-3, 1.3e-2
    /// and 4.4e-2 rad off.
    /// </summary>
    [Theory]
    [InlineData(60.0)]
    [InlineData(180.0)]
    [InlineData(600.0)]
    public void OrientationCache_PastItsLastGridPoint_ReadsTheFrame(double pastTheGrid)
    {
        // Arrange: a window of 3 900 s, whose grid of 60 s from 120 s before its start ends at 4 020 s
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var tdbStart = Start.ToTDB();
        var cache = new PropagationFrameOrientationCache(new Window(tdbStart, tdbStart.AddSeconds(3900.0)), earth.Frame,
            TimeSpan.FromSeconds(60.0));
        var epoch = tdbStart.AddSeconds(4020.0 + pastTheGrid);

        // Act
        var cached = cache.GetOrientation(epoch);

        // Assert: the frame read at the epoch, CSPICE's own value
        var spice = SpiceAPI.Instance.TransformFrame(epoch, earth.Frame, Frames.Frame.ICRF);
        double angle = RotationAngle(spice.Rotation, cached.Rotation);
        _output.WriteLine($"MEASURE #626 orientation {pastTheGrid} s past the grid: {angle:E3} rad from sxform");
        Assert.True(angle <= 2.3e-16, $"{angle:E3} rad");
        Assert.Equal(spice.AngularVelocity, cached.AngularVelocity);
    }

    /// <summary>
    /// A read of the ephemeris cache after its last grid point, or before its first, reads the body's ephemeris at the
    /// epoch: the Moon from the Earth 1 680 s after the grid (row 2 of #626) and 600 s before it against CSPICE
    /// <c>spkezr</c>. On a85098d the 8-point polynomial of the end points was extrapolated: see the MEASURE line.
    /// </summary>
    [Theory]
    [InlineData(1680.0)]
    [InlineData(-600.0)]
    public void EphemerisCache_OutsideItsGrid_ReadsTheEphemeris(double fromTheGrid)
    {
        // Arrange: a window of 3 900 s, whose grid of 60 s from 240 s before its start ends 240 s after its end
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var tdbStart = Start.ToTDB();
        using var cache = new PropagationEphemerisCache(new Window(tdbStart, tdbStart.AddSeconds(3900.0)),
            new (CelestialItem, Aberration)[] { (PlanetsAndMoons.MOON_BODY, Aberration.None) }, earth,
            TimeSpan.FromSeconds(60.0));
        var epoch = fromTheGrid > 0.0 ? tdbStart.AddSeconds(4140.0 + fromTheGrid) : tdbStart.AddSeconds(-240.0 + fromTheGrid);

        // Act
        var position = cache.GetPosition(PlanetsAndMoons.MOON.NaifId, Aberration.None, epoch);
        var (statePosition, stateVelocity) = cache.GetState(PlanetsAndMoons.MOON.NaifId, Aberration.None, epoch);

        // Assert
        var spice = SpiceAPI.Instance.ReadEphemeris(epoch, PlanetsAndMoons.EARTH.NaifId, PlanetsAndMoons.MOON.NaifId,
            Frames.Frame.ICRF, Aberration.None);
        double apart = (position - spice.Position).Magnitude();
        _output.WriteLine($"MEASURE #626 Moon {fromTheGrid} s from the grid: {apart:E3} m from spkezr");
        Assert.Equal(spice.Position, position);
        Assert.Equal(spice.Position, statePosition);
        Assert.Equal(spice.Velocity, stateVelocity);
    }
}
