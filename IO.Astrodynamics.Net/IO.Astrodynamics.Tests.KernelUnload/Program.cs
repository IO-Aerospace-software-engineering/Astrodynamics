// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.KernelUnload;

/// <summary>
/// The child process of <c>KernelRegistryTests</c> (#673): one scenario of <see cref="SpiceAPI.LoadKernels"/> or
/// <see cref="SpiceAPI.UnloadKernels"/> while a loaded binary kernel was deleted, run in a process of its own.
/// </summary>
/// <remarks>
/// <para>
/// When SPICE opens the loaded binary kernels of a kind again after an unload (<c>unload_c --&gt; UNLOAD --&gt; PCKLOF</c>,
/// <c>SPKLEF</c>, <c>CKLPF</c>) and fails on one deleted since its load, KEEPER records the handle 0 for it: its later
/// unload, and <c>kclear_c</c>, close nothing, and the deleted file stays open in the DAF and PCK tables of the process
/// (measured with CSPICE N0067). Once more than the 23 logical units of CSPICE's handle manager are in use, its unit is
/// released, and every later search of the PCKs (<c>TISBOD --&gt; PCKSFS --&gt; DAFBBS --&gt; ZZDDHHLU</c>, each IAU
/// frame) fails to reconnect it: in the shared test process the IAU frames of other test classes failed intermittently
/// (CI of 725ee1a and a993265 on Linux and macOS arm64). A deleted PCK never unloaded does the same once its unit is
/// released. Since the second round of #668 each native load and unload, those of the native writers included, first
/// closes every SPK, CK, binary PCK and EK whose file was deleted by the handle KEEPER holds, then unloads it
/// (<c>KernelsLoader</c>), a kernel held under a relative name, from a meta-kernel, left to SPICE: the scenarios check that
/// nothing stays open (KEEPER's list and the DAF handles through <see cref="CspiceDirect"/>, on Linux, where the native
/// library exports CSPICE) and that the IAU_EARTH rotation and the Moon's state are bit for bit those read before, once 25
/// more DAFs are loaded. Run here, the scenarios leave nothing behind, and still run in the shared test process on a native
/// library without that unload.
/// </para>
/// <para>
/// Arguments: the kernel directory (<c>Data/SolarSystem</c>), loaded first as in the test process, and the scenario, the
/// name of the test of <c>KernelRegistryTests</c> it was. Exit codes: 0 the scenario held; 1 it did not (the reason on the
/// standard error); 2 wrong arguments.
/// </para>
/// </remarks>
internal static class Program
{
    internal const string FolderOfTwoPcks = "UnloadKernels_OfAFolderOfTwoPcks_WhoseSecondIsDeleted_RestoresThePrecedence";
    internal const string PckOfAFolder = "UnloadKernels_OfAPck_WhenAnotherPckWasDeleted_ClosesIt_AndKeepsThePrecedence";
    internal const string MetaKernelOfAPck = "UnloadKernels_OfAMetaKernelOfAPckThenATextKernel_WhenAnotherPckWasDeleted_DoesNotThrow";
    internal const string FolderOfTwoDeletedPcks = "UnloadKernels_OfAFolderOfTwoPcks_BothDeleted_ClosesThem";
    internal const string DeletedSpk = "UnloadKernels_OfAnSpk_WhenAnotherSpkWasDeleted_ClosesIt";
    internal const string DeletedPckNeverUnloaded = "LoadKernels_WhenAPckWasDeletedAndNeverUnloaded_ClosesIt";
    internal const string DeletedCk = "UnloadKernels_OfACk_WhenAnotherCkWasDeleted_ClosesIt";
    internal const string WriterOfAnSpk = "WriteEphemeris_WhenAnSpkWasDeleted_ClosesIt";
    internal const string WriterOfACk = "WriteOrientation_WhenACkWasDeleted_ClosesIt";
    internal const string RelativeName = "LoadKernels_AfterTheWorkingDirectoryChanged_KeepsAnSpkAMetaKernelNamesRelatively";
    internal const string WriterOfAnSpkRelative = "WriteEphemeris_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending";
    internal const string WriterOfACkRelative = "WriteOrientation_WhenACkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending";

