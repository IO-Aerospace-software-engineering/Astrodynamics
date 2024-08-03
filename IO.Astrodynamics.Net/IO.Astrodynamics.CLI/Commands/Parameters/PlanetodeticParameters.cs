// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Cocona;

namespace IO.Astrodynamics.CLI.Commands.Parameters;

public class PlanetodeticParameters : ICommandParameterSet
{
    [Argument(Description = $"Planetodetic coordinates <longitude latitude altitude>")]
    public string Planetodetic { get; set; }
}