using System.Text;
using IO.Astrodynamics.CLI.Commands;

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

            Assert.Equal(
                $"                          Type : Planet                          {Environment.NewLine}                    Identifier : 399                             {Environment.NewLine}                          Name : EARTH                           {Environment.NewLine}                     Mass (kg) : 5.972168E+024                   {Environment.NewLine}                   GM (m^3.s^2): 3.986004E+014                   {Environment.NewLine}                   Fixed frame : ITRF93                          {Environment.NewLine}         Equatorial radius (m) : 6.378137E+006                   {Environment.NewLine}              Polar radius (m) : 6.356752E+006                   {Environment.NewLine}                    Flattening : 0.0033528131084554157           {Environment.NewLine}                            J2 : 0.001082616                     {Environment.NewLine}                            J3 : -2.5388099999999996E-06         {Environment.NewLine}                            J4 : -1.65597E-06                    {Environment.NewLine}{Environment.NewLine}"
                , res);
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
            var command = new BodyInformationCommand();
            // A named body first, so that the native stack holds its record
            Console.SetOut(new StringWriter());
            command.GetInformations("Data", 399);
            var e = Assert.Throws<ArgumentException>(() => { _ = command.GetInformations("Data", id); });
            Assert.Contains(id.ToString(), e.Message);
        }
    }
}
