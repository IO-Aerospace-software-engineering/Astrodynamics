using System;
using System.IO;
using System.Text;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// The data files a case takes from the conformance suite, which keeps its kernels in Git LFS (#425): a clone without
/// LFS holds a pointer of about 130 bytes in place of each file, from which SPICE loads nothing, so that the case
/// would come out as FAIL, as if the library were wrong.
/// </summary>
public static class SuiteDataFile
{
    /// <summary>
    /// The first line of a Git LFS pointer file.
    /// </summary>
    public const string LfsPointerHeader = "version https://git-lfs.github.com/spec/";

    /// <summary>
    /// Checks that <paramref name="path"/> holds the content of the file, not a Git LFS pointer. Only the pointer is
    /// detected, so that a valid file of a format this check does not know is never rejected.
    /// </summary>
    /// <param name="path">Path of the data file.</param>
    /// <exception cref="InvalidOperationException">The file is missing or is a Git LFS pointer; the runner reports the
    /// case as ERROR with this message.</exception>
    public static void EnsureContent(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The data file {path} does not exist.");
        }

        var header = new byte[LfsPointerHeader.Length];
        int read;
        // Shared for reading and writing: on Windows, a kernel SPICE has already opened cannot be opened otherwise
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        }

        if (read == header.Length && Encoding.ASCII.GetString(header) == LfsPointerHeader)
        {
            throw new InvalidOperationException(
                $"The data file {path} is a Git LFS pointer, not its content: run 'git lfs install && git lfs pull' in the conformance suite.");
        }
    }
}
