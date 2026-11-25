// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The kernels of one instrument that <see cref="Spacecraft.WriteKernels"/> wrote: its frame kernel
/// (<see cref="Instrument.WriteFrame"/>) and its instrument kernel (<see cref="Instrument.WriteKernel"/>).
/// </summary>
/// <param name="Instrument">The instrument.</param>
/// <param name="Frame">Its frame kernel, <c>&lt;spacecraft&gt;_&lt;instrument&gt;.tf</c>.</param>
/// <param name="Kernel">Its instrument kernel, <c>&lt;spacecraft&gt;_&lt;instrument&gt;.ti</c>.</param>
public sealed record InstrumentKernelFiles(Instrument Instrument, FileInfo Frame, FileInfo Kernel);

/// <summary>
/// The kernels of a spacecraft that <see cref="Spacecraft.WriteKernels"/> wrote into one directory and loaded, each file
/// by its own path: <see cref="Dispose"/> unloads exactly what the call loaded, and the files stay on disk (R14 and
/// decisions 79 and 80 of #668, decided by S. Guillet, 2026-11-23).
/// </summary>
/// <remarks>
/// <para>
/// The handle unloads the records of its own load, by reference: a file a user's load of the directory, or of a directory
/// above it, took into its entry of <see cref="SpiceAPI.GetLoadedKernels"/> while the handle lived is unloaded at
/// <see cref="Dispose"/> and that entry replaced by its other files, as <see cref="SpiceAPI.UnloadKernels(FileSystemInfo)"/>
/// of a file inside it does: a load made while the handle lives furnishes nothing for its files and does not take them
/// over; load them again after <see cref="Dispose"/> to keep them. A file loaded again under the same name after the
/// handle's record left SPICE (a user's unload, a writer that replaced it) is not the handle's and stays.
/// </para>
/// <para>
/// No finalizer: an undisposed handle leaves its kernels loaded for the life of the process, as
/// <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/> does, and never takes the SPICE lock from the finalizer thread.
/// </para>
/// </remarks>
public sealed class SpacecraftKernels : IDisposable
{
    private readonly IReadOnlyList<SpiceAPI.KernelFile> _loaded;
    private int _disposed;

    internal SpacecraftKernels(FileInfo clock, FileInfo frame, FileInfo ephemeris, FileInfo orientation,
        IReadOnlyList<InstrumentKernelFiles> instruments, IReadOnlyList<SpiceAPI.KernelFile> loaded)
    {
        Clock = clock;
        Frame = frame;
        Ephemeris = ephemeris;
        Orientation = orientation;
        Instruments = instruments;
        _loaded = loaded;
    }

    /// <summary>The clock kernel (SCLK), <c>&lt;spacecraft&gt;.tsc</c> (<see cref="IO.Astrodynamics.Body.Spacecraft.Clock.Write"/>).</summary>
    public FileInfo Clock { get; }

    /// <summary>The frame kernel of the spacecraft frame, <c>&lt;spacecraft&gt;.tf</c> (<see cref="SpacecraftFrame.Write"/>).</summary>
    public FileInfo Frame { get; }

    /// <summary>The SPK of the stored states, <c>&lt;spacecraft&gt;.bsp</c> (<see cref="Spacecraft.WriteEphemeris"/>).</summary>
    public FileInfo Ephemeris { get; }

    /// <summary>The CK of the attitude record, <c>&lt;spacecraft&gt;.bc</c> (<see cref="Spacecraft.WriteOrientation"/>).</summary>
    public FileInfo Orientation { get; }

    /// <summary>The frame and instrument kernels of each instrument, in the order of <see cref="Spacecraft.Instruments"/>.</summary>
    public IReadOnlyList<InstrumentKernelFiles> Instruments { get; }

    /// <summary>
    /// The files in the order <see cref="Spacecraft.WriteKernels"/> loads them: the clock, the frame, each instrument's
    /// frame and instrument kernels, the SPK and the CK.
    /// </summary>
    internal IEnumerable<FileInfo> Files => new[] { Clock, Frame }.Concat(Instruments.SelectMany(x => new[] { x.Frame, x.Kernel }))
        .Concat(new[] { Ephemeris, Orientation });

    /// <summary>
    /// True until <see cref="Dispose"/>: the handle has not unloaded its files. A user's <see cref="SpiceAPI.UnloadKernels(FileSystemInfo)"/>
    /// or <see cref="SpiceAPI.ClearKernels"/> of them is not tracked.
    /// </summary>
    public bool IsLoaded => Volatile.Read(ref _disposed) == 0;

    /// <summary>
    /// Unloads the files the call loaded, those SPICE still holds from it, in one pass, and leaves them on disk. A second
    /// call does nothing.
    /// </summary>
    /// <exception cref="IO.Astrodynamics.Exceptions.SpiceException">SPICE failed to unload a file and no later unload of the call repaired it (a text
    /// kernel of the pool deleted since its load); every file of the handle is unloaded before the exception, and the
    /// handle is disposed.</exception>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        SpiceAPI.Instance.UnloadKernelFilesScoped(_loaded);
    }
}
