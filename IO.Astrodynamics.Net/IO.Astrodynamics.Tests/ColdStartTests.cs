// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// No deadlock between the SpiceAPI lock and the type initializers that read SPICE (#623), in fresh processes: in this
/// process another test has already run every initializer, so the test starts the child process
/// <c>IO.Astrodynamics.Tests.ColdStart</c> (copied to <c>ColdStart/</c> by the build), which calls the library on two
/// threads before any static class of the library is initialized, and requires it to end and exit 0.
/// </summary>
/// <remarks>
/// <para>
/// Each mode pairs a method that takes the lock (thread A) with the first read of a static class whose initializer takes
/// it (thread B): <c>direct</c> <see cref="SpiceAPI.ConvertTleToStateVector(string, string, string, TimeSystem.Time)"/>
/// and <c>tle</c> <see cref="OrbitalParameters.TLE.TLE.ToStateVector(TimeSystem.Time)"/> with
/// <see cref="SolarSystemObjects.PlanetsAndMoons"/>, <c>lagrange</c> <see cref="SpiceAPI.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>
/// from L1 with <see cref="SolarSystemObjects.Barycenters"/>. Before #623 thread A read the static class while it held the
/// lock and the process hung forever.
/// </para>
/// <para>
/// Schedules: <c>held</c> holds thread B inside the initializer, before it takes the lock, until thread A has run, which
/// reproduced the hang in every child of every mode before the fix (measured, Linux x64, 2026-11-11, see
/// <see cref="HeldChildren"/>); <c>race</c> starts the two threads behind a barrier, the reproduction of #623, which hung in
/// every child of <c>direct</c> and <c>lagrange</c> and in 10 % to 25 % of those of <c>tle</c>.
/// </para>
/// </remarks>
public class ColdStartTests
{
    // Children per mode and schedule, and the hold of thread B, ms. Before the fix (Linux x64, 4 cores, Release, 2026-11-11,
    // a child hung when it had not ended 3 s after its start): held with a hold of 200 ms, 40 / 40 children hung in direct
    // and tle, 30 / 30 in lagrange; race, 120 / 120 in direct and lagrange, 35 / 200 in tle (25 / 100 and 10 / 100 in two
    // series). After it, 0 hangs over 200 children of each mode and schedule. One held child per mode finds the defect;
    // the race children add the reproduction of #623
    private const int HeldChildren = 3;
    private const int RaceChildren = 3;
    private const int HoldMilliseconds = 200;

    // A child that ends takes 0.11 to 0.40 s (Linux x64, 4 cores, 1200 children), 0.12 to 0.53 s with the 4 cores kept
    // busy by 4 other processes (180 children); a deadlock never ends. The limit is more than 100 times the slowest child,
    // for a slow CI machine
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("direct")]
    [InlineData("tle")]
    [InlineData("lagrange")]
    public void TwoThreadsEndOnTheFirstUseOfAStaticBodyClass(string mode)
    {
        for (int i = 0; i < HeldChildren; i++)
        {
            RunChild(mode, "held", HoldMilliseconds.ToString(CultureInfo.InvariantCulture));
        }

        for (int i = 0; i < RaceChildren; i++)
        {
            RunChild(mode, "race");
        }
    }

    private static void RunChild(string mode, string schedule, string hold = null)
    {
        string child = Path.Combine(AppContext.BaseDirectory, "ColdStart", "IO.Astrodynamics.Tests.ColdStart.dll");
        Assert.True(File.Exists(child), $"The child process {child} is missing: the build of the test project copies it.");

        var start = new ProcessStartInfo(DotnetHost())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(child);
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Data", "SolarSystem"));
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(schedule);
        if (hold != null)
        {
            start.ArgumentList.Add(hold);
        }

        using var process = Process.Start(start);
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ChildTimeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail($"The child ({mode}, {schedule}) did not end within {ChildTimeout.TotalSeconds} s: a thread holding the " +
                        $"SpiceAPI lock waits for a type initializer that waits for the lock (#623). Output: {output.Result} {error.Result}");
        }

        // The parameterless wait also waits for the end of the redirected streams
        process.WaitForExit();
        Assert.True(process.ExitCode == 0,
            $"The child ({mode}, {schedule}) exited {process.ExitCode}. Output: {output.Result} Error: {error.Result}");
    }

    /// <summary>
    /// The dotnet host of the runtime that runs the tests: the process itself when it is the host (<c>dotnet exec
    /// testhost.dll</c>), otherwise the host at the root of the runtime's installation (the directory of the runtime is
    /// <c>&lt;root&gt;/shared/Microsoft.NETCore.App/&lt;version&gt;</c>), then <c>DOTNET_HOST_PATH</c>, which the .NET CLI
    /// sets, then <c>dotnet</c> on the path.
    /// </summary>
    private static string DotnetHost()
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
