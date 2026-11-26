// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// No deadlock between the SpiceApi lock and the type initializers that read SPICE (#623), in fresh processes: in this
/// process another test has already run every initializer, so the test starts the child process
/// <c>IO.Astrodynamics.Tests.ColdStart</c> (copied to <c>ColdStart/</c> by the build, or built beside the tests when the
/// output directory is given on the command line), which calls the library on two threads before any static class of
/// the library is initialized, and requires it to end and exit 0.
/// </summary>
/// <remarks>
/// <para>
/// Each mode pairs a method that takes the lock (thread A) with the first read of a static class whose initializer takes
/// it (thread B): <c>direct</c> <see cref="SpiceApi.ConvertTleToStateVector(string, string, string, TimeSystem.Time)"/>
/// and <c>tle</c> <see cref="OrbitalParameters.Tle.ToStateVector(TimeSystem.Time)"/> with
/// <see cref="SolarSystemObjects.PlanetsAndMoons"/>, <c>lagrange</c> <see cref="SpiceApi.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>
/// and <c>lagrange-window</c> <see cref="SpiceApi.ReadEphemeris(TimeSystem.Window, int, int, Frames.Frame, Aberration, TimeSpan)"/>
/// from L1 with <see cref="SolarSystemObjects.Barycenters"/>. Before #623 thread A read the static class while it held the
/// lock and the process hung forever.
/// </para>
/// <para>
/// Schedules: <c>held</c> holds thread B inside the initializer, before it takes the lock, until thread A has run, which
/// reproduced the hang in every child of every mode before the fix (measured, Linux x64, 2026-11-11, see
/// <see cref="HeldChildren"/>); <c>race</c> starts the two threads behind a barrier, the reproduction of #623, which hung in
/// every child of <c>direct</c>, <c>lagrange</c> and <c>lagrange-window</c> and in 10 % to 25 % of those of <c>tle</c>.
/// </para>
/// <para>
/// The class runs alone, after the test classes that run in parallel (<see cref="ColdStartCollection"/>): on Windows a
/// child process inherits every inheritable handle of the test host, among them the files CSPICE has open, so a child
/// started while another test class had an SPK loaded kept that file open until it ended, and that test could not delete
/// its SPK after unloading it (<c>PropagatedArcsTests</c>, <c>IOException</c>, CI of 2026-11-11 on Windows x64 only).
/// </para>
/// </remarks>
[Collection(nameof(ColdStartCollection))]
public class ColdStartTests
{
    // Children per mode and schedule, and the hold of thread B, ms. Before the fix (Linux x64, 4 cores, Release, 2026-11-11,
    // a child hung when it had not ended 3 s after its start): held with a hold of 200 ms, 40 / 40 children hung in direct
    // and tle, 30 / 30 in lagrange; race, 120 / 120 in direct and lagrange, 35 / 200 in tle (25 / 100 and 10 / 100 in two
    // series). After it, 0 hangs over 200 children of each mode and schedule. lagrange-window, with only the window overload
    // reverted (killed at 10 s): 3 / 3 held and 3 / 3 race children hung, while lagrange ended 2 / 2; with the fix, 0 / 20
    // of each. One held child per mode finds the defect; the race children add the reproduction of #623
    private const int HeldChildren = 3;
    private const int RaceChildren = 3;
    private const int HoldMilliseconds = 200;

    // A child that ends takes 0.11 to 0.40 s (Linux x64, 4 cores, 1200 children), 0.12 to 0.53 s with the 4 cores kept
    // busy by 4 other processes (180 children); a deadlock never ends. The limit is more than 100 times the slowest child,
    // for a slow CI machine
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromSeconds(60);
    private readonly ITestOutputHelper _output;

