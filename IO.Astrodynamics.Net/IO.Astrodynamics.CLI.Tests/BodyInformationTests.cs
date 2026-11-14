using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.Tests;

namespace IO.Astrodynamics.CLI.Tests;

public class BodyInformationsTests
{
    [Fact]
    public void CelestialBodyInformation()
    {
        using (Configuration.Lock())
        {
            var command = new BodyInformationCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.GetInformations("Data", 399);
            var res = sb.ToString();
            var earth = new Body.CelestialBody(399);

            // Every value parses back to its property (#681: Mass, GM and the radii were printed to 7 digits, GM under
            // "m^3.s^2"), and against the text of the kernels of these tests, read independently of CSPICE's pool: GM
            // within 1 ulp of gm_de431.tpc's BODY399_GM times 1e9 (1 ulp measured, CSPICE's parse), the radii exactly
            // pck00011.tpc's BODY399_RADII times 1000
            Assert.StartsWith($"{"Type :",32} Planet", res);
            Assert.All(res.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), line => Assert.Equal(" : ", line.Substring(30, 3)));
            Assert.Equal("399", TextKernelReference.PrintedValue(res, "Identifier :"));
            Assert.Equal("EARTH", TextKernelReference.PrintedValue(res, "Name :"));
            Assert.Equal("ITRF93", TextKernelReference.PrintedValue(res, "Fixed frame :"));
            Assert.Equal(earth.Mass, TextKernelReference.PrintedNumber(res, "Mass (kg) :"));
            var gm = TextKernelReference.PrintedNumber(res, "GM (m^3/s^2) :");
            Assert.Equal(earth.GM, gm);
            var gmText = TextKernelReference.Read("Data/SolarSystem/gm_de431.tpc", "BODY399_GM").Single() * 1e9;
            Assert.True(TextKernelReference.Ulps(gm, gmText) <= 1, $"GM {gm:R}, kernel text {gmText:R}");
            var radii = TextKernelReference.Read("Data/SolarSystem/pck00011.tpc", "BODY399_RADII");
            Assert.Equal(radii[0] * 1000.0, TextKernelReference.PrintedNumber(res, "Equatorial radius (m) :"));
            Assert.Equal(radii[2] * 1000.0, TextKernelReference.PrintedNumber(res, "Polar radius (m) :"));
            Assert.Equal(earth.EquatorialRadius, TextKernelReference.PrintedNumber(res, "Equatorial radius (m) :"));
            Assert.Equal(earth.PolarRadius, TextKernelReference.PrintedNumber(res, "Polar radius (m) :"));
            Assert.Equal(earth.Flattening, TextKernelReference.PrintedNumber(res, "Flattening :"));
            Assert.Equal(earth.J2, TextKernelReference.PrintedNumber(res, "J2 :"));
            Assert.Equal(earth.J3, TextKernelReference.PrintedNumber(res, "J3 :"));
            Assert.Equal(earth.J4, TextKernelReference.PrintedNumber(res, "J4 :"));
        }
    }

    /// <summary>
    /// CASSINI's identifier, a name built into SPICE, ended the process in the native layer (#560, exit code 134); it is
    /// now an argument error, raised before the native call (an identifier without a name, which returned an empty record
    /// and then an <see cref="System.InvalidOperationException"/>, raises the same argument error).
    /// </summary>
    [Fact]
    public void SpacecraftIdentifier_IsAnArgumentError()
    {
        using (Configuration.Lock())
        {
            var command = new BodyInformationCommand();
            Assert.Throws<ArgumentException>(() => { _ = command.GetInformations("Data", -82); });
        }
    }
}
