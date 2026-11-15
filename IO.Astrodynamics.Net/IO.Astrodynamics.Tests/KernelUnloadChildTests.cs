// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The kernel scenarios of <see cref="KernelRegistryTests"/> that run in the child process
/// <c>IO.Astrodynamics.Tests.KernelUnload</c>: the deleted binary kernels, and the change of the working directory.
/// </summary>
/// <remarks>
/// In the collection of <see cref="ColdStartTests"/>, run alone after the classes that run in parallel: on Windows a child
/// inherits the handles of the test host, the kernel files other classes hold open included, and a class that deleted its
/// kernel while a child ran failed with "being used by another process" (PropagatedArcsTests on windows-latest, run
/// 43731955114 of #703).
/// </remarks>
[Collection(nameof(ColdStartCollection))]
public class KernelUnloadChildTests
{
    // A child that ends takes about 1 s (Linux x64, 3 cores); the limit is 60 times that, for a slow CI machine
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The scenarios in which a loaded binary kernel (PCK, SPK, CK) is deleted, each run in the child process
    /// <c>IO.Astrodynamics.Tests.KernelUnload</c>, which loads <c>Data/SolarSystem</c>, plays the scenario and exits 0 when
    /// it held (its scenarios and their measurements in its <c>Program</c>). Before the second round of #668, SPICE failed to
    /// open such a kernel again after the unload of another kernel of its kind, and the deleted kernel stayed open in SPICE
    /// for good, KEEPER's handle for it being 0: once its logical unit was released the PCK searches of every IAU frame failed
    /// to reconnect it (#673, measured with CSPICE N0067; CI of 725ee1a and a993265). The native load and unload, and those
    /// of the native writers, now close and unload every deleted SPK, CK, binary PCK and EK first; the scenarios still run in a child process, so that a
    /// native library without that unload leaves the test process sound.
    /// </summary>
    [NotOnWindowsTheory("A loaded binary kernel cannot be deleted on Windows, where SPICE keeps it open")]
    [InlineData("UnloadKernels_OfAFolderOfTwoPcks_WhoseSecondIsDeleted_RestoresThePrecedence")]
    [InlineData("UnloadKernels_OfAPck_WhenAnotherPckWasDeleted_ClosesIt_AndKeepsThePrecedence")]
    [InlineData("UnloadKernels_OfAMetaKernelOfAPckThenATextKernel_WhenAnotherPckWasDeleted_DoesNotThrow")]
    [InlineData("UnloadKernels_OfAFolderOfTwoPcks_BothDeleted_ClosesThem")]
    [InlineData("UnloadKernels_OfAnSpk_WhenAnotherSpkWasDeleted_ClosesIt")]
    [InlineData("LoadKernels_WhenAPckWasDeletedAndNeverUnloaded_ClosesIt")]
    [InlineData("UnloadKernels_OfACk_WhenAnotherCkWasDeleted_ClosesIt")]
    [InlineData("WriteEphemeris_WhenAnSpkWasDeleted_ClosesIt")]
    [InlineData("WriteOrientation_WhenACkWasDeleted_ClosesIt")]
    [InlineData("LoadKernels_WhenAnSpkOfALoadedFolderWasDeleted_ListsTheFolderUntilItsLastFileIsDropped")]
    [InlineData("SpacecraftWriteEphemeris_ReplacingAFile_WhenAnSpkWasDeleted_NoLongerListsIt")]
    [InlineData("WriteEphemeris_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending")]
    [InlineData("WriteOrientation_WhenACkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending")]
    public void DeletedBinaryKernel_InAChildProcess(string scenario) => RunKernelUnloadChild(scenario);

    /// <summary>
    /// A meta-kernel that names its SPK relative to the working directory of its load, then a change of the working directory:
    /// the next load keeps the SPK, which still exists, readable (review of the second round of #668). Run in the child process
    /// on every platform: it changes the working directory of its process.
    /// </summary>
    [Fact]
    public void LoadKernels_AfterTheWorkingDirectoryChanged_KeepsAnSpkAMetaKernelNamesRelatively_InAChildProcess() =>
        RunKernelUnloadChild("LoadKernels_AfterTheWorkingDirectoryChanged_KeepsAnSpkAMetaKernelNamesRelatively");

    private static void RunKernelUnloadChild(string scenario)
    {
        string child = ChildProcess.AssemblyPath("KernelUnload", "IO.Astrodynamics.Tests.KernelUnload.dll");
        Assert.True(File.Exists(child), $"The child process {child} is missing: the build of the test project copies it.");

        var result = ChildProcess.Run(child, ChildTimeout, Path.Combine(AppContext.BaseDirectory, "Data", "SolarSystem"), scenario);

        Assert.True(result.HasValue, $"The child ({scenario}) did not end within {ChildTimeout.TotalSeconds} s.");
        Assert.True(result.Value.ExitCode == 0,
            $"The child ({scenario}) exited {result.Value.ExitCode}. Output: {result.Value.Output} Error: {result.Value.Error}");
    }
}
