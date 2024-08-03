using System.Globalization;
using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class EphemerisTests
{
    [Fact]
    public void CallWithAllOptions()
    {
        lock (Configuration.objLock)
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Ephemeris("Data", 399, 10,
                new WindowParameters
                {
                    Begin = new DateTime(2023, 01, 01, 1, 0, 0).ToString(CultureInfo.InvariantCulture) ,
                    End = new DateTime(2023, 01, 01, 1, 1, 0).ToString(CultureInfo.InvariantCulture)
                }, TimeSpan.FromMinutes(1), "ICRF", "LT", toKeplerian: true);
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 (TDB) A : 149548023692.8589 Ecc. : 0.016383768660595866 Inc. : 0.4090475386632512 AN : 6.283111166910646 AOP : 1.8230020944251075 M : 6.208211132464601 Frame : j2000{Environment.NewLine}Epoch : 2023-01-01T01:01:00.0000000 (TDB) A : 149548005605.26834 Ecc. : 0.016383648084550295 Inc. : 0.4090475387953283 AN : 6.283111165029856 AOP : 1.8230010654683242 M : 6.20822406354363 Frame : j2000{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void CallWithoutOptions()
    {
        lock (Configuration.objLock)
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Ephemeris("Data", 399, 10, new WindowParameters
            {
                Begin =new DateTime(2023, 01, 01, 1, 0, 0).ToString(CultureInfo.InvariantCulture) ,
                End = new DateTime(2023, 01, 01, 1, 1, 0).ToString(CultureInfo.InvariantCulture)
            }, TimeSpan.FromMinutes(1));
            var res = sb.ToString();
            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 (TDB) Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654 Velocity : X : -29812.532391293124 Y : -4864.249418137372 Z: -2109.60702632249 Frame : j2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 (TDB) Position : X : -25579051481.302353 Y : 132913028585.51353 Z: 57616880972.382614 Velocity : X : -29812.466803377927 Y : -4864.580889886812 Z: -2109.7507418164614 Frame : j2000{
                    Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void SubPoint()
    {
        lock (Configuration.objLock)
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

    [Fact]
    public void SubPointCartesian()
    {
        lock (Configuration.objLock)
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
        lock (Configuration.objLock)
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.SubPoint("Data", 301, 399, new EpochParameters{Epoch = "0.0"}, planetodetic: true);

            var res = sb.ToString();
            Assert.Equal($"Planetodetic {{ Longitude = -1.0078683256327345, Latitude = -0.1914579069599901, Altitude = 396166994.9749323 }}{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void SubPointPlanetodeticFromSpacecraft()
    {
        lock (Configuration.objLock)
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.SubPoint("Data", -172, 399, new EpochParameters{Epoch = "676555200.0"}, planetodetic: true);

            var res = sb.ToString();
            Assert.Equal($"Planetodetic {{ Longitude = 1.7801173242050727, Latitude = 0.0020669681623675575, Altitude = 421863.5026230626 }}{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void AngularSeparation()
    {
        lock (Configuration.objLock)
        {
            var command = new EphemerisCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.AngularSeparation("Data", 399, 301, 10, new EpochParameters{Epoch = "0.0"});

            var res = sb.ToString();
            Assert.Equal($"0.9984998794278185{Environment.NewLine}", res);
        }
    }
}