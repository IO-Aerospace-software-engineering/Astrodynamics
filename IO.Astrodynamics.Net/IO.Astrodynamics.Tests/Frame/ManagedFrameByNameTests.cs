using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;

namespace IO.Astrodynamics.Tests.Frame;

/// <summary>
/// A frame named CIRS or TIRS, in any case, is <see cref="Frames.Frame.CIRS"/> or <see cref="Frames.Frame.TIRS"/> on every
/// path, as a frame named TEME is <see cref="Frames.Frame.TEME"/> (#656): <see cref="Frames.Frame.ToFrame"/> both ways, both
/// data providers, the ephemeris reads with and without light time, a state converted to it and
/// <see cref="SpiceAPI.TransformFrame(TimeSystem_Time, Frames.Frame, Frames.Frame)"/>, which composes CIRS and TIRS through
/// ICRF. Before, a frame built by name reached SPICE, which does not know these names, and threw.
/// </summary>
/// <remarks>
/// The independent reference is SOFA (<c>Data/SofaReference/iau2006_sweep.json</c>, <c>tools/sofa_reference/sofa_ref.c
/// --sweep</c>, IAU 2006/2000A, UT1 = UTC): its GCRS → TIRS matrix, and its CIP X, Y and CIO locator s, from which the test
/// builds GCRS → CIRS by the closed form of SOFA's <c>iauC2ixys</c>, R = Rz(−(E + s)) · Ry(d) · Rz(E), E = atan2(Y, X),
/// d = atan(√(r² / (1 − r²))), r² = X² + Y² (SOFA's passive rotations). The bound is that of the SOFA sweep of the frames,
/// 5e-9 rad (<see cref="Iau2006SofaSweepTests"/>: IAU 2000B against 2000A, 3.55e-9 measured on 2026-10-04); the routing
/// itself is checked bit for bit against the static frames.
/// </remarks>
public class ManagedFrameByNameTests
{
    // The bound of the SOFA sweep of the CIP and of GCRS -> TIRS (Iau2006SofaSweepTests, 3.55e-9 rad measured)
    private const double SofaTolerance = 5e-9;

    private readonly ITestOutputHelper _output;

    public ManagedFrameByNameTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private sealed record SofaEpoch(TimeSystem_Time Utc, double X, double Y, double S, double[] GcrsToTirs);

