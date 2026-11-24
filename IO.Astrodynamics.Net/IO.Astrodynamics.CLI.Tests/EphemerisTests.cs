using System.Globalization;
using IO.Astrodynamics.TimeSystem;
using Xunit.Abstractions;

namespace IO.Astrodynamics.CLI.Tests;

public class EphemerisTests
{
    private readonly ITestOutputHelper _output;

    private static readonly string Begin = new Time(new DateTime(2023, 01, 01, 1, 0, 0), TimeScale.TDB).ToString();
    private static readonly string End = new Time(new DateTime(2023, 01, 01, 1, 1, 0), TimeScale.TDB).ToString();

    public EphemerisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string[] Ephemeris(string target, string observer, params string[] options) =>
        ["ephemeris", target, observer, "-K", "Data", "--begin", Begin, "--end", End, "--step", "1min", .. options];

    [Fact]
    public void CallWithAllOptions()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Ephemeris("399", "10", "--frame", "ICRF", "--aberration", "LT", "--as", "keplerian"));

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
            var res = CliRun.Ok(Ephemeris("399", "10"));
            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654 Velocity : X : -29812.532391293124 Y : -4864.249418137372 Z: -2109.60702632249 Frame : J2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 TDB Position : X : -25579051481.302353 Y : 132913028585.51353 Z: 57616880972.382614 Velocity : X : -29812.466803377927 Y : -4864.580889886812 Z: -2109.7507418164614 Frame : J2000{
                    Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// Objects by name (choice 12 of #668, <c>bodn2c_c</c>): EARTH and SUN read as 399 and 10, in any case, the states bit
    /// for bit those of their identifiers; a name SPICE does not know is an input error.
    /// </summary>
    [Fact]
    public void ObjectsByName_AreTheirIdentifiers()
    {
        using (Configuration.Lock())
        {
            Assert.Equal(CliRun.Ok(Ephemeris("399", "10")), CliRun.Ok(Ephemeris("EARTH", "sun")));
            Assert.Equal(CliRun.Ok(Ephemeris("301", "399013")), CliRun.Ok(Ephemeris("Moon", "DSS-13")));

            var error = CliRun.Fails(2, Ephemeris("NOT A BODY", "10"));
            Assert.Equal($"Error: target: 'NOT A BODY' is neither a NAIF identifier nor a name SPICE knows, built in or given by the loaded kernels.{Environment.NewLine}", error);
        }
    }

    /// <summary>
    /// The aberration by its SPICE name (<c>LT+S</c>, which Enum.Parse refused before 11.0.0) or its member name, the state
    /// that of the library's read with that correction; an unknown correction lists the names.
    /// </summary>
    [Fact]
    public void Aberration_ByItsSpiceName()
    {
        using (Configuration.Lock())
        {
            var bySpiceName = CliRun.Ok(Ephemeris("301", "399", "--aberration", "lt+s"));
            Assert.Equal(bySpiceName, CliRun.Ok(Ephemeris("301", "399", "--aberration", "LTS")));
            Assert.NotEqual(bySpiceName, CliRun.Ok(Ephemeris("301", "399", "--aberration", "LT")));

            var error = CliRun.Fails(2, Ephemeris("301", "399", "--aberration", "LT+X"));
            Assert.StartsWith("Error: Unknown --aberration 'LT+X': the command reads NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN, XCN+S", error);
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
            var icrf = CliRun.Ok(Ephemeris("399", "10", "--frame", "ICRF"));
            Assert.Contains("Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654", icrf);
            Assert.Equal(icrf.Replace("Frame : J2000", $"Frame : {frame}"), CliRun.Ok(Ephemeris("399", "10", "--frame", frame)));
        }
    }

    [Fact]
    public void AnUnknownFrame_IsAnInputError()
    {
        using (Configuration.Lock())
        {
            var error = CliRun.Fails(2, Ephemeris("399", "10", "--frame", "NOT_A_FRAME"));
            Assert.Equal($"Error: --frame: 'NOT_A_FRAME' is not a frame of the library or of the loaded kernels.{Environment.NewLine}", error);
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
        // Frame.ICRF, whose name is J2000; the numbers are the same
        using (Configuration.Lock())
        {
            foreach (var observer in new[] { "10", "399013" })
            {
                var lines = CliRun.Ok(Ephemeris(observer == "10" ? "399" : "301", observer, "--frame", frame))
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(2, lines.Length);
                Assert.All(lines, line => Assert.EndsWith("Frame : J2000", line));
            }

            var orientation = CliRun.Ok("orientation", "399", "-K", "Data", "--begin", new Time(2023, 01, 01, 1, 0, 0, TimeScale.TDB).ToString(),
                "--end", new Time(2023, 01, 01, 1, 1, 0, TimeScale.TDB).ToString(), "--step", "00:01:00", "--frame", frame);
            Assert.All(orientation.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), line => Assert.EndsWith("Frame : J2000", line));
        }
    }

    [Fact]
    public void CallFromASite()
    {
        // A site is given by its NAIF identifier, here 399013, DSS-13 on the Earth; the expected states are SPICE's
        // spkezr("301", et, "J2000", "NONE", "399013") on the kernels of Data/SolarSystem (spiceypy, CSPICE N0067)
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Ephemeris("301", "399013"));

            OutputAssert.EqualApprox(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB Position : X : 318432518.66904956 Y : 201167633.45658574 Z: 78470616.8558369 Velocity : X : -525.6735216135344 Y : 374.6728331151655 Z: 423.04259903334116 Frame : J2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 TDB Position : X : 318401024.1503668 Y : 201190109.75510025 Z: 78495998.30144708 Velocity : X : -524.1437266303659 Y : 374.538327456151 Z: 423.00558627606205 Frame : J2000{
                    Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// The printed epochs are in the scale of the input (R29 of #668, decided by S. Guillet, 2026-11-19), <c>--time-scale</c>
    /// another: a UTC window prints UTC labels, read on the grid of the UTC window, and in TDB the same states labelled with
    /// the nearest TDB tick of each instant.
    /// </summary>
    [Fact]
    public void Epochs_AreInTheScaleOfTheInput_OrOfTimeScale()
    {
        using (Configuration.Lock())
        {
            string[] Utc(params string[] options) =>
                ["ephemeris", "301", "399", "-K", "Data", "--begin", "2023-01-01T01:00:00Z", "--end", "2023-01-01T01:01:00Z", "--step", "60s", .. options];

            var utc = CliRun.Ok(Utc()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("Epoch : 2023-01-01T01:00:00.0000000Z ", utc[0]);
            Assert.StartsWith("Epoch : 2023-01-01T01:01:00.0000000Z ", utc[1]);

            var tdb = CliRun.Ok(Utc("--time-scale", "TDB")).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var start = Time.Parse("2023-01-01T01:00:00Z").ToTDB();
            Assert.StartsWith($"Epoch : {start} ", tdb[0]);
            Assert.Equal(utc[0][utc[0].IndexOf(" Position", StringComparison.Ordinal)..], tdb[0][tdb[0].IndexOf(" Position", StringComparison.Ordinal)..]);

            Assert.StartsWith("Error: --time-scale: 'TCG' is not one of tdb, tt, tai, utc, gps.", CliRun.Fails(2, Utc("--time-scale", "TCG")));
        }
    }

    /// <summary>
    /// csv: a header of field names with their unit, then one row per state, every real in its shortest round-trip form,
    /// read back bit for bit; the elements of <c>--as</c> likewise.
    /// </summary>
    [Fact]
    public void Csv_ReadsBackBitForBit()
    {
        using (Configuration.Lock())
        {
            var csv = CliRun.Ok(Ephemeris("399", "10", "--format", "csv")).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("epoch,frame,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s", csv[0]);
            Assert.Equal(3, csv.Length);
            var row = csv[1].Split(',');
            Assert.Equal("2023-01-01T01:00:00.0000000 TDB", row[0]);
            Assert.Equal("J2000", row[1]);
            Assert.Equal(-25577262731.326492, double.Parse(row[2], CultureInfo.InvariantCulture));
            Assert.Equal(-2109.60702632249, double.Parse(row[7], CultureInfo.InvariantCulture));

            var keplerian = CliRun.Ok(Ephemeris("399", "10", "--format", "csv", "--as", "keplerian", "--aberration", "LT"))
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("epoch,frame,a_m,e,i_rad,raan_rad,aop_rad,m_rad", keplerian[0]);
            var text = CliRun.Ok(Ephemeris("399", "10", "--as", "keplerian", "--aberration", "LT")).Split(Environment.NewLine)[0];
            Assert.Contains($"A : {keplerian[1].Split(',')[2]},", text);

            Assert.Equal("epoch,frame,p_m,f,g,h,k,l0_rad",
                CliRun.Ok(Ephemeris("399", "10", "--format", "csv", "--as", "equinoctial")).Split(Environment.NewLine)[0]);
            Assert.StartsWith("Error: --format: this command writes text or csv, not json.", CliRun.Fails(2, Ephemeris("399", "10", "--format", "json")));
        }
    }

    /// <summary>
    /// An observer identifier from 100001 to 999998 is read as <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/>
    /// reads it (cli-site-rule of #668's second round): a site by the library's rule built from the kernels under its SPICE
    /// name, anything else a celestial item, its error the library's, exit code 1.
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
            var fromCli = CliRun.Fails(1, "ephemeris", "301", observerId.ToString(CultureInfo.InvariantCulture), "-K", "Data",
                "--begin", begin.ToString(), "--end", begin.AddSeconds(60.0).ToString(), "--step", "1min");

            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var fromLibrary = Record.Exception(() => SpiceAPI.Instance.ReadEphemeris(begin, observerId, 301, Frames.Frame.ICRF, Aberration.None));

            Assert.Equal(observerId == 301001, SpiceAPI.IsSiteId(observerId));
            Assert.NotNull(fromLibrary);
            Assert.Equal(CliRun.ErrorLine(fromLibrary), fromCli);
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

            var site = Assert.IsType<Surface.Site>(Helpers.CreateLocalizable(399013, begin));
            var fromLibrary = SpiceAPI.Instance.ReadEphemeris(begin, 399013, 301, Frames.Frame.ICRF, Aberration.None);

            Assert.Equal("DSS-13", site.Name);
            Assert.Equal(399013, site.NaifId);
            var librarySite = Assert.IsType<Surface.Site>(fromLibrary.Observer);
            Assert.Equal(librarySite.Name, site.Name);
            Assert.Equal(librarySite.NaifId, site.NaifId);
        }
    }

    /// <summary>
    /// A site whose SPK is loaded without a kernel that names it fails as
    /// <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/> fails, "has no name in SPICE"; with
    /// the kernel that names it loaded, its frame kernel, the CLI reads it under that name, by its identifier or its name.
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
            string[] Args(string observer) =>
                ["ephemeris", "301", observer, "-K", "Data", "--begin", begin.ToString(), "--end", begin.AddSeconds(60.0).ToString(), "--step", "1min"];
            SpiceAPI.Instance.LoadKernels(spk);
            try
            {
                // Without the kernel that names it
                var fromCli = CliRun.Fails(1, Args("399777"));
                var fromLibrary = Record.Exception(() => SpiceAPI.Instance.ReadEphemeris(begin, 399777, 301, Frames.Frame.ICRF, Aberration.None));
                Assert.NotNull(fromLibrary);
                Assert.Equal(CliRun.ErrorLine(fromLibrary), fromCli);
                Assert.Contains("399777 has no name in SPICE", fromCli);
                Assert.Contains("'CLISITE' is neither a NAIF identifier nor a name SPICE knows", CliRun.Fails(2, Args("CLISITE")));

                // With it, by its identifier and by its name
                SpiceAPI.Instance.LoadKernels(frameKernel);
                try
                {
                    var res = CliRun.Ok(Args("399777"));
                    Assert.Equal(res, CliRun.Ok(Args("clisite")));
                    var expected = SpiceAPI.Instance.ReadEphemeris(begin, 399777, 301, Frames.Frame.ICRF, Aberration.None);
                    Assert.Equal("CLISITE", expected.Observer.Name);
                    // The command reads through GetEphemeris: the position of the library's read to its last digit (5.960e-8 m
                    // in X, 1.487e-16 relative, the same on Linux x64, Windows x64 and macOS arm64, CI run 43773937161,
                    // 2026-11-15); threshold twice that, rounded up at the second digit
                    var line = res.Split(Environment.NewLine)[0];
                    Assert.StartsWith($"Epoch : {begin} Position : ", line);
                    var printed = Position(line);
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

    private static Math.Vector3 Position(string line)
    {
        var xyz = System.Text.RegularExpressions.Regex.Matches(line, @"(?<=X : |Y : |Z: )-?[0-9.E+-]+").Take(3)
            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
        return new Math.Vector3(xyz[0], xyz[1], xyz[2]);
    }

    /// <summary>
    /// #750: NOTO (398989), SPICE's built-in ESA station whose identifier has the form of a site of 398, a body SPICE does
    /// not name, as observer of <c>ephemeris</c> and as object of <c>sub-point</c>, by its identifier and by its name: the
    /// states of <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frames.Frame, Aberration)"/> from NOTO, and the
    /// sub-point of the library's read of NOTO from the Earth in its body-fixed frame.
    /// </summary>
    [Fact]
    public void Ephemeris_FromNoto_AndItsSubPoint_AreTheLibrarysReads()
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
                var res = CliRun.Ok("ephemeris", "301", "398989", "-K", "Data", "--begin", begin.ToString(), "--end", begin.AddSeconds(60.0).ToString(),
                    "--step", "1min");
                Assert.Equal(res, CliRun.Ok("ephemeris", "301", "NOTO", "-K", "Data", "--begin", begin.ToString(), "--end", begin.AddSeconds(60.0).ToString(),
                    "--step", "1min"));
                var expected = SpiceAPI.Instance.ReadEphemeris(begin, 398989, 301, Frames.Frame.ICRF, Aberration.None);
                Assert.Equal("NOTO", expected.Observer.Name);
                var line = res.Split(Environment.NewLine)[0];
                Assert.StartsWith($"Epoch : {begin} Position : ", line);
                var printed = Position(line);
                _output.WriteLine($"MEASURE #750 CLI NOTO ephemeris against SpiceAPI.ReadEphemeris: {(printed - expected.Position).Magnitude():E3} m");
                // The printed digits of the state, its shortest round trip (the same read)
                Assert.Equal(expected.Position, printed);

                var subPoint = CliRun.Ok("sub-point", "398989", "399", "-K", "Data", "--epoch", begin.ToString());
                var fixedPosition = SpiceAPI.Instance.ReadEphemeris(begin, 399, 398989, earth.Frame, Aberration.None).Position;
                var planetocentric = new Coordinates.Planetocentric(System.Math.Atan2(fixedPosition.Y, fixedPosition.X),
                    System.Math.Asin(fixedPosition.Z / fixedPosition.Magnitude()), fixedPosition.Magnitude());
                Assert.Equal($"{planetocentric}{Environment.NewLine}", subPoint);
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
    /// <see cref="Body.CelestialItem.Create"/> does: it does not call 391013, 399000 or 100500 a site, and fails as it.
    /// </summary>
    [Theory]
    [InlineData(391013)]
    [InlineData(399000)]
    [InlineData(100500)]
    public void SubPoint_OfAnIdentifierThatIsNotASite_IsACelestialItem(int objectId)
    {
        using (Configuration.Lock())
        {
            var fromCli = CliRun.Fails(1, "sub-point", objectId.ToString(CultureInfo.InvariantCulture), "399", "-K", "Data", "--epoch", "0.0");

            SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
            var fromLibrary = Record.Exception(() => Body.CelestialItem.Create(objectId));

            Assert.NotNull(fromLibrary);
            Assert.DoesNotContain("is a site", fromCli);
            Assert.Equal(CliRun.ErrorLine(fromLibrary), fromCli);
        }
    }

    [Fact]
    public void SubPoint()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok("sub-point", "301", "399", "-K", "Data", "--epoch", "0.0");
            Assert.Equal($"Planetocentric {{ Longitude = -1.0078683256327345, Latitude = -0.19020700391621248, Radius = 402448639.8873273 }}{Environment.NewLine}"
                , res);

            var json = System.Text.Json.JsonDocument.Parse(CliRun.Ok("sub-point", "MOON", "EARTH", "-K", "Data", "--epoch", "0.0", "--format", "json")).RootElement;
            Assert.Equal(-1.0078683256327345, json.GetProperty("longitude_rad").GetDouble());
            Assert.Equal(-0.19020700391621248, json.GetProperty("latitude_rad").GetDouble());
            Assert.Equal(402448639.8873273, json.GetProperty("radius_m").GetDouble());
            Assert.StartsWith("Error: --format: this command writes text or json, not csv.", CliRun.Fails(2, "sub-point", "301", "399", "-K", "Data", "--epoch", "0", "--format", "csv"));
        }
    }

    /// <summary>
    /// A site's identifier (its body's times 1000 plus its own, 399013 DSS-13 on the Earth) names no sub-observer point: an
    /// input error naming the kinds sub-point accepts (#681: before, a NullReferenceException).
    /// </summary>
    [Fact]
    public void SubPoint_OfASite_IsAnInputError()
    {
        using (Configuration.Lock())
        {
            var error = CliRun.Fails(2, "sub-point", "399013", "399", "-K", "Data", "--epoch", "0.0");
            Assert.Equal($"Error: object: sub-point projects a celestial body or a spacecraft; 399013 is a site.{Environment.NewLine}", error);
            Assert.Contains("DSS-13 is a site", CliRun.Fails(2, "sub-point", "DSS-13", "399", "-K", "Data", "--epoch", "0.0"));
        }
    }

    [Fact]
    public void SubPointCartesian()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok("sub-point", "301", "399", "-K", "Data", "--epoch", "0.0", "--as", "cartesian");
            Assert.Equal($"X : 3341996.8388491296 Y : -5296048.281834691 Z: -1205719.1237167474{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void SubPointPlanetodetic()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok("sub-point", "301", "399", "-K", "Data", "--epoch", "0.0", "--as", "planetodetic");

            // SPICE recgeo_c of the Moon relative to the Earth in ITRF93 at J2000 TDB, on the ellipsoid of pck00011
            // (tools/geodetic_reference/test_values_ref.c). Before #411: latitude −0.1914579069599901 rad and altitude
            // 396 166 994.97 m, the latitude relation of the surface applied 396 000 km above it
            var match = System.Text.RegularExpressions.Regex.Match(res,
                @"^Planetodetic \{ Longitude = (?<lon>[^,]+), Latitude = (?<lat>[^,]+), Altitude = (?<alt>[^ ]+) \}\r?\n$");
            Assert.True(match.Success, res);
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
            var res = CliRun.Ok("angular-separation", "399", "301", "10", "-K", "Data", "--epoch", "0.0");
            Assert.Equal(0.9984998794278305, double.Parse(res, CultureInfo.InvariantCulture), 12);

            var json = System.Text.Json.JsonDocument.Parse(CliRun.Ok("angular-separation", "EARTH", "MOON", "SUN", "-K", "Data", "--epoch", "0.0", "--format", "json"));
            Assert.Equal(double.Parse(res, CultureInfo.InvariantCulture), json.RootElement.GetProperty("angle_rad").GetDouble());
        }
    }
}
