using Cocona;

namespace IO.Astrodynamics.CLI.Commands.Parameters;

/// <summary>The kernels of a command (R29 of #668): <c>-K</c>, after those of <c>ASTRO_KERNELS</c>.</summary>
public sealed class KernelOptions : ICommandParameterSet
{
    [Option("kernels", ['K'], ValueName = "path",
        Description = "A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)")]
    [HasDefaultValue]
    public string[] Kernels { get; set; }
}

/// <summary>The output of a command: its format and the trace of its errors.</summary>
public sealed class OutputOptions : ICommandParameterSet
{
    [Option("format", ValueName = "text|csv|json",
        Description = "Output format: text (default), csv for the commands that write rows, json for those that write one record")]
    [HasDefaultValue]
    public string Format { get; set; }

    [Option("verbose", Description = "Write the trace of an error after its message")]
    [HasDefaultValue]
    public bool Verbose { get; set; }
}

/// <summary>The scale of the epochs a command writes.</summary>
public sealed class TimeScaleOptions : ICommandParameterSet
{
    [Option("time-scale", ValueName = "tdb|tt|tai|utc|gps",
        Description = "Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)")]
    [HasDefaultValue]
    public string TimeScale { get; set; }
}

/// <summary>The window and the step of a command.</summary>
public sealed class WindowOptions : ICommandParameterSet
{
    [Option("begin", ValueName = "epoch", Description = "Start of the window: " + EpochOptions.Forms)]
    public string Begin { get; set; }

    [Option("end", ValueName = "epoch", Description = "End of the window, in the same forms")]
    public string End { get; set; }

    [Option("step", ValueName = "step", Description = "Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h)")]
    public string Step { get; set; }
}

/// <summary>The window of a propagation and its output step, 60 s by default (the library's, D4 of #668).</summary>
public sealed class PropagationWindowOptions : ICommandParameterSet
{
    [Option("begin", ValueName = "epoch", Description = "Start of the propagation: " + EpochOptions.Forms)]
    public string Begin { get; set; }

    [Option("end", ValueName = "epoch", Description = "End of the propagation, in the same forms")]
    public string End { get; set; }

    [Option("step", ValueName = "step", Description = "Output step: [d.]hh:mm:ss[.f], or a number and s, min, h or d")]
    [HasDefaultValue]
    public string Step { get; set; } = "60s";
}

/// <summary>The epoch of a command.</summary>
public sealed class EpochOptions : ICommandParameterSet
{
    internal const string Forms =
        "an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given";

    [Option("epoch", ValueName = "epoch", Description = "Epoch: " + Forms)]
    public string Epoch { get; set; }
}

/// <summary>The aberration correction of a command.</summary>
public sealed class AberrationOptions : ICommandParameterSet
{
    [Option("aberration", ValueName = "correction",
        Description = "Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case")]
    [HasDefaultValue]
    public string Aberration { get; set; } = "NONE";
}
