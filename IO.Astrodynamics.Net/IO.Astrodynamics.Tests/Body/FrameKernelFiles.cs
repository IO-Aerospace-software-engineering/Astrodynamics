// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The frame kernels the writers of the spacecraft, instrument and site frames write for the tests of their names (#676):
/// a new file under <c>Data/User/FrameKernelNames</c>, and its load by CSPICE (<c>furnsh_c</c>), unloaded in
/// <c>finally</c>.
/// </summary>
internal static class FrameKernelFiles
{
    private static readonly string Directory = Path.Combine(Constants.OutputPath.FullName, "FrameKernelNames");

    /// <summary>A file that does not exist yet, unique to the call.</summary>
    internal static FileInfo NewFile(string stem)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var file = new FileInfo(Path.Combine(Directory, $"{stem}_{Guid.NewGuid():N}.tf"));
        Assert.False(file.Exists);
        return file;
    }

    /// <summary>Loads the kernel in SPICE (an error of SPICE throws), runs <paramref name="whileLoaded"/>, and unloads it.</summary>
    internal static void Load(FileInfo kernel, Action whileLoaded = null)
    {
        // FileInfo caches Exists, and LoadKernels loads nothing for a path it believes absent
        kernel.Refresh();
        Assert.True(kernel.Exists, kernel.FullName);
        SpiceAPI.Instance.LoadKernels(kernel);
        try
        {
            whileLoaded?.Invoke();
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(kernel);
        }
    }
}
