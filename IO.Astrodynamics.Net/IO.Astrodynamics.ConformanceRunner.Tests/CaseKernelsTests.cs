// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The kernels of a frame case, loaded from the suite's <c>data/</c> on top of the runner's set and unloaded after
/// the case: here the suite's Earth binary PCK, which the fixture ships because it differs from the runner's.
/// </summary>
public sealed class CaseKernelsTests : IDisposable
{
    private const string Kernel = "earth_latest_high_prec.bpc";
    private const string Pointer = SuiteDataFile.LfsPointerHeader + "v1\noid sha256:3f6ce47a0a\nsize 4939776\n";
    private static readonly Time Epoch = new(new DateTime(2021, 3, 20, 12, 0, 0), TimeFrame.TDBFrame);

    private readonly string _directory;

    public CaseKernelsTests()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(Lot1Fixture.KernelsPath));
        _directory = Directory.CreateTempSubdirectory("case-kernels-").FullName;
        Directory.CreateDirectory(Path.Combine(_directory, "data"));
        Assert.True(File.Exists(Path.Combine(Lot1Fixture.Root, "data", Kernel)),
            "The fixture ships the suite's Earth binary PCK, which differs from the runner's (design of #458, section 5)");
        File.Copy(Path.Combine(Lot1Fixture.Root, "data", Kernel), Path.Combine(_directory, "data", Kernel));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    private static StateOrientation Itrf93() => Frame.ICRF.ToFrame(new Frame("ITRF93"), Epoch);

    [Fact]
    public void TheSuiteBpc_MovesItrf93WhileLoaded_AndTheRunnersIsRestoredExactlyAfterDispose()
    {
        // Arrange
        var before = Itrf93();
        var loadedBefore = SpiceAPI.Instance.GetLoadedKernels().Count();

        // Act
        StateOrientation inside;
        using (var kernels = new CaseKernels(_directory, new[] { Kernel }))
        {
            inside = Itrf93();
            Assert.Equal(Path.Combine(_directory, "data", Kernel), kernels.Files.Single().FullName);
        }

        var after = Itrf93();

        // Assert: moved by the version spread of the two files (4.68e-12 rad at this epoch), then restored bit for bit
        Assert.NotEqual(before.Rotation, inside.Rotation);
        Assert.Equal(before.Rotation, after.Rotation);
        Assert.Equal(before.AngularVelocity, after.AngularVelocity);
        Assert.Equal(loadedBefore, SpiceAPI.Instance.GetLoadedKernels().Count());
    }

    [Fact]
    public void NoKernel_LoadsNothing()
    {
        using var none = new CaseKernels(_directory, Array.Empty<string>());
        using var nullList = new CaseKernels(_directory, null);

        Assert.Empty(none.Files);
        Assert.Empty(nullList.Files);
    }

    [Fact]
    public void AMissingFile_IsAnError_AndNothingIsLoaded()
    {
        var loadedBefore = SpiceAPI.Instance.GetLoadedKernels().ToArray();

        var exception = Assert.Throws<InvalidOperationException>(() => new CaseKernels(_directory, new[] { Kernel, "missing.bpc" }));

        Assert.Contains("missing.bpc", exception.Message);
        Assert.Equal(loadedBefore.Select(k => k.FullName), SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName));
    }

    [Fact]
    public void AGitLfsPointer_IsAnError_AndNothingIsLoaded()
    {
        File.WriteAllText(Path.Combine(_directory, "data", "pointer.bpc"), Pointer);
        var loadedBefore = SpiceAPI.Instance.GetLoadedKernels().ToArray();

        var exception = Assert.Throws<InvalidOperationException>(() => new CaseKernels(_directory, new[] { Kernel, "pointer.bpc" }));

        Assert.Contains("git lfs pull", exception.Message);
        Assert.Equal(loadedBefore.Select(k => k.FullName), SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName));
    }

    [Theory]
    [InlineData("../earth_latest_high_prec.bpc")]
    [InlineData("data/earth_latest_high_prec.bpc")]
    [InlineData(".hidden.bpc")]
    [InlineData("")]
    [InlineData(null)]
    public void ANameThatIsNotAFileNameOfData_IsRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new CaseKernels(_directory, new[] { name }));
    }

    [Fact]
    public void AKernelInsideASetAlreadyLoaded_IsAnError_AndTheKernelsLoadedBeforeItAreUnloaded()
    {
        // Arrange: a directory data/inside/ loaded as a whole, as the runner loads its set. SpiceAPI takes a path whose
        // name contains that of a loaded set for a part of it and reloads the set instead, so data/inside.bpc is never
        // loaded for the case
        var data = Path.Combine(_directory, "data");
        var set = new DirectoryInfo(Path.Combine(data, "inside"));
        set.Create();
        File.Copy(Path.Combine(data, Kernel), Path.Combine(set.FullName, Kernel));
        File.Copy(Path.Combine(data, Kernel), Path.Combine(data, "inside.bpc"));
        SpiceAPI.Instance.LoadKernels(set);
        try
        {
            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => new CaseKernels(_directory, new[] { Kernel, "inside.bpc" }));

            // Assert: the suite's bpc, loaded first, is unloaded
            Assert.Contains("inside.bpc", exception.Message);
            Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(), k => k.FullName == Path.Combine(data, Kernel));
            Assert.Contains(SpiceAPI.Instance.GetLoadedKernels(), k => k.FullName == set.FullName);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(set);
        }
    }
}
