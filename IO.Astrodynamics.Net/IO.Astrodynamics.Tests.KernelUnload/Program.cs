// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.KernelUnload;

/// <summary>
/// The child process of <c>KernelRegistryTests</c> (#673): one scenario of <see cref="SpiceAPI.UnloadKernels"/> in which
/// SPICE fails to open a deleted binary PCK again, run in a process of its own.
/// </summary>
/// <remarks>
/// <para>
/// When SPICE opens the loaded binary kernels of a kind again after an unload (<c>unload_c --&gt; UNLOAD --&gt; PCKLOF</c>,
/// <c>SPKLEF</c>, <c>CKLPF</c>) and fails on one deleted since its load, KEEPER records the handle 0 for it: its later
/// unload, and <c>kclear_c</c>, close nothing, and the deleted file stays open in the DAF and PCK tables of the process
/// (measured with CSPICE N0067). Once more than the 23 logical units of CSPICE's handle manager are in use, its unit is
/// released, and every later search of the PCKs (<c>TISBOD --&gt; PCKSFS --&gt; DAFBBS --&gt; ZZDDHHLU</c>, each IAU
/// frame) fails to reconnect it: in the shared test process the IAU frames of other test classes failed intermittently
/// (CI of 725ee1a and a993265 on Linux and macOS arm64). Run here, the scenario leaves nothing behind.
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
    internal const string PckOfAFolder = "UnloadKernels_OfAPck_WhenSpiceCannotOpenAnotherPckAgain_Throws_AndThePcksLoadedBeforeTakePrecedence";
    internal const string MetaKernelOfAPck = "UnloadKernels_OfAMetaKernelOfAPckThenATextKernel_WhenSpiceCannotOpenAnotherPckAgain_Throws";

    private static readonly Time Epoch = new(2024, 1, 1);

    private static int Main(string[] args)
    {
        if (args.Length != 2 || !Directory.Exists(args[0]) || args[1] is not (FolderOfTwoPcks or PckOfAFolder or MetaKernelOfAPck))
        {
            Console.Error.WriteLine($"Usage: <kernel directory> <{FolderOfTwoPcks} | {PckOfAFolder} | {MetaKernelOfAPck}>");
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
                    UnloadPckOfAFolder(root, moonPck);
                    break;
                default:
                    UnloadMetaKernelOfAPck(root, moonPck);
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

    // P, the folder [X, D] and Q, D deleted after the load: the unload of X opens P again, then fails on D, before Q, and P
    // takes precedence over Q, loaded after it, until a PCK unload succeeds (measured with CSPICE N0067)
    private static void UnloadPckOfAFolder(DirectoryInfo root, string moonPck)
    {
        var (folder, x, d, withP, withQ) = LoadPcksAroundAFolder(root, moonPck, 1999958, "IOA_F02_PCK_B");
        File.Delete(d.FullName);

        var exception = Throws(() => SpiceAPI.Instance.UnloadKernels(x));
        Check(exception.Message.Contains(x.FullName), $"the failure names X: {exception.Message}");
        CheckSameRotation(withP, "IOA_F02_PCK_B", "P takes precedence over Q after the failure");

        SpiceAPI.Instance.UnloadKernels(d);
        Check(!Listed(root).Contains(folder.FullName), "the folder is no longer listed");
        CheckSameRotation(withQ, "IOA_F02_PCK_B", "Q takes precedence over P once D is unloaded");
    }

    // A binary PCK D, deleted after its load, and a folder of a meta-kernel that names another PCK, listed before a text
    // kernel. The meta-kernel's unload unloads that PCK, which opens D again and fails; the text kernel's unload reads the
    // text kernels again, not the PCKs, and repairs nothing (measured with CSPICE N0067)
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
        File.Delete(gone.FullName);

        // Before, the text kernel's success was taken for a repair of the meta-kernel's failure
        var exception = Throws(() => SpiceAPI.Instance.UnloadKernels(folder));
        Check(exception.Message.Contains(metaKernel.FullName), $"the failure names the meta-kernel: {exception.Message}");
        Check(Listed(root).SequenceEqual([gone.FullName]), "only the deleted PCK is listed");

        // The unload of the deleted PCK opens the others again without it
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
