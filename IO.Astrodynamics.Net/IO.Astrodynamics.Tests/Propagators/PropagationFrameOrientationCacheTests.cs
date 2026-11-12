using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators;

public class PropagationFrameOrientationCacheTests
{
    private readonly ITestOutputHelper _output;

    public PropagationFrameOrientationCacheTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
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

    // ---- KR-1 of lot 6 (#457): a window that ends (or starts) inside the PCK coverage, its next grid point outside ----

    // The coverage of ITRF93 in earth_latest_high_prec.bpc, CSPICE pckcov (body 3000), ET: sxform('ITRF93', 'J2000')
    // succeeds at the end and 1 ms before it, fails 1 ms after it; succeeds at the start, fails 1 ms before it
    // (spiceypy 8.3.0, CSPICE N0067)
    private const double CoverageStart = -43135.816087188054;
    private const double CoverageEnd = 831729669.1853374;

    // The grid is aligned on the window start minus two steps of 60 s: a length whose remainder modulo 60 s is in
    // (0, 30) s puts the first grid point after the window end more than 30 s past it
    private const double UnalignedLength = 3610.0;

    // The SLERP of the grid against CSPICE sxform, 98 epochs evenly spaced over each window besides the chosen ones:
    // worst 3.43e-10 rad (a window starting 59.999 s after the coverage start), angular velocity 1.117e-17 rad/s
    // (1.116e-17 on Linux x64); bounds twice the worst over Linux x64, Windows x64 and macOS arm64 (CI of #622 at
    // 7f5e6dfb, 2026-11-10), rounded up to two digits
    private const double AngleBound = 6.9e-10;
    private const double AngularVelocityBound = 2.3e-17;

    [Theory]
    [InlineData(0.0, UnalignedLength)]
    [InlineData(-30.0, UnalignedLength)]
    [InlineData(0.0, 3600.0)]
    [InlineData(-30.0, 3600.0)]
    public void WindowEndingInsideTheCoverage_ReadsEveryEpochOfTheWindow(double endFromCoverageEnd, double length)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var end = TimeSystem.Time.CreateTDB(CoverageEnd + endFromCoverageEnd);
        var start = end.AddSeconds(-length);
        var cache = new PropagationFrameOrientationCache(new Window(start, end), earth.Frame, TimeSpan.FromSeconds(60));

        // Against CSPICE sxform at each epoch: the grid's SLERP over its last interval, the window end itself, and 98
        // epochs evenly spaced over the window
        double worstAngle = 0.0, worstAngularVelocity = 0.0;
        foreach (double fromEnd in new[] { -length, -100.0, -45.0, -30.0, -10.0, -1.0, 0.0 }.Concat(EvenlySpaced(-length, 0.0)))
        {
            var epoch = end.AddSeconds(fromEnd);
            var cached = cache.GetOrientation(epoch);
            var direct = SpiceAPI.Instance.TransformFrame(epoch, earth.Frame, Frames.Frame.ICRF);
            double angle = RotationAngle(direct.Rotation, cached.Rotation);
            double angularVelocity = (direct.AngularVelocity - cached.AngularVelocity).Magnitude();
            worstAngle = System.Math.Max(worstAngle, angle);
            worstAngularVelocity = System.Math.Max(worstAngularVelocity, angularVelocity);
            Assert.True(angle < AngleBound, $"{fromEnd} s from the window end: {angle:E3} rad");
            Assert.True(angularVelocity < AngularVelocityBound, $"{fromEnd} s from the window end: {angularVelocity:E3} rad/s");
            Assert.Equal(direct.Rotation.Magnitude(), cached.Rotation.Magnitude(), 12);
        }

        _output.WriteLine($"MEASURE KR-1 cache end {endFromCoverageEnd} s, {length} s: worst {worstAngle:E3} rad, {worstAngularVelocity:E3} rad/s");

