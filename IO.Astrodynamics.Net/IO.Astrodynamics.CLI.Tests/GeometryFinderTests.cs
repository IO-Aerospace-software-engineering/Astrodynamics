using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class GeometryFinderTests
{
    [Fact]
    public void CoordinateConstraint()
    {
        lock (Configuration.objLock)
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
                $"From 2024-03-17T05:19:00.9354395 (TDB) to 2024-03-17T05:19:00.9354395 (TDB) - Length 00:00:00{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void DistanceConstraint()
    {
        lock (Configuration.objLock)
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
                $"From 2024-03-19T18:04:11.9364664 (TDB) to 2024-03-19T18:04:11.9364664 (TDB) - Length 00:00:00{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void OccultationConstraint()
    {
        lock (Configuration.objLock)
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
                $"From 2001-12-14T20:10:15.4105881 (TDB) to 2001-12-14T21:35:49.1005208 (TDB) - Length 01:25:33.6899327{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void FOVConstraint()
    {
        lock (Configuration.objLock)
        {
            var command = new GeometryFinderCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.InFieldOfViewConstraint("Data", -172, -172600, 399, "ITRF93", "Ellipsoid",
                new WindowParameters { Begin = "676555200.0" , End = "676561647.0"  }, TimeSpan.FromHours(1.0), "LT");
            var res = sb.ToString();

            Assert.Equal(
                $"From 2021-06-10T00:00:00.0000000 (TDB) to 2021-06-10T00:29:05.3691494 (TDB) - Length 00:29:05.3691494{Environment.NewLine}From 2021-06-10T01:03:45.4719345 (TDB) to 2021-06-10T01:47:27.0000000 (TDB) - Length 00:43:41.5280655{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void IlluminationConstraint()
    {
        //Find time windows when the planetodetic point is illuminated by the sun (Official twilight 0.8° bellow horizon)
        lock (Configuration.objLock)
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
                $"From 2021-05-17T12:00:00.0000000 (TDB) to 2021-05-17T19:36:13.5596725 (TDB) - Length 07:36:13.5596725{Environment.NewLine}From 2021-05-18T04:19:19.9753258 (TDB) to 2021-05-18T12:00:00.0000000 (TDB) - Length 07:40:40.0246742{Environment.NewLine}"
                , res);
        }
    }
}