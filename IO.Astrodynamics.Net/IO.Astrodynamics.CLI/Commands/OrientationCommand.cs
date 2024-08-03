using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.CLI.Commands;

public class OrientationCommand
{
    public OrientationCommand()
    {
    }

    [Command("orientation", Description = "Compute orientations of given object")]
    public Task Orientation(
        [Argument(Description = "Kernels directory path")] string kernelsPath,
        [Argument(Description = "Object identifier (Naif Identifier)")] int objectId,
        WindowParameters windowParameters,
        [Argument(Description = "Step size <d.hh:mm:ss.fff>")] TimeSpan step,
        [Argument(Description = "Frame")] string frame="ICRF")
    {
        if (frame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            frame = "j2000";
        }
        
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));
        
        var celestialItem = Helpers.CreateOrientable(objectId);

        List<StateOrientation> orientations = new List<StateOrientation>();
        Frame targetFrame = new Frame(frame);
        Window windowInput = Helpers.ConvertWindowInput(windowParameters.Begin, windowParameters.End);
        for (DateTime epoch = windowInput.StartDate; epoch <= windowInput.EndDate; epoch+=step)
        {
            orientations.Add(celestialItem.GetOrientation(targetFrame,epoch));
        }

        foreach (var orientation in orientations)
        {
            Console.WriteLine(orientation.ToString());
        }

        return Task.CompletedTask;
    }

   
}