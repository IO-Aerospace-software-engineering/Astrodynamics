// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

/// <summary>
/// <c>Frame.RotationTo</c>, the rotation of <c>Frame.ToFrame</c> without allocation, shared by the geopotential
/// acceleration and its partials, and <c>Frame.AngularVelocityTo</c>, its angular velocity, read by atmospheric drag:
/// the same doubles as <c>ToFrame</c>, with and without the orientation cache of a propagation.
/// </summary>
public class FrameRotationTests
{
    private static readonly TimeSystem.Time Start = new(2021, 3, 20, 12, 0, 0);

    public FrameRotationTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static TimeSystem.Time[] Epochs => new[]
    {
        Start, Start.AddSeconds(30.0), Start.AddSeconds(1234.5678), Start.AddSeconds(3599.9999999)
    };

    [Fact]
    public void IcrfToIcrf_IsTheIdentityBitForBit()
    {
        // The premise of the shortcut of Frame.GetRotationToICRF: SPICE gives exactly (1, 0, 0, 0)
        foreach (var epoch in Epochs)
        {
            AssertSameBits(Quaternion.Zero, Frames.Frame.ICRF.GetStateOrientationToICRF(epoch).Rotation);
            AssertSameBits(Quaternion.Zero, new Frames.Frame("J2000").GetStateOrientationToICRF(epoch).Rotation);
        }
    }

    [Fact]
    public void RotationTo_IsTheRotationOfToFrame_BitForBit()
    {
        // Arrange: SPICE frames, the IAU chain and the same frame
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var pairs = new (Frames.Frame Source, Frames.Frame Target)[]
        {
            (Frames.Frame.ICRF, earth.Frame), (earth.Frame, Frames.Frame.ICRF), (Frames.Frame.ICRF, moon.Frame),
            (moon.Frame, earth.Frame), (Frames.Frame.ICRF, Frames.Frame.ECLIPTIC_J2000),
            (Frames.Frame.GCRF, Frames.Frame.TIRS), (Frames.Frame.CIRS, Frames.Frame.ICRF),
            (Frames.Frame.ICRF, Frames.Frame.ICRF), (earth.Frame, earth.Frame)
        };

        foreach (var epoch in Epochs)
        {
            foreach (var (source, target) in pairs)
            {
                // Act and assert
                AssertSameBits(source.ToFrame(target, epoch).Rotation, source.RotationTo(target, epoch));
            }
        }
    }

    [Fact]
    public void RotationTo_WithTheOrientationCacheOfAPropagation_IsTheRotationOfToFrame_AndAllocatesNothing()
    {
        // Arrange: the cache of a propagation over one hour, active on this thread for the Earth-fixed frame
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var cache = new PropagationFrameOrientationCache(new Window(Start, Start.AddSeconds(3600.0)), earth.Frame,
            TimeSpan.FromSeconds(60.0));

        using (Frames.Frame.UseOrientationCache(earth.Frame, cache))
        {
            foreach (var epoch in Epochs)
            {
                // Act and assert: both directions, as the acceleration uses them
                AssertSameBits(Frames.Frame.ICRF.ToFrame(earth.Frame, epoch).Rotation,
                    Frames.Frame.ICRF.RotationTo(earth.Frame, epoch));
                AssertSameBits(earth.Frame.ToFrame(Frames.Frame.ICRF, epoch).Rotation,
                    earth.Frame.RotationTo(Frames.Frame.ICRF, epoch));
                AssertSameBits(cache.GetOrientation(epoch).Rotation, cache.GetRotation(epoch));
            }

            _ = Frames.Frame.ICRF.RotationTo(earth.Frame, Start);
            long before = GC.GetAllocatedBytesForCurrentThread();
            _ = Frames.Frame.ICRF.RotationTo(earth.Frame, Start.AddSeconds(77.7));
            _ = earth.Frame.RotationTo(Frames.Frame.ICRF, Start.AddSeconds(88.8));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
        }
    }

    [Fact]
    public void IcrfToIcrf_HasAZeroAngularVelocityBitForBit()
    {
        // The premise of the shortcut of Frame.GetRotationAndAngularVelocityToICRF: SPICE gives exactly (0, 0, 0)
        foreach (var epoch in Epochs)
        {
            AssertSameBits(Vector3.Zero, Frames.Frame.ICRF.GetStateOrientationToICRF(epoch).AngularVelocity);
            AssertSameBits(Vector3.Zero, new Frames.Frame("J2000").GetStateOrientationToICRF(epoch).AngularVelocity);
        }
    }

    [Fact]
    public void AngularVelocityTo_IsTheAngularVelocityOfToFrame_BitForBit()
    {
        // Arrange: SPICE frames, the IAU chain and the same frame
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var pairs = new (Frames.Frame Source, Frames.Frame Target)[]
        {
            (Frames.Frame.ICRF, earth.Frame), (earth.Frame, Frames.Frame.ICRF), (Frames.Frame.ICRF, moon.Frame),
            (moon.Frame, earth.Frame), (Frames.Frame.ECLIPTIC_J2000, earth.Frame),
            (Frames.Frame.GCRF, Frames.Frame.TIRS), (Frames.Frame.CIRS, Frames.Frame.ICRF),
            (Frames.Frame.ICRF, Frames.Frame.ICRF), (earth.Frame, earth.Frame)
        };

        foreach (var epoch in Epochs)
        {
            foreach (var (source, target) in pairs)
            {
                // Act and assert
                AssertSameBits(source.ToFrame(target, epoch).AngularVelocity, source.AngularVelocityTo(target, epoch));
            }
        }
    }

