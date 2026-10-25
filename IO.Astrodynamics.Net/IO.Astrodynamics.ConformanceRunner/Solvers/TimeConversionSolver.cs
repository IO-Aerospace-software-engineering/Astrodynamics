using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>time_conversion</c> category (spec.md section 11.2): an instant as the seconds from 2000-01-01T12:00:00 in
/// the calendar of TAI, UTC, TT, TDB and GPS.
/// </summary>
/// <remarks>
/// Each output is the difference of the ticks (100 ns) of the converted <see cref="DateTime"/> and of J2000, never a
/// Julian date, whose double resolves 40 µs. TT is the framework's TDT.
/// </remarks>
public class TimeConversionSolver : ICategorySolver
{
    private static readonly (string Output, ITimeFrame Frame)[] Scales =
    {
        ("tai_s", TimeFrame.TAIFrame),
        ("utc_s", TimeFrame.UTCFrame),
        ("tt_s", TimeFrame.TDTFrame),
        ("tdb_s", TimeFrame.TDBFrame),
        ("gps_s", TimeFrame.GPSFrame)
    };

    public string Category => "time_conversion";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseTimeConversionInputs(caseInput.Inputs);
        var epoch = EpochParser.Parse(inputs.Epoch);
        // A scale whose date the calendar cannot hold (after 9999-12-31) is given as its exception (SolverOutput)
        return Scales.ToDictionary(scale => scale.Output,
            scale => SolverOutput.Of(() => SecondsFromJ2000(epoch.ConvertTo(scale.Frame))));
    }

    /// <summary>
    /// The seconds from 2000-01-01T12:00:00 of the calendar date and time of <paramref name="time"/>, in its own scale.
    /// </summary>
    internal static double SecondsFromJ2000(Time time)
    {
        return (time.DateTime.Ticks - Time.J2000.Ticks) / (double)TimeSpan.TicksPerSecond;
    }
}
