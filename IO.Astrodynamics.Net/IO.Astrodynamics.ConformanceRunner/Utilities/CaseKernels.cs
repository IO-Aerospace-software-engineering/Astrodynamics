using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// The kernels of a frame case (<c>inputs.kernels</c>, spec.md section 2.1), loaded from the suite's <c>data/</c> on
/// top of the runner's set for the case alone, and unloaded by <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// A kernel loaded last takes precedence in SPICE, so the case computes with the orientation data its golden was made
/// with: the suite's Earth binary PCK differs from the runner's by up to 1.2e-11 rad before mid-2025. Unloading it
/// restores the runner's set exactly. The SPICE pool is process-global: this is safe because the runner runs its cases
/// one after the other.
/// </remarks>
public sealed class CaseKernels : IDisposable
{
    private readonly List<FileInfo> _loaded = new();

    /// <summary>
    /// Loads <c>&lt;suite&gt;/data/&lt;name&gt;</c> for each name, in their order.
    /// </summary>
    /// <param name="suitePath">Root of the conformance suite.</param>
    /// <param name="kernels">File names of <c>data/</c>; none for a case that needs no kernel.</param>
    /// <exception cref="ArgumentException">A name is not a file name of <c>data/</c>.</exception>
    /// <exception cref="InvalidOperationException">A file is missing, is a Git LFS pointer, or could not be loaded on
    /// its own; nothing stays loaded.</exception>
    public CaseKernels(string suitePath, IEnumerable<string> kernels)
    {
        var files = (kernels ?? Enumerable.Empty<string>()).Select(name => DataFile(suitePath, name)).ToList();

        // Every file is checked before any is loaded
        foreach (var file in files)
        {
            SuiteDataFile.EnsureContent(file.FullName);
        }

        try
        {
            foreach (var file in files)
            {
                SpiceAPI.Instance.LoadKernels(file);

                // A path inside a set already loaded (the runner's kernel directory) reloads that set instead
                if (!SpiceAPI.Instance.GetLoadedKernels().Any(k => k.FullName == file.FullName))
                {
                    throw new InvalidOperationException(
                        $"The kernel {file.FullName} lies in a kernel set the runner has loaded: it cannot be loaded for the case alone.");
                }

                _loaded.Add(file);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The files loaded for the case, in their order.</summary>
    public IReadOnlyList<FileInfo> Files => _loaded;

    /// <summary>
    /// Unloads the files of the case, the last loaded first.
    /// </summary>
    public void Dispose()
    {
        for (int i = _loaded.Count - 1; i >= 0; i--)
        {
            SpiceAPI.Instance.UnloadKernels(_loaded[i]);
        }

        _loaded.Clear();
    }

    private static FileInfo DataFile(string suitePath, string name)
    {
        if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.StartsWith('.'))
        {
            throw new ArgumentException($"The kernel '{name}' is not a file name of the suite's data/.", nameof(name));
        }

        return new FileInfo(Path.Combine(suitePath, "data", name));
    }
}
