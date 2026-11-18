// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The mode <c>conversions-lock</c> of the child process (#664): the element conversions and the two-body propagation of
/// <see cref="SpiceAPI"/> read nothing from the data provider under the SpiceAPI lock. A probe provider, set as the global
/// provider (which the test process never does, #631), delegates to <see cref="SpiceDataProvider"/> and counts its calls,
/// and those made while the calling thread holds the lock (<see cref="Monitor.IsEntered"/>).
/// </summary>
/// <remarks>
/// Before the fix each call built the observer of its result, <c>new CelestialBody(id)</c>, under the lock: 9 provider
/// calls about the Earth, all under the lock (#664, measured on 016237bd). Since #725 the result carries the observer of
/// the input, so no call reads the provider, under the lock or not. The counts are written to the standard output as
/// <c>MEASURE</c> lines; exit 0 when no call reads the provider under the lock, 1 otherwise.
/// </remarks>
internal static class ConversionsLockChild
{
    internal static int Run(string kernels)
    {
        var probe = new ProbeDataProvider();
        Configuration.Instance.SetDataProvider(probe);
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));

        var epoch = new Time(2021, 3, 20, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var earth = new CelestialBody(399, Frame.ICRF, epoch);
        double gm = earth.GM;
        var state = new StateVector(new IO.Astrodynamics.Math.Vector3(6800000.0, 1000.0, 2000.0),
            new IO.Astrodynamics.Math.Vector3(10.0, 7500.0, 1000.0), earth, epoch, Frame.ICRF);
        var ellipse = new KeplerianElements(7.0e6, 0.1, 0.5, 1.0, 2.0, 0.3, earth, epoch, Frame.ICRF);
        var hyperbola = new KeplerianElements(-7.0e6, 1.5, 0.5, 1.0, 2.0, 0.3, earth, epoch, Frame.ICRF);
        var equinoctial = new EquinoctialElements(7.0e6, 0.01, 0.02, 0.1, 0.2, 0.5, earth, epoch, Frame.ICRF);
        var calls = new List<(string Name, Func<object> Call)>
        {
            ("ConvertStateVectorToConicOrbitalElement(sv, gm)", () => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state, gm)),
            ("ConvertStateVectorToConicOrbitalElement(sv)", () => SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state)),
            ("ConvertEquinoctialElementsToStateVector(eq)", () => SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(equinoctial)),
            ("ConvertConicElementsToStateVector(ke)", () => SpiceAPI.Instance.ConvertConicElementsToStateVector(ellipse)),
            ("ConvertConicElementsToStateVector(hyperbola)", () => SpiceAPI.Instance.ConvertConicElementsToStateVector(hyperbola)),
            ("ConvertConicElementsToStateVector(ke, epoch)", () => SpiceAPI.Instance.ConvertConicElementsToStateVector(ellipse, epoch.AddSeconds(60.0))),
            ("ConvertConicElementsToStateVector(ke, epoch, gm)",
                () => SpiceAPI.Instance.ConvertConicElementsToStateVector(ellipse, epoch.AddSeconds(60.0), gm)),
            ("Propagate2Bodies(sv, gm, dt)", () => SpiceAPI.Instance.Propagate2Bodies(state, gm, TimeSpan.FromSeconds(600.0))),
            ("Propagate2Bodies(sv, dt)", () => SpiceAPI.Instance.Propagate2Bodies(state, TimeSpan.FromSeconds(600.0))),
            ("Propagate2Bodies(sv, epoch)", () => SpiceAPI.Instance.Propagate2Bodies(state, epoch.AddSeconds(600.0)))
        };

        int failures = 0;
        foreach (var (name, call) in calls)
        {
            probe.Reset();
            call();
            Console.WriteLine($"MEASURE #664 {name}: {probe.Calls} provider calls, {probe.CallsUnderLock} under the SpiceAPI lock");
            if (probe.CallsUnderLock != 0)
            {
                Console.Error.WriteLine($"{name}: {probe.CallsUnderLock} provider calls under the SpiceAPI lock");
                failures++;
            }
        }

        return failures == 0 ? 0 : 1;
    }

    // Delegates to SPICE and counts the calls made while the calling thread holds the SpiceAPI lock
    private sealed class ProbeDataProvider : IDataProvider
    {
        private readonly IDataProvider _inner = new SpiceDataProvider();

        // SpiceAPI.SyncRoot, internal to the library: the lock of every SPICE call
        private static readonly object Lock = typeof(SpiceAPI)
            .GetProperty("SyncRoot", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);

        internal int Calls { get; private set; }
        internal int CallsUnderLock { get; private set; }

        internal void Reset()
        {
            Calls = 0;
            CallsUnderLock = 0;
        }

        private void Count()
        {
            Calls++;
            if (Monitor.IsEntered(Lock))
            {
                CallsUnderLock++;
            }
        }

        public StateOrientation FrameTransformationToICRF(in Time date, Frame source)
        {
            Count();
            return _inner.FrameTransformationToICRF(date, source);
        }

        public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame,
            Aberration aberration)
        {
            Count();
            return _inner.GetEphemeris(date, target, observer, frame, aberration);
        }

        public DTO.CelestialBody GetCelestialBodyInfo(int naifId)
        {
            Count();
            return _inner.GetCelestialBodyInfo(naifId);
        }
    }
}
