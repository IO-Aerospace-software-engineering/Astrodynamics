using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

/// <remarks>
/// The windows are those of the same searches in spiceypy, the window ends and the results each the nearest tick of its
/// ET (#625): <c>tools/seconds_to_ticks/cli_windows_reference.py</c>, whose truncated mode gives the strings held before.
/// </remarks>
public class GeometryFinderTests
{
    [Fact]
    public void CoordinateConstraint()
    {
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.CoordinateConstraint("Data", 10, 399, "Rectangular", "Z", "=", 0.0,
                new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-05-01T00:00:00" },
                TimeSpan.FromDays(20), frame: "ECLIPJ2000");
            var res = sb.ToString();

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
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.DistanceConstraint("Data", 399, 301, "=", 400000000.0,
                new WindowParameters { Begin = "2024-03-01T00:00:00" , End = "2024-03-21T00:00:00" },
                TimeSpan.FromDays(10));
            var res = sb.ToString();

            Assert.Equal(
                $"From 2024-03-19T18:04:11.9364665 TDB to 2024-03-19T18:04:11.9364665 TDB - Length 00:00:00{Environment.NewLine}"
                , res);
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
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.DistanceConstraint("Data", 399, 301, relation, 400000000.0,
                new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-03-21T00:00:00" },
                TimeSpan.FromDays(10));
            var res = sb.ToString();

            Assert.Equal(string.Join("", expected.Split('|').Select(x => x + Environment.NewLine)), res);
        }
    }

    [Fact]
    public void OccultationConstraint()
    {
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.OccultationConstraint("Data", 399, 10, "Ellipsoid", 301, "Ellipsoid", "ANY",
                new WindowParameters { Begin = "61473664.183390938", End = "61646464.183445148" },
                TimeSpan.FromHours(1.0), "LT");
            var res = sb.ToString();

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
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.InFieldOfViewConstraint("Data", -172, -172600, 399, "ITRF93", "Ellipsoid",
                new WindowParameters { Begin = "676555200.0" , End = "676561647.0"  }, TimeSpan.FromHours(1.0), "LT");
            var res = sb.ToString();

            Assert.Equal(
                $"From 2021-06-10T00:00:00.0000000 TDB to 2021-06-10T00:29:05.3691494 TDB - Length 00:29:05.3691494{Environment.NewLine}From 2021-06-10T01:03:45.4719346 TDB to 2021-06-10T01:47:27.0000000 TDB - Length 00:43:41.5280654{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void IlluminationConstraint()
    {
        //Find time windows when the planetodetic point is illuminated by the sun (Official twilight 0.8° bellow horizon)
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.IlluminationConstraint("Data", 10, 399, 10, "ITRF93", new PlanetodeticParameters { Planetodetic = "0.0349 0.8378 0.0" }, "Incidence", "<",
                System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad),
                new WindowParameters { Begin = "674524800.0" , End = "674611200.0" }, TimeSpan.FromHours(4.5),
                "CNS");
            var res = sb.ToString();

            Assert.Equal(
                $"From 2021-05-17T12:00:00.0000000 TDB to 2021-05-17T19:36:13.5596726 TDB - Length 07:36:13.5596726{Environment.NewLine}From 2021-05-18T04:19:19.9753258 TDB to 2021-05-18T12:00:00.0000000 TDB - Length 07:40:40.0246742{Environment.NewLine}"
                , res);
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
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.IlluminationConstraint("Data", 10, 399, 10, "ITRF93", new PlanetodeticParameters { Planetodetic = "0.0349 0.8378 1071.0" }, "Incidence", "<",
                System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad),
                new WindowParameters { Begin = "674524800.0" , End = "674611200.0" }, TimeSpan.FromHours(4.5),
                "CNS");
            var res = sb.ToString();

            Assert.Equal(
                $"From 2021-05-17T12:00:00.0000000 TDB to 2021-05-17T19:36:13.5664368 TDB - Length 07:36:13.5664368{Environment.NewLine}From 2021-05-18T04:19:19.9685371 TDB to 2021-05-18T12:00:00.0000000 TDB - Length 07:40:40.0314629{Environment.NewLine}"
                , res);
        }
    }

    /// <summary>
    /// A search SPICE cannot run ends the command with SPICE's error, <c>InvalidOperationException</c>, and prints no window
    /// (fourth round of #668; before, the native proxies dropped the error and the command printed nothing and succeeded).
    /// The reference is SPICE's own error signal: a frame name it does not know, a body without ephemeris data (399999), a
    /// window past de440s (2160), an instrument whose kernel is not loaded (<c>INS-999123_FOV_FRAME</c>).
    /// </summary>
    [Fact]
    public void SearchesSpiceCannotRun_ThrowSpiceError()
    {
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            var window = new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-03-21T00:00:00" };
            var past = new WindowParameters { Begin = "2160-03-01T00:00:00", End = "2160-03-02T00:00:00" };

            var e = Assert.Throws<InvalidOperationException>(() => { _ = command.CoordinateConstraint("Data", 10, 399, "Rectangular", "Z", "=", 0.0, window,
                TimeSpan.FromDays(10), frame: "NOT_A_FRAME_668"); });
            Assert.Contains("NOT_A_FRAME_668", e.Message);
            Assert.Contains("not recognized", e.Message);

            e = Assert.Throws<InvalidOperationException>(() => { _ = command.DistanceConstraint("Data", 399, 399999, ">", 400000000.0, window, TimeSpan.FromDays(10)); });
            Assert.Contains("399999", e.Message);

            e = Assert.Throws<InvalidOperationException>(() => { _ = command.OccultationConstraint("Data", 399, 10, "Ellipsoid", 301, "Ellipsoid", "ANY", past,
                TimeSpan.FromHours(1.0), "LT"); });
            Assert.Contains("Insufficient ephemeris data", e.Message);
            Assert.Contains("2160", e.Message);

            e = Assert.Throws<InvalidOperationException>(() => { _ = command.InFieldOfViewConstraint("Data", -999, -999123, 399, "ITRF93", "Ellipsoid", window,
                TimeSpan.FromHours(1.0), "LT"); });
            Assert.Contains("INS-999123_FOV_FRAME", e.Message);

            e = Assert.Throws<InvalidOperationException>(() => { _ = command.IlluminationConstraint("Data", 10, 399, 10, "NOT_A_FRAME_668",
                new PlanetodeticParameters { Planetodetic = "0.0349 0.8378 0.0" }, "Incidence", "<", System.Math.PI * 0.5, window, TimeSpan.FromHours(4.5),
                "None"); });
            Assert.Contains("NOT_A_FRAME_668", e.Message);

            Assert.Equal(string.Empty, sb.ToString());
        }
    }

    /// <summary>
    /// The help of <c>find-windows-from-coordinate-constraint</c> lists exactly the coordinate systems and coordinates of
    /// the library, by their descriptions (#652: it listed LATITUDINAL, SPHERICAL, CYLINDRICAL and COLATITUDE, which the
    /// library has not). The reference is the enumerations themselves, <see cref="CoordinateSystem"/> and
    /// <see cref="Coordinate"/>.
    /// </summary>
    [Fact]
    public void CoordinateConstraint_HelpListsTheValuesOfTheEnumerations()
    {
        var parameters = typeof(GeometryFinderCommand).GetMethod(nameof(GeometryFinderCommand.CoordinateConstraint))!.GetParameters();

        Assert.Equal(Enum.GetValues<CoordinateSystem>().Select(x => x.GetDescription()), ListedValues(parameters, "coordinateSystem"));
        Assert.Equal(Enum.GetValues<Coordinate>().Select(x => x.GetDescription()), ListedValues(parameters, "coordinate"));
    }

    private static IEnumerable<string> ListedValues(System.Reflection.ParameterInfo[] parameters, string name)
    {
        var description = parameters.Single(x => x.Name == name).GetCustomAttributes(typeof(Cocona.ArgumentAttribute), false)
            .Cast<Cocona.ArgumentAttribute>().Single().Description!;
        var open = description.IndexOf('(');
        return description.Substring(open + 1, description.IndexOf(')', open) - open - 1).Split(',').Select(x => x.Trim());
    }

    /// <summary>
    /// Every coordinate system and coordinate the help lists, by its description or its member name in any case, reads as
    /// its member (#652: they were read by member name only, so RA/DEC and RIGHT ASCENSION, the values the help showed,
    /// were rejected). The reference is the search SpiceAPI runs with the members themselves: the Earth's right ascension
    /// and declination seen from the Sun in ICRF, which cross π and 0 at the March equinox of 2024.
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
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            var window = new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-05-01T00:00:00" };
            command.CoordinateConstraint("Data", 10, 399, coordinateSystem, coordinate, "=", value, window, TimeSpan.FromDays(20));

            var member = coordinate.Contains("asc", StringComparison.OrdinalIgnoreCase) ? Coordinate.RightAscension : Coordinate.Declination;
            var expected = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(
                    new TimeSystem.Window(new TimeSystem.Time(new DateTime(2024, 3, 1), TimeSystem.TimeFrame.TDBFrame), new TimeSystem.Time(new DateTime(2024, 5, 1), TimeSystem.TimeFrame.TDBFrame)), 10, 399,
                    Frames.Frame.ICRF, CoordinateSystem.RaDec, member, RelationnalOperator.Equal, value, 0.0, Aberration.None, TimeSpan.FromDays(20))
                .ToArray();
            Assert.Single(expected);
            Assert.Equal(2024, expected[0].StartDate.DateTime.Year);
            Assert.Equal(3, expected[0].StartDate.DateTime.Month);
            Assert.Equal($"{expected[0]}{Environment.NewLine}", sb.ToString());
        }
    }

    /// <summary>
    /// Every listed value, by its description and by its member name, is read; an unknown one ends the command with an
    /// <see cref="ArgumentException"/> naming its argument and the values the command reads, before any search (#652:
    /// LATITUDINAL, SPHERICAL, CYLINDRICAL and COLATITUDE, which the help listed, ended in <c>Enum.Parse</c>'s "Requested
    /// value was not found").
    /// </summary>
    [Theory]
    [InlineData("LATITUDINAL", "Z", "coordinateSystem")]
    [InlineData("SPHERICAL", "Z", "coordinateSystem")]
    [InlineData("CYLINDRICAL", "Z", "coordinateSystem")]
    [InlineData("1", "Z", "coordinateSystem")]
    [InlineData("RECTANGULAR", "COLATITUDE", "coordinate")]
    [InlineData("RECTANGULAR", "", "coordinate")]
    public void CoordinateConstraint_UnknownValue_ThrowsListingTheValues(string coordinateSystem, string coordinate, string parameter)
    {
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            var window = new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-05-01T00:00:00" };

            var e = Assert.Throws<ArgumentException>(() => { _ = command.CoordinateConstraint("Data", 10, 399, coordinateSystem, coordinate, "=", 0.0, window,
                TimeSpan.FromDays(20)); });
            Assert.Equal(parameter, e.ParamName);
            var listed = parameter == "coordinateSystem"
                ? Enum.GetValues<CoordinateSystem>().Select(x => x.GetDescription())
                : Enum.GetValues<Coordinate>().Select(x => x.GetDescription());
            foreach (var value in listed)
            {
                Assert.Contains(value, e.Message);
            }

            Assert.Equal(string.Empty, sb.ToString());
        }
    }

    /// <summary>
    /// A coordinate the coordinate system has not is SPICE's error (<c>gfposc_c</c> signals it), which ends the command
    /// with <see cref="InvalidOperationException"/>, as the help says (#652).
    /// </summary>
    [Theory]
    [InlineData("RECTANGULAR", "RADIUS")]
    [InlineData("RA/DEC", "LATITUDE")]
    [InlineData("GEODETIC", "RIGHT ASCENSION")]
    public void CoordinateConstraint_CoordinateOfAnotherSystem_ThrowsSpiceError(string coordinateSystem, string coordinate)
    {
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            Console.SetOut(new StringWriter(sb));
            var window = new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-05-01T00:00:00" };

            var e = Assert.Throws<InvalidOperationException>(() => { _ = command.CoordinateConstraint("Data", 10, 399, coordinateSystem, coordinate, "=", 0.0, window,
                TimeSpan.FromDays(20)); });
            Assert.Contains(coordinate, e.Message);
            Assert.Equal(string.Empty, sb.ToString());
        }
    }

    /// <summary>
    /// Each coordinate system and coordinate of the help reads as its member, by description and by member name (#652).
    /// </summary>
    [Fact]
    public void CoordinateConstraint_EveryListedValueIsRead()
    {
        using (Configuration.Lock())
        {
            var command = new GeometryFinderCommand();
            Console.SetOut(new StringWriter(new StringBuilder()));
            var window = new WindowParameters { Begin = "2024-03-01T00:00:00", End = "2024-03-02T00:00:00" };
            foreach (var system in Enum.GetValues<CoordinateSystem>())
            {
                foreach (var spelling in new[] { system.GetDescription(), system.ToString() })
                {
                    var e = Record.Exception(() => { _ = command.CoordinateConstraint("Data", 10, 399, spelling, "NOT_A_COORDINATE_652", "=", 0.0, window,
                        TimeSpan.FromDays(1)); });
                    Assert.Equal("coordinate", Assert.IsType<ArgumentException>(e).ParamName);
                }
            }

            foreach (var coordinate in Enum.GetValues<Coordinate>())
            {
                foreach (var spelling in new[] { coordinate.GetDescription(), coordinate.ToString() })
                {
                    var e = Record.Exception(() => { _ = command.CoordinateConstraint("Data", 10, 399, "RECTANGULAR", spelling, "NOT_AN_OPERATOR_652", 0.0, window,
                        TimeSpan.FromDays(1)); });
                    Assert.Equal("description", Assert.IsType<ArgumentException>(e).ParamName);
                }
            }
        }
    }
}
