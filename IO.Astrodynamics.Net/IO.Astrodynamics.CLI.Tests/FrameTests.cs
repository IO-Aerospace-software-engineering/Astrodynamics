using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class FrameTests
{
    [Fact]
    public void Convert()
    {
        using (Configuration.Lock())
        {
            var command = new FrameConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Convert("Data", "ICRF", "ITRF93", new EpochParameters { Epoch = "2000-01-01T12:00:00Z" });
            var res = sb.ToString();

            // The TDB label of the UTC epoch takes the nearest tick of TDB − TT since #625 (.1839274 when it was truncated,
            // tools/seconds_to_ticks/time_conversion_reference.py): the quaternion is spiceypy's m2q of sxform(J2000, ITRF93)
            // at 64.1839273 s, digit for digit, the angular velocity its xf2rav within 2.3e-13 relative
            Assert.Equal(
                $"Epoch : 2000-01-01T12:01:04.1839273 TDB Orientation : Quaternion {{ W = 0.7686303144036278, VectorPart = X : -1.861440644864163E-05 Y : 8.939414975839561E-07 Z: 0.6396932385389773 }} Angular velocity : X : -1.963756068266585E-09 Y : -2.038938971497743E-09 Z: 7.292115064256362E-05 Frame : J2000{Environment.NewLine}"
                , res);
        }
    }

    [Theory]
    [InlineData("GCRF")]
    [InlineData("gcrf")]
    public void ConvertFromGcrf(string frame)
    {
        // GCRF has the axes of J2000 (#543): the conversion from a frame built from the name GCRF is that from ICRF, SPICE's
        // sxform from J2000 (Convert), labelled with the frame given
        using (Configuration.Lock())
        {
            string Run(string from)
            {
                var command = new FrameConverterCommand();
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);
                Console.SetOut(sw);
                command.Convert("Data", from, "ITRF93", new EpochParameters { Epoch = "2000-01-01T12:00:00Z" });
                return sb.ToString();
            }

            var icrf = Run("ICRF");
            Assert.Contains("W = 0.7686303144036278", icrf);
            Assert.Equal(icrf.Replace("Frame : J2000", $"Frame : {frame}"), Run(frame));
        }
    }
}