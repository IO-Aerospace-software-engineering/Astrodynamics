using System;
using System.IO;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>
/// The output of the run in progress and its exit code (R29 of #668): the data on <see cref="Out"/>, the errors on
/// <see cref="Error"/>, one line each, the trace with <c>--verbose</c>. One run at a time (<see cref="Program.Run"/>).
/// </summary>
internal static class CliContext
{
    /// <summary>Exit code of a successful command.</summary>
    internal const int Success = 0;

    /// <summary>Exit code of an error that is neither the input's nor the kernels': the library's or SPICE's.</summary>
    internal const int Failure = 1;

    /// <summary>Exit code of an argument the command cannot read (<see cref="CliInputException"/>), and of the
    /// arguments the parser rejects (an unknown option, a missing argument).</summary>
    internal const int InputError = 2;

    /// <summary>Exit code of kernels the command cannot load (<see cref="CliKernelException"/>).</summary>
    internal const int KernelError = 3;

    internal static TextWriter Out { get; private set; } = Console.Out;

    internal static TextWriter Error { get; private set; } = Console.Error;

    /// <summary>
    /// The caller's standard error, written at once: the progress of a long command under <c>--verbose</c>, which the
    /// errors, buffered until the run ends (<see cref="Program.Run"/>), would deliver after its output.
    /// </summary>
    internal static TextWriter Progress { get; private set; } = Console.Error;

    /// <summary>The exit code the command set, null while no command ran (the parser's own errors, the help).</summary>
    internal static int? ExitCode { get; private set; }

    internal static void Begin(TextWriter output, TextWriter error, TextWriter progress)
    {
        Out = output;
        Error = error;
        Progress = progress;
        ExitCode = null;
    }

    /// <summary>
    /// Runs the body of a command: its exit code, or that of its exception, written on one line on
    /// <see cref="Error"/>, with the trace when <paramref name="verbose"/>.
    /// </summary>
    internal static int Execute(bool verbose, Action body)
    {
        try
        {
            body();
            ExitCode = Success;
        }
        catch (Exception e)
        {
            ExitCode = e switch
            {
                CliInputException => InputError,
                // An identifier the library refuses (no name in SPICE, not a celestial body): the object of the input
                ArgumentException { ParamName: "naifId" } => InputError,
                CliKernelException => KernelError,
                _ => Failure
            };
            Error.WriteLine($"Error: {OneLine(e.Message)}");
            if (verbose)
            {
                Error.WriteLine(e.ToString());
            }
        }

        return ExitCode.Value;
    }

    /// <summary>A message on one line: its line breaks and tabs become spaces.</summary>
    internal static string OneLine(string message) =>
        string.Join(" ", message.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
