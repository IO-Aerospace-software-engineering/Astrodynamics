using System.Text;
using IO.Astrodynamics.Tests;

namespace IO.Astrodynamics.CLI.Tests;

public class BodyInformationsTests
{
    [Fact]
    public void CelestialBodyInformation()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok("celestial-body-info", "399", "-K", "Data");
            Assert.Equal(res, CliRun.Ok("celestial-body-info", "Earth", "-K", "Data"));
            var earth = new Body.CelestialBody(399);

            // Every value parses back to its property (#681: Mass, GM and the radii were printed to 7 digits, GM under
            // "m^3.s^2"), and against the text of the kernels of these tests, read independently of CSPICE's pool: GM
            // within 1 ulp of gm_de431.tpc's BODY399_GM times 1e9 (1 ulp measured, CSPICE's parse), the radii exactly
            // pck00011.tpc's BODY399_RADII times 1000
            Assert.StartsWith($"{"Type :",32} Planet", res);
            Assert.All(res.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), line => Assert.Equal(" : ", line.Substring(30, 3)));
            Assert.Equal("399", ToStringLines.Value(res, "Identifier :"));
            Assert.Equal("EARTH", ToStringLines.Value(res, "Name :"));
            Assert.Equal("ITRF93", ToStringLines.Value(res, "Fixed frame :"));
            Assert.Equal(earth.Mass, ToStringLines.Number(res, "Mass (kg) :"));
            var gm = ToStringLines.Number(res, "GM (m^3/s^2) :");
            Assert.Equal(earth.GM, gm);
            var gmText = TextKernelReference.Read("Data/SolarSystem/gm_de431.tpc", "BODY399_GM").Single() * 1e9;
            Assert.True(System.Math.Abs(BitConverter.DoubleToInt64Bits(gm) - BitConverter.DoubleToInt64Bits(gmText)) <= 1, $"GM {gm:R}, kernel text {gmText:R}");
            var radii = TextKernelReference.Read("Data/SolarSystem/pck00011.tpc", "BODY399_RADII");
            Assert.Equal(radii[0] * 1000.0, ToStringLines.Number(res, "Equatorial radius (m) :"));
            Assert.Equal(radii[2] * 1000.0, ToStringLines.Number(res, "Polar radius (m) :"));
            Assert.Equal(earth.EquatorialRadius, ToStringLines.Number(res, "Equatorial radius (m) :"));
            Assert.Equal(earth.PolarRadius, ToStringLines.Number(res, "Polar radius (m) :"));
            Assert.Equal(earth.Flattening, ToStringLines.Number(res, "Flattening :"));
            Assert.Equal(earth.J2, ToStringLines.Number(res, "J2 :"));
            Assert.Equal(earth.J3, ToStringLines.Number(res, "J3 :"));
            Assert.Equal(earth.J4, ToStringLines.Number(res, "J4 :"));
        }
    }

    /// <summary>
    /// json: the fields of the record with their unit, read back bit for bit; a value the body has not (J2 of the Moon in
    /// these kernels) is null.
    /// </summary>
    [Fact]
    public void Json_ReadsBackBitForBit()
    {
        using (Configuration.Lock())
        {
            var earth = new Body.CelestialBody(399);
            var json = System.Text.Json.JsonDocument.Parse(CliRun.Ok("celestial-body-info", "399", "-K", "Data", "--format", "json")).RootElement;
            Assert.Equal(399, json.GetProperty("naif_id").GetInt32());
            Assert.Equal("EARTH", json.GetProperty("name").GetString());
            Assert.Equal(earth.Mass, json.GetProperty("mass_kg").GetDouble());
            Assert.Equal(earth.GM, json.GetProperty("gm_m3_s2").GetDouble());
            Assert.Equal("ITRF93", json.GetProperty("fixed_frame").GetString());
            Assert.Equal(earth.EquatorialRadius, json.GetProperty("equatorial_radius_m").GetDouble());
            Assert.Equal(earth.PolarRadius, json.GetProperty("polar_radius_m").GetDouble());
            Assert.Equal(earth.Flattening, json.GetProperty("flattening").GetDouble());
            Assert.Equal(earth.J2, json.GetProperty("j2").GetDouble());
            Assert.Equal(earth.J4, json.GetProperty("j4").GetDouble());

            var moon = System.Text.Json.JsonDocument.Parse(CliRun.Ok("celestial-body-info", "MOON", "-K", "Data", "--format", "json")).RootElement;
            Assert.Equal(System.Text.Json.JsonValueKind.Null, moon.GetProperty("j2").ValueKind);
        }
    }

    /// <summary>
    /// CASSINI's identifier, a name built into SPICE, ended the process in the native layer (#560, exit code 134); it is
    /// the library's argument error, raised before the native call, exit code 1.
    /// </summary>
    [Fact]
    public void SpacecraftIdentifier_IsTheLibrarysArgumentError()
    {
        using (Configuration.Lock())
        {
            var fromLibrary = Assert.Throws<ArgumentException>(() => new Body.CelestialBody(-82));
            Assert.Equal(CliRun.ErrorLine(fromLibrary), CliRun.Fails(1, "celestial-body-info", "-82", "-K", "Data"));
            Assert.Equal(CliRun.ErrorLine(fromLibrary), CliRun.Fails(1, "celestial-body-info", "CASSINI", "-K", "Data"));
        }
    }

    /// <summary>
    /// An identifier of the celestial classes that SPICE does not name (<c>bodc2n_c</c> finds none of these with the
    /// kernels of Data): the record held the name of a previous call or stack bytes and the body was built from it, or
    /// threw <see cref="System.InvalidOperationException"/> (#672); it is now an argument error naming the identifier.
    /// </summary>
    [Theory]
    [InlineData(398)]
    [InlineData(12345678)]
    public void UnnamedIdentifier_IsAnArgumentError(int id)
    {
        using (Configuration.Lock())
        {
            // A named body first, so that the native stack holds its record
            CliRun.Ok("celestial-body-info", "399", "-K", "Data");
            var e = CliRun.Fails(1, "celestial-body-info", id.ToString(), "-K", "Data");
            Assert.Contains(id.ToString(), e);
        }
    }
}
