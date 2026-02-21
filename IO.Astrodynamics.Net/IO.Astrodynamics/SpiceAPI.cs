// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using IO.Astrodynamics.Converters;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using CelestialBody = IO.Astrodynamics.DTO.CelestialBody;
using EquinoctialElements = IO.Astrodynamics.DTO.EquinoctialElements;
using Instrument = IO.Astrodynamics.Body.Spacecraft.Instrument;
using KeplerianElements = IO.Astrodynamics.DTO.KeplerianElements;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateOrientation = IO.Astrodynamics.DTO.StateOrientation;
using StateVector = IO.Astrodynamics.DTO.StateVector;
using Window = IO.Astrodynamics.DTO.Window;

namespace IO.Astrodynamics;

/// <summary>
///     API to communicate with IO.Astrodynamics
/// </summary>
public class SpiceAPI
{
    private static IntPtr _libHandle = IntPtr.Zero;
    private readonly List<FileSystemInfo> _kernels = [];

    /// <summary>
    ///     Instantiate API
    /// </summary>
    private SpiceAPI()
    {
        NativeLibrary.SetDllImportResolver(typeof(SpiceAPI).Assembly, Resolver);
    }

    public static SpiceAPI Instance { get; } = new();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetSpiceVersionProxy();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool LoadKernelsProxy(string directoryPath);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool UnloadKernelsProxy(string directoryPath);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnDistanceConstraintProxy(Window searchWindow, int observerId,
        int targetId, string constraint, double value, string aberration, double stepSize, [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnOccultationConstraintProxy(Window searchWindow, int observerId,
        int targetId,
        string targetFrame, string targetShape, int frontBodyId, string frontFrame, string frontShape,
        string occultationType,
        string aberration, double stepSize, [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnCoordinateConstraintProxy(Window searchWindow, int observerId, int targetId,
        string frame, string coordinateSystem, string coordinate,
        string relationalOperator, double value, double adjustValue, string aberration, double stepSize,
        [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnIlluminationConstraintProxy(Window searchWindow, int observerId,
        string illuminationSource, int targetBody, string fixedFrame,
        Planetodetic geodetic, string illuminationType, string relationalOperator, double value, double adjustValue,
        string aberration, double stepSize, string method, [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsInFieldOfViewConstraintProxy(Window searchWindow, int observerId,
        int instrumentId, int targetId, string targetFrame, string targetShape, string aberration, double stepSize,
        [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void ReadOrientationProxy(Window searchWindow, int spacecraftId, double tolerance,
        string frame, double stepSize, [In] [Out] StateOrientation[] stateOrientations);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void ReadEphemerisProxy(Window searchWindow, int observerId, int targetId,
        string frame, string aberration, double stepSize, [In] [Out] StateVector[] stateVectors);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool
        WriteEphemerisProxy(string filePath, int objectId, StateVector[] stateVectors, uint size);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool
        WriteOrientationProxy(string filePath, int objectId, StateOrientation[] stateOrientations, uint size);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern CelestialBody GetCelestialBodyInfoProxy(int celestialBodyId);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FrameTransformation TransformFrameProxy(string fromFrame, string toFrame, double epoch);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ReadEphemerisAtGivenEpochProxy(double epoch, int observerId, int targetId,
        string frame, string aberration);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ConvertTLEToStateVectorProxy(string line1, string line2, string line3,
        double epoch);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern TLEElements GetTLEElementsProxy(string line1, string line2, string line3);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void KClearProxy();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern DTO.KeplerianElements ConvertStateVectorToConicOrbitalElementProxy(DTO.StateVector stateVector, double mu);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern DTO.StateVector ConvertEquinoctialElementsToStateVectorProxy(DTO.EquinoctialElements equinoctialElementsDto);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector Propagate2BodiesProxy(StateVector stateVector, double mu, double dt);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ConvertConicElementsToStateVectorProxy(KeplerianElements keplerianElements);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ConvertConicElementsToStateVectorAtEpochProxy(KeplerianElements keplerianElements, double epoch, double gm);

    private static IntPtr Resolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        _libHandle = IntPtr.Zero;

        if (libraryName != "IO.Astrodynamics") return _libHandle;
        string sharedLibName = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            sharedLibName = "resources/IO.Astrodynamics.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) sharedLibName = "resources/libIO.Astrodynamics.so";

        if (!string.IsNullOrEmpty(sharedLibName))
            NativeLibrary.TryLoad(sharedLibName, typeof(SpiceAPI).Assembly, DllImportSearchPath.AssemblyDirectory, out _libHandle);
        else
            throw new PlatformNotSupportedException();

        return _libHandle;
    }

    //Use the same lock for all cspice calls because it doesn't support multithreading.
    private static object lockObject = new object();

    /// <summary>
    ///     Get spice toolkit version number
    /// </summary>
    /// <returns></returns>
    public string GetSpiceVersion()
    {
        lock (lockObject)
        {
            var strptr = GetSpiceVersionProxy();
            var str = Marshal.PtrToStringAnsi(strptr);
            Marshal.FreeHGlobal(strptr);
            return str;
        }
    }

    public IEnumerable<FileSystemInfo> GetLoadedKernels()
    {
        return _kernels;
    }

    /// <summary>
    ///     Load kernel at given path
    /// </summary>
    /// <param name="path">Path where kernels are located. This could be a file path or a directory path</param>
    public void LoadKernels(FileSystemInfo path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        lock (lockObject)
        {
            if (_kernels.Any(x => path.FullName.Contains(x.FullName)))
            {
                foreach (var kernel in _kernels.Where(x => path.FullName.Contains(x.FullName)).ToArray())
                {
                    UnloadKernels(kernel);
                    LoadKernels(kernel);
                }

                return;
            }


            var existingKernels = _kernels.Where(x => x.FullName.Contains(path.FullName)).ToArray();
            foreach (var existingKernel in existingKernels)
            {
                UnloadKernels(existingKernel);
            }

            if (path.Exists)
            {
                if (!LoadKernelsProxy(path.FullName))
                {
                    throw new InvalidOperationException($"Kernel {path.FullName} can't be loaded. You can have more details on standard output");
                }

                _kernels.Add(path);
            }
        }
    }

    /// <summary>
    ///     Unload kernel at given path
    /// </summary>
    /// <param name="path">Path where kernels are located. This could be a file path or a directory path</param>
    public void UnloadKernels(FileSystemInfo path)
    {
        if (path == null) return;
        lock (lockObject)
        {
            if (path.Exists)
            {
                if (!UnloadKernelsProxy(path.FullName))
                {
                    throw new InvalidOperationException($"Kernel {path.FullName} can't be unloaded. You can have more details on standard output");
                }

                _kernels.RemoveAll(x => x.FullName == path.FullName);
                foreach (var kernel in _kernels.Where(x => x.FullName.Contains(path.FullName)).ToArray())
                {
                    UnloadKernels(kernel);
                }
            }
        }
    }

    public void ClearKernels()
    {
        lock (lockObject)
        {
            foreach (var kernel in _kernels.ToArray())
            {
                UnloadKernels(kernel);
            }

            KClearProxy();
        }
    }

    /// <summary>
    ///     Find time windows based on distance constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observer"></param>
    /// <param name="target"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public IEnumerable<TimeSystem.Window> FindWindowsOnDistanceConstraint(TimeSystem.Window searchWindow, INaifObject observer,
        INaifObject target, RelationnalOperator relationalOperator, double value, Aberration aberration,
        TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (target == null) throw new ArgumentNullException(nameof(target));
        return FindWindowsOnDistanceConstraint(searchWindow, observer.NaifId, target.NaifId, relationalOperator, value, aberration, stepSize);
    }

    public IEnumerable<TimeSystem.Window> FindWindowsOnDistanceConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, RelationnalOperator relationalOperator, double value, Aberration aberration,
        TimeSpan stepSize)
    {
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnDistanceConstraintProxy(searchWindow.Convert(), observerId, targetId, relationalOperator.GetDescription(), value,
                aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
    }

    /// <summary>
    ///     Find time windows based on occultation constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="target"></param>
    /// <param name="targetShape"></param>
    /// <param name="frontBody"></param>
    /// <param name="frontShape"></param>
    /// <param name="occultationType"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <param name="observer"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public IEnumerable<TimeSystem.Window> FindWindowsOnOccultationConstraint(TimeSystem.Window searchWindow, INaifObject observer,
        INaifObject target, ShapeType targetShape, INaifObject frontBody, ShapeType frontShape,
        OccultationType occultationType, Aberration aberration, TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (frontBody == null) throw new ArgumentNullException(nameof(frontBody));
        return FindWindowsOnOccultationConstraint(searchWindow, observer.NaifId, target.NaifId, targetShape,
            frontBody.NaifId, frontShape, occultationType, aberration, stepSize);
    }

    /// <summary>
    /// Find time windows based on occultation constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="targetShape"></param>
    /// <param name="frontBodyId"></param>
    /// <param name="frontShape"></param>
    /// <param name="occultationType"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public IEnumerable<TimeSystem.Window> FindWindowsOnOccultationConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, ShapeType targetShape, int frontBodyId, ShapeType frontShape,
        OccultationType occultationType, Aberration aberration, TimeSpan stepSize)
    {
        lock (lockObject)
        {
            string frontFrame = frontShape == ShapeType.Ellipsoid
                ? GetCelestialBodyInfoProxy(frontBodyId).FrameName
                : string.Empty;
            string targetFrame = targetShape == ShapeType.Ellipsoid
                ? GetCelestialBodyInfoProxy(targetId).FrameName
                : string.Empty;
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnOccultationConstraintProxy(searchWindow.Convert(), observerId, targetId, targetFrame, targetShape.GetDescription(),
                frontBodyId, frontFrame, frontShape.GetDescription(), occultationType.GetDescription(), aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
    }

    /// <summary>
    ///     Find time windows based on coordinate constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="target"></param>
    /// <param name="frame"></param>
    /// <param name="coordinateSystem"></param>
    /// <param name="coordinate"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value"></param>
    /// <param name="adjustValue"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <param name="observer"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public IEnumerable<TimeSystem.Window> FindWindowsOnCoordinateConstraint(TimeSystem.Window searchWindow, INaifObject observer,
        INaifObject target, Frame frame, CoordinateSystem coordinateSystem, Coordinate coordinate,
        RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        return FindWindowsOnCoordinateConstraint(searchWindow, observer.NaifId, target.NaifId, frame,
            coordinateSystem, coordinate, relationalOperator, value, adjustValue, aberration, stepSize);
    }

    /// <summary>
    /// Find time windows based on coordinate constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="frame"></param>
    /// <param name="coordinateSystem"></param>
    /// <param name="coordinate"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value"></param>
    /// <param name="adjustValue"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TimeSystem.Window> FindWindowsOnCoordinateConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, Frame frame, CoordinateSystem coordinateSystem, Coordinate coordinate,
        RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        TimeSpan stepSize)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnCoordinateConstraintProxy(searchWindow.Convert(), observerId, targetId,
                frame.Name, coordinateSystem.GetDescription(),
                coordinate.GetDescription(), relationalOperator.GetDescription(), value, adjustValue,
                aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
    }

    /// <summary>
    ///     Find time windows based on illumination constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="illuminationSource"></param>
    /// <param name="observer"></param>
    /// <param name="targetBody"></param>
    /// <param name="fixedFrame"></param>
    /// <param name="planetodetic"></param>
    /// <param name="illuminationType"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value"></param>
    /// <param name="adjustValue"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public IEnumerable<TimeSystem.Window> FindWindowsOnIlluminationConstraint(TimeSystem.Window searchWindow, INaifObject observer,
        INaifObject targetBody, Frame fixedFrame,
        Coordinates.Planetodetic planetodetic, IlluminationAngle illuminationType, RelationnalOperator relationalOperator,
        double value, double adjustValue, Aberration aberration, TimeSpan stepSize, INaifObject illuminationSource,
        string method = "Ellipsoid")
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (targetBody == null) throw new ArgumentNullException(nameof(targetBody));
        if (illuminationSource == null) throw new ArgumentNullException(nameof(illuminationSource));
        return FindWindowsOnIlluminationConstraint(searchWindow, observer.NaifId, targetBody.NaifId, fixedFrame,
            planetodetic, illuminationType, relationalOperator, value, adjustValue, aberration, stepSize,
            illuminationSource.NaifId, method);
    }

    public IEnumerable<TimeSystem.Window> FindWindowsOnIlluminationConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetBodyId, Frame fixedFrame, Coordinates.Planetodetic planetodetic, IlluminationAngle illuminationType, RelationnalOperator relationalOperator,
        double value, double adjustValue, Aberration aberration, TimeSpan stepSize, int illuminationSourceId,
        string method = "Ellipsoid")
    {
        if (fixedFrame == null) throw new ArgumentNullException(nameof(fixedFrame));
        if (method == null) throw new ArgumentNullException(nameof(method));
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnIlluminationConstraintProxy(searchWindow.Convert(), observerId,
                illuminationSourceId.ToString(), targetBodyId, fixedFrame.Name,
                planetodetic.Convert(),
                illuminationType.GetDescription(), relationalOperator.GetDescription(), value, adjustValue,
                aberration.GetDescription(), stepSize.TotalSeconds, method, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
    }

    /// <summary>
    ///     Find time window when a target is in instrument's field of view
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observer"></param>
    /// <param name="instrument"></param>
    /// <param name="target"></param>
    /// <param name="targetFrame"></param>
    /// <param name="targetShape"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public IEnumerable<TimeSystem.Window> FindWindowsInFieldOfViewConstraint(TimeSystem.Window searchWindow, Spacecraft observer,
        Instrument instrument, INaifObject target, Frame targetFrame, ShapeType targetShape, Aberration aberration,
        TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (instrument == null) throw new ArgumentNullException(nameof(instrument));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));
        return FindWindowsInFieldOfViewConstraint(searchWindow, observer.NaifId, instrument.NaifId, target.NaifId, targetFrame, targetShape, aberration, stepSize);
    }

    /// <summary>
    /// Find time window when a target is in instrument's field of view
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="instrumentId"></param>
    /// <param name="targetId"></param>
    /// <param name="targetFrame"></param>
    /// <param name="targetShape"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TimeSystem.Window> FindWindowsInFieldOfViewConstraint(TimeSystem.Window searchWindow, int observerId,
        int instrumentId, int targetId, Frame targetFrame, ShapeType targetShape, Aberration aberration, TimeSpan stepSize)
    {
        if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            var searchWindowDto = searchWindow.Convert();

            FindWindowsInFieldOfViewConstraintProxy(searchWindowDto, observerId, instrumentId, targetId, targetFrame.Name, targetShape.GetDescription(),
                aberration.GetDescription(), stepSize.TotalSeconds, windows);

            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
    }

