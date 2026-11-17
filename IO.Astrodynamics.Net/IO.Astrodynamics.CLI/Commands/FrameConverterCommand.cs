using System;
using System.IO;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.CLI.Commands;

public class FrameConverterCommand
{
    public FrameConverterCommand()
    {
    }

    [Command("frame-converter", Description = "Convert a frame to another at given epoch")]
    public Task Convert(
        [Argument(Description = "Kernels path: a directory, a kernel file or a meta-kernel")]
        string kernelsPath,
        [Argument(Description = "Origin frame")]
        string from,
        [Argument(Description = "Target frame")]
        string to,
        EpochParameters epoch)
    {
        SpiceAPI.Instance.LoadKernels(Helpers.KernelsPath(kernelsPath));

        var inputEpoch = Helpers.ConvertDateTimeInput(epoch.Epoch);

        var q = Helpers.ResolveFrame(from).ToFrame(Helpers.ResolveFrame(to), inputEpoch);
        Console.WriteLine(q);
        return Task.CompletedTask;
    }
}