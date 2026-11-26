using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// <see cref="SpiceApi.TryGetNaifId"/>, the name lookup of <c>bodn2c_c</c> (choice 12 of #668, decided by S. Guillet,
/// 2026-11-19): SPICE's built-in names (NAIF IDs Required Reading, "NAIF Object ID numbers") and those of the loaded
/// kernels, in any case, blanks compressed.
/// </summary>
public class SpiceApiNaifIdTests
{
    public SpiceApiNaifIdTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [InlineData("MOON", 301)]
    [InlineData("earth", 399)]
    [InlineData("Sun", 10)]
    [InlineData("SOLAR SYSTEM BARYCENTER", 0)]
    [InlineData("SSB", 0)]
    [InlineData("Earth  Barycenter", 3)]
    [InlineData("  mars  ", 499)]
    [InlineData("DSS-13", 399013)]
    [InlineData("LRO", -85)]
    public void TryGetNaifId_KnownName(string name, int expected)
    {
        Assert.True(SpiceApi.Instance.TryGetNaifId(name, out var naifId));
        Assert.Equal(expected, naifId);
    }

    [Theory]
    [InlineData("NOT A BODY")]
    [InlineData("399")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetNaifId_UnknownName_IsFalse(string name)
    {
        Assert.False(SpiceApi.Instance.TryGetNaifId(name, out var naifId));
        Assert.Equal(0, naifId);

        // Nothing left pending in SPICE: the next read works
        Assert.True(SpiceApi.Instance.TryGetNaifId("MOON", out naifId));
        Assert.Equal(301, naifId);
    }

    [Fact]
    public void TryGetNaifId_Null_Throws()
    {
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => SpiceApi.Instance.TryGetNaifId(null, out _)).ParamName);
    }

    [Fact]
    public void TryGetNaifId_NameOfALoadedKernel()
    {
        // A name a text kernel defines (NAIF_BODY_NAME, NAIF_BODY_CODE) is known while the kernel is loaded
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"IOA_L8_NAME_{Guid.NewGuid():N}.tk"));
        File.WriteAllText(file.FullName,
            "\\begindata\n   NAIF_BODY_NAME += 'IOA L8 NET PROBE'\n   NAIF_BODY_CODE += -999124\n\\begintext\n");
        try
        {
            Assert.False(SpiceApi.Instance.TryGetNaifId("IOA L8 NET PROBE", out _));
            SpiceApi.Instance.LoadKernels(file);
            try
            {
                Assert.True(SpiceApi.Instance.TryGetNaifId("ioa l8 net probe", out var naifId));
                Assert.Equal(-999124, naifId);
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(file);
            }

            Assert.False(SpiceApi.Instance.TryGetNaifId("IOA L8 NET PROBE", out _));
        }
        finally
        {
            file.Delete();
        }
    }

    /// <summary>
    /// <see cref="SpiceApi.KnowsFrame"/>, the check of the frame arguments of the command-line tool (R29 of #668): the
    /// frames the library holds and those SPICE knows by the name it is handed, not a name neither knows.
    /// </summary>
    [Fact]
    public void KnowsFrame()
    {
        Assert.True(SpiceApi.KnowsFrame(Frames.Frame.ICRF));
        Assert.True(SpiceApi.KnowsFrame(new Frames.Frame("GCRF")));
        Assert.True(SpiceApi.KnowsFrame(new Frames.Frame("teme")));
        Assert.True(SpiceApi.KnowsFrame(new Frames.Frame("CIRS")));
        Assert.True(SpiceApi.KnowsFrame(Frames.Frame.ITRF93));
        Assert.True(SpiceApi.KnowsFrame(new Frames.Frame("iau_earth")));
        Assert.True(SpiceApi.KnowsFrame(new Frames.Frame("ECLIPJ2000")));
        Assert.False(SpiceApi.KnowsFrame(new Frames.Frame("NOT_A_FRAME_L8")));
        Assert.False(SpiceApi.KnowsFrame(new Frames.Frame("ICRF")));
        Assert.False(SpiceApi.KnowsFrame(null));
    }
}
