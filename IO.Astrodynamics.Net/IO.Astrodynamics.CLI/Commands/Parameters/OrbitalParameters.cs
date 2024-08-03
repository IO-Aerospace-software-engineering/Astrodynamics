// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Cocona;

namespace IO.Astrodynamics.CLI.Commands.Parameters;

public class OrbitalParameters : ICommandParameterSet
{
    [Argument(Description = "Center of motion identifier (Naif Identifier)")]
    public int CenterOfMotionId { get; set; }

    [Argument(Description = "Orbital parameters values like state vector, keplerian, equinoctial or two lines. <X Y Z vX vY vZ> <A E I AN PA M> <P F G H K L0> <L1,L2>")]
    public string OrbitalParametersValues { get; set; }

    [Argument(Description = $"Epoch that matches the following format :\n\t\t\t\t\tDateTime format (ISO 8601)\t\t\t2024-01-01T21:00:00Z\n\t\t\t\t\tSeconds from J2000 (decimal value)\t\t144521.0 <UTC or TDB>\n\t\t\t\t\tJulian date (decimal value)\t\t\t245000.0 JD<UTC or TDB>")]
    public string OrbitalParametersEpoch { get; set; }

    [Argument(Description = "Frame")] public string Frame { get; set; }

    [Option('s', Description = "Orbital parameters values represents a state vector")]
    public bool FromStateVector { get; set; }

    [Option('k', Description = "Orbital parameters values represents keplerian elements")]
    public bool FromKeplerian { get; set; }

    [Option('q', Description = "Orbital parameters values represents equinoctial elements")]
    public bool FromEquinoctial { get; set; }

    [Option('t', Description = "Orbital parameters values represents two lines elements")]
    public bool FromTLE { get; set; }
}