        // At the window end, the orientation read there: 0 rad measured (Linux x64, 2026-11-09), bounded by twice the
        // resolution of the angle (1.1e-16 rad), a floor rather than a measurement; passed on Linux x64, Windows x64
        // and macOS arm64 (CI of #622 at 7f5e6dfb, 2026-11-10), which print no figure
        var atEnd = cache.GetOrientation(end);
        var spiceAtEnd = SpiceAPI.Instance.TransformFrame(end, earth.Frame, Frames.Frame.ICRF);
        Assert.True(RotationAngle(spiceAtEnd.Rotation, atEnd.Rotation) < 2.3e-16);
    }

    [Fact]
    public void ReadPastTheCoverage_ThrowsWithSpicesMessage()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var end = TimeSystem.Time.CreateTDB(CoverageEnd + 1.0);
        var cache = new PropagationFrameOrientationCache(new Window(end.AddSeconds(-UnalignedLength), end), earth.Frame,
            TimeSpan.FromSeconds(60));

        // Inside the coverage, the orientation; past it, SPICE's message
        var inside = TimeSystem.Time.CreateTDB(CoverageEnd - 1.0);
        Assert.True(RotationAngle(SpiceAPI.Instance.TransformFrame(inside, earth.Frame, Frames.Frame.ICRF).Rotation,
            cache.GetOrientation(inside).Rotation) < AngleBound);
        var spice = Assert.Throws<InvalidOperationException>(() =>
            SpiceAPI.Instance.TransformFrame(end, earth.Frame, Frames.Frame.ICRF));
        var exception = Assert.Throws<InvalidOperationException>(() => cache.GetOrientation(end));
        Assert.Contains("ITRF93", exception.Message);
        Assert.Contains(spice.Message, exception.Message);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrientation(TimeSystem.Time.CreateTDB(CoverageEnd + 0.001)));
    }

    [Fact]
    public void WindowStartingInsideTheCoverage_ReadsEveryEpochOfTheWindow()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double worst = 0.0, worstAngularVelocity = 0.0;
        foreach (double fromCoverageStart in new[] { 0.0, 1.0e-3, 0.5, 30.0, 59.0, 59.999, 61.0 })
        {
            // The first tick inside the coverage: the start, −431358160871.88 ticks, has its nearest tick 12 ns before it
            // (#625; the truncation toward zero put it 88 ns after)
            var start = TimeSystem.Time.CreateTDB(CoverageStart + fromCoverageStart);
            if (start.TimeSpanFromJ2000().TotalSeconds < CoverageStart + fromCoverageStart) start = start.AddTicks(1);
            var cache = new PropagationFrameOrientationCache(new Window(start, start.AddSeconds(UnalignedLength)), earth.Frame,
                TimeSpan.FromSeconds(60));
            foreach (double fromStart in new[] { 0.0, 1.0, 30.0, 61.0 }.Concat(EvenlySpaced(0.0, UnalignedLength)))
            {
                var epoch = start.AddSeconds(fromStart);
                var direct = SpiceAPI.Instance.TransformFrame(epoch, earth.Frame, Frames.Frame.ICRF);
                var cached = cache.GetOrientation(epoch);
                double angle = RotationAngle(direct.Rotation, cached.Rotation);
                double angularVelocity = (direct.AngularVelocity - cached.AngularVelocity).Magnitude();
                worst = System.Math.Max(worst, angle);
                worstAngularVelocity = System.Math.Max(worstAngularVelocity, angularVelocity);
                Assert.True(angle < AngleBound, $"{fromCoverageStart} s, {fromStart} s: {angle:E3} rad");
                Assert.True(angularVelocity < AngularVelocityBound, $"{fromCoverageStart} s, {fromStart} s: {angularVelocity:E3} rad/s");
            }
        }

        _output.WriteLine($"MEASURE KR-1 cache start worst {worst:E3} rad, {worstAngularVelocity:E3} rad/s");

        // Before the coverage, SPICE's message
        var early = TimeSystem.Time.CreateTDB(CoverageStart - 1.0);
        var before = new PropagationFrameOrientationCache(new Window(early, early.AddSeconds(UnalignedLength)), earth.Frame,
            TimeSpan.FromSeconds(60));
        Assert.Throws<InvalidOperationException>(() => before.GetOrientation(early));
        Assert.True(RotationAngle(SpiceAPI.Instance.TransformFrame(early.AddSeconds(2.0), earth.Frame, Frames.Frame.ICRF).Rotation,
            before.GetOrientation(early.AddSeconds(2.0)).Rotation) < AngleBound);
    }

    [Theory]
    [InlineData(0.0, UnalignedLength, false)]
    [InlineData(-30.0, UnalignedLength, false)]
    [InlineData(0.0, 3600.0, false)]
    [InlineData(-30.0, 3600.0, false)]
    [InlineData(1.0, UnalignedLength, true)]
    [InlineData(0.0, 3570.0, true)] // 59.5 steps of 60 s: the propagation runs to 3600 s, 30 s past the coverage
    public void Propagation_EndingAtTheCoverageEnd_Runs(double endFromCoverageEnd, double length, bool throws)
    {
        // A geopotential reads the orientation of the Earth at every stage, up to the window end
        var end = TimeSystem.Time.CreateTDB(CoverageEnd + endFromCoverageEnd);
        var start = end.AddSeconds(-length);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var leo = new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 7612.6, 0.0), earth, start, Frames.Frame.ICRF);
        int id = -622100 - (int)(endFromCoverageEnd + 31.0) * 10 - (int)(length - 3600.0);
        var spacecraft = new Spacecraft(id, $"KR1{-id}", 100.0, 1000.0, new Clock($"KR1{-id}CLK", 256), leo);

        void Propagate() => spacecraft.Propagate(new Window(start, end), new CelestialItem[] { earth }, new RK78Integrator(), false,
            false, TimeSpan.FromSeconds(60.0));

        if (throws)
        {
            var exception = Assert.Throws<InvalidOperationException>(Propagate);
            Assert.Contains("ITRF93", exception.Message);
            return;
        }

        Propagate();
        var last = spacecraft.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        Assert.InRange(last.Position.Magnitude(), 6.0e6, 8.0e6);
    }

    // 98 epochs evenly spaced over [from, to], the ends included (the review's denser sampling)
    private static System.Collections.Generic.IEnumerable<double> EvenlySpaced(double from, double to) =>
        Enumerable.Range(0, 98).Select(i => from + (to - from) * i / 97.0);

    private static double RotationAngle(Quaternion expected, Quaternion actual)
    {
        // The angle of expected* actual, from its vector part (the sign of a quaternion is free)
        var difference = expected.Conjugate() * actual;
        return 2.0 * System.Math.Asin(System.Math.Min(1.0, difference.VectorPart.Magnitude() / difference.Magnitude()));
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
