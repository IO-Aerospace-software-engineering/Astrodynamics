// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Math;
using Xunit;
using Xunit.Abstractions;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.Tests.DataProvider;

/// <summary>
/// The reads of <see cref="MemoryDataProvider"/> between its entries (#548): a read depends on the entries only, never on
/// the reads before it, which it no longer stores; an orientation is the exact SLERP of the two entries around it, no
/// longer the normalised Lerp of <c>Quaternion.SLERP</c> below 0.028 rad. The references are closed forms: the rotation at
/// the interpolated angle about the axis of a constant rotation, and a circular orbit.
/// </summary>
public class MemoryDataProviderTests
{
    private static readonly TimeSystem.Time Start = TimeSystem.Time.J2000TDB;

    private readonly ITestOutputHelper _output;

    public MemoryDataProviderTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // The angle of the rotation between two unit quaternions, whatever their signs
    private static double AngleBetween(in Quaternion a, in Quaternion b)
    {
        var delta = a * b.Conjugate();
        return 2.0 * System.Math.Atan2(delta.VectorPart.Magnitude(), System.Math.Abs(delta.W));
    }

    // The axis of the issue's probe, and an initial rotation off the identity
    private static readonly Vector3 Axis = new Vector3(0.3, -0.5, 0.81).Normalize();

    private static readonly Quaternion Q0 = new Quaternion(new Vector3(1.0, 2.0, 3.0).Normalize(), 0.7);

    // The closed form: the rotation by angle * fraction about the axis, after Q0
    private static Quaternion Exact(double angle, double fraction) => (new Quaternion(Axis, angle * fraction) * Q0).Normalize();

    private static (MemoryDataProvider Provider, Frames.Frame Frame) OrientationProvider(double angle)
    {
        var provider = new MemoryDataProvider();
        var frame = new Frames.Frame("MEMORY_TEST_FRAME");
        provider.AddStateOrientationToICRF(frame, Start, new StateOrientation(Q0, Vector3.Zero, Start, frame));
        provider.AddStateOrientationToICRF(frame, Start.AddSeconds(10.0),
            new StateOrientation(Exact(angle, 1.0), Vector3.Zero, Start.AddSeconds(10.0), frame));
        return (provider, frame);
    }

    // Measured, the worst over the 999 fractions, the same on Linux, Windows and macOS arm64 (run 41567621620, 2026-11-03):
    // 1.451e-16 rad at 0.0113 rad, 1.809e-16 rad at 0.025 rad, 2.388e-16 rad at 0.05 rad; the bound is twice the worst.
    // Before the fix, #548 measured 5.9e-9 and 6.3e-8 rad on a fresh provider (the Lerp below 0.028 rad), and 1.2e-8 to
    // 1.8e-7 rad in sequence; its 5.0e-10 rad at 0.05 rad (SLERP) was the rounding of the dates to the tick, which this
    // test takes out.
    private const double OrientationTolerance = 4.8e-16;