    [Fact]
    public void AngularVelocityTo_WithTheOrientationCacheOfAPropagation_IsTheAngularVelocityOfToFrame_AndAllocatesNothing()
    {
        // Arrange: the cache of a propagation over one hour, active on this thread for the Earth-fixed frame
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var cache = new PropagationFrameOrientationCache(new Window(Start, Start.AddSeconds(3600.0)), earth.Frame,
            TimeSpan.FromSeconds(60.0));

        using (Frames.Frame.UseOrientationCache(earth.Frame, cache))
        {
            foreach (var epoch in Epochs)
            {
                // Act and assert: the direction drag uses, the opposite one, and the cache itself
                AssertSameBits(Frames.Frame.ICRF.ToFrame(earth.Frame, epoch).AngularVelocity,
                    Frames.Frame.ICRF.AngularVelocityTo(earth.Frame, epoch));
                AssertSameBits(earth.Frame.ToFrame(Frames.Frame.ICRF, epoch).AngularVelocity,
                    earth.Frame.AngularVelocityTo(Frames.Frame.ICRF, epoch));
                cache.GetRotationAndAngularVelocity(epoch, out var rotation, out var angularVelocity);
                AssertSameBits(cache.GetOrientation(epoch).Rotation, rotation);
                AssertSameBits(cache.GetOrientation(epoch).AngularVelocity, angularVelocity);
            }

            _ = Frames.Frame.ICRF.AngularVelocityTo(earth.Frame, Start);
            long before = GC.GetAllocatedBytesForCurrentThread();
            _ = Frames.Frame.ICRF.AngularVelocityTo(earth.Frame, Start.AddSeconds(77.7));
            _ = earth.Frame.AngularVelocityTo(Frames.Frame.ICRF, Start.AddSeconds(88.8));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
        }
    }

    [Fact]
    public void AngularVelocityTo_AFrameThatDoesNotReadTheCache_KeepsItsOwnOrientation()
    {
        // Arrange: a cache made active for TIRS, whose orientation does not read it
        var tirs = new Frames.TirsFrame();
        var cache = new PropagationFrameOrientationCache(new Window(Start, Start.AddSeconds(3600.0)), tirs,
            TimeSpan.FromSeconds(600.0));

        using (Frames.Frame.UseOrientationCache(tirs, cache))
        {
            var epoch = Start.AddSeconds(1234.5678);

            // Act and assert: the IAU chain, not the interpolated grid
            AssertSameBits(Frames.Frame.ICRF.ToFrame(tirs, epoch).AngularVelocity,
                Frames.Frame.ICRF.AngularVelocityTo(tirs, epoch));
        }
    }

    [Fact]
    public void RotationTo_AFrameThatDoesNotReadTheCache_KeepsItsOwnOrientation()
    {
        // Arrange: a cache made active for TIRS, whose orientation does not read it
        var tirs = new Frames.TirsFrame();
        var cache = new PropagationFrameOrientationCache(new Window(Start, Start.AddSeconds(3600.0)), tirs,
            TimeSpan.FromSeconds(600.0));

        using (Frames.Frame.UseOrientationCache(tirs, cache))
        {
            var epoch = Start.AddSeconds(1234.5678);

            // Act and assert: the IAU chain, not the interpolated grid
            AssertSameBits(Frames.Frame.ICRF.ToFrame(tirs, epoch).Rotation, Frames.Frame.ICRF.RotationTo(tirs, epoch));
            AssertSameBits(tirs.GetStateOrientationToICRF(epoch).Rotation, tirs.GetRotationToICRF(epoch));
        }
    }

    [Fact]
    public void RotationTo_AFrameThatReadsTheCache_UsesIt()
    {
        // Arrange: GCRF reads the cache like the base frame; a cache made active for it replaces its identity
        var gcrf = new Frames.GcrfFrame();
        var cache = new PropagationFrameOrientationCache(new Window(Start, Start.AddSeconds(3600.0)),
            new CelestialBody(PlanetsAndMoons.EARTH).Frame, TimeSpan.FromSeconds(60.0));

        using (Frames.Frame.UseOrientationCache(gcrf, cache))
        {
            var epoch = Start.AddSeconds(1234.5678);

            // Act and assert
            AssertSameBits(cache.GetRotation(epoch), gcrf.GetRotationToICRF(epoch));
            AssertSameBits(gcrf.ToFrame(Frames.Frame.ICRF, epoch).Rotation, gcrf.RotationTo(Frames.Frame.ICRF, epoch));
        }
    }

    private static void AssertSameBits(in Vector3 expected, in Vector3 actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.X), BitConverter.DoubleToInt64Bits(actual.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Y), BitConverter.DoubleToInt64Bits(actual.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Z), BitConverter.DoubleToInt64Bits(actual.Z));
    }

    private static void AssertSameBits(in Quaternion expected, in Quaternion actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.W), BitConverter.DoubleToInt64Bits(actual.W));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.VectorPart.X),
            BitConverter.DoubleToInt64Bits(actual.VectorPart.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.VectorPart.Y),
            BitConverter.DoubleToInt64Bits(actual.VectorPart.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.VectorPart.Z),
            BitConverter.DoubleToInt64Bits(actual.VectorPart.Z));
    }
}