    public ColdStartTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("tle")]
    [InlineData("lagrange")]
    [InlineData("lagrange-window")]
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

    [Fact]
    public void TleText_IsCheckedBeforeAnyKernelRead()
    {
        // A child that loads no kernel (#675): a malformed TLE throws the error of its text (a wrong checksum, a short line
        // 1, two catalog numbers, a null line 1), each checked by the child. Before the fix the constructor built its Earth
        // first, which threw the kernel error, and CSPICE printed the banners of its failed reads on the standard output:
        // an empty output shows that no SPICE read was attempted
        string output = RunChild("tle-text");

        Assert.DoesNotContain("SPICE(", output);
        Assert.DoesNotContain("Toolkit", output);
        Assert.Equal(string.Empty, output.Trim());
    }

    [Fact]
    public void Tle_ReadBeforeItsKernels_ThrowsSpicesError_AndWorksOnceTheyAreLoaded()
    {
        // A child that loads its kernels after the first read (#711): the read throws SPICE's KERNELVARNOTFOUND of BODY399_GM
        // (before, "SGP4 failed ... zero state"), and the same TLE instance gives the state of tools/sgp4_reference once the
        // kernels are loaded (before, it kept the epoch of the failed read and threw for its life); each checked by the child
        string output = RunChild("tle-before-kernels");
        foreach (var line in output.Split('\n').Where(x => x.StartsWith("MEASURE", StringComparison.Ordinal)))
        {
            _output.WriteLine(line.TrimEnd('\r'));
        }
    }

    [Fact]
    public void SpiceErrors_ReachTheCallerOnly_NothingOnTheStandardOutput()
    {
        // A child whose calls SPICE fails (#727): a read past de440s, a CSPICE window search past it and an orientation read
        // without a CK, each throwing SPICE's message, checked by the child. Before, ActivateErrorManagement set erract_c to
        // RETURN only, and SPICE printed the banner of each error on the standard output, which corrupts the output of a host
        // application (a CLI's); its error output is now NONE (errprt_c, decided by S. Guillet, 2026-11-17)
        string output = RunChild("spice-errors-silent");

        Assert.DoesNotContain("SPICE(", output);
        Assert.DoesNotContain("Toolkit", output);
        Assert.Equal(string.Empty, output.Trim());
    }

    [Fact]
    public void FrameReads_RetainNoOrientationPerEpoch()
    {
        // A frame keeps at most the orientation of its last epoch (#541): in a fresh process, 100 000 reads of ITRF93 by name
        // and 5 000 of each managed frame retain nothing that grows with them, checked by the child against its bound.
        // Before the fix, ITRF93 retained 16.5 MB (the issue's probe) and each frame one orientation per epoch read
        string output = RunChild("frame-memory");
        foreach (var line in output.Split('\n').Where(x => x.StartsWith("MEASURE", StringComparison.Ordinal)))
        {
            _output.WriteLine(line.TrimEnd('\r'));
        }

        Assert.Equal(6, output.Split('\n').Count(x => x.StartsWith("MEASURE #541", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("two-body-first-propagate")]
    [InlineData("two-body-first-conic")]
    public void TwoBodyCall_AsTheFirstNativeCall_ThrowsSpicesError(string mode)
    {
        // A child that loads no kernel and makes no native call before the two-body call (#714), about a body built from custom
        // parameters, of a zero velocity: the call throws SPICE's error, checked by the child. Before the fix, SPICE's default
        // action (ABORT) ended the child with exit code 1
        RunChild(mode);
    }

    [Fact]
    public void ElementConversions_ReadNoProviderUnderTheSpiceLock()
    {
        // A child with a probe provider set as the global provider (#664): each element conversion and two-body propagation
        // of SpiceApi, every overload, makes no provider call under the SpiceApi lock, checked by the child. Before the fix
        // each built the observer of its result under the lock: 9 provider calls about the Earth, all under the lock
        string output = RunChild("conversions-lock");
        foreach (var line in output.Split('\n').Where(x => x.StartsWith("MEASURE", StringComparison.Ordinal)))
        {
            _output.WriteLine(line.TrimEnd('\r'));
        }
    }

    // Runs the child with the mode, and the schedule and hold when given; returns its standard output
    private static string RunChild(string mode, string schedule = null, string hold = null)
    {
        string[] arguments = schedule == null ? [mode] : hold == null ? [mode, schedule] : [mode, schedule, hold];
        return RunChild($"({mode}, {schedule})", arguments);
    }

    /// <summary>
    /// Runs the child process with the kernel directory and <paramref name="arguments"/>, requires it to end within
    /// <see cref="ChildTimeout"/> and exit 0, and returns its standard output.
    /// </summary>
    internal static string RunChild(string description, params string[] arguments)
    {
        return RunChildWithEnvironment(description, null, arguments);
    }

    /// <summary>
    /// <see cref="RunChild(string, string[])"/> with the variables of <paramref name="environment"/> set in the child's
    /// environment (<c>TZ</c> for <c>CcsdsLocalTimeTests</c>, #617).
    /// </summary>
    internal static string RunChildWithEnvironment(string description, System.Collections.Generic.IReadOnlyDictionary<string, string> environment,
        params string[] arguments)
    {
        // ColdStart/, or beside the tests when the build was given an output directory (-o), which the child is built into
        string child = Path.Combine(AppContext.BaseDirectory, "ColdStart", "IO.Astrodynamics.Tests.ColdStart.dll");
        if (!File.Exists(child))
        {
            child = Path.Combine(AppContext.BaseDirectory, "IO.Astrodynamics.Tests.ColdStart.dll");
        }

        Assert.True(File.Exists(child), $"The child process {child} is missing: the build of the test project copies it.");

        var start = new ProcessStartInfo(ChildProcess.DotnetHost())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (var variable in environment ?? new System.Collections.Generic.Dictionary<string, string>())
        {
            start.Environment[variable.Key] = variable.Value;
        }

        start.ArgumentList.Add(child);
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Data", "SolarSystem"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ChildTimeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail($"The child {description} did not end within {ChildTimeout.TotalSeconds} s: a thread holding the " +
                        $"SpiceApi lock waits for a type initializer that waits for the lock (#623). Output: {output.Result} {error.Result}");
        }

        // The parameterless wait also waits for the end of the redirected streams
        process.WaitForExit();
        Assert.True(process.ExitCode == 0,
            $"The child {description} exited {process.ExitCode}. Output: {output.Result} Error: {error.Result}");
        return output.Result;
    }
}

/// <summary>
/// The collection of <see cref="ColdStartTests"/>, run alone after the test classes that run in parallel: no other class
/// holds a kernel file while its child processes inherit the handles of the test host on Windows.
/// </summary>
[CollectionDefinition(nameof(ColdStartCollection), DisableParallelization = true)]
public class ColdStartCollection;
