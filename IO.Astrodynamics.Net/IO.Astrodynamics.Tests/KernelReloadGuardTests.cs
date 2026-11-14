// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// No test of the process reloaded the solar system kernels that <see cref="TestKernels"/> loads once, whatever the
/// spelling of the path (the constant, another <c>DirectoryInfo</c> of the folder, a file inside it), loaded a parent
/// of the folder, removed the folder or its anchor, or loaded the folder before the helper; and no kernel still loaded
/// shadows it (<see cref="KernelShadowing"/>): the class runs alone, after the test classes that run in parallel
/// (<see cref="KernelReloadGuardCollection"/>).
/// </summary>
/// <remarks>
/// A reload moves the folder after its anchor (<see cref="SharedKernels.ReloadObserved"/>, kept by every later call of
/// <see cref="TestKernels.EnsureSolarSystem"/>); a removal, a <see cref="SpiceAPI.ClearKernels"/> included, leaves the
/// folder or its anchor out of the loaded kernels at the next call (<see cref="SharedKernels.RemovalObserved"/>); a first
/// load by another caller leaves the helper's loads at 0; the load of a parent replaces the folder's entry by the
/// parent's; the unload of a file inside the folder replaces the folder's entry by its other files (#673). In a filtered
/// run, a class whose tests all ran before the first call of the helper escapes.
/// </remarks>
[Collection(nameof(KernelReloadGuardCollection))]
public class KernelReloadGuardTests
{
    [Fact]
    public void NoTestReloadedTheSolarSystemKernels()
    {
        // Arrange: the last state, and the class alone still checks something
        TestKernels.EnsureSolarSystem();

        // Act
        var loaded = SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName).ToArray();
        string list = string.Join(Environment.NewLine, loaded);
        var solarSystem = TestKernels.SolarSystem;

        // Assert
        Assert.False(solarSystem.ReloadObserved, $"A test loaded {solarSystem.Path.FullName} again: call TestKernels.EnsureSolarSystem(). Loaded:{Environment.NewLine}{list}");
        Assert.False(solarSystem.RemovalObserved,
            $"A test unloaded {solarSystem.Path.FullName}, a parent of it or its anchor, or cleared the kernels: run it in a child process. Loaded:{Environment.NewLine}{list}");
        Assert.True(solarSystem.Loads == 1,
            $"TestKernels loaded {solarSystem.Path.FullName} {solarSystem.Loads} times: another caller loaded it first, or a test removed it. Loaded:{Environment.NewLine}{list}");
        Assert.True(loaded.Count(k => k == solarSystem.Path.FullName) == 1, $"{solarSystem.Path.FullName} is not loaded once. Loaded:{Environment.NewLine}{list}");
        Assert.True(Array.IndexOf(loaded, solarSystem.Path.FullName) < Array.IndexOf(loaded, solarSystem.Anchor.FullName),
            $"{solarSystem.Path.FullName} is after its anchor. Loaded:{Environment.NewLine}{list}");
    }

    [Fact]
    public void NoLoadedKernelShadowsTheSolarSystemKernels()
    {
        // Arrange: the kernels still loaded, those the tests wrote included, which stay above Data/SolarSystem; a file
        // deleted after its load can no longer be read
        TestKernels.EnsureSolarSystem();
        var loaded = SpiceAPI.Instance.GetLoadedKernels()
            .Where(k => k.FullName != TestKernels.SolarSystem.Path.FullName && (File.Exists(k.FullName) || Directory.Exists(k.FullName)))
            .ToArray();

        // Act
        var conflicts = KernelShadowing.Find(loaded, KernelShadowing.SolarSystem);

        // Assert
        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }
}

/// <summary>
/// The collection of <see cref="KernelReloadGuardTests"/>, run alone after the test classes that run in parallel.
/// </summary>
[CollectionDefinition(nameof(KernelReloadGuardCollection), DisableParallelization = true)]
public class KernelReloadGuardCollection;
