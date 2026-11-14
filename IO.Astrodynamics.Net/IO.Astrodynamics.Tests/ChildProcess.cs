// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The child processes of the tests: console projects the test project builds and copies to a folder next to its output
/// (<c>ColdStart/</c>, <c>KernelUnload/</c>), started with the dotnet host that runs the tests.
/// </summary>
internal static class ChildProcess
{
    /// <summary>
    /// The path of a child's assembly: in <paramref name="folder"/> next to the tests, or beside the tests when the build was
    /// given an output directory (-o), which the child is built into.
    /// </summary>
    internal static string AssemblyPath(string folder, string assembly)
    {
        string child = Path.Combine(AppContext.BaseDirectory, folder, assembly);
        return File.Exists(child) ? child : Path.Combine(AppContext.BaseDirectory, assembly);
    }

    /// <summary>
    /// Runs a child's assembly with the arguments, in the output directory of the tests, and returns its exit code and its
    /// outputs; a child that has not ended within <paramref name="timeout"/> is killed and gives null.
    /// </summary>
    internal static (int ExitCode, string Output, string Error)? Run(string assembly, TimeSpan timeout, params string[] arguments)
    {
        var start = new ProcessStartInfo(DotnetHost())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return null;
        }

        // The parameterless wait also waits for the end of the redirected streams
        process.WaitForExit();
        return (process.ExitCode, output.Result, error.Result);
    }

    /// <summary>
    /// The dotnet host of the runtime that runs the tests: the process itself when it is the host (<c>dotnet exec
    /// testhost.dll</c>), otherwise the host at the root of the runtime's installation (the directory of the runtime is
    /// <c>&lt;root&gt;/shared/Microsoft.NETCore.App/&lt;version&gt;</c>), then <c>DOTNET_HOST_PATH</c>, which the .NET CLI
    /// sets, then <c>dotnet</c> on the path.
    /// </summary>
    internal static string DotnetHost()
    {
        string executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string processPath = Environment.ProcessPath;
        if (processPath != null && string.Equals(Path.GetFileName(processPath), executable, StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var runtime = new DirectoryInfo(Path.GetDirectoryName(typeof(object).Assembly.Location));
        string root = runtime.Parent?.Parent?.Parent?.FullName;
        if (root != null && File.Exists(Path.Combine(root, executable)))
        {
            return Path.Combine(root, executable);
        }

        string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return !string.IsNullOrEmpty(hostPath) && File.Exists(hostPath) ? hostPath : executable;
    }
}
