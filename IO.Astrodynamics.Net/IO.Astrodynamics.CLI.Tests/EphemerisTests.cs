using System.Globalization;
using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Tests;

public class EphemerisTests
{
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
                    Begin =  new Time( new DateTime(2023, 01, 01, 1, 0, 0),TimeFrame.TDBFrame).ToString() ,
                    End = new Time( new DateTime(2023, 01, 01, 1, 1, 0),TimeFrame.TDBFrame).ToString()
                }, TimeSpan.FromMinutes(1), "ICRF", "LT", toKeplerian: true);
            var res = sb.ToString();

            OutputAssert.EqualApprox(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB A : 149548023692.8589, Ecc. : 0.016383768660595866, Inc. : 0.4090475386632512, AN : 6.283111166910646, AOP : 1.8230020944251075, M : 6.208211132464601, Frame : j2000{Environment.NewLine}Epoch : 2023-01-01T01:01:00.0000000 TDB A : 149548005605.26834, Ecc. : 0.016383648084550295, Inc. : 0.4090475387953283, AN : 6.283111165029856, AOP : 1.8230010654683242, M : 6.20822406354363, Frame : j2000{Environment.NewLine}"
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
                Begin =  new TimeSystem.Time( new DateTime(2023, 01, 01, 1, 0, 0),TimeFrame.TDBFrame).ToString() ,
                End = new TimeSystem.Time( new DateTime(2023, 01, 01, 1, 1, 0),TimeFrame.TDBFrame).ToString()
            }, TimeSpan.FromMinutes(1));
            var res = sb.ToString();
            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654 Velocity : X : -29812.532391293124 Y : -4864.249418137372 Z: -2109.60702632249 Frame : j2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 TDB Position : X : -25579051481.302353 Y : 132913028585.51353 Z: 57616880972.382614 Velocity : X : -29812.466803377927 Y : -4864.580889886812 Z: -2109.7507418164614 Frame : j2000{
                    Environment.NewLine}"
                , res);
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