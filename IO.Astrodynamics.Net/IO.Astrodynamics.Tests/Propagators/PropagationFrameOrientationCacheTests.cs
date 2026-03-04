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

        // Inject cache
        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));
        earth.Frame.OrientationCache = cache;

        try
        {
            // Should now go through cache
            var cachedResult = earth.Frame.GetStateOrientationToICRF(start.AddSeconds(30));
            AssertQuaternionClose(directResult.Rotation, cachedResult.Rotation, 1e-9);
        }
        finally
        {
            // Clean up
            earth.Frame.OrientationCache = null;
        }
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
