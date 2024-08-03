using System.Text;
using IO.Astrodynamics.CLI.Commands;

namespace IO.Astrodynamics.CLI.Tests;

public class BodyInformationsTests
{
    [Fact]
    public void CelestialBodyInformation()
    {
        lock (Configuration.objLock)
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
}