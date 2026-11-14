// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The solar system kernels (<see cref="Constants.SolarSystemKernelPath"/>) loaded once per test process, and again only
/// if a test removed them: the call of every test class constructor and class fixture that needs them.
/// </summary>
/// <remarks>
/// <para>
/// xUnit builds one instance of a test class per test, and <see cref="SpiceAPI.LoadKernels"/> of a path already loaded
/// unloads it and loads it again, under the SPICE lock. The unload of a text kernel makes CSPICE re-read every text kernel
/// still loaded, so the load of <c>Data/SolarSystem</c> in each constructor cost 0.3 to 0.9 s per test, holding the lock
/// that every other test class waits for: about 2,050 s of the Linux CI run, 63 % of its wall time (run 43191227552).
/// The per-test reload restored nothing: no kernel the tests load covers an object, assigns a variable, or names a body or
/// a frame that <c>Data/SolarSystem</c> defines (spacecraft NAIF ids are negative; a site SPK or frame of a DSN station id
/// 13 to 65 must not be loaded; <see cref="KernelShadowing"/> checks it), and nothing in this project unloads it.
/// </para>
/// <para>
/// Rules for the tests of this project, which share one SPICE pool:
/// a test that needs a fresh pool, fresh type initializers or a pool without <c>Data/SolarSystem</c> runs in a child
/// process, as <see cref="ColdStartTests"/>: no test of this process calls <see cref="SpiceAPI.ClearKernels"/>, nor
/// unloads <c>Data/SolarSystem</c>, a file inside it, a parent of it or its anchor, which other classes read in parallel
/// (a sanctioned exception would have to be declared to the guard, not the guard weakened); a test of the reload
/// branches of <see cref="SpiceAPI.LoadKernels"/> uses its own folder (as <see cref="SpiceApiTest"/> with <c>scn100</c>),
/// never <c>Data/SolarSystem</c>, a file inside it or a parent of it.
/// <see cref="KernelReloadGuardTests"/> fails when a test reloaded the folder, removed it or its anchor, or loaded it
/// before the first call of <see cref="EnsureSolarSystem"/>, and when a kernel still loaded at its end shadows the folder
/// (<see cref="KernelShadowing"/>).
/// </para>
/// </remarks>
internal static class TestKernels
{
    /// <summary>The solar system kernels and their anchor, an empty directory of the output directory.</summary>
    internal static readonly SharedKernels SolarSystem = new(Constants.SolarSystemKernelPath,
        new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "KernelReloadAnchor")));

    /// <summary>Loads <c>Data/SolarSystem</c> if it is not loaded, where a constructor or a fixture loaded it.</summary>
    internal static void EnsureSolarSystem() => SolarSystem.Ensure();
}

/// <summary>
/// A folder of kernels the tests share, loaded once per process by <see cref="Ensure"/>, and again only if it is no longer
/// in <see cref="SpiceAPI.GetLoadedKernels"/>.
/// </summary>
/// <remarks>
/// The check and the load are one critical section under <see cref="SpiceAPI.SyncRoot"/>: no SPICE call of another thread
/// comes between them. A failed load records nothing, so the next call tries again (a <see cref="Lazy{T}"/> would cache the
/// exception, or load twice, and would never load again after a removal). Each load of the folder is followed by the load
/// of the anchor, an empty directory, which <see cref="SpiceAPI.LoadKernels"/> records without a native call: a reload of
/// the folder by any other caller moves its entry after the anchor's, which <see cref="ReloadObserved"/> keeps. A call that
/// finds the folder or its anchor gone after a call found both in place (an unload of the folder, of a parent of it or of
/// its anchor, a <see cref="SpiceAPI.ClearKernels"/>, whether another caller loaded the folder again or not) sets
/// <see cref="RemovalObserved"/>; a first load of the folder by another caller leaves <see cref="Loads"/> at 0. Nothing
/// under the lock reads a static class whose type initializer takes it (#623).
/// </remarks>
internal sealed class SharedKernels
{
    private int _loads;
    private bool _inPlace;
    private bool _reloadObserved;
    private bool _removalObserved;

    /// <param name="path">The folder of kernels.</param>
    /// <param name="anchor">An empty directory, created if absent, whose name contains no loaded path and is contained in none.</param>
    internal SharedKernels(DirectoryInfo path, DirectoryInfo anchor)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
    }

    internal DirectoryInfo Path { get; }

    internal DirectoryInfo Anchor { get; }

    /// <summary>The loads of the folder this instance performed.</summary>
    internal int Loads
    {
        get
        {
            lock (SpiceAPI.SyncRoot)
            {
                return _loads;
            }
        }
    }

    /// <summary>True once a call found the folder after its anchor: another caller loaded it again.</summary>
    internal bool ReloadObserved
    {
        get
        {
            lock (SpiceAPI.SyncRoot)
            {
                return _reloadObserved;
            }
        }
    }

    /// <summary>True once a call found the folder or its anchor gone after a call found both in place: another caller removed it.</summary>
    internal bool RemovalObserved
    {
        get
        {
            lock (SpiceAPI.SyncRoot)
            {
                return _removalObserved;
            }
        }
    }

    /// <summary>
    /// Loads the folder if it is not loaded, then its anchor; otherwise records whether it was reloaded. Records whether
    /// the folder or its anchor was removed since the last call.
    /// </summary>
    internal void Ensure()
    {
        Anchor.Create();
        lock (SpiceAPI.SyncRoot)
        {
            var kernels = SpiceAPI.Instance.GetLoadedKernels().ToArray();
            int path = Array.FindIndex(kernels, k => k.FullName == Path.FullName);
            int anchor = Array.FindIndex(kernels, k => k.FullName == Anchor.FullName);
            if (path >= 0 && anchor >= 0)
            {
                _reloadObserved |= path > anchor;
                _inPlace = true;
                return;
            }

            _removalObserved |= _inPlace;
            if (path < 0)
            {
                // LoadKernels ignores a path that does not exist
                if (!Directory.Exists(Path.FullName))
                {
                    throw new DirectoryNotFoundException($"The kernels {Path.FullName} do not exist.");
                }

                SpiceAPI.Instance.LoadKernels(Path);
                _loads++;
            }

            // Added, or moved after the folder
            SpiceAPI.Instance.LoadKernels(Anchor);
            _inPlace = true;
        }
    }
}
