// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Text;

namespace IO.Astrodynamics.Kernels;

/// <summary>
/// The rule of the kernel variable names that the frame kernel writers build from a name (#676): the
/// <c>FRAME_&lt;SPACECRAFT&gt;_FRAME</c> of a spacecraft, the <c>FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;</c> of an
/// instrument and the <c>FRAME_&lt;SITE&gt;_TOPO</c> of a site. The instrument kernel writers check the variable of the
/// instrument FK, whose frame name their <c>INS&lt;id&gt;_FOV_FRAME</c> string holds.
/// </summary>
/// <remarks>
/// Kernel Required Reading, "Variable Name Rules": a variable name can include any printable character except a space, a
/// comma, an open or close parenthesis, an equal sign and a TAB, and must not exceed 32 characters; SPICE counts them in
/// the bytes of the file, which the writers write in UTF-8 (measured on CSPICE N0067: a 13-character name of 'É', 38
/// bytes, fails with SPICE(BADVARNAME), 'ABCDEFGHIJKLMNOPQRÉ', 32 bytes, loads). A single quote ends the string values
/// the name is also written in ("String values": a quote inside a string is doubled), and SPICE rejects it with
/// SPICE(BADVARASSIGN) (measured). The rule is checked on the exact string written, before the file is opened.
/// </remarks>
internal static class TextKernelNames
{
    /// <summary>The longest kernel variable name, in bytes.</summary>
    internal const int MaximumVariableNameBytes = 32;

    /// <summary>
    /// Throws when <paramref name="variableName"/> is no kernel variable name SPICE can read.
    /// </summary>
    /// <param name="variableName">The variable name the writer builds, as it is written.</param>
    /// <param name="owner">What the name belongs to, for the message (e.g. "the spacecraft 'Demo Sat'").</param>
    /// <param name="kernel">The kernel the writer writes, for the message.</param>
    /// <exception cref="InvalidOperationException">The name holds a whitespace or control character, a quote, a
    /// parenthesis, a comma or '=', or exceeds 32 bytes in UTF-8.</exception>
    internal static void Check(string variableName, string owner, string kernel = "frame kernel")
    {
        foreach (var c in variableName)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || c is '\'' or '(' or ')' or ',' or '=')
            {
                throw new InvalidOperationException(
                    $"The {kernel} of {owner} cannot be written: the frame kernel variable {variableName} holds '{c}' (U+{(int)c:X4}), which SPICE does not read in a variable name (a blank, a tab or another whitespace or control character, a quote, a parenthesis, a comma or '='). Rename it.");
            }
        }

        int bytes = Encoding.UTF8.GetByteCount(variableName);
        if (bytes > MaximumVariableNameBytes)
        {
            throw new InvalidOperationException(
                $"The {kernel} of {owner} cannot be written: the frame kernel variable {variableName} is {bytes} bytes long in UTF-8, and SPICE reads variable names of {MaximumVariableNameBytes} bytes at most. Shorten the name.");
        }
    }
}
