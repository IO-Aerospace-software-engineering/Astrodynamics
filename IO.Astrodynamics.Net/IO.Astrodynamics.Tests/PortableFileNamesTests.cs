// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Kernels;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The portable file-name rule of the names the library makes into paths (internal <see cref="PortableFileNames"/>,
/// #799(a), decisions 74 and 105 of #668): one path component that writes the same file on Linux, macOS and Windows.
/// </summary>
public class PortableFileNamesTests
{
    [Theory]
    [InlineData("", "it is empty")]
    [InlineData(null, "it is empty")]
    [InlineData("a/b", "'/' (U+002F)")]
    [InlineData("/rooted", "'/' (U+002F)")]
    [InlineData("a\\b", "'\\' (U+005C)")]
    [InlineData("C:", "':' (U+003A)")]
    [InlineData("a*", "'*' (U+002A)")]
    [InlineData("a?", "'?' (U+003F)")]
    [InlineData("a\"b", "'\"' (U+0022)")]
    [InlineData("a<b", "'<' (U+003C)")]
    [InlineData("a>b", "'>' (U+003E)")]
    [InlineData("a|b", "'|' (U+007C)")]
    [InlineData("a\tb", "(U+0009)")]
    [InlineData("a\u007Fb", "(U+007F)")]
    [InlineData("a\u0085b", "(U+0085)")]
    [InlineData(".", "'.' or '..'")]
    [InlineData("..", "'.' or '..'")]
    [InlineData("name.", "ends with a dot")]
    [InlineData("name ", "ends with a space")]
    [InlineData("con", "device name CON")]
    [InlineData("CON", "device name CON")]
    [InlineData("Prn", "device name PRN")]
    [InlineData("aux", "device name AUX")]
    [InlineData("NUL.txt", "device name NUL")]
    [InlineData("COM1", "device name COM1")]
    [InlineData("com9.tf", "device name COM9")]
    [InlineData("LPT1", "device name LPT1")]
    [InlineData("lpt9.a.b", "device name LPT9")]
    public void Check_RefusesANameThatIsNotAPortableFileName(string name, string reason)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => PortableFileNames.Check(name, "the spacecraft 'x'"));
        Assert.StartsWith("The files of the spacecraft 'x' cannot be written: its name is not a portable file name, ", exception.Message);
        Assert.Contains(reason, exception.Message);
    }

    [Theory]
    [InlineData("SC799")]
    [InlineData("DSS-13")]
    [InlineData("Demo Sat")]
    [InlineData("a.b")]
    [InlineData(".hidden")]
    [InlineData("...a")]
    [InlineData("CONSOLE")]
    [InlineData("CON_S")]
    [InlineData("COM10")]
    [InlineData("COM0")]
    [InlineData("LPT")]
    [InlineData("xNUL")]
    [InlineData("İSS_É")]
    public void Check_AcceptsAPortableFileName(string name)
    {
        PortableFileNames.Check(name, "the spacecraft 'x'");
    }
}
