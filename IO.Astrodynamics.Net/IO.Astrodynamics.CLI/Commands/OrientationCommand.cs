using System.Collections.Generic;
using System.Linq;
using Cocona;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.CLI.Commands;

public class OrientationCommand
{
    [Command("orientation", Description = "The orientations of a celestial body or a spacecraft relative to a frame over a window: the rotation from the frame and the angular velocity")]
    public int Orientation(
        [Argument("object", Description = "Celestial body, or spacecraft read from its CK: " + EphemerisCommand.ObjectDescription)]
        string @object,
        KernelOptions kernels,
        WindowOptions window,
        TimeScaleOptions timeScale,
        OutputOptions output,
        [Option("frame", ValueName = "frame", Description = "Reference frame: ICRF (written J2000) or a frame of the library or of the loaded kernels")]
        string frame = "ICRF",
        [Argument("unexpected", Description = "Nothing: a value with spaces is quoted, everything but the objects is an option")]
        string[] unexpected = null)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            CliValues.NoMore(unexpected);
            var format = CliOutput.Format(output.Format, tabular: true);
            var searchWindow = CliValues.Window(window.Begin, window.End);
            var step = CliValues.Step(window.Step, "--step");
            var scale = CliValues.TimeScale(timeScale.TimeScale, searchWindow.StartDate.Scale);

            CliKernels.Load(kernels.Kernels);
            var objectId = CliValues.Object(@object, "object");
            var referenceFrame = CliValues.Frame(frame, "--frame");
            var orientationAt = Helpers.CreateOrientationReader(objectId, referenceFrame);

            // The epochs of the ephemeris reads: start + i·step within the window, in the scale of its start (#563; unified
            // with Window.Sample in L6, decision 9 of #668)
            var orientations = new List<StateOrientation>();
            for (long i = 0;; i++)
            {
                var epoch = searchWindow.StartDate + step * i;
                if (epoch > searchWindow.EndDate)
                {
                    break;
                }

                orientations.Add(CliTime.Relabel(orientationAt(epoch), scale));
            }

            if (format == OutputFormat.Csv)
            {
                CliOutput.WriteCsv(CliContext.Out, CliOutput.OrientationHeader, orientations.Select(CliOutput.Orientation));
                return;
            }

            foreach (var orientation in orientations)
            {
                CliContext.Out.WriteLine(CliOutput.OrientationText(orientation));
            }
        });
    }
}
