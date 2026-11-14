// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Text;

namespace IO.Astrodynamics.Kernels;

/// <summary>
/// The length of a kernel path SPICE can take (#673): "Kernel file names, including path specifications, must not exceed
/// 255 characters" (NAIF, SPICE Kernel Required Reading, "Recommendations on Kernel File Naming"). FURNSH rejects a longer
/// name with <c>SPICE(FILENAMETOOLONG)</c>, and the native writers of CSPICE write under the name truncated to 255, then
/// fail (measured with CSPICE N0067).
/// </summary>
internal static class KernelPath
{
    /// <summary>The longest kernel name SPICE stores, in the bytes it receives.</summary>
    internal const int MaximumLength = 255;

    /// <summary>
    /// Throws when <paramref name="fullPath"/> is longer than SPICE takes. The length is that of the bytes the P/Invoke
    /// marshaller hands to CSPICE, UTF-8 on Linux and macOS; on Windows, where it hands the ANSI code page, an ASCII path
    /// counts the same and another one may be rejected a few bytes early.
    /// </summary>
    /// <param name="fullPath">The full path of the kernel.</param>
    /// <param name="paramName">The parameter of the caller that gives the path.</param>
    /// <exception cref="ArgumentException">The path is longer than <see cref="MaximumLength"/> bytes.</exception>
    internal static void EnsureLoadable(string fullPath, string paramName)
    {
        int length = Encoding.UTF8.GetByteCount(fullPath);
        if (length > MaximumLength)
        {
            throw new ArgumentException(
                $"The kernel path {fullPath} has {length} bytes: SPICE takes a kernel path of at most {MaximumLength} (NAIF, SPICE Kernel Required Reading). Choose a shorter directory.",
                paramName);
        }
    }
}
