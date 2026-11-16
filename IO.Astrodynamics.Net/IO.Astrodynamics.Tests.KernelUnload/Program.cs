// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Surface;
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
    internal const string FolderOfADeletedSpk = "LoadKernels_WhenAnSpkOfALoadedFolderWasDeleted_ListsTheFolderUntilItsLastFileIsDropped";
    internal const string ManagedWriterOfAnSpk = "SpacecraftWriteEphemeris_ReplacingAFile_WhenAnSpkWasDeleted_NoLongerListsIt";
    internal const string WriterOfAnSpkRelative = "WriteEphemeris_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending";
    internal const string WriterOfACkRelative = "WriteOrientation_WhenACkHeldUnderARelativeNameWasDeleted_ThrowsAndLeavesNothingPending";
    internal const string ReplacerOfAnSpkRelative = "WriteEphemeris_ReplacingAFile_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndKeepsTheFile";
    internal const string ReplacerOfACkRelative = "WriteOrientation_ReplacingAFile_WhenACkHeldUnderARelativeNameWasDeleted_ThrowsAndKeepsTheFile";
    internal const string NoNativeCall = "LoadKernels_WithoutANativeCall_KeepsListingADeletedSpkSpiceStillHolds";
    internal const string DanglingLink = "LoadKernels_WhenTheTargetOfALinkedSpkWasDeleted_NoLongerListsTheLink";
    internal const string WriterOfOneState = "WriteEphemeris_OfOneState_ThrowsArgumentException";
    internal const string WriterOfTwoStates = "WriteEphemeris_OfTwoStates_ThrowsArgumentException";
    internal const string WriterOfTwoFrames = "WriteEphemeris_InTwoFrames_ThrowsArgumentException";
    internal const string WriterAboutASpacecraft = "WriteEphemeris_AboutASpacecraft_ThrowsArgumentException";
    internal const string SiteWriterOfTwoStates = "SiteWriteEphemeris_OverAWindowOfTwoStates_ThrowsArgumentException";
    internal const string LinkLoop = "WritersAndLoadKernels_OfASymbolicLinkLoop_ThrowInvalidOperationException";
    internal const string ReplacerOfALoadedFolderFile = "WriteEphemeris_ReplacingAFileOfALoadedFolder_UnloadsIt";
    internal const string ReplacerOfALoadedCk = "WriteOrientation_ReplacingALoadedFile_UnloadsIt";
    internal const string ManagedReplacerOfALoadedFile = "SpacecraftWriteEphemeris_ReplacingALoadedFile_UnloadsIt";
    internal const string ManagedReplacerOfALoadedFileRelative =
        "SpacecraftWriteEphemeris_ReplacingALoadedFile_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndKeepsTheFile";
    internal const string ManagedReplacerOfAMetaKernelFileRelative =
        "SpacecraftWriteEphemeris_ReplacingAFileAMetaKernelHolds_WhenAnSpkHeldUnderARelativeNameWasDeleted_ThrowsAndKeepsTheFile";

    internal const string ConicAboutASpacecraftWithAGm = "ConvertConicElementsToStateVector_AboutASpacecraftWithAGm_ThrowsArgumentException";
    internal const string EquinoctialAboutASpacecraftWithAGm = "ConvertEquinoctialElementsToStateVector_AboutASpacecraftWithAGm_ThrowsArgumentException";
    internal const string ConicAboutABodyNoLongerNamed = "ConvertConicElementsToStateVector_AboutABodyNoLongerNamed_ThrowsArgumentException";

    private static readonly string[] Scenarios =
    [
        ConicAboutASpacecraftWithAGm, EquinoctialAboutASpacecraftWithAGm, ConicAboutABodyNoLongerNamed,
        FolderOfTwoPcks, PckOfAFolder, MetaKernelOfAPck, FolderOfTwoDeletedPcks, DeletedSpk, DeletedPckNeverUnloaded, DeletedCk, WriterOfAnSpk,
        WriterOfACk, RelativeName, FolderOfADeletedSpk, ManagedWriterOfAnSpk, WriterOfAnSpkRelative, WriterOfACkRelative,
        ReplacerOfAnSpkRelative, ReplacerOfACkRelative, NoNativeCall, DanglingLink, WriterOfOneState, WriterOfTwoStates, WriterOfTwoFrames,
        WriterAboutASpacecraft, SiteWriterOfTwoStates, LinkLoop, ReplacerOfALoadedFolderFile, ReplacerOfALoadedCk, ManagedReplacerOfALoadedFile,
        ManagedReplacerOfALoadedFileRelative, ManagedReplacerOfAMetaKernelFileRelative
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
                case FolderOfADeletedSpk:
                    LoadWhenAnSpkOfAFolderWasDeleted(root, kernels);
                    break;
                case ManagedWriterOfAnSpk:
                    ReplaceWithTheManagedWriterWhenAnSpkWasDeleted(root, kernels);
                    break;
                case WriterOfAnSpkRelative:
                    WriteSpkWhenAnSpkHeldRelativelyIsDeleted(root, kernels);
                    break;
                case WriterOfACkRelative:
                    WriteCkWhenACkHeldRelativelyIsDeleted(root, kernels);
                    break;
                case ReplacerOfAnSpkRelative:
                    ReplaceSpkWhenAnSpkHeldRelativelyIsDeleted(root, kernels);
                    break;
                case ReplacerOfACkRelative:
                    ReplaceCkWhenACkHeldRelativelyIsDeleted(root);
                    break;
                case NoNativeCall:
                    LoadWithoutANativeCallWhenAnSpkWasDeleted(root);
                    break;
                case DanglingLink:
                    LoadWhenTheTargetOfALinkedSpkWasDeleted(root);
                    break;
                case WriterOfOneState:
                    WriteEphemerisRejected(root, EarthStates().Take(1).ToArray());
                    break;
                case WriterOfTwoStates:
                    WriteEphemerisRejected(root, EarthStates().Take(2).ToArray());
                    break;
                case WriterOfTwoFrames:
                    WriteEphemerisRejected(root, EarthStatesInTwoFrames());
                    break;
                case WriterAboutASpacecraft:
                    WriteEphemerisRejected(root, StatesAboutASpacecraft());
                    break;
                case SiteWriterOfTwoStates:
                    WriteSiteEphemerisOfTwoStates(root);
                    break;
                case LinkLoop:
                    WriteAndLoadASymbolicLinkLoop(root);
                    break;
                case ReplacerOfALoadedFolderFile:
                    ReplaceAFileOfALoadedFolder(root);
                    break;
                case ReplacerOfALoadedCk:
                    ReplaceALoadedCk(root);
                    break;
                case ManagedReplacerOfALoadedFile:
                    ReplaceALoadedFileWithTheManagedWriter(root);
                    break;
                case ManagedReplacerOfALoadedFileRelative:
                    ReplaceALoadedFileWithTheManagedWriterWhenAnSpkHeldRelativelyIsDeleted(root, kernels);
                    break;
                case ManagedReplacerOfAMetaKernelFileRelative:
                    ReplaceAFileAMetaKernelHoldsWithTheManagedWriterWhenAnSpkHeldRelativelyIsDeleted(root, kernels);
                    break;
                case ConicAboutASpacecraftWithAGm:
                    ConvertAboutASpacecraftWithAGm(root, equinoctial: false);
                    break;
                case EquinoctialAboutASpacecraftWithAGm:
                    ConvertAboutASpacecraftWithAGm(root, equinoctial: true);
                    break;
                case ConicAboutABodyNoLongerNamed:
                    ConvertAboutABodyNoLongerNamed(root);
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
        // The native unload dropped D: the registry no longer lists it (fourth round of #668; before, until it was unloaded)
        Check(!Listed(root).Contains(d.FullName) && !Listed(root).Contains(folder.FullName), "the folder and D are no longer listed");
        CheckRegistryFollowsKeeper(root, x, d);
        CheckClosed(d, handle);
        CheckSearchesAfterManyDafs(root, kernels, before);
        var listed = Listed(root);
        SpiceAPI.Instance.UnloadKernels(d);
        Check(Listed(root).SequenceEqual(listed), "the unload of the dropped D changes nothing");
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
        CheckRegistryFollowsKeeper(root, x, d);
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

        Check(Listed(root).Length == 0, "nothing is listed once the unload of A dropped B");
        CheckRegistryFollowsKeeper(root, a, b);
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

        Check(Listed(root).SequenceEqual([text.FullName]), "only the text kernel is listed once the load dropped P");
        CheckRegistryFollowsKeeper(root, p, text);
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

        Check(Listed(root).Length == 0, "nothing is listed once the unload of A dropped B");
        CheckRegistryFollowsKeeper(root, a, b);
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

        Check(Listed(root).SequenceEqual([a.FullName]), "only A is listed once the writer dropped B");
        CheckRegistryFollowsKeeper(root, a, b);
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

        Check(Listed(root).SequenceEqual([clockFile.FullName, a.FullName]), "only the clock and A are listed once the writer dropped B");
        CheckRegistryFollowsKeeper(root, clockFile, a, b);
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

    // A loaded folder [X, D] of two copies of the L2 SPK, D deleted, then an unrelated text kernel loaded: the native load
    // drops D (closed by its handle, then unloaded). Before the fourth round of #668 the registry still held D in the folder's
    // entry, so that the unload of D split the folder; it now follows the native drop: the folder stays listed while X is
    // loaded, the unload of D does nothing, and the folder's unload unloads X. A folder [Y, Z] whose two files are deleted is
    // no longer listed after the next native call
    private static void LoadWhenAnSpkOfAFolderWasDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Folder"));
        var x = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "x.bsp");
        var d = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "d.bsp");
        SpiceAPI.Instance.LoadKernels(folder);
        var handle = KeeperHandle(d);
        File.Delete(d.FullName);

        var text = TestKernelWriter.WriteFrameKernel(new DirectoryInfo(Path.Combine(root.FullName, "Text")), "t.tf", 1999975, "IOA_J1_TEXT", 90.0);
        SpiceAPI.Instance.LoadKernels(text);

        Check(Listed(root).SequenceEqual([folder.FullName, text.FullName]), "the folder is still listed while X is loaded");
        CheckRegistryFollowsKeeper(root, x, d, text);
        CheckClosed(d, handle);
        SpiceAPI.Instance.UnloadKernels(d);
        Check(Listed(root).SequenceEqual([folder.FullName, text.FullName]), "the unload of the dropped D changes nothing");
        SpiceAPI.Instance.UnloadKernels(folder);
        Check(Listed(root).SequenceEqual([text.FullName]), "the folder's unload unloads X");
        Check(CspiceDirectHolds(x) != true, "KEEPER no longer holds X");

        var both = Directory.CreateDirectory(Path.Combine(root.FullName, "Both"));
        var y = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), both, "y.bsp");
        var z = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), both, "z.bsp");
        SpiceAPI.Instance.LoadKernels(both);
        File.Delete(y.FullName);
        File.Delete(z.FullName);
        SpiceAPI.Instance.UnloadKernels(text);
        Check(Listed(root).Length == 0, "the folder whose two files were dropped is no longer listed");
        CheckRegistryFollowsKeeper(root, y, z);
    }

    // Two copies A and B of the L2 SPK of Data/SolarSystem, B deleted after the load, then an SPK C the managed writer
    // (Spacecraft.WriteEphemeris) replaces: its unload of the file it replaces (UnloadKernelsProxy) drops B, which the
    // registry no longer lists (fourth round of #668)
    private static void ReplaceWithTheManagedWriterWhenAnSpkWasDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Spks"));
        var a = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "a.bsp");
        var b = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "b.bsp");
        var spacecraft = new Spacecraft(-1976, "IOA_J1_SPC", 3000.0, 10000.0, new Clock("IOA_J1_CLOCK", 65536), EarthStates()[0]);
        spacecraft.AddPropagatedStates(EarthStates());
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        spacecraft.WriteEphemeris(c);
        SpiceAPI.Instance.LoadKernels(a);
        SpiceAPI.Instance.LoadKernels(b);
        var handle = KeeperHandle(b);
        File.Delete(b.FullName);

        spacecraft.WriteEphemeris(c);

        Check(Listed(root).SequenceEqual([a.FullName]), "only A is listed once the managed writer dropped B");
        CheckRegistryFollowsKeeper(root, a, b);
        CheckClosed(b, handle);
        SpiceAPI.Instance.UnloadKernels(a);
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

    // An SPK C written, then a meta-kernel that names an SPK X relatively, X deleted, then C written again by the native
    // writer, with another object: its kernel object loads C, and WriteData unloads C before removing it. That unload opens the
    // other SPKs again (unload_c --> UNLOAD --> SPKLEF) and fails on X, left to SPICE (a relative name). Before the review of
    // the fourth round of #668 the writer still removed C, then wrote nothing on the pending error: the call threw with C
    // gone. It now stops before the removal: C is on disk as it was (its bytes before the call), KEEPER no longer holds it,
    // and the Moon is read bit for bit as before, twice
    private static void ReplaceSpkWhenAnSpkHeldRelativelyIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "ReplaceRelativeSpk"));
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        SpiceAPI.Instance.WriteEphemeris(c, -1999985, EarthStates());
        var bytes = File.ReadAllBytes(c.FullName);
        var x = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "x.bsp");
        LoadRelativeMetaKernel(folder, "x.bsp");
        var before = ReadMoon();
        File.Delete(x.FullName);

        var error = Throws(() => SpiceAPI.Instance.WriteEphemeris(c, -1999986, EarthStates()));

        Check(error.Message.Contains("x.bsp"), $"the exception names x.bsp ({error.Message})");
        Check(File.Exists(c.FullName) && File.ReadAllBytes(c.FullName).SequenceEqual(bytes), "the SPK it would replace is on disk as it was");
        Check(CspiceDirectHolds(c) != true, "KEEPER does not hold the SPK it would replace");
        CheckSameMoon(before, "the first read after the writer");
        CheckSameMoon(before, "the second read after the writer");
    }

    // The CK analog: a CK C written, a meta-kernel that names a CK X relatively, X deleted, then C written again with other
    // records: WriteOrientations unloads C (unload_c --> UNLOAD --> CKLPF), fails on X, and keeps C as it was
    private static void ReplaceCkWhenACkHeldRelativelyIsDeleted(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "ReplaceRelativeCk"));
        var clock = new Clock("IOA_J1_CLOCK", 65536);
        var spacecraft = new Spacecraft(-1978, "IOA_J1_SPC", 3000.0, 10000.0, clock, EarthStates()[0]);
        var clockFile = new FileInfo(Path.Combine(folder.FullName, "clock.tsc"));
        clock.WriteAsync(clockFile).GetAwaiter().GetResult();
        SpiceAPI.Instance.LoadKernels(clockFile);
        var records = Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bc"));
        SpiceAPI.Instance.WriteOrientation(c, spacecraft.NaifId, records);
        var bytes = File.ReadAllBytes(c.FullName);
        var x = new FileInfo(Path.Combine(folder.FullName, "x.bc"));
        SpiceAPI.Instance.WriteOrientation(x, spacecraft.NaifId, records);
        LoadRelativeMetaKernel(folder, "x.bc");
        var before = ReadMoon();
        File.Delete(x.FullName);

        var turned = Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(new Quaternion(0.0, 1.0, 0.0, 0.0), Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();
        var error = Throws(() => SpiceAPI.Instance.WriteOrientation(c, spacecraft.NaifId, turned));

        Check(error.Message.Contains("x.bc"), $"the exception names x.bc ({error.Message})");
        Check(File.Exists(c.FullName) && File.ReadAllBytes(c.FullName).SequenceEqual(bytes), "the CK it would replace is on disk as it was");
        Check(CspiceDirectHolds(c) != true, "KEEPER does not hold the CK it would replace");
        CheckSameMoon(before, "the first read after the writer");
        CheckSameMoon(before, "the second read after the writer");
    }

    // #706. The native writer rejected fewer than three states, states in several frames and a centre it cannot read as a
    // celestial body with a C++ exception across P/Invoke: the process ended (std::terminate, exit code 134, measured on
    // c533a6e). SpiceAPI.WriteEphemeris now rejects them with an ArgumentException naming stateVectors before the native
    // call: nothing is written, and the process goes on (the Moon read after it, bit for bit as before)
    private static void WriteEphemerisRejected(DirectoryInfo root, StateVector[] states)
    {
        var before = ReadMoon();
        var file = new FileInfo(Path.Combine(root.FullName, "rejected.bsp"));

        var error = ThrowsArgument(() => SpiceAPI.Instance.WriteEphemeris(file, -1999705, states));

        Check(error.ParamName == "stateVectors", $"the exception names stateVectors ({error.ParamName}: {error.Message})");
        Check(!File.Exists(file.FullName), "nothing is written");
        CheckSameMoon(before, "the read after the rejected writer");
    }

    // Three states of EarthStates, the last one in ECLIPJ2000
    private static StateVector[] EarthStatesInTwoFrames()
    {
        var states = EarthStates();
        states[2] = new StateVector(states[2].Position, states[2].Velocity, states[2].Observer, states[2].Epoch, Frame.ECLIPTIC_J2000);
        return states;
    }

    // Three states about a spacecraft: the native writer reads its centre as a celestial body (CelestialBody(int)), whose GM
    // read failed and returned an uninitialised value (measured on c533a6e: an InvalidOperationException through that path)
    private static StateVector[] StatesAboutASpacecraft()
    {
        var spacecraft = new Spacecraft(-1999902, "IOA_K1_SPC", 3000.0, 10000.0, new Clock("IOA_K1_CLOCK", 65536), EarthStates()[0]);
        return EarthStates().Select(x => new StateVector(x.Position, x.Velocity, spacecraft, x.Epoch, Frame.ICRF)).ToArray();
    }

    // A site over 90 s: its 1-minute step gives two states, which the native writer rejected (exit code 134, measured on
    // c533a6e). The ArgumentException of SpiceAPI.WriteEphemeris reaches the caller of the site
    private static void WriteSiteEphemerisOfTwoStates(DirectoryInfo root)
    {
        var before = ReadMoon();
        var site = new Site(339, "S339", new global::IO.Astrodynamics.Body.CelestialBody(399),
            new Planetodetic(30.0 * Constants.Deg2Rad, 10.0 * Constants.Deg2Rad, 1000.0));
        var file = new FileInfo(Path.Combine(root.FullName, "site.bsp"));

        var error = ThrowsArgument(() => site.WriteEphemeris(new Window(Epoch, Epoch.AddSeconds(90.0)), file));

        Check(error.ParamName == "stateVectors", $"the exception names stateVectors ({error.ParamName}: {error.Message})");
        Check(!File.Exists(file.FullName), "nothing is written");
        CheckSameMoon(before, "the read after the rejected writer");
    }

    // A loop of symbolic links a.bsp -> b.bsp -> a.bsp: the native layer's std::filesystem calls (exists in the kernel
    // objects, is_regular_file in KernelsLoader) threw filesystem_error (ELOOP) across P/Invoke, which ended the process
    // (exit code 134, measured on c533a6e). The writers and LoadKernels now throw InvalidOperationException with that
    // message; nothing is loaded, and the process goes on
    private static void WriteAndLoadASymbolicLinkLoop(DirectoryInfo root)
    {
        var before = ReadMoon();
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Loop"));
        var a = new FileInfo(Path.Combine(folder.FullName, "a.bsp"));
        var b = new FileInfo(Path.Combine(folder.FullName, "b.bsp"));
        File.CreateSymbolicLink(a.FullName, b.FullName);
        File.CreateSymbolicLink(b.FullName, a.FullName);
        var records = Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();

        var spk = Throws(() => SpiceAPI.Instance.WriteEphemeris(a, -1999706, EarthStates()));
        var ck = Throws(() => SpiceAPI.Instance.WriteOrientation(a, -1999706, records));
        var load = Throws(() => SpiceAPI.Instance.LoadKernels(a));

        foreach (var (error, what) in new[] { (spk, "WriteEphemeris"), (ck, "WriteOrientation"), (load, "LoadKernels") })
        {
            Check(error.Message.Contains("symbolic links"), $"{what}: the exception gives the file system's error ({error.Message})");
        }

        Check(Listed(root).Length == 0, "nothing is listed");
        CheckRegistryFollowsKeeper(root, a);
        CheckSameMoon(before, "the read after the writers");
    }

    // Three states of a circular orbit 5 km farther than EarthStates, and one more, 60 s later: a coverage the file it
    // replaces does not have
    private static StateVector[] OtherEarthStates()
    {
        var earth = new global::IO.Astrodynamics.Body.CelestialBody(399);
        return Enumerable.Range(0, 4).Select(i => new StateVector(new Vector3(6805000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth,
            Epoch.AddSeconds(60.0 * i), Frame.ICRF)).ToArray();
    }

    // The folder [U, V] of two SPKs the native writer wrote, loaded, then U written again by the native writer. Its kernel
    // object loaded U once more and WriteData unloaded it once: the folder's load was left in KEEPER, which held the replaced
    // U, deleted but open, and read its data, while the registry listed the folder (measured on c533a6e: the replaced
    // states read until U was unloaded). The writer now unloads a file loaded through LoadKernels first, as UnloadKernels
    // does: the folder is replaced by V, KEEPER no longer holds U and its handle is closed, U's object is not read until U
    // is loaded again, and then it is read from the new file: the states written. References: KEEPER (CspiceDirect, Linux)
    // and the states written
    private static void ReplaceAFileOfALoadedFolder(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Folder"));
        var u = new FileInfo(Path.Combine(folder.FullName, "u.bsp"));
        var v = new FileInfo(Path.Combine(folder.FullName, "v.bsp"));
        SpiceAPI.Instance.WriteEphemeris(u, -1999707, EarthStates());
        SpiceAPI.Instance.WriteEphemeris(v, -1999708, EarthStates());
        SpiceAPI.Instance.LoadKernels(folder);
        var handle = KeeperHandle(u);
        var written = OtherEarthStates();

        SpiceAPI.Instance.WriteEphemeris(u, -1999707, written);

        Check(Listed(root).SequenceEqual([v.FullName]), $"the folder is replaced by V ({string.Join(", ", Listed(root))})");
        CheckRegistryFollowsKeeper(root, u, v);
        CheckClosed(u, handle);
        Throws(() => ReadObject(-1999707));
        SpiceAPI.Instance.LoadKernels(u);
        var read = ReadObject(-1999707);
        Check(read.Position == written[1].Position && read.Velocity == written[1].Velocity,
            $"the new file gives the state written ({read.Position} against {written[1].Position})");
        SpiceAPI.Instance.UnloadKernels(u);
        SpiceAPI.Instance.UnloadKernels(v);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // The CK analog: a CK C of identity records, loaded, then written again with five records of 90° about z. Before, the
    // load was left in KEEPER, which read the replaced identity records (measured on c533a6e). C is now unloaded first: it
    // is no longer listed, no orientation is read until it is loaded again, and then the 90° records. References: KEEPER and
    // the records written, compared as a rotation angle
    private static void ReplaceALoadedCk(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Ck"));
        var clock = new Clock("IOA_K1_CLOCK", 65536);
        var spacecraft = new Spacecraft(-1999709, "IOA_K1_SPC", 3000.0, 10000.0, clock, EarthStates()[0]);
        var clockFile = new FileInfo(Path.Combine(folder.FullName, "clock.tsc"));
        clock.WriteAsync(clockFile).GetAwaiter().GetResult();
        SpiceAPI.Instance.LoadKernels(clockFile);
        var identity = Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();
        // Another CK of the spacecraft, outside the epochs read, stays loaded: with no CK loaded at all, a failed read leaves
        // SPICE's CKBSS error pending for the next call (ReadOrientationProxy, outside #706)
        var keep = new FileInfo(Path.Combine(folder.FullName, "keep.bc"));
        SpiceAPI.Instance.WriteOrientation(keep, spacecraft.NaifId, Enumerable.Range(100, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray());
        SpiceAPI.Instance.LoadKernels(keep);
        var turned = new Quaternion(Vector3.VectorZ, System.Math.PI / 2.0);
        var written = Enumerable.Range(0, 5)
            .Select(i => new StateOrientation(turned, Vector3.Zero, Epoch.AddSeconds(i), Frame.ICRF)).ToArray();
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bc"));
        SpiceAPI.Instance.WriteOrientation(c, spacecraft.NaifId, identity);
        SpiceAPI.Instance.LoadKernels(c);
        var handle = KeeperHandle(c);

        SpiceAPI.Instance.WriteOrientation(c, spacecraft.NaifId, written);

        Check(Listed(root).SequenceEqual([clockFile.FullName, keep.FullName]), $"the clock and the other CK are listed ({string.Join(", ", Listed(root))})");
        CheckRegistryFollowsKeeper(root, clockFile, keep, c);
        CheckClosed(c, handle);
        Throws(() => ReadOrientationAt(spacecraft.NaifId, 1.0));
        SpiceAPI.Instance.LoadKernels(c);
        var q = ReadOrientationAt(spacecraft.NaifId, 4.0).Rotation.Conjugate() * turned;
        double angle = 2.0 * System.Math.Atan2(q.VectorPart.Magnitude(), System.Math.Abs(q.W));
        Console.WriteLine($"MEASURE #706 CK replaced while loaded (child): angle between the record written and the one read {angle:E3} rad");
        Check(angle <= CkRecordAngleBound, $"the new file gives the record written ({angle:E3} rad)");
        SpiceAPI.Instance.UnloadKernels(c);
        SpiceAPI.Instance.UnloadKernels(keep);
        SpiceAPI.Instance.UnloadKernels(clockFile);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // #711. Elements about a spacecraft whose GM a text kernel gives: the native conversions built the centre as a celestial
    // body (CelestialBody(int)), whose name SPICE does not know, and threw SDKException across P/Invoke: the process ended
    // (exit code 134, measured on 46e6975). SpiceAPI now rejects a centre that is not a celestial body with an
    // ArgumentException naming the elements, before the native call, and the process goes on
    private static void ConvertAboutASpacecraftWithAGm(DirectoryInfo root, bool equinoctial)
    {
        var before = ReadMoon();
        var gm = WriteTextKernel(root, "gm.tpc", "BODY-1999820_GM = ( 1.0E-10 )");
        SpiceAPI.Instance.LoadKernels(gm);
        var spacecraft = new Spacecraft(-1999820, "IOA_711_SPC", 3000.0, 10000.0, new Clock("IOA_711_CLOCK", 65536), EarthStates()[0]);
        var elements = new KeplerianElements(7.0e6, 0.01, 0.2, 0.3, 0.4, 0.5, spacecraft, Epoch, Frame.ICRF);

        var error = equinoctial
            ? ThrowsArgument(() => SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(elements.ToEquinoctial()))
            : ThrowsArgument(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(elements));

        string parameter = equinoctial ? "equinoctialElements" : "keplerianElements";
        Check(error.ParamName == parameter, $"the exception names {parameter} ({error.ParamName}: {error.Message})");
        SpiceAPI.Instance.UnloadKernels(gm);
        CheckSameMoon(before, "the read after the rejected conversion");
        CheckMoonAsSpice("the read after the rejected conversion");
    }

    // A body SPICE named when it was built (its frame J2000, OBJECT_1999821_FRAME, Frames Required Reading), then no longer:
    // the kernel of its name and frame unloaded, the kernel of its GM kept. The
    // native conversion read its GM and threw SDKException for its name (exit code 134 on 46e6975); SpiceAPI now rejects an
    // identifier SPICE does not name with the ArgumentException of #680, naming the elements, before the native call
    private static void ConvertAboutABodyNoLongerNamed(DirectoryInfo root)
    {
        var before = ReadMoon();
        var name = WriteTextKernel(root, "name.tpc", "NAIF_BODY_NAME += ( 'IOA_711_BODY' )\nNAIF_BODY_CODE += ( 1999821 )\nOBJECT_1999821_FRAME = 'J2000'");
        var gm = WriteTextKernel(root, "gm.tpc", "BODY1999821_GM = ( 1.0 )");
        SpiceAPI.Instance.LoadKernels(name);
        SpiceAPI.Instance.LoadKernels(gm);
        var body = new global::IO.Astrodynamics.Body.CelestialBody(1999821);
        var elements = new KeplerianElements(7.0e6, 0.01, 0.2, 0.3, 0.4, 0.5, body, Epoch, Frame.ICRF);
        SpiceAPI.Instance.UnloadKernels(name);

        var error = ThrowsArgument(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(elements));

        Check(error.ParamName == "keplerianElements", $"the exception names keplerianElements ({error.ParamName}: {error.Message})");
        SpiceAPI.Instance.UnloadKernels(gm);
        CheckSameMoon(before, "the read after the rejected conversion");
        CheckMoonAsSpice("the read after the rejected conversion");
    }

    // The Moon from the Earth in J2000 at the epoch: on Linux, the state of spkezr_c through CspiceDirect, bit for bit; on
    // every platform, a finite state
    private static void CheckMoonAsSpice(string what)
    {
        var moon = ReadMoon();
        Check(double.IsFinite(moon.Position.Magnitude()) && moon.Position.Magnitude() > 0.0, $"{what}: the Moon is read ({moon.Position})");
        if (!OperatingSystem.IsLinux()) return;
        var spice = CspiceDirect.Spkezr("301", Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, "J2000", "NONE", "399");
        var expected = new Vector3(spice[0], spice[1], spice[2]) * 1000.0;
        Check(moon.Position == expected, $"{what}: the Moon is spkezr_c's state ({moon.Position} against {expected})");
    }

    private static FileInfo WriteTextKernel(DirectoryInfo root, string name, string data)
    {
        var file = new FileInfo(Path.Combine(root.FullName, name));
        File.WriteAllText(file.FullName, "KPL/PCK\n\\begindata\n" + data + "\n\\begintext\n");
        return file;
    }

    // The bound of the angle between a CK record written and the one read back at its epoch: twice the worst over Linux x64,
    // Windows and macOS arm64, rounded up at two significant digits (MEASURE lines of CI run 43851568556: 0.000E+000 rad on
    // Linux and macOS arm64, 2.220E-016 rad on Windows, in process and in the child; review of #706: before, 1e-9° taken
    // from other tests)
    private const double CkRecordAngleBound = 4.5e-16;

    private static StateOrientation ReadOrientationAt(int naifId, double seconds) => SpiceAPI.Instance
        .ReadOrientation(new Window(Epoch.AddSeconds(seconds), Epoch.AddSeconds(seconds)), naifId, TimeSpan.Zero, Frame.ICRF, TimeSpan.FromSeconds(1.0))
        .Single();

    // A spacecraft's SPK C, written by the managed writer and loaded, then written again by it: the managed writer unloaded C
    // natively, but the registry went on listing C, which SPICE no longer held (measured on c533a6e). C is now unloaded as
    // UnloadKernels does: no longer listed, its object not read until it is loaded again
    private static void ReplaceALoadedFileWithTheManagedWriter(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Managed"));
        var spacecraft = new Spacecraft(-1999710, "IOA_K1_SPC", 3000.0, 10000.0, new Clock("IOA_K1_CLOCK", 65536), EarthStates()[0]);
        spacecraft.AddPropagatedStates(EarthStates());
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        spacecraft.WriteEphemeris(c);
        SpiceAPI.Instance.LoadKernels(c);
        var handle = KeeperHandle(c);

        spacecraft.WriteEphemeris(c);

        Check(Listed(root).Length == 0, $"C is no longer listed ({string.Join(", ", Listed(root))})");
        CheckRegistryFollowsKeeper(root, c);
        CheckClosed(c, handle);
        Throws(() => ReadObject(-1999710));
        SpiceAPI.Instance.LoadKernels(c);
        var read = ReadObject(-1999710);
        Check(read.Position == EarthStates()[1].Position, $"the new file gives the state written ({read.Position})");
        SpiceAPI.Instance.UnloadKernels(c);
    }

    // A spacecraft's SPK C, loaded, a meta-kernel that names an SPK X relatively, X deleted, then C written again by the
    // managed writer. C's unload opens the other SPKs again (unload_c --> UNLOAD --> SPKLEF) and fails on X, left to SPICE (a
    // relative name). The writer unloads C as UnloadKernels does, which throws with SPICE's message, before anything is
    // written: C is on disk as it was, no longer listed nor held, and the Moon is read bit for bit as before (measured on
    // c533a6e: the call returned and replaced C, the failure reported only on the standard output)
    private static void ReplaceALoadedFileWithTheManagedWriterWhenAnSpkHeldRelativelyIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "ManagedRelative"));
        var spacecraft = new Spacecraft(-1999711, "IOA_K1_SPC", 3000.0, 10000.0, new Clock("IOA_K1_CLOCK", 65536), EarthStates()[0]);
        spacecraft.AddPropagatedStates(EarthStates());
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        spacecraft.WriteEphemeris(c);
        var bytes = File.ReadAllBytes(c.FullName);
        SpiceAPI.Instance.LoadKernels(c);
        var x = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "x.bsp");
        LoadRelativeMetaKernel(folder, "x.bsp");
        var before = ReadMoon();
        File.Delete(x.FullName);

        var error = Throws(() => spacecraft.WriteEphemeris(c));

        Check(error.Message.Contains("x.bsp"), $"the exception names x.bsp ({error.Message})");
        Check(File.Exists(c.FullName) && File.ReadAllBytes(c.FullName).SequenceEqual(bytes), "the SPK it would replace is on disk as it was");
        Check(!Listed(root).Contains(c.FullName), "C is no longer listed");
        Check(CspiceDirectHolds(c) != true, "KEEPER does not hold the SPK it would replace");
        CheckSameMoon(before, "the first read after the writer");
        CheckSameMoon(before, "the second read after the writer");
    }

    // The same with C held by the meta-kernel, by its absolute path: the registry lists the meta-kernel only, so the writer
    // unloads C natively, whose failure it ignored (measured on c533a6e: the call returned and replaced C). It now throws
    // with SPICE's message and keeps C as it was, as the native writers do
    private static void ReplaceAFileAMetaKernelHoldsWithTheManagedWriterWhenAnSpkHeldRelativelyIsDeleted(DirectoryInfo root, DirectoryInfo kernels)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "MetaRelative"));
        var spacecraft = new Spacecraft(-1999712, "IOA_K1_SPC", 3000.0, 10000.0, new Clock("IOA_K1_CLOCK", 65536), EarthStates()[0]);
        spacecraft.AddPropagatedStates(EarthStates());
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        spacecraft.WriteEphemeris(c);
        var bytes = File.ReadAllBytes(c.FullName);
        var x = Copy(Path.Combine(kernels.FullName, "L2_de431.bsp"), folder, "x.bsp");
        LoadMetaKernel(folder, "x.bsp", c.FullName);
        Check(CspiceDirectHolds(c) != false, "KEEPER holds C");
        var before = ReadMoon();
        File.Delete(x.FullName);

        var error = Throws(() => spacecraft.WriteEphemeris(c));

        Check(error.Message.Contains("x.bsp"), $"the exception names x.bsp ({error.Message})");
        Check(File.Exists(c.FullName) && File.ReadAllBytes(c.FullName).SequenceEqual(bytes), "the SPK it would replace is on disk as it was");
        Check(CspiceDirectHolds(c) != true, "KEEPER does not hold the SPK it would replace");
        CheckSameMoon(before, "the first read after the writer");
        CheckSameMoon(before, "the second read after the writer");
    }

    // Loads the meta-kernel mk.tm of the folder, which names a file relatively and another by its absolute path (written in
    // pieces of at most 40 characters joined by the continuation marker +, a string of a text kernel holding 80 at most),
    // with the working directory set to the folder
    private static void LoadMetaKernel(DirectoryInfo folder, string relative, string absolute)
    {
        var pieces = Enumerable.Range(0, (absolute.Length + 39) / 40).Select(i => absolute.Substring(40 * i, System.Math.Min(40, absolute.Length - 40 * i)))
            .ToArray();
        var absoluteName = string.Join("\n", pieces.Select((piece, i) => $"'{piece}{(i < pieces.Length - 1 ? "+" : "")}'"));
        var metaKernel = new FileInfo(Path.Combine(folder.FullName, "mk.tm"));
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '{relative}'\n{absoluteName} )\n\\begintext\n");
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

    // An SPK S of an object the library writes, loaded by its full path, then deleted: SPICE still holds it, open, and reads
    // its object until a native load, unload or write drops it. Two calls that make no native call, the load of a path that
    // does not exist (ArgumentException) and the load and unload of an empty directory, leave it so: before the review of the
    // fourth round of #668 the registry forgot S after each of them all the same, while KEEPER still held it. It is listed
    // until the next native call, the load of a text kernel, which drops it in SPICE (its object no longer known)
    private static void LoadWithoutANativeCallWhenAnSpkWasDeleted(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "NoNativeCall"));
        var s = new FileInfo(Path.Combine(folder.FullName, "s.bsp"));
        SpiceAPI.Instance.WriteEphemeris(s, -1999987, EarthStates());
        SpiceAPI.Instance.LoadKernels(s);
        var read = ReadObject(-1999987);
        File.Delete(s.FullName);

        bool rejected = false;
        try
        {
            SpiceAPI.Instance.LoadKernels(new FileInfo(Path.Combine(folder.FullName, "nope.bsp")));
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "the load of a path that does not exist throws ArgumentException");
        Check(Listed(root).SequenceEqual([s.FullName]), "S is listed after a load that made no native call");
        CheckRegistryFollowsKeeper(root, s);
        CheckSameObject(read, -1999987, "S's object after the rejected load");

        var empty = Directory.CreateDirectory(Path.Combine(folder.FullName, "Empty"));
        SpiceAPI.Instance.LoadKernels(empty);
        Check(Listed(root).SequenceEqual([s.FullName, empty.FullName]), "S is listed after the load of an empty directory");
        CheckRegistryFollowsKeeper(root, s);
        SpiceAPI.Instance.UnloadKernels(empty);
        Check(Listed(root).SequenceEqual([s.FullName]), "S is listed after the unload of the empty directory");
        CheckRegistryFollowsKeeper(root, s);
        CheckSameObject(read, -1999987, "S's object after the empty directory");

        var text = TestKernelWriter.WriteFrameKernel(new DirectoryInfo(Path.Combine(root.FullName, "Text")), "t.tf", 1999983, "IOA_J1_NONATIVE", 90.0);
        SpiceAPI.Instance.LoadKernels(text);
        Check(Listed(root).SequenceEqual([text.FullName]), "S is no longer listed once the native load dropped it");
        CheckRegistryFollowsKeeper(root, s, text);
        Check(Throws(() => ReadObject(-1999987)) != null, "S's object is no longer known once the native load dropped S");
        SpiceAPI.Instance.UnloadKernels(text);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    // A symbolic link L to an SPK R, and a link K to an SPK Q, both loaded by the links, then R deleted: L dangles. The native
    // layer tests the name SPICE holds with std::filesystem::exists, which follows the link: it drops L at the next native
    // call, the load of a text kernel, and keeps K. Before the review of the fourth round of #668 the registry tested L by
    // its own entry (lstat), which a dangling link still has, and listed L until it was unloaded. The references are
    // KEEPER's list (Linux) and the objects of the two SPKs: L's no longer known, K's read bit for bit as before
    private static void LoadWhenTheTargetOfALinkedSpkWasDeleted(DirectoryInfo root)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "Links"));
        var r = new FileInfo(Path.Combine(folder.FullName, "r.bsp"));
        var q = new FileInfo(Path.Combine(folder.FullName, "q.bsp"));
        SpiceAPI.Instance.WriteEphemeris(r, -1999988, EarthStates());
        SpiceAPI.Instance.WriteEphemeris(q, -1999984, EarthStates());
        var l = new FileInfo(Path.Combine(folder.FullName, "l.bsp"));
        var k = new FileInfo(Path.Combine(folder.FullName, "k.bsp"));
        File.CreateSymbolicLink(l.FullName, r.FullName);
        File.CreateSymbolicLink(k.FullName, q.FullName);
        SpiceAPI.Instance.LoadKernels(l);
        SpiceAPI.Instance.LoadKernels(k);
        var kept = ReadObject(-1999984);
        File.Delete(r.FullName);

        var text = TestKernelWriter.WriteFrameKernel(new DirectoryInfo(Path.Combine(root.FullName, "Text")), "t.tf", 1999984, "IOA_J1_LINK", 90.0);
        SpiceAPI.Instance.LoadKernels(text);

        Check(Listed(root).SequenceEqual([k.FullName, text.FullName]), "the dangling link L is no longer listed, K is");
        CheckRegistryFollowsKeeper(root, l, k, text);
        Check(Throws(() => ReadObject(-1999988)) != null, "L's object is no longer known once the native load dropped L");
        CheckSameObject(kept, -1999984, "K's object after the load");
        SpiceAPI.Instance.UnloadKernels(k);
        SpiceAPI.Instance.UnloadKernels(text);
        Check(Listed(root).Length == 0, "nothing is listed");
    }

    private static StateVector ReadObject(int naifId) =>
        SpiceAPI.Instance.ReadEphemeris(Epoch.AddSeconds(60.0), 399, naifId, Frame.ICRF, Aberration.None).ToStateVector();

    private static void CheckSameObject(StateVector before, int naifId, string what)
    {
        var after = ReadObject(naifId);
        Check(after.Position == before.Position && after.Velocity == before.Velocity,
            $"{what}: the state read before ({after.Position} against {before.Position})");
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

    // SPICE's own list as the reference of the registry (Linux only): a file listed by its own path is held by KEEPER, and a
    // file KEEPER holds is listed, by its path or by a directory that holds it
    private static void CheckRegistryFollowsKeeper(DirectoryInfo root, params FileInfo[] files)
    {
        if (!OperatingSystem.IsLinux()) return;
        var listed = Listed(root);
        foreach (var file in files)
        {
            bool held = CspiceDirect.LoadedKernelType(file.FullName) != null;
            Check(!listed.Contains(file.FullName) || held, $"{file.Name}, listed by its path, is held by KEEPER");
            Check(!held || listed.Any(p => p == file.FullName || file.FullName.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.Ordinal)),
                $"{file.Name}, held by KEEPER, is listed");
        }
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

        // The native unload dropped the deleted PCK: nothing is listed (fourth round of #668; before, it was until unloaded)
        Check(Listed(root).Length == 0, "nothing is listed");
        CheckRegistryFollowsKeeper(root, gone, pck, text);
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

    private static ArgumentException ThrowsArgument(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException e)
        {
            return e;
        }

        throw new InvalidOperationException("expected an ArgumentException, none was thrown");
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed: {what}");
        }
    }
}
