using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>The orbit of <see cref="OrbitOptions"/> (R29 of #668).</summary>
internal static class CliOrbit
{
    private static readonly Dimension[] StateDimensions =
        [Dimension.Length, Dimension.Length, Dimension.Length, Dimension.Speed, Dimension.Speed, Dimension.Speed];

    private static readonly Dimension[] KeplerianDimensions =
        [Dimension.Length, Dimension.None, Dimension.Angle, Dimension.Angle, Dimension.Angle, Dimension.Angle];

    private static readonly Dimension[] EquinoctialDimensions =
        [Dimension.Length, Dimension.None, Dimension.None, Dimension.None, Dimension.None, Dimension.Angle];

    /// <summary>The kind of the orbit, checked before the kernels are loaded, with the arguments a TLE refuses.</summary>
    internal static string Kind(OrbitOptions options)
    {
        var kind = CliValues.Choice(options.From, "--from", "state", "keplerian", "equinoctial", "tle")
                   ?? throw new CliInputException("--from is required: state, keplerian, equinoctial or tle.");
        if (options.Orbit == null || options.Orbit.Length == 0)
        {
            throw new CliInputException("--orbit is required: the values of the orbit, or the two lines of a TLE.");
        }

        if (kind == "tle")
        {
            if (options.Center != null || options.Epoch != null || options.Frame != null)
            {
                throw new CliInputException(
                    "--from tle: a TLE carries its centre (the Earth), its epoch and its frame (TEME); leave out --center, --epoch and --frame.");
            }

            if (options.Orbit.Length != 2)
            {
                throw new CliInputException($"--orbit: a TLE is two lines, given as --orbit \"<line 1>\" --orbit \"<line 2>\"; {options.Orbit.Length} were given.");
            }
        }
        else if (options.Center == null || options.Epoch == null || options.Frame == null)
        {
            throw new CliInputException($"--from {kind}: --center, --epoch and --frame are required.");
        }

        return kind;
    }

    /// <summary>
    /// The orbit, once the kernels are loaded, about the centre <paramref name="center"/> builds from its identifier.
    /// </summary>
    internal static OrbitalParameters.OrbitalParameters Read(OrbitOptions options, Func<int, CelestialBody> center)
    {
        var kind = Kind(options);
        try
        {
            if (kind == "tle")
            {
                // A malformed TLE throws ArgumentException (R21 of #668)
                return new Tle("body", options.Orbit[0], options.Orbit[1]);
            }

            var epoch = CliValues.Epoch(options.Epoch, "--epoch");
            var frame = CliValues.Frame(options.Frame, "--frame");
            var centerOfMotion = center(CliValues.Object(options.Center, "--center"));
            switch (kind)
            {
                case "state":
                    var sv = CliValues.Quantities(options.Orbit, StateDimensions, "--orbit");
                    return new StateVector(new Math.Vector3(sv[0], sv[1], sv[2]), new Math.Vector3(sv[3], sv[4], sv[5]), centerOfMotion, epoch, frame);
                case "keplerian":
                    var ke = CliValues.Quantities(options.Orbit, KeplerianDimensions, "--orbit");
                    return new KeplerianElements(ke[0], ke[1], ke[2], ke[3], ke[4], ke[5], centerOfMotion, epoch, frame);
                default:
                    var eq = CliValues.Quantities(options.Orbit, EquinoctialDimensions, "--orbit");
                    return new EquinoctialElements(eq[0], eq[1], eq[2], eq[3], eq[4], eq[5], centerOfMotion, epoch, frame);
            }
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            // Values no orbit has (#476), a malformed TLE (#675): the input's
            throw new CliInputException($"--orbit: {CliContext.OneLine(e.Message)}", e);
        }
    }
}
