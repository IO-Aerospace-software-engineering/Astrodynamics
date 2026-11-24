using Cocona;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Commands;

public class OrbitalParametersConverterCommand
{
    [Command("orbital-parameters-converter", Description = "Converts an orbit to a state vector, Keplerian or equinoctial elements, at an epoch and in a frame")]
    public int Converter(
        [Option("as", ValueName = "state|keplerian|equinoctial", Description = "Kind of the orbit written: state, keplerian or equinoctial")]
        string @as,
        OrbitOptions orbit,
        KernelOptions kernels,
        TimeScaleOptions timeScale,
        OutputOptions output,
        [Option("target-epoch", ValueName = "epoch", Description = "Epoch of the orbit written, by the two-body motion (SGP4 for a TLE): the epoch of the orbit given by default")]
        string targetEpoch = null,
        [Option("target-frame", ValueName = "frame", Description = "Frame of the orbit written: ICRF (written J2000) or a frame of the library or of the loaded kernels")]
        string targetFrame = "ICRF")
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var format = CliOutput.Format(output.Format, tabular: false);
            var kind = CliValues.Choice(@as, "--as", "state", "keplerian", "equinoctial");
            CliOrbit.Kind(orbit);
            var outputEpoch = targetEpoch == null ? (TimeSystem.Time?)null : CliValues.Epoch(targetEpoch, "--target-epoch");

            CliKernels.Load(kernels.Kernels);
            var outputFrame = CliValues.Frame(targetFrame, "--target-frame");
            var input = CliOrbit.Read(orbit, id => new CelestialBody(id));

            // At the epoch and in the frame asked, then of the kind asked, labelled in the scale of the epoch written
            var epoch = outputEpoch ?? input.Epoch;
            var converted = input.AtEpoch(epoch).ToFrame(outputFrame);
            converted = CliTime.Relabel(converted, CliValues.TimeScale(timeScale.TimeScale, epoch.Scale));
            OrbitalParameters.OrbitalParameters result = kind switch
            {
                "keplerian" => converted.ToKeplerianElements(),
                "equinoctial" => converted.ToEquinoctial(),
                _ => converted.ToStateVector()
            };

            EphemerisCommand.Write(format, result.ToString(), CliOutput.Orbit(result));
        });
    }
}