    [Theory]
    [InlineData(0.0113)]
    [InlineData(0.025)]
    [InlineData(0.05)]
    public void Orientation_BetweenTwoEntries_IsTheExactSlerp_WhateverTheReadsBefore(double angle)
    {
        // Arrange: one provider read in sequence, one fresh provider per read
        var (sequence, frame) = OrientationProvider(angle);
        double worst = 0.0;

        for (int i = 1; i < 1000; i++)
        {
            // The fraction of the date as the provider sees it: an epoch counts 100 ns ticks, so 10 s * i / 1000 is rounded to
            // the tick, a fraction off by up to 5e-9 (5e-10 rad of the 0.05 rad rotation, the "SLERP" figure of #548)
            var date = Start.AddSeconds(10.0 * i / 1000.0);
            double fraction = (date - Start).TotalSeconds / 10.0;

            // Act
            var inSequence = sequence.FrameTransformationToICRF(date, frame);
            var (fresh, freshFrame) = OrientationProvider(angle);
            var alone = fresh.FrameTransformationToICRF(date, freshFrame);

            // Assert: the closed form, and the same read whatever the reads before it
            worst = System.Math.Max(worst, AngleBetween(inSequence.Rotation, Exact(angle, fraction)));
            Assert.Equal(alone.Rotation, inSequence.Rotation);
        }

        _output.WriteLine($"MEASURE #548 orientation {angle} rad: {worst:E3} rad");
        Assert.True(worst < OrientationTolerance, $"{worst:E3} rad");

        // The issue's pair: the read at 7.5 s, fresh and after a read at 5 s
        var (first, firstFrame) = OrientationProvider(angle);
        var (second, secondFrame) = OrientationProvider(angle);
        second.FrameTransformationToICRF(Start.AddSeconds(5.0), secondFrame);
        Assert.Equal(first.FrameTransformationToICRF(Start.AddSeconds(7.5), firstFrame).Rotation,
            second.FrameTransformationToICRF(Start.AddSeconds(7.5), secondFrame).Rotation);
    }

    [Fact]
    public void Orientation_AtAnEntry_IsThatEntry()
    {
        var (provider, frame) = OrientationProvider(0.025);
        provider.FrameTransformationToICRF(Start.AddSeconds(3.0), frame);
        Assert.Equal(Q0, provider.FrameTransformationToICRF(Start, frame).Rotation);
        Assert.Equal(Exact(0.025, 1.0), provider.FrameTransformationToICRF(Start.AddSeconds(10.0), frame).Rotation);
    }

    // A circular orbit of radius 6 778 km about the Earth's GM, the issue's: 21 nodes 60 s apart
    private const double Radius = 6778137.0;

    private const double Gm = 3.986004418e14;

    private static readonly double MeanMotion = System.Math.Sqrt(Gm / (Radius * Radius * Radius));

    private static (Vector3 Position, Vector3 Velocity) Circular(double t) =>
        (new Vector3(Radius * System.Math.Cos(MeanMotion * t), Radius * System.Math.Sin(MeanMotion * t), 0.0),
            new Vector3(-Radius * MeanMotion * System.Math.Sin(MeanMotion * t), Radius * MeanMotion * System.Math.Cos(MeanMotion * t), 0.0));

    private static MemoryDataProvider StateProvider()
    {
        var provider = new MemoryDataProvider();
        var earth = TestHelpers.EarthAtJ2000;
        for (int i = 0; i <= 20; i++)
        {
            var (position, velocity) = Circular(60.0 * i);
            provider.AddStateVector(earth.NaifId, Start.AddSeconds(60.0 * i),
                new StateVector(position, velocity, TestHelpers.SsbAtJ2000, Start.AddSeconds(60.0 * i), Frames.Frame.ICRF));
        }

        return provider;
    }

    private static StateVector Read(MemoryDataProvider provider, double t) =>
        provider.GetEphemeris(Start.AddSeconds(t), TestHelpers.EarthAtJ2000, TestHelpers.SsbAtJ2000, Frames.Frame.ICRF, Aberration.None)
            .ToStateVector();

    // Measured 8.805e-7 m at 605 s, the same on Linux, Windows and macOS arm64 (run 41567621620, 2026-11-03), the error of the
    // 8-point Lagrange interpolation of the nodes; the bound is twice it. Before the fix, 7.70 m after the 40 reads (#548).
    private const double PositionTolerance = 1.8e-6;

