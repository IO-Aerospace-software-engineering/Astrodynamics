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
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;

namespace IO.Astrodynamics.Tests.Frame;

/// <summary>
/// The managed TEME frame (#451) against GMAT R2026a's TEME → EarthICRF and ERFA's CIP route (IAU 2006/2000A, the
/// definition, and IAU 2006/2000B, the library's model), from <c>tools/teme_reference/teme_ref.py</c>
/// (<c>Data/TemeReference/teme_reference.json</c>): a LEO and a GEO state at 13 epochs from 1980 to 2026, the epochs of
/// the TEME cases of the conformance suite, Vallado's appendix C, and the SGP4 states of five TLEs at their epoch.
/// </summary>
/// <remarks>
/// The bounds are twice the worst value over Linux x64, Windows x64 and macOS arm64 (CI run 42490199772, 2026-11-08;
/// the three agree to the digits printed). Against GMAT the residual is GMAT's own: from 1997-02-27 its TEME →
/// EarthICRF carries the kinematic terms of the 1994 equation of the equinoxes (up to 2.6 mas), which Vallado's TEME
/// leaves out; against the 2000A route it is the IAU 2000B nutation (0.55 mas).
/// </remarks>
public class TemeFrameTests
{
    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (CI run 42490199772, 2026-11-08), positions in m,
    // velocities in m/s. Measured: GMAT 0.0936 m (LEO), 0.558 m (GEO), 9.39e-5 m/s; ERFA 2006/2000B 9.03e-4 m, 1.81e-2
    // m, 6.43e-6 m/s; ERFA 2006/2000A 1.89e-2 m, 0.106 m, 9.40e-6 m/s. The five TLEs at their epoch 0.371 m (TDRS 6),
    // 5.93e-5 m/s, their TEME states 9.3e-6 m from Vallado's code; at an instant 6.60e-2 m, 7.57e-5 m/s, TEME 3.65e-3 m
    // (the ISS of 2024 at a TDB instant, whose TT the library and CSPICE convert 1 us apart). Before #599 (tsince
    // counted in TDB seconds from the epoch the library converted): TEME 8.07e-3 m at the epoch, 0.520 m at an instant
    private const double GmatPositionLeo = 0.19, GmatPositionGeo = 1.2, GmatVelocity = 1.9e-4;
    private const double Cip2000BPositionLeo = 1.9e-3, Cip2000BPositionGeo = 3.7e-2, Cip2000BVelocity = 1.3e-5;
    private const double Cip2000APositionLeo = 3.8e-2, Cip2000APositionGeo = 0.22, Cip2000AVelocity = 1.9e-5;
    private const double TlePosition = 0.75, TleVelocity = 1.2e-4, TleTemePosition = 1.9e-5;
    private const double TleInstantPosition = 0.14, TleInstantVelocity = 1.6e-4, TleInstantTemePosition = 7.4e-3;

    private readonly ITestOutputHelper _output;

    public TemeFrameTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    public sealed record Point(string Kind, string Name, string Utc, string Instant, string Line1, string Line2, double[] Teme,
        double[] Gmat, double[] CipA, double[] CipB, double Gmst82Rad, double[] Dss63J2000);

