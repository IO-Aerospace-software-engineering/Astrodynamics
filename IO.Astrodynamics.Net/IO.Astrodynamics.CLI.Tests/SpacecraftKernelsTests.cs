// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit.Abstractions;
using Frame = IO.Astrodynamics.Frames.Frame;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// The commands on a spacecraft given by its NAIF identifier read its kernels (#542): before, the CLI built a managed
/// spacecraft with no state and an empty attitude record, so <c>orientation</c> printed the identity whatever the CK and
/// <c>ephemeris</c>, <c>sub-point</c> and <c>angular-separation</c> threw a NullReferenceException. The kernels are those
/// the library writes for a spacecraft propagated 600 s in LEO with a nadir attitude (its clock, frame kernel, SPK and
/// CK); the references are CSPICE in the test process on those kernels: <c>sxform</c> from the frame to the spacecraft
/// frame (the rotation of <c>pxform</c>, the angular velocity of <c>xf2rav</c>), through the frame system, while the
/// command reads the CK by <c>ckgpav_c</c>, and <c>spkezr</c>. Rotations are compared as an angle, never as components.
/// </summary>
public class SpacecraftKernelsTests
{
    private const int SpacecraftId = -542;

    private const string SpacecraftName = "CLI542";

    // The frame kernel the library writes names the spacecraft frame after the spacecraft (FrameTemplate.tf)
    private const string SpacecraftFrame = SpacecraftName + "_FRAME";

    // Inside the Earth orientation kernel of Data (earth_latest_high_prec.bpc, to 2023-05-12), which ITRF93 needs
    private static readonly Time Start = new(2023, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

    private static readonly Time End = Start.AddSeconds(600.0);

    // The kernels written once for the class: they stay loaded, as every kernel the tests load
    private static string _kernels;

    private static readonly Regex NumberRx = new(@"-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?", RegexOptions.Compiled);

    private readonly ITestOutputHelper _output;

    public SpacecraftKernelsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>The directory of the clock, the frame kernel, the SPK and the CK the library writes for the spacecraft,
    /// propagated (Velocity-Verlet, 10 s outputs) with a nadir attitude. Called under <see cref="Configuration.Lock"/>.</summary>
    private static string Kernels()
    {
        if (_kernels != null)
        {
            return _kernels;
        }

        // The kernels the other tests load
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
        var earth = new CelestialBody(399);
        var sun = new CelestialBody(10);
        var moon = new CelestialBody(301);
        var orbit = new KeplerianElements(6778137.0, 0.001, 51.6 * Constants.Deg2Rad, 0.3, 0.5, 0.0, earth, Start, Frame.ICRF);
        var spacecraft = new Spacecraft(SpacecraftId, SpacecraftName, 1000.0, 3000.0, new Clock(SpacecraftName + "_CLK", 65536), orbit);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(new NadirAttitude(earth, new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, engine));
        spacecraft.Propagate(new Window(Start, End), [earth, sun, moon], false, false, TimeSpan.FromSeconds(10.0));

        // A relative path out of Data, which the other tests load: the native writers take a bounded file name
        const string directory = "Cli542";
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
        string stem = Path.Combine(directory, SpacecraftName.ToLowerInvariant());
        spacecraft.Clock.WriteAsync(new FileInfo(stem + ".tsc")).GetAwaiter().GetResult();
        // The CK writer encodes its epochs with the clock kernel
        SpiceAPI.Instance.LoadKernels(new FileInfo(stem + ".tsc"));
        ((SpacecraftFrame)spacecraft.Frame).WriteAsync(new FileInfo(stem + ".tf")).GetAwaiter().GetResult();
        spacecraft.WriteEphemeris(new FileInfo(stem + ".bsp"));
        spacecraft.WriteOrientation(new FileInfo(stem + ".bc"));
        // Loaded for the references read before a command (the commands load the directory again)
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(directory));
        _kernels = directory;
        return _kernels;
    }