    /// <summary>
    ///     Read object ephemeris for a given period
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="target"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <param name="observer"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public IEnumerable<OrbitalParameters.OrbitalParameters> ReadEphemeris(TimeSystem.Window searchWindow,
        ILocalizable observer, ILocalizable target, Frame frame,
        Aberration aberration, TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frame);
        return ReadEphemeris(searchWindow, observer.NaifId, target.NaifId, frame, aberration, stepSize);
    }

    /// <summary>
    /// Read object ephemeris for a given period using NAIF IDs directly.
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public IEnumerable<OrbitalParameters.OrbitalParameters> ReadEphemeris(TimeSystem.Window searchWindow,
        int observerId, int targetId, Frame frame,
        Aberration aberration, TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (lockObject)
        {
            var observer = CelestialItem.Create(observerId);
            const int messageSize = 10000;
            List<OrbitalParameters.OrbitalParameters> orbitalParameters = [];
            int occurrences = (int)(searchWindow.Length / stepSize / messageSize);

            for (int i = 0; i <= occurrences; i++)
            {
                var start = searchWindow.StartDate + i * messageSize * stepSize;
                var end = start + messageSize * stepSize > searchWindow.EndDate ? searchWindow.EndDate : (start + messageSize * stepSize) - stepSize;
                var window = new TimeSystem.Window(start.ToTDB(), end.ToTDB());
                var stateVectors = new StateVector[messageSize];
                ReadEphemerisProxy(window.Convert(), observerId, targetId, frame.Name,
                    aberration.GetDescription(), stepSize.TotalSeconds,
                    stateVectors);
                orbitalParameters.AddRange(stateVectors.Where(x => !string.IsNullOrEmpty(x.Frame)).Select(x =>
                    new OrbitalParameters.StateVector(x.Position.Convert(), x.Velocity.Convert(), observer, Time.CreateTDB(x.Epoch).ConvertTo(searchWindow.StartDate.Frame), frame)));
            }

            return orbitalParameters;
        }
    }

    /// <summary>
    /// Return state vector at given epoch
    /// </summary>
    /// <param name="epoch"></param>
    /// <param name="observer"></param>
    /// <param name="target"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("Use the overload that accepts int IDs instead.")]
    public OrbitalParameters.OrbitalParameters ReadEphemeris(Time epoch, ILocalizable observer,
        ILocalizable target, Frame frame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frame);
        return ReadEphemeris(epoch, observer.NaifId, target.NaifId, frame, aberration);
    }

    /// <summary>
    /// Return state vector at given epoch using NAIF IDs directly.
    /// </summary>
    /// <param name="epoch"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    public OrbitalParameters.OrbitalParameters ReadEphemeris(Time epoch, int observerId,
        int targetId, Frame frame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (lockObject)
        {
            var observer = CelestialItem.Create(observerId);
            var stateVector = ReadEphemerisAtGivenEpochProxy(epoch.TimeSpanFromJ2000().TotalSeconds, observerId,
                targetId, frame.Name, aberration.GetDescription());
            return new OrbitalParameters.StateVector(stateVector.Position.Convert(), stateVector.Velocity.Convert(), observer,
                Time.Create(stateVector.Epoch, TimeFrame.TDBFrame), frame);
        }
    }

    /// <summary>
    ///     Read spacecraft orientation for a given period
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="spacecraft"></param>
    /// <param name="tolerance"></param>
    /// <param name="referenceFrame"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int naifId instead.")]
    public IEnumerable<OrbitalParameters.StateOrientation> ReadOrientation(TimeSystem.Window searchWindow,
        Spacecraft spacecraft, TimeSpan tolerance,
        Frame referenceFrame, TimeSpan stepSize)
    {
        if (spacecraft == null) throw new ArgumentNullException(nameof(spacecraft));
        return ReadOrientation(searchWindow, spacecraft.NaifId, tolerance, referenceFrame, stepSize);
    }

    /// <summary>
    ///     Read spacecraft orientation for a given period using a NAIF ID directly.
    ///     Use this overload to read CK kernels from external missions (NASA/ESA) where
    ///     you have only the NAIF ID, not a Spacecraft object.
    ///     Both the CK kernel and the corresponding SCLK kernel must be loaded via
    ///     LoadKernels() before calling this method.
    /// </summary>
    /// <param name="searchWindow">Time window to query</param>
    /// <param name="naifId">NAIF ID of the spacecraft (the CK bus frame ID is derived as naifId * 1000)</param>
    /// <param name="tolerance">Search tolerance — data within this duration of each step epoch will be returned</param>
    /// <param name="referenceFrame">Reference frame for the returned orientations</param>
    /// <param name="stepSize">Time step between successive query epochs</param>
    public IEnumerable<OrbitalParameters.StateOrientation> ReadOrientation(TimeSystem.Window searchWindow,
        int naifId, TimeSpan tolerance,
        Frame referenceFrame, TimeSpan stepSize)
    {
        if (referenceFrame == null) throw new ArgumentNullException(nameof(referenceFrame));
        lock (lockObject)
        {
            var stateOrientations = new StateOrientation[10000];
            ReadOrientationProxy(searchWindow.Convert(), naifId, tolerance.TotalSeconds,
                referenceFrame.Name, stepSize.TotalSeconds,
                stateOrientations);
            // ByValTStr marshaling returns "" (not null) for zero-padded unwritten slots;
            // use IsNullOrEmpty to correctly exclude entries the proxy did not populate.
            return stateOrientations.Where(x => !string.IsNullOrEmpty(x.Frame)).Select(x => new OrbitalParameters.StateOrientation(
                x.Rotation.Convert(), x.AngularVelocity.Convert(), Time.Create(x.Epoch, TimeFrame.TDBFrame), referenceFrame));
        }
    }

    /// <summary>
    ///     Write ephemeris file
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="naifObject"></param>
    /// <param name="stateVectors"></param>
    /// <returns></returns>
    [Obsolete("Use the overload that accepts int naifId instead.")]
    public bool WriteEphemeris(FileInfo filePath, INaifObject naifObject,
        IEnumerable<OrbitalParameters.StateVector> stateVectors)
    {
        if (naifObject == null) throw new ArgumentNullException(nameof(naifObject));
        return WriteEphemeris(filePath, naifObject.NaifId, stateVectors);
    }

    /// <summary>
    /// Write ephemeris file using a NAIF ID directly.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="naifId"></param>
    /// <param name="stateVectors"></param>
    /// <returns></returns>
    public bool WriteEphemeris(FileInfo filePath, int naifId,
        IEnumerable<OrbitalParameters.StateVector> stateVectors)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateVectors == null) throw new ArgumentNullException(nameof(stateVectors));
        lock (lockObject)
        {
            var enumerable = stateVectors as OrbitalParameters.StateVector[] ?? stateVectors.ToArray();
            if (!enumerable.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(stateVectors));
            bool res = WriteEphemerisProxy(filePath.FullName, naifId, stateVectors.Select(x => x.Convert()).ToArray(),
                (uint)enumerable.Length);
            if (res == false)
            {
                throw new InvalidOperationException(
                    "An error occurred while writing ephemeris. You can have more details on standard output.");
            }

            return true;
        }
    }

    [Obsolete("Use the overload that accepts int naifId instead.")]
    public bool WriteOrientation(FileInfo filePath, INaifObject naifObject, IEnumerable<OrbitalParameters.StateOrientation> stateOrientations)
    {
        if (naifObject == null) throw new ArgumentNullException(nameof(naifObject));
        return WriteOrientation(filePath, naifObject.NaifId, stateOrientations);
    }

    /// <summary>
    /// Write orientation file using a NAIF ID directly.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="naifId"></param>
    /// <param name="stateOrientations"></param>
    /// <returns></returns>
    public bool WriteOrientation(FileInfo filePath, int naifId, IEnumerable<OrbitalParameters.StateOrientation> stateOrientations)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateOrientations == null) throw new ArgumentNullException(nameof(stateOrientations));
        lock (lockObject)
        {
            var enumerable = stateOrientations as OrbitalParameters.StateOrientation[] ?? stateOrientations.ToArray();
            if (!enumerable.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(stateOrientations));
            bool res = WriteOrientationProxy(filePath.FullName, naifId, stateOrientations.Select(x => x.Convert()).ToArray(), (uint)enumerable.Length);
            if (res == false)
            {
                throw new InvalidOperationException(
                    "An error occurred while writing orientation. You can have more details on standard output.");
            }

            return true;
        }
    }

    /// <summary>
    ///     Get celestial celestialItem information like radius, GM, name, associated frame, ...
    /// </summary>
    /// <param name="naifId"></param>
    /// <returns></returns>
    public CelestialBody GetCelestialBodyInfo(int naifId)
    {
        lock (lockObject)
        {
            return GetCelestialBodyInfoProxy(naifId);
        }
    }

    /// <summary>
    /// Transform a frame to another
    /// </summary>
    /// <param name="fromFrame"></param>
    /// <param name="toFrame"></param>
    /// <param name="epoch"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public OrbitalParameters.StateOrientation TransformFrame(Time epoch, Frame fromFrame, Frame toFrame)
    {
        lock (lockObject)
        {
            if (fromFrame == null) throw new ArgumentNullException(nameof(fromFrame));
            if (toFrame == null) throw new ArgumentNullException(nameof(toFrame));
            var res = TransformFrameProxy(fromFrame.Name, toFrame.Name, epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds);

            return new OrbitalParameters.StateOrientation(
                new Quaternion(res.Rotation.W, res.Rotation.X, res.Rotation.Y, res.Rotation.Z),
                new Vector3(res.AngularVelocity.X, res.AngularVelocity.Y, res.AngularVelocity.Z), epoch, fromFrame);
        }
    }

    public IEnumerable<OrbitalParameters.StateOrientation> TransformFrame(TimeSystem.Window window, Frame fromFrame, Frame toFrame, TimeSpan stepSize)
    {
        for (Time i = window.StartDate; i < window.EndDate; i += stepSize)
        {
            yield return TransformFrame(i, fromFrame, toFrame);
        }
    }

    /// <summary>
    /// Convert TLE to state vector at given epoch
    /// </summary>
    /// <param name="line1"></param>
    /// <param name="line2"></param>
    /// <param name="line3"></param>
    /// <param name="epoch"></param>
    /// <returns></returns>
    public OrbitalParameters.OrbitalParameters ConvertTleToStateVector(string line1, string line2, string line3,
        Time epoch)
    {
        lock (lockObject)
        {
            var res = ConvertTLEToStateVectorProxy(line1, line2, line3, epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds);
            return new OrbitalParameters.StateVector(res.Position.Convert(), res.Velocity.Convert(),
                new Body.CelestialBody(PlanetsAndMoons.EARTH, Frame.ECLIPTIC_J2000, epoch), epoch,
                new Frame(res.Frame));
        }
    }

    public IO.Astrodynamics.OrbitalParameters.KeplerianElements ConvertStateVectorToConicOrbitalElement(IO.Astrodynamics.OrbitalParameters.StateVector stateVector)
    {
        return ConvertStateVectorToConicOrbitalElement(stateVector, stateVector.Observer.GM);
    }

    /// <summary>
    /// Convert state vector to Keplerian elements with an explicit GM value.
    /// </summary>
    /// <param name="stateVector"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <returns></returns>
    public IO.Astrodynamics.OrbitalParameters.KeplerianElements ConvertStateVectorToConicOrbitalElement(IO.Astrodynamics.OrbitalParameters.StateVector stateVector, double gm)
    {
        lock (lockObject)
        {
            var svDto = stateVector.Convert();
            return ConvertStateVectorToConicOrbitalElementProxy(svDto, gm).Convert();
        }
    }

    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertEquinoctialElementsToStateVector(IO.Astrodynamics.OrbitalParameters.EquinoctialElements equinoctialElements)
    {
        lock (lockObject)
        {
            return ConvertEquinoctialElementsToStateVectorProxy(equinoctialElements.Convert()).Convert();
        }
    }

    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements)
    {
        lock (lockObject)
        {
            return ConvertConicElementsToStateVectorProxy(keplerianElements.Convert()).Convert();
        }
    }

    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements, Time epoch)
    {
        return ConvertConicElementsToStateVector(keplerianElements, epoch, keplerianElements.Observer.GM);
    }

    /// <summary>
    /// Convert Keplerian elements to state vector at a given epoch with an explicit GM value.
    /// </summary>
    /// <param name="keplerianElements"></param>
    /// <param name="epoch"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <returns></returns>
    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements, Time epoch, double gm)
    {
        lock (lockObject)
        {
            return ConvertConicElementsToStateVectorAtEpochProxy(keplerianElements.Convert(), epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, gm)
                .Convert();
        }
    }

    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, TimeSpan dt)
    {
        return Propagate2Bodies(stateVector, stateVector.Observer.GM, dt);
    }

    /// <summary>
    /// Propagate a state vector using two-body mechanics with an explicit GM value.
    /// </summary>
    /// <param name="stateVector"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <param name="dt"></param>
    /// <returns></returns>
    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, double gm, TimeSpan dt)
    {
        lock (lockObject)
        {
            return Propagate2BodiesProxy(stateVector.Convert(), gm, dt.TotalSeconds).Convert();
        }
    }

    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, Time targetEpoch)
    {
        return Propagate2Bodies(stateVector, targetEpoch - stateVector.Epoch);
    }
}