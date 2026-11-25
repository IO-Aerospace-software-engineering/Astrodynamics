using Cocona;

namespace IO.Astrodynamics.CLI.Commands.Parameters;

/// <summary>
/// The orbit given to a command (R29 of #668): its kind (<c>--from</c>), its values (<c>--orbit</c>), and, but for a TLE,
/// which carries them, its centre, epoch and frame.
/// </summary>
public sealed class OrbitOptions : ICommandParameterSet
{
    [Option("from", ValueName = "state|keplerian|equinoctial|tle", Description = "Kind of the orbit given: state, keplerian, equinoctial or tle")]
    public string From { get; set; }

    [Option("orbit", ValueName = "values",
        Description = "The values of the orbit, in one quoted argument or several: state \"x y z vx vy vz\" (m and m/s, or km and km/s), keplerian \"a e i raan aop m\" (m or km, rad or deg), equinoctial \"p f g h k l0\" (m or km, rad or deg for l0); tle: --orbit \"<line 1>\" --orbit \"<line 2>\"")]
    public string[] Orbit { get; set; }

    [Option("center", ValueName = "body", Description = "Centre of motion: a NAIF identifier or a name SPICE knows; not given for a TLE")]
    [HasDefaultValue]
    public string Center { get; set; }

    [Option("epoch", ValueName = "epoch", Description = "Epoch of the orbit: " + EpochOptions.Forms + "; not given for a TLE")]
    [HasDefaultValue]
    public string Epoch { get; set; }

    [Option("frame", ValueName = "frame", Description = "Frame of the orbit: ICRF (written J2000) or a frame of the library or of the loaded kernels; not given for a TLE")]
    [HasDefaultValue]
    public string Frame { get; set; }
}
