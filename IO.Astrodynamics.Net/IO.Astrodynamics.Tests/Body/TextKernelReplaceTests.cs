// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The text kernel writers over a file SPICE holds (#799(c), decision 103 of #668, decided by S. Guillet, 2026-11-25):
/// <see cref="Clock.Write"/>, <see cref="SpacecraftFrame.Write"/>, <see cref="Instrument.WriteFrame"/>,
/// <see cref="Instrument.WriteKernel"/>, <see cref="Site.WriteFrame"/> and <see cref="SiteFrame.Write"/> unload the file
/// they replace, under its path and every name SPICE holds that resolves through it (a meta-kernel's included), write it
/// and leave it unloaded, as the SPK and CK writers since #706. Before, the file was rewritten under SPICE's load, which
/// a later <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/> skipped (the file was held): SPICE read the old content
/// (measured: a frame kernel rewritten for another spacecraft still named the first). The reference is the decided rule
/// and SPICE's reads of the files.
/// </summary>
public sealed class TextKernelReplaceTests : IDisposable
{
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("text-kernel-replace-");

    public TextKernelReplaceTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose() => _root.Delete(true);

    private static Spacecraft NewSpacecraft(int naifId, string name)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, Start, Frames.Frame.ICRF);
        return new Spacecraft(naifId, name, 1000.0, orbit);
    }

    private FileInfo File(string name) => new(Path.Combine(_root.FullName, name));

    private static bool Listed(FileInfo file) => SpiceAPI.Instance.GetLoadedKernels().Any(x => x.FullName == file.FullName);

    // Writes the file, loads it, writes it again: the second write leaves it unloaded, neither listed nor held by SPICE
    private static void AssertRewriteUnloads(FileInfo file, Action<FileInfo> write)
    {
        write(file);
        SpiceAPI.Instance.LoadKernels(file);
        try
        {
            Assert.True(SpiceAPI.Instance.HoldsKernelFile(file));

            write(file);

            Assert.False(Listed(file));
            Assert.False(SpiceAPI.Instance.HoldsKernelFile(file));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
        }
    }

    [Fact]
    public void EachTextKernelWriter_UnloadsTheFileItReplaces_AndLeavesItUnloaded()
    {
        var spacecraft = NewSpacecraft(-9331, "TK9331");
        var camera = spacecraft.AddCircularInstrument("CAM", 0.5, Vector3.VectorZ, Vector3.VectorY);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var site = new Site(933, "TK933", earth, new Planetodetic(0.4, -0.6, 120.0));

        AssertRewriteUnloads(File("clock.tsc"), spacecraft.Clock.Write);
        AssertRewriteUnloads(File("frame.tf"), spacecraft.Frame.Write);
        AssertRewriteUnloads(File("camera.tf"), camera.WriteFrame);
        AssertRewriteUnloads(File("camera.ti"), camera.WriteKernel);
        AssertRewriteUnloads(File("site.tf"), site.WriteFrame);
        AssertRewriteUnloads(File("siteframe.tf"), site.Frame.Write);
    }

    [Fact]
    public void ARewrittenFrameKernel_LoadedAgain_IsReadAnew()
    {
        // The sonde of the gap analysis: the frame kernel of one spacecraft rewritten for another, then loaded again
        var file = File("sonde.tf");
        NewSpacecraft(-9332, "TKA9332").Frame.Write(file);
        SpiceAPI.Instance.LoadKernels(file);
        try
        {
            Assert.True(SpiceAPI.Instance.TryGetNaifId("TKA9332", out _));

            NewSpacecraft(-9333, "TKB9333").Frame.Write(file);
            SpiceAPI.Instance.LoadKernels(file);

            Assert.False(SpiceAPI.Instance.TryGetNaifId("TKA9332", out _));
            Assert.True(SpiceAPI.Instance.TryGetNaifId("TKB9333", out int id));
            Assert.Equal(-9333, id);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
        }

        Assert.False(SpiceAPI.Instance.TryGetNaifId("TKB9333", out _));
    }

    [Fact]
    public void ATextKernelWriter_UnloadsTheLoadOfAMetaKernel()
    {
        // As the SPK and CK writers (review of #706): every name SPICE holds that resolves through the file
        var spacecraft = NewSpacecraft(-9334, "TK9334");
        var file = File("meta-member.tf");
        spacecraft.Frame.Write(file);
        var metaKernel = File("mission.tm");
        System.IO.File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( {MetaKernelString(file.FullName)} )\n\\begintext\n");
        SpiceAPI.Instance.LoadKernels(metaKernel);
        try
        {
            Assert.True(SpiceAPI.Instance.HoldsKernelFile(file));

            spacecraft.Frame.Write(file);

            Assert.False(SpiceAPI.Instance.HoldsKernelFile(file));
            Assert.False(SpiceAPI.Instance.TryGetNaifId("TK9334", out _));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(metaKernel);
        }
    }

    // A string value of a meta-kernel, in pieces of 60 characters at most, each but the last ended by '+'
    private static string MetaKernelString(string path)
    {
        var value = new StringBuilder();
        for (int i = 0; i < path.Length; i += 60)
        {
            value.Append('\'').Append(path.Substring(i, System.Math.Min(60, path.Length - i))).Append(i + 60 < path.Length ? "+'\n" : "'");
        }

        return value.ToString();
    }
}