    private static readonly Lazy<Point[]> Points = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText("Data/TemeReference/teme_reference.json"));
        static double[] Read(JsonElement e, string name) => e.GetProperty(name).EnumerateArray().Select(x => x.GetDouble()).ToArray();
        static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var p) ? p.GetString() : null;
        return document.RootElement.GetProperty("points").EnumerateArray().Select(e => new Point(Text(e, "kind"), Text(e, "name"),
            Text(e, "utc"), Text(e, "instant"), Text(e, "line1"), Text(e, "line2"), Read(e, "teme"), Read(e, "gmat"), Read(e, "cip_a"),
            Read(e, "cip_b"), e.GetProperty("gmst82_rad").GetDouble(),
            e.TryGetProperty("dss63_j2000", out _) ? Read(e, "dss63_j2000") : null)).ToArray();
    });

    private static IEnumerable<Point> FramePoints => Points.Value.Where(p => p.Kind == "frame");

    private static TimeSystem_Time Utc(string iso) => new(DateTime.ParseExact(iso, "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal), TimeFrame.UTCFrame);

    private static double Angle(Quaternion left, Quaternion right)
    {
        // atan2 keeps full precision for small angles
        var relative = left.Conjugate() * right;
        return 2.0 * System.Math.Atan2(relative.VectorPart.Magnitude(), System.Math.Abs(relative.W));
    }

    /// <summary>
    /// The reference points of tools/teme_reference, by name.
    /// </summary>
    internal static Point Reference(string name) => Points.Value.Single(p => p.Name == name);

    /// <summary>
    /// An instant of the reference, "yyyy-MM-dd HH:mm:ss.fff UTC" or "... TDB".
    /// </summary>
    internal static TimeSystem_Time Instant(string instant)
    {
        var dateTime = DateTime.ParseExact(instant[..23], "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return instant.EndsWith("TDB") ? new TimeSystem_Time(dateTime, TimeFrame.TDBFrame) : new TimeSystem_Time(dateTime, TimeFrame.UTCFrame);
    }

    internal static Vector3 Position(double[] s) => new Vector3(s[0], s[1], s[2]) * 1e3;
    internal static Vector3 Velocity(double[] s) => new Vector3(s[3], s[4], s[5]) * 1e3;

    private static StateVector ToIcrf(Point p, Frames.Frame teme)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        return new StateVector(Position(p.Teme), Velocity(p.Teme), earth, Utc(p.Utc), teme).ToFrame(Frames.Frame.ICRF).ToStateVector();
    }

    private (double positionLeo, double positionGeo, double velocity) Worst(Func<Point, double[]> reference, string label)
    {
        double leo = 0.0, geo = 0.0, velocity = 0.0;
        foreach (var p in FramePoints)
        {
            var icrf = ToIcrf(p, Frames.Frame.TEME);
            var r = reference(p);
            double dr = (icrf.Position - Position(r)).Magnitude();
            double dv = (icrf.Velocity - Velocity(r)).Magnitude();
            if (p.Name == "GEO") geo = System.Math.Max(geo, dr);
            else leo = System.Math.Max(leo, dr);
            velocity = System.Math.Max(velocity, dv);
            _output.WriteLine($"{label} {p.Utc} {p.Name}: {dr:E3} m, {dv:E3} m/s");
        }

        _output.WriteLine($"{label} worst: LEO {leo:E3} m, GEO {geo:E3} m, velocity {velocity:E3} m/s");
        return (leo, geo, velocity);
    }

    [Fact]
    public void TemeToIcrf_MatchesGmat()
    {
        var (leo, geo, velocity) = Worst(p => p.Gmat, "GMAT");
        Assert.InRange(leo, 0.0, GmatPositionLeo);
        Assert.InRange(geo, 0.0, GmatPositionGeo);
        Assert.InRange(velocity, 0.0, GmatVelocity);
    }

    [Fact]
    public void TemeToIcrf_MatchesErfaCipRouteWithIau2000B()
    {
        var (leo, geo, velocity) = Worst(p => p.CipB, "CIP 2006/2000B");
        Assert.InRange(leo, 0.0, Cip2000BPositionLeo);
        Assert.InRange(geo, 0.0, Cip2000BPositionGeo);
        Assert.InRange(velocity, 0.0, Cip2000BVelocity);
    }

    [Fact]
    public void TemeToIcrf_MatchesErfaCipRouteWithIau2000A()
    {
        var (leo, geo, velocity) = Worst(p => p.CipA, "CIP 2006/2000A");
        Assert.InRange(leo, 0.0, Cip2000APositionLeo);
        Assert.InRange(geo, 0.0, Cip2000APositionGeo);
        Assert.InRange(velocity, 0.0, Cip2000AVelocity);
    }

    [Fact]
    public void IcrfToTeme_InvertsGmat()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        double leo = 0.0, geo = 0.0, velocity = 0.0;
        foreach (var p in FramePoints)
        {
            var teme = new StateVector(Position(p.Gmat), Velocity(p.Gmat), earth, Utc(p.Utc), Frames.Frame.ICRF)
                .ToFrame(Frames.Frame.TEME).ToStateVector();
            Assert.Same(Frames.Frame.TEME, teme.Frame);
            double dr = (teme.Position - Position(p.Teme)).Magnitude();
            if (p.Name == "GEO") geo = System.Math.Max(geo, dr);
            else leo = System.Math.Max(leo, dr);
            velocity = System.Math.Max(velocity, (teme.Velocity - Velocity(p.Teme)).Magnitude());
        }

        _output.WriteLine($"ICRF -> TEME of GMAT's ICRF: LEO {leo:E3} m, GEO {geo:E3} m, velocity {velocity:E3} m/s");
        Assert.InRange(leo, 0.0, GmatPositionLeo);
        Assert.InRange(geo, 0.0, GmatPositionGeo);
        Assert.InRange(velocity, 0.0, GmatVelocity);
    }

    [Fact]
    public void Gmst82_MatchesErfa()
    {
        double worst = 0.0;
        foreach (var p in FramePoints)
        {
            var utc = Utc(p.Utc);
            double gmst = TemeFrame.Gmst82(TimeSystem_Time.JULIAN_J2000, utc.DaysFromJ2000());
            double difference = System.Math.Abs(System.Math.IEEERemainder(gmst - p.Gmst82Rad, IO.Astrodynamics.Constants._2PI));
            worst = System.Math.Max(worst, difference);
            Assert.InRange(gmst, 0.0, IO.Astrodynamics.Constants._2PI);
        }

        _output.WriteLine($"GMST82 - ERFA gmst82: {worst:E3} rad");
        // The double resolution of an angle of a few radians computed from 1e9 s of sidereal time: about 1e-12 rad
        Assert.InRange(worst, 0.0, 1e-11);
    }

    [Fact]
    public void TemeIsQuasiInertial()
    {
        // v' = R v: no angular velocity (Vallado's teme2eci, GMAT)
        var epoch = Utc("2021-03-20T11:58:50.814");
        Assert.Equal(Vector3.Zero, Frames.Frame.TEME.GetStateOrientationToICRF(epoch).AngularVelocity);
        Assert.Equal(Vector3.Zero, Frames.Frame.TEME.ToFrame(Frames.Frame.ICRF, epoch).AngularVelocity);
        Assert.IsType<TemeFrame>(Frames.Frame.TEME);
    }

    [Fact]
    public void Ut1MinusUtcCancels()
    {
        // UT1 enters GMST82 and the ERA alike: 0.9 s of UT1 - UTC moves the GEO state of 2012 by less than a millimetre
        var p = FramePoints.Single(x => x.Name == "GEO" && x.Utc.StartsWith("2012"));
        var withEop = new TemeFrame(new ConstantUt1(0.9));
        var shifted = ToIcrf(p, withEop);
        var nominal = ToIcrf(p, Frames.Frame.TEME);
        Assert.InRange((shifted.Position - nominal.Position).Magnitude(), 0.0, 1e-3);
        Assert.Same(Frames.Frame.TEME.Name, new TemeFrame(null).Name);
    }

    private sealed class ConstantUt1(double deltaUt1) : IEarthOrientationParameters
    {
        public double GetDeltaUT1(TimeSystem_Time utcEpoch) => deltaUt1;
        public double GetXp(TimeSystem_Time utcEpoch) => 0.0;
        public double GetYp(TimeSystem_Time utcEpoch) => 0.0;
    }

    [Fact]
    public void FrameByName_IsRoutedToTheManagedTeme()
    {
        // #562, the routing only: a TEME built by name takes the managed rotation through the SPICE data provider
        var epoch = Utc("2012-02-17T00:00:00.000");
        var byName = new Frames.Frame("TEME").ToFrame(Frames.Frame.ICRF, epoch).Rotation;
        var lowerCase = new Frames.Frame("teme").ToFrame(Frames.Frame.ICRF, epoch).Rotation;
        var managed = Frames.Frame.TEME.ToFrame(Frames.Frame.ICRF, epoch).Rotation;
        Assert.InRange(Angle(byName, managed), 0.0, 1e-15);
        Assert.InRange(Angle(lowerCase, managed), 0.0, 1e-15);
    }

    [Fact]
    public void FrameByName_IsRoutedByTheMemoryDataProvider()
    {
        // The TEME routing of an empty MemoryDataProvider, called directly (#631): the global provider is never swapped,
        // since an object another test class builds during a swap would keep the empty provider
        var provider = new MemoryDataProvider();
        var epoch = Utc("2012-02-17T00:00:00.000");
        var orientation = provider.FrameTransformationToICRF(epoch, new Frames.Frame("TEME"));
        Assert.InRange(Angle(orientation.Rotation, Frames.Frame.TEME.GetStateOrientationToICRF(epoch).Rotation), 0.0, 1e-15);
        Assert.Equal("TEME", orientation.ReferenceFrame.Name);
    }

    [Fact]
    public void SpiceTransformFrame_ComposesTemeThroughIcrf()
    {
        // TEME -> ITRF93: the managed TEME, then SPICE's ITRF93 with its angular velocity
        var epoch = Utc("2004-04-06T07:51:28.386");
        var itrf93 = new Frames.Frame("ITRF93");
        var direct = SpiceAPI.Instance.TransformFrame(epoch, Frames.Frame.TEME, itrf93);
        var composed = Frames.Frame.TEME.ToFrame(itrf93, epoch);
        Assert.InRange(Angle(direct.Rotation, composed.Rotation), 0.0, 1e-15);
        Assert.InRange((direct.AngularVelocity - composed.AngularVelocity).Magnitude(), 0.0, 1e-20);

        var inverse = SpiceAPI.Instance.TransformFrame(epoch, itrf93, new Frames.Frame("TEME"));
        Assert.InRange(Angle(inverse.Rotation * direct.Rotation, Quaternion.Identity), 0.0, 1e-15);

        var temeToTeme = SpiceAPI.Instance.TransformFrame(epoch, Frames.Frame.TEME, new Frames.Frame("TEME"));
        Assert.InRange(Angle(temeToTeme.Rotation, Quaternion.Identity), 0.0, 1e-15);
        Assert.Equal(Vector3.Zero, temeToTeme.AngularVelocity);
    }

    [Fact]
    public void SpiceTransformFrame_ThrowsOutsideTheCoverageOfTheOtherFrame()
    {
        // #454 on the TEME path: ITRF93 before the Earth's binary PCK (2000-01-01) gives no rotation
        var epoch = Utc("1990-01-01T00:00:00.000");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SpiceAPI.Instance.TransformFrame(epoch, Frames.Frame.TEME, new Frames.Frame("ITRF93")));
        Assert.Contains("ITRF93", ex.Message);
        // TEME itself reads no kernel: TEME <-> ICRF works there
        Assert.InRange(SpiceAPI.Instance.TransformFrame(epoch, Frames.Frame.TEME, Frames.Frame.ICRF).Rotation.Magnitude(), 1.0 - 1e-15, 1.0 + 1e-15);
    }

    [Fact]
    public void TleToIcrf_MatchesSgp4ThroughGmat()
    {
        // The SGP4 state of Vallado's code at the TLE epoch, or at an instant (the ISS 6 h later, the ISS of ScenarioTests 1.8 and
        // 2.8 days later; sgp4 2.27, WGS-72 old, AFSPC mode), through GMAT's
        // TEME -> EarthICRF, against the library's SGP4 (CSPICE evsgp4_c, the same model) and its managed TEME
        double position = 0.0, velocity = 0.0;
        foreach (var p in Points.Value.Where(x => x.Kind == "tle"))
        {
            var tle = new TLE(p.Name, p.Line1, p.Line2);
            var teme = p.Instant == null ? tle.ToOsculating() : tle.ToOsculating(Instant(p.Instant));
            Assert.Same(Frames.Frame.TEME, teme.Frame);
            var icrf = teme.ToFrame(Frames.Frame.ICRF).ToStateVector();
            double dr = (icrf.Position - Position(p.Gmat)).Magnitude();
            double dv = (icrf.Velocity - Velocity(p.Gmat)).Magnitude();
            double sgp4 = (teme.Position - Position(p.Teme)).Magnitude();
            _output.WriteLine($"{p.Name}: ICRF {dr:E3} m, {dv:E3} m/s; TEME - sgp4 {sgp4:E3} m");
            position = System.Math.Max(position, dr);
            velocity = System.Math.Max(velocity, dv);
            bool atEpoch = p.Instant == null;
            Assert.InRange(dr, 0.0, atEpoch ? TlePosition : TleInstantPosition);
            Assert.InRange(dv, 0.0, atEpoch ? TleVelocity : TleInstantVelocity);
            Assert.InRange(sgp4, 0.0, atEpoch ? TleTemePosition : TleInstantTemePosition);
        }

        _output.WriteLine($"TLE worst: {position:E3} m, {velocity:E3} m/s");
    }

    [Fact]
    public void EphemerisReadInManagedFrames_EqualsTheIcrfReadConverted()
    {
        // #558: a read in TEME (managed or by name), CIRS or TIRS is read in J2000 and rotated, as ToFrame
        var epoch = Utc("2021-03-20T11:58:50.814");
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var icrf = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.LT).ToStateVector();
        foreach (var frame in new[] { Frames.Frame.TEME, new Frames.Frame("TEME"), Frames.Frame.CIRS, Frames.Frame.TIRS })
        {
            var read = moon.GetEphemeris(epoch, earth, frame, Aberration.LT).ToStateVector();
            var converted = icrf.ToFrame(frame).ToStateVector();
            Assert.Same(frame, read.Frame);
            Assert.InRange((read.Position - converted.Position).Magnitude(), 0.0, 1e-6);
            Assert.InRange((read.Velocity - converted.Velocity).Magnitude(), 0.0, 1e-9);
        }

        // The SpiceAPI reads by NAIF identifiers, at an epoch and over a window
        var atEpoch = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.TEME, Aberration.None).ToStateVector();
        var expected = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.None).ToFrame(Frames.Frame.TEME).ToStateVector();
        Assert.InRange((atEpoch.Position - expected.Position).Magnitude(), 0.0, 1e-6);
        Assert.Same(Frames.Frame.TEME, atEpoch.Frame);

        var window = new Window(epoch, epoch + TimeSpan.FromHours(2.0));
        var windowed = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.CIRS, Aberration.None, TimeSpan.FromHours(1.0))
            .Select(x => x.ToStateVector()).ToArray();
        Assert.Equal(3, windowed.Length);
        foreach (var state in windowed)
        {
            var reference = SpiceAPI.Instance.ReadEphemeris(state.Epoch, 399, 301, Frames.Frame.ICRF, Aberration.None).ToFrame(Frames.Frame.CIRS).ToStateVector();
            Assert.InRange((state.Position - reference.Position).Magnitude(), 0.0, 1e-6);
            Assert.Same(Frames.Frame.CIRS, state.Frame);
        }
    }

    [Fact]
    public void EphemerisReadInManagedSpacecraftFrame_EqualsTheIcrfReadRotated()
    {
        // #682: the frame of a managed spacecraft, which only the library holds, reached spkezr_c by name
        // (SPICE(UNKNOWNFRAME)); with an attitude record it is read in J2000 and rotated by the record, as the frames of #558
        var epoch = new TimeSystem_Time(2024, 3, 1);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var orbit = new KeplerianElements(6800000.0, 0.0001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, earth, epoch, Frames.Frame.ICRF);
        var spacecraft = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-6825, "F07SC", 100.0, 10000.0,
            new IO.Astrodynamics.Body.Spacecraft.Clock("F07SC_CLK", 65536), orbit);
        var frame = spacecraft.Frame;

        // Its record empty, the frame is read by name, as before 11.0.0: no kernel defines F07SC_FRAME, so SPICE rejects it
        Assert.Contains("F07SC_FRAME", Assert.Throws<InvalidOperationException>(() => moon.GetEphemeris(epoch, earth, frame, Aberration.LT)).Message);
        Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, frame, Aberration.None));

        // An attitude other than the identity, given by hand
        frame.AddStateOrientationToICRF(new StateOrientation(new Quaternion(new Vector3(1.0, 2.0, 3.0).Normalize(), 0.7), Vector3.Zero, epoch, frame));
        var icrf = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.LT).ToStateVector();
        var read = moon.GetEphemeris(epoch, earth, frame, Aberration.LT).ToStateVector();
        var converted = icrf.ToFrame(frame).ToStateVector();
        Assert.Same(frame, read.Frame);
        Assert.InRange((read.Position - converted.Position).Magnitude(), 0.0, 1e-6);
        Assert.InRange((read.Velocity - converted.Velocity).Magnitude(), 0.0, 1e-9);

        var atEpoch = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, frame, Aberration.None).ToStateVector();
        var expected = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.None).ToFrame(frame).ToStateVector();
        Assert.Same(frame, atEpoch.Frame);
        Assert.InRange((atEpoch.Position - expected.Position).Magnitude(), 0.0, 1e-6);
        Assert.InRange((atEpoch.Velocity - expected.Velocity).Magnitude(), 0.0, 1e-9);

        var windowed = SpiceAPI.Instance.ReadEphemeris(new Window(epoch, epoch + TimeSpan.FromHours(2.0)), 399, 301, frame, Aberration.None,
            TimeSpan.FromHours(1.0)).Select(x => x.ToStateVector()).ToArray();
        Assert.Equal(3, windowed.Length);
        foreach (var state in windowed)
        {
            var reference = SpiceAPI.Instance.ReadEphemeris(state.Epoch, 399, 301, Frames.Frame.ICRF, Aberration.None).ToFrame(frame).ToStateVector();
            Assert.InRange((state.Position - reference.Position).Magnitude(), 0.0, 1e-6);
            Assert.Same(frame, state.Frame);
        }
    }

    // Provisional: twice the worst on Linux x64 (1.69e-16 rad, 2026-11-14) rounded up, until the CI of the three platforms
    // has printed its MEASURE lines. The rotation of a CK the library writes against the closed form of the attitude it holds
    private const double CkFrameReadAngle = 4.0e-16;

    // The angle between two vectors, resolved near zero (atan2 of the cross and dot products, not an arccosine)
    private static double AngleBetween(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    /// <summary>The attitude of the kernels of <see cref="ManagedSpacecraftFrameKernels"/>: 90° about Z, then 30° about X.</summary>
    private static Quaternion CkAttitude(int k) => k < 60
        ? new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.PI2)
        : new Quaternion(Vector3.VectorX, 30.0 * IO.Astrodynamics.Constants.Deg2Rad);

    /// <summary>
    /// A managed spacecraft whose attitude, two held attitudes of <see cref="CkAttitude"/> one minute apart over two hours,
    /// was given by hand, and its clock, frame and C-kernels written by the library and loaded; disposed, they are unloaded.
    /// </summary>
    private sealed class ManagedSpacecraftFrameKernels : IDisposable
    {
        private readonly DirectoryInfo _directory;
        private readonly FileInfo[] _kernels;

        public ManagedSpacecraftFrameKernels(int id, string name, TimeSystem_Time start)
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.22, 0.0), earth, start, Frames.Frame.ICRF);
            Writer = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(id, name, 1000.0, 3000.0, new IO.Astrodynamics.Body.Spacecraft.Clock(name + "_CLK", 65536), orbit);
            for (int k = 0; k <= 120; k++)
            {
                Writer.Frame.AddStateOrientationToICRF(new StateOrientation(CkAttitude(k), Vector3.Zero, start.AddSeconds(60.0 * k), Writer.Frame));
            }

            _directory = Directory.CreateTempSubdirectory("f07ck-");
            var clock = new FileInfo(Path.Combine(_directory.FullName, "f07ck.tsc"));
            var fk = new FileInfo(Path.Combine(_directory.FullName, "f07ck.tf"));
            var ck = new FileInfo(Path.Combine(_directory.FullName, "f07ck.bc"));
            Writer.Clock.WriteAsync(clock).GetAwaiter().GetResult();
            ((IO.Astrodynamics.Body.Spacecraft.SpacecraftFrame)Writer.Frame).WriteAsync(fk).GetAwaiter().GetResult();
            clock.Refresh();
            fk.Refresh();
            Assert.Empty(KernelShadowing.Find([clock, fk], KernelShadowing.SolarSystem));
            SpiceAPI.Instance.LoadKernels(clock);
            SpiceAPI.Instance.LoadKernels(fk);
            _kernels = [ck, fk, clock];
            Writer.WriteOrientation(ck);
            ck.Refresh();
            SpiceAPI.Instance.LoadKernels(ck);
        }

        public IO.Astrodynamics.Body.Spacecraft.Spacecraft Writer { get; }

        public void Dispose()
        {
            foreach (var kernel in _kernels)
            {
                kernel.Refresh();
                if (kernel.Exists)
                {
                    SpiceAPI.Instance.UnloadKernels(kernel);
                }
            }

            _directory.Delete(true);
        }
    }

    /// <summary>
    /// #682: a managed spacecraft whose attitude record is empty (never propagated, a new object of the id of a spacecraft
    /// whose kernels the library wrote) is read in its frame by name, as before 11.0.0: with the frame and C-kernels of that
    /// id loaded, the read is rotated by the attitude of the C-kernel, the closed form of the attitude written; not by the
    /// identity of an empty record. Once an attitude is recorded, the record is read.
    /// </summary>
    [Fact]
    public void EphemerisReadInManagedSpacecraftFrame_EmptyRecord_ReadsTheKernelsByName()
    {
        var start = new TimeSystem_Time(2024, 3, 1);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        using var kernels = new ManagedSpacecraftFrameKernels(-6826, "F07CK", start);
        var fresh = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-6826, "F07CK", 1000.0, 3000.0, new IO.Astrodynamics.Body.Spacecraft.Clock("F07CK_CLK", 65536),
            kernels.Writer.InitialOrbitalParameters);
        Assert.Null(fresh.Frame.GetLatestStateOrientationToICRF());

        double worst = 0.0;
        foreach (int k in new[] { 7, 30, 59, 61, 90, 119 })
        {
            // Inside a held attitude, 17 s after its sample
            var t = start.AddSeconds(60.0 * k + 17.0);
            var icrf = moon.GetEphemeris(t, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var expected = icrf.Position.Rotate(CkAttitude(k).Conjugate());
            foreach (var read in new[]
                     {
                         moon.GetEphemeris(t, earth, fresh.Frame, Aberration.None).ToStateVector(),
                         SpiceAPI.Instance.ReadEphemeris(t, 399, 301, fresh.Frame, Aberration.None).ToStateVector(),
                         moon.GetEphemeris(t, earth, kernels.Writer.Frame, Aberration.None).ToStateVector()
                     })
            {
                worst = System.Math.Max(worst, AngleBetween(read.Position, expected));
            }
        }

        _output.WriteLine($"MEASURE #682 managed spacecraft frame, empty record read through its kernels, against the attitude written: {worst:E3} rad");
        Assert.True(worst < CkFrameReadAngle, $"{worst:E3} rad");

        // An attitude recorded, the identity: the record is read, not the kernels
        fresh.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, start, fresh.Frame));
        var epoch = start.AddSeconds(600.0);
        var fromRecord = moon.GetEphemeris(epoch, earth, fresh.Frame, Aberration.None).ToStateVector();
        Assert.Same(fresh.Frame, fromRecord.Frame);
        Assert.InRange(AngleBetween(fromRecord.Position, moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position), 0.0, 1e-15);
    }

    /// <summary>
    /// #682: the read in the frame of a managed spacecraft, rotated by its attitude record, against CSPICE on the frame and
    /// C-kernels the library writes from that record: <c>spkezr_c</c> of the Moon from the Earth in the spacecraft's frame,
    /// the C-matrix of the kernel applied by SPICE, independent of the rotation by the record.
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public void EphemerisReadInManagedSpacecraftFrame_MatchesSpkezrOnTheKernelsWritten()
    {
        var start = new TimeSystem_Time(2024, 3, 1);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        using var kernels = new ManagedSpacecraftFrameKernels(-6827, "F07CKV", start);
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (int k in new[] { 0, 7, 30, 59, 61, 90, 119 })
        {
            var t = start.AddSeconds(60.0 * k + 17.0);
            var library = moon.GetEphemeris(t, earth, kernels.Writer.Frame, Aberration.None).ToStateVector();
            double et = t.ToTDB().TimeSpanFromJ2000().TotalSeconds;
            var st = IO.Astrodynamics.Tests.Body.CspiceDirect.Spkezr("MOON", et, "F07CKV_FRAME", "NONE", "EARTH");
            worstPosition = System.Math.Max(worstPosition, (library.Position - new Vector3(st[0], st[1], st[2]) * 1000.0).Magnitude());
            worstVelocity = System.Math.Max(worstVelocity, (library.Velocity - new Vector3(st[3], st[4], st[5]) * 1000.0).Magnitude());
        }

        _output.WriteLine($"MEASURE #682 managed spacecraft frame against spkezr on the kernels written: |dr| {worstPosition:E3} m, |dv| {worstVelocity:E3} m/s");
        Assert.True(worstPosition < CkFrameReadPosition, $"{worstPosition:E3} m");
        Assert.True(worstVelocity < CkFrameReadVelocity, $"{worstVelocity:E3} m/s");
    }

    // Twice the worst on Linux x64 (6.8e-8 m, 1.6e-13 m/s, 2026-11-14) rounded up; the test runs on Linux only (Validation,
    // CspiceDirect)
    private const double CkFrameReadPosition = 1.4e-7;
    private const double CkFrameReadVelocity = 3.3e-13;
}
