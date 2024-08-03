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
                    OrbitalParametersValues = "-26499033.67742509 132757417.33833946 57556718.47053819 -29.79426007 -5.01805231 -2.17539380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                }, true, false, false, new EpochParameters { Epoch = "0.0" }, "ECLIPJ2000");
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T12:00:00.0000000 (TDB) Position : X : -26499033.69125964 Y : 144697296.86606458 Z: -611.1493929959834 Velocity : X : -29.796555134442006 Y : -5.456766635259731 Z: 0.00018173387892583825 Frame : ECLIPJ2000{Environment.NewLine}",
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
                $"Epoch : 2000-01-01T12:00:00.0000000 (TDB) A : 149665479719.88266 Ecc. : 0.017121683001766336 Inc. : 0.4090876369492606 AN : 1.2954252300503235E-05 AOP : 1.776884894312699 M : 6.259056257481824 Frame : j2000{Environment.NewLine}",
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
                    $"Epoch : 2000-01-01T12:00:00.0000000 (TDB) P : 10170000 F : 0.3535533905932738 G : 0.3535533905932738 H : 0.08450755960720442 K 0.022643732351075387 L0 : 2.589226533382245 Frame : j2000{
                        Environment.NewLine}",
                    res);
            }
            else
            {
                Assert.Equal(
                    $"Epoch : 2000-01-01T12:00:00.0000000 (TDB) P : 10170000 F : 0.3535533905932738 G : 0.35355339059327373 H : 0.08450755960720442 K 0.022643732351075387 L0 : 2.589226533382245 Frame : j2000{Environment.NewLine}",
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
                    $"Epoch : 2000-01-01T12:00:00.0000000 (TDB) Position : X : -6116559.468933809 Y : -1546174.6944518015 Z: 2521950.161516388 Velocity : X : -807.8383114627195 Y : -5477.646280596453 Z: -5297.633402454895 Frame : j2000{Environment.NewLine}",
                    res);
            }
            else
            {
                Assert.Equal(
                    $"Epoch : 2000-01-01T12:00:00.0000000 (TDB) Position : X : -6116559.468933809 Y : -1546174.6944518015 Z: 2521950.161516389 Velocity : X : -807.8383114627195 Y : -5477.646280596454 Z: -5297.633402454895 Frame : j2000{Environment.NewLine}",
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
                $"Epoch : 2000-01-01T13:00:00.0000000 (TDB) A : 6800803.544958167 Ecc. : 0.001353139738203394 Inc. : 0.9026706683232326 AN : 0.5685593860871466 AOP : 1.85454203659022 M : 4.845168091487241 Frame : j2000{Environment.NewLine}",
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