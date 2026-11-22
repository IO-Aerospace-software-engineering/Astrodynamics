using System.Globalization;
using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.TimeSystem;
using Xunit.Abstractions;

namespace IO.Astrodynamics.CLI.Tests;

public class EphemerisTests
{
    private readonly ITestOutputHelper _output;

    public EphemerisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CallWithAllOptions()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Ephemeris("Data", 399, 10,
                new WindowParameters
                {
                    Begin =  new Time( new DateTime(2023, 01, 01, 1, 0, 0),TimeScale.TDB).ToString() ,
                    End = new Time( new DateTime(2023, 01, 01, 1, 1, 0),TimeScale.TDB).ToString()
                }, TimeSpan.FromMinutes(1), "ICRF", "LT", toKeplerian: true);
            var res = sb.ToString();

            OutputAssert.EqualApprox(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB A : 149548023692.8589, Ecc. : 0.016383768660595866, Inc. : 0.4090475386632512, AN : 6.283111166910646, AOP : 1.8230020944251075, M : 6.208211132464601, Frame : J2000{Environment.NewLine}Epoch : 2023-01-01T01:01:00.0000000 TDB A : 149548005605.26834, Ecc. : 0.016383648084550295, Inc. : 0.4090475387953283, AN : 6.283111165029856, AOP : 1.8230010654683242, M : 6.20822406354363, Frame : J2000{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void CallWithoutOptions()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Ephemeris("Data", 399, 10, new WindowParameters
            {
                Begin =  new TimeSystem.Time( new DateTime(2023, 01, 01, 1, 0, 0),TimeScale.TDB).ToString() ,
                End = new TimeSystem.Time( new DateTime(2023, 01, 01, 1, 1, 0),TimeScale.TDB).ToString()
            }, TimeSpan.FromMinutes(1));
            var res = sb.ToString();
            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654 Velocity : X : -29812.532391293124 Y : -4864.249418137372 Z: -2109.60702632249 Frame : J2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 TDB Position : X : -25579051481.302353 Y : 132913028585.51353 Z: 57616880972.382614 Velocity : X : -29812.466803377927 Y : -4864.580889886812 Z: -2109.7507418164614 Frame : J2000{
                    Environment.NewLine}"
                , res);
        }
    }

    [Theory]
    [InlineData("GCRF")]
    [InlineData("gcrf")]
    public void CallInGcrf(string frame)
    {
        // GCRF has the axes of J2000 (#543): the command builds a frame from the name, which reads SPICE in J2000. The
        // reference is the command in ICRF, SPICE's spkezr in J2000 (CallWithoutOptions); in GCRF the same states, labelled
        // with the frame given
        using (Configuration.Lock())
        {
            string Run(string name)
            {
                var command = new EphemerisCommand();
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);
                Console.SetOut(sw);
                command.Ephemeris("Data", 399, 10, new WindowParameters
                {
                    Begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB).ToString(),
                    End = new Time(new DateTime(2023, 01, 01, 1, 1, 0), TimeScale.TDB).ToString()
                }, TimeSpan.FromMinutes(1), name);
                return sb.ToString();
            }

            var icrf = Run("ICRF");
            Assert.Contains("Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654", icrf);
            Assert.Equal(icrf.Replace("Frame : J2000", $"Frame : {frame}"), Run(frame));
        }
    }

    [Theory]
    [InlineData("ICRF")]
    [InlineData("icrf")]
    [InlineData("J2000")]
    [InlineData("j2000")]
    public void IcrfIsLabelledJ2000_OnTheSpiceAndTheSitePaths(string frame)
    {
        // #657: one rule for the frame argument of every command (Helpers.ResolveFrame): ICRF or J2000, in any case, is
        // Frame.ICRF, whose name is J2000. Before, the command rewrote ICRF as j2000, which the SPICE path (a body observer)
        // printed as given and the site path as J2000; the numbers are the same
        using (Configuration.Lock())
        {
            foreach (var observer in new[] { 10, 399013 })
            {
                var command = new EphemerisCommand();
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);
                Console.SetOut(sw);
                command.Ephemeris("Data", observer == 10 ? 399 : 301, observer, new WindowParameters
                {
                    Begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB).ToString(),
                    End = new Time(new DateTime(2023, 01, 01, 1, 1, 0), TimeScale.TDB).ToString()
                }, TimeSpan.FromMinutes(1), frame);
                var lines = sb.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(2, lines.Length);
                Assert.All(lines, line => Assert.EndsWith("Frame : J2000", line));
            }

            var orientation = new OrientationCommand();
            StringBuilder osb = new StringBuilder();
            Console.SetOut(new StringWriter(osb));
            orientation.Orientation("Data", 399, new WindowParameters
            {
                Begin = new Time(2023, 01, 01, 1, 0, 0, TimeScale.TDB).ToString(),
                End = new Time(2023, 01, 01, 1, 1, 0, TimeScale.TDB).ToString()
            }, TimeSpan.FromMinutes(1), frame);
            Assert.All(osb.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
                line => Assert.EndsWith("Frame : J2000", line));
        }
    }

    [Fact]
    public void CallFromASite()
    {
        // A site is given by its NAIF identifier, here 399013, DSS-13 on the Earth; the expected states are SPICE's
        // spkezr("301", et, "J2000", "NONE", "399013") on the kernels of Data/SolarSystem (spiceypy, CSPICE N0067)
        // The states from a site observer are computed in ICRF and converted to the frame of the command, Frame.ICRF (#657):
        // the conversion is the identity and keeps the frame of the state, J2000, the label of the SPICE path too
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Ephemeris("Data", 301, 399013, new WindowParameters
            {
                Begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB).ToString(),
                End = new Time(new DateTime(2023, 01, 01, 1, 1, 0), TimeScale.TDB).ToString()
            }, TimeSpan.FromMinutes(1));
            var res = sb.ToString();

            OutputAssert.EqualApprox(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB Position : X : 318432518.66904956 Y : 201167633.45658574 Z: 78470616.8558369 Velocity : X : -525.6735216135344 Y : 374.6728331151655 Z: 423.04259903334116 Frame : J2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 TDB Position : X : 318401024.1503668 Y : 201190109.75510025 Z: 78495998.30144708 Velocity : X : -524.1437266303659 Y : 374.538327456151 Z: 423.00558627606205 Frame : J2000{
                    Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// An observer identifier from 100001 to 999998 is read as <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/>
    /// reads it (cli-site-rule of #668's second round): a site by the library's rule (<see cref="SpiceAPI.IsSiteId"/>, the
    /// rule of <c>Site.NaifId</c>) built from the kernels under its SPICE name, anything else (x000, a body of at most 100,
    /// a Lagrange point) a celestial item. Before, the CLI kept its own rule (100001 to 999998) and built the site
    /// <c>site&lt;id&gt;</c> of any of them, which failed otherwise: <c>ArgumentOutOfRangeException</c> from
    /// <c>Site(0, ...)</c> for 399000, "391 has no name" for 391013, "100 is not a celestial body" for 100500, and for
    /// 301001, a site of the Moon by the rule that the kernels do not name, no ephemeris data instead of the missing name.
    /// </summary>
    [Theory]
    [InlineData(391013)]
    [InlineData(399000)]
    [InlineData(100500)]
    [InlineData(301001)]
    [InlineData(395013)]
    public void Ephemeris_FromAnIdentifierThatIsNotASite_FailsAsTheLibrary(int observerId)
    {
        using (Configuration.Lock())
        {
            var begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB);
            var command = new EphemerisCommand();
            Console.SetOut(new StringWriter(new StringBuilder()));
            var fromCli = Record.Exception(() =>
            {
                _ = command.Ephemeris("Data", 301, observerId, new WindowParameters
                {
                    Begin = begin.ToString(),
                    End = begin.AddSeconds(60.0).ToString()
                }, TimeSpan.FromMinutes(1));
            });

            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var fromLibrary = Record.Exception(() => SpiceAPI.Instance.ReadEphemeris(begin, observerId, 301, Frames.Frame.ICRF, Aberration.None));

            Assert.Equal(observerId == 301001, SpiceAPI.IsSiteId(observerId));
            Assert.NotNull(fromLibrary);
            Assert.NotNull(fromCli);
            Assert.IsType(fromLibrary.GetType(), fromCli);
            Assert.Equal((fromLibrary as ArgumentException)?.ParamName, (fromCli as ArgumentException)?.ParamName);
            Assert.Equal(fromLibrary.Message, fromCli.Message);
        }
    }

    /// <summary>
    /// A site observer is named by its SPICE name, as <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/>
    /// names it (DSS-13 for 399013), and its states do not move: <see cref="CallFromASite"/>.
    /// </summary>
    [Fact]
    public void ASiteObserver_IsTheSiteOfTheLibrary()
    {
        using (Configuration.Lock())
        {
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB);

            // The observer the ephemeris command builds (Helpers.CreateLocalizable, internal to the CLI)
            var createLocalizable = typeof(EphemerisCommand).Assembly.GetType("IO.Astrodynamics.CLI.Helpers")!
                .GetMethod("CreateLocalizable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var site = Assert.IsType<Surface.Site>(createLocalizable.Invoke(null, [399013, begin]));
            var fromLibrary = SpiceAPI.Instance.ReadEphemeris(begin, 399013, 301, Frames.Frame.ICRF, Aberration.None);

            Assert.Equal("DSS-13", site.Name);
            Assert.Equal(399013, site.NaifId);
            var librarySite = Assert.IsType<Surface.Site>(fromLibrary.Observer);
            Assert.Equal(librarySite.Name, site.Name);
            Assert.Equal(librarySite.NaifId, site.NaifId);
        }
    }

    /// <summary>
    /// A site whose SPK is loaded without a kernel that names it (the SPK <c>Site.WriteEphemeris</c> writes, the name being
    /// in the frame kernel of <c>Site.WriteFrameAsync</c>) fails as <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/>
    /// fails, "has no name in SPICE" (cli-site-rule of #668's second round; before, the CLI built it as <c>site&lt;id&gt;</c>);
    /// with the kernel that names it loaded, its frame kernel, the CLI reads it under that name, the states of
    /// <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/>: the migration of the Breaking entry.
    /// </summary>
    [Fact]
    public async Task Ephemeris_FromASiteTheKernelsDoNotName_FailsAsTheLibrary_AndReadsOnceNamed()
    {
        using (Configuration.Lock())
        {
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var begin = Time.J2000TDB;
            var directory = Directory.CreateTempSubdirectory("clisite-");
            var spk = new FileInfo(Path.Combine(directory.FullName, "site777.bsp"));
            var frameKernel = new FileInfo(Path.Combine(directory.FullName, "site777.tf"));
            var site = new Surface.Site(777, "CLISITE", new Body.CelestialBody(399), new Coordinates.Planetodetic(0.1, 0.6, 100.0));
            site.WriteEphemeris(new Window(begin.AddDays(-1.0), begin.AddDays(1.0)), spk);
            await site.WriteFrameAsync(frameKernel);
            var window = new WindowParameters { Begin = begin.ToString(), End = begin.AddSeconds(60.0).ToString() };
            SpiceAPI.Instance.LoadKernels(spk);
            try
            {
                // Without the kernel that names it
                var command = new EphemerisCommand();
                Console.SetOut(new StringWriter(new StringBuilder()));
                var fromCli = Record.Exception(() => { _ = command.Ephemeris("Data", 301, 399777, window, TimeSpan.FromMinutes(1)); });
                var fromLibrary = Record.Exception(() => SpiceAPI.Instance.ReadEphemeris(begin, 399777, 301, Frames.Frame.ICRF, Aberration.None));
                Assert.NotNull(fromLibrary);
                Assert.NotNull(fromCli);
                Assert.IsType(fromLibrary.GetType(), fromCli);
                Assert.Equal(fromLibrary.Message, fromCli.Message);
                Assert.Contains("399777 has no name in SPICE", fromCli.Message);

                // With it
                SpiceAPI.Instance.LoadKernels(frameKernel);
                try
                {
                    var sb = new StringBuilder();
                    Console.SetOut(new StringWriter(sb));
                    await command.Ephemeris("Data", 301, 399777, window, TimeSpan.FromMinutes(1));
                    var expected = SpiceAPI.Instance.ReadEphemeris(begin, 399777, 301, Frames.Frame.ICRF, Aberration.None);
                    Assert.Equal("CLISITE", expected.Observer.Name);
                    // The command reads through GetEphemeris: the position of the library's read to its last digit (5.960e-8 m
                    // in X, 1.487e-16 relative, the same on Linux x64, Windows x64 and macOS arm64, CI run 43773937161,
                    // 2026-11-15); threshold twice that, rounded up at the second digit
                    var line = sb.ToString().Split(Environment.NewLine)[0];
                    Assert.StartsWith($"Epoch : {begin} Position : ", line);
                    var xyz = System.Text.RegularExpressions.Regex.Matches(line, @"(?<=X : |Y : |Z: )-?[0-9.E+-]+").Take(3)
                        .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                    var printed = new Math.Vector3(xyz[0], xyz[1], xyz[2]);
                    _output.WriteLine($"MEASURE CLI site ephemeris against SpiceAPI.ReadEphemeris: X {System.Math.Abs(printed.X - expected.Position.X):E3} m, " +
                                      $"relative {(printed - expected.Position).Magnitude() / expected.Position.Magnitude():E3}");
                    Assert.True((printed - expected.Position).Magnitude() < 3.0e-16 * expected.Position.Magnitude(),
                        $"{printed} against {expected.Position}");
                }
                finally
                {
                    SpiceAPI.Instance.UnloadKernels(frameKernel);
                }
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(spk);
                directory.Delete(true);
            }
        }
    }

    /// <summary>
    /// #750: NOTO (398989), SPICE's built-in ESA station whose identifier has the form of a site of 398, a body SPICE does
    /// not name, as observer of <c>ephemeris</c> and as object of <c>sub-point</c>, with an SPK of NOTO the test writes (a
    /// point fixed on the Earth, the states of a site built from its coordinates): the states of
    /// <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/> from NOTO, and the sub-point of the
    /// library's read of NOTO from the Earth in its body-fixed frame. Before the fix both built NOTO as a site of the body
    /// 398 and threw <see cref="ArgumentException"/> "The NAIF identifier 398 has no name in SPICE" (sub-point after its
    /// check of a site, "398989 is a site"), measured on f70ee32.
    /// </summary>
    [Fact]
    public async Task Ephemeris_FromNoto_AndItsSubPoint_AreTheLibrarysReads()
    {
        using (Configuration.Lock())
        {
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB);
            var directory = Directory.CreateTempSubdirectory("clinoto-");
            var spk = new FileInfo(Path.Combine(directory.FullName, "noto750.bsp"));
            var earth = new Body.CelestialBody(399);
            var site = new Surface.Site(989, "N750", earth, new Coordinates.Planetodetic(0.2616, 0.6436, 100.0));
            var states = site.GetEphemeris(new Window(begin.AddHours(-1.0), begin.AddHours(1.0)), earth, Frames.Frame.ICRF, Aberration.None,
                TimeSpan.FromSeconds(60.0)).Select(x => x.ToStateVector()).ToArray();
            SpiceAPI.Instance.WriteEphemeris(spk, 398989, states);
            SpiceAPI.Instance.LoadKernels(spk);
            try
            {
                var command = new EphemerisCommand();
                var sb = new StringBuilder();
                Console.SetOut(new StringWriter(sb));
                await command.Ephemeris("Data", 301, 398989, new WindowParameters { Begin = begin.ToString(), End = begin.AddSeconds(60.0).ToString() },
                    TimeSpan.FromMinutes(1));
                var expected = SpiceAPI.Instance.ReadEphemeris(begin, 398989, 301, Frames.Frame.ICRF, Aberration.None);
                Assert.Equal("NOTO", expected.Observer.Name);
                var line = sb.ToString().Split(Environment.NewLine)[0];
                Assert.StartsWith($"Epoch : {begin} Position : ", line);
                var xyz = System.Text.RegularExpressions.Regex.Matches(line, @"(?<=X : |Y : |Z: )-?[0-9.E+-]+").Take(3)
                    .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                var printed = new Math.Vector3(xyz[0], xyz[1], xyz[2]);
                _output.WriteLine($"MEASURE #750 CLI NOTO ephemeris against SpiceAPI.ReadEphemeris: {(printed - expected.Position).Magnitude():E3} m");
                // The printed digits of the state, its shortest round trip (the same read)
                Assert.Equal(expected.Position, printed);

                var subPoint = new StringBuilder();
                Console.SetOut(new StringWriter(subPoint));
                await command.SubPoint("Data", 398989, 399, new EpochParameters { Epoch = begin.ToString() });
                var fixedPosition = SpiceAPI.Instance.ReadEphemeris(begin, 399, 398989, earth.Frame, Aberration.None).Position;
                var planetocentric = new Coordinates.Planetocentric(System.Math.Atan2(fixedPosition.Y, fixedPosition.X),
                    System.Math.Asin(fixedPosition.Z / fixedPosition.Magnitude()), fixedPosition.Magnitude());
                Assert.Equal($"{planetocentric}{Environment.NewLine}", subPoint.ToString());
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(spk);
                directory.Delete(true);
            }
        }
    }

    /// <summary>
    /// sub-point takes an identifier that is not a site by the library's rule for a celestial item, as
    /// <see cref="CelestialItem.Create"/> does: it no longer calls 391013, 399000 or 100500 a site.
    /// </summary>
    [Theory]
    [InlineData(391013)]
    [InlineData(399000)]
    [InlineData(100500)]
    public void SubPoint_OfAnIdentifierThatIsNotASite_IsACelestialItem(int objectId)
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            Console.SetOut(new StringWriter(new StringBuilder()));
            var fromCli = Record.Exception(() => { _ = command.SubPoint("Data", objectId, 399, new EpochParameters { Epoch = "0.0" }); });

            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var fromLibrary = Record.Exception(() => Body.CelestialItem.Create(objectId));

            Assert.NotNull(fromLibrary);
            Assert.NotNull(fromCli);
            Assert.DoesNotContain("is a site", fromCli.Message);
            Assert.IsType(fromLibrary.GetType(), fromCli);
            Assert.Equal(fromLibrary.Message, fromCli.Message);
        }
    }

    [Fact]
    public void SubPoint()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.SubPoint("Data", 301, 399, new EpochParameters{Epoch = "0.0"});

            var res = sb.ToString();
            Assert.Equal($"Planetocentric {{ Longitude = -1.0078683256327345, Latitude = -0.19020700391621248, Radius = 402448639.8873273 }}{Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// A site's identifier (its body's times 1000 plus its own, 399013 DSS-13 on the Earth) names no sub-observer point:
    /// before #681 the site was cast to a celestial item, null, then dereferenced (NullReferenceException). It is an
    /// argument error naming the kinds sub-point accepts.
    /// </summary>
    [Fact]
    public void SubPoint_OfASite_IsAnArgumentError()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            var exception = Assert.Throws<ArgumentException>(() => { _ = command.SubPoint("Data", 399013, 399, new EpochParameters { Epoch = "0.0" }); });
            Assert.Contains("celestial body", exception.Message);
            Assert.Contains("spacecraft", exception.Message);
            Assert.Contains("399013", exception.Message);
        }
    }

    [Fact]
    public void SubPointCartesian()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.SubPoint("Data", 301, 399, new EpochParameters{Epoch = "0.0"}, cartesian: true);

            var res = sb.ToString();
            Assert.Equal($"X : 3341996.8388491296 Y : -5296048.281834691 Z: -1205719.1237167474{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void SubPointPlanetodetic()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.SubPoint("Data", 301, 399, new EpochParameters{Epoch = "0.0"}, planetodetic: true);

            // SPICE recgeo_c of the Moon relative to the Earth in ITRF93 at J2000 TDB, on the ellipsoid of pck00011
            // (tools/geodetic_reference/test_values_ref.c). Before #411: latitude −0.1914579069599901 rad and altitude
            // 396 166 994.97 m, the latitude relation of the surface applied 396 000 km above it
            var match = System.Text.RegularExpressions.Regex.Match(sb.ToString(),
                @"^Planetodetic \{ Longitude = (?<lon>[^,]+), Latitude = (?<lat>[^,]+), Altitude = (?<alt>[^ ]+) \}\r?\n$");
            Assert.True(match.Success, sb.ToString());
            Assert.Equal(-1.0078683256327345, double.Parse(match.Groups["lon"].Value, CultureInfo.InvariantCulture), 15);
            Assert.Equal(-0.19022670497171018, double.Parse(match.Groups["lat"].Value, CultureInfo.InvariantCulture), 15);
            Assert.Equal(396071266.51515061, double.Parse(match.Groups["alt"].Value, CultureInfo.InvariantCulture), 6);
        }
    }

    [Fact]
    public void AngularSeparation()
    {
        using (Configuration.Lock())
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.AngularSeparation("Data", 399, 301, 10, new EpochParameters{Epoch = "0.0"});

            var res = sb.ToString();
            Assert.Equal(0.9984998794278305, double.Parse(res),12);
        }
    }
}