    private static readonly string[] Scenarios =
    [
        FolderOfTwoPcks, PckOfAFolder, MetaKernelOfAPck, FolderOfTwoDeletedPcks, DeletedSpk, DeletedPckNeverUnloaded, DeletedCk, WriterOfAnSpk,
        WriterOfACk, RelativeName, WriterOfAnSpkRelative, WriterOfACkRelative
    ];

    // More DAFs than the 23 logical units of CSPICE's handle manager, with those of Data/SolarSystem
    private const int ManyDafs = 25;

    private static readonly Time Epoch = new(2024, 1, 1);

    private static int Main(string[] args)
    {
        if (args.Length != 2 || !Directory.Exists(args[0]) || !Scenarios.Contains(args[1]))
        {
            Console.Error.WriteLine($"Usage: <kernel directory> <{string.Join(" | ", Scenarios)}>");
            return 2;
        }

        var kernels = new DirectoryInfo(args[0]);
        SpiceAPI.Instance.LoadKernels(kernels);
        var root = Directory.CreateTempSubdirectory("kernel-unload-");
        try
        {
            var moonPck = Path.Combine(kernels.FullName, "moon_pa_de421_1900-2050.bpc");
            switch (args[1])
            {
                case FolderOfTwoPcks:
                    UnloadFolderOfTwoPcks(root, moonPck);
                    break;
                case PckOfAFolder:
                    UnloadPckOfAFolder(root, kernels, moonPck);
                    break;
                case MetaKernelOfAPck:
                    UnloadMetaKernelOfAPck(root, moonPck);
                    break;
                case FolderOfTwoDeletedPcks:
                    UnloadFolderOfTwoDeletedPcks(root, kernels, moonPck);
                    break;
                case DeletedSpk:
                    UnloadSpkWhenAnotherIsDeleted(root, kernels);
                    break;
                case DeletedPckNeverUnloaded:
                    LoadWhenAPckWasDeleted(root, kernels, moonPck);
                    break;
                case DeletedCk:
                    UnloadCkWhenAnotherIsDeleted(root, kernels);
                    break;
                case WriterOfAnSpk:
                    WriteSpkWhenAnotherIsDeleted(root, kernels);
                    break;
                case WriterOfACk:
                    WriteCkWhenAnotherIsDeleted(root, kernels);
                    break;
                case WriterOfAnSpkRelative:
                    WriteSpkWhenAnSpkHeldRelativelyIsDeleted(root, kernels);
                    break;
                case WriterOfACkRelative:
                    WriteCkWhenACkHeldRelativelyIsDeleted(root, kernels);
                    break;
                default:
                    LoadAfterTheWorkingDirectoryChanged(root);
                    break;
            }

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            root.Delete(true);
        }
    }

    // P, the folder [X, D] and Q, D deleted after the load. SPICE unloads a binary PCK by opening again the other PCKs still
    // loaded, in their load order (unload_c --> UNLOAD --> PCKLOF --> DAFOPR): the unload of X opens P again, which then
    // takes precedence over Q, and fails on D; the unload of D opens P and Q again, which restores the precedence of Q
    // (measured with CSPICE N0067). The first unload fails and the second one repairs it
    private static void UnloadFolderOfTwoPcks(DirectoryInfo root, string moonPck)
    {
        var (folder, _, d, _, withQ) = LoadPcksAroundAFolder(root, moonPck, 1999955, "IOA_F02_PCK_A");
        File.Delete(d.FullName);

        SpiceAPI.Instance.UnloadKernels(folder);

        Check(!Listed(root).Contains(folder.FullName), "the folder is no longer listed");
        CheckSameRotation(withQ, "IOA_F02_PCK_A", "Q takes precedence over P again");
    }