    private static string Run(Action command)
    {
        var sb = new StringBuilder();
        Console.SetOut(new StringWriter(sb));
        command();
        return sb.ToString();
    }

    private static double[] Numbers(string text) =>
        NumberRx.Matches(text).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();

    private static string Between(string line, string from, string to)
    {
        int start = line.IndexOf(from, StringComparison.Ordinal) + from.Length;
        return line.Substring(start, line.IndexOf(to, start, StringComparison.Ordinal) - start);
    }

    // The angle of the rotation between two unit quaternions, whatever their signs
    private static double RotationAngle(in Quaternion a, in Quaternion b)
    {
        var delta = a * b.Conjugate();
        return 2.0 * System.Math.Atan2(delta.VectorPart.Magnitude(), System.Math.Abs(delta.W));
    }

    // Measured, the worst over the 91 epochs of each case, the same on Linux, Windows and macOS arm64 (run 41567621620,
    // 2026-11-03): 0 rad in J2000, ECLIPJ2000 and ITRF93, UTC window included (ckgpav_c and sxform read the same CK
    // segment at the same encoded SCLK), and 2.182e-18, 2.211e-18 and 2.177e-18 rad/s on rates of 1.1e-3 rad/s; before the
    // fix the identity, 2.1 rad or more off. The bounds are twice the worst rounded up at the second digit, the rotation
    // with a floor of two roundings of the quaternion, 2 ulp(1) = 4.44e-16 rad, 4.5e-16 rounded up.
    private const double RotationTolerance = 4.5e-16;

    private const double AngularVelocityTolerance = 4.5e-18;

    [Theory]
    [InlineData("ICRF", "J2000", false)]
    [InlineData("ICRF", "J2000", true)]
    [InlineData("ECLIPJ2000", "ECLIPJ2000", false)]
    [InlineData("ITRF93", "ITRF93", false)]
    public void Orientation_OfASpacecraft_IsItsCk(string frameArgument, string referenceFrame, bool utcWindow)
    {
        using (Configuration.Lock())
        {
            var kernels = Kernels();
            // 91 epochs between the CK's records (every 10 s), the UTC window the same instants
            var begin = Start.AddSeconds(3.25);
            var end = Start.AddSeconds(590.0);
            var window = new WindowParameters
            {
                Begin = utcWindow ? begin.ToUTC().ToString() : begin.ToString(),
                End = utcWindow ? end.ToUTC().ToString() : end.ToString()
            };

            var output = Run(() => new OrientationCommand().Orientation(kernels, SpacecraftId, window, TimeSpan.FromSeconds(6.5), frameArgument));

            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(91, lines.Length);
            double worstAngle = 0.0, worstRate = 0.0, smallestFromIdentity = double.MaxValue;
            for (int i = 0; i < lines.Length; i++)
            {
                // The epoch the command printed: the instant of the grid, within the rounding of a UTC window to the tick
                var epoch = new Time(Between(lines[i], "Epoch : ", " Orientation :"));
                Assert.True(System.Math.Abs((epoch - Start.AddSeconds(3.25 + 6.5 * i)).TotalSeconds) <= 1.0e-7, lines[i]);
                Assert.EndsWith($"Frame : {(frameArgument == "ICRF" ? "j2000" : frameArgument)}", lines[i]);
                var n = Numbers(Between(lines[i], "Orientation :", " Frame :"));
                Assert.Equal(7, n.Length);
                var rotation = new Quaternion(n[0], n[1], n[2], n[3]);
                var rate = new Vector3(n[4], n[5], n[6]);

                // pxform / xf2rav(sxform) from the frame to the spacecraft frame, through its frame kernel
                var reference = SpiceAPI.Instance.TransformFrame(epoch, new Frame(referenceFrame), new Frame(SpacecraftFrame));
                Assert.Equal(1.0, reference.Rotation.Magnitude(), 12);
                worstAngle = System.Math.Max(worstAngle, RotationAngle(rotation, reference.Rotation));
                worstRate = System.Math.Max(worstRate, (rate - reference.AngularVelocity).Magnitude());
                smallestFromIdentity = System.Math.Min(smallestFromIdentity, RotationAngle(rotation, Quaternion.Identity));
            }

            _output.WriteLine($"MEASURE #542 orientation from {referenceFrame}{(utcWindow ? " (UTC window)" : "")}: CLI - pxform {worstAngle:E3} rad, " +
                              $"angular velocity {worstRate:E3} rad/s, smallest angle from the identity {smallestFromIdentity:E3} rad");
            Assert.True(worstAngle <= RotationTolerance, $"{worstAngle:E3} rad");
            Assert.True(worstRate <= AngularVelocityTolerance, $"{worstRate:E3} rad/s");
            // What the CLI printed before: the identity
            Assert.True(smallestFromIdentity > 0.1, $"{smallestFromIdentity:E3} rad");
        }
    }

