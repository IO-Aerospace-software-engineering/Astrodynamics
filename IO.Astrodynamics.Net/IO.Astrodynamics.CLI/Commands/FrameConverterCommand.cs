using Cocona;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Commands;

public class FrameConverterCommand
{
    [Command("frame-converter", Description = "The orientation of a frame relative to another at an epoch: the rotation from the first and the angular velocity")]
    public int Convert(
        [Argument("from", Description = "Frame converted from: ICRF (written J2000) or a frame of the library or of the loaded kernels")]
        string from,
        [Argument("to", Description = "Frame converted to, as the first")]
        string to,
        KernelOptions kernels,
        EpochOptions epoch,
        TimeScaleOptions timeScale,
        OutputOptions output,
        [Argument("unexpected", Description = "Nothing: a value with spaces is quoted, everything but the objects is an option")]
        string[] unexpected = null)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            CliValues.NoMore(unexpected);
            var format = CliOutput.Format(output.Format, tabular: false);
            var at = CliValues.Epoch(epoch.Epoch, "--epoch");
            var scale = CliValues.TimeScale(timeScale.TimeScale, at.Scale);

            CliKernels.Load(kernels.Kernels);
            var fromFrame = CliValues.Frame(from, "from");
            var toFrame = CliValues.Frame(to, "to");

            var orientation = CliTime.Relabel(fromFrame.ToFrame(toFrame, at), scale);
            EphemerisCommand.Write(format, CliOutput.OrientationText(orientation), CliOutput.Orientation(orientation));
        });
    }
}
