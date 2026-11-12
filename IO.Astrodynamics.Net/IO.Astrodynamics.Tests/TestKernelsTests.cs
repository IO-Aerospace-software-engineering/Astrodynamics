// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// <see cref="SharedKernels"/> on empty temporary directories, which <see cref="SpiceAPI.LoadKernels"/> records without a
/// native call: loaded once, loaded again after a removal, which is observed, a reload by another caller observed, a first
/// load by another caller left at no load; then the solar system kernels of <see cref="TestKernels"/>, loaded once.
/// </summary>
public sealed class TestKernelsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("test-kernels-");
    private readonly DirectoryInfo _set;
    private readonly DirectoryInfo _anchor;
    private readonly DirectoryInfo _marker;
    private readonly SharedKernels _kernels;

    public TestKernelsTests()
    {
        _set = _root.CreateSubdirectory("set");
        _marker = _root.CreateSubdirectory("marker");
        _anchor = new DirectoryInfo(Path.Combine(_root.FullName, "anchor"));
        _kernels = new SharedKernels(_set, _anchor);
    }

    public void Dispose()
    {
        foreach (var directory in new[] { _set, _anchor, _marker })
        {
            SpiceAPI.Instance.UnloadKernels(new DirectoryInfo(directory.FullName));
        }

        _root.Delete(true);
    }

    private static string[] Loaded() => SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName).ToArray();

    [Fact]
    public void Ensure_Twice_LoadsOnce()
    {
        // Act: another kernel loaded between the two calls
        _kernels.Ensure();
        SpiceAPI.Instance.LoadKernels(_marker);
        _kernels.Ensure();

        // Assert: one entry, before its anchor and the marker, never reloaded
        var loaded = Loaded();
        Assert.Equal(1, _kernels.Loads);
        Assert.Single(loaded, k => k == _set.FullName);
        Assert.True(Array.IndexOf(loaded, _set.FullName) < Array.IndexOf(loaded, _anchor.FullName));
        Assert.True(Array.IndexOf(loaded, _anchor.FullName) < Array.IndexOf(loaded, _marker.FullName));
        Assert.False(_kernels.ReloadObserved);
        Assert.False(_kernels.RemovalObserved);
        Assert.True(_anchor.Exists);
    }

    [Fact]
    public void Ensure_AfterARemoval_LoadsAgain()
    {
        // Arrange
        _kernels.Ensure();
        SpiceAPI.Instance.UnloadKernels(_set);
        Assert.DoesNotContain(_set.FullName, Loaded());

        // Act
        _kernels.Ensure();

        // Assert: loaded again, the anchor after it, the removal observed, no reload
        var loaded = Loaded();
        Assert.Equal(2, _kernels.Loads);
        Assert.Single(loaded, k => k == _set.FullName);
        Assert.Single(loaded, k => k == _anchor.FullName);
        Assert.True(Array.IndexOf(loaded, _set.FullName) < Array.IndexOf(loaded, _anchor.FullName));
        Assert.True(_kernels.RemovalObserved);
        Assert.False(_kernels.ReloadObserved);
    }

    [Fact]
    public void Ensure_AfterARemovalOfBothAndALoadByAnotherCaller_ObservesTheRemoval()
    {
        // Arrange: what a ClearKernels followed by the former pattern leaves, the folder loaded by another caller and no anchor
        _kernels.Ensure();
        SpiceAPI.Instance.UnloadKernels(_set);
        SpiceAPI.Instance.UnloadKernels(_anchor);
        SpiceAPI.Instance.LoadKernels(_set);

        // Act
        _kernels.Ensure();

        // Assert: the anchor loaded again after the folder, the folder not loaded by this instance again
        var loaded = Loaded();
        Assert.True(_kernels.RemovalObserved);
        Assert.False(_kernels.ReloadObserved);
        Assert.Equal(1, _kernels.Loads);
        Assert.True(Array.IndexOf(loaded, _set.FullName) < Array.IndexOf(loaded, _anchor.FullName));
    }

    [Fact]
    public void Ensure_AfterAFirstLoadByAnotherCaller_LoadsNothing()
    {
        // Arrange: the folder loaded before the first call
        SpiceAPI.Instance.LoadKernels(_set);

        // Act
        _kernels.Ensure();
        _kernels.Ensure();

        // Assert: no load of its own, which the guard reads, the anchor after the folder, no flag
        var loaded = Loaded();
        Assert.Equal(0, _kernels.Loads);
        Assert.True(Array.IndexOf(loaded, _set.FullName) < Array.IndexOf(loaded, _anchor.FullName));
        Assert.False(_kernels.ReloadObserved);
        Assert.False(_kernels.RemovalObserved);
    }

    [Fact]
    public void Ensure_AfterAReloadByAnotherCaller_ObservesIt()
    {
        // Arrange: the former pattern, a load of the folder already loaded, which moves it after the anchor
        _kernels.Ensure();
        SpiceAPI.Instance.LoadKernels(_set);

        // Act
        _kernels.Ensure();

        // Assert
        Assert.True(_kernels.ReloadObserved);
        Assert.False(_kernels.RemovalObserved);
        Assert.Equal(1, _kernels.Loads);

        // A removal and a restore afterwards keep the observation, and observe the removal
        SpiceAPI.Instance.UnloadKernels(_set);
        _kernels.Ensure();
        Assert.True(_kernels.ReloadObserved);
        Assert.True(_kernels.RemovalObserved);
        Assert.Equal(2, _kernels.Loads);
    }

    [Fact]
    public void Ensure_OfAMissingFolder_Throws()
    {
        // Arrange
        var missing = new SharedKernels(new DirectoryInfo(Path.Combine(_root.FullName, "missing")), _anchor);

        // Act, Assert: nothing recorded, so that the next call tries again
        Assert.Throws<DirectoryNotFoundException>(missing.Ensure);
        Assert.Equal(0, missing.Loads);
        Assert.False(missing.RemovalObserved);
        Assert.DoesNotContain(missing.Path.FullName, Loaded());
    }

    [Fact]
    public void Constructor_WithoutPaths_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SharedKernels(null, _anchor));
        Assert.Throws<ArgumentNullException>(() => new SharedKernels(_set, null));
    }

    [Fact]
    public void EnsureSolarSystem_Twice_LoadsOnce()
    {
        // Act
        TestKernels.EnsureSolarSystem();
        int loads = TestKernels.SolarSystem.Loads;
        TestKernels.EnsureSolarSystem();

        // Assert: one entry, before its anchor, not loaded again
        var loaded = Loaded();
        Assert.Equal(loads, TestKernels.SolarSystem.Loads);
        Assert.Single(loaded, k => k == Constants.SolarSystemKernelPath.FullName);
        Assert.True(Array.IndexOf(loaded, TestKernels.SolarSystem.Path.FullName) < Array.IndexOf(loaded, TestKernels.SolarSystem.Anchor.FullName));
    }
}
