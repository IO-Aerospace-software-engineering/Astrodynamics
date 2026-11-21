// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Cocona;

namespace IO.Astrodynamics.CLI.Commands.Parameters;

public class EpochParameters : ICommandParameterSet
{
    [Argument(Description = "Epoch, in one of the following forms (a scale tag is UTC, TT, TDT, TDB, TAI or GPS, in any case; TDB when none is given):\n\t\t\t\t\tISO 8601 or day of year\t\t\t2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC, 2024-01-01T21:00:00+02:00\n\t\t\t\t\tSeconds from J2000\t\t\t144521.0 [TAG]\n\t\t\t\t\tJulian date\t\t\t\t2450000.0 JD [TAG]")]
    public string Epoch { get; set; }
}