using System.Globalization;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.Exceptions;

namespace IO.Astrodynamics.CLI.Tests;

/// <remarks>
/// The windows are those of the same searches in spiceypy, the window ends and the results each the nearest tick of its
/// ET (#625): <c>tools/seconds_to_ticks/cli_windows_reference.py</c>, whose truncated mode gives the strings held before.
/// </remarks>
public class GeometryFinderTests
{
    private static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string[] Distance(string relation, string begin = "2024-03-01T00:00:00", string end = "2024-03-21T00:00:00", string target = "301",
        params string[] options) =>
    [
        "find-windows-from-distance-constraint", "399", target, "-K", "Data", "--relation", relation, "--value", "400000000",
        "--begin", begin, "--end", end, "--step", "10d", .. options
    ];

    private static string[] Coordinate(string system, string coordinate, string relation, string value, string begin = "2024-03-01T00:00:00",
        string end = "2024-05-01T00:00:00", string step = "20d", params string[] options) =>
    [
        "find-windows-from-coordinate-constraint", "10", "399", "-K", "Data", "--system", system, "--coordinate", coordinate,
        "--relation", relation, "--value", value, "--begin", begin, "--end", end, "--step", step, .. options
    ];

    [Fact]
    public void CoordinateConstraint()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Coordinate("Rectangular", "Z", "=", "0.0", options: ["--frame", "ECLIPJ2000"]));
            Assert.Equal(
                $"From 2024-03-17T05:19:00.9354396 TDB to 2024-03-17T05:19:00.9354396 TDB - Length 00:00:00{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void DistanceConstraint()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Distance("="));
            Assert.Equal(
                $"From 2024-03-19T18:04:11.9364665 TDB to 2024-03-19T18:04:11.9364665 TDB - Length 00:00:00{Environment.NewLine}"
                , res);

            // The value in km and the objects by name: the same search
            Assert.Equal(res, CliRun.Ok(["find-windows-from-distance-constraint", "EARTH", "MOON", "-K", "Data", "--relation", "=", "--value", "400000km",
                "--begin", "2024-03-01T00:00:00", "--end", "2024-03-21T00:00:00", "--step", "10d"]));
        }
    }

    /// <summary>
    /// The window ends in the scale of the input (R29 of #668): a UTC window prints UTC ends, <c>--time-scale tdb</c> the
    /// TDB labels of the same instants; csv one row per window, its length in seconds.
    /// </summary>
    [Fact]
    public void Windows_AreInTheScaleOfTheInput_AndInCsv()
    {
        using (Configuration.Lock())
        {
            var utc = CliRun.Ok(Distance("<", "2024-03-01T00:00:00Z", "2024-03-21T00:00:00Z"));
            var tdb = CliRun.Ok(Distance("<", "2024-03-01T00:00:00Z", "2024-03-21T00:00:00Z", options: ["--time-scale", "tdb"]));
            var found = SpiceApi.Instance.FindWindowsOnDistanceConstraint(new TimeSystem.Window(TimeSystem.Time.Parse("2024-03-01T00:00:00Z"),
                TimeSystem.Time.Parse("2024-03-21T00:00:00Z")), 399, 301, RelationalOperator.LessThan, 400000000.0, Aberration.None, TimeSpan.FromDays(10)).Single();
            Assert.Equal($"{new TimeSystem.Window(found.StartDate.ToTdb(), found.EndDate.ToTdb())}{Environment.NewLine}", tdb);
            Assert.Equal($"{new TimeSystem.Window(found.StartDate.ToUtc(), found.EndDate.ToUtc())}{Environment.NewLine}", utc);
            Assert.StartsWith("From 2024-03-01T00:00:00.0000000Z to 2024-03-19T", utc);
            Assert.StartsWith("From 2024-03-01T00:01:09.1853864 TDB to 2024-03-19T18:04:11.9364660 TDB", tdb);

            var csv = CliRun.Ok(Distance("<", options: ["--format", "csv"])).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(new[] { "start,end,length_s", "2024-03-01T00:00:00.0000000 TDB,2024-03-19T18:04:11.9364667 TDB,1620251.9364667" }, csv);
        }
    }

    /// <summary>
    /// The operators CSPICE has not (#482), which ended the process: <c>&gt;=</c> and <c>&lt;=</c> searched as
    /// <c>&gt;</c> and <c>&lt;</c>, <c>!</c> as the search window less the instants of <c>=</c>. The windows are those of
    /// <c>gfdist</c> in spiceypy, <c>tools/geometry_reference/operators_reference.py</c> ("cli distance").
    /// </summary>
    [Theory]
    [InlineData(">=", "From 2024-03-19T18:04:11.9364667 TDB to 2024-03-21T00:00:00.0000000 TDB - Length 1.05:55:48.0635333")]
    [InlineData("<=", "From 2024-03-01T00:00:00.0000000 TDB to 2024-03-19T18:04:11.9364667 TDB - Length 18.18:04:11.9364667")]
    [InlineData("!", "From 2024-03-01T00:00:00.0000000 TDB to 2024-03-19T18:04:11.9364665 TDB - Length 18.18:04:11.9364665|From 2024-03-19T18:04:11.9364665 TDB to 2024-03-21T00:00:00.0000000 TDB - Length 1.05:55:48.0635335")]
    public void DistanceConstraint_OperatorsCspiceHasNot(string relation, string expected)
    {
        using (Configuration.Lock())
        {
            Assert.Equal(string.Join("", expected.Split('|').Select(x => x + Environment.NewLine)), CliRun.Ok(Distance(relation)));
        }
    }

    /// <summary>Every relation by its symbol or its member name, in any case.</summary>
    [Fact]
    public void Relations_ByNameOrSymbol()
    {
        using (Configuration.Lock())
        {
            Assert.Equal(CliRun.Ok(Distance("<=")), CliRun.Ok(Distance("lessthanorequal")));
            Assert.Equal(CliRun.Ok(Distance("=")), CliRun.Ok(Distance("Equal")));
            Assert.StartsWith("Error: Unknown --relation 'lt': the command reads >, <, =, !, >=, <=, ABSMIN, ABSMAX, LOCMIN, LOCMAX",
                CliRun.Fails(2, Distance("lt")));
        }
    }

    [Fact]
    public void OccultationConstraint()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok("find-windows-from-occultation-constraint", "399", "10", "301", "-K", "Data", "--target-shape", "Ellipsoid",
                "--front-shape", "Ellipsoid", "--occultation", "ANY", "--begin", "61473664.183390938", "--end", "61646464.183445148", "--step", "1h",
                "--aberration", "LT");
            Assert.Equal(
                $"From 2001-12-14T20:10:15.4105882 TDB to 2001-12-14T21:35:49.1005209 TDB - Length 01:25:33.6899327{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void FOVConstraint()
    {
        using (Configuration.Lock())
        {
            var expected =
                $"From 2021-06-10T00:00:00.0000000 TDB to 2021-06-10T00:29:05.3691494 TDB - Length 00:29:05.3691494{Environment.NewLine}From 2021-06-10T01:03:45.4719346 TDB to 2021-06-10T01:47:27.0000000 TDB - Length 00:43:41.5280654{Environment.NewLine}";
            Assert.Equal(expected, CliRun.Ok("find-windows-from-fov-constraint", "-172", "-172600", "399", "-K", "Data", "--target-frame", "ITRF93",
                "--target-shape", "Ellipsoid", "--begin", "676555200.0", "--end", "676561647.0", "--step", "1h", "--aberration", "LT"));
            // The spelling of the command before 11.0.0, which Cocona reads in any case
            Assert.Equal(expected, CliRun.Ok("find-windows-from-FOV-constraint", "-172", "-172600", "EARTH", "-K", "Data", "--target-frame", "ITRF93",
                "--target-shape", "ellipsoid", "--begin", "676555200.0", "--end", "676561647.0", "--step", "01:00:00", "--aberration", "lt"));
        }
    }

    private static string[] Illumination(string site, string value, params string[] options) =>
    [
        "find-windows-from-illumination-constraint", "10", "399", "10", "-K", "Data", "--fixed-frame", "ITRF93", "--site", site,
        "--angle", "Incidence", "--relation", "<", "--value", value, "--begin", "674524800.0", "--end", "674611200.0", "--step", "4.5h",
        "--aberration", "CNS", .. options
    ];

    /// <summary>
    /// An angular threshold outside the range of its angle (R11 of #668, lot L6) is an error of the input, exit code 2,
    /// naming <c>--value</c>: a declination of 100 rad, an incidence of 200 deg.
    /// </summary>
    [Fact]
    public void AngleThresholdOutOfRange_InputError()
    {
        using (Configuration.Lock())
        {
            var e = CliRun.Fails(2, Coordinate("RA/DEC", "DECLINATION", ">", "100"));
            Assert.StartsWith("Error: --value:", e);
            Assert.Contains("[−π/2, π/2]", e);
            e = CliRun.Fails(2, Illumination("0.0349 0.8378 0.0", "200deg"));
            Assert.StartsWith("Error: --value:", e);
            Assert.Contains("[0, π]", e);
        }
    }

    [Fact]
    public void IlluminationConstraint()
    {
        //Find time windows when the planetodetic point is illuminated by the sun (Official twilight 0.8° bellow horizon)
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Illumination("0.0349 0.8378 0.0", R(System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad))));
            Assert.Equal(
                $"From 2021-05-17T12:00:00.0000000 TDB to 2021-05-17T19:36:13.5596726 TDB - Length 07:36:13.5596726{Environment.NewLine}From 2021-05-18T04:19:19.9753258 TDB to 2021-05-18T12:00:00.0000000 TDB - Length 07:40:40.0246742{Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// A site west of Greenwich, a negative longitude given bare (R29 of #668: before, Cocona read "-0.0349 …" as an
    /// option), and its values in degrees and km: the search of the library with the same values in rad and m.
    /// </summary>
    [Fact]
    public void IlluminationConstraint_NegativeValuesAndUnits()
    {
        using (Configuration.Lock())
        {
            var value = System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad);
            var res = CliRun.Ok(Illumination("-0.0349 0.8378 1000", R(value)));
            var expected = SpiceApi.Instance.FindWindowsOnIlluminationConstraint(
                    new TimeSystem.Window(TimeSystem.Time.Create(674524800.0, TimeSystem.TimeScale.TDB), TimeSystem.Time.Create(674611200.0, TimeSystem.TimeScale.TDB)),
                    10, 399, new Frames.Frame("ITRF93"), new Coordinates.Planetodetic(-0.0349, 0.8378, 1000.0), IlluminationAngle.Incidence,
                    RelationalOperator.LessThan, value, 0.0, Aberration.CnS, TimeSpan.FromHours(4.5), 10)
                .Select(x => x + Environment.NewLine);
            Assert.Equal(string.Join("", expected), res);
            Assert.Equal(2, res.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);

            Assert.Equal(CliRun.Ok(Illumination("-2deg 48deg 1km", "90.8deg")), CliRun.Ok(Illumination(
                $"{R(-2.0 * IO.Astrodynamics.Constants.Deg2Rad)} {R(48.0 * IO.Astrodynamics.Constants.Deg2Rad)} 1000", R(90.8 * IO.Astrodynamics.Constants.Deg2Rad))));
        }
    }

    [Fact]
    public void IlluminationConstraint_AltitudeInMetres()
    {
        // #488: the point at 1071 m, read as 1071 km before (edges seconds off). The expected strings are gfilum at
        // georec(lon, lat, 1.071 km) replicated as the native layer runs it (tools/geometry_reference/kernel_search_reference.py),
        // 6.8 ms from those at 0 m above
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Illumination("0.0349 0.8378 1071.0", R(System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad))));
            Assert.Equal(
                $"From 2021-05-17T12:00:00.0000000 TDB to 2021-05-17T19:36:13.5664368 TDB - Length 07:36:13.5664368{Environment.NewLine}From 2021-05-18T04:19:19.9685371 TDB to 2021-05-18T12:00:00.0000000 TDB - Length 07:40:40.0314629{Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// A search SPICE cannot run ends the command with SPICE's error on one line, exit code 1, and prints no window (fourth
    /// round of #668; before, the native proxies dropped the error and the command printed nothing and succeeded): a body
    /// without ephemeris data (399999), a window past de440s (2160), an instrument whose kernel is not loaded
    /// (<c>INS-999123_FOV_FRAME</c>). A frame name neither the library nor SPICE knows is an input error of the command,
    /// exit code 2, checked once the kernels are loaded.
    /// </summary>
    [Fact]
    public void SearchesSpiceCannotRun_FailWithSpicesError()
    {
        using (Configuration.Lock())
        {
            var unknownFrame = CliRun.Fails(2, Coordinate("Rectangular", "Z", "=", "0.0", end: "2024-03-21T00:00:00", step: "10d", options: ["--frame", "NOT_A_FRAME_668"]));
            Assert.Equal($"Error: --frame: 'NOT_A_FRAME_668' is not a frame of the library or of the loaded kernels.{Environment.NewLine}", unknownFrame);

            var e = CliRun.Fails(1, Distance(">", target: "399999"));
            Assert.Contains("399999", e);

            e = CliRun.Fails(1, "find-windows-from-occultation-constraint", "399", "10", "301", "-K", "Data", "--target-shape", "Ellipsoid",
                "--front-shape", "Ellipsoid", "--occultation", "ANY", "--begin", "2160-03-01T00:00:00", "--end", "2160-03-02T00:00:00", "--step", "1h",
                "--aberration", "LT");
            Assert.Contains("Insufficient ephemeris data", e);
            Assert.Contains("2160", e);

            e = CliRun.Fails(1, "find-windows-from-fov-constraint", "-999", "-999123", "399", "-K", "Data", "--target-frame", "ITRF93",
                "--target-shape", "Ellipsoid", "--begin", "2024-03-01T00:00:00", "--end", "2024-03-21T00:00:00", "--step", "1h", "--aberration", "LT");
            Assert.Contains("INS-999123_FOV_FRAME", e);
            Assert.Single(e.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

            unknownFrame = CliRun.Fails(2, "find-windows-from-illumination-constraint", "10", "399", "10", "-K", "Data", "--fixed-frame", "NOT_A_FRAME_668",
                "--site", "0.0349 0.8378 0.0", "--angle", "Incidence", "--relation", "<", "--value", "1.5", "--begin", "2024-03-01T00:00:00",
                "--end", "2024-03-21T00:00:00", "--step", "4.5h");
            Assert.Contains("NOT_A_FRAME_668", unknownFrame);

            // With --verbose, the trace follows the line
            var verbose = CliRun.Fails(1, Distance(">", target: "399999", options: ["--verbose"]));
            Assert.Contains(typeof(SpiceException).FullName!, verbose);
        }
    }

    /// <summary>
    /// The help of <c>find-windows-from-coordinate-constraint</c> lists exactly the coordinate systems and coordinates of
    /// the library, by their descriptions (#652). The reference is the enumerations themselves.
    /// </summary>
    [Fact]
    public void CoordinateConstraint_HelpListsTheValuesOfTheEnumerations()
    {
        var parameters = typeof(GeometryFinderCommand).GetMethod(nameof(GeometryFinderCommand.CoordinateConstraint))!.GetParameters();

        Assert.Equal(Enum.GetValues<CoordinateSystem>().Select(x => x.GetDescription()), ListedValues(parameters, "system"));
        Assert.Equal(Enum.GetValues<Coordinate>().Select(x => x.GetDescription()), ListedValues(parameters, "coordinate"));
    }

    private static IEnumerable<string> ListedValues(System.Reflection.ParameterInfo[] parameters, string name)
    {
        var description = parameters.Single(x => x.Name == name).GetCustomAttributes(typeof(Cocona.OptionAttribute), false)
            .Cast<Cocona.OptionAttribute>().Single().Description!;
        var colon = description.IndexOf(':') + 1;
        var values = description.Substring(colon, description.IndexOf(", in any case", StringComparison.Ordinal) - colon);
        return values.Replace(" or ", ", ").Split(',').Select(x => x.Trim());
    }

    /// <summary>
    /// Every coordinate system and coordinate the help lists, by its description or its member name in any case, reads as
    /// its member (#652). The reference is the search SpiceApi runs with the members themselves: the Earth's right
    /// ascension and declination seen from the Sun in ICRF, which cross π and 0 at the March equinox of 2024.
    /// </summary>
    [Theory]
    [InlineData("RA/DEC", "RIGHT ASCENSION", 3.0)]
    [InlineData("ra/dec", "right ascension", 3.0)]
    [InlineData("RaDec", "RightAscension", 3.0)]
    [InlineData("RA/DEC", "DECLINATION", 0.0)]
    [InlineData("radec", "declination", 0.0)]
    public void CoordinateConstraint_ReadsDescriptionsAndMemberNames(string coordinateSystem, string coordinate, double value)
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Coordinate(coordinateSystem, coordinate, "=", R(value)));

            var member = coordinate.Contains("asc", StringComparison.OrdinalIgnoreCase) ? IO.Astrodynamics.Coordinate.RightAscension : IO.Astrodynamics.Coordinate.Declination;
            var expected = SpiceApi.Instance.FindWindowsOnCoordinateConstraint(
                    new TimeSystem.Window(new TimeSystem.Time(new DateTime(2024, 3, 1), TimeSystem.TimeScale.TDB), new TimeSystem.Time(new DateTime(2024, 5, 1), TimeSystem.TimeScale.TDB)), 10, 399,
                    Frames.Frame.ICRF, CoordinateSystem.RaDec, member, RelationalOperator.Equal, value, 0.0, Aberration.None, TimeSpan.FromDays(20))
                .ToArray();
            Assert.Single(expected);
            Assert.Equal(2024, expected[0].StartDate.DateTime.Year);
            Assert.Equal(3, expected[0].StartDate.DateTime.Month);
            Assert.Equal($"{expected[0]}{Environment.NewLine}", res);
        }
    }

    /// <summary>
    /// An unknown coordinate system or coordinate is an input error naming its option and the values the command reads,
    /// before any search (#652).
    /// </summary>
    [Theory]
    [InlineData("LATITUDINAL", "Z", "--system")]
    [InlineData("SPHERICAL", "Z", "--system")]
    [InlineData("CYLINDRICAL", "Z", "--system")]
    [InlineData("1", "Z", "--system")]
    [InlineData("RECTANGULAR", "COLATITUDE", "--coordinate")]
    [InlineData("RECTANGULAR", "", "--coordinate")]
    public void CoordinateConstraint_UnknownValue_FailsListingTheValues(string coordinateSystem, string coordinate, string option)
    {
        using (Configuration.Lock())
        {
            var e = CliRun.Fails(2, Coordinate(coordinateSystem, coordinate, "=", "0.0"));
            Assert.StartsWith($"Error: Unknown {option} '", e);
            var listed = option == "--system"
                ? Enum.GetValues<CoordinateSystem>().Select(x => x.GetDescription())
                : Enum.GetValues<IO.Astrodynamics.Coordinate>().Select(x => x.GetDescription());
            foreach (var value in listed)
            {
                Assert.Contains(value, e);
            }
        }
    }

    /// <summary>
    /// A coordinate the coordinate system has not is SPICE's error (<c>gfposc_c</c> signals it), exit code 1 (#652).
    /// </summary>
    [Theory]
    [InlineData("RECTANGULAR", "RADIUS")]
    [InlineData("RA/DEC", "LATITUDE")]
    [InlineData("GEODETIC", "RIGHT ASCENSION")]
    public void CoordinateConstraint_CoordinateOfAnotherSystem_FailsWithSpicesError(string coordinateSystem, string coordinate)
    {
        using (Configuration.Lock())
        {
            var e = CliRun.Fails(1, Coordinate(coordinateSystem, coordinate, "=", "0.0"));
            Assert.Contains(coordinate, e);
        }
    }

    /// <summary>
    /// Each coordinate system and coordinate of the help reads as its member, by description and by member name (#652):
    /// the error is then that of the next value read.
    /// </summary>
    [Fact]
    public void CoordinateConstraint_EveryListedValueIsRead()
    {
        using (Configuration.Lock())
        {
            foreach (var system in Enum.GetValues<CoordinateSystem>())
            {
                foreach (var spelling in new[] { system.GetDescription(), system.ToString() })
                {
                    Assert.StartsWith("Error: Unknown --coordinate", CliRun.Fails(2, Coordinate(spelling, "NOT_A_COORDINATE_652", "=", "0.0")));
                }
            }

            foreach (var coordinate in Enum.GetValues<IO.Astrodynamics.Coordinate>())
            {
                foreach (var spelling in new[] { coordinate.GetDescription(), coordinate.ToString() })
                {
                    Assert.StartsWith("Error: Unknown --relation", CliRun.Fails(2, Coordinate("RECTANGULAR", spelling, "NOT_AN_OPERATOR_652", "0.0")));
                }
            }
        }
    }

    /// <summary>The value of a coordinate takes the unit of its dimension: a length for Z, an angle for DECLINATION.</summary>
    [Fact]
    public void CoordinateConstraint_ValueUnitsFollowTheCoordinate()
    {
        using (Configuration.Lock())
        {
            Assert.Equal(CliRun.Ok(Coordinate("RA/DEC", "DECLINATION", "=", "0deg")), CliRun.Ok(Coordinate("RA/DEC", "DECLINATION", "=", "0")));
            Assert.Equal(CliRun.Ok(Coordinate("Rectangular", "Z", "=", "0km", options: ["--frame", "ECLIPJ2000"])),
                CliRun.Ok(Coordinate("Rectangular", "Z", "=", "0", options: ["--frame", "ECLIPJ2000"])));
            Assert.StartsWith("Error: --value: 'km' is not a unit of this value, in rad or deg", CliRun.Fails(2, Coordinate("RA/DEC", "DECLINATION", "=", "0km")));
            Assert.StartsWith("Error: --value: 'deg' is not a unit of this value, in m or km", CliRun.Fails(2, Coordinate("Rectangular", "Z", "=", "0deg")));
        }
    }
}
