// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The readers of the references of #681's tests: <see cref="TextKernelReference"/> on a text kernel written here (the
/// rules of the Kernel Required Reading, "Text Kernel Format"), and <see cref="ToStringLines"/> on lines "title : value".
/// </summary>
public sealed class ReferenceReadersTests : IDisposable
{
    private readonly string _kernel = Path.Combine(Directory.CreateTempSubdirectory("text-kernel-").FullName, "test.tpc");

    public ReferenceReadersTests()
    {
        File.WriteAllLines(_kernel,
        [
            "KPL/PCK",
            @"\begindata",
            "BODY399_GM = ( 1.0D0 )",
            "BODY399_RADII = ( 6378.1366   6378.1366",
            "                  6356.7519 )",
            @"\begintext",
            "BODY399_IGNORED = ( 5.0 )",
            @"\begindata",
            "BODY399_GM = ( 3.9860043550702266D+05 )",
            @"\begintext"
        ]);
    }

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(_kernel)!, true);
    }

    [Fact]
    public void TextKernelReference_ReadsTheLastAssignmentOfTheDataBlocks()
    {
        Assert.Equal([398600.43550702266], TextKernelReference.Read(_kernel, "BODY399_GM"));
        Assert.Equal([6378.1366, 6378.1366, 6356.7519], TextKernelReference.Read(_kernel, "BODY399_RADII"));
    }

    [Fact]
    public void TextKernelReference_NameOutsideTheDataBlocks_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TextKernelReference.Read(_kernel, "BODY399_IGNORED"));
        Assert.Throws<InvalidOperationException>(() => TextKernelReference.Read(_kernel, "BODY399_NOT_ASSIGNED"));
    }

    [Fact]
    public void ToStringLines_ReadsTheValueOfATitle()
    {
        var text = $"{"Name :",32} EARTH{Environment.NewLine}{"GM (m^3/s^2) :",32} 398600435507022.6   {Environment.NewLine}";
        Assert.Equal("EARTH", ToStringLines.Value(text, "Name :"));
        Assert.Equal(398600435507022.6, ToStringLines.Number(text, "GM (m^3/s^2) :"));
        Assert.Throws<InvalidOperationException>(() => ToStringLines.Value(text, "Absent :"));
    }
}
