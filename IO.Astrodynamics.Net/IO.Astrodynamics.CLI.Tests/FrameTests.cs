using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class FrameTests
{
    [Fact]
    public void Convert()
    {
        lock (Configuration.objLock)
        {
            var command = new FrameConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Convert("Data", "ICRF", "ITRF93", new EpochParameters { Epoch = "2000-01-01T12:00:00Z" });
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T12:01:04.1839999 (TDB) Orientation : Quaternion {{ W = 0.76863031609692, VectorPart = X : -1.861440644352336E-05 Y : 8.93941550765311E-07 Z: 0.6396932365043838 }} Angular velocity : X : -1.9637560682399114E-09 Y : -2.038938971500096E-09 Z: 7.292115064256361E-05 Frame : J2000{Environment.NewLine}"
                , res);
        }
    }
}