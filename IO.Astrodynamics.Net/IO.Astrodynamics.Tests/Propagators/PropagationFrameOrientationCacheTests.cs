using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Exceptions;
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
        var start = TimeSystem.Time.J2000Tdb;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at an exact grid point (t=300s from start)
        var queryEpoch = start.AddSeconds(300);
        var cached = cache.GetOrientation(queryEpoch);
        var direct = earth.Frame.GetStateOrientationToIcrf(queryEpoch);

        // At grid points, should match very closely (just floating-point arithmetic)
        AssertQuaternionClose(direct.Rotation, cached.Rotation, 1e-12);
        AssertVector3Close(direct.AngularVelocity, cached.AngularVelocity, 1e-14);
    }

    [Fact]
    public void InterpolatedOrientationMatchesSpiceMidGrid()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000Tdb;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at mid-grid point (t=330s = midway between 300s and 360s grid points)
        var queryEpoch = start.AddSeconds(330);
        var cached = cache.GetOrientation(queryEpoch);
        var direct = earth.Frame.GetStateOrientationToIcrf(queryEpoch);

        // SLERP over 60s for Earth rotation should be extremely accurate
        // Earth rotates ~0.0042 deg/s, so 60s = 0.25 deg, SLERP error is negligible
        AssertQuaternionClose(direct.Rotation, cached.Rotation, 1e-9);
        AssertVector3Close(direct.AngularVelocity, cached.AngularVelocity, 1e-10);
    }

    [Fact]
    public void InterpolatedOrientationMatchesSpiceAtWindowBoundaries()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000Tdb;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at exact window start
        var cachedStart = cache.GetOrientation(start);
        var directStart = earth.Frame.GetStateOrientationToIcrf(start);
        AssertQuaternionClose(directStart.Rotation, cachedStart.Rotation, 1e-9);

        // Query at exact window end
        var cachedEnd = cache.GetOrientation(end);
        var directEnd = earth.Frame.GetStateOrientationToIcrf(end);
        AssertQuaternionClose(directEnd.Rotation, cachedEnd.Rotation, 1e-9);
    }

    [Fact]
    public void CacheWorksWithArbitraryQueryTimes()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000Tdb;
        var end = start.AddSeconds(86400);
        var window = new Window(start, end);

        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));

        // Query at various non-aligned times through the 24h window
        double[] offsets = { 7.3, 123.456, 999.99, 43200.0, 86399.5 };
        foreach (var offset in offsets)
        {
            var queryEpoch = start.AddSeconds(offset);
            var cached = cache.GetOrientation(queryEpoch);
            var direct = earth.Frame.GetStateOrientationToIcrf(queryEpoch);

            AssertQuaternionClose(direct.Rotation, cached.Rotation, 1e-9);
            AssertVector3Close(direct.AngularVelocity, cached.AngularVelocity, 1e-10);
        }
    }

    [Fact]
    public void ConstructorThrowsOnNullFrame()
    {
        var start = TimeSystem.Time.J2000Tdb;
        var window = new Window(start, start.AddSeconds(3600));

        Assert.Throws<ArgumentNullException>(() =>
            new PropagationFrameOrientationCache(window, null, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void ConstructorThrowsOnNonPositiveGridStep()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = TimeSystem.Time.J2000Tdb;
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
        var start = TimeSystem.Time.J2000Tdb;
        var end = start.AddSeconds(3600);
        var window = new Window(start, end);

        // Get direct result first (no cache)
        var directResult = earth.Frame.GetStateOrientationToIcrf(start.AddSeconds(30));

        // Make the cache active on this thread
        var cache = new PropagationFrameOrientationCache(window, earth.Frame, TimeSpan.FromSeconds(60));
        using (Frames.Frame.UseOrientationCache(earth.Frame, cache))
        {
            // Should now go through cache
            Assert.Same(cache, earth.Frame.OrientationCache);
            var cachedResult = earth.Frame.GetStateOrientationToIcrf(start.AddSeconds(30));
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
        var start = TimeSystem.Time.J2000Tdb;
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
        var start = TimeSystem.Time.J2000Tdb;
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

    /// <summary>
    /// Every cell of the 60 s grid of the Earth over 6 h, at 19 fractions of each (0.05 to 0.95), against CSPICE
    /// <c>sxform</c>: the exact SLERP of the grid points (#561). The normalised-Lerp fallback of <c>Quaternion.Slerp</c>,
    /// which every cell of the grid took (4.4e-3 rad between two points), was up to 3.4e-10 rad off; the exact SLERP of the
    /// same points 2.4e-12 rad (measured in #561, 2026-11-03).
    /// </summary>
    /// <remarks>Measured on a85098d (Linux x64, Release, 2026-11-17): 3.371e-10 rad; with the exact SLERP 2.381e-12 rad, the
    /// angular velocity 3.7e-15 rad/s (linear between the points, unchanged); with the exact SLERP 2.381e-12 rad on Linux
    /// x64, Windows x64 and macOS arm64 alike (CI run 44225490669). The bound is twice that worst, rounded up.</remarks>
    [Fact]
    public void Interpolation_IsTheExactSlerp_AgainstSxform()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var start = new TimeSystem.Time(new DateTime(2024, 3, 1, 0, 0, 0), TimeScale.TDB);
        var cache = new PropagationFrameOrientationCache(new Window(start, start.AddHours(6.0)), earth.Frame,
            TimeSpan.FromSeconds(60));

        // Act
        double worst = 0.0;
        double worstRate = 0.0;
        for (int cell = 0; cell < 360; cell++)
        {
            for (int k = 1; k <= 19; k++)
            {
                var epoch = start.AddSeconds(60.0 * cell + 3.0 * k);
                var spice = SpiceApi.Instance.TransformFrame(epoch, earth.Frame, Frames.Frame.ICRF);
                var cached = cache.GetOrientation(epoch);
                worst = System.Math.Max(worst, RotationAngle(spice.Rotation, cached.Rotation));
                worstRate = System.Math.Max(worstRate, (spice.AngularVelocity - cached.AngularVelocity).Magnitude());
            }
        }

        // Assert
        _output.WriteLine($"MEASURE exact SLERP against sxform over 6 840 epochs: {worst:E3} rad, angular velocity {worstRate:E3} rad/s");
        Assert.True(worst < ExactSlerpAgainstSxform, $"{worst:E3} rad");
    }

    /// <summary>Twice the worst with the exact SLERP, 2.381e-12 rad on the three platforms (CI run 44225490669), rounded
    /// up.</summary>
    private const double ExactSlerpAgainstSxform = 4.8e-12;

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
    // worst 3.43e-10 rad with the normalised-Lerp fallback (a window starting 59.999 s after the coverage start),
    // angular velocity 1.117e-17 rad/s (1.116e-17 on Linux x64); bounds twice the worst over Linux x64, Windows x64 and
    // macOS arm64 (CI of #622 at 7f5e6dfb, 2026-11-10), rounded up to two digits. With the exact SLERP (#561) the angle is
    // 1.012e-13 rad at worst (the window start) on Linux x64, Windows x64 and macOS arm64 alike (CI run 44225490669):
    // twice that, rounded up; the angular velocity bound unchanged
    private const double AngleBound = 2.1e-13;
    private const double AngularVelocityBound = 2.3e-17;

    [Theory]
    [InlineData(0.0, UnalignedLength)]
    [InlineData(-30.0, UnalignedLength)]
    [InlineData(0.0, 3600.0)]
    [InlineData(-30.0, 3600.0)]
    public void WindowEndingInsideTheCoverage_ReadsEveryEpochOfTheWindow(double endFromCoverageEnd, double length)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var end = TimeSystem.Time.CreateTdb(CoverageEnd + endFromCoverageEnd);
        var start = end.AddSeconds(-length);
        var cache = new PropagationFrameOrientationCache(new Window(start, end), earth.Frame, TimeSpan.FromSeconds(60));

        // Against CSPICE sxform at each epoch: the grid's SLERP over its last interval, the window end itself, and 98
        // epochs evenly spaced over the window
        double worstAngle = 0.0, worstAngularVelocity = 0.0;
        foreach (double fromEnd in new[] { -length, -100.0, -45.0, -30.0, -10.0, -1.0, 0.0 }.Concat(EvenlySpaced(-length, 0.0)))
        {
            var epoch = end.AddSeconds(fromEnd);
            var cached = cache.GetOrientation(epoch);
            var direct = SpiceApi.Instance.TransformFrame(epoch, earth.Frame, Frames.Frame.ICRF);
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
        var spiceAtEnd = SpiceApi.Instance.TransformFrame(end, earth.Frame, Frames.Frame.ICRF);
        Assert.True(RotationAngle(spiceAtEnd.Rotation, atEnd.Rotation) < 2.3e-16);
    }

    [Fact]
    public void ReadPastTheCoverage_ThrowsWithSpicesMessage()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var end = TimeSystem.Time.CreateTdb(CoverageEnd + 1.0);
        var cache = new PropagationFrameOrientationCache(new Window(end.AddSeconds(-UnalignedLength), end), earth.Frame,
            TimeSpan.FromSeconds(60));

        // Inside the coverage, the orientation; past it, SPICE's message
        var inside = TimeSystem.Time.CreateTdb(CoverageEnd - 1.0);
        Assert.True(RotationAngle(SpiceApi.Instance.TransformFrame(inside, earth.Frame, Frames.Frame.ICRF).Rotation,
            cache.GetOrientation(inside).Rotation) < AngleBound);
        var spice = Assert.Throws<SpiceException>(() =>
            SpiceApi.Instance.TransformFrame(end, earth.Frame, Frames.Frame.ICRF));
        var exception = Assert.Throws<InvalidOperationException>(() => cache.GetOrientation(end));
        Assert.Contains("ITRF93", exception.Message);
        Assert.Contains(spice.Message, exception.Message);
        Assert.IsType<SpiceException>(exception.InnerException);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrientation(TimeSystem.Time.CreateTdb(CoverageEnd + 0.001)));
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
            var start = TimeSystem.Time.CreateTdb(CoverageStart + fromCoverageStart);
            if (start.TimeSpanFromJ2000().TotalSeconds < CoverageStart + fromCoverageStart) start = start.AddTicks(1);
            var cache = new PropagationFrameOrientationCache(new Window(start, start.AddSeconds(UnalignedLength)), earth.Frame,
                TimeSpan.FromSeconds(60));
            foreach (double fromStart in new[] { 0.0, 1.0, 30.0, 61.0 }.Concat(EvenlySpaced(0.0, UnalignedLength)))
            {
                var epoch = start.AddSeconds(fromStart);
                var direct = SpiceApi.Instance.TransformFrame(epoch, earth.Frame, Frames.Frame.ICRF);
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
        var early = TimeSystem.Time.CreateTdb(CoverageStart - 1.0);
        var before = new PropagationFrameOrientationCache(new Window(early, early.AddSeconds(UnalignedLength)), earth.Frame,
            TimeSpan.FromSeconds(60));
        Assert.Throws<InvalidOperationException>(() => before.GetOrientation(early));
        Assert.True(RotationAngle(SpiceApi.Instance.TransformFrame(early.AddSeconds(2.0), earth.Frame, Frames.Frame.ICRF).Rotation,
            before.GetOrientation(early.AddSeconds(2.0)).Rotation) < AngleBound);
    }

    [Theory]
    [InlineData(0.0, UnalignedLength, false)]
    [InlineData(-30.0, UnalignedLength, false)]
    [InlineData(0.0, 3600.0, false)]
    [InlineData(-30.0, 3600.0, false)]
    [InlineData(1.0, UnalignedLength, true)]
    // 59.5 steps of 60 s: before 11.0.0 the propagation ran to 3600 s, 30 s past the coverage, and threw; since choice 6 of
    // #668 it ends at the window end, within the coverage
    [InlineData(0.0, 3570.0, false)]
    public void Propagation_EndingAtTheCoverageEnd_Runs(double endFromCoverageEnd, double length, bool throws)
    {
        // A geopotential reads the orientation of the Earth at every stage, up to the window end
        var end = TimeSystem.Time.CreateTdb(CoverageEnd + endFromCoverageEnd);
        var start = end.AddSeconds(-length);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var leo = new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 7612.6, 0.0), earth, start, Frames.Frame.ICRF);
        int id = -622100 - (int)(endFromCoverageEnd + 31.0) * 10 - (int)(length - 3600.0);
        var spacecraft = new Spacecraft(id, $"KR1{-id}", 100.0, leo, 1000.0, new Clock($"KR1{-id}CLK", 256));

        void Propagate() => spacecraft.Propagate(new Window(start, end), new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = new CelestialItem[] { earth }, CovariancePropagation = CovariancePropagationOptions.None });

        if (throws)
        {
            var exception = Assert.Throws<InvalidOperationException>(Propagate);
            Assert.Contains("ITRF93", exception.Message);
            return;
        }

        Propagate();
        // At the window end: the stored states run to it (#741; at 60 s outputs a window of 3 610 s stored them to 3 600 s
        // before, and a read past them throws, #527)
        var last = spacecraft.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None);
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
