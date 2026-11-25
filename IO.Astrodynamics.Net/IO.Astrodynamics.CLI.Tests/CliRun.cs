using IO.Astrodynamics.CLI.Cli;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// Runs the command-line tool in the test process through its entry point (<see cref="Program.Run"/>), as a shell runs
/// it: the arguments as given, the standard output and error captured, the exit code returned (R29 of #668).
/// </summary>
internal static class CliRun
{
    internal readonly record struct Result(int ExitCode, string Out, string Error);

    internal static Result Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return new Result(code, output.ToString(), error.ToString());
    }

    /// <summary>The standard output of a run that succeeds, with nothing on the standard error.</summary>
    internal static string Ok(params string[] args)
    {
        var result = Run(args);
        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.Error}");
        Assert.Equal(string.Empty, result.Error);
        return result.Out;
    }

    /// <summary>The standard error of a run that fails with <paramref name="exitCode"/>, with nothing on the standard output.</summary>
    internal static string Fails(int exitCode, params string[] args)
    {
        var result = Run(args);
        Assert.True(result.ExitCode == exitCode, $"exit {result.ExitCode}, expected {exitCode}: {result.Error}{result.Out}");
        Assert.Equal(string.Empty, result.Out);
        return result.Error;
    }

    /// <summary>
    /// The exit code of an exception of the library: 2 for an identifier the library refuses (<c>naifId</c>), the object
    /// of the input, 1 otherwise.
    /// </summary>
    internal static int ExitCodeOf(Exception e) => e is ArgumentException { ParamName: "naifId" } ? 2 : 1;

    /// <summary>The line the tool writes for an exception of the library.</summary>
    internal static string ErrorLine(Exception e) => $"Error: {CliContext.OneLine(e.Message)}{Environment.NewLine}";
}