    // P, the folder [X, D] and Q, D deleted after the load. Before the second round of #668, the unload of X opened P again,
    // then failed on D, before Q: it threw, P took precedence over Q until a PCK unload succeeded, and D stayed open in SPICE
    // (measured with CSPICE N0067). The unload first closes D by its handle and unloads it: it returns, Q keeps its
    // precedence, nothing holds D, and the PCK searches of IAU_EARTH no longer reach it
    private static void UnloadPckOfAFolder(DirectoryInfo root, DirectoryInfo kernels, string moonPck)
    {
        var before = SearchesBefore();
        var (folder, x, d, _, withQ) = LoadPcksAroundAFolder(root, moonPck, 1999958, "IOA_F02_PCK_B");
        var handle = KeeperHandle(d);
        File.Delete(d.FullName);

        SpiceAPI.Instance.UnloadKernels(x);

        CheckSameRotation(withQ, "IOA_F02_PCK_B", "Q keeps its precedence over P");
        Check(Listed(root).Contains(d.FullName), "the deleted PCK is still listed until it is unloaded");
        CheckClosed(d, handle);
        CheckSearchesAfterManyDafs(root, kernels, before);
        SpiceAPI.Instance.UnloadKernels(d);
        Check(!Listed(root).Contains(folder.FullName) && !Listed(root).Contains(d.FullName), "the folder and D are no longer listed");
        CheckSameRotation(withQ, "IOA_F02_PCK_B", "Q keeps its precedence over P once D is unloaded");
    }

    // P, the folder [X, D] and Q, X and D both deleted after the load. Before the second round of #668, the unload of X failed
    // on D, which then stayed open in SPICE, and the unload of D repaired the precedence: the folder's unload returned, and
    // the PCK searches of IAU_EARTH later failed on D. Both are now closed first
    private static void UnloadFolderOfTwoDeletedPcks(DirectoryInfo root, DirectoryInfo kernels, string moonPck)
    {
        var before = SearchesBefore();
        var (folder, x, d, _, withQ) = LoadPcksAroundAFolder(root, moonPck, 1999966, "IOA_G4_PCK_C");
        var handleX = KeeperHandle(x);
        var handleD = KeeperHandle(d);
        File.Delete(x.FullName);
        File.Delete(d.FullName);

        SpiceAPI.Instance.UnloadKernels(folder);

        Check(!Listed(root).Contains(folder.FullName), "the folder is no longer listed");
        CheckSameRotation(withQ, "IOA_G4_PCK_C", "Q keeps its precedence over P");
        CheckClosed(x, handleX);
        CheckClosed(d, handleD);
        CheckSearchesAfterManyDafs(root, kernels, before);
    }

