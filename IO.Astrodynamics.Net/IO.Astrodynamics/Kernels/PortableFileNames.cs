// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;

namespace IO.Astrodynamics.Kernels;

/// <summary>
/// The rule of the names the library makes into file and directory names (#799(a), decisions 74 and 105 of #668, decided
/// by S. Guillet, 2026-11-23 and 2026-11-25): the spacecraft, instrument, site and star names of the kernels it writes, and
/// the export directory <c>&lt;Mission&gt;_&lt;Scenario&gt;</c> of a Cosmographia export. A name is one portable path
/// component: not empty, none of <c>/ \ : * ? " &lt; &gt; |</c> nor a control character, not <c>.</c> or <c>..</c>, no
/// trailing dot or space, and not a reserved device name of Windows (CON, PRN, AUX, NUL, COM1 to COM9, LPT1 to LPT9, in any
/// case, alone or before a first dot), so the same name writes the same files on Linux, macOS and Windows and never leaves
/// the directory it is written into. Checked where the paths are built, before anything is deleted or written, as
/// <see cref="TextKernelNames"/> for the kernel variables; the constructors of the objects do not check it.
/// </summary>
internal static class PortableFileNames
{
    // The characters Windows refuses in a file name; '/' and '\' separate directories
    private const string ForbiddenCharacters = "/\\:*?\"<>|";

    // The device names Windows reserves, alone or followed by an extension
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3",
        "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// Throws when <paramref name="name"/> is not a portable file name.
    /// </summary>
    /// <param name="name">The name, as the path holds it.</param>
    /// <param name="owner">What the name belongs to, for the message (e.g. "the spacecraft 'Demo Sat'").</param>
    /// <exception cref="InvalidOperationException">The name is empty, holds one of <c>/ \ : * ? " &lt; &gt; |</c> or a
    /// control character, is <c>.</c> or <c>..</c>, ends with a dot or a space, or is a reserved device name of Windows.</exception>
    internal static void Check(string name, string owner)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw Refused(owner, "it is empty");
        }

        foreach (var c in name)
        {
            if (ForbiddenCharacters.Contains(c) || char.IsControl(c))
            {
                throw Refused(owner, $"it holds '{c}' (U+{(int)c:X4})");
            }
        }

        if (name is "." or "..")
        {
            throw Refused(owner, "it is '.' or '..', a directory");
        }

        if (name[^1] is '.' or ' ')
        {
            throw Refused(owner, name[^1] == '.' ? "it ends with a dot" : "it ends with a space");
        }

        int dot = name.IndexOf('.');
        var stem = dot < 0 ? name : name[..dot];
        var reserved = ReservedNames.FirstOrDefault(x => string.Equals(x, stem, StringComparison.OrdinalIgnoreCase));
        if (reserved != null)
        {
            throw Refused(owner, $"it is the device name {reserved}, which Windows reserves alone or before an extension");
        }
    }

    private static InvalidOperationException Refused(string owner, string reason) => new(
        $"The files of {owner} cannot be written: its name is not a portable file name, {reason}. A file name is not empty, holds none of / \\ : * ? \" < > | nor a control character, is not . or .., ends with no dot or space and is not a device name of Windows (CON, PRN, AUX, NUL, COM1 to COM9, LPT1 to LPT9). Rename it.");
}
