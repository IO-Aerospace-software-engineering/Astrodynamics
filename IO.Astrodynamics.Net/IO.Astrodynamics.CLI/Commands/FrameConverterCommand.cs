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
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Origin frame")]
        string from,
        [Argument(Description = "Target frame")]
        string to,
        EpochParameters epoch)
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        if (from.Equals("ICRF", StringComparison.InvariantCultureIgnoreCase))
        {
            from = "J2000";
        }
        
        if (to.Equals("ICRF", StringComparison.InvariantCultureIgnoreCase))
        {
            to = "J2000";
        }
        
        var inputEpoch = Helpers.ConvertDateTimeInput(epoch.Epoch);

        var q = new Frame(from).ToFrame(new Frame(to), inputEpoch);
        Console.WriteLine(q);
        return Task.CompletedTask;
    }
}