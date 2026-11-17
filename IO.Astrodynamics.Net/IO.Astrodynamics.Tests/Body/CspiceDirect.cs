using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// CSPICE routines the library has no proxy for, called in the test process from the native library, which links CSPICE
/// N0067 statically: the DAF reads of <c>dafus</c> (the layout of an SPK, through the f2c routines <c>dafbfs_</c>,
/// <c>daffna_</c>, <c>dafgs_</c>, <c>dafus_</c>, <c>dafgda_</c>) and the SPK writer <c>spkw09_c</c>, the reference of the
/// managed writer (#540), with its append (<c>dafopw_c</c>); <c>unitim_c</c>, the conversion between TDB and TT, the
/// reference of the TT windows of a propagation about the Earth (#547); <c>recpgr_c</c>, <c>georec_c</c>, <c>spkezr_c</c>
/// and <c>pxform_c</c>, the references of the planetographic coordinates and of a site's frame (#682); <c>getfov_c</c>, the
/// reference of the fields of view an instrument may hold (#707), and <c>reclat_c</c>, that of a star's crossing of a pole
/// (#709); <c>prop2b_c</c>, <c>oscltx_c</c>, <c>sxform_c</c> and <c>conics_c</c>, the references of the errors of the two-body
/// proxies, of a frame transformation and of the elements a conversion checks, and the CK writer <c>ckw03_c</c> with
/// <c>sce2c_c</c> and <c>ckgp_c</c>, the reference of a CK without angular velocity (#714); the list of the
/// files SPICE holds, from KEEPER (the f2c routines <c>ktotal_</c> and <c>kdata_</c>; <c>ktotal_c</c> is not exported), the
/// reference of the kernel registry and of the type of a text kernel (#673), with their handles and the files of open DAF
/// handles (<c>dafhfn_</c>), the reference of the unload of deleted binary kernels (#668, second round). Every call holds the lock of the library's CSPICE calls (<see cref="SpiceAPI.SyncRoot"/>).
/// </summary>
/// <remarks>
/// The Linux build of the native library exports the CSPICE symbols; the Windows DLL exports only the proxies, so the tests
/// that use this class are in the Validation category, which the CI runs on Linux. The f2c integers of CSPICE on 64-bit
/// Linux are 32-bit (<c>SpiceZfc.h</c>).
/// </remarks>
internal static partial class CspiceDirect
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

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int dafhfn_(ref int handle, [Out] byte[] fname, int fnameLength);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern double unitim_c(double epoch, string insys, string outsys);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void recpgr_c(string body, [In] double[] rectan, double re, double f, out double lon, out double lat, out double alt);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void georec_c(double lon, double lat, double alt, double re, double f, [Out] double[] rectan);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void spkezr_c(string targ, double et, string re, string abcorr, string obs, [Out] double[] starg, out double lt);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void pxform_c(string from, string to, double et, [Out] double[] rotate);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void q2m_c([In] double[] q, [Out] double[] r);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int axisar_([In] double[] axis, ref double angle, [Out] double[] r);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int vrotv_([In] double[] v, [In] double[] axis, ref double theta, [Out] double[] r);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void oscltx_c([In] double[] state, double et, double mu, [Out] double[] elts);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void sxform_c(string from, string to, double et, [Out] double[] xform);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void ckopn_c(string fname, string ifname, int ncomch, out int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void ckw03_c(int handle, double begtim, double endtim, int inst, string reff, int avflag, string segid, int nrec,
        [In] double[] sclkdp, [In] double[] quats, [In] double[] avvs, int nints, [In] double[] starts);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ckcls_c(int handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sce2c_c(int sc, double et, out double sclkdp);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void ckgp_c(int inst, double sclkdp, double tol, string reff, [Out] double[] cmat, out double clkout, out int found);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void conics_c([In] double[] elts, double et, [Out] double[] state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void prop2b_c(double gm, [In] double[] pvinit, double dt, [Out] double[] pvprop);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void latrec_c(double radius, double longitude, double latitude, [Out] double[] rectan);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void reclat_c([In] double[] rectan, out double radius, out double longitude, out double latitude);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void getfov_c(int instid, int room, int shapelen, int framelen, [Out] byte[] shape, [Out] byte[] frame,
        [Out] double[] bsight, out int n, [Out] double[] bounds);

    /// <summary>
    /// The rectangular coordinates of a range, right ascension and declination: <c>latrec_c</c> of the range, the right
    /// ascension as longitude and the declination as latitude, what CSPICE's <c>radrec_c</c> computes (radrec_c is not
    /// exported by the native library).
    /// </summary>
    internal static double[] Radrec(double range, double rightAscension, double declination)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var rectan = new double[3];
            latrec_c(range, rightAscension, declination, rectan);
            Check("latrec");
            return rectan;
        }
    }

    /// <summary>The range, right ascension and declination of rectangular coordinates (<c>reclat_c</c>: the longitude in
    /// (-π, π], the latitude in [-π/2, π/2]).</summary>
    internal static (double Range, double Longitude, double Latitude) Reclat(double[] rectan)
    {
        lock (SpiceAPI.SyncRoot)
        {
            reclat_c(rectan, out double radius, out double longitude, out double latitude);
            Check("reclat");
            return (radius, longitude, latitude);
        }
    }

    /// <summary>
    /// The field of view of an instrument as <c>getfov_c</c> reads it from the loaded instrument kernels: its shape, its
    /// boresight and its number of boundary vectors, or the short error message SPICE signals (<c>SPICE(ZEROBORESIGHT)</c>,
    /// <c>SPICE(BADREFVECTORSPEC)</c>...) with nulls; SPICE is reset after the error.
    /// </summary>
    internal static (string Error, string Shape, double[] Boresight, int Count) Getfov(int instrumentId)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var shape = new byte[32];
            var frame = new byte[64];
            var boresight = new double[3];
            var bounds = new double[3 * 10];
            getfov_c(instrumentId, 10, shape.Length, frame.Length, shape, frame, boresight, out int n, bounds);
            if (failed_c() != 0)
            {
                var message = new StringBuilder(64);
                getmsg_c("SHORT", message.Capacity, message);
                reset_c();
                return (message.ToString().Trim(), null, null, 0);
            }

            return (null, Encoding.ASCII.GetString(shape).TrimEnd(' ', '\0'), boresight, n);
        }
    }

    /// <summary>The planetographic coordinates of a body-fixed point (<c>recpgr_c</c>): the longitude in [0, 2π] in the
    /// sense of the body (the kernel pool's), the latitude and the altitude on the spheroid of <paramref name="re"/> and
    /// <paramref name="f"/>, in the units of the point.</summary>
    internal static (double Longitude, double Latitude, double Altitude) Recpgr(string body, double[] rectan, double re, double f)
    {
        lock (SpiceAPI.SyncRoot)
        {
            recpgr_c(body, rectan, re, f, out double lon, out double lat, out double alt);
            Check("recpgr");
            return (lon, lat, alt);
        }
    }

    /// <summary>The body-fixed position of a geodetic point (<c>georec_c</c>), in the units of <paramref name="re"/>.</summary>
    internal static double[] Georec(double lon, double lat, double alt, double re, double f)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var rectan = new double[3];
            georec_c(lon, lat, alt, re, f, rectan);
            Check("georec");
            return rectan;
        }
    }

    /// <summary>The state of a target from an observer (<c>spkezr_c</c>), km and km/s.</summary>
    internal static double[] Spkezr(string target, double et, string frame, string abcorr, string observer)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var state = new double[6];
            spkezr_c(target, et, frame, abcorr, observer, state, out _);
            Check("spkezr");
            return state;
        }
    }

    /// <summary>The rotation from one frame to another at an epoch (<c>pxform_c</c>), row-major.</summary>
    internal static double[] Pxform(string from, string to, double et)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var rotation = new double[9];
            pxform_c(from, to, et, rotation);
            Check("pxform");
            return rotation;
        }
    }

    /// <summary>The rotation matrix of a SPICE quaternion (<c>q2m_c</c>, scalar first), row-major.</summary>
    internal static double[] Q2m(double w, double x, double y, double z)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var r = new double[9];
            q2m_c([w, x, y, z], r);
            Check("q2m");
            return r;
        }
    }

    /// <summary>The matrix that rotates vectors by <paramref name="angle"/> radians about <paramref name="axis"/> (the f2c
    /// routine <c>axisar_</c>, whose column-major matrix is returned row-major; the axis need not be a unit vector).</summary>
    internal static double[] Axisar(double[] axis, double angle)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var columnMajor = new double[9];
            double theta = angle;
            axisar_(axis, ref theta, columnMajor);
            Check("axisar");
            var r = new double[9];
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[3 * i + j] = columnMajor[i + 3 * j];
            return r;
        }
    }

    /// <summary>The vector <paramref name="v"/> rotated by <paramref name="angle"/> radians about <paramref name="axis"/>
    /// (the f2c routine <c>vrotv_</c>; the axis need not be a unit vector).</summary>
    internal static double[] Vrotv(double[] v, double[] axis, double angle)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var r = new double[3];
            double theta = angle;
            vrotv_(v, axis, ref theta, r);
            Check("vrotv");
            return r;
        }
    }

    /// <summary>The short error message SPICE signals for the osculating elements of a state (<c>oscltx_c</c>), or null
    /// when it gives them; SPICE is reset after the error.</summary>
    internal static string OscltxError(double[] state, double et, double mu)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var elts = new double[20];
            oscltx_c(state, et, mu, elts);
            if (failed_c() == 0) return null;
            var message = new StringBuilder(64);
            getmsg_c("SHORT", message.Capacity, message);
            reset_c();
            return message.ToString().Trim();
        }
    }

    /// <summary>The state <c>prop2b_c</c> gives (m and m/s with a GM in m^3/s^2), or null with the short and long messages of
    /// the error it signals, SPICE reset (#714).</summary>
    internal static (double[] State, string Short, string Long) Prop2b(double gm, double[] state, double dt)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var result = new double[6];
            prop2b_c(gm, state, dt, result);
            var (shortMessage, longMessage) = TakeError();
            return (shortMessage == null ? result : null, shortMessage, longMessage);
        }
    }

    /// <summary>The elements <c>oscltx_c</c> gives (its 20 elements), or null with the short and long messages of the error it
    /// signals, SPICE reset (#714).</summary>
    internal static (double[] Elements, string Short, string Long) Oscltx(double[] state, double et, double mu)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var elements = new double[20];
            oscltx_c(state, et, mu, elements);
            var (shortMessage, longMessage) = TakeError();
            return (shortMessage == null ? elements : null, shortMessage, longMessage);
        }
    }

    /// <summary>The long message of the error <c>sxform_c</c> signals for a transformation between two frames, or null when it
    /// gives one; SPICE is reset (#714).</summary>
    internal static string SxformError(string from, string to, double et)
    {
        lock (SpiceAPI.SyncRoot)
        {
            sxform_c(from, to, et, new double[36]);
            return TakeError().Long;
        }
    }

    /// <summary>The state <c>conics_c</c> gives for its eight elements (rp, e, i, node, argp, M0, t0, GM) at an epoch, with
    /// the short message of the error it signals, null when none; SPICE is reset (#714).</summary>
    internal static (double[] State, string Error) Conics(double[] elements, double et)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var state = new double[6];
            conics_c(elements, et, state);
            return (state, TakeError().Short);
        }
    }

    /// <summary>The continuous encoded clock of a spacecraft at an ephemeris time (<c>sce2c_c</c>), its clock kernel loaded.</summary>
    internal static double Sce2c(int spacecraftId, double et)
    {
        lock (SpiceAPI.SyncRoot)
        {
            sce2c_c(spacecraftId, et, out double sclk);
            Check("sce2c");
            return sclk;
        }
    }

    /// <summary>Writes one type-3 CK segment of one interval with CSPICE's own writer (<c>ckopn</c>, <c>ckw03</c>,
    /// <c>ckcls</c>): the SPICE quaternions (scalar first, 4 per record) and, when <paramref name="withRates"/>, the angular
    /// velocities (3 per record); without them the segment carries pointing only (#714).</summary>
    internal static void WriteType3Ck(string path, int instrument, string frame, bool withRates, double[] sclk, double[] quaternions,
        double[] angularVelocities)
    {
        lock (SpiceAPI.SyncRoot)
        {
            ckopn_c(path, "IO.Astrodynamics", 0, out int handle);
            Check("ckopn");
            ckw03_c(handle, sclk[0], sclk[^1], instrument, frame, withRates ? 1 : 0, "IOA", sclk.Length, sclk, quaternions, angularVelocities, 1,
                [sclk[0]]);
            Check("ckw03");
            ckcls_c(handle);
            Check("ckcls");
        }
    }

    /// <summary>The C-matrix (row-major) a CK gives at an encoded clock (<c>ckgp_c</c>), or null when it finds none.</summary>
    internal static double[] Ckgp(int instrument, double sclk, double tolerance, string frame)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var cmat = new double[9];
            ckgp_c(instrument, sclk, tolerance, frame, cmat, out _, out int found);
            Check("ckgp");
            return found != 0 ? cmat : null;
        }
    }

    // The short and long messages of a pending SPICE error, SPICE reset, or nulls when none is pending
    private static (string Short, string Long) TakeError()
    {
        if (failed_c() == 0) return (null, null);
        var shortMessage = new StringBuilder(64);
        getmsg_c("SHORT", shortMessage.Capacity, shortMessage);
        var longMessage = new StringBuilder(1841);
        getmsg_c("LONG", longMessage.Capacity, longMessage);
        reset_c();
        return (shortMessage.ToString().Trim(), longMessage.ToString().Trim());
    }

    /// <summary>Whether SPICE has an error pending (<c>failed_c</c>), which a failed proxy must not leave for the next call
    /// (#712); nothing is reset.</summary>
    internal static bool ErrorPending()
    {
        lock (SpiceAPI.SyncRoot)
        {
            return failed_c() != 0;
        }
    }

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
    internal static List<string> LoadedKernelFiles() => LoadedKernels().Select(x => x.File).ToList();

    /// <summary>The type KEEPER gives a file SPICE holds (<c>kdata_</c>: SPK, CK, PCK, DSK, EK, TEXT or META), null for a file
    /// it does not hold.</summary>
    internal static string LoadedKernelType(string file) => LoadedKernels().Where(x => x.File == file).Select(x => x.Type).FirstOrDefault();

    /// <summary>The handle KEEPER holds for a file SPICE holds (<c>kdata_</c>; 0 for a text kernel, or for a binary kernel SPICE
    /// failed to open again), null for a file it does not hold.</summary>
    internal static int? LoadedKernelHandle(string file) =>
        LoadedKernels().Where(x => x.File == file).Select(x => (int?)x.Handle).FirstOrDefault();

    /// <summary>The file of an open DAF handle (<c>dafhfn_</c>), null when no open DAF has that handle: DAF handles are not
    /// reused, so a handle once closed stays unknown.</summary>
    internal static string OpenDafFile(int handle)
    {
        lock (SpiceAPI.SyncRoot)
        {
            var file = new byte[1024];
            dafhfn_(ref handle, file, file.Length);
            if (failed_c() != 0)
            {
                reset_c();
                return null;
            }

            return Encoding.UTF8.GetString(file).TrimEnd(' ', '\0');
        }
    }

    // The files SPICE holds, their types and their handles, in their load order
    private static List<(string File, string Type, int Handle)> LoadedKernels()
    {
        lock (SpiceAPI.SyncRoot)
        {
            var all = Encoding.ASCII.GetBytes("ALL");
            int count = 0;
            ktotal_(all, ref count, all.Length);
            Check("ktotal");
            var files = new List<(string File, string Type, int Handle)>(count);
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
                    files.Add((Encoding.UTF8.GetString(file).TrimEnd(' ', '\0'), Encoding.ASCII.GetString(fileType).TrimEnd(' ', '\0'), handle));
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
