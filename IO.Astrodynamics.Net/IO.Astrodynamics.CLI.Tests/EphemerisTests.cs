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
        lock (Configuration.objLock)
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

            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB A : 149547984744.6522 Ecc. : 0.016383513214941237 Inc. : 0.40904753866331567 AN : 6.283111166910646 AOP : 1.8230032812672177 M : 6.208209945338079 Frame : j2000{Environment.NewLine}Epoch : 2023-01-01T01:01:00.0000000 TDB A : 149547966664.0716 Ecc. : 0.01638339268455749 Inc. : 0.40904753879539246 AN : 6.283111165026861 AOP : 1.8230022519007245 M : 6.208222876826916 Frame : j2000{Environment.NewLine}"
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
                Begin =  new TimeSystem.Time( new DateTime(2023, 01, 01, 1, 0, 0),TimeFrame.TDBFrame).ToString() ,
                End = new TimeSystem.Time( new DateTime(2023, 01, 01, 1, 1, 0),TimeFrame.TDBFrame).ToString()
            }, TimeSpan.FromMinutes(1));
            var res = sb.ToString();
            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 TDB Position : X : -25577262731.326492 Y : 132913320450.42278 Z: 57617007553.115654 Velocity : X : -29812.532391293124 Y : -4864.249418137372 Z: -2109.6070263224897 Frame : j2000{
                    Environment.NewLine
                }Epoch : 2023-01-01T01:01:00.0000000 TDB Position : X : -25579051481.302357 Y : 132913028585.51353 Z: 57616880972.382614 Velocity : X : -29812.466803377923 Y : -4864.580889886811 Z: -2109.750741816462 Frame : j2000{
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
            Assert.Equal($"Planetocentric {{ Longitude = -1.007868325632746, Latitude = -0.1902070039162086, Radius = 402448639.8873211 }}{Environment.NewLine}"
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
            Assert.Equal($"X : 3341996.8388490714 Y : -5296048.281834733 Z: -1205719.123716723{Environment.NewLine}"
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
            Assert.Equal($"Planetodetic {{ Longitude = -1.007868325632746, Latitude = -0.19145790695998618, Altitude = 396166994.9749261 }}{Environment.NewLine}"
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
            Assert.Equal(0.9984998794278305, double.Parse(res),12);
        }
    }
}