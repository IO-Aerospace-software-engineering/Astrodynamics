// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// <see cref="Spacecraft.WriteKernels"/>, lot L5c of #668 (R14, decisions 79, 80 and 81 of #668, decided by S. Guillet,
/// 2026-11-23, and decision 102, 2026-11-25): the clock, the frame, the instrument frames and kernels, the SPK and the CK
/// of a spacecraft written into one directory, byte for byte the files of the manual sequence of the writers, then loaded
/// each by its own path, and <see cref="SpacecraftKernels.Dispose"/> unloading exactly what the call loaded; the refusals
/// before anything is written, created or loaded (content, names, a target inside a loaded directory, a file SPICE holds,
/// from a load of the library, a meta-kernel or a symbolic link). The references are the manual sequence of the writers
/// (bytes), SPICE's reads of the files through a kernel-backed twin against the managed reads of the spacecraft, and the
/// decided rules.
/// </summary>
public sealed class WriteKernelsTests : IDisposable
{
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSystem.Time Min = new(DateTime.MinValue, TimeScale.TDB);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("write-kernels-");

    public WriteKernelsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(true);
        }
        catch (IOException)
        {
            // A file a failed test left loaded on Windows
        }
    }

    // A spacecraft with two instruments and a nadir attitude, an apogee raise and a TRIAD after it, propagated 1 h, the burn about 45 min in
    private static Spacecraft Propagated(int naifId, string integrator, string name = null)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var sun = new CelestialBody(Stars.Sun);
        var leo = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, earth, Start, Frames.Frame.ICRF).ToStateVector();
        var orbit = new StateVector(leo.Position, leo.Velocity * 0.993, earth, Start, Frames.Frame.ICRF);
        name ??= $"WK{-naifId}";
        var spacecraft = new Spacecraft(naifId, name, 1000.0, orbit, 3000.0, new Clock(name + "_CLK", 65536));
        var tank = spacecraft.AddFuelTank(new FuelTank("ft", 1000.0, 1000.0));
        var engine = spacecraft.AddEngine(new Engine("eng", 300.0, 10.0, tank));
        spacecraft.AddCircularInstrument("CAM", 0.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.3, 0.5, 0.2));
        spacecraft.AddRectangularInstrument("SPEC", 0.3, 0.2, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.0, System.Math.PI / 2.0, 0.0));
        spacecraft.SetManeuvers(new NadirAttitude(Min, TimeSpan.Zero, earth),
            new ApogeeHeightManeuver(7200000.0, engine, Start.AddSeconds(100.0), TimeSpan.Zero, earth),
            new TriadAttitude(Spacecraft.Front, new CelestialAttitudeTarget(earth), Spacecraft.Up, new CelestialAttitudeTarget(sun), Start.AddSeconds(100.0),
                TimeSpan.Zero));
        var window = new Window(Start, Start.AddHours(1.0));
        IntegratorOptions options = integrator == "RK78"
            ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }
            : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) };
        spacecraft.Propagate(window, new PropagationOptions { Integrator = options, OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = [earth, sun] });
        Assert.Contains(spacecraft.ExecutedManeuvers, x => x is ImpulseManeuver);
        return spacecraft;
    }

    // The manual sequence of the writers, into a directory, with the file names of WriteKernels; nothing left loaded
    private static Dictionary<string, byte[]> ManualSequence(Spacecraft spacecraft, DirectoryInfo directory)
    {
        directory.Create();
        FileInfo File(string name) => new(Path.Combine(directory.FullName, name));
        var clock = File(spacecraft.Name + ".tsc");
        spacecraft.Clock.Write(clock);
        spacecraft.Frame.Write(File(spacecraft.Name + ".tf"));
        foreach (var instrument in spacecraft.Instruments)
        {
            instrument.WriteFrame(File($"{spacecraft.Name}_{instrument.Name}.tf"));
            instrument.WriteKernel(File($"{spacecraft.Name}_{instrument.Name}.ti"));
        }

        spacecraft.WriteEphemeris(File(spacecraft.Name + ".bsp"));
        SpiceAPI.Instance.LoadKernels(clock);
        try
        {
            spacecraft.WriteOrientation(File(spacecraft.Name + ".bc"));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(clock);
        }

        return directory.GetFiles().ToDictionary(x => x.Name, x => System.IO.File.ReadAllBytes(x.FullName));
    }

    private static string[] Loaded() => SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName).ToArray();

    [Theory]
    [InlineData("VV")]
    [InlineData("RK78")]
    public void WriteKernels_WritesTheBytesOfTheManualSequence_LoadsEachFile_AndDisposeUnloadsThem(string integrator)
    {
        // Arrange
        var spacecraft = Propagated(integrator == "VV" ? -9301 : -9302, integrator);
        var manual = ManualSequence(spacecraft, new DirectoryInfo(Path.Combine(_root.FullName, "manual")));
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "set"));

        // Act
        var kernels = spacecraft.WriteKernels(target);
        try
        {
            // Assert: the layout, the files byte for byte those of the writers, and nothing else in the directory (the
            // scratch subdirectory deleted)
            Assert.Equal(Path.Combine(target.FullName, spacecraft.Name + ".tsc"), kernels.Clock.FullName);
            Assert.Equal(Path.Combine(target.FullName, spacecraft.Name + ".tf"), kernels.Frame.FullName);
            Assert.Equal(Path.Combine(target.FullName, spacecraft.Name + ".bsp"), kernels.Ephemeris.FullName);
            Assert.Equal(Path.Combine(target.FullName, spacecraft.Name + ".bc"), kernels.Orientation.FullName);
            Assert.Equal(spacecraft.Instruments.ToArray(), kernels.Instruments.Select(x => x.Instrument).ToArray());
            Assert.Equal(Path.Combine(target.FullName, spacecraft.Name + "_CAM.tf"), kernels.Instruments[0].Frame.FullName);
            Assert.Equal(Path.Combine(target.FullName, spacecraft.Name + "_SPEC.ti"), kernels.Instruments[1].Kernel.FullName);
            Assert.Empty(target.GetDirectories());
            var written = target.GetFiles().ToDictionary(x => x.Name, x => File.ReadAllBytes(x.FullName));
            Assert.Equal(manual.Keys.OrderBy(x => x, StringComparer.Ordinal), written.Keys.OrderBy(x => x, StringComparer.Ordinal));
            foreach (var (name, bytes) in manual)
            {
                Assert.True(bytes.AsSpan().SequenceEqual(written[name]), $"{name} differs from the manual sequence");
            }

            // Each file loaded by its own path, and held by SPICE
            Assert.True(kernels.IsLoaded);
            var loaded = Loaded();
            Assert.All(target.GetFiles(), x => Assert.Contains(x.FullName, loaded));
            Assert.All(target.GetFiles(), x => Assert.True(SpiceAPI.Instance.HoldsKernelFile(x)));

            // SPICE reads them: a kernel-backed twin against the managed reads, within spacecraft.md's accuracy table
            // (3.9e-3 m between the stored states and spkezr in 2024; 6.1e-10 rad between the attitude and pxform)
            var twin = new Spacecraft(spacecraft.NaifId, spacecraft.Name, Start, 1000.0, 3000.0, new Clock(spacecraft.Name + "_CLK", 65536));
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            foreach (var epoch in new[] { Start.AddSeconds(35.0), Start.AddSeconds(1805.0), Start.AddSeconds(3575.0) })
            {
                var managed = spacecraft.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                var read = twin.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                Assert.True((managed.Position - read.Position).Magnitude() < 3.9e-3, $"{(managed.Position - read.Position).Magnitude()} m at {epoch}");
                var q = spacecraft.GetOrientation(Frames.Frame.ICRF, epoch).Rotation;
                var r = twin.GetOrientation(Frames.Frame.ICRF, epoch).Rotation;
                double angle = 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(q.W * r.W + q.VectorPart.Dot(r.VectorPart))));
                Assert.True(angle < 6.1e-10, $"{angle} rad at {epoch}");
            }
        }
        finally
        {
            kernels.Dispose();
        }

        // Dispose unloads them, keeps them on disk, and a second Dispose does nothing
        Assert.False(kernels.IsLoaded);
        var after = Loaded();
        Assert.All(target.GetFiles(), x => Assert.DoesNotContain(x.FullName, after));
        Assert.All(target.GetFiles(), x => Assert.False(SpiceAPI.Instance.HoldsKernelFile(x)));
        Assert.Equal(4 + 2 * 2, target.GetFiles().Length);
        kernels.Dispose();
    }

    [Fact]
    public void WriteKernels_ATleSampledSpacecraft_IsRefused_ThenWrittenOnceGivenAttitudes()
    {
        // Decision 81 of #668 ([46]): by content; a TLEPropagator run stores states and no attitude (#546)
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var spacecraft = new Spacecraft(-9303, "WK9303", 420000.0, tle);
        var start = tle.Epoch.ToTDB();
        new TLEPropagator(new Window(start, start.AddMinutes(10.0)), spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "tle"));

        var exception = Assert.Throws<InvalidOperationException>(() => spacecraft.WriteKernels(target));
        Assert.Contains("TLEPropagator", exception.Message);
        Assert.Contains("AddStateOrientationToICRF", exception.Message);
        Assert.False(target.Exists);

        // Attitudes given after the run (its re-arm drops those added before it)
        spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, start, spacecraft.Frame));
        spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, start.AddMinutes(10.0), spacecraft.Frame));
        using var kernels = spacecraft.WriteKernels(target);
        Assert.True(kernels.IsLoaded);
        Assert.Empty(kernels.Instruments);
        Assert.Equal(4, target.GetFiles().Length);
        Assert.Contains(kernels.Orientation.FullName, Loaded());
    }

    // The state of the target and of the registry before a refusal, to compare after it
    private static (bool Exists, string[] Files, string[] Loaded) Snapshot(DirectoryInfo target)
    {
        target.Refresh();
        return (target.Exists,
            target.Exists ? target.GetFileSystemInfos("*", SearchOption.AllDirectories).Select(x => x.FullName).Order(StringComparer.Ordinal).ToArray() : [],
            Loaded());
    }

    private static void AssertRefusedUnchanged(Spacecraft spacecraft, DirectoryInfo target, string expected)
    {
        var before = Snapshot(target);
        var exception = Assert.Throws<InvalidOperationException>(() => spacecraft.WriteKernels(target));
        Assert.Contains(expected, exception.Message);
        var after = Snapshot(target);
        Assert.Equal(before.Exists, after.Exists);
        Assert.Equal(before.Files, after.Files);
        Assert.Equal(before.Loaded, after.Loaded);
    }

    [Fact]
    public void WriteKernels_ByContent_RefusesBeforeAnything()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, Start, Frames.Frame.ICRF);
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "content"));

        // No stored state
        var never = new Spacecraft(-9304, "WK9304", 1000.0, orbit);
        AssertRefusedUnchanged(never, target, "no stored state");

        // An arc of one state, which WriteEphemeris refuses with ArgumentException (decisions 87 and 89)
        var lone = new Spacecraft(-9305, "WK9305", 1000.0, orbit);
        lone.AddPropagatedStates([orbit]);
        AssertRefusedUnchanged(lone, target, "WK9305");

        // States without an attitude record (#546)
        var states = new Spacecraft(-9306, "WK9306", 1000.0, orbit);
        states.AddPropagatedStates([orbit, orbit.AtEpoch(Start.AddSeconds(60.0)).ToStateVector(), orbit.AtEpoch(Start.AddSeconds(120.0)).ToStateVector()]);
        AssertRefusedUnchanged(states, target, "AddStateOrientationToICRF");

        // Null
        Assert.Throws<ArgumentNullException>(() => never.WriteKernels(null));
    }

    [Fact]
    public void WriteKernels_ByName_RefusesBeforeAnything()
    {
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "names"));

        // A name SPICE cannot read in a kernel variable (#676)
        AssertRefusedUnchanged(Propagated(-9307, "VV", "WK 9307"), target, "kernel variable");

        // A name that is not a portable file name (#799(a))
        AssertRefusedUnchanged(Propagated(-9308, "VV", "WK:9308"), target, "portable file name");

        // A path SPICE cannot take (#673): ArgumentException naming the directory, nothing created
        var deep = new DirectoryInfo(Path.Combine(_root.FullName, new string('d', 120), new string('e', 120)));
        var spacecraft = Propagated(-9309, "VV");
        var exception = Assert.Throws<ArgumentException>(() => spacecraft.WriteKernels(deep));
        Assert.Equal("directory", exception.ParamName);
        Assert.False(deep.Exists);
        Assert.False(deep.Parent!.Exists);
    }

    [Fact]
    public void WriteKernels_IntoOrInsideALoadedDirectory_IsRefused()
    {
        var spacecraft = Propagated(-9310, "VV");
        var loaded = Directory.CreateDirectory(Path.Combine(_root.FullName, "loaded"));
        File.WriteAllText(Path.Combine(loaded.FullName, "user.tk"), "KPL/PCK\n\\begindata\nIOA_WK_USER = 1\n\\begintext\n");
        SpiceAPI.Instance.LoadKernels(loaded);
        try
        {
            AssertRefusedUnchanged(spacecraft, loaded, "loaded kernel path");
            AssertRefusedUnchanged(spacecraft, new DirectoryInfo(Path.Combine(loaded.FullName, "sub")), "loaded kernel path");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(loaded);
        }

        // A sibling whose name extends it is another directory
        using var kernels = spacecraft.WriteKernels(new DirectoryInfo(loaded.FullName + "2"));
        Assert.True(kernels.IsLoaded);
    }

    [Fact]
    public void WriteKernels_OverAFileSpiceHolds_IsRefused()
    {
        var spacecraft = Propagated(-9311, "VV");
        var target = Directory.CreateDirectory(Path.Combine(_root.FullName, "held"));
        var frame = new FileInfo(Path.Combine(target.FullName, spacecraft.Name + ".tf"));
        spacecraft.Frame.Write(frame);
        var bytes = File.ReadAllBytes(frame.FullName);

        // A load of the library
        SpiceAPI.Instance.LoadKernels(frame);
        try
        {
            AssertRefusedUnchanged(spacecraft, target, frame.FullName);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(frame);
        }

        // A meta-kernel's (decision 102: the registry lists the meta-kernel only)
        var metaKernel = new FileInfo(Path.Combine(_root.FullName, "mission.tm"));
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( {MetaKernelString(frame.FullName)} )\n\\begintext\n");
        SpiceAPI.Instance.LoadKernels(metaKernel);
        try
        {
            AssertRefusedUnchanged(spacecraft, target, frame.FullName);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(metaKernel);
        }

        Assert.Equal(bytes, File.ReadAllBytes(frame.FullName));

        // Once unloaded, it is replaced
        using var kernels = spacecraft.WriteKernels(target);
        Assert.Contains(frame.FullName, Loaded());
    }

    [NotOnWindowsFact("A symbolic link needs a privilege on Windows")]
    public void WriteKernels_OverAFileSpiceHoldsThroughASymbolicLink_IsRefused()
    {
        var spacecraft = Propagated(-9312, "VV");
        var target = Directory.CreateDirectory(Path.Combine(_root.FullName, "linked"));
        var clock = new FileInfo(Path.Combine(target.FullName, spacecraft.Name + ".tsc"));
        spacecraft.Clock.Write(clock);
        var link = new FileInfo(Path.Combine(_root.FullName, "clock-link.tsc"));
        link.CreateAsSymbolicLink(clock.FullName);
        SpiceAPI.Instance.LoadKernels(link);
        try
        {
            AssertRefusedUnchanged(spacecraft, target, clock.FullName);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(link);
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

    [Fact]
    public void WriteKernels_TwoSpacecraftIntoOneDirectory_BothLoaded_EachDisposeUnloadsItsOwn()
    {
        var first = Propagated(-9313, "VV");
        var second = Propagated(-9314, "VV");
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "shared"));

        var a = first.WriteKernels(target);
        SpacecraftKernels b = null;
        try
        {
            b = second.WriteKernels(target);

            // The second call over the first's files, while the first handle lives, is refused (a file SPICE holds)
            AssertRefusedUnchanged(first, target, first.Name + ".tsc");

            a.Dispose();
            var loaded = Loaded();
            Assert.DoesNotContain(a.Clock.FullName, loaded);
            Assert.Contains(b.Clock.FullName, loaded);
            Assert.Contains(b.Orientation.FullName, loaded);
        }
        finally
        {
            a.Dispose();
            b?.Dispose();
        }

        Assert.DoesNotContain(b.Orientation.FullName, Loaded());
    }

    [Fact]
    public void WriteKernels_AgainAfterDispose_ReplacesTheFiles_AndSpiceReadsTheNewOnes()
    {
        var spacecraft = Propagated(-9315, "VV");
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "again"));
        spacecraft.WriteKernels(target).Dispose();
        var before = File.ReadAllBytes(Path.Combine(target.FullName, spacecraft.Name + ".bsp"));

        // Another propagation, over 5 min: SPICE reads its states, not the first file's
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        spacecraft.Propagate(new Window(Start, Start.AddMinutes(5.0)), new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }, OutputStep = TimeSpan.FromSeconds(10.0),
            ThirdBodies = [earth, new CelestialBody(Stars.Sun)]
        });
        using var kernels = spacecraft.WriteKernels(target);
        Assert.False(before.AsSpan().SequenceEqual(File.ReadAllBytes(kernels.Ephemeris.FullName)));
        var twin = new Spacecraft(spacecraft.NaifId, spacecraft.Name, Start, 1000.0, 3000.0, new Clock(spacecraft.Name + "_CLK", 65536));
        var epoch = Start.AddSeconds(155.0);
        var managed = spacecraft.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var read = twin.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        Assert.True((managed.Position - read.Position).Magnitude() < 3.9e-3);
        Assert.ThrowsAny<InvalidOperationException>(() => twin.GetEphemeris(Start.AddSeconds(455.0), earth, Frames.Frame.ICRF, Aberration.None));
    }

    [Fact]
    public void Dispose_AfterAUserLoadOfTheParentDirectory_UnloadsTheHandlesFiles_AndKeepsTheUsersOthers()
    {
        // IC-4 of the lot plan: the user's directory entry takes the handle's files; Dispose unloads them and replaces the
        // entry by its other files, as UnloadKernels of a file inside it does
        var spacecraft = Propagated(-9316, "VV");
        var parent = Directory.CreateDirectory(Path.Combine(_root.FullName, "parent"));
        var user = new FileInfo(Path.Combine(parent.FullName, "user.tk"));
        File.WriteAllText(user.FullName, "KPL/PCK\n\\begindata\nIOA_WK_PARENT = 1\n\\begintext\n");
        var kernels = spacecraft.WriteKernels(new DirectoryInfo(Path.Combine(parent.FullName, "set")));
        try
        {
            SpiceAPI.Instance.LoadKernels(parent);
            Assert.Contains(parent.FullName, Loaded());
            Assert.DoesNotContain(kernels.Clock.FullName, Loaded());

            kernels.Dispose();

            var loaded = Loaded();
            Assert.DoesNotContain(parent.FullName, loaded);
            Assert.Contains(user.FullName, loaded);
            Assert.False(SpiceAPI.Instance.HoldsKernelFile(kernels.Clock));
            Assert.False(SpiceAPI.Instance.HoldsKernelFile(kernels.Orientation));
            Assert.True(SpiceAPI.Instance.HoldsKernelFile(user));
        }
        finally
        {
            kernels.Dispose();
            SpiceAPI.Instance.UnloadKernels(parent);
        }

        Assert.False(SpiceAPI.Instance.HoldsKernelFile(user));
    }

    [Fact]
    public void Dispose_AfterTheDirectoryIsDeleted_UnloadsEverything()
    {
        // #673: the unloads of one pass repair each other's failures on the deleted text kernels
        var spacecraft = Propagated(-9317, "VV");
        var target = new DirectoryInfo(Path.Combine(_root.FullName, "deleted"));
        var kernels = spacecraft.WriteKernels(target);
        var files = kernels.Files.ToArray();
        try
        {
            target.Delete(true);
        }
        catch (IOException)
        {
            // Windows keeps a loaded DAF open: the case is the Linux and macOS one
            kernels.Dispose();
            return;
        }

        kernels.Dispose();
        Assert.False(kernels.IsLoaded);
        var loaded = Loaded();
        Assert.All(files, x => Assert.DoesNotContain(x.FullName, loaded));
        Assert.All(files, x => Assert.False(SpiceAPI.Instance.HoldsKernelFile(x)));
    }

    [Fact]
    public void WriteKernels_OfAKernelBackedSpacecraft_IsRefused()
    {
        var spacecraft = Propagated(-9318, "VV");
        using var kernels = spacecraft.WriteKernels(new DirectoryInfo(Path.Combine(_root.FullName, "source")));
        var twin = new Spacecraft(spacecraft.NaifId, spacecraft.Name, Start, 1000.0, 3000.0, new Clock(spacecraft.Name + "_CLK", 65536));

        AssertRefusedUnchanged(twin, new DirectoryInfo(Path.Combine(_root.FullName, "twin")), "kernel-backed");
    }
}
