using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class OrbitalParametersTests
{
    [Fact]
    public void SvToSv()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data",
                new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "-26499033677.42509 132757417338.33946 57556718470.53819 -29794.26007 -5018.05231 -2175.39380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                }, true, false, false, new EpochParameters { Epoch = "0.0" }, "ICRF");
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T12:00:00.0000000 TDB Position : X : -26499033676.707573 Y : 132757417338.4827 Z: 57556718470.538155 Velocity : X : -29794.260070027125 Y : -5018.052309838966 Z: -2175.393800000007 Frame : J2000{Environment.NewLine}",
                res);
        }
    }
    
    [Fact]
    public void SvToSvToDifferentFrame()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data",
                new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "-26499033677.42509 132757417338.33946 57556718470.53819 -29794.26007 -5018.05231 -2175.39380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                }, true, false, false, new EpochParameters { Epoch = "0.0" }, "ECLIPJ2000");
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T12:00:00.0000000 TDB Position : X : -26499033676.707577 Y : 144697296792.6746 Z: -611149.4830513 Velocity : X : -29794.260070027125 Y : -5469.294939597997 Z: 0.18178668879363613 Frame : ECLIPJ2000{Environment.NewLine}",
                res);
        }
    }

    [Fact]
    public void SvToKeplerian()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data",
                new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "-26499033677.42509 132757417338.33946 57556718470.53819 -29794.26007 -5018.05231 -2175.39380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                },
                false, true, false, new EpochParameters());
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T12:00:00.0000000 TDB A : 149665479719.88266 Ecc. : 0.017121683001766336 Inc. : 0.4090876369492606 AN : 1.2954252300503235E-05 AOP : 1.776884894312699 M : 6.259056257481824 Frame : J2000{Environment.NewLine}",
                res);
        }
    }

    [Fact]
    public void KeplerianToEquinocial()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "13560000.0 0.5 0.17453292519943295 0.26179938779914941 0.52359877559829882 0.78539816339744828", CenterOfMotionId = 399,
                OrbitalParametersEpoch = "0.0",
                Frame = "ICRF", FromStateVector = false, FromKeplerian = true, FromEquinoctial = false, FromTLE = false
            }, false, false, true, new EpochParameters());
            var res = sb.ToString();
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    $"Epoch : 2000-01-01T12:00:00.0000000 TDB P : 10170000 F : 0.3535533905932738 G : 0.3535533905932738 H : 0.08450755960720442 K 0.022643732351075387 L0 : 2.589226534411255 Frame : J2000{
                        Environment.NewLine}",
                    res);
            }
            else
            {
                Assert.Equal(
                    $"Epoch : 2000-01-01T12:00:00.0000000 TDB P : 10170000 F : 0.3535533905932738 G : 0.35355339059327373 H : 0.08450755960720442 K 0.022643732351075387 L0 : 2.589226533382245 Frame : J2000{Environment.NewLine}",
                    res);
            }
        }
    }

    [Fact]
    public void KeplerianToSv()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800803.5449581668 0.001353139738203394 0.90267066832323262 0.56855938608714662 1.8545420365902201 0.7925932793200029",
                CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0",
                Frame = "ICRF", FromStateVector = false, FromKeplerian = true, FromEquinoctial = false, FromTLE = false
            }, true, false, false, new EpochParameters());
            var res = sb.ToString();

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    $"Epoch : 2000-01-01T12:00:00.0000000 TDB Position : X : -6116559.469556896 Y : -1546174.69867672 Z: 2521950.1574303135 Velocity : X : -807.8383054672158 Y : -5477.646279080879 Z: -5297.633404926932 Frame : J2000{Environment.NewLine}",
                    res);
            }
            else
            {
                Assert.Equal(
                    $"Epoch : 2000-01-01T12:00:00.0000000 TDB Position : X : -6116559.469556896 Y : -1546174.69867672 Z: 2521950.1574303135 Velocity : X : -807.8383114627195 Y : -5477.646280596454 Z: -5297.633402454895 Frame : J2000{Environment.NewLine}",
                    res);
            }
        }
    }

    [Fact]
    public void KeplerianToKeplerian()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800803.5449581668 0.001353139738203394 0.90267066832323262 0.56855938608714662 1.8545420365902201 0.7925932793200029",
                CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF", FromStateVector = false, FromKeplerian = true, FromEquinoctial = false,
                FromTLE = false
            }, false, true, false, new EpochParameters{Epoch = "3600.0"});
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T13:00:00.0000000 TDB A : 6800803.544958167 Ecc. : 0.001353139738203394 Inc. : 0.9026706683232326 AN : 0.5685593860871466 AOP : 1.85454203659022 M : 4.845168091449668 Frame : J2000{Environment.NewLine}",
                res);
        }
    }

    [Fact]
    public void Exceptions()
    {
        lock (Configuration.objLock)
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            Assert.ThrowsAsync<ArgumentException>(() =>
                command.Converter("Data", new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0", Frame = "ICRF",
                    FromStateVector = true,
                    FromKeplerian = true, FromEquinoctial = false, FromTLE = false
                }, true, false, false, new EpochParameters())).Wait();
            Assert.ThrowsAsync<ArgumentException>(() =>
                command.Converter("Data", new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0", Frame = "ICRF",
                    FromStateVector = false,
                    FromKeplerian = false, FromEquinoctial = false, FromTLE = false
                }, true, false, false, new EpochParameters())).Wait();
            Assert.ThrowsAsync<ArgumentException>(() =>
                command.Converter("Data", new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF",
                    FromStateVector = true,
                    FromKeplerian = false, FromEquinoctial = false, FromTLE = false
                }, false, false, false, new EpochParameters())).Wait();
            Assert.ThrowsAsync<ArgumentException>(() =>
                command.Converter("Data", new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF",
                    FromStateVector = true,
                    FromKeplerian = false, FromEquinoctial = false, FromTLE = false
                }, true, true, false, new EpochParameters())).Wait();
        }
    }
}