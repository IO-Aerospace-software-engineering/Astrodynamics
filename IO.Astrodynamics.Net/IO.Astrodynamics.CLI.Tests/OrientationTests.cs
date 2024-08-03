using System.Globalization;
using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class OrientationsTests
{
    [Fact]
    public void CallWithAllOptions()
    {
        lock (Configuration.objLock)
        {
            var command = new OrientationCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Orientation("Data", 399,
                new WindowParameters
                {
                    Begin = new DateTime(2023, 01, 01, 1, 0, 0).ToString(CultureInfo.InvariantCulture),
                    End = new DateTime(2023, 01, 01, 1, 1, 0).ToString(CultureInfo.InvariantCulture)
                }, TimeSpan.FromMinutes(1), "ICRF");
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 (TDB) Orientation : Quaternion {{ W = 0.5384132956449944, VectorPart = X : -0.0009258429802486701 Y : -0.0006066138778422119 Z: -0.842680187204538 }} Angular velocity : X : 1.6147412630749635E-07 Y : 1.9022200070721644E-09 Z: 7.292097246566707E-05 Frame : j2000{Environment.NewLine}Epoch : 2023-01-01T01:01:00.0000000 (TDB) Orientation : Quaternion {{ W = 0.5365685324801104, VectorPart = X : -0.0009271700009545769 Y : -0.0006045864227802883 Z: -0.8438560213586128 }} Angular velocity : X : 1.6147414096478552E-07 Y : 1.9022120345744233E-09 Z: 7.292097246572818E-05 Frame : j2000{Environment.NewLine}"
                , res);
        }
    }

    [Fact]
    public void CallWithoutOptions()
    {
        lock (Configuration.objLock)
        {
            var command = new OrientationCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Orientation("Data", 399,
                new WindowParameters
                {
                    Begin = new DateTime(2023, 01, 01, 1, 0, 0).ToString(CultureInfo.InvariantCulture),
                    End = new DateTime(2023, 01, 01, 1, 1, 0).ToString(CultureInfo.InvariantCulture)
                }, TimeSpan.FromMinutes(1));
            var res = sb.ToString();
            Assert.Equal(
                $"Epoch : 2023-01-01T01:00:00.0000000 (TDB) Orientation : Quaternion {{ W = 0.5384132956449944, VectorPart = X : -0.0009258429802486701 Y : -0.0006066138778422119 Z: -0.842680187204538 }} Angular velocity : X : 1.6147412630749635E-07 Y : 1.9022200070721644E-09 Z: 7.292097246566707E-05 Frame : j2000{Environment.NewLine}Epoch : 2023-01-01T01:01:00.0000000 (TDB) Orientation : Quaternion {{ W = 0.5365685324801104, VectorPart = X : -0.0009271700009545769 Y : -0.0006045864227802883 Z: -0.8438560213586128 }} Angular velocity : X : 1.6147414096478552E-07 Y : 1.9022120345744233E-09 Z: 7.292097246572818E-05 Frame : j2000{Environment.NewLine}"
                , res);
        }
    }
}