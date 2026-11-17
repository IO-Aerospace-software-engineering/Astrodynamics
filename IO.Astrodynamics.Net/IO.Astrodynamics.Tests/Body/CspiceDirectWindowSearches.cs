// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Runtime.InteropServices;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// CSPICE's window searches called in the test process with a result cell of <see cref="WindowCapacity"/> windows, twice
/// the native layer's: <c>gfposc_c</c>, <c>gfdist_c</c>, <c>gfilum_c</c>, <c>gfoclt_c</c> and <c>gftfov_c</c>, the references of the
/// searches past 1000 windows (#653). The cells are the <c>SpiceCell</c> of <c>SpiceCel.h</c> over pinned arrays, as the
/// <c>SPICEDOUBLE_CELL</c> macro builds them (an empty, set window); <c>SpiceInt</c> and <c>SpiceBoolean</c> are 32-bit on
/// 64-bit Linux (<c>SpiceZdf.h</c>).
/// </summary>
internal static partial class CspiceDirect
{
    /// <summary>The windows a result cell of the references holds: twice the 10000 of the native layer's cells.</summary>
    internal const int WindowCapacity = 20000;

    private const int CellControlSize = 6;
    private const int SpiceDoublePrecision = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpiceCell
    {
        public int DataType;
        public int Length;
        public int Size;
        public int Card;
        public int IsSet;
        public int Adjust;
        public int Init;
        public IntPtr Base;
        public IntPtr Data;
    }

    private delegate void Search(ref SpiceCell confinement, ref SpiceCell result);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void wninsd_c(double left, double right, ref SpiceCell window);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wncard_c(ref SpiceCell window);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void wnfetd_c(ref SpiceCell window, int n, out double left, out double right);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void gfposc_c(string target, string frame, string abcorr, string obsrvr, string crdsys, string coord,
        string relate, double refval, double adjust, double step, int nintvls, ref SpiceCell cnfine, ref SpiceCell result);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void gfdist_c(string target, string abcorr, string obsrvr, string relate, double refval, double adjust,
        double step, int nintvls, ref SpiceCell cnfine, ref SpiceCell result);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void gfilum_c(string method, string angtyp, string target, string illmn, string fixref, string abcorr,
        string obsrvr, [In] double[] spoint, string relate, double refval, double adjust, double step, int nintvls,
        ref SpiceCell cnfine, ref SpiceCell result);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void gfoclt_c(string occtyp, string front, string fshape, string fframe, string back, string bshape,
        string bframe, string abcorr, string obsrvr, double step, ref SpiceCell cnfine, ref SpiceCell result);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void gftfov_c(string inst, string target, string tshape, string tframe, string abcorr, string obsrvr,
        double step, ref SpiceCell cnfine, ref SpiceCell result);

    /// <summary><c>gfposc_c</c> over [<paramref name="start"/>, <paramref name="end"/>] (TDB seconds past J2000): its windows,
    /// [start, end] in TDB seconds.</summary>
    internal static double[][] Gfposc(double start, double end, string target, string frame, string abcorr, string observer,
        string coordinateSystem, string coordinate, string relate, double value, double adjust, double step) =>
        Windows("gfposc", start, end, (ref SpiceCell cnfine, ref SpiceCell result) => gfposc_c(target, frame, abcorr, observer,
            coordinateSystem, coordinate, relate, value, adjust, step, WindowCapacity, ref cnfine, ref result));

    /// <summary><c>gfdist_c</c>, the value in km.</summary>
    internal static double[][] Gfdist(double start, double end, string target, string abcorr, string observer, string relate,
        double value, double adjust, double step) =>
        Windows("gfdist", start, end, (ref SpiceCell cnfine, ref SpiceCell result) => gfdist_c(target, abcorr, observer, relate,
            value, adjust, step, WindowCapacity, ref cnfine, ref result));

    /// <summary><c>gfilum_c</c> at <paramref name="point"/>, body-fixed, km.</summary>
    internal static double[][] Gfilum(double start, double end, string angle, string target, string source, string fixedFrame,
        string abcorr, string observer, double[] point, string relate, double value, double adjust, double step) =>
        Windows("gfilum", start, end, (ref SpiceCell cnfine, ref SpiceCell result) => gfilum_c("ELLIPSOID", angle, target, source,
            fixedFrame, abcorr, observer, point, relate, value, adjust, step, WindowCapacity, ref cnfine, ref result));

    /// <summary><c>gfoclt_c</c>.</summary>
    internal static double[][] Gfoclt(double start, double end, string type, string front, string frontShape, string frontFrame,
        string back, string backShape, string backFrame, string abcorr, string observer, double step) =>
        Windows("gfoclt", start, end, (ref SpiceCell cnfine, ref SpiceCell result) => gfoclt_c(type, front, frontShape, frontFrame,
            back, backShape, backFrame, abcorr, observer, step, ref cnfine, ref result));

    /// <summary><c>gftfov_c</c>.</summary>
    internal static double[][] Gftfov(double start, double end, string instrument, string target, string targetShape, string targetFrame,
        string abcorr, string observer, double step) =>
        Windows("gftfov", start, end, (ref SpiceCell cnfine, ref SpiceCell result) => gftfov_c(instrument, target, targetShape, targetFrame,
            abcorr, observer, step, ref cnfine, ref result));

    private static double[][] Windows(string routine, double start, double end, Search search)
    {
        var confinementData = new double[CellControlSize + 2];
        var resultData = new double[CellControlSize + 2 * WindowCapacity];
        var confinementHandle = GCHandle.Alloc(confinementData, GCHandleType.Pinned);
        var resultHandle = GCHandle.Alloc(resultData, GCHandleType.Pinned);
        try
        {
            var confinement = Cell(confinementHandle, 2);
            var result = Cell(resultHandle, 2 * WindowCapacity);
            lock (SpiceAPI.SyncRoot)
            {
                wninsd_c(start, end, ref confinement);
                Check("wninsd");
                search(ref confinement, ref result);
                Check(routine);
                int count = wncard_c(ref result);
                var windows = new double[count][];
                for (int i = 0; i < count; i++)
                {
                    wnfetd_c(ref result, i, out double left, out double right);
                    windows[i] = [left, right];
                }

                Check("wnfetd");
                return windows;
            }
        }
        finally
        {
            confinementHandle.Free();
            resultHandle.Free();
        }
    }

    // The SPICEDOUBLE_CELL macro's cell over a pinned array of CellControlSize + size doubles
    private static SpiceCell Cell(GCHandle handle, int size)
    {
        IntPtr baseAddress = handle.AddrOfPinnedObject();
        return new SpiceCell
        {
            DataType = SpiceDoublePrecision, Length = 0, Size = size, Card = 0, IsSet = 1, Adjust = 0, Init = 0, Base = baseAddress,
            Data = baseAddress + CellControlSize * sizeof(double)
        };
    }
}