    [Fact]
    public void State_BetweenNodes_IsTheInterpolationOfTheNodes_WhateverTheReadsBefore()
    {
        // Arrange: the issue's 40 reads between 300 and 442 s, among the 8 nodes nearest to 605 s
        var fresh = StateProvider();
        var sequence = StateProvider();
        for (int i = 0; i < 40; i++)
        {
            Read(sequence, 300.0 + 142.0 * i / 39.0);
        }

        // Act
        var alone = Read(fresh, 605.0);
        var afterReads = Read(sequence, 605.0);

        // Assert: the same read, the orbit within the interpolation error
        Assert.Equal(alone.Position, afterReads.Position);
        Assert.Equal(alone.Velocity, afterReads.Velocity);
        double error = (afterReads.Position - Circular(605.0).Position).Magnitude();
        _output.WriteLine($"MEASURE #548 state at 605 s: {error:E3} m");
        Assert.True(error < PositionTolerance, $"{error:E3} m");

        // A node is read as it is, before and after interpolated reads
        Assert.Equal(Circular(600.0).Position, Read(sequence, 600.0).Position);
    }

    // #680, the contract of the reads (#668, C10): a target or a frame without data at the epoch throws
    // InvalidOperationException, the type SPICE throws for the same situations, which the library's handlers of missing data
    // catch (a body whose barycentre the data do not reach builds without an orbit; the orientation cache of a propagation
    // keeps the point unavailable); a frame never added throws ArgumentException; an id without body information throws
    // InvalidOperationException, the type the construction of a body without a name throws on SPICE (CelestialItem), not
    // the SPICE provider's read itself, which returns a record. Before, ArgumentException and KeyNotFoundException
    [Fact]
    public void MissingData_ThrowsInvalidOperationException_UnknownFrame_ArgumentException()
    {
        var earth = TestHelpers.EarthAtJ2000;
        var ssb = TestHelpers.SsbAtJ2000;
        var early = new TimeSystem.Time(1500, 1, 1, 0, 0, 0, frame: TimeSystem.TimeFrame.TDBFrame);

        // The reference: the types SPICE gives, the Earth outside de440s and ITRF93 outside the PCK
        var spice = new SpiceDataProvider();
        Assert.Throws<InvalidOperationException>(() => spice.GetEphemeris(early, earth, ssb, Frames.Frame.ICRF, Aberration.None));
        Assert.Throws<InvalidOperationException>(() => spice.FrameTransformationToICRF(early, new Frames.Frame("ITRF93")));

        // A target without states, and one read before its first or after its last state
        var provider = new MemoryDataProvider();
        var targetException = Assert.Throws<InvalidOperationException>(() =>
            provider.GetEphemeris(Start, earth, ssb, Frames.Frame.ICRF, Aberration.None));
        Assert.Contains("399", targetException.Message);
        var states = StateProvider();
        Assert.Throws<InvalidOperationException>(() =>
            states.GetEphemeris(Start.AddSeconds(-1.0), earth, ssb, Frames.Frame.ICRF, Aberration.None));
        Assert.Throws<InvalidOperationException>(() =>
            states.GetEphemeris(Start.AddSeconds(1201.0), earth, ssb, Frames.Frame.ICRF, Aberration.None));

        // A frame read outside its stored orientations; a frame never added keeps ArgumentException
        var (orientations, frame) = OrientationProvider(0.025);
        var frameException = Assert.Throws<InvalidOperationException>(() => orientations.FrameTransformationToICRF(Start.AddSeconds(-1.0), frame));
        Assert.Contains(frame.Name, frameException.Message);
        Assert.Throws<InvalidOperationException>(() => orientations.FrameTransformationToICRF(Start.AddSeconds(11.0), frame));
        Assert.Throws<ArgumentException>(() => orientations.FrameTransformationToICRF(Start, new Frames.Frame("IAU_MARS")));

        // An id without body info: InvalidOperationException, the type CelestialItem throws on SPICE for a body without a name.
        // There is no SPICE reference for the read itself: GetCelestialBodyInfoProxy leaves the name of an id the kernels do not
        // name uninitialised (CelestialBodyDTO.Name has no initialiser): "\uFFFD\u007F" for 12345 and an empty name for 5000 in
        // this process, measured 2026-11-14, a defect of the native layer outside #680
        var infoException = Assert.Throws<InvalidOperationException>(() => provider.GetCelestialBodyInfo(999));
        Assert.Contains("999", infoException.Message);
    }
}
