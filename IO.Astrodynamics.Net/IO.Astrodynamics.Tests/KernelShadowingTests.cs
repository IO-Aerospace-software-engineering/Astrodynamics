// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Kernels;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// No kernel of the test data shadows <c>Data/SolarSystem</c>, which <see cref="TestKernels"/> loads once and every later
/// kernel stays above (<see cref="KernelShadowing"/>); the kernels the tests write and keep loaded are checked at the end
/// of the run by <see cref="KernelReloadGuardTests"/>. Then <see cref="KernelShadowing"/> itself, on kernels written to a
/// temporary directory and never loaded.
/// </summary>
public sealed class KernelShadowingTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("kernel-shadowing-");

    public KernelShadowingTests()
    {
        // The Earth of the SPKs these tests write reads PlanetsAndMoons, whose type initializer reads the kernels
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose() => _root.Delete(true);

    [Fact]
    public void NoKernelOfTheTestDataShadowsTheSolarSystemKernels()
    {
        // Arrange: every folder of Data/ but Data/SolarSystem and the output folder the tests write in, read at the end
        var data = new DirectoryInfo("Data");
        var folders = data.EnumerateFileSystemInfos()
            .Where(f => f.FullName != Constants.SolarSystemKernelPath.FullName &&
                        f.FullName != Constants.OutputPath.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToArray();

        // Act
        var conflicts = KernelShadowing.Find(folders, KernelShadowing.SolarSystem);

        // Assert: the kernels were read, and none shadows
        Assert.Contains(folders, f => f.Name == "FieldOfViewReference");
        Assert.True(folders.SelectMany(SpiceAPI.KernelFiles).Count() > 20);
        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }

    [Fact]
    public void SolarSystem_HoldsWhatItDefines()
    {
        // Act
        var solarSystem = KernelShadowing.SolarSystem;

        // Assert: the objects and frames of its kernels (de440s, the DSN stations, pck00011, the frame kernels, the PCKs)
        Assert.Contains(399, solarSystem.SpkTargets);
        Assert.Contains(301, solarSystem.SpkTargets);
        Assert.Contains(399013, solarSystem.SpkTargets);
        Assert.Contains(3000, solarSystem.PckFrames);
        Assert.Contains("BODY399_RADII", solarSystem.Variables.Keys);
        Assert.Contains("DELTET/DELTA_AT", solarSystem.Variables.Keys);
        Assert.Contains("DSS-13", solarSystem.Names);
        Assert.Contains(399013, solarSystem.Codes);
        Assert.Contains("DSS-13_TOPO", solarSystem.FrameNames);
        Assert.Contains(1399013, solarSystem.FrameIds);
        Assert.Contains("EARTH", solarSystem.FrameAssociations);
        Assert.Empty(solarSystem.MetaKernels);
    }

    [Fact]
    public void Find_OnAKernelOfItsOwnIds_FindsNothing()
    {
        // Arrange: a spacecraft's SPK, frame and names, as the tests write them
        var spk = new FileInfo(Path.Combine(_root.FullName, "own.bsp"));
        File.WriteAllBytes(spk.FullName, Spk(-135));
        Text("own.tf", "NAIF_BODY_NAME += ( 'OWN' )", "NAIF_BODY_CODE += ( -135 )", "FRAME_OWN_FRAME = -135000",
            "FRAME_-135000_NAME = 'OWN_FRAME'", "FRAME_-135000_CENTER = -135", "CK_-135000_SCLK = -135",
            "TKFRAME_-135000_RELATIVE = 'J2000'");

        // Act
        var conflicts = KernelShadowing.Find([_root], KernelShadowing.SolarSystem);

        // Assert
        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }

    [Fact]
    public void Find_OnShadowingKernels_FindsEachKind()
    {
        // Arrange: one kernel per kind
        File.WriteAllBytes(Path.Combine(_root.FullName, "station.bsp"), Spk(399013));
        Text("variable.tpc", "BODY399_RADII = ( 6378.0 6378.0 6357.0 )");
        Text("body.tpc", "BODY399_SHADOWING_TEST = 0.0");
        Text("association.tf", "OBJECT_399_FRAME = 'IAU_EARTH'");
        Text("name.tf", "NAIF_BODY_NAME += 'dss-13'", "NAIF_BODY_CODE += -999");
        Text("code.tf", "NAIF_BODY_NAME += 'OTHER'", "NAIF_BODY_CODE += 399065");
        Text("framename.tf", "FRAME_-999000_NAME = 'DSS-13_TOPO'");
        Text("frameid.tf", "FRAME_OTHER_TOPO = 1399065");
        Text("meta.tm", "KERNELS_TO_LOAD = ( 'a.bsp' )");

        // Act
        var conflicts = KernelShadowing.Find([_root], KernelShadowing.SolarSystem);

        // Assert
        string list = string.Join(Environment.NewLine, conflicts);
        Assert.Contains(conflicts, c => c.Contains("station.bsp") && c.Contains("target 399013"));
        Assert.Contains(conflicts, c => c.Contains("variable.tpc") && c.Contains("BODY399_RADII, which Data/SolarSystem assigns"));
        Assert.Contains(conflicts, c => c.Contains("body.tpc") && c.Contains("of body 399"));
        Assert.Contains(conflicts, c => c.Contains("association.tf") && c.Contains("the frame of an object"));
        Assert.Contains(conflicts, c => c.Contains("name.tf") && c.Contains("names a body DSS-13"));
        Assert.Contains(conflicts, c => c.Contains("code.tf") && c.Contains("body code 399065"));
        Assert.Contains(conflicts, c => c.Contains("framename.tf") && c.Contains("the frame DSS-13_TOPO"));
        Assert.Contains(conflicts, c => c.Contains("frameid.tf") && c.Contains("frame id 1399065"));
        Assert.Contains(conflicts, c => c.Contains("meta.tm") && c.Contains("meta-kernel"));
        Assert.True(conflicts.Count == 9, list);
    }

    [Fact]
    public void ReadKernel_OfATextKernel_ReadsEveryForm()
    {
        // Arrange: lists over lines, a doubled quote, a += with no blank, D exponents, text outside the data
        Text("forms.tk", "A = ( 1 2", "      3 )", "B = 'it''s'", "C+= 4.0D0", "C += ( 5 )", "\\begintext", "D = 6", "\\begindata", "E = @2000-JAN-01");

        // Act
        var content = KernelContent.ReadKernel(Path.Combine(_root.FullName, "forms.tk"));

        // Assert
        Assert.Equal(["1", "2", "3"], content.Variables["A"]);
        Assert.Equal(["it's"], content.Variables["B"]);
        Assert.Equal(["4.0D0", "5"], content.Variables["C"]);
        Assert.DoesNotContain("D", content.Variables.Keys);
        Assert.Equal(["@2000-JAN-01"], content.Variables["E"]);
        Assert.Equal(4, KernelContent.ParseInt(content.Variables["C"][0]));
    }

    [Fact]
    public void ReadKernel_OfAKernelOpenForWriting_ReadsIt()
    {
        // Arrange: a text and a binary kernel held open as CSPICE holds a kernel it loads, for reading and writing ("r+",
        // shared for reading and writing on Windows); a FileShare.Read open fails there with a sharing violation
        Text("held.tf", "FRAME_-135000_NAME = 'HELD_FRAME'");
        var spk = Path.Combine(_root.FullName, "held.bsp");
        File.WriteAllBytes(spk, Spk(-135));
        using var text = new FileStream(Path.Combine(_root.FullName, "held.tf"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var binary = new FileStream(spk, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        // Act
        var content = KernelContent.Read([_root]);

        // Assert
        Assert.Equal(["HELD_FRAME"], content.Variables["FRAME_-135000_NAME"]);
        Assert.Contains(-135, content.SpkTargets);
    }

    [Fact]
    public void ReadKernel_OfAMalformedTextKernel_Throws()
    {
        Text("malformed.tk", "A 1");
        Assert.Throws<InvalidDataException>(() => KernelContent.ReadKernel(Path.Combine(_root.FullName, "malformed.tk")));
    }

    private void Text(string name, params string[] data) =>
        File.WriteAllLines(Path.Combine(_root.FullName, name), new[] { "KPL/FK", "", "\\begindata" }.Concat(data).Append("\\begintext"));

    // An SPK of one type-9 segment of the given target about the Earth, written by the managed writer, never loaded
    private static byte[] Spk(int target)
    {
        var states = Enumerable.Range(0, 2).Select(i => new StateVector(new Vector3(7.0e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0),
            TestHelpers.EarthAtJ2000, TimeSystem.Time.CreateTDB(i), Frames.Frame.ICRF)).ToArray();
        return SpkWriter.BuildType9(target, 1, [states]);
    }
}
