using Cocona;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Commands;

public class BodyInformationCommand
{
    [Command("celestial-body-info", Description = "The information of a celestial body: identifier, name, mass, GM, fixed frame, radii, flattening, J2 to J4")]
    public int GetInformations(
        [Argument("body", Description = "Celestial body: " + EphemerisCommand.ObjectDescription)]
        string body,
        KernelOptions kernels,
        OutputOptions output,
        [Argument("unexpected", Description = "Nothing: a value with spaces is quoted, everything but the objects is an option")]
        string[] unexpected = null)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            CliValues.NoMore(unexpected);
            var format = CliOutput.Format(output.Format, tabular: false);
            CliKernels.Load(kernels.Kernels);
            var celestialBody = new CelestialBody(CliValues.Object(body, "body"));
            EphemerisCommand.Write(format, celestialBody.ToString(), CliOutput.Body(celestialBody));
        });
    }
}
