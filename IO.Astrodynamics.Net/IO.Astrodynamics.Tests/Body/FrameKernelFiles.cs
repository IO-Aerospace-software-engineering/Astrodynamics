// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The frame kernels the writers of the spacecraft, instrument and site frames write for the tests of their names (#676):
/// a new file under <c>Data/User/FrameKernelNames</c>, and its load by CSPICE (<c>furnsh_c</c>), unloaded and deleted in
/// <c>finally</c>.
/// </summary>
internal static class FrameKernelFiles
{
    private static readonly string Directory = Path.Combine(Constants.OutputPath.FullName, "FrameKernelNames");

    /// <summary>
    /// A file that does not exist yet, unique to the call: 8 hexadecimal digits after the stem, so that the path stays well
    /// within the 255 characters of <c>furnsh_c</c>.
    /// </summary>
    internal static FileInfo NewFile(string stem, string extension = ".tf")
    {
        System.IO.Directory.CreateDirectory(Directory);
        var file = new FileInfo(Path.Combine(Directory, $"{stem}_{Guid.NewGuid().ToString("N")[..8]}{extension}"));
        Assert.False(file.Exists);
        return file;
    }

    /// <summary>Deletes the kernel, if it was written.</summary>
    internal static void Delete(FileInfo kernel)
    {
        kernel.Refresh();
        if (kernel.Exists)
        {
            kernel.Delete();
        }
    }

    /// <summary>
    /// Loads the kernel in SPICE (an error of SPICE throws), runs <paramref name="whileLoaded"/>, unloads it and deletes it.
    /// </summary>
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
            Delete(kernel);
        }
    }
}
