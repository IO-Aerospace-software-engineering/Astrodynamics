using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// <see cref="SpiceAPI.TryGetNaifId"/>, the name lookup of <c>bodn2c_c</c> (choice 12 of #668, decided by S. Guillet,
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
        Assert.True(SpiceAPI.Instance.TryGetNaifId(name, out var naifId));
        Assert.Equal(expected, naifId);
    }

    [Theory]
    [InlineData("NOT A BODY")]
    [InlineData("399")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetNaifId_UnknownName_IsFalse(string name)
    {
        Assert.False(SpiceAPI.Instance.TryGetNaifId(name, out var naifId));
        Assert.Equal(0, naifId);

        // Nothing left pending in SPICE: the next read works
        Assert.True(SpiceAPI.Instance.TryGetNaifId("MOON", out naifId));
        Assert.Equal(301, naifId);
    }

    [Fact]
    public void TryGetNaifId_Null_Throws()
    {
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.TryGetNaifId(null, out _)).ParamName);
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
            Assert.False(SpiceAPI.Instance.TryGetNaifId("IOA L8 NET PROBE", out _));
            SpiceAPI.Instance.LoadKernels(file);
            try
            {
                Assert.True(SpiceAPI.Instance.TryGetNaifId("ioa l8 net probe", out var naifId));
                Assert.Equal(-999124, naifId);
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(file);
            }

            Assert.False(SpiceAPI.Instance.TryGetNaifId("IOA L8 NET PROBE", out _));
        }
        finally
        {
            file.Delete();
        }
    }
}