    private static readonly Lazy<SofaEpoch[]> Sweep = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText("Data/SofaReference/iau2006_sweep.json"));
        var epochs = new List<SofaEpoch>();
        foreach (var e in document.RootElement.EnumerateArray())
        {
            var utc = DateTime.ParseExact(e.GetProperty("utc").GetString()!, "yyyy-MM-dd'T'HH:mm:ss.fffffff",
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            epochs.Add(new SofaEpoch(new TimeSystem_Time(utc, TimeScale.UTC), e.GetProperty("cip_x").GetDouble(),
                e.GetProperty("cip_y").GetDouble(), e.GetProperty("cio_s").GetDouble(),
                e.GetProperty("gcrs_to_tirs").EnumerateArray().Select(v => v.GetDouble()).ToArray()));
        }

        return epochs.ToArray();
    });

    // Epochs of the sweep within the coverage of the test kernels and of the Earth's binary PCK: 2000, 2012, 2020 and 2024
    private static IEnumerable<SofaEpoch> Epochs => new[] { 60, 132, 180, 204 }.Select(i => Sweep.Value[i]);

    public static IEnumerable<object[]> Names => new[] { "CIRS", "cirs", "Cirs", "TIRS", "tirs", "Tirs" }.Select(n => new object[] { n });

    private static Frames.Frame StaticFrameOf(string name) =>
        string.Equals(name, "CIRS", StringComparison.OrdinalIgnoreCase) ? Frames.Frame.CIRS : Frames.Frame.TIRS;

    // SOFA's passive rotations (iauRz, iauRy) as row-major 3x3 arrays
    private static double[,] Rz(double psi)
    {
        double c = System.Math.Cos(psi), s = System.Math.Sin(psi);
        return new[,] { { c, s, 0.0 }, { -s, c, 0.0 }, { 0.0, 0.0, 1.0 } };
    }

    private static double[,] Ry(double theta)
    {
        double c = System.Math.Cos(theta), s = System.Math.Sin(theta);
        return new[,] { { c, 0.0, -s }, { 0.0, 1.0, 0.0 }, { s, 0.0, c } };
    }

    private static double[,] Multiply(double[,] a, double[,] b)
    {
        var r = new double[3, 3];
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
        for (int k = 0; k < 3; k++)
            r[i, j] += a[i, k] * b[k, j];
        return r;
    }

    // GCRS -> frame, SOFA: iauC2ixys for CIRS, the sweep's iauC2tcio for TIRS
    private static double[,] SofaGcrsTo(string name, SofaEpoch epoch)
    {
        if (string.Equals(name, "CIRS", StringComparison.OrdinalIgnoreCase))
        {
            double r2 = epoch.X * epoch.X + epoch.Y * epoch.Y;
            double e = r2 > 0.0 ? System.Math.Atan2(epoch.Y, epoch.X) : 0.0;
            double d = System.Math.Atan(System.Math.Sqrt(r2 / (1.0 - r2)));
            return Multiply(Rz(-(e + epoch.S)), Multiply(Ry(d), Rz(e)));
        }

        var m = new double[3, 3];
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
            m[i, j] = epoch.GcrsToTirs[3 * i + j];
        return m;
    }

    private static Vector3 Apply(double[,] m, Vector3 v) => new(
        m[0, 0] * v.X + m[0, 1] * v.Y + m[0, 2] * v.Z,
        m[1, 0] * v.X + m[1, 1] * v.Y + m[1, 2] * v.Z,
        m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z);

    private static Vector3 ApplyTranspose(double[,] m, Vector3 v) => new(
        m[0, 0] * v.X + m[1, 0] * v.Y + m[2, 0] * v.Z,
        m[0, 1] * v.X + m[1, 1] * v.Y + m[2, 1] * v.Z,
        m[0, 2] * v.X + m[1, 2] * v.Y + m[2, 2] * v.Z);

    private static double AngleBetween(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a.Dot(b));

    private static double Angle(Quaternion left, Quaternion right)
    {
        var relative = left.Conjugate() * right;
        return 2.0 * System.Math.Atan2(relative.VectorPart.Magnitude(), System.Math.Abs(relative.W));
    }

    // The largest angle between the axes rotated by q (frame -> ICRF) and by the transpose of SOFA's GCRS -> frame
    private static double WorstAxisAngle(Quaternion frameToIcrf, double[,] sofaGcrsToFrame)
    {
        return new[] { Vector3.VectorX, Vector3.VectorY, Vector3.VectorZ }
            .Max(axis => AngleBetween(axis.Rotate(frameToIcrf), ApplyTranspose(sofaGcrsToFrame, axis)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ToFrame_ByName_IsTheManagedFrameBothWays(string name)
    {
        double worst = 0.0;
        foreach (var epoch in Epochs)
        {
            var byName = new Frames.Frame(name);
            var toIcrf = byName.ToFrame(Frames.Frame.ICRF, epoch.Utc);
            var fromIcrf = Frames.Frame.ICRF.ToFrame(byName, epoch.Utc);
            var sofa = SofaGcrsTo(name, epoch);

            worst = System.Math.Max(worst, WorstAxisAngle(toIcrf.Rotation, sofa));
            worst = System.Math.Max(worst, WorstAxisAngle(fromIcrf.Rotation.Conjugate(), sofa));

            // The routing: the static frame's orientation, bit for bit, labelled with the frame built by name
            var managed = StaticFrameOf(name).ToFrame(Frames.Frame.ICRF, epoch.Utc);
            Assert.Equal(managed.Rotation, toIcrf.Rotation);
            Assert.Equal(managed.AngularVelocity, toIcrf.AngularVelocity);
            Assert.Same(byName, toIcrf.ReferenceFrame);
        }

        _output.WriteLine($"MEASURE {name} by name -> ICRF against SOFA: {worst.ToString("E3", CultureInfo.InvariantCulture)} rad");
        Assert.InRange(worst, 0.0, SofaTolerance);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void MemoryDataProvider_RoutesTheNameToTheManagedFrame(string name)
    {
        // Called directly on an empty provider: the global provider is never swapped (#631)
        var provider = new MemoryDataProvider();
        var byName = new Frames.Frame(name);
        foreach (var epoch in Epochs)
        {
            var orientation = provider.FrameTransformationToICRF(epoch.Utc, byName);
            var managed = StaticFrameOf(name).GetStateOrientationToICRF(epoch.Utc);
            Assert.Equal(managed.Rotation, orientation.Rotation);
            Assert.Equal(managed.AngularVelocity, orientation.AngularVelocity);
            Assert.Same(byName, orientation.ReferenceFrame);
            Assert.InRange(WorstAxisAngle(orientation.Rotation, SofaGcrsTo(name, epoch)), 0.0, SofaTolerance);
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void SpiceDataProvider_RoutesTheNameToTheManagedFrame(string name)
    {
        var provider = new SpiceDataProvider();
        var byName = new Frames.Frame(name);
        foreach (var epoch in Epochs)
        {
            var orientation = provider.FrameTransformationToICRF(epoch.Utc, byName);
            var managed = StaticFrameOf(name).GetStateOrientationToICRF(epoch.Utc);
            Assert.Equal(managed.Rotation, orientation.Rotation);
            Assert.Equal(managed.AngularVelocity, orientation.AngularVelocity);
            Assert.Same(byName, orientation.ReferenceFrame);
        }
    }

    [Theory]
    [InlineData("CIRS", AberrationNone)]
    [InlineData("cirs", AberrationLt)]
    [InlineData("TIRS", AberrationNone)]
    [InlineData("tirs", AberrationLt)]
    [InlineData("Cirs", AberrationCnS)]
    [InlineData("Tirs", AberrationCnS)]
    public void EphemerisRead_ByName_IsTheReadInTheManagedFrame(string name, string aberrationName)
    {
        var aberration = Enum.Parse<Aberration>(aberrationName);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        double worst = 0.0;
        foreach (var epoch in Epochs)
        {
            var byName = new Frames.Frame(name);
            var read = moon.GetEphemeris(epoch.Utc, earth, byName, aberration);
            var inStatic = moon.GetEphemeris(epoch.Utc, earth, StaticFrameOf(name), aberration);
            var icrf = moon.GetEphemeris(epoch.Utc, earth, Frames.Frame.ICRF, aberration);

            // The read in the static frame, bit for bit, labelled with the frame asked
            Assert.Equal(inStatic.Position, read.Position);
            Assert.Equal(inStatic.Velocity, read.Velocity);
            Assert.Equal(byName, read.Frame);
            Assert.Equal(name, read.Frame.Name);

            // The ICRF read rotated by SOFA's GCRS -> frame
            worst = System.Math.Max(worst, AngleBetween(read.Position, Apply(SofaGcrsTo(name, epoch), icrf.Position)));

            // A state converted to the frame built by name, and back
            var converted = icrf.ToFrame(byName).ToStateVector();
            Assert.Equal(inStatic.Position, converted.Position);
            var back = converted.ToFrame(Frames.Frame.ICRF).ToStateVector();
            Assert.InRange((back.Position - icrf.Position).Magnitude(), 0.0, 1e-6);
        }

        _output.WriteLine($"MEASURE Moon from the Earth in {name} ({aberrationName}) against SOFA: {worst.ToString("E3", CultureInfo.InvariantCulture)} rad");
        Assert.InRange(worst, 0.0, SofaTolerance);
    }

    private const string AberrationNone = nameof(Aberration.None);
    private const string AberrationLt = nameof(Aberration.LT);
    private const string AberrationCnS = nameof(Aberration.CNS);

    [Theory]
    [MemberData(nameof(Names))]
    public void SpiceTransformFrame_ComposesTheManagedFrameThroughIcrf(string name)
    {
        var itrf93 = new Frames.Frame("ITRF93");
        double worst = 0.0, worstComposedAngle = 0.0, worstComposedRate = 0.0, worstIdentity = 0.0;
        foreach (var epoch in Epochs)
        {
            // Instance and name alike, to ICRF against SOFA
            foreach (var frame in new[] { new Frames.Frame(name), StaticFrameOf(name) })
            {
                var toIcrf = SpiceAPI.Instance.TransformFrame(epoch.Utc, frame, Frames.Frame.ICRF);
                worst = System.Math.Max(worst, WorstAxisAngle(toIcrf.Rotation, SofaGcrsTo(name, epoch)));
                Assert.Same(frame, toIcrf.ReferenceFrame);

                var fromIcrf = SpiceAPI.Instance.TransformFrame(epoch.Utc, Frames.Frame.ICRF, frame);
                worst = System.Math.Max(worst, WorstAxisAngle(fromIcrf.Rotation.Conjugate(), SofaGcrsTo(name, epoch)));

                // Toward a SPICE frame: the composition through ICRF of Frame.ToFrame, the angular velocity included. The same
                // formula on the same orientations (the managed frame's, and SPICE's ITRF93 -> ICRF, which the SPICE provider
                // reads through TransformFrame), so the same doubles on every platform: equality, not a bound
                var direct = SpiceAPI.Instance.TransformFrame(epoch.Utc, frame, itrf93);
                var composed = StaticFrameOf(name).ToFrame(itrf93, epoch.Utc);
                worstComposedAngle = System.Math.Max(worstComposedAngle, Angle(direct.Rotation, composed.Rotation));
                worstComposedRate = System.Math.Max(worstComposedRate, (direct.AngularVelocity - composed.AngularVelocity).Magnitude());
                Assert.Equal(composed.Rotation, direct.Rotation);
                Assert.Equal(composed.AngularVelocity, direct.AngularVelocity);

                // A frame to itself by name: q* q, whose vector part cancels exactly (each product appears twice, opposite)
                var identity = SpiceAPI.Instance.TransformFrame(epoch.Utc, frame, new Frames.Frame(name.ToUpperInvariant()));
                worstIdentity = System.Math.Max(worstIdentity, Angle(identity.Rotation, Quaternion.Identity));
                Assert.Equal(0.0, Angle(identity.Rotation, Quaternion.Identity));
            }
        }

        _output.WriteLine($"MEASURE TransformFrame {name} -> ICRF against SOFA: {worst.ToString("E3", CultureInfo.InvariantCulture)} rad");
        _output.WriteLine($"MEASURE TransformFrame {name} -> ITRF93 against ToFrame: {worstComposedAngle.ToString("E3", CultureInfo.InvariantCulture)} rad, "
                          + $"{worstComposedRate.ToString("E3", CultureInfo.InvariantCulture)} rad/s; to itself {worstIdentity.ToString("E3", CultureInfo.InvariantCulture)} rad");
        Assert.InRange(worst, 0.0, SofaTolerance);
    }

    [Fact]
    public void SpiceTransformFrame_CirsToTirs_IsTheEarthRotationAngle()
    {
        // CIRS -> TIRS is Rz(ERA) about the CIP: the angle between the two frames' x-axes is SOFA's ERA, by name and instance
        foreach (var epoch in Epochs)
        {
            var transform = SpiceAPI.Instance.TransformFrame(epoch.Utc, new Frames.Frame("cirs"), new Frames.Frame("tirs"));
            var sofaCirs = SofaGcrsTo("CIRS", epoch);
            var sofaTirs = SofaGcrsTo("TIRS", epoch);
            // CIRS -> TIRS = (GCRS -> TIRS) (GCRS -> CIRS)^T
            var x = Apply(sofaTirs, ApplyTranspose(sofaCirs, Vector3.VectorX));
            Assert.InRange(AngleBetween(Vector3.VectorX.Rotate(transform.Rotation), x), 0.0, SofaTolerance);
            // ToFrame's composition of the same two managed orientations: the same doubles
            var composed = Frames.Frame.CIRS.ToFrame(Frames.Frame.TIRS, epoch.Utc);
            Assert.Equal(composed.Rotation, transform.Rotation);
            Assert.Equal(composed.AngularVelocity, transform.AngularVelocity);
        }
    }
}
