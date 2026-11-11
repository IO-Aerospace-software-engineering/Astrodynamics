// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The child process of <c>ColdStartTests</c> (#623): in a fresh process, thread A calls a method of the library that
/// takes the SpiceAPI lock while thread B reads, for the first time in the process, a static class whose type
/// initializer reads SPICE (<see cref="PlanetsAndMoons"/> or <see cref="Barycenters"/>). Before #623, thread A held the
/// lock while it waited for that type initializer, which waited for the lock: the process hung, with no exception.
/// The process exits 0 when both threads have ended.
/// </summary>
/// <remarks>
/// <para>
/// Arguments: the kernel directory, the mode and the schedule, then, for the schedule <c>held</c>, the hold in
/// milliseconds. The modes, thread A then thread B:
/// <list type="bullet">
/// <item><c>direct</c>: <see cref="SpiceAPI.ConvertTleToStateVector(string, string, string, Time)"/>; the first read of
/// <see cref="PlanetsAndMoons.EARTH_BODY"/>.</item>
/// <item><c>tle</c>: <see cref="TLE.ToStateVector(Time)"/>, the TLE built before the threads start; the same read.</item>
/// <item><c>lagrange</c>: <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frame, Aberration)"/> from L1 (391), whose
/// observer is a <see cref="LagrangePoint"/>, which reads <see cref="Barycenters.EARTH_BARYCENTER"/>; the first read of
/// <see cref="Barycenters.EARTH_BARYCENTER"/>.</item>
/// </list>
/// </para>
/// <para>
/// The schedules: <c>race</c> starts the two threads behind a <see cref="Barrier"/>, the reproduction of #623;
/// <c>held</c> places thread B inside the type initializer before thread A starts: a data provider that delegates to
/// <see cref="SpiceDataProvider"/> holds thread B, at its first read of a body (inside the initializer, before it takes
/// the lock), for the given time, and thread A starts once thread B is held. Thread A then takes the lock while thread
/// B is inside the initializer, which a deadlock needs, whatever the timing of the machine, as long as thread A reaches
/// the static class within the hold.
/// </para>
/// <para>
/// Exit codes: 0 both threads ended; 1 a thread threw (its exception on the standard error); 2 wrong arguments; 3 with
/// the schedule <c>held</c>, thread B did not enter the type initializer within 60 s (the class was initialized before,
/// so the process does not test what it should).
/// </para>
/// </remarks>
internal static class Program
{
    private const string Name = "ISS";
    private const string Line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    private const string Line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";
    private static readonly TimeSpan EnterTimeout = TimeSpan.FromSeconds(60);

    private static int Main(string[] args)
    {
        if (args.Length < 3 || args[2] is not ("race" or "held") || (args[2] == "held") != (args.Length == 4))
        {
            return Usage();
        }

        int holdMilliseconds = 0;
        if (args[2] == "held" && (!int.TryParse(args[3], out holdMilliseconds) || holdMilliseconds < 0))
        {
            return Usage();
        }

        // Before anything reads a body, so that every body of the process reads through it
        var provider = new HoldingDataProvider(holdMilliseconds);
        if (args[2] == "held")
        {
            Configuration.Instance.SetDataProvider(provider);
        }

        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(args[0]));
        var epoch = new Time(2021, 1, 20, 18, 0, 0, frame: TimeFrame.UTCFrame);

        Action threadA;
        Action threadB;
        switch (args[1])
        {
            case "direct":
                threadA = () => SpiceAPI.Instance.ConvertTleToStateVector(Name, Line1, Line2, epoch);
                threadB = ReadEarthBody;
                break;
            case "tle":
                var tle = new TLE(Name, Line1, Line2);
                threadA = () => tle.ToStateVector(epoch);
                threadB = ReadEarthBody;
                break;
            case "lagrange":
                threadA = () => SpiceAPI.Instance.ReadEphemeris(epoch, 391, 399, Frame.ICRF, Aberration.None);
                threadB = ReadEarthBarycenter;
                break;
            default:
                return Usage();
        }

        using var barrier = new Barrier(2);
        bool held = args[2] == "held";
        int exitCode = 0;
        var elapsedA = TimeSpan.Zero;
        var elapsedB = TimeSpan.Zero;
        var total = Stopwatch.StartNew();

        var a = new Thread(() =>
        {
            if (held)
            {
                if (!provider.Entered.Wait(EnterTimeout))
                {
                    Console.Error.WriteLine("Thread B did not enter the type initializer: the class was initialized before.");
                    Interlocked.CompareExchange(ref exitCode, 3, 0);
                    return;
                }
            }
            else
            {
                barrier.SignalAndWait();
            }

            elapsedA = Run("A", threadA, ref exitCode);
        });
        var b = new Thread(() =>
        {
            if (held)
            {
                HoldingDataProvider.HoldsThisThread = true;
            }
            else
            {
                barrier.SignalAndWait();
            }

            elapsedB = Run("B", threadB, ref exitCode);
        });

        a.Start();
        b.Start();
        a.Join();
        b.Join();
        Console.WriteLine(
            $"{args[1]} {args[2]} exit {exitCode} A {elapsedA.TotalMilliseconds:F1} ms B {elapsedB.TotalMilliseconds:F1} ms total {total.Elapsed.TotalMilliseconds:F1} ms");
        return exitCode;
    }

    private static TimeSpan Run(string thread, Action action, ref int exitCode)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            action();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Thread {thread} threw: {e}");
            Interlocked.CompareExchange(ref exitCode, 1, 0);
        }

        return watch.Elapsed;
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: <kernel directory> <direct|tle|lagrange> <race|held> [hold in ms, for held]");
        return 2;
    }

    // The first reads of the static classes, each in a method of its own, compiled when thread B calls it
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReadEarthBody() => GC.KeepAlive(PlanetsAndMoons.EARTH_BODY);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReadEarthBarycenter() => GC.KeepAlive(Barycenters.EARTH_BARYCENTER);

    /// <summary>
    /// <see cref="SpiceDataProvider"/>, which holds the thread that set <see cref="HoldsThisThread"/> at its first read of
    /// a body, once, after it has set <see cref="Entered"/>.
    /// </summary>
    private sealed class HoldingDataProvider(int holdMilliseconds) : IDataProvider
    {
        private readonly IDataProvider _inner = new SpiceDataProvider();

        [ThreadStatic] internal static bool HoldsThisThread;

        internal ManualResetEventSlim Entered { get; } = new(false);

        public StateOrientation FrameTransformationToICRF(in Time date, Frame source) =>
            _inner.FrameTransformationToICRF(date, source);

        public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame,
            Aberration aberration) => _inner.GetEphemeris(date, target, observer, frame, aberration);

        public DTO.CelestialBody GetCelestialBodyInfo(int naifId)
        {
            if (HoldsThisThread && !Entered.IsSet)
            {
                Entered.Set();
                Thread.Sleep(holdMilliseconds);
            }

            return _inner.GetCelestialBodyInfo(naifId);
        }
    }
}