    [Fact]
    public void Orientation_OfASpacecraft_OutsideItsCk_Throws()
    {
        using (Configuration.Lock())
        {
            var kernels = Kernels();
            var window = new WindowParameters { Begin = Start.AddSeconds(-120.0).ToString(), End = Start.AddSeconds(-60.0).ToString() };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                Run(() => new OrientationCommand().Orientation(kernels, SpacecraftId, window, TimeSpan.FromSeconds(60.0))));
            Assert.Contains("No orientation found", exception.Message);
        }
    }

    /// <summary>The state of the spacecraft relative to the Earth from <c>spkezr</c>.</summary>
    private static StateVector Spkezr(Time epoch, string frame, Aberration aberration)
    {
        var state = SpiceAPI.Instance.ReadEphemeris(epoch, 399, SpacecraftId, new Frame(frame), aberration).ToStateVector();
        // The native read returns zeros on a SPICE error: the reference is the orbit, about 6 778 km from the Earth
        Assert.InRange(state.Position.Magnitude(), 6.7e6, 6.9e6);
        return state;
    }

    [Theory]
    [InlineData("ICRF", "J2000", "None")]
    [InlineData("ICRF", "J2000", "LT")]
    [InlineData("ITRF93", "ITRF93", "None")]
    public void Ephemeris_OfASpacecraft_IsItsSpk(string frameArgument, string frame, string aberration)
    {
        using (Configuration.Lock())
        {
            var kernels = Kernels();
            var window = new WindowParameters { Begin = Start.AddSeconds(5.0).ToString(), End = Start.AddSeconds(595.0).ToString() };

            var output = Run(() => new EphemerisCommand().Ephemeris(kernels, SpacecraftId, 399, window, TimeSpan.FromSeconds(30.0), frameArgument, aberration));

            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(20, lines.Length);
            double worst = 0.0;
            for (int i = 0; i < lines.Length; i++)
            {
                var epoch = Start.AddSeconds(5.0 + 30.0 * i);
                Assert.StartsWith($"Epoch : {epoch} Position : ", lines[i]);
                var n = Numbers(Between(lines[i], "Position :", " Frame :"));
                var expected = Spkezr(epoch, frame, Enum.Parse<Aberration>(aberration, true));
                worst = System.Math.Max(worst, (new Vector3(n[0], n[1], n[2]) - expected.Position).Magnitude() +
                                               (new Vector3(n[3], n[4], n[5]) - expected.Velocity).Magnitude());
            }

            _output.WriteLine($"MEASURE #542 ephemeris in {frame} {aberration}: CLI - spkezr {worst:E3}");
            // The same spkezr read, printed at full precision (round-trip doubles): measured 0 (Linux x64, 2026-11-03)
            Assert.Equal(0.0, worst);
        }
    }

    [Fact]
    public void Ephemeris_FromASpacecraft_IsItsSpk()
    {
        using (Configuration.Lock())
        {
            var kernels = Kernels();
            var window = new WindowParameters { Begin = Start.AddSeconds(5.0).ToString(), End = Start.AddSeconds(95.0).ToString() };

            var output = Run(() => new EphemerisCommand().Ephemeris(kernels, 399, SpacecraftId, window, TimeSpan.FromSeconds(30.0)));

            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                // The Earth from the spacecraft: the opposite of the spacecraft from the Earth, geometric
                var expected = Spkezr(Start.AddSeconds(5.0 + 30.0 * i), "J2000", Aberration.None);
                var n = Numbers(Between(lines[i], "Position :", " Frame :"));
                Assert.Equal(expected.Position.Inverse(), new Vector3(n[0], n[1], n[2]));
                Assert.Equal(expected.Velocity.Inverse(), new Vector3(n[3], n[4], n[5]));
            }
        }
    }

    [Fact]
    public void SubPoint_OfASpacecraft_IsAboveItsSpkPosition()
    {
        using (Configuration.Lock())
        {
            var kernels = Kernels();
            var epoch = Start.AddSeconds(301.5);

            var output = Run(() => new EphemerisCommand().SubPoint(kernels, SpacecraftId, 399, new EpochParameters { Epoch = epoch.ToString() }));

            // The closed form on spkezr in ITRF93, the Earth's frame
            var position = Spkezr(epoch, "ITRF93", Aberration.None).Position;
            var n = Numbers(output);
            Assert.Equal(3, n.Length);
            Assert.Equal(System.Math.Atan2(position.Y, position.X), n[0]);
            Assert.Equal(System.Math.Asin(position.Z / position.Magnitude()), n[1]);
            Assert.Equal(position.Magnitude(), n[2]);
        }
    }

    [Fact]
    public void AngularSeparation_WithASpacecraft_IsThatOfItsSpkPositions()
    {
        using (Configuration.Lock())
        {
            var kernels = Kernels();
            var epoch = Start.AddSeconds(301.5);
            var moonFromEarth = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frame.ICRF, Aberration.None).ToStateVector().Position;
            var spacecraftFromEarth = Spkezr(epoch, "J2000", Aberration.None).Position;

            // From the spacecraft, between the Earth and the Moon; from the Earth, between the spacecraft and the Moon
            var fromSpacecraft = double.Parse(Run(() => new EphemerisCommand().AngularSeparation(kernels, SpacecraftId, 399, 301,
                new EpochParameters { Epoch = epoch.ToString() })), CultureInfo.InvariantCulture);
            var fromEarth = double.Parse(Run(() => new EphemerisCommand().AngularSeparation(kernels, 399, SpacecraftId, 301,
                new EpochParameters { Epoch = epoch.ToString() })), CultureInfo.InvariantCulture);

            // The angle of the two directions, by the atan2 of their cross and dot products
            static double Angle(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);
            double expectedFromSpacecraft = Angle(spacecraftFromEarth.Inverse(), moonFromEarth - spacecraftFromEarth);
            double expectedFromEarth = Angle(spacecraftFromEarth, moonFromEarth);
            _output.WriteLine($"MEASURE #542 angular separation: from the spacecraft {fromSpacecraft - expectedFromSpacecraft:E3} rad, from the Earth {fromEarth - expectedFromEarth:E3} rad");
            // Measured 0 and 1.110e-16 rad on Linux and Windows, 0 and 0 rad on macOS arm64 (run 41567621620, 2026-11-03), the
            // library's Vector3.Angle against the atan2 form, angles of 2.48 and 0.65 rad; the bound is twice the worst
            // with a floor of one ulp of 2.48 rad, 4.44e-16 rad, 4.5e-16 rounded up (4.4e-16 would demand the same bits)
            Assert.True(System.Math.Abs(fromSpacecraft - expectedFromSpacecraft) <= 4.5e-16);
            Assert.True(System.Math.Abs(fromEarth - expectedFromEarth) <= 4.5e-16);
        }
    }
}
