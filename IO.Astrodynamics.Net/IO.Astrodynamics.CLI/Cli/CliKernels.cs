using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>
/// The kernels of a command (R29 of #668): the paths of the environment variable <c>ASTRO_KERNELS</c>, separated by the
/// path separator of the platform (<c>:</c>, <c>;</c> on Windows), then those of <c>-K</c>, in order; each a kernel file, a
/// meta-kernel or a directory, loaded by <see cref="SpiceAPI.LoadKernels(string)"/>.
/// </summary>
internal static class CliKernels
{
    /// <summary>The environment variable of the kernels every command loads.</summary>
    internal const string EnvironmentVariable = "ASTRO_KERNELS";

    /// <summary>The paths a command loads, in order.</summary>
    internal static IReadOnlyList<string> Paths(IEnumerable<string> options)
    {
        var environment = Environment.GetEnvironmentVariable(EnvironmentVariable) ?? string.Empty;
        return environment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(options ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    /// <summary>
    /// Loads the kernels of the command; <see cref="CliKernelException"/> when none is given, for a path that does not
    /// exist and for a file SPICE refuses.
    /// </summary>
    internal static void Load(IEnumerable<string> options)
    {
        var paths = Paths(options);
        if (paths.Count == 0)
        {
            throw new CliKernelException($"No kernels: give -K <path> (a kernel, a meta-kernel or a directory), or set {EnvironmentVariable}.");
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new CliKernelException($"-K: the kernel path {path} does not exist.");
            }

            try
            {
                SpiceAPI.Instance.LoadKernels(path);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                throw new CliKernelException($"-K: {e.Message}", e);
            }
        }
    }
}
