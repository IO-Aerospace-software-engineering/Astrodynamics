// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The mode <c>frame-memory</c> (#541): the memory a frame retains after its reads, alone in a fresh process, so that no
/// other test allocates while <see cref="GC.GetTotalMemory(bool)"/> is read. A frame read at N distinct epochs kept N
/// orientations for its life before 11.0.0 (16.5 MB after 100 000 reads of ITRF93, the issue's probe), and the static
/// frames live for the whole process. Now a frame keeps at most the orientation of its last epoch.
/// </summary>
/// <remarks>
/// After a warm-up (every path read once, at its own epoch, so that JIT and first-use allocations are done), the frames
/// are read at <see cref="ItrfReads"/> epochs 0.5 s apart for ITRF93 (by name, a SPICE frame) and <see cref="ManagedReads"/>
/// for each managed frame (CIRS, TIRS, TEME, GCRF, the frame of a site from coordinates), and the memory retained after a
/// full collection, the frames still referenced, is compared with the memory before. Prints one MEASURE line per frame and
/// exits 1 when one retains more than <see cref="RetainedBound"/>.
/// </remarks>
internal static class FrameMemoryChild
{
    private const int ItrfReads = 100_000;
    private const int ManagedReads = 5_000;

    // Twice the worst magnitude over Linux x64, Windows x64 and macOS arm64, the noise of a full collection, of either sign
    // (CI run 44225490669): 656 bytes on Linux x64 (−656, ITRF93; 0 for the others), 24 on Windows x64 (CIRS, TIRS), 9 000
    // on macOS arm64 (−9 000, CIRS); 8 248 bytes (CIRS) on Linux x64 locally, 2026-11-17. Before the fix, ITRF93 retained
    // 16.5 MB (#541) and every frame grew with its reads
    private const long RetainedBound = 18_000;

    internal static int Run(string kernels)
    {
        SpiceApi.Instance.LoadKernels(new DirectoryInfo(kernels));
        var start = new Time(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var site = new Site(13, "FRAMEMEMORY", earth, new Planetodetic(-116.79 * Constants.Deg2Rad, 35.25 * Constants.Deg2Rad, 1070.0));

        var frames = new (string Label, Frame Frame, int Reads)[]
        {
            ("ITRF93 by name", new Frame("ITRF93"), ItrfReads),
            ("CIRS", Frame.CIRS, ManagedReads),
            ("TIRS", Frame.TIRS, ManagedReads),
            ("TEME", Frame.TEME, ManagedReads),
            ("GCRF", Frame.GCRF, ManagedReads),
            ("site from coordinates", site.Frame, ManagedReads)
        };

        // Warm-up: each path once, at an epoch the measured reads do not use
        foreach (var (_, frame, _) in frames)
        {
            frame.GetStateOrientationToICRF(start.AddSeconds(-1.0));
        }

        int exitCode = 0;
        foreach (var (label, frame, reads) in frames)
        {
            long before = GC.GetTotalMemory(true);
            var sum = Vector3.Zero;
            for (int i = 0; i < reads; i++)
            {
                sum += frame.GetStateOrientationToICRF(start.AddSeconds(0.5 * i)).AngularVelocity;
            }

            long retained = GC.GetTotalMemory(true) - before;
            GC.KeepAlive(frame);
            Console.WriteLine(
                $"MEASURE #541 {label}: {retained.ToString(CultureInfo.InvariantCulture)} bytes retained after {reads.ToString(CultureInfo.InvariantCulture)} reads ({sum.Magnitude().ToString("E3", CultureInfo.InvariantCulture)})");
            if (retained > RetainedBound)
            {
                Console.Error.WriteLine($"Failed: {label} retained {retained} bytes after {reads} reads (bound {RetainedBound})");
                exitCode = 1;
            }
        }

        GC.KeepAlive(frames);
        GC.KeepAlive(site);
        return exitCode;
    }
}
