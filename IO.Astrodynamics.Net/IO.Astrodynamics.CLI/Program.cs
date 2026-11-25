using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cocona;
using Cocona.Application;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IO.Astrodynamics.CLI;

/// <summary>
/// The command-line tool <c>astro</c> (R29 of #668): its objects positional, everything else an option, the data on the
/// standard output, each error on one line of the standard error, exit code 0, 2 for the input, 3 for the kernels and 1
/// for any other error.
/// </summary>
internal static class Program
{
    private static readonly object RunLock = new();

    /// <summary>The options that take no value; every other option takes the next argument.</summary>
    private static readonly HashSet<string> Flags =
    [
        "--help", "-h", "--version", "--verbose", "--drag", "--srp",
        // The aliases of time-converter (D3.7 of #668)
        "-t", "-u", "-l", "-j", "-e", "-d"
    ];

    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// Runs the command of <paramref name="args"/>, its data on <paramref name="output"/> and its errors on
    /// <paramref name="error"/>, and returns its exit code. One run at a time: the commands share SPICE and the writers.
    /// </summary>
    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        lock (RunLock)
        {
            var exitCode = Environment.ExitCode;
            Environment.ExitCode = 0;
            // The errors of the run, Cocona's included, written to the caller's error once the run ends: whether Cocona
            // reported an error tells a help from an error that holds --help
            var errors = new StringWriter();
            CliContext.Begin(output, errors, error);
            try
            {
                string[] normalized;
                try
                {
                    normalized = Normalize(args);
                }
                catch (CliInputException e)
                {
                    errors.WriteLine($"Error: {e.Message}");
                    return CliContext.InputError;
                }

                var builder = CoconaApp.CreateBuilder(normalized, options =>
                {
                    options.EnableShellCompletionSupport = false;
                    options.TreatPublicMethodsAsCommands = false;
                });
                builder.Logging.ClearProviders();
                builder.Services.AddSingleton<ICoconaConsoleProvider>(new WriterConsoleProvider(output, errors));
                builder.Services.AddSingleton<ICoconaApplicationMetadataProvider>(new AstroMetadataProvider());

                var app = builder.Build();
                app.AddCommands<EphemerisCommand>();
                app.AddCommands<OrientationCommand>();
                app.AddCommands<GeometryFinderCommand>();
                app.AddCommands<OrbitalParametersConverterCommand>();
                app.AddCommands<FrameConverterCommand>();
                app.AddCommands<TimeConverterCommand>();
                app.AddCommands<BodyInformationCommand>();
                app.AddCommands<PropagateCommand>();
                app.Run();

                // No command ran: the help or the version (0; Cocona ends the help of a command with its own code), or an
                // error of the parser (an unknown command or option, a missing argument), which Cocona reports with its own
                // code, 1 or 129
                if (CliContext.ExitCode is { } code)
                {
                    return code;
                }

                var help = normalized.TakeWhile(x => x != "--").Any(x => x is "--help" or "-h");
                return Environment.ExitCode == 0 || (help && errors.GetStringBuilder().Length == 0) ? CliContext.Success : CliContext.InputError;
            }
            catch (Exception e)
            {
                errors.WriteLine($"Error: {CliContext.OneLine(e.Message)}");
                return CliContext.Failure;
            }
            finally
            {
                Environment.ExitCode = exitCode;
                error.Write(errors.ToString());
            }
        }
    }

    /// <summary>
    /// The arguments as Cocona reads them (<see cref="CliInputException"/> for an option given last without its value):
    /// each option joined to its value (<c>--value=-5deg</c>, <c>--kernels=path</c>),
    /// then <c>--</c> and the positional arguments, so that an argument that starts with a minus sign and a digit or a dot
    /// (<c>-1000</c>, <c>-0.5</c>) is read as a value, never as an option (R29 of #668: Cocona read <c>-1000</c> as the
    /// options <c>-1</c>, <c>-0</c>, ...). The first positional argument, the command, stays first.
    /// </summary>
    internal static string[] Normalize(IReadOnlyList<string> args)
    {
        var options = new List<string>();
        var positionals = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg == "--")
            {
                positionals.AddRange(args.Skip(i + 1));
                break;
            }

            if (!IsOption(arg))
            {
                positionals.Add(arg);
                continue;
            }

            if ((arg.StartsWith("--", StringComparison.Ordinal) && arg.Contains('=')) || Flags.Contains(arg) || IsBundledAliases(arg))
            {
                options.Add(arg);
                continue;
            }

            if (i + 1 == args.Count)
            {
                // An option given last without its value: it would take the "--" that precedes the positional arguments
                throw new CliInputException($"{(arg == "-K" ? "-K" : arg)} requires a value.");
            }

            var value = args[++i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                options.Add($"{arg}={value}");
            }
            else if (arg == "-K")
            {
                options.Add($"--kernels={value}");
            }
            else
            {
                // An unknown short option: Cocona reports it
                options.Add(arg);
                options.Add(value);
            }
        }

        if (positionals.Count == 0)
        {
            return options.ToArray();
        }

        return [positionals[0], .. options, "--", .. positionals.Skip(1)];
    }

    // The aliases of time-converter bundled behind one minus sign (-tj, -ue): flags, as each of them
    private static bool IsBundledAliases(string arg) =>
        arg.Length > 2 && arg[0] == '-' && arg[1] != '-' && arg.Skip(1).All(c => "tuljed".Contains(c));

    // An option starts with a minus sign, not followed by a digit or a dot (a negative number)
    private static bool IsOption(string arg) =>
        arg.Length > 1 && arg[0] == '-' && !char.IsDigit(arg[1]) && arg[1] != '.';

    private sealed class WriterConsoleProvider(TextWriter output, TextWriter error) : ICoconaConsoleProvider
    {
        public TextWriter Output { get; } = output;
        public TextWriter Error { get; } = error;
    }

    /// <summary>The name of the tool in its help, <c>astro</c>, whatever the host process (<c>dotnet</c> in a clone).</summary>
    private sealed class AstroMetadataProvider : ICoconaApplicationMetadataProvider
    {
        public string GetExecutableName() => "astro";

        public string GetProductName() => "astro";

        public string GetVersion() => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? string.Empty;

        public string GetDescription() =>
            typeof(Program).Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
    }
}
