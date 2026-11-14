using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// CSPICE routines the library has no proxy for, called in the test process from the native library, which links CSPICE
/// N0067 statically: the DAF reads of <c>dafus</c> (the layout of an SPK, through the f2c routines <c>dafbfs_</c>,
/// <c>daffna_</c>, <c>dafgs_</c>, <c>dafus_</c>, <c>dafgda_</c>) and the SPK writer <c>spkw09_c</c>, the reference of the
/// managed writer (#540), with its append (<c>dafopw_c</c>); <c>unitim_c</c>, the conversion between TDB and TT, the
/// reference of the TT windows of a propagation about the Earth (#547); the list of the files SPICE holds, from KEEPER
/// (the f2c routines <c>ktotal_</c> and <c>kdata_</c>; <c>ktotal_c</c> is not exported), the reference of the kernel
/// registry (#673). Every call holds the lock of the library's CSPICE calls (<see cref="SpiceAPI.SyncRoot"/>).
/// </summary>
/// <remarks>
/// The Linux build of the native library exports the CSPICE symbols; the Windows DLL exports only the proxies, so the tests
/// that use this class are in the Validation category, which the CI runs on Linux. The f2c integers of CSPICE on 64-bit
/// Linux are 32-bit (<c>SpiceZfc.h</c>).
/// </remarks>
internal static class CspiceDirect
{
    private const string Library = "IO.Astrodynamics";

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void dafopr_c(string fname, out int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void dafcls_c(int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int dafbfs_(ref int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int daffna_(out int found);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int dafgs_([Out] double[] sum);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int dafus_([In] double[] sum, ref int nd, ref int ni, [Out] double[] dc, [Out] int[] ic);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int dafgda_(ref int handle, ref int baddr, ref int eaddr, [Out] double[] data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void spkopn_c(string fname, string ifname, int ncomch, out int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void spkw09_c(int handle, int body, int center, string frame, double first, double last, string segid,
        int degree, int n, [In] double[] states, [In] double[] epochs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void dafopw_c(string fname, out int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void spkcls_c(int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int failed_c();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void getmsg_c(string option, int length, [Out] StringBuilder message);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void reset_c();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ktotal_([In] byte[] kind, ref int count, int kindLength);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int kdata_(ref int which, [In] byte[] kind, [Out] byte[] file, [Out] byte[] fileType, [Out] byte[] source,
        ref int handle, ref int found, int kindLength, int fileLength, int fileTypeLength, int sourceLength);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern double unitim_c(double epoch, string insys, string outsys);

    /// <summary>The TT seconds past J2000 of a TDB epoch given in seconds past J2000 (<c>unitim_c(et, "TDB", "TT")</c>,
    /// the leapseconds kernel loaded).</summary>
    internal static double TdbToTt(double tdbSecondsPastJ2000)
    {
        lock (SpiceAPI.SyncRoot)
        {
            double tt = unitim_c(tdbSecondsPastJ2000, "TDB", "TT");
            Check("unitim");
            return tt;
        }
    }

    /// <summary>The TDB seconds past J2000 of a TT epoch given in seconds past J2000 (<c>unitim_c(tt, "TT", "TDB")</c>).</summary>
    internal static double TtToTdb(double ttSecondsPastJ2000)
    {
        lock (SpiceAPI.SyncRoot)
        {
            double tdb = unitim_c(ttSecondsPastJ2000, "TT", "TDB");
            Check("unitim");
            return tdb;
        }
    }

    /// <summary>A segment as <c>dafus</c> unpacks its summary, and its data words as <c>dafgda</c> reads them.</summary>
    internal sealed record Segment(double Start, double End, int Body, int Center, int Frame, int Type, int Begin, int End2,
        double[] Data);

    /// <summary>The segments of an SPK in file order (<c>dafbfs</c>, <c>daffna</c>, <c>dafgs</c>, <c>dafus</c>,
    /// <c>dafgda</c>).</summary>
    internal static List<Segment> ReadSegments(string path)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var segments = new List<Segment>();
            dafopr_c(path, out int handle);
            Check("dafopr");
            try
            {
                dafbfs_(ref handle);
                Check("dafbfs");
                while (true)
                {
                    daffna_(out int found);
                    Check("daffna");
                    if (found == 0) break;
                    var sum = new double[5];
                    dafgs_(sum);
                    int nd = 2, ni = 6;
                    var dc = new double[2];
                    var ic = new int[6];
                    dafus_(sum, ref nd, ref ni, dc, ic);
                    int begin = ic[4], end = ic[5];
                    var data = new double[end - begin + 1];
                    dafgda_(ref handle, ref begin, ref end, data);
                    Check("dafgda");
                    segments.Add(new Segment(dc[0], dc[1], ic[0], ic[1], ic[2], ic[3], ic[4], ic[5], data));
                }
            }
            finally
            {
                dafcls_c(handle);
            }

            Check("dafcls");
            return segments;
        }
    }

    /// <summary>Writes the given segments of type 9 with CSPICE's own writer (<c>spkopn</c>, <c>spkw09</c>, <c>spkcls</c>),
    /// in J2000, no comment area.</summary>
    internal static void WriteType9(string path, int body, IReadOnlyList<(int Center, int Degree, double[] Epochs, double[] States, string Name)> segments)
    {
        lock (SpiceAPI.SyncRoot)
        {
            spkopn_c(path, "IO.Astrodynamics", 0, out int handle);
            Check("spkopn");
            foreach (var s in segments)
            {
                spkw09_c(handle, body, s.Center, "J2000", s.Epochs[0], s.Epochs[^1], s.Name, s.Degree, s.Epochs.Length, s.States, s.Epochs);
                Check("spkw09");
            }

            spkcls_c(handle);
            Check("spkcls");
        }
    }

    /// <summary>Appends a segment of type 9 to an existing SPK with CSPICE's own writer, in J2000: <c>dafopw</c> (what
    /// <c>spkopa</c> calls, which the library does not export), <c>spkw09</c>, <c>spkcls</c>; CSPICE's append (<c>dafbna</c>,
    /// <c>dafena</c>) needs the DAF's summary records to be as the DAF Required Reading lays them out.</summary>
    internal static void AppendType9(string path, int body, (int Center, int Degree, double[] Epochs, double[] States, string Name) segment)
    {
        lock (SpiceAPI.SyncRoot)
        {
            dafopw_c(path, out int handle);
            Check("dafopw");
            spkw09_c(handle, body, segment.Center, "J2000", segment.Epochs[0], segment.Epochs[^1], segment.Name, segment.Degree, segment.Epochs.Length, segment.States,
                segment.Epochs);
            Check("spkw09");
            spkcls_c(handle);
            Check("spkcls");
        }
    }

    // A CSPICE error, as the proxies' error management leaves it (RETURN mode): its long message, then a reset
    /// <summary>The names of the files SPICE holds, in their load order, as KEEPER records them (<c>ktotal_("ALL")</c>,
    /// then <c>kdata_</c> for each; a meta-kernel and the files it loaded are listed each).</summary>
    internal static List<string> LoadedKernelFiles()
    {
        lock (SpiceAPI.SyncRoot)
        {
            var all = Encoding.ASCII.GetBytes("ALL");
            int count = 0;
            ktotal_(all, ref count, all.Length);
            Check("ktotal");
            var files = new List<string>(count);
            for (int which = 1; which <= count; which++)
            {
                var file = new byte[1024];
                var fileType = new byte[32];
                var source = new byte[1024];
                int handle = 0, found = 0, index = which;
                kdata_(ref index, all, file, fileType, source, ref handle, ref found, all.Length, file.Length, fileType.Length,
                    source.Length);
                Check("kdata");
                if (found != 0)
                {
                    files.Add(Encoding.UTF8.GetString(file).TrimEnd(' ', '\0'));
                }
            }

            return files;
        }
    }

    private static void Check(string routine)
    {
        if (failed_c() == 0) return;
        var message = new StringBuilder(1841);
        getmsg_c("LONG", message.Capacity, message);
        reset_c();
        throw new InvalidOperationException($"{routine}: {message}");
    }
}