    // Two copies A and B of the L2 SPK of Data/SolarSystem, B deleted after the load. Before the second round of #668, the
    // unload of A failed on B (unload_c --> UNLOAD --> SPKLEF --> DAFOPR), which stayed open in SPICE
    private static void UnloadSpkWhenAnotherIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var before = SearchesBefore();
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Spks"));
        var a = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "a.bsp");
        var b = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "b.bsp");
        SpiceAPI.Instance.LoadKernels(a);
        SpiceAPI.Instance.LoadKernels(b);
        var handle = KeeperHandle(b);
        File.Delete(b.FullName);

        SpiceAPI.Instance.UnloadKernels(a);

        CheckClosed(b, handle);
        CheckSearchesAfterManyDafs(root, kernels, before);
        SpiceAPI.Instance.UnloadKernels(b);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // A PCK P of a frame class Data/SolarSystem does not define, deleted after its load and never unloaded. Before the second
    // round of #668, it stayed open, and once its logical unit was released the PCK searches of IAU_EARTH failed on it. The
    // next load, of an unrelated text kernel, closes it
    private static void LoadWhenAPckWasDeleted(DirectoryInfo root, DirectoryInfo kernels, string moonPck)
    {
        var before = SearchesBefore();
        var p = TestKernelWriter.WritePck(moonPck, new DirectoryInfo(Path.Combine(root.FullName, "Pck")), "p.bpc", 1999967);
        SpiceAPI.Instance.LoadKernels(p);
        var handle = KeeperHandle(p);
        File.Delete(p.FullName);

        var text = TestKernelWriter.WriteFrameKernel(new DirectoryInfo(Path.Combine(root.FullName, "Text")), "t.tf", 1999968, "IOA_G4_TEXT", 90.0);
        SpiceAPI.Instance.LoadKernels(text);

        Check(System.Math.Abs(RotationAngleDeg("IOA_G4_TEXT") - 90.0) < 1e-12, "the text kernel's frame is rotated by 90°");
        CheckClosed(p, handle);
        CheckSearchesAfterManyDafs(root, kernels, before);
        SpiceAPI.Instance.UnloadKernels(text);
        SpiceAPI.Instance.UnloadKernels(p);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // Two copies A and B of the CK of Data/CLPS, B deleted after the load. Before the second round of #668, the unload of A
    // failed on B (unload_c --> UNLOAD --> CKLPF --> DAFOPR), which stayed open in SPICE
    private static void UnloadCkWhenAnotherIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var before = SearchesBefore();
        var ck = Path.Combine(kernels.Parent!.FullName, "CLPS", "clps_to2ab_apm1_cru_rec_240108_240118_v02.bc");
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Cks"));
        var a = Copy(ck, folder, "a.bc");
        var b = Copy(ck, folder, "b.bc");
        SpiceAPI.Instance.LoadKernels(a);
        SpiceAPI.Instance.LoadKernels(b);
        var handle = KeeperHandle(b);
        File.Delete(b.FullName);

        SpiceAPI.Instance.UnloadKernels(a);

        CheckClosed(b, handle);
        CheckSearchesAfterManyDafs(root, kernels, before);
        SpiceAPI.Instance.UnloadKernels(b);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // Two copies A and B of the L2 SPK of Data/SolarSystem, B deleted after the load, then an SPK C written twice. The native
    // writer unloads the file it writes when its kernel object is destroyed, and the file it replaces before writing, each an
    // SPK unload that opens the other SPKs again (unload_c --> UNLOAD --> SPKLEF). Before the second round of
    // #668 those unloads did not close the deleted kernels first: the reopening failed on B, which stayed open in SPICE, and
    // the failure was left pending, so that the next kernel call threw it
    private static void WriteSpkWhenAnotherIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var before = SearchesBefore();
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Spks"));
        var a = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "a.bsp");
        var b = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "b.bsp");
        SpiceAPI.Instance.LoadKernels(a);
        SpiceAPI.Instance.LoadKernels(b);
        var handle = KeeperHandle(b);
        File.Delete(b.FullName);

        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        SpiceAPI.Instance.WriteEphemeris(c, -1999971, EarthStates());

        CheckClosed(b, handle);
        SpiceAPI.Instance.WriteEphemeris(c, -1999971, EarthStates());
        SpiceAPI.Instance.UnloadKernels(a);
        CheckSearchesAfterManyDafs(root, kernels, before);
        SpiceAPI.Instance.UnloadKernels(b);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // Two CKs A and B of a spacecraft, written by the library and loaded, B deleted after the load, then a CK C written. The
    // native writer unloads the CK it writes when its kernel object is destroyed, which opens the other CKs again
    // (unload_c --> UNLOAD --> CKLPF): before the second round of #668 the reopening failed on B, which stayed open in SPICE,
    // and the failure was left pending, so that the next kernel call threw it
    private static void WriteCkWhenAnotherIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var before = SearchesBefore();
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Cks"));
        var clock = new Clock("IOA_G4_CLOCK", 65536);
        var spacecraft = new Spacecraft(-1979, "IOA_G4_SPC", 3000.0, 10000.0, clock, EarthStates()[0]);
        var clockFile = new FileInfo(Path.Combine(folder.FullName, "clock.tsc"));
        clock.WriteAsync(clockFile).GetAwaiter().GetResult();
        SpiceAPI.Instance.LoadKernels(clockFile);
        var records = Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();
        var a = new FileInfo(Path.Combine(folder.FullName, "a.bc"));
        var b = new FileInfo(Path.Combine(folder.FullName, "b.bc"));
        SpiceAPI.Instance.WriteOrientation(a, spacecraft.NaifId, records);
        SpiceAPI.Instance.WriteOrientation(b, spacecraft.NaifId, records);
        SpiceAPI.Instance.LoadKernels(a);
        SpiceAPI.Instance.LoadKernels(b);
        var handle = KeeperHandle(b);
        File.Delete(b.FullName);

        SpiceAPI.Instance.WriteOrientation(new FileInfo(Path.Combine(folder.FullName, "c.bc")), spacecraft.NaifId, records);

        CheckClosed(b, handle);
        SpiceAPI.Instance.UnloadKernels(a);
        CheckSearchesAfterManyDafs(root, kernels, before);
        SpiceAPI.Instance.UnloadKernels(b);
        SpiceAPI.Instance.UnloadKernels(clockFile);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // A meta-kernel that names its SPK relative to the working directory of its load, as NAIF's examples do: SPICE holds the
    // SPK under that relative name. Once the working directory changed, the name no longer reaches the file, which still exists
    // and which SPICE holds open. Tested by that name, the next load of any kernel would take the SPK for deleted and unload it
    // (the first version of the cleanup of the second round of #668 did). The relative name is left to SPICE: the object of the SPK, written by the library, is read
    // bit for bit after the load as before it, KEEPER keeps its handle open, and the meta-kernel's unload unloads it
    private static void LoadAfterTheWorkingDirectoryChanged(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Relative"));
        var spk = new FileInfo(Path.Combine(folder.FullName, "x.bsp"));
        SpiceAPI.Instance.WriteEphemeris(spk, -1999972, EarthStates());
        var metaKernel = new FileInfo(Path.Combine(folder.FullName, "mk.tm"));
        File.WriteAllText(metaKernel.FullName, "KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( 'x.bsp' )\n\\begintext\n");
        var workingDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(folder.FullName);
            SpiceAPI.Instance.LoadKernels(metaKernel);
        }
        finally
        {
            Directory.SetCurrentDirectory(workingDirectory);
        }

        var handle = OperatingSystem.IsLinux() ? CspiceDirect.LoadedKernelHandle("x.bsp") : null;
        Check(!OperatingSystem.IsLinux() || handle is > 0, $"KEEPER holds the SPK under its relative name ({handle})");
        var read = ReadRelativeObject();

        var text = TestKernelWriter.WriteFrameKernel(new DirectoryInfo(Path.Combine(root.FullName, "Text")), "t.tf", 1999973, "IOA_G4_REL", 90.0);
        SpiceAPI.Instance.LoadKernels(text);

        var after = ReadRelativeObject();
        Check(after.Position == read.Position && after.Velocity == read.Velocity,
            $"the SPK's object after the load is the state read before ({after.Position} against {read.Position})");
        if (OperatingSystem.IsLinux())
        {
            Check(CspiceDirect.LoadedKernelHandle("x.bsp") == handle, "KEEPER still holds the SPK with its handle");
            Check(CspiceDirect.OpenDafFile(handle!.Value) != null, $"the handle {handle} of the SPK is still open");
        }

        SpiceAPI.Instance.UnloadKernels(text);
        SpiceAPI.Instance.UnloadKernels(metaKernel);
        Check(Throws(() => ReadRelativeObject()) != null, "the SPK's object is no longer known once the meta-kernel is unloaded");
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // A meta-kernel that names an SPK X relative to the working directory of its load, X deleted, then an SPK written by
    // the native writer, whose kernel object unloads the file it wrote when it is destroyed: that unload opens the other SPKs
    // again (unload_c --> UNLOAD --> SPKLEF --> DAFOPR) and fails on X, which the native drop leaves to SPICE (a relative
    // name). Before the fourth round of #668 the failure came after the proxy's last check: WriteEphemeris returned, and the
    // next kernel call, the read of the Moon, threw "Celestial celestialItem name can't be defined". WriteEphemeris now throws
    // SPICE's message and leaves nothing pending: the Moon is read bit for bit as before, twice
    private static void WriteSpkWhenAnSpkHeldRelativelyIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "RelativeSpk"));
        var x = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "x.bsp");
        LoadRelativeMetaKernel(folder, "x.bsp");
        var before = ReadMoon();
        File.Delete(x.FullName);

        var error = Throws(() => SpiceAPI.Instance.WriteEphemeris(new FileInfo(Path.Combine(folder.FullName, "c.bsp")), -1999976, EarthStates()));

        Check(error.Message.Contains("x.bsp"), $"the exception names x.bsp ({error.Message})");
        CheckSameMoon(before, "the first read after the writer");
        CheckSameMoon(before, "the second read after the writer");
    }

    // The CK analog: a meta-kernel that names a CK X relatively, X deleted, then a CK written by the native writer, whose
    // kernel object's unload of the written CK opens the other CKs again (unload_c --> UNLOAD --> CKLPF) and fails on X
    private static void WriteCkWhenACkHeldRelativelyIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "RelativeCk"));
        var clock = new Clock("IOA_J1_CLOCK", 65536);
        var spacecraft = new Spacecraft(-1977, "IOA_J1_SPC", 3000.0, 10000.0, clock, EarthStates()[0]);
        var clockFile = new FileInfo(Path.Combine(folder.FullName, "clock.tsc"));
        clock.WriteAsync(clockFile).GetAwaiter().GetResult();
        SpiceAPI.Instance.LoadKernels(clockFile);
        var records = Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();
        var x = new FileInfo(Path.Combine(folder.FullName, "x.bc"));
        SpiceAPI.Instance.WriteOrientation(x, spacecraft.NaifId, records);
        LoadRelativeMetaKernel(folder, "x.bc");
        var before = ReadMoon();
        File.Delete(x.FullName);

        var error = Throws(() =>
            SpiceAPI.Instance.WriteOrientation(new FileInfo(Path.Combine(folder.FullName, "c.bc")), spacecraft.NaifId, records));

        Check(error.Message.Contains("x.bc"), $"the exception names x.bc ({error.Message})");
        CheckSameMoon(before, "the first read after the writer");
        CheckSameMoon(before, "the second read after the writer");
    }

    // Loads the meta-kernel mk.tm of the folder, which names the file relatively, with the working directory set to the folder
    private static void LoadRelativeMetaKernel(DirectoryInfo folder, string name)
    {
        var metaKernel = new FileInfo(Path.Combine(folder.FullName, "mk.tm"));
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '{name}' )\n\\begintext\n");
        var workingDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(folder.FullName);
            SpiceAPI.Instance.LoadKernels(metaKernel);
        }
        finally
        {
            Directory.SetCurrentDirectory(workingDirectory);
        }
    }

    private static StateVector ReadMoon() => SpiceAPI.Instance.ReadEphemeris(Epoch, 399, 301, Frame.ICRF, Aberration.None).ToStateVector();

    private static void CheckSameMoon(StateVector before, string what)
    {
        var moon = ReadMoon();
        Check(moon.Position == before.Position && moon.Velocity == before.Velocity,
            $"{what}: the Moon is the state read before ({moon.Position} against {before.Position})");
    }

    private static StateVector ReadRelativeObject() =>
        SpiceAPI.Instance.ReadEphemeris(Epoch.AddSeconds(60.0), 399, -1999972, Frame.ICRF, Aberration.None).ToStateVector();

    // Three states of a circular orbit about the Earth in ICRF, 60 s apart from the epoch
    private static StateVector[] EarthStates()
    {
        var earth = new global::IO.Astrodynamics.Body.CelestialBody(399);
        return Enumerable.Range(0, 3).Select(i => new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0), earth,
            Epoch.AddSeconds(60.0 * i), Frame.ICRF)).ToArray();
    }

    private static FileInfo Copy(string source, DirectoryInfo folder, string name)
    {
        var file = new FileInfo(Path.Combine(folder.FullName, name));
        File.Copy(source, file.FullName);
        return file;
    }

    // The handle KEEPER holds for a loaded file, read through CSPICE on Linux only (0 elsewhere)
    private static int KeeperHandle(FileInfo file)
    {
        if (!OperatingSystem.IsLinux()) return 0;
        var handle = CspiceDirect.LoadedKernelHandle(file.FullName);
        Check(handle is > 0, $"KEEPER holds {file.Name} with a handle ({handle})");
        return handle!.Value;
    }

    // KEEPER no longer holds the file, and its handle is no longer an open DAF (Linux only)
    private static void CheckClosed(FileInfo file, int handle)
    {
        if (!OperatingSystem.IsLinux()) return;
        Check(CspiceDirect.LoadedKernelHandle(file.FullName) == null, $"KEEPER no longer holds {file.Name}");
        Check(CspiceDirect.OpenDafFile(handle) == null, $"the handle {handle} of {file.Name} is no longer open");
    }

    // The IAU_EARTH rotation (its PCK search goes through every binary PCK) and the state of the Moon from the Earth (de440s,
    // loaded before every kernel of the scenarios, so that its search passes every SPK loaded after it)
    private static (Quaternion IauEarth, StateVector Moon) SearchesBefore() =>
        (SpiceAPI.Instance.TransformFrame(Epoch, Frame.ICRF, new Frame("IAU_EARTH")).Rotation,
            SpiceAPI.Instance.ReadEphemeris(Epoch, 399, 301, Frame.ICRF, Aberration.None).ToStateVector());

    // Loads 25 copies of the L1 SPK, then reads IAU_EARTH and the Moon: bit for bit what they were before the scenario
    private static void CheckSearchesAfterManyDafs(DirectoryInfo root, DirectoryInfo kernels, (Quaternion IauEarth, StateVector Moon) before)
    {
        var many = Directory.CreateDirectory(Path.Combine(root.FullName, "Many"));
        for (int i = 0; i < ManyDafs; i++)
        {
            Copy(Path.Combine(kernels.FullName, "L1_de431.bsp"), many, $"x{i:D2}.bsp");
        }

        SpiceAPI.Instance.LoadKernels(many);
        try
        {
            var q = SpiceAPI.Instance.TransformFrame(Epoch, Frame.ICRF, new Frame("IAU_EARTH")).Rotation;
            Check(q.W == before.IauEarth.W && q.VectorPart == before.IauEarth.VectorPart,
                $"IAU_EARTH after {ManyDafs} more DAFs is the rotation read before ({q} against {before.IauEarth})");
            var moon = SpiceAPI.Instance.ReadEphemeris(Epoch, 399, 301, Frame.ICRF, Aberration.None).ToStateVector();
            Check(moon.Position == before.Moon.Position && moon.Velocity == before.Moon.Velocity,
                $"the Moon after {ManyDafs} more DAFs is the state read before ({moon.Position} against {before.Moon.Position})");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(many);
        }
    }

    // A binary PCK D, deleted after its load, and a folder of a meta-kernel that names another PCK, listed before a text
    // kernel. Before the second round of #668, the meta-kernel's unload unloaded that PCK, which opened D again and failed,
    // and the text kernel's unload, which reads the text kernels again, not the PCKs, repaired nothing: the unload threw
    // (measured with CSPICE N0067). D is now closed and unloaded first, and the unload returns
    private static void UnloadMetaKernelOfAPck(DirectoryInfo root, string moonPck)
    {
        var gone = TestKernelWriter.WritePck(moonPck, new DirectoryInfo(Path.Combine(root.FullName, "Deleted")), "d.bpc", 1999952);
        var pck = TestKernelWriter.WritePck(moonPck, new DirectoryInfo(Path.Combine(root.FullName, "Pck")), "p.bpc", 1999953);
        var folder = new DirectoryInfo(Path.Combine(root.FullName, "Batch"));
        var text = TestKernelWriter.WriteFrameKernel(folder, "x.tf", 1999954, "IOA_F02_MKP_X", 90.0);
        var metaKernel = TestKernelWriter.ListedBefore(folder, text,
            stem => TestKernelWriter.WriteMetaKernel(new FileInfo(Path.Combine(folder.FullName, $"{stem}.tm")), pck.FullName));
        SpiceAPI.Instance.LoadKernels(gone);
        SpiceAPI.Instance.LoadKernels(folder);
        Check(System.Math.Abs(RotationAngleDeg("IOA_F02_MKP_X") - 90.0) < 1e-12, "the text kernel's frame is rotated by 90°");

        var handle = KeeperHandle(gone);
        File.Delete(gone.FullName);

        SpiceAPI.Instance.UnloadKernels(folder);

        Check(Listed(root).SequenceEqual([gone.FullName]), "only the deleted PCK is listed");
        CheckClosed(gone, handle);
        Check(CspiceDirectHolds(metaKernel) != true, "KEEPER no longer holds the meta-kernel");

        // The unload of the deleted PCK, which SPICE no longer holds, does nothing
        SpiceAPI.Instance.UnloadKernels(gone);
        Check(Listed(root).Length == 0, "nothing is listed");
        Check(Throws(() => RotationAngleDeg("IOA_F02_MKP_X")) != null, "the text kernel's frame is unknown");
    }

    // A PCK frame of the class classId, centred on the Moon, and three loads: a PCK P of that class relative to J2000, a
    // folder of two PCKs of other classes (X listed before D), then a PCK Q of that class relative to ECLIPJ2000 (17), which
    // takes precedence over P (NAIF, PCK Required Reading: the PCK loaded last). Returns the folder, X, D and the rotations
    // of the frame from ICRF with P alone and with Q
    private static (DirectoryInfo Folder, FileInfo X, FileInfo D, Quaternion WithP, Quaternion WithQ) LoadPcksAroundAFolder(DirectoryInfo root,
        string moonPck, int classId, string frameName)
    {
        var frame = new FileInfo(Path.Combine(root.FullName, "Frame", "pck.tf"));
        frame.Directory!.Create();
        File.WriteAllText(frame.FullName,
            $"KPL/FK\n\\begindata\nFRAME_{frameName} = {classId}\nFRAME_{classId}_NAME = '{frameName}'\nFRAME_{classId}_CLASS = 2\n" +
            $"FRAME_{classId}_CLASS_ID = {classId}\nFRAME_{classId}_CENTER = 301\n\\begintext\n");
        var p = TestKernelWriter.WritePck(moonPck, new DirectoryInfo(Path.Combine(root.FullName, "Before")), "p.bpc", classId);
        var folder = new DirectoryInfo(Path.Combine(root.FullName, "Pcks"));
        var d = TestKernelWriter.WritePck(moonPck, folder, "d.bpc", classId + 2);
        var x = TestKernelWriter.ListedBefore(folder, d, stem => TestKernelWriter.WritePck(moonPck, folder, $"{stem}.bpc", classId + 1));
        var q = TestKernelWriter.WritePck(moonPck, new DirectoryInfo(Path.Combine(root.FullName, "After")), "q.bpc", classId, 17);
        SpiceAPI.Instance.LoadKernels(frame);
        SpiceAPI.Instance.LoadKernels(p);
        SpiceAPI.Instance.LoadKernels(folder);
        var withP = Rotation(frameName);
        SpiceAPI.Instance.LoadKernels(q);
        var withQ = Rotation(frameName);

        // The two rotations differ by the obliquity of the ecliptic, about 23.4°
        Check(AngleBetweenDeg(withP, withQ) > 20.0, "P and Q give rotations about 23.4° apart");
        return (folder, x, d, withP, withQ);
    }

    // Whether KEEPER holds the file (Linux only; null elsewhere)
    private static bool? CspiceDirectHolds(FileInfo file) =>
        OperatingSystem.IsLinux() ? CspiceDirect.LoadedKernelHandle(file.FullName) != null : null;

    private static Quaternion Rotation(string frameName) => SpiceAPI.Instance.TransformFrame(Epoch, new Frame(frameName), Frame.ICRF).Rotation;

    // The rotation angle of the frame from ICRF, in degrees (the sign of a quaternion is free)
    private static double RotationAngleDeg(string frameName)
    {
        var q = Rotation(frameName);
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(q.W))) * Constants.Rad2Deg;
    }

    // The angle of the rotation from a to b, in degrees
    private static double AngleBetweenDeg(Quaternion a, Quaternion b)
    {
        double dot = a.W * b.W + a.VectorPart.X * b.VectorPart.X + a.VectorPart.Y * b.VectorPart.Y + a.VectorPart.Z * b.VectorPart.Z;
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(dot))) * Constants.Rad2Deg;
    }

    // The same rotation to 9 decimals of a degree, as xUnit's Assert.Equal(0.0, angle, 9) of the tests this scenario was
    private static void CheckSameRotation(Quaternion expected, string frameName, string what)
    {
        double angle = AngleBetweenDeg(expected, Rotation(frameName));
        Check(System.Math.Round(angle, 9) == 0.0, $"{what} (angle {angle}°)");
    }

    // The paths listed under the root
    private static string[] Listed(DirectoryInfo root) => SpiceAPI.Instance.GetLoadedKernels().Select(k => k.FullName)
        .Where(k => k == root.FullName || k.StartsWith(root.FullName + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();

    private static InvalidOperationException Throws(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException e)
        {
            return e;
        }

        throw new InvalidOperationException("expected an InvalidOperationException, none was thrown");
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed: {what}");
        }
    }
}
