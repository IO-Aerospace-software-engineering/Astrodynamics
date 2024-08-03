using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.CLI.Tests;

public class TimeTests
{
    [Fact]
    public void DateToJDUTC()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters {Epoch = "1950-01-01T00:00:00Z"}, false, true, false, true, false, false);
            var res = sb.ToString();

            Assert.Equal($"2433282.5 JD UTC{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void DateToSecondsFromJ2000()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, false, false, false, true, false);
            var res = sb.ToString();

            Assert.Equal($"631152069.1839999 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void DateToDateTDB()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, false, false, false, false, true);
            var res = sb.ToString();

            Assert.Equal($"2020-01-01T12:01:09.1839999 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void J2000ToDateUTC()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "631152069.184 TDB"}, false, true, false, false, false, true);
            var res = sb.ToString();

            Assert.Equal($"2020-01-01T12:00:00.0000001Z UTC{Environment.NewLine}", res);
        }
    }

    // [Fact]
    internal void J2000ToLocal()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "631152069.184 TDB"}, false, false, true, false, false, true);
            var res = sb.ToString();

            Assert.Equal($"2020-01-01T13:00:00.0000001+01:00 {Environment.NewLine}", res);
        }
    }

    [Fact]
    public void LocalToJ2000()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T13:00:00.0000001+01:00"}, true, false, false, false, true, false);
            var res = sb.ToString();

            Assert.Equal($"631152069.184 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void J2000ToJulianTDB()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "0.0 TDB"}, true, false, false, true, false, false);
            var res = sb.ToString();

            Assert.Equal($"{DateTimeExtension.JULIAN_J2000} JD TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void J2000ToJ2000UTC()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = "0.0 TDB"}, false, true, false, false, true, false);
            var res = sb.ToString();

            Assert.Equal($"-64.1839999 UTC{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void JulianToJ2000TDB()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = $"{DateTimeExtension.JULIAN_J2000} JD TDB" }, true, false, false, false, true, false);
            var res = sb.ToString();

            Assert.Equal($"0 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void JulianToDateTDB()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = $"{DateTimeExtension.JULIAN_J2000} JD TDB" }, true, false, false, false, false, true);
            var res = sb.ToString();

            Assert.Equal($"2000-01-01T12:00:00.0000000 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void JulianToJulianUTC()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.TimeConverter(new EpochParameters{Epoch = $"{DateTimeExtension.JULIAN_J2000} JD TDB" }, false, true, false, true, false, false);
            var res = sb.ToString();

            Assert.Equal($"2451544.9992571296 JD UTC{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void Exceptions()
    {
        lock (Configuration.objLock)
        {
            var command = new TimeConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            Assert.ThrowsAsync<ArgumentException>(() => command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, false, false, false, true, false, true)).Wait();
            Assert.ThrowsAsync<ArgumentException>(() => command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, true, false, true, false, true)).Wait();
            Assert.ThrowsAsync<ArgumentException>(() => command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, false, false, false, false, false)).Wait();
            Assert.ThrowsAsync<ArgumentException>(() => command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, false, false, true, true, false)).Wait();
            Assert.ThrowsAsync<ArgumentException>(() => command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, false, false, true, false, true)).Wait();
            Assert.ThrowsAsync<ArgumentException>(() => command.TimeConverter(new EpochParameters{Epoch = "2020-01-01T12:00:00Z"}, true, false, false, false, true, true)).Wait();
        }
    }
}