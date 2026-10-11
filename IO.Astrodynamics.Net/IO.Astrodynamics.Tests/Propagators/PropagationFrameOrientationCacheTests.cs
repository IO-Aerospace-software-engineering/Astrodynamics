using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators;

public class PropagationFrameOrientationCacheTests
{
    public PropagationFrameOrientationCacheTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void InterpolatedOrientationMatchesSpiceAtGridPoint()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at an exact grid point (t=300s from start)
        var queryEpoch = start.AddSeconds(300);
        var cached = cache.GetOrientation(queryEpoch);
        var direct = earth.Frame.GetStateOrientationToICRF(queryEpoch);

        // At grid points, should match very closely (just floating-point arithmetic)
        AssertQuaternionClose(direct.Rotation, cached.Rotation, 1e-12);
        AssertVector3Close(direct.AngularVelocity, cached.AngularVelocity, 1e-14);
    }

    [Fact]
    public void InterpolatedOrientationMatchesSpiceMidGrid()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at mid-grid point (t=330s = midway between 300s and 360s grid points)
        var queryEpoch = start.AddSeconds(330);
        var cached = cache.GetOrientation(queryEpoch);
        var direct = earth.Frame.GetStateOrientationToICRF(queryEpoch);

        // SLERP over 60s for Earth rotation should be extremely accurate
        // Earth rotates ~0.0042 deg/s, so 60s = 0.25 deg, SLERP error is negligible
        AssertQuaternionClose(direct.Rotation, cached.Rotation, 1e-9);
        AssertVector3Close(direct.AngularVelocity, cached.AngularVelocity, 1e-10);
    }

    [Fact]
    public void InterpolatedOrientationMatchesSpiceAtWindowBoundaries()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at exact window start
        var cachedStart = cache.GetOrientation(start);
        var directStart = earth.Frame.GetStateOrientationToICRF(start);
        AssertQuaternionClose(directStart.Rotation, cachedStart.Rotation, 1e-9);

        // Query at exact window end
        var cachedEnd = cache.GetOrientation(end);
        var directEnd = earth.Frame.GetStateOrientationToICRF(end);
        AssertQuaternionClose(directEnd.Rotation, cachedEnd.Rotation, 1e-9);
    }

    [Fact]
    public void CacheWorksWithArbitraryQueryTimes()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var end = start.AddSeconds(86400);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at various non-aligned times through the 24h window
        double[] offsets = { 7.3, 123.456, 999.99, 43200.0, 86399.5 };
        foreach (var offset in offsets)
        {
            var queryEpoch = start.AddSeconds(offset);
            var cached = cache.GetOrientation(queryEpoch);
            var direct = earth.Frame.GetStateOrientationToICRF(queryEpoch);

            AssertQuaternionClose(direct.Rotation, cached.Rotation, 1e-9);
            AssertVector3Close(direct.AngularVelocity, cached.AngularVelocity, 1e-10);
        }
    }

    [Fact]
    public void ConstructorThrowsOnNullFrame()
    {
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start, start.AddSeconds(3600));

        Assert.Throws<ArgumentNullException>(() =>
            new PropagationFrameOrientationCache(window, null, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void ConstructorThrowsOnNonPositiveGridStep()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start, start.AddSeconds(3600));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void FrameOrientationCacheBypassWorks()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        // Get direct result first (no cache)
        var directResult = earth.Frame.GetStateOrientationToICRF(start.AddSeconds(30));

        // Make the cache active on this thread
        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));
        using (Frames.Frame.UseOrientationCache(earth.Frame, cache))
        {
            // Should now go through cache
            Assert.Same(cache, earth.Frame.OrientationCache);
            var cachedResult = earth.Frame.GetStateOrientationToICRF(start.AddSeconds(30));
            AssertQuaternionClose(directResult.Rotation, cachedResult.Rotation, 1e-9);
        }

        // Restored when the scope ends
        Assert.Null(earth.Frame.OrientationCache);
    }

    [Fact]
    public void FrameOrientationCache_IsActiveOnTheCurrentThreadOnly_AndForThatFrameInstanceOnly()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var otherEarth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000TDB;
        var cache = new PropagationFrameOrientationCache(new Window(start, start.AddSeconds(3600)), earth.Frame,
            TimeSpan.FromSeconds(60));

        using (Frames.Frame.UseOrientationCache(earth.Frame, cache))
        {
            PropagationFrameOrientationCache seenFromAnotherThread = cache;
            var thread = new System.Threading.Thread(() => seenFromAnotherThread = earth.Frame.OrientationCache);
            thread.Start();
            thread.Join();

            Assert.Null(seenFromAnotherThread);
            Assert.Null(otherEarth.Frame.OrientationCache);
            Assert.Same(cache, earth.Frame.OrientationCache);
        }
    }

    [Fact]
    public void FrameOrientationCache_NestedScopes_RestoreTheEnclosingCache()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start, start.AddSeconds(3600));
        var earthCache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));
        var moonCache = new PropagationFrameOrientationCache(window, moon.Frame, TimeSpan.FromSeconds(60));

        using (Frames.Frame.UseOrientationCache(earth.Frame, earthCache))
        {
            using (Frames.Frame.UseOrientationCache(moon.Frame, moonCache))
            {
                Assert.Same(moonCache, moon.Frame.OrientationCache);
                Assert.Null(earth.Frame.OrientationCache);
            }

            // A null frame or cache changes nothing
            using (Frames.Frame.UseOrientationCache(null, moonCache))
            using (Frames.Frame.UseOrientationCache(moon.Frame, null))
            {
                Assert.Same(earthCache, earth.Frame.OrientationCache);
            }

            Assert.Same(earthCache, earth.Frame.OrientationCache);
            Assert.Null(moon.Frame.OrientationCache);
        }

        Assert.Null(earth.Frame.OrientationCache);
    }

    private static void AssertQuaternionClose(Quaternion expected, Quaternion actual, double tolerance)
    {
        // Account for quaternion double-cover (q and -q represent same rotation)
        double dot = expected.W * actual.W
                     + expected.VectorPart.X * actual.VectorPart.X
                     + expected.VectorPart.Y * actual.VectorPart.Y
                     + expected.VectorPart.Z * actual.VectorPart.Z;

        // |dot| should be very close to 1 for nearly identical rotations
        Assert.True(System.Math.Abs(System.Math.Abs(dot) - 1.0) < tolerance,
            $"Quaternion mismatch: |dot|={System.Math.Abs(dot):E15}, expected ~1.0 within {tolerance:E2}");
    }

    private static void AssertVector3Close(Vector3 expected, Vector3 actual, double tolerance)
    {
        var diff = expected - actual;
        double error = diff.Magnitude();
        Assert.True(error < tolerance,
            $"Vector3 mismatch: error={error:E15}, tolerance={tolerance:E2}");
    }
}
