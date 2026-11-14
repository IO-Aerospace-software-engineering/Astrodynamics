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
    private const string Pointer = SuiteDataFile.LfsPointerHeader + "v1\noid sha256:a3e65d8acf\nsize 5153792\n";
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

        // Assert: moved by the version spread of the two files (1.82e-10 rad at this epoch), then restored bit for bit
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
    public void AKernelInsideASetAlreadyLoaded_IsAnError_AndTheSetStaysLoaded()
    {
        // Arrange: data/ loaded as a whole, as the runner loads its set. SpiceAPI reloads the set for a path inside it (paths
        // compared by components, #673), so the suite's bpc is never loaded for the case alone
        var data = new DirectoryInfo(Path.Combine(_directory, "data"));
        SpiceAPI.Instance.LoadKernels(data);
        try
        {
            var loadedBefore = SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName).ToArray();

            // Act
            var exception = Assert.IsType<InvalidOperationException>(Construct(new[] { Kernel }));

            // Assert: the set is listed as before, the kernel not on its own
            Assert.Contains(Kernel, exception.Message);
            Assert.Equal(loadedBefore, SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName));
            Assert.Contains(data.FullName, loadedBefore);
            Assert.DoesNotContain(Path.Combine(data.FullName, Kernel), loadedBefore);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(data);
        }
    }

    [Fact]
    public void AKernelSpiceCannotLoad_IsAnError_AndTheKernelsLoadedBeforeItAreUnloaded()
    {
        // Arrange: a binary PCK cut after its ID word, which SPICE fails to read and does not hold (measured with CSPICE N0067)
        File.WriteAllBytes(Path.Combine(_directory, "data", "cut.bpc"), "DAF/PCK "u8.ToArray().Concat(new byte[100]).ToArray());
        var before = Itrf93();
        var loadedBefore = SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName).ToArray();

        // Act
        var exception = Assert.IsType<InvalidOperationException>(Construct(new[] { Kernel, "cut.bpc" }));

        // Assert: the suite's bpc, loaded first, is unloaded, and the runner's orientation restored bit for bit
        Assert.Contains("cut.bpc", exception.Message);
        Assert.Equal(loadedBefore, SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName));
        Assert.Equal(before.Rotation, Itrf93().Rotation);
    }

    // The exception of the constructor, and the kernels of a constructor that did not throw unloaded, so that no case kernel
    // stays loaded for the next tests of the process
    private Exception Construct(string[] kernels)
    {
        CaseKernels created = null;
        var exception = Record.Exception(() => created = new CaseKernels(_directory, kernels));
        created?.Dispose();
        return exception;
    }
}
