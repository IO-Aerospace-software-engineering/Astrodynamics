using System;
using System.IO;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.CLI.Commands;

public class OrbitalParametersConverterCommand
{
    public OrbitalParametersConverterCommand()
    {
    }

    [Command("orbital-parameters-converter", Description = "Convert an orbital parameters to another type at given epoch in given frame")]
    public Task Converter(
        [Argument(Description = "Directory kernels path")]
        string kernelsPath,
        Parameters.OrbitalParameters orbitalParameters,
        [Option( Description = "Convert to state vector")]
        bool toStateVector,
        [Option(Description = "Convert to keplerian elements")]
        bool toKeplerian,
        [Option(Description = "Convert to Equinoctial")]
        bool toEquinoctial,
        EpochParameters targetEpoch,
        [Argument(Description = "Target frame")]
        string targetFrame = "ICRF")
    {
        //Load kernels
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        //Check inputs
        if (!(orbitalParameters.FromEquinoctial ^ orbitalParameters.FromKeplerian ^ orbitalParameters.FromStateVector ^ orbitalParameters.FromTLE))
        {
            throw new ArgumentException("You must set the original orbital parameters type. use --help for more information");
        }

        if (!(toStateVector ^ toKeplerian ^ toEquinoctial))
        {
            throw new ArgumentException("You must set the target orbital parameters type. use --help for more information");
        }

        //Clean inputs
        if (orbitalParameters.Frame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            orbitalParameters.Frame = "j2000";
        }

        if (targetFrame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            targetFrame = "j2000";
        }

        if (string.IsNullOrEmpty(targetEpoch?.Epoch))
        {
            targetEpoch.Epoch = orbitalParameters.OrbitalParametersEpoch;
        }

        //Initialize data

        var outputFrame = new Frame(targetFrame);

        var outputEpoch = Helpers.ConvertDateTimeInput(targetEpoch.Epoch);


        var inputOrbitalParameters = Helpers.ConvertToOrbitalParameters(orbitalParameters.OrbitalParametersValues, orbitalParameters.CenterOfMotionId,
            orbitalParameters.OrbitalParametersEpoch,
            orbitalParameters.Frame, orbitalParameters.FromStateVector, orbitalParameters.FromKeplerian, orbitalParameters.FromEquinoctial, orbitalParameters.FromTLE);

        //At given epoch and in given frame
        inputOrbitalParameters = inputOrbitalParameters!.AtEpoch(outputEpoch).ToFrame(outputFrame);

        //Generate required output
        if (toStateVector)
        {
            inputOrbitalParameters = inputOrbitalParameters.ToStateVector();
        }
        else if (toKeplerian)
        {
            inputOrbitalParameters = inputOrbitalParameters.ToKeplerianElements();
        }
        else if (toEquinoctial)
        {
            inputOrbitalParameters = inputOrbitalParameters.ToEquinoctial();
        }

        Console.WriteLine(inputOrbitalParameters.ToString());

        return Task.CompletedTask;
    }
}