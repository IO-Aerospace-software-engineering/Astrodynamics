using System;
using System.IO;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.CLI.Commands;

public class BodyInformationCommand
{
    public BodyInformationCommand()
    {
    }

    [Command("celestial-body-info", Description = "Get celestial body informations")]
    public Task GetInformations(
        [Argument(Description = "Kernels path: a directory, a kernel file or a meta-kernel")]
        string kernelsPath,
        [Argument(Description = "Celestial body identifier (Naif Identifier)")]
        int id)
    {
        SpiceAPI.Instance.LoadKernels(Helpers.KernelsPath(kernelsPath));
        CelestialBody body = new CelestialBody(id);
        Console.WriteLine(body.ToString());
        return Task.CompletedTask;
    }
}