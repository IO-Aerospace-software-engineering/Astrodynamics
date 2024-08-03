// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Cocona;

namespace IO.Astrodynamics.CLI.Commands.Parameters;

public class EpochParameters : ICommandParameterSet
{
    [Argument(Description = $"Epoch that matches the following format :\n\t\t\t\t\tDateTime format (ISO 8601)\t\t\t2024-01-01T21:00:00Z\n\t\t\t\t\tSeconds from J2000 (decimal value)\t\t144521.0 <UTC or TDB>\n\t\t\t\t\tJulian date (decimal value)\t\t\t245000.0 JD<UTC or TDB>")]
    public string Epoch { get; set; }